using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapEmbeddedClcMoveCandidateAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapEmbeddedClcMoveCandidateReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapEmbeddedClcMoveCandidateReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath);
        var activeFiles = 0;
        var candidates = new List<PcapEmbeddedClcMoveCandidate>();
        foreach (var file in files)
        {
            var replay = _extractor.Extract(file);
            if (replay is null)
            {
                continue;
            }

            activeFiles++;
            AnalyzeReplay(inputPath, replay, candidates);
        }

        var commandCandidates = candidates.Count(static item => item.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString());
        var hardMarkerlessCandidates = candidates.Count(static item => item.IsHardMarkerless);
        var exactBoundaryCandidates = candidates.Count(static item => item.ExactBoundaryKind.Length > 0);
        var offsetGroups = CountBy(candidates.Select(static item => item.EmbeddedOffset.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var lengthGroups = CountBy(candidates.Select(static item => item.BodyLength.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var fileGroups = CountBy(candidates.Select(static item => item.File));
        var ready = false;
        return new PcapEmbeddedClcMoveCandidateReport(
            "pcap-embedded-clc-move-candidates",
            "Lists every weak embedded CLC_Move offset-scan hit with active-flow context. These are not native boundaries until TF.elf proves the offset/wrapper rule.",
            new PcapEmbeddedClcMoveCandidateSummary(
                files.Length,
                activeFiles,
                candidates.Count,
                commandCandidates,
                hardMarkerlessCandidates,
                exactBoundaryCandidates,
                candidates.Count(static item => item.IncludesMessageType),
                candidates.Count(static item => !item.IncludesMessageType),
                ready,
                candidates.Count == 0
                    ? "no embedded offset-scan CLC_Move candidates found"
                    : hardMarkerlessCandidates > 0
                        ? "embedded CLC_Move candidates overlap the strict hard markerless set; prove the offset rule in TF.elf before accepting"
                        : "embedded CLC_Move candidates exist only outside the strict hard markerless set or as weak offset hits; do not use as production boundaries"),
            offsetGroups,
            lengthGroups,
            fileGroups,
            candidates
                .OrderBy(static item => item.File, StringComparer.Ordinal)
                .ThenBy(static item => item.SourceStep)
                .ToArray(),
            [
                "Offset-scan hits are treated as leads only. They are intentionally excluded from NativeSourceInputReady.",
                "A valid production boundary must be backed by a TF.elf function that selects the same offset/payload length before invoking category 5 / CLC_Move decoding.",
                "The current strict corpus still has zero exact native CLC_Move boundary hits."
            ]);
    }

    private static void AnalyzeReplay(
        string inputRoot,
        PcapActiveFlowReplay replay,
        List<PcapEmbeddedClcMoveCandidate> output)
    {
        ushort? previousClientSequence = null;
        var clientPacketCount = 0;
        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var packet = replay.SourcePackets[sourceStep];
            if (packet.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
            {
                continue;
            }

            clientPacketCount++;
            var sequenceDelta = previousClientSequence is null
                ? (int?)null
                : Ps3SourceTransportPacket.SequenceDelta(previousClientSequence.Value, transport.CandidateSequence);
            previousClientSequence = transport.CandidateSequence;
            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientPacketCount, sequenceDelta);
            if (Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var exactBoundary))
            {
                AddCandidateIfEmbedded(
                    inputRoot,
                    replay,
                    sourceStep,
                    packet,
                    transport,
                    info,
                    exactBoundary.Kind.ToString(),
                    output);
                continue;
            }

            AddCandidateIfEmbedded(
                inputRoot,
                replay,
                sourceStep,
                packet,
                transport,
                info,
                "",
                output);
        }
    }

    private static void AddCandidateIfEmbedded(
        string inputRoot,
        PcapActiveFlowReplay replay,
        int sourceStep,
        PcapActiveFlowDatagram packet,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info,
        string exactBoundaryKind,
        List<PcapEmbeddedClcMoveCandidate> output)
    {
        if (!TryFindEmbeddedClcMove(transport.Body, out var embedded))
        {
            return;
        }

        var previousServer = FindNeighbor(replay, sourceStep, PcapActiveFlowDirection.ServerToClient, -1);
        var nextServer = FindNeighbor(replay, sourceStep, PcapActiveFlowDirection.ServerToClient, 1);
        var previousClient = FindNeighbor(replay, sourceStep, PcapActiveFlowDirection.ClientToServer, -1);
        var nextClient = FindNeighbor(replay, sourceStep, PcapActiveFlowDirection.ClientToServer, 1);
        output.Add(new PcapEmbeddedClcMoveCandidate(
            Path.GetRelativePath(inputRoot, replay.Path),
            sourceStep,
            packet.PacketIndex,
            transport.CandidateSequence,
            info.SequenceDelta,
            packet.TimestampMicroseconds,
            transport.PayloadLength,
            transport.Body.Length,
            info.Role.ToString(),
            info.Shape.ToString(),
            info.NativeFrameKind.ToString(),
            IsHardMarkerless(info),
            info.BodyPrefixHex,
            Suffix(transport.Body, 8),
            info.PayloadObjectFrameKind,
            info.PayloadObjectHeaderValue,
            info.PayloadObjectInnerPayloadOffset,
            info.PayloadObjectInnerPayloadLength,
            info.PayloadObjectAssociatedToken,
            exactBoundaryKind,
            embedded.Offset,
            embedded.IncludesMessageType,
            embedded.NewCommands,
            embedded.BackupCommands,
            embedded.CommandDataBitCount,
            embedded.TotalBitsConsumed,
            HexWindow(transport.Body, embedded.Offset, 24),
            previousClient,
            nextClient,
            previousServer,
            nextServer));
    }

    private static PcapEmbeddedNeighborPacket? FindNeighbor(
        PcapActiveFlowReplay replay,
        int sourceStep,
        PcapActiveFlowDirection direction,
        int step)
    {
        for (var index = sourceStep + step; index >= 0 && index < replay.SourcePackets.Length; index += step)
        {
            var packet = replay.SourcePackets[index];
            if (packet.Direction != direction
                || !Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
            {
                continue;
            }

            var semantic = Ps3SourcePayloadSemantics.Analyze(transport.Body);
            return new PcapEmbeddedNeighborPacket(
                index,
                packet.PacketIndex,
                transport.CandidateSequence,
                transport.PayloadLength,
                transport.Body.Length,
                semantic.Role.ToString(),
                Prefix(transport.Body, 12));
        }

        return null;
    }

    private static bool TryFindEmbeddedClcMove(ReadOnlySpan<byte> body, out PcapSourceEmbeddedClcMoveCandidate candidate)
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

    private static bool IsHardMarkerless(Ps3SourceClientPayloadInfo info)
    {
        return info.Role == Ps3SourceClientPayloadRole.UserCommandCandidate
            && info.AttachedFrameKind is null
            && info.BitSidecarOffset is null
            && info.PayloadObjectFrameKind is not nameof(Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control)
            && !IsPayloadObjectInnerDecodeCandidate(info)
            && info.DecodedNetMessageName is null;
    }

    private static bool IsPayloadObjectInnerDecodeCandidate(Ps3SourceClientPayloadInfo info)
    {
        if (info.PayloadObjectFrameKind is null
            || info.PayloadObjectInnerPayloadOffset is null
            || info.PayloadObjectInnerPayloadLength is null)
        {
            return false;
        }

        return info.PayloadObjectFrameKind != nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken)
            || info.PayloadObjectAssociatedToken is > 0 and <= 0xffff;
    }

    private static PcapEmbeddedClcMoveCandidateCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapEmbeddedClcMoveCandidateCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string[] EnumeratePcapInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return [inputPath];
        }

        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException(inputPath);
        }

        return Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string HexWindow(ReadOnlySpan<byte> body, int offset, int bytes)
    {
        var start = Math.Max(0, offset - bytes);
        var end = Math.Min(body.Length, offset + bytes);
        return Convert.ToHexString(body[start..end]).ToLowerInvariant();
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();
    }

    private static string Suffix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[^Math.Min(length, body.Length)..]).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapEmbeddedClcMoveCandidateReport(
    string Status,
    string Note,
    PcapEmbeddedClcMoveCandidateSummary Summary,
    PcapEmbeddedClcMoveCandidateCount[] OffsetCounts,
    PcapEmbeddedClcMoveCandidateCount[] BodyLengthCounts,
    PcapEmbeddedClcMoveCandidateCount[] FileCounts,
    PcapEmbeddedClcMoveCandidate[] Candidates,
    string[] Conclusions);

