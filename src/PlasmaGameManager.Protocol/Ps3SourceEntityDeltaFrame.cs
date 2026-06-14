namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceEntityDeltaGroupHeader(byte GroupIndex);

public sealed record Ps3SourceEntityDeltaNativeRecord(
    byte GroupIndex,
    bool IsPartialRun,
    int? StartIndex,
    byte? EntityCount,
    uint? ObjectId,
    string? ObjectName,
    int? QueuedHandle,
    int BitLength,
    byte[] RawPayload,
    bool UsesNativePartialWindow = false);

public sealed record Ps3SourceEntityDeltaNativeRecordOptions(
    byte GroupIndex,
    bool IsPartialRun,
    int? StartIndex,
    byte? EntityCount,
    uint? ObjectId,
    string? ObjectName,
    int? QueuedHandle,
    int BitLength,
    byte[] RawPayload,
    bool UseNativePartialWindow = false);

public sealed record Ps3SourceQueuedBitstreamDescriptor(
    byte[] Buffer,
    int AllocationByteLength,
    int ByteLength,
    int BitLength,
    bool QueuedHandlePresent,
    int QueuedHandle,
    bool LargePayloadFlag,
    byte ChunkCount);

public enum Ps3SourceEntityDeltaGroupWriteState
{
    Inactive = 0,
    Active = 1,
    Written = 2
}

public sealed record Ps3SourceEntityDeltaGroupState(
    byte GroupIndex,
    Ps3SourceEntityDeltaGroupWriteState WriteState,
    int StartIndex,
    byte EntityCount,
    uint? ObjectId,
    string? ObjectName,
    Ps3SourceQueuedBitstreamDescriptor? Descriptor,
    int LastWrittenFrameIndex)
{
    public Ps3SourceEntityDeltaGroupState MarkActive(Ps3SourceQueuedBitstreamDescriptor descriptor, int startIndex, byte entityCount)
    {
        return this with
        {
            WriteState = Ps3SourceEntityDeltaGroupWriteState.Active,
            Descriptor = descriptor,
            StartIndex = startIndex,
            EntityCount = entityCount
        };
    }

    public Ps3SourceEntityDeltaGroupState MarkWritten(int frameIndex)
    {
        return this with
        {
            WriteState = Ps3SourceEntityDeltaGroupWriteState.Written,
            LastWrittenFrameIndex = frameIndex
        };
    }

    public Ps3SourceEntityDeltaGroupState Clear()
    {
        return this with
        {
            WriteState = Ps3SourceEntityDeltaGroupWriteState.Inactive,
            Descriptor = null,
            EntityCount = 0
        };
    }
}

public sealed record Ps3SourceEntityDeltaGroupEncodeResult(
    byte[] Payload,
    Ps3SourceEntityDeltaGroupState[] NextGroups,
    bool HasWrittenGroups);

public sealed record Ps3SourceEntityDeltaDescriptor(
    byte GroupIndex,
    bool SubchannelPresent,
    int? QueuedHandle,
    int? QueuedBitLength,
    int? StartIndex,
    byte? EntityCount,
    uint? ObjectId);

public static class Ps3SourceEntityDeltaFrameBuilder
{
    private const int GroupIndexBitWidth = 3;
    private const int QueuedHandleBitWidth = 26;
    private const int FullGroupBitLengthWidth = 17;
    private const int PartialRunStartIndexBitWidth = 18;
    private const int PartialRunEntityCountBitWidth = 3;
    private const int PartialRunBitLengthWidth = 26;
    private const int QueuedDescriptorAllocationOverhead = 3;
    private const int QueuedDescriptorMaxAllocationBytes = 96_000;

    public static byte[] EncodeGroupHeader(byte groupIndex)
    {
        if (groupIndex >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(groupIndex), "TF.elf encodes entity delta group indexes in three bits.");
        }

