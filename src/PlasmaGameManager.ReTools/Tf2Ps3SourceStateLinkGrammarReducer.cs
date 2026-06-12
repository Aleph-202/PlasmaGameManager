using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceStateLinkGrammarReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceStateLinkGrammarReport> ReduceAsync(
        string templatePatchLayoutPath,
        string embeddedObjectsReportPath,
        string queuedPeerOpaqueReportPath,
        string outputPath)
    {
        using var templatePatchLayout = JsonDocument.Parse(await File.ReadAllTextAsync(templatePatchLayoutPath));
        using var embeddedObjects = JsonDocument.Parse(await File.ReadAllTextAsync(embeddedObjectsReportPath));
        using var queuedPeerOpaque = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPeerOpaqueReportPath));

        var templateLayouts = templatePatchLayout.RootElement.GetProperty("TemplateLayouts").EnumerateArray().ToArray();
        var staticRecords = ExtractStaticTemplateRecords(templateLayouts).ToArray();
        var dynamicTails = ExtractDynamicTailFamilies(templateLayouts).ToArray();
        var prefixDebt = ExtractPrefixDebt(templateLayouts).ToArray();
        var generatedPrefixDebt = ExtractGeneratedPrefixDebt(templatePatchLayout.RootElement).ToArray();
        var embeddedSummary = embeddedObjects.RootElement.GetProperty("Summary");
        var queuedSummary = queuedPeerOpaque.RootElement.GetProperty("Summary");

        var report = new Tf2Ps3SourceStateLinkGrammarReport(
            "tf2ps3-source-state-link-grammar",
            "Field grammar for the PS3 Source PNG player/object state-link records, separated from the still-unresolved queued-prefix bytes around them.",
            new Tf2Ps3SourceStateLinkGrammarInputs(
                templatePatchLayoutPath,
                embeddedObjectsReportPath,
                queuedPeerOpaqueReportPath),
            new Tf2Ps3SourceStateLinkGrammarSummary(
                Ps3SourcePlayerStateLinkRecord.Length,
                Ps3SourcePlayerStateLinkRecord.Version,
                staticRecords.Length,
                dynamicTails.Sum(static tail => tail.RecordCount),
                dynamicTails.Length,
                ReadCount(embeddedSummary, "RecordRoleCounts", "PlayerStateLink"),
                ReadCount(embeddedSummary, "MarkerCounts", "PNG"),
                embeddedSummary.GetProperty("FilesWithEmbeddedRecords").GetInt32(),
                embeddedSummary.GetProperty("EmbeddedRecordCount").GetInt32(),
                queuedSummary.GetProperty("QueuedChunkWithEmbeddedRecordsCount").GetInt32(),
                queuedSummary.GetProperty("OpaquePrefixBytes").GetInt32(),
                queuedSummary.GetProperty("LzssWrappedPrefixCount").GetInt32(),
                queuedSummary.GetProperty("StrictSnapshotPrefixCount").GetInt32(),
                prefixDebt.Sum(static debt => debt.PrefixByteLength),
                generatedPrefixDebt.Length,
                generatedPrefixDebt.Sum(static debt => debt.PrefixByteLength),
                generatedPrefixDebt.Sum(static debt => debt.RecordCount)),
            new Tf2Ps3SourceStateLinkWireFormat(
                Ps3SourcePlayerStateLinkRecord.Marker,
                Ps3SourcePlayerStateLinkRecord.Version,
                Ps3SourcePlayerStateLinkRecord.Length,
                [
                    new("0..2", "ascii-marker", "PNG"),
                    new("3", "version", "0x01"),
                    new("4..7", "object-id", "big-endian uint32 source object id"),
                    new("8..11", "linked-object-id", "big-endian uint32 associated/root/player object id")
                ]),
            staticRecords,
            dynamicTails,
            prefixDebt,
            generatedPrefixDebt,
            new Tf2Ps3SourceStateLinkCorpusEvidence(
                embeddedSummary.GetProperty("FileCount").GetInt32(),
                embeddedSummary.GetProperty("ActiveSourceFlowCount").GetInt32(),
                embeddedSummary.GetProperty("FilesWithEmbeddedRecords").GetInt32(),
                TopCountsAcrossFiles(embeddedObjects.RootElement, "ObjectLinkCounts"),
                TopCountsAcrossFiles(embeddedObjects.RootElement, "FieldACounts"),
                TopCountsAcrossFiles(embeddedObjects.RootElement, "FieldBCounts"),
                ReadTopCounts(queuedSummary, "TopFamilies"),
                ReadTopCounts(queuedSummary, "TopOpaquePrefixLengths")),
            [
                "Generate every PNG record through Ps3SourcePlayerStateLinkRecord; do not hide state-link records inside Convert.FromHexString bodies.",
                "Build state-link batches from the Source object association graph: host/root object, player objects, FrozenState peers, descriptor objects, and map-load object links.",
                "Replace queued-prefix/control bytes before PNG records with the native queued peer-channel submessage builder recovered from TF.elf, not copied prefixes.",
                "Replace generated queued-prefix bytes from BuildQueuedPlayerStateLinkBody with the same native queued peer-channel writer before declaring the state-link layer complete.",
                "Keep object-stream bootstrap and snapshot/entity-delta generation separate from the 12-byte PNG record grammar; PNG is only the linked-object record layer."
            ],
            [
                "The 12-byte PNG record grammar is now named and shared by protocol/server code.",
                "Queued-prefix/control bytes before many PNG batches remain unresolved native-debt and are tracked by PrefixDebt, GeneratedPrefixDebt, plus PCAP opaque-prefix counts.",
                "EA Tunnel remains outside this record grammar; current evidence points at TF.elf queued peer-channel and object-stream paths for client-visible traffic."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static IEnumerable<Tf2Ps3SourceStateLinkStaticRecord> ExtractStaticTemplateRecords(JsonElement[] layouts)
    {
        foreach (var layout in layouts)
        {
            var name = ReadString(layout, "Name");
            foreach (var record in layout.GetProperty("EmbeddedStateLinkRecords").EnumerateArray())
            {
                yield return new Tf2Ps3SourceStateLinkStaticRecord(
                    name,
                    record.GetProperty("Offset").GetInt32(),
                    Hex(record.GetProperty("ObjectId").GetUInt32()),
                    Hex(record.GetProperty("LinkedObjectId").GetUInt32()));
            }
        }
    }

    private static IEnumerable<Tf2Ps3SourceStateLinkDynamicTailFamily> ExtractDynamicTailFamilies(JsonElement[] layouts)
    {
        foreach (var layout in layouts)
        {
            var name = ReadString(layout, "Name");
            var target = ReadString(layout, "ReplacementTarget");
            foreach (var tail in layout.GetProperty("TailPatches").EnumerateArray())
            {
                yield return new Tf2Ps3SourceStateLinkDynamicTailFamily(
                    name,
                    ReadString(tail, "Method"),
                    target,
                    tail.GetProperty("TailRecordCount").GetInt32(),
                    tail.GetProperty("TailByteLength").GetInt32(),
                    tail.GetProperty("ObjectIdExpressions").EnumerateArray()
                        .Select(static item => item.GetString() ?? "")
                        .Where(static item => item.Length > 0)
                        .ToArray(),
                    ReadString(tail, "LinkedObjectIdExpression"));
            }
        }
    }

    private static IEnumerable<Tf2Ps3SourceStateLinkPrefixDebt> ExtractPrefixDebt(JsonElement[] layouts)
    {
        foreach (var layout in layouts)
        {
            var records = layout.GetProperty("EmbeddedStateLinkRecords").EnumerateArray().ToArray();
            if (records.Length == 0)
            {
                continue;
            }

            var firstOffset = records.Min(static record => record.GetProperty("Offset").GetInt32());
            yield return new Tf2Ps3SourceStateLinkPrefixDebt(
                ReadString(layout, "Name"),
                ReadString(layout, "ReplacementTarget"),
                firstOffset,
                records.Length,
                firstOffset == 0
                    ? "record-only"
                    : firstOffset <= 8
                        ? "compact-control-prefix"
                        : "queued-or-object-stream-prefix");
        }
    }

    private static IEnumerable<Tf2Ps3SourceStateLinkGeneratedPrefixDebt> ExtractGeneratedPrefixDebt(JsonElement root)
    {
        if (!root.TryGetProperty("GeneratedQueuedPrefixBodies", out var bodies) || bodies.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var body in bodies.EnumerateArray())
        {
            yield return new Tf2Ps3SourceStateLinkGeneratedPrefixDebt(
                ReadString(body, "Family"),
                ReadString(body, "Method"),
                ReadString(body, "ReplacementTarget") is { Length: > 0 } target ? target : "queued-peer-submessage-boundaries",
                body.GetProperty("BodyLength").GetInt32(),
                body.GetProperty("PrefixByteLength").GetInt32(),
                body.GetProperty("RecordCount").GetInt32(),
                body.GetProperty("TailByteLength").GetInt32(),
                ReadString(body, "PrefixKind"),
                body.GetProperty("ObjectIdExpressions").EnumerateArray()
                    .Select(static item => item.GetString() ?? "")
                    .Where(static item => item.Length > 0)
                    .ToArray(),
                ReadString(body, "LinkedObjectIdExpression"));
        }
    }

    private static int ReadCount(JsonElement summary, string property, string value)
    {
        if (!summary.TryGetProperty(property, out var counts))
        {
            return 0;
        }

        if (counts.ValueKind == JsonValueKind.Object
            && counts.TryGetProperty(value, out var objectCount))
        {
            return objectCount.GetInt32();
        }

        if (counts.ValueKind == JsonValueKind.Array)
        {
            foreach (var count in counts.EnumerateArray())
            {
                if (ReadString(count, "Value") == value)
                {
                    return count.GetProperty("Count").GetInt32();
                }
            }
        }

        return 0;
    }

    private static Tf2Ps3SourceStateLinkCount[] TopCountsAcrossFiles(JsonElement root, string property)
    {
        return root.GetProperty("Files").EnumerateArray()
            .SelectMany(file => file.TryGetProperty(property, out var counts)
                ? counts.EnumerateArray()
                : [])
            .Select(static count => new
            {
                Value = ReadString(count, "Value"),
                Count = count.GetProperty("Count").GetInt32()
            })
            .Where(static count => count.Value.Length > 0)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Sum(static count => count.Count))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => new Tf2Ps3SourceStateLinkCount(group.Key, group.Sum(static count => count.Count)))
            .ToArray();
    }

    private static Tf2Ps3SourceStateLinkCount[] ReadTopCounts(JsonElement summary, string property)
    {
        if (!summary.TryGetProperty(property, out var counts))
        {
            return [];
        }

        return counts.EnumerateArray()
            .Select(static count => new Tf2Ps3SourceStateLinkCount(
                ReadString(count, "Value"),
                count.GetProperty("Count").GetInt32()))
            .Where(static count => count.Value.Length > 0)
            .Take(16)
            .ToArray();
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            ? value.GetString() ?? ""
            : "";
    }

    private static string Hex(uint value)
    {
        return value.ToString("x8");
    }
}

