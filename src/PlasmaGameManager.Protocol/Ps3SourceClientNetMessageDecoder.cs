namespace PlasmaGameManager.Protocol;

public enum Ps3SourceClientNetMessagePayloadKind
{
    DirectRawBody,
    AttachedType2Payload,
    BitSidecar,
    PayloadObjectInnerPayload,
    OwnerSlot8Bitstream,
    OwnerForwarderWord6Bitstream,
    OwnerForwarderDeferredPointerWord6Bitstream,
    OwnerForwarderConfigFallbackWord4Bitstream
}

public sealed record Ps3SourceDecodedClientNetMessage(
    Ps3SourceClientNetMessagePayloadKind PayloadKind,
    int PayloadOffset,
    int PayloadLength,
    int PayloadBitCount,
    int MessageType,
    string MessageName,
    object Message);

public sealed record Ps3SourceClientNetMessageDecodeStrength(
    bool IsStrong,
    int? ConsumedBits,
    int UnconsumedBits,
    bool UnconsumedBitsAreZero,
    string Reason);

public static class Ps3SourceClientNetMessageDecoder
{
    private static readonly string[] FileCrcFilenamePrefixes = ["", "materials", "models", "sounds", "scripts"];

    public static bool TryDecode(ReadOnlySpan<byte> body, out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;

        foreach (var bitSidecar in Ps3SourceBitSidecarFrame.DetectAll(body))
        {
            if (Ps3SourceSendWrapper.TryDecodeBitSidecar(
                    body,
                    bitSidecar.Offset,
                    out var sidecarBitCount,
                    out var sidecarPayload)
                && TryDecodePayload(
                    sidecarPayload,
                    sidecarBitCount,
                    Ps3SourceClientNetMessagePayloadKind.BitSidecar,
                    bitSidecar.Offset + Ps3SourceSendWrapper.BitSidecarCountBytes,
                    sidecarPayload.Length,
                    out message))
            {
                return true;
            }
        }

        if (Ps3SourceAttachedClientFrame.TryDecode(body, out var attached)
            && attached is { Kind: 2 }
            && body.Length > 7)
        {
            var payloadLength = body.Length - 7;
            if (attached.DeclaredLength is { } declaredLength and > 0)
            {
                payloadLength = Math.Min(payloadLength, declaredLength);
            }

            if (payloadLength > 0
                && TryDecodePayload(
                    body.Slice(7, payloadLength),
                    payloadLength * 8,
                    Ps3SourceClientNetMessagePayloadKind.AttachedType2Payload,
                    7,
                    payloadLength,
                    out message))
            {
                return true;
            }
        }

        if (TryDecodeOwnerSlot8Bitstream(body, out message)
            || TryDecodeOwnerForwarderBitstream(
                body,
                Ps3SourceOwnerForwarderBitstreamLayout.Word6PayloadWord7ReaderWord16,
                Ps3SourceClientNetMessagePayloadKind.OwnerForwarderWord6Bitstream,
                out message)
            || TryDecodeOwnerForwarderBitstream(
                body,
                Ps3SourceOwnerForwarderBitstreamLayout.DeferredPointerWord6PayloadWord10PointerWord19,
                Ps3SourceClientNetMessagePayloadKind.OwnerForwarderDeferredPointerWord6Bitstream,
                out message)
            || TryDecodeOwnerForwarderBitstream(
                body,
                Ps3SourceOwnerForwarderBitstreamLayout.ConfigFallbackWord4PayloadWord5ReaderWord14,
                Ps3SourceClientNetMessagePayloadKind.OwnerForwarderConfigFallbackWord4Bitstream,
                out message))
        {
            return true;
        }

        if (Ps3SourcePayloadObjectFrame.TryDecode(body, out var payloadObject)
            && payloadObject is not null
            && payloadObject.ShouldTryInnerPayloadDecoding
            && payloadObject.InnerPayloadLength > 0)
        {
            var inner = payloadObject.SliceInnerPayload(body);
            if (!inner.IsEmpty
                && TryDecodePayload(
                    inner,
                    inner.Length * 8,
                    Ps3SourceClientNetMessagePayloadKind.PayloadObjectInnerPayload,
                    payloadObject.InnerPayloadOffset,
                    inner.Length,
                    out message))
            {
                return true;
            }
        }

        return TryDecodePayload(
            body,
            body.Length * 8,
            Ps3SourceClientNetMessagePayloadKind.DirectRawBody,
            0,
            body.Length,
            out message);
    }

