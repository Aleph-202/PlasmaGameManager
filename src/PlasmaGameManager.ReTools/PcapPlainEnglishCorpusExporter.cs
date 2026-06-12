using System.Text;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public sealed class PcapPlainEnglishCorpusExporter
{
    private readonly PcapPlainEnglishTraceExporter _traceExporter = new();

    public async Task<PcapPlainEnglishCorpusReport> ExportAsync(string inputPath, string outputPath)
    {
        var report = Export(inputPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        await File.WriteAllTextAsync(CompanionMarkdownPath(outputPath), BuildMarkdown(report));
        return report;
    }

    public PcapPlainEnglishCorpusReport Export(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath);
        var captures = new List<PcapPlainEnglishCorpusCapture>();
        var skipped = new List<PcapPlainEnglishCorpusSkippedInput>();
        foreach (var file in files)
        {
            try
            {
                var trace = _traceExporter.Export(file);
                captures.Add(BuildCapture(inputPath, file, trace));
            }
            catch (InvalidOperationException ex)
            {
                skipped.Add(new PcapPlainEnglishCorpusSkippedInput(RelativeTo(inputPath, file), ex.Message));
            }
        }

        captures.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        skipped.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));

        var summary = new PcapPlainEnglishCorpusSummary(
            inputPath,
            captures.Count,
            skipped.Count,
            captures.Sum(static capture => capture.NativePacketCount),
            captures.Sum(static capture => capture.ClientToServerPacketCount),
            captures.Sum(static capture => capture.ServerToClientPacketCount),
            captures.Sum(static capture => capture.TurnCount),
            captures.Sum(static capture => capture.HighConfidencePacketCount),
            captures.Sum(static capture => capture.MediumConfidencePacketCount),
            captures.Sum(static capture => capture.LowConfidencePacketCount),
            captures.Sum(static capture => capture.FullyFieldNamedPacketCount),
            captures.Sum(static capture => capture.PacketCountWithUnknownFields),
            captures.Sum(static capture => capture.QueuedPeerChunkPacketCount),
            captures.Sum(static capture => capture.QueuedPeerChunkOpaquePrefixBytes),
            captures.Sum(static capture => capture.ClientAttachedFramePacketCount),
            captures.Sum(static capture => capture.ClientAttachedPayloadFramePacketCount),
            captures.Sum(static capture => capture.ClientBitSidecarPacketCount),
            captures.Sum(static capture => capture.ClientDecodedNetMessagePacketCount),
            captures.Sum(static capture => capture.ClientUserCommandCandidatePacketCount),
            CountBy(captures.SelectMany(static capture => capture.PhaseCounts.SelectMany(static count => Repeat(count.Value, count.Count)))),
            CountBy(captures.SelectMany(static capture => capture.SemanticRoleCounts.SelectMany(static count => Repeat(count.Value, count.Count)))),
            CountBy(captures.SelectMany(static capture => capture.ClientPayloadRoleCounts.SelectMany(static count => Repeat(count.Value, count.Count)))),
            CountBy(captures.SelectMany(static capture => capture.ClientDecodedNetMessageNameCounts.SelectMany(static count => Repeat(count.Value, count.Count)))),
            CountBy(captures.SelectMany(static capture => capture.TopUnknownFields.SelectMany(static count => Repeat(count.Value, count.Count)))));

        return new PcapPlainEnglishCorpusReport(
            "pcap-plain-english-corpus",
            summary,
            captures.ToArray(),
            skipped.ToArray(),
            [
                "High-confidence packet roles are decoded structurally; remaining unknown fields still require TF.elf/server.dll writer or reader recovery.",
                "Queued peer-channel opaque prefix bytes are the clearest native Source blocker; these must become named fields, not replay bytes.",
                "Use the per-capture semantic trace for packet-level evidence, and this corpus index to compare scenarios."
            ]);
    }

    private static PcapPlainEnglishCorpusCapture BuildCapture(string inputRoot, string file, PcapPlainEnglishTraceReport trace)
    {
        var unknowns = CountBy(trace.Packets.SelectMany(static packet => packet.UnknownFields));
        var examples = trace.Turns
            .Take(8)
            .Select(static turn => new PcapPlainEnglishCorpusTurnExample(
                turn.TurnIndex,
                turn.ClientSourceSteps,
                turn.ServerSourceSteps,
                turn.PlainEnglish,
                turn.HasUnknownFields))
            .ToArray();

        return new PcapPlainEnglishCorpusCapture(
            RelativeTo(inputRoot, file),
            trace.Summary.ClientEndpoint,
            trace.Summary.ServerEndpoint,
            trace.Summary.ClientHelloPacketIndex,
            trace.Summary.ServerHelloPacketIndex,
            trace.Summary.FirstSourcePacketIndex,
            trace.Summary.NativePacketCount,
            trace.Summary.ClientToServerPacketCount,
            trace.Summary.ServerToClientPacketCount,
            trace.Summary.TurnCount,
            trace.Summary.HighConfidencePacketCount,
            trace.Summary.MediumConfidencePacketCount,
            trace.Summary.LowConfidencePacketCount,
            trace.Summary.FullyFieldNamedPacketCount,
            trace.Summary.PacketCountWithUnknownFields,
            trace.Summary.NativeSnapshotFrameCount,
            trace.Summary.NativeSnapshotEntityDeltaRecordCount,
            trace.Summary.QueuedPeerChunkPacketCount,
            trace.Summary.QueuedPeerChunkWithEmbeddedRecordsCount,
            trace.Summary.QueuedPeerChunkWithoutEmbeddedRecordsCount,
            trace.Summary.QueuedPeerChunkOpaquePrefixBytes,
            trace.Summary.ClientAttachedFramePacketCount,
            trace.Summary.ClientAttachedPayloadFramePacketCount,
            trace.Summary.ClientBitSidecarPacketCount,
            trace.Summary.ClientDecodedNetMessagePacketCount,
            trace.Summary.ClientUserCommandCandidatePacketCount,
            trace.Summary.PhaseCounts,
            trace.Summary.SemanticRoleCounts,
            trace.Summary.ClientPayloadRoleCounts.Take(12).ToArray(),
            trace.Summary.ClientDecodedNetMessageNameCounts.Take(12).ToArray(),
            trace.Summary.CompactControlFamilyCounts.Take(12).ToArray(),
            trace.Summary.MarkerlessPayloadFamilyCounts.Take(12).ToArray(),
            trace.Summary.EmbeddedPrefixFamilyCounts.Take(12).ToArray(),
            unknowns.Take(12).ToArray(),
            examples);
    }

    private static string BuildMarkdown(PcapPlainEnglishCorpusReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# PCAP Packet Language Corpus");
        builder.AppendLine();
        builder.AppendLine("This index summarizes the active GameManager/native Source flow in every PCAP that currently has one. It is not replay data; it is a field-understanding ledger for reverse engineering.");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- Active captures: `{report.Summary.ActiveCaptureCount}`");
        builder.AppendLine($"- Skipped inputs: `{report.Summary.SkippedInputCount}`");
        builder.AppendLine($"- Native packets: `{report.Summary.NativePacketCount}`");
        builder.AppendLine($"- Client -> server packets: `{report.Summary.ClientToServerPacketCount}`");
        builder.AppendLine($"- Server -> client packets: `{report.Summary.ServerToClientPacketCount}`");
        builder.AppendLine($"- Request/response turns: `{report.Summary.TurnCount}`");
        builder.AppendLine($"- High-confidence packets: `{report.Summary.HighConfidencePacketCount}`");
        builder.AppendLine($"- Medium-confidence packets: `{report.Summary.MediumConfidencePacketCount}`");
        builder.AppendLine($"- Low-confidence packets: `{report.Summary.LowConfidencePacketCount}`");
        builder.AppendLine($"- Fully field-named packets: `{report.Summary.FullyFieldNamedPacketCount}`");
        builder.AppendLine($"- Packets with unknown fields: `{report.Summary.PacketCountWithUnknownFields}`");
        builder.AppendLine($"- Queued peer-channel chunks: `{report.Summary.QueuedPeerChunkPacketCount}`");
        builder.AppendLine($"- Queued peer-channel opaque prefix bytes: `{report.Summary.QueuedPeerChunkOpaquePrefixBytes}`");
        builder.AppendLine($"- Client attached-frame packets: `{report.Summary.ClientAttachedFramePacketCount}`");
        builder.AppendLine($"- Client attached payload frames: `{report.Summary.ClientAttachedPayloadFramePacketCount}`");
        builder.AppendLine($"- Client bit-sidecar packets: `{report.Summary.ClientBitSidecarPacketCount}`");
        builder.AppendLine($"- Client decoded native net message candidates: `{report.Summary.ClientDecodedNetMessagePacketCount}`");
        builder.AppendLine($"- Client user-command/loading-ack candidates: `{report.Summary.ClientUserCommandCandidatePacketCount}`");
        builder.AppendLine();

        AppendCounts(builder, "Top Unknown Fields", report.Summary.TopUnknownFields, limit: 12);
        AppendCounts(builder, "Semantic Roles", report.Summary.SemanticRoleCounts, limit: 16);
        AppendCounts(builder, "Client Payload Roles", report.Summary.ClientPayloadRoleCounts, limit: 16);
        AppendCounts(builder, "Decoded Client Native Message Candidates", report.Summary.ClientDecodedNetMessageNameCounts, limit: 16);
        AppendCounts(builder, "Phases", report.Summary.PhaseCounts, limit: 8);

        builder.AppendLine("## Captures");
        foreach (var capture in report.Captures)
        {
            builder.AppendLine();
            builder.AppendLine($"### {capture.RelativePath}");
            builder.AppendLine();
            builder.AppendLine($"- Endpoint: `{capture.ClientEndpoint}` -> `{capture.ServerEndpoint}`");
            builder.AppendLine($"- Native packets: `{capture.NativePacketCount}` (`{capture.ClientToServerPacketCount}` client, `{capture.ServerToClientPacketCount}` server)");
            builder.AppendLine($"- Turns: `{capture.TurnCount}`");
            builder.AppendLine($"- Confidence: `{capture.HighConfidencePacketCount}` high, `{capture.MediumConfidencePacketCount}` medium, `{capture.LowConfidencePacketCount}` low");
            builder.AppendLine($"- Fully field-named packets: `{capture.FullyFieldNamedPacketCount}`");
            builder.AppendLine($"- Packets with unknown fields: `{capture.PacketCountWithUnknownFields}`");
            builder.AppendLine($"- Queued peer chunks: `{capture.QueuedPeerChunkPacketCount}`, opaque prefix bytes `{capture.QueuedPeerChunkOpaquePrefixBytes}`");
            builder.AppendLine($"- Client native frames: attached `{capture.ClientAttachedFramePacketCount}`, bit-sidecar `{capture.ClientBitSidecarPacketCount}`, decoded message candidates `{capture.ClientDecodedNetMessagePacketCount}`, command candidates `{capture.ClientUserCommandCandidatePacketCount}`");
            AppendInlineCounts(builder, "Roles", capture.SemanticRoleCounts, 8);
            AppendInlineCounts(builder, "Client payloads", capture.ClientPayloadRoleCounts, 6);
            AppendInlineCounts(builder, "Unknowns", capture.TopUnknownFields, 6);
            if (capture.TurnExamples.Length > 0)
            {
                builder.AppendLine("- Early turns:");
                foreach (var turn in capture.TurnExamples.Take(4))
                {
                    builder.AppendLine($"  - `{turn.TurnIndex}` client `{string.Join(",", turn.ClientSourceSteps)}` -> server `{string.Join(",", turn.ServerSourceSteps)}`: {turn.PlainEnglish} unknown=`{turn.HasUnknownFields.ToString().ToLowerInvariant()}`");
                }
            }
        }

        if (report.SkippedInputs.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Skipped Inputs");
            foreach (var skipped in report.SkippedInputs.Take(32))
            {
                builder.AppendLine($"- `{skipped.RelativePath}`: {skipped.Reason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Native Completion Rule");
        builder.AppendLine();
        builder.AppendLine("A packet is not considered native-complete until each unknown field here is replaced by a named TF.elf/server.dll reader or writer path. Matching retail length, entropy, or record count is evidence, not a replay license.");
        return builder.ToString();
    }

    private static void AppendCounts(StringBuilder builder, string title, IReadOnlyList<PcapPlainEnglishCount> counts, int limit)
    {
        if (counts.Count == 0)
        {
            return;
        }

        builder.AppendLine($"## {title}");
        builder.AppendLine();
        foreach (var count in counts.Take(limit))
        {
            builder.AppendLine($"- `{count.Value}`: `{count.Count}`");
        }

        builder.AppendLine();
    }

    private static void AppendInlineCounts(StringBuilder builder, string label, IReadOnlyList<PcapPlainEnglishCount> counts, int limit)
    {
        if (counts.Count == 0)
        {
            return;
        }

        builder.Append("- ");
        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(string.Join(", ", counts.Take(limit).Select(static count => $"`{count.Value}` `{count.Count}`")));
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

    private static string RelativeTo(string root, string file)
    {
        return Directory.Exists(root)
            ? Path.GetRelativePath(root, file)
            : Path.GetFileName(file);
    }

    private static IEnumerable<string> Repeat(string value, int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return value;
        }
    }

    private static PcapPlainEnglishCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapPlainEnglishCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string CompanionMarkdownPath(string outputPath)
    {
        return outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? outputPath[..^".json".Length] + ".md"
            : outputPath + ".md";
    }
}

public sealed record PcapPlainEnglishCorpusReport(
    string Status,
    PcapPlainEnglishCorpusSummary Summary,
    PcapPlainEnglishCorpusCapture[] Captures,
    PcapPlainEnglishCorpusSkippedInput[] SkippedInputs,
    string[] Conclusions);

public sealed record PcapPlainEnglishCorpusSummary(
    string InputPath,
    int ActiveCaptureCount,
    int SkippedInputCount,
    int NativePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int TurnCount,
    int HighConfidencePacketCount,
    int MediumConfidencePacketCount,
    int LowConfidencePacketCount,
    int FullyFieldNamedPacketCount,
    int PacketCountWithUnknownFields,
    int QueuedPeerChunkPacketCount,
    int QueuedPeerChunkOpaquePrefixBytes,
    int ClientAttachedFramePacketCount,
    int ClientAttachedPayloadFramePacketCount,
    int ClientBitSidecarPacketCount,
    int ClientDecodedNetMessagePacketCount,
    int ClientUserCommandCandidatePacketCount,
    PcapPlainEnglishCount[] PhaseCounts,
    PcapPlainEnglishCount[] SemanticRoleCounts,
    PcapPlainEnglishCount[] ClientPayloadRoleCounts,
    PcapPlainEnglishCount[] ClientDecodedNetMessageNameCounts,
    PcapPlainEnglishCount[] TopUnknownFields);

public sealed record PcapPlainEnglishCorpusCapture(
    string RelativePath,
    string ClientEndpoint,
    string ServerEndpoint,
    long ClientHelloPacketIndex,
    long ServerHelloPacketIndex,
    long FirstSourcePacketIndex,
    int NativePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int TurnCount,
    int HighConfidencePacketCount,
    int MediumConfidencePacketCount,
    int LowConfidencePacketCount,
    int FullyFieldNamedPacketCount,
    int PacketCountWithUnknownFields,
    int NativeSnapshotFrameCount,
    int NativeSnapshotEntityDeltaRecordCount,
    int QueuedPeerChunkPacketCount,
    int QueuedPeerChunkWithEmbeddedRecordsCount,
    int QueuedPeerChunkWithoutEmbeddedRecordsCount,
    int QueuedPeerChunkOpaquePrefixBytes,
    int ClientAttachedFramePacketCount,
    int ClientAttachedPayloadFramePacketCount,
    int ClientBitSidecarPacketCount,
    int ClientDecodedNetMessagePacketCount,
    int ClientUserCommandCandidatePacketCount,
    PcapPlainEnglishCount[] PhaseCounts,
    PcapPlainEnglishCount[] SemanticRoleCounts,
    PcapPlainEnglishCount[] ClientPayloadRoleCounts,
    PcapPlainEnglishCount[] ClientDecodedNetMessageNameCounts,
    PcapPlainEnglishCount[] CompactControlFamilyCounts,
    PcapPlainEnglishCount[] MarkerlessPayloadFamilyCounts,
    PcapPlainEnglishCount[] EmbeddedPrefixFamilyCounts,
    PcapPlainEnglishCount[] TopUnknownFields,
    PcapPlainEnglishCorpusTurnExample[] TurnExamples);

public sealed record PcapPlainEnglishCorpusTurnExample(
    int TurnIndex,
    int[] ClientSourceSteps,
    int[] ServerSourceSteps,
    string PlainEnglish,
    bool HasUnknownFields);

public sealed record PcapPlainEnglishCorpusSkippedInput(
    string RelativePath,
    string Reason);
