using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceQueuedPeerOpaqueAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceQueuedPeerOpaqueReport> AnalyzePathAsync(string inputPath, string outputPath)
    {
        var report = AnalyzePath(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceQueuedPeerOpaqueReport AnalyzePath(string inputPath)
    {
        var files = EnumerateInputs(inputPath).ToArray();
        var reports = files.Select(file => AnalyzeFile(inputPath, file)).ToArray();
        var samples = reports.SelectMany(static file => file.Samples).ToArray();
        return new PcapSourceQueuedPeerOpaqueReport(
            "pcap-source-queued-peer-opaque-prefixes",
            new PcapSourceQueuedPeerOpaqueSummary(
                files.Length,
                reports.Count(static file => file.HasActiveSourceFlow),
                reports.Sum(static file => file.SourcePacketCount),
                reports.Sum(static file => file.QueuedChunkPacketCount),
                reports.Sum(static file => file.ServerQueuedChunkPacketCount),
                reports.Sum(static file => file.ClientQueuedChunkPacketCount),
                reports.Sum(static file => file.QueuedChunkWithEmbeddedRecordsCount),
                reports.Sum(static file => file.QueuedChunkWithoutEmbeddedRecordsCount),
                reports.Sum(static file => file.OpaquePrefixBytes),
                reports.Sum(static file => file.LzssWrappedPrefixCount),
                reports.Sum(static file => file.StrictSnapshotPrefixCount),
                TopCounts(samples, static sample => sample.Family),
                TopCounts(samples, static sample => sample.OpaquePrefixLength.ToString()),
                TopCounts(samples, static sample => sample.OpaquePrefixFirst4Hex),
                TopCounts(samples, static sample => sample.OpaquePrefixLast4Hex)),
            reports);
    }

    private PcapSourceQueuedPeerOpaqueFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = File.Exists(inputPath)
            ? Path.GetFileName(file)
            : Path.GetRelativePath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceQueuedPeerOpaqueFile(
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
                []);
        }

        var samples = new List<PcapSourceQueuedPeerOpaqueSample>();
        var queuedCount = 0;
        var serverCount = 0;
        var clientCount = 0;
        var withRecords = 0;
        var withoutRecords = 0;
        var opaqueBytes = 0;
        var lzss = 0;
        var snapshots = 0;

        for (var i = 0; i < replay.SourcePackets.Length; i++)
        {
            var packet = replay.SourcePackets[i];
            if (!Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
            {
                continue;
            }

            var frame = transport.ClassifyNativeFrame();
            if (frame.Kind != Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
            {
                continue;
            }

            var records = Ps3SourceEmbeddedObjectRecords.Extract(transport.Body)
                .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
                .ToArray();
            var firstRecordOffset = records.Length == 0
                ? transport.Body.Length
                : records.Min(static record => record.Offset);
            firstRecordOffset = Math.Clamp(firstRecordOffset, 0, transport.Body.Length);
            var prefix = transport.Body.AsSpan(0, firstRecordOffset);
            byte[] decoded = [];
            var isLzss = Ps3SourceLzss.IsWrapped(prefix) && Ps3SourceLzss.TryDecode(prefix, out decoded);
            var hasSnapshot = Ps3SourceSnapshotFrameBuilder.TryDecode(prefix, out var snapshot)
                && IsStrictSnapshot(snapshot);
            if (!hasSnapshot && isLzss)
            {
                hasSnapshot = Ps3SourceSnapshotFrameBuilder.TryDecode(decoded, out snapshot)
                    && IsStrictSnapshot(snapshot);
            }

            queuedCount++;
            if (packet.Direction == PcapActiveFlowDirection.ServerToClient)
            {
                serverCount++;
            }
            else
            {
                clientCount++;
            }

            if (records.Length == 0)
            {
                withoutRecords++;
            }
            else
            {
                withRecords++;
            }

            opaqueBytes += prefix.Length;
            if (isLzss)
            {
                lzss++;
            }

            if (hasSnapshot)
            {
                snapshots++;
            }

            if (samples.Count < 128)
            {
                samples.Add(BuildSample(
                    relativePath,
                    packet,
                    i,
                    transport,
                    prefix,
                    records,
                    isLzss,
                    isLzss ? decoded.Length : null,
                    hasSnapshot,
                    hasSnapshot ? snapshot.EntityDeltaSection?.Records.Length : null));
            }
        }

        return new PcapSourceQueuedPeerOpaqueFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.SourcePackets.Length,
            queuedCount,
            serverCount,
            clientCount,
            withRecords,
            withoutRecords,
            opaqueBytes,
            lzss,
            snapshots,
            samples.Count,
            samples.ToArray());
    }

    private static PcapSourceQueuedPeerOpaqueSample BuildSample(
        string file,
        PcapActiveFlowDatagram packet,
        int sourceStep,
        Ps3SourceTransportPacket transport,
        ReadOnlySpan<byte> prefix,
        Ps3SourceEmbeddedObjectRecord[] records,
        bool lzssWrapped,
        int? lzssDecodedLength,
        bool strictSnapshotPrefix,
        int? strictSnapshotRecordCount)
    {
        var family = FamilyFor(packet.Direction, records.Length, prefix.Length, lzssWrapped, strictSnapshotPrefix);
        return new PcapSourceQueuedPeerOpaqueSample(
            file,
            packet.PacketIndex,
            sourceStep,
            packet.Direction.ToString(),
            transport.CandidateSequence,
            transport.PayloadLength,
            transport.Body.Length,
            prefix.Length,
            records.Length,
            family,
            Math.Round(Entropy(prefix), 4),
            Math.Round(PrintableRatio(prefix), 4),
            FirstHex(prefix, 4),
            LastHex(prefix, 4),
            FirstHex(prefix, 16),
            LastHex(prefix, 16),
            lzssWrapped,
            lzssDecodedLength,
            strictSnapshotPrefix,
            strictSnapshotRecordCount,
            records.Select(RecordSummary).ToArray());
    }

    private static string FamilyFor(
        PcapActiveFlowDirection direction,
        int recordCount,
        int prefixLength,
        bool lzssWrapped,
        bool strictSnapshotPrefix)
    {
        var side = direction == PcapActiveFlowDirection.ServerToClient ? "server" : "client";
        if (strictSnapshotPrefix)
        {
            return $"{side}-queued-strict-snapshot-prefix";
        }

        if (lzssWrapped)
        {
            return $"{side}-queued-lzss-prefix";
        }

        return recordCount == 0
            ? $"{side}-queued-opaque-only-{prefixLength}"
            : $"{side}-queued-opaque-prefix-{prefixLength}-records-{recordCount}";
    }

    private static bool IsStrictSnapshot(Ps3SourceDecodedSnapshotFrame snapshot)
    {
        return snapshot.Header.HasEntityDelta
            && snapshot.EntityDeltaSection is { Records.Length: > 0 } section
            && section.Records.Any(IsPlausibleSourceEntityRecord);
    }

    private static bool IsPlausibleSourceEntityRecord(Ps3SourceEntityDeltaNativeRecord record)
    {
        return record.IsPartialRun
            && record.ObjectId.HasValue
            && record.ObjectName is { Length: > 1 }
            && record.ObjectName[0] == 'C'
            && record.EntityCount is > 0;
    }

    private static string RecordSummary(Ps3SourceEmbeddedObjectRecord record)
    {
        return record.Role switch
        {
            Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink => $"PNG {Hex(record.ObjectId)}->{Hex(record.LinkedObjectId)}",
            Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject => $"COc {Hex(record.ObjectId)} owner={Hex(record.FieldB)} class={Hex(record.ClassId)} name={record.DisplayName ?? ""}",
            Ps3SourceEmbeddedObjectRecordRole.PlayerObject => $"COc {Hex(record.ObjectId)} owner={Hex(record.FieldB)} class={Hex(record.ClassId)} name={record.DisplayName ?? ""}",
            Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor => $"DSC {Hex(record.ObjectId)} owner={Hex(record.FieldB)} class={Hex(record.ClassId)} name={record.DisplayName ?? ""}",
            _ => $"{record.Marker} {Hex(record.ObjectId)}"
        };
    }

    private static IEnumerable<string> EnumerateInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            yield return inputPath;
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal))
        {
            yield return path;
        }
    }

    private static PcapSourceQueuedPeerOpaqueCount[] TopCounts<T>(
        IEnumerable<T> values,
        Func<T, string> selector)
    {
        return values
            .Select(selector)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(32)
            .Select(static group => new PcapSourceQueuedPeerOpaqueCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string FirstHex(ReadOnlySpan<byte> value, int byteCount)
    {
        if (value.IsEmpty)
        {
            return "";
        }

        var count = Math.Min(byteCount, value.Length);
        return Convert.ToHexString(value[..count]).ToLowerInvariant();
    }

    private static string LastHex(ReadOnlySpan<byte> value, int byteCount)
    {
        if (value.IsEmpty)
        {
            return "";
        }

        var count = Math.Min(byteCount, value.Length);
        return Convert.ToHexString(value[^count..]).ToLowerInvariant();
    }

    private static string Hex(uint? value)
    {
        return value is null ? "????????" : value.Value.ToString("x8");
    }

    private static double PrintableRatio(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        var printable = 0;
        foreach (var b in data)
        {
            if (b is >= 0x20 and <= 0x7e or 0x09 or 0x0a or 0x0d)
            {
                printable++;
            }
        }

        return printable / (double)data.Length;
    }

    private static double Entropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
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

            var p = count / (double)data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}

public sealed record PcapSourceQueuedPeerOpaqueReport(
    string Status,
    PcapSourceQueuedPeerOpaqueSummary Summary,
    PcapSourceQueuedPeerOpaqueFile[] Files);

public sealed record PcapSourceQueuedPeerOpaqueSummary(
    int FileCount,
    int ActiveSourceFlowFileCount,
    int SourcePacketCount,
    int QueuedChunkPacketCount,
    int ServerQueuedChunkPacketCount,
    int ClientQueuedChunkPacketCount,
    int QueuedChunkWithEmbeddedRecordsCount,
    int QueuedChunkWithoutEmbeddedRecordsCount,
    int OpaquePrefixBytes,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount,
    PcapSourceQueuedPeerOpaqueCount[] TopFamilies,
    PcapSourceQueuedPeerOpaqueCount[] TopOpaquePrefixLengths,
    PcapSourceQueuedPeerOpaqueCount[] TopOpaquePrefixFirst4Hex,
    PcapSourceQueuedPeerOpaqueCount[] TopOpaquePrefixLast4Hex);

public sealed record PcapSourceQueuedPeerOpaqueFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int QueuedChunkPacketCount,
    int ServerQueuedChunkPacketCount,
    int ClientQueuedChunkPacketCount,
    int QueuedChunkWithEmbeddedRecordsCount,
    int QueuedChunkWithoutEmbeddedRecordsCount,
    int OpaquePrefixBytes,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount,
    int SampleCount,
    PcapSourceQueuedPeerOpaqueSample[] Samples);

public sealed record PcapSourceQueuedPeerOpaqueSample(
    string File,
    long PacketIndex,
    int SourceStep,
    string Direction,
    ushort Sequence,
    int PayloadLength,
    int BodyLength,
    int OpaquePrefixLength,
    int EmbeddedRecordCount,
    string Family,
    double OpaquePrefixEntropy,
    double OpaquePrefixPrintableRatio,
    string OpaquePrefixFirst4Hex,
    string OpaquePrefixLast4Hex,
    string OpaquePrefixFirst16Hex,
    string OpaquePrefixLast16Hex,
    bool LzssWrappedPrefix,
    int? LzssDecodedLength,
    bool StrictSnapshotPrefix,
    int? StrictSnapshotRecordCount,
    string[] EmbeddedRecordSummaries);

public sealed record PcapSourceQueuedPeerOpaqueCount(string Value, int Count);
