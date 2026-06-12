using System.Text;

namespace PlasmaGameManager.Protocol;

public static class Ps3SourceEmbeddedObjectRecords
{
    private static readonly string[] PrimaryMarkers = ["COc", "PNG", "DSC"];
    private static readonly HashSet<string> MarkerSet = PrimaryMarkers.ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> IgnoredTextCandidates = new(
        ["COc", "PNG", "DSC"],
        StringComparer.Ordinal);

    public static Ps3SourceEmbeddedObjectRecord[] Extract(ReadOnlySpan<byte> body)
    {
        var markerOffsets = Ps3SourcePayloadSemantics.FindEmbeddedMarkers(body)
            .Where(static marker => MarkerSet.Contains(marker.Marker))
            .OrderBy(static marker => marker.Offset)
            .ThenBy(static marker => marker.Marker, StringComparer.Ordinal)
            .ToArray();
        if (markerOffsets.Length == 0)
        {
            return [];
        }

        var records = new List<Ps3SourceEmbeddedObjectRecord>();
        for (var index = 0; index < markerOffsets.Length; index++)
        {
            var marker = markerOffsets[index];
            var expectedLength = ExpectedRecordLength(marker.Marker);
            var nextOffset = expectedLength is not null
                ? marker.Offset + expectedLength.Value
                : index + 1 < markerOffsets.Length
                    ? markerOffsets[index + 1].Offset
                    : body.Length;
            if (nextOffset <= marker.Offset)
            {
                continue;
            }

            if (nextOffset > body.Length)
            {
                nextOffset = body.Length;
            }

            var bytes = body[marker.Offset..nextOffset];
            records.Add(new Ps3SourceEmbeddedObjectRecord(
                marker.Marker,
                marker.Offset,
                bytes.Length,
                HeaderHex(bytes),
                bytes.Length >= 4 ? bytes[3] : null,
                ReadUInt32BigEndian(bytes, 4),
                ReadUInt32BigEndian(bytes, 8),
                ReadUInt32BigEndian(bytes, 12),
                ExtractPrintableCandidates(bytes, marker.Marker),
                LooksFixedStride(markerOffsets, index),
                Classify(marker.Marker, bytes)));
        }

        return records.ToArray();
    }

    private static int? ExpectedRecordLength(string marker)
    {
        return marker switch
        {
            "PNG" => 12,
            "DSC" => 16,
            "COc" => 37,
            _ => null
        };
    }

    private static string HeaderHex(ReadOnlySpan<byte> bytes)
    {
        var count = Math.Min(bytes.Length, 16);
        return Convert.ToHexString(bytes[..count]).ToLowerInvariant();
    }

    private static uint? ReadUInt32BigEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        if (bytes.Length < offset + 4)
        {
            return null;
        }

        return ((uint)bytes[offset] << 24)
            | ((uint)bytes[offset + 1] << 16)
            | ((uint)bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

    private static string[] ExtractPrintableCandidates(ReadOnlySpan<byte> bytes, string marker)
    {
        var candidates = new List<string>();
        var start = -1;
        for (var i = 0; i <= bytes.Length; i++)
        {
            var isPrintable = i < bytes.Length && bytes[i] is >= 0x20 and <= 0x7e;
            if (isPrintable && start < 0)
            {
                start = i;
            }
            else if ((!isPrintable || i == bytes.Length) && start >= 0)
            {
                var value = Encoding.ASCII.GetString(bytes[start..i]).Trim();
                start = -1;
                if (value.Length >= 3
                    && !IgnoredTextCandidates.Contains(value)
                    && !value.Equals(marker, StringComparison.Ordinal))
                {
                    candidates.Add(value);
                }
            }
        }

        return candidates
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();
    }

    private static bool LooksFixedStride(Ps3SourceEmbeddedMarker[] markers, int index)
    {
        if (index + 2 >= markers.Length)
        {
            return false;
        }

        var current = markers[index];
        var next = markers[index + 1];
        var nextNext = markers[index + 2];
        return current.Marker == next.Marker
            && current.Marker == nextNext.Marker
            && next.Offset - current.Offset == nextNext.Offset - next.Offset;
    }

    private static Ps3SourceEmbeddedObjectRecordRole Classify(string marker, ReadOnlySpan<byte> bytes)
    {
        var version = bytes.Length >= 4 ? bytes[3] : (int?)null;
        var fieldC = ReadUInt32BigEndian(bytes, 12);

        return marker switch
        {
            "COc" when bytes.Length == 37 && version == 1 && fieldC == 0x00004408u
                => Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject,
            "COc" when bytes.Length == 37 && version == 1
                => Ps3SourceEmbeddedObjectRecordRole.PlayerObject,
            "PNG" when bytes.Length == 12 && version == 1
                => Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink,
            "DSC" when bytes.Length == 16 && version == 0
                => Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor,
            "COc" or "PNG" or "DSC"
                => Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise,
            _ => Ps3SourceEmbeddedObjectRecordRole.Unknown
        };
    }
}

public sealed record Ps3SourceEmbeddedObjectRecord(
    string Marker,
    int Offset,
    int Length,
    string HeaderHex,
    int? Version,
    uint? FieldA,
    uint? FieldB,
    uint? FieldC,
    string[] PrintableCandidates,
    bool LooksFixedStride,
    Ps3SourceEmbeddedObjectRecordRole Role)
{
    public uint? ObjectId => FieldA;

    public uint? LinkedObjectId => Role == Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink ? FieldB : null;

    public uint? ClassId => Role is Ps3SourceEmbeddedObjectRecordRole.PlayerObject
        or Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject
        or Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor
            ? FieldC
            : null;

    public string? DisplayName => PrintableCandidates.FirstOrDefault(static candidate =>
        !candidate.Equals("FrozenState_", StringComparison.Ordinal));
}

public enum Ps3SourceEmbeddedObjectRecordRole
{
    Unknown,
    FrozenStateObject,
    PlayerObject,
    PlayerStateLink,
    PlayerDescriptor,
    MarkerCollisionNoise
}
