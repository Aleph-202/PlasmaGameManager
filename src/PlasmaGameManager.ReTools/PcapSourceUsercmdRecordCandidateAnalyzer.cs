using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceUsercmdRecordCandidateAnalyzer
{
    private const int OfficialRecordStrideBytes = 0x58;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceUsercmdRecordCandidateReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceUsercmdRecordCandidateReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var packets = files.SelectMany(static file => file.Candidates).ToArray();
        var undecoded = packets.Where(static packet => packet.NativeDecodeKind.Length == 0).ToArray();
        var exactTwoRecord = packets.Count(static packet => packet.RecordCount == 2);
        var highEntropy = packets.Count(static packet => packet.BodyEntropy >= 7.0);
        var queueDeltaDecoded = packets.Count(static packet => packet.TfElfQueueDeltaProbeStatus != "decode-failed");
        var queueDeltaExact = packets.Count(static packet => packet.TfElfQueueDeltaProbeStatus == "exact-zero-trailing");
        var queueDeltaTrailing = packets.Count(static packet => packet.TfElfQueueDeltaProbeStatus == "decoded-with-nonzero-trailing-data");
        var segments = packets.SelectMany(static packet => packet.Segments).ToArray();
        var segmentDeltaExact = segments.Count(static segment => segment.TfElfSingleRecordProbeStatus == "exact-zero-trailing");
        var segmentDeltaTrailing = segments.Count(static segment => segment.TfElfSingleRecordProbeStatus == "decoded-with-nonzero-trailing-data");
        var segmentDeltaFailed = segments.Count(static segment => segment.TfElfSingleRecordProbeStatus == "decode-failed");

        return new PcapSourceUsercmdRecordCandidateReport(
            "pcap-source-usercmd-record-candidates",
            "Audits hard markerless client Source packets whose body length is an exact multiple of TF.elf's recovered 0x58-byte usercmd queue record stride.",
            new PcapSourceUsercmdRecordCandidateSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                packets.Length,
                packets.Count(static packet => packet.NativeDecodeKind.Length > 0),
                undecoded.Length,
                exactTwoRecord,
                highEntropy,
                queueDeltaDecoded,
                queueDeltaExact,
                queueDeltaTrailing,
                segments.Length,
                segmentDeltaExact,
                segmentDeltaTrailing,
                segmentDeltaFailed,
                packets.Length == 0
                    ? "no hard markerless packets have exact 0x58 record-multiple lengths"
                    : queueDeltaExact > 0
                        ? "some exact 0x58-multiple bodies structurally fit the TF.elf 0080ad88 queue-delta stream with zero trailing bits"
                    : queueDeltaTrailing > 0
                        ? "exact 0x58-multiple bodies can begin like TF.elf queue-delta streams, but all decoded candidates leave non-zero trailing data; the markerless wrapper/transform is still missing"
                    : undecoded.Length > 0
                        ? "some exact 0x58-multiple bodies remain markerless and undecoded; inspect segment prefixes/entropy as possible ingress-transform evidence"
                        : "all exact 0x58-multiple bodies already decode as known client net messages"),
            CountBy(packets.Select(static packet => packet.BodyLength.ToString())),
            CountBy(packets.Select(static packet => packet.NativeDecodeKind.Length == 0 ? "undecoded" : packet.NativeDecodeKind)),
            files,
            undecoded.Take(256).ToArray());
    }

    private PcapSourceUsercmdRecordCandidateFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceUsercmdRecordCandidateFile(relativePath, false, "", "", 0, 0, []);
        }

        var candidates = new List<PcapSourceUsercmdRecordCandidatePacket>();
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
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
            if (!IsHardMarkerless(info)
                || transport.Body.Length < OfficialRecordStrideBytes
                || transport.Body.Length % OfficialRecordStrideBytes != 0)
            {
                continue;
            }

            candidates.Add(BuildCandidate(relativePath, sourceStep, raw, transport, info));
        }

        return new PcapSourceUsercmdRecordCandidateFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            candidates.Count,
            candidates.Count(static candidate => candidate.NativeDecodeKind.Length == 0),
            candidates.ToArray());
    }

    private static PcapSourceUsercmdRecordCandidatePacket BuildCandidate(
        string file,
        int sourceStep,
        PcapActiveFlowDatagram raw,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info)
    {
        var nativeDecodeKind = "";
        var nativeDecodeDetail = "";
        var weakDecodeDetail = "";
        if (Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var boundary))
        {
            nativeDecodeKind = "CLC_Move";
            nativeDecodeDetail = $"{boundary.Kind}: new={boundary.Move.NewCommands} backup={boundary.Move.BackupCommands} commands={boundary.Batch.Commands.Count} bits={boundary.Move.TotalBitsConsumed}/{boundary.PayloadBitCount}";
        }
        else if (Ps3SourceClientNetMessageDecoder.TryDecode(transport.Body, out var netMessage))
        {
            var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(
                netMessage,
                transport.Body.AsSpan(netMessage.PayloadOffset, netMessage.PayloadLength));
            if (strength.IsStrong)
            {
                nativeDecodeKind = "NET_Message";
                nativeDecodeDetail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits}/{netMessage.PayloadBitCount}";
            }
            else
            {
                weakDecodeDetail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits?.ToString() ?? "unknown"}/{netMessage.PayloadBitCount}; {strength.Reason}";
            }
        }

        var body = transport.Body.AsSpan();
        var recordCount = body.Length / OfficialRecordStrideBytes;
        var segmentCount = Math.Min(recordCount, 8);
        var segments = new PcapSourceUsercmdRecordCandidateSegment[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var segment = body.Slice(index * OfficialRecordStrideBytes, OfficialRecordStrideBytes);
            var singleRecordProbe = TfElfQueueDeltaProbe.TryDecode(segment, 1);
            segments[index] = new PcapSourceUsercmdRecordCandidateSegment(
                index,
                Prefix(segment, 24),
                ReadUInt32BigEndian(segment),
                ShannonEntropy(segment),
                singleRecordProbe.Status,
                singleRecordProbe.ConsumedBits,
                singleRecordProbe.TrailingNonZeroBits,
                singleRecordProbe.Detail);
        }

        var queueDeltaProbe = TfElfQueueDeltaProbe.TryDecode(body, recordCount);

        return new PcapSourceUsercmdRecordCandidatePacket(
            file,
            sourceStep,
            raw.PacketIndex,
            raw.TimestampMicroseconds,
            transport.CandidateSequence,
            info.SequenceDelta,
            transport.PayloadLength,
            body.Length,
            recordCount,
            info.Role.ToString(),
            info.Shape.ToString(),
            nativeDecodeKind,
            nativeDecodeDetail.Length > 0 ? nativeDecodeDetail : weakDecodeDetail,
            queueDeltaProbe.Status,
            queueDeltaProbe.RecordCount,
            queueDeltaProbe.ConsumedBits,
            queueDeltaProbe.TrailingNonZeroBits,
            queueDeltaProbe.Detail,
            ShannonEntropy(body),
            Prefix(body, 64),
            segments);
    }

    private static bool IsHardMarkerless(Ps3SourceClientPayloadInfo info)
    {
        if (info.AttachedFrameKind is not null
            || info.BitSidecarOffset is not null
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

    private static PcapSourceUsercmdRecordCandidateCount[] CountBy(IEnumerable<string> values) =>
        values
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceUsercmdRecordCandidateCount(group.Key, group.Count()))
            .ToArray();

    private static string DisplayPath(string inputPath, string file) =>
        Directory.Exists(inputPath)
            ? Path.GetRelativePath(inputPath, file)
            : Path.GetFileName(file);

    private static string Prefix(ReadOnlySpan<byte> body, int length) =>
        body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> body) =>
        body.Length < 4
            ? 0
            : ((uint)body[0] << 24) | ((uint)body[1] << 16) | ((uint)body[2] << 8) | body[3];

    private static double ShannonEntropy(ReadOnlySpan<byte> body)
    {
        if (body.IsEmpty)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var value in body)
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

            var p = (double)count / body.Length;
            entropy -= p * Math.Log2(p);
        }

        return Math.Round(entropy, 4);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapSourceUsercmdRecordCandidateReport(
    string Status,
    string Note,
    PcapSourceUsercmdRecordCandidateSummary Summary,
    PcapSourceUsercmdRecordCandidateCount[] BodyLengthCounts,
    PcapSourceUsercmdRecordCandidateCount[] DecodeKindCounts,
    PcapSourceUsercmdRecordCandidateFile[] Files,
    PcapSourceUsercmdRecordCandidatePacket[] UndecodedSamples);

