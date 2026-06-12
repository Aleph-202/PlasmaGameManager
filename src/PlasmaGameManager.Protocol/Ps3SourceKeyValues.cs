using System.Buffers.Binary;
using System.Text;

namespace PlasmaGameManager.Protocol;

public enum Ps3SourceKeyValueType : byte
{
    None = 0,
    String = 1,
    Int = 2,
    Float = 3,
    Ptr = 4,
    WString = 5,
    Color = 6,
    UInt64 = 7,
    NumTypes = 8,
}

public sealed record Ps3SourceKeyValue(
    Ps3SourceKeyValueType Type,
    string Name,
    IReadOnlyList<Ps3SourceKeyValue> Children,
    string StringValue,
    int IntValue,
    float FloatValue,
    int PtrValue,
    byte ColorR,
    byte ColorG,
    byte ColorB,
    byte ColorA,
    ulong UInt64Value)
{
    public static Ps3SourceKeyValue Section(string name, IReadOnlyList<Ps3SourceKeyValue> children)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.None, name, children, "", 0, 0.0f, 0, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue String(string name, string value)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.String, name, [], value, 0, 0.0f, 0, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue Int(string name, int value)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.Int, name, [], "", value, 0.0f, 0, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue Float(string name, float value)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.Float, name, [], "", 0, value, 0, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue Ptr(string name, int value)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.Ptr, name, [], "", 0, 0.0f, value, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue WString(string name)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.WString, name, [], "", 0, 0.0f, 0, 0, 0, 0, 0, 0);
    }

    public static Ps3SourceKeyValue Color(string name, byte r, byte g, byte b, byte a)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.Color, name, [], "", 0, 0.0f, 0, r, g, b, a, 0);
    }

    public static Ps3SourceKeyValue UInt64(string name, ulong value)
    {
        return new Ps3SourceKeyValue(Ps3SourceKeyValueType.UInt64, name, [], "", 0, 0.0f, 0, 0, 0, 0, 0, value);
    }
}

public static class Ps3SourceKeyValues
{
    public const int MaxRecursionDepth = 100;
    public const int MaxTokenBytes = 4096;

    public static byte[] BuildBinary(Ps3SourceKeyValue node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return BuildBinary([node]);
    }

    public static byte[] BuildBinary(IReadOnlyList<Ps3SourceKeyValue> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var bytes = new List<byte>();
        WritePeerList(bytes, nodes, depth: 0);
        return bytes.ToArray();
    }

    public static bool TryDecodeBinary(ReadOnlySpan<byte> data, out IReadOnlyList<Ps3SourceKeyValue> nodes)
    {
        nodes = [];
        var reader = new BinaryKeyValueReader(data);
        if (!TryReadPeerList(ref reader, depth: 0, out var decodedNodes))
        {
            return false;
        }

        if (!reader.IsAtEnd)
        {
            return false;
        }

        nodes = decodedNodes;
        return true;
    }

    private static void WritePeerList(List<byte> bytes, IReadOnlyList<Ps3SourceKeyValue> nodes, int depth)
    {
        if (depth > MaxRecursionDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(nodes), "KeyValues binary recursion depth exceeds Source's 100-level guard.");
        }

        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (node.Type == Ps3SourceKeyValueType.NumTypes)
            {
                throw new ArgumentException("TYPE_NUMTYPES is reserved as the KeyValues peer-list terminator.", nameof(nodes));
            }

            bytes.Add((byte)node.Type);
            WriteCString(bytes, node.Name);

