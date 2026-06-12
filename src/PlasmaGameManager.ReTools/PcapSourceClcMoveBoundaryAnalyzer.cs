using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceClcMoveBoundaryAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceClcMoveBoundaryReport> AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceClcMoveBoundaryReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var reports = files.Select(file => AnalyzeFile(inputDirectory, file)).ToArray();
        return new PcapSourceClcMoveBoundaryReport(
            "pcap-source-clc-move-boundaries",
            BuildSummary(reports),
            BuildCounts(reports),
            reports);
    }

    private PcapSourceClcMoveBoundaryFile AnalyzeFile(string inputDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceClcMoveBoundaryFile(
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
                [],
                [],
                []);
        }

        var decodedSourcePackets = replay.SourcePackets
            .Where(static packet => Ps3SourceTransportPacket.TryDecode(packet.Payload, out _))
            .Select(static packet =>
            {
                Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport);
                return new DecodedSourcePacket(packet, transport);
            })
            .ToArray();

        var clientPackets = new List<ClientPacketBoundaryProbe>();
        ushort? previousSequence = null;
        var directionPacketCount = 0;
        foreach (var packet in decodedSourcePackets.Where(static packet => packet.Packet.Direction == PcapActiveFlowDirection.ClientToServer))
        {
            directionPacketCount++;
            int? sequenceDelta = previousSequence.HasValue
                ? Ps3SourceTransportPacket.SequenceDelta(previousSequence.Value, packet.Transport.CandidateSequence)
                : null;
            previousSequence = packet.Transport.CandidateSequence;
            var info = Ps3SourceClientPayloadClassifier.Classify(packet.Transport, directionPacketCount, sequenceDelta);
            var resolved = Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, packet.Transport.Body, out var boundary);
            var embedded = resolved
                ? null
                : TryFindEmbeddedClcMove(packet.Transport.Body, out var embeddedCandidate)
                    ? embeddedCandidate
                    : null;
            clientPackets.Add(new ClientPacketBoundaryProbe(packet.Packet, packet.Transport, info, resolved ? boundary : null, embedded));
        }

        var serverNearMtu = decodedSourcePackets.Count(static packet =>
            packet.Packet.Direction == PcapActiveFlowDirection.ServerToClient
            && IsNearMtu(packet.Transport));
        var serverQueuedCandidates = decodedSourcePackets.Count(static packet =>
            packet.Packet.Direction == PcapActiveFlowDirection.ServerToClient
            && packet.Transport.ClassifyNativeFrame().Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate);
        return new PcapSourceClcMoveBoundaryFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            clientPackets.Count,
            clientPackets.Count(static packet => packet.ExactBoundary is not null),
            clientPackets.Count(static packet => packet.EmbeddedBoundaryCandidate is not null),
            clientPackets.Count(static packet => IsNearMtu(packet.Transport)),
            clientPackets.Count(static packet => IsNearMtu(packet.Transport) && packet.ExactBoundary is null),
            clientPackets.Count(static packet => IsNearMtu(packet.Transport) && packet.EmbeddedBoundaryCandidate is not null),
            clientPackets.Count(static packet => packet.Info.NativeFrameKind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate),
            serverNearMtu,
            serverQueuedCandidates,
            BuildBoundaryCounts(clientPackets),
            BuildMissCounts(clientPackets),
            BuildSamples(clientPackets));
    }

    private static PcapSourceClcMoveBoundarySummary BuildSummary(PcapSourceClcMoveBoundaryFile[] files)
    {
        var active = files.Where(static file => file.HasActiveSourceFlow).ToArray();
        var exact = active.Sum(static file => file.ExactBoundaryHitCount);
        var embedded = active.Sum(static file => file.EmbeddedBoundaryCandidateCount);
        var nearMtu = active.Sum(static file => file.NearMtuClientPacketCount);
        var nearMtuMisses = active.Sum(static file => file.NearMtuExactMissCount);
        var serverNearMtu = active.Sum(static file => file.ServerNearMtuPacketCount);
        return new PcapSourceClcMoveBoundarySummary(
            files.Length,
            active.Length,
            active.Sum(static file => file.ClientPacketCount),
            exact,
            embedded,
            nearMtu,
            nearMtuMisses,
            active.Sum(static file => file.NearMtuEmbeddedBoundaryHitCount),
            active.Sum(static file => file.QueuedPeerChannelCandidateCount),
            serverNearMtu,
            active.Sum(static file => file.ServerQueuedPeerChannelCandidateCount),
            exact > 0
                ? "exact-native-clc-move-boundaries-observed"
                : embedded > 0 && serverNearMtu > 0
                    ? "no-exact-clc-move-boundary-observed; embedded-candidates-need-native-wrapper-proof; server-near-mtu-generation-remains"
                    : embedded > 0
                        ? "no-exact-clc-move-boundary-observed; embedded-candidates-need-native-wrapper-proof"
                    : nearMtuMisses > 0
                        ? "no-clc-move-boundary-in-markerless-near-mtu-client-chunks"
                        : serverNearMtu > 0
                            ? "no-exact-clc-move-boundary-observed; server-near-mtu-generation-remains"
                            : "no-active-clc-move-boundaries-observed");
    }

    private static PcapSourceClcMoveBoundaryCount[] BuildCounts(PcapSourceClcMoveBoundaryFile[] files)
    {
        return files
            .Where(static file => file.HasActiveSourceFlow)
            .SelectMany(static file => file.BoundaryCounts.Concat(file.MissReasonCounts))
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Sum(static count => count.Count))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(64)
            .Select(static group => new PcapSourceClcMoveBoundaryCount(group.Key, group.Sum(static count => count.Count)))
            .ToArray();
    }

    private static PcapSourceClcMoveBoundaryCount[] BuildBoundaryCounts(IReadOnlyCollection<ClientPacketBoundaryProbe> packets)
    {
        return packets
            .Where(static packet => packet.ExactBoundary is not null)
            .GroupBy(static packet => packet.ExactBoundary!.Kind.ToString(), StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceClcMoveBoundaryCount(group.Key, group.Count()))
            .ToArray();
    }

    private static PcapSourceClcMoveBoundaryCount[] BuildMissCounts(IReadOnlyCollection<ClientPacketBoundaryProbe> packets)
    {
        return packets
            .Where(static packet => packet.ExactBoundary is null)
            .GroupBy(MissReason, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceClcMoveBoundaryCount(group.Key, group.Count()))
            .ToArray();
    }

    private static PcapSourceClcMoveBoundarySample[] BuildSamples(IReadOnlyCollection<ClientPacketBoundaryProbe> packets)
    {
        return packets
            .Where(static packet => packet.ExactBoundary is not null || packet.EmbeddedBoundaryCandidate is not null || IsNearMtu(packet.Transport))
            .OrderBy(static packet => packet.Packet.PacketIndex)
            .Take(48)
            .Select(static packet => new PcapSourceClcMoveBoundarySample(
                packet.Packet.PacketIndex,
                packet.Packet.TimestampMicroseconds,
                packet.Transport.CandidateSequence,
                packet.Transport.PayloadLength,
                packet.Transport.Body.Length,
                packet.Info.Role.ToString(),
                packet.Info.NativeFrameKind.ToString(),
                packet.ExactBoundary?.Kind.ToString() ?? "",
                packet.ExactBoundary?.PayloadOffset,
                packet.ExactBoundary?.PayloadLength,
                packet.ExactBoundary?.Move.NewCommands,
                packet.ExactBoundary?.Move.BackupCommands,
                packet.EmbeddedBoundaryCandidate?.Offset,
                packet.EmbeddedBoundaryCandidate?.IncludesMessageType,
                packet.EmbeddedBoundaryCandidate?.NewCommands,
                packet.EmbeddedBoundaryCandidate?.BackupCommands,
                MissReason(packet),
                Prefix(packet.Transport.Body, 16)))
            .ToArray();
    }

    private static bool TryFindEmbeddedClcMove(
        ReadOnlySpan<byte> body,
        out PcapSourceEmbeddedClcMoveCandidate candidate)
    {
        candidate = default!;
        for (var offset = 0; offset < body.Length; offset++)
        {
            var slice = body[offset..];
            foreach (var includesMessageType in new[] { true, false })
            {
                if (!Ps3SourceClcMoveMessage.TryDecode(slice, includesMessageType, out var move)
                    || move.TotalCommands == 0
                    || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
                    || move.CommandDataBitCount <= 0
                    || !move.TryDecodeUserCmdBatch(default, out var batch)
                    || batch.ConsumedBits != move.CommandDataBitCount)
                {
                    continue;
                }

                candidate = new PcapSourceEmbeddedClcMoveCandidate(
                    offset,
                    includesMessageType,
                    move.NewCommands,
                    move.BackupCommands,
                    move.CommandDataBitCount,
                    move.TotalBitsConsumed);
                return true;
            }
        }

        return false;
    }

    private static string MissReason(ClientPacketBoundaryProbe packet)
    {
        if (packet.ExactBoundary is not null)
        {
            return "exact-boundary-hit";
        }

        if (packet.EmbeddedBoundaryCandidate is not null)
        {
            return "embedded-clc-move-candidate-only";
        }

        if (IsNearMtu(packet.Transport))
        {
            return "near-mtu-markerless-queued-client-chunk";
        }

        return packet.Info.Role switch
        {
            Ps3SourceClientPayloadRole.InitialHandoffProbe => "initial-handoff-probe",
            Ps3SourceClientPayloadRole.ReliableAssociationProbe => "reliable-association-probe",
            Ps3SourceClientPayloadRole.AttachedPlayerControlFrame => "attached-player-control-frame",
            Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame => "attached-player-payload-frame-without-clc-move",
            Ps3SourceClientPayloadRole.ShortControlAck => "short-control-ack",
            Ps3SourceClientPayloadRole.SetupControlPayload => "setup-control-payload",
            Ps3SourceClientPayloadRole.EmbeddedObjectNotice => "embedded-object-notice",
            Ps3SourceClientPayloadRole.FragmentedClientPayload => "fragmented-client-payload-without-clc-move",
            Ps3SourceClientPayloadRole.UserCommandCandidate => "user-command-candidate-without-clc-move",
            _ => "binary-control-payload"
        };
    }

    private static bool IsNearMtu(Ps3SourceTransportPacket packet)
    {
        return packet.PayloadLength >= 1000 || packet.Body.Length >= 998;
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

    private sealed record ClientPacketBoundaryProbe(
        PcapActiveFlowDatagram Packet,
        Ps3SourceTransportPacket Transport,
        Ps3SourceClientPayloadInfo Info,
        Ps3SourceNativeToClcMoveBoundary? ExactBoundary,
        PcapSourceEmbeddedClcMoveCandidate? EmbeddedBoundaryCandidate);

    private sealed record DecodedSourcePacket(
        PcapActiveFlowDatagram Packet,
        Ps3SourceTransportPacket Transport);
}