public sealed record PcapSourceUsercmdRecordCandidateSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ExactRecordMultiplePacketCount,
    int NativeDecodedExactRecordMultiplePacketCount,
    int UndecodedExactRecordMultiplePacketCount,
    int ExactTwoRecordPacketCount,
    int HighEntropyPacketCount,
    int TfElfQueueDeltaDecodedPacketCount,
    int TfElfQueueDeltaExactZeroTrailingPacketCount,
    int TfElfQueueDeltaNonZeroTrailingPacketCount,
    int TfElfSingleRecordSegmentProbeCount,
    int TfElfSingleRecordSegmentExactZeroTrailingCount,
    int TfElfSingleRecordSegmentNonZeroTrailingCount,
    int TfElfSingleRecordSegmentDecodeFailedCount,
    string Conclusion);

public sealed record PcapSourceUsercmdRecordCandidateCount(
    string Value,
    int Count);

public sealed record PcapSourceUsercmdRecordCandidateFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int ExactRecordMultiplePacketCount,
    int UndecodedExactRecordMultiplePacketCount,
    PcapSourceUsercmdRecordCandidatePacket[] Candidates);

public sealed record PcapSourceUsercmdRecordCandidatePacket(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    int RecordCount,
    string Role,
    string Shape,
    string NativeDecodeKind,
    string NativeDecodeDetail,
    string TfElfQueueDeltaProbeStatus,
    int TfElfQueueDeltaRecordCount,
    int TfElfQueueDeltaConsumedBits,
    int TfElfQueueDeltaTrailingNonZeroBits,
    string TfElfQueueDeltaDetail,
    double BodyEntropy,
    string BodyPrefix64Hex,
    PcapSourceUsercmdRecordCandidateSegment[] Segments);

