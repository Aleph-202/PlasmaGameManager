using System.Buffers.Binary;

namespace PlasmaGameManager.Protocol;

public static class Ps3SourceClientPayloadClassifier
{
    public static Ps3SourceClientPayloadInfo Classify(
        Ps3SourceTransportPacket packet,
        int directionPacketCount,
        int? sequenceDelta)
    {
        var nativeFrame = packet.ClassifyNativeFrame();
        var shape = Ps3SourceGameplaySession.ClassifyShape(packet);
        var semantic = directionPacketCount == 1
            ? Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(packet.Body)
            : Ps3SourcePayloadSemantics.Analyze(packet.Body);
        Ps3SourceReliableAssociationProbe.TryDecode(packet.Body, out var associationProbe);
        Ps3SourceAttachedClientFrame.TryDecode(packet.Body, out var attachedFrame);
        Ps3SourceBitSidecarFrame.TryDetect(packet.Body, out var bitSidecar);
        Ps3SourcePayloadObjectFrame.TryDecode(packet.Body, out var payloadObjectFrame);
        var clientNetMessage = TryDecodeStrongClientNetMessage(packet.Body, out var decodedClientNetMessage)
            ? decodedClientNetMessage
            : null;
        var nativeFrameKind = bitSidecar is not null
            ? Ps3SourceNativeFrameKind.DirectWithBitPayloadSidecarCandidate
            : nativeFrame.Kind;
        var role = ClassifyRole(packet, directionPacketCount, nativeFrame, shape, semantic, attachedFrame, payloadObjectFrame);
        return new Ps3SourceClientPayloadInfo(
            role,
            packet.CandidateSequence,
            sequenceDelta,
            packet.Body.Length,
            shape,
            nativeFrameKind,
            PacketPrefix(packet.Body),
            FirstUInt16BigEndian(packet.Body),
            FirstUInt16LittleEndian(packet.Body),
            LastUInt16BigEndian(packet.Body),
            semantic.Role,
            associationProbe?.MessageType,
            associationProbe?.NativeAssociationToken,
            associationProbe?.AssociationTokenBigEndian,
            associationProbe?.AssociationTokenLittleEndian,
            attachedFrame?.Kind,
            attachedFrame?.DeclaredLength,
            attachedFrame?.NativeToken,
            attachedFrame?.TokenBigEndian,
            attachedFrame?.TokenLittleEndian,
            bitSidecar?.Offset,
            bitSidecar?.BitCount,
            bitSidecar?.PayloadLength,
            payloadObjectFrame?.Kind.ToString(),
            payloadObjectFrame?.HeaderValue,
            payloadObjectFrame?.HeaderSignedValue,
            payloadObjectFrame?.InnerPayloadOffset,
            payloadObjectFrame?.InnerPayloadLength,
            payloadObjectFrame?.PayloadObjectBitreaderFieldOffset,
            payloadObjectFrame?.AssociatedObjectToken,
            payloadObjectFrame?.FragmentIndex,
            payloadObjectFrame?.FragmentTotalCount,
            payloadObjectFrame?.FragmentPacketCounter,
            payloadObjectFrame?.FragmentWrappedOrCompressed,
            clientNetMessage?.MessageType,
            clientNetMessage?.MessageName,
            clientNetMessage?.PayloadKind.ToString(),
            clientNetMessage?.PayloadOffset,
            clientNetMessage?.PayloadLength,
            clientNetMessage?.PayloadBitCount);
    }

    private static bool TryDecodeStrongClientNetMessage(
        ReadOnlySpan<byte> body,
        out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;
        if (!Ps3SourceClientNetMessageDecoder.TryDecode(body, out var candidate)
            || candidate.PayloadOffset < 0
            || candidate.PayloadLength < 0
            || candidate.PayloadOffset > body.Length
            || candidate.PayloadOffset + candidate.PayloadLength > body.Length)
        {
            return false;
        }

        if (candidate.PayloadKind == Ps3SourceClientNetMessagePayloadKind.DirectRawBody)
        {
            return false;
        }

        var payload = body.Slice(candidate.PayloadOffset, candidate.PayloadLength);
        var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(candidate, payload);
        if (!strength.IsStrong)
        {
            return false;
        }

        message = candidate;
        return true;
    }

