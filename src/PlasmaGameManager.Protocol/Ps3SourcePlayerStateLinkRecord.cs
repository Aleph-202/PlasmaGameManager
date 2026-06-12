using System.Buffers.Binary;
using System.Text;

namespace PlasmaGameManager.Protocol;

public readonly record struct Ps3SourcePlayerStateLinkRecord(uint ObjectId, uint LinkedObjectId)
{
    public const int Length = 12;
    public const byte Version = 1;
    public const string Marker = "PNG";

    private static readonly byte[] MarkerBytes = Encoding.ASCII.GetBytes(Marker);

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Length)
        {
            throw new ArgumentException("PS3 Source player state-link record requires 12 bytes.", nameof(destination));
        }

        destination[..Length].Clear();
        MarkerBytes.CopyTo(destination);
        destination[3] = Version;
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..8], ObjectId);
        BinaryPrimitives.WriteUInt32BigEndian(destination[8..12], LinkedObjectId);
    }

    public static bool TryDecode(ReadOnlySpan<byte> source, out Ps3SourcePlayerStateLinkRecord record)
    {
        record = default;
        if (source.Length < Length
            || source[0] != (byte)'P'
            || source[1] != (byte)'N'
            || source[2] != (byte)'G'
            || source[3] != Version)
        {
            return false;
        }

        record = new Ps3SourcePlayerStateLinkRecord(
            BinaryPrimitives.ReadUInt32BigEndian(source[4..8]),
            BinaryPrimitives.ReadUInt32BigEndian(source[8..12]));
        return true;
    }

    public static byte[] BuildBatch(IEnumerable<Ps3SourcePlayerStateLinkRecord> records)
    {
        var items = records.ToArray();
        var body = new byte[checked(items.Length * Length)];
        var offset = 0;
        foreach (var item in items)
        {
            item.WriteTo(body.AsSpan(offset, Length));
            offset += Length;
        }

        return body;
    }
}
