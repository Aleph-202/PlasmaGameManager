namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceCompactControlFrame(
    string Family,
    int BodyLength,
    int PrefixLength,
    string PrefixHex,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    ushort? LastUInt16BigEndian,
    ushort? LastUInt16LittleEndian,
    uint? LastUInt32BigEndian,
    uint? LastUInt32LittleEndian)
{
    public static Ps3SourceCompactControlFrame? TryAnalyze(
        ReadOnlySpan<byte> body,
        IReadOnlyList<Ps3SourceEmbeddedObjectRecord> embeddedRecords)
    {
        if (body.Length == 0 || body.Length >= 32)
        {
            return null;
        }

        var prefixLength = embeddedRecords.Count == 0
            ? body.Length
            : Math.Clamp(embeddedRecords.Min(static record => record.Offset), 0, body.Length);
        var prefix = body[..prefixLength];
        return new Ps3SourceCompactControlFrame(
            FamilyFor(body.Length, prefixLength, embeddedRecords),
            body.Length,
            prefixLength,
            Convert.ToHexString(prefix).ToLowerInvariant(),
            ReadUInt16BigEndian(prefix, 0),
            ReadUInt16LittleEndian(prefix, 0),
            ReadUInt32BigEndian(prefix, 0),
            ReadUInt32LittleEndian(prefix, 0),
            ReadUInt16BigEndian(prefix, Math.Max(0, prefix.Length - 2)),
            ReadUInt16LittleEndian(prefix, Math.Max(0, prefix.Length - 2)),
            ReadUInt32BigEndian(prefix, Math.Max(0, prefix.Length - 4)),
            ReadUInt32LittleEndian(prefix, Math.Max(0, prefix.Length - 4)));
    }

    private static string FamilyFor(
        int bodyLength,
        int prefixLength,
        IReadOnlyCollection<Ps3SourceEmbeddedObjectRecord> embeddedRecords)
    {
        if (embeddedRecords.Count > 0)
        {
            var markers = string.Join(
                "+",
                embeddedRecords.Select(static record => record.Marker).Distinct(StringComparer.Ordinal));
            return $"CompactEmbeddedRecordPulse{bodyLength}_Prefix{prefixLength}_{markers}";
        }

        return bodyLength switch
        {
            10 => "CompactAckToken10",
            21 => "CompactAckWindow21",
            28 => "CompactAckWindow28",
            31 => "CompactServerControl31",
            _ => $"CompactControl{bodyLength}"
        };
    }

    private static ushort? ReadUInt16BigEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 2
            ? null
            : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static ushort? ReadUInt16LittleEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 2
            ? null
            : (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static uint? ReadUInt32BigEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 4
            ? null
            : ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
    }

    private static uint? ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 4
            ? null
            : (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
    }
}
