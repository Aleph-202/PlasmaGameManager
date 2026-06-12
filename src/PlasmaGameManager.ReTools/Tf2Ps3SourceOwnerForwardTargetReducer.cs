using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerForwardTargetReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008722a0",
        "00872460",
        "00878108"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceOwnerForwardTargetReport> ReduceAsync(
        string cExportPath,
        string ownerControlSubobjectPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        using var ownerControlDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerControlSubobjectPath));
        var ownerControlSummary = ownerControlDoc.RootElement.GetProperty("Summary");
        var upstreamOwnerForwarderRecovered =
            ReadBool(ownerControlSummary, "OwnerSlot8TargetRecovered")
            && ReadBool(ownerControlSummary, "OwnerSlot8ForwarderRecovered");

        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var forwardTarget = byAddress.GetValueOrDefault("008722a0");
        var contextLookup = byAddress.GetValueOrDefault("00872460");
        var alternateReaderRoute = byAddress.GetValueOrDefault("00878108");

        var ownerForwardTargetRecovered =
            forwardTarget?.EvidenceTokens.Contains("disabled-direct-payload-slot18", StringComparer.Ordinal) == true
            && forwardTarget.EvidenceTokens.Contains("payload-predicate-slot1c-runtime-state-d8", StringComparer.Ordinal)
            && forwardTarget.EvidenceTokens.Contains("payload-message-id-slot20", StringComparer.Ordinal)
            && forwardTarget.EvidenceTokens.Contains("context-table-base-528c", StringComparer.Ordinal)
            && forwardTarget.EvidenceTokens.Contains("context-table-stride-0x18", StringComparer.Ordinal)
            && forwardTarget.EvidenceTokens.Contains("payload-dispatch-slot18-with-context", StringComparer.Ordinal);
        var categoryRulesRecovered =
            forwardTarget?.EvidenceTokens.Contains("message-id-0x11-category-4", StringComparer.Ordinal) == true
            && forwardTarget.EvidenceTokens.Contains("message-id-0x0f-category-3", StringComparer.Ordinal)
            && forwardTarget.EvidenceTokens.Contains("message-id-0x1b-category-5");
        var contextLookupRecovered =
            contextLookup?.EvidenceTokens.Contains("context-index-upper-bound-6", StringComparer.Ordinal) == true
            && contextLookup.EvidenceTokens.Contains("context-table-base-528c", StringComparer.Ordinal)
            && contextLookup.EvidenceTokens.Contains("context-table-stride-0x18", StringComparer.Ordinal);
        var alternateBitreaderRouteRecovered =
            alternateReaderRoute?.EvidenceTokens.Contains("stack-bitreader-init-00870968", StringComparer.Ordinal) == true
            && alternateReaderRoute.EvidenceTokens.Contains("cbin-recognized-route-to-008722a0", StringComparer.Ordinal);

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "owner-control-subobject-upstream-recovered",
                upstreamOwnerForwarderRecovered ? "proven" : "missing",
                ownerControlSubobjectPath,
                "The owner/control report must prove 00a55d38 -> 00a52720 -> 008722a0 before this router is actionable."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "owner-forward-target-router-recovered",
                ownerForwardTargetRecovered ? "proven" : "missing",
                "008722a0",
                "The forward target routes payload objects into a six-entry context table and calls payload vtable slot +0x18."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "owner-forward-target-category-rules-recovered",
                categoryRulesRecovered ? "proven" : "missing",
                "008722a0 slot +0x20 ids 0x0f/0x11/0x1b",
                "The recovered category rules identify the visible type-id split before context dispatch."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "owner-forward-target-context-lookup-recovered",
                contextLookupRecovered ? "proven" : "missing",
                "00872460",
                "The table helper returns category * 0x18 + param_1 + 0x528c for category values below 6."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "alternate-bitreader-route-recovered",
                alternateBitreaderRouteRecovered ? "proven" : "missing",
                "00878108",
                "A non-network reader route can initialize a stack bitreader and feed 008722a0 when the cbin recognizer succeeds."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "native-owner-forward-context-handlers-implemented",
                "missing",
                "server implementation gate",
                "The native replacement still needs semantic handlers for the six 008722a0 context slots and payload vtable +0x18 calls."),
            new Tf2Ps3SourceOwnerForwardTargetGate(
                "native-source-input-ready",
                "missing",
                "server/live verification gate",
                "This remains incomplete until the context handlers decode live/PCAP owner-control uploads into named client commands.")
        };

        var report = new Tf2Ps3SourceOwnerForwardTargetReport(
            "tf2ps3-source-owner-forward-target-map",
            "Reduces TF.elf owner-control forward target 008722a0 after the 00a52720 bitreader rebuild: context table, category selector, payload object virtual dispatch, and remaining native handler gates.",
            new Tf2Ps3SourceOwnerForwardTargetInputs(cExportPath, ownerControlSubobjectPath),
            new Tf2Ps3SourceOwnerForwardTargetSummary(
                TargetAddresses.Length,
                functions.Length,
                upstreamOwnerForwarderRecovered,
                ownerForwardTargetRecovered,
                categoryRulesRecovered,
                contextLookupRecovered,
                alternateBitreaderRouteRecovered,
                false,
                false,
                gates.Count(static gate => gate.Status == "missing")),
            new Tf2Ps3SourceOwnerForwardTargetRouter(
                "008722a0",
                "param_1 + 0x528c",
                0x18,
                6,
                "payload vtable +0x18",
                "payload vtable +0x1c",
                "payload vtable +0x20",
                [
                    new("disabled/direct", "param_1 + 0x531c != 0", "payload vtable +0x18 with no context pointer"),
                    new("category-1", "payload vtable +0x1c(param_2, param_1 + 0xd8) returns true", "context table index 1"),
                    new("category-2", "default when +0x1c is false and +0x20 id is not 0x0f, 0x11, or 0x1b", "context table index 2"),
                    new("category-3", "payload vtable +0x20 returns 0x0f", "context table index 3"),
                    new("category-4", "payload vtable +0x20 returns 0x11", "context table index 4"),
                    new("category-5", "payload vtable +0x20 returns 0x1b", "context table index 5")
                ]),
            functions,
            gates,
            [
                "The owner-control payload rebuilt by 00a52720 is not parsed directly there. It is forwarded to 008722a0, which chooses a context slot at param_1 + 0x528c with 0x18-byte stride and then calls payload vtable +0x18.",
                "Payload vtable +0x1c is a runtime-state predicate against param_1 + 0xd8. If it passes, category 1 is used before reading a type id.",
                "Payload vtable +0x20 returns a visible type id used by 008722a0: 0x11 maps to category 4, 0x0f maps to category 3, 0x1b maps to category 5, and the default branch maps to category 2.",
                "The next implementation target is no longer just 00a52720. It is the six context handlers reached by 008722a0's payload vtable +0x18 dispatch."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerForwardTargetFunction BuildFunction(ExportedFunction function) =>
        new(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyRole(function.Address),
            ExtractCalls(function.Body),
            BuildEvidenceTokens(function),
            Preview(function.Lines));

    private static string ClassifyRole(string address) =>
        address switch
        {
            "008722a0" => "owner-control-context-router",
            "00872460" => "owner-control-context-table-lookup",
            "00878108" => "alternate-cbin-bitreader-route-to-owner-router",
            _ => "owner-forward-target-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "param_1 + 0x531c", "disabled-direct-state-flag-531c");
        AddIf(body, tokens, "*param_2 + 0x18", "payload-dispatch-slot18");
        AddIf(body, tokens, "*(char *)(param_1 + 0x531c) != '\\0'", "disabled-direct-payload-slot18");
        AddIf(body, tokens, "*param_2 + 0x1c", "payload-predicate-slot1c");
        AddIf(body, tokens, "param_1 + 0xd8", "payload-predicate-slot1c-runtime-state-d8");
        AddIf(body, tokens, "*param_2 + 0x20", "payload-message-id-slot20");
        AddIf(body, tokens, "iVar1 != 0x11", "message-id-0x11-category-4");
        AddIf(body, tokens, "iVar1 != 0xf", "message-id-0x0f-category-3");
        AddIf(body, tokens, "uVar2 ^ 0x1b", "message-id-0x1b-category-5");
        AddIf(body, tokens, "uVar5 * 0x20 + (uVar5 & 0x1fffffff) * -8", "context-table-stride-0x18");
        AddIf(body, tokens, "param_2 * 0x18", "context-table-stride-0x18");
        AddIf(body, tokens, "param_1 + 0x528c", "context-table-base-528c");
        AddIf(body, tokens, "param_2 * 0x18 + param_1 + 0x528c", "context-table-lookup-expression");
        AddIf(body, tokens, "param_2 < 6", "context-index-upper-bound-6");
        AddIf(body, tokens, "(param_2,uVar5 * 0x20", "payload-dispatch-slot18-with-context");
        AddIf(body, tokens, "FUN_00870968(puVar3,auStack_430,0x400,0,-1)", "stack-bitreader-init-00870968");
        AddIf(body, tokens, "_opd_FUN_008722a0(param_1,(int *)lVar5)", "cbin-recognized-route-to-008722a0");
        AddIf(body, tokens, "**(int **)PTR_s__s_cbin_01971570", "cbin-recognizer");

        foreach (var token in new[]
        {
            "_opd_FUN_008722a0",
            "FUN_00870968",
            "FUN_0086db98"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                tokens.Add(token);
            }
        }

        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
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
        var matches = Regex.Matches(body, @"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto)\s*\(");
        return matches
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static ExportedFunction[] ExtractFunctions(string[] lines)
    {
        var starts = new List<(int Index, string Name, string Address)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
                continue;
            }

            match = SplitFunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
            }
        }

        var functions = new List<ExportedFunction>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].Index - 1 : lines.Length - 1;
            functions.Add(BuildExportedFunction(lines, start.Index, end, start.Name, start.Address));
        }

        return functions.ToArray();
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(100));
        return text.Length <= 3200 ? text : text[..3200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceOwnerForwardTargetReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerForwardTargetInputs Inputs,
    Tf2Ps3SourceOwnerForwardTargetSummary Summary,
    Tf2Ps3SourceOwnerForwardTargetRouter Router,
    Tf2Ps3SourceOwnerForwardTargetFunction[] Functions,
    Tf2Ps3SourceOwnerForwardTargetGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerForwardTargetInputs(
    string CExport,
    string OwnerControlSubobjectMap);

public sealed record Tf2Ps3SourceOwnerForwardTargetSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool UpstreamOwnerForwarderRecovered,
    bool OwnerForwardTargetRecovered,
    bool CategoryRulesRecovered,
    bool ContextLookupRecovered,
    bool AlternateBitreaderRouteRecovered,
    bool NativeOwnerForwardContextHandlersImplemented,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceOwnerForwardTargetRouter(
    string Address,
    string ContextTableBase,
    int ContextStrideBytes,
    int ContextSlotCount,
    string PayloadDispatchSlot,
    string PayloadPredicateSlot,
    string PayloadMessageIdSlot,
    Tf2Ps3SourceOwnerForwardTargetCategory[] Categories);

public sealed record Tf2Ps3SourceOwnerForwardTargetCategory(
    string Name,
    string Condition,
    string Target);

public sealed record Tf2Ps3SourceOwnerForwardTargetFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceOwnerForwardTargetGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
