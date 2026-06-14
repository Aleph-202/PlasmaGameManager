using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceNativeDebtPriorityReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceNativeDebtPriorityReport> ReduceAsync(
        string nativeTemplateDebtPath,
        string templatePatchLayoutPath,
        string queuedPrefixContractPath,
        string outputPath)
    {
        using var nativeTemplateDebt = JsonDocument.Parse(await File.ReadAllTextAsync(nativeTemplateDebtPath));
        using var templatePatchLayout = JsonDocument.Parse(await File.ReadAllTextAsync(templatePatchLayoutPath));
        using var queuedPrefixContract = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));

        var templateTargets = templatePatchLayout.RootElement
            .GetProperty("TemplateLayouts")
            .EnumerateArray()
            .Select(ReadTemplateTarget)
            .OrderByDescending(static target => target.PriorityScore)
            .ThenByDescending(static target => target.BlockingByteCount)
            .ThenBy(static target => target.TemplateName, StringComparer.Ordinal)
            .ToArray();

        var debtSummary = nativeTemplateDebt.RootElement.GetProperty("Summary");
        var prefixSummary = queuedPrefixContract.RootElement.GetProperty("Summary");
        var highEntropyCount = debtSummary.GetProperty("HighEntropyLoadingFrameCount").GetInt32();
        var mixedBinaryCount = debtSummary.GetProperty("MixedBinaryLoadingFrameCount").GetInt32();
        var generatedFillerTargets = BuildGeneratedFillerTargets(highEntropyCount, mixedBinaryCount).ToArray();
        var generatedPrefixTargets = ReadGeneratedQueuedPrefixTargets(queuedPrefixContract.RootElement).ToArray();
        var prioritizedTargets = templateTargets
            .Concat(generatedFillerTargets)
            .Concat(generatedPrefixTargets)
            .OrderByDescending(static target => target.PriorityScore)
            .ThenByDescending(static target => target.BlockingByteCount)
            .ThenBy(static target => target.TemplateName, StringComparer.Ordinal)
            .ToArray();

        var report = new Tf2Ps3SourceNativeDebtPriorityReport(
            "tf2ps3-source-native-debt-priority",
            "Ranks current PS3 Source responder fake-byte/template debt by production call path and native replacement boundary. This is an implementation worklist, not native completion.",
            new Tf2Ps3SourceNativeDebtPriorityInputs(
                nativeTemplateDebtPath,
                templatePatchLayoutPath,
                queuedPrefixContractPath),
            new Tf2Ps3SourceNativeDebtPrioritySummary(
                debtSummary.GetProperty("StaticHexTemplateCount").GetInt32(),
                debtSummary.GetProperty("StaticHexTemplateBytes").GetInt32(),
                highEntropyCount,
                mixedBinaryCount,
                templateTargets.Length,
                templateTargets.Count(static target => target.HasDirectSendSite),
                templateTargets.Count(static target => target.StaticPrefixByteCount > 0),
                templateTargets.Sum(static target => target.TailRecordCount),
                prefixSummary.GetProperty("StaticTemplatePrefixDebtBytes").GetInt32(),
                prefixSummary.GetProperty("GeneratedQueuedPrefixDebtBytes").GetInt32(),
                prioritizedTargets.Length),
            prioritizedTargets,
            [
                "Remove targets in priority order only when the replacement bytes are built from TF.elf/server.dll field writers, not hidden in new hex literals.",
                "Object-stream bootstrap targets must route through the recovered 00a55e60/Ps3SourceObjectStream envelope and Source netmessage builders.",
                "Queued-peer submessage targets must route through 008bc978 -> 008b9f70/008bb058/008bc490 semantics and may append native COc/DSC/PNG records only after a named prefix/control writer exists.",
                "Generated filler targets remain fake until BuildHighEntropyBinaryBody, FillHighEntropyDeterministic, BuildMixedBinaryBody, and deterministic padding are no longer used for production Source payload fields.",
                "EA Tunnel remains a closed lead for this worklist unless future server.dll Ghidra evidence finds executable xrefs."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceNativeDebtPriorityTarget ReadTemplateTarget(JsonElement layout)
    {
        var name = layout.GetProperty("Name").GetString() ?? "";
        var replacementTarget = layout.GetProperty("ReplacementTarget").GetString() ?? "";
        var byteLength = layout.GetProperty("ByteLength").GetInt32();
        var directSendSites = layout.GetProperty("DirectSendSites").EnumerateArray().ToArray();
        var tailPatches = layout.GetProperty("TailPatches").EnumerateArray().ToArray();
        var embeddedRecords = layout.GetProperty("EmbeddedStateLinkRecords").EnumerateArray().ToArray();
        var frozenRoutes = layout.GetProperty("FrozenStatePrefixRoutes").EnumerateArray().ToArray();
        var staticPrefixBytes = StaticPrefixByteCount(layout, byteLength);
        var buildMethods = StringArray(layout, "BuildMethods");
        var buildKinds = StringArray(layout, "BuildKinds");
        var directMethods = directSendSites
            .Select(static site => site.GetProperty("Method").GetString() ?? "")
            .Where(static method => method.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static method => method, StringComparer.Ordinal)
            .ToArray();
        var sequenceAdvances = directSendSites
            .Where(static site => site.TryGetProperty("SequenceAdvance", out _))
            .Select(static site => site.GetProperty("SequenceAdvance").GetInt32())
            .Distinct()
            .Order()
            .ToArray();

        var tailRecordCount = tailPatches.Sum(static patch => patch.GetProperty("TailRecordCount").GetInt32());
        var evidence = new List<string>();
        if (directSendSites.Length > 0)
        {
            evidence.Add($"direct AddPacket send site(s): {directSendSites.Length}");
        }

        if (staticPrefixBytes > 0)
        {
            evidence.Add($"static prefix/control bytes before decoded records: {staticPrefixBytes}");
        }

        if (tailRecordCount > 0)
        {
            evidence.Add($"dynamic native PNG tail records: {tailRecordCount}");
        }

        if (embeddedRecords.Length > 0)
        {
            evidence.Add($"embedded native PNG records already visible: {embeddedRecords.Length}");
        }

        if (frozenRoutes.Length > 0)
        {
            evidence.Add($"retail FrozenState prefix route(s): {frozenRoutes.Length}");
        }

        var score = byteLength
            + (directSendSites.Length * 200)
            + staticPrefixBytes.GetValueOrDefault()
            + (replacementTarget == "native-object-stream-bootstrap" ? 120 : 0)
            + (replacementTarget == "queued-peer-submessage-boundaries" ? 90 : 0)
            + (tailRecordCount * 20)
            + (embeddedRecords.Length * 10);

        return new Tf2Ps3SourceNativeDebtPriorityTarget(
            name,
            "static-template",
            replacementTarget,
            score,
            byteLength,
            staticPrefixBytes,
            tailRecordCount,
            embeddedRecords.Length,
            directSendSites.Length > 0,
            buildMethods,
            buildKinds,
            directMethods,
            sequenceAdvances,
            evidence.ToArray(),
            RecommendationFor(replacementTarget, directSendSites.Length > 0, staticPrefixBytes, tailRecordCount));
    }

    private static IEnumerable<Tf2Ps3SourceNativeDebtPriorityTarget> BuildGeneratedFillerTargets(int highEntropyCount, int mixedBinaryCount)
    {
        if (highEntropyCount > 0)
        {
            yield return new Tf2Ps3SourceNativeDebtPriorityTarget(
                "LoadingContinuationFrameKind.HighEntropy",
                "generated-filler-recipe",
                "native-snapshot-and-entity-delta-route",
                highEntropyCount * 20,
                null,
                null,
                0,
                0,
                false,
                ["AddLoadingFrames", "TryBuildNativeSnapshotBody", "BuildHighEntropyBinaryBody", "FillHighEntropyDeterministic"],
                ["native-snapshot-attempt-with-deterministic-fallback"],
                [],
                [],
                [$"high-entropy generated loading frame recipes: {highEntropyCount}"],
                "High-entropy loading frames now attempt native Source snapshot/entity-delta bodies first; prove coverage and remove the deterministic fallback before marking this route native.");
        }

        if (mixedBinaryCount > 0)
        {
            yield return new Tf2Ps3SourceNativeDebtPriorityTarget(
                "LoadingContinuationFrameKind.MixedBinary",
                "generated-filler-recipe",
                "queued-peer-submessage-boundaries",
                mixedBinaryCount * 12,
                null,
                null,
                0,
                0,
                false,
                ["AddLoadingFrames", "BuildMixedBinaryBody"],
                ["deterministic-loading-recipe"],
                [],
                [],
                [$"mixed-binary generated loading frame recipes: {mixedBinaryCount}"],
                "Replace generated mixed-binary loading frames with named queued-peer control/submessage fields.");
        }
    }

    private static IEnumerable<Tf2Ps3SourceNativeDebtPriorityTarget> ReadGeneratedQueuedPrefixTargets(JsonElement root)
    {
        if (!root.TryGetProperty("GeneratedQueuedPrefixDebt", out var debts) || debts.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var debt in debts.EnumerateArray())
        {
            var family = debt.GetProperty("Family").GetString() ?? "generated-queued-prefix";
            var prefixBytes = debt.GetProperty("PrefixByteLength").GetInt32();
            var recordCount = debt.GetProperty("RecordCount").GetInt32();
            yield return new Tf2Ps3SourceNativeDebtPriorityTarget(
                family,
                "generated-queued-prefix-body",
                debt.GetProperty("ReplacementTarget").GetString() ?? "queued-peer-submessage-boundaries",
                prefixBytes + (recordCount * 20) + 160,
                debt.GetProperty("BodyLength").GetInt32(),
                prefixBytes,
                recordCount,
                recordCount,
                true,
                [debt.GetProperty("Method").GetString() ?? "unknown"],
                ["generated-queued-prefix", "native-state-link-record-tail"],
                [],
                [],
                [
                    $"generated queued prefix bytes before native PNG records: {prefixBytes}",
                    $"native PNG tail records: {recordCount}"
                ],
                "Replace the generated queued-prefix bytes with the recovered TF.elf peer-channel prefix/control writer, then keep the native PNG record tail.");
        }
    }

    private static int? StaticPrefixByteCount(JsonElement layout, int byteLength)
    {
        var tailPatches = layout.GetProperty("TailPatches").EnumerateArray().ToArray();
        if (tailPatches.Length > 0)
        {
            return byteLength - tailPatches.Sum(static patch => patch.GetProperty("TailByteLength").GetInt32());
        }

        var embeddedRecords = layout.GetProperty("EmbeddedStateLinkRecords").EnumerateArray().ToArray();
        if (embeddedRecords.Length > 0)
        {
            return embeddedRecords.Min(static record => record.GetProperty("Offset").GetInt32());
        }

        var frozenRoutes = layout.GetProperty("FrozenStatePrefixRoutes").EnumerateArray().ToArray();
        if (frozenRoutes.Length > 0)
        {
            return frozenRoutes.Sum(static route => route.GetProperty("PrefixLength").GetInt32());
        }

        return null;
    }

    private static string RecommendationFor(string replacementTarget, bool hasDirectSendSite, int? staticPrefixBytes, int tailRecordCount)
    {
        var prefix = staticPrefixBytes.GetValueOrDefault() > 0
            ? "Recover the prefix/control writer first, then append the already decoded native records."
            : "Recover the full body writer; no decoded record tail is currently enough to split the payload.";
        var route = replacementTarget switch
        {
            "native-object-stream-bootstrap" => "Use the Source netmessage builders and Ps3SourceObjectStream envelope, then move the production send path away from the static body.",
            "queued-peer-submessage-boundaries" => "Recover the queued-peer submessage/control fields around COc/DSC/PNG records and send through the native peer-channel path.",
            "native-snapshot-and-entity-delta-route" => "Replace with named snapshot/entity-delta fields derived from TF.elf/server.dll sendtable semantics.",
            _ => "Recover a named native writer before deleting the static bytes."
        };
        var send = hasDirectSendSite
            ? " This is directly sent in production and should be prioritized over diagnostic-only paths."
            : "";
        var tail = tailRecordCount > 0
            ? $" The {tailRecordCount} decoded PlayerStateLink tail record(s) are native but do not make the prefix native."
            : "";
        return $"{prefix} {route}{send}{tail}";
    }

    private static string[] StringArray(JsonElement parent, string propertyName)
    {
        return parent.GetProperty(propertyName)
            .EnumerateArray()
            .Select(static item => item.GetString() ?? "")
            .Where(static item => item.Length > 0)
            .ToArray();
    }
}

public sealed record Tf2Ps3SourceNativeDebtPriorityReport(
    string Status,
    string Note,
    Tf2Ps3SourceNativeDebtPriorityInputs Inputs,
    Tf2Ps3SourceNativeDebtPrioritySummary Summary,
    Tf2Ps3SourceNativeDebtPriorityTarget[] PrioritizedTargets,
    string[] AcceptanceCriteria);

public sealed record Tf2Ps3SourceNativeDebtPriorityInputs(
    string NativeTemplateDebt,
    string TemplatePatchLayout,
    string QueuedPrefixContract);

public sealed record Tf2Ps3SourceNativeDebtPrioritySummary(
    int StaticHexTemplateCount,
    int StaticHexTemplateBytes,
    int HighEntropyLoadingFrameCount,
    int MixedBinaryLoadingFrameCount,
    int StaticTemplateTargetCount,
    int DirectStaticSendTargetCount,
    int StaticPrefixTargetCount,
    int NativeTailRecordCount,
    int StaticTemplatePrefixDebtBytes,
    int GeneratedQueuedPrefixDebtBytes,
    int PrioritizedTargetCount);

public sealed record Tf2Ps3SourceNativeDebtPriorityTarget(
    string TemplateName,
    string Kind,
    string ReplacementTarget,
    int PriorityScore,
    int? BlockingByteCount,
    int? StaticPrefixByteCount,
    int TailRecordCount,
    int EmbeddedStateLinkRecordCount,
    bool HasDirectSendSite,
    string[] BuildMethods,
    string[] BuildKinds,
    string[] DirectSendMethods,
    int[] DirectSequenceAdvances,
    string[] Evidence,
    string RecommendedNextAction);
