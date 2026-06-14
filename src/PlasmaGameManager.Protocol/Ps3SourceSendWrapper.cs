namespace PlasmaGameManager.Protocol;

public static class Ps3SourceSendWrapper
{
    public const int NativeStagingBufferBytes = 0x1000;
    public const int BitSidecarCountBytes = 2;
    public const int MaxBitSidecarBits = ushort.MaxValue;
    public const int QueuedCellAllocatorOffset = 0x228;
    public const int QueuedCellAllocationBytes = Ps3SourceTransportPacket.InlineQueueAllocationBytes;
    public const int QueuedCellPointerBytes = 4;
    public const int QueuedCellLengthBytes = 4;
    public const int InlineQueuedPayloadOffset = QueuedCellPointerBytes + QueuedCellLengthBytes;
    public const int InlineQueuedPayloadThresholdExclusive = Ps3SourceTransportPacket.InlineQueuePayloadCeiling + 1;
    public const int NativeQueuedPayloadThresholdExclusive = Ps3SourceTransportPacket.NativeQueueAllocationCeiling;
    public const int QueueChannelOneSentinelOffset = 0x258;
    public const int QueueChannelOneHeadOffset = 0x260;
    public const int QueueChannelOneWakeEventOffset = 0x268;
    public const int QueueChannelOneRecycleListOffset = 0x270;
    public const int QueueChannelZeroSentinelOffset = 0x278;
    public const int QueueChannelZeroHeadOffset = 0x280;
    public const int QueueChannelZeroWakeEventOffset = 0x288;
    public const int QueueChannelZeroRecycleListOffset = 0x290;
    public const ushort ImmediateSendChannelZeroPort = 0x697d;
    public const ushort ImmediateSendChannelOnePort = 0x6987;
    public const uint ReliablePeerLoopbackAddress = 0x7f000001;
    public const int ReliablePeerPrimaryQueueOffset = 0x28;
    public const int ReliablePeerSecondaryQueueOffset = 0x0c;
    public const int ReliablePeerLocalAddressOffset = 0x48;
    public const int ReliablePeerPrimaryPortOffset = 0x4c;
    public const int ReliablePeerSecondaryPortOffset = 0x4e;
    public const int ReliablePeerLastErrorOffset = 0x50;
    public const uint ReliablePeerUnavailableError = 0x80010006;
    public const uint ReliablePeerPortMismatchError = 0x80010016;
    public const int ReliablePeerSlotBytes = 0x18;
    public const int ReliablePeerQueueEntryBytes = 8;

    public static Ps3SourceStagedPayload StageNativePayload(
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> bitPayload,
        int bitCount,
        bool allowCompression)
    {
        var transformed = payload.ToArray();
        var wrappedOrCompressed = false;
        if (allowCompression && Ps3SourceLzss.TryEncode(payload, out var encoded))
        {
            transformed = encoded;
            wrappedOrCompressed = true;
        }

        var staged = StageDirectPayload(transformed, bitPayload, bitCount);
        return new Ps3SourceStagedPayload(
            staged,
            wrappedOrCompressed,
            payload.Length,
            bitCount);
    }

    public static byte[] StageDirectPayload(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> bitPayload, int bitCount)
    {
        var sidecar = EncodeBitSidecar(bitPayload, bitCount);
        var staged = new byte[payload.Length + sidecar.Length];
        payload.CopyTo(staged);
        sidecar.CopyTo(staged.AsSpan(payload.Length));
        return staged;
    }

    public static byte[] EncodeBitSidecar(ReadOnlySpan<byte> bitPayload, int bitCount)
    {
        if (bitCount == 0)
        {
            return [];
        }

        ValidateBitSidecar(bitPayload, bitCount);
        var byteCount = GetBitSidecarPayloadByteCount(bitCount);
        var sidecar = new byte[BitSidecarCountBytes + byteCount];
        sidecar[0] = (byte)(bitCount >> 8);
        sidecar[1] = (byte)bitCount;
        bitPayload[..byteCount].CopyTo(sidecar.AsSpan(BitSidecarCountBytes));
        return sidecar;
    }

    public static bool TryDecodeBitSidecar(
        ReadOnlySpan<byte> staged,
        int offset,
        out int bitCount,
        out byte[] bitPayload)
    {
        bitCount = 0;
        bitPayload = [];
        if (offset < 0 || staged.Length - offset < BitSidecarCountBytes)
        {
            return false;
        }

        bitCount = (staged[offset] << 8) | staged[offset + 1];
        if (bitCount == 0)
        {
            return staged.Length == offset + BitSidecarCountBytes;
        }

        var byteCount = GetBitSidecarPayloadByteCount(bitCount);
        if (staged.Length - offset - BitSidecarCountBytes < byteCount)
        {
            bitCount = 0;
            return false;
        }

        bitPayload = staged.Slice(offset + BitSidecarCountBytes, byteCount).ToArray();
        return true;
    }

