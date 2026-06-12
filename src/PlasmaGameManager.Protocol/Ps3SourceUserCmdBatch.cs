using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PlasmaGameManager.Protocol;

public readonly record struct Ps3SourceUserCmd(
    int CommandNumber,
    int TickCount,
    uint ViewAngleXRaw,
    uint ViewAngleYRaw,
    uint ViewAngleZRaw,
    float ForwardMove,
    float SideMove,
    float UpMove,
    uint Buttons,
    byte Impulse,
    uint WeaponSelect,
    uint WeaponSubtype,
    uint RandomSeed,
    short MouseDx,
    short MouseDy,
    bool HasBeenPredicted);

public sealed record Ps3SourceClientCommandBatch(
    IReadOnlyList<Ps3SourceUserCmd> Commands,
    int ConsumedBits,
    int MaxCommandsPerBatch,
    string ProcessUsercmdsEntry,
    string DecodeFunctionEntry)
{
    public const int OfficialMaxCommandsPerBatch = 28;
    public const int OfficialCUserCmdStrideBytes = 0x40;
    public const string OfficialProcessUsercmdsEntry = "10125420";
    public const string OfficialDecodeFunctionEntry = "1021c080";

    public Ps3SourceUserCmd? LatestCommand => Commands.Count == 0 ? null : Commands[Commands.Count - 1];

    public static bool TryDecodeBatch(
        ReadOnlySpan<byte> bitstream,
        int bitCount,
        int commandCount,
        Ps3SourceUserCmd previousCommand,
        out Ps3SourceClientCommandBatch batch)
    {
        batch = default!;
        if (bitCount < 0
            || bitCount > bitstream.Length * 8
            || commandCount < 0
            || commandCount > OfficialMaxCommandsPerBatch)
        {
            return false;
        }

        var reader = new Ps3SourceUserCmdBitReader(bitstream, bitCount);
        var commands = new Ps3SourceUserCmd[commandCount];
        var previous = previousCommand;

        // server.dll 10125420 decodes from the last stack CUserCmd slot down to
        // slot zero, passing each decoded command as the next previous/default.
        for (var i = commandCount - 1; i >= 0; i--)
        {
            if (!TryDecodeOne(ref reader, previous, out var decoded))
            {
                return false;
            }

            commands[i] = decoded;
            previous = decoded;
        }

        batch = new Ps3SourceClientCommandBatch(
            commands,
            reader.ConsumedBits,
            OfficialMaxCommandsPerBatch,
            OfficialProcessUsercmdsEntry,
            OfficialDecodeFunctionEntry);
        return true;
    }

    public static bool TryDecodeBatch(
        ReadOnlySpan<byte> bitstream,
        int commandCount,
        Ps3SourceUserCmd previousCommand,
        out Ps3SourceClientCommandBatch batch)
    {
        return TryDecodeBatch(bitstream, bitstream.Length * 8, commandCount, previousCommand, out batch);
    }

    public static bool TryDecodeBitSidecarBatch(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        int commandCount,
        Ps3SourceUserCmd previousCommand,
        out Ps3SourceClientCommandBatch batch)
    {
        batch = default!;
        if (payloadInfo.BitSidecarOffset is not { } offset
            || !Ps3SourceSendWrapper.TryDecodeBitSidecar(body, offset, out var bitCount, out var bitPayload)
            || bitCount == 0)
        {
            return false;
        }

        return TryDecodeBatch(bitPayload, bitCount, commandCount, previousCommand, out batch);
    }

    public static bool TryDecodeSingleBitSidecarCommand(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        Ps3SourceUserCmd previousCommand,
        out Ps3SourceClientCommandBatch batch)
    {
        batch = default!;
        if (!TryDecodeBitSidecarBatch(
                payloadInfo,
                body,
                commandCount: 1,
                previousCommand,
                out var candidate)
            || candidate.ConsumedBits != payloadInfo.BitSidecarBitCount)
        {
            return false;
        }

        batch = candidate;
        return true;
    }

    public static bool TryDecodeBitSidecarClcMoveBatch(
        Ps3SourceClientPayloadInfo payloadInfo,
        ReadOnlySpan<byte> body,
        out Ps3SourceClcMoveMessage move,
        out Ps3SourceClientCommandBatch batch)
    {
        move = default!;
        batch = default!;
        if (payloadInfo.BitSidecarOffset is not { } offset
            || !Ps3SourceSendWrapper.TryDecodeBitSidecar(body, offset, out var bitCount, out var bitPayload)
            || bitCount == 0)
        {
            return false;
        }

        foreach (var includesMessageType in new[] { true, false })
        {
            if (!Ps3SourceClcMoveMessage.TryDecode(bitPayload, includesMessageType, out var candidate)
                || candidate.TotalBitsConsumed != bitCount
                || !candidate.TryDecodeUserCmdBatch(default, out var candidateBatch)
                || candidateBatch.ConsumedBits != candidate.CommandDataBitCount)
            {
                continue;
            }

            move = candidate;
            batch = candidateBatch;
            return true;
        }

        return false;
    }

    private static bool TryDecodeOne(
        ref Ps3SourceUserCmdBitReader reader,
        Ps3SourceUserCmd previous,
        out Ps3SourceUserCmd decoded)
    {
        var commandNumber = previous.CommandNumber;
        var tickCount = previous.TickCount;
        var viewAngleXRaw = previous.ViewAngleXRaw;
        var viewAngleYRaw = previous.ViewAngleYRaw;
        var viewAngleZRaw = previous.ViewAngleZRaw;
        var forwardMove = previous.ForwardMove;
        var sideMove = previous.SideMove;
        var upMove = previous.UpMove;
        var buttons = previous.Buttons;
        var impulse = previous.Impulse;
        var weaponSelect = previous.WeaponSelect;
        var weaponSubtype = previous.WeaponSubtype;
        var mouseDx = previous.MouseDx;
        var mouseDy = previous.MouseDy;

        decoded = default;
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        if (present)
        {
            if (!reader.TryReadBits(32, out var value))
            {
                return false;
            }

            commandNumber = unchecked((int)value);
        }
        else
        {
            commandNumber = unchecked(previous.CommandNumber + 1);
        }

        if (!reader.TryReadBit(out present))
        {
            return false;
        }

        if (present)
        {
            if (!reader.TryReadBits(32, out var value))
            {
                return false;
            }

            tickCount = unchecked((int)value);
        }
        else
        {
            tickCount = unchecked(previous.TickCount + 1);
        }

        if (!ReadOptionalUInt32(ref reader, ref viewAngleXRaw)
            || !ReadOptionalUInt32(ref reader, ref viewAngleYRaw)
            || !ReadOptionalUInt32(ref reader, ref viewAngleZRaw)
            || !ReadOptionalFloatFromSigned16(ref reader, ref forwardMove)
            || !ReadOptionalFloatFromSigned16(ref reader, ref sideMove)
            || !ReadOptionalFloatFromSigned16(ref reader, ref upMove)
            || !ReadOptionalUInt32(ref reader, ref buttons)
            || !ReadOptionalByte(ref reader, ref impulse))
        {
            return false;
        }

        if (!reader.TryReadBit(out present))
        {
            return false;
        }

        if (present)
        {
            if (!reader.TryReadBits(11, out weaponSelect)
                || !reader.TryReadBit(out var subtypePresent))
            {
                return false;
            }

            if (subtypePresent && !reader.TryReadBits(6, out weaponSubtype))
            {
                return false;
            }
        }

        if (!ReadOptionalInt16(ref reader, ref mouseDx)
            || !ReadOptionalInt16(ref reader, ref mouseDy))
        {
            return false;
        }

        decoded = new Ps3SourceUserCmd(
            commandNumber,
            tickCount,
            viewAngleXRaw,
            viewAngleYRaw,
            viewAngleZRaw,
            forwardMove,
            sideMove,
            upMove,
            buttons,
            impulse,
            weaponSelect,
            weaponSubtype,
            ComputeOfficialRandomSeed(commandNumber),
            mouseDx,
            mouseDy,
            previous.HasBeenPredicted);
        return true;
    }

    private static bool ReadOptionalUInt32(ref Ps3SourceUserCmdBitReader reader, ref uint field)
    {
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        return reader.TryReadBits(32, out field);
    }

    private static bool ReadOptionalFloatFromSigned16(ref Ps3SourceUserCmdBitReader reader, ref float field)
    {
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!reader.TryReadBits(16, out var raw))
        {
            return false;
        }

        field = unchecked((short)raw);
        return true;
    }

    private static bool ReadOptionalByte(ref Ps3SourceUserCmdBitReader reader, ref byte field)
    {
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!reader.TryReadBits(8, out var raw))
        {
            return false;
        }

        field = (byte)raw;
        return true;
    }

    private static bool ReadOptionalInt16(ref Ps3SourceUserCmdBitReader reader, ref short field)
    {
        if (!reader.TryReadBit(out var present))
        {
            return false;
        }

        if (!present)
        {
            return true;
        }

        if (!reader.TryReadBits(16, out var raw))
        {
            return false;
        }

        field = unchecked((short)raw);
        return true;
    }

    public static uint ComputeOfficialRandomSeed(int commandNumber)
    {
        Span<byte> seed = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(seed, commandNumber);
        var digest = MD5.HashData(seed);
        return BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(6, sizeof(uint))) & 0x7fffffffu;
    }
}

internal ref struct Ps3SourceUserCmdBitReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private readonly int _bitCount;
    private int _bitOffset;

    public Ps3SourceUserCmdBitReader(ReadOnlySpan<byte> bytes, int bitCount)
    {
        _bytes = bytes;
        _bitCount = bitCount;
        _bitOffset = 0;
    }

    public int ConsumedBits => _bitOffset;

    public bool TryReadBit(out bool bit)
    {
        if (!TryReadBits(1, out var value))
        {
            bit = false;
            return false;
        }

        bit = value != 0;
        return true;
    }

    public bool TryReadBits(int count, out uint value)
    {
        value = 0;
        if (count is < 0 or > 32 || _bitOffset + count > _bitCount)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            var absoluteBit = _bitOffset + i;
            var byteIndex = absoluteBit >> 3;
            var bitInByte = absoluteBit & 7;
            if ((_bytes[byteIndex] & (1 << bitInByte)) != 0)
            {
                value |= 1u << i;
            }
        }

        _bitOffset += count;
        return true;
    }
}