public sealed record PcapEmbeddedClcMoveCandidateSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int EmbeddedCandidateCount,
    int UserCommandCandidateCount,
    int HardMarkerlessCandidateCount,
    int ExactBoundaryCandidateCount,
    int IncludesMessageTypeCount,
    int PayloadOnlyCount,
    bool NativeBoundaryReady,
    string BoundaryConclusion);

public sealed record PcapEmbeddedClcMoveCandidate(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    long TimestampMicroseconds,
    int PayloadLength,
    int BodyLength,
    string Role,
    string Shape,
    string NativeFrameKind,
    bool IsHardMarkerless,
    string BodyPrefixHex,
    string BodySuffixHex,
    string? PayloadObjectFrameKind,
    uint? PayloadObjectHeaderValue,
    int? PayloadObjectInnerPayloadOffset,
    int? PayloadObjectInnerPayloadLength,
    uint? PayloadObjectAssociatedToken,
    string ExactBoundaryKind,
    int EmbeddedOffset,
    bool IncludesMessageType,
    int NewCommands,
    int BackupCommands,
    int CommandDataBitCount,
    int TotalBitsConsumed,
    string CandidateWindowHex,
    PcapEmbeddedNeighborPacket? PreviousClientPacket,
    PcapEmbeddedNeighborPacket? NextClientPacket,
    PcapEmbeddedNeighborPacket? PreviousServerPacket,
    PcapEmbeddedNeighborPacket? NextServerPacket);

public sealed record PcapEmbeddedNeighborPacket(
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    string SemanticRole,
    string BodyPrefixHex);

public sealed record PcapEmbeddedClcMoveCandidateCount(string Key, int Count);