    private static Ps3SourceClientPayloadRole ClassifyRole(
        Ps3SourceTransportPacket packet,
        int directionPacketCount,
        Ps3SourceNativeFrameInfo nativeFrame,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourcePayloadSemanticInfo semantic,
        Ps3SourceAttachedClientFrame? attachedFrame,
        Ps3SourcePayloadObjectFrame? payloadObjectFrame)
    {
        if (Ps3SourceReliableAssociationProbe.TryDecode(packet.Body, out var associationProbe)
            && associationProbe is { IsAssociationRequest: true })
        {
            return Ps3SourceClientPayloadRole.ReliableAssociationProbe;
        }

        if (attachedFrame is { Kind: 2 })
        {
            return Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame;
        }

        if (attachedFrame is not null)
        {
            return Ps3SourceClientPayloadRole.AttachedPlayerControlFrame;
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.InitialHandoffClientProbe)
        {
            return Ps3SourceClientPayloadRole.InitialHandoffProbe;
        }

        if (nativeFrame.Kind == Ps3SourceNativeFrameKind.FragmentedSendCandidate
            || Ps3SourceFragmentHeader.TryDecode(packet.Body, out _))
        {
            return Ps3SourceClientPayloadRole.FragmentedClientPayload;
        }

        if (shape == Ps3SourceGameplayPacketShape.ShortControl)
        {
            return Ps3SourceClientPayloadRole.ShortControlAck;
        }

        if (semantic.EmbeddedMarkers.Length > 0)
        {
            return Ps3SourceClientPayloadRole.EmbeddedObjectNotice;
        }

        if (directionPacketCount <= 3)
        {
            return Ps3SourceClientPayloadRole.SetupControlPayload;
        }

        if (shape is Ps3SourceGameplayPacketShape.MediumBinary or Ps3SourceGameplayPacketShape.HighEntropyBinary
            && packet.Body.Length is >= 32 and <= 256)
        {
            return Ps3SourceClientPayloadRole.UserCommandCandidate;
        }

        return Ps3SourceClientPayloadRole.BinaryControlPayload;
    }

    private static string PacketPrefix(ReadOnlySpan<byte> body)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(8, body.Length)]).ToLowerInvariant();
    }

    private static ushort? FirstUInt16BigEndian(ReadOnlySpan<byte> body)
    {
        return body.Length < 2 ? null : (ushort)((body[0] << 8) | body[1]);
    }

    private static ushort? FirstUInt16LittleEndian(ReadOnlySpan<byte> body)
    {
        return body.Length < 2 ? null : (ushort)(body[0] | (body[1] << 8));
    }

    private static ushort? LastUInt16BigEndian(ReadOnlySpan<byte> body)
    {
        return body.Length < 2 ? null : (ushort)((body[^2] << 8) | body[^1]);
    }
}

public enum Ps3SourceClientPayloadRole
{
    InitialHandoffProbe,
    ReliableAssociationProbe,
    AttachedPlayerControlFrame,
    AttachedPlayerPayloadFrame,
    ShortControlAck,
    SetupControlPayload,
    EmbeddedObjectNotice,
    FragmentedClientPayload,
    AssociatedObjectStateReset,
    UserCommandCandidate,
    BinaryControlPayload
}

public sealed record Ps3SourceClientPayloadInfo(
    Ps3SourceClientPayloadRole Role,
    ushort Sequence,
    int? SequenceDelta,
    int BodyLength,
    Ps3SourceGameplayPacketShape Shape,
    Ps3SourceNativeFrameKind NativeFrameKind,
    string BodyPrefixHex,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    ushort? LastUInt16BigEndian,
    Ps3SourcePayloadSemanticRole PayloadSemanticRole,
    byte? ReliableAssociationMessageType,
    uint? ReliableAssociationNativeToken,
    uint? ReliableAssociationTokenBigEndian,
    uint? ReliableAssociationTokenLittleEndian,
    byte? AttachedFrameKind,
    ushort? AttachedFrameDeclaredLength,
    uint? AttachedFrameNativeToken,
    uint? AttachedFrameTokenBigEndian,
    uint? AttachedFrameTokenLittleEndian,
    int? BitSidecarOffset,
    int? BitSidecarBitCount,
    int? BitSidecarPayloadLength,
    string? PayloadObjectFrameKind = null,
    uint? PayloadObjectHeaderValue = null,
    int? PayloadObjectSignedHeaderValue = null,
    int? PayloadObjectInnerPayloadOffset = null,
    int? PayloadObjectInnerPayloadLength = null,
    int? PayloadObjectBitreaderFieldOffset = null,
    uint? PayloadObjectAssociatedToken = null,
    byte? PayloadObjectFragmentIndex = null,
    byte? PayloadObjectFragmentTotalCount = null,
    uint? PayloadObjectFragmentPacketCounter = null,
    bool? PayloadObjectFragmentWrappedOrCompressed = null,
    int? DecodedNetMessageType = null,
    string? DecodedNetMessageName = null,
    string? DecodedNetMessagePayloadKind = null,
    int? DecodedNetMessagePayloadOffset = null,
    int? DecodedNetMessagePayloadLength = null,
    int? DecodedNetMessagePayloadBitCount = null);