        var writer = new EntityDeltaBitWriter();
        writer.WriteBits(groupIndex, 3);
        return writer.ToArray();
    }

    public static byte[] EncodeDescriptor(Ps3SourceEntityDeltaDescriptor descriptor)
    {
        if (descriptor.GroupIndex >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "TF.elf encodes entity delta group indexes in three bits.");
        }

        var writer = new EntityDeltaBitWriter();
        writer.WriteBits(descriptor.GroupIndex, 3);
        writer.WriteBits(descriptor.SubchannelPresent ? 1u : 0u, 1);

        if (descriptor.QueuedHandle is { } queuedHandle)
        {
            writer.WriteBits(checked((uint)queuedHandle), 26);
        }

        if (descriptor.QueuedBitLength is { } queuedBitLength)
        {
            writer.WriteBits(checked((uint)queuedBitLength), 17);
        }

        if (descriptor.StartIndex is { } startIndex)
        {
            writer.WriteBits(checked((uint)startIndex), 18);
        }

        if (descriptor.EntityCount is { } entityCount)
        {
            writer.WriteBits(entityCount, 3);
        }

        if (descriptor.ObjectId is { } objectId)
        {
            writer.WriteBits(objectId, 32);
        }

        return writer.ToArray();
    }

    public static byte[] EncodeNativeRecord(Ps3SourceEntityDeltaNativeRecordOptions record)
    {
        var writer = new EntityDeltaBitWriter();
        WriteNativeRecord(writer, record);
        return writer.ToArray();
    }

    public static byte[] EncodeNativeRecords(IReadOnlyList<Ps3SourceEntityDeltaNativeRecordOptions> records)
    {
        var writer = new EntityDeltaBitWriter();
        foreach (var record in records)
        {
            WriteNativeRecord(writer, record);
        }

        return writer.ToArray();
    }

    public static byte[] PackEncodedNativeRecords(IReadOnlyList<byte[]> encodedRecords)
    {
        var records = new List<Ps3SourceEntityDeltaNativeRecordOptions>(encodedRecords.Count);
        foreach (var encodedRecord in encodedRecords)
        {
            if (!TryDecodeNativeRecord(encodedRecord, out var record, out var consumedBits) || consumedBits <= 0)
            {
                throw new ArgumentException("Encoded native entity delta record could not be decoded.", nameof(encodedRecords));
            }

            records.Add(new Ps3SourceEntityDeltaNativeRecordOptions(
                record.GroupIndex,
                record.IsPartialRun,
                record.StartIndex,
                record.EntityCount,
                record.ObjectId,
                record.ObjectName,
                record.QueuedHandle,
                record.BitLength,
                record.RawPayload,
                record.UsesNativePartialWindow));
        }

        return EncodeNativeRecords(records);
    }

    public static Ps3SourceQueuedBitstreamDescriptor BuildQueuedBitstreamDescriptor(
        ReadOnlySpan<byte> payload,
        int bitLength,
        int? queuedHandle,
        bool overflowDetectionEnabled = false,
        int inlineByteThreshold = int.MaxValue)
    {
        if (bitLength < 0 || bitLength > payload.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength), "Queued bit length must fit the supplied payload.");
        }

        if (queuedHandle is < 0 or >= (1 << QueuedHandleBitWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(queuedHandle), "Queued handles are 26 bits in TF.elf.");
        }

        var byteLength = (bitLength + 7) >> 3;
        var allocationByteLength = RoundUp4(byteLength + QueuedDescriptorAllocationOverhead);
        if (allocationByteLength >= QueuedDescriptorMaxAllocationBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "TF.elf rejects queued bitstream descriptor allocations at or above 96000 bytes.");
        }

        var buffer = payload[..byteLength].ToArray();
        MaskUnusedTailBits(buffer, bitLength);
        var largePayloadFlag = overflowDetectionEnabled && byteLength > inlineByteThreshold;
        var chunkCount = checked((byte)((byteLength + 0xff) >> 8));
        return new Ps3SourceQueuedBitstreamDescriptor(
            buffer,
            allocationByteLength,
            byteLength,
            bitLength,
            queuedHandle.HasValue,
            queuedHandle ?? 0,
            largePayloadFlag,
            chunkCount);
    }

    public static Ps3SourceEntityDeltaNativeRecordOptions BuildPartialRunRecordFromQueuedDescriptor(
        byte groupIndex,
        int startIndex,
        byte entityCount,
        uint? objectId,
        string? objectName,
        Ps3SourceQueuedBitstreamDescriptor descriptor,
        bool useNativePartialWindow = false)
    {
        return new Ps3SourceEntityDeltaNativeRecordOptions(
            groupIndex,
            IsPartialRun: true,
            startIndex,
            entityCount,
            objectId,
            objectName,
            descriptor.QueuedHandlePresent ? descriptor.QueuedHandle : null,
            descriptor.BitLength,
            descriptor.Buffer,
            useNativePartialWindow);
    }

    public static Ps3SourceEntityDeltaNativeRecordOptions BuildFullGroupRecordFromQueuedDescriptor(
        byte groupIndex,
        Ps3SourceQueuedBitstreamDescriptor descriptor)
    {
        return new Ps3SourceEntityDeltaNativeRecordOptions(
            groupIndex,
            IsPartialRun: false,
            StartIndex: null,
            EntityCount: null,
            ObjectId: null,
            ObjectName: null,
            descriptor.QueuedHandlePresent ? descriptor.QueuedHandle : null,
            descriptor.BitLength,
            descriptor.Buffer);
    }

    public static Ps3SourceEntityDeltaGroupEncodeResult EncodeActiveGroups(
        IReadOnlyList<Ps3SourceEntityDeltaGroupState> groups,
        int frameIndex)
    {
        return EncodeActiveGroups(groups, frameIndex, useNativePartialWindows: false);
    }

    public static Ps3SourceEntityDeltaGroupEncodeResult EncodeActiveGroupsNativePartialWindows(
        IReadOnlyList<Ps3SourceEntityDeltaGroupState> groups,
        int frameIndex)
    {
        return EncodeActiveGroups(groups, frameIndex, useNativePartialWindows: true);
    }

    private static Ps3SourceEntityDeltaGroupEncodeResult EncodeActiveGroups(
        IReadOnlyList<Ps3SourceEntityDeltaGroupState> groups,
        int frameIndex,
        bool useNativePartialWindows)
    {
        var writer = new EntityDeltaBitWriter();
        var nextGroups = new Ps3SourceEntityDeltaGroupState[groups.Count];
        var hasWrittenGroups = false;
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.GroupIndex >= 8)
            {
                throw new ArgumentOutOfRangeException(nameof(groups), "TF.elf scans eight possible entity delta groups.");
            }

            if (group.WriteState == Ps3SourceEntityDeltaGroupWriteState.Active)
            {
                if (group.Descriptor is null)
                {
                    throw new ArgumentException("Active entity delta groups require a queued descriptor.", nameof(groups));
                }

                var record = group.EntityCount > 0
                    ? BuildPartialRunRecordFromQueuedDescriptor(
                        group.GroupIndex,
                        group.StartIndex,
                        group.EntityCount,
                        group.ObjectId,
                        group.ObjectName,
                        group.Descriptor,
                        useNativePartialWindows)
                    : BuildFullGroupRecordFromQueuedDescriptor(group.GroupIndex, group.Descriptor);
                WriteNativeRecord(writer, record);
                nextGroups[i] = group.MarkWritten(frameIndex);
                hasWrittenGroups = true;
            }
            else
            {
                nextGroups[i] = group;
            }
        }

        return new Ps3SourceEntityDeltaGroupEncodeResult(writer.ToArray(), nextGroups, hasWrittenGroups);
    }

    public static bool TryDecodeNativeRecord(ReadOnlySpan<byte> payload, out Ps3SourceEntityDeltaNativeRecord record, out int consumedBits)
    {
        var reader = new EntityDeltaBitReader(payload);
        if (!TryDecodeNativeRecord(reader, out record))
        {
            consumedBits = 0;
            return false;
        }

        consumedBits = reader.ConsumedBits;
        return true;
    }

    public static bool TryDecodeNativeRecords(
        ReadOnlySpan<byte> payload,
        out Ps3SourceEntityDeltaNativeRecord[] records,
        out int consumedBits)
    {
        var reader = new EntityDeltaBitReader(payload);
        var decoded = new List<Ps3SourceEntityDeltaNativeRecord>();
        while (reader.RemainingBits >= MinimumNativeRecordHeaderBits)
        {
            var checkpoint = reader.ConsumedBits;
            if (!TryDecodeNativeRecord(reader, out var record))
            {
                reader.ConsumedBits = checkpoint;
                break;
            }

            decoded.Add(record);
        }

        records = [.. decoded];
        consumedBits = reader.ConsumedBits;
        return records.Length > 0;
    }

    private static bool TryDecodeNativeRecord(EntityDeltaBitReader reader, out Ps3SourceEntityDeltaNativeRecord record)
    {
        record = default!;
        if (!reader.TryReadBits(GroupIndexBitWidth, out var groupIndex)
            || !reader.TryReadBits(1, out var partialRun))
        {
            return false;
        }

        int? startIndex = null;
        byte? entityCount = null;
        uint? objectId = null;
        string? objectName = null;
        int bitLength;
        if (partialRun != 0)
        {
            if (!reader.TryReadBits(PartialRunStartIndexBitWidth, out var start)
                || !reader.TryReadBits(PartialRunEntityCountBitWidth, out var count))
            {
                return false;
            }

            startIndex = checked((int)start);
            entityCount = checked((byte)count);
            var partialTailCheckpoint = reader.ConsumedBits;
            if (TryDecodeNativePartialWindowTail(
                    reader,
                    checked((byte)groupIndex),
                    startIndex.Value,
                    entityCount.Value,
                    out record))
            {
                return true;
            }

            reader.ConsumedBits = partialTailCheckpoint;
            if (!reader.TryReadBits(1, out var hasObject))
            {
                return false;
            }

            if (hasObject != 0)
            {
                if (!reader.TryReadBits(32, out var id) || !reader.TryReadStringZ(128, out objectName))
                {
                    return false;
                }

                objectId = id;
            }
            if (!TryReadOptionalQueuedHandle(reader, out var queuedHandle)
                || !reader.TryReadBits(PartialRunBitLengthWidth, out var rawBitLength))
            {
                return false;
            }

            bitLength = checked((int)rawBitLength);
            if (!reader.TryReadRawBits(bitLength, out var rawPayload))
            {
                return false;
            }

            record = new Ps3SourceEntityDeltaNativeRecord(
                checked((byte)groupIndex),
                IsPartialRun: true,
                startIndex,
                entityCount,
                objectId,
                objectName,
                queuedHandle,
                bitLength,
                rawPayload,
                UsesNativePartialWindow: false);
            return true;
        }

        if (!TryReadOptionalQueuedHandle(reader, out var fullQueuedHandle)
            || !reader.TryReadBits(FullGroupBitLengthWidth, out var fullRawBitLength))
        {
            return false;
        }

        bitLength = checked((int)fullRawBitLength);
        if (!reader.TryReadRawBits(bitLength, out var fullPayload))
        {
            return false;
        }

        record = new Ps3SourceEntityDeltaNativeRecord(
            checked((byte)groupIndex),
            IsPartialRun: false,
            startIndex,
            entityCount,
            objectId,
            objectName,
            fullQueuedHandle,
            bitLength,
            fullPayload,
            UsesNativePartialWindow: false);
        return true;
    }

    private static bool TryDecodeNativePartialWindowTail(
        EntityDeltaBitReader reader,
        byte groupIndex,
        int startIndex,
        byte entityCount,
        out Ps3SourceEntityDeltaNativeRecord record)
    {
        record = default!;
        if (startIndex != 0)
        {
            return false;
        }

        if (!reader.TryReadBits(1, out var partialWindowMode))
        {
            return false;
        }

        uint? objectId = null;
        string? objectName = null;
        int? queuedHandle;
        int bitLength;
        if (partialWindowMode == 0)
        {
            if (!TryReadOptionalQueuedHandle(reader, out queuedHandle)
                || !reader.TryReadBits(FullGroupBitLengthWidth, out var rawBitLength))
            {
                return false;
            }

            bitLength = checked((int)rawBitLength);
        }
        else
        {
            if (startIndex == 0)
            {
                if (!reader.TryReadBits(1, out var hasObject))
                {
                    return false;
                }

                if (hasObject != 0)
                {
                    if (!reader.TryReadBits(32, out var id)
                        || !reader.TryReadStringZ(128, out objectName))
                    {
                        return false;
                    }

                    objectId = id;
                }
            }

            if (!TryReadOptionalQueuedHandle(reader, out queuedHandle)
                || !reader.TryReadBits(PartialRunBitLengthWidth, out var rawBitLength))
            {
                return false;
            }

            bitLength = checked((int)rawBitLength);
        }

        if (!reader.TryReadRawBits(bitLength, out var rawPayload))
        {
            return false;
        }

        record = new Ps3SourceEntityDeltaNativeRecord(
            groupIndex,
            IsPartialRun: true,
            startIndex,
            entityCount,
            objectId,
            objectName,
            queuedHandle,
            bitLength,
            rawPayload,
            UsesNativePartialWindow: true);
        return true;
    }

    public static bool TryDecodeGroupHeader(ReadOnlySpan<byte> payload, out Ps3SourceEntityDeltaGroupHeader header)
    {
        var reader = new EntityDeltaBitReader(payload);
        if (!reader.TryReadBits(3, out var groupIndex))
        {
            header = default!;
            return false;
        }

        header = new Ps3SourceEntityDeltaGroupHeader((byte)groupIndex);
        return true;
    }

    public static bool TryDecodeDescriptor(
        ReadOnlySpan<byte> payload,
        bool hasQueuedHandle,
        bool hasQueuedBitLength,
        bool hasStartIndex,
        bool hasEntityCount,
        bool hasObjectId,
        out Ps3SourceEntityDeltaDescriptor descriptor,
        out int consumedBits)
    {
        descriptor = default!;
        consumedBits = 0;
        var reader = new EntityDeltaBitReader(payload);
        if (!reader.TryReadBits(3, out var groupIndex)
            || !reader.TryReadBits(1, out var present))
        {
            return false;
        }

        int? queuedHandle = null;
        if (hasQueuedHandle)
        {
            if (!reader.TryReadBits(26, out var value))
            {
                return false;
            }

            queuedHandle = checked((int)value);
        }

        int? queuedBitLength = null;
        if (hasQueuedBitLength)
        {
            if (!reader.TryReadBits(17, out var value))
            {
                return false;
            }

            queuedBitLength = checked((int)value);
        }

        int? startIndex = null;
        if (hasStartIndex)
        {
            if (!reader.TryReadBits(18, out var value))
            {
                return false;
            }

            startIndex = checked((int)value);
        }

        byte? entityCount = null;
        if (hasEntityCount)
        {
            if (!reader.TryReadBits(3, out var value))
            {
                return false;
            }

            entityCount = checked((byte)value);
        }

        uint? objectId = null;
        if (hasObjectId)
        {
            if (!reader.TryReadBits(32, out var value))
            {
                return false;
            }

            objectId = value;
        }

        consumedBits = reader.ConsumedBits;
        descriptor = new Ps3SourceEntityDeltaDescriptor(
            checked((byte)groupIndex),
            present != 0,
            queuedHandle,
            queuedBitLength,
            startIndex,
            entityCount,
            objectId);
        return true;
    }

    private static int RoundUp4(int value)
    {
        return (value + 3) & ~3;
    }

    private static void MaskUnusedTailBits(byte[] buffer, int bitLength)
    {
        if (buffer.Length == 0 || (bitLength & 7) == 0)
        {
            return;
        }

        var usedBitsInLastByte = bitLength & 7;
        buffer[^1] &= (byte)((1 << usedBitsInLastByte) - 1);
    }

    private static void ValidateNativeRecord(Ps3SourceEntityDeltaNativeRecordOptions record)
    {
        if (record.GroupIndex >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(record), "TF.elf scans eight entity delta groups and writes the group index in three bits.");
        }

        if (record.BitLength < 0 || record.BitLength > record.RawPayload.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(record), "Bit length must fit the supplied raw payload.");
        }

        if (record.IsPartialRun)
        {
            if (record.StartIndex is < 0 or >= (1 << PartialRunStartIndexBitWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(record), "Partial entity delta start index is 18 bits in TF.elf.");
            }

            if (record.EntityCount is null or > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(record), "Partial entity delta count is 3 bits in TF.elf.");
            }

            if (record.ObjectName is not null && record.ObjectId is null)
            {
                throw new ArgumentException("Object name can only be serialized when ObjectId is present.", nameof(record));
            }

            if (record.UseNativePartialWindow
                && record.ObjectId is not null
                && record.StartIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(record), "TF.elf writes object descriptors only at native partial-window start index zero.");
            }
        }
        else if (record.BitLength >= (1 << FullGroupBitLengthWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(record), "Full-group entity delta bit length is 17 bits in TF.elf.");
        }

        if (record.QueuedHandle is < 0 or >= (1 << QueuedHandleBitWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(record), "Queued handles are 26 bits in TF.elf.");
        }
    }

    private const int MinimumNativeRecordHeaderBits = GroupIndexBitWidth + 1 + 1 + FullGroupBitLengthWidth;

    private static void WriteNativeRecord(EntityDeltaBitWriter writer, Ps3SourceEntityDeltaNativeRecordOptions record)
    {
        ValidateNativeRecord(record);

        writer.WriteBits(record.GroupIndex, GroupIndexBitWidth);
        writer.WriteBits(record.IsPartialRun ? 1u : 0u, 1);
        if (record.IsPartialRun)
        {
            writer.WriteBits(checked((uint)record.StartIndex!.Value), PartialRunStartIndexBitWidth);
            writer.WriteBits(record.EntityCount!.Value, PartialRunEntityCountBitWidth);
            if (record.UseNativePartialWindow)
            {
                WriteNativePartialWindowTail(writer, record);
            }
            else
            {
                if (record.ObjectId is { } objectId)
                {
                    writer.WriteBits(1, 1);
                    writer.WriteBits(objectId, 32);
                    writer.WriteStringZ(record.ObjectName ?? "");
                }
                else
                {
                    writer.WriteBits(0, 1);
                }

                WriteOptionalQueuedHandle(writer, record.QueuedHandle);
                writer.WriteBits(checked((uint)record.BitLength), PartialRunBitLengthWidth);
            }
        }
        else
        {
            WriteOptionalQueuedHandle(writer, record.QueuedHandle);
            writer.WriteBits(checked((uint)record.BitLength), FullGroupBitLengthWidth);
        }

        writer.WriteRawBits(record.RawPayload, record.BitLength);
    }

    private static void WriteNativePartialWindowTail(EntityDeltaBitWriter writer, Ps3SourceEntityDeltaNativeRecordOptions record)
    {
        writer.WriteBits(1, 1);
        if (record.StartIndex == 0)
        {
            if (record.ObjectId is { } objectId)
            {
                writer.WriteBits(1, 1);
                writer.WriteBits(objectId, 32);
                writer.WriteStringZ(record.ObjectName ?? "");
            }
            else
            {
                writer.WriteBits(0, 1);
            }
        }

        WriteOptionalQueuedHandle(writer, record.QueuedHandle);
        writer.WriteBits(checked((uint)record.BitLength), PartialRunBitLengthWidth);
    }

    private static void WriteOptionalQueuedHandle(EntityDeltaBitWriter writer, int? queuedHandle)
    {
        if (queuedHandle is { } handle)
        {
            writer.WriteBits(1, 1);
            writer.WriteBits(checked((uint)handle), QueuedHandleBitWidth);
        }
        else
        {
            writer.WriteBits(0, 1);
        }
    }

    private static bool TryReadOptionalQueuedHandle(EntityDeltaBitReader reader, out int? queuedHandle)
    {
        queuedHandle = null;
        if (!reader.TryReadBits(1, out var hasHandle))
        {
            return false;
        }

        if (hasHandle == 0)
        {
            return true;
        }

        if (!reader.TryReadBits(QueuedHandleBitWidth, out var handle))
        {
            return false;
        }

        queuedHandle = checked((int)handle);
        return true;
    }

    private sealed class EntityDeltaBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitLength;

        public void WriteBits(uint value, int bitCount)
        {
            if (bitCount is < 0 or > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount));
            }

            if (bitCount < 32 && value >= (1u << bitCount))
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} does not fit in {bitCount} bits.");
            }

            for (var i = 0; i < bitCount; i++)
            {
                if ((_bitLength & 7) == 0)
                {
                    _bytes.Add(0);
                }

                if (((value >> i) & 1) != 0)
                {
                    _bytes[^1] |= (byte)(1 << (_bitLength & 7));
                }

                _bitLength++;
            }
        }

        public void WriteStringZ(string value)
        {
            foreach (var ch in System.Text.Encoding.ASCII.GetBytes(value))
            {
                WriteBits(ch, 8);
            }

            WriteBits(0, 8);
        }

        public void WriteRawBits(ReadOnlySpan<byte> values, int bitCount)
        {
            if (bitCount < 0 || bitCount > values.Length * 8)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount));
            }

            for (var i = 0; i < bitCount; i++)
            {
                var bit = (values[i >> 3] >> (i & 7)) & 1;
                WriteBits((uint)bit, 1);
            }
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }
    }

    private sealed class EntityDeltaBitReader(ReadOnlySpan<byte> bytes)
    {
        private readonly byte[] _bytes = bytes.ToArray();
        private int _bitOffset;

        public int ConsumedBits
        {
            get => _bitOffset;
            set
            {
                if (value < 0 || value > _bytes.Length * 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _bitOffset = value;
            }
        }

        public int RemainingBits => _bytes.Length * 8 - _bitOffset;

        public bool TryReadBits(int bitCount, out uint value)
        {
            value = 0;
            if (bitCount is < 0 or > 32 || _bitOffset + bitCount > _bytes.Length * 8)
            {
                return false;
            }

            for (var i = 0; i < bitCount; i++)
            {
                var bit = (_bytes[_bitOffset >> 3] >> (_bitOffset & 7)) & 1;
                value |= (uint)(bit << i);
                _bitOffset++;
            }

            return true;
        }

        public bool TryReadStringZ(int maxBytes, out string value)
        {
            var bytes = new List<byte>();
            for (var i = 0; i < maxBytes; i++)
            {
                if (!TryReadBits(8, out var raw))
                {
                    value = "";
                    return false;
                }

                if (raw == 0)
                {
                    value = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
                    return true;
                }

                bytes.Add(checked((byte)raw));
            }

            value = "";
            return false;
        }

        public bool TryReadRawBits(int bitCount, out byte[] values)
        {
            values = [];
            if (bitCount < 0 || _bitOffset + bitCount > _bytes.Length * 8)
            {
                return false;
            }

            var output = new byte[(bitCount + 7) >> 3];
            for (var i = 0; i < bitCount; i++)
            {
                if (!TryReadBits(1, out var bit))
                {
                    return false;
                }

                if (bit != 0)
                {
                    output[i >> 3] |= (byte)(1 << (i & 7));
                }
            }

            values = output;
            return true;
        }
    }
}
