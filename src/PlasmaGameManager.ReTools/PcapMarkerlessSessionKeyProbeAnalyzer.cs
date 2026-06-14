using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapMarkerlessSessionKeyProbeAnalyzer
{
    private const int MaxSamples = 96;
    private const int MaxKeyedSliceOffset = 16;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapMarkerlessSessionKeyProbeReport> AnalyzeDirectoryAsync(
        string inputPath,
        string eaTextSummaryPath,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputPath, eaTextSummaryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapMarkerlessSessionKeyProbeReport AnalyzeDirectory(string inputPath, string eaTextSummaryPath)
    {
        var keyIndex = PcapSessionKeyIndex.Load(eaTextSummaryPath);
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file, keyIndex))
            .ToArray();
        var hitSamples = files.SelectMany(static file => file.HitSamples).Take(MaxSamples).ToArray();
        var noHitSamples = files.SelectMany(static file => file.NoHitSamples).Take(MaxSamples).ToArray();
        var transformCounts = files
            .SelectMany(static file => file.TransformCounts)
            .GroupBy(static count => count.Transform, StringComparer.Ordinal)
            .Select(static group => new PcapMarkerlessSessionKeyProbeTransformCount(
                group.Key,
                group.Sum(static count => count.ProbedPacketCount),
                group.Sum(static count => count.StrictDecodeHitCount),
                group.Sum(static count => count.QueueDeltaExactHitCount)))
            .OrderByDescending(static count => count.StrictDecodeHitCount + count.QueueDeltaExactHitCount)
            .ThenByDescending(static count => count.ProbedPacketCount)
            .ThenBy(static count => count.Transform, StringComparer.Ordinal)
            .ToArray();
        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var keyedPackets = files.Sum(static file => file.PacketsWithSessionKeys);
        var packetHits = files.Sum(static file => file.PacketWithAcceptedTransformHitCount);
        var strictHits = files.Sum(static file => file.StrictDecodeHitCount);
        var clcHits = files.Sum(static file => file.ClcMoveHitCount);
        var netHits = files.Sum(static file => file.NetMessageHitCount);
        var queueHits = files.Sum(static file => file.QueueDeltaExactHitCount);
        var nativeReady = hardMarkerless > 0 && packetHits == hardMarkerless;

        return new PcapMarkerlessSessionKeyProbeReport(
            "pcap-markerless-session-key-probes",
            "Tests hard markerless TF2 PS3 Source client bodies against per-capture EKEY/LKEY/TICKET session material. Accepted hits must decode as official CLC/NET or as TF.elf queue-delta streams with zero trailing bits.",
            new PcapMarkerlessSessionKeyProbeSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                keyIndex.FileKeySetCount,
                files.Sum(static file => file.SessionKeySetCount),
                hardMarkerless,
                keyedPackets,
                files.Sum(static file => file.ProbedTransformCount),
                strictHits,
                clcHits,
                netHits,
                queueHits,
                packetHits,
                nativeReady,
                nativeReady
                    ? "every hard markerless client packet accepted a session-key transform into a native TF.elf/server.dll decode"
                    : packetHits == 0
                        ? "EKEY/LKEY/TICKET-derived repeated-XOR, RC4, and SHA stream probes did not turn hard markerless packets into accepted native Source input"
                        : "session-key transform hits are partial; do not promote until one keyed transform family covers the hard markerless corpus"),
            transformCounts,
            CountBy(hitSamples.Select(static sample => sample.AcceptedKind)),
            hitSamples,
            noHitSamples,
            files);
    }

    private PcapMarkerlessSessionKeyProbeFile AnalyzeFile(
        string inputPath,
        string file,
        PcapSessionKeyIndex keyIndex)
    {
        var relativePath = DisplayPath(inputPath, file);
        var canonical = CanonicalRelativeFile(relativePath);
        var replay = _extractor.Extract(file);
        var keySets = keyIndex.GetKeys(canonical);
        if (replay is null)
        {
            return new PcapMarkerlessSessionKeyProbeFile(relativePath, canonical, false, "", "", keySets.Length, 0, 0, 0, 0, 0, 0, 0, 0, [], [], []);
        }

        var transformCounts = new Dictionary<string, PcapMarkerlessSessionKeyProbeTransformCount>(StringComparer.Ordinal);
        var hitSamples = new List<PcapMarkerlessSessionKeyProbeHitSample>();
        var noHitSamples = new List<PcapMarkerlessSessionKeyProbeNoHitSample>();
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var keyedPackets = 0;
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
            if (keySets.Length == 0)
            {
                AddNoHitSample(noHitSamples, relativePath, sourceStep, raw, transport, info, "no session keys in EA text summary for this capture");
                continue;
            }

            keyedPackets++;
            var packetHadHit = false;
            var seenTransforms = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in EnumerateTransformCandidates(keySets, transport.CandidateSequence, sequenceDelta, transport.Body))
            {
                if (!seenTransforms.Add(candidate.Name))
                {
                    continue;
                }

                probedTransforms++;
                AddProbeCount(transformCounts, candidate.Name, strictDecodeHit: false, queueDeltaExactHit: false);
                if (!TryAcceptedDecode(candidate.Payload, out var acceptedKind, out var detail))
                {
                    continue;
                }

                packetHadHit = true;
                if (acceptedKind == "TF_ELF_QueueDelta")
                {
                    queueHits++;
                    AddProbeCount(transformCounts, candidate.Name, strictDecodeHit: false, queueDeltaExactHit: true);
                }
                else
                {
                    strictHits++;
                    if (acceptedKind == "CLC_Move")
                    {
                        clcHits++;
                    }
                    else if (acceptedKind == "NET_Message")
                    {
                        netHits++;
                    }

                    AddProbeCount(transformCounts, candidate.Name, strictDecodeHit: true, queueDeltaExactHit: false);
                }

                if (hitSamples.Count < MaxSamples)
                {
                    hitSamples.Add(new PcapMarkerlessSessionKeyProbeHitSample(
                        relativePath,
                        sourceStep,
                        raw.PacketIndex,
                        raw.TimestampMicroseconds,
                        transport.CandidateSequence,
                        sequenceDelta,
                        transport.PayloadLength,
                        transport.Body.Length,
                        info.Role.ToString(),
                        Prefix(transport.Body, 32),
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
                AddNoHitSample(noHitSamples, relativePath, sourceStep, raw, transport, info, "no EKEY/LKEY/TICKET-derived transform accepted by native decoders");
            }
        }

        return new PcapMarkerlessSessionKeyProbeFile(
            relativePath,
            canonical,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            keySets.Length,
            hardMarkerless,
            keyedPackets,
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

    private static IEnumerable<SessionKeyTransformCandidate> EnumerateTransformCandidates(
        PcapSessionKeySet[] keySets,
        ushort sequence,
        int? sequenceDelta,
        byte[] body)
    {
        foreach (var keySet in keySets)
        {
            foreach (var key in keySet.Keys)
            {
                if (key.Bytes.Length == 0)
                {
                    continue;
                }

                var keyLabel = $"{key.Field}:{key.Kind}:{key.FingerprintHex}";
                yield return new SessionKeyTransformCandidate($"{keyLabel}:xor-repeat", 0, XorRepeat(body, key.Bytes));
                yield return new SessionKeyTransformCandidate($"{keyLabel}:rc4", 0, Rc4(body, key.Bytes));
                yield return new SessionKeyTransformCandidate($"{keyLabel}:sha1-stream", 0, XorDigestStream(body, key.Bytes, "sha1"));
                yield return new SessionKeyTransformCandidate($"{keyLabel}:md5-stream", 0, XorDigestStream(body, key.Bytes, "md5"));
                yield return new SessionKeyTransformCandidate($"{keyLabel}:sha1-seq-be", 0, XorDigestStream(body, Combine(key.Bytes, [(byte)(sequence >> 8), (byte)sequence]), "sha1"));
                yield return new SessionKeyTransformCandidate($"{keyLabel}:sha1-seq-le", 0, XorDigestStream(body, Combine(key.Bytes, [(byte)sequence, (byte)(sequence >> 8)]), "sha1"));
                if (sequenceDelta is { } delta)
                {
                    yield return new SessionKeyTransformCandidate($"{keyLabel}:sha1-delta", 0, XorDigestStream(body, Combine(key.Bytes, [(byte)delta]), "sha1"));
                }

                var maxOffset = Math.Min(MaxKeyedSliceOffset, body.Length - 1);
                for (var offset = 1; offset <= maxOffset; offset++)
                {
                    var slice = body.AsSpan(offset).ToArray();
                    yield return new SessionKeyTransformCandidate($"{keyLabel}:skip-{offset}:xor-repeat", offset, XorRepeat(slice, key.Bytes));
                    if (offset <= 4)
                    {
                        yield return new SessionKeyTransformCandidate($"{keyLabel}:skip-{offset}:rc4", offset, Rc4(slice, key.Bytes));
                    }
                }
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
        Dictionary<string, PcapMarkerlessSessionKeyProbeTransformCount> counts,
        string transform,
        bool strictDecodeHit,
        bool queueDeltaExactHit)
    {
        counts.TryGetValue(transform, out var existing);
        existing ??= new PcapMarkerlessSessionKeyProbeTransformCount(transform, 0, 0, 0);
        counts[transform] = existing with
        {
            ProbedPacketCount = existing.ProbedPacketCount + (strictDecodeHit || queueDeltaExactHit ? 0 : 1),
            StrictDecodeHitCount = existing.StrictDecodeHitCount + (strictDecodeHit ? 1 : 0),
            QueueDeltaExactHitCount = existing.QueueDeltaExactHitCount + (queueDeltaExactHit ? 1 : 0)
        };
    }

    private static void AddNoHitSample(
        List<PcapMarkerlessSessionKeyProbeNoHitSample> samples,
        string file,
        int sourceStep,
        PcapActiveFlowDatagram raw,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info,
        string reason)
    {
        if (samples.Count >= MaxSamples)
        {
            return;
        }

        samples.Add(new PcapMarkerlessSessionKeyProbeNoHitSample(
            file,
            sourceStep,
            raw.PacketIndex,
            raw.TimestampMicroseconds,
            transport.CandidateSequence,
            info.SequenceDelta,
            transport.PayloadLength,
            transport.Body.Length,
            info.Role.ToString(),
            Prefix(transport.Body, 32),
            reason));
    }

    private static byte[] XorRepeat(ReadOnlySpan<byte> input, ReadOnlySpan<byte> key)
    {
        var result = input.ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            result[i] ^= key[i % key.Length];
        }

        return result;
    }

    private static byte[] Rc4(ReadOnlySpan<byte> input, ReadOnlySpan<byte> key)
    {
        Span<byte> s = stackalloc byte[256];
        for (var i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xff;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var result = input.ToArray();
        var x = 0;
        j = 0;
        for (var n = 0; n < result.Length; n++)
        {
            x = (x + 1) & 0xff;
            j = (j + s[x]) & 0xff;
            (s[x], s[j]) = (s[j], s[x]);
            var k = s[(s[x] + s[j]) & 0xff];
            result[n] ^= k;
        }

        return result;
    }

    private static byte[] XorDigestStream(ReadOnlySpan<byte> input, byte[] seed, string algorithm)
    {
        var result = input.ToArray();
        Span<byte> counter = stackalloc byte[4];
        var offset = 0;
        for (uint block = 0; offset < result.Length; block++)
        {
            counter[0] = (byte)(block >> 24);
            counter[1] = (byte)(block >> 16);
            counter[2] = (byte)(block >> 8);
            counter[3] = (byte)block;
            var material = Combine(seed, counter.ToArray());
            var digest = algorithm == "md5"
                ? MD5.HashData(material)
                : SHA1.HashData(material);
            for (var i = 0; i < digest.Length && offset < result.Length; i++, offset++)
            {
                result[offset] ^= digest[i];
            }
        }

        return result;
    }

    private static byte[] Combine(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var result = new byte[left.Length + right.Length];
        left.CopyTo(result);
        right.CopyTo(result.AsSpan(left.Length));
        return result;
    }

    private static PcapMarkerlessSessionKeyProbeValueCount[] CountBy(IEnumerable<string?> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapMarkerlessSessionKeyProbeValueCount(group.Key, group.Count()))
            .ToArray();

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

    private static string DisplayPath(string inputPath, string file) =>
        Directory.Exists(inputPath)
            ? Path.GetRelativePath(inputPath, file)
            : Path.GetFileName(file);

    private static string CanonicalRelativeFile(string relativeFile)
    {
        var normalized = relativeFile.Replace('\\', '/');
        const string serverCorpusPrefix = "TF2_PS3_network_traffic/packets/server/";
        return normalized.StartsWith(serverCorpusPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[serverCorpusPrefix.Length..]
            : normalized;
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length) =>
        body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private sealed record SessionKeyTransformCandidate(string Name, int PayloadOffset, byte[] Payload);

    private sealed class PcapSessionKeyIndex
    {
        private readonly IReadOnlyDictionary<string, PcapSessionKeySet[]> _keysByFile;

        private PcapSessionKeyIndex(IReadOnlyDictionary<string, PcapSessionKeySet[]> keysByFile)
        {
            _keysByFile = keysByFile;
            FileKeySetCount = keysByFile.Count;
        }

        public int FileKeySetCount { get; }

        public static PcapSessionKeyIndex Load(string path)
        {
            if (!File.Exists(path))
            {
                return new PcapSessionKeyIndex(new Dictionary<string, PcapSessionKeySet[]>(StringComparer.OrdinalIgnoreCase));
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var map = new Dictionary<string, PcapSessionKeySet[]>(StringComparer.OrdinalIgnoreCase);
            if (!doc.RootElement.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array)
            {
                return new PcapSessionKeyIndex(map);
            }

            foreach (var file in files.EnumerateArray())
            {
                var relativeFile = ReadString(file, "File");
                if (relativeFile.Length == 0
                    || !file.TryGetProperty("Packets", out var packets)
                    || packets.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var keySets = new List<PcapSessionKeySet>();
                foreach (var packet in packets.EnumerateArray())
                {
                    if (!packet.TryGetProperty("Fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var keys = BuildKeys(fields);
                    if (keys.Length == 0)
                    {
                        continue;
                    }

                    keySets.Add(new PcapSessionKeySet(
                        ReadString(fields, "GID"),
                        ReadString(fields, "PID"),
                        ReadString(fields, "TICKET"),
                        keys));
                }

                var unique = keySets
                    .GroupBy(static set => string.Join('|', set.Keys.Select(static key => $"{key.Field}:{key.Kind}:{key.FingerprintHex}")), StringComparer.Ordinal)
                    .Select(static group => group.First())
                    .ToArray();
                if (unique.Length > 0)
                {
                    map[relativeFile] = unique;
                }
            }

            return new PcapSessionKeyIndex(map);
        }

        public PcapSessionKeySet[] GetKeys(string relativeFile) =>
            _keysByFile.TryGetValue(relativeFile, out var keys)
                ? keys
                : [];

        private static PcapSessionKey[] BuildKeys(JsonElement fields)
        {
            var keys = new List<PcapSessionKey>();
            AddFieldKeys(keys, fields, "EKEY");
            AddFieldKeys(keys, fields, "LKEY");
            AddFieldKeys(keys, fields, "TICKET");

            var ekeyBytes = keys.FirstOrDefault(static key => key.Field == "EKEY" && key.Kind == "base64")?.Bytes;
            if (ekeyBytes is not null)
            {
                var ticket = ReadString(fields, "TICKET");
                var pid = ReadString(fields, "PID");
                var gid = ReadString(fields, "GID");
                if (ticket.Length > 0)
                {
                    keys.Add(MakeKey("EKEY+TICKET", "base64+ascii", Combine(ekeyBytes, Encoding.ASCII.GetBytes(ticket))));
                }

                if (pid.Length > 0)
                {
                    keys.Add(MakeKey("EKEY+PID", "base64+ascii", Combine(ekeyBytes, Encoding.ASCII.GetBytes(pid))));
                }

                if (gid.Length > 0)
                {
                    keys.Add(MakeKey("EKEY+GID", "base64+ascii", Combine(ekeyBytes, Encoding.ASCII.GetBytes(gid))));
                }
            }

            return keys
                .Where(static key => key.Bytes.Length > 0)
                .GroupBy(static key => $"{key.Field}:{key.Kind}:{key.FingerprintHex}", StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray();
        }

        private static void AddFieldKeys(List<PcapSessionKey> keys, JsonElement fields, string field)
        {
            var raw = ReadString(fields, field);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var decoded = Uri.UnescapeDataString(raw);
            keys.Add(MakeKey(field, "ascii", Encoding.ASCII.GetBytes(decoded)));
            if (TryBase64Decode(decoded, out var base64))
            {
                keys.Add(MakeKey(field, "base64", base64));
            }

            if (TryHexDecode(decoded, out var hex))
            {
                keys.Add(MakeKey(field, "hex", hex));
            }
        }

        private static bool TryBase64Decode(string value, out byte[] bytes)
        {
            bytes = [];
            var normalized = value.Replace('-', '+').Replace('_', '/').TrimEnd('.');
            var padding = (4 - normalized.Length % 4) % 4;
            normalized = normalized + new string('=', padding);
            try
            {
                bytes = Convert.FromBase64String(normalized);
                return bytes.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryHexDecode(string value, out byte[] bytes)
        {
            bytes = [];
            var hex = value.StartsWith('$') ? value[1..] : value;
            if (hex.Length == 0 || hex.Length % 2 != 0 || hex.Any(static c => !Uri.IsHexDigit(c)))
            {
                return false;
            }

            bytes = Convert.FromHexString(hex);
            return bytes.Length > 0;
        }

        private static PcapSessionKey MakeKey(string field, string kind, byte[] bytes) =>
            new(field, kind, Convert.ToHexString(SHA256.HashData(bytes)[..6]).ToLowerInvariant(), bytes);

        private static string ReadString(JsonElement element, string propertyName) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }
}

public sealed record PcapMarkerlessSessionKeyProbeReport(
    string Status,
    string Note,
    PcapMarkerlessSessionKeyProbeSummary Summary,
    PcapMarkerlessSessionKeyProbeTransformCount[] TransformCounts,
    PcapMarkerlessSessionKeyProbeValueCount[] AcceptedKindCounts,
    PcapMarkerlessSessionKeyProbeHitSample[] HitSamples,
    PcapMarkerlessSessionKeyProbeNoHitSample[] NoHitSamples,
    PcapMarkerlessSessionKeyProbeFile[] Files);

public sealed record PcapMarkerlessSessionKeyProbeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int EaTextFileKeySetCount,
    int SessionKeySetCount,
    int HardMarkerlessPacketCount,
    int PacketsWithSessionKeys,
    int ProbedTransformCount,
    int StrictDecodeHitCount,
    int ClcMoveHitCount,
    int NetMessageHitCount,
    int QueueDeltaExactHitCount,
    int PacketWithAcceptedTransformHitCount,
    bool NativeMarkerlessTransformReady,
    string Conclusion);

public sealed record PcapMarkerlessSessionKeyProbeFile(
    string File,
    string CanonicalFile,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SessionKeySetCount,
    int HardMarkerlessPacketCount,
    int PacketsWithSessionKeys,
    int ProbedTransformCount,
    int StrictDecodeHitCount,
    int ClcMoveHitCount,
    int NetMessageHitCount,
    int QueueDeltaExactHitCount,
    int PacketWithAcceptedTransformHitCount,
    PcapMarkerlessSessionKeyProbeTransformCount[] TransformCounts,
    PcapMarkerlessSessionKeyProbeHitSample[] HitSamples,
    PcapMarkerlessSessionKeyProbeNoHitSample[] NoHitSamples);

public sealed record PcapMarkerlessSessionKeyProbeTransformCount(
    string Transform,
    int ProbedPacketCount,
    int StrictDecodeHitCount,
    int QueueDeltaExactHitCount);

public sealed record PcapMarkerlessSessionKeyProbeHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string BodyPrefix32Hex,
    string Transform,
    int PayloadOffset,
    string AcceptedKind,
    string Detail,
    string TransformedPrefix32Hex);

public sealed record PcapMarkerlessSessionKeyProbeNoHitSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string BodyPrefix32Hex,
    string Reason);

public sealed record PcapMarkerlessSessionKeyProbeValueCount(
    string Value,
    int Count);

internal sealed record PcapSessionKeySet(
    string GameId,
    string PlayerId,
    string Ticket,
    PcapSessionKey[] Keys);

internal sealed record PcapSessionKey(
    string Field,
    string Kind,
    string FingerprintHex,
    byte[] Bytes);
