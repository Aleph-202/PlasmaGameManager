namespace PlasmaGameManager.Protocol;

public sealed class Ps3SourceGameplaySession
{
    private readonly Dictionary<Ps3SourceGameplayDirection, Ps3SourceGameplayDirectionState> _states = new()
    {
        [Ps3SourceGameplayDirection.ClientToServer] = new(Ps3SourceGameplayDirection.ClientToServer),
        [Ps3SourceGameplayDirection.ServerToClient] = new(Ps3SourceGameplayDirection.ServerToClient)
    };

    public Ps3SourceGameplayObservation Observe(Ps3SourceGameplayDirection direction, ReadOnlySpan<byte> payload)
    {
        if (IsClassicSourceConnectionless(payload))
        {
            return new Ps3SourceGameplayObservation(
                direction,
                false,
                null,
                null,
                payload.Length,
                0,
                Ps3SourceGameplayPacketShape.ClassicConnectionless,
                Ps3SourceNativeFrameKind.EmptyBody,
                0,
                false,
                false,
                _states[direction].PacketCount);
        }

        if (!Ps3SourceTransportPacket.TryDecode(payload, out var packet))
        {
            return new Ps3SourceGameplayObservation(
                direction,
                false,
                null,
                null,
                payload.Length,
                0,
                Ps3SourceGameplayPacketShape.Invalid,
                Ps3SourceNativeFrameKind.EmptyBody,
                0,
                false,
                false,
                _states[direction].PacketCount);
        }

        var state = _states[direction];
        var previous = state.LastSequence;
        int? delta = previous is null
            ? null
            : Ps3SourceTransportPacket.SequenceDelta(previous.Value, packet.CandidateSequence);
        var sequenceDecrease = previous is not null && packet.CandidateSequence < previous.Value;
        var sequenceWrap = sequenceDecrease && delta is > 0;
        var shape = ClassifyShape(packet);
        var nativeFrame = packet.ClassifyNativeFrame();

        state.PacketCount++;
        state.LastSequence = packet.CandidateSequence;
        state.LastBodyLength = packet.Body.Length;
        state.ShapeCounts[shape] = state.ShapeCounts.GetValueOrDefault(shape) + 1;
        var semanticBody = TryReassembleFragment(state, packet.Body, out var reassembledBody)
            ? reassembledBody
            : packet.Body;
        TrackEmbeddedObjects(state, semanticBody);
        if (sequenceDecrease)
        {
            state.SequenceDecreaseCount++;
        }

        return new Ps3SourceGameplayObservation(
            direction,
            true,
            packet.CandidateSequence,
            delta,
            packet.PayloadLength,
            packet.Body.Length,
            shape,
            nativeFrame.Kind,
            Math.Round(Entropy(packet.Body), 3),
            sequenceDecrease,
            sequenceWrap,
            state.PacketCount,
            ReassembledFragmentBody: reassembledBody);
    }

    public Ps3SourceGameplayDirectionState GetState(Ps3SourceGameplayDirection direction)
    {
        return _states[direction];
    }

    public Ps3SourceGameplaySummary BuildSummary()
    {
        var client = _states[Ps3SourceGameplayDirection.ClientToServer];
        var server = _states[Ps3SourceGameplayDirection.ServerToClient];
        return new Ps3SourceGameplaySummary(
            client.PacketCount + server.PacketCount,
            client.PacketCount,
            server.PacketCount,
            client.SequenceDecreaseCount,
            server.SequenceDecreaseCount,
            client.LastSequence,
            server.LastSequence,
            client.ShapeCounts
                .Concat(server.ShapeCounts)
                .GroupBy(static pair => pair.Key)
                .ToDictionary(static group => group.Key, static group => group.Sum(static pair => pair.Value)),
            MergeCounts(client.EmbeddedRecordRoleCounts, server.EmbeddedRecordRoleCounts),
            MergeCounts(client.EmbeddedObjectLinkCounts, server.EmbeddedObjectLinkCounts),
            MergeCounts(client.EmbeddedDisplayNameCounts, server.EmbeddedDisplayNameCounts),
            MergeCounts(client.EmbeddedClassIdCounts, server.EmbeddedClassIdCounts));
    }

    public static Ps3SourceGameplayPacketShape ClassifyShape(Ps3SourceTransportPacket packet)
    {
        var body = packet.Body;
        if (body.Length < 32)
        {
            return Ps3SourceGameplayPacketShape.ShortControl;
        }

        if (packet.PayloadLength >= 1000 || body.Length >= 998)
        {
            return Ps3SourceGameplayPacketShape.NearMtuFragment;
        }

        if (Entropy(body) >= 7.0)
        {
            return Ps3SourceGameplayPacketShape.HighEntropyBinary;
        }

        if (body.Length >= 256)
        {
            return Ps3SourceGameplayPacketShape.LargeBinary;
        }

        return Ps3SourceGameplayPacketShape.MediumBinary;
    }

    private static bool IsClassicSourceConnectionless(ReadOnlySpan<byte> payload)
    {
        return payload.Length >= 4
            && payload[0] == 0xff
            && payload[1] == 0xff
            && payload[2] == 0xff
            && payload[3] == 0xff;
    }

