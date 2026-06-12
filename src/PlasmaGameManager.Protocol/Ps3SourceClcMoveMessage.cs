namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourceClcMoveMessage(
    bool IncludesMessageType,
    int MessageType,
    int NewCommands,
    int BackupCommands,
    int CommandDataBitCount,
    byte[] CommandData,
    int HeaderBitsConsumed,
    int TotalBitsConsumed)
{
    public const int ClcMoveMessageType = 9;
    public const int NetMessageTypeBits = 6;
    public const int NewCommandBits = 4;
    public const int BackupCommandBits = 3;
    public const int CommandDataLengthBits = 16;
    public const int MaxNewCommands = (1 << NewCommandBits) - 1;
    public const int MaxBackupCommands = (1 << BackupCommandBits) - 1;

    public int TotalCommands => NewCommands + BackupCommands;

    public static bool TryDecode(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcMoveMessage message)
    {
        message = default!;
        var reader = new Ps3SourceLsbBitReader(payload, payload.Length * 8);
        var messageType = ClcMoveMessageType;
        if (includesMessageType)
        {
            if (!reader.TryReadBits(NetMessageTypeBits, out var type)
                || type != ClcMoveMessageType)
            {
                return false;
            }

            messageType = (int)type;
        }

        if (!reader.TryReadBits(NewCommandBits, out var newCommands)
            || !reader.TryReadBits(BackupCommandBits, out var backupCommands)
            || !reader.TryReadBits(CommandDataLengthBits, out var commandDataBitCount)
            || newCommands > MaxNewCommands
            || backupCommands > MaxBackupCommands
            || commandDataBitCount > payload.Length * 8 - reader.ConsumedBits)
        {
            return false;
        }

        var headerBits = reader.ConsumedBits;
        if (!reader.TryReadBitPayload((int)commandDataBitCount, out var commandData))
        {
            return false;
        }

        message = new Ps3SourceClcMoveMessage(
            includesMessageType,
            messageType,
            (int)newCommands,
            (int)backupCommands,
            (int)commandDataBitCount,
            commandData,
            headerBits,
            reader.ConsumedBits);
        return true;
    }

    public bool TryDecodeUserCmdBatch(
        Ps3SourceUserCmd previousCommand,
        out Ps3SourceClientCommandBatch batch)
    {
        return Ps3SourceClientCommandBatch.TryDecodeBatch(
            CommandData,
            CommandDataBitCount,
            TotalCommands,
            previousCommand,
            out batch);
    }
}

public sealed record Ps3SourceClientCommandBatchBoundary(
    bool IncludesMessageType,
    int MessageType,
    int NewCommands,
    int BackupCommands,
    int TotalCommands,
    int CommandDataBitCount,
    int HeaderBitsConsumed,
    int TotalBitsConsumed,
    byte[] CommandData)
{
    public static Ps3SourceClientCommandBatchBoundary FromClcMove(Ps3SourceClcMoveMessage move)
    {
        return new Ps3SourceClientCommandBatchBoundary(
            move.IncludesMessageType,
            move.MessageType,
            move.NewCommands,
            move.BackupCommands,
            move.TotalCommands,
            move.CommandDataBitCount,
            move.HeaderBitsConsumed,
            move.TotalBitsConsumed,
            move.CommandData);
    }
}

public static class Ps3SourceClientCommandBatchBoundaryResolver
{
    public static bool TryResolveClcMove(
        ReadOnlySpan<byte> payload,
        out Ps3SourceClientCommandBatchBoundary boundary)
    {
        return TryResolveClcMove(payload, includesMessageType: true, out boundary);
    }

    public static bool TryResolveClcMove(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClientCommandBatchBoundary boundary)
    {
        boundary = default!;
        if (!Ps3SourceClcMoveMessage.TryDecode(payload, includesMessageType, out var move)
            || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
            || move.TotalCommands == 0)
        {
            return false;
        }

        boundary = Ps3SourceClientCommandBatchBoundary.FromClcMove(move);
        return true;
    }
}

internal ref struct Ps3SourceLsbBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _bitCount;

    public Ps3SourceLsbBitReader(ReadOnlySpan<byte> data, int bitCount)
    {
        _data = data;
        _bitCount = bitCount;
        ConsumedBits = 0;
    }

    public int ConsumedBits { get; private set; }

    public bool TryReadBits(int bitCount, out uint value)
    {
        value = 0;
        if (bitCount is < 0 or > 32 || ConsumedBits + bitCount > _bitCount)
        {
            return false;
        }

        for (var i = 0; i < bitCount; i++)
        {
            if (((_data[(ConsumedBits + i) >> 3] >> ((ConsumedBits + i) & 7)) & 1) != 0)
            {
                value |= 1u << i;
            }
        }

        ConsumedBits += bitCount;
        return true;
    }

    public bool TryReadBitPayload(int bitCount, out byte[] payload)
    {
        payload = [];
        if (bitCount < 0 || ConsumedBits + bitCount > _bitCount)
        {
            return false;
        }

        payload = new byte[(bitCount + 7) >> 3];
        for (var i = 0; i < bitCount; i++)
        {
            if (((_data[(ConsumedBits + i) >> 3] >> ((ConsumedBits + i) & 7)) & 1) != 0)
            {
                payload[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        ConsumedBits += bitCount;
        return true;
    }
}
