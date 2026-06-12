using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceClientReceiveContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceClientReceiveContractReport> ReduceAsync(
        string receivePathPath,
        string helperSlicePath,
        string markerlessClassifierPath,
        string markerlessTransformPath,
        string outputPath)
    {
        using var receivePath = JsonDocument.Parse(await File.ReadAllTextAsync(receivePathPath));
        using var helperSlice = JsonDocument.Parse(await File.ReadAllTextAsync(helperSlicePath));
        using var markerlessClassifier = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessClassifierPath));
        using var markerlessTransform = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessTransformPath));

        var receiveRoot = receivePath.RootElement;
        var helperRoot = helperSlice.RootElement;
        var classifierSummary = markerlessClassifier.RootElement.GetProperty("Summary");
        var transformSummary = markerlessTransform.RootElement.GetProperty("Summary");

        var functions = receiveRoot.GetProperty("Functions").EnumerateArray().ToArray();
        var anchors = receiveRoot.GetProperty("Anchors").EnumerateArray().ToArray();
        var helperSlots = helperRoot.GetProperty("Slots").EnumerateArray().ToArray();

        var acceptedPeer = functions.FirstOrDefault(static function => ReadString(function, "Address") == "008b9468");
        var attachedReader = functions.FirstOrDefault(static function => ReadString(function, "Address") == "00a5c2e8");
        var udpDrain = functions.FirstOrDefault(static function => ReadString(function, "Address") == "008b8d50");
        var queueDrainSlot = helperSlots.FirstOrDefault(static slot => ReadString(slot, "FunctionAddress") == "00a5b4f0");
        var bitreaderTransformSlot = helperSlots.FirstOrDefault(static slot => ReadString(slot, "FunctionAddress") == "00a5d9e0");
        var attachedReaderSlot = helperSlots.FirstOrDefault(static slot => ReadString(slot, "FunctionAddress") == "00a5c2e8");

        var acceptedPeerProven = HasCall(acceptedPeer, "_opd_FUN_008b82c0")
            && HasEvidence(acceptedPeer, "uVar12 == 4")
            && HasAnchor(anchors, "accepted-peer-control-header")
            && HasAnchor(anchors, "source-player-associated-flag");
        var attachedFrameType2Proven = HasCall(attachedReader, "_opd_FUN_008b82c0")
            && HasCall(attachedReader, "_opd_FUN_00a58c10")
            && HasEvidence(attachedReader, "param_1[0x10c]")
            && HasEvidence(attachedReader, "param_1[0x151]")
            && HasAnchor(anchors, "attached-player-type-2")
            && HasAnchor(anchors, "attached-player-payload-dispatch");
        var udpDrainProven = HasCall(udpDrain, "recvfrom")
            && ReadBool(udpDrain, "ContainsRecvfrom")
            && ReadString(udpDrain, "RecvBufferLengthHex") == "0x800";
        var queueDrainProven = ReadString(queueDrainSlot, "FunctionAddress") == "00a5b4f0"
            && StringArray(queueDrainSlot, "EvidenceTokens").Contains("payload-dispatcher-call", StringComparer.Ordinal);
        var bitreaderCandidateRecovered = ReadBool(transformSummary, "BitreaderTransformCandidateRecovered")
            && ReadString(transformSummary, "BestTransformCandidateAddress") == "00a5d9e0";
        var directMarkerlessIngressProven = ReadBool(transformSummary, "DirectIngressLinkProven");
        var nativeMarkerlessInputReady = ReadBool(transformSummary, "NativeMarkerlessInputReady");

        var contracts = new[]
        {
            new Tf2Ps3SourceClientReceiveContract(
                "accepted-peer-association",
                "008b9468",
                "accepted-peer-control-reader",
                acceptedPeerProven ? "proven" : "missing",
                "Reads a 5-byte connected-peer control record, decodes message type 4 plus a 32-bit association token, matches a Source player object, stores the accepted socket at object +0x90, sets object +0x42e, invokes object vtable +0x6c, and removes the accepted-peer record.",
                [
                    "connected recv wrapper 008b82c0",
                    "accepted peer record stride 0x18",
                    "message type 4 attach path",
                    "player callbacks +0xc0/+0xc4 for peer id/token matching",
                    "object +0x90 attached socket",
                    "object +0x42e associated flag",
                    "post-attach vtable +0x6c"
                ],
                "This is the explicit client association path before regular Source payload handling."),
            new Tf2Ps3SourceClientReceiveContract(
                "attached-source-frame-reader",
                "00a5c2e8",
                "helper-slice-slot-0x6c",
                attachedFrameType2Proven ? "proven" : "missing",
                "Reads frame kinds 1-4 from the attached socket. Frame kind 2 reads a 16-bit length plus 32-bit token/id, stages the declared payload at object +0x544, and dispatches the completed bitstream through 00a58c10.",
                [
                    $"helper slot {ReadString(attachedReaderSlot, "VisibleSliceOffset")}",
                    "frame kind stored at object +0x430 / param_1[0x10c]",
                    "frame token stored at object +0x434 / param_1[0x10d]",
                    "frame length stored at object +0x438 / param_1[0x10e]",
                    "frame buffer object +0x544 / param_1[0x151]",
                    "inner Source dispatcher 00a58c10"
                ],
                "This explains visible attached type-2 Source frames, but not the hard markerless packet set."),
            new Tf2Ps3SourceClientReceiveContract(
                "udp-slot-drain",
                "008b8d50",
                "udp-gameplay-receive-drain",
                udpDrainProven ? "proven-drain-only" : "missing",
                "Iterates Source/gameplay UDP slots and drains nonblocking recvfrom packets into a 0x800 stack buffer.",
                [
                    "slot table PTR_DAT_0197336c + 0xe8",
                    "slot count PTR_DAT_0197336c + 0xf4",
                    "socket field slot +0x08",
                    "recvfrom max length 0x800",
                    "nonblocking flag 0x80"
                ],
                "Current C/decompile evidence exposes a drain, not the direct markerless body transform."),
            new Tf2Ps3SourceClientReceiveContract(
                "queued-peer-payload-drain",
                "00a5b4f0",
                "helper-slice-slot-0x68",
                queueDrainProven ? "proven" : "missing",
                "Pops queued payload objects from the global queue through virtual slot +0x48 and dispatches each queued body at iVar2 + 0x1c through 00a58c10.",
                [
                    $"helper slot {ReadString(queueDrainSlot, "VisibleSliceOffset")}",
                    "global queue PTR_PTR_01977d5c",
                    "queued payload body iVar2 + 0x1c",
                    "owner callbacks +0x14/+0x18 around dispatch",
                    "inner Source dispatcher 00a58c10"
                ],
                "This is a staged/queued payload drain, not direct proof that PCAP markerless client bodies begin at iVar2 + 0x1c."),
            new Tf2Ps3SourceClientReceiveContract(
                "peer-bitreader-transform",
                "00a5d9e0",
                "helper-slice-slot-0x70",
                bitreaderCandidateRecovered ? "candidate-proven" : "missing",
                "Receives a bitreader-like param_2, validates it against object +0x98, optionally consumes subpayload control bits through 00a5aa00/00a594e8/00a5d720, then dispatches remaining bits through 00a58c10(param_1, param_2 + 7).",
                [
                    $"helper slot {ReadString(bitreaderTransformSlot, "VisibleSliceOffset")}",
                    "param_2[0xb]/[0xc]/[0xd]/[0xe] bitreader state",
                    "object address/token compare via FUN_0086fb58",
                    "subpayload helpers 00a5aa00 / 00a594e8 / 00a5d720",
                    "remaining-bit dispatch 00a58c10(param_1, param_2 + 7)"
                ],
                "Strongest current inner transform candidate for markerless client input, but caller/ingress proof is still missing.")
        };

        var gates = new[]
        {
            new Tf2Ps3SourceClientReceiveGate(
                "association-and-attached-frame-contract",
                acceptedPeerProven && attachedFrameType2Proven ? "proven" : "missing",
                "008b9468 -> object +0x6c -> 00a5c2e8",
                "The accepted-peer attach path and attached type-2 Source bitstream reader are recovered."),
            new Tf2Ps3SourceClientReceiveGate(
                "peer-channel-queued-and-bitreader-contract",
                queueDrainProven && bitreaderCandidateRecovered ? "candidate-proven" : "missing",
                "00a5b4f0 / 00a5d9e0",
                "The queue drain is proven and the slot +0x70 bitreader transform is recovered as a candidate."),
            new Tf2Ps3SourceClientReceiveGate(
                "direct-markerless-pcap-ingress",
                directMarkerlessIngressProven ? "proven" : "missing",
                "direct PCAP markerless client bodies -> 00a5d9e0/00a58c10",
                "No current report proves that the 11,874 opaque markerless client bodies are passed into slot +0x70 or any sibling transform."),
            new Tf2Ps3SourceClientReceiveGate(
                "native-input-server-ready",
                nativeMarkerlessInputReady ? "proven" : "missing",
                "server implementation gate",
                "The replacement server can claim native client input only after the direct markerless ingress gate is proven.")
        };

        var report = new Tf2Ps3SourceClientReceiveContractReport(
            "tf2ps3-source-client-receive-contract",
            "Consolidates TF.elf receive-side Source contracts for the remaining map-load/native-input work. This is a semantic RE report, not packet replay.",
            new Tf2Ps3SourceClientReceiveInputs(
                receivePathPath,
                helperSlicePath,
                markerlessClassifierPath,
                markerlessTransformPath),
            new Tf2Ps3SourceClientReceiveSummary(
                helperSlots.Length,
                ReadInt(classifierSummary, "OpaquePacketCount"),
                ReadInt(classifierSummary, "DominantBodyLength"),
                acceptedPeerProven,
                attachedFrameType2Proven,
                udpDrainProven,
                queueDrainProven,
                bitreaderCandidateRecovered,
                directMarkerlessIngressProven,
                nativeMarkerlessInputReady,
                gates.Count(static gate => gate.Status != "proven")),
            contracts,
            gates,
            [
                "The normal accepted-peer path is now clearly mapped: 008b9468 attaches a connected peer socket to the Source player object and invokes the object +0x6c reader.",
                "The attached frame reader 00a5c2e8 is authoritative for visible frame-kind payloads and dispatches type-2 bitstreams through 00a58c10.",
                "The dominant hard PCAP set is still separate: 11,874 markerless client packets remain after excluding attached frames and decoded NET/CLC candidates.",
                "00a5d9e0 should stay the top candidate for the markerless inner transform, but the exact caller edge from client-visible packets remains the next native proof target.",
                "Do not mark the native replacement server input-complete until direct-markerless-pcap-ingress is proven."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static bool HasCall(JsonElement function, string call) =>
        StringArray(function, "Calls").Contains(call, StringComparer.Ordinal);

    private static bool HasEvidence(JsonElement function, string token) =>
        StringArray(function, "EvidenceTokens").Contains(token, StringComparer.Ordinal);

    private static bool HasAnchor(JsonElement[] anchors, string name) =>
        anchors.Any(anchor => ReadString(anchor, "Name") == name);

    private static string[] StringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(static item => item.GetString() ?? "")
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
}

public sealed record Tf2Ps3SourceClientReceiveContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceClientReceiveInputs Inputs,
    Tf2Ps3SourceClientReceiveSummary Summary,
    Tf2Ps3SourceClientReceiveContract[] Contracts,
    Tf2Ps3SourceClientReceiveGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceClientReceiveInputs(
    string ReceivePathReport,
    string HelperSliceReport,
    string MarkerlessClassifierReport,
    string MarkerlessTransformReport);

public sealed record Tf2Ps3SourceClientReceiveSummary(
    int HelperSliceSlotCount,
    int OpaqueMarkerlessClientPacketCount,
    int DominantOpaqueBodyLength,
    bool AcceptedPeerAttachProven,
    bool AttachedType2FrameDispatchProven,
    bool UdpDrainProven,
    bool QueuedPayloadDrainProven,
    bool BitreaderTransformCandidateRecovered,
    bool DirectMarkerlessIngressProven,
    bool NativeInputServerReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceClientReceiveContract(
    string Id,
    string Address,
    string Role,
    string Status,
    string Semantics,
    string[] Evidence,
    string ImplementationMeaning);

public sealed record Tf2Ps3SourceClientReceiveGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
