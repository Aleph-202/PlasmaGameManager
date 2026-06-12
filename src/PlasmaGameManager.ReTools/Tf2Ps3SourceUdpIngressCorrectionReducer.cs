using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceUdpIngressCorrectionReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceUdpIngressCorrectionReport> ReduceAsync(
        string receivePathReportPath,
        string networkAnchorReportPath,
        string helperSliceContractPath,
        string receiveDispatchSlotsPath,
        string instructionContextPath,
        string outputPath)
    {
        using var receivePathReport = JsonDocument.Parse(await File.ReadAllTextAsync(receivePathReportPath));
        using var networkAnchorReport = JsonDocument.Parse(await File.ReadAllTextAsync(networkAnchorReportPath));
        using var helperSliceReport = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceContractPath));
        using var dispatchSlotsReport = JsonDocument.Parse(await File.ReadAllTextAsync(receiveDispatchSlotsPath));
        using var instructionContextReport = JsonDocument.Parse(await File.ReadAllTextAsync(instructionContextPath));

        var drain = FindObject(receivePathReport.RootElement, "Functions", "Address", "008b8d50");
        var recvWrapper = FindObject(receivePathReport.RootElement, "Functions", "Address", "008b82c0");
        var attachedReader = FindObject(receivePathReport.RootElement, "Functions", "Address", "00a5c2e8");
        var socketOpen = FindObject(networkAnchorReport.RootElement, "functions", "Entry", "008b8668");
        var socketConnect = FindObject(networkAnchorReport.RootElement, "functions", "Entry", "008b8e70");
        var peerAttach = FindObject(helperSliceReport.RootElement, "Slots", "FunctionAddress", "00a5b6c0");
        var drainInstructionContext = FindObject(instructionContextReport.RootElement, "Entries", "Address", "008b8d50");

        var drainPreview = ReadString(drain, "Preview");
        var drainCalls = StringArray(drain, "Calls");
        var drainEvidence = StringArray(drain, "EvidenceTokens");
        var drainFirstBlockCallTargets = CallTargetsBefore(drainInstructionContext, "008b8e44");
        var recvfromCallsBeforeReturn = drainFirstBlockCallTargets.Count(static target => target == "0176969c");
        var drainHasPostRecvConsumer = drainPreview.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal)
            || drainPreview.Contains("_opd_FUN_00a5d9e0", StringComparison.Ordinal)
            || drainPreview.Contains("_opd_FUN_008b82c0", StringComparison.Ordinal)
            || drainPreview.Contains("param_2", StringComparison.Ordinal);
        var drainDiscardProven = drainCalls.Contains("recvfrom", StringComparer.Ordinal)
            && drainEvidence.Contains("0x800", StringComparer.Ordinal)
            && drainEvidence.Contains("0x80", StringComparer.Ordinal)
            && recvfromCallsBeforeReturn >= 2
            && !drainHasPostRecvConsumer;

        var socketConnectEvidence = StringArray(socketConnect, "EvidenceTokens");
        var socketOpenEvidence = StringArray(socketOpen, "EvidenceTokens");
        var peerAttachCalls = StringArray(peerAttach, "Calls");
        var peerAttachEvidence = StringArray(peerAttach, "EvidenceTokens");
        var connectedSocketOpenStoreProven = socketConnectEvidence.Contains("connect(", StringComparer.Ordinal)
            && socketConnectEvidence.Contains("_opd_FUN_008b8668", StringComparer.Ordinal)
            && socketOpenEvidence.Contains("socket(2,2,0x11)", StringComparer.Ordinal)
            && peerAttachCalls.Contains("_opd_FUN_008b8e70", StringComparer.Ordinal)
            && (peerAttachEvidence.Contains("attached-socket-field", StringComparer.Ordinal)
                || ReadString(peerAttach, "Preview").Contains("param_1[0x24] = iVar2", StringComparison.Ordinal));

        var attachedReaderCalls = StringArray(attachedReader, "Calls");
        var attachedReaderEvidence = StringArray(attachedReader, "EvidenceTokens");
        var connectedSocketAttachedReaderProven = attachedReaderCalls.Contains("_opd_FUN_008b82c0", StringComparer.Ordinal)
            && attachedReaderCalls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal)
            && attachedReaderEvidence.Contains("param_1[0x24]", StringComparer.Ordinal);

        var dispatchSummary = dispatchSlotsReport.RootElement.GetProperty("Summary");
        var dispatchSlot70IngressProven = ReadBool(dispatchSummary, "DirectMarkerlessIngressSlot70Proven");
        var nativeReady = false;

        var evidence = new[]
        {
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "008b8d50",
                "_opd_FUN_008b8d50",
                "udp-slot-drain-discard",
                drainDiscardProven ? "proven-negative-for-ingress" : "needs-review",
                "Iterates slot table field +0x08, calls recvfrom into a 0x800 stack buffer with nonblocking flag 0x80, and drops positive reads without handing the buffer to a Source payload dispatcher.",
                drainEvidence,
                Preview(drainPreview)),
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "008b8668",
                "_opd_FUN_008b8668",
                "udp-socket-open-bind",
                socketOpenEvidence.Contains("socket(2,2,0x11)", StringComparer.Ordinal) ? "proven" : "needs-review",
                "Creates/binds the PS3 UDP gameplay socket and configures the socket options used by the connected gameplay path.",
                socketOpenEvidence,
                Preview(ReadString(socketOpen, "SnippetPreview"))),
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "008b8e70",
                "_opd_FUN_008b8e70",
                "connected-udp-socket-open-connect",
                socketConnectEvidence.Contains("connect(", StringComparer.Ordinal) ? "proven" : "needs-review",
                "Closes any existing slot +0x0c socket, opens a UDP socket through 008b8668, stores it as the connected peer socket, and calls connect().",
                socketConnectEvidence,
                Preview(ReadString(socketConnect, "SnippetPreview"))),
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "00a5b6c0",
                "_opd_FUN_00a5b6c0",
                "source-object-connected-socket-opener",
                connectedSocketOpenStoreProven ? "proven" : "needs-review",
                "Calls 008b8e70 from the Source helper slice and stores the connected socket at Source object field param_1[0x24] / object +0x90.",
                peerAttachEvidence,
                Preview(ReadString(peerAttach, "Preview"))),
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "008b82c0",
                "_opd_FUN_008b82c0",
                "connected-socket-recv-wrapper",
                recvWrapper.ValueKind == JsonValueKind.Object ? "proven" : "missing",
                "Wraps recv() for connected gameplay sockets and maps transient PS3 errors 0x23/0x39 to zero-byte no-data reads.",
                StringArray(recvWrapper, "EvidenceTokens"),
                Preview(ReadString(recvWrapper, "Preview"))),
            new Tf2Ps3SourceUdpIngressCorrectionEvidence(
                "00a5c2e8",
                "_opd_FUN_00a5c2e8",
                "connected-socket-attached-frame-reader",
                connectedSocketAttachedReaderProven ? "proven" : "needs-review",
                "Reads from object +0x90 via 008b82c0, consumes visible attached frame kinds, and dispatches type-2 bitstreams to 00a58c10.",
                attachedReaderEvidence,
                Preview(ReadString(attachedReader, "Preview")))
        };

        var gates = new[]
        {
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "udp-drain-discard-path-proven",
                drainDiscardProven ? "proven" : "missing",
                "008b8d50 C export and instruction context before 008b8e44",
                "The unconnected slot +0x08 recvfrom path is a drain/discard loop and should not be used as the live markerless gameplay command parser."),
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "connected-socket-open-store-path-proven",
                connectedSocketOpenStoreProven ? "proven" : "missing",
                "008b8668 -> 008b8e70 -> 00a5b6c0",
                "The live peer socket is opened through the connected UDP path and stored into Source object field +0x90."),
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "connected-socket-attached-reader-path-proven",
                connectedSocketAttachedReaderProven ? "proven" : "missing",
                "00a5b6c0 -> object +0x90 -> 00a5c2e8 -> 008b82c0 -> 00a58c10",
                "Visible attached frames are read through the connected-socket frame reader, not through the 008b8d50 drain buffer."),
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "008b8d50-direct-markerless-consumer",
                drainHasPostRecvConsumer || dispatchSlot70IngressProven ? "proven" : "not-present",
                "008b8d50 recvfrom stack buffer",
                "Current decompile evidence does not show the 008b8d50 stack buffer flowing to 00a5d9e0, 00a58c10, or any other Source message consumer."),
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "hard-markerless-body-to-attached-reader-boundary",
                "missing",
                "PCAP hard markerless bodies versus 00a5c2e8 attached-frame grammar",
                "The remaining 11,874 hard markerless client bodies still need a proven wrapper/boundary before they can be decoded as connected Source input."),
            new Tf2Ps3SourceUdpIngressCorrectionGate(
                "native-source-input-ready",
                nativeReady ? "proven" : "missing",
                "server implementation gate",
                "Native client input remains incomplete until the markerless body boundary is proven and implemented.")
        };

        var report = new Tf2Ps3SourceUdpIngressCorrectionReport(
            "tf2ps3-source-udp-ingress-correction",
            "Corrects the Source gameplay receive model: 008b8d50 is a UDP drain/discard path, while the live connected gameplay receive path is 008b8e70/00a5b6c0/00a5c2e8 through object +0x90.",
            new Tf2Ps3SourceUdpIngressCorrectionInputs(
                receivePathReportPath,
                networkAnchorReportPath,
                helperSliceContractPath,
                receiveDispatchSlotsPath,
                instructionContextPath),
            new Tf2Ps3SourceUdpIngressCorrectionSummary(
                drainDiscardProven,
                drainHasPostRecvConsumer,
                recvfromCallsBeforeReturn,
                "slot + 0x08",
                "slot + 0x0c",
                "object + 0x90 / param_1[0x24]",
                connectedSocketOpenStoreProven,
                connectedSocketAttachedReaderProven,
                !drainHasPostRecvConsumer,
                nativeReady,
                gates.Count(static gate => gate.Status == "missing")),
            evidence,
            gates,
            [
                "Do not model 008b8d50 as the markerless client command ingress; it is currently proven only as a drain/discard loop over the unconnected slot +0x08 sockets.",
                "The live gameplay socket path is connected UDP: 008b8668 opens/binds, 008b8e70 connects/stores slot +0x0c, 00a5b6c0 stores that socket into Source object +0x90, and 00a5c2e8 reads frames from it.",
                "The next native reverse-engineering target is the boundary that turns hard markerless PCAP client bodies into the connected attached-frame/bitreader stream, not a post-recvfrom consumer in 008b8d50."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static JsonElement FindObject(JsonElement root, string arrayPropertyName, string keyPropertyName, string value)
    {
        if (!root.TryGetProperty(arrayPropertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(keyPropertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && property.GetString() == value)
            {
                return item.Clone();
            }
        }

        return default;
    }

    private static string[] CallTargetsBefore(JsonElement context, string exclusiveEndAddress)
    {
        if (context.ValueKind != JsonValueKind.Object
            || !context.TryGetProperty("Instructions", out var instructions)
            || instructions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var targets = new List<string>();
        foreach (var instruction in instructions.EnumerateArray())
        {
            var address = ReadString(instruction, "Address");
            if (string.Compare(address, exclusiveEndAddress, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                break;
            }

            var flowType = ReadString(instruction, "FlowType");
            if (!flowType.Contains("CALL", StringComparison.Ordinal))
            {
                continue;
            }

            if (!instruction.TryGetProperty("Flows", out var flows) || flows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            targets.AddRange(flows.EnumerateArray()
                .Where(static flow => flow.ValueKind == JsonValueKind.String)
                .Select(static flow => flow.GetString() ?? "")
                .Where(static flow => flow.Length > 0));
        }

        return targets.ToArray();
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
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? "")
            .ToArray();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static string Preview(string text) =>
        text.Length <= 2200 ? text : text[..2200];
}

public sealed record Tf2Ps3SourceUdpIngressCorrectionReport(
    string Status,
    string Note,
    Tf2Ps3SourceUdpIngressCorrectionInputs Inputs,
    Tf2Ps3SourceUdpIngressCorrectionSummary Summary,
    Tf2Ps3SourceUdpIngressCorrectionEvidence[] Evidence,
    Tf2Ps3SourceUdpIngressCorrectionGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceUdpIngressCorrectionInputs(
    string SourceReceivePathReport,
    string SourceNetworkAnchorReport,
    string SourceHelperSliceContractReport,
    string SourceReceiveDispatchSlotsReport,
    string SourceReceiveInstructionContextReport);

public sealed record Tf2Ps3SourceUdpIngressCorrectionSummary(
    bool UdpDrainDiscardPathProven,
    bool DrainHasPostRecvConsumer,
    int DrainRecvfromCallCountBeforeFunctionReturn,
    string DrainSocketSlotField,
    string ConnectedSocketSlotField,
    string SourceObjectConnectedSocketField,
    bool ConnectedSocketOpenStorePathProven,
    bool ConnectedSocketAttachedReaderPathProven,
    bool MarkerlessTargetRedirectedFromRecvfromDrain,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceUdpIngressCorrectionEvidence(
    string Address,
    string Name,
    string Role,
    string Status,
    string Meaning,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceUdpIngressCorrectionGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
