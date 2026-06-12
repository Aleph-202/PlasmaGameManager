using System.Text.Json;
using PlasmaGameManager.Protocol;
using PlasmaGameManager.Server;

namespace PlasmaGameManager.ReTools;

public sealed class PcapGeneratedSourceResponderShapeAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapGeneratedSourceResponderShapeReport> AnalyzeDirectoryAsync(
        string inputDirectory,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapGeneratedSourceResponderShapeReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var analyses = files.Select(path => AnalyzeFile(inputDirectory, path)).ToArray();
        var active = analyses.Where(static analysis => analysis.HasActiveSourceFlow).ToArray();
        return new PcapGeneratedSourceResponderShapeReport(
            "pcap-generated-source-responder-shapes",
            new PcapGeneratedSourceResponderShapeSummary(
                files.Length,
                active.Length,
                active.Sum(static analysis => analysis.TurnCount),
                active.Sum(static analysis => analysis.ComparedTurnCount),
                active.Sum(static analysis => analysis.PacketCountMatchedTurnCount),
                active.Sum(static analysis => analysis.ShapeMatchedTurnCount),
                active.Sum(static analysis => analysis.CapturedSilentGeneratedRespondedTurnCount),
                active.Sum(static analysis => analysis.CapturedRespondedGeneratedSilentTurnCount),
                active.Sum(static analysis => analysis.TotalCapturedServerPacketCount),
                active.Sum(static analysis => analysis.TotalGeneratedServerPacketCount),
                active.Length == 0
                    ? "no-active-source-flows"
                    : "generated-responder-shape-comparison-ready"),
            analyses);
    }

    private PcapGeneratedSourceResponderFileAnalysis AnalyzeFile(string inputDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapGeneratedSourceResponderFileAnalysis(
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
                "no-active-source-flow",
                null,
                null,
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

        var results = new List<PcapGeneratedSourceResponderTurnComparison>(turns.Length);
        ushort? previousClientSequence = null;
        var clientDirectionPacketCount = 0;
        foreach (var turn in turns)
        {
            var generated = new List<byte[]>();
            var clientSummaries = new List<PcapGeneratedSourceResponderPacketSummary>(turn.ClientPackets.Length);
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

                clientSummaries.Add(SourcePacketSummary(
                    clientPacket.Payload,
                    clientDirectionPacketCount,
                    sequenceDelta));
                var responses = responder.BuildResponses(game, player, clientPacket.Payload);
                generated.AddRange(responses.Select(static response => response.Payload));
            }

            var capturedServerPayloads = turn.ServerResponses.Select(static packet => packet.Payload).ToArray();
            var generatedPayloads = generated.ToArray();
            var capturedShape = Ps3SourceGameplaySignatures.ShapeRunSignature(capturedServerPayloads);
            var generatedShape = Ps3SourceGameplaySignatures.ShapeRunSignature(generatedPayloads);
            var capturedBody = Ps3SourceGameplaySignatures.BodyRunSignature(capturedServerPayloads);
            var generatedBody = Ps3SourceGameplaySignatures.BodyRunSignature(generatedPayloads);
            var packetCountMatches = capturedServerPayloads.Length == generatedPayloads.Length;
            var shapeMatches = packetCountMatches && capturedShape == generatedShape;
            results.Add(new PcapGeneratedSourceResponderTurnComparison(
                turn.TurnIndex,
                turn.ClientPackets.Length,
                capturedServerPayloads.Length,
                generatedPayloads.Length,
                packetCountMatches,
                shapeMatches,
                capturedShape,
                generatedShape,
                capturedBody,
                generatedBody,
                capturedServerPayloads.Select(SourcePacketShape).ToArray(),
                generatedPayloads.Select(SourcePacketShape).ToArray(),
                clientSummaries.ToArray(),
                capturedServerPayloads.Select(SourcePacketSummary).ToArray(),
                generatedPayloads.Select(SourcePacketSummary).ToArray()));
        }

        var compared = results.Count;
        var packetCountMatched = results.Count(static result => result.PacketCountMatches);
        var shapeMatched = results.Count(static result => result.ShapeMatches);
        var capturedSilentGeneratedResponded = results.Count(static result =>
            result.CapturedServerPacketCount == 0 && result.GeneratedServerPacketCount > 0);
        var capturedRespondedGeneratedSilent = results.Count(static result =>
            result.CapturedServerPacketCount > 0 && result.GeneratedServerPacketCount == 0);
        var firstDivergentTurn = results.FirstOrDefault(static result => !result.ShapeMatches);

        return new PcapGeneratedSourceResponderFileAnalysis(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.SourcePackets.Length,
            turns.Length,
            compared,
            packetCountMatched,
            shapeMatched,
            capturedSilentGeneratedResponded,
            capturedRespondedGeneratedSilent,
            results.Sum(static result => result.CapturedServerPacketCount),
            results.Sum(static result => result.GeneratedServerPacketCount),
            compared == 0
                ? "no-source-turns"
                : shapeMatched == compared
                    ? "all-turn-shapes-match"
                    : "generated-shape-diverges-from-capture",
            firstDivergentTurn?.TurnIndex,
            firstDivergentTurn is null ? null : DescribeDivergence(firstDivergentTurn),
            results.ToArray());
    }

    private static string DescribeDivergence(PcapGeneratedSourceResponderTurnComparison turn)
    {
        if (turn.CapturedServerPacketCount == 0 && turn.GeneratedServerPacketCount > 0)
        {
            return $"captured-silent-generated-{turn.GeneratedServerPacketCount}-packet";
        }

        if (turn.CapturedServerPacketCount > 0 && turn.GeneratedServerPacketCount == 0)
        {
            return $"captured-{turn.CapturedServerPacketCount}-packet-generated-silent";
        }

        if (!turn.PacketCountMatches)
        {
            return $"packet-count-captured-{turn.CapturedServerPacketCount}-generated-{turn.GeneratedServerPacketCount}";
        }

        return $"shape-captured-{turn.CapturedServerShapeSignature}-generated-{turn.GeneratedServerShapeSignature}";
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

    private static string SourcePacketShape(byte[] payload)
    {
        if (!Ps3SourceTransportPacket.TryDecode(payload, out var packet))
        {
            return "Invalid";
        }

        return Ps3SourceGameplaySession.ClassifyShape(packet).ToString();
    }

    private static PcapGeneratedSourceResponderPacketSummary SourcePacketSummary(byte[] payload)
    {
        return SourcePacketSummary(payload, null, null);
    }

    private static PcapGeneratedSourceResponderPacketSummary SourcePacketSummary(
        byte[] payload,
        int? clientDirectionPacketCount,
        int? sequenceDelta)
    {
        if (!Ps3SourceTransportPacket.TryDecode(payload, out var packet))
        {
            return new PcapGeneratedSourceResponderPacketSummary(
                payload.Length,
                0,
                "Invalid",
                "Invalid",
                "Invalid",
                "Invalid",
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                "",
                "");
        }

        var semantics = Ps3SourcePayloadSemantics.Analyze(packet.Body);
        var nativeFrame = packet.ClassifyNativeFrame();
        var clientInfo = clientDirectionPacketCount is { } count
            ? Ps3SourceClientPayloadClassifier.Classify(packet, count, sequenceDelta)
            : null;
        return new PcapGeneratedSourceResponderPacketSummary(
            payload.Length,
            packet.Body.Length,
            Ps3SourceGameplaySession.ClassifyShape(packet).ToString(),
            nativeFrame.Kind.ToString(),
            semantics.Kind.ToString(),
            semantics.Role.ToString(),
            clientInfo?.Role.ToString(),
            clientInfo?.Sequence,
            clientInfo?.SequenceDelta,
            clientInfo?.ReliableAssociationNativeToken,
            clientInfo?.AttachedFrameKind,
            semantics.Entropy,
            semantics.PrintableRatio,
            Convert.ToHexString(packet.Body.AsSpan(0, Math.Min(packet.Body.Length, 12))).ToLowerInvariant(),
            semantics.AsciiPreview);
    }
}

