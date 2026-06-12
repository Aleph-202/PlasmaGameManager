using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapGameManagerHandoffBoundaryAnalyzer
{
    private readonly PlasmaPacketClassifier _classifier = new();
    private readonly GameManagerCommandDecoder _commandDecoder = new();

    public async Task<PcapGameManagerHandoffBoundaryReport> AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapGameManagerHandoffBoundaryReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static p => p.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static p => p, StringComparer.Ordinal)
            .Select(file => AnalyzeFile(inputDirectory, file))
            .ToArray();

        return new PcapGameManagerHandoffBoundaryReport("pcap-gamemanager-handoff-boundary", BuildSummary(files), files);
    }

    private PcapGameManagerHandoffBoundaryFile AnalyzeFile(string inputDirectory, string file)
    {
        var packets = CaptureUdpPacketParser.ReadUdpPackets(file)
            .Where(static packet => packet.Payload.Length > 0)
            .Select(DecodePacket)
            .ToArray();
        var flow = InferPrimaryFlow(packets);
        var directed = packets
            .Select(packet => packet with
            {
                Direction = DirectionFor(packet, flow.ClientEndpoint, flow.ServerEndpoint),
                IsPrimaryFlow = IsPrimaryFlow(packet, flow.ClientEndpoint, flow.ServerEndpoint),
                IsPrimaryAddressPair = IsPrimaryAddressPair(packet, flow.ClientAddress, flow.ServerAddress),
                VisibleServerEndpoint = VisibleServerEndpoint(packet, flow.ClientAddress, flow.ServerAddress) ?? ""
            })
            .ToArray();

        var firstSource = directed
            .Where(static packet => packet.Phase == GameManagerScenarioPhase.SourceTraffic && packet.IsPrimaryAddressPair)
            .OrderBy(static packet => packet.PacketIndex)
            .FirstOrDefault();
        var lastGameManagerBeforeSource = firstSource is null
            ? null
            : directed
                .Where(packet => packet.PacketIndex < firstSource.PacketIndex
                    && packet.IsPrimaryAddressPair
                    && IsGameManagerBoundaryCandidate(packet.Phase))
                .OrderByDescending(static packet => packet.PacketIndex)
                .FirstOrDefault();
        var sourceTransport = firstSource is not null && Ps3SourceTransportPacket.TryDecode(firstSource.Payload, out var decodedSource)
            ? decodedSource
            : null;
        var nativeFrame = sourceTransport?.ClassifyNativeFrame();
        var sourceSemantic = sourceTransport is null
            ? null
            : Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(sourceTransport.Body);
        var boundaryModel = firstSource is null
            ? "no-source-traffic"
            : firstSource.IsPrimaryFlow
                ? "same-visible-gamemanager-flow"
                : firstSource.IsPrimaryAddressPair
                    ? "same-visible-address-port-shift"
                    : "different-visible-address";
        var gapPacketCount = firstSource is null || lastGameManagerBeforeSource is null
            ? 0
            : directed.Count(packet =>
                packet.PacketIndex > lastGameManagerBeforeSource.PacketIndex
                && packet.PacketIndex < firstSource.PacketIndex
                && packet.IsPrimaryAddressPair);

        return new PcapGameManagerHandoffBoundaryFile(
            Path.GetRelativePath(inputDirectory, file),
            packets.Length,
            flow,
            firstSource is not null,
            boundaryModel,
            lastGameManagerBeforeSource is null ? null : ToBoundaryPacket(lastGameManagerBeforeSource),
            firstSource is null ? null : ToBoundaryPacket(firstSource),
            firstSource?.VisibleServerEndpoint ?? "",
            gapPacketCount,
            sourceTransport?.CandidateSequence,
            sourceTransport?.Body.Length,
            nativeFrame?.Kind.ToString() ?? "",
            nativeFrame?.FitsInlineQueue,
            nativeFrame?.FitsNativeQueue,
            nativeFrame?.FragmentHeaderHex ?? "",
            sourceSemantic?.Kind.ToString() ?? "",
            sourceSemantic?.Role.ToString() ?? "",
            BuildConclusion(boundaryModel, lastGameManagerBeforeSource, firstSource, sourceTransport, nativeFrame, sourceSemantic));
    }

    private DecodedBoundaryPacket DecodePacket(CaptureUdpPacket packet)
    {
        var hasTransportFrame = PlasmaTransportFrame.TryDecode(packet.Payload, out var transportFrame);
        var semanticPayload = hasTransportFrame ? transportFrame.Payload : packet.Payload;
        var decoded = _classifier.Decode(semanticPayload, enableNativeBinary: hasTransportFrame);
        var command = _commandDecoder.Decode(decoded);
        var phase = GameManagerScenarioPhaseClassifier.Classify(command, packet.SourcePort, packet.DestinationPort);
        return new DecodedBoundaryPacket(
            packet.PacketIndex,
            packet.SourceAddress,
            packet.SourcePort,
            packet.DestinationAddress,
            packet.DestinationPort,
            $"{packet.SourceAddress}:{packet.SourcePort}",
            $"{packet.DestinationAddress}:{packet.DestinationPort}",
            "unknown",
            false,
            false,
            "",
            packet.Payload,
            packet.Payload.Length,
            semanticPayload.Length,
            phase,
            decoded.Kind,
            decoded.Marker ?? "",
            command.Name,
            decoded.HexPrefix(16),
            decoded.AsciiPreview(48));
    }

    private static PcapPrimaryFlow InferPrimaryFlow(IReadOnlyList<DecodedBoundaryPacket> packets)
    {
        var serverHello = packets.FirstOrDefault(static packet => packet.Kind == PlasmaCommandKind.ServerHello);
        if (serverHello is not null)
        {
            return FlowFrom(serverHello, serverHello.DestinationEndpoint, serverHello.SourceEndpoint, "server-hello");
        }

        var clientHello = packets.FirstOrDefault(static packet => packet.Kind == PlasmaCommandKind.ClientHello);
        if (clientHello is not null)
        {
            return FlowFrom(clientHello, clientHello.SourceEndpoint, clientHello.DestinationEndpoint, "client-hello");
        }

        var gameManagerLike = packets.FirstOrDefault(static packet => packet.Kind != PlasmaCommandKind.Unknown);
        if (gameManagerLike is not null)
        {
            return FlowFrom(gameManagerLike, gameManagerLike.SourceEndpoint, gameManagerLike.DestinationEndpoint, "first-gamemanager-like");
        }

        return new PcapPrimaryFlow("", "", "", "", 0, 0, "none");
    }

    private static PcapPrimaryFlow FlowFrom(DecodedBoundaryPacket packet, string clientEndpoint, string serverEndpoint, string inferredFrom)
    {
        var clientIsSource = clientEndpoint == packet.SourceEndpoint;
        return new PcapPrimaryFlow(
            clientEndpoint,
            serverEndpoint,
            clientIsSource ? packet.SourceAddress : packet.DestinationAddress,
            clientIsSource ? packet.DestinationAddress : packet.SourceAddress,
            clientIsSource ? packet.SourcePort : packet.DestinationPort,
            clientIsSource ? packet.DestinationPort : packet.SourcePort,
            inferredFrom);
    }

    private static bool IsGameManagerBoundaryCandidate(GameManagerScenarioPhase phase)
    {
        return phase is GameManagerScenarioPhase.Hello
            or GameManagerScenarioPhase.HandshakeControl
            or GameManagerScenarioPhase.Reservation
            or GameManagerScenarioPhase.Roster
            or GameManagerScenarioPhase.Mesh
            or GameManagerScenarioPhase.JoinComplete;
    }

    private static bool IsPrimaryFlow(DecodedBoundaryPacket packet, string clientEndpoint, string serverEndpoint)
    {
        return clientEndpoint.Length > 0
            && serverEndpoint.Length > 0
            && ((packet.SourceEndpoint == clientEndpoint && packet.DestinationEndpoint == serverEndpoint)
                || (packet.SourceEndpoint == serverEndpoint && packet.DestinationEndpoint == clientEndpoint));
    }

    private static bool IsPrimaryAddressPair(DecodedBoundaryPacket packet, string clientAddress, string serverAddress)
    {
        return clientAddress.Length > 0
            && serverAddress.Length > 0
            && ((packet.SourceAddress == clientAddress && packet.DestinationAddress == serverAddress)
                || (packet.SourceAddress == serverAddress && packet.DestinationAddress == clientAddress));
    }

    private static string DirectionFor(DecodedBoundaryPacket packet, string clientEndpoint, string serverEndpoint)
    {
        if (packet.SourceEndpoint == clientEndpoint && packet.DestinationEndpoint == serverEndpoint)
        {
            return "client-to-server";
        }

        if (packet.SourceEndpoint == serverEndpoint && packet.DestinationEndpoint == clientEndpoint)
        {
            return "server-to-client";
        }

        return "other";
    }

    private static string? VisibleServerEndpoint(DecodedBoundaryPacket packet, string clientAddress, string serverAddress)
    {
        if (!IsPrimaryAddressPair(packet, clientAddress, serverAddress))
        {
            return null;
        }

        return packet.SourceAddress == serverAddress ? packet.SourceEndpoint : packet.DestinationEndpoint;
    }

    private static PcapGameManagerHandoffBoundaryPacket ToBoundaryPacket(DecodedBoundaryPacket packet)
    {
        return new PcapGameManagerHandoffBoundaryPacket(
            packet.PacketIndex,
            packet.Direction,
            packet.SourceEndpoint,
            packet.DestinationEndpoint,
            packet.VisibleServerEndpoint,
            packet.RawLength,
            packet.SemanticLength,
            packet.Phase.ToString(),
            packet.Kind.ToString(),
            packet.Marker,
            packet.Command,
            packet.IsPrimaryFlow,
            packet.HexPrefix,
            packet.AsciiPreview);
    }

    private static PcapGameManagerHandoffBoundarySummary BuildSummary(PcapGameManagerHandoffBoundaryFile[] files)
    {
        var filesWithSource = files.Count(static file => file.HasSourceTraffic);
        return new PcapGameManagerHandoffBoundarySummary(
            files.Length,
            files.Sum(static file => file.UdpPacketCount),
            filesWithSource,
            files.Count(static file => file.BoundaryModel == "same-visible-gamemanager-flow"),
            files.Count(static file => file.BoundaryModel == "same-visible-address-port-shift"),
            files.Count(static file => file.BoundaryModel == "different-visible-address"),
            files.Count(static file => file.LastGameManagerPacket is not null),
            files.Count(static file => file.FirstSourceNativeFrameKind == Ps3SourceNativeFrameKind.DirectDatagramCandidate.ToString()),
            files.Count(static file => file.FirstSourceNativeFrameKind == Ps3SourceNativeFrameKind.FragmentedSendCandidate.ToString()),
            files.Count(static file => file.FirstSourceFitsNativeQueue == true),
            files
                .Where(static file => file.HasSourceTraffic)
                .GroupBy(static file => file.FirstSourcePayloadSemanticRole, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal),
            files
                .Where(static file => file.HasSourceTraffic)
                .GroupBy(static file => file.BoundaryModel, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal));
    }

    private static string BuildConclusion(
        string boundaryModel,
        DecodedBoundaryPacket? lastGameManager,
        DecodedBoundaryPacket? firstSource,
        Ps3SourceTransportPacket? sourceTransport,
        Ps3SourceNativeFrameInfo? nativeFrame,
        Ps3SourcePayloadSemanticInfo? sourceSemantic)
    {
        if (firstSource is null)
        {
            return "No Source/gameplay traffic was found on the primary visible game-server address pair.";
        }

        var native = sourceTransport is null || nativeFrame is null
            ? "the first Source packet is not parseable as the recovered PS3 Source transport envelope"
            : $"the first Source packet has sequence {sourceTransport.CandidateSequence}, body {sourceTransport.Body.Length}, native frame {nativeFrame.Kind}";
        var semantic = sourceSemantic is null
            ? ""
            : $", semantic role {sourceSemantic.Role}";
        var previous = lastGameManager is null
            ? "no prior classified GameManager packet was found"
            : $"last GameManager packet before handoff is {lastGameManager.Kind}/{lastGameManager.Phase} at packet {lastGameManager.PacketIndex}";
        return $"{boundaryModel}: {previous}; {native}{semantic}.";
    }

    private sealed record DecodedBoundaryPacket(
        long PacketIndex,
        string SourceAddress,
        int SourcePort,
        string DestinationAddress,
        int DestinationPort,
        string SourceEndpoint,
        string DestinationEndpoint,
        string Direction,
        bool IsPrimaryFlow,
        bool IsPrimaryAddressPair,
        string VisibleServerEndpoint,
        byte[] Payload,
        int RawLength,
        int SemanticLength,
        GameManagerScenarioPhase Phase,
        PlasmaCommandKind Kind,
        string Marker,
        string Command,
        string HexPrefix,
        string AsciiPreview);
}

