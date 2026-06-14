using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapMarkerlessTransformProbeAnalyzer
{
    private const int MaxOffset = 32;
    private const int MaxSamples = 128;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapMarkerlessTransformProbeReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapMarkerlessTransformProbeReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var transformCounts = MergeTransformCounts(files.SelectMany(static file => file.TransformProbeCounts));
        var hitSamples = files.SelectMany(static file => file.HitSamples).Take(MaxSamples).ToArray();
        var noHitSamples = files.SelectMany(static file => file.NoHitSamples).Take(MaxSamples).ToArray();
        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var packetHits = files.Sum(static file => file.PacketWithStrictTransformHitCount);
        var strictHits = files.Sum(static file => file.StrictTransformHitCount);
        var clcHits = files.Sum(static file => file.ClcMoveTransformHitCount);
        var netHits = files.Sum(static file => file.NetMessageTransformHitCount);
        var nativeReady = hardMarkerless > 0 && packetHits == hardMarkerless;

        return new PcapMarkerlessTransformProbeReport(
            "pcap-markerless-transform-probes-strict",
            "Strict transform probe for hard markerless TF2 PS3 Source client bodies. Hits require official CLC_Move/usercmd decode or a strong known Source client net-message decode after the transform.",
            new PcapMarkerlessTransformProbeSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                strictHits,
                packetHits,
                clcHits,
                netHits,
                transformCounts.Length,
                nativeReady,
                nativeReady
                    ? "every hard markerless body has a strict shallow transform decode"
                    : strictHits == 0
                        ? "no shallow byte/bit/endian/xor transform turns hard markerless bodies into official CLC_Move or strong Source net-message payloads"
                        : "partial shallow transform hits exist; inspect samples before treating any transform as native"),
            transformCounts,
            CountBy(hitSamples.Select(static hit => hit.Transform)),
            CountBy(hitSamples.Select(static hit => hit.Kind)),
            hitSamples,
            noHitSamples,
            files);
    }

    private PcapMarkerlessTransformProbeFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapMarkerlessTransformProbeFile(relativePath, false, "", "", 0, 0, 0, 0, 0, [], [], []);
        }

        var transformCounts = new Dictionary<string, PcapMarkerlessTransformProbeCount>(StringComparer.Ordinal);
        var hitSamples = new List<PcapMarkerlessTransformProbeHitSample>();
        var noHitSamples = new List<PcapMarkerlessTransformProbeNoHitSample>();
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var packetHits = 0;
        var strictHits = 0;
        var clcHits = 0;
        var netHits = 0;

        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var raw = replay.SourcePackets[sourceStep];
            if (raw.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(raw.Payload, out var transport))
            {
                continue;
            }

            clientDirectionCount++;
            int? sequenceDelta = previousSequence is null
                ? null
                : Ps3SourceTransportPacket.SequenceDelta(previousSequence.Value, transport.CandidateSequence);
            previousSequence = transport.CandidateSequence;

            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientDirectionCount, sequenceDelta);
            if (!IsHardMarkerless(info))
            {
                continue;
            }

            hardMarkerless++;
            var packetHadHit = false;
            foreach (var candidate in EnumerateTransformCandidates(transport.CandidateSequence, sequenceDelta, transport.Body))
            {
                AddProbeCount(transformCounts, candidate.Name, strictHit: false);
                if (!TryStrictDecode(candidate.Payload, out var kind, out var detail))
                {
                    continue;
                }

                packetHadHit = true;
                strictHits++;
                if (kind == "CLC_Move")
                {
                    clcHits++;
                }
                else if (kind == "NET_Message")
                {
                    netHits++;
                }

                AddProbeCount(transformCounts, candidate.Name, strictHit: true);
                if (hitSamples.Count < MaxSamples)
                {
                    hitSamples.Add(new PcapMarkerlessTransformProbeHitSample(
                        relativePath,
                        sourceStep,
                        raw.PacketIndex,
                        raw.TimestampMicroseconds,
                        transport.CandidateSequence,
                        sequenceDelta,
                        transport.PayloadLength,
                        transport.Body.Length,
                        info.BodyPrefixHex,
                        Prefix(transport.Body, 32),
                        candidate.Name,
                        candidate.PayloadOffset,
                        kind,
                        detail,
                        Prefix(candidate.Payload, 32)));
                }
            }

            if (packetHadHit)
            {
                packetHits++;
            }
            else if (noHitSamples.Count < MaxSamples)
            {
                noHitSamples.Add(new PcapMarkerlessTransformProbeNoHitSample(
                    relativePath,
                    sourceStep,
                    raw.PacketIndex,
                    raw.TimestampMicroseconds,
                    transport.CandidateSequence,
                    sequenceDelta,
                    transport.PayloadLength,
                    transport.Body.Length,
                    info.BodyPrefixHex,
                    Prefix(transport.Body, 32)));
            }
        }

        return new PcapMarkerlessTransformProbeFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            hardMarkerless,
            packetHits,
            strictHits,
            clcHits,
            netHits,
            transformCounts.Values
                .OrderByDescending(static count => count.StrictHitCount)
                .ThenByDescending(static count => count.ProbedPacketCount)
                .ThenBy(static count => count.Transform, StringComparer.Ordinal)
                .ToArray(),
            hitSamples.ToArray(),
            noHitSamples.ToArray());
    }

    private static IEnumerable<TransformCandidate> EnumerateTransformCandidates(
        ushort sequence,
        int? sequenceDelta,
        byte[] body)
    {
        for (var offset = 0; offset <= Math.Min(MaxOffset, body.Length - 1); offset++)
        {
            var label = offset == 0 ? "identity" : $"skip-{offset}";
            var slice = SliceToArray(body, offset);
            yield return new TransformCandidate(label, offset, slice);
            yield return new TransformCandidate($"{label}+xor-ff", offset, Xor(slice, 0xff));
            yield return new TransformCandidate($"{label}+bitrev8", offset, BitReverseBytes(slice));
            yield return new TransformCandidate($"{label}+nibbleswap", offset, NibbleSwapBytes(slice));
            yield return new TransformCandidate($"{label}+swap16", offset, SwapWords(slice, 2));
            yield return new TransformCandidate($"{label}+swap32", offset, SwapWords(slice, 4));

            if (offset <= 4)
            {
                for (var bitShift = 1; bitShift <= 7; bitShift++)
                {
                    yield return new TransformCandidate($"{label}+bitshift-right-{bitShift}", offset, ShiftBitsRight(slice, bitShift));
                    yield return new TransformCandidate($"{label}+bitshift-left-{bitShift}", offset, ShiftBitsLeft(slice, bitShift));
                }
            }
        }

        var sequenceHigh = (byte)(sequence >> 8);
        var sequenceLow = (byte)sequence;
        yield return new TransformCandidate("xor-sequence-high", 0, Xor(body, sequenceHigh));
        yield return new TransformCandidate("xor-sequence-low", 0, Xor(body, sequenceLow));
        yield return new TransformCandidate("xor-sequence-alternating-high-low", 0, XorAlternating(body, sequenceHigh, sequenceLow));
        if (sequenceDelta is { } delta)
        {
            yield return new TransformCandidate("xor-sequence-delta-low", 0, Xor(body, (byte)delta));
        }

        for (var key = 0; key <= 0xff; key++)
        {
            yield return new TransformCandidate($"xor-constant-{key:x2}", 0, Xor(body, (byte)key));
        }
    }

    private static bool TryStrictDecode(ReadOnlySpan<byte> payload, out string kind, out string detail)
    {
        kind = "";
        detail = "";
        if (TryStrictClcMove(payload, out detail))
        {
            kind = "CLC_Move";
            return true;
        }

        if (!Ps3SourceClientNetMessageDecoder.TryDecode(payload, out var netMessage))
        {
            return false;
        }

        var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(
            netMessage,
            payload.Slice(netMessage.PayloadOffset, netMessage.PayloadLength));
        if (!strength.IsStrong)
        {
            return false;
        }

        kind = "NET_Message";
        detail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits}/{netMessage.PayloadBitCount}; {strength.Reason}";
        return true;
    }

    private static bool TryStrictClcMove(ReadOnlySpan<byte> payload, out string detail)
    {
        detail = "";
        foreach (var includesMessageType in new[] { true, false })
        {
            if (!Ps3SourceClcMoveMessage.TryDecode(payload, includesMessageType, out var move)
                || move.TotalCommands <= 0
                || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
                || move.TotalBitsConsumed > payload.Length * 8
                || !HasOnlyZeroBits(payload, move.TotalBitsConsumed, payload.Length * 8)
                || !move.TryDecodeUserCmdBatch(default, out var batch)
                || batch.ConsumedBits != move.CommandDataBitCount)
            {
                continue;
            }

            detail = $"{(includesMessageType ? "with-type" : "without-type")}: new={move.NewCommands} backup={move.BackupCommands} cmdBits={move.CommandDataBitCount} consumed={move.TotalBitsConsumed}/{payload.Length * 8}";
            return true;
        }

        return false;
    }

    private static bool HasOnlyZeroBits(ReadOnlySpan<byte> payload, int startBit, int endBit)
    {
        if (startBit < 0 || endBit < startBit || endBit > payload.Length * 8)
        {
            return false;
        }

        for (var bit = startBit; bit < endBit; bit++)
        {
            if (((payload[bit >> 3] >> (bit & 7)) & 1) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHardMarkerless(Ps3SourceClientPayloadInfo info)
    {
        if (info.AttachedFrameKind is not null
            || info.BitSidecarOffset is not null
            || info.DecodedNetMessageType is not null
            || info.Role is Ps3SourceClientPayloadRole.InitialHandoffProbe
                or Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.ShortControlAck
                or Ps3SourceClientPayloadRole.SetupControlPayload
                or Ps3SourceClientPayloadRole.EmbeddedObjectNotice
                or Ps3SourceClientPayloadRole.FragmentedClientPayload)
        {
            return false;
        }

        return info.Role == Ps3SourceClientPayloadRole.UserCommandCandidate
            || info.Role == Ps3SourceClientPayloadRole.BinaryControlPayload && info.BodyLength >= 32;
    }

    private static void AddProbeCount(
        Dictionary<string, PcapMarkerlessTransformProbeCount> counts,
        string transform,
        bool strictHit)
    {
        counts.TryGetValue(transform, out var existing);
        existing ??= new PcapMarkerlessTransformProbeCount(transform, 0, 0);
        counts[transform] = existing with
        {
            ProbedPacketCount = existing.ProbedPacketCount + (strictHit ? 0 : 1),
            StrictHitCount = existing.StrictHitCount + (strictHit ? 1 : 0)
        };
    }

    private static PcapMarkerlessTransformProbeCount[] MergeTransformCounts(IEnumerable<PcapMarkerlessTransformProbeCount> counts)
    {
        return counts
            .GroupBy(static count => count.Transform, StringComparer.Ordinal)
            .Select(static group => new PcapMarkerlessTransformProbeCount(
                group.Key,
                group.Sum(static count => count.ProbedPacketCount),
                group.Sum(static count => count.StrictHitCount)))
            .OrderByDescending(static count => count.StrictHitCount)
            .ThenByDescending(static count => count.ProbedPacketCount)
            .ThenBy(static count => count.Transform, StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] Xor(ReadOnlySpan<byte> input, byte key)
    {
        var result = input.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            result[i] ^= key;
        }

        return result;
    }

    private static byte[] XorAlternating(ReadOnlySpan<byte> input, byte evenKey, byte oddKey)
    {
        var result = input.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            result[i] ^= (i & 1) == 0 ? evenKey : oddKey;
        }

        return result;
    }

    private static byte[] BitReverseBytes(ReadOnlySpan<byte> input)
    {
        var result = input.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = ReverseBits(result[i]);
        }

        return result;
    }

    private static byte ReverseBits(byte value)
    {
        value = (byte)(((value & 0xf0) >> 4) | ((value & 0x0f) << 4));
        value = (byte)(((value & 0xcc) >> 2) | ((value & 0x33) << 2));
        value = (byte)(((value & 0xaa) >> 1) | ((value & 0x55) << 1));
        return value;
    }

    private static byte[] NibbleSwapBytes(ReadOnlySpan<byte> input)
    {
        var result = input.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (byte)((result[i] << 4) | (result[i] >> 4));
        }

        return result;
    }

    private static byte[] SwapWords(ReadOnlySpan<byte> input, int wordSize)
    {
        var result = input.ToArray();
        for (var offset = 0; offset + wordSize <= result.Length; offset += wordSize)
        {
            Array.Reverse(result, offset, wordSize);
        }

        return result;
    }

    private static byte[] ShiftBitsRight(ReadOnlySpan<byte> input, int bitShift)
    {
        var result = new byte[input.Length];
        for (var bit = bitShift; bit < input.Length * 8; bit++)
        {
            if (((input[bit >> 3] >> (bit & 7)) & 1) == 0)
            {
                continue;
            }

            var target = bit - bitShift;
            result[target >> 3] |= (byte)(1 << (target & 7));
        }

        return result;
    }

    private static byte[] ShiftBitsLeft(ReadOnlySpan<byte> input, int bitShift)
    {
        var result = new byte[input.Length];
        for (var bit = 0; bit < input.Length * 8 - bitShift; bit++)
        {
            if (((input[bit >> 3] >> (bit & 7)) & 1) == 0)
            {
                continue;
            }

            var target = bit + bitShift;
            result[target >> 3] |= (byte)(1 << (target & 7));
        }

        return result;
    }

    private static byte[] SliceToArray(byte[] input, int offset)
    {
        var length = Math.Max(0, input.Length - offset);
        var result = new byte[length];
        Array.Copy(input, offset, result, 0, length);
        return result;
    }

    private static PcapMarkerlessTransformValueCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapMarkerlessTransformValueCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string[] EnumeratePcapInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return [inputPath];
        }

        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException(inputPath);
        }

        return Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string DisplayPath(string inputPath, string file)
    {
        return Directory.Exists(inputPath)
            ? Path.GetRelativePath(inputPath, file)
            : Path.GetFileName(file);
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record TransformCandidate(
        string Name,
        int PayloadOffset,
        byte[] Payload);
}

