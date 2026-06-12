namespace PlasmaGameManager.Protocol;

public sealed class Ps3SourceGameplayReplayDriver
{
    private readonly Ps3SourceGameplayReplayStep[] _steps;
    private readonly Ps3SourceGameplayReplayMatchMode _matchMode;
    private readonly int _clientSearchWindow;
    private bool _coalesceLargeDynamicFollowerGroups;
    private string _coalesceLargeDynamicFollowerReason = "";
    private int _cursor;

    public Ps3SourceGameplayReplayDriver(
        IEnumerable<Ps3SourceGameplayReplayStep> steps,
        Ps3SourceGameplayReplayMatchMode matchMode = Ps3SourceGameplayReplayMatchMode.ExactPayload,
        int clientSearchWindow = 0)
    {
        _steps = steps.ToArray();
        _matchMode = matchMode;
        _clientSearchWindow = Math.Max(0, clientSearchWindow);
    }

    public int Cursor => _cursor;

    public bool IsComplete => _cursor >= _steps.Length;

    public Ps3SourceGameplayReplayResult HandleClientPacket(ReadOnlySpan<byte> payload)
    {
        SkipLeadingServerPackets();
        if (_cursor >= _steps.Length)
        {
            return new Ps3SourceGameplayReplayResult(
            false,
            true,
            _cursor,
            null,
            null,
            [],
            Ps3SourceGameplayReplayMatchKind.None,
            "Replay script is complete; no client packet was expected.");
        }

        var search = FindMatchingClientStep(payload);
        if (!search.Found)
        {
            return new Ps3SourceGameplayReplayResult(
                false,
                false,
                _cursor,
                null,
                null,
                [],
                Ps3SourceGameplayReplayMatchKind.None,
                search.Explanation);
        }

        if (search.StepIndex > _cursor)
        {
            _cursor = search.StepIndex;
        }

        var expected = _steps[_cursor];
        if (expected.Direction != Ps3SourceGameplayDirection.ClientToServer)
        {
            return new Ps3SourceGameplayReplayResult(
                false,
                false,
                _cursor,
                null,
                null,
                [],
                Ps3SourceGameplayReplayMatchKind.None,
                $"Expected {expected.Direction} at replay step {_cursor}, not a client packet.");
        }

        var match = search.Match;

        _cursor++;
        var responses = DrainContiguousServerPackets().ToList();
        var coalescedGroups = 0;
        var coalesceDrainExplanation = "";
        if (_coalesceLargeDynamicFollowerGroups && IsShortControlPayload(payload))
        {
            coalescedGroups = DrainCoalescedLargeDynamicFollowerGroups(responses, out coalesceDrainExplanation);
            _coalesceLargeDynamicFollowerGroups = false;
            _coalesceLargeDynamicFollowerReason = "";
        }

        var armedCoalescing = false;
        if (match.CoalesceFollowingClientGroups)
        {
            _coalesceLargeDynamicFollowerGroups = true;
            _coalesceLargeDynamicFollowerReason = search.Explanation;
            armedCoalescing = true;
        }

        var explanation = responses.Count == 0
            ? $"{search.Explanation}; no immediate server packets before the next client packet."
            : coalescedGroups == 0
                ? $"{search.Explanation}; emitted {responses.Count} captured server packet(s)."
                : $"{search.Explanation}; emitted {responses.Count} captured server packet(s), including {coalescedGroups} coalesced follower client group(s): {coalesceDrainExplanation}.";
        if (armedCoalescing)
        {
            explanation += " Armed large dynamic follower coalescing for the next short-control packet.";
        }
        else if (coalesceDrainExplanation.Length > 0 && coalescedGroups == 0)
        {
            explanation += $" Large dynamic follower coalescing was armed by previous match but did not drain: {coalesceDrainExplanation}.";
        }

        return new Ps3SourceGameplayReplayResult(
            true,
            IsComplete,
            _cursor,
            expected.PacketIndex,
            expected.TimestampMicroseconds,
            responses.ToArray(),
            match.MatchKind,
            explanation);
    }

