using System.Buffers.Binary;

namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceObjectStreamRecord(
    byte MessageKind,
    uint OwnerOrCallbackId,
    uint Sequence,
    byte[] Payload,
    int PayloadBitCount,
    byte[]? Sidecar = null);

public sealed record Ps3SourceDecodedObjectStreamRecord(
    byte MessageKind,
    uint OwnerOrCallbackId,
    uint SequenceA,
    uint SequenceB,
    byte[] Sidecar,
    byte[] Payload);

public static class Ps3SourceObjectStream
{
    public const int SidecarByteCount = 0x4c;
    public const int TerminatorBitCount = 5;
    public const int HeaderByteCount = 1 + sizeof(uint) + SidecarByteCount + sizeof(uint) + sizeof(uint) + sizeof(uint);

    public static byte[] Encode(Ps3SourceObjectStreamRecord record)
    {
        if (record.PayloadBitCount < 0 || record.PayloadBitCount > record.Payload.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(record), "Payload bit count must fit inside the supplied payload bytes.");
        }

        var sidecar = record.Sidecar ?? new byte[SidecarByteCount];
        if (sidecar.Length != SidecarByteCount)
        {
            throw new ArgumentException($"PS3 Source object-stream sidecar must be exactly {SidecarByteCount} bytes.", nameof(record));
        }

        var payload = AppendFiveBitTerminator(record.Payload, record.PayloadBitCount);
        var output = new byte[HeaderByteCount + payload.Length];
        output[0] = record.MessageKind;
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(1, sizeof(uint)), record.OwnerOrCallbackId);
        sidecar.CopyTo(output.AsSpan(1 + sizeof(uint), SidecarByteCount));
        var offset = 1 + sizeof(uint) + SidecarByteCount;
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset, sizeof(uint)), record.Sequence);
        offset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset, sizeof(uint)), record.Sequence);
        offset += sizeof(uint);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset, sizeof(uint)), checked((uint)payload.Length));
        offset += sizeof(uint);
        payload.CopyTo(output.AsSpan(offset));
        return output;
    }

    public static bool TryDecode(ReadOnlySpan<byte> encoded, out Ps3SourceDecodedObjectStreamRecord record)
    {
        record = default!;
        if (encoded.Length < HeaderByteCount)
        {
            return false;
        }

        var messageKind = encoded[0];
        var ownerOrCallbackId = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(1, sizeof(uint)));
        var sidecar = encoded.Slice(1 + sizeof(uint), SidecarByteCount).ToArray();
        var offset = 1 + sizeof(uint) + SidecarByteCount;
        var sequenceA = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        var sequenceB = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(offset, sizeof(uint)));
        offset += sizeof(uint);
        if (payloadLength > encoded.Length - offset)
        {
            return false;
        }

        record = new Ps3SourceDecodedObjectStreamRecord(
            messageKind,
            ownerOrCallbackId,
            sequenceA,
            sequenceB,
            sidecar,
            encoded.Slice(offset, checked((int)payloadLength)).ToArray());
        return true;
    }

    public static byte[] AppendFiveBitTerminator(ReadOnlySpan<byte> payload, int payloadBitCount)
    {
        if (payloadBitCount < 0 || payloadBitCount > payload.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadBitCount));
        }

        if (payloadBitCount == 0)
        {
            return [];
        }

        var outputBitCount = payloadBitCount + TerminatorBitCount;
        var output = new byte[(outputBitCount + 7) >> 3];
        var inputBytesToCopy = (payloadBitCount + 7) >> 3;
        payload[..inputBytesToCopy].CopyTo(output);
        var usedBitsInLastByte = payloadBitCount & 7;
        if (usedBitsInLastByte != 0)
        {
            output[inputBytesToCopy - 1] &= (byte)((1 << usedBitsInLastByte) - 1);
        }

        return output;
    }
}
