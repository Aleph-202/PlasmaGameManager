namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceTransportPacket(
    ushort CandidateSequence,
    byte[] Body,
    int PayloadLength)
{
    public const int HeaderBytes = 2;
    public const int NativeFragmentHeaderBytes = 10;
    public const int InlineQueuePayloadCeiling = 0x800;
    public const int InlineQueueAllocationBytes = 0x808;
    public const int NativeQueuePayloadCeiling = 0x17700;
    public const int NativeQueueAllocationCeiling = 0x17701;
    public const int NativeStagingBufferBytes = 0x1000;
    public const int ConnectedShortControlPayloadBytes = 5;

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Ps3SourceTransportPacket packet)
    {
        if (payload.Length < HeaderBytes)
        {
            packet = new Ps3SourceTransportPacket(0, Array.Empty<byte>(), payload.Length);
            return false;
        }

        var sequence = (ushort)((payload[0] << 8) | payload[1]);
        packet = new Ps3SourceTransportPacket(sequence, payload[HeaderBytes..].ToArray(), payload.Length);
        return true;
    }

    public static byte[] Encode(ushort sequence, ReadOnlySpan<byte> body)
    {
        var payloadLength = HeaderBytes + body.Length;
        if (payloadLength > NativeQueuePayloadCeiling)
        {
            throw new ArgumentOutOfRangeException(
                nameof(body),
                body.Length,
                $"PS3 Source payload length {payloadLength} exceeds native queue ceiling {NativeQueuePayloadCeiling}.");
        }

        var payload = new byte[payloadLength];
        payload[0] = (byte)(sequence >> 8);
        payload[1] = (byte)sequence;
        body.CopyTo(payload.AsSpan(HeaderBytes));
        return payload;
    }

    public static int SequenceDelta(ushort previous, ushort current)
    {
        return (current - previous + 0x10000) & 0xffff;
    }

    public Ps3SourceNativeFrameInfo ClassifyNativeFrame()
    {
        var decodedFragment = Ps3SourceFragmentHeader.TryDecode(Body, out var fragmentHeader);
        var kind = Body.Length switch
        {
            0 => Ps3SourceNativeFrameKind.EmptyBody,
            <= ConnectedShortControlPayloadBytes - HeaderBytes => Ps3SourceNativeFrameKind.ConnectedShortControlCandidate,
            _ when decodedFragment => Ps3SourceNativeFrameKind.FragmentedSendCandidate,
            _ when LooksLikeQueuedPeerChunkCandidate() => Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate,
            _ when LooksLikeBitPayloadSidecarCandidate() => Ps3SourceNativeFrameKind.DirectWithBitPayloadSidecarCandidate,
            _ => Ps3SourceNativeFrameKind.DirectDatagramCandidate
        };

        return new Ps3SourceNativeFrameInfo(
            kind,
            PayloadLength <= InlineQueuePayloadCeiling,
            PayloadLength <= NativeQueuePayloadCeiling,
            PayloadLength >= 1000 || Body.Length >= 998,
            decodedFragment
                ? fragmentHeader.WrappedOrCompressed
                : null,
            Body.Length >= NativeFragmentHeaderBytes
                ? Convert.ToHexString(Body.AsSpan(0, NativeFragmentHeaderBytes)).ToLowerInvariant()
                : "");
    }

    private bool LooksLikeQueuedPeerChunkCandidate()
    {
        // TF.elf 008bc978 normally takes the queued path through 008b9f70
        // unless the immediate-send flag at 0197336c[0x5c] is set. That path
        // copies markerless peer-channel chunks into the native send queue, so
        // near-MTU packets without the explicit 008bc490 fragment sentinel
        // should not be treated as fragmented-send records.
        return Body.Length >= NativeFragmentHeaderBytes
            && (PayloadLength >= 1000 || Body.Length >= 998);
    }

    private bool LooksLikeBitPayloadSidecarCandidate()
    {
        // TF.elf 008bc978 appends a two-byte big-endian bit-length sidecar when
        // the optional bit payload is present. We cannot prove the sidecar
        // boundary without the caller's original param_5 length, so keep this
        // deliberately conservative: only tiny direct packets with a plausible
        // bit count are marked as candidates.
        if (Body.Length is < 4 or > 64)
        {
            return false;
        }

        var bitCount = (Body[^2] << 8) | Body[^1];
        var maxBitsBeforeSidecar = Math.Max(0, Body.Length - 2) * 8;
        return bitCount > 0 && bitCount <= maxBitsBeforeSidecar;
    }
}

public sealed record Ps3SourceFragmentHeader(
    byte TotalCount,
    byte FragmentIndex,
    uint PacketCounter,
    bool WrappedOrCompressed)
{
    public const int HeaderBytes = Ps3SourceTransportPacket.NativeFragmentHeaderBytes;
    private const uint FragmentSentinel = 0xfffffffe;

    public static byte[] Encode(Ps3SourceFragmentHeader header)
    {
        if (header.TotalCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(header), "Fragment total count must be non-zero.");
        }

        if (header.FragmentIndex >= header.TotalCount)
        {
            throw new ArgumentOutOfRangeException(nameof(header), "Fragment index must be lower than the total fragment count.");
        }

        var result = new byte[HeaderBytes];
        result[0] = (byte)((FragmentSentinel >> 24) & 0xff);
        result[1] = (byte)((FragmentSentinel >> 16) & 0xff);
        result[2] = (byte)((FragmentSentinel >> 8) & 0xff);
        result[3] = (byte)(FragmentSentinel & 0xff);
        result[4] = (byte)(header.PacketCounter >> 24);
        result[5] = (byte)(header.PacketCounter >> 16);
        result[6] = (byte)(header.PacketCounter >> 8);
        result[7] = (byte)(header.PacketCounter & 0x7f);
        if (header.WrappedOrCompressed)
        {
            result[7] |= 0x80;
        }

        result[8] = header.FragmentIndex;
        result[9] = header.TotalCount;
        return result;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Ps3SourceFragmentHeader header)
    {
        header = default!;
        if (payload.Length <= HeaderBytes)
        {
            return false;
        }

        var sentinel =
            ((uint)payload[0] << 24)
            | ((uint)payload[1] << 16)
            | ((uint)payload[2] << 8)
            | payload[3];
        if (sentinel != FragmentSentinel)
        {
            return false;
        }

        var totalCount = payload[9];
        var fragmentIndex = payload[8];
        if (totalCount == 0 || totalCount > 64 || fragmentIndex >= totalCount)
        {
            return false;
        }

        var packetCounter =
            ((uint)payload[4] << 24)
            | ((uint)payload[5] << 16)
            | ((uint)payload[6] << 8)
            | (uint)(payload[7] & 0x7f);
        header = new Ps3SourceFragmentHeader(
            totalCount,
            fragmentIndex,
            packetCounter,
            (payload[7] & 0x80) != 0);
        return true;
    }
}

public enum Ps3SourceNativeFrameKind
{
    EmptyBody,
    ConnectedShortControlCandidate,
    DirectDatagramCandidate,
    DirectWithBitPayloadSidecarCandidate,
    QueuedPeerChannelChunkCandidate,
    FragmentedSendCandidate
}

public sealed record Ps3SourceNativeFrameInfo(
    Ps3SourceNativeFrameKind Kind,
    bool FitsInlineQueue,
    bool FitsNativeQueue,
    bool NearMtu,
    bool? FragmentWrappedOrCompressedFlag,
    string FragmentHeaderHex);