            switch (node.Type)
            {
                case Ps3SourceKeyValueType.None:
                    WritePeerList(bytes, node.Children, depth + 1);
                    break;
                case Ps3SourceKeyValueType.String:
                    WriteCString(bytes, node.StringValue);
                    break;
                case Ps3SourceKeyValueType.Int:
                    WriteInt32LittleEndian(bytes, node.IntValue);
                    break;
                case Ps3SourceKeyValueType.Float:
                    WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(node.FloatValue));
                    break;
                case Ps3SourceKeyValueType.Ptr:
                    WriteInt32LittleEndian(bytes, node.PtrValue);
                    break;
                case Ps3SourceKeyValueType.WString:
                    break;
                case Ps3SourceKeyValueType.Color:
                    bytes.Add(node.ColorR);
                    bytes.Add(node.ColorG);
                    bytes.Add(node.ColorB);
                    bytes.Add(node.ColorA);
                    break;
                case Ps3SourceKeyValueType.UInt64:
                    WriteUInt64LittleEndian(bytes, node.UInt64Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(nodes), $"Unsupported KeyValues binary type {node.Type}.");
            }
        }

        bytes.Add((byte)Ps3SourceKeyValueType.NumTypes);
    }

    private static bool TryReadPeerList(
        ref BinaryKeyValueReader reader,
        int depth,
        out IReadOnlyList<Ps3SourceKeyValue> nodes)
    {
        nodes = [];
        if (depth > MaxRecursionDepth)
        {
            return false;
        }

        var decodedNodes = new List<Ps3SourceKeyValue>();
        while (true)
        {
            if (!reader.TryReadByte(out var rawType)
                || !TryConvertKeyValueType(rawType, out var type))
            {
                return false;
            }

            if (type == Ps3SourceKeyValueType.NumTypes)
            {
                nodes = decodedNodes;
                return true;
            }

            if (!reader.TryReadCString(MaxTokenBytes, out var name))
            {
                return false;
            }

            Ps3SourceKeyValue node;
            switch (type)
            {
                case Ps3SourceKeyValueType.None:
                    if (!TryReadPeerList(ref reader, depth + 1, out var children))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.Section(name, children);
                    break;
                case Ps3SourceKeyValueType.String:
                    if (!reader.TryReadCString(MaxTokenBytes, out var stringValue))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.String(name, stringValue);
                    break;
                case Ps3SourceKeyValueType.Int:
                    if (!reader.TryReadInt32LittleEndian(out var intValue))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.Int(name, intValue);
                    break;
                case Ps3SourceKeyValueType.Float:
                    if (!reader.TryReadInt32LittleEndian(out var floatBits))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.Float(name, BitConverter.Int32BitsToSingle(floatBits));
                    break;
                case Ps3SourceKeyValueType.Ptr:
                    if (!reader.TryReadInt32LittleEndian(out var ptrValue))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.Ptr(name, ptrValue);
                    break;
                case Ps3SourceKeyValueType.WString:
                    node = Ps3SourceKeyValue.WString(name);
                    break;
                case Ps3SourceKeyValueType.Color:
                    if (!reader.TryReadByte(out var r)
                        || !reader.TryReadByte(out var g)
                        || !reader.TryReadByte(out var b)
                        || !reader.TryReadByte(out var a))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.Color(name, r, g, b, a);
                    break;
                case Ps3SourceKeyValueType.UInt64:
                    if (!reader.TryReadUInt64LittleEndian(out var uint64Value))
                    {
                        return false;
                    }

                    node = Ps3SourceKeyValue.UInt64(name, uint64Value);
                    break;
                default:
                    return false;
            }

            decodedNodes.Add(node);
        }
    }

    private static void WriteCString(List<byte> bytes, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var stringBytes = Encoding.ASCII.GetBytes(value);
        if (stringBytes.Length >= MaxTokenBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "KeyValues binary strings must fit in Source's 4096-byte token buffer including the NUL terminator.");
        }

        bytes.AddRange(stringBytes);
        bytes.Add(0);
    }

    private static void WriteInt32LittleEndian(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        for (var i = 0; i < buffer.Length; i++)
        {
            bytes.Add(buffer[i]);
        }
    }

    private static void WriteUInt64LittleEndian(List<byte> bytes, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        for (var i = 0; i < buffer.Length; i++)
        {
            bytes.Add(buffer[i]);
        }
    }

    private static bool TryConvertKeyValueType(byte raw, out Ps3SourceKeyValueType type)
    {
        type = raw switch
        {
            (byte)Ps3SourceKeyValueType.None => Ps3SourceKeyValueType.None,
            (byte)Ps3SourceKeyValueType.String => Ps3SourceKeyValueType.String,
            (byte)Ps3SourceKeyValueType.Int => Ps3SourceKeyValueType.Int,
            (byte)Ps3SourceKeyValueType.Float => Ps3SourceKeyValueType.Float,
            (byte)Ps3SourceKeyValueType.Ptr => Ps3SourceKeyValueType.Ptr,
            (byte)Ps3SourceKeyValueType.WString => Ps3SourceKeyValueType.WString,
            (byte)Ps3SourceKeyValueType.Color => Ps3SourceKeyValueType.Color,
            (byte)Ps3SourceKeyValueType.UInt64 => Ps3SourceKeyValueType.UInt64,
            (byte)Ps3SourceKeyValueType.NumTypes => Ps3SourceKeyValueType.NumTypes,
            _ => default,
        };

        return raw <= (byte)Ps3SourceKeyValueType.NumTypes;
    }

    private ref struct BinaryKeyValueReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        public BinaryKeyValueReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public bool IsAtEnd => _offset == _data.Length;

        public bool TryReadByte(out byte value)
        {
            value = 0;
            if (_offset >= _data.Length)
            {
                return false;
            }

            value = _data[_offset++];
            return true;
        }

        public bool TryReadCString(int maxBytes, out string value)
        {
            value = "";
            if (maxBytes <= 0)
            {
                return false;
            }

            var start = _offset;
            var bytesRead = 0;
            while (_offset < _data.Length && bytesRead < maxBytes)
            {
                if (_data[_offset++] == 0)
                {
                    value = Encoding.ASCII.GetString(_data[start..(_offset - 1)]);
                    return true;
                }

                bytesRead++;
            }

            return false;
        }

        public bool TryReadInt32LittleEndian(out int value)
        {
            value = 0;
            if (_offset + 4 > _data.Length)
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(_data[_offset..(_offset + 4)]);
            _offset += 4;
            return true;
        }

        public bool TryReadUInt64LittleEndian(out ulong value)
        {
            value = 0;
            if (_offset + 8 > _data.Length)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt64LittleEndian(_data[_offset..(_offset + 8)]);
            _offset += 8;
            return true;
        }
    }
}