    private static void TrackEmbeddedObjects(Ps3SourceGameplayDirectionState state, ReadOnlySpan<byte> body)
    {
        foreach (var record in Ps3SourceEmbeddedObjectRecords.Extract(body))
        {
            if (record.Role == Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
            {
                continue;
            }

            Increment(state.EmbeddedRecordRoleCounts, record.Role.ToString());
            if (record.ObjectId is { } objectId && record.LinkedObjectId is { } linkedObjectId)
            {
                Increment(state.EmbeddedObjectLinkCounts, $"{objectId:x8}->{linkedObjectId:x8}");
            }

            if (record.DisplayName is { Length: > 0 } displayName)
            {
                Increment(state.EmbeddedDisplayNameCounts, displayName);
            }

            if (record.ClassId is { } classId)
            {
                Increment(state.EmbeddedClassIdCounts, $"{classId:x8}");
            }
        }
    }

    private static bool TryReassembleFragment(
        Ps3SourceGameplayDirectionState state,
        ReadOnlySpan<byte> body,
        out byte[]? reassembledBody)
    {
        reassembledBody = null;
        if (!Ps3SourceFragmentHeader.TryDecode(body, out var header))
        {
            state.PendingFragmentBaseCounter = null;
            state.PendingFragmentTotalCount = 0;
            state.PendingFragmentWrappedOrCompressed = false;
            state.PendingFragments.Clear();
            return false;
        }

        var groupBaseCounter = header.PacketCounter - header.FragmentIndex;
        if (state.PendingFragmentBaseCounter != groupBaseCounter
            || state.PendingFragmentTotalCount != header.TotalCount
            || state.PendingFragmentWrappedOrCompressed != header.WrappedOrCompressed)
        {
            state.PendingFragmentBaseCounter = groupBaseCounter;
            state.PendingFragmentTotalCount = header.TotalCount;
            state.PendingFragmentWrappedOrCompressed = header.WrappedOrCompressed;
            state.PendingFragments.Clear();
        }

        state.PendingFragments[header.FragmentIndex] = body[Ps3SourceFragmentHeader.HeaderBytes..].ToArray();
        if (state.PendingFragments.Count != header.TotalCount)
        {
            return false;
        }

        var payloadLength = 0;
        for (var index = 0; index < header.TotalCount; index++)
        {
            if (!state.PendingFragments.TryGetValue(checked((byte)index), out var fragment))
            {
                return false;
            }

            payloadLength += fragment.Length;
        }

        var combined = new byte[payloadLength];
        var offset = 0;
        for (var index = 0; index < header.TotalCount; index++)
        {
            var fragment = state.PendingFragments[checked((byte)index)];
            fragment.CopyTo(combined.AsSpan(offset));
            offset += fragment.Length;
        }

        state.PendingFragmentBaseCounter = null;
        state.PendingFragmentTotalCount = 0;
        state.PendingFragmentWrappedOrCompressed = false;
        state.PendingFragments.Clear();

        reassembledBody = header.WrappedOrCompressed && Ps3SourceLzss.TryDecode(combined, out var decoded)
            ? decoded
            : combined;
        return true;
    }

    private static void Increment(Dictionary<string, int> counts, string value)
    {
        counts[value] = counts.GetValueOrDefault(value) + 1;
    }

    private static IReadOnlyDictionary<string, int> MergeCounts(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
    {
        return left
            .Concat(right)
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Sum(static pair => pair.Value), StringComparer.Ordinal);
    }

    private static double Entropy(byte[] body)
    {
        if (body.Length == 0)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in body)
        {
            counts[b]++;
        }

        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = count / (double)body.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}

public enum Ps3SourceGameplayDirection
{
    ClientToServer,
    ServerToClient
}

public enum Ps3SourceGameplayPacketShape
{
    Invalid,
    ClassicConnectionless,
    ShortControl,
    MediumBinary,
    LargeBinary,
    NearMtuFragment,
    HighEntropyBinary
}

public sealed record Ps3SourceGameplayObservation(
    Ps3SourceGameplayDirection Direction,
    bool IsTransportPacket,
    int? Sequence,
    int? SequenceDeltaFromPreviousSameDirection,
    int PayloadLength,
    int BodyLength,
    Ps3SourceGameplayPacketShape Shape,
    Ps3SourceNativeFrameKind NativeFrameKind,
    double BodyEntropy,
    bool SequenceDecrease,
    bool SequenceWrap,
    int DirectionPacketCount,
    byte[]? ReassembledFragmentBody = null);

public sealed class Ps3SourceGameplayDirectionState
{
    public Ps3SourceGameplayDirectionState(Ps3SourceGameplayDirection direction)
    {
        Direction = direction;
    }

    public Ps3SourceGameplayDirection Direction { get; }

    public int PacketCount { get; set; }

    public ushort? LastSequence { get; set; }

    public int LastBodyLength { get; set; }

    public int SequenceDecreaseCount { get; set; }

    public Dictionary<Ps3SourceGameplayPacketShape, int> ShapeCounts { get; } = new();

    public Dictionary<string, int> EmbeddedRecordRoleCounts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> EmbeddedObjectLinkCounts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> EmbeddedDisplayNameCounts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> EmbeddedClassIdCounts { get; } = new(StringComparer.Ordinal);

    public uint? PendingFragmentBaseCounter { get; set; }

    public byte PendingFragmentTotalCount { get; set; }

    public bool PendingFragmentWrappedOrCompressed { get; set; }

    public Dictionary<byte, byte[]> PendingFragments { get; } = new();
}

public sealed record Ps3SourceGameplaySummary(
    int PacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int ClientSequenceDecreaseCount,
    int ServerSequenceDecreaseCount,
    int? LastClientSequence,
    int? LastServerSequence,
    IReadOnlyDictionary<Ps3SourceGameplayPacketShape, int> ShapeCounts,
    IReadOnlyDictionary<string, int> EmbeddedRecordRoleCounts,
    IReadOnlyDictionary<string, int> EmbeddedObjectLinkCounts,
    IReadOnlyDictionary<string, int> EmbeddedDisplayNameCounts,
    IReadOnlyDictionary<string, int> EmbeddedClassIdCounts);
