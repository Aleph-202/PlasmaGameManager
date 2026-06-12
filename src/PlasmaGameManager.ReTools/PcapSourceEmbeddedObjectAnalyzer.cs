using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceEmbeddedObjectAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceEmbeddedObjectReport> AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var report = AnalyzeDirectory(inputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceEmbeddedObjectReport AnalyzeDirectory(string inputDirectory)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var reports = files.Select(file => AnalyzeFile(inputDirectory, file)).ToArray();
        return new PcapSourceEmbeddedObjectReport(
            "pcap-source-embedded-object-records",
            BuildSummary(reports),
            reports);
    }

    private PcapSourceEmbeddedObjectFile AnalyzeFile(string inputDirectory, string file)
    {
        var relativePath = Path.GetRelativePath(inputDirectory, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceEmbeddedObjectFile(
                relativePath,
                false,
                "",
                "",
                0,
                0,
                0,
                0,
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                []);
        }

        var packets = replay.SourcePackets
            .Select(packet => DecodePacket(packet))
            .Where(static packet => packet is not null)
            .Select(static packet => packet!)
            .ToArray();
        var records = packets.SelectMany(static packet => packet.Records).ToArray();
        return new PcapSourceEmbeddedObjectFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.SourcePackets.Length,
            packets.Length,
            records.Length,
            records.Count(static record => record.LooksFixedStride),
            TopCounts(records, static record => record.Marker),
            TopCounts(records, static record => $"{record.Marker}:{record.Length}"),
            TopCounts(records, SchemaKey),
            TopCounts(records, static record => record.Role.ToString()),
            TopCounts(records, RoleSchemaKey),
            TopCounts(records, static record => FieldKey(record.Marker, "a", record.FieldA)),
            TopCounts(records, static record => FieldKey(record.Marker, "b", record.FieldB)),
            TopCounts(records, static record => FieldKey(record.Marker, "c", record.FieldC)),
            TopCounts(records, ObjectLinkKey),
            TopCounts(records.SelectMany(static record => record.PrintableCandidates), static candidate => candidate),
            BuildParticipantSummaries(records),
            packets
                .Where(static packet => packet.Records.Length > 0)
                .Take(24)
                .Select(static packet => new PcapSourceEmbeddedObjectPacketSample(
                    packet.Packet.PacketIndex,
                    packet.Packet.Direction.ToString(),
                    packet.Sequence,
                    packet.BodyLength,
                    packet.Records.Select(static record => new PcapSourceEmbeddedObjectRecordSample(
                        record.Marker,
                        record.Offset,
                        record.Length,
                        record.HeaderHex,
                        record.Version,
                        record.FieldA,
                        record.FieldB,
                        record.FieldC,
                        Hex(record.ObjectId),
                        Hex(record.LinkedObjectId),
                        Hex(record.ClassId),
                        record.DisplayName ?? "",
                        record.Role.ToString(),
                        record.PrintableCandidates)).ToArray()))
                .ToArray(),
            packets
                .SelectMany(static packet => packet.Records
                    .Where(static record => record.Role == Ps3SourceEmbeddedObjectRecordRole.Unknown)
                    .Select(record => new PcapSourceEmbeddedObjectUnknownRecordSample(
                        packet.Packet.PacketIndex,
                        packet.Packet.Direction.ToString(),
                        packet.Sequence,
                        packet.BodyLength,
                        record.Marker,
                        record.Offset,
                        record.Length,
                        record.HeaderHex,
                        record.Version,
                        record.FieldA,
                        record.FieldB,
                        record.FieldC,
                        SchemaKey(record),
                        record.PrintableCandidates)))
                .Take(32)
                .ToArray());
    }

    private static PcapSourceEmbeddedObjectPacket? DecodePacket(PcapActiveFlowDatagram packet)
    {
        if (!Ps3SourceTransportPacket.TryDecode(packet.Payload, out var decoded))
        {
            return null;
        }

        var records = Ps3SourceEmbeddedObjectRecords.Extract(decoded.Body);
        if (records.Length == 0)
        {
            return null;
        }

        return new PcapSourceEmbeddedObjectPacket(packet, decoded.CandidateSequence, decoded.Body.Length, records);
    }

    private static PcapSourceEmbeddedObjectSummary BuildSummary(PcapSourceEmbeddedObjectFile[] files)
    {
        var active = files.Where(static file => file.HasActiveSourceFlow).ToArray();
        var withRecords = active.Where(static file => file.PacketsWithEmbeddedRecords > 0).ToArray();
        var markerCounts = active
            .SelectMany(static file => file.MarkerCounts.Select(static count => (count.Value, count.Count)))
            .GroupBy(static item => item.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Sum(static item => item.Count), StringComparer.Ordinal);
        var roleCounts = active
            .SelectMany(static file => file.RecordRoleCounts.Select(static count => (count.Value, count.Count)))
            .GroupBy(static item => item.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Sum(static item => item.Count), StringComparer.Ordinal);
        return new PcapSourceEmbeddedObjectSummary(
            files.Length,
            active.Length,
            withRecords.Length,
            active.Sum(static file => file.SourcePacketCount),
            active.Sum(static file => file.PacketsWithEmbeddedRecords),
            active.Sum(static file => file.EmbeddedRecordCount),
            active.Sum(static file => file.FixedStrideRecordCount),
            markerCounts,
            roleCounts);
    }

    private static PcapSourceEmbeddedObjectCount[] TopCounts<T>(IEnumerable<T> values, Func<T, string> selector)
    {
        return values
            .Select(selector)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(32)
            .Select(static group => new PcapSourceEmbeddedObjectCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string SchemaKey(Ps3SourceEmbeddedObjectRecord record)
    {
        return $"{record.Marker}:{record.Length}:v{record.Version?.ToString() ?? "?"}:c{Hex(record.FieldC)}";
    }

    private static string RoleSchemaKey(Ps3SourceEmbeddedObjectRecord record)
    {
        return $"{record.Role}:{SchemaKey(record)}";
    }

    private static string FieldKey(string marker, string field, uint? value)
    {
        return value is null ? "" : $"{marker}:{field}:{Hex(value)}";
    }

    private static string ObjectLinkKey(Ps3SourceEmbeddedObjectRecord record)
    {
        return record.LinkedObjectId is null ? "" : $"{Hex(record.ObjectId)}->{Hex(record.LinkedObjectId)}";
    }

    private static PcapSourceEmbeddedObjectParticipantSummary[] BuildParticipantSummaries(
        Ps3SourceEmbeddedObjectRecord[] records)
    {
        static IEnumerable<uint> RecordIds(Ps3SourceEmbeddedObjectRecord record)
        {
            if (record.FieldA is not null)
            {
                yield return record.FieldA.Value;
            }

            if (record.FieldB is not null)
            {
                yield return record.FieldB.Value;
            }
        }

        var ids = records
            .SelectMany(RecordIds)
            .Distinct()
            .OrderBy(static id => id)
            .ToArray();
        var summaries = new List<PcapSourceEmbeddedObjectParticipantSummary>();
        foreach (var id in ids)
        {
            var asFieldA = records.Where(record => record.FieldA == id).ToArray();
            var asFieldB = records.Where(record => record.FieldB == id).ToArray();
            var selfLinks = records.Count(record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                && record.FieldA == id
                && record.FieldB == id);
            var outboundLinks = records.Count(record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                && record.FieldA == id
                && record.FieldB != id);
            var inboundLinks = records.Count(record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                && record.FieldA != id
                && record.FieldB == id);
            var objectRecords = asFieldA.Count(static record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerObject);
            var frozenStateRecords = asFieldA.Count(static record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject);
            var descriptorRecords = asFieldA.Count(static record =>
                record.Role == Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor);
            var names = asFieldA
                .SelectMany(static record => record.PrintableCandidates)
                .Where(static candidate => !candidate.Equals("FrozenState_", StringComparison.Ordinal))
                .GroupBy(static candidate => candidate, StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static group => new PcapSourceEmbeddedObjectCount(group.Key, group.Count()))
                .ToArray();
            var classIds = asFieldA
                .Where(static record => record.ClassId is not null)
                .GroupBy(static record => Hex(record.ClassId), StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static group => new PcapSourceEmbeddedObjectCount(group.Key, group.Count()))
                .ToArray();
            var outboundTargets = records
                .Where(record => record.LinkedObjectId is not null && record.ObjectId == id)
                .GroupBy(record => Hex(record.LinkedObjectId), StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static group => new PcapSourceEmbeddedObjectCount(group.Key, group.Count()))
                .ToArray();
            var inboundSources = records
                .Where(record => record.LinkedObjectId == id && record.ObjectId != id)
                .GroupBy(record => Hex(record.ObjectId), StringComparer.Ordinal)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.Ordinal)
                .Take(8)
                .Select(static group => new PcapSourceEmbeddedObjectCount(group.Key, group.Count()))
                .ToArray();

            summaries.Add(new PcapSourceEmbeddedObjectParticipantSummary(
                Hex(id),
                asFieldA.Length,
                asFieldB.Length,
                selfLinks,
                outboundLinks,
                inboundLinks,
                objectRecords,
                frozenStateRecords,
                descriptorRecords,
                names,
                classIds,
                outboundTargets,
                inboundSources));
        }

        return summaries
            .OrderByDescending(static participant => participant.FieldARecordCount + participant.FieldBRecordCount)
            .ThenBy(static participant => participant.ObjectIdHex, StringComparer.Ordinal)
            .Take(32)
            .ToArray();
    }

    private static string Hex(uint? value)
    {
        return value is null ? "none" : value.Value.ToString("x8");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record PcapSourceEmbeddedObjectPacket(
        PcapActiveFlowDatagram Packet,
        int Sequence,
        int BodyLength,
        Ps3SourceEmbeddedObjectRecord[] Records);
}

public sealed record PcapSourceEmbeddedObjectReport(
    string Status,
    PcapSourceEmbeddedObjectSummary Summary,
    PcapSourceEmbeddedObjectFile[] Files);

public sealed record PcapSourceEmbeddedObjectSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int FilesWithEmbeddedRecords,
    int SourcePacketCount,
    int PacketsWithEmbeddedRecords,
    int EmbeddedRecordCount,
    int FixedStrideRecordCount,
    IReadOnlyDictionary<string, int> MarkerCounts,
    IReadOnlyDictionary<string, int> RecordRoleCounts);

public sealed record PcapSourceEmbeddedObjectFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int PacketsWithEmbeddedRecords,
    int EmbeddedRecordCount,
    int FixedStrideRecordCount,
    PcapSourceEmbeddedObjectCount[] MarkerCounts,
    PcapSourceEmbeddedObjectCount[] RecordLengthCounts,
    PcapSourceEmbeddedObjectCount[] RecordSchemaCounts,
    PcapSourceEmbeddedObjectCount[] RecordRoleCounts,
    PcapSourceEmbeddedObjectCount[] RecordRoleSchemaCounts,
    PcapSourceEmbeddedObjectCount[] FieldACounts,
    PcapSourceEmbeddedObjectCount[] FieldBCounts,
    PcapSourceEmbeddedObjectCount[] FieldCCounts,
    PcapSourceEmbeddedObjectCount[] ObjectLinkCounts,
    PcapSourceEmbeddedObjectCount[] PrintableCandidateCounts,
    PcapSourceEmbeddedObjectParticipantSummary[] ParticipantSummaries,
    PcapSourceEmbeddedObjectPacketSample[] Samples,
    PcapSourceEmbeddedObjectUnknownRecordSample[] UnknownRecordSamples);

public sealed record PcapSourceEmbeddedObjectCount(
    string Value,
    int Count);

