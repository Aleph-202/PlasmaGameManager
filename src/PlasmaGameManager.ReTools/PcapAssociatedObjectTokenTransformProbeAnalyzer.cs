using System.Security.Cryptography;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapAssociatedObjectTokenTransformProbeAnalyzer
{
    private const int MaxSamples = 128;
    private const int MaxInnerOffset = 16;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapAssociatedObjectTokenTransformProbeReport> AnalyzeDirectoryAsync(
        string inputPath,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapAssociatedObjectTokenTransformProbeReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var transformCounts = files
            .SelectMany(static file => file.TransformCounts)
            .GroupBy(static count => count.Transform, StringComparer.Ordinal)
            .Select(static group => new PcapAssociatedObjectTokenTransformProbeCount(
                group.Key,
                group.Sum(static count => count.ProbedPacketCount),
                group.Sum(static count => count.StrictDecodeHitCount),
                group.Sum(static count => count.ClcMoveHitCount),
                group.Sum(static count => count.NetMessageHitCount),
                group.Sum(static count => count.QueueDeltaExactHitCount)))
            .OrderByDescending(static count => count.StrictDecodeHitCount + count.QueueDeltaExactHitCount)
            .ThenByDescending(static count => count.ProbedPacketCount)
            .ThenBy(static count => count.Transform, StringComparer.Ordinal)
            .ToArray();
        var hitSamples = files.SelectMany(static file => file.HitSamples).Take(MaxSamples).ToArray();
        var noHitSamples = files.SelectMany(static file => file.NoHitSamples).Take(MaxSamples).ToArray();
        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var associated = files.Sum(static file => file.AssociatedObjectTokenPacketCount);
        var packetHits = files.Sum(static file => file.PacketWithAcceptedTransformHitCount);
        var strictHits = files.Sum(static file => file.StrictDecodeHitCount);
        var clcHits = files.Sum(static file => file.ClcMoveHitCount);
        var netHits = files.Sum(static file => file.NetMessageHitCount);
        var queueHits = files.Sum(static file => file.QueueDeltaExactHitCount);
        var ready = hardMarkerless > 0 && associated == hardMarkerless && packetHits == hardMarkerless;

        return new PcapAssociatedObjectTokenTransformProbeReport(
            "pcap-associated-object-token-transform-probes",
            "Tests hard markerless TF2 PS3 Source client bodies against transforms keyed by the per-packet associated-object word consumed by TF.elf 008b9ad8/vtable +0xb4. Accepted hits must decode as official CLC_Move, a strong Source client net message, or an exact TF.elf queue-delta stream.",
            new PcapAssociatedObjectTokenTransformProbeSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                associated,
                files.Sum(static file => file.ProbedTransformCount),
                strictHits,
                clcHits,
                netHits,
                queueHits,
                packetHits,
                ready,
                ready
                    ? "every hard markerless associated-object packet accepts a token-derived transform into native TF.elf/server.dll input"
                    : packetHits == 0
                        ? "per-packet associated-object token transforms did not turn hard markerless packets into accepted native Source input"
                        : "associated-object token transform hits are partial; do not promote until one token-derived family covers the hard markerless corpus"),
            transformCounts,
            CountBy(hitSamples.Select(static sample => sample.AcceptedKind)),
            hitSamples,
            noHitSamples,
            files);
    }

    private PcapAssociatedObjectTokenTransformProbeFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapAssociatedObjectTokenTransformProbeFile(relativePath, false, "", "", 0, 0, 0, 0, 0, 0, 0, 0, [], [], []);
        }

        var transformCounts = new Dictionary<string, PcapAssociatedObjectTokenTransformProbeCount>(StringComparer.Ordinal);
        var hitSamples = new List<PcapAssociatedObjectTokenTransformProbeHitSample>();
        var noHitSamples = new List<PcapAssociatedObjectTokenTransformProbeNoHitSample>();
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var associated = 0;
        var probedTransforms = 0;
        var packetHits = 0;
        var strictHits = 0;
        var clcHits = 0;
        var netHits = 0;
        var queueHits = 0;

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
            if (!Ps3SourcePayloadObjectFrame.TryDecode(transport.Body, out var frame)
                || frame is not { Kind: Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken, AssociatedObjectToken: { } token })
            {
                AddNoHitSample(noHitSamples, relativePath, sourceStep, raw, transport, info, null, "hard markerless packet did not decode as associated-object token frame");
                continue;
            }

            associated++;
            var packetHadHit = false;
            var seenTransforms = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in EnumerateTransformCandidates(transport.Body, frame, token, transport.CandidateSequence, sequenceDelta))
            {
                if (!seenTransforms.Add(candidate.Name))
                {
                    continue;
                }

                probedTransforms++;
                if (!TryAcceptedDecode(candidate.Payload, out var acceptedKind, out var detail))
                {
                    AddProbeCount(transformCounts, candidate.Name, strictDecodeHit: false, clcHit: false, netHit: false, queueDeltaExactHit: false);
                    continue;
                }

                packetHadHit = true;
                var clcHit = acceptedKind == "CLC_Move";
                var netHit = acceptedKind == "NET_Message";
                var queueHit = acceptedKind == "TF_ELF_QueueDelta";
                strictHits += queueHit ? 0 : 1;
                clcHits += clcHit ? 1 : 0;
                netHits += netHit ? 1 : 0;
                queueHits += queueHit ? 1 : 0;
                AddProbeCount(transformCounts, candidate.Name, strictDecodeHit: !queueHit, clcHit, netHit, queueDeltaExactHit: queueHit);
                if (hitSamples.Count < MaxSamples)
                {
                    hitSamples.Add(new PcapAssociatedObjectTokenTransformProbeHitSample(
                        relativePath,
                        sourceStep,
                        raw.PacketIndex,
                        raw.TimestampMicroseconds,
                        transport.CandidateSequence,
                        sequenceDelta,
                        transport.PayloadLength,
                        transport.Body.Length,
                        Hex(token),
                        info.BodyPrefixHex,
                        candidate.Name,
                        candidate.PayloadOffset,
                        acceptedKind,
                        detail,
                        Prefix(candidate.Payload, 32)));
                }
            }

            if (packetHadHit)
            {
                packetHits++;
            }
            else
            {
                AddNoHitSample(noHitSamples, relativePath, sourceStep, raw, transport, info, token, "no associated-object-token-derived transform accepted by native decoders");
            }
        }

        return new PcapAssociatedObjectTokenTransformProbeFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            hardMarkerless,
            associated,
            probedTransforms,
            strictHits,
            clcHits,
            netHits,
            queueHits,
            packetHits,
            transformCounts.Values
                .OrderByDescending(static count => count.StrictDecodeHitCount + count.QueueDeltaExactHitCount)
                .ThenByDescending(static count => count.ProbedPacketCount)
                .ThenBy(static count => count.Transform, StringComparer.Ordinal)
                .ToArray(),
            hitSamples.ToArray(),
            noHitSamples.ToArray());
    }

    private static IEnumerable<TokenTransformCandidate> EnumerateTransformCandidates(
        byte[] body,
        Ps3SourcePayloadObjectFrame frame,
        uint token,
        ushort sequence,
        int? sequenceDelta)
    {
        var inner = frame.SliceInnerPayload(body).ToArray();
        if (inner.Length == 0)
        {
            yield break;
        }

        var tokenBe = new[]
        {
            (byte)(token >> 24),
            (byte)(token >> 16),
            (byte)(token >> 8),
            (byte)token
        };
        var tokenLe = tokenBe.Reverse().ToArray();
        var sequenceBe = new[] { (byte)(sequence >> 8), (byte)sequence };
        var sequenceLe = new[] { (byte)sequence, (byte)(sequence >> 8) };

        yield return new TokenTransformCandidate("strip-token:identity", frame.InnerPayloadOffset, inner);
        yield return new TokenTransformCandidate("strip-token:xor-token-be", frame.InnerPayloadOffset, XorRepeat(inner, tokenBe));
        yield return new TokenTransformCandidate("strip-token:xor-token-le", frame.InnerPayloadOffset, XorRepeat(inner, tokenLe));
        yield return new TokenTransformCandidate("strip-token:add-token-be", frame.InnerPayloadOffset, AddRepeat(inner, tokenBe));
        yield return new TokenTransformCandidate("strip-token:add-token-le", frame.InnerPayloadOffset, AddRepeat(inner, tokenLe));
        yield return new TokenTransformCandidate("strip-token:sub-token-be", frame.InnerPayloadOffset, SubtractRepeat(inner, tokenBe));
        yield return new TokenTransformCandidate("strip-token:sub-token-le", frame.InnerPayloadOffset, SubtractRepeat(inner, tokenLe));
        yield return new TokenTransformCandidate("strip-token:xor-token-seq-be", frame.InnerPayloadOffset, XorRepeat(inner, Combine(tokenBe, sequenceBe)));
        yield return new TokenTransformCandidate("strip-token:xor-token-seq-le", frame.InnerPayloadOffset, XorRepeat(inner, Combine(tokenLe, sequenceLe)));
        yield return new TokenTransformCandidate("strip-token:sha1-token-stream", frame.InnerPayloadOffset, XorDigestStream(inner, tokenBe, "sha1"));
        yield return new TokenTransformCandidate("strip-token:md5-token-stream", frame.InnerPayloadOffset, XorDigestStream(inner, tokenBe, "md5"));
        if (sequenceDelta is { } delta)
        {
            yield return new TokenTransformCandidate("strip-token:xor-token-delta", frame.InnerPayloadOffset, XorRepeat(inner, Combine(tokenBe, [(byte)delta])));
            yield return new TokenTransformCandidate("strip-token:sha1-token-delta-stream", frame.InnerPayloadOffset, XorDigestStream(inner, Combine(tokenBe, [(byte)delta]), "sha1"));
        }

        var maxOffset = Math.Min(MaxInnerOffset, Math.Max(0, inner.Length - 1));
        for (var offset = 1; offset <= maxOffset; offset++)
        {
            var slice = inner[offset..];
            yield return new TokenTransformCandidate($"strip-token-skip-{offset}:identity", frame.InnerPayloadOffset + offset, slice);
            yield return new TokenTransformCandidate($"strip-token-skip-{offset}:xor-token-be", frame.InnerPayloadOffset + offset, XorRepeat(slice, tokenBe));
            if (offset <= 4)
            {
                yield return new TokenTransformCandidate($"strip-token-skip-{offset}:sha1-token-stream", frame.InnerPayloadOffset + offset, XorDigestStream(slice, tokenBe, "sha1"));
            }
        }
    }

    private static bool TryAcceptedDecode(ReadOnlySpan<byte> payload, out string acceptedKind, out string detail)
    {
        acceptedKind = "";
        detail = "";
        if (TryStrictClcMove(payload, out detail))
        {
            acceptedKind = "CLC_Move";
            return true;
        }

        if (Ps3SourceClientNetMessageDecoder.TryDecode(payload, out var netMessage))
        {
            var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(
                netMessage,
                payload.Slice(netMessage.PayloadOffset, netMessage.PayloadLength));
            if (strength.IsStrong)
            {
                acceptedKind = "NET_Message";
                detail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits}/{netMessage.PayloadBitCount}; {strength.Reason}";
                return true;
            }
        }

        if (payload.Length > 0 && payload.Length % 0x58 == 0)
        {
            var recordProbe = TfElfQueueDeltaProbe.TryDecode(payload, payload.Length / 0x58);
            if (recordProbe.Status == "exact-zero-trailing")
            {
                acceptedKind = "TF_ELF_QueueDelta";
                detail = $"{recordProbe.RecordCount} records; consumed={recordProbe.ConsumedBits}; {recordProbe.Detail}";
                return true;
            }
        }

        if (payload.Length == 0x58)
        {
            var recordProbe = TfElfQueueDeltaProbe.TryDecode(payload, 1);
            if (recordProbe.Status == "exact-zero-trailing")
            {
                acceptedKind = "TF_ELF_QueueDelta";
                detail = $"single record; consumed={recordProbe.ConsumedBits}; {recordProbe.Detail}";
                return true;
            }
        }

        return false;
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
        Dictionary<string, PcapAssociatedObjectTokenTransformProbeCount> counts,
        string transform,
        bool strictDecodeHit,
        bool clcHit,
        bool netHit,
        bool queueDeltaExactHit)
    {
        counts.TryGetValue(transform, out var existing);
        existing ??= new PcapAssociatedObjectTokenTransformProbeCount(transform, 0, 0, 0, 0, 0);
        counts[transform] = existing with
        {
            ProbedPacketCount = existing.ProbedPacketCount + 1,
            StrictDecodeHitCount = existing.StrictDecodeHitCount + (strictDecodeHit ? 1 : 0),
            ClcMoveHitCount = existing.ClcMoveHitCount + (clcHit ? 1 : 0),
            NetMessageHitCount = existing.NetMessageHitCount + (netHit ? 1 : 0),
            QueueDeltaExactHitCount = existing.QueueDeltaExactHitCount + (queueDeltaExactHit ? 1 : 0)
        };
    }

    private static void AddNoHitSample(
        List<PcapAssociatedObjectTokenTransformProbeNoHitSample> samples,
        string file,
        int sourceStep,
        PcapActiveFlowDatagram raw,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info,
        uint? token,
        string reason)
    {
        if (samples.Count >= MaxSamples)
        {
            return;
        }

        samples.Add(new PcapAssociatedObjectTokenTransformProbeNoHitSample(
            file,
            sourceStep,
            raw.PacketIndex,
            raw.TimestampMicroseconds,
            transport.CandidateSequence,
            info.SequenceDelta,
            transport.PayloadLength,
            transport.Body.Length,
            token is { } value ? Hex(value) : "",
            info.Role.ToString(),
            Prefix(transport.Body, 32),
            reason));
    }

    private static byte[] XorRepeat(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            return body.ToArray();
        }

        var output = new byte[body.Length];
        for (var index = 0; index < body.Length; index++)
        {
            output[index] = (byte)(body[index] ^ key[index % key.Length]);
        }

        return output;
    }

    private static byte[] AddRepeat(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            return body.ToArray();
        }

        var output = new byte[body.Length];
        for (var index = 0; index < body.Length; index++)
        {
            output[index] = unchecked((byte)(body[index] + key[index % key.Length]));
        }

        return output;
    }

    private static byte[] SubtractRepeat(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        if (key.IsEmpty)
        {
            return body.ToArray();
        }

        var output = new byte[body.Length];
        for (var index = 0; index < body.Length; index++)
        {
            output[index] = unchecked((byte)(body[index] - key[index % key.Length]));
        }

        return output;
    }

    private static byte[] XorDigestStream(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key, string digest)
    {
        var output = new byte[body.Length];
        var offset = 0;
        var counter = 0u;
        while (offset < body.Length)
        {
            var input = new byte[key.Length + 4];
            key.CopyTo(input);
            input[^4] = (byte)(counter >> 24);
            input[^3] = (byte)(counter >> 16);
            input[^2] = (byte)(counter >> 8);
            input[^1] = (byte)counter;
            var block = digest == "md5" ? MD5.HashData(input) : SHA1.HashData(input);
            var count = Math.Min(block.Length, body.Length - offset);
            for (var index = 0; index < count; index++)
            {
                output[offset + index] = (byte)(body[offset + index] ^ block[index]);
            }

            offset += count;
            counter++;
        }

        return output;
    }

    private static byte[] Combine(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var output = new byte[first.Length + second.Length];
        first.CopyTo(output);
        second.CopyTo(output.AsSpan(first.Length));
        return output;
    }

    private static PcapAssociatedObjectTokenTransformProbeCount[] CountBy(IEnumerable<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapAssociatedObjectTokenTransformProbeCount(group.Key, group.Count(), 0, 0, 0, 0))
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

    private static string Hex(uint value) => $"0x{value:x8}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

