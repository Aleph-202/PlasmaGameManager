using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public sealed class LiveFrozenStateRetailComparator
{
    public async Task<LiveFrozenStateRetailComparisonReport> CompareAsync(
        string eventLogPath,
        string retailFrozenStateReportPath,
        string outputPath)
    {
        var report = Compare(eventLogPath, retailFrozenStateReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public LiveFrozenStateRetailComparisonReport Compare(
        string eventLogPath,
        string retailFrozenStateReportPath)
    {
        var liveEvents = ReadLiveEvents(eventLogPath);
        var liveTurns = BuildLiveTurns(liveEvents);
        var retailReport = JsonSerializer.Deserialize<PcapFrozenStateTurnReport>(File.ReadAllText(retailFrozenStateReportPath))
            ?? throw new InvalidOperationException($"Unable to read retail FrozenState report: {retailFrozenStateReportPath}");
        var retailTurns = BuildRetailTurns(retailReport).ToArray();
        var comparisons = liveTurns
            .Select(turn => CompareTurn(turn, retailTurns))
            .ToArray();
        var exactPattern = comparisons.Count(static comparison => comparison.BestMatch?.PayloadLengthPatternStatus == "exact");
        var needsNativeWork = comparisons.Count(static comparison => comparison.Status != "native-equivalent");

        return new LiveFrozenStateRetailComparisonReport(
            "live-frozen-state-vs-retail",
            eventLogPath,
            retailFrozenStateReportPath,
            File.Exists(eventLogPath),
            liveEvents.Length,
            liveTurns.Length,
            retailTurns.Length,
            exactPattern,
            needsNativeWork,
            needsNativeWork == 0 && liveTurns.Length > 0 ? "native-equivalent" : "needs-investigation",
            comparisons);
    }

    private static LiveFrozenStateEvent[] ReadLiveEvents(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var events = new List<LiveFrozenStateEvent>();
        var index = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var eventName = ReadString(root, "Event");
            if (!IsSourceClientEvent(eventName) && !IsSourceServerEvent(eventName))
            {
                index++;
                continue;
            }

            var embeddedObjectIds = PreferOrdered(root, "SourceEmbeddedObjectIdOrder", "SourceEmbeddedObjectIds");
            var embeddedObjectLinks = PreferOrdered(root, "SourceEmbeddedObjectLinkOrder", "SourceEmbeddedObjectLinks");
            var nativeRootObjectId = ReadNullableLong(root, "NativeSourceRootObjectId");
            events.Add(new LiveFrozenStateEvent(
                index++,
                eventName,
                ReadString(root, "Endpoint"),
                ReadInt(root, "PayloadLength"),
                ReadNullableInt(root, "SourceSequence"),
                ReadNullableInt(root, "SourceSequenceDelta"),
                ReadNullableInt(root, "SourceBodyLength"),
                ReadString(root, "SourcePacketShape"),
                ReadString(root, "SourcePayloadSemanticRole"),
                ReadString(root, "SourceNativeFrameKind"),
                ReadString(root, "SourceFragmentHeaderHex"),
                ReadString(root, "SourceEmbeddedPrefixHex"),
                ReadNullableInt(root, "SourceEmbeddedPrefixLength"),
                ReadString(root, "SourceBodySignature"),
                ReadString(root, "SourceClientPayloadRole"),
                embeddedObjectIds,
                NormalizeObjectLinks(embeddedObjectLinks, embeddedObjectIds, nativeRootObjectId),
                PreferOrdered(root, "SourceEmbeddedClassIdOrder", "SourceEmbeddedClassIds"),
                PreferOrdered(root, "SourceEmbeddedDisplayNameOrder", "SourceEmbeddedDisplayNames"),
                ReadStringArray(root, "SourceEmbeddedRecordSummaries"),
                ReadString(root, "Explanation")));
        }

        return events.ToArray();
    }

    private static LiveFrozenStateTurn[] BuildLiveTurns(IReadOnlyList<LiveFrozenStateEvent> events)
    {
        var turns = new List<LiveFrozenStateTurn>();
        var clients = new List<LiveFrozenStateEvent>();
        var serverFrozen = new List<LiveFrozenStateEvent>();
        var sawAnyServerForCurrentClientBurst = false;

        void Flush(LiveFrozenStateEvent? nextClient)
        {
            if (serverFrozen.Count == 0)
            {
                clients.Clear();
                sawAnyServerForCurrentClientBurst = false;
                return;
            }

            turns.Add(new LiveFrozenStateTurn(
                turns.Count,
                clients.ToArray(),
                serverFrozen.ToArray(),
                nextClient,
                string.Join("+", serverFrozen.Select(static packet => packet.PayloadLength.ToString())),
                string.Join("+", serverFrozen.Select(static packet => packet.EmbeddedObjectIds.Length.ToString()))));
            clients.Clear();
            serverFrozen.Clear();
            sawAnyServerForCurrentClientBurst = false;
        }

        foreach (var item in events)
        {
            if (IsSourceClientEvent(item.Event))
            {
                if (sawAnyServerForCurrentClientBurst)
                {
                    Flush(item);
                }

                clients.Add(item);
                continue;
            }

            if (item.SourcePayloadSemanticRole == "FrozenStateBatch")
            {
                serverFrozen.Add(item);
            }

            sawAnyServerForCurrentClientBurst = true;
        }

        Flush(null);
        return turns.ToArray();
    }

    private static IEnumerable<RetailFrozenStateTurn> BuildRetailTurns(PcapFrozenStateTurnReport report)
    {
        foreach (var file in report.Files.Where(static file => file.HasActiveSourceFlow))
        {
            foreach (var turn in file.FrozenStateTurns)
            {
                var packets = turn.ServerPackets
                    .Select(static packet => new RetailFrozenStatePacket(
                        packet.PayloadLength,
                        packet.BodyLength,
                        packet.PrefixLength,
                        packet.PrefixHex,
                        packet.Records
                            .Select(static record => record.ObjectId)
                            .OfType<string>()
                            .ToArray(),
                        packet.Records
                            .Select(static record => record.ObjectId is null || record.OwnerOrRootObjectId is null
                                ? null
                                : $"{record.ObjectId}->{record.OwnerOrRootObjectId}")
                            .OfType<string>()
                            .ToArray(),
                        packet.Records
                            .Select(static record => record.ClassId)
                            .OfType<string>()
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        packet.Records
                            .Select(static record => record.DisplayName)
                            .Where(static name => !string.IsNullOrWhiteSpace(name))
                            .Select(static name => name!)
                            .ToArray(),
                        packet.Records
                            .Select(static record => $"offset={record.Offset} marker={record.Marker} role={record.Role} object={record.ObjectId ?? ""} link={record.OwnerOrRootObjectId ?? ""} class={record.ClassId ?? ""} name={record.DisplayName ?? ""}")
                            .ToArray()))
                    .ToArray();
                yield return new RetailFrozenStateTurn(
                    file.File,
                    turn.TurnIndex,
                    turn.Classification,
                    turn.ServerPayloadLengthPattern,
                    string.Join("+", packets.Select(static packet => packet.ObjectIds.Length.ToString())),
                    turn.NextClientPacket?.PayloadLength,
                    turn.IsRetailPostRosterTrioShape,
                    packets);
            }
        }
    }

    private static LiveFrozenStateTurnComparison CompareTurn(
        LiveFrozenStateTurn live,
        IReadOnlyList<RetailFrozenStateTurn> retailTurns)
    {
        var candidates = retailTurns
            .Select(candidate => ScoreCandidate(live, candidate))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.File, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.TurnIndex)
            .Take(8)
            .ToArray();
        var best = candidates.FirstOrDefault();
        var status = best is not null
            && best.PayloadLengthPatternStatus == "exact"
            && best.ObjectCountPatternStatus == "exact"
            && best.MissingRetailObjectIds.Length == 0
            && best.UnexpectedLiveObjectIds.Length == 0
            && best.MissingRetailObjectLinks.Length == 0
            ? "native-equivalent"
            : "needs-investigation";
        var plainEnglish = best is null
            ? "No retail FrozenState turn was available for comparison."
            : BuildPlainEnglish(live, best);
        return new LiveFrozenStateTurnComparison(
            live.TurnIndex,
            live.ClientPackets.Select(SummarizeLivePacket).ToArray(),
            live.ServerPackets.Select(SummarizeLivePacket).ToArray(),
            live.NextClientPacket is null ? null : SummarizeLivePacket(live.NextClientPacket),
            live.ServerPayloadLengthPattern,
            live.ServerObjectCountPattern,
            status,
            plainEnglish,
            best,
            candidates);
    }

    private static LiveFrozenStateRetailCandidateMatch ScoreCandidate(
        LiveFrozenStateTurn live,
        RetailFrozenStateTurn retail)
    {
        var liveObjectIds = Set(live.ServerPackets.SelectMany(static packet => packet.EmbeddedObjectIds));
        var retailObjectIds = Set(retail.Packets.SelectMany(static packet => packet.ObjectIds));
        var liveNames = Set(live.ServerPackets.SelectMany(static packet => packet.EmbeddedDisplayNames));
        var retailNames = Set(retail.Packets.SelectMany(static packet => packet.DisplayNames));
        var liveLinks = Set(live.ServerPackets.SelectMany(static packet => packet.EmbeddedObjectLinks));
        var retailLinks = Set(retail.Packets.SelectMany(static packet => packet.ObjectLinks));
        var liveClassIds = Set(live.ServerPackets.SelectMany(static packet => packet.EmbeddedClassIds));
        var retailClassIds = Set(retail.Packets.SelectMany(static packet => packet.ClassIds));
        var livePrefixes = live.ServerPackets.Select(static packet => packet.EffectiveEmbeddedPrefixHex()).Where(static value => value.Length > 0).ToArray();
        var retailPrefixes = retail.Packets.Select(static packet => packet.PrefixHex).Where(static value => value.Length > 0).ToArray();
        var objectOrderStatus = OrderedStatus(
            live.ServerPackets.Select(static packet => packet.EmbeddedObjectIds).ToArray(),
            retail.Packets.Select(static packet => packet.ObjectIds).ToArray());
        var linkOrderStatus = OrderedStatus(
            live.ServerPackets.Select(static packet => packet.EmbeddedObjectLinks).ToArray(),
            retail.Packets.Select(static packet => packet.ObjectLinks).ToArray());

        var score = 0;
        score += live.ServerPayloadLengthPattern == retail.ServerPayloadLengthPattern ? 400 : 0;
        score += live.ServerObjectCountPattern == retail.ServerObjectCountPattern ? 250 : 0;
        score += IntersectionCount(liveObjectIds, retailObjectIds) * 30;
        score += IntersectionCount(liveNames, retailNames) * 25;
        score += IntersectionCount(liveClassIds, retailClassIds) * 15;
        score += IntersectionCount(liveLinks, retailLinks) * 20;
        score += live.NextClientPacket?.PayloadLength == retail.NextClientPayloadLength ? 40 : 0;
        score += objectOrderStatus == "exact" ? 80 : 0;
        score += linkOrderStatus == "exact" ? 40 : 0;
        score += PrefixStatus(livePrefixes, retailPrefixes) switch
        {
            "exact" => 40,
            "prefix-starts-with" => 25,
            "partial" => 10,
            _ => 0
        };

        return new LiveFrozenStateRetailCandidateMatch(
            retail.File,
            retail.TurnIndex,
            retail.Classification,
            retail.ServerPayloadLengthPattern,
            retail.ServerObjectCountPattern,
            retail.NextClientPayloadLength,
            retail.IsRetailPostRosterTrioShape,
            score,
            live.ServerPayloadLengthPattern == retail.ServerPayloadLengthPattern ? "exact" : "different",
            live.ServerObjectCountPattern == retail.ServerObjectCountPattern ? "exact" : "different",
            live.NextClientPacket?.PayloadLength == retail.NextClientPayloadLength ? "exact" : "different",
            PrefixStatus(livePrefixes, retailPrefixes),
            objectOrderStatus,
            linkOrderStatus,
            Missing(retailObjectIds, liveObjectIds),
            Missing(liveObjectIds, retailObjectIds),
            Missing(retailNames, liveNames),
            Missing(liveNames, retailNames),
            Missing(retailLinks, liveLinks),
            Missing(liveLinks, retailLinks),
            Missing(retailClassIds, liveClassIds),
            Missing(liveClassIds, retailClassIds),
            retail.Packets);
    }

    private static string BuildPlainEnglish(LiveFrozenStateTurn live, LiveFrozenStateRetailCandidateMatch best)
    {
        var clientPattern = string.Join("+", live.ClientPackets.Select(static packet => $"{packet.PayloadLength}/{packet.SourceClientPayloadRole}".TrimEnd('/')));
        var issues = new List<string>();
        if (best.PayloadLengthPatternStatus != "exact")
        {
            issues.Add($"server length pattern is {live.ServerPayloadLengthPattern}, retail expects {best.ServerPayloadLengthPattern}");
        }

        if (best.ObjectCountPatternStatus != "exact")
        {
            issues.Add($"COc object grouping is {live.ServerObjectCountPattern}, retail expects {best.ServerObjectCountPattern}");
        }

        if (best.MissingRetailObjectIds.Length > 0)
        {
            issues.Add($"missing retail object ids {string.Join(",", best.MissingRetailObjectIds)}");
        }

        if (best.UnexpectedLiveObjectIds.Length > 0)
        {
            issues.Add($"unexpected live object ids {string.Join(",", best.UnexpectedLiveObjectIds)}");
        }

        if (best.MissingRetailDisplayNames.Length > 0 || best.UnexpectedLiveDisplayNames.Length > 0)
        {
            issues.Add($"display names differ, which is dynamic unless reproducing the exact captured roster; retail={string.Join(",", best.MissingRetailDisplayNames)} live={string.Join(",", best.UnexpectedLiveDisplayNames)}");
        }

        if (best.MissingRetailObjectLinks.Length > 0)
        {
            issues.Add($"missing retail owner links {string.Join(",", best.MissingRetailObjectLinks)}");
        }

        if (best.PrefixStatus is not "exact" and not "prefix-starts-with")
        {
            issues.Add($"prefix/header status is {best.PrefixStatus}");
        }

        if (best.ObjectOrderStatus != "exact")
        {
            issues.Add($"COc object order is {best.ObjectOrderStatus}");
        }

        if (best.ObjectLinkOrderStatus != "exact")
        {
            issues.Add($"COc owner-link order is {best.ObjectLinkOrderStatus}");
        }

        var issueText = issues.Count == 0 ? "no structural differences found" : string.Join("; ", issues);
        return $"After client packet pattern {clientPattern}, live FrozenState replies best match retail {best.File} turn {best.TurnIndex}. {issueText}.";
    }

    private static LiveFrozenStatePacketSummary SummarizeLivePacket(LiveFrozenStateEvent packet)
    {
        return new LiveFrozenStatePacketSummary(
            packet.EventIndex,
            packet.Event,
            packet.PayloadLength,
            packet.SourceSequence,
            packet.SourceSequenceDelta,
            packet.SourceBodyLength,
            packet.SourcePacketShape,
            packet.SourcePayloadSemanticRole,
            packet.SourceNativeFrameKind,
            packet.SourceFragmentHeaderHex,
            packet.SourceEmbeddedPrefixLength,
            packet.SourceEmbeddedPrefixHex,
            packet.SourceClientPayloadRole,
            packet.EmbeddedObjectIds,
            packet.EmbeddedObjectLinks,
            packet.EmbeddedClassIds,
            packet.EmbeddedDisplayNames,
            packet.EmbeddedRecordSummaries,
            packet.Explanation);
    }

    private static string PrefixStatus(IReadOnlyList<string> livePrefixes, IReadOnlyList<string> retailPrefixes)
    {
        if (livePrefixes.Count == 0 || retailPrefixes.Count == 0)
        {
            return "missing";
        }

        if (livePrefixes.SequenceEqual(retailPrefixes, StringComparer.Ordinal))
        {
            return "exact";
        }

        var compared = Math.Min(livePrefixes.Count, retailPrefixes.Count);
        var starts = 0;
        for (var i = 0; i < compared; i++)
        {
            if (retailPrefixes[i].StartsWith(livePrefixes[i], StringComparison.Ordinal)
                || livePrefixes[i].StartsWith(retailPrefixes[i], StringComparison.Ordinal))
            {
                starts++;
            }
        }

        if (starts == compared)
        {
            return "prefix-starts-with";
        }

        return starts > 0 ? "partial" : "different";
    }

    private static string OrderedStatus(IReadOnlyList<string[]> live, IReadOnlyList<string[]> retail)
    {
        if (live.Count == 0 || retail.Count == 0)
        {
            return "missing";
        }

        if (live.Count != retail.Count)
        {
            return "packet-count-different";
        }

        var anyDifferent = false;
        for (var i = 0; i < live.Count; i++)
        {
            if (live[i].SequenceEqual(retail[i], StringComparer.Ordinal))
            {
                continue;
            }

            if (Set(live[i]).SetEquals(retail[i]))
            {
                anyDifferent = true;
                continue;
            }

            return "different-members";
        }

        return anyDifferent ? "same-members-different-order" : "exact";
    }

    private static int IntersectionCount(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        return left.Count(right.Contains);
    }

    private static string[] Missing(IReadOnlySet<string> expected, IReadOnlySet<string> actual)
    {
        return expected
            .Where(value => !actual.Contains(value))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> Set(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsSourceClientEvent(string eventName)
    {
        return eventName is "source-traffic" or "source-proxy-forward";
    }

    private static bool IsSourceServerEvent(string eventName)
    {
        return eventName is "source-generated-send" or "source-send" or "source-proxy-send";
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static int? ReadNullableInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static long? ReadNullableLong(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static string[] ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(static item => item.ValueKind != JsonValueKind.Null)
            .Select(static item => item.ToString())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string[] PreferOrdered(JsonElement element, string orderedProperty, string fallbackProperty)
    {
        var ordered = ReadStringArray(element, orderedProperty);
        return ordered.Length > 0 ? ordered : ReadStringArray(element, fallbackProperty);
    }

    private static string[] NormalizeObjectLinks(
        string[] links,
        IReadOnlyList<string> objectIds,
        long? rootObjectId)
    {
        if (links.Length > 0 || objectIds.Count == 0 || rootObjectId is null or <= 0)
        {
            return links;
        }

        var root = ((uint)rootObjectId.Value).ToString("x8");
        return objectIds
            .Select(objectId => $"{objectId}->{root}")
            .ToArray();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record LiveFrozenStateRetailComparisonReport(
    string Kind,
    string EventLogPath,
    string RetailFrozenStateReportPath,
    bool EventLogExists,
    int LiveSourceEventCount,
    int LiveFrozenStateTurnCount,
    int RetailFrozenStateTurnCount,
    int ExactLengthPatternMatchCount,
    int NeedsInvestigationTurnCount,
    string GateStatus,
    LiveFrozenStateTurnComparison[] Turns);

public sealed record LiveFrozenStateTurnComparison(
    int LiveTurnIndex,
    LiveFrozenStatePacketSummary[] ClientPackets,
    LiveFrozenStatePacketSummary[] ServerPackets,
    LiveFrozenStatePacketSummary? NextClientPacket,
    string ServerPayloadLengthPattern,
    string ServerObjectCountPattern,
    string Status,
    string PlainEnglish,
    LiveFrozenStateRetailCandidateMatch? BestMatch,
    LiveFrozenStateRetailCandidateMatch[] CandidateMatches);

public sealed record LiveFrozenStateRetailCandidateMatch(
    string File,
    int TurnIndex,
    string Classification,
    string ServerPayloadLengthPattern,
    string ServerObjectCountPattern,
    int? NextClientPayloadLength,
    bool IsRetailPostRosterTrioShape,
    int Score,
    string PayloadLengthPatternStatus,
    string ObjectCountPatternStatus,
    string NextClientPayloadLengthStatus,
    string PrefixStatus,
    string ObjectOrderStatus,
    string ObjectLinkOrderStatus,
    string[] MissingRetailObjectIds,
    string[] UnexpectedLiveObjectIds,
    string[] MissingRetailDisplayNames,
    string[] UnexpectedLiveDisplayNames,
    string[] MissingRetailObjectLinks,
    string[] UnexpectedLiveObjectLinks,
    string[] MissingRetailClassIds,
    string[] UnexpectedLiveClassIds,
    RetailFrozenStatePacket[] RetailPackets);

public sealed record LiveFrozenStatePacketSummary(
    int EventIndex,
    string Event,
    int PayloadLength,
    int? SourceSequence,
    int? SourceSequenceDelta,
    int? SourceBodyLength,
    string SourcePacketShape,
    string SourcePayloadSemanticRole,
    string SourceNativeFrameKind,
    string SourceFragmentHeaderHex,
    int? SourceEmbeddedPrefixLength,
    string SourceEmbeddedPrefixHex,
    string SourceClientPayloadRole,
    string[] EmbeddedObjectIds,
    string[] EmbeddedObjectLinks,
    string[] EmbeddedClassIds,
    string[] EmbeddedDisplayNames,
    string[] EmbeddedRecordSummaries,
    string Explanation);

internal sealed record LiveFrozenStateEvent(
    int EventIndex,
    string Event,
    string Endpoint,
    int PayloadLength,
    int? SourceSequence,
    int? SourceSequenceDelta,
    int? SourceBodyLength,
    string SourcePacketShape,
    string SourcePayloadSemanticRole,
    string SourceNativeFrameKind,
    string SourceFragmentHeaderHex,
    string SourceEmbeddedPrefixHex,
    int? SourceEmbeddedPrefixLength,
    string SourceBodySignature,
    string SourceClientPayloadRole,
    string[] EmbeddedObjectIds,
    string[] EmbeddedObjectLinks,
    string[] EmbeddedClassIds,
    string[] EmbeddedDisplayNames,
    string[] EmbeddedRecordSummaries,
    string Explanation);

internal static class LiveFrozenStateEventExtensions
{
    public static string EffectiveEmbeddedPrefixHex(this LiveFrozenStateEvent packet)
    {
        return packet.SourceEmbeddedPrefixHex.Length > 0
            ? packet.SourceEmbeddedPrefixHex
            : packet.SourceFragmentHeaderHex;
    }
}

internal sealed record LiveFrozenStateTurn(
    int TurnIndex,
    LiveFrozenStateEvent[] ClientPackets,
    LiveFrozenStateEvent[] ServerPackets,
    LiveFrozenStateEvent? NextClientPacket,
    string ServerPayloadLengthPattern,
    string ServerObjectCountPattern);

public sealed record RetailFrozenStateTurn(
    string File,
    int TurnIndex,
    string Classification,
    string ServerPayloadLengthPattern,
    string ServerObjectCountPattern,
    int? NextClientPayloadLength,
    bool IsRetailPostRosterTrioShape,
    RetailFrozenStatePacket[] Packets);

public sealed record RetailFrozenStatePacket(
    int PayloadLength,
    int? BodyLength,
    int PrefixLength,
    string PrefixHex,
    string[] ObjectIds,
    string[] ObjectLinks,
    string[] ClassIds,
    string[] DisplayNames,
    string[] RecordSummaries);
