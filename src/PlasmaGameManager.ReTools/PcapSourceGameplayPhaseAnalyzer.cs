using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceGameplayPhaseAnalyzer
{
    private const long GapSegmentThresholdMicroseconds = 1_000_000;
    private const long SetupWindowMicroseconds = 2_000_000;
    private const long LoadingWindowMicroseconds = 15_000_000;
    private const int MinimumSetupPackets = 32;

    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceGameplayPhaseReport> AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapSourceGameplayPhaseReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var analyses = files.Select(file => AnalyzeFile(inputDirectory, file)).ToArray();
        return new PcapSourceGameplayPhaseReport(BuildSummary(analyses), analyses);
    }

    private PcapSourceGameplayPhaseFile AnalyzeFile(string inputDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceGameplayPhaseFile(
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
                "",
                Array.Empty<PcapSourceGameplayPhaseSegment>(),
                Array.Empty<PcapSourceGameplayPhaseSegment>());
        }

        var packets = replay.SourcePackets;
        var windows = BuildInferredWindows(packets);
        var gapSegments = BuildGapSegments(packets);
        return new PcapSourceGameplayPhaseFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.ServerPort,
            replay.FirstSourceClientPacketIndex,
            packets.Length,
            packets.Count(static packet => packet.Direction == PcapActiveFlowDirection.ClientToServer),
            packets.Count(static packet => packet.Direction == PcapActiveFlowDirection.ServerToClient),
            DurationMilliseconds(packets),
            MaxInterArrivalMilliseconds(packets),
            CountDirectionSwitches(packets),
            ClassifyFile(windows),
            windows,
            gapSegments);
    }

    private static PcapSourceGameplayPhaseSummary BuildSummary(PcapSourceGameplayPhaseFile[] files)
    {
        var active = files.Where(static file => file.HasActiveSourceFlow).ToArray();
        var longSessions = active.Where(static file => file.DurationMilliseconds >= 30_000 || file.SourcePacketCount >= 2_000).ToArray();
        return new PcapSourceGameplayPhaseSummary(
            files.Length,
            active.Length,
            longSessions.Length,
            active.Sum(static file => file.SourcePacketCount),
            active.Sum(static file => file.ClientToServerPacketCount),
            active.Sum(static file => file.ServerToClientPacketCount),
            active.Length == 0 ? 0 : active.Max(static file => file.DurationMilliseconds),
            active.Length == 0 ? 0 : active.Max(static file => file.MaxInterArrivalMilliseconds),
            active.Sum(static file => file.InferredWindows.Length),
            active.Sum(static file => file.GapSegments.Length),
            active.Count(static file => file.SessionClassification == "long-gameplay-session"),
            active.Count(static file => file.InferredWindows.Any(static segment => segment.Label == "inferred-gameplay-steady-traffic")),
            BuildTopCounts(active.SelectMany(static file => file.InferredWindows.Select(static segment => segment.Label)), 16));
    }

    private static PcapSourceGameplayPhaseSegment[] BuildInferredWindows(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        if (packets.Count == 0)
        {
            return Array.Empty<PcapSourceGameplayPhaseSegment>();
        }

        var segments = new List<PcapSourceGameplayPhaseSegment>();
        var setupEnd = FindSetupEnd(packets);
        AddSegment(segments, "inferred-source-handoff-setup", packets, 0, setupEnd);

        if (setupEnd >= packets.Count)
        {
            return segments.ToArray();
        }

        var loadingEnd = FindLoadingEnd(packets, setupEnd);
        AddSegment(segments, "inferred-loading-or-motd-transfer", packets, setupEnd, loadingEnd);

        if (loadingEnd < packets.Count)
        {
            AddSegment(segments, "inferred-gameplay-steady-traffic", packets, loadingEnd, packets.Count);
        }

        return segments.ToArray();
    }

    private static int FindSetupEnd(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        var firstTimestamp = packets[0].TimestampMicroseconds;
        var index = 0;
        while (index < packets.Count
            && (index < MinimumSetupPackets
                || packets[index].TimestampMicroseconds - firstTimestamp <= SetupWindowMicroseconds))
        {
            index++;
        }

        return Math.Min(index, packets.Count);
    }

    private static int FindLoadingEnd(IReadOnlyList<PcapActiveFlowDatagram> packets, int start)
    {
        if (start >= packets.Count)
        {
            return packets.Count;
        }

        var firstTimestamp = packets[0].TimestampMicroseconds;
        var index = start;
        while (index < packets.Count && packets[index].TimestampMicroseconds - firstTimestamp <= LoadingWindowMicroseconds)
        {
            index++;
        }

        return Math.Min(index, packets.Count);
    }

    private static PcapSourceGameplayPhaseSegment[] BuildGapSegments(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        if (packets.Count == 0)
        {
            return Array.Empty<PcapSourceGameplayPhaseSegment>();
        }

        var segments = new List<PcapSourceGameplayPhaseSegment>();
        var start = 0;
        for (var index = 1; index < packets.Count; index++)
        {
            var gap = packets[index].TimestampMicroseconds - packets[index - 1].TimestampMicroseconds;
            if (gap >= GapSegmentThresholdMicroseconds)
            {
                AddSegment(segments, "observed-gap-delimited-run", packets, start, index);
                start = index;
            }
        }

        AddSegment(segments, "observed-gap-delimited-run", packets, start, packets.Count);
        return segments.ToArray();
    }

    private static void AddSegment(
        List<PcapSourceGameplayPhaseSegment> segments,
        string label,
        IReadOnlyList<PcapActiveFlowDatagram> packets,
        int start,
        int endExclusive)
    {
        if (start >= endExclusive)
        {
            return;
        }

        var segmentPackets = packets.Skip(start).Take(endExclusive - start).ToArray();
        segments.Add(BuildSegment(segments.Count, label, segmentPackets));
    }

    private static PcapSourceGameplayPhaseSegment BuildSegment(
        int segmentIndex,
        string label,
        PcapActiveFlowDatagram[] packets)
    {
        var clientPackets = packets.Where(static packet => packet.Direction == PcapActiveFlowDirection.ClientToServer).ToArray();
        var serverPackets = packets.Where(static packet => packet.Direction == PcapActiveFlowDirection.ServerToClient).ToArray();
        var observations = packets
            .Select(packet =>
            {
                if (!Ps3SourceTransportPacket.TryDecode(packet.Payload, out var decoded))
                {
                    return null;
                }

                var frame = decoded.ClassifyNativeFrame();
                var semantic = Ps3SourcePayloadSemantics.Analyze(decoded.Body);
                return new DecodedPhasePacket(packet, decoded, Ps3SourceGameplaySession.ClassifyShape(decoded), frame, semantic);
            })
            .OfType<DecodedPhasePacket>()
            .ToArray();
        var embeddedRecords = observations
            .SelectMany(static item => Ps3SourceEmbeddedObjectRecords.Extract(item.Decoded.Body))
            .ToArray();

        return new PcapSourceGameplayPhaseSegment(
            segmentIndex,
            label,
            packets[0].PacketIndex,
            packets[^1].PacketIndex,
            packets[0].TimestampMicroseconds,
            packets[^1].TimestampMicroseconds,
            DurationMilliseconds(packets),
            packets.Length,
            clientPackets.Length,
            serverPackets.Length,
            CountDirectionSwitches(packets),
            FirstSequence(clientPackets),
            LastSequence(clientPackets),
            FirstSequence(serverPackets),
            LastSequence(serverPackets),
            packets.Length == 0 ? 0 : packets.Min(static packet => packet.Payload.Length),
            packets.Length == 0 ? 0 : packets.Max(static packet => packet.Payload.Length),
            packets.Length == 0 ? 0 : Math.Round(packets.Average(static packet => packet.Payload.Length), 2),
            BuildTopCounts(packets.Select(static packet => packet.Payload.Length.ToString()), 12),
            BuildTopCounts(observations.Select(static item => item.Shape.ToString()), 12),
            BuildTopCounts(observations.Select(static item => item.Frame.Kind.ToString()), 12),
            BuildTopCounts(observations.Select(static item => item.Packet.Direction.ToString()), 2),
            BuildTopCounts(observations.Select(static item => item.PayloadSemantic.Role.ToString()), 12),
            observations.Count(static item => item.Frame.Kind == Ps3SourceNativeFrameKind.FragmentedSendCandidate),
            observations.Count(static item => item.Frame.Kind is Ps3SourceNativeFrameKind.FragmentedSendCandidate
                or Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate),
            observations.Count(static item => item.Shape == Ps3SourceGameplayPacketShape.HighEntropyBinary),
            observations.Count(static item => item.Shape == Ps3SourceGameplayPacketShape.ShortControl),
            embeddedRecords.Length,
            BuildTopCounts(embeddedRecords.Select(static record => record.Role.ToString()), 12),
            BuildTopCounts(embeddedRecords.Select(RoleSchemaKey), 16),
            Math.Round(observations.Length == 0 ? 0 : observations.Average(static item => Entropy(item.Decoded.Body)), 3));
    }

    private static string ClassifyFile(IReadOnlyList<PcapSourceGameplayPhaseSegment> windows)
    {
        var totalPackets = windows.Sum(static window => window.PacketCount);
        var totalDuration = windows.Sum(static window => window.DurationMilliseconds);
        var hasGameplayWindow = windows.Any(static window =>
            window.Label == "inferred-gameplay-steady-traffic" && window.PacketCount >= 100);
        if (hasGameplayWindow && (totalPackets >= 2_000 || totalDuration >= 30_000))
        {
            return "long-gameplay-session";
        }

        if (hasGameplayWindow)
        {
            return "source-handoff-with-gameplay-tail";
        }

        return "source-handoff-short-session";
    }

    private static PcapSourceGameplayPhaseCount[] BuildTopCounts(IEnumerable<string> values, int take)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(take)
            .Select(static group => new PcapSourceGameplayPhaseCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string RoleSchemaKey(Ps3SourceEmbeddedObjectRecord record)
    {
        return $"{record.Role}:{record.Marker}:{record.Length}:v{record.Version?.ToString() ?? "?"}:c{Hex(record.FieldC)}";
    }

    private static string Hex(uint? value)
    {
        return value is null ? "none" : value.Value.ToString("x8");
    }

    private static double DurationMilliseconds(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        if (packets.Count < 2)
        {
            return 0;
        }

        return Math.Round((packets[^1].TimestampMicroseconds - packets[0].TimestampMicroseconds) / 1000.0, 3);
    }

    private static double MaxInterArrivalMilliseconds(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        if (packets.Count < 2)
        {
            return 0;
        }

        var max = 0L;
        for (var index = 1; index < packets.Count; index++)
        {
            max = Math.Max(max, packets[index].TimestampMicroseconds - packets[index - 1].TimestampMicroseconds);
        }

        return Math.Round(max / 1000.0, 3);
    }

    private static int CountDirectionSwitches(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        var count = 0;
        for (var index = 1; index < packets.Count; index++)
        {
            if (packets[index].Direction != packets[index - 1].Direction)
            {
                count++;
            }
        }

        return count;
    }

    private static int? FirstSequence(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        return packets.Count > 0 && Ps3SourceTransportPacket.TryDecode(packets[0].Payload, out var packet)
            ? packet.CandidateSequence
            : null;
    }

    private static int? LastSequence(IReadOnlyList<PcapActiveFlowDatagram> packets)
    {
        return packets.Count > 0 && Ps3SourceTransportPacket.TryDecode(packets[^1].Payload, out var packet)
            ? packet.CandidateSequence
            : null;
    }

    private static double Entropy(byte[] body)
    {
        if (body.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in body)
        {
            counts[b]++;
        }

        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = count / (double)body.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }

    private sealed record DecodedPhasePacket(
        PcapActiveFlowDatagram Packet,
        Ps3SourceTransportPacket Decoded,
        Ps3SourceGameplayPacketShape Shape,
        Ps3SourceNativeFrameInfo Frame,
        Ps3SourcePayloadSemanticInfo PayloadSemantic);
}

