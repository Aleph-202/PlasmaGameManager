using System.Buffers.Binary;

namespace PlasmaGameManager.Protocol;

public static class Ps3SourceLzss
{
    public const uint Magic = 0x4c5a5353;
    public const int HeaderBytes = 8;
    public const int MinimumNativeInputBytes = 0x10;
    public const int MinimumMatchBytes = 3;
    public const int MaximumMatchBytes = 0x10;
    public const int MaximumBackReferenceDistance = 0xfff;

    public static bool IsWrapped(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= HeaderBytes
            && BinaryPrimitives.ReadUInt32BigEndian(payload) == Magic;
    }

    public static bool TryGetUncompressedLength(ReadOnlySpan<byte> payload, out int length)
    {
        length = 0;
        if (!IsWrapped(payload))
        {
            return false;
        }

        length = BinaryPrimitives.ReadInt32LittleEndian(payload[4..8]);
        return length >= 0;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out byte[] decoded)
    {
        decoded = [];
        if (!TryGetUncompressedLength(payload, out var expectedLength))
        {
            return false;
        }

        var output = new byte[expectedLength];
        var inputOffset = HeaderBytes;
        var outputOffset = 0;
        var control = 0;
        var controlBitIndex = 0;

        while (inputOffset < payload.Length)
        {
            if (controlBitIndex == 0)
            {
                control = payload[inputOffset++];
            }

            if ((control & 1) == 0)
            {
                if (inputOffset >= payload.Length || outputOffset >= output.Length)
                {
                    return false;
                }

                output[outputOffset++] = payload[inputOffset++];
            }
            else
            {
                if (inputOffset + 1 >= payload.Length)
                {
                    return false;
                }

                var first = payload[inputOffset++];
                var second = payload[inputOffset++];
                var encodedLength = second & 0x0f;
                if (encodedLength == 0)
                {
                    if (outputOffset != expectedLength)
                    {
                        return false;
                    }

                    decoded = output;
                    return true;
                }

                var copyLength = encodedLength + 1;
                var copyDistance = (first << 4) | (second >> 4);
                var sourceOffset = outputOffset - copyDistance - 1;
                if (sourceOffset < 0 || outputOffset + copyLength > output.Length)
                {
                    return false;
                }

                for (var i = 0; i < copyLength; i++)
                {
                    output[outputOffset++] = output[sourceOffset + i];
                }
            }

            control >>= 1;
            controlBitIndex = (controlBitIndex + 1) & 7;
        }

        return false;
    }

    public static byte[] EncodeLiteralStream(ReadOnlySpan<byte> payload)
    {
        var output = new List<byte>(HeaderBytes + payload.Length + (payload.Length / 8) + 4);
        WriteHeader(output, payload.Length);

        var controlIndex = -1;
        var controlBitIndex = 0;
        foreach (var value in payload)
        {
            EnsureControlByte(output, ref controlIndex, ref controlBitIndex);
            output.Add(value);
            AdvanceControlBit(ref controlBitIndex);
        }

        EnsureControlByte(output, ref controlIndex, ref controlBitIndex);
        output[controlIndex] |= (byte)(1 << controlBitIndex);
        output.Add(0);
        output.Add(0);
        return output.ToArray();
    }

    public static bool TryEncode(ReadOnlySpan<byte> payload, out byte[] encoded)
    {
        encoded = [];
        if (payload.Length <= MinimumNativeInputBytes)
        {
            return false;
        }

        var candidate = EncodeGreedy(payload);
        if (candidate.Length >= payload.Length - MinimumNativeInputBytes)
        {
            return false;
        }

        encoded = candidate;
        return true;
    }

    public static byte[] EncodeGreedy(ReadOnlySpan<byte> payload)
    {
        var output = new List<byte>(HeaderBytes + payload.Length + (payload.Length / 8) + 4);
        WriteHeader(output, payload.Length);

        var positionsByFirstByte = new List<int>[256];
        for (var i = 0; i < positionsByFirstByte.Length; i++)
        {
            positionsByFirstByte[i] = [];
        }

        var controlIndex = -1;
        var controlBitIndex = 0;
        var offset = 0;
        while (offset < payload.Length)
        {
            var match = FindBestMatch(payload, positionsByFirstByte[payload[offset]], offset);
            EnsureControlByte(output, ref controlIndex, ref controlBitIndex);
            if (match.Length >= MinimumMatchBytes)
            {
                output[controlIndex] |= (byte)(1 << controlBitIndex);
                output.Add((byte)(match.Distance >> 4));
                output.Add((byte)(((match.Distance & 0x0f) << 4) | (match.Length - 1)));

                for (var i = 0; i < match.Length; i++)
                {
                    AddPosition(positionsByFirstByte, payload, offset + i);
                }

                offset += match.Length;
            }
            else
            {
                output.Add(payload[offset]);
                AddPosition(positionsByFirstByte, payload, offset);
                offset++;
            }

            AdvanceControlBit(ref controlBitIndex);
        }

        EnsureControlByte(output, ref controlIndex, ref controlBitIndex);
        output[controlIndex] |= (byte)(1 << controlBitIndex);
        output.Add(0);
        output.Add(0);
        return output.ToArray();
    }

    private static Ps3SourceLzssMatch FindBestMatch(ReadOnlySpan<byte> payload, List<int> candidates, int offset)
    {
        var best = new Ps3SourceLzssMatch(0, 0);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var candidateOffset = candidates[i];
            var distance = offset - candidateOffset - 1;
            if (distance > MaximumBackReferenceDistance)
            {
                break;
            }

            var length = 0;
            var maxLength = Math.Min(MaximumMatchBytes, payload.Length - offset);
            while (length < maxLength && payload[candidateOffset + length] == payload[offset + length])
            {
                length++;
            }

            if (length > best.Length)
            {
                best = new Ps3SourceLzssMatch(distance, length);
                if (length == MaximumMatchBytes)
                {
                    break;
                }
            }
        }

        return best;
    }

    private static void AddPosition(List<int>[] positionsByFirstByte, ReadOnlySpan<byte> payload, int offset)
    {
        var positions = positionsByFirstByte[payload[offset]];
        positions.Add(offset);
        while (positions.Count > 0 && offset - positions[0] - 1 > MaximumBackReferenceDistance)
        {
            positions.RemoveAt(0);
        }
    }

    private static void WriteHeader(List<byte> output, int uncompressedLength)
    {
        output.Add((byte)((Magic >> 24) & 0xff));
        output.Add((byte)((Magic >> 16) & 0xff));
        output.Add((byte)((Magic >> 8) & 0xff));
        output.Add((byte)(Magic & 0xff));
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, uncompressedLength);
        output.AddRange(lengthBytes.ToArray());
    }

    private static void EnsureControlByte(List<byte> output, ref int controlIndex, ref int controlBitIndex)
    {
        if (controlIndex >= 0 && controlBitIndex != 0)
        {
            return;
        }

        controlIndex = output.Count;
        output.Add(0);
        controlBitIndex = 0;
    }

    private static void AdvanceControlBit(ref int controlBitIndex)
    {
        controlBitIndex = (controlBitIndex + 1) & 7;
    }

    private readonly record struct Ps3SourceLzssMatch(int Distance, int Length);
}
