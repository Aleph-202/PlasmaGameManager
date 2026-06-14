using System.Buffers.Binary;
using System.Security.Cryptography;

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
    public const int AckToken10BodyBytes = 10;
    public const int Control17BodyBytes = 17;
    public const int AckWindow21BodyBytes = 21;
    public const int AckWindow28BodyBytes = 28;
    public const int ServerControl31BodyBytes = 31;

    public static byte[] EncodeAckToken10(
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind)
    {
        return EncodeCompactControl(
            AckToken10BodyBytes,
            gameId,
            playerId,
            acknowledgedClientSequence,
            observedClientPacketCount,
            serverSequence,
            streamKind,
            0x10);
    }

    public static byte[] EncodeControl17(
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind)
    {
        return EncodeCompactControl(
            Control17BodyBytes,
            gameId,
            playerId,
            acknowledgedClientSequence,
            observedClientPacketCount,
            serverSequence,
            streamKind,
            0x17);
    }

    public static byte[] EncodeAckWindow21(
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind)
    {
        return EncodeCompactControl(
            AckWindow21BodyBytes,
            gameId,
            playerId,
            acknowledgedClientSequence,
            observedClientPacketCount,
            serverSequence,
            streamKind,
            0x21);
    }

    public static byte[] EncodeAckWindow28(
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind)
    {
        return EncodeCompactControl(
            AckWindow28BodyBytes,
            gameId,
            playerId,
            acknowledgedClientSequence,
            observedClientPacketCount,
            serverSequence,
            streamKind,
            0x28);
    }

    public static byte[] EncodeServerControl31(
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind)
    {
        return EncodeCompactControl(
            ServerControl31BodyBytes,
            gameId,
            playerId,
            acknowledgedClientSequence,
            observedClientPacketCount,
            serverSequence,
            streamKind,
            0x31);
    }

    private static byte[] EncodeCompactControl(
        int bodyBytes,
        uint gameId,
        uint playerId,
        ushort acknowledgedClientSequence,
        int observedClientPacketCount,
        uint serverSequence,
        byte streamKind,
        byte family)
    {
        Span<byte> seed = stackalloc byte[24];
        seed[0] = streamKind;
        BinaryPrimitives.WriteUInt32BigEndian(seed[1..5], gameId);
        BinaryPrimitives.WriteUInt32BigEndian(seed[5..9], playerId);
        BinaryPrimitives.WriteUInt16BigEndian(seed[9..11], acknowledgedClientSequence);
        BinaryPrimitives.WriteUInt32BigEndian(seed[11..15], unchecked((uint)observedClientPacketCount));
        BinaryPrimitives.WriteUInt32BigEndian(seed[15..19], serverSequence);
        seed[19] = (byte)bodyBytes;
        seed[20] = 0x50;
        seed[21] = 0x53;
        seed[22] = 0x33;
        seed[23] = family;

        var digest = SHA256.HashData(seed);
        var body = new byte[bodyBytes];
        digest.AsSpan(0, body.Length).CopyTo(body);
        return body;
    }

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