public enum Ps3SourcePayloadObjectFrameKind
{
    OwnerSlot8Control,
    FragmentedSpecialWrapper,
    RepackedSpecialWrapper,
    AssociatedObjectToken
}

public enum Ps3SourceOwnerForwarderBitstreamLayout
{
    Word5PayloadWord6ReaderWord15,
    Word6PayloadWord7ReaderWord16,
    DeferredPointerWord6PayloadWord10PointerWord19,
    ConfigFallbackWord4PayloadWord5ReaderWord14
}

public sealed record Ps3SourcePayloadObjectFrame(
    Ps3SourcePayloadObjectFrameKind Kind,
    uint HeaderValue,
    int HeaderSignedValue,
    int InnerPayloadOffset,
    int InnerPayloadLength,
    int PayloadObjectBitreaderFieldOffset,
    uint? AssociatedObjectToken,
    byte? FragmentIndex,
    byte? FragmentTotalCount,
    uint? FragmentPacketCounter,
    bool? FragmentWrappedOrCompressed)
{
    public const int HeaderBytes = 4;

    public const int OwnerSlot8BitCountFieldOffset = 0x14;

    public const int OwnerSlot8BitPayloadOffset = 0x18;

    public const int OwnerForwarderWord6BitCountFieldOffset = 0x18;

    public const int OwnerForwarderWord6BitPayloadOffset = 0x1c;

    public const int OwnerForwarderWord6RebuiltBitreaderFieldOffsetValue = 0x40;

    public const int OwnerForwarderDeferredPointerWord6BitCountFieldOffset = 0x18;

    public const int OwnerForwarderDeferredPointerWord6BitPayloadOffset = 0x28;

    public const int OwnerForwarderDeferredPointerWord6TempPointerFieldOffsetValue = 0x4c;

    public const int OwnerForwarderConfigFallbackWord4BitCountFieldOffset = 0x10;

    public const int OwnerForwarderConfigFallbackWord4BitPayloadOffset = 0x14;

    public const int OwnerForwarderConfigFallbackWord4RebuiltBitreaderFieldOffsetValue = 0x38;

    public const int PayloadObjectBufferFieldOffset = 0x18;

    public const int PayloadObjectBitreaderFieldOffsetValue = 0x1c;

    public const int OwnerSlot8RebuiltBitreaderFieldOffsetValue = 0x3c;

    public const int PayloadObjectLengthFieldOffset = 0x40;

    public const int PayloadObjectOwnerLengthFieldOffset = 0x44;

    public bool ShouldTryInnerPayloadDecoding =>
        Kind != Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken;

    public ReadOnlySpan<byte> SliceInnerPayload(ReadOnlySpan<byte> body)
    {
        if (InnerPayloadOffset < 0
            || InnerPayloadLength < 0
            || InnerPayloadOffset > body.Length
            || InnerPayloadOffset + InnerPayloadLength > body.Length)
        {
            return [];
        }

        return body.Slice(InnerPayloadOffset, InnerPayloadLength);
    }

    public static bool TryDecode(ReadOnlySpan<byte> body, out Ps3SourcePayloadObjectFrame? frame)
    {
        frame = null;
        if (body.Length < HeaderBytes)
        {
            return false;
        }

        var header =
            ((uint)body[0] << 24)
            | ((uint)body[1] << 16)
            | ((uint)body[2] << 8)
            | body[3];
        var signedHeader = unchecked((int)header);
        var kind = signedHeader switch
        {
            -1 => Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control,
            -2 => Ps3SourcePayloadObjectFrameKind.FragmentedSpecialWrapper,
            -3 => Ps3SourcePayloadObjectFrameKind.RepackedSpecialWrapper,
            _ => Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken
        };

        var innerPayloadOffset = kind == Ps3SourcePayloadObjectFrameKind.FragmentedSpecialWrapper
            ? Math.Min(Ps3SourceFragmentHeader.HeaderBytes, body.Length)
            : Math.Min(HeaderBytes, body.Length);
        var innerPayloadLength = body.Length - innerPayloadOffset;
        var payloadObjectBitreaderFieldOffset = PayloadObjectBitreaderFieldOffsetValue;
        byte? fragmentIndex = null;
        byte? fragmentTotalCount = null;
        uint? fragmentPacketCounter = null;
        bool? fragmentWrappedOrCompressed = null;
        if (kind == Ps3SourcePayloadObjectFrameKind.FragmentedSpecialWrapper
            && Ps3SourceFragmentHeader.TryDecode(body, out var fragmentHeader))
        {
            fragmentIndex = fragmentHeader.FragmentIndex;
            fragmentTotalCount = fragmentHeader.TotalCount;
            fragmentPacketCounter = fragmentHeader.PacketCounter;
            fragmentWrappedOrCompressed = fragmentHeader.WrappedOrCompressed;
        }
        else if (kind == Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control
            && TryReadOwnerSlot8Bitstream(body, out var bitCount, out var bitPayloadLength))
        {
            // TF.elf owner slot +0x08 target 00a52720 reads param_2[5] as
            // bit-count, copies bits from param_2 + 6, rebuilds a bitreader at
            // param_2 + 0xf, then forwards into 008722a0.
            innerPayloadOffset = OwnerSlot8BitPayloadOffset;
            innerPayloadLength = bitPayloadLength;
            payloadObjectBitreaderFieldOffset = OwnerSlot8RebuiltBitreaderFieldOffsetValue;
        }

        frame = new Ps3SourcePayloadObjectFrame(
            kind,
            header,
            signedHeader,
            innerPayloadOffset,
            innerPayloadLength,
            payloadObjectBitreaderFieldOffset,
            kind == Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken ? header : null,
            fragmentIndex,
            fragmentTotalCount,
            fragmentPacketCounter,
            fragmentWrappedOrCompressed);
        return true;
    }

    public static bool TryReadOwnerSlot8Bitstream(
        ReadOnlySpan<byte> body,
        out int bitCount,
        out int payloadLength)
    {
        if (TryReadOwnerForwarderBitstream(
                body,
                Ps3SourceOwnerForwarderBitstreamLayout.Word5PayloadWord6ReaderWord15,
                out bitCount,
                out _,
                out payloadLength,
                out _))
        {
            return true;
        }

        bitCount = 0;
        payloadLength = 0;
        return false;
    }

    public static bool TryReadOwnerForwarderBitstream(
        ReadOnlySpan<byte> body,
        Ps3SourceOwnerForwarderBitstreamLayout layout,
        out int bitCount,
        out int payloadOffset,
        out int payloadLength,
        out int readerOrPointerFieldOffset)
    {
        bitCount = 0;
        payloadOffset = 0;
        payloadLength = 0;
        readerOrPointerFieldOffset = 0;
        var descriptor = DescribeOwnerForwarderBitstreamLayout(layout);
        if (body.Length < descriptor.PayloadOffset
            || body.Length < descriptor.BitCountOffset + sizeof(int))
        {
            return false;
        }

        var bitCountField = body.Slice(descriptor.BitCountOffset, sizeof(int));
        var bigEndian = BinaryPrimitives.ReadInt32BigEndian(bitCountField);
        var littleEndian = BinaryPrimitives.ReadInt32LittleEndian(bitCountField);
        if (!TryUseBitCount(bigEndian, body.Length, descriptor.PayloadOffset, out bitCount, out payloadLength)
            && !TryUseBitCount(littleEndian, body.Length, descriptor.PayloadOffset, out bitCount, out payloadLength))
        {
            return false;
        }

        payloadOffset = descriptor.PayloadOffset;
        readerOrPointerFieldOffset = descriptor.ReaderOrPointerFieldOffset;
        return true;
    }

    public static Ps3SourceOwnerForwarderBitstreamDescriptor DescribeOwnerForwarderBitstreamLayout(
        Ps3SourceOwnerForwarderBitstreamLayout layout) =>
        layout switch
        {
            Ps3SourceOwnerForwarderBitstreamLayout.Word5PayloadWord6ReaderWord15 => new(
                OwnerSlot8BitCountFieldOffset,
                OwnerSlot8BitPayloadOffset,
                OwnerSlot8RebuiltBitreaderFieldOffsetValue),
            Ps3SourceOwnerForwarderBitstreamLayout.Word6PayloadWord7ReaderWord16 => new(
                OwnerForwarderWord6BitCountFieldOffset,
                OwnerForwarderWord6BitPayloadOffset,
                OwnerForwarderWord6RebuiltBitreaderFieldOffsetValue),
            Ps3SourceOwnerForwarderBitstreamLayout.DeferredPointerWord6PayloadWord10PointerWord19 => new(
                OwnerForwarderDeferredPointerWord6BitCountFieldOffset,
                OwnerForwarderDeferredPointerWord6BitPayloadOffset,
                OwnerForwarderDeferredPointerWord6TempPointerFieldOffsetValue),
            Ps3SourceOwnerForwarderBitstreamLayout.ConfigFallbackWord4PayloadWord5ReaderWord14 => new(
                OwnerForwarderConfigFallbackWord4BitCountFieldOffset,
                OwnerForwarderConfigFallbackWord4BitPayloadOffset,
                OwnerForwarderConfigFallbackWord4RebuiltBitreaderFieldOffsetValue),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null)
        };

    private static bool TryUseBitCount(
        int candidate,
        int bodyLength,
        int payloadOffset,
        out int bitCount,
        out int payloadLength)
    {
        bitCount = 0;
        payloadLength = 0;
        if (candidate <= 0 || candidate > Ps3SourceSendWrapper.MaxBitSidecarBits)
        {
            return false;
        }

        var byteCount = Ps3SourceSendWrapper.GetBitSidecarPayloadByteCount(candidate);
        if (payloadOffset + byteCount > bodyLength)
        {
            return false;
        }

        bitCount = candidate;
        payloadLength = byteCount;
        return true;
    }
}