public sealed record PcapSourceUsercmdRecordCandidateSegment(
    int Index,
    string Prefix24Hex,
    uint FirstWordBigEndian,
    double Entropy,
    string TfElfSingleRecordProbeStatus,
    int TfElfSingleRecordConsumedBits,
    int TfElfSingleRecordTrailingNonZeroBits,
    string TfElfSingleRecordDetail);

internal readonly record struct TfElfQueueDeltaProbe(
    string Status,
    int RecordCount,
    int ConsumedBits,
    int TrailingNonZeroBits,
    string Detail)
{
    public static TfElfQueueDeltaProbe TryDecode(ReadOnlySpan<byte> bitstream, int expectedRecordCount)
    {
        if (expectedRecordCount <= 0 || bitstream.IsEmpty)
        {
            return new TfElfQueueDeltaProbe("decode-failed", 0, 0, 0, "no records");
        }

        var reader = new TfElfQueueDeltaBitReader(bitstream);
        var previous = new TfElfQueueDeltaRecord();
        var decoded = 0;
        for (; decoded < expectedRecordCount; decoded++)
        {
            if (!TryDecodeRecord(ref reader, previous, out var current))
            {
                return new TfElfQueueDeltaProbe(
                    "decode-failed",
                    decoded,
                    reader.ConsumedBits,
                    CountNonZeroBits(bitstream, reader.ConsumedBits),
                    "recovered 0080ad88 field order ran out of bits or hit an invalid branch");
            }

            previous = current;
            if (current.ModeCountOrEarlyReturn == 4)
            {
                decoded++;
                break;
            }
        }

        var trailing = CountNonZeroBits(bitstream, reader.ConsumedBits);
        return new TfElfQueueDeltaProbe(
            trailing == 0 ? "exact-zero-trailing" : "decoded-with-nonzero-trailing-data",
            decoded,
            reader.ConsumedBits,
            trailing,
            trailing == 0
                ? "body structurally matches the recovered 0080ad88 delta-field order"
                : "body has a recoverable 0080ad88-style prefix, but non-zero trailing data prevents treating it as a direct queue-delta stream");
    }

    private static bool TryDecodeRecord(
        ref TfElfQueueDeltaBitReader reader,
        TfElfQueueDeltaRecord previous,
        out TfElfQueueDeltaRecord current)
    {
        current = previous;
        if (!ReadOptionalUInt(ref reader, previous.Preamble11, 11, out var preamble11)
            || !ReadOptionalUInt(ref reader, previous.Preamble13, 13, out var preamble13)
            || !ReadOptionalUInt(ref reader, previous.ModeCountOrEarlyReturn, 9, out var mode)
            || !ReadOptionalUInt(ref reader, previous.SmallMode, 3, out var smallMode)
            || !reader.TryReadBit(out var flag51)
            || !reader.TryReadBit(out var flag50))
        {
            return false;
        }

        current = current with
        {
            Preamble11 = preamble11,
            Preamble13 = preamble13,
            ModeCountOrEarlyReturn = mode,
            SmallMode = smallMode,
            Flag51 = flag51,
            Flag50 = flag50
        };
        if (mode == 4)
        {
            return true;
        }

        if (!TryReadCommandNumber(ref reader, previous.CommandNumber, out var commandNumber)
            || !ReadOptionalUInt(ref reader, previous.ViewAngleXOrFloat, 32, out var viewX)
            || !ReadOptionalUInt(ref reader, previous.ViewAngleYQuantized, 9, out var viewY)
            || !ReadOptionalUInt(ref reader, previous.ViewAngleZQuantized, 8, out var viewZ)
            || !ReadOptionalUInt(ref reader, previous.ForwardMoveQuantized, 13, out var forward)
            || !ReadOptionalUInt(ref reader, previous.SideMoveQuantized, 15, out var side)
            || !ReadOptionalUInt(ref reader, previous.UpMoveQuantized, 15, out var up)
            || !ReadOptionalUInt(ref reader, previous.UnresolvedMovementOrButtons, 15, out var unresolved)
            || !ReadOptionalUInt(ref reader, previous.WeaponOrTailHint, 12, out var weapon))
        {
            return false;
        }

        current = current with
        {
            CommandNumber = commandNumber,
            ViewAngleXOrFloat = viewX,
            ViewAngleYQuantized = viewY,
            ViewAngleZQuantized = viewZ,
            ForwardMoveQuantized = forward,
            SideMoveQuantized = side,
            UpMoveQuantized = up,
            UnresolvedMovementOrButtons = unresolved,
            WeaponOrTailHint = weapon
        };
        return true;
    }

    private static bool ReadOptionalUInt(
        ref TfElfQueueDeltaBitReader reader,
        uint previous,
        int bitCount,
        out uint value)
    {
        value = previous;
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        return !present || reader.TryReadBits(bitCount, out value);
    }

    private static bool TryReadCommandNumber(
        ref TfElfQueueDeltaBitReader reader,
        uint previous,
        out uint value)
    {
        value = previous;
        if (!reader.TryReadBit(out var first))
        {
            return false;
        }

        if (first)
        {
            return true;
        }

        if (!reader.TryReadBit(out var second))
        {
            return false;
        }

        if (second)
        {
            value = previous + 1;
            return true;
        }

        return reader.TryReadBits(31, out value);
    }

    private static int CountNonZeroBits(ReadOnlySpan<byte> bytes, int startBit)
    {
        var totalBits = bytes.Length * 8;
        var count = 0;
        for (var bit = Math.Max(0, startBit); bit < totalBits; bit++)
        {
            if ((bytes[bit >> 3] & (1 << (bit & 7))) != 0)
            {
                count++;
            }
        }

        return count;
    }
}