public sealed record PcapSourceEmbeddedObjectParticipantSummary(
    string ObjectIdHex,
    int FieldARecordCount,
    int FieldBRecordCount,
    int SelfLinkCount,
    int OutboundLinkCount,
    int InboundLinkCount,
    int PlayerObjectRecordCount,
    int FrozenStateObjectRecordCount,
    int PlayerDescriptorRecordCount,
    PcapSourceEmbeddedObjectCount[] NameCounts,
    PcapSourceEmbeddedObjectCount[] ClassIdCounts,
    PcapSourceEmbeddedObjectCount[] OutboundTargetCounts,
    PcapSourceEmbeddedObjectCount[] InboundSourceCounts);

public sealed record PcapSourceEmbeddedObjectPacketSample(
    long PacketIndex,
    string Direction,
    int Sequence,
    int BodyLength,
    PcapSourceEmbeddedObjectRecordSample[] Records);

public sealed record PcapSourceEmbeddedObjectRecordSample(
    string Marker,
    int Offset,
    int Length,
    string HeaderHex,
    int? Version,
    uint? FieldA,
    uint? FieldB,
    uint? FieldC,
    string ObjectIdHex,
    string LinkedObjectIdHex,
    string ClassIdHex,
    string DisplayName,
    string Role,
    string[] PrintableCandidates);

public sealed record PcapSourceEmbeddedObjectUnknownRecordSample(
    long PacketIndex,
    string Direction,
    int Sequence,
    int BodyLength,
    string Marker,
    int Offset,
    int Length,
    string HeaderHex,
    int? Version,
    uint? FieldA,
    uint? FieldB,
    uint? FieldC,
    string Schema,
    string[] PrintableCandidates);