public sealed record PcapSourceClcMoveBoundaryReport(
    string Status,
    PcapSourceClcMoveBoundarySummary Summary,
    PcapSourceClcMoveBoundaryCount[] TopCounts,
    PcapSourceClcMoveBoundaryFile[] Files);

public sealed record PcapSourceClcMoveBoundarySummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ClientPacketCount,
    int ExactBoundaryHitCount,
    int EmbeddedBoundaryCandidateCount,
    int NearMtuClientPacketCount,
    int NearMtuExactMissCount,
    int NearMtuEmbeddedBoundaryHitCount,
    int QueuedPeerChannelCandidateCount,
    int ServerNearMtuPacketCount,
    int ServerQueuedPeerChannelCandidateCount,
    string BoundaryConclusion);

public sealed record PcapSourceClcMoveBoundaryFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int ClientPacketCount,
    int ExactBoundaryHitCount,
    int EmbeddedBoundaryCandidateCount,
    int NearMtuClientPacketCount,
    int NearMtuExactMissCount,
    int NearMtuEmbeddedBoundaryHitCount,
    int QueuedPeerChannelCandidateCount,
    int ServerNearMtuPacketCount,
    int ServerQueuedPeerChannelCandidateCount,
    PcapSourceClcMoveBoundaryCount[] BoundaryCounts,
    PcapSourceClcMoveBoundaryCount[] MissReasonCounts,
    PcapSourceClcMoveBoundarySample[] Samples);

public sealed record PcapSourceClcMoveBoundaryCount(
    string Value,
    int Count);

public sealed record PcapSourceEmbeddedClcMoveCandidate(
    int Offset,
    bool IncludesMessageType,
    int NewCommands,
    int BackupCommands,
    int CommandDataBitCount,
    int TotalBitsConsumed);

public sealed record PcapSourceClcMoveBoundarySample(
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    string Role,
    string NativeFrameKind,
    string ExactBoundaryKind,
    int? ExactPayloadOffset,
    int? ExactPayloadLength,
    int? ExactNewCommands,
    int? ExactBackupCommands,
    int? EmbeddedOffset,
    bool? EmbeddedIncludesMessageType,
    int? EmbeddedNewCommands,
    int? EmbeddedBackupCommands,
    string MissReason,
    string BodyPrefixHex);
