using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceLoadingFrameDebtReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceLoadingFrameDebtReport> ReduceAsync(
        string responderSourcePath,
        string nativeTemplateDebtPath,
        string queuedPrefixContractPath,
        string tunnelGhidraMapPath,
        string outputPath)
    {
        var source = await File.ReadAllTextAsync(responderSourcePath);
        var lines = SplitLines(source);
        var collections = ParseFrameCollections(source, lines)
            .OrderBy(static collection => collection.SortKey)
            .ThenBy(static collection => collection.Name, StringComparer.Ordinal)
            .ToArray();
        var frames = collections
            .SelectMany(static collection => collection.Frames)
            .Select(ClassifyFrame)
            .OrderBy(static frame => frame.CollectionSortKey)
            .ThenBy(static frame => frame.StageIndex)
            .ThenBy(static frame => frame.FrameIndex)
            .ToArray();
        var collectionSummaries = frames
            .GroupBy(static frame => new
            {
                frame.Collection,
                frame.CollectionSortKey,
                frame.Variant,
                frame.Phase
            })
            .Select(static group => new Tf2Ps3SourceLoadingFrameCollectionSummary(
                group.Key.Collection,
                group.Key.Variant,
                group.Key.Phase,
                group.Select(static frame => frame.StageIndex).Distinct().Count(),
                group.Count(),
                group.Count(static frame => frame.Kind == "HighEntropy"),
                group.Count(static frame => frame.Kind == "MixedBinary"),
                group.Count(static frame => frame.Kind == "PlayerStateLink"),
                group.Count(static frame => frame.Risk == "production-fake-generated-filler"),
                group.Count(static frame => frame.Risk == "native-records-with-generated-prefix-or-padding"),
                group.Count(static frame => frame.Risk == "steady-native-snapshot-with-deterministic-padding-risk"),
                group.Sum(static frame => frame.BlockingByteCount)))
            .OrderBy(static summary => CollectionSortKey(summary.Collection))
            .ThenBy(static summary => summary.Collection, StringComparer.Ordinal)
            .ToArray();

        using var nativeTemplateDebt = JsonDocument.Parse(await File.ReadAllTextAsync(nativeTemplateDebtPath));
        using var queuedPrefixContract = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));
        using var tunnelGhidraMap = JsonDocument.Parse(await File.ReadAllTextAsync(tunnelGhidraMapPath));

        var directGeneratorCallSites = ExtractGeneratorCallSites(lines);
        var queuedSummary = queuedPrefixContract.RootElement.GetProperty("Summary");
        var nativeDebtSummary = nativeTemplateDebt.RootElement.GetProperty("Summary");
        var report = new Tf2Ps3SourceLoadingFrameDebtReport(
            "tf2ps3-source-loading-frame-debt",
            "Per-frame implementation ledger for PS3 Source loading/map-load payloads. This report is intentionally conservative: a packet remains debt until its bytes are built from named TF.elf/server.dll fields rather than deterministic filler or captured/static bodies.",
            new Tf2Ps3SourceLoadingFrameDebtInputs(
                responderSourcePath,
                nativeTemplateDebtPath,
                queuedPrefixContractPath,
                tunnelGhidraMapPath),
            new Tf2Ps3SourceLoadingFrameDebtSummary(
                collections.Length,
                frames.Length,
                frames.Count(static frame => frame.Phase != "steady-state-delta"),
                frames.Count(static frame => frame.Phase == "steady-state-delta"),
                frames.Count(static frame => frame.Kind == "HighEntropy"),
                frames.Count(static frame => frame.Kind == "MixedBinary"),
                frames.Count(static frame => frame.Kind == "PlayerStateLink"),
                frames.Count(static frame => frame.Risk == "production-fake-generated-filler"),
                frames.Count(static frame => frame.Risk == "native-records-with-generated-prefix-or-padding"),
                frames.Count(static frame => frame.Risk == "steady-native-snapshot-with-deterministic-padding-risk"),
                frames.Sum(static frame => frame.BlockingByteCount),
                directGeneratorCallSites.Length,
                nativeDebtSummary.GetProperty("StaticHexTemplateCount").GetInt32(),
                nativeDebtSummary.GetProperty("HighEntropyLoadingFrameCount").GetInt32(),
                nativeDebtSummary.GetProperty("MixedBinaryLoadingFrameCount").GetInt32(),
                queuedSummary.GetProperty("GeneratedQueuedPrefixDebtCount").GetInt32(),
                queuedSummary.GetProperty("GeneratedQueuedPrefixDebtBytes").GetInt32(),
                queuedSummary.GetProperty("GeneratedQueuedPrefixRecordCount").GetInt32(),
                queuedSummary.GetProperty("OpaquePrefixBytes").GetInt32(),
                queuedSummary.GetProperty("EaTunnelReferenceCount").GetInt32(),
                queuedSummary.GetProperty("EaTunnelNeighborhoodReferenceCount").GetInt32(),
                CountArray(tunnelGhidraMap.RootElement, "reachedFunctions")),
            collectionSummaries,
            frames,
            frames
                .OrderByDescending(static frame => frame.PriorityScore)
                .ThenByDescending(static frame => frame.BlockingByteCount)
                .ThenBy(static frame => frame.CollectionSortKey)
                .ThenBy(static frame => frame.StageIndex)
                .ThenBy(static frame => frame.FrameIndex)
                .Take(80)
                .ToArray(),
            directGeneratorCallSites,
            [
                "Early loading HighEntropy and MixedBinary frames are still generated filler. They must be replaced with TF.elf queued Source control/submessage writers or snapshot/entity-delta writers before this responder is native.",
                "PlayerStateLink frames contain native PNG records, but their deterministic prefix/trailing bytes are still queued-prefix debt unless BlockingByteCount is zero.",
                "Steady-state frames routed through TryBuildSteadyNativeSnapshotBody are not fully proven native while PadNativeWrappedPayload can append deterministic high-entropy bytes to reach pcap-shaped lengths.",
                "EA Tunnel remains a closed lead for client-visible packet ownership in current evidence: server.dll has no executable xrefs from the EA Tunnel string or descriptor neighborhood. Continue treating TF.elf's 008bc978 -> 008b9f70/008bb058/008bc490 path as authoritative unless new xrefs appear.",
                "The native completion gate for loading/map-load packets is ProductionFakeFrameCount == 0, NativeRecordWithGeneratedPrefixFrameCount == 0, GeneratedQueuedPrefixDebtBytes == 0, SteadyNativeSnapshotPaddingRiskCount == 0, plus StaticHexTemplateCount == 0 in source-native-template-debt."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceLoadingFrameFrame ClassifyFrame(Tf2Ps3SourceLoadingFrameRawFrame frame)
    {
        var steadyCandidate = frame.SeedStageIndex >= 200 && frame.Length >= 48;
        var forcedPlayerHighEntropy = frame.Kind == "PlayerStateLink"
            && ShouldBuildPlayerStateLinkAsHighEntropy(frame.Variant, frame.Length, frame.SeedStageIndex);

        if (steadyCandidate)
        {
            return BuildClassifiedFrame(
                frame,
                "TryBuildSteadyNativeSnapshotBody -> Ps3SourceSnapshotFrame -> PadNativeWrappedPayload",
                "steady-native-snapshot-with-deterministic-padding-risk",
                "native-snapshot-and-entity-delta-route",
                frame.Length,
                (frame.Length * 4) + 500,
                "Recover the exact queued snapshot/entity-delta body length or remove PadNativeWrappedPayload so native snapshots are not length-shaped with deterministic filler.");
        }

        if (frame.Kind == "HighEntropy" || forcedPlayerHighEntropy)
        {
            return BuildClassifiedFrame(
                frame,
                "BuildHighEntropyBinaryBody -> FillHighEntropyDeterministic",
                "production-fake-generated-filler",
                "native-snapshot-and-entity-delta-route",
                frame.Length,
                (frame.Length * 4) + (forcedPlayerHighEntropy ? 250 : 300),
                forcedPlayerHighEntropy
                    ? "This PlayerStateLink-shaped slot is deliberately forced into high-entropy filler for this variant/phase. Recover the real TF.elf writer before sending it in production."
                    : "Replace this high-entropy filler with named Source snapshot/entity-delta or object-stream fields from TF.elf/server.dll semantics.");
        }

        if (frame.Kind == "MixedBinary")
        {
            return BuildClassifiedFrame(
                frame,
                "BuildMixedBinaryBody",
                "production-fake-generated-filler",
                "queued-peer-submessage-boundaries",
                frame.Length,
                (frame.Length * 3) + 220,
                "Replace this printable deterministic filler with named queued-peer control/submessage fields around COc/DSC/PNG records.");
        }

        var nativeRecordBytes = NativePlayerStateLinkBytes(frame);
        var blockingBytes = Math.Max(0, frame.Length - nativeRecordBytes);
        return BuildClassifiedFrame(
            frame,
            "BuildPlayerStateLinkBody -> Ps3SourcePlayerStateLinkRecord.BuildBatch",
            blockingBytes == 0
                ? "native-records-no-prefix-debt-detected"
                : "native-records-with-generated-prefix-or-padding",
            "queued-peer-submessage-boundaries",
            blockingBytes,
            (blockingBytes * 2) + Math.Min(nativeRecordBytes, 96),
            blockingBytes == 0
                ? "The visible frame body can be fully accounted for as native PNG records; keep it on Ps3SourcePlayerStateLinkRecord."
                : "The PNG tail records are native, but the prefix/trailing deterministic bytes still need the TF.elf queued-prefix/control writer.");
    }

    private static Tf2Ps3SourceLoadingFrameFrame BuildClassifiedFrame(
        Tf2Ps3SourceLoadingFrameRawFrame frame,
        string currentBuilder,
        string risk,
        string replacementTarget,
        int blockingBytes,
        int priorityScore,
        string recommendedNextAction)
    {
        return new Tf2Ps3SourceLoadingFrameFrame(
            frame.Collection,
            frame.CollectionSortKey,
            frame.Variant,
            frame.Phase,
            frame.StageIndex,
            frame.FrameIndex,
            frame.SeedStageIndex,
            frame.SequenceAdvance,
            frame.Kind,
            frame.Length,
            frame.PrefixLength,
            frame.MaxRecords,
            frame.SuppressAfter,
            currentBuilder,
            risk,
            replacementTarget,
            blockingBytes,
            priorityScore,
            frame.LineNumber,
            recommendedNextAction);
    }

    private static int NativePlayerStateLinkBytes(Tf2Ps3SourceLoadingFrameRawFrame frame)
    {
        var offset = Math.Min(frame.PrefixLength.GetValueOrDefault(), frame.Length);
        var availableRecordCount = Math.Max(0, (frame.Length - offset) / 12);
        var recordCount = Math.Min(frame.MaxRecords.GetValueOrDefault(int.MaxValue), availableRecordCount);
        return recordCount * 12;
    }

    private static bool ShouldBuildPlayerStateLinkAsHighEntropy(int initialSetupVariant, int length, int seedStageIndex)
    {
        return initialSetupVariant switch
        {
            2 => length == 210 || length is >= 256 and < 998,
            3 => length is >= 256 and < 998 || (length == 220 && seedStageIndex is 19 or 28),
            4 => length == 220 || length is >= 256 and < 998,
            _ => false
        };
    }

    private static Tf2Ps3SourceLoadingFrameCollection[] ParseFrameCollections(string source, string[] lines)
    {
        var collections = new List<Tf2Ps3SourceLoadingFrameCollection>();
        collections.Add(ParseExplicitContinuationCollection(lines));
        collections.AddRange(ParseEncodedContinuationCollections(source));
        collections.AddRange(ParseEncodedLineCollections(source));
        collections.AddRange(ParseDefaultSwitchCollections(source));
        return collections
            .Where(static collection => collection.Frames.Length > 0)
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingFrameCollection ParseExplicitContinuationCollection(string[] lines)
    {
        var frames = new List<Tf2Ps3SourceLoadingFrameRawFrame>();
        var inCollection = false;
        var inStage = false;
        var stageIndex = -1;
        var frameIndex = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (!inCollection)
            {
                if (line.Contains("private static readonly LoadingContinuationFrame[][] LoadingContinuationStages", StringComparison.Ordinal))
                {
                    inCollection = true;
                }

                continue;
            }

            if (line.Contains("private static readonly LoadingContinuationFrame[] Variant2LoadingBurstFrames", StringComparison.Ordinal))
            {
                break;
            }

            var trimmed = line.Trim();
            if (!inStage && trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                inStage = true;
                stageIndex++;
                frameIndex = 0;
            }

            if (inStage)
            {
                foreach (Match match in FrameConstructorRegex().Matches(line))
                {
                    frames.Add(CreateRawFrame(
                        "LoadingContinuationStages",
                        1,
                        "loading-continuation",
                        stageIndex,
                        frameIndex++,
                        seedStageIndex: stageIndex + 2,
                        lineNumber: lineIndex + 1,
                        match));
                }
            }

            if (inStage && trimmed.StartsWith("]", StringComparison.Ordinal))
            {
                inStage = false;
            }
        }

        return new Tf2Ps3SourceLoadingFrameCollection("LoadingContinuationStages", 1, "loading-continuation", CollectionSortKey("LoadingContinuationStages"), frames.ToArray());
    }

    private static IEnumerable<Tf2Ps3SourceLoadingFrameCollection> ParseEncodedContinuationCollections(string source)
    {
        foreach (Match match in EncodedContinuationCollectionRegex().Matches(source))
        {
            var name = match.Groups["name"].Value;
            var encoded = match.Groups["encoded"].Value;
            var startLine = LineNumberAt(source, match.Groups["encoded"].Index);
            var variant = VariantFromName(name);
            var phase = name == "SteadyStateDeltaStages"
                ? "steady-state-delta"
                : "loading-continuation";
            var seedBase = name == "SteadyStateDeltaStages"
                ? 200
                : 2;
            var frames = ParseEncodedContinuationFrames(
                name,
                variant,
                phase,
                seedBase,
                encoded,
                startLine);
            yield return new Tf2Ps3SourceLoadingFrameCollection(name, variant, phase, CollectionSortKey(name), frames);
        }
    }

    private static IEnumerable<Tf2Ps3SourceLoadingFrameCollection> ParseEncodedLineCollections(string source)
    {
        foreach (Match match in EncodedLineCollectionRegex().Matches(source))
        {
            var name = match.Groups["name"].Value;
            var encoded = match.Groups["encoded"].Value;
            var variant = VariantFromName(name);
            var phase = name.Contains("PostBurst", StringComparison.Ordinal)
                ? "loading-post-burst"
                : "loading-burst";
            var seedStageIndex = phase == "loading-post-burst" ? 1 : 0;
            var frames = ParseEncodedLineFrames(
                name,
                variant,
                phase,
                stageIndex: 0,
                seedStageIndex,
                encoded,
                LineNumberAt(source, match.Groups["encoded"].Index));
            yield return new Tf2Ps3SourceLoadingFrameCollection(name, variant, phase, CollectionSortKey(name), frames);
        }
    }

    private static IEnumerable<Tf2Ps3SourceLoadingFrameCollection> ParseDefaultSwitchCollections(string source)
    {
        var burstBody = ExtractDefaultSwitchFrameBody(
            source,
            "private static LoadingContinuationFrame[] LoadingBurstFramesFor",
            "private static LoadingContinuationFrame[] LoadingPostBurstFramesFor");
        if (burstBody is not null)
        {
            var frames = ParseConstructorFrames(
                "DefaultLoadingBurstFrames",
                1,
                "loading-burst",
                stageIndex: 0,
                seedStageIndex: 0,
                burstBody.Value.Body,
                burstBody.Value.LineNumber);
            yield return new Tf2Ps3SourceLoadingFrameCollection("DefaultLoadingBurstFrames", 1, "loading-burst", CollectionSortKey("DefaultLoadingBurstFrames"), frames);
        }

        var postBurstBody = ExtractDefaultSwitchFrameBody(
            source,
            "private static LoadingContinuationFrame[] LoadingPostBurstFramesFor",
            "private static int LoadingBurstSuppressAfter");
        if (postBurstBody is not null)
        {
            var frames = ParseConstructorFrames(
                "DefaultLoadingPostBurstFrames",
                1,
                "loading-post-burst",
                stageIndex: 0,
                seedStageIndex: 1,
                postBurstBody.Value.Body,
                postBurstBody.Value.LineNumber);
            yield return new Tf2Ps3SourceLoadingFrameCollection("DefaultLoadingPostBurstFrames", 1, "loading-post-burst", CollectionSortKey("DefaultLoadingPostBurstFrames"), frames);
        }
    }

    private static Tf2Ps3SourceLoadingFrameRawFrame[] ParseEncodedContinuationFrames(
        string collection,
        int variant,
        string phase,
        int seedBase,
        string encoded,
        int startLine)
    {
        var frames = new List<Tf2Ps3SourceLoadingFrameRawFrame>();
        var stageIndex = 0;
        var encodedLines = encoded.Split(['\r', '\n'], StringSplitOptions.None);
        for (var rawLineIndex = 0; rawLineIndex < encodedLines.Length; rawLineIndex++)
        {
            var line = encodedLines[rawLineIndex].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            frames.AddRange(ParseEncodedLineFrames(
                collection,
                variant,
                phase,
                stageIndex,
                seedBase + stageIndex,
                line,
                startLine + rawLineIndex));
            stageIndex++;
        }

        return frames.ToArray();
    }

    private static Tf2Ps3SourceLoadingFrameRawFrame[] ParseEncodedLineFrames(
        string collection,
        int variant,
        string phase,
        int stageIndex,
        int seedStageIndex,
        string encoded,
        int lineNumber)
    {
        var frames = new List<Tf2Ps3SourceLoadingFrameRawFrame>();
        var frameIndex = 0;
        foreach (var token in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kindCode = token[^1];
            var length = int.Parse(token.AsSpan(0, token.Length - 1));
            var (kind, prefix, maxRecords) = kindCode switch
            {
                'L' => PlayerStateLinkFrameShape(length),
                'M' => ("MixedBinary", (int?)null, (int?)null),
                _ => ("HighEntropy", (int?)null, (int?)null)
            };
            frames.Add(new Tf2Ps3SourceLoadingFrameRawFrame(
                collection,
                CollectionSortKey(collection),
                variant,
                phase,
                stageIndex,
                frameIndex++,
                seedStageIndex,
                LoadingContinuationSequenceAdvance(length),
                kind,
                length,
                prefix,
                maxRecords,
                null,
                lineNumber));
        }

        return frames.ToArray();
    }

    private static Tf2Ps3SourceLoadingFrameRawFrame[] ParseConstructorFrames(
        string collection,
        int variant,
        string phase,
        int stageIndex,
        int seedStageIndex,
        string body,
        int lineNumber)
    {
        return FrameConstructorRegex()
            .Matches(body)
            .Cast<Match>()
            .Select((match, frameIndex) => CreateRawFrame(
                collection,
                variant,
                phase,
                stageIndex,
                frameIndex,
                seedStageIndex,
                lineNumber,
                match))
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingFrameRawFrame CreateRawFrame(
        string collection,
        int variant,
        string phase,
        int stageIndex,
        int frameIndex,
        int seedStageIndex,
        int lineNumber,
        Match match)
    {
        var kind = match.Groups["kind"].Value;
        var length = int.Parse(match.Groups["length"].Value);
        int? prefixLength = match.Groups["prefix"].Success ? int.Parse(match.Groups["prefix"].Value) : null;
        int? maxRecords = match.Groups["max"].Success ? int.Parse(match.Groups["max"].Value) : null;
        int? suppressAfter = match.Groups["suppress"].Success ? int.Parse(match.Groups["suppress"].Value) : null;
        return new Tf2Ps3SourceLoadingFrameRawFrame(
            collection,
            CollectionSortKey(collection),
            variant,
            phase,
            stageIndex,
            frameIndex,
            seedStageIndex,
            LoadingContinuationSequenceAdvance(length),
            kind,
            length,
            prefixLength,
            maxRecords,
            suppressAfter,
            lineNumber);
    }

    private static (string Body, int LineNumber)? ExtractDefaultSwitchFrameBody(string source, string startNeedle, string endNeedle)
    {
        var start = source.IndexOf(startNeedle, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var end = source.IndexOf(endNeedle, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var block = source[start..end];
        var match = DefaultSwitchFramesRegex().Match(block);
        if (!match.Success)
        {
            return null;
        }

        return (match.Groups["body"].Value, LineNumberAt(source, start + match.Groups["body"].Index));
    }

    private static (string Kind, int? PrefixLength, int? MaxRecords) PlayerStateLinkFrameShape(int length)
    {
        var (prefixLength, maxRecords) = length switch
        {
            >= 1000 => (28, 9),
            >= 600 => (24, 9),
            >= 250 => (18, 9),
            >= 160 => (14, 8),
            >= 120 => (14, 7),
            >= 90 => (13, 6),
            >= 70 => (10, 5),
            >= 56 => (8, 4),
            >= 49 => (25, 2),
            >= 35 => (11, 2),
            _ => (4, 2)
        };

        return ("PlayerStateLink", prefixLength, maxRecords);
    }

    private static int LoadingContinuationSequenceAdvance(int length)
    {
        if (length >= 1000)
        {
            return 6;
        }

        if (length >= 512)
        {
            return 4;
        }

        return 3;
    }

    private static Tf2Ps3SourceLoadingFrameGeneratorCallSite[] ExtractGeneratorCallSites(string[] lines)
    {
        var callSites = new List<Tf2Ps3SourceLoadingFrameGeneratorCallSite>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("private static", StringComparison.Ordinal))
            {
                continue;
            }

            var function = line switch
            {
                var value when value.Contains("BuildHighEntropyBinaryBody(", StringComparison.Ordinal) => "BuildHighEntropyBinaryBody",
                var value when value.Contains("BuildMixedBinaryBody(", StringComparison.Ordinal) => "BuildMixedBinaryBody",
                var value when value.Contains("PadNativeWrappedPayload(", StringComparison.Ordinal) => "PadNativeWrappedPayload",
                var value when value.Contains("FillHighEntropyDeterministic(", StringComparison.Ordinal) => "FillHighEntropyDeterministic",
                _ => ""
            };

            if (function.Length == 0)
            {
                continue;
            }

            callSites.Add(new Tf2Ps3SourceLoadingFrameGeneratorCallSite(
                index + 1,
                function,
                line,
                function == "PadNativeWrappedPayload"
                    ? "snapshot/entity-delta padding risk"
                    : "production deterministic filler risk"));
        }

        return callSites.ToArray();
    }

    private static int VariantFromName(string name)
    {
        var match = VariantRegex().Match(name);
        return match.Success ? int.Parse(match.Groups["variant"].Value) : 1;
    }

    private static int CollectionSortKey(string collection)
    {
        return collection switch
        {
            "DefaultLoadingBurstFrames" => 0,
            "Variant2LoadingBurstFrames" => 1,
            "Variant3LoadingBurstFrames" => 2,
            "Variant4LoadingBurstFrames" => 3,
            "DefaultLoadingPostBurstFrames" => 10,
            "Variant2LoadingPostBurstFrames" => 11,
            "Variant3LoadingPostBurstFrames" => 12,
            "Variant4LoadingPostBurstFrames" => 13,
            "LoadingContinuationStages" => 20,
            "Variant2LoadingContinuationStages" => 21,
            "Variant3LoadingContinuationStages" => 22,
            "Variant4LoadingContinuationStages" => 23,
            "SteadyStateDeltaStages" => 40,
            _ => 100
        };
    }

    private static string[] SplitLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static int LineNumberAt(string source, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static int CountArray(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }

    [GeneratedRegex("""new\s*\(\s*LoadingContinuationFrameKind\.(?<kind>\w+)\s*,\s*(?<length>\d+)(?:\s*,\s*(?<prefix>\d+)\s*,\s*(?<max>\d+))?(?:\s*,\s*SuppressAfter:\s*(?<suppress>\d+))?\s*\)""")]
    private static partial Regex FrameConstructorRegex();

    [GeneratedRegex("private\\s+static\\s+readonly\\s+LoadingContinuationFrame\\[\\]\\[\\]\\s+(?<name>\\w+)\\s*=\\s*ParseLoadingContinuationStages\\(\\s*\"\"\"\\s*(?<encoded>.*?)\\s*\"\"\"\\s*\\);", RegexOptions.Singleline)]
    private static partial Regex EncodedContinuationCollectionRegex();

    [GeneratedRegex("""private\s+static\s+readonly\s+LoadingContinuationFrame\[\]\s+(?<name>\w+)\s*=\s*ParseLoadingFrameLine\("(?<encoded>[^"]*)"\);""")]
    private static partial Regex EncodedLineCollectionRegex();

    [GeneratedRegex("""_\s*=>\s*\[(?<body>[^\]]+)\]""", RegexOptions.Singleline)]
    private static partial Regex DefaultSwitchFramesRegex();

    [GeneratedRegex("""Variant(?<variant>\d+)""")]
    private static partial Regex VariantRegex();
}

public sealed record Tf2Ps3SourceLoadingFrameDebtReport(
    string Status,
    string Note,
    Tf2Ps3SourceLoadingFrameDebtInputs Inputs,
    Tf2Ps3SourceLoadingFrameDebtSummary Summary,
    Tf2Ps3SourceLoadingFrameCollectionSummary[] Collections,
    Tf2Ps3SourceLoadingFrameFrame[] Frames,
    Tf2Ps3SourceLoadingFrameFrame[] PriorityFrames,
    Tf2Ps3SourceLoadingFrameGeneratorCallSite[] DirectGeneratorCallSites,
    string[] Conclusions);

public sealed record Tf2Ps3SourceLoadingFrameDebtInputs(
    string ResponderSource,
    string NativeTemplateDebt,
    string QueuedPrefixContract,
    string TunnelGhidraMap);

public sealed record Tf2Ps3SourceLoadingFrameDebtSummary(
    int CollectionCount,
    int FrameCount,
    int EarlyLoadingFrameCount,
    int SteadyFrameCount,
    int HighEntropyFrameCount,
    int MixedBinaryFrameCount,
    int PlayerStateLinkFrameCount,
    int ProductionFakeFrameCount,
    int NativeRecordWithGeneratedPrefixFrameCount,
    int SteadyNativeSnapshotPaddingRiskCount,
    int BlockingByteCount,
    int DirectGeneratorCallSiteCount,
    int StaticHexTemplateCount,
    int NativeTemplateDebtHighEntropyLoadingFrameCount,
    int NativeTemplateDebtMixedBinaryLoadingFrameCount,
    int GeneratedQueuedPrefixDebtCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount,
    int QueuedOpaquePrefixBytes,
    int EaTunnelReferenceCount,
    int EaTunnelNeighborhoodReferenceCount,
    int EaTunnelReachedFunctionCount);

public sealed record Tf2Ps3SourceLoadingFrameCollectionSummary(
    string Collection,
    int Variant,
    string Phase,
    int StageCount,
    int FrameCount,
    int HighEntropyFrameCount,
    int MixedBinaryFrameCount,
    int PlayerStateLinkFrameCount,
    int ProductionFakeFrameCount,
    int NativeRecordWithGeneratedPrefixFrameCount,
    int SteadyNativeSnapshotPaddingRiskCount,
    int BlockingByteCount);

public sealed record Tf2Ps3SourceLoadingFrameCollection(
    string Name,
    int Variant,
    string Phase,
    int SortKey,
    Tf2Ps3SourceLoadingFrameRawFrame[] Frames);

public sealed record Tf2Ps3SourceLoadingFrameRawFrame(
    string Collection,
    int CollectionSortKey,
    int Variant,
    string Phase,
    int StageIndex,
    int FrameIndex,
    int SeedStageIndex,
    int SequenceAdvance,
    string Kind,
    int Length,
    int? PrefixLength,
    int? MaxRecords,
    int? SuppressAfter,
    int LineNumber);

public sealed record Tf2Ps3SourceLoadingFrameFrame(
    string Collection,
    int CollectionSortKey,
    int Variant,
    string Phase,
    int StageIndex,
    int FrameIndex,
    int SeedStageIndex,
    int SequenceAdvance,
    string Kind,
    int Length,
    int? PrefixLength,
    int? MaxRecords,
    int? SuppressAfter,
    string CurrentBuilder,
    string Risk,
    string ReplacementTarget,
    int BlockingByteCount,
    int PriorityScore,
    int LineNumber,
    string RecommendedNextAction);

public sealed record Tf2Ps3SourceLoadingFrameGeneratorCallSite(
    int LineNumber,
    string Function,
    string SourceLine,
    string Risk);
