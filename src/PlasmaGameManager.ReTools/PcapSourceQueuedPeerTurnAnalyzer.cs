using System.Text.Json;
using PlasmaGameManager.Protocol;
using PlasmaGameManager.Server;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceQueuedPeerTurnAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceQueuedPeerTurnReport> AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceQueuedPeerTurnReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var fileReports = files.Select(file => AnalyzeFile(inputDirectory, file)).ToArray();
        return new PcapSourceQueuedPeerTurnReport(
            "pcap-source-queued-peer-turns",
            BuildSummary(fileReports),
            BuildTopCapturedNearMtuShapes(fileReports),
            fileReports);
    }

    private PcapSourceQueuedPeerTurnFile AnalyzeFile(string inputDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceQueuedPeerTurnFile(
                relativePath,
                false,
                "",
                "",
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "no-active-source-flow",
                []);
        }

        var steps = replay.SourcePackets
            .Select(static packet => new Ps3SourceGameplayReplayStep(
                packet.Direction == PcapActiveFlowDirection.ClientToServer
                    ? Ps3SourceGameplayDirection.ClientToServer
                    : Ps3SourceGameplayDirection.ServerToClient,
                packet.Payload,
                packet.PacketIndex,
                packet.TimestampMicroseconds))
            .ToArray();
        var turns = Ps3SourceGameplayTurnReplayDriver.BuildTurns(steps);
        var responder = new Ps3NativeSourceResponder();
        var game = new GameManagerSession(BuildSessionOptions(relativePath, replay));
        var player = game.GetOrAddPlayer(replay.ClientEndpoint);
        player.Name = "CapturedPlayer";

        var analyses = new List<PcapSourceQueuedPeerTurnSample>();
        var capturedNearMtuTurnCount = 0;
        var capturedNearMtuServerPacketCount = 0;
        var generatedNearMtuTurnCount = 0;
        var generatedNearMtuServerPacketCount = 0;
        var capturedNearMtuPacketCountMatchedTurnCount = 0;
        var capturedNearMtuShapeMatchedTurnCount = 0;
        var capturedNearMtuGeneratedSilentTurnCount = 0;
        var capturedNearMtuGeneratedWithoutNearMtuTurnCount = 0;
        var maxCapturedNearMtuPacketsInTurn = 0;
        var maxGeneratedNearMtuPacketsInTurn = 0;

        ushort? previousClientSequence = null;
        var clientDirectionPacketCount = 0;
        foreach (var turn in turns)
        {
            var clientSummaries = new List<PcapSourceQueuedPeerPacketSummary>();
            var generated = new List<byte[]>();
            foreach (var clientPacket in turn.ClientPackets)
            {
                clientDirectionPacketCount++;
                int? sequenceDelta = null;
                if (Ps3SourceTransportPacket.TryDecode(clientPacket.Payload, out var decodedClient))
                {
                    sequenceDelta = previousClientSequence is { } previous
                        ? Ps3SourceTransportPacket.SequenceDelta(previous, decodedClient.CandidateSequence)
                        : null;
                    previousClientSequence = decodedClient.CandidateSequence;
                }

                clientSummaries.Add(PacketSummary(clientPacket.Payload, clientDirectionPacketCount, sequenceDelta));
                generated.AddRange(responder.BuildResponses(game, player, clientPacket.Payload).Select(static response => response.Payload));
            }

            var capturedServer = turn.ServerResponses.Select(static packet => packet.Payload).ToArray();
            var generatedServer = generated.ToArray();
            var capturedNearMtuPackets = capturedServer.Where(IsQueuedNearMtuPacket).ToArray();
            var generatedNearMtuPackets = generatedServer.Where(IsQueuedNearMtuPacket).ToArray();
            if (capturedNearMtuPackets.Length > 0)
            {
                capturedNearMtuTurnCount++;
                capturedNearMtuServerPacketCount += capturedNearMtuPackets.Length;
                maxCapturedNearMtuPacketsInTurn = Math.Max(maxCapturedNearMtuPacketsInTurn, capturedNearMtuPackets.Length);
                if (generatedServer.Length == 0)
                {
                    capturedNearMtuGeneratedSilentTurnCount++;
                }

                if (generatedNearMtuPackets.Length == 0)
                {
                    capturedNearMtuGeneratedWithoutNearMtuTurnCount++;
                }

                if (capturedNearMtuPackets.Length == generatedNearMtuPackets.Length)
                {
                    capturedNearMtuPacketCountMatchedTurnCount++;
                }

                if (Ps3SourceGameplaySignatures.ShapeRunSignature(capturedServer) == Ps3SourceGameplaySignatures.ShapeRunSignature(generatedServer))
                {
                    capturedNearMtuShapeMatchedTurnCount++;
                }
            }

            if (generatedNearMtuPackets.Length > 0)
            {
                generatedNearMtuTurnCount++;
                generatedNearMtuServerPacketCount += generatedNearMtuPackets.Length;
                maxGeneratedNearMtuPacketsInTurn = Math.Max(maxGeneratedNearMtuPacketsInTurn, generatedNearMtuPackets.Length);
            }

            if (capturedNearMtuPackets.Length > 0 && analyses.Count < 64)
            {
                analyses.Add(new PcapSourceQueuedPeerTurnSample(
                    turn.TurnIndex,
                    turn.ClientPackets.Length,
                    capturedServer.Length,
                    generatedServer.Length,
                    capturedNearMtuPackets.Length,
                    generatedNearMtuPackets.Length,
                    capturedNearMtuPackets.Length == generatedNearMtuPackets.Length,
                    Ps3SourceGameplaySignatures.ShapeRunSignature(capturedServer) == Ps3SourceGameplaySignatures.ShapeRunSignature(generatedServer),
                    turn.ClientPackets[0].PacketIndex,
                    (capturedServer.Length > 0 ? turn.ServerResponses[^1] : turn.ClientPackets[^1]).PacketIndex,
                    FirstSequence(turn.ClientPackets),
                    LastSequence(turn.ClientPackets),
                    FirstSequence(turn.ServerResponses),
                    LastSequence(turn.ServerResponses),
                    ShapeLengthRun(capturedServer),
                    ShapeLengthRun(generatedServer),
                    clientSummaries.ToArray(),
                    capturedServer.Select(static packet => PacketSummary(packet, null, null)).ToArray(),
                    generatedServer.Select(static packet => PacketSummary(packet, null, null)).ToArray()));
            }
        }

        return new PcapSourceQueuedPeerTurnFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.SourcePackets.Length,
            turns.Length,
            capturedNearMtuTurnCount,
            capturedNearMtuServerPacketCount,
            generatedNearMtuTurnCount,
            generatedNearMtuServerPacketCount,
            capturedNearMtuPacketCountMatchedTurnCount,
            capturedNearMtuShapeMatchedTurnCount,
            capturedNearMtuGeneratedSilentTurnCount,
            capturedNearMtuGeneratedWithoutNearMtuTurnCount,
            maxCapturedNearMtuPacketsInTurn,
            maxGeneratedNearMtuPacketsInTurn,
            capturedNearMtuTurnCount == 0
                ? "no-captured-server-near-mtu-turns"
                : capturedNearMtuShapeMatchedTurnCount == capturedNearMtuTurnCount
                    ? "generated-queued-peer-turn-shapes-match"
                    : "generated-queued-peer-turn-shapes-diverge",
            analyses.ToArray());
    }

    private static PcapSourceQueuedPeerTurnSummary BuildSummary(PcapSourceQueuedPeerTurnFile[] files)
    {
        var active = files.Where(static file => file.HasActiveSourceFlow).ToArray();
        var capturedNearMtuTurns = active.Sum(static file => file.CapturedNearMtuTurnCount);
        var shapeMatched = active.Sum(static file => file.CapturedNearMtuShapeMatchedTurnCount);
        return new PcapSourceQueuedPeerTurnSummary(
            files.Length,
            active.Length,
            active.Sum(static file => file.SourcePacketCount),
            active.Sum(static file => file.TurnCount),
            capturedNearMtuTurns,
            active.Sum(static file => file.CapturedNearMtuServerPacketCount),
            active.Sum(static file => file.GeneratedNearMtuTurnCount),
            active.Sum(static file => file.GeneratedNearMtuServerPacketCount),
            active.Sum(static file => file.CapturedNearMtuPacketCountMatchedTurnCount),
            shapeMatched,
            active.Sum(static file => file.CapturedNearMtuGeneratedSilentTurnCount),
            active.Sum(static file => file.CapturedNearMtuGeneratedWithoutNearMtuTurnCount),
            active.Length == 0 ? 0 : active.Max(static file => file.MaxCapturedNearMtuPacketsInTurn),
            active.Length == 0 ? 0 : active.Max(static file => file.MaxGeneratedNearMtuPacketsInTurn),
            capturedNearMtuTurns == 0
                ? "no-captured-server-near-mtu-turns"
                : shapeMatched == capturedNearMtuTurns
                    ? "generated-queued-peer-turn-shapes-match"
                    : "generated-queued-peer-turn-shapes-diverge");
    }

    private static PcapSourceQueuedPeerTurnCount[] BuildTopCapturedNearMtuShapes(PcapSourceQueuedPeerTurnFile[] files)
    {
        return files
            .Where(static file => file.HasActiveSourceFlow)
            .SelectMany(static file => file.Samples)
            .GroupBy(static sample => sample.CapturedServerShapeLengthRun, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(32)
            .Select(static group => new PcapSourceQueuedPeerTurnCount(group.Key, group.Count()))
            .ToArray();
    }

    private static GameManagerSessionOptions BuildSessionOptions(string relativePath, PcapActiveFlowReplay replay)
    {
        var map = relativePath.Contains("dustbowl", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("cp_db", StringComparison.OrdinalIgnoreCase)
            ? "cp_dustbowl"
            : "ctf_2fort";
        var gameMode = map.StartsWith("cp_", StringComparison.Ordinal)
            ? "control-point"
            : "capture-the-flag";
        return new GameManagerSessionOptions(
            GameId: 9843,
            MapName: map,
            Name: "TF2PS3",
            MaxPlayers: 24,
            PreferredPlayerId: 197,
            GameMode: gameMode,
            RankingMode: "Unranked",
            IsRanked: false,
            TimeLimitMinutes: gameMode == "control-point" ? 15 : 30,
            MaxRounds: 5,
            FlagCaptureLimit: gameMode == "capture-the-flag" ? 3 : 0,
            AutoBalance: true,
            AdvertisedHost: replay.ServerEndpoint.Split(':')[0],
            AdvertisedPort: replay.ServerPort);
    }

    private static bool IsQueuedNearMtuPacket(byte[] payload)
    {
        return Ps3SourceTransportPacket.TryDecode(payload, out var packet)
            && packet.ClassifyNativeFrame().Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate;
    }

    private static PcapSourceQueuedPeerPacketSummary PacketSummary(
        byte[] payload,
        int? clientDirectionPacketCount,
        int? sequenceDelta)
    {
        if (!Ps3SourceTransportPacket.TryDecode(payload, out var packet))
        {
            return new PcapSourceQueuedPeerPacketSummary(
                payload.Length,
                0,
                "Invalid",
                "Invalid",
                "Invalid",
                "Invalid",
                null,
                null,
                null,
                0,
                "");
        }

        var semantics = Ps3SourcePayloadSemantics.Analyze(packet.Body);
        var nativeFrame = packet.ClassifyNativeFrame();
        var clientInfo = clientDirectionPacketCount is { } count
            ? Ps3SourceClientPayloadClassifier.Classify(packet, count, sequenceDelta)
            : null;
        return new PcapSourceQueuedPeerPacketSummary(
            payload.Length,
            packet.Body.Length,
            Ps3SourceGameplaySession.ClassifyShape(packet).ToString(),
            nativeFrame.Kind.ToString(),
            semantics.Kind.ToString(),
            semantics.Role.ToString(),
            clientInfo?.Role.ToString(),
            packet.CandidateSequence,
            sequenceDelta,
            semantics.Entropy,
            Prefix(packet.Body, 12));
    }

    private static string ShapeLengthRun(IReadOnlyList<byte[]> payloads)
    {
        return payloads.Count == 0
            ? ""
            : string.Join(
                ";",
                payloads.Select(static payload =>
                {
                    if (!Ps3SourceTransportPacket.TryDecode(payload, out var packet))
                    {
                        return $"Invalid:{payload.Length}";
                    }

                    return $"{packet.ClassifyNativeFrame().Kind}:{payload.Length}";
                }));
    }

    private static int? FirstSequence(IReadOnlyList<Ps3SourceGameplayReplayStep> packets)
    {
        return packets.Count > 0 && Ps3SourceTransportPacket.TryDecode(packets[0].Payload, out var packet)
            ? packet.CandidateSequence
            : null;
    }

    private static int? LastSequence(IReadOnlyList<Ps3SourceGameplayReplayStep> packets)
    {
        return packets.Count > 0 && Ps3SourceTransportPacket.TryDecode(packets[^1].Payload, out var packet)
            ? packet.CandidateSequence
            : null;
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapSourceQueuedPeerTurnReport(
    string Status,
    PcapSourceQueuedPeerTurnSummary Summary,
    PcapSourceQueuedPeerTurnCount[] TopCapturedNearMtuShapeLengthRuns,
    PcapSourceQueuedPeerTurnFile[] Files);

public sealed record PcapSourceQueuedPeerTurnSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int SourcePacketCount,
    int TurnCount,
    int CapturedNearMtuTurnCount,
    int CapturedNearMtuServerPacketCount,
    int GeneratedNearMtuTurnCount,
    int GeneratedNearMtuServerPacketCount,
    int CapturedNearMtuPacketCountMatchedTurnCount,
    int CapturedNearMtuShapeMatchedTurnCount,
    int CapturedNearMtuGeneratedSilentTurnCount,
    int CapturedNearMtuGeneratedWithoutNearMtuTurnCount,
    int MaxCapturedNearMtuPacketsInTurn,
    int MaxGeneratedNearMtuPacketsInTurn,
    string Conclusion);

public sealed record PcapSourceQueuedPeerTurnFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int TurnCount,
    int CapturedNearMtuTurnCount,
    int CapturedNearMtuServerPacketCount,
    int GeneratedNearMtuTurnCount,
    int GeneratedNearMtuServerPacketCount,
    int CapturedNearMtuPacketCountMatchedTurnCount,
    int CapturedNearMtuShapeMatchedTurnCount,
    int CapturedNearMtuGeneratedSilentTurnCount,
    int CapturedNearMtuGeneratedWithoutNearMtuTurnCount,
    int MaxCapturedNearMtuPacketsInTurn,
    int MaxGeneratedNearMtuPacketsInTurn,
    string Status,
    PcapSourceQueuedPeerTurnSample[] Samples);

public sealed record PcapSourceQueuedPeerTurnCount(
    string Value,
    int Count);

public sealed record PcapSourceQueuedPeerTurnSample(
    int TurnIndex,
    int ClientPacketCount,
    int CapturedServerPacketCount,
    int GeneratedServerPacketCount,
    int CapturedNearMtuServerPacketCount,
    int GeneratedNearMtuServerPacketCount,
    bool NearMtuPacketCountMatches,
    bool FullShapeMatches,
    long FirstPacketIndex,
    long LastPacketIndex,
    int? FirstClientSequence,
    int? LastClientSequence,
    int? FirstServerSequence,
    int? LastServerSequence,
    string CapturedServerShapeLengthRun,
    string GeneratedServerShapeLengthRun,
    PcapSourceQueuedPeerPacketSummary[] ClientPackets,
    PcapSourceQueuedPeerPacketSummary[] CapturedServerPackets,
    PcapSourceQueuedPeerPacketSummary[] GeneratedServerPackets);

public sealed record PcapSourceQueuedPeerPacketSummary(
    int PayloadLength,
    int BodyLength,
    string Shape,
    string NativeFrameKind,
    string SemanticKind,
    string SemanticRole,
    string? ClientRole,
    ushort? Sequence,
    int? SequenceDelta,
    double Entropy,
    string BodyPrefixHex);