public sealed record Ps3SourceOwnerForwarderBitstreamDescriptor(
    int BitCountOffset,
    int PayloadOffset,
    int ReaderOrPointerFieldOffset);

public sealed record Ps3SourceBitSidecarFrame(
    int Offset,
    int BitCount,
    int PayloadLength)
{
    private const int MinimumCommandPayloadBytes = 32;

    public static bool TryDetect(ReadOnlySpan<byte> body, out Ps3SourceBitSidecarFrame? frame)
    {
        var candidates = DetectAll(body);
        frame = candidates.Count == 0 ? null : candidates[0];
        return frame is not null;
    }

    public static IReadOnlyList<Ps3SourceBitSidecarFrame> DetectAll(ReadOnlySpan<byte> body)
    {
        var frames = new List<Ps3SourceBitSidecarFrame>();
        if (body.Length < MinimumCommandPayloadBytes + Ps3SourceSendWrapper.BitSidecarCountBytes)
        {
            return frames;
        }

        // TF.elf appends [u16 bit-count BE][ceil(bit-count/8) sidecar bytes]
        // after the direct payload. The direct prefix length is caller-owned, so
        // collect every exact suffix boundary and let semantic decoders choose.
        for (var offset = MinimumCommandPayloadBytes; offset <= body.Length - Ps3SourceSendWrapper.BitSidecarCountBytes; offset++)
        {
            if (!LooksLikeRecoveredNativeSidecarPrefix(body[..offset]))
            {
                continue;
            }

            if (!Ps3SourceSendWrapper.TryDecodeBitSidecar(body, offset, out var bitCount, out var bitPayload)
                || bitCount == 0)
            {
                continue;
            }

            var sidecarBytes = Ps3SourceSendWrapper.BitSidecarCountBytes + bitPayload.Length;
            if (offset + sidecarBytes != body.Length)
            {
                continue;
            }

            frames.Add(new Ps3SourceBitSidecarFrame(offset, bitCount, bitPayload.Length));
        }

        return frames;
    }

    private static bool LooksLikeRecoveredNativeSidecarPrefix(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length == MinimumCommandPayloadBytes && IsAllZero(prefix))
        {
            return true;
        }

        if (StartsWithNativeDirectCommandMarker(prefix))
        {
            return true;
        }

        return prefix.Length >= 7
            && prefix[0] == 0x02
            && StartsWithNativeDirectCommandMarker(prefix[7..]);
    }

    private static bool StartsWithNativeDirectCommandMarker(ReadOnlySpan<byte> value)
    {
        return value.Length >= 4
            && value[0] == 0x01
            && value[1] == 0xb2
            && value[2] == 0xb8
            && value[3] == 0x7b;
    }

    private static bool IsAllZero(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            if (item != 0)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record Ps3SourceReliableAssociationProbe(
    byte MessageType,
    uint AssociationTokenBigEndian,
    uint AssociationTokenLittleEndian)
{
    public bool IsAssociationRequest => MessageType == 4;

    public uint NativeAssociationToken => AssociationTokenLittleEndian;

    public static byte[] Encode(byte messageType, uint nativeAssociationToken)
    {
        var body = new byte[5];
        body[0] = messageType;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(1), nativeAssociationToken);
        return body;
    }

    public static byte[] EncodeAssociationAck(uint nativeAssociationToken)
    {
        return Encode(5, nativeAssociationToken);
    }

    public static bool TryDecode(ReadOnlySpan<byte> body, out Ps3SourceReliableAssociationProbe? probe)
    {
        probe = null;
        if (body.Length != 5)
        {
            return false;
        }

        var bigEndian = (uint)((body[1] << 24) | (body[2] << 16) | (body[3] << 8) | body[4]);
        var littleEndian = (uint)(body[1] | (body[2] << 8) | (body[3] << 16) | (body[4] << 24));
        probe = new Ps3SourceReliableAssociationProbe(body[0], bigEndian, littleEndian);
        return true;
    }
}