internal readonly record struct TokenTransformCandidate(
    string Name,
    int PayloadOffset,
    byte[] Payload);

public sealed record PcapAssociatedObjectTokenTransformProbeReport(
    string Status,
    string Note,
    PcapAssociatedObjectTokenTransformProbeSummary Summary,
    PcapAssociatedObjectTokenTransformProbeCount[] TransformCounts,
    PcapAssociatedObjectTokenTransformProbeCount[] AcceptedKindCounts,
    PcapAssociatedObjectTokenTransformProbeHitSample[] HitSamples,
    PcapAssociatedObjectTokenTransformProbeNoHitSample[] NoHitSamples,
    PcapAssociatedObjectTokenTransformProbeFile[] Files);

public sealed record PcapAssociatedObjectTokenTransformProbeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int AssociatedObjectTokenPacketCount,
    int ProbedTransformCount,
    int StrictDecodeHitCount,
    int ClcMoveHitCount,
    int NetMessageHitCount,
    int QueueDeltaExactHitCount,
    int PacketWithAcceptedTransformHitCount,
    bool NativeAssociatedObjectTokenTransformReady,
    string Conclusion);

public sealed record PcapAssociatedObjectTokenTransformProbeFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int AssociatedObjectTokenPacketCount,
    int ProbedTransformCount,
    int StrictDecodeHitCount,
    int ClcMoveHitCount,
    int NetMessageHitCount,
    int QueueDeltaExactHitCount,
    int PacketWithAcceptedTransformHitCount,
    PcapAssociatedObjectTokenTransformProbeCount[] TransformCounts,
    PcapAssociatedObjectTokenTransformProbeHitSample[] HitSamples,
    PcapAssociatedObjectTokenTransformProbeNoHitSample[] NoHitSamples);

public sealed record PcapAssociatedObjectTokenTransformProbeCount(
    string Transform,
    int ProbedPacketCount,
    int StrictDecodeHitCount,
    int ClcMoveHitCount,
    int NetMessageHitCount,
    int QueueDeltaExactHitCount);

public sealed record PcapAssociatedObjectTokenTransformProbeHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string TokenHex,
    string BodyPrefixHex,
    string Transform,
    int PayloadOffset,
    string AcceptedKind,
    string Detail,
    string TransformedPrefix32Hex);

public sealed record PcapAssociatedObjectTokenTransformProbeNoHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string TokenHex,
    string Role,
    string BodyPrefix32Hex,
    string Reason);
