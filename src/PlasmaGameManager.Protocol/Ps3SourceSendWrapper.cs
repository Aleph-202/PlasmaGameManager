namespace PlasmaGameManager.Protocol;

public static class Ps3SourceSendWrapper
{
    public const int NativeStagingBufferBytes = 0x1000;
    public const int BitSidecarCountBytes = 2;
    public const int MaxBitSidecarBits = ushort.MaxValue;

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