public sealed record PcapSourceGameplayPhaseReport(
    PcapSourceGameplayPhaseSummary Summary,
    PcapSourceGameplayPhaseFile[] Files);

public sealed record PcapSourceGameplayPhaseSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int LongGameplaySessionCount,
    int SourcePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    double MaxDurationMilliseconds,
    double MaxInterArrivalMilliseconds,
    int InferredWindowCount,
    int GapSegmentCount,
    int ClassifiedLongGameplaySessionCount,
    int FilesWithSteadyGameplayWindowCount,
    PcapSourceGameplayPhaseCount[] InferredWindowLabelCounts);

public sealed record PcapSourceGameplayPhaseFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int ServerPort,
    long FirstSourceClientPacketIndex,
    int SourcePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    double DurationMilliseconds,
    double MaxInterArrivalMilliseconds,
    int DirectionSwitchCount,
    string SessionClassification,
    PcapSourceGameplayPhaseSegment[] InferredWindows,
    PcapSourceGameplayPhaseSegment[] GapSegments);

public sealed record PcapSourceGameplayPhaseSegment(
    int SegmentIndex,
    string Label,
    long FirstPacketIndex,
    long LastPacketIndex,
    long FirstTimestampMicroseconds,
    long LastTimestampMicroseconds,
    double DurationMilliseconds,
    int PacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int DirectionSwitchCount,
    int? FirstClientSequence,
    int? LastClientSequence,
    int? FirstServerSequence,
    int? LastServerSequence,
    int MinPayloadLength,
    int MaxPayloadLength,
    double AveragePayloadLength,
    PcapSourceGameplayPhaseCount[] TopPayloadLengthCounts,
    PcapSourceGameplayPhaseCount[] ShapeCounts,
    PcapSourceGameplayPhaseCount[] NativeFrameKindCounts,
    PcapSourceGameplayPhaseCount[] DirectionCounts,
    PcapSourceGameplayPhaseCount[] PayloadSemanticRoleCounts,
    int FragmentedSendCandidateCount,
    int NativeNearMtuSendCandidateCount,
    int HighEntropyBinaryCount,
    int ShortControlCount,
    int EmbeddedRecordCount,
    PcapSourceGameplayPhaseCount[] EmbeddedRecordRoleCounts,
    PcapSourceGameplayPhaseCount[] EmbeddedRecordRoleSchemaCounts,
    double AverageBodyEntropy);

public sealed record PcapSourceGameplayPhaseCount(
    string Value,
    int Count);