public sealed record PcapMarkerlessTransformProbeReport(
    string Status,
    string Note,
    PcapMarkerlessTransformProbeSummary Summary,
    PcapMarkerlessTransformProbeCount[] TransformProbeCounts,
    PcapMarkerlessTransformValueCount[] HitTransformCounts,
    PcapMarkerlessTransformValueCount[] HitKindCounts,
    PcapMarkerlessTransformProbeHitSample[] HitSamples,
    PcapMarkerlessTransformProbeNoHitSample[] NoHitSamples,
    PcapMarkerlessTransformProbeFile[] Files);

public sealed record PcapMarkerlessTransformProbeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int StrictTransformHitCount,
    int PacketWithStrictTransformHitCount,
    int ClcMoveTransformHitCount,
    int NetMessageTransformHitCount,
    int TransformKindCount,
    bool NativeMarkerlessTransformReady,
    string Conclusion);

public sealed record PcapMarkerlessTransformProbeFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int PacketWithStrictTransformHitCount,
    int StrictTransformHitCount,
    int ClcMoveTransformHitCount,
    int NetMessageTransformHitCount,
    PcapMarkerlessTransformProbeCount[] TransformProbeCounts,
    PcapMarkerlessTransformProbeHitSample[] HitSamples,
    PcapMarkerlessTransformProbeNoHitSample[] NoHitSamples);

public sealed record PcapMarkerlessTransformProbeHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string BodyPrefixHex,
    string BodyPrefix32Hex,
    string Transform,
    int PayloadOffset,
    string Kind,
    string Detail,
    string TransformedPrefix32Hex);

public sealed record PcapMarkerlessTransformProbeNoHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string BodyPrefixHex,
    string BodyPrefix32Hex);

public sealed record PcapMarkerlessTransformProbeCount(
    string Transform,
    int ProbedPacketCount,
    int StrictHitCount);

public sealed record PcapMarkerlessTransformValueCount(
    string Value,
    int Count);
