namespace PlasmaGameManager.Protocol;

public static class Ps3SourceFragmentedSend
{
    public const int MaxFragmentCount = 64;

    public static Ps3SourceFragmentedSendChunk[] BuildFragments(
        ReadOnlySpan<byte> payload,
        int maxFragmentBodyBytes,
        uint packetCounterBase,
        bool wrappedOrCompressed)
    {
        if (maxFragmentBodyBytes <= Ps3SourceFragmentHeader.HeaderBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFragmentBodyBytes),
                maxFragmentBodyBytes,
                "Fragment body budget must fit the native fragment header plus at least one payload byte.");
        }

        if (payload.Length == 0)
        {
            return [];
        }

        var chunkBytes = maxFragmentBodyBytes - Ps3SourceFragmentHeader.HeaderBytes;
        var totalCount = checked((payload.Length + chunkBytes - 1) / chunkBytes);
        if (totalCount > MaxFragmentCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                payload.Length,
                $"Native fragment send supports at most {MaxFragmentCount} fragments for one payload.");
        }

        var chunks = new Ps3SourceFragmentedSendChunk[totalCount];
        var offset = 0;
        for (var index = 0; index < totalCount; index++)
        {
            var bodyPayload = payload.Slice(offset, Math.Min(chunkBytes, payload.Length - offset));
            var header = Ps3SourceFragmentHeader.Encode(new Ps3SourceFragmentHeader(
                TotalCount: checked((byte)totalCount),
                FragmentIndex: checked((byte)index),
                PacketCounter: packetCounterBase + checked((uint)index),
                WrappedOrCompressed: wrappedOrCompressed));
            var body = new byte[header.Length + bodyPayload.Length];
            header.CopyTo(body.AsSpan());
            bodyPayload.CopyTo(body.AsSpan(header.Length));
            chunks[index] = new Ps3SourceFragmentedSendChunk(
                checked((byte)index),
                checked((byte)totalCount),
                packetCounterBase + checked((uint)index),
                body);
            offset += bodyPayload.Length;
        }

        return chunks;
    }
}

public sealed record Ps3SourceFragmentedSendChunk(
    byte FragmentIndex,
    byte TotalCount,
    uint PacketCounter,
    byte[] Body);