internal readonly record struct TfElfQueueDeltaRecord
{
    public uint Preamble11 { get; init; }
    public uint Preamble13 { get; init; }
    public uint ModeCountOrEarlyReturn { get; init; }
    public uint SmallMode { get; init; }
    public bool Flag51 { get; init; }
    public bool Flag50 { get; init; }
    public uint CommandNumber { get; init; }
    public uint ViewAngleXOrFloat { get; init; }
    public uint ViewAngleYQuantized { get; init; }
    public uint ViewAngleZQuantized { get; init; }
    public uint ForwardMoveQuantized { get; init; }
    public uint SideMoveQuantized { get; init; }
    public uint UpMoveQuantized { get; init; }
    public uint UnresolvedMovementOrButtons { get; init; }
    public uint WeaponOrTailHint { get; init; }
}

internal ref struct TfElfQueueDeltaBitReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private int _bitOffset;

    public TfElfQueueDeltaBitReader(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes;
        _bitOffset = 0;
    }

    public int ConsumedBits => _bitOffset;

    public bool TryReadBit(out bool bit)
    {
        if (!TryReadBits(1, out var value))
        {
            bit = false;
            return false;
        }

        bit = value != 0;
        return true;
    }

    public bool TryReadBits(int count, out uint value)
    {
        value = 0;
        if (count is < 0 or > 32 || _bitOffset + count > _bytes.Length * 8)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            var bit = _bitOffset + i;
            if ((_bytes[bit >> 3] & (1 << (bit & 7))) != 0)
            {
                value |= 1u << i;
            }
        }

        _bitOffset += count;
        return true;
    }
}