public sealed record Ps3SourceAttachedClientFrame(
    byte Kind,
    ushort? DeclaredLength,
    uint? TokenBigEndian,
    uint? TokenLittleEndian)
{
    public uint? NativeToken => TokenLittleEndian;

    public static bool TryDecode(ReadOnlySpan<byte> body, out Ps3SourceAttachedClientFrame? frame)
    {
        frame = null;
        if (body.IsEmpty || body[0] is < 1 or > 4)
        {
            return false;
        }

        if (body[0] == 2)
        {
            if (body.Length < 7)
            {
                frame = new Ps3SourceAttachedClientFrame(body[0], null, null, null);
                return true;
            }

            var length = (ushort)(body[1] | (body[2] << 8));
            var tokenBigEndian = (uint)((body[3] << 24) | (body[4] << 16) | (body[5] << 8) | body[6]);
            var tokenLittleEndian = (uint)(body[3] | (body[4] << 8) | (body[5] << 16) | (body[6] << 24));
            frame = new Ps3SourceAttachedClientFrame(body[0], length, tokenBigEndian, tokenLittleEndian);
            return true;
        }

        if (body[0] == 4)
        {
            if (body.Length < 5)
            {
                frame = new Ps3SourceAttachedClientFrame(body[0], null, null, null);
                return true;
            }

            var tokenBigEndian = (uint)((body[1] << 24) | (body[2] << 16) | (body[3] << 8) | body[4]);
            var tokenLittleEndian = (uint)(body[1] | (body[2] << 8) | (body[3] << 16) | (body[4] << 24));
            frame = new Ps3SourceAttachedClientFrame(body[0], null, tokenBigEndian, tokenLittleEndian);
            return true;
        }

        if (body.Length > 16)
        {
            return false;
        }

        frame = new Ps3SourceAttachedClientFrame(body[0], null, null, null);
        return true;
    }
}