    public static int GetBitSidecarPayloadByteCount(int bitCount)
    {
        if (bitCount < 0 || bitCount > MaxBitSidecarBits)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "PS3 Source bit sidecar count must fit u16.");
        }

        return (bitCount + 7) >> 3;
    }

    public static Ps3SourceQueuedPayloadStorage ClassifyQueuedPayloadStorage(int payloadByteLength)
    {
        if (payloadByteLength < 0 || payloadByteLength >= NativeQueuedPayloadThresholdExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadByteLength),
                payloadByteLength,
                $"PS3 Source queued payload length must be >= 0 and < 0x{NativeQueuedPayloadThresholdExclusive:x}.");
        }

        return payloadByteLength < InlineQueuedPayloadThresholdExclusive
            ? new Ps3SourceQueuedPayloadStorage(
                Ps3SourceQueuedPayloadStorageKind.InlineCell,
                payloadByteLength,
                QueuedCellAllocationBytes,
                InlineQueuedPayloadOffset,
                "008b9f70 stores cell[0] = cell + 8 and cell[1] = payload length.")
            : new Ps3SourceQueuedPayloadStorage(
                Ps3SourceQueuedPayloadStorageKind.HeapPointer,
                payloadByteLength,
                payloadByteLength,
                0,
                "008b9f70 stores cell[0] = heap allocation pointer and cell[1] = payload length.");
    }

    public static ushort GetImmediateSendPort(int channel)
    {
        return channel switch
        {
            0 => ImmediateSendChannelZeroPort,
            1 => ImmediateSendChannelOnePort,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "TF.elf immediate send channel must be 0 or 1.")
        };
    }

    public static Ps3SourceReliablePeerRoute SelectReliablePeerRoute(
        uint localAddress,
        ushort primaryLocalPort,
        ushort secondaryLocalPort,
        uint peerAddress,
        ushort peerPort)
    {
        if (peerAddress != ReliablePeerLoopbackAddress && peerAddress != localAddress)
        {
            return new Ps3SourceReliablePeerRoute(
                Ps3SourceReliablePeerRouteKind.RemotePeer,
                0,
                0,
                "00925858 routes non-loopback, non-local addresses through the remote peer send virtual call.");
        }

        if (peerPort == primaryLocalPort)
        {
            return new Ps3SourceReliablePeerRoute(
                Ps3SourceReliablePeerRouteKind.LocalPrimary,
                ReliablePeerPrimaryQueueOffset,
                0,
                "00925858 queues same-host traffic through the primary local queue at object+0x28.");
        }

        if (peerPort == secondaryLocalPort)
        {
            return new Ps3SourceReliablePeerRoute(
                Ps3SourceReliablePeerRouteKind.LocalSecondary,
                ReliablePeerSecondaryQueueOffset,
                0,
                "00925858 queues same-host traffic through the secondary local queue at object+0x0c.");
        }

        return new Ps3SourceReliablePeerRoute(
            Ps3SourceReliablePeerRouteKind.PortMismatch,
            0,
            ReliablePeerPortMismatchError,
            "00925858 writes 0x80010016 when same-host traffic does not target either local peer port.");
    }

    private static void ValidateBitSidecar(ReadOnlySpan<byte> bitPayload, int bitCount)
    {
        if (bitCount < 0 || bitCount > MaxBitSidecarBits)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "PS3 Source bit sidecar count must fit u16.");
        }

        var byteCount = GetBitSidecarPayloadByteCount(bitCount);
        if (bitPayload.Length < byteCount)
        {
            throw new ArgumentException("Bit payload does not contain enough bytes for the requested bit count.", nameof(bitPayload));
        }
    }
}

public sealed record Ps3SourceStagedPayload(
    byte[] Payload,
    bool WrappedOrCompressed,
    int OriginalPayloadLength,
    int BitSidecarBitCount);

public enum Ps3SourceQueuedPayloadStorageKind
{
    InlineCell,
    HeapPointer
}

public sealed record Ps3SourceQueuedPayloadStorage(
    Ps3SourceQueuedPayloadStorageKind Kind,
    int PayloadByteLength,
    int AllocationByteLength,
    int PayloadOffset,
    string NativeRule);

public enum Ps3SourceReliablePeerRouteKind
{
    RemotePeer,
    LocalPrimary,
    LocalSecondary,
    PortMismatch
}

public sealed record Ps3SourceReliablePeerRoute(
    Ps3SourceReliablePeerRouteKind Kind,
    int QueueOffset,
    uint ErrorCode,
    string NativeRule);
