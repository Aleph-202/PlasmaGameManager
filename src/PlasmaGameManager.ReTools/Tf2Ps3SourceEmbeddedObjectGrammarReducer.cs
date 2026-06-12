using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceEmbeddedObjectGrammarReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceEmbeddedObjectGrammarReport> ReduceAsync(
        string embeddedObjectsReportPath,
        string outputPath)
    {
        using var embeddedObjects = JsonDocument.Parse(await File.ReadAllTextAsync(embeddedObjectsReportPath));
        var root = embeddedObjects.RootElement;
        var summary = root.GetProperty("Summary");
        var roleCounts = ReadObjectCounts(summary, "RecordRoleCounts");
        var markerCounts = ReadObjectCounts(summary, "MarkerCounts");

        var report = new Tf2Ps3SourceEmbeddedObjectGrammarReport(
            "tf2ps3-source-embedded-object-grammar",
            "Native wire grammar for PS3 Source embedded COc/DSC object records, backed by PCAP corpus role/schema evidence.",
            new Tf2Ps3SourceEmbeddedObjectGrammarInputs(embeddedObjectsReportPath),
            new Tf2Ps3SourceEmbeddedObjectGrammarSummary(
                summary.GetProperty("FileCount").GetInt32(),
                summary.GetProperty("ActiveSourceFlowCount").GetInt32(),
                summary.GetProperty("FilesWithEmbeddedRecords").GetInt32(),
                summary.GetProperty("EmbeddedRecordCount").GetInt32(),
                markerCounts.GetValueOrDefault("COc"),
                markerCounts.GetValueOrDefault("DSC"),
                markerCounts.GetValueOrDefault("PNG"),
                roleCounts.GetValueOrDefault("PlayerObject"),
                roleCounts.GetValueOrDefault("FrozenStateObject"),
                roleCounts.GetValueOrDefault("PlayerDescriptor"),
                roleCounts.GetValueOrDefault("PlayerStateLink"),
                roleCounts.GetValueOrDefault("MarkerCollisionNoise")),
            [
                new(
                    "COc",
                    "PlayerObject",
                    Ps3SourceEmbeddedObjectWireRecord.PlayerObjectLength,
                    1,
                    "non-0x4408 class id, most commonly 0x000043fc",
                    [
                        new("0..2", "ascii-marker", "COc"),
                        new("3", "version", "0x01"),
                        new("4..7", "object-id", "big-endian uint32 advertised object id"),
                        new("8..11", "owner-or-root-object-id", "big-endian uint32 owner/root/parent object id"),
                        new("12..15", "class-id", "big-endian uint32 Source object class id"),
                        new("16..36", "display-name", "ASCII display name, zero padded/truncated")
                    ]),
                new(
                    "COc",
                    "FrozenStateObject",
                    Ps3SourceEmbeddedObjectWireRecord.FrozenStateObjectLength,
                    1,
                    "class id 0x00004408",
                    [
                        new("0..2", "ascii-marker", "COc"),
                        new("3", "version", "0x01"),
                        new("4..7", "object-id", "big-endian uint32 advertised FrozenState object id"),
                        new("8..11", "owner-or-root-object-id", "big-endian uint32 root/player object id"),
                        new("12..15", "class-id", "0x00004408"),
                        new("16..36", "display-name", "ASCII FrozenState/player name, zero padded/truncated")
                    ]),
                new(
                    "DSC",
                    "PlayerDescriptor",
                    Ps3SourceEmbeddedObjectWireRecord.PlayerDescriptorLength,
                    0,
                    "descriptor class id, most commonly 0x000043fc with 0x00004408/0x43d3/0x43e0 variants in corpus",
                    [
                        new("0..2", "ascii-marker", "DSC"),
                        new("3", "version", "0x00"),
                        new("4..7", "object-id", "big-endian uint32 descriptor object id"),
                        new("8..11", "owner-or-root-object-id", "big-endian uint32 linked object id"),
                        new("12..15", "class-id", "big-endian uint32 Source object class id")
                    ])
            ],
            new Tf2Ps3SourceEmbeddedObjectCorpusEvidence(
                TopCountsAcrossFiles(root, "RecordRoleSchemaCounts"),
                TopCountsAcrossFiles(root, "RecordSchemaCounts"),
                TopCountsAcrossFiles(root, "FieldCCounts"),
                TopCountsAcrossFiles(root, "PrintableCandidateCounts"),
                TopParticipantEvidence(root)),
            [
                "Generate COc and DSC records through Ps3SourceEmbeddedObjectWireRecord rather than ad hoc byte writes.",
                "Keep class id configurable. TF2 PS3 corpus includes COc/DSC class ids beyond 0x000043fc and 0x00004408.",
                "Treat marker-collision noise as evidence filter failures, not as valid native object records.",
                "COc/DSC grammar is now separate from the unresolved queued-prefix/control bytes that may precede embedded records."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Dictionary<string, int> ReadObjectCounts(JsonElement summary, string property)
    {
        if (!summary.TryGetProperty(property, out var value))
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return value.EnumerateObject()
                .ToDictionary(static item => item.Name, static item => item.Value.GetInt32(), StringComparer.Ordinal);
        }

        return value.EnumerateArray()
            .ToDictionary(static item => ReadString(item, "Value"), static item => item.GetProperty("Count").GetInt32(), StringComparer.Ordinal);
    }

    private static Tf2Ps3SourceEmbeddedObjectCount[] TopCountsAcrossFiles(JsonElement root, string property)
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
            .Take(24)
            .Select(static group => new Tf2Ps3SourceEmbeddedObjectCount(group.Key, group.Sum(static count => count.Count)))
            .ToArray();
    }

    private static Tf2Ps3SourceEmbeddedObjectParticipantEvidence[] TopParticipantEvidence(JsonElement root)
    {
        return root.GetProperty("Files").EnumerateArray()
            .Where(static file => ReadString(file, "File") is "2Fort.PCAPNG" or "dustbowl_final.PCAPNG")
            .SelectMany(static file => file.TryGetProperty("ParticipantSummaries", out var participants)
                ? participants.EnumerateArray().Select(participant => new Tf2Ps3SourceEmbeddedObjectParticipantEvidence(
                    ReadString(file, "File"),
                    ReadString(participant, "ObjectIdHex"),
                    participant.GetProperty("PlayerObjectRecordCount").GetInt32(),
                    participant.GetProperty("FrozenStateObjectRecordCount").GetInt32(),
                    participant.GetProperty("PlayerDescriptorRecordCount").GetInt32(),
                    TopNestedCounts(participant, "ClassIdCounts"),
                    TopNestedCounts(participant, "NameCounts")))
                : [])
            .Where(static participant =>
                participant.PlayerObjectRecordCount > 0
                || participant.FrozenStateObjectRecordCount > 0
                || participant.PlayerDescriptorRecordCount > 0)
            .OrderByDescending(static participant => participant.PlayerObjectRecordCount + participant.FrozenStateObjectRecordCount + participant.PlayerDescriptorRecordCount)
            .ThenBy(static participant => participant.File, StringComparer.Ordinal)
            .ThenBy(static participant => participant.ObjectIdHex, StringComparer.Ordinal)
            .Take(12)
            .ToArray();
    }

    private static Tf2Ps3SourceEmbeddedObjectCount[] TopNestedCounts(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var counts))
        {
            return [];
        }

        return counts.EnumerateArray()
            .Select(static count => new Tf2Ps3SourceEmbeddedObjectCount(
                ReadString(count, "Value"),
                count.GetProperty("Count").GetInt32()))
            .Where(static count => count.Value.Length > 0)
            .Take(8)
            .ToArray();
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            ? value.GetString() ?? ""
            : "";
    }
}

