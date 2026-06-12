using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class LiveHandoffEvidenceAnalyzer
{
    private readonly PlasmaPacketClassifier _classifier = new();

    public async Task<LiveHandoffEvidenceReport> AnalyzeAsync(
        string eventLogPath,
        string outputPath,
        IReadOnlyList<string> sourceEvidencePaths)
    {
        var report = Analyze(eventLogPath, sourceEvidencePaths);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public LiveHandoffEvidenceReport Analyze(string eventLogPath, IReadOnlyList<string> sourceEvidencePaths)
    {
        var eventLog = AnalyzeEventLog(eventLogPath);
        var sourceEvidence = sourceEvidencePaths
            .Select(AnalyzeSourceEvidence)
            .ToArray();
        var hasSourceMotdTraffic = eventLog.HasSourceTrafficEvent
            || sourceEvidence.Any(static evidence => evidence.HasSourceOrMotdTraffic);
        var status = eventLog.HasSourceHandoffEvent && hasSourceMotdTraffic ? "passed" : "missing-evidence";

        return new LiveHandoffEvidenceReport(
            "live-rpcs3-source-handoff-evidence",
            status,
            eventLog,
            sourceEvidence,
            eventLog.HasSourceHandoffEvent,
            hasSourceMotdTraffic,
            status == "passed"
                ? Array.Empty<string>()
                : MissingReasons(eventLog, sourceEvidence));
    }

    private static LiveGameManagerEventEvidence AnalyzeEventLog(string path)
    {
        if (!File.Exists(path))
        {
            return new LiveGameManagerEventEvidence(
                path,
                false,
                0,
                false,
                0,
                false,
                0,
                "",
                "",
                "",
                Array.Empty<LiveSourceSemanticRoleCount>(),
                Array.Empty<LiveSourceEmbeddedValueCount>(),
                Array.Empty<LiveSourceEmbeddedValueCount>(),
                Array.Empty<LiveSourceEmbeddedValueCount>(),
                Array.Empty<LiveSourceEmbeddedValueCount>());
        }

        var eventCount = 0;
        var handoffCount = 0;
        var sourceTrafficCount = 0;
        var firstEndpoint = "";
        var firstKind = "";
        var firstTimestamp = "";
        var sourceSemanticRoles = new List<string>();
        var embeddedRecordRoles = new List<string>();
        var embeddedObjectLinks = new List<string>();
        var embeddedDisplayNames = new List<string>();
        var embeddedClassIds = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            eventCount++;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var eventName = ReadString(root, "Event");
            var stateAfter = ReadString(root, "StateAfter");
            if (eventName is "source-traffic" or "source-send" or "source-proxy-forward" or "source-proxy-send")
            {
                sourceTrafficCount++;
                AddStringProperty(sourceSemanticRoles, root, "SourcePayloadSemanticRole");
                AddStringArrayProperty(embeddedRecordRoles, root, "SourceEmbeddedRecordRoles");
                AddStringArrayProperty(embeddedObjectLinks, root, "SourceEmbeddedObjectLinks");
                AddStringArrayProperty(embeddedDisplayNames, root, "SourceEmbeddedDisplayNames");
                AddStringArrayProperty(embeddedClassIds, root, "SourceEmbeddedClassIds");
            }

            if (eventName != "source-handoff" || stateAfter != "SourceHandoff")
            {
                continue;
            }

            handoffCount++;
            if (firstEndpoint.Length == 0)
            {
                firstEndpoint = ReadString(root, "Endpoint");
                firstKind = ReadString(root, "Kind");
                firstTimestamp = ReadString(root, "Timestamp");
            }
        }

        return new LiveGameManagerEventEvidence(
            path,
            true,
            eventCount,
            handoffCount > 0,
            handoffCount,
            sourceTrafficCount > 0,
            sourceTrafficCount,
            firstEndpoint,
            firstKind,
            firstTimestamp,
            TopSemanticRoleCounts(sourceSemanticRoles),
            TopEmbeddedValueCounts(embeddedRecordRoles),
            TopEmbeddedValueCounts(embeddedObjectLinks),
            TopEmbeddedValueCounts(embeddedDisplayNames),
            TopEmbeddedValueCounts(embeddedClassIds));
    }

    private LiveSourceEvidence AnalyzeSourceEvidence(string path)
    {
        if (!File.Exists(path))
        {
            return new LiveSourceEvidence(path, false, "missing", false, 0, 0, Array.Empty<string>());
        }

        if (path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
        {
            return AnalyzeSourcePcap(path);
        }

        return AnalyzeSourceText(path);
    }

    private LiveSourceEvidence AnalyzeSourcePcap(string path)
    {
        var packets = CaptureUdpPacketParser.ReadUdpPackets(path)
            .Where(static packet => packet.Payload.Length > 0)
            .ToArray();
        var sourceLike = 0;
        var sampleRoles = new List<string>();

        foreach (var packet in packets)
        {
            var decoded = _classifier.Decode(packet.Payload, enableNativeBinary: true);
            var preview = decoded.AsciiPreview(128);
            if (decoded.Kind == PlasmaCommandKind.SourceProbe
                || ContainsSourceMotdToken(preview)
                || packet.SourcePort is 27015 or 27016 or >= 3076 and <= 3105
                || packet.DestinationPort is 27015 or 27016 or >= 3076 and <= 3105)
            {
                sourceLike++;
                if (sampleRoles.Count < 8)
                {
                    sampleRoles.Add(decoded.Kind == PlasmaCommandKind.SourceProbe ? "source-probe" : $"udp:{packet.SourcePort}->{packet.DestinationPort}");
                }
            }
        }

        return new LiveSourceEvidence(
            path,
            true,
            "pcap",
            sourceLike > 0,
            packets.Length,
            sourceLike,
            sampleRoles.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static LiveSourceEvidence AnalyzeSourceText(string path)
    {
        var lines = File.ReadLines(path).ToArray();
        var replayEvents = lines
            .Select(ParseReplayEvent)
            .OfType<LiveSourceReplayLine>()
            .ToArray();
        var sourceLikeText = lines.Count(static line => ContainsSourceTrafficTextToken(line));
        var sourceLike = sourceLikeText + replayEvents.Length;
        var samples = lines
            .Where(static line => ContainsSourceTrafficTextToken(line))
            .Take(8)
            .Select(static line => line.Trim())
            .ToArray();
        if (samples.Length < 8)
        {
            samples = samples
                .Concat(replayEvents
                    .Select(static replay => $"{replay.Event}:{replay.SourceSessionPhase}:{replay.Direction}:seq={replay.SourceSequence}")
                    .Take(8 - samples.Length))
                .ToArray();
        }

        var phaseCounts = replayEvents
            .Where(static replay => replay.SourceSessionPhase.Length > 0)
            .GroupBy(static replay => replay.SourceSessionPhase, StringComparer.Ordinal)
            .OrderByDescending(static group => PhaseRank(group.Key))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new LiveSourcePhaseCount(group.Key, group.Count()))
            .ToArray();
        var roleCounts = replayEvents
            .Where(static replay => replay.SourcePayloadSemanticRole.Length > 0)
            .GroupBy(static replay => replay.SourcePayloadSemanticRole, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new LiveSourceSemanticRoleCount(group.Key, group.Count()))
            .ToArray();
        var embeddedRecordRoleCounts = TopEmbeddedValueCounts(replayEvents.SelectMany(static replay => replay.SourceEmbeddedRecordRoles));
        var embeddedObjectLinkCounts = TopEmbeddedValueCounts(replayEvents.SelectMany(static replay => replay.SourceEmbeddedObjectLinks));
        var embeddedDisplayNameCounts = TopEmbeddedValueCounts(replayEvents.SelectMany(static replay => replay.SourceEmbeddedDisplayNames));
        var embeddedClassIdCounts = TopEmbeddedValueCounts(replayEvents.SelectMany(static replay => replay.SourceEmbeddedClassIds));

        return new LiveSourceEvidence(
            path,
            true,
            "text",
            sourceLike > 0,
            lines.Length,
            sourceLike,
            samples)
        {
            SourceReplayEventCount = replayEvents.Length,
            SourceReplayReceiveEventCount = replayEvents.Count(static replay => replay.Event == "source-replay-receive"),
            SourceReplaySendEventCount = replayEvents.Count(static replay => replay.Event == "source-replay-send"),
            HasSteadyGameplayTraffic = replayEvents.Any(static replay => replay.SourceSessionPhase == "inferred-gameplay-steady-traffic"),
            HighestSourceSessionPhase = replayEvents
                .Select(static replay => replay.SourceSessionPhase)
                .Where(static phase => phase.Length > 0)
                .OrderByDescending(PhaseRank)
                .FirstOrDefault() ?? "",
            SourceSessionPhaseCounts = phaseCounts,
            SourcePayloadSemanticRoleCounts = roleCounts,
            SourceEmbeddedRecordRoleCounts = embeddedRecordRoleCounts,
            SourceEmbeddedObjectLinkCounts = embeddedObjectLinkCounts,
            SourceEmbeddedDisplayNameCounts = embeddedDisplayNameCounts,
            SourceEmbeddedClassIdCounts = embeddedClassIdCounts
        };
    }

    private static LiveSourceReplayLine? ParseReplayEvent(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.Contains("source-replay-", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var eventName = ReadString(root, "Event");
            if (!eventName.StartsWith("source-replay-", StringComparison.Ordinal))
            {
                return null;
            }

            return new LiveSourceReplayLine(
                eventName,
                ReadString(root, "Direction"),
                ReadString(root, "SourceSessionPhase"),
                ReadString(root, "SourcePayloadSemanticRole"),
                ReadStringArray(root, "SourceEmbeddedRecordRoles"),
                ReadStringArray(root, "SourceEmbeddedObjectLinks"),
                ReadStringArray(root, "SourceEmbeddedDisplayNames"),
                ReadStringArray(root, "SourceEmbeddedClassIds"),
                root.TryGetProperty("SourceSequence", out var sequence) && sequence.ValueKind == JsonValueKind.Number
                    ? sequence.GetInt32()
                    : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int PhaseRank(string phase)
    {
        return phase switch
        {
            "inferred-gameplay-steady-traffic" => 3,
            "inferred-loading-or-motd-transfer" => 2,
            "inferred-source-handoff-setup" => 1,
            _ => 0
        };
    }

    private static bool ContainsSourceMotdToken(string text)
    {
        return text.Contains("motd", StringComparison.OrdinalIgnoreCase)
            || text.Contains("A2S_", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ff ff ff ff", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ffffffff", StringComparison.OrdinalIgnoreCase)
            || text.Contains("client->backend", StringComparison.OrdinalIgnoreCase)
            || text.Contains("backend->client", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSourceTrafficTextToken(string text)
    {
        return ContainsSourceMotdToken(text)
            || text.Contains("\"Event\":\"source-traffic\"", StringComparison.Ordinal)
            || text.Contains("\"Event\":\"source-send\"", StringComparison.Ordinal)
            || text.Contains("\"Event\":\"source-proxy-forward\"", StringComparison.Ordinal)
            || text.Contains("\"Event\":\"source-proxy-send\"", StringComparison.Ordinal);
    }

    private static string[] MissingReasons(LiveGameManagerEventEvidence eventLog, LiveSourceEvidence[] sourceEvidence)
    {
        var reasons = new List<string>();
        if (!eventLog.Exists)
        {
            reasons.Add("Missing live GameManager JSONL event log.");
        }
        else if (!eventLog.HasSourceHandoffEvent)
        {
            reasons.Add("GameManager event log has no source-handoff event.");
        }

        if (sourceEvidence.Length == 0)
        {
            reasons.Add("No Source/MOTD evidence files were supplied and the GameManager event log has no source-traffic/source-send events.");
        }
        else if (!eventLog.HasSourceTrafficEvent && !sourceEvidence.Any(static evidence => evidence.HasSourceOrMotdTraffic))
        {
            reasons.Add("Supplied Source/MOTD evidence files do not show Source/MOTD traffic.");
        }

        return reasons.ToArray();
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.ToString() : "";
    }

    private static string[] ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return array.EnumerateArray()
            .Select(static item => item.GetString() ?? "")
            .Where(static value => value.Length > 0)
            .ToArray();
    }

    private static void AddStringProperty(List<string> values, JsonElement element, string property)
    {
        var value = ReadString(element, property);
        if (value.Length > 0)
        {
            values.Add(value);
        }
    }

    private static void AddStringArrayProperty(List<string> values, JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in array.EnumerateArray())
        {
            var value = item.GetString() ?? "";
            if (value.Length > 0)
            {
                values.Add(value);
            }
        }
    }

    private static LiveSourceSemanticRoleCount[] TopSemanticRoleCounts(IEnumerable<string> values)
    {
        return values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => new LiveSourceSemanticRoleCount(group.Key, group.Count()))
            .ToArray();
    }

    private static LiveSourceEmbeddedValueCount[] TopEmbeddedValueCounts(IEnumerable<string> values)
    {
        return values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => new LiveSourceEmbeddedValueCount(group.Key, group.Count()))
            .ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record LiveHandoffEvidenceReport(
    string Status,
    string GateStatus,
    LiveGameManagerEventEvidence GameManagerEvents,
    LiveSourceEvidence[] SourceEvidence,
    bool HasSourceHandoffEvent,
    bool HasSourceMotdTraffic,
    string[] MissingReasons);

public sealed record LiveGameManagerEventEvidence(
    string Path,
    bool Exists,
    int EventCount,
    bool HasSourceHandoffEvent,
    int SourceHandoffEventCount,
    bool HasSourceTrafficEvent,
    int SourceTrafficEventCount,
    string FirstSourceHandoffEndpoint,
    string FirstSourceHandoffKind,
    string FirstSourceHandoffTimestamp,
    LiveSourceSemanticRoleCount[] SourcePayloadSemanticRoleCounts,
    LiveSourceEmbeddedValueCount[] SourceEmbeddedRecordRoleCounts,
    LiveSourceEmbeddedValueCount[] SourceEmbeddedObjectLinkCounts,
    LiveSourceEmbeddedValueCount[] SourceEmbeddedDisplayNameCounts,
    LiveSourceEmbeddedValueCount[] SourceEmbeddedClassIdCounts);

public sealed record LiveSourceEvidence(
    string Path,
    bool Exists,
    string Kind,
    bool HasSourceOrMotdTraffic,
    int ObservedItemCount,
    int SourceOrMotdItemCount,
    string[] SampleEvidence)
{
    public int SourceReplayEventCount { get; init; }

    public int SourceReplayReceiveEventCount { get; init; }

    public int SourceReplaySendEventCount { get; init; }

    public bool HasSteadyGameplayTraffic { get; init; }

    public string HighestSourceSessionPhase { get; init; } = "";

    public LiveSourcePhaseCount[] SourceSessionPhaseCounts { get; init; } = Array.Empty<LiveSourcePhaseCount>();

    public LiveSourceSemanticRoleCount[] SourcePayloadSemanticRoleCounts { get; init; } = Array.Empty<LiveSourceSemanticRoleCount>();

    public LiveSourceEmbeddedValueCount[] SourceEmbeddedRecordRoleCounts { get; init; } = Array.Empty<LiveSourceEmbeddedValueCount>();

    public LiveSourceEmbeddedValueCount[] SourceEmbeddedObjectLinkCounts { get; init; } = Array.Empty<LiveSourceEmbeddedValueCount>();

    public LiveSourceEmbeddedValueCount[] SourceEmbeddedDisplayNameCounts { get; init; } = Array.Empty<LiveSourceEmbeddedValueCount>();

    public LiveSourceEmbeddedValueCount[] SourceEmbeddedClassIdCounts { get; init; } = Array.Empty<LiveSourceEmbeddedValueCount>();
}

public sealed record LiveSourcePhaseCount(
    string Phase,
    int Count);

public sealed record LiveSourceSemanticRoleCount(
    string Role,
    int Count);

public sealed record LiveSourceEmbeddedValueCount(
    string Value,
    int Count);

internal sealed record LiveSourceReplayLine(
    string Event,
    string Direction,
    string SourceSessionPhase,
    string SourcePayloadSemanticRole,
    string[] SourceEmbeddedRecordRoles,
    string[] SourceEmbeddedObjectLinks,
    string[] SourceEmbeddedDisplayNames,
    string[] SourceEmbeddedClassIds,
    int? SourceSequence);