    private ReplayClientSearch FindMatchingClientStep(ReadOnlySpan<byte> payload)
    {
        var checkedClientSteps = 0;
        ReplayPayloadMatch? firstMismatch = null;
        ReplayClientSearch? bestSearch = null;
        var bestScore = ReplayCandidateScore.Zero;
        for (var index = _cursor; index < _steps.Length; index++)
        {
            if (_steps[index].Direction != Ps3SourceGameplayDirection.ClientToServer)
            {
                continue;
            }

            if (checkedClientSteps > _clientSearchWindow)
            {
                break;
            }

            var match = MatchPayload(payload, _steps[index].Payload);
            if (match.Matched)
            {
                var candidate = new ReplayClientSearch(true, index, match, index == _cursor
                    ? $"Matched expected client replay step {_cursor}: {match.Explanation}"
                    : $"Resynchronized from replay step {_cursor} to client step {index}: {match.Explanation}");
                var score = ReplayCandidateScore.FromMatch(match, checkedClientSteps);
                if (score.CompareTo(bestScore) > 0)
                {
                    bestSearch = candidate;
                    bestScore = score;
                }

                if (match.MatchKind == Ps3SourceGameplayReplayMatchKind.ExactPayload)
                {
                    break;
                }
            }

            firstMismatch ??= match;
            checkedClientSteps++;
        }

        if (bestSearch is not null)
        {
            return bestSearch;
        }

        var searchDescription = _clientSearchWindow == 0
            ? $"Client packet did not match replay step {_cursor}"
            : $"Client packet did not match replay step {_cursor} or the next {_clientSearchWindow} client step(s)";
        return new ReplayClientSearch(
            false,
            _cursor,
            firstMismatch ?? new ReplayPayloadMatch(false, Ps3SourceGameplayReplayMatchKind.None, "no client replay step was available"),
            $"{searchDescription}: {(firstMismatch?.Explanation ?? "no client replay step was available")}");
    }

    private ReplayPayloadMatch MatchPayload(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        if (actual.SequenceEqual(expected))
        {
            return new ReplayPayloadMatch(true, Ps3SourceGameplayReplayMatchKind.ExactPayload, "exact payload");
        }

        if (_matchMode == Ps3SourceGameplayReplayMatchMode.ExactPayload)
        {
            return new ReplayPayloadMatch(false, Ps3SourceGameplayReplayMatchKind.None, "exact payload mode");
        }

        if (!Ps3SourceTransportPacket.TryDecode(actual, out var actualTransport)
            || !Ps3SourceTransportPacket.TryDecode(expected, out var expectedTransport))
        {
            return new ReplayPayloadMatch(false, Ps3SourceGameplayReplayMatchKind.None, "one or both payloads are not PS3 Source transport packets");
        }

        var actualShape = Ps3SourceGameplaySession.ClassifyShape(actualTransport);
        var expectedShape = Ps3SourceGameplaySession.ClassifyShape(expectedTransport);
        var shapesCompatible = actualShape == expectedShape
            || (_matchMode == Ps3SourceGameplayReplayMatchMode.LooseTransportShape
                && AreLooseBinaryShapesCompatible(actualShape, expectedShape));
        if (!shapesCompatible)
        {
            return new ReplayPayloadMatch(false, Ps3SourceGameplayReplayMatchKind.None, $"packet shape differs actual={actualShape} expected={expectedShape}");
        }

        if (_matchMode == Ps3SourceGameplayReplayMatchMode.LooseTransportShape)
        {
            if (actualTransport.Body.SequenceEqual(expectedTransport.Body))
            {
                return new ReplayPayloadMatch(
                    true,
                    Ps3SourceGameplayReplayMatchKind.ExactTransportBody,
                    $"transport body exact, sequence actual={actualTransport.CandidateSequence} expected={expectedTransport.CandidateSequence}, shape={actualShape}, len actual={actualTransport.PayloadLength} expected={expectedTransport.PayloadLength}",
                    Math.Abs(actualTransport.PayloadLength - expectedTransport.PayloadLength));
            }

            if (!IsLooseLengthCompatible(actualTransport.PayloadLength, expectedTransport.PayloadLength))
            {
                return new ReplayPayloadMatch(
                    false,
                    Ps3SourceGameplayReplayMatchKind.None,
                    $"loose payload length differs actual={actualTransport.PayloadLength} expected={expectedTransport.PayloadLength}");
            }

            return new ReplayPayloadMatch(
                true,
                Ps3SourceGameplayReplayMatchKind.LooseTransportShape,
                $"loose transport shape {actualShape}, len actual={actualTransport.PayloadLength} expected={expectedTransport.PayloadLength}",
                Math.Abs(actualTransport.PayloadLength - expectedTransport.PayloadLength),
                ShouldCoalesceLargeDynamicFollowerGroups(actualTransport.PayloadLength, expectedTransport.PayloadLength, actualShape, expectedShape));
        }

        if (actualTransport.PayloadLength != expectedTransport.PayloadLength)
        {
            return new ReplayPayloadMatch(
                false,
                Ps3SourceGameplayReplayMatchKind.None,
                $"payload length differs actual={actualTransport.PayloadLength} expected={expectedTransport.PayloadLength}");
        }

        if (actualTransport.Body.Length != expectedTransport.Body.Length)
        {
            return new ReplayPayloadMatch(
                false,
                Ps3SourceGameplayReplayMatchKind.None,
                $"body length differs actual={actualTransport.Body.Length} expected={expectedTransport.Body.Length}");
        }

        if (actualTransport.Body.SequenceEqual(expectedTransport.Body))
        {
            return new ReplayPayloadMatch(
                true,
                Ps3SourceGameplayReplayMatchKind.ExactTransportBody,
                $"transport body exact, sequence actual={actualTransport.CandidateSequence} expected={expectedTransport.CandidateSequence}, shape={actualShape}, len={actualTransport.PayloadLength}",
                0);
        }

        return new ReplayPayloadMatch(true, Ps3SourceGameplayReplayMatchKind.TransportShape, $"transport shape {actualShape}, len={actualTransport.PayloadLength}", 0);
    }