public sealed record Tf2Ps3SourceEmbeddedObjectGrammarReport(
    string Status,
    string Note,
    Tf2Ps3SourceEmbeddedObjectGrammarInputs Inputs,
    Tf2Ps3SourceEmbeddedObjectGrammarSummary Summary,
    Tf2Ps3SourceEmbeddedObjectWireFormat[] WireFormats,
    Tf2Ps3SourceEmbeddedObjectCorpusEvidence CorpusEvidence,
    string[] NativeBuilderObligations);

public sealed record Tf2Ps3SourceEmbeddedObjectGrammarInputs(string EmbeddedObjectsReport);

public sealed record Tf2Ps3SourceEmbeddedObjectGrammarSummary(
    int PcapFileCount,
    int ActiveSourceFlowCount,
    int FilesWithEmbeddedRecords,
    int EmbeddedRecordCount,
    int CocMarkerCount,
    int DscMarkerCount,
    int PngMarkerCount,
    int PlayerObjectRecordCount,
    int FrozenStateObjectRecordCount,
    int PlayerDescriptorRecordCount,
    int PlayerStateLinkRecordCount,
    int MarkerCollisionNoiseCount);

public sealed record Tf2Ps3SourceEmbeddedObjectWireFormat(
    string Marker,
    string Role,
    int Length,
    int Version,
    string ClassIdRule,
    Tf2Ps3SourceEmbeddedObjectWireField[] Fields);

public sealed record Tf2Ps3SourceEmbeddedObjectWireField(
    string ByteRange,
    string Name,
    string Meaning);

public sealed record Tf2Ps3SourceEmbeddedObjectCorpusEvidence(
    Tf2Ps3SourceEmbeddedObjectCount[] TopRoleSchemas,
    Tf2Ps3SourceEmbeddedObjectCount[] TopRecordSchemas,
    Tf2Ps3SourceEmbeddedObjectCount[] TopClassIdCounts,
    Tf2Ps3SourceEmbeddedObjectCount[] TopPrintableCandidates,
    Tf2Ps3SourceEmbeddedObjectParticipantEvidence[] TopParticipants);

public sealed record Tf2Ps3SourceEmbeddedObjectParticipantEvidence(
    string File,
    string ObjectIdHex,
    int PlayerObjectRecordCount,
    int FrozenStateObjectRecordCount,
    int PlayerDescriptorRecordCount,
    Tf2Ps3SourceEmbeddedObjectCount[] ClassIdCounts,
    Tf2Ps3SourceEmbeddedObjectCount[] NameCounts);

public sealed record Tf2Ps3SourceEmbeddedObjectCount(string Value, int Count);