public sealed record Tf2Ps3SourceStateLinkGrammarReport(
    string Status,
    string Note,
    Tf2Ps3SourceStateLinkGrammarInputs Inputs,
    Tf2Ps3SourceStateLinkGrammarSummary Summary,
    Tf2Ps3SourceStateLinkWireFormat WireFormat,
    Tf2Ps3SourceStateLinkStaticRecord[] StaticTemplateRecords,
    Tf2Ps3SourceStateLinkDynamicTailFamily[] DynamicTailFamilies,
    Tf2Ps3SourceStateLinkPrefixDebt[] PrefixDebt,
    Tf2Ps3SourceStateLinkGeneratedPrefixDebt[] GeneratedPrefixDebt,
    Tf2Ps3SourceStateLinkCorpusEvidence CorpusEvidence,
    string[] NativeBuilderObligations,
    string[] Conclusions);

public sealed record Tf2Ps3SourceStateLinkGrammarInputs(
    string TemplatePatchLayout,
    string EmbeddedObjectsReport,
    string QueuedPeerOpaqueReport);

public sealed record Tf2Ps3SourceStateLinkGrammarSummary(
    int WireRecordLength,
    byte WireVersion,
    int StaticTemplateRecordCount,
    int DynamicTailRecordCount,
    int DynamicTailFamilyCount,
    int PcapPlayerStateLinkRecordCount,
    int PcapPngMarkerCount,
    int PcapFilesWithEmbeddedRecords,
    int PcapEmbeddedRecordCount,
    int QueuedChunkWithEmbeddedRecordsCount,
    int QueuedOpaquePrefixBytes,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount,
    int StaticEmbeddedPrefixDebtBytes,
    int GeneratedQueuedPrefixBodyCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount);