    private static bool IsLooseLengthCompatible(int actualLength, int expectedLength)
    {
        var smaller = Math.Min(actualLength, expectedLength);
        var larger = Math.Max(actualLength, expectedLength);
        if (larger - smaller <= 24)
        {
            return true;
        }

        if (smaller >= 80 && larger >= 200 && larger - smaller <= 160 && larger <= smaller * 3.0)
        {
            return true;
        }

        return larger <= smaller * 2.25 && larger - smaller <= 128;
    }

    private static bool ShouldCoalesceLargeDynamicFollowerGroups(
        int actualLength,
        int expectedLength,
        Ps3SourceGameplayPacketShape actualShape,
        Ps3SourceGameplayPacketShape expectedShape)
    {
        return actualLength >= 200
            && expectedLength is >= 80 and <= 128
            && actualLength - expectedLength > 128
            && IsLooseBinaryShape(actualShape)
            && IsLooseBinaryShape(expectedShape);
    }

    private static bool AreLooseBinaryShapesCompatible(
        Ps3SourceGameplayPacketShape actualShape,
        Ps3SourceGameplayPacketShape expectedShape)
    {
        return IsLooseBinaryShape(actualShape) && IsLooseBinaryShape(expectedShape);
    }

    private static bool IsLooseBinaryShape(Ps3SourceGameplayPacketShape shape)
    {
        return shape is Ps3SourceGameplayPacketShape.MediumBinary
            or Ps3SourceGameplayPacketShape.LargeBinary
            or Ps3SourceGameplayPacketShape.HighEntropyBinary;
    }

    private void SkipLeadingServerPackets()
    {
        while (_cursor < _steps.Length && _steps[_cursor].Direction == Ps3SourceGameplayDirection.ServerToClient)
        {
            _cursor++;
        }
    }

    private int DrainCoalescedLargeDynamicFollowerGroups(
        List<Ps3SourceGameplayReplayStep> responses,
        out string explanation)
    {
        var groups = 0;
        var notes = new List<string>
        {
            $"armed by {_coalesceLargeDynamicFollowerReason}"
        };

        if (TryCoalesceFollowerClientGroup(responses, requireBinaryShape: true, out var binaryExplanation))
        {
            groups++;
            notes.Add(binaryExplanation);
            if (TryCoalesceFollowerClientGroup(responses, requireBinaryShape: false, out var shortExplanation))
            {
                groups++;
                notes.Add(shortExplanation);
            }
            else
            {
                notes.Add(shortExplanation);
            }
        }
        else
        {
            notes.Add(binaryExplanation);
        }

        explanation = string.Join("; ", notes.Where(static note => note.Length > 0));
        return groups;
    }

