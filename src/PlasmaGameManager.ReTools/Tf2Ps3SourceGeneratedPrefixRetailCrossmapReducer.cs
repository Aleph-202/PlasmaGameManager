using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceGeneratedPrefixRetailCrossmapReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceGeneratedPrefixRetailCrossmapReport> ReduceAsync(
        string queuedPrefixContractPath,
        string semanticTraceDirectory,
        string queuedOpaqueReportPath,
        string outputPath)
    {
        using var queuedPrefixContract = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));
        using var queuedOpaque = JsonDocument.Parse(await File.ReadAllTextAsync(queuedOpaqueReportPath));

        var generatedDebt = ReadGeneratedDebt(queuedPrefixContract.RootElement).ToArray();
        var tracePackets = ReadSemanticTracePackets(semanticTraceDirectory).ToArray();
        var queuedRecordPackets = tracePackets
            .Where(static packet => packet.Direction == "ServerToClient"
                && packet.PrefixByteLength > 0
                && packet.RecordCount > 0)
            .ToArray();

        var targets = generatedDebt
            .Select(debt => BuildTarget(debt, queuedRecordPackets))
            .ToArray();

        var report = new Tf2Ps3SourceGeneratedPrefixRetailCrossmapReport(
            "tf2ps3-source-generated-prefix-retail-crossmap",
            "Crossmaps generated queued-prefix responder debt against retail semantic traces. Exact shape evidence pins native body families; it is not replay permission.",
            new Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs(
                queuedPrefixContractPath,
                semanticTraceDirectory,
                queuedOpaqueReportPath),
            new Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary(
                generatedDebt.Length,
                generatedDebt.Sum(static debt => debt.PrefixByteLength),
                generatedDebt.Sum(static debt => debt.RecordCount),
                Directory.Exists(semanticTraceDirectory)
                    ? Directory.EnumerateFiles(semanticTraceDirectory, "*.semantic-trace.json").Count()
                    : 0,
                tracePackets.Length,
                queuedRecordPackets.Length,
                ReadInt(queuedOpaque.RootElement.GetProperty("Summary"), "QueuedChunkPacketCount"),
                ReadInt(queuedOpaque.RootElement.GetProperty("Summary"), "ServerQueuedChunkPacketCount"),
                targets.Count(static target => target.ExactRetailSampleCount > 0),
                targets.Count(static target => target.ExactRetailSampleCount == 0)),
            targets,
            BuildNativeImplementationRules(),
            BuildConclusions(targets));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget BuildTarget(
        Tf2Ps3SourceGeneratedPrefixRetailDebt debt,
        Tf2Ps3SourceGeneratedPrefixRetailSample[] queuedRecordPackets)
    {
        var exact = queuedRecordPackets
            .Where(packet => packet.PrefixByteLength == debt.PrefixByteLength && packet.RecordCount == debt.RecordCount)
            .OrderBy(static packet => packet.SourceStep)
            .ThenBy(static packet => packet.PacketIndex)
            .Take(12)
            .ToArray();

        var nearby = queuedRecordPackets
            .Where(packet => packet.PrefixByteLength == debt.PrefixByteLength
                || packet.RecordCount == debt.RecordCount
                || Math.Abs(packet.PrefixByteLength - debt.PrefixByteLength) <= 8)
            .GroupBy(static packet => (packet.PrefixByteLength, packet.RecordCount, packet.Family))
            .Select(group => new Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily(
                $"server-queued-opaque-prefix-{group.Key.PrefixByteLength}-records-{group.Key.RecordCount}",
                group.Key.Family,
                group.Key.PrefixByteLength,
                group.Key.RecordCount,
                Math.Abs(group.Key.PrefixByteLength - debt.PrefixByteLength),
                Math.Abs(group.Key.RecordCount - debt.RecordCount),
                group.Count(),
                group.OrderBy(static packet => packet.SourceStep).First().SourceTraceFile,
                group.OrderBy(static packet => packet.SourceStep).First().SourceStep))
            .OrderBy(static family => family.PrefixDelta)
            .ThenBy(static family => family.RecordCountDelta)
            .ThenByDescending(static family => family.SampleCount)
            .Take(12)
            .ToArray();

        var conclusion = exact.Length > 0
            ? "Retail semantic traces contain this exact server queued-prefix shape. Replace generated filler with native field writers for the same shape."
            : "No exact retail semantic-trace sample currently matches this generated shape. Treat it as open native debt and use nearby families to locate the correct TF.elf writer path.";

        var nextAction = debt.Family switch
        {
            "late-large-command-prep-segment-a" =>
                "Implement the prefix through the TF.elf queued peer-channel writer for the retail Prefix578/Records2 player-state-link family.",
            "late-large-command-prep-segment-b" =>
                "Implement the prefix through the TF.elf queued peer-channel writer for the retail Prefix570/Records3 player-state-link family.",
            "late-large-command-followup" =>
                "Implement the prefix through the TF.elf queued peer-channel writer for the retail Prefix848/Records1 player-state-link family.",
            "late-large-command-prep" =>
                "Recheck TF.elf/server.dll builder callsites before freezing this body shape; current semantic traces show nearby Prefix576/Records1 and Prefix570/Records3 families rather than a confirmed Prefix576/Records6 server packet.",
            _ =>
                "Map this generated prefix to a named TF.elf/server.dll writer before removing it from native debt."
        };

        return new Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget(
            debt.Family,
            debt.Method,
            debt.BodyLength,
            debt.PrefixByteLength,
            debt.RecordCount,
            debt.TailByteLength,
            debt.ObjectIdExpressions,
            debt.LinkedObjectIdExpression,
            $"server-queued-opaque-prefix-{debt.PrefixByteLength}-records-{debt.RecordCount}",
            exact.Length,
            nearby.Length,
            exact,
            nearby,
            conclusion,
            nextAction);
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixRetailDebt> ReadGeneratedDebt(JsonElement root)
    {
        if (!root.TryGetProperty("GeneratedQueuedPrefixDebt", out var debts) || debts.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var debt in debts.EnumerateArray())
        {
            yield return new Tf2Ps3SourceGeneratedPrefixRetailDebt(
                ReadString(debt, "Family"),
                ReadString(debt, "Method"),
                ReadInt(debt, "BodyLength"),
                ReadInt(debt, "PrefixByteLength"),
                ReadInt(debt, "RecordCount"),
                ReadInt(debt, "TailByteLength"),
                StringArray(debt, "ObjectIdExpressions"),
                ReadString(debt, "LinkedObjectIdExpression"));
        }
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixRetailSample> ReadSemanticTracePackets(string semanticTraceDirectory)
    {
        if (!Directory.Exists(semanticTraceDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(semanticTraceDirectory, "*.semantic-trace.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("Packets", out var packets) || packets.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var packet in packets.EnumerateArray())
            {
                var queued = packet.TryGetProperty("QueuedPeerChunk", out var queuedPeerChunk)
                    && queuedPeerChunk.ValueKind == JsonValueKind.Object
                    ? queuedPeerChunk
                    : default;

                var embeddedPrefix = packet.TryGetProperty("EmbeddedPrefix", out var prefix)
                    && prefix.ValueKind == JsonValueKind.Object
                    ? prefix
                    : default;

                var prefixLength = embeddedPrefix.ValueKind == JsonValueKind.Object
                    ? ReadInt(embeddedPrefix, "PrefixLength")
                    : queued.ValueKind == JsonValueKind.Object
                        ? ReadInt(queued, "OpaquePrefixLength")
                        : ReadNullableInt(packet, "EmbeddedPrefixLength") ?? 0;

                var recordCount = queued.ValueKind == JsonValueKind.Object
                    ? ReadInt(queued, "EmbeddedRecordCount")
                    : packet.TryGetProperty("EmbeddedRecords", out var records) && records.ValueKind == JsonValueKind.Array
                        ? records.GetArrayLength()
                        : 0;

                var family = embeddedPrefix.ValueKind == JsonValueKind.Object
                    ? ReadString(embeddedPrefix, "Family")
                    : queued.ValueKind == JsonValueKind.Object
                        ? ReadString(queued, "Family")
                        : "";

                if (prefixLength <= 0 && recordCount <= 0 && family.Length == 0)
                {
                    continue;
                }

                yield return new Tf2Ps3SourceGeneratedPrefixRetailSample(
                    Path.GetFileName(path),
                    ReadInt(packet, "PacketIndex"),
                    ReadInt(packet, "SourceStep"),
                    ReadString(packet, "Direction"),
                    ReadInt(packet, "Sequence"),
                    ReadInt(packet, "PayloadLength"),
                    ReadInt(packet, "BodyLength"),
                    prefixLength,
                    recordCount,
                    family,
                    ReadString(packet, "SemanticRole"),
                    ReadString(packet, "Confidence"),
                    ReadString(packet, "PlainEnglish"),
                    ReadString(queued, "OpaquePrefixHex"),
                    ReadString(queued, "OpaquePrefixSuffixHex"),
                    ReadDouble(queued, "OpaquePrefixEntropy"),
                    StringArray(packet, "EmbeddedRecordSummaries"));
            }
        }
    }

    private static string[] BuildNativeImplementationRules()
    {
        return
        [
            "Exact retail shape matches only validate body shape and record count; they must not be copied as replay bytes.",
            "Generated prefixes remain native debt until emitted by the recovered TF.elf path 008bc978 -> 008b9f70/008bb058/008bc490.",
            "Decoded PNG/COc/DSC records should keep using typed record builders; only the prefix/control layer before those records remains unresolved here.",
            "A missing exact match is stronger evidence to revisit TF.elf/server.dll writer semantics than to add another deterministic filler family."
        ];
    }

    private static string[] BuildConclusions(Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget[] targets)
    {
        var conclusions = new List<string>
        {
            "The generated queued-prefix bodies now have retail-shape evidence separate from static-template debt.",
            "This report is a native implementation guide, not a replay whitelist."
        };

        foreach (var target in targets)
        {
            conclusions.Add(target.ExactRetailSampleCount > 0
                ? $"{target.Family} has exact retail semantic-trace shape evidence for {target.RetailShapeKey}."
                : $"{target.Family} has no exact retail semantic-trace shape evidence for {target.RetailShapeKey}; keep it open in native debt.");
        }

        return conclusions.ToArray();
    }

    private static string[] StringArray(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetString() ?? "").Where(static item => item.Length > 0).ToArray()
            : [];
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int? ReadNullableInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.True => 1,
            _ => 0
        };
    }

    private static double ReadDouble(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;
    }
}

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapReport(
    string Status,
    string Note,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs Inputs,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary Summary,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget[] Targets,
    string[] NativeImplementationRules,
    string[] Conclusions);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs(
    string QueuedPrefixContract,
    string SemanticTraceDirectory,
    string QueuedOpaqueReport);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary(
    int GeneratedQueuedPrefixDebtCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount,
    int SemanticTraceFileCount,
    int SemanticTracePacketCount,
    int RetailQueuedRecordPacketCount,
    int CorpusQueuedChunkPacketCount,
    int CorpusServerQueuedChunkPacketCount,
    int ExactRetailShapeTargetCount,
    int TargetWithoutExactRetailShapeCount);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget(
    string Family,
    string Method,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int TailByteLength,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression,
    string RetailShapeKey,
    int ExactRetailSampleCount,
    int NearbyRetailFamilyCount,
    Tf2Ps3SourceGeneratedPrefixRetailSample[] ExactRetailSamples,
    Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily[] NearbyRetailFamilies,
    string Conclusion,
    string NativeNextAction);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailDebt(
    string Family,
    string Method,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int TailByteLength,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailSample(
    string SourceTraceFile,
    int PacketIndex,
    int SourceStep,
    string Direction,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    string Family,
    string SemanticRole,
    string Confidence,
    string PlainEnglish,
    string PrefixFirst16Hex,
    string PrefixLast16Hex,
    double PrefixEntropy,
    string[] EmbeddedRecordSummaries);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily(
    string RetailShapeKey,
    string Family,
    int PrefixByteLength,
    int RecordCount,
    int PrefixDelta,
    int RecordCountDelta,
    int SampleCount,
    string FirstTraceFile,
    int FirstSourceStep);
