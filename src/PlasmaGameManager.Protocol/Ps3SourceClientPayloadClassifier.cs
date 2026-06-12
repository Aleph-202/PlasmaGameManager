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
        Ps3SourceClientNetMessageDecoder.TryDecode(packet.Body, out var clientNetMessage);
        var nativeFrameKind = bitSidecar is not null
            ? Ps3SourceNativeFrameKind.DirectWithBitPayloadSidecarCandidate
            : nativeFrame.Kind;
        var role = ClassifyRole(packet, directionPacketCount, nativeFrame, shape, semantic, attachedFrame);
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

    private static Ps3SourceClientPayloadRole ClassifyRole(
        Ps3SourceTransportPacket packet,
        int directionPacketCount,
        Ps3SourceNativeFrameInfo nativeFrame,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourcePayloadSemanticInfo semantic,
        Ps3SourceAttachedClientFrame? attachedFrame)
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

    public const int PayloadObjectBufferFieldOffset = 0x18;

    public const int PayloadObjectBitreaderFieldOffsetValue = 0x1c;

    public const int PayloadObjectLengthFieldOffset = 0x40;

    public const int PayloadObjectOwnerLengthFieldOffset = 0x44;

    public bool ShouldTryInnerPayloadDecoding =>
        Kind != Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken
        || AssociatedObjectToken is > 0 and <= 0xffff;

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

        frame = new Ps3SourcePayloadObjectFrame(
            kind,
            header,
            signedHeader,
            innerPayloadOffset,
            body.Length - innerPayloadOffset,
            PayloadObjectBitreaderFieldOffsetValue,
            kind == Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken ? header : null,
            fragmentIndex,
            fragmentTotalCount,
            fragmentPacketCounter,
            fragmentWrappedOrCompressed);
        return true;
    }
}

public sealed record Ps3SourceBitSidecarFrame(
    int Offset,
    int BitCount,
    int PayloadLength)
{
    private const int MinimumCommandPayloadBytes = 32;

    public static bool TryDetect(ReadOnlySpan<byte> body, out Ps3SourceBitSidecarFrame? frame)
    {
        frame = null;
        if (body.Length < MinimumCommandPayloadBytes + Ps3SourceSendWrapper.BitSidecarCountBytes)
        {
            return false;
        }

        // TF.elf appends [u16 bit-count BE][ceil(bit-count/8) sidecar bytes]
        // after the direct payload. Without the native param_5 direct length,
        // find the last valid boundary and require enough prefix for a command.
        for (var offset = body.Length - Ps3SourceSendWrapper.BitSidecarCountBytes; offset >= MinimumCommandPayloadBytes; offset--)
        {
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

            frame = new Ps3SourceBitSidecarFrame(offset, bitCount, bitPayload.Length);
            return true;
        }

        return false;
    }
}

public sealed record Ps3SourceReliableAssociationProbe(
    byte MessageType,
    uint AssociationTokenBigEndian,
    uint AssociationTokenLittleEndian)
{
    public bool IsAssociationRequest => MessageType == 4;

    public uint NativeAssociationToken => AssociationTokenLittleEndian;

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

    public static bool TryDecode(Ps3SourceClientPayloadInfo info, ReadOnlySpan<byte> body, out Ps3SourceClientCommandIntent intent)
    {
        intent = default!;
        if (body.Length < 32
            || info.Role is Ps3SourceClientPayloadRole.InitialHandoffProbe
                or Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.ShortControlAck
                or Ps3SourceClientPayloadRole.EmbeddedObjectNotice
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