    public static Ps3SourceClientNetMessageDecodeStrength AssessDecodeStrength(
        Ps3SourceDecodedClientNetMessage message,
        ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!TryEstimateConsumedBits(message.Message, out var consumedBits))
        {
            return new Ps3SourceClientNetMessageDecodeStrength(
                false,
                null,
                Math.Max(0, message.PayloadBitCount),
                false,
                "decoded first message type, but this message shape does not yet expose an exact consumed-bit estimate");
        }

        if (consumedBits > message.PayloadBitCount)
        {
            return new Ps3SourceClientNetMessageDecodeStrength(
                false,
                consumedBits,
                0,
                false,
                $"decoded {message.MessageName} consumes {consumedBits} bits, which exceeds the advertised {message.PayloadBitCount}-bit payload");
        }

        if (!IsSemanticallyPlausible(message.Message, out var semanticReason))
        {
            return new Ps3SourceClientNetMessageDecodeStrength(
                false,
                consumedBits,
                Math.Max(0, message.PayloadBitCount - consumedBits),
                false,
                $"decoded {message.MessageName} has implausible client fields: {semanticReason}");
        }

        var unconsumedBits = message.PayloadBitCount - consumedBits;
        if (unconsumedBits == 0)
        {
            return new Ps3SourceClientNetMessageDecodeStrength(
                true,
                consumedBits,
                0,
                true,
                "decoded message consumes the advertised payload bit count exactly");
        }

        if (TryAssessConcatenatedMessageStream(payload, message.PayloadBitCount, consumedBits, out var streamMessageCount, out var streamConsumedBits))
        {
            return new Ps3SourceClientNetMessageDecodeStrength(
                true,
                streamConsumedBits,
                message.PayloadBitCount - streamConsumedBits,
                true,
                $"decoded {streamMessageCount} concatenated Source client net messages and only zero padding remains");
        }

