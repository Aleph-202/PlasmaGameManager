using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceUsercmdQueueDeltaTailAnalyzer
{
    public async Task<PcapSourceUsercmdQueueDeltaTailReport> AnalyzeAsync(string inputPath, string outputPath)
    {
        var report = Analyze(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceUsercmdQueueDeltaTailReport Analyze(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Missing pcap-source-usercmd-record-candidates report.", inputPath);
        }

        var source = JsonSerializer.Deserialize<PcapSourceUsercmdRecordCandidateReport>(
            File.ReadAllText(inputPath),
            JsonOptions) ?? throw new InvalidOperationException($"Could not parse {inputPath}");

        var packets = source.Files
            .SelectMany(static file => file.Candidates)
            .Where(static packet => packet.TfElfQueueDeltaProbeStatus != "decode-failed")
            .ToArray();
        var exactTwoRecord = packets.Where(static packet => packet.RecordCount == 2).ToArray();
        var nonZeroTrailing = packets
            .Where(static packet => packet.TfElfQueueDeltaProbeStatus == "decoded-with-nonzero-trailing-data")
            .ToArray();
        var exactZeroTrailing = packets
            .Where(static packet => packet.TfElfQueueDeltaProbeStatus == "exact-zero-trailing")
            .ToArray();

        var sequenceDeltaCounts = CountByNullableInt(packets.Select(static packet => packet.SequenceDelta));
        var consumedBitCounts = CountByInt(packets.Select(static packet => packet.TfElfQueueDeltaConsumedBits));
        var consumedDeltaCounts = packets
            .GroupBy(static packet => $"bits={packet.TfElfQueueDeltaConsumedBits};delta={packet.SequenceDelta?.ToString() ?? "none"}", StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceUsercmdQueueDeltaTailCount(group.Key, group.Count()))
            .ToArray();

        var mostCommonDelta = sequenceDeltaCounts.FirstOrDefault(static count => count.Value != "none");
        var fixedTrailerRuledOut = packets.Length > 0
            && exactZeroTrailing.Length == 0
            && nonZeroTrailing.Length == packets.Length
            && consumedBitCounts.Length > 1
            && packets.All(static packet => packet.TfElfQueueDeltaTrailingNonZeroBits > 0);
        var nativeReady = packets.Length > 0
            && exactZeroTrailing.Length == packets.Length
            && nonZeroTrailing.Length == 0;

        var summary = new PcapSourceUsercmdQueueDeltaTailSummary(
            source.Status,
            packets.Length,
            exactTwoRecord.Length,
            nonZeroTrailing.Length,
            exactZeroTrailing.Length,
            sequenceDeltaCounts.Length,
            TryParseNullableInt(mostCommonDelta?.Value),
            mostCommonDelta?.Count ?? 0,
            consumedBitCounts.Length,
            packets.Length == 0 ? 0 : packets.Min(static packet => packet.TfElfQueueDeltaConsumedBits),
            packets.Length == 0 ? 0 : packets.Max(static packet => packet.TfElfQueueDeltaConsumedBits),
            packets.Length == 0 ? 0 : packets.Min(static packet => packet.TfElfQueueDeltaTrailingNonZeroBits),
            packets.Length == 0 ? 0 : packets.Max(static packet => packet.TfElfQueueDeltaTrailingNonZeroBits),
            packets.Count(IsHighTrailingDensity),
            fixedTrailerRuledOut,
            nativeReady,
            nativeReady
                ? "all queue-delta candidates have zero trailing data and can be promoted to native input after live verification"
                : fixedTrailerRuledOut
                    ? "exact 0x58-multiple hard markerless bodies are queue-delta prefix collisions only: every decoded prefix leaves variable non-zero trailing data, so recover the upstream TF.elf wrapper/transform before promoting this family"
                    : packets.Length == 0
                        ? "no queue-delta candidates were available; regenerate pcap-source-usercmd-record-candidates first"
                        : "queue-delta candidates remain mixed or inconclusive; inspect per-file samples before promoting this family");

        var fileSummaries = source.Files
            .Where(static file => file.Candidates.Length > 0)
            .Select(BuildFileSummary)
            .ToArray();
        var samples = packets
            .OrderBy(static packet => packet.File, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .Take(256)
            .Select(BuildSample)
            .ToArray();

        return new PcapSourceUsercmdQueueDeltaTailReport(
            "pcap-source-usercmd-queue-delta-tail",
            "Classifies exact 0x58-multiple hard markerless packets that only decode as TF.elf queue-delta prefixes, with emphasis on variable non-zero trailing data.",
            summary,
            sequenceDeltaCounts,
            consumedBitCounts,
            consumedDeltaCounts,
            fileSummaries,
            samples);
    }

    private static PcapSourceUsercmdQueueDeltaTailFileSummary BuildFileSummary(PcapSourceUsercmdRecordCandidateFile file)
    {
        var candidates = file.Candidates
            .Where(static packet => packet.TfElfQueueDeltaProbeStatus != "decode-failed")
            .ToArray();
        var sequenceDeltaCounts = CountByNullableInt(candidates.Select(static packet => packet.SequenceDelta));
        var consumedBitCounts = CountByInt(candidates.Select(static packet => packet.TfElfQueueDeltaConsumedBits));
        var mostCommonDelta = sequenceDeltaCounts.FirstOrDefault(static count => count.Value != "none");

        return new PcapSourceUsercmdQueueDeltaTailFileSummary(
            file.File,
            candidates.Length,
            candidates.Count(static packet => packet.RecordCount == 2),
            candidates.Count(static packet => packet.TfElfQueueDeltaProbeStatus == "decoded-with-nonzero-trailing-data"),
            candidates.Count(static packet => packet.TfElfQueueDeltaProbeStatus == "exact-zero-trailing"),
            TryParseNullableInt(mostCommonDelta?.Value),
            mostCommonDelta?.Count ?? 0,
            consumedBitCounts.Length,
            candidates.Length == 0 ? 0 : candidates.Min(static packet => packet.TfElfQueueDeltaConsumedBits),
            candidates.Length == 0 ? 0 : candidates.Max(static packet => packet.TfElfQueueDeltaConsumedBits),
            candidates.Length == 0 ? 0 : candidates.Min(static packet => packet.TfElfQueueDeltaTrailingNonZeroBits),
            candidates.Length == 0 ? 0 : candidates.Max(static packet => packet.TfElfQueueDeltaTrailingNonZeroBits),
            sequenceDeltaCounts.Take(12).ToArray(),
            consumedBitCounts.Take(12).ToArray());
    }

    private static PcapSourceUsercmdQueueDeltaTailSample BuildSample(PcapSourceUsercmdRecordCandidatePacket packet)
    {
        var trailingBitCount = Math.Max(0, (packet.BodyLength * 8) - packet.TfElfQueueDeltaConsumedBits);
        var density = trailingBitCount == 0
            ? 0
            : Math.Round((double)packet.TfElfQueueDeltaTrailingNonZeroBits / trailingBitCount, 4);

        return new PcapSourceUsercmdQueueDeltaTailSample(
            packet.File,
            packet.SourceStep,
            packet.PacketIndex,
            packet.Sequence,
            packet.SequenceDelta,
            packet.BodyLength,
            packet.RecordCount,
            packet.TfElfQueueDeltaConsumedBits,
            trailingBitCount,
            packet.TfElfQueueDeltaTrailingNonZeroBits,
            density,
            packet.BodyPrefix64Hex,
            packet.TfElfQueueDeltaDetail);
    }

    private static bool IsHighTrailingDensity(PcapSourceUsercmdRecordCandidatePacket packet)
    {
        var trailingBitCount = Math.Max(0, (packet.BodyLength * 8) - packet.TfElfQueueDeltaConsumedBits);
        return trailingBitCount > 0
            && (double)packet.TfElfQueueDeltaTrailingNonZeroBits / trailingBitCount >= 0.40;
    }

    private static PcapSourceUsercmdQueueDeltaTailCount[] CountByNullableInt(IEnumerable<int?> values) =>
        values
            .Select(static value => value?.ToString() ?? "none")
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceUsercmdQueueDeltaTailCount(group.Key, group.Count()))
            .ToArray();

    private static PcapSourceUsercmdQueueDeltaTailCount[] CountByInt(IEnumerable<int> values) =>
        values
            .GroupBy(static value => value)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Select(static group => new PcapSourceUsercmdQueueDeltaTailCount(group.Key.ToString(), group.Count()))
            .ToArray();

    private static int? TryParseNullableInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public sealed record PcapSourceUsercmdQueueDeltaTailReport(
    string Status,
    string Note,
    PcapSourceUsercmdQueueDeltaTailSummary Summary,
    PcapSourceUsercmdQueueDeltaTailCount[] SequenceDeltaCounts,
    PcapSourceUsercmdQueueDeltaTailCount[] ConsumedBitCounts,
    PcapSourceUsercmdQueueDeltaTailCount[] ConsumedBitSequenceDeltaCounts,
    PcapSourceUsercmdQueueDeltaTailFileSummary[] FileSummaries,
    PcapSourceUsercmdQueueDeltaTailSample[] Samples);

public sealed record PcapSourceUsercmdQueueDeltaTailSummary(
    string SourceReportStatus,
    int CandidatePacketCount,
    int ExactTwoRecordPacketCount,
    int DecodedWithNonZeroTrailingPacketCount,
    int ExactZeroTrailingPacketCount,
    int DistinctSequenceDeltaCount,
    int? MostCommonSequenceDelta,
    int MostCommonSequenceDeltaCount,
    int DistinctConsumedBitCount,
    int MinConsumedBits,
    int MaxConsumedBits,
    int MinTrailingNonZeroBits,
    int MaxTrailingNonZeroBits,
    int HighTrailingNonZeroDensityPacketCount,
    bool FixedTrailerHypothesisRuledOut,
    bool NativeBoundaryReady,
    string Conclusion);

public sealed record PcapSourceUsercmdQueueDeltaTailCount(
    string Value,
    int Count);

public sealed record PcapSourceUsercmdQueueDeltaTailFileSummary(
    string File,
    int CandidatePacketCount,
    int ExactTwoRecordPacketCount,
    int DecodedWithNonZeroTrailingPacketCount,
    int ExactZeroTrailingPacketCount,
    int? MostCommonSequenceDelta,
    int MostCommonSequenceDeltaCount,
    int DistinctConsumedBitCount,
    int MinConsumedBits,
    int MaxConsumedBits,
    int MinTrailingNonZeroBits,
    int MaxTrailingNonZeroBits,
    PcapSourceUsercmdQueueDeltaTailCount[] TopSequenceDeltaCounts,
    PcapSourceUsercmdQueueDeltaTailCount[] TopConsumedBitCounts);

public sealed record PcapSourceUsercmdQueueDeltaTailSample(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    int BodyLength,
    int RecordCount,
    int ConsumedBits,
    int TrailingBitCount,
    int TrailingNonZeroBits,
    double TrailingNonZeroDensity,
    string BodyPrefix64Hex,
    string Detail);
