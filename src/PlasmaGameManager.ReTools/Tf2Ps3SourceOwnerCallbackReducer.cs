using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerCallbackReducer
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
        ["0039f330"] = ("source-object-creator-wrapper", "C-visible wrapper around 008b9c38. The fourth argument is forwarded as the Source object's owner callback pointer."),
        ["0070c300"] = ("source-object-owner-caller-global-setup", "Global setup path that passes the stack/global value at +0x4078 as the Source object owner callback."),
        ["007cc0d0"] = ("source-object-owner-caller-session-setup", "Session setup path that passes param_1 as the Source object owner callback, then calls the Source object +0x68 slot."),
        ["0080e250"] = ("source-object-owner-caller-player-bind-a", "Player/session bind path that passes piVar4 + 1 as the Source object owner callback and then notifies the owner through slot +0x2c."),
        ["0080ea68"] = ("source-object-owner-caller-player-bind-b", "Join/reconnect style path that passes piVar5 + 1 as the Source object owner callback and then notifies the owner through slot +0x2c."),
        ["00a5d0c0"] = ("owner-callback-association", "Stores the owner callback at Source object +0x1e08 and calls owner slot +0x08 with the Source object."),
        ["00a58868"] = ("owner-callback-inline-control-branch", "Payload dispatcher helper for low native message ids. It reads bounded inline string/control payloads and calls owner slots +0x0c or +0x1c/+0x24."),
        ["00a58c10"] = ("owner-callback-dispatch-error-path", "Payload dispatcher calls owner slot +0x10 when the incoming bitstream enters an error/end state and delegates low native message ids to 00a58868."),
        ["00a5b4f0"] = ("owner-callback-packet-dispatch-wrapper", "Iterates queued payloads, calls owner slot +0x14 before dispatch, 00a58c10 for payload decode, and owner slot +0x18 after dispatch."),
        ["00a5d720"] = ("owner-callback-subpayload-complete", "Completes queued subpayloads and calls owner slot +0x20 with the subpayload key and sequence metadata."),
        ["00a5d9e0"] = ("owner-callback-packet-receive-path", "Receive path calls owner slot +0x14 before dispatching payload data and then later calls post-dispatch paths.")
    };

    private static readonly Tf2Ps3SourceOwnerCallbackSlot[] CallbackSlots =
    [
        new("0x08", "bind-source-object", "00a5d0c0", "Called after Source object reset/association. Argument 2 is the concrete 0x1e28 Source object."),
        new("0x0c", "dispatcher-inline-string", "00a58868", "Called by the low-id dispatcher helper after reading a bounded inline string/control payload."),
        new("0x10", "dispatch-error-or-stream-end", "00a58c10", "Called when the payload dispatcher sees stream/error state before a full message can be read."),
        new("0x14", "pre-payload-dispatch", "00a5b4f0/00a5d9e0", "Called before 00a58c10 receives a queued/native Source payload. Arguments include Source object sequence/frame fields."),
        new("0x18", "post-payload-dispatch", "00a5b4f0", "Called after 00a58c10 handles a queued payload."),
        new("0x1c", "dispatcher-inline-owner-branch-a", "00a58868", "Selected by one payload bit after the low-id dispatcher helper reads a bounded inline string/control payload."),
        new("0x20", "queued-subpayload-complete", "00a5d720", "Called when a deferred subpayload/keyed record is completed."),
        new("0x24", "dispatcher-inline-owner-branch-b", "00a58868", "Selected by the alternate payload bit after the low-id dispatcher helper reads a bounded inline string/control payload.")
    ];

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);

        var targetFunctions = Targets
            .Select(target => BuildFunction(target.Key, target.Value.Role, target.Value.Purpose, functions))
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var report = new Tf2Ps3SourceOwnerCallbackReport(
            "tf2ps3-source-owner-callback-map",
            "Maps the owner callback interface stored at Source object +0x1e08. This is the next proven native boundary after the 0x1e28 Source object lifecycle and before registered Source payload handlers are installed.",
            cExportPath,
            new Tf2Ps3SourceOwnerCallbackSummary(
                "0x1e28",
                "0x1e08",
                CallbackSlots.Length,
                targetFunctions.Length,
                targetFunctions.Count(static function => function.Present),
                targetFunctions.Count(static function => function.OwnerExpression.Length > 0),
                targetFunctions.Count(static function => function.CallbackSlots.Length > 0)),
            CallbackSlots,
            targetFunctions,
            BuildFindings(targetFunctions));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceOwnerCallbackFunction BuildFunction(
        string address,
        string role,
        string purpose,
        IReadOnlyDictionary<string, ExportedFunction> functions)
    {
        if (!functions.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceOwnerCallbackFunction(
                address,
                "",
                role,
                purpose,
                false,
                "",
                [],
                [],
                [],
                []);
        }

        return new Tf2Ps3SourceOwnerCallbackFunction(
            address,
            function.Name,
            role,
            purpose,
            true,
            ExtractOwnerExpression(address, function.Body),
            ExtractCallbackSlots(function.Body),
            ExtractCalls(function.Body),
            BuildEvidence(function.Body),
            ExtractCallExpressions(function.Body));
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceOwnerCallbackFunction> functions)
    {
        var findings = new List<string>
        {
            "00a5d0c0 stores the owner callback object at Source object +0x1e08 and immediately calls owner slot +0x08 with the newly associated Source object.",
            "00a58868, 00a58c10, 00a5b4f0, 00a5d720, and 00a5d9e0 prove that the owner callback interface has at least eight used slots: +0x08, +0x0c, +0x10, +0x14, +0x18, +0x1c, +0x20, and +0x24.",
            "00a58868 uses the direct Source object owner pointer at +0x1e08 for slots +0x0c and +0x1c/+0x24 after reading bounded inline string/control payloads from the native bitstream.",
            "The four wrapper callers prove multiple owner expressions: global setup passes stack/global +0x4078, session setup passes param_1, and player bind paths pass piVar4 + 1 or piVar5 + 1.",
            "This map still does not prove the concrete owner callback vtable or the owner callback method that reaches the 0180c9c0 registration helper slice for real handler objects.",
            "Runtime changes should wait until that owner-class constructor/vtable path is recovered; otherwise handler ids and map-load semantics would still be speculative."
        };

        if (functions.Any(static function => !function.Present))
        {
            findings.Add("One or more owner-callback targets were not found in the current TF.elf C export; rerun the Ghidra export if this changes.");
        }

        return findings.ToArray();
    }

    private static string ExtractOwnerExpression(string address, string body)
    {
        return address switch
        {
            "0070c300" when body.Contains("FUN_0039f330(0,0,uVar5,uVar10,0", StringComparison.Ordinal) => "uVar10 from stack/global +0x4078",
            "007cc0d0" when body.Contains("FUN_0039f330((longlong)piVar2[3],param_2 & 0xffffffff", StringComparison.Ordinal) => "param_1 / piVar2 session object",
            "0080e250" when body.Contains("ZEXT48(piVar4 + 1)", StringComparison.Ordinal) => "piVar4 + 1 owner subobject",
            "0080ea68" when body.Contains("piVar20 = piVar5 + 1", StringComparison.Ordinal) => "piVar5 + 1 owner subobject",
            "00a5d0c0" when body.Contains("param_1[0x782] = param_5", StringComparison.Ordinal) => "param_5 stored at Source object +0x1e08",
            _ => ""
        };
    }

    private static string[] ExtractCallbackSlots(string body)
    {
        var slots = new List<string>();
        AddIf(body, slots, "*(int *)param_1[0x782] + 8", "0x08");
        AddIf(body, slots, "**(int **)(param_1 + 0x1e08) + 0xc", "0x0c");
        AddIf(body, slots, "*(int *)param_1[0x782] + 0x10", "0x10");
        AddIf(body, slots, "*(int *)param_1[0x782] + 0x14", "0x14");
        AddIf(body, slots, "*(int *)param_1[0x782] + 0x18", "0x18");
        AddIf(body, slots, "*(int *)param_1[0x782] + 0x20", "0x20");
        AddIf(body, slots, "*piVar3 + 0x1c", "0x1c");
        AddIf(body, slots, "*piVar3 + 0x24", "0x24");
        return slots
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildEvidence(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "FUN_0039f330", "creator-wrapper-call");
        AddIf(body, tokens, "_opd_FUN_008b9c38", "creator-wrapper-target");
        AddIf(body, tokens, "param_1[0x782] = param_5", "owner-stored-at-0x1e08");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 8", "owner-slot-0x08-bind");
        AddIf(body, tokens, "**(int **)(param_1 + 0x1e08) + 0xc", "owner-slot-0x0c-inline-string");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x10", "owner-slot-0x10-error");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x14", "owner-slot-0x14-pre-dispatch");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x18", "owner-slot-0x18-post-dispatch");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x20", "owner-slot-0x20-subpayload-complete");
        AddIf(body, tokens, "*piVar3 + 0x1c", "owner-slot-0x1c-inline-branch");
        AddIf(body, tokens, "*piVar3 + 0x24", "owner-slot-0x24-inline-branch");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, tokens, "_opd_FUN_00a5b610", "source-object-tail-reset");
        AddIf(body, tokens, "*piVar4 + 0x2c", "owner-notify-slot-0x2c");
        AddIf(body, tokens, "*piVar5 + 0x2c", "owner-notify-slot-0x2c");
        AddIf(body, tokens, "*puVar8 + 0x20", "source-object-ready-slot-0x20");
        AddIf(body, tokens, "*puVar8 + 0xc", "source-object-cleanup-slot-0x0c");
        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractCallExpressions(string body)
    {
        return body.Split('\n')
            .Where(static line =>
                line.Contains("param_1[0x782]", StringComparison.Ordinal)
                || line.Contains("FUN_0039f330", StringComparison.Ordinal)
                || line.Contains("param_1 + 0x1e08", StringComparison.Ordinal)
                || line.Contains("+ 0x2c", StringComparison.Ordinal)
                || line.Contains("+0x2c", StringComparison.Ordinal))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .Take(24)
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
        return Regex.Matches(body, @"\b(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto|connect|socket")
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

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceOwnerCallbackReport(
    string Status,
    string Note,
    string CExportInput,
    Tf2Ps3SourceOwnerCallbackSummary Summary,
    Tf2Ps3SourceOwnerCallbackSlot[] CallbackSlots,
    Tf2Ps3SourceOwnerCallbackFunction[] Functions,
    string[] Findings);

public sealed record Tf2Ps3SourceOwnerCallbackSummary(
    string SourceObjectSize,
    string OwnerCallbackOffset,
    int CallbackSlotCount,
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int OwnerExpressionCount,
    int CallbackUsingFunctionCount);

public sealed record Tf2Ps3SourceOwnerCallbackSlot(
    string Offset,
    string Role,
    string EvidenceFunction,
    string Meaning);

public sealed record Tf2Ps3SourceOwnerCallbackFunction(
    string Address,
    string Name,
    string Role,
    string Purpose,
    bool Present,
    string OwnerExpression,
    string[] CallbackSlots,
    string[] Calls,
    string[] EvidenceTokens,
    string[] CallExpressions);