        var trailingZero = HasOnlyZeroBits(payload, consumedBits, message.PayloadBitCount);
        return new Ps3SourceClientNetMessageDecodeStrength(
            trailingZero,
            consumedBits,
            unconsumedBits,
            trailingZero,
            trailingZero
                ? $"decoded {message.MessageName} leaves {unconsumedBits} zero padding bits"
                : $"decoded only the first {consumedBits} bits of a {message.PayloadBitCount}-bit payload; remaining non-zero bits require a recovered multi-message stream or wrapper transform");
    }

    private static bool TryDecodeOwnerSlot8Bitstream(
        ReadOnlySpan<byte> body,
        out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;
        if (!Ps3SourcePayloadObjectFrame.TryDecode(body, out var payloadObject)
            || payloadObject is not { Kind: Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control }
            || payloadObject.PayloadObjectBitreaderFieldOffset != Ps3SourcePayloadObjectFrame.OwnerSlot8RebuiltBitreaderFieldOffsetValue
            || !Ps3SourcePayloadObjectFrame.TryReadOwnerSlot8Bitstream(body, out var bitCount, out var payloadLength)
            || payloadLength <= 0
            || Ps3SourcePayloadObjectFrame.OwnerSlot8BitPayloadOffset + payloadLength > body.Length)
        {
            return false;
        }

        return TryDecodePayload(
            body.Slice(Ps3SourcePayloadObjectFrame.OwnerSlot8BitPayloadOffset, payloadLength),
            bitCount,
            Ps3SourceClientNetMessagePayloadKind.OwnerSlot8Bitstream,
            Ps3SourcePayloadObjectFrame.OwnerSlot8BitPayloadOffset,
            payloadLength,
            out message);
    }

    private static bool TryDecodeOwnerForwarderBitstream(
        ReadOnlySpan<byte> body,
        Ps3SourceOwnerForwarderBitstreamLayout layout,
        Ps3SourceClientNetMessagePayloadKind payloadKind,
        out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;
        if (!Ps3SourcePayloadObjectFrame.TryReadOwnerForwarderBitstream(
                body,
                layout,
                out var bitCount,
                out var payloadOffset,
                out var payloadLength,
                out _)
            || bitCount <= 0
            || payloadLength <= 0
            || payloadOffset + payloadLength > body.Length)
        {
            return false;
        }

        return TryDecodePayload(
            body.Slice(payloadOffset, payloadLength),
            bitCount,
            payloadKind,
            payloadOffset,
            payloadLength,
            out message);
    }

    private static bool TryDecodePayload(
        ReadOnlySpan<byte> payload,
        int payloadBitCount,
        Ps3SourceClientNetMessagePayloadKind payloadKind,
        int payloadOffset,
        int payloadLength,
        out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;
        if (payloadBitCount < Ps3SourceNetMessageConstants.NetMessageTypeBits
            || !Ps3SourceNetMessages.TryReadMessageType(payload, out var messageType))
        {
            return false;
        }

        return messageType switch
        {
            Ps3SourceNetMessageConstants.NetTick
                => TryDecodeTyped<Ps3SourceNetTick>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "NET_Tick", Ps3SourceNetMessages.TryDecodeNetTick, out message),
            Ps3SourceNetMessageConstants.NetStringCmd
                => TryDecodeTyped<Ps3SourceNetStringCmd>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "NET_StringCmd", Ps3SourceNetMessages.TryDecodeNetStringCmd, out message),
            Ps3SourceNetMessageConstants.NetSetConVar
                => TryDecodeTyped<Ps3SourceNetSetConVar>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "NET_SetConVar", Ps3SourceNetMessages.TryDecodeNetSetConVar, out message),
            Ps3SourceNetMessageConstants.NetSignonState
                => TryDecodeTyped<Ps3SourceNetSignonState>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "NET_SignonState", Ps3SourceNetMessages.TryDecodeNetSignonState, out message),
            Ps3SourceNetMessageConstants.ClcClientInfo
                => TryDecodeTyped<Ps3SourceClcClientInfo>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_ClientInfo", Ps3SourceNetMessages.TryDecodeClientInfo, out message),
            Ps3SourceNetMessageConstants.ClcVoiceData
                => TryDecodeTyped<Ps3SourceClcVoiceData>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_VoiceData", Ps3SourceNetMessages.TryDecodeVoiceData, out message),
            Ps3SourceNetMessageConstants.ClcBaselineAck
                => TryDecodeTyped<Ps3SourceClcBaselineAck>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_BaselineAck", Ps3SourceNetMessages.TryDecodeBaselineAck, out message),
            Ps3SourceNetMessageConstants.ClcListenEvents
                => TryDecodeTyped<Ps3SourceClcListenEvents>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_ListenEvents", Ps3SourceNetMessages.TryDecodeListenEvents, out message),
            Ps3SourceNetMessageConstants.ClcRespondCvarValue
                => TryDecodeTyped<Ps3SourceClcRespondCvarValue>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_RespondCvarValue", Ps3SourceNetMessages.TryDecodeRespondCvarValue, out message),
            Ps3SourceNetMessageConstants.ClcFileCrcCheck
                => TryDecodeTyped<Ps3SourceClcFileCrcCheck>(payload, payloadBitCount, payloadKind, payloadOffset, payloadLength, messageType, "CLC_FileCRCCheck", Ps3SourceNetMessages.TryDecodeFileCrcCheck, out message),
            _ => false
        };
    }

    private static bool TryDecodeTyped<TMessage>(
        ReadOnlySpan<byte> payload,
        int payloadBitCount,
        Ps3SourceClientNetMessagePayloadKind payloadKind,
        int payloadOffset,
        int payloadLength,
        int messageType,
        string messageName,
        TryDecodeClientNetMessage<TMessage> decoder,
        out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;
        if (!decoder(payload, includesMessageType: true, out var typedMessage))
        {
            return false;
        }

        message = new Ps3SourceDecodedClientNetMessage(
            payloadKind,
            payloadOffset,
            payloadLength,
            payloadBitCount,
            messageType,
            messageName,
            typedMessage!);
        return true;
    }

    private static bool TryEstimateConsumedBits(object message, out int consumedBits)
    {
        const int messageTypeBits = Ps3SourceNetMessageConstants.NetMessageTypeBits;
        consumedBits = 0;

        switch (message)
        {
            case Ps3SourceNetTick:
                consumedBits = messageTypeBits + 32;
                return true;
            case Ps3SourceNetStringCmd stringCmd:
                consumedBits = messageTypeBits + StringBits(stringCmd.Command);
                return true;
            case Ps3SourceNetSetConVar setConVar:
                consumedBits = messageTypeBits + 8;
                foreach (var pair in setConVar.Values)
                {
                    consumedBits += StringBits(pair.Name) + StringBits(pair.Value);
                }

                return true;
            case Ps3SourceNetSignonState:
                consumedBits = messageTypeBits + 8 + 32;
                return true;
            case Ps3SourceClcClientInfo clientInfo:
                consumedBits = messageTypeBits + 32 + 32 + 1 + 32 + StringBits(clientInfo.FriendsName);
                foreach (var customFile in clientInfo.CustomFiles)
                {
                    consumedBits += 1;
                    if (customFile != 0)
                    {
                        consumedBits += 32;
                    }
                }

                return true;
            case Ps3SourceClcVoiceData voiceData:
                consumedBits = messageTypeBits + 16 + voiceData.DataBitCount;
                return true;
            case Ps3SourceClcBaselineAck:
                consumedBits = messageTypeBits + 32 + 1;
                return true;
            case Ps3SourceClcListenEvents listenEvents:
                consumedBits = messageTypeBits + listenEvents.EventMaskWords.Count * 32;
                return true;
            case Ps3SourceClcRespondCvarValue cvarValue:
                consumedBits = messageTypeBits + 32 + 4 + StringBits(cvarValue.CvarName) + StringBits(cvarValue.CvarValue);
                return true;
            case Ps3SourceClcFileCrcCheck fileCrc:
                if (!TryEstimateFileCrcCheckBits(fileCrc, out var fileCrcBits))
                {
                    return false;
                }

                consumedBits = fileCrcBits;
                return true;
            default:
                return false;
        }
    }

    private static bool IsSemanticallyPlausible(object message, out string reason)
    {
        reason = "";
        switch (message)
        {
            case Ps3SourceNetTick tick:
                return InRange(tick.Tick, 0, 10_000_000, "tick", out reason);

            case Ps3SourceNetStringCmd stringCmd:
                if (!IsPrintableClientText(stringCmd.Command, allowEmpty: false))
                {
                    reason = "NET_StringCmd command is empty or contains non-printable bytes";
                    return false;
                }

                return true;

            case Ps3SourceNetSetConVar setConVar:
                if (setConVar.Values.Count is <= 0 or > 64)
                {
                    reason = $"NET_SetConVar count {setConVar.Values.Count} is outside 1..64";
                    return false;
                }

                foreach (var pair in setConVar.Values)
                {
                    if (!IsPrintableClientText(pair.Name, allowEmpty: false)
                        || !IsPrintableClientText(pair.Value, allowEmpty: true))
                    {
                        reason = "NET_SetConVar contains a non-printable name/value";
                        return false;
                    }
                }

                return true;

            case Ps3SourceNetSignonState signon:
                if (signon.SignonState > 7)
                {
                    reason = $"signon state {signon.SignonState} is outside 0..7";
                    return false;
                }

                return InRange(signon.SpawnCount, 0, 10_000_000, "spawn count", out reason);

            case Ps3SourceClcClientInfo clientInfo:
                if (clientInfo.IsHltv)
                {
                    reason = "PS3 client info unexpectedly marks itself as HLTV";
                    return false;
                }

                if (!IsPrintableClientText(clientInfo.FriendsName, allowEmpty: false))
                {
                    reason = "client friends name is empty or non-printable";
                    return false;
                }

                return true;

            case Ps3SourceClcVoiceData voiceData:
                if (voiceData.DataBitCount < 0 || voiceData.DataBitCount > voiceData.Data.Length * 8)
                {
                    reason = $"voice bit count {voiceData.DataBitCount} exceeds payload bytes";
                    return false;
                }

                return true;

            case Ps3SourceClcBaselineAck ack:
                if (!InRange(ack.BaselineTick, -1, 10_000_000, "baseline tick", out reason))
                {
                    return false;
                }

                if (ack.BaselineNumber is not 0 and not 1)
                {
                    reason = $"baseline number {ack.BaselineNumber} is not 0 or 1";
                    return false;
                }

                return true;

            case Ps3SourceClcListenEvents listen:
                var setBits = 0;
                foreach (var word in listen.EventMaskWords)
                {
                    setBits += CountSetBits(word);
                }

                if (setBits > 128)
                {
                    reason = $"listen-events mask has {setBits} bits set";
                    return false;
                }

                return true;

            case Ps3SourceClcRespondCvarValue cvar:
                if (cvar.StatusCode is < 0 or > 3)
                {
                    reason = $"respond-cvar status {cvar.StatusCode} is outside 0..3";
                    return false;
                }

                if (!IsPrintableClientText(cvar.CvarName, allowEmpty: false)
                    || !IsPrintableClientText(cvar.CvarValue, allowEmpty: true))
                {
                    reason = "respond-cvar contains non-printable name/value";
                    return false;
                }

                return true;

            case Ps3SourceClcFileCrcCheck fileCrc:
                if (!IsPrintableClientText(fileCrc.PathId, allowEmpty: true)
                    || !IsPrintableClientText(fileCrc.Filename, allowEmpty: false))
                {
                    reason = "file CRC path or filename is non-printable";
                    return false;
                }

                if (!LooksLikeSourcePath(fileCrc.Filename))
                {
                    reason = $"file CRC filename '{fileCrc.Filename}' does not look like a Source asset path";
                    return false;
                }

                return true;

            default:
                return true;
        }
    }

    private static bool InRange(int value, int min, int max, string name, out string reason)
    {
        if (value < min || value > max)
        {
            reason = $"{name} {value} is outside {min}..{max}";
            return false;
        }

        reason = "";
        return true;
    }

    private static bool IsPrintableClientText(string value, bool allowEmpty)
    {
        if (value.Length == 0)
        {
            return allowEmpty;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is '\t' or '\n' or '\r')
            {
                continue;
            }

            if (c < 0x20 || c > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeSourcePath(string value)
    {
        if (value.Length == 0 || !IsPrintableClientText(value, allowEmpty: false))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/');
        return normalized.Contains('.', StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.StartsWith("materials", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("models", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("scripts", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("sound", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountSetBits(uint value)
    {
        var count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    private static bool TryEstimateFileCrcCheckBits(Ps3SourceClcFileCrcCheck message, out int consumedBits)
    {
        consumedBits = Ps3SourceNetMessageConstants.NetMessageTypeBits + 1 + 2;
        if (message.PathIdCode == 0)
        {
            consumedBits += StringBits(message.PathId);
        }

        consumedBits += 3;
        if (message.FilenamePrefixCode == 0)
        {
            consumedBits += StringBits(message.Filename);
        }
        else
        {
            if (message.FilenamePrefixCode < 0 || message.FilenamePrefixCode >= FileCrcFilenamePrefixes.Length)
            {
                return false;
            }

            consumedBits += StringBits(ExtractFileCrcTail(FileCrcFilenamePrefixes[message.FilenamePrefixCode], message.Filename));
        }

        consumedBits += 32;
        return true;
    }

    private static string ExtractFileCrcTail(string prefix, string filename)
    {
        if (prefix.Length == 0)
        {
            return filename;
        }

        if (filename.Equals(prefix, StringComparison.Ordinal))
        {
            return "";
        }

        if (filename.StartsWith(prefix + "/", StringComparison.Ordinal)
            || filename.StartsWith(prefix + "\\", StringComparison.Ordinal))
        {
            return filename[(prefix.Length + 1)..];
        }

        return filename;
    }

    private static int StringBits(string value)
    {
        return checked((value.Length + 1) * 8);
    }

    private static bool HasOnlyZeroBits(ReadOnlySpan<byte> payload, int startBit, int endBitExclusive)
    {
        var cappedEndBit = Math.Min(endBitExclusive, payload.Length * 8);
        for (var bit = startBit; bit < cappedEndBit; bit++)
        {
            if (((payload[bit >> 3] >> (bit & 7)) & 1) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryAssessConcatenatedMessageStream(
        ReadOnlySpan<byte> payload,
        int payloadBitCount,
        int firstConsumedBits,
        out int messageCount,
        out int totalConsumedBits)
    {
        messageCount = 1;
        totalConsumedBits = firstConsumedBits;

        while (totalConsumedBits < payloadBitCount)
        {
            if (HasOnlyZeroBits(payload, totalConsumedBits, payloadBitCount))
            {
                return true;
            }

            var remainingBits = payloadBitCount - totalConsumedBits;
            var remainingPayload = ExtractBitSlice(payload, totalConsumedBits, remainingBits);
            if (!TryDecodePayload(
                    remainingPayload,
                    remainingBits,
                    Ps3SourceClientNetMessagePayloadKind.DirectRawBody,
                    0,
                    remainingPayload.Length,
                    out var nextMessage)
                || !TryEstimateConsumedBits(nextMessage.Message, out var nextConsumedBits)
                || nextConsumedBits <= 0
                || nextConsumedBits > remainingBits)
            {
                return false;
            }

            totalConsumedBits += nextConsumedBits;
            messageCount++;
            if (messageCount > 64)
            {
                return false;
            }
        }

        return totalConsumedBits == payloadBitCount;
    }

    private static byte[] ExtractBitSlice(ReadOnlySpan<byte> payload, int startBit, int bitCount)
    {
        var result = new byte[(bitCount + 7) >> 3];
        for (var bit = 0; bit < bitCount; bit++)
        {
            var sourceBit = startBit + bit;
            if (sourceBit >= payload.Length * 8)
            {
                break;
            }

            if (((payload[sourceBit >> 3] >> (sourceBit & 7)) & 1) != 0)
            {
                result[bit >> 3] |= (byte)(1 << (bit & 7));
            }
        }

        return result;
    }

    private delegate bool TryDecodeClientNetMessage<TMessage>(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out TMessage message);
}
