using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceSlot70Param2BuilderReducer
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

    public static async Task<Tf2Ps3SourceSlot70Param2BuilderReport> ReduceAsync(
        string cExportPath,
        string helperSliceContractPath,
        string helperSliceOpdRefsPath,
        string sourceReceiveDispatchSlotsPath,
        string payloadDispatchBoundaryPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        using var helperSlice = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceContractPath));
        using var opdRefs = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceOpdRefsPath));
        using var receiveSlots = JsonDocument.Parse(await File.ReadAllTextAsync(sourceReceiveDispatchSlotsPath));
        using var payloadBoundary = JsonDocument.Parse(await File.ReadAllTextAsync(payloadDispatchBoundaryPath));

        var slot70 = FindObjectByProperty(helperSlice.RootElement, "FunctionAddress", "00a5d9e0");
        var opdRef = FindOpdReference(opdRefs.RootElement, "0190e530");
        var payloadSummary = payloadBoundary.RootElement.GetProperty("Summary");
        var directOrdinaryCallers = ReadInt(payloadSummary, "DirectBitreaderTransformCallerCount");
        var sourceRelevantSlot70Callsites = ReadSourceRelevantSlot70Callsites(receiveSlots.RootElement);
        var setupOnlyCallsites = sourceRelevantSlot70Callsites
            .Where(static callsite => callsite.Status.Contains("setup", StringComparison.Ordinal)
                || callsite.Role.Contains("setup", StringComparison.Ordinal)
                || callsite.Role.Contains("association", StringComparison.Ordinal)
                || callsite.Meaning.Contains("not direct markerless ingress", StringComparison.Ordinal))
            .ToArray();

        var slot70Function = functions.Single(static function => function.Address == "00a5d9e0");
        var bitreaderShapeCandidates = BuildBitreaderShapeCandidates(functions)
            .OrderByDescending(static candidate => candidate.SourceRelevanceScore)
            .ThenBy(static candidate => candidate.Address, StringComparer.Ordinal)
            .Take(40)
            .ToArray();
        var falsePositiveClusters = BuildFalsePositiveClusters(functions);

        var directRefCount = ReadInt(opdRef, "ReferenceHitCount");
        var scalarHitCount = ReadInt(opdRef, "ScalarHitCount");
        var sourceRelevantInputBuilderCandidates = bitreaderShapeCandidates
            .Where(static candidate => candidate.Classification is "source-relevant-not-param2-builder" or "slot70-wrapper-not-builder")
            .ToArray();

        var param2ConstructionSiteRecovered = false;
        var nativeSourceInputReady = false;
        var gates = new[]
        {
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "helper-slot-0x70-data-only",
                directRefCount == 1 && scalarHitCount == 0 ? "proven" : "needs-review",
                "source-helper-slice-opd-refs.json",
                "The slot +0x70 target is referenced as OPD table data, not as an ordinary direct call target."),
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "ordinary-direct-call-to-00a5d9e0",
                directOrdinaryCallers == 0 ? "none-found" : "candidate",
                "source-payload-dispatch-boundary.json",
                "The C export still shows no normal caller that constructs param_2 and invokes 00a5d9e0 directly."),
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "source-relevant-slot70-ingress",
                sourceRelevantSlot70Callsites.Length == setupOnlyCallsites.Length ? "missing" : "candidate",
                "source-receive-dispatch-slots.json",
                "Known Source-relevant slot +0x70 callsites are setup/association paths; no live markerless input ingress callsite is proven."),
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "generic-bitreader-field-false-positives-filtered",
                falsePositiveClusters.Length > 0 ? "proven" : "needs-review",
                "TF.elf C export bitreader-shape scan",
                "The same param_2[0xb..0xe] layout appears in unrelated systems, so raw field scans cannot identify the native input builder alone."),
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "param2-construction-site",
                param2ConstructionSiteRecovered ? "proven" : "missing",
                "targeted caller/dataflow around vtable slot +0x70",
                "Still need the native code path that materializes the caller-supplied bitreader object consumed as param_2 by 00a5d9e0."),
            new Tf2Ps3SourceSlot70Param2BuilderGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "The native replacement cannot claim full Source input support until markerless client packets map into a recovered TF.elf input builder.")
        };

        var report = new Tf2Ps3SourceSlot70Param2BuilderReport(
            "tf2ps3-source-slot70-param2-builder",
            "Narrows the remaining TF2 PS3 native Source input blocker: 00a5d9e0 is a registered slot +0x70 bitreader wrapper, but the caller-side param_2 construction site is not recovered yet.",
            new Tf2Ps3SourceSlot70Param2BuilderInputs(
                cExportPath,
                helperSliceContractPath,
                helperSliceOpdRefsPath,
                sourceReceiveDispatchSlotsPath,
                payloadDispatchBoundaryPath),
            new Tf2Ps3SourceSlot70Param2BuilderSummary(
                "00a5d9e0",
                ReadString(slot70, "OpdAddress"),
                ReadString(slot70, "TableAddress"),
                ReadString(slot70, "VisibleSliceOffset"),
                ReadString(slot70, "FullSliceOffset"),
                directOrdinaryCallers,
                directRefCount,
                scalarHitCount,
                sourceRelevantSlot70Callsites.Length,
                setupOnlyCallsites.Length,
                bitreaderShapeCandidates.Length,
                sourceRelevantInputBuilderCandidates.Length,
                falsePositiveClusters.Length,
                param2ConstructionSiteRecovered,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "candidate" or "needs-review")),
            new Tf2Ps3SourceSlot70Wrapper(
                slot70Function.Address,
                slot70Function.Name,
                ReadString(slot70, "VisibleSliceOffset"),
                ReadString(slot70, "OpdAddress"),
                ReadString(slot70, "TableAddress"),
                [
                    "param_2 + 7",
                    "param_2[0xb]",
                    "param_2[0xc]",
                    "param_2[0xd]",
                    "param_2[0xe]",
                    "param_2[0x11] + 0x1c",
                    "_opd_FUN_00a58c10",
                    "_opd_FUN_00a5d720"
                ],
                Preview(slot70Function)),
            new Tf2Ps3SourceSlot70OpdReferenceProof(
                NormalizeHex(ReadString(slot70, "OpdAddress")),
                directRefCount,
                scalarHitCount,
                ReferenceHitRows(opdRef)),
            sourceRelevantSlot70Callsites,
            bitreaderShapeCandidates,
            falsePositiveClusters,
            gates,
            [
                "00a5d9e0 is the downstream slot +0x70 wrapper, not the input builder itself.",
                "The only OPD reference for 00a5d9e0 is the helper-slice table cell at 0180ca30; ordinary C-export caller search remains empty.",
                "The two Source-relevant slot +0x70 callsites currently recovered are setup/association calls, so they cannot explain live markerless client input.",
                "The next productive reverse-engineering target is targeted Ghidra/dataflow around the object or vtable path that prepares the param_2 bitreader before slot +0x70 dispatch."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceBitreaderShapeCandidate[] BuildBitreaderShapeCandidates(IReadOnlyList<ExportedFunction> functions)
    {
        var candidates = new List<Tf2Ps3SourceBitreaderShapeCandidate>();
        foreach (var function in functions)
        {
            var tokens = BuildBitreaderTokens(function);
            if (tokens.Length < 3
                && !function.Body.Contains("param_2 + 7", StringComparison.Ordinal)
                && !function.Body.Contains("((int)param_2 + 7)", StringComparison.Ordinal))
            {
                continue;
            }

            var classification = ClassifyBitreaderCandidate(function);
            var relevance = SourceRelevanceScore(function, tokens, classification);
            candidates.Add(new Tf2Ps3SourceBitreaderShapeCandidate(
                function.Address,
                function.Name,
                classification,
                relevance,
                tokens,
                BuildSourceTokens(function),
                MeaningForClassification(classification),
                Preview(function)));
        }

        return candidates.ToArray();
    }

    private static string[] BuildBitreaderTokens(ExportedFunction function)
    {
        var tokens = new List<string>();
        AddIf(function.Body, tokens, "param_2[0xb]", "word-or-x-field-0xb");
        AddIf(function.Body, tokens, "param_2[0xc]", "bit-count-or-y-field-0xc");
        AddIf(function.Body, tokens, "param_2[0xd]", "cursor-or-z-field-0xd");
        AddIf(function.Body, tokens, "param_2[0xe]", "end-or-w-field-0xe");
        AddIf(function.Body, tokens, "param_2[0x11]", "payload-pointer-or-generic-field-0x11");
        AddIf(function.Body, tokens, "param_2 + 7", "param2-plus-7");
        AddIf(function.Body, tokens, "((int)param_2 + 7)", "param2-byte-plus-7");
        AddIf(function.Body, tokens, "FUN_0086de68", "bitbuffer-init-helper");
        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] BuildSourceTokens(ExportedFunction function)
    {
        var tokens = new List<string>();
        AddIf(function.Body, tokens, "_opd_FUN_00a58c10", "calls-native-source-payload-dispatcher");
        AddIf(function.Body, tokens, "_opd_FUN_00a5d720", "calls-buffered-subpayload-dispatcher");
        AddIf(function.Body, tokens, "param_1[0x782]", "source-owner-field-0x782");
        AddIf(function.Body, tokens, "PTR_PTR_01977d5c", "global-source-payload-queue");
        AddIf(function.Body, tokens, "_opd_FUN_008b82c0", "connected-socket-read");
        AddIf(function.Body, tokens, "HudMenuSpyDisguise", "hud-spy-disguise-ui");
        AddIf(function.Body, tokens, "CHudMenuSpyDisguise", "hud-spy-disguise-class");
        AddIf(function.Body, tokens, "+ 0x70", "virtual-slot-0x70-text");
        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string ClassifyBitreaderCandidate(ExportedFunction function)
    {
        if (function.Address == "00a5d9e0")
        {
            return "slot70-wrapper-not-builder";
        }

        if (function.Body.Contains("HudMenuSpyDisguise", StringComparison.Ordinal)
            || function.Body.Contains("CHudMenuSpyDisguise", StringComparison.Ordinal))
        {
            return "ui-false-positive";
        }

        if (function.Body.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal)
            || function.Body.Contains("_opd_FUN_00a5d720", StringComparison.Ordinal)
            || function.Body.Contains("param_1[0x782]", StringComparison.Ordinal))
        {
            return "source-relevant-not-param2-builder";
        }

        if (function.Body.Contains("FUN_0086de68", StringComparison.Ordinal))
        {
            return "generic-bitbuffer-helper";
        }

        return "generic-struct-field-shape";
    }

    private static int SourceRelevanceScore(ExportedFunction function, string[] tokens, string classification)
    {
        var score = tokens.Length;
        if (classification == "slot70-wrapper-not-builder")
        {
            score += 100;
        }
        else if (classification == "source-relevant-not-param2-builder")
        {
            score += 40;
        }
        else if (classification == "ui-false-positive")
        {
            score -= 20;
        }

        if (function.Body.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal))
        {
            score += 20;
        }

        if (function.Body.Contains("+ 0x70", StringComparison.Ordinal))
        {
            score += 5;
        }

        return score;
    }

    private static string MeaningForClassification(string classification) => classification switch
    {
        "slot70-wrapper-not-builder" =>
            "This is the callee that consumes param_2; it proves the downstream wrapper but not the caller-side construction site.",
        "source-relevant-not-param2-builder" =>
            "Source-adjacent code using the same bitbuffer shape, but not a proven markerless client input builder.",
        "ui-false-positive" =>
            "Unrelated TF HUD/UI code that uses the same field offsets and must be excluded from Source transport recovery.",
        "generic-bitbuffer-helper" =>
            "Generic bitbuffer helper shape without enough Source ownership or ingress evidence.",
        _ =>
            "Generic struct field shape; field offsets alone are not enough to identify Source packet semantics."
    };

    private static Tf2Ps3SourceSlot70FalsePositiveCluster[] BuildFalsePositiveClusters(IReadOnlyList<ExportedFunction> functions)
    {
        var hudFunctions = functions
            .Where(static function => function.Body.Contains("HudMenuSpyDisguise", StringComparison.Ordinal)
                || function.Body.Contains("CHudMenuSpyDisguise", StringComparison.Ordinal))
            .Select(static function => function.Address)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (hudFunctions.Length == 0)
        {
            return [];
        }

        return
        [
            new Tf2Ps3SourceSlot70FalsePositiveCluster(
                "hud-spy-disguise",
                "CHudMenuSpyDisguise / HudMenuSpyDisguise",
                hudFunctions,
                "The TF UI cluster contains param_2/puVar10 field patterns around the same offsets used by bitbuffer-like code. It proves raw field scans are noisy and cannot be used as native Source input proof.")
        ];
    }

    private static Tf2Ps3SourceSlot70Callsite[] ReadSourceRelevantSlot70Callsites(JsonElement root)
    {
        if (!root.TryGetProperty("SourceRelevantCallsites", out var callsites)
            || callsites.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return callsites.EnumerateArray()
            .Where(static callsite => StringArray(callsite, "SlotOffsets").Contains("0x70", StringComparer.Ordinal))
            .Select(static callsite => new Tf2Ps3SourceSlot70Callsite(
                ReadString(callsite, "Address"),
                ReadString(callsite, "Name"),
                ReadString(callsite, "Role"),
                ReadString(callsite, "Status"),
                StringArray(callsite, "SlotOffsets"),
                StringArray(callsite, "CallExpressions"),
                StringArray(callsite, "EvidenceTokens"),
                ReadString(callsite, "Meaning"),
                ReadString(callsite, "Preview")))
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonElement FindOpdReference(JsonElement root, string address)
    {
        if (root.TryGetProperty("Targets", out var targets) && targets.ValueKind == JsonValueKind.Array)
        {
            foreach (var target in targets.EnumerateArray())
            {
                var value = NormalizeHex(ReadString(target, "Address"));
                if (value.EndsWith(address, StringComparison.OrdinalIgnoreCase))
                {
                    return target.Clone();
                }
            }
        }

        return default;
    }

    private static JsonElement FindObjectByProperty(JsonElement root, string propertyName, string value)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() == value)
            {
                return root.Clone();
            }

            foreach (var child in root.EnumerateObject())
            {
                var result = FindObjectByProperty(child.Value, propertyName, value);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    return result;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in root.EnumerateArray())
            {
                var result = FindObjectByProperty(child, propertyName, value);
                if (result.ValueKind != JsonValueKind.Undefined)
                {
                    return result;
                }
            }
        }

        return default;
    }

    private static Tf2Ps3SourceSlot70OpdReferenceHit[] ReferenceHitRows(JsonElement target)
    {
        if (target.ValueKind != JsonValueKind.Object
            || !target.TryGetProperty("ReferenceHits", out var hits)
            || hits.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return hits.EnumerateArray()
            .Select(static hit => new Tf2Ps3SourceSlot70OpdReferenceHit(
                ReadString(hit, "From"),
                ReadString(hit, "Type"),
                ReadString(hit, "Source"),
                ReadString(hit, "FunctionEntry"),
                ReadString(hit, "FunctionName")))
            .ToArray();
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string[] StringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "")
            .Where(static item => item.Length > 0)
            .ToArray();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;

    private static string NormalizeHex(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value.ToLowerInvariant()
            : "0x" + value.ToLowerInvariant();
    }

    private static string Preview(ExportedFunction function)
    {
        var text = string.Join('\n', function.Lines.Take(80));
        return text.Length <= 2600 ? text : text[..2600];
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

public sealed record Tf2Ps3SourceSlot70Param2BuilderReport(
    string Status,
    string Note,
    Tf2Ps3SourceSlot70Param2BuilderInputs Inputs,
    Tf2Ps3SourceSlot70Param2BuilderSummary Summary,
    Tf2Ps3SourceSlot70Wrapper Slot70Wrapper,
    Tf2Ps3SourceSlot70OpdReferenceProof OpdReferenceProof,
    Tf2Ps3SourceSlot70Callsite[] SourceRelevantSlot70Callsites,
    Tf2Ps3SourceBitreaderShapeCandidate[] BitreaderShapeCandidates,
    Tf2Ps3SourceSlot70FalsePositiveCluster[] FalsePositiveClusters,
    Tf2Ps3SourceSlot70Param2BuilderGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceSlot70Param2BuilderInputs(
    string CExportInput,
    string SourceHelperSliceContract,
    string SourceHelperSliceOpdRefs,
    string SourceReceiveDispatchSlots,
    string SourcePayloadDispatchBoundary);

public sealed record Tf2Ps3SourceSlot70Param2BuilderSummary(
    string Slot70FunctionAddress,
    string Slot70OpdAddress,
    string Slot70TableAddress,
    string Slot70VisibleSliceOffset,
    string Slot70FullSliceOffset,
    int DirectOrdinaryCallerCount,
    int DirectOpdReferenceCount,
    int OpdScalarHitCount,
    int SourceRelevantSlot70CallsiteCount,
    int SetupOnlySourceRelevantSlot70CallsiteCount,
    int BitreaderShapeCandidateCount,
    int SourceRelevantBitreaderShapeCandidateCount,
    int FalsePositiveClusterCount,
    bool Param2ConstructionSiteRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceSlot70Wrapper(
    string Address,
    string Name,
    string SlotOffset,
    string OpdAddress,
    string TableAddress,
    string[] ConsumedParam2Evidence,
    string Preview);

public sealed record Tf2Ps3SourceSlot70OpdReferenceProof(
    string OpdAddress,
    int ReferenceHitCount,
    int ScalarHitCount,
    Tf2Ps3SourceSlot70OpdReferenceHit[] ReferenceHits);

public sealed record Tf2Ps3SourceSlot70OpdReferenceHit(
    string From,
    string Type,
    string Source,
    string FunctionEntry,
    string FunctionName);

public sealed record Tf2Ps3SourceSlot70Callsite(
    string Address,
    string Name,
    string Role,
    string Status,
    string[] SlotOffsets,
    string[] CallExpressions,
    string[] EvidenceTokens,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceBitreaderShapeCandidate(
    string Address,
    string Name,
    string Classification,
    int SourceRelevanceScore,
    string[] BitreaderShapeTokens,
    string[] SourceTokens,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceSlot70FalsePositiveCluster(
    string Id,
    string Label,
    string[] FunctionAddresses,
    string Meaning);

public sealed record Tf2Ps3SourceSlot70Param2BuilderGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
