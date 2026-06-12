namespace PlasmaGameManager.Protocol;

public enum Ps3SourceNativeToClcMoveBoundaryKind
{
    DirectRawBody,
    AttachedType2RawBody,
    BitSidecar,
    PayloadObjectInnerPayload
}

public sealed record Ps3SourceNativeToClcMoveBoundary(
    Ps3SourceNativeToClcMoveBoundaryKind Kind,
    int PayloadOffset,
    int PayloadLength,
    int PayloadBitCount,
    Ps3SourceClcMoveMessage Move,
    Ps3SourceClientCommandBatch Batch);

public static class Ps3SourceNativeToClcMoveBoundaryResolver
{
    public static bool TryResolve(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        return TryResolveBitSidecar(payloadInfo, body, out boundary)
            || TryResolveAttachedType2RawBody(payloadInfo, body, out boundary)
            || TryResolvePayloadObjectInnerPayload(payloadInfo, body, out boundary)
            || TryResolveDirectRawBody(payloadInfo, body, out boundary);
    }

    private static bool TryResolveBitSidecar(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        if (payloadInfo.BitSidecarOffset is not { } offset
            || !Ps3SourceSendWrapper.TryDecodeBitSidecar(body, offset, out var bitCount, out var bitPayload)
            || bitCount == 0)
        {
            return false;
        }

        return TryDecodeClcMovePayload(
            bitPayload,
            bitCount,
            Ps3SourceNativeToClcMoveBoundaryKind.BitSidecar,
            offset + Ps3SourceSendWrapper.BitSidecarCountBytes,
            bitPayload.Length,
            requireExactBitCount: true,
            out boundary);
    }

    private static bool TryResolveAttachedType2RawBody(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        if (payloadInfo.AttachedFrameKind != 2 || body.Length <= 7)
        {
            return false;
        }

        var payloadLength = body.Length - 7;
        if (payloadInfo.AttachedFrameDeclaredLength is { } declaredLength and > 0)
        {
            payloadLength = Math.Min(payloadLength, declaredLength);
        }

        if (payloadLength <= 0)
        {
            return false;
        }

        return TryDecodeClcMovePayload(
            body.Slice(7, payloadLength),
            payloadLength * 8,
            Ps3SourceNativeToClcMoveBoundaryKind.AttachedType2RawBody,
            PayloadOffset: 7,
            PayloadLength: payloadLength,
            requireExactBitCount: false,
            out boundary);
    }

    private static bool TryResolveDirectRawBody(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        if (body.Length == 0
            || payloadInfo.Role is Ps3SourceClientPayloadRole.InitialHandoffProbe
                or Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.ShortControlAck
                or Ps3SourceClientPayloadRole.EmbeddedObjectNotice)
        {
            return false;
        }

        return TryDecodeClcMovePayload(
            body,
            body.Length * 8,
            Ps3SourceNativeToClcMoveBoundaryKind.DirectRawBody,
            PayloadOffset: 0,
            PayloadLength: body.Length,
            requireExactBitCount: false,
            out boundary);
    }

    private static bool TryResolvePayloadObjectInnerPayload(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        if (payloadInfo.PayloadObjectInnerPayloadOffset is not { } offset
            || payloadInfo.PayloadObjectInnerPayloadLength is not { } length
            || !IsPayloadObjectInnerDecodeCandidate(payloadInfo)
            || offset < Ps3SourcePayloadObjectFrame.HeaderBytes
            || length <= 0
            || offset > body.Length
            || offset + length > body.Length)
        {
            return false;
        }

        return TryDecodeClcMovePayload(
            body.Slice(offset, length),
            length * 8,
            Ps3SourceNativeToClcMoveBoundaryKind.PayloadObjectInnerPayload,
            PayloadOffset: offset,
            PayloadLength: length,
            requireExactBitCount: false,
            out boundary);
    }

    private static bool IsPayloadObjectInnerDecodeCandidate(Ps3SourceClientPayloadInfo payloadInfo)
    {
        if (payloadInfo.PayloadObjectFrameKind is null)
        {
            return false;
        }

        return payloadInfo.PayloadObjectFrameKind != nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken)
            || payloadInfo.PayloadObjectAssociatedToken is > 0 and <= 0xffff;
    }

    private static bool TryDecodeClcMovePayload(
        ReadOnlySpan<byte> payload,
        int bitCount,
        Ps3SourceNativeToClcMoveBoundaryKind kind,
        int PayloadOffset,
        int PayloadLength,
        bool requireExactBitCount,
        out Ps3SourceNativeToClcMoveBoundary boundary)
    {
        boundary = default!;
        foreach (var includesMessageType in new[] { true, false })
        {
            if (!Ps3SourceClcMoveMessage.TryDecode(payload, includesMessageType, out var move)
                || move.TotalCommands == 0
                || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
                || move.TotalBitsConsumed > bitCount
                || (requireExactBitCount && move.TotalBitsConsumed != bitCount)
                || (!requireExactBitCount && !HasOnlyZeroPadding(payload, move.TotalBitsConsumed, bitCount))
                || !move.TryDecodeUserCmdBatch(default, out var batch)
                || batch.ConsumedBits != move.CommandDataBitCount)
            {
                continue;
            }

            boundary = new Ps3SourceNativeToClcMoveBoundary(
                kind,
                PayloadOffset,
                PayloadLength,
                bitCount,
                move,
                batch);
            return true;
        }

        return false;
    }

    private static bool HasOnlyZeroPadding(ReadOnlySpan<byte> payload, int fromBit, int toBit)
    {
        if (fromBit > toBit || toBit > payload.Length * 8)
        {
            return false;
        }

        for (var bit = fromBit; bit < toBit; bit++)
        {
            if (((payload[bit >> 3] >> (bit & 7)) & 1) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
