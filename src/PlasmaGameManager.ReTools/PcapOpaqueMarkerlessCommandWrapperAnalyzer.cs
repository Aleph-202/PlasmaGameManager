using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapOpaqueMarkerlessCommandWrapperAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapOpaqueMarkerlessCommandWrapperReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapOpaqueMarkerlessCommandWrapperReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath);
        var packets = new List<OpaqueMarkerlessCommandPacket>();
        var activeFiles = 0;
        foreach (var file in files)
        {
            var replay = _extractor.Extract(file);
            if (replay is null)
            {
                continue;
            }

            activeFiles++;
            AnalyzeReplay(inputPath, replay, packets);
        }

        var summary = BuildSummary(files.Length, activeFiles, packets);
        return new PcapOpaqueMarkerlessCommandWrapperReport(
            "pcap-opaque-markerless-command-wrapper",
            summary,
            BuildLengthFamilies(packets),
            BuildFileSummaries(packets),
            BuildPhaseCounts(packets),
            BuildSamples(packets),
            [
                "This report filters to client packets that are UserCommandCandidate, direct markerless datagrams, and not visible CLC_Move, attached-frame, bit-sidecar, embedded-object, or decoded non-move NET/CLC messages.",
                "The dominant opaque families are high-entropy, variable-length direct datagrams with mostly unique prefixes/suffixes; this points at an outer PS3 wrapper/transform before the recovered TF.elf CLC_Move reader.",
                "The next native decompile target remains the receive-side function that classifies these markerless bodies and chooses when to construct NET/CLC bit readers for 008c8a50/00a291c0."
            ]);
    }

    private static void AnalyzeReplay(string inputRoot, PcapActiveFlowReplay replay, List<OpaqueMarkerlessCommandPacket> output)
    {
        ushort? previousClientSequence = null;
        long? previousClientTimestamp = null;
        var clientPacketCount = 0;
        var firstTimestamp = replay.SourcePackets.Length == 0 ? 0 : replay.SourcePackets[0].TimestampMicroseconds;

        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var packet = replay.SourcePackets[sourceStep];
            if (packet.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
            {
                continue;
            }

            clientPacketCount++;
            int? sequenceDelta = previousClientSequence is null
                ? null
                : Ps3SourceTransportPacket.SequenceDelta(previousClientSequence.Value, transport.CandidateSequence);
            var clientGapMilliseconds = previousClientTimestamp is null
                ? (double?)null
                : Math.Round((packet.TimestampMicroseconds - previousClientTimestamp.Value) / 1000.0, 3);
            previousClientSequence = transport.CandidateSequence;
            previousClientTimestamp = packet.TimestampMicroseconds;

            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientPacketCount, sequenceDelta);
            if (info.Role != Ps3SourceClientPayloadRole.UserCommandCandidate
                || info.AttachedFrameKind is not null
                || info.BitSidecarBitCount is not null
                || info.DecodedNetMessageName is not null
                || Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out _)
                || TryFindEmbeddedClcMove(transport.Body, out _))
            {
                continue;
            }

            var semantic = Ps3SourcePayloadSemantics.Analyze(transport.Body);
            Ps3SourceClientCommandIntent.TryDecode(info, transport.Body, out var intent);
            var previousServerBodyLength = FindNeighborServerBodyLength(replay, sourceStep, -1);
            var nextServerBodyLength = FindNeighborServerBodyLength(replay, sourceStep, 1);
            var nextServerDelayMilliseconds = FindNextServerDelayMilliseconds(replay, sourceStep, packet.TimestampMicroseconds);
            output.Add(new OpaqueMarkerlessCommandPacket(
                Path.GetRelativePath(inputRoot, replay.Path),
                sourceStep,
                packet.PacketIndex,
                Math.Round((packet.TimestampMicroseconds - firstTimestamp) / 1000.0, 3),
                transport.CandidateSequence,
                sequenceDelta,
                clientGapMilliseconds,
                transport.PayloadLength,
                transport.Body.Length,
                PhaseFor(sourceStep, Math.Round((packet.TimestampMicroseconds - firstTimestamp) / 1000.0, 3)),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                Math.Round(semantic.Entropy, 4),
                Math.Round(semantic.PrintableRatio, 4),
                Prefix(transport.Body, 8),
                Prefix(transport.Body, 16),
                Suffix(transport.Body, 8),
                ReadUInt16BigEndian(transport.Body, 0),
                ReadUInt16LittleEndian(transport.Body, 0),
                ReadUInt32BigEndian(transport.Body, 0),
                ReadUInt32LittleEndian(transport.Body, 0),
                previousServerBodyLength,
                nextServerBodyLength,
                nextServerDelayMilliseconds,
                intent?.ForwardMove,
                intent?.SideMove,
                intent?.UpMove,
                intent?.Buttons,
                intent?.WeaponSlotHint,
                intent?.TeamHint,
                intent?.ClassHint));
        }
    }

    private static PcapOpaqueMarkerlessCommandWrapperSummary BuildSummary(
        int fileCount,
        int activeFileCount,
        IReadOnlyCollection<OpaqueMarkerlessCommandPacket> packets)
    {
        var entropy = packets.Select(static packet => packet.Entropy).ToArray();
        var bodyLengths = packets.Select(static packet => packet.BodyLength).ToArray();
        var uniquePrefixes = packets.Select(static packet => packet.BodyPrefix8Hex).Distinct(StringComparer.Ordinal).Count();
        var uniqueSuffixes = packets.Select(static packet => packet.BodySuffix8Hex).Distinct(StringComparer.Ordinal).Count();
        return new PcapOpaqueMarkerlessCommandWrapperSummary(
            fileCount,
            activeFileCount,
            packets.Count,
            bodyLengths.Distinct().Count(),
            bodyLengths.Length == 0 ? 0 : bodyLengths.Min(),
            bodyLengths.Length == 0 ? 0 : bodyLengths.Max(),
            bodyLengths.Length == 0 ? 0.0 : Math.Round(bodyLengths.Average(), 3),
            entropy.Length == 0 ? 0.0 : Math.Round(entropy.Average(), 4),
            packets.Count(static packet => packet.Entropy >= 5.0),
            packets.Count(static packet => packet.PrintableRatio >= 0.5),
            uniquePrefixes,
            packets.Count == 0 ? 0.0 : Math.Round(uniquePrefixes / (double)packets.Count, 4),
            uniqueSuffixes,
            packets.Count == 0 ? 0.0 : Math.Round(uniqueSuffixes / (double)packets.Count, 4),
            packets.Count(static packet => packet.SequenceDelta == 20),
            packets.Count(static packet => packet.NextServerBodyLength is >= 1024),
            "opaque-direct-markerless-wrapper-remains; no visible CLC_Move/attached/bit-sidecar/netmessage boundary");
    }

    private static PcapOpaqueMarkerlessCommandLengthFamily[] BuildLengthFamilies(IReadOnlyCollection<OpaqueMarkerlessCommandPacket> packets)
    {
        return packets
            .GroupBy(static packet => packet.BodyLength)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Take(64)
            .Select(static group => new PcapOpaqueMarkerlessCommandLengthFamily(
                group.Key,
                group.Count(),
                group.Select(static packet => packet.File).Distinct(StringComparer.Ordinal).Count(),
                Math.Round(group.Average(static packet => packet.Entropy), 4),
                Math.Round(group.Average(static packet => packet.PrintableRatio), 4),
                CountBy(group.Select(static packet => packet.Phase)).Take(6).ToArray(),
                CountBy(group.Select(static packet => packet.SequenceDelta?.ToString())).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.PreviousServerBodyLength?.ToString())).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.NextServerBodyLength?.ToString())).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.HeuristicButtons?.ToString("x2"))).Take(8).ToArray(),
                group
                    .OrderBy(static packet => packet.File, StringComparer.Ordinal)
                    .ThenBy(static packet => packet.SourceStep)
                    .Take(8)
                    .Select(static packet => packet.ToSample())
                    .ToArray()))
            .ToArray();
    }

    private static PcapOpaqueMarkerlessCommandFileSummary[] BuildFileSummaries(IReadOnlyCollection<OpaqueMarkerlessCommandPacket> packets)
    {
        return packets
            .GroupBy(static packet => packet.File, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapOpaqueMarkerlessCommandFileSummary(
                group.Key,
                group.Count(),
                group.Min(static packet => packet.SourceStep),
                group.Max(static packet => packet.SourceStep),
                group.Select(static packet => packet.BodyLength).Distinct().Count(),
                Math.Round(group.Average(static packet => packet.Entropy), 4),
                CountBy(group.Select(static packet => packet.BodyLength.ToString())).Take(12).ToArray(),
                CountBy(group.Select(static packet => packet.Phase)).Take(6).ToArray()))
            .ToArray();
    }

    private static PcapOpaqueMarkerlessCommandCount[] BuildPhaseCounts(IReadOnlyCollection<OpaqueMarkerlessCommandPacket> packets)
    {
        return CountBy(packets.Select(static packet => packet.Phase));
    }

    private static PcapOpaqueMarkerlessCommandSample[] BuildSamples(IReadOnlyCollection<OpaqueMarkerlessCommandPacket> packets)
    {
        return packets
            .OrderByDescending(static packet => packet.BodyLength)
            .ThenBy(static packet => packet.File, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .Take(128)
            .Select(static packet => packet.ToSample())
            .ToArray();
    }

    private static int? FindNeighborServerBodyLength(PcapActiveFlowReplay replay, int sourceStep, int direction)
    {
        for (var i = sourceStep + direction; i >= 0 && i < replay.SourcePackets.Length; i += direction)
        {
            var candidate = replay.SourcePackets[i];
            if (candidate.Direction == PcapActiveFlowDirection.ServerToClient
                && Ps3SourceTransportPacket.TryDecode(candidate.Payload, out var transport))
            {
                return transport.Body.Length;
            }
        }

        return null;
    }

    private static double? FindNextServerDelayMilliseconds(PcapActiveFlowReplay replay, int sourceStep, long timestamp)
    {
        for (var i = sourceStep + 1; i < replay.SourcePackets.Length; i++)
        {
            var candidate = replay.SourcePackets[i];
            if (candidate.Direction == PcapActiveFlowDirection.ServerToClient)
            {
                return Math.Round((candidate.TimestampMicroseconds - timestamp) / 1000.0, 3);
            }
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

    private static string PhaseFor(int sourceStep, double elapsedMilliseconds)
    {
        if (sourceStep < 32 || elapsedMilliseconds < 500.0)
        {
            return "inferred-source-handoff-setup";
        }

        if (sourceStep < 372 || elapsedMilliseconds < 15000.0)
        {
            return "inferred-loading-or-motd-transfer";
        }

        return "inferred-gameplay-steady-traffic";
    }

    private static PcapOpaqueMarkerlessCommandCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapOpaqueMarkerlessCommandCount(group.Key, group.Count()))
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

    private static ushort? ReadUInt16BigEndian(ReadOnlySpan<byte> body, int offset)
    {
        return offset < 0 || body.Length < offset + 2 ? null : (ushort)((body[offset] << 8) | body[offset + 1]);
    }

    private static ushort? ReadUInt16LittleEndian(ReadOnlySpan<byte> body, int offset)
    {
        return offset < 0 || body.Length < offset + 2 ? null : (ushort)(body[offset] | (body[offset + 1] << 8));
    }

    private static uint? ReadUInt32BigEndian(ReadOnlySpan<byte> body, int offset)
    {
        return offset < 0 || body.Length < offset + 4
            ? null
            : (uint)((body[offset] << 24) | (body[offset + 1] << 16) | (body[offset + 2] << 8) | body[offset + 3]);
    }

    private static uint? ReadUInt32LittleEndian(ReadOnlySpan<byte> body, int offset)
    {
        return offset < 0 || body.Length < offset + 4
            ? null
            : (uint)(body[offset] | (body[offset + 1] << 8) | (body[offset + 2] << 16) | (body[offset + 3] << 24));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record OpaqueMarkerlessCommandPacket(
        string File,
        int SourceStep,
        long PacketIndex,
        double ElapsedMilliseconds,
        int Sequence,
        int? SequenceDelta,
        double? ClientGapMilliseconds,
        int PayloadLength,
        int BodyLength,
        string Phase,
        string NativeFrameKind,
        string Shape,
        double Entropy,
        double PrintableRatio,
        string BodyPrefix8Hex,
        string BodyPrefix16Hex,
        string BodySuffix8Hex,
        ushort? FirstUInt16BigEndian,
        ushort? FirstUInt16LittleEndian,
        uint? FirstUInt32BigEndian,
        uint? FirstUInt32LittleEndian,
        int? PreviousServerBodyLength,
        int? NextServerBodyLength,
        double? NextServerDelayMilliseconds,
        short? HeuristicForwardMove,
        short? HeuristicSideMove,
        short? HeuristicUpMove,
        byte? HeuristicButtons,
        byte? HeuristicWeaponSlotHint,
        byte? HeuristicTeamHint,
        byte? HeuristicClassHint)
    {
        public PcapOpaqueMarkerlessCommandSample ToSample()
        {
            return new PcapOpaqueMarkerlessCommandSample(
                File,
                SourceStep,
                PacketIndex,
                ElapsedMilliseconds,
                Sequence,
                SequenceDelta,
                ClientGapMilliseconds,
                PayloadLength,
                BodyLength,
                Phase,
                NativeFrameKind,
                Shape,
                Entropy,
                PrintableRatio,
                BodyPrefix16Hex,
                BodySuffix8Hex,
                FirstUInt16BigEndian,
                FirstUInt16LittleEndian,
                FirstUInt32BigEndian,
                FirstUInt32LittleEndian,
                PreviousServerBodyLength,
                NextServerBodyLength,
                NextServerDelayMilliseconds,
                HeuristicForwardMove,
                HeuristicSideMove,
                HeuristicUpMove,
                HeuristicButtons,
                HeuristicWeaponSlotHint,
                HeuristicTeamHint,
                HeuristicClassHint);
        }
    }
}

public sealed record PcapOpaqueMarkerlessCommandWrapperReport(
    string Status,
    PcapOpaqueMarkerlessCommandWrapperSummary Summary,
    PcapOpaqueMarkerlessCommandLengthFamily[] LengthFamilies,
    PcapOpaqueMarkerlessCommandFileSummary[] Files,
    PcapOpaqueMarkerlessCommandCount[] PhaseCounts,
    PcapOpaqueMarkerlessCommandSample[] Samples,
    string[] Conclusions);

public sealed record PcapOpaqueMarkerlessCommandWrapperSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int OpaquePacketCount,
    int DistinctBodyLengthCount,
    int MinBodyLength,
    int MaxBodyLength,
    double AverageBodyLength,
    double AverageEntropy,
    int HighEntropyPacketCount,
    int HighPrintableRatioPacketCount,
    int UniquePrefix8Count,
    double UniquePrefix8Ratio,
    int UniqueSuffix8Count,
    double UniqueSuffix8Ratio,
    int SequenceDelta20Count,
    int FollowedByNearMtuServerPacketCount,
    string BoundaryConclusion);

public sealed record PcapOpaqueMarkerlessCommandLengthFamily(
    int BodyLength,
    int Count,
    int DistinctFileCount,
    double AverageEntropy,
    double AveragePrintableRatio,
    PcapOpaqueMarkerlessCommandCount[] PhaseCounts,
    PcapOpaqueMarkerlessCommandCount[] SequenceDeltaCounts,
    PcapOpaqueMarkerlessCommandCount[] PreviousServerBodyLengthCounts,
    PcapOpaqueMarkerlessCommandCount[] NextServerBodyLengthCounts,
    PcapOpaqueMarkerlessCommandCount[] HeuristicButtonCounts,
    PcapOpaqueMarkerlessCommandSample[] Samples);

public sealed record PcapOpaqueMarkerlessCommandFileSummary(
    string File,
    int Count,
    int FirstSourceStep,
    int LastSourceStep,
    int DistinctBodyLengthCount,
    double AverageEntropy,
    PcapOpaqueMarkerlessCommandCount[] TopBodyLengthCounts,
    PcapOpaqueMarkerlessCommandCount[] PhaseCounts);

public sealed record PcapOpaqueMarkerlessCommandCount(string Value, int Count);

public sealed record PcapOpaqueMarkerlessCommandSample(
    string File,
    int SourceStep,
    long PacketIndex,
    double ElapsedMilliseconds,
    int Sequence,
    int? SequenceDelta,
    double? ClientGapMilliseconds,
    int PayloadLength,
    int BodyLength,
    string Phase,
    string NativeFrameKind,
    string Shape,
    double Entropy,
    double PrintableRatio,
    string BodyPrefix16Hex,
    string BodySuffix8Hex,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    int? PreviousServerBodyLength,
    int? NextServerBodyLength,
    double? NextServerDelayMilliseconds,
    short? HeuristicForwardMove,
    short? HeuristicSideMove,
    short? HeuristicUpMove,
    byte? HeuristicButtons,
    byte? HeuristicWeaponSlotHint,
    byte? HeuristicTeamHint,
    byte? HeuristicClassHint);
