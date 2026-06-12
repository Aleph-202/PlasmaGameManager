using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePayloadDispatchBoundaryReducer
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

    public static async Task<Tf2Ps3SourcePayloadDispatchBoundaryReport> ReduceAsync(
        string cExportPath,
        string playerVtableMapPath,
        string connectedWrapperBoundaryPath,
        string markerlessBoundaryHypothesesPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        using var playerVtableMap = JsonDocument.Parse(await File.ReadAllTextAsync(playerVtableMapPath));
        using var connectedWrapper = JsonDocument.Parse(await File.ReadAllTextAsync(connectedWrapperBoundaryPath));
        using var markerlessBoundary = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessBoundaryHypothesesPath));

        var dispatcherCallers = functions
            .Where(static function => function.Address != "00a58c10")
            .Select(function => BuildDispatchEntryPoint(function, "_opd_FUN_00a58c10"))
            .Where(static entry => entry.DirectCallLines.Length > 0)
            .OrderBy(static entry => entry.Address, StringComparer.Ordinal)
            .ToArray();
        var directD9e0Callers = functions
            .Where(static function => function.Address != "00a5d9e0")
            .Select(function => BuildDirectCallsite(function, "_opd_FUN_00a5d9e0"))
            .Where(static callsite => callsite.DirectCallLines.Length > 0)
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();
        var vtableEvidence = new[]
        {
            BuildVtableSlot(playerVtableMap.RootElement, "00a5b4f0", "slot-0x68-queued-global-payload-drain"),
            BuildVtableSlot(playerVtableMap.RootElement, "00a5c2e8", "slot-0x6c-connected-attached-frame-reader"),
            BuildVtableSlot(playerVtableMap.RootElement, "00a5d9e0", "slot-0x70-bitreader-wrapper-candidate")
        };

        var markerlessSummary = markerlessBoundary.RootElement.GetProperty("Summary");
        var connectedSummary = connectedWrapper.RootElement.GetProperty("Summary");
        var queuedDrainRecovered = dispatcherCallers.Any(static entry => entry.Address == "00a5b4f0");
        var attachedFrameRecovered = dispatcherCallers.Any(static entry => entry.Address == "00a5c2e8")
            && ReadBool(connectedSummary, "AttachedFrameReaderContractProven");
        var bufferedSubpayloadRecovered = dispatcherCallers.Any(static entry => entry.Address == "00a5d720");
        var bitreaderWrapperRecovered = dispatcherCallers.Any(static entry => entry.Address == "00a5d9e0")
            && vtableEvidence.Any(static slot => slot.FunctionAddress == "00a5d9e0" && slot.SlotOffset == "0x00000070");
        var hardBodiesAtVisibleBoundary = ReadInt(markerlessSummary, "HardRawAttachedType2LittleEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardRawAttachedType2BigEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardEmbeddedAttachedType2LittleEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardEmbeddedAttachedType2BigEndianExactHitCount") > 0;

        var gates = new[]
        {
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "all-direct-payload-dispatch-entrypoints-named",
                dispatcherCallers.Length == 4
                    && queuedDrainRecovered
                    && attachedFrameRecovered
                    && bufferedSubpayloadRecovered
                    && bitreaderWrapperRecovered
                        ? "proven"
                        : "needs-review",
                "TF.elf C export callers of 00a58c10",
                "All currently visible direct payload-dispatch entrypoints are classified by source boundary."),
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "slot-0x70-bitreader-wrapper-registered",
                bitreaderWrapperRecovered ? "candidate-proven" : "missing",
                "source-player-vtable-map.json",
                "00a5d9e0 is registered as player helper/vtable slot +0x70 and dispatches param_2 + 7 to 00a58c10."),
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "direct-ordinary-callers-to-00a5d9e0",
                directD9e0Callers.Length == 0 ? "none-found" : "candidate",
                "TF.elf C export direct call scan",
                "The C export has no ordinary direct caller for 00a5d9e0; the missing ingress is a vtable/constructed-bitreader path, not a normal call edge."),
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "hard-pcap-bodies-at-visible-dispatch-entrypoint",
                hardBodiesAtVisibleBoundary ? "candidate" : "ruled-out",
                "artifacts/pcap-markerless-boundary-hypotheses.json",
                "The hard markerless bodies do not align with the visible attached-frame type-2 dispatch entrypoint."),
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "slot-0x70-param2-construction-site",
                "missing",
                "caller of vtable slot +0x70 / bitreader object builder",
                "Still need the native construction site that supplies the param_2 bitreader consumed by 00a5d9e0."),
            new Tf2Ps3SourcePayloadDispatchBoundaryGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "Native input cannot be called complete until the hard markerless body is decoded into one of the named dispatch entrypoints.")
        };

        var report = new Tf2Ps3SourcePayloadDispatchBoundaryReport(
            "tf2ps3-source-payload-dispatch-boundary",
            "Classifies every visible TF.elf entrypoint that calls the native Source payload dispatcher 00a58c10 and narrows the remaining markerless client input target to the slot +0x70 bitreader construction site.",
            new Tf2Ps3SourcePayloadDispatchBoundaryInputs(
                cExportPath,
                playerVtableMapPath,
                connectedWrapperBoundaryPath,
                markerlessBoundaryHypothesesPath),
            new Tf2Ps3SourcePayloadDispatchBoundarySummary(
                ReadInt(markerlessSummary, "HardOpaqueMarkerlessPacketCount"),
                dispatcherCallers.Length,
                directD9e0Callers.Length,
                queuedDrainRecovered,
                attachedFrameRecovered,
                bufferedSubpayloadRecovered,
                bitreaderWrapperRecovered,
                vtableEvidence.Single(slot => slot.FunctionAddress == "00a5d9e0").SlotOffset,
                false,
                false,
                gates.Count(static gate => gate.Status is "missing" or "candidate" or "candidate-proven" or "needs-review")),
            dispatcherCallers,
            directD9e0Callers,
            vtableEvidence,
            gates,
            [
                "There are four visible direct entrypoints into 00a58c10: queued global payload drain 00a5b4f0, connected attached-frame reader 00a5c2e8, buffered subpayload dispatcher 00a5d720, and slot +0x70 bitreader wrapper 00a5d9e0.",
                "00a5d9e0 has no ordinary direct caller in the C export; it is reached through the player helper/vtable slot +0x70.",
                "The hard markerless PCAP bodies are not at the visible 00a5c2e8 type-2 boundary, so the next native target is the code that constructs the param_2 bitreader object before invoking slot +0x70.",
                "Implementation consequence: build server input only from named dispatch boundaries; keep markerless packets behind the incomplete wrapper gate until slot +0x70 param_2 construction is recovered."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourcePayloadDispatchEntryPoint BuildDispatchEntryPoint(ExportedFunction function, string target)
    {
        var directCallLines = DirectCallLines(function, target);
        var (role, status, argument, sourceBoundary, meaning) = ClassifyDispatchEntry(function);
        return new Tf2Ps3SourcePayloadDispatchEntryPoint(
            function.Address,
            function.Name,
            role,
            status,
            argument,
            sourceBoundary,
            directCallLines,
            BuildEvidenceTokens(function),
            meaning,
            Preview(function));
    }

    private static Tf2Ps3SourcePayloadDispatchDirectCallsite BuildDirectCallsite(ExportedFunction function, string target) =>
        new(function.Address, function.Name, DirectCallLines(function, target), Preview(function));

    private static (string Role, string Status, string Argument, string SourceBoundary, string Meaning) ClassifyDispatchEntry(ExportedFunction function) =>
        function.Address switch
        {
            "00a5b4f0" => (
                "queued-global-payload-drain",
                "recovered",
                "iVar2 + 0x1c",
                "PTR_PTR_01977d5c queue object, virtual slot +0x48 returns queued payload object",
                "Drains globally queued native payload objects and dispatches their body at payload +0x1c."),
            "00a5c2e8" => (
                "connected-attached-frame-type2",
                "recovered",
                "&local_258",
                "object +0x90 connected socket, frame kind 2, staged payload at object +0x544",
                "Reads visible connected attached frames and dispatches completed type-2 payloads."),
            "00a5d720" => (
                "buffered-subpayload-dispatcher",
                "recovered",
                "&local_50",
                "per-channel buffered payload at param_1 + param_2*0x4c + 0x7c/0x7d",
                "Dispatches a buffered subpayload after optional file/cache handling; used by the slot +0x70 bitreader wrapper."),
            "00a5d9e0" => (
                "slot-0x70-bitreader-wrapper",
                "candidate-recovered",
                "param_2 + 7",
                "caller-supplied bitreader object; construction site still missing",
                "Consumes optional bitreader subpayload controls and dispatches remaining bits through the native Source dispatcher."),
            _ => (
                "unclassified-dispatcher-caller",
                "needs-review",
                "unknown",
                "unknown",
                "Calls 00a58c10 but has not been classified.")
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var tokens = new List<string>();
        AddIf(function.Body, tokens, "PTR_PTR_01977d5c", "global-queued-payload-source");
        AddIf(function.Body, tokens, "+ 0x48", "queue-virtual-slot-0x48");
        AddIf(function.Body, tokens, "iVar2 + 0x1c", "queued-payload-body-plus-0x1c");
        AddIf(function.Body, tokens, "_opd_FUN_008b82c0", "connected-socket-read");
        AddIf(function.Body, tokens, "param_1[0x151]", "attached-staging-buffer-object-0x544");
        AddIf(function.Body, tokens, "local_258", "attached-frame-bitreader-local");
        AddIf(function.Body, tokens, "param_1[param_2 * 0x4c + 0x7c]", "buffered-subpayload-pointer");
        AddIf(function.Body, tokens, "FUN_0086de68((int)&local_50", "buffered-subpayload-bitreader-init");
        AddIf(function.Body, tokens, "_opd_FUN_00a5d720", "slot70-calls-buffered-subpayload-dispatcher");
        AddIf(function.Body, tokens, "param_2 + 7", "slot70-dispatches-param2-plus-7");
        AddIf(function.Body, tokens, "param_2[0xb]", "slot70-bitreader-word-field");
        AddIf(function.Body, tokens, "param_2[0xc]", "slot70-bitreader-bit-count-field");
        AddIf(function.Body, tokens, "param_2[0xd]", "slot70-bitreader-cursor-field");
        AddIf(function.Body, tokens, "param_2[0xe]", "slot70-bitreader-end-field");
        AddIf(function.Body, tokens, "_opd_FUN_00a594e8", "slot70-optional-subpayload-bit-handler");
        AddIf(function.Body, tokens, "_opd_FUN_00a5aa00", "slot70-optional-selector");
        AddIf(function.Body, tokens, "param_2[0x11] + 0x1c", "slot70-owner-callback-payload-offset");
        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] DirectCallLines(ExportedFunction function, string target) =>
        function.Lines
            .Select(static line => line.Trim())
            .Where(line => line.Contains(target, StringComparison.Ordinal)
                && !line.StartsWith("void ", StringComparison.Ordinal)
                && !line.StartsWith("uint ", StringComparison.Ordinal)
                && !line.StartsWith("undefined", StringComparison.Ordinal)
                && !line.StartsWith("bool ", StringComparison.Ordinal)
                && !line.StartsWith("byte ", StringComparison.Ordinal)
                && !line.StartsWith("int ", StringComparison.Ordinal))
            .ToArray();

    private static Tf2Ps3SourcePayloadDispatchVtableSlot BuildVtableSlot(JsonElement root, string functionAddress, string role)
    {
        var matches = new List<JsonElement>();
        CollectObjectsByProperty(root, "FunctionAddress", functionAddress, matches);
        var match = matches.FirstOrDefault();
        return new Tf2Ps3SourcePayloadDispatchVtableSlot(
            functionAddress,
            role,
            ReadString(match, "SlotOffset"),
            ReadInt(match, "SlotIndex"),
            ReadString(match, "OpdAddress"),
            ReadString(match, "Role"),
            StringArray(match, "Calls"),
            StringArray(match, "EvidenceTokens"));
    }

    private static void CollectObjectsByProperty(JsonElement element, string propertyName, string value, List<JsonElement> output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() == value)
            {
                output.Add(element.Clone());
            }

            foreach (var child in element.EnumerateObject())
            {
                CollectObjectsByProperty(child.Value, propertyName, value, output);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                CollectObjectsByProperty(child, propertyName, value, output);
            }
        }
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

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

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

public sealed record Tf2Ps3SourcePayloadDispatchBoundaryReport(
    string Status,
    string Note,
    Tf2Ps3SourcePayloadDispatchBoundaryInputs Inputs,
    Tf2Ps3SourcePayloadDispatchBoundarySummary Summary,
    Tf2Ps3SourcePayloadDispatchEntryPoint[] DispatchEntryPoints,
    Tf2Ps3SourcePayloadDispatchDirectCallsite[] DirectBitreaderTransformCallers,
    Tf2Ps3SourcePayloadDispatchVtableSlot[] VtableSlots,
    Tf2Ps3SourcePayloadDispatchBoundaryGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourcePayloadDispatchBoundaryInputs(
    string CExportInput,
    string SourcePlayerVtableMap,
    string SourceConnectedWrapperBoundary,
    string MarkerlessBoundaryHypotheses);

public sealed record Tf2Ps3SourcePayloadDispatchBoundarySummary(
    int HardOpaqueMarkerlessPacketCount,
    int DirectPayloadDispatcherCallerCount,
    int DirectBitreaderTransformCallerCount,
    bool QueuedGlobalPayloadDrainRecovered,
    bool ConnectedAttachedFrameDispatchRecovered,
    bool BufferedSubpayloadDispatchRecovered,
    bool Slot70BitreaderWrapperRecovered,
    string Slot70FunctionOffset,
    bool Slot70Param2ConstructionSiteRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourcePayloadDispatchEntryPoint(
    string Address,
    string Name,
    string Role,
    string Status,
    string DispatcherArgument,
    string SourceBoundary,
    string[] DirectCallLines,
    string[] EvidenceTokens,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourcePayloadDispatchDirectCallsite(
    string Address,
    string Name,
    string[] DirectCallLines,
    string Preview);

public sealed record Tf2Ps3SourcePayloadDispatchVtableSlot(
    string FunctionAddress,
    string Role,
    string SlotOffset,
    int SlotIndex,
    string OpdAddress,
    string SourceRole,
    string[] Calls,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourcePayloadDispatchBoundaryGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
