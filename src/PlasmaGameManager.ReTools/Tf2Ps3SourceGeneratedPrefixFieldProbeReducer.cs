using System.Security.Cryptography;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceGeneratedPrefixFieldProbeReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceGeneratedPrefixFieldProbeReport> ReduceAsync(
        string generatedPrefixRetailCrossmapPath,
        string semanticTraceDirectory,
        string outputPath)
    {
        using var crossmap = JsonDocument.Parse(await File.ReadAllTextAsync(generatedPrefixRetailCrossmapPath));
        var tracePackets = ReadSemanticTracePackets(semanticTraceDirectory)
            .ToDictionary(static packet => TraceKey(packet.SourceTraceFile, packet.PacketIndex, packet.SourceStep), StringComparer.Ordinal);

        var targets = ReadCrossmapTargets(crossmap.RootElement)
            .Select(target => BuildTarget(target, tracePackets))
            .ToArray();

        var sampleCount = targets.Sum(static target => target.ExactRetailSampleCount);
        var scalarHitCount = targets.Sum(static target => target.ScalarHitCount);
        var tailScalarHitCount = targets.Sum(static target => target.TailScalarHitCount);
        var samplesWithScalarHits = targets.Sum(static target => target.SamplesWithScalarHits);
        var samplesWithTailScalarHits = targets.Sum(static target => target.SamplesWithTailScalarHits);
        var report = new Tf2Ps3SourceGeneratedPrefixFieldProbeReport(
            "tf2ps3-source-generated-prefix-field-probe",
            "Probes exact retail generated/native-record stream samples for direct scalar fields and byte stability. This is reverse-engineering evidence for the native Source replacement, not packet replay data.",
            new Tf2Ps3SourceGeneratedPrefixFieldProbeInputs(
                generatedPrefixRetailCrossmapPath,
                semanticTraceDirectory),
            new Tf2Ps3SourceGeneratedPrefixFieldProbeSummary(
                targets.Length,
                sampleCount,
                samplesWithScalarHits,
                scalarHitCount,
                samplesWithTailScalarHits,
                tailScalarHitCount,
                targets.Count(static target => target.StableBytePositionCount > 0),
                targets.Count(static target => target.TailStableBytePositionCount > 0),
                targets.Count(static target => target.NoObviousScalarFields),
                targets.Sum(static target => target.PlainObjectStreamEnvelopeCandidateCount),
                sampleCount - targets.Sum(static target => target.PlainObjectStreamEnvelopeCandidateCount),
                targets.Sum(static target => target.StableBytePositionCount),
                targets.Sum(static target => target.TailStableBytePositionCount),
                targets.Sum(static target => target.Samples.Sum(static sample => sample.TrailingByteLength))),
            targets,
            BuildProbeRules(),
            BuildConclusions(targets));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeTarget BuildTarget(
        Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget target,
        IReadOnlyDictionary<string, Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket> tracePackets)
    {
        var samples = target.ExactRetailSamples
            .Select(sample => BuildSample(target, sample, tracePackets))
            .Where(static sample => sample is not null)
            .Cast<Tf2Ps3SourceGeneratedPrefixFieldProbeSample>()
            .ToArray();
        var prefixArrays = samples
            .Select(static sample => HexToBytes(sample.PrefixHex))
            .Where(static bytes => bytes.Length > 0)
            .ToArray();
        var tailArrays = samples
            .Select(static sample => HexToBytes(sample.TrailingHex))
            .Where(static bytes => bytes.Length > 0)
            .ToArray();
        var stablePositions = FindStableBytePositions(prefixArrays);
        var tailStablePositions = FindStableBytePositions(tailArrays);
        var stableRuns = BuildStableRuns(stablePositions)
            .Take(16)
            .ToArray();
        var tailStableRuns = BuildStableRuns(tailStablePositions)
            .Take(16)
            .ToArray();
        var scalarHitCount = samples.Sum(static sample => sample.ScalarHits.Length);
        var tailScalarHitCount = samples.Sum(static sample => sample.TailScalarHits.Length);
        var plainObjectStreamEnvelopeCandidates = samples.Count(static sample => sample.PlainObjectStreamEnvelopeCandidate);

        return new Tf2Ps3SourceGeneratedPrefixFieldProbeTarget(
            target.Family,
            target.Method,
            target.BodyLength,
            target.PrefixByteLength,
            target.RecordCount,
            samples.Length,
            scalarHitCount,
            samples.Count(static sample => sample.ScalarHits.Length > 0),
            tailScalarHitCount,
            samples.Count(static sample => sample.TailScalarHits.Length > 0),
            stablePositions.Length,
            tailStablePositions.Length,
            stableRuns,
            tailStableRuns,
            CommonPrefixLength(prefixArrays),
            CommonSuffixLength(prefixArrays),
            CommonPrefixLength(tailArrays),
            CommonSuffixLength(tailArrays),
            plainObjectStreamEnvelopeCandidates,
            samples.Length > 0 && scalarHitCount == 0 && tailScalarHitCount == 0,
            samples,
            BuildTargetConclusion(target, samples.Length, scalarHitCount, tailScalarHitCount, stablePositions.Length, tailStablePositions.Length, plainObjectStreamEnvelopeCandidates));
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeSample? BuildSample(
        Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget target,
        Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapSample sample,
        IReadOnlyDictionary<string, Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket> tracePackets)
    {
        if (!tracePackets.TryGetValue(TraceKey(sample.SourceTraceFile, sample.PacketIndex, sample.SourceStep), out var tracePacket))
        {
            return null;
        }

        var prefixBytes = HexToBytes(tracePacket.PrefixHex);
        var tailBytes = HexToBytes(tracePacket.TrailingHex);
        var scalarHits = ProbeScalarFields(tracePacket, prefixBytes);
        var tailScalarHits = ProbeScalarFields(tracePacket, tailBytes);
        return new Tf2Ps3SourceGeneratedPrefixFieldProbeSample(
            tracePacket.SourceTraceFile,
            tracePacket.PacketIndex,
            tracePacket.SourceStep,
            tracePacket.Sequence,
            tracePacket.PayloadLength,
            tracePacket.BodyLength,
            tracePacket.PrefixByteLength,
            tracePacket.NativeFrameKind,
            tracePacket.EmbeddedRecords.Length,
            tracePacket.PrefixHex,
            Sha256Hex(prefixBytes),
            Entropy(prefixBytes),
            FirstHex(tracePacket.PrefixHex, 16),
            LastHex(tracePacket.PrefixHex, 16),
            tracePacket.TrailingHex,
            tailBytes.Length,
            Sha256Hex(tailBytes),
            Entropy(tailBytes),
            FirstHex(tracePacket.TrailingHex, 16),
            LastHex(tracePacket.TrailingHex, 16),
            LooksLikePlainObjectStreamEnvelope(prefixBytes),
            tracePacket.EmbeddedRecords,
            scalarHits,
            tailScalarHits,
            target.PrefixByteLength == tracePacket.PrefixByteLength && target.RecordCount == tracePacket.EmbeddedRecords.Length);
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit[] ProbeScalarFields(
        Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket packet,
        byte[] prefixBytes)
    {
        if (prefixBytes.Length == 0)
        {
            return [];
        }

        var candidates = new List<Tf2Ps3SourceGeneratedPrefixScalarCandidate>
        {
            new("PacketSequence", packet.Sequence),
            new("PayloadLength", packet.PayloadLength),
            new("BodyLength", packet.BodyLength),
            new("PrefixByteLength", packet.PrefixByteLength),
            new("TrailingByteLength", HexToBytes(packet.TrailingHex).Length),
            new("PacketIndex", packet.PacketIndex)
        };

        for (var i = 0; i < packet.EmbeddedRecords.Length; i++)
        {
            var record = packet.EmbeddedRecords[i];
            AddHexCandidate(candidates, $"EmbeddedRecord[{i}].ObjectId", record.ObjectId);
            AddHexCandidate(candidates, $"EmbeddedRecord[{i}].OwnerOrRootObjectId", record.OwnerOrRootObjectId);
            AddHexCandidate(candidates, $"EmbeddedRecord[{i}].LinkedObjectId", record.LinkedObjectId);
        }

        return candidates
            .SelectMany(candidate => ProbeScalarCandidate(prefixBytes, candidate))
            .ToArray();
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit> ProbeScalarCandidate(
        byte[] prefixBytes,
        Tf2Ps3SourceGeneratedPrefixScalarCandidate candidate)
    {
        if (candidate.Value < 0)
        {
            yield break;
        }

        if (candidate.Value is >= 16 and <= ushort.MaxValue)
        {
            foreach (var hit in ProbePattern(prefixBytes, candidate, "UInt16BigEndian", ToBytes((ushort)candidate.Value, bigEndian: true)))
            {
                yield return hit;
            }

            foreach (var hit in ProbePattern(prefixBytes, candidate, "UInt16LittleEndian", ToBytes((ushort)candidate.Value, bigEndian: false)))
            {
                yield return hit;
            }
        }

        if (candidate.Value <= uint.MaxValue)
        {
            foreach (var hit in ProbePattern(prefixBytes, candidate, "UInt32BigEndian", ToBytes((uint)candidate.Value, bigEndian: true)))
            {
                yield return hit;
            }

            foreach (var hit in ProbePattern(prefixBytes, candidate, "UInt32LittleEndian", ToBytes((uint)candidate.Value, bigEndian: false)))
            {
                yield return hit;
            }
        }
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit> ProbePattern(
        byte[] prefixBytes,
        Tf2Ps3SourceGeneratedPrefixScalarCandidate candidate,
        string encoding,
        byte[] pattern)
    {
        var offset = IndexOf(prefixBytes, pattern, 0);
        while (offset >= 0)
        {
            yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit(
                candidate.Name,
                candidate.Value,
                encoding,
                offset,
                Convert.ToHexString(pattern).ToLowerInvariant());
            offset = IndexOf(prefixBytes, pattern, offset + 1);
        }
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeStableByte[] FindStableBytePositions(byte[][] prefixes)
    {
        if (prefixes.Length < 2)
        {
            return [];
        }

        var length = prefixes.Min(static prefix => prefix.Length);
        var stable = new List<Tf2Ps3SourceGeneratedPrefixFieldProbeStableByte>();
        for (var offset = 0; offset < length; offset++)
        {
            var value = prefixes[0][offset];
            if (prefixes.All(prefix => prefix[offset] == value))
            {
                stable.Add(new Tf2Ps3SourceGeneratedPrefixFieldProbeStableByte(
                    offset,
                    value,
                    value.ToString("x2")));
            }
        }

        return stable.ToArray();
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun> BuildStableRuns(
        Tf2Ps3SourceGeneratedPrefixFieldProbeStableByte[] stablePositions)
    {
        if (stablePositions.Length == 0)
        {
            yield break;
        }

        var start = stablePositions[0].Offset;
        var bytes = new List<byte> { stablePositions[0].Value };
        var previous = stablePositions[0].Offset;
        for (var i = 1; i < stablePositions.Length; i++)
        {
            var current = stablePositions[i];
            if (current.Offset == previous + 1)
            {
                bytes.Add(current.Value);
                previous = current.Offset;
                continue;
            }

            yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun(
                start,
                bytes.Count,
                Convert.ToHexString(bytes.ToArray()).ToLowerInvariant());
            start = current.Offset;
            previous = current.Offset;
            bytes = [current.Value];
        }

        yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun(
            start,
            bytes.Count,
            Convert.ToHexString(bytes.ToArray()).ToLowerInvariant());
    }

    private static int CommonPrefixLength(byte[][] prefixes)
    {
        if (prefixes.Length < 2)
        {
            return 0;
        }

        var length = prefixes.Min(static prefix => prefix.Length);
        for (var offset = 0; offset < length; offset++)
        {
            var value = prefixes[0][offset];
            if (prefixes.Any(prefix => prefix[offset] != value))
            {
                return offset;
            }
        }

        return length;
    }

    private static int CommonSuffixLength(byte[][] prefixes)
    {
        if (prefixes.Length < 2)
        {
            return 0;
        }

        var length = prefixes.Min(static prefix => prefix.Length);
        for (var suffix = 0; suffix < length; suffix++)
        {
            var value = prefixes[0][prefixes[0].Length - 1 - suffix];
            if (prefixes.Any(prefix => prefix[prefix.Length - 1 - suffix] != value))
            {
                return suffix;
            }
        }

        return length;
    }

    private static string[] BuildProbeRules()
    {
        return
        [
            "Scalar probes search exact retail generated/native-record prefix and trailing bytes for packet sequence, payload/body/prefix/tail lengths, packet index, and embedded object ids.",
            "Scalar probes intentionally use direct UInt16/UInt32 big/little-endian encodings only; one-byte values are too collision-prone in high-entropy payloads.",
            "SourceStep is PCAP-analysis metadata and is not treated as a protocol scalar candidate.",
            "A plain object-stream envelope candidate must start with the known object-stream kind byte 0x01 or 0x02; transformed/bit-packed payloads are not ruled out by this check.",
            "Single-sample targets do not contribute stable-byte evidence."
        ];
    }

    private static string[] BuildConclusions(Tf2Ps3SourceGeneratedPrefixFieldProbeTarget[] targets)
    {
        var conclusions = new List<string>
        {
            targets.Length == 0
                ? "No exact retail generated/native-record-prefix targets were available to probe."
                : $"Probed {targets.Length} generated/native-record-prefix target families from the retail crossmap.",
            "Exact retail generated/native-record-prefix bytes are checked for plain sequence, length, packet index, object-id, and linked-object scalar fields before any writer assumptions are made.",
            targets.Length == 0
                ? "No late PNG/COc prefix probe targets remain after the generated native-record-prefix routes were replaced."
                : "The late PNG/COc prefixes do not match the plain 93-byte Ps3SourceObjectStream envelope; they are more likely TF.elf Source bitstream/control families before embedded records.",
            targets.Length == 0
                ? "No generated/native-record-prefix field-probe blocker remains in the current map-load path."
                : "The embedded PNG/COc records are native and typed; the remaining map-load blocker is the surrounding Source bitstream before and after embedded records, not the record writer."
        };

        foreach (var target in targets)
        {
            conclusions.Add(target.StableBytePositionCount > 0 || target.TailStableBytePositionCount > 0
                ? $"{target.Family} has {target.StableBytePositionCount} stable prefix byte positions and {target.TailStableBytePositionCount} stable trailing byte positions across exact retail samples; validate any stable bytes against TF.elf/server.dll writers before implementation."
                : $"{target.Family} has no repeated stable prefix or trailing bytes across multiple exact retail samples.");
        }

        return conclusions.ToArray();
    }

    private static string BuildTargetConclusion(
        Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget target,
        int sampleCount,
        int scalarHitCount,
        int tailScalarHitCount,
        int stableByteCount,
        int tailStableByteCount,
        int plainObjectStreamEnvelopeCandidateCount)
    {
        if (sampleCount == 0)
        {
            return "No exact retail samples were available for this generated-prefix target.";
        }

        if (scalarHitCount == 0 && tailScalarHitCount == 0 && stableByteCount == 0 && tailStableByteCount == 0 && plainObjectStreamEnvelopeCandidateCount == 0)
        {
            return $"{target.Family} has exact retail shape evidence, but its prefix/trailing bytes do not expose direct scalar fields or stable bytes. Continue from TF.elf/server.dll bitstream writers rather than static filler.";
        }

        if (scalarHitCount == 0 && tailScalarHitCount == 0 && (stableByteCount > 0 || tailStableByteCount > 0))
        {
            return $"{target.Family} has sparse stable prefix/trailing byte positions but no direct scalar field hits. Treat stable bytes as weak anchors only.";
        }

        return $"{target.Family} has probe hits that need manual validation against TF.elf/server.dll writer code before implementation.";
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget> ReadCrossmapTargets(JsonElement root)
    {
        if (root.TryGetProperty("Targets", out var targets) && targets.ValueKind == JsonValueKind.Array)
        {
            foreach (var target in targets.EnumerateArray())
            {
                yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget(
                    ReadString(target, "Family"),
                    ReadString(target, "Method"),
                    ReadInt(target, "BodyLength"),
                    ReadInt(target, "PrefixByteLength"),
                    ReadInt(target, "RecordCount"),
                    ReadCrossmapSamples(target));
            }
        }

        if (!root.TryGetProperty("NativeRecordPrefixTargets", out var nativeTargets) || nativeTargets.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var target in nativeTargets.EnumerateArray())
        {
            var variant = ReadInt(target, "Variant");
            var method = $"native-record-prefix:{ReadString(target, "Phase")}:variant{variant}";
            yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget(
                ReadString(target, "Family"),
                method,
                ReadInt(target, "BodyLength"),
                ReadInt(target, "PrefixByteLength"),
                ReadInt(target, "RecordCount"),
                ReadCrossmapSamples(target));
        }
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapSample[] ReadCrossmapSamples(JsonElement target)
    {
        if (!target.TryGetProperty("ExactRetailSamples", out var samples) || samples.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return samples.EnumerateArray()
            .Select(static sample => new Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapSample(
                ReadString(sample, "SourceTraceFile"),
                ReadInt(sample, "PacketIndex"),
                ReadInt(sample, "SourceStep")))
            .ToArray();
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket> ReadSemanticTracePackets(string semanticTraceDirectory)
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
                var prefixHex = ReadString(packet, "EmbeddedPrefixHex");
                if (string.IsNullOrWhiteSpace(prefixHex)
                    && packet.TryGetProperty("EmbeddedPrefix", out var embeddedPrefix)
                    && embeddedPrefix.ValueKind == JsonValueKind.Object)
                {
                    prefixHex = ReadString(embeddedPrefix, "PrefixHex");
                }

                if (string.IsNullOrWhiteSpace(prefixHex)
                    && packet.TryGetProperty("QueuedPeerChunk", out var queued)
                    && queued.ValueKind == JsonValueKind.Object)
                {
                    prefixHex = ReadString(queued, "OpaquePrefixHex");
                }

                var bodyHex = ReadString(packet, "BodyHex");
                if (string.IsNullOrWhiteSpace(bodyHex))
                {
                    bodyHex = ReadString(packet, "PayloadHex");
                    if (bodyHex.Length >= 4)
                    {
                        bodyHex = bodyHex[4..];
                    }
                }

                if (string.IsNullOrWhiteSpace(prefixHex))
                {
                    continue;
                }

                var records = ReadEmbeddedRecords(packet);
                var trailingHex = ExtractTrailingHex(bodyHex, records);

                yield return new Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket(
                    Path.GetFileName(path),
                    ReadInt(packet, "PacketIndex"),
                    ReadInt(packet, "SourceStep"),
                    ReadInt(packet, "Sequence"),
                    ReadInt(packet, "PayloadLength"),
                    ReadInt(packet, "BodyLength"),
                    ReadNullableInt(packet, "EmbeddedPrefixLength") ?? HexToBytes(prefixHex).Length,
                    ReadString(packet, "NativeFrameKind"),
                    prefixHex,
                    bodyHex,
                    trailingHex,
                    records);
            }
        }
    }

    private static string ExtractTrailingHex(
        string bodyHex,
        Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord[] records)
    {
        if (records.Length == 0 || string.IsNullOrWhiteSpace(bodyHex))
        {
            return "";
        }

        var endOffset = records.Max(static record => record.Offset + record.Length);
        var byteLength = HexToBytes(bodyHex).Length;
        if (endOffset >= byteLength)
        {
            return "";
        }

        var start = endOffset * 2;
        return start >= bodyHex.Length ? "" : bodyHex[start..];
    }

    private static Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord[] ReadEmbeddedRecords(JsonElement packet)
    {
        if (!packet.TryGetProperty("EmbeddedRecords", out var records) || records.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return records.EnumerateArray()
            .Select(static record => new Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord(
                ReadString(record, "Marker"),
                ReadInt(record, "Offset"),
                ReadInt(record, "Length"),
                ReadString(record, "Role"),
                ReadString(record, "ObjectId"),
                ReadString(record, "OwnerOrRootObjectId"),
                ReadString(record, "LinkedObjectId"),
                ReadString(record, "HeaderHex")))
            .ToArray();
    }

    private static void AddHexCandidate(
        List<Tf2Ps3SourceGeneratedPrefixScalarCandidate> candidates,
        string name,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !long.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
        {
            return;
        }

        candidates.Add(new Tf2Ps3SourceGeneratedPrefixScalarCandidate(name, parsed));
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length || start >= haystack.Length)
        {
            return -1;
        }

        for (var offset = Math.Max(0, start); offset <= haystack.Length - needle.Length; offset++)
        {
            var matched = true;
            for (var i = 0; i < needle.Length; i++)
            {
                if (haystack[offset + i] != needle[i])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return offset;
            }
        }

        return -1;
    }

    private static byte[] ToBytes(ushort value, bool bigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian == bigEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    private static byte[] ToBytes(uint value, bool bigEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian == bigEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    private static bool LooksLikePlainObjectStreamEnvelope(byte[] prefixBytes)
    {
        return prefixBytes.Length >= 93 && prefixBytes[0] is 0x01 or 0x02;
    }

    private static string TraceKey(string traceFile, int packetIndex, int sourceStep)
    {
        return $"{traceFile}|{packetIndex}|{sourceStep}";
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static double Entropy(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var value in bytes)
        {
            counts[value]++;
        }

        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var probability = count / (double)bytes.Length;
            entropy -= probability * Math.Log2(probability);
        }

        return Math.Round(entropy, 4);
    }

    private static byte[] HexToBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return [];
        }

        var clean = new string(hex.Where(static character => !char.IsWhiteSpace(character)).ToArray());
        if ((clean.Length & 1) != 0)
        {
            return [];
        }

        try
        {
            return Convert.FromHexString(clean);
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static string FirstHex(string hex, int byteCount)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return "";
        }

        return hex[..Math.Min(hex.Length, byteCount * 2)];
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
}

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeReport(
    string Status,
    string Note,
    Tf2Ps3SourceGeneratedPrefixFieldProbeInputs Inputs,
    Tf2Ps3SourceGeneratedPrefixFieldProbeSummary Summary,
    Tf2Ps3SourceGeneratedPrefixFieldProbeTarget[] Targets,
    string[] ProbeRules,
    string[] Conclusions);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeInputs(
    string GeneratedPrefixRetailCrossmap,
    string SemanticTraceDirectory);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeSummary(
    int TargetCount,
    int ExactRetailSampleCount,
    int SamplesWithScalarHits,
    int ScalarHitCount,
    int SamplesWithTailScalarHits,
    int TailScalarHitCount,
    int TargetWithStableBytePositionsCount,
    int TargetWithStableTailBytePositionsCount,
    int NoObviousScalarFieldTargetCount,
    int PlainObjectStreamEnvelopeCandidateCount,
    int ExactSamplePlainObjectStreamEnvelopeMismatchCount,
    int StableBytePositionCount,
    int StableTailBytePositionCount,
    int ExactSampleTrailingByteCount);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeTarget(
    string Family,
    string Method,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int ExactRetailSampleCount,
    int ScalarHitCount,
    int SamplesWithScalarHits,
    int TailScalarHitCount,
    int SamplesWithTailScalarHits,
    int StableBytePositionCount,
    int TailStableBytePositionCount,
    Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun[] StableRuns,
    Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun[] TailStableRuns,
    int CommonPrefixByteCount,
    int CommonSuffixByteCount,
    int CommonTailPrefixByteCount,
    int CommonTailSuffixByteCount,
    int PlainObjectStreamEnvelopeCandidateCount,
    bool NoObviousScalarFields,
    Tf2Ps3SourceGeneratedPrefixFieldProbeSample[] Samples,
    string Conclusion);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeSample(
    string SourceTraceFile,
    int PacketIndex,
    int SourceStep,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    int PrefixByteLength,
    string NativeFrameKind,
    int EmbeddedRecordCount,
    string PrefixHex,
    string PrefixSha256,
    double PrefixEntropy,
    string PrefixFirst16Hex,
    string PrefixLast16Hex,
    string TrailingHex,
    int TrailingByteLength,
    string TrailingSha256,
    double TrailingEntropy,
    string TrailingFirst16Hex,
    string TrailingLast16Hex,
    bool PlainObjectStreamEnvelopeCandidate,
    Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord[] EmbeddedRecords,
    Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit[] ScalarHits,
    Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit[] TailScalarHits,
    bool MatchesTargetShape);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord(
    string Marker,
    int Offset,
    int Length,
    string Role,
    string ObjectId,
    string OwnerOrRootObjectId,
    string LinkedObjectId,
    string HeaderHex);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeScalarHit(
    string FieldName,
    long Value,
    string Encoding,
    int Offset,
    string EncodedHex);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeStableByte(
    int Offset,
    byte Value,
    string Hex);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeStableRun(
    int Offset,
    int Length,
    string Hex);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapTarget(
    string Family,
    string Method,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapSample[] ExactRetailSamples);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeCrossmapSample(
    string SourceTraceFile,
    int PacketIndex,
    int SourceStep);

public sealed record Tf2Ps3SourceGeneratedPrefixFieldProbeTracePacket(
    string SourceTraceFile,
    int PacketIndex,
    int SourceStep,
    int Sequence,
    int PayloadLength,
    int BodyLength,
    int PrefixByteLength,
    string NativeFrameKind,
    string PrefixHex,
    string BodyHex,
    string TrailingHex,
    Tf2Ps3SourceGeneratedPrefixFieldProbeEmbeddedRecord[] EmbeddedRecords);

public sealed record Tf2Ps3SourceGeneratedPrefixScalarCandidate(
    string Name,
    long Value);
