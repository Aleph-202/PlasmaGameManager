using System.Text.Json;
using PlasmaGameManager.Protocol;

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
        string outputPath,
        string sourceLoadingFrameDebtPath = "")
    {
        using var queuedPrefixContract = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));
        using var queuedOpaque = JsonDocument.Parse(await File.ReadAllTextAsync(queuedOpaqueReportPath));
        using var sourceLoadingFrameDebt = File.Exists(sourceLoadingFrameDebtPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(sourceLoadingFrameDebtPath))
            : null;

        var generatedDebt = ReadGeneratedDebt(queuedPrefixContract.RootElement).ToArray();
        var nativeRecordPrefixDebt = sourceLoadingFrameDebt is null
            ? []
            : ReadNativeRecordPrefixDebt(sourceLoadingFrameDebt.RootElement).ToArray();
        var tracePackets = ReadSemanticTracePackets(semanticTraceDirectory).ToArray();
        var queuedRecordPackets = tracePackets
            .Where(static packet => packet.Direction == "ServerToClient"
                && packet.PrefixByteLength > 0
                && packet.RecordCount > 0)
            .ToArray();

        var targets = generatedDebt
            .Select(debt => BuildTarget(debt, queuedRecordPackets))
            .ToArray();
        var nativeRecordPrefixTargets = nativeRecordPrefixDebt
            .Select(debt => BuildNativeRecordPrefixTarget(debt, queuedRecordPackets))
            .ToArray();
        var exactSamples = targets.SelectMany(static target => target.ExactRetailSamples).ToArray();
        var nativeExactSamples = nativeRecordPrefixTargets
            .SelectMany(static target => target.ExactRetailSamples)
            .ToArray();

        var report = new Tf2Ps3SourceGeneratedPrefixRetailCrossmapReport(
            "tf2ps3-source-generated-prefix-retail-crossmap",
            "Crossmaps generated/provisional queued-prefix responder debt against retail semantic traces. Exact shape evidence pins native body families; it is not replay permission.",
            new Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs(
                queuedPrefixContractPath,
                semanticTraceDirectory,
                queuedOpaqueReportPath,
                sourceLoadingFrameDebtPath),
            new Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary(
                generatedDebt.Length,
                generatedDebt.Sum(static debt => debt.PrefixByteLength),
                generatedDebt.Sum(static debt => debt.RecordCount),
                nativeRecordPrefixDebt.Length,
                nativeRecordPrefixDebt.Sum(static debt => debt.FrameCount),
                nativeRecordPrefixDebt.Sum(static debt => debt.BlockingByteCount),
                nativeRecordPrefixDebt.Sum(static debt => debt.PrefixDebtBytes),
                nativeRecordPrefixDebt.Sum(static debt => debt.TrailingDebtBytes),
                nativeRecordPrefixDebt.Sum(static debt => debt.NativeRecordByteCount),
                Directory.Exists(semanticTraceDirectory)
                    ? Directory.EnumerateFiles(semanticTraceDirectory, "*.semantic-trace.json").Count()
                    : 0,
                tracePackets.Length,
                queuedRecordPackets.Length,
                ReadInt(queuedOpaque.RootElement.GetProperty("Summary"), "QueuedChunkPacketCount"),
                ReadInt(queuedOpaque.RootElement.GetProperty("Summary"), "ServerQueuedChunkPacketCount"),
                targets.Count(static target => target.ExactRetailSampleCount > 0),
                targets.Count(static target => target.ExactRetailSampleCount == 0),
                exactSamples.Count(static sample => sample.NativeFrameKind == "DirectDatagramCandidate"),
                exactSamples.Count(static sample => sample.NativePrefixProbe.StartsWithNativeMessageSentinel),
                exactSamples.Count(static sample => sample.NativePrefixProbe.NativeDirectDecode != "none"),
                exactSamples.Count(static sample => sample.NativePrefixProbe.NativeSentinelMessageOffsetCount > 0),
                exactSamples.Count(static sample => sample.NativePrefixProbe.ReadableSourceTokenCount > 0),
                nativeRecordPrefixTargets.Count(static target => target.ExactRetailSampleCount > 0),
                nativeRecordPrefixTargets.Count(static target => target.ExactRetailSampleCount == 0),
                nativeExactSamples.Length,
                nativeExactSamples.Count(static sample => sample.NativePrefixProbe.NativeSentinelMessageOffsetCount > 0),
                nativeExactSamples.Count(static sample => sample.NativePrefixProbe.ReadableSourceTokenCount > 0)),
            targets,
            nativeRecordPrefixTargets,
            BuildNativeWriterEvidence(),
            BuildNativeImplementationRules(),
            BuildConclusions(targets, nativeRecordPrefixTargets));

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
                || Math.Abs(packet.BodyLength - debt.BodyLength) <= 128
                || Math.Abs(packet.PayloadLength - debt.BodyLength) <= 128
                || Math.Abs(packet.PrefixByteLength - debt.PrefixByteLength) <= 8)
            .GroupBy(static packet => (packet.PrefixByteLength, packet.RecordCount, BodyLength: packet.BodyLength, packet.Family))
            .Select(group => new Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily(
                $"server-queued-opaque-prefix-{group.Key.PrefixByteLength}-records-{group.Key.RecordCount}-body-{group.Key.BodyLength}",
                group.Key.Family,
                group.Key.PrefixByteLength,
                group.Key.RecordCount,
                group.Key.BodyLength,
                Math.Abs(group.Key.PrefixByteLength - debt.PrefixByteLength),
                Math.Abs(group.Key.RecordCount - debt.RecordCount),
                Math.Abs(group.Key.BodyLength - debt.BodyLength),
                group.Count(),
                group.OrderBy(static packet => packet.SourceStep).First().SourceTraceFile,
                group.OrderBy(static packet => packet.SourceStep).First().SourceStep))
            .OrderBy(static family => family.BodyLengthDelta)
            .ThenBy(static family => family.PrefixDelta)
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
                "Implement the Prefix578/Records2 player-state-link family as a native direct-datagram Source payload before it enters the TF.elf send wrapper.",
            "late-large-command-prep-segment-b" =>
                "Implement the Prefix570/Records3 player-state-link family as a native direct-datagram Source payload before it enters the TF.elf send wrapper.",
            "late-large-command-followup" =>
                "Implement the Prefix848/Records1 player-state-link family as a native direct-datagram Source payload before it enters the TF.elf send wrapper.",
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

    private static Tf2Ps3SourceNativeRecordPrefixRetailCrossmapTarget BuildNativeRecordPrefixTarget(
        Tf2Ps3SourceNativeRecordPrefixRetailDebt debt,
        Tf2Ps3SourceGeneratedPrefixRetailSample[] queuedRecordPackets)
    {
        var exact = queuedRecordPackets
            .Where(packet => packet.PrefixByteLength == debt.PrefixLength
                && packet.RecordCount == debt.MaxRecords
                && (packet.BodyLength == debt.Length || packet.PayloadLength == debt.Length || packet.PayloadLength == debt.Length + 2))
            .OrderBy(static packet => packet.SourceTraceFile, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .ThenBy(static packet => packet.PacketIndex)
            .Take(12)
            .ToArray();

        var nearby = queuedRecordPackets
            .Where(packet => packet.PrefixByteLength == debt.PrefixLength
                || packet.RecordCount == debt.MaxRecords
                || Math.Abs(packet.PrefixByteLength - debt.PrefixLength) <= 8
                || Math.Abs(packet.BodyLength - debt.Length) <= 128
                || Math.Abs(packet.PayloadLength - debt.Length) <= 128)
            .GroupBy(static packet => (packet.PrefixByteLength, packet.RecordCount, BodyLength: packet.BodyLength, packet.Family))
            .Select(group => new Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily(
                $"server-queued-opaque-prefix-{group.Key.PrefixByteLength}-records-{group.Key.RecordCount}-body-{group.Key.BodyLength}",
                group.Key.Family,
                group.Key.PrefixByteLength,
                group.Key.RecordCount,
                group.Key.BodyLength,
                Math.Abs(group.Key.PrefixByteLength - debt.PrefixLength),
                Math.Abs(group.Key.RecordCount - debt.MaxRecords),
                Math.Abs(group.Key.BodyLength - debt.Length),
                group.Count(),
                group.OrderBy(static packet => packet.SourceStep).First().SourceTraceFile,
                group.OrderBy(static packet => packet.SourceStep).First().SourceStep))
            .OrderBy(static family => family.BodyLengthDelta)
            .ThenBy(static family => family.PrefixDelta)
            .ThenBy(static family => family.RecordCountDelta)
            .ThenByDescending(static family => family.SampleCount)
            .Take(12)
            .ToArray();

        var conclusion = exact.Length > 0
            ? "Retail semantic traces contain this exact provisional loading state-link shape. Use it to recover the native field writer; do not replay the prefix bytes."
            : "No exact retail semantic-trace sample currently matches this provisional loading state-link shape. Use nearby families and TF.elf/server.dll writer evidence.";

        return new Tf2Ps3SourceNativeRecordPrefixRetailCrossmapTarget(
            debt.Family,
            debt.Phase,
            debt.Variant,
            debt.Length,
            debt.PrefixLength,
            debt.MaxRecords,
            debt.FrameCount,
            debt.BlockingByteCount,
            debt.PrefixDebtBytes,
            debt.TrailingDebtBytes,
            debt.NativeRecordByteCount,
            debt.DominantDebtKind,
            debt.PriorityScore,
            debt.FirstCollection,
            debt.FirstStageIndex,
            debt.FirstFrameIndex,
            $"server-queued-opaque-prefix-{debt.PrefixLength}-records-{debt.MaxRecords}-body-{debt.Length}",
            exact.Length,
            nearby.Length,
            exact,
            nearby,
            conclusion,
            "Recover the TF.elf queued peer-channel prefix/control fields for this length/prefix/record-count family, then replace WriteQueuedBoundaryBytes for this family.");
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

    private static IEnumerable<Tf2Ps3SourceNativeRecordPrefixRetailDebt> ReadNativeRecordPrefixDebt(JsonElement root)
    {
        if (!root.TryGetProperty("NativeRecordPrefixDebtFamilies", out var families) || families.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var family in families.EnumerateArray())
        {
            yield return new Tf2Ps3SourceNativeRecordPrefixRetailDebt(
                ReadString(family, "Family"),
                ReadString(family, "Phase"),
                ReadInt(family, "Variant"),
                ReadInt(family, "Length"),
                ReadInt(family, "PrefixLength"),
                ReadInt(family, "MaxRecords"),
                ReadInt(family, "FrameCount"),
                ReadInt(family, "BlockingByteCount"),
                ReadInt(family, "PrefixDebtBytes"),
                ReadInt(family, "TrailingDebtBytes"),
                ReadInt(family, "NativeRecordByteCount"),
                ReadString(family, "DominantDebtKind"),
                ReadInt(family, "PriorityScore"),
                ReadString(family, "FirstCollection"),
                ReadInt(family, "FirstStageIndex"),
                ReadInt(family, "FirstFrameIndex"));
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

                var queuedPrefixHex = ReadString(queued, "OpaquePrefixHex");
                var packetEmbeddedPrefixHex = ReadString(packet, "EmbeddedPrefixHex");
                var embeddedPrefixHex = embeddedPrefix.ValueKind == JsonValueKind.Object
                    ? ReadString(embeddedPrefix, "PrefixHex")
                    : "";
                var prefixHex = FirstNonEmpty(queuedPrefixHex, packetEmbeddedPrefixHex, embeddedPrefixHex);

                yield return new Tf2Ps3SourceGeneratedPrefixRetailSample(
                    Path.GetFileName(path),
                    ReadInt(packet, "PacketIndex"),
                    ReadInt(packet, "SourceStep"),
                    ReadString(packet, "Direction"),
                    ReadInt(packet, "Sequence"),
                    ReadInt(packet, "PayloadLength"),
                    ReadInt(packet, "BodyLength"),
                    ReadString(packet, "NativeFrameKind"),
                    prefixLength,
                    recordCount,
                    family,
                    ReadString(packet, "SemanticRole"),
                    ReadString(packet, "Confidence"),
                    ReadString(packet, "PlainEnglish"),
                    PrefixHexSource(queued, packetEmbeddedPrefixHex, embeddedPrefixHex),
                    FirstHex(prefixHex, 16),
                    LastHex(prefixHex, 16),
                    ReadDouble(queued, "OpaquePrefixEntropy") is > 0.0 and var queuedEntropy
                        ? queuedEntropy
                        : ReadDouble(embeddedPrefix, "PrefixEntropy"),
                    ProbeNativePrefix(prefixHex),
                    StringArray(packet, "EmbeddedRecordSummaries"));
            }
        }
    }

    private static Tf2Ps3SourceGeneratedPrefixNativeProbe ProbeNativePrefix(string prefixHex)
    {
        if (!TryHexToBytes(prefixHex, out var prefix))
        {
            return new Tf2Ps3SourceGeneratedPrefixNativeProbe(false, false, "", "none", 0, [], 0, []);
        }

        var offsets = new List<string>();
        for (var offset = 0; offset + 4 < prefix.Length; offset++)
        {
            if (IsNativeMessageSentinelAt(prefix, offset, out var messageId))
            {
                offsets.Add($"0x{offset:x}:{messageId}");
            }
        }

        var startsWithSentinel = IsNativeMessageSentinelAt(prefix, 0, out var startMessageId);
        var nativeDecode = "none";
        if (startsWithSentinel)
        {
            nativeDecode = startMessageId switch
            {
                "0x44" when Ps3SourceNativeMessages.TryDecodePlayerSummary(prefix, out _) => "native-player-summary-0x44",
                "0x45" when Ps3SourceNativeMessages.TryDecodeResourceStringTable(prefix, out _) => "native-resource-string-table-0x45",
                "0x49" when Ps3SourceNativeMessages.TryDecodeServerInfo(prefix, out _) => "native-server-info-0x49",
                _ => "native-sentinel-undecoded"
            };
        }

        var readableTokens = new[]
            {
                "maps/",
                "ctf_",
                "DT_",
                "GAME",
                "SENDTABLE",
                "Player"
            }
            .Where(token => IndexOf(prefix, token) >= 0)
            .ToArray();

        return new Tf2Ps3SourceGeneratedPrefixNativeProbe(
            true,
            startsWithSentinel,
            startMessageId,
            nativeDecode,
            offsets.Count,
            offsets.ToArray(),
            readableTokens.Length,
            readableTokens);
    }

    private static bool TryHexToBytes(string hex, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(hex) || (hex.Length & 1) != 0)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsNativeMessageSentinelAt(byte[] bytes, int offset, out string messageId)
    {
        messageId = "";
        if (offset < 0
            || offset + 4 >= bytes.Length
            || bytes[offset] != 0xff
            || bytes[offset + 1] != 0xff
            || bytes[offset + 2] != 0xff
            || bytes[offset + 3] != 0xff)
        {
            return false;
        }

        messageId = bytes[offset + 4] switch
        {
            0x44 => "0x44",
            0x45 => "0x45",
            0x49 => "0x49",
            _ => ""
        };
        return messageId.Length > 0;
    }

    private static int IndexOf(byte[] bytes, string token)
    {
        var needle = System.Text.Encoding.ASCII.GetBytes(token);
        for (var i = 0; i <= bytes.Length - needle.Length; i++)
        {
            var matches = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (bytes[i + j] != needle[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return i;
            }
        }

        return -1;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string PrefixHexSource(JsonElement queued, string packetEmbeddedPrefixHex, string embeddedPrefixHex)
    {
        if (queued.ValueKind == JsonValueKind.Object && !string.IsNullOrWhiteSpace(ReadString(queued, "OpaquePrefixHex")))
        {
            return "QueuedPeerChunk.OpaquePrefixHex";
        }

        if (!string.IsNullOrWhiteSpace(packetEmbeddedPrefixHex))
        {
            return "Packet.EmbeddedPrefixHex";
        }

        return !string.IsNullOrWhiteSpace(embeddedPrefixHex)
            ? "Packet.EmbeddedPrefix.PrefixHex"
            : "";
    }

    private static string[] BuildNativeImplementationRules()
    {
        return
        [
            "Exact retail shape matches only validate body shape and record count; they must not be copied as replay bytes.",
            "Exact late-large samples currently classify as DirectDatagramCandidate in the semantic trace, so their unresolved prefix is the native Source payload body, not the 008b9f70 queued-cell header.",
            "Generated prefixes remain native debt until emitted by the recovered TF.elf path 008bc978 -> 008b9f70/008bb058/008bc490.",
            "Decoded PNG/COc/DSC records should keep using typed record builders; only the prefix/control layer before those records remains unresolved here.",
            "A missing exact match is stronger evidence to revisit TF.elf/server.dll writer semantics than to add another deterministic filler family."
        ];
    }

    private static Tf2Ps3SourceGeneratedPrefixNativeWriterEvidence[] BuildNativeWriterEvidence()
    {
        return
        [
            new(
                "0090e550",
                "direct-source-player-summary-writer",
                "Builds a local 0x1800-byte bit buffer, writes message id 0x44, serializes per-player summary/object fields, then calls 008bc978 with param_6=0,param_7=0.",
                ["FUN_00870c28(local_1870,0x44)", "_opd_FUN_008bc978(... local_1870[0], (local_1864 + 7) >> 3, 0, 0)"]),
            new(
                "0090e8b0",
                "direct-source-resource-stringtable-writer",
                "Builds a local 0x2000-byte bit buffer, writes message id 0x45, serializes string/resource table entries, then calls 008bc978 with param_6=0,param_7=0.",
                ["FUN_00870c28(local_2050,0x45)", "_opd_FUN_008bc978(... local_2050[0], (local_2044 + 7) >> 3, 0, 0)"]),
            new(
                "0090ebf8",
                "direct-source-serverinfo-or-state-writer",
                "Builds a local 0x578-byte bit buffer, writes message id 0x49 and fixed field 8, serializes server/map/player state strings and counts, then calls 008bc978 with param_6=0,param_7=0.",
                ["FUN_0086e948(local_700,...,0x578,-1)", "FUN_00870c28(local_700,0x49)", "FUN_00870c28(local_700,8)", "_opd_FUN_008bc978(... local_700[0], (local_6f4 + 7) >> 3, 0, 0)"]),
            new(
                "008bc978",
                "source-send-wrapper",
                "Stages direct Source payload bytes. With param_6=0,param_7=0 it does not append the optional bit-sidecar or transform header before selecting direct/fragment send.",
                ["param_6 == 0 skips sidecar", "param_7 == 0 skips transform prefix", "_opd_FUN_008bb058 direct send", "_opd_FUN_008bc490 fragmented send"])
        ];
    }

    private static string[] BuildConclusions(
        Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget[] targets,
        Tf2Ps3SourceNativeRecordPrefixRetailCrossmapTarget[] nativeRecordPrefixTargets)
    {
        var conclusions = new List<string>
        {
            targets.Length == 0
                ? "No generated queued-prefix responder debt remains, so there are no retail-shape targets to pin in this report."
                : "The generated queued-prefix bodies now have retail-shape evidence separate from static-template debt.",
            nativeRecordPrefixTargets.Length == 0
                ? "No provisional native-record prefix loading debt remains."
                : $"{nativeRecordPrefixTargets.Length} provisional native-record prefix loading families remain; exact retail-shape matches are reverse-engineering targets, not replay permission.",
            "This report is a native implementation guide, not a replay whitelist."
        };

        foreach (var target in targets)
        {
            conclusions.Add(target.ExactRetailSampleCount > 0
                ? $"{target.Family} has exact retail semantic-trace shape evidence for {target.RetailShapeKey}; exact samples are direct Source datagrams in the current trace."
                : $"{target.Family} has no exact retail semantic-trace shape evidence for {target.RetailShapeKey}; keep it open in native debt.");
        }

        foreach (var target in nativeRecordPrefixTargets.Take(12))
        {
            conclusions.Add(target.ExactRetailSampleCount > 0
                ? $"{target.Family} has exact retail shape evidence for {target.RetailShapeKey}; recover the field writer for this family."
                : $"{target.Family} has no exact retail shape evidence for {target.RetailShapeKey}; use nearby corpus families and TF.elf/server.dll evidence.");
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

    private static string FirstHex(string hex, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return "";
        }

        var characterCount = Math.Min(hex.Length, byteCount * 2);
        return hex[..characterCount];
    }

    private static string LastHex(string hex, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return "";
        }

        var characterCount = Math.Min(hex.Length, byteCount * 2);
        return hex[^characterCount..];
    }
}

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapReport(
    string Status,
    string Note,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs Inputs,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary Summary,
    Tf2Ps3SourceGeneratedPrefixRetailCrossmapTarget[] Targets,
    Tf2Ps3SourceNativeRecordPrefixRetailCrossmapTarget[] NativeRecordPrefixTargets,
    Tf2Ps3SourceGeneratedPrefixNativeWriterEvidence[] NativeWriterEvidence,
    string[] NativeImplementationRules,
    string[] Conclusions);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapInputs(
    string QueuedPrefixContract,
    string SemanticTraceDirectory,
    string QueuedOpaqueReport,
    string SourceLoadingFrameDebt);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailCrossmapSummary(
    int GeneratedQueuedPrefixDebtCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount,
    int NativeRecordPrefixDebtFamilyCount,
    int NativeRecordPrefixDebtFrameCount,
    int NativeRecordPrefixDebtBlockingBytes,
    int NativeRecordPrefixDebtBytes,
    int NativeRecordTrailingDebtBytes,
    int NativeRecordVisibleRecordBytes,
    int SemanticTraceFileCount,
    int SemanticTracePacketCount,
    int RetailQueuedRecordPacketCount,
    int CorpusQueuedChunkPacketCount,
    int CorpusServerQueuedChunkPacketCount,
    int ExactRetailShapeTargetCount,
    int TargetWithoutExactRetailShapeCount,
    int ExactRetailDirectDatagramSampleCount,
    int ExactRetailStartsWithNativeMessageSentinelCount,
    int ExactRetailNativeDirectDecodeCount,
    int ExactRetailContainsNativeMessageSentinelCount,
    int ExactRetailContainsReadableSourceTokenCount,
    int NativeRecordPrefixExactRetailShapeTargetCount,
    int NativeRecordPrefixTargetWithoutExactRetailShapeCount,
    int NativeRecordPrefixExactRetailSampleCount,
    int NativeRecordPrefixExactRetailContainsNativeMessageSentinelCount,
    int NativeRecordPrefixExactRetailContainsReadableSourceTokenCount);

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

public sealed record Tf2Ps3SourceNativeRecordPrefixRetailCrossmapTarget(
    string Family,
    string Phase,
    int Variant,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int FrameCount,
    int BlockingByteCount,
    int PrefixDebtBytes,
    int TrailingDebtBytes,
    int NativeRecordByteCount,
    string DominantDebtKind,
    int PriorityScore,
    string FirstCollection,
    int FirstStageIndex,
    int FirstFrameIndex,
    string RetailShapeKey,
    int ExactRetailSampleCount,
    int NearbyRetailFamilyCount,
    Tf2Ps3SourceGeneratedPrefixRetailSample[] ExactRetailSamples,
    Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily[] NearbyRetailFamilies,
    string Conclusion,
    string NativeNextAction);

public sealed record Tf2Ps3SourceNativeRecordPrefixRetailDebt(
    string Family,
    string Phase,
    int Variant,
    int Length,
    int PrefixLength,
    int MaxRecords,
    int FrameCount,
    int BlockingByteCount,
    int PrefixDebtBytes,
    int TrailingDebtBytes,
    int NativeRecordByteCount,
    string DominantDebtKind,
    int PriorityScore,
    string FirstCollection,
    int FirstStageIndex,
    int FirstFrameIndex);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailSample(
    string SourceTraceFile,
    int PacketIndex,
    int SourceStep,
    string Direction,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    string NativeFrameKind,
    int PrefixByteLength,
    int RecordCount,
    string Family,
    string SemanticRole,
    string Confidence,
    string PlainEnglish,
    string PrefixHexSource,
    string PrefixFirst16Hex,
    string PrefixLast16Hex,
    double PrefixEntropy,
    Tf2Ps3SourceGeneratedPrefixNativeProbe NativePrefixProbe,
    string[] EmbeddedRecordSummaries);

public sealed record Tf2Ps3SourceGeneratedPrefixNativeProbe(
    bool HasPrefixBytes,
    bool StartsWithNativeMessageSentinel,
    string StartingNativeMessageId,
    string NativeDirectDecode,
    int NativeSentinelMessageOffsetCount,
    string[] NativeSentinelMessageOffsets,
    int ReadableSourceTokenCount,
    string[] ReadableSourceTokens);

public sealed record Tf2Ps3SourceGeneratedPrefixNativeWriterEvidence(
    string Address,
    string Role,
    string Meaning,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailNearbyFamily(
    string RetailShapeKey,
    string Family,
    int PrefixByteLength,
    int RecordCount,
    int BodyLength,
    int PrefixDelta,
    int RecordCountDelta,
    int BodyLengthDelta,
    int SampleCount,
    string FirstTraceFile,
    int FirstSourceStep);
