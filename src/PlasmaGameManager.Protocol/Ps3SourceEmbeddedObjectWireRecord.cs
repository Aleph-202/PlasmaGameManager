using System.Buffers.Binary;
using System.Text;

namespace PlasmaGameManager.Protocol;

public readonly record struct Ps3SourceEmbeddedObjectWireRecord(
    string Marker,
    byte Version,
    uint ObjectId,
    uint LinkedObjectId,
    uint ClassId,
    string DisplayName,
    int Length)
{
    public const int PlayerObjectLength = 37;
    public const int FrozenStateObjectLength = 37;
    public const int PlayerDescriptorLength = 16;
    public const uint PlayerClassId = 0x000043fcu;
    public const uint FrozenStateClassId = 0x00004408u;

    public static Ps3SourceEmbeddedObjectWireRecord CocObject(uint objectId, uint linkedObjectId, uint classId, string name)
    {
        return new Ps3SourceEmbeddedObjectWireRecord("COc", 1, objectId, linkedObjectId, classId, name, PlayerObjectLength);
    }

    public static Ps3SourceEmbeddedObjectWireRecord PlayerObject(uint objectId, uint parentId, string name)
    {
        return CocObject(objectId, parentId, PlayerClassId, name);
    }

    public static Ps3SourceEmbeddedObjectWireRecord FrozenStateObject(uint objectId, uint linkedObjectId, string name)
    {
        return CocObject(objectId, linkedObjectId, FrozenStateClassId, name);
    }

    public static Ps3SourceEmbeddedObjectWireRecord PlayerDescriptor(uint objectId, uint linkedObjectId, uint classId = PlayerClassId)
    {
        return new Ps3SourceEmbeddedObjectWireRecord("DSC", 0, objectId, linkedObjectId, classId, "", PlayerDescriptorLength);
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Length)
        {
            throw new ArgumentException($"PS3 Source embedded object record requires {Length} bytes.", nameof(destination));
        }

        var record = destination[..Length];
        record.Clear();
        WriteAscii(record, 0, Marker);
        record[3] = Version;
        BinaryPrimitives.WriteUInt32BigEndian(record[4..8], ObjectId);
        BinaryPrimitives.WriteUInt32BigEndian(record[8..12], LinkedObjectId);
        BinaryPrimitives.WriteUInt32BigEndian(record[12..16], ClassId);
        if (Length > 16 && !string.IsNullOrWhiteSpace(DisplayName))
        {
            WriteAsciiPadded(record[16..], DisplayName);
        }
    }

    public static bool TryDecode(ReadOnlySpan<byte> source, out Ps3SourceEmbeddedObjectWireRecord record)
    {
        record = default;
        if (source.Length < 16)
        {
            return false;
        }

        var marker = Encoding.ASCII.GetString(source[..3]);
        var version = source[3];
        var expectedLength = marker switch
        {
            "COc" when version == 1 => PlayerObjectLength,
            "DSC" when version == 0 => PlayerDescriptorLength,
            _ => 0
        };
        if (expectedLength == 0 || source.Length < expectedLength)
        {
            return false;
        }

        var classId = BinaryPrimitives.ReadUInt32BigEndian(source[12..16]);
        var name = expectedLength > 16
            ? ReadAsciiPadded(source[16..expectedLength])
            : "";
        record = new Ps3SourceEmbeddedObjectWireRecord(
            marker,
            version,
            BinaryPrimitives.ReadUInt32BigEndian(source[4..8]),
            BinaryPrimitives.ReadUInt32BigEndian(source[8..12]),
            classId,
            name,
            expectedLength);
        return true;
    }

    public Ps3SourceEmbeddedObjectRecordRole Role => Marker switch
    {
        "COc" when Length == FrozenStateObjectLength && Version == 1 && ClassId == FrozenStateClassId
            => Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject,
        "COc" when Length == PlayerObjectLength && Version == 1
            => Ps3SourceEmbeddedObjectRecordRole.PlayerObject,
        "DSC" when Length == PlayerDescriptorLength && Version == 0
            => Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor,
        _ => Ps3SourceEmbeddedObjectRecordRole.Unknown
    };

    private static void WriteAscii(Span<byte> destination, int offset, string value)
    {
        Encoding.ASCII.GetBytes(value, destination[offset..]);
    }

    private static void WriteAsciiPadded(Span<byte> destination, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }

    private static string ReadAsciiPadded(ReadOnlySpan<byte> source)
    {
        var end = source.IndexOf((byte)0);
        if (end < 0)
        {
            end = source.Length;
        }

        return Encoding.ASCII.GetString(source[..end]).Trim();
    }
}
