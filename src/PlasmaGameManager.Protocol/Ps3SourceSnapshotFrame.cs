namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceSnapshotFrame(
    int FrameIndex,
    int BaseOrAckFrame,
    byte UpdateFlags,
    byte PendingCount,
    bool HasEntityDelta,
    IReadOnlyList<byte[]> ExtraSections)
{
    public static Ps3SourceSnapshotFrame Empty(int frameIndex, int baseOrAckFrame)
    {
        return new Ps3SourceSnapshotFrame(frameIndex, baseOrAckFrame, 0, 0, false, []);
    }
}

public sealed record Ps3SourceDecodedSnapshotFrame(
    Ps3SourceSnapshotFrameHeader Header,
    Ps3SourceDecodedSnapshotEntityDeltaSection? EntityDeltaSection,
    byte[] TrailingPayload,
    int ConsumedBytes,
    string ContractSource);

public sealed record Ps3SourceDecodedSnapshotEntityDeltaSection(
    int OffsetBytes,
    int ByteLength,
    int ConsumedBits,
    Ps3SourceEntityDeltaNativeRecord[] Records);

public static class Ps3SourceSnapshotFrameBuilder
{
    public static byte[] Encode(Ps3SourceSnapshotFrame frame)
    {
        var writer = new SnapshotBitWriter();
        writer.WriteSigned32(frame.FrameIndex);
        writer.WriteSigned32(frame.BaseOrAckFrame);
        writer.WriteByte(0);

        var flags = frame.UpdateFlags;
        if (frame.PendingCount > 0)
        {
            flags |= 0x10;
        }

        if (frame.HasEntityDelta)
        {
            flags |= 0x01;
        }

        writer.WriteByte(flags);
        if (frame.PendingCount > 0)
        {
            writer.WriteByte(frame.PendingCount);
        }

        foreach (var section in frame.ExtraSections)
        {
            writer.WriteBytes(section);
        }

        writer.PadZeroBits(5);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Ps3SourceDecodedSnapshotFrame frame)
    {
        frame = default!;
        if (!TryDecodeHeader(payload, out var header))
        {
            return false;
        }

        var offset = header.ConsumedBytes;
        Ps3SourceDecodedSnapshotEntityDeltaSection? entityDelta = null;
        if (header.HasEntityDelta && offset < payload.Length)
        {
            var tail = payload[offset..];
            if (Ps3SourceEntityDeltaFrameBuilder.TryDecodeNativeRecords(
                    tail,
                    out var records,
                    out var consumedBits)
                && records.Length > 0
                && consumedBits > 0)
            {
                var consumedBytes = (consumedBits + 7) >> 3;
                entityDelta = new Ps3SourceDecodedSnapshotEntityDeltaSection(
                    offset,
                    consumedBytes,
                    consumedBits,
                    records);
                offset += consumedBytes;
            }
        }

        frame = new Ps3SourceDecodedSnapshotFrame(
            header,
            entityDelta,
            payload[offset..].ToArray(),
            offset,
            "TF.elf snapshot route 00a61150 header + 00a5fb80 entity-delta prefix");
        return true;
    }

    public static bool TryDecodeHeader(ReadOnlySpan<byte> payload, out Ps3SourceSnapshotFrameHeader header)
    {
        header = default;
        var reader = new SnapshotBitReader(payload);
        if (!reader.TryReadSigned32(out var frameIndex)
            || !reader.TryReadSigned32(out var baseOrAckFrame)
            || !reader.TryReadByte(out var messageId)
            || !reader.TryReadByte(out var flags))
        {
            return false;
        }

        byte? pendingCount = null;
        if ((flags & 0x10) != 0)
        {
            if (!reader.TryReadByte(out var count))
            {
                return false;
            }

            pendingCount = count;
        }

        header = new Ps3SourceSnapshotFrameHeader(
            frameIndex,
            baseOrAckFrame,
            messageId,
            flags,
            pendingCount,
            (flags & 0x01) != 0,
            reader.ConsumedBytes);
        return true;
    }

    private sealed class SnapshotBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitLength;

        public void WriteByte(byte value)
        {
            WriteBits(value, 8);
        }

        public void WriteBytes(ReadOnlySpan<byte> values)
        {
            foreach (var value in values)
            {
                WriteByte(value);
            }
        }

        public void WriteSigned32(int value)
        {
            WriteSigned(value, 32);
        }

        public void PadZeroBits(int bitCount)
        {
            WriteBits(0, bitCount);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }

        private void WriteSigned(int value, int width)
        {
            var payloadBits = width - 1;
            var payloadMask = payloadBits == 31 ? 0x7fffffffu : (1u << payloadBits) - 1u;
            var encoded = (uint)value & payloadMask;
            WriteBits(encoded, payloadBits);
            WriteBits(value < 0 ? 1u : 0u, 1);
        }

        private void WriteBits(uint value, int bitCount)
        {
            for (var i = 0; i < bitCount; i++)
            {
                if ((_bitLength & 7) == 0)
                {
                    _bytes.Add(0);
                }

                if (((value >> i) & 1) != 0)
                {
                    _bytes[^1] |= (byte)(1 << (_bitLength & 7));
                }

                _bitLength++;
            }
        }
    }

    private sealed class SnapshotBitReader(ReadOnlySpan<byte> bytes)
    {
        private readonly byte[] _bytes = bytes.ToArray();
        private int _bitOffset;

        public int ConsumedBytes => (_bitOffset + 7) >> 3;

        public bool TryReadByte(out byte value)
        {
            if (!TryReadBits(8, out var raw))
            {
                value = 0;
                return false;
            }

            value = (byte)raw;
            return true;
        }

        public bool TryReadSigned32(out int value)
        {
            if (!TryReadBits(31, out var payload) || !TryReadBits(1, out var sign))
            {
                value = 0;
                return false;
            }

            value = (int)(payload | (sign == 0 ? 0u : 0x80000000u));
            return true;
        }

        private bool TryReadBits(int count, out uint value)
        {
            value = 0;
            if (count is < 0 or > 32 || _bitOffset + count > _bytes.Length * 8)
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                var bit = (_bytes[_bitOffset >> 3] >> (_bitOffset & 7)) & 1;
                value |= (uint)(bit << i);
                _bitOffset++;
            }

            return true;
        }
    }
}

public readonly record struct Ps3SourceSnapshotFrameHeader(
    int FrameIndex,
    int BaseOrAckFrame,
    byte MessageId,
    byte UpdateFlags,
    byte? PendingCount,
    bool HasEntityDelta,
    int ConsumedBytes);
