using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceObjectLifecycleReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Dictionary<string, (string Role, string Purpose)> Targets = new(StringComparer.Ordinal)
    {
        ["0039f330"] = ("source-object-creator-wrapper", "Interface wrapper around 008b9c38 used by higher-level engine/server setup paths."),
        ["0070c300"] = ("source-object-wrapper-caller-setup-a", "Higher-level setup caller that creates a Source-side object through wrapper 0039f330 and stores it on its owning structure."),
        ["007cc0d0"] = ("source-object-wrapper-caller-setup-b", "Higher-level setup caller that creates a Source-side object through wrapper 0039f330 and immediately calls object slot +0x68."),
        ["0080e250"] = ("source-object-wrapper-caller-owner-bind-a", "Higher-level caller that passes an owner object pointer through wrapper 0039f330 before registering the object with another owner-side table."),
        ["0080ea68"] = ("source-object-wrapper-caller-owner-bind-b", "Higher-level caller that creates a Source-side object through wrapper 0039f330 and passes it into an owner-side slot +0x2c call."),
        ["008b9c38"] = ("source-player-object-create-or-reuse", "Allocates/reuses the native Source-side player/session object, inserts it into the global peer list, then associates it with the backend owner."),
        ["00a5e058"] = ("source-player-object-constructor", "Initializes the native Source-side object function table, buffers, attached socket state, owner pointer, handler vector, and tail reset block."),
        ["00a5d0c0"] = ("source-player-object-associate-owner", "Associates an allocated object with peer address data and the owner callback object, resizes bit buffers, resets state, then binds the owner back to the object."),
        ["00a5b610"] = ("source-player-object-reset-tail", "Clears the large late object field range used by gameplay/map-load state."),
        ["00a57f48"] = ("registered-handler-table-lookup", "Scans the handler vector at object +0x1e0c/+0x1e18 by each handler's vtable +0x20 message id."),
        ["00a5df70"] = ("registered-handler-install", "Rejects duplicate handler ids, appends a handler object to the handler vector, then binds the handler back to this Source-side object."),
        ["00a625e8"] = ("registered-handler-vector-append", "Vector append helper used by 00a5df70 to store a handler pointer."),
        ["00a5c2e8"] = ("attached-source-stream-reader", "Reads attached Source stream control frames and dispatches completed type-2 bitstreams to the native payload dispatcher."),
        ["00a58c10"] = ("source-payload-dispatcher", "Reads 5-bit Source payload message ids and routes built-in or registered handler payloads.")
    };

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);

        var targetFunctions = Targets
            .Select(target => BuildFunction(target.Key, target.Value.Role, target.Value.Purpose, functions))
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var report = new Tf2Ps3SourceObjectLifecycleReport(
            "tf2ps3-source-object-lifecycle-map",
            "Maps the TF.elf Source-side player/session object lifecycle around allocation, construction, owner association, handler-vector initialization, and registered payload-handler lookup/install. This is the current native path to real Source payload handler enumeration.",
            cExportPath,
            new Tf2Ps3SourceObjectLifecycleSummary(
                "0x1e28",
                "0x1e08",
                "0x1e0c",
                "0x1e18",
                "0x0090",
                "0x042e",
                "0x0544",
                targetFunctions.Length,
                targetFunctions.Count(static function => function.Present),
                targetFunctions.Count(static function => function.EvidenceTokens.Contains("handler-vector", StringComparer.Ordinal))),
            targetFunctions,
            BuildFindings(targetFunctions));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceObjectLifecycleFunction BuildFunction(
        string address,
        string role,
        string purpose,
        IReadOnlyDictionary<string, ExportedFunction> functions)
    {
        if (!functions.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceObjectLifecycleFunction(
                address,
                "",
                role,
                purpose,
                false,
                [],
                [],
                [],
                "");
        }

        return new Tf2Ps3SourceObjectLifecycleFunction(
            address,
            function.Name,
            role,
            purpose,
            true,
            ExtractCalls(function.Body),
            BuildEvidence(function.Body),
            BuildFieldContracts(function.Body),
            Preview(function.Lines));
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceObjectLifecycleFunction> functions)
    {
        var findings = new List<string>
        {
            "008b9c38 allocates 0x1e28 bytes, constructs the Source-side object through 00a5e058, inserts it into the global peer list, then calls 00a5d0c0 for owner association.",
            "0039f330 is the C-visible creator wrapper. Ghidra xrefs identify 0070c300, 007cc0d0, 0080e250, and 0080ea68 as its current higher-level code callers.",
            "00a5e058 initializes the handler vector at object +0x1e0c and count/capacity fields around +0x1e18 through 00a61e60 before calling 00a5b610.",
            "00a5d0c0 stores the owner callback object at +0x1e08, resizes the three native bit buffers through vtable +0xf0, resets object state, then calls owner vtable +0x08 with the Source object.",
            "00a5df70 and 00a57f48 are now anchored to a concrete object lifecycle: registered handlers live on the same 0x1e28 Source-side object, not on the earlier demoted UI/HUD structural candidate tables.",
            "The remaining unknown is the owner callback object's concrete class and the call path that invokes this object's +0x44 registration slot for real payload handlers."
        };

        if (functions.Any(static function => !function.Present))
        {
            findings.Add("One or more lifecycle targets were not found in the current TF.elf C export; rerun the Ghidra export if this changes.");
        }

        return findings.ToArray();
    }

    private static string[] BuildEvidence(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "FUN_00870fc8(0x1e28", "allocation-size-0x1e28");
        AddIf(body, tokens, "FUN_0039f330", "creator-wrapper-call");
        AddIf(body, tokens, "_opd_FUN_008b9c38", "creator-wrapper-target");
        AddIf(body, tokens, "_opd_FUN_00a5e058", "constructor-call");
        AddIf(body, tokens, "_opd_FUN_008bf970", "global-peer-list-insert");
        AddIf(body, tokens, "_opd_FUN_00a5d0c0", "associate-owner-call");
        AddIf(body, tokens, "PTR_PTR_01977dbc", "source-object-function-table");
        AddIf(body, tokens, "param_1[0x782]", "owner-callback-offset-0x1e08");
        AddIf(body, tokens, "param_1 + 0x783", "handler-vector");
        AddIf(body, tokens, "param_1[0x786]", "handler-vector-count-or-capacity");
        AddIf(body, tokens, "param_1[0x787]", "handler-vector-storage-shadow");
        AddIf(body, tokens, "param_1 + 0x151", "attached-payload-buffer");
        AddIf(body, tokens, "0x1e0c", "handler-table-storage");
        AddIf(body, tokens, "0x1e18", "handler-count-storage");
        AddIf(body, tokens, "0x42e", "attached-state-byte");
        AddIf(body, tokens, "0x544", "staged-payload-buffer");
        AddIf(body, tokens, "0x550", "tail-reset-start");
        AddIf(body, tokens, "0x1858", "tail-reset-length");
        AddIf(body, tokens, "*param_1 + 0xf0", "buffer-resize-slot-0xf0");
        AddIf(body, tokens, "*param_1 + 0x70", "rate-or-time-slot-0x70");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 8", "owner-bind-slot-0x08");
        AddIf(body, tokens, "_opd_FUN_00a61e60", "vector-init");
        AddIf(body, tokens, "_opd_FUN_00a5b610", "tail-reset-call");
        AddIf(body, tokens, "_opd_FUN_00a57f48", "handler-id-lookup");
        AddIf(body, tokens, "_opd_FUN_00a625e8", "handler-vector-append");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher");
        AddIf(body, tokens, "_opd_FUN_00a584d0", "attached-state-writer");
        AddIf(body, tokens, "_opd_FUN_008b82c0", "attached-socket-read");
        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildFieldContracts(string body)
    {
        var contracts = new List<string>();
        AddIf(body, contracts, "FUN_00870fc8(0x1e28", "object allocation size = 0x1e28 bytes");
        AddIf(body, contracts, "param_1[0x782]", "object +0x1e08 stores owner callback object");
        AddIf(body, contracts, "param_1 + 0x783", "object +0x1e0c starts registered handler vector");
        AddIf(body, contracts, "param_1[0x786]", "object +0x1e18 stores registered handler vector count/capacity field");
        AddIf(body, contracts, "param_1[0x787]", "object +0x1e1c shadows registered handler vector storage pointer");
        AddIf(body, contracts, "param_1[0x24]", "object +0x90 stores attached socket/peer handle");
        AddIf(body, contracts, "0x42e", "object +0x42e is attached/associated state byte");
        AddIf(body, contracts, "0x544", "object +0x544 is staged native Source payload buffer");
        AddIf(body, contracts, "0x550", "object +0x550 begins large reset gameplay/map-load field block");
        AddIf(body, contracts, "0x1858", "object +0x550 reset length is 0x1858 bytes");
        return contracts
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string[] ExtractCalls(string body)
    {
        return Regex.Matches(body, @"\b(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto|connect|socket|sys_lwmutex_lock|sys_lwmutex_unlock")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExportedFunction>();
        var start = -1;
        var name = "";
        var address = "";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]))
            {
                continue;
            }

            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[i]);
            }

            if (!match.Success)
            {
                continue;
            }

            if (start >= 0)
            {
                functions.Add(BuildExportedFunction(lines, start, i - 1, name, address));
            }

            start = i;
            name = match.Groups["name"].Value;
            address = match.Groups["address"].Value;
        }

        if (start >= 0)
        {
            functions.Add(BuildExportedFunction(lines, start, lines.Length - 1, name, address));
        }

        return functions;
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(70));
        return text.Length <= 2200 ? text : text[..2200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceObjectLifecycleReport(
    string Status,
    string Note,
    string CExportInput,
    Tf2Ps3SourceObjectLifecycleSummary Summary,
    Tf2Ps3SourceObjectLifecycleFunction[] Functions,
    string[] Findings);

public sealed record Tf2Ps3SourceObjectLifecycleSummary(
    string SourceObjectSize,
    string OwnerCallbackOffset,
    string RegisteredHandlerVectorOffset,
    string RegisteredHandlerCountOffset,
    string AttachedSocketOffset,
    string AttachedStateByteOffset,
    string StagedPayloadBufferOffset,
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int HandlerVectorFunctionCount);

public sealed record Tf2Ps3SourceObjectLifecycleFunction(
    string Address,
    string Name,
    string Role,
    string Purpose,
    bool Present,
    string[] Calls,
    string[] EvidenceTokens,
    string[] FieldContracts,
    string Preview);
