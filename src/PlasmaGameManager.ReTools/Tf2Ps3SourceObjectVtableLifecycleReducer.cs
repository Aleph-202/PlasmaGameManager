using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceObjectVtableLifecycleReducer
{
    private const string VtablePointerSymbol = "01977dbc";

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
        ["00a5c058"] = ("source-object-constructor-twin-a", "Constructor body that installs PTR_PTR_01977dbc and initializes the same handler-vector and buffer fields as 00a5e058."),
        ["00a5e058"] = ("source-object-constructor-twin-b", "Primary constructor reached from the proven allocation path; installs PTR_PTR_01977dbc and initializes the 0x1e28 Source object."),
        ["00a5e2c8"] = ("source-object-buffer-resize-slot", "Virtual buffer-resize target used by owner association through installed object vtable slot +0xf0."),
        ["00a5e4b8"] = ("source-object-string-payload-writer", "Built-in Source-object writer that increments the staged message counter and writes a small string payload into the bit buffer."),
        ["00a5e9e0"] = ("source-object-queued-bitstream-cache", "Queued bitstream/descriptor cache helper for late Source gameplay payloads."),
        ["00a5eed0"] = ("source-object-reset-destructor", "Reset/disconnect body that tears down sockets, notifies the owner, destroys registered handlers, and resets queue state."),
        ["00a5f308"] = ("source-object-delete-destructor-wrapper", "Destructor wrapper that reinstalls the concrete vtable, calls 00a5eed0, clears vectors/buffers, then frees the object."),
        ["00a5f440"] = ("source-object-destructor-wrapper-a", "Destructor wrapper that reinstalls the concrete vtable, calls 00a5eed0, and clears vectors/buffers without freeing."),
        ["00a5f560"] = ("source-object-destructor-wrapper-b", "Second destructor wrapper with the same concrete-vtable reset and vector/buffer cleanup shape."),
        ["00ecbb08"] = ("ptr-01977dbc-non-source-false-positive", "Large render/material-style function with a PTR_PTR_01977dbc reference unrelated to the 0x1e28 Source object lifecycle."),
        ["01249b00"] = ("ptr-01977dbc-material-proxy-false-positive", "Material-proxy/global table initializer that reads a word relative to PTR_PTR_01977dbc; not a Source object lifecycle function.")
    };

    public static async Task ReduceAsync(string cExportPath, string ghidraRefsPath, string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var references = LoadReferenceHits(ghidraRefsPath);

        var targetFunctions = Targets
            .Select(target => BuildFunction(target.Key, target.Value.Role, target.Value.Purpose, functions, references))
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var report = new Tf2Ps3SourceObjectVtableLifecycleReport(
            "tf2ps3-source-object-vtable-lifecycle-map",
            "Classifies every Ghidra reference to PTR_PTR_01977dbc plus the adjacent Source-object helper bodies. This separates the concrete 0x1e28 object constructor/destructor lifecycle from false-positive global/material references and keeps runtime work focused on the still-missing handler-install path.",
            cExportPath,
            ghidraRefsPath,
            "0x" + VtablePointerSymbol,
            new Tf2Ps3SourceObjectVtableLifecycleSummary(
                references.Length,
                references.Select(static reference => reference.FunctionEntry).Distinct(StringComparer.Ordinal).Count(static entry => entry.Length > 0),
                targetFunctions.Count(static function => function.Present),
                targetFunctions.Count(static function => function.Role.Contains("constructor-twin", StringComparison.Ordinal)),
                targetFunctions.Count(static function => function.Role.Contains("destructor-wrapper", StringComparison.Ordinal)),
                targetFunctions.Count(static function => function.Role == "source-object-reset-destructor"),
                targetFunctions.Count(static function => function.Role.Contains("false-positive", StringComparison.Ordinal)),
                targetFunctions.Count(static function => function.EvidenceTokens.Contains("handler-registration-call", StringComparer.Ordinal))),
            targetFunctions,
            BuildFindings(targetFunctions, references));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceObjectVtableLifecycleFunction BuildFunction(
        string address,
        string role,
        string purpose,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        IReadOnlyCollection<Tf2Ps3SourceObjectVtableLifecycleReference> references)
    {
        var hits = references
            .Where(reference => reference.FunctionEntry == address)
            .OrderBy(static reference => reference.From, StringComparer.Ordinal)
            .ToArray();

        if (!functions.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceObjectVtableLifecycleFunction(
                address,
                "",
                role,
                purpose,
                false,
                hits,
                [],
                [],
                [],
                "");
        }

        return new Tf2Ps3SourceObjectVtableLifecycleFunction(
            address,
            function.Name,
            role,
            purpose,
            true,
            hits,
            ExtractCalls(function.Body),
            BuildEvidence(address, role, function.Body),
            BuildFieldContracts(function.Body),
            Preview(function.Lines));
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceObjectVtableLifecycleFunction> functions,
        IReadOnlyCollection<Tf2Ps3SourceObjectVtableLifecycleReference> references)
    {
        var findings = new List<string>
        {
            "Ghidra currently reports seven references to PTR_PTR_01977dbc, all captured in this report.",
            "00a5c058 and 00a5e058 are constructor twins: both install PTR_PTR_01977dbc, initialize the registered-handler vector at object +0x1e0c, clear attached state, and reset the late Source object tail.",
            "00a5eed0 is the concrete reset/destructor body. It tears down the socket/attached state, notifies the owner callback, iterates already-registered handler objects, and calls handler vtable slot +0x04 to destroy them.",
            "00a5f308, 00a5f440, and 00a5f560 are destructor wrappers that restore the concrete vtable before calling 00a5eed0 and clearing the vector/buffer fields.",
            "00ecbb08 and 01249b00 are false positives for this Source-object lifecycle search: they reference the same symbol but do not construct, reset, or install handlers on the 0x1e28 object.",
            "The concrete lifecycle evidence still contains zero calls to 00a5df70. The missing runtime path remains the constructor/setup code that creates handler objects and appends them through 00a5df70."
        };

        var referenceEntries = references
            .Select(static reference => reference.FunctionEntry)
            .Where(static entry => entry.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        findings.Add($"PTR_PTR_01977dbc reference functions: {string.Join(", ", referenceEntries)}.");

        if (functions.Any(static function => !function.Present))
        {
            findings.Add("One or more target functions were not found in the current TF.elf C export; rerun the Ghidra export if this changes.");
        }

        return findings.ToArray();
    }

    private static string[] BuildEvidence(string address, string role, string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "PTR_PTR_01977dbc", "concrete-vtable-pointer-reference");
        AddIf(body, tokens, "*param_1 = PTR_PTR_01977dbc", "installs-concrete-vtable");
        AddIf(body, tokens, "*param_1 = (int)PTR_PTR_01977dbc", "restores-concrete-vtable");
        AddIf(body, tokens, "param_1 + 0x783", "handler-vector-field");
        AddIf(body, tokens, "_opd_FUN_00a61e60", "handler-vector-init");
        AddIf(body, tokens, "_opd_FUN_00a61b70", "handler-vector-clear");
        AddIf(body, tokens, "_opd_FUN_00a62330", "handler-vector-storage-reset");
        AddIf(body, tokens, "_opd_FUN_00a62398", "handler-vector-release");
        AddIf(body, tokens, "_opd_FUN_00a5b610", "tail-reset-call");
        AddIf(body, tokens, "_opd_FUN_00a5eed0", "reset-destructor-call");
        AddIf(body, tokens, "FUN_00871968((int)param_1)", "delete-free-call");
        AddIf(body, tokens, "*param_1 + 0x80", "active-object-slot-0x80-call");
        AddIf(body, tokens, "*param_1 + 0xb0", "post-error-slot-0xb0-call");
        AddIf(body, tokens, "_opd_FUN_008b83a8", "socket-close-call");
        AddIf(body, tokens, "param_1[0x24]", "attached-socket-field");
        AddIf(body, tokens, "param_1[0x23]", "attached-state-or-peer-index");
        AddIf(body, tokens, "param_1 + 0x151", "attached-payload-buffer");
        AddIf(body, tokens, "param_1[0x787]", "handler-vector-shadow");
        AddIf(body, tokens, "param_1[0x782]", "owner-callback-field");
        AddIf(body, tokens, "*puVar8 + 0xc", "owner-callback-slot-0x0c");
        AddIf(body, tokens, "*piVar5 + 0xc", "owner-callback-slot-0x0c");
        AddIf(body, tokens, "*piVar1 + 4", "handler-destroy-slot-0x04");
        AddIf(body, tokens, "_opd_FUN_008b9e70", "source-queue-reset-call");
        AddIf(body, tokens, "0x428", "staged-message-counter");
        AddIf(body, tokens, "0x2c", "bitstream-bit-position");
        AddIf(body, tokens, "0x17701", "buffer-size-ceiling-96000");
        AddIf(body, tokens, "96000", "large-buffer-limit");
        AddIf(body, tokens, "local_78[0x42]", "queued-descriptor-buffer-pointer");
        AddIf(body, tokens, "local_78[0x44]", "queued-descriptor-bit-length");
        AddIf(body, tokens, "PTR_s_MatrixRotate_IMaterialProxy003", "material-proxy-string");
        AddIf(body, tokens, "PTR_s_LinearRamp_IMaterialProxy003", "material-proxy-string");
        AddIf(body, tokens, "PTR_s_TE_Decal_0197ca80", "render-decal-token");
        AddIf(body, tokens, "_opd_FUN_00a5df70", "handler-registration-call");
        AddIf(body, tokens, "FUN_00a5df70", "handler-registration-call");

        if (role.Contains("false-positive", StringComparison.Ordinal))
        {
            tokens.Add("classified-non-source-lifecycle");
        }

        if (address is "00a5c058" or "00a5e058")
        {
            tokens.Add("constructor-twin");
        }

        if (address is "00a5f308" or "00a5f440" or "00a5f560")
        {
            tokens.Add("destructor-wrapper");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildFieldContracts(string body)
    {
        var contracts = new List<string>();
        AddIf(body, contracts, "param_1 + 0x783", "object +0x1e0c starts registered handler vector");
        AddIf(body, contracts, "param_1[0x786]", "object +0x1e18 stores registered handler vector count/capacity field");
        AddIf(body, contracts, "param_1[0x787]", "object +0x1e1c shadows registered handler vector storage pointer");
        AddIf(body, contracts, "param_1[0x782]", "object +0x1e08 stores owner callback object");
        AddIf(body, contracts, "param_1[0x24]", "object +0x90 stores attached socket/peer handle");
        AddIf(body, contracts, "param_1[0x23]", "object +0x8c stores active peer/state index");
        AddIf(body, contracts, "param_1 + 0x151", "object +0x544 is attached/staged payload buffer");
        AddIf(body, contracts, "0x428", "object +0x428 counts staged string/source payload writes");
        AddIf(body, contracts, "local_78[0x42]", "queued descriptor +0x108 stores copied payload buffer pointer");
        AddIf(body, contracts, "local_78[0x44]", "queued descriptor +0x110 stores payload bit length");
        AddIf(body, contracts, "PTR_s_MatrixRotate_IMaterialProxy003", "global material proxy false-positive table, not Source object field");
        return contracts
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static Tf2Ps3SourceObjectVtableLifecycleReference[] LoadReferenceHits(string ghidraRefsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(ghidraRefsPath));
        var hits = new List<Tf2Ps3SourceObjectVtableLifecycleReference>();
        foreach (var target in document.RootElement.GetProperty("Targets").EnumerateArray())
        {
            if (!string.Equals(target.GetProperty("Address").GetString(), VtablePointerSymbol, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var hit in target.GetProperty("ReferenceHits").EnumerateArray())
            {
                hits.Add(new Tf2Ps3SourceObjectVtableLifecycleReference(
                    hit.GetProperty("From").GetString() ?? "",
                    hit.GetProperty("Type").GetString() ?? "",
                    hit.GetProperty("Source").GetString() ?? "",
                    hit.GetProperty("FunctionEntry").GetString() ?? "",
                    hit.GetProperty("FunctionName").GetString() ?? ""));
            }
        }

        return hits.ToArray();
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

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(60));
        return text.Length <= 1800 ? text : text[..1800];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceObjectVtableLifecycleReport(
    string Status,
    string Note,
    string CExportInput,
    string GhidraRefsInput,
    string VtablePointerSymbol,
    Tf2Ps3SourceObjectVtableLifecycleSummary Summary,
    Tf2Ps3SourceObjectVtableLifecycleFunction[] Functions,
    string[] Findings);

public sealed record Tf2Ps3SourceObjectVtableLifecycleSummary(
    int VtablePointerReferenceHitCount,
    int VtablePointerReferenceFunctionCount,
    int LocatedFunctionCount,
    int ConstructorTwinCount,
    int DestructorWrapperCount,
    int ResetDestructorCount,
    int FalsePositiveReferenceCount,
    int HandlerRegistrationEvidenceCount);

public sealed record Tf2Ps3SourceObjectVtableLifecycleFunction(
    string Address,
    string Name,
    string Role,
    string Purpose,
    bool Present,
    Tf2Ps3SourceObjectVtableLifecycleReference[] ReferenceHits,
    string[] Calls,
    string[] EvidenceTokens,
    string[] FieldContracts,
    string Preview);

public sealed record Tf2Ps3SourceObjectVtableLifecycleReference(
    string From,
    string Type,
    string Source,
    string FunctionEntry,
    string FunctionName);