public sealed record Tf2Ps3SourceStateLinkWireFormat(
    string Marker,
    byte Version,
    int Length,
    Tf2Ps3SourceStateLinkWireField[] Fields);

public sealed record Tf2Ps3SourceStateLinkWireField(
    string ByteRange,
    string Name,
    string Meaning);

public sealed record Tf2Ps3SourceStateLinkStaticRecord(
    string TemplateName,
    int Offset,
    string ObjectId,
    string LinkedObjectId);

public sealed record Tf2Ps3SourceStateLinkDynamicTailFamily(
    string TemplateName,
    string Method,
    string ReplacementTarget,
    int RecordCount,
    int TailByteLength,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression);

public sealed record Tf2Ps3SourceStateLinkPrefixDebt(
    string TemplateName,
    string ReplacementTarget,
    int PrefixByteLength,
    int EmbeddedRecordCount,
    string PrefixKind);

public sealed record Tf2Ps3SourceStateLinkGeneratedPrefixDebt(
    string Family,
    string Method,
    string ReplacementTarget,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int TailByteLength,
    string PrefixKind,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression);

public sealed record Tf2Ps3SourceStateLinkCorpusEvidence(
    int PcapFileCount,
    int ActiveSourceFlowCount,
    int FilesWithEmbeddedRecords,
    Tf2Ps3SourceStateLinkCount[] TopObjectLinks,
    Tf2Ps3SourceStateLinkCount[] TopObjectIdCounts,
    Tf2Ps3SourceStateLinkCount[] TopLinkedObjectIdCounts,
    Tf2Ps3SourceStateLinkCount[] TopQueuedOpaqueFamilies,
    Tf2Ps3SourceStateLinkCount[] TopQueuedOpaquePrefixLengths);

public sealed record Tf2Ps3SourceStateLinkCount(
    string Value,
    int Count);