    private bool TryCoalesceFollowerClientGroup(
        List<Ps3SourceGameplayReplayStep> responses,
        bool requireBinaryShape,
        out string explanation)
    {
        SkipLeadingServerPackets();
        if (_cursor >= _steps.Length || _steps[_cursor].Direction != Ps3SourceGameplayDirection.ClientToServer)
        {
            explanation = _cursor >= _steps.Length
                ? "no replay follower step remains"
                : $"replay follower step {_cursor} is {_steps[_cursor].Direction}, not ClientToServer";
            return false;
        }

        if (!Ps3SourceTransportPacket.TryDecode(_steps[_cursor].Payload, out var transport))
        {
            explanation = $"replay follower step {_cursor} packetIndex={_steps[_cursor].PacketIndex} is not a PS3 Source transport packet";
            return false;
        }

        var shape = Ps3SourceGameplaySession.ClassifyShape(transport);
        if (requireBinaryShape ? !IsLooseBinaryShape(shape) : shape != Ps3SourceGameplayPacketShape.ShortControl)
        {
            var expectedShape = requireBinaryShape ? "binary" : "ShortControl";
            explanation = $"replay follower step {_cursor} packetIndex={_steps[_cursor].PacketIndex} shape={shape} len={transport.PayloadLength}, expected {expectedShape}";
            return false;
        }

        var coalescedStep = _steps[_cursor];
        _cursor++;
        var responseStartCount = responses.Count;
        responses.AddRange(DrainContiguousServerPackets());
        explanation = $"coalesced replay follower step {_cursor - 1} packetIndex={coalescedStep.PacketIndex} shape={shape} len={transport.PayloadLength} and {responses.Count - responseStartCount} server response packet(s)";
        return true;
    }

    private static bool IsShortControlPayload(ReadOnlySpan<byte> payload)
    {
        return Ps3SourceTransportPacket.TryDecode(payload, out var transport)
            && Ps3SourceGameplaySession.ClassifyShape(transport) == Ps3SourceGameplayPacketShape.ShortControl;
    }

    private Ps3SourceGameplayReplayStep[] DrainContiguousServerPackets()
    {
        var start = _cursor;
        while (_cursor < _steps.Length && _steps[_cursor].Direction == Ps3SourceGameplayDirection.ServerToClient)
        {
            _cursor++;
        }

        return _steps[start.._cursor];
    }

    private sealed record ReplayPayloadMatch(
        bool Matched,
        Ps3SourceGameplayReplayMatchKind MatchKind,
        string Explanation,
        int? LengthDistance = null,
        bool CoalesceFollowingClientGroups = false);

    private sealed record ReplayClientSearch(
        bool Found,
        int StepIndex,
        ReplayPayloadMatch Match,
        string Explanation);

    private readonly record struct ReplayCandidateScore(
        int MatchRank,
        int ReplayCostScore) : IComparable<ReplayCandidateScore>
    {
        private const int SearchDistanceCost = 16;

        public static ReplayCandidateScore Zero => new(0, int.MinValue);

        public static ReplayCandidateScore FromMatch(ReplayPayloadMatch match, int searchDistance)
        {
            var lengthDistance = match.LengthDistance ?? int.MaxValue;
            var replayCost = (searchDistance * SearchDistanceCost) + lengthDistance;
            return new ReplayCandidateScore(
                ReplayMatchRank(match.MatchKind),
                -replayCost);
        }

        public int CompareTo(ReplayCandidateScore other)
        {
            var rank = MatchRank.CompareTo(other.MatchRank);
            if (rank != 0)
            {
                return rank;
            }

            return ReplayCostScore.CompareTo(other.ReplayCostScore);
        }

        private static int ReplayMatchRank(Ps3SourceGameplayReplayMatchKind matchKind)
        {
            return matchKind switch
            {
                Ps3SourceGameplayReplayMatchKind.ExactPayload => 4,
                Ps3SourceGameplayReplayMatchKind.ExactTransportBody => 3,
                Ps3SourceGameplayReplayMatchKind.TransportShape => 2,
                Ps3SourceGameplayReplayMatchKind.LooseTransportShape => 1,
                _ => 0
            };
        }
    }
}

public enum Ps3SourceGameplayReplayMatchMode
{
    ExactPayload,
    TransportShape,
    LooseTransportShape
}

public enum Ps3SourceGameplayReplayMatchKind
{
    None,
    LooseTransportShape,
    TransportShape,
    ExactTransportBody,
    ExactPayload
}

public sealed record Ps3SourceGameplayReplayStep(
    Ps3SourceGameplayDirection Direction,
    byte[] Payload,
    long PacketIndex,
    long TimestampMicroseconds);

public sealed record Ps3SourceGameplayReplayResult(
    bool Matched,
    bool IsComplete,
    int Cursor,
    long? MatchedClientPacketIndex,
    long? MatchedClientTimestampMicroseconds,
    Ps3SourceGameplayReplayStep[] ServerResponses,
    Ps3SourceGameplayReplayMatchKind MatchKind,
    string Explanation);
