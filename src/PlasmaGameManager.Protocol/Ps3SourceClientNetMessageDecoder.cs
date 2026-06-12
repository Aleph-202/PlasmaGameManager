namespace PlasmaGameManager.Protocol;

public enum Ps3SourceClientNetMessagePayloadKind
{
    DirectRawBody,
    AttachedType2Payload,
    BitSidecar,
    PayloadObjectInnerPayload
}

public sealed record Ps3SourceDecodedClientNetMessage(
    Ps3SourceClientNetMessagePayloadKind PayloadKind,
    int PayloadOffset,
    int PayloadLength,
    int PayloadBitCount,
    int MessageType,
    string MessageName,
    object Message);

public static class Ps3SourceClientNetMessageDecoder
{
    public static bool TryDecode(ReadOnlySpan<byte> body, out Ps3SourceDecodedClientNetMessage message)
    {
        message = default!;

        if (Ps3SourceBitSidecarFrame.TryDetect(body, out var bitSidecar)
            && bitSidecar is not null
            && Ps3SourceSendWrapper.TryDecodeBitSidecar(
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

    private delegate bool TryDecodeClientNetMessage<TMessage>(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out TMessage message);
}
