namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceEmbeddedPrefixFrame(
    string Family,
    int PrefixLength,
    string PrefixHex,
    double PrefixEntropy,
    double PrefixPrintableRatio,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    ushort? LastUInt16BigEndian,
    ushort? LastUInt16LittleEndian,
    uint? LastUInt32BigEndian,
    uint? LastUInt32LittleEndian,
    string RecordRoleSummary,
    string Meaning,
    string UnknownField)
{
    public static Ps3SourceEmbeddedPrefixFrame? TryAnalyze(
        ReadOnlySpan<byte> body,
        IReadOnlyList<Ps3SourceEmbeddedObjectRecord> records,
        string? directionHint = null)
    {
        if (records.Count == 0)
        {
            return null;
        }

        var prefixLength = Math.Clamp(records.Min(static record => record.Offset), 0, body.Length);
        var prefix = body[..prefixLength];
        var family = FamilyFor(prefixLength, records, directionHint);
        return new Ps3SourceEmbeddedPrefixFrame(
            family,
            prefixLength,
            Convert.ToHexString(prefix).ToLowerInvariant(),
            Math.Round(Entropy(prefix), 4),
            Math.Round(PrintableRatio(prefix), 4),
            ReadUInt16BigEndian(prefix, 0),
            ReadUInt16LittleEndian(prefix, 0),
            ReadUInt32BigEndian(prefix, 0),
            ReadUInt32LittleEndian(prefix, 0),
            ReadUInt16BigEndian(prefix, Math.Max(0, prefix.Length - 2)),
            ReadUInt16LittleEndian(prefix, Math.Max(0, prefix.Length - 2)),
            ReadUInt32BigEndian(prefix, Math.Max(0, prefix.Length - 4)),
            ReadUInt32LittleEndian(prefix, Math.Max(0, prefix.Length - 4)),
            RoleSummary(records),
            MeaningFor(family),
            UnknownFieldFor(family));
    }

    private static string FamilyFor(
        int prefixLength,
        IReadOnlyList<Ps3SourceEmbeddedObjectRecord> records,
        string? directionHint)
    {
        var direction = string.IsNullOrWhiteSpace(directionHint)
            ? "Unknown"
            : directionHint;
        var roles = records
            .Select(static record => record.Role)
            .Distinct()
            .OrderBy(static role => role)
            .ToArray();
        var markers = string.Join("+", records
            .Select(static record => record.Marker)
            .Distinct(StringComparer.Ordinal));
        var recordCount = records.Count;

        if (roles.Length == 1)
        {
            return roles[0] switch
            {
                Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject
                    => $"EmbeddedFrozenState{direction}Prefix{prefixLength}_Records{recordCount}",
                Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink
                    => $"EmbeddedPlayerStateLink{direction}Prefix{prefixLength}_Records{recordCount}",
                Ps3SourceEmbeddedObjectRecordRole.PlayerObject
                    => $"EmbeddedPlayerObject{direction}Prefix{prefixLength}_Records{recordCount}",
                Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor
                    => $"EmbeddedPlayerDescriptor{direction}Prefix{prefixLength}_Records{recordCount}",
                _ => $"Embedded{markers}{direction}Prefix{prefixLength}_Records{recordCount}"
            };
        }

        return $"EmbeddedMixed{markers}{direction}Prefix{prefixLength}_Records{recordCount}";
    }

    private static string RoleSummary(IReadOnlyList<Ps3SourceEmbeddedObjectRecord> records)
    {
        return string.Join(
            "+",
            records
                .GroupBy(static record => record.Role)
                .OrderBy(static group => group.Key)
                .Select(static group => $"{group.Key}:{group.Count()}"));
    }

    private static string MeaningFor(string family)
    {
        if (family.StartsWith("EmbeddedFrozenStateClientToServer", StringComparison.Ordinal))
        {
            return "client-side FrozenState object-graph request prefix before COc records";
        }

        if (family.StartsWith("EmbeddedFrozenStateServerToClient", StringComparison.Ordinal))
        {
            return "server-side FrozenState object advertisement prefix before COc records";
        }

        if (family.StartsWith("EmbeddedPlayerStateLinkClientToServer", StringComparison.Ordinal))
        {
            return "client-side player/object state-link acknowledgement prefix before PNG records";
        }

        if (family.StartsWith("EmbeddedPlayerStateLinkServerToClient", StringComparison.Ordinal))
        {
            return "server-side player/object state-link prefix before PNG records";
        }

        if (family.StartsWith("EmbeddedPlayerDescriptor", StringComparison.Ordinal))
        {
            return "player descriptor prefix before DSC records";
        }

        return "embedded-object prefix bytes before native Source object records";
    }

    private static string UnknownFieldFor(string family)
    {
        return $"field-level layout for {family}";
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

    private static double PrintableRatio(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        var printable = 0;
        foreach (var b in data)
        {
            if (b is >= 0x20 and <= 0x7e or 0x09 or 0x0a or 0x0d)
            {
                printable++;
            }
        }

        return printable / (double)data.Length;
    }

    private static double Entropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
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

            var p = count / (double)data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}