public sealed record PcapGameManagerHandoffBoundaryReport(
    string Status,
    PcapGameManagerHandoffBoundarySummary Summary,
    PcapGameManagerHandoffBoundaryFile[] Files);

public sealed record PcapGameManagerHandoffBoundarySummary(
    int FileCount,
    int UdpPacketCount,
    int FilesWithSourceTraffic,
    int SameVisibleGameManagerFlowCount,
    int SameVisibleAddressPortShiftCount,
    int DifferentVisibleAddressCount,
    int FilesWithPriorGameManagerBoundaryPacket,
    int FirstSourceDirectDatagramCount,
    int FirstSourceFragmentedCandidateCount,
    int FirstSourceFitsNativeQueueCount,
    IReadOnlyDictionary<string, int> FirstSourcePayloadSemanticRoleCounts,
    IReadOnlyDictionary<string, int> BoundaryModelCounts);

public sealed record PcapGameManagerHandoffBoundaryFile(
    string File,
    int UdpPacketCount,
    PcapPrimaryFlow PrimaryFlow,
    bool HasSourceTraffic,
    string BoundaryModel,
    PcapGameManagerHandoffBoundaryPacket? LastGameManagerPacket,
    PcapGameManagerHandoffBoundaryPacket? FirstSourcePacket,
    string FirstSourceVisibleServerEndpoint,
    int GapPacketsOnPrimaryAddressPair,
    int? FirstSourceSequence,
    int? FirstSourceBodyLength,
    string FirstSourceNativeFrameKind,
    bool? FirstSourceFitsInlineQueue,
    bool? FirstSourceFitsNativeQueue,
    string FirstSourceFragmentHeaderHex,
    string FirstSourcePayloadSemanticKind,
    string FirstSourcePayloadSemanticRole,
    string Conclusion);

public sealed record PcapGameManagerHandoffBoundaryPacket(
    long PacketIndex,
    string Direction,
    string SourceEndpoint,
    string DestinationEndpoint,
    string VisibleServerEndpoint,
    int RawLength,
    int SemanticLength,
    string Phase,
    string Kind,
    string Marker,
    string Command,
    bool IsPrimaryFlow,
    string HexPrefix,
    string AsciiPreview);