public sealed record Ps3SourceClientCommandIntent(
    short ForwardMove,
    short SideMove,
    short UpMove,
    float YawDelta,
    float PitchDelta,
    byte Buttons,
    byte WeaponSlotHint,
    byte TeamHint,
    byte ClassHint)
{
    public bool HasMovement => ForwardMove != 0 || SideMove != 0 || UpMove != 0;

    public static bool TryDecode(
        Ps3SourceClientPayloadInfo info,
        ReadOnlySpan<byte> body,
        out Ps3SourceClientCommandIntent intent,
        bool allowMarkerlessAssociatedObject = false)
    {
        intent = default!;
        var markerlessAssociatedObject = info.Role != Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame
            && info.BitSidecarOffset is null
            && string.Equals(
                info.PayloadObjectFrameKind,
                nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken),
                StringComparison.Ordinal);
        if (body.Length < 32
            || (markerlessAssociatedObject && !allowMarkerlessAssociatedObject)
            || info.Role is Ps3SourceClientPayloadRole.InitialHandoffProbe
                or Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.ShortControlAck
                or Ps3SourceClientPayloadRole.EmbeddedObjectNotice
                or Ps3SourceClientPayloadRole.AssociatedObjectStateReset
                or Ps3SourceClientPayloadRole.FragmentedClientPayload)
        {
            return false;
        }

        var commandBody = ExtractCommandBody(info, body);
        if (commandBody.Length < 32)
        {
            return false;
        }

        var forward = ReadBiasedCommandAxis(commandBody, commandBody.Length / 3);
        var side = ReadBiasedCommandAxis(commandBody, commandBody.Length / 2);
        var up = ReadBiasedCommandAxis(commandBody, Math.Max(0, commandBody.Length - 4));
        var yawDelta = ReadSigned16(commandBody, 0) / 32768.0f * 6.0f;
        var pitchDelta = ReadSigned16(commandBody, Math.Min(2, commandBody.Length - 2)) / 32768.0f * 3.0f;
        var buttons = commandBody[^1];
        var weaponSlotHint = (byte)(commandBody.Length > 7 ? commandBody[7] & 0x07 : 0);
        var teamHint = (byte)(2 + (commandBody.Length > 5 ? commandBody[5] & 0x01 : 0));
        var classHint = (byte)(1 + (commandBody.Length > 6 ? commandBody[6] % 9 : 0));
        intent = new Ps3SourceClientCommandIntent(
            forward,
            side,
            up,
            yawDelta,
            pitchDelta,
            buttons,
            weaponSlotHint,
            teamHint,
            classHint);
        return true;
    }

    private static ReadOnlySpan<byte> ExtractCommandBody(Ps3SourceClientPayloadInfo info, ReadOnlySpan<byte> body)
    {
        var payload = body;
        if (info.Role != Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame
            || info.AttachedFrameKind != 2
            || body.Length <= 7)
        {
            return StripBitSidecar(info, payload);
        }

        payload = body[7..];
        if (info.AttachedFrameDeclaredLength is not { } declaredLength || declaredLength == 0)
        {
            return StripBitSidecar(info, payload);
        }

        payload = payload[..Math.Min(payload.Length, declaredLength)];
        return StripBitSidecar(info, payload);
    }

    private static ReadOnlySpan<byte> StripBitSidecar(Ps3SourceClientPayloadInfo info, ReadOnlySpan<byte> payload)
    {
        if (info.BitSidecarOffset is { } offset && offset > 0 && offset <= payload.Length)
        {
            return payload[..offset];
        }

        if (!Ps3SourceBitSidecarFrame.TryDetect(payload, out var payloadSidecar) || payloadSidecar is null)
        {
            return payload;
        }

        return payload[..payloadSidecar.Offset];
    }

    private static short ReadBiasedCommandAxis(ReadOnlySpan<byte> body, int offset)
    {
        if (body.Length < 2)
        {
            return 0;
        }

        offset = Math.Clamp(offset, 0, body.Length - 2);
        var raw = ReadSigned16(body, offset);
        return (short)Math.Clamp(raw / 128, -450, 450);
    }

    private static short ReadSigned16(ReadOnlySpan<byte> body, int offset)
    {
        if (body.Length < 2)
        {
            return 0;
        }

        offset = Math.Clamp(offset, 0, body.Length - 2);
        return unchecked((short)((body[offset] << 8) | body[offset + 1]));
    }
}