public sealed record PcapGeneratedSourceResponderShapeReport(
    string Status,
    PcapGeneratedSourceResponderShapeSummary Summary,
    PcapGeneratedSourceResponderFileAnalysis[] Files);

public sealed record PcapGeneratedSourceResponderShapeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int TurnCount,
    int ComparedTurnCount,
    int PacketCountMatchedTurnCount,
    int ShapeMatchedTurnCount,
    int CapturedSilentGeneratedRespondedTurnCount,
    int CapturedRespondedGeneratedSilentTurnCount,
    int TotalCapturedServerPacketCount,
    int TotalGeneratedServerPacketCount,
    string Conclusion);

public sealed record PcapGeneratedSourceResponderFileAnalysis(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int TurnCount,
    int ComparedTurnCount,
    int PacketCountMatchedTurnCount,
    int ShapeMatchedTurnCount,
    int CapturedSilentGeneratedRespondedTurnCount,
    int CapturedRespondedGeneratedSilentTurnCount,
    int TotalCapturedServerPacketCount,
    int TotalGeneratedServerPacketCount,
    string Status,
    int? FirstDivergentTurnIndex,
    string? FirstDivergenceReason,
    PcapGeneratedSourceResponderTurnComparison[] SampleTurns);

public sealed record PcapGeneratedSourceResponderTurnComparison(
    int TurnIndex,
    int ClientPacketCount,
    int CapturedServerPacketCount,
    int GeneratedServerPacketCount,
    bool PacketCountMatches,
    bool ShapeMatches,
    string CapturedServerShapeSignature,
    string GeneratedServerShapeSignature,
    string CapturedServerBodySignature,
    string GeneratedServerBodySignature,
    string[] CapturedServerShapes,
    string[] GeneratedServerShapes,
    PcapGeneratedSourceResponderPacketSummary[] ClientPackets,
    PcapGeneratedSourceResponderPacketSummary[] CapturedServerPackets,
    PcapGeneratedSourceResponderPacketSummary[] GeneratedServerPackets);

public sealed record PcapGeneratedSourceResponderPacketSummary(
    int PayloadLength,
    int BodyLength,
    string Shape,
    string NativeFrameKind,
    string SemanticKind,
    string SemanticRole,
    string? ClientRole,
    ushort? CandidateSequence,
    int? SequenceDelta,
    uint? ReliableAssociationNativeToken,
    byte? AttachedFrameKind,
    double Entropy,
    double PrintableRatio,
    string BodyPrefixHex,
    string AsciiPreview);
