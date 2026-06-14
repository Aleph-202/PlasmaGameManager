using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerForwardContextReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] FixedTargetAddresses =
    [
        "008722a0",
        "00872460",
        "00a2b060"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceOwnerForwardContextReport> ReduceAsync(
        string cExportPath,
        string ownerVtablePath,
        string ownerForwardTargetPath,
        string outputPath,
        string? sourceRoot = null)
    {
        var allFunctions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First(),
                StringComparer.Ordinal);

        using var ownerVtableDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerVtablePath));
        using var ownerForwardTargetDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerForwardTargetPath));

        var sourceOwnerTable = FindOwnerTable(ownerVtableDoc.RootElement, "0x0180c81c");
        var ownerForwarders = BuildOwnerForwarders(sourceOwnerTable, allFunctions);
        var targetFunctions = FixedTargetAddresses
            .Select(address => allFunctions.GetValueOrDefault(address))
            .Where(static function => function is not null)
            .Select(static function => BuildFunction(function!))
            .Concat(ownerForwarders
                .Select(forwarder => allFunctions.GetValueOrDefault(forwarder.FunctionAddress))
                .Where(static function => function is not null)
                .Select(static function => BuildFunction(function!)))
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var forwardTargetSummary = ownerForwardTargetDoc.RootElement.GetProperty("Summary");
        var upstreamRouterRecovered = ReadBool(forwardTargetSummary, "OwnerForwardTargetRecovered")
            && ReadBool(forwardTargetSummary, "ContextLookupRecovered")
            && ReadBool(forwardTargetSummary, "CategoryRulesRecovered");

        var category5Consumer = targetFunctions.SingleOrDefault(static function => function.Address == "00a2b060");
        var category5UsercmdRouteCandidateRecovered =
            category5Consumer?.EvidenceTokens.Contains("source-queued-client-command-count-20b0", StringComparer.Ordinal) == true
            && category5Consumer.EvidenceTokens.Contains("stack-bitreader-init-96000", StringComparer.Ordinal)
            && category5Consumer.EvidenceTokens.Contains("route-local-bitreader-through-008722a0", StringComparer.Ordinal)
            && category5Consumer.EvidenceTokens.Contains("lookup-context-category-5", StringComparer.Ordinal)
            && category5Consumer.EvidenceTokens.Contains("post-router-call-00874458", StringComparer.Ordinal);
        var context5PassedToHandler =
            category5Consumer?.EvidenceTokens.Contains("passes-context5-to-dynamic-handler-98", StringComparer.Ordinal) == true;
        var implementation = ScanImplementation(sourceRoot);
        var nativeOwnerForwardContextHandlersImplemented =
            category5UsercmdRouteCandidateRecovered
            && context5PassedToHandler
            && implementation.Category5UsercmdContextHandlerImplemented;
        var nativeSourceInputReady = nativeOwnerForwardContextHandlersImplemented;
        var ownerForwarderFamilyRecovered = ownerForwarders.Length >= 10
            && ownerForwarders.Any(static forwarder => forwarder.LayoutKind == "bitreader-rebuild-word5")
            && ownerForwarders.Any(static forwarder => forwarder.LayoutKind == "bitreader-rebuild-word6")
            && ownerForwarders.Any(static forwarder => forwarder.LayoutKind == "deferred-pointer-word6")
            && ownerForwarders.Any(static forwarder => forwarder.LayoutKind == "config-state-or-fallback-word4");

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerForwardContextGate(
                "owner-forward-target-upstream-router-recovered",
                upstreamRouterRecovered ? "proven" : "missing",
                ownerForwardTargetPath,
                "008722a0 category routing and 00872460 context lookup must be recovered before assigning context-handler meaning."),
            new Tf2Ps3SourceOwnerForwardContextGate(
                "owner-forwarder-family-recovered",
                ownerForwarderFamilyRecovered ? "proven" : "candidate",
                "owner table 0x0180c81c slots that call 008722a0",
                "The Source owner table exposes the concrete wrapper family that rebuilds or forwards payload objects into 008722a0."),
            new Tf2Ps3SourceOwnerForwardContextGate(
                "category-5-usercmd-route-candidate",
                category5UsercmdRouteCandidateRecovered ? "proven" : "missing",
                "00a2b060",
                "00a2b060 rebuilds a local bitreader, feeds 008722a0, looks up context 5, and performs the movement/update follow-up."),
            new Tf2Ps3SourceOwnerForwardContextGate(
                "category-5-dynamic-handler-target",
                context5PassedToHandler ? "candidate" : "missing",
                "00a2b060 indirect call through *(iVar9 + 8) + 0x98",
                "The exact target behind the category-5 context consumer is still an indirect vtable call, so the handler body remains unresolved."),
            new Tf2Ps3SourceOwnerForwardContextGate(
                "native-owner-forward-context-handlers-implemented",
                nativeOwnerForwardContextHandlersImplemented ? "proven" : "missing",
                implementation.Category5UsercmdContextHandlerEvidence,
                "The native replacement must route decoded 008722a0 category-5 usercmd payloads into the official CLC_Move/CUserCmd semantic handler."),
            new Tf2Ps3SourceOwnerForwardContextGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "candidate" : "missing",
                "server/live verification gate",
                "This remains incomplete until live/PCAP markerless client payloads are decoded into these native context commands.")
        };

        var report = new Tf2Ps3SourceOwnerForwardContextReport(
            "tf2ps3-source-owner-forward-context-map",
            "Extends the TF.elf 008722a0 owner-forward target by naming the owner wrapper payload shapes and the strongest visible category-5 usercmd/CLC move route candidate.",
            new Tf2Ps3SourceOwnerForwardContextInputs(cExportPath, ownerVtablePath, ownerForwardTargetPath),
            new Tf2Ps3SourceOwnerForwardContextSummary(
                FixedTargetAddresses.Length,
                targetFunctions.Length,
                ownerForwarders.Length,
                ownerForwarders.Count(static forwarder => forwarder.LayoutKind == "direct-forward"),
                ownerForwarders.Count(static forwarder => forwarder.LayoutKind.StartsWith("bitreader-rebuild", StringComparison.Ordinal)),
                ownerForwarders.Count(static forwarder => forwarder.LayoutKind == "deferred-pointer-word6"),
                ownerForwarders.Count(static forwarder => forwarder.LayoutKind == "config-state-or-fallback-word4"),
                upstreamRouterRecovered,
                ownerForwarderFamilyRecovered,
                category5UsercmdRouteCandidateRecovered,
                context5PassedToHandler,
                nativeOwnerForwardContextHandlersImplemented,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            new Tf2Ps3SourceOwnerForwardContextConsumer(
                "00a2b060",
                "category-5-usercmd-or-clc-move-route-candidate",
                "00872460(..., 5)",
                "dynamic call through *(iVar9 + 8) + 0x98",
                [
                    "Only active/client-state path reaches this branch; inactive path calls 00a2abe8 and 009fb5c8.",
                    "When queued command count at param_1[0x20b0] is positive, a 96000-byte stack bitreader is initialized and populated through 00a2ae20.",
                    "The local bitreader is routed through 008722a0 before category 5 is fetched with 00872460.",
                    "The fetched context pointer is passed into the dynamic handler, then 00874458 performs the post-router update/flush."
                ],
                category5Consumer?.EvidenceTokens ?? []),
            ownerForwarders,
            targetFunctions,
            gates,
            [
                "The owner table 0x0180c81c has ten concrete slots that forward to 008722a0. They are not one packet format; they describe several payload wrapper layouts.",
                "00a52720 and 00a52930 rebuild a bitreader from param_2[5], payload bytes at param_2 + 6, and reader state at param_2 + 0xf.",
                "00a52840 rebuilds a sibling layout from param_2[6], payload bytes at param_2 + 7, and reader state at param_2 + 0x10.",
                "00a52ae0 copies param_2[6] bits from param_2 + 10 and stores the temporary pointer at param_2[0x13] before routing.",
                "00a539d0 is a state/config wrapper. It either writes runtime fields under *(param_1 + 0x4a18) + 0x6344/0x6350/0x6354/0x6358 or falls back to a param_2[4] bitreader rebuild.",
                "00a2b060 is the strongest currently recovered consumer for context category 5. It likely corresponds to the usercmd/CLC move route, but its final dynamic handler target still needs vtable resolution before the replacement server can claim native input completion."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerForwardContextForwarder[] BuildOwnerForwarders(
        JsonElement ownerTable,
        IReadOnlyDictionary<string, ExportedFunction> allFunctions)
    {
        var forwarders = new List<Tf2Ps3SourceOwnerForwardContextForwarder>();
        foreach (var slot in ownerTable.GetProperty("Slots").EnumerateArray())
        {
            var calls = slot.TryGetProperty("Calls", out var callsElement)
                ? callsElement.EnumerateArray().Select(static call => call.GetString() ?? "").ToArray()
                : [];
            if (!calls.Contains("_opd_FUN_008722a0", StringComparer.Ordinal))
            {
                continue;
            }

            var functionAddress = ReadString(slot, "FunctionAddress");
            if (!allFunctions.TryGetValue(functionAddress, out var function))
            {
                continue;
            }

            var tokens = BuildForwarderEvidenceTokens(function.Body);
            forwarders.Add(new Tf2Ps3SourceOwnerForwardContextForwarder(
                slot.GetProperty("SlotIndex").GetInt32(),
                ReadString(slot, "SlotOffset"),
                ReadString(slot, "TableAddress"),
                functionAddress,
                ReadString(slot, "OpdAddress"),
                function.Name,
                ClassifyForwarderLayout(tokens),
                BitCountField(tokens),
                BitSource(tokens),
                ReaderTarget(tokens),
                tokens,
                calls,
                MeaningForForwarder(tokens),
                Preview(function.Lines)));
        }

        return forwarders
            .OrderBy(static forwarder => forwarder.SlotIndex)
            .ToArray();
    }

    private static string[] BuildForwarderEvidenceTokens(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "_opd_FUN_008722a0(*(int *)(param_1 + 0x4a18),param_2)", "forwards-to-source-context-router");
        AddIf(body, tokens, "param_2[4]", "bit-count-param2-word4");
        AddIf(body, tokens, "param_2[5]", "bit-count-param2-word5");
        AddIf(body, tokens, "param_2[6]", "bit-count-param2-word6");
        AddIf(body, tokens, "param_2 + 5", "bit-source-param2-plus5");
        AddIf(body, tokens, "param_2 + 6", "bit-source-param2-plus6");
        AddIf(body, tokens, "param_2 + 7", "bit-source-param2-plus7");
        AddIf(body, tokens, "param_2 + 10", "bit-source-param2-plus10");
        AddIf(body, tokens, "param_2 + 0xe", "reader-target-param2-plus0e");
        AddIf(body, tokens, "param_2 + 0xf", "reader-target-param2-plus0f");
        AddIf(body, tokens, "param_2 + 0x10", "reader-target-param2-plus10");
        AddIf(body, tokens, "param_2[0x13]", "stores-temporary-payload-pointer-param2-word13");
        AddIf(body, tokens, "FUN_0086acb8", "copies-bit-payload");
        AddIf(body, tokens, "FUN_00870968", "rebuilds-bitreader");
        AddIf(body, tokens, "PTR_PTR_01977c44", "uses-config-state-map");
        AddIf(body, tokens, "+ 0x6350", "writes-runtime-field-6350");
        AddIf(body, tokens, "+ 0x6354", "writes-runtime-field-6354");
        AddIf(body, tokens, "+ 0x6358", "writes-runtime-field-6358");
        AddIf(body, tokens, "+ 0x6344", "writes-runtime-field-6344");
        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static NativeOwnerForwardContextImplementationScan ScanImplementation(string? sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            return new NativeOwnerForwardContextImplementationScan(
                false,
                sourceRoot is null ? "source root not supplied" : $"source root not found: {sourceRoot}");
        }

        var text = string.Join('\n', Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}PlasmaGameManager.ReTools{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        var implemented =
            text.Contains("owner-forward-context", StringComparison.Ordinal)
            && text.Contains("ApplyNativeSourceClcMoveBoundaryContext", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderWord6Bitstream", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderDeferredPointerWord6Bitstream", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderConfigFallbackWord4Bitstream", StringComparison.Ordinal)
            && text.Contains("ApplyNativeSourceClientCommandContext", StringComparison.Ordinal)
            && text.Contains("source-usercmd", StringComparison.Ordinal)
            && text.Contains("OfficialEatf2ServerDllContracts.CUserCmdStrideBytes", StringComparison.Ordinal);

        return new NativeOwnerForwardContextImplementationScan(
            implemented,
            implemented
                ? "GameManagerState routes decoded owner-forward 008722a0 bitstream boundaries through ApplyNativeSourceClcMoveBoundaryContext and the official CLC_Move/CUserCmd semantic path"
                : "native owner-forward category-5 usercmd context handler markers not found");
    }

    private static string ClassifyForwarderLayout(IReadOnlyCollection<string> tokens)
    {
        if (tokens.Contains("uses-config-state-map", StringComparer.Ordinal))
        {
            return "config-state-or-fallback-word4";
        }

        if (tokens.Contains("stores-temporary-payload-pointer-param2-word13", StringComparer.Ordinal))
        {
            return "deferred-pointer-word6";
        }

        if (tokens.Contains("bit-count-param2-word5", StringComparer.Ordinal)
            && tokens.Contains("reader-target-param2-plus0f", StringComparer.Ordinal))
        {
            return "bitreader-rebuild-word5";
        }

        if (tokens.Contains("bit-count-param2-word6", StringComparer.Ordinal)
            && tokens.Contains("reader-target-param2-plus10", StringComparer.Ordinal))
        {
            return "bitreader-rebuild-word6";
        }

        return "direct-forward";
    }

    private static string BitCountField(IReadOnlyCollection<string> tokens)
    {
        if (tokens.Contains("bit-count-param2-word4", StringComparer.Ordinal))
        {
            return "param_2[4]";
        }

        if (tokens.Contains("bit-count-param2-word5", StringComparer.Ordinal))
        {
            return "param_2[5]";
        }

        if (tokens.Contains("bit-count-param2-word6", StringComparer.Ordinal))
        {
            return "param_2[6]";
        }

        return "";
    }

    private static string BitSource(IReadOnlyCollection<string> tokens)
    {
        if (tokens.Contains("bit-source-param2-plus5", StringComparer.Ordinal))
        {
            return "param_2 + 5";
        }

        if (tokens.Contains("bit-source-param2-plus6", StringComparer.Ordinal))
        {
            return "param_2 + 6";
        }

        if (tokens.Contains("bit-source-param2-plus7", StringComparer.Ordinal))
        {
            return "param_2 + 7";
        }

        if (tokens.Contains("bit-source-param2-plus10", StringComparer.Ordinal))
        {
            return "param_2 + 10";
        }

        return "";
    }

    private static string ReaderTarget(IReadOnlyCollection<string> tokens)
    {
        if (tokens.Contains("reader-target-param2-plus0e", StringComparer.Ordinal))
        {
            return "param_2 + 0xe";
        }

        if (tokens.Contains("reader-target-param2-plus0f", StringComparer.Ordinal))
        {
            return "param_2 + 0xf";
        }

        if (tokens.Contains("reader-target-param2-plus10", StringComparer.Ordinal))
        {
            return "param_2 + 0x10";
        }

        return "";
    }

    private static string MeaningForForwarder(IReadOnlyCollection<string> tokens)
    {
        var layout = ClassifyForwarderLayout(tokens);
        return layout switch
        {
            "bitreader-rebuild-word5" => "Copies param_2[5] bits from param_2 + 6, rebuilds reader state at param_2 + 0xf, then routes into 008722a0.",
            "bitreader-rebuild-word6" => "Copies param_2[6] bits from param_2 + 7, rebuilds reader state at param_2 + 0x10, then routes into 008722a0.",
            "deferred-pointer-word6" => "Copies param_2[6] bits from param_2 + 10, stores the temporary payload pointer in param_2[0x13], then routes into 008722a0.",
            "config-state-or-fallback-word4" => "Attempts a config/state lookup and runtime-field update; if that fails it copies param_2[4] bits from param_2 + 5, rebuilds reader state at param_2 + 0xe, then routes into 008722a0.",
            _ => "Routes the existing payload object into 008722a0 without rebuilding reader state in this wrapper."
        };
    }

    private static Tf2Ps3SourceOwnerForwardContextFunction BuildFunction(ExportedFunction function) =>
        new(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyFunctionRole(function.Address),
            ExtractCalls(function.Body),
            BuildFunctionEvidenceTokens(function),
            Preview(function.Lines));

    private static string ClassifyFunctionRole(string address) =>
        address switch
        {
            "008722a0" => "owner-control-context-router",
            "00872460" => "owner-control-context-table-lookup",
            "00a2b060" => "category-5-usercmd-or-clc-move-route-candidate",
            _ => "owner-forward-wrapper"
        };

    private static string[] BuildFunctionEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "param_1 + 0x528c", "context-table-base-528c");
        AddIf(body, tokens, "param_2 * 0x18 + param_1 + 0x528c", "context-table-lookup-expression");
        AddIf(body, tokens, "param_2 < 6", "context-index-upper-bound-6");
        AddIf(body, tokens, "*param_2 + 0x18", "payload-dispatch-slot18");
        AddIf(body, tokens, "*param_2 + 0x1c", "payload-predicate-slot1c");
        AddIf(body, tokens, "*param_2 + 0x20", "payload-message-id-slot20");
        AddIf(body, tokens, "iVar1 != 0x11", "message-id-0x11-category-4");
        AddIf(body, tokens, "iVar1 != 0xf", "message-id-0x0f-category-3");
        AddIf(body, tokens, "uVar2 ^ 0x1b", "message-id-0x1b-category-5");

        AddIf(body, tokens, "param_1[0x20b0]", "source-queued-client-command-count-20b0");
        AddIf(body, tokens, "FUN_00870968(auStack_80,auStack_177d0,96000,0,-1)", "stack-bitreader-init-96000");
        AddIf(body, tokens, "_opd_FUN_00a2ae20((int)param_1,&local_c0)", "populate-local-bitreader-00a2ae20");
        AddIf(body, tokens, "_opd_FUN_008722a0(*piVar1,&local_c0)", "route-local-bitreader-through-008722a0");
        AddIf(body, tokens, "_opd_FUN_00872460(iVar9,5)", "lookup-context-category-5");
        AddIf(body, tokens, "*(int *)(*(int *)(puVar8[-0x8a2] + 0x1c) + 0x30)", "source-channel-runtime-field-30");
        AddIf(body, tokens, "*(int *)(iVar9 + 8) + 0x98", "category5-handler-table-plus98");
        AddIf(body, tokens, "(*(code *)*puVar8)(iVar9 + 8,param_1,uVar2,uVar3,iVar10", "passes-context5-to-dynamic-handler-98");
        AddIf(body, tokens, "_opd_FUN_00874458((int *)*piVar1,param_2", "post-router-call-00874458");
        AddIf(body, tokens, "(*(code *)**(undefined4 **)(*param_1 + 0xa0))", "post-client-vtable-a0-update");

        foreach (var token in BuildForwarderEvidenceTokens(body))
        {
            tokens.Add(token);
        }

        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement FindOwnerTable(JsonElement root, string baseAddress)
    {
        foreach (var table in root.GetProperty("Tables").EnumerateArray())
        {
            if (ReadString(table, "BaseAddress") == baseAddress)
            {
                return table;
            }
        }

        throw new InvalidOperationException($"Owner table {baseAddress} not found.");
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

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

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
        var text = string.Join('\n', lines.Take(90));
        return text.Length <= 3200 ? text : text[..3200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);

    private sealed record NativeOwnerForwardContextImplementationScan(
        bool Category5UsercmdContextHandlerImplemented,
        string Category5UsercmdContextHandlerEvidence);
}

public sealed record Tf2Ps3SourceOwnerForwardContextReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerForwardContextInputs Inputs,
    Tf2Ps3SourceOwnerForwardContextSummary Summary,
    Tf2Ps3SourceOwnerForwardContextConsumer Category5Consumer,
    Tf2Ps3SourceOwnerForwardContextForwarder[] OwnerForwarders,
    Tf2Ps3SourceOwnerForwardContextFunction[] Functions,
    Tf2Ps3SourceOwnerForwardContextGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerForwardContextInputs(
    string CExport,
    string OwnerVtableMap,
    string OwnerForwardTargetMap);

public sealed record Tf2Ps3SourceOwnerForwardContextSummary(
    int FixedTargetFunctionCount,
    int LocatedFunctionCount,
    int OwnerForwarderSlotCount,
    int DirectForwarderCount,
    int BitreaderRebuildForwarderCount,
    int DeferredPointerForwarderCount,
    int ConfigStateOrFallbackForwarderCount,
    bool UpstreamRouterRecovered,
    bool OwnerForwarderFamilyRecovered,
    bool Category5UsercmdRouteCandidateRecovered,
    bool Context5PassedToDynamicHandler,
    bool NativeOwnerForwardContextHandlersImplemented,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceOwnerForwardContextConsumer(
    string Address,
    string Role,
    string ContextLookup,
    string DynamicHandler,
    string[] Sequence,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceOwnerForwardContextForwarder(
    int SlotIndex,
    string SlotOffset,
    string TableAddress,
    string FunctionAddress,
    string OpdAddress,
    string FunctionName,
    string LayoutKind,
    string BitCountField,
    string BitSource,
    string ReaderTarget,
    string[] EvidenceTokens,
    string[] Calls,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceOwnerForwardContextFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceOwnerForwardContextGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
