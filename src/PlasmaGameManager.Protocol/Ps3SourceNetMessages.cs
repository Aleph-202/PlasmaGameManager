using System.Text;

namespace PlasmaGameManager.Protocol;

public static class Ps3SourceNetMessageConstants
{
    public const int NetMessageTypeBits = 5;
    public const int MaxEdictBits = 11;
    public const int DeltaSizeBits = 20;
    public const int MaxStringTables = 32;
    public const int StringTableMaxUserDataBits = 14;
    public const int MaxPlayerNameLength = 32;
    public const int MaxCustomFiles = 4;

    public const int SvcServerInfo = 8;
    public const int SvcSendTable = 9;
    public const int SvcClassInfo = 10;
    public const int SvcCreateStringTable = 12;
    public const int SvcUpdateStringTable = 13;
    public const int SvcSetView = 18;
    public const int SvcPacketEntities = 26;

    public const int NetTick = 3;
    public const int NetStringCmd = 4;
    public const int NetSetConVar = 5;
    public const int NetSignonState = 6;

    public const int ClcClientInfo = 8;
    public const int ClcVoiceData = 10;
    public const int ClcBaselineAck = 11;
    public const int ClcListenEvents = 12;
    public const int ClcRespondCvarValue = 13;
    public const int ClcFileCrcCheck = 14;

    public const int MaxNetStringCommandLength = 0x400;
    public const int MaxSetConVarStringLength = 0x104;
    public const int MaxRespondCvarStringLength = 0x100;
    public const int ListenEventMaskWordCount = 16;
    public const int MaxFileCrcPathLength = 0x104;
    public const int MaxFileCrcFilenameLength = 0x104;

    public const int SvcPrint = 7;
    public const int SvcSetPause = 11;
    public const int SvcVoiceInit = 14;
    public const int SvcVoiceData = 15;
    public const int SvcSounds = 17;
    public const int SvcFixAngle = 19;
    public const int SvcCrosshairAngle = 20;
    public const int SvcBspDecal = 21;
    public const int SvcUserMessage = 23;
    public const int SvcEntityMessage = 24;
    public const int SvcGameEvent = 25;
    public const int SvcTempEntities = 27;
    public const int SvcPrefetch = 28;
    public const int SvcMenu = 29;
    public const int SvcGameEventList = 30;
    public const int SvcGetCvarValue = 31;
    public const int MaxSvcPrintLength = 0x800;
    public const int MaxSvcVoiceCodecLength = 0x104;
    public const int NetMessageLengthBits = 11;
    public const int MaxServerClassBits = 9;
    public const int MaxSvcGameEventBits = (1 << 11) - 1;
    public const int TempEntityEventIndexBits = 8;
    public const int TempEntityPayloadLengthBits = 17;
    public const int MaxSoundIndexBits = 13;
    public const int SoundFlagBitsEncode = 11;
    public const int SoundSequenceNumberBits = 10;
    public const int MaxSoundLevelBits = 9;
    public const int SoundDelayMsecEncodeBits = 13;
    public const int SoundOriginIntegerBits = 12;
    public const int DefaultSoundChannel = 6;
    public const int DefaultSoundLevel = 75;
    public const int DefaultSoundPitch = 100;
    public const int SoundStopFlag = 1 << 2;
    public const float SoundDelayOffset = 0.100f;
    public const int MaxSvcCvarNameLength = 0x100;
    public const int MaxDecalIndexBits = 9;
    public const int Ps3BspDecalModelIndexBits = 11;
    public const int MaxEventBits = 9;
    public const int GameEventFieldTypeBits = 3;
    public const int MaxGameEventNameLength = 32;
    public const int MaxGameEventDescriptorStringLength = 0x100;
    public const int MaxMenuDataBytes = 4096;
}

public sealed record Ps3SourceSvcServerInfo(
    short Protocol,
    int ServerCount,
    bool IsHltv,
    bool IsDedicated,
    uint LegacyClientCrc,
    ushort MaxClasses,
    uint MapCrcOrDigest32,
    byte PlayerSlot,
    byte MaxClients,
    float TickInterval,
    byte OperatingSystem,
    string GameDirectory,
    string MapName,
    string SkyName,
    string HostName);

public sealed record Ps3SourceSvcSendTable(
    bool NeedsDecoder,
    byte[] Data,
    int DataBitCount);

public sealed record Ps3SourceClassInfoEntry(
    int ClassId,
    string ClassName,
    string DataTableName);

public sealed record Ps3SourceSvcClassInfo(
    short NumServerClasses,
    bool CreateOnClient,
    IReadOnlyList<Ps3SourceClassInfoEntry> Classes);

public sealed record Ps3SourceSvcStringTable(
    string TableName,
    ushort MaxEntries,
    int NumEntries,
    bool UserDataFixedSize,
    int UserDataSize,
    int UserDataSizeBits,
    byte[] Data,
    int DataBitCount);

public sealed record Ps3SourceSvcStringTableUpdate(
    int TableId,
    int ChangedEntries,
    byte[] Data,
    int DataBitCount);

public sealed record Ps3SourceStringTableEntry(
    int Index,
    string Value,
    byte[] UserData,
    int UserDataBitCount);

public sealed record Ps3SourceNetSignonState(
    byte SignonState,
    int SpawnCount);

public sealed record Ps3SourceSvcSetView(
    int EntityIndex);

public sealed record Ps3SourceSvcPacketEntities(
    int MaxEntries,
    bool IsDelta,
    int DeltaFrom,
    int Baseline,
    int UpdatedEntries,
    bool UpdateBaseline,
    byte[] Data,
    int DataBitCount);

public sealed record Ps3SourceNetMessageFrame(byte[] Payload, int BitCount);

public sealed record Ps3SourceSvcPrint(string Text);

public sealed record Ps3SourceSvcSetPause(bool Paused);

public sealed record Ps3SourceSvcVoiceInit(string Codec, byte LegacyQuality);

public sealed record Ps3SourceSvcVoiceData(
    byte FromClient,
    bool Proximity,
    int DataBitCount,
    byte[] Data);

public sealed record Ps3SourceSvcSounds(
    bool ReliableSound,
    byte NumSounds,
    int DataBitCount,
    byte[] Data);

public sealed record Ps3SourceSoundInfo(
    int SequenceNumber,
    int EntityIndex,
    int Channel,
    Ps3SourceVector Origin,
    float Volume,
    int SoundLevel,
    int Pitch,
    int SpecialDsp,
    int Flags,
    int SoundNumber,
    float Delay,
    bool IsSentence,
    bool IsAmbient,
    int SpeakerEntity)
{
    public static Ps3SourceSoundInfo Default { get; } = new(
        SequenceNumber: 0,
        EntityIndex: 0,
        Channel: Ps3SourceNetMessageConstants.DefaultSoundChannel,
        Origin: new Ps3SourceVector(0.0f, 0.0f, 0.0f),
        Volume: 1.0f,
        SoundLevel: Ps3SourceNetMessageConstants.DefaultSoundLevel,
        Pitch: Ps3SourceNetMessageConstants.DefaultSoundPitch,
        SpecialDsp: 0,
        Flags: 0,
        SoundNumber: 0,
        Delay: 0.0f,
        IsSentence: false,
        IsAmbient: false,
        SpeakerEntity: -1);

    public Ps3SourceSoundInfo ClearStopFields()
    {
        return this with
        {
            SequenceNumber = 0,
            Origin = new Ps3SourceVector(0.0f, 0.0f, 0.0f),
            Volume = 0.0f,
            SoundLevel = 0,
            Pitch = Ps3SourceNetMessageConstants.DefaultSoundPitch,
            SpecialDsp = 0,
            Delay = 0.0f,
            SpeakerEntity = -1,
        };
    }
}

public sealed record Ps3SourceQAngle(float X, float Y, float Z);

public sealed record Ps3SourceSvcFixAngle(bool Relative, Ps3SourceQAngle Angle);

public sealed record Ps3SourceSvcCrosshairAngle(Ps3SourceQAngle Angle);

public sealed record Ps3SourceVector(float X, float Y, float Z);

public sealed record Ps3SourceSvcBspDecal(
    Ps3SourceVector Position,
    int DecalTextureIndex,
    int EntityIndex,
    int ModelIndex,
    bool LowPriority);

public sealed record Ps3SourceSvcUserMessage(
    byte MessageType,
    int DataBitCount,
    byte[] Data);

public sealed record Ps3SourceSvcEntityMessage(
    int EntityIndex,
    int ClassId,
    int DataBitCount,
    byte[] Data);

public sealed record Ps3SourceSvcGameEvent(int DataBitCount, byte[] Data);

public sealed record Ps3SourceSvcTempEntities(
    byte NumEntries,
    int DataBitCount,
    byte[] Data);

public sealed record Ps3SourceSvcPrefetch(ushort SoundIndex);

public sealed record Ps3SourceSvcMenu(
    short DialogType,
    byte[] BinaryKeyValues);

public sealed record Ps3SourceSvcMenuKeyValues(
    short DialogType,
    IReadOnlyList<Ps3SourceKeyValue> KeyValues);

public sealed record Ps3SourceSvcGameEventList(
    int NumEvents,
    int DataBitCount,
    byte[] Data);

public enum Ps3SourceGameEventFieldType
{
    Local = 0,
    String = 1,
    Float = 2,
    Long = 3,
    Short = 4,
    Byte = 5,
    Bool = 6,
    UInt64 = 7,
}

public sealed record Ps3SourceGameEventDescriptor(
    int EventId,
    string Name,
    IReadOnlyList<Ps3SourceGameEventFieldDescriptor> Fields);

public sealed record Ps3SourceGameEventFieldDescriptor(
    Ps3SourceGameEventFieldType Type,
    string Name);

public sealed record Ps3SourceGameEventInstance(
    int EventId,
    IReadOnlyList<Ps3SourceGameEventFieldValue> Fields);

public sealed record Ps3SourceGameEventFieldValue(
    Ps3SourceGameEventFieldType Type,
    string Name,
    string StringValue,
    float FloatValue,
    int IntValue,
    bool BoolValue)
{
    public static Ps3SourceGameEventFieldValue String(string name, string value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.String, name, value, 0.0f, 0, false);
    }

    public static Ps3SourceGameEventFieldValue Float(string name, float value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.Float, name, "", value, 0, false);
    }

    public static Ps3SourceGameEventFieldValue Long(string name, int value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.Long, name, "", 0.0f, value, false);
    }

    public static Ps3SourceGameEventFieldValue Short(string name, short value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.Short, name, "", 0.0f, value, false);
    }

    public static Ps3SourceGameEventFieldValue Byte(string name, byte value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.Byte, name, "", 0.0f, value, false);
    }

    public static Ps3SourceGameEventFieldValue Bool(string name, bool value)
    {
        return new Ps3SourceGameEventFieldValue(Ps3SourceGameEventFieldType.Bool, name, "", 0.0f, value ? 1 : 0, value);
    }
}

public sealed record Ps3SourceSvcGetCvarValue(int Cookie, string CvarName);

public sealed record Ps3SourceClcClientInfo(
    int ServerCount,
    uint SendTableCrc,
    bool IsHltv,
    uint FriendsId,
    string FriendsName,
    IReadOnlyList<uint> CustomFiles);

public sealed record Ps3SourceNetTick(int Tick);

public sealed record Ps3SourceNetStringCmd(string Command);

public sealed record Ps3SourceNetConVarPair(string Name, string Value);

public sealed record Ps3SourceNetSetConVar(IReadOnlyList<Ps3SourceNetConVarPair> Values);

public sealed record Ps3SourceClcVoiceData(int DataBitCount, byte[] Data);

public sealed record Ps3SourceClcBaselineAck(
    int BaselineTick,
    int BaselineNumber);

public sealed record Ps3SourceClcListenEvents(IReadOnlyList<uint> EventMaskWords);

public sealed record Ps3SourceClcRespondCvarValue(
    uint Cookie,
    int StatusCode,
    string CvarName,
    string CvarValue);

public sealed record Ps3SourceClcFileCrcCheck(
    bool ReservedBit,
    int PathIdCode,
    string PathId,
    int FilenamePrefixCode,
    string Filename,
    uint Crc);

public static class Ps3SourceNetMessages
{
    private static readonly string[] CommonFileCrcPathIds = ["", "GAME", "MOD"];
    private static readonly string[] CommonFileCrcFilenamePrefixes = ["", "materials", "models", "sounds", "scripts"];

    public static Ps3SourceNetMessageFrame ConcatenateFrames(IEnumerable<Ps3SourceNetMessageFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var writer = new Ps3SourceNetBitWriter();
        foreach (var frame in frames)
        {
            writer.WriteBits(frame.Payload, frame.BitCount);
        }

        return writer.ToFrame();
    }

    public static byte[] BuildServerInfo(Ps3SourceSvcServerInfo message)
    {
        return BuildServerInfoFrame(message).Payload;
    }

    public static byte[] BuildNetTick(Ps3SourceNetTick message)
    {
        return BuildNetTickFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildNetTickFrame(Ps3SourceNetTick message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.NetTick);
        writer.WriteLong(message.Tick);
        return writer.ToFrame();
    }

    public static byte[] BuildNetStringCmd(Ps3SourceNetStringCmd message)
    {
        return BuildNetStringCmdFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildNetStringCmdFrame(Ps3SourceNetStringCmd message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.NetStringCmd);
        writer.WriteString(string.IsNullOrEmpty(message.Command) ? " NET_StringCmd NULL" : message.Command);
        return writer.ToFrame();
    }

    public static byte[] BuildNetSetConVar(Ps3SourceNetSetConVar message)
    {
        return BuildNetSetConVarFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildNetSetConVarFrame(Ps3SourceNetSetConVar message)
    {
        var writer = new Ps3SourceNetBitWriter();
        writer.WriteUBitLong(Ps3SourceNetMessageConstants.NetSetConVar, Ps3SourceNetMessageConstants.NetMessageTypeBits);
        writer.WriteByte(checked((byte)message.Values.Count));
        foreach (var pair in message.Values)
        {
            writer.WriteString(pair.Name);
            writer.WriteString(pair.Value);
        }

        return writer.ToFrame();
    }

    public static byte[] BuildPrint(Ps3SourceSvcPrint message)
    {
        return BuildPrintFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildPrintFrame(Ps3SourceSvcPrint message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcPrint);
        writer.WriteString(string.IsNullOrEmpty(message.Text) ? " svc_print NULL" : message.Text);
        return writer.ToFrame();
    }

    public static byte[] BuildSetPause(Ps3SourceSvcSetPause message)
    {
        return BuildSetPauseFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildSetPauseFrame(Ps3SourceSvcSetPause message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcSetPause);
        writer.WriteOneBit(message.Paused);
        return writer.ToFrame();
    }

    public static byte[] BuildVoiceInit(Ps3SourceSvcVoiceInit message)
    {
        return BuildVoiceInitFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildVoiceInitFrame(Ps3SourceSvcVoiceInit message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcVoiceInit);
        writer.WriteString(message.Codec);
        writer.WriteByte(message.LegacyQuality);
        return writer.ToFrame();
    }

    public static byte[] BuildVoiceData(Ps3SourceSvcVoiceData message)
    {
        return BuildVoiceDataFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildVoiceDataFrame(Ps3SourceSvcVoiceData message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcVoiceData);
        writer.WriteByte(message.FromClient);
        writer.WriteByte(message.Proximity ? (byte)1 : (byte)0);
        writer.WriteWord(checked((ushort)message.DataBitCount));
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildSounds(Ps3SourceSvcSounds message)
    {
        return BuildSoundsFrame(message).Payload;
    }

    public static byte[] BuildSounds(bool reliableSound, IReadOnlyList<Ps3SourceSoundInfo> sounds)
    {
        return BuildSoundsFrame(reliableSound, sounds).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildSoundsFrame(Ps3SourceSvcSounds message)
    {
        if (message.ReliableSound && message.NumSounds != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Reliable SVC_Sounds frames always carry one sound.");
        }

        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcSounds);
        writer.WriteOneBit(message.ReliableSound);
        if (message.ReliableSound)
        {
            writer.WriteUBitLong(checked((uint)message.DataBitCount), 8);
        }
        else
        {
            writer.WriteByte(message.NumSounds);
            writer.WriteUBitLong(checked((uint)message.DataBitCount), 16);
        }

        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildSoundsFrame(bool reliableSound, IReadOnlyList<Ps3SourceSoundInfo> sounds)
    {
        return BuildSoundsFrame(BuildSoundsMessage(reliableSound, sounds));
    }

    public static Ps3SourceSvcSounds BuildSoundsMessage(bool reliableSound, IReadOnlyList<Ps3SourceSoundInfo> sounds)
    {
        ArgumentNullException.ThrowIfNull(sounds);
        if (sounds.Count == 0 || sounds.Count > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sounds), "SVC_Sounds must contain 1..255 sound entries.");
        }

        if (reliableSound && sounds.Count != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sounds), "Reliable SVC_Sounds frames always carry one sound.");
        }

        var payload = BuildSoundInfoPayload(sounds, out var bitCount);
        return new Ps3SourceSvcSounds(reliableSound, checked((byte)sounds.Count), bitCount, payload);
    }

    public static byte[] BuildSoundInfoPayload(IReadOnlyList<Ps3SourceSoundInfo> sounds, out int bitCount)
    {
        ArgumentNullException.ThrowIfNull(sounds);

        var writer = new Ps3SourceNetBitWriter();
        var delta = Ps3SourceSoundInfo.Default;
        foreach (var sound in sounds)
        {
            ArgumentNullException.ThrowIfNull(sound);
            WriteSoundInfoDelta(writer, sound, delta);
            delta = sound.Flags == Ps3SourceNetMessageConstants.SoundStopFlag
                ? sound.ClearStopFields()
                : sound;
        }

        var frame = writer.ToFrame();
        bitCount = frame.BitCount;
        return frame.Payload;
    }

    public static byte[] BuildFixAngle(Ps3SourceSvcFixAngle message)
    {
        return BuildFixAngleFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildFixAngleFrame(Ps3SourceSvcFixAngle message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcFixAngle);
        writer.WriteOneBit(message.Relative);
        writer.WriteBitAngle(message.Angle.X, 16);
        writer.WriteBitAngle(message.Angle.Y, 16);
        writer.WriteBitAngle(message.Angle.Z, 16);
        return writer.ToFrame();
    }

    public static byte[] BuildCrosshairAngle(Ps3SourceSvcCrosshairAngle message)
    {
        return BuildCrosshairAngleFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildCrosshairAngleFrame(Ps3SourceSvcCrosshairAngle message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcCrosshairAngle);
        writer.WriteBitAngle(message.Angle.X, 16);
        writer.WriteBitAngle(message.Angle.Y, 16);
        writer.WriteBitAngle(message.Angle.Z, 16);
        return writer.ToFrame();
    }

    public static byte[] BuildBspDecal(Ps3SourceSvcBspDecal message)
    {
        return BuildBspDecalFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildBspDecalFrame(Ps3SourceSvcBspDecal message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcBspDecal);
        writer.WriteBitVec3Coord(message.Position);
        writer.WriteUBitLong(checked((uint)message.DecalTextureIndex), Ps3SourceNetMessageConstants.MaxDecalIndexBits);
        if (message.EntityIndex != 0)
        {
            writer.WriteOneBit(true);
            writer.WriteUBitLong(checked((uint)message.EntityIndex), Ps3SourceNetMessageConstants.MaxEdictBits);
            writer.WriteUBitLong(checked((uint)message.ModelIndex), Ps3SourceNetMessageConstants.Ps3BspDecalModelIndexBits);
        }
        else
        {
            writer.WriteOneBit(false);
        }

        writer.WriteOneBit(message.LowPriority);
        return writer.ToFrame();
    }

    public static byte[] BuildUserMessage(Ps3SourceSvcUserMessage message)
    {
        return BuildUserMessageFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildUserMessageFrame(Ps3SourceSvcUserMessage message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcUserMessage);
        writer.WriteByte(message.MessageType);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.NetMessageLengthBits);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildEntityMessage(Ps3SourceSvcEntityMessage message)
    {
        return BuildEntityMessageFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildEntityMessageFrame(Ps3SourceSvcEntityMessage message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcEntityMessage);
        writer.WriteUBitLong(checked((uint)message.EntityIndex), Ps3SourceNetMessageConstants.MaxEdictBits);
        writer.WriteUBitLong(checked((uint)message.ClassId), Ps3SourceNetMessageConstants.MaxServerClassBits);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.NetMessageLengthBits);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildGameEvent(Ps3SourceSvcGameEvent message)
    {
        return BuildGameEventFrame(message).Payload;
    }

    public static byte[] BuildGameEvent(
        Ps3SourceGameEventDescriptor descriptor,
        IReadOnlyList<Ps3SourceGameEventFieldValue> values)
    {
        return BuildGameEventFrame(descriptor, values).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildGameEventFrame(Ps3SourceSvcGameEvent message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcGameEvent);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), 11);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildGameEventFrame(
        Ps3SourceGameEventDescriptor descriptor,
        IReadOnlyList<Ps3SourceGameEventFieldValue> values)
    {
        return BuildGameEventFrame(BuildGameEventMessage(descriptor, values));
    }

    public static Ps3SourceSvcGameEvent BuildGameEventMessage(
        Ps3SourceGameEventDescriptor descriptor,
        IReadOnlyList<Ps3SourceGameEventFieldValue> values)
    {
        var data = BuildGameEventPayload(descriptor, values, out var bitCount);
        return new Ps3SourceSvcGameEvent(bitCount, data);
    }

    public static byte[] BuildGameEventPayload(
        Ps3SourceGameEventDescriptor descriptor,
        IReadOnlyList<Ps3SourceGameEventFieldValue> values,
        out int bitCount)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(values);

        var valueIndex = BuildGameEventValueIndex(descriptor, values);
        var writer = new Ps3SourceNetBitWriter();
        writer.WriteUBitLong(checked((uint)descriptor.EventId), Ps3SourceNetMessageConstants.MaxEventBits);
        foreach (var field in descriptor.Fields)
        {
            if (field.Type == Ps3SourceGameEventFieldType.Local)
            {
                continue;
            }

            var value = valueIndex.TryGetValue(field.Name, out var indexedValue)
                ? indexedValue
                : CreateDefaultGameEventValue(field);

            WriteGameEventFieldValue(writer, value);
        }

        var frame = writer.ToFrame();
        bitCount = frame.BitCount;
        return frame.Payload;
    }

    public static byte[] BuildTempEntities(Ps3SourceSvcTempEntities message)
    {
        return BuildTempEntitiesFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildTempEntitiesFrame(Ps3SourceSvcTempEntities message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcTempEntities);
        writer.WriteUBitLong(message.NumEntries, Ps3SourceNetMessageConstants.TempEntityEventIndexBits);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.TempEntityPayloadLengthBits);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildPrefetch(Ps3SourceSvcPrefetch message)
    {
        return BuildPrefetchFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildPrefetchFrame(Ps3SourceSvcPrefetch message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcPrefetch);
        writer.WriteUBitLong(message.SoundIndex, Ps3SourceNetMessageConstants.MaxSoundIndexBits);
        return writer.ToFrame();
    }

    public static byte[] BuildMenu(Ps3SourceSvcMenu message)
    {
        return BuildMenuFrame(message).Payload;
    }

    public static byte[] BuildMenu(Ps3SourceSvcMenuKeyValues message)
    {
        return BuildMenuFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildMenuFrame(Ps3SourceSvcMenu message)
    {
        if (message.BinaryKeyValues.Length > Ps3SourceNetMessageConstants.MaxMenuDataBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "SVC_Menu binary KeyValues payload exceeds the TF.elf 4096 byte cap.");
        }

        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcMenu);
        writer.WriteShort(message.DialogType);
        writer.WriteWord(checked((ushort)message.BinaryKeyValues.Length));
        writer.WriteBytes(message.BinaryKeyValues);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildMenuFrame(Ps3SourceSvcMenuKeyValues message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return BuildMenuFrame(new Ps3SourceSvcMenu(
            message.DialogType,
            Ps3SourceKeyValues.BuildBinary(message.KeyValues)));
    }

    public static byte[] BuildGameEventList(Ps3SourceSvcGameEventList message)
    {
        return BuildGameEventListFrame(message).Payload;
    }

    public static byte[] BuildGameEventList(IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors)
    {
        return BuildGameEventListFrame(descriptors).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildGameEventListFrame(Ps3SourceSvcGameEventList message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcGameEventList);
        writer.WriteUBitLong(checked((uint)message.NumEvents), Ps3SourceNetMessageConstants.MaxEventBits);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.DeltaSizeBits);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildGameEventListFrame(IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors)
    {
        return BuildGameEventListFrame(BuildGameEventListMessage(descriptors));
    }

    public static Ps3SourceSvcGameEventList BuildGameEventListMessage(IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        if (descriptors.Count >= (1 << Ps3SourceNetMessageConstants.MaxEventBits))
        {
            throw new ArgumentOutOfRangeException(nameof(descriptors), "SVC_GameEventList event count exceeds the TF.elf 9-bit event-count field.");
        }

        var data = BuildGameEventDescriptorPayload(descriptors, out var bitCount);
        return new Ps3SourceSvcGameEventList(descriptors.Count, bitCount, data);
    }

    public static byte[] BuildGameEventDescriptorPayload(
        IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors,
        out int bitCount)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var writer = new Ps3SourceNetBitWriter();
        for (var eventIndex = 0; eventIndex < descriptors.Count; eventIndex++)
        {
            var descriptor = descriptors[eventIndex];
            if (descriptor is null)
            {
                throw new ArgumentException("Game event descriptor list contains a null descriptor.", nameof(descriptors));
            }

            if (descriptor.EventId < 0 || descriptor.EventId >= (1 << Ps3SourceNetMessageConstants.MaxEventBits))
            {
                throw new ArgumentOutOfRangeException(nameof(descriptors), $"Game event id {descriptor.EventId} does not fit in {Ps3SourceNetMessageConstants.MaxEventBits} bits.");
            }

            ValidateGameEventDescriptorString(
                descriptor.Name,
                nameof(descriptors),
                Ps3SourceNetMessageConstants.MaxGameEventNameLength,
                "game event name");

            writer.WriteUBitLong(checked((uint)descriptor.EventId), Ps3SourceNetMessageConstants.MaxEventBits);
            writer.WriteString(descriptor.Name);

            ArgumentNullException.ThrowIfNull(descriptor.Fields);
            for (var fieldIndex = 0; fieldIndex < descriptor.Fields.Count; fieldIndex++)
            {
                var field = descriptor.Fields[fieldIndex];
                if (field is null)
                {
                    throw new ArgumentException("Game event descriptor field list contains a null field.", nameof(descriptors));
                }

                if (field.Type == Ps3SourceGameEventFieldType.Local)
                {
                    throw new ArgumentException("TYPE_LOCAL is reserved as the event-field list terminator and cannot be emitted as a named field.", nameof(descriptors));
                }

                ValidateGameEventDescriptorString(
                    field.Name,
                    nameof(descriptors),
                    Ps3SourceNetMessageConstants.MaxGameEventDescriptorStringLength,
                    "game event field name");

                writer.WriteUBitLong(checked((uint)field.Type), Ps3SourceNetMessageConstants.GameEventFieldTypeBits);
                writer.WriteString(field.Name);
            }

            writer.WriteUBitLong((uint)Ps3SourceGameEventFieldType.Local, Ps3SourceNetMessageConstants.GameEventFieldTypeBits);
        }

        var frame = writer.ToFrame();
        bitCount = frame.BitCount;
        return frame.Payload;
    }

    public static byte[] BuildGetCvarValue(Ps3SourceSvcGetCvarValue message)
    {
        return BuildGetCvarValueFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildGetCvarValueFrame(Ps3SourceSvcGetCvarValue message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcGetCvarValue);
        writer.WriteSBitLong(message.Cookie, 32);
        writer.WriteString(message.CvarName);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildServerInfoFrame(Ps3SourceSvcServerInfo message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcServerInfo);
        writer.WriteShort(message.Protocol);
        writer.WriteLong(message.ServerCount);
        writer.WriteOneBit(message.IsHltv);
        writer.WriteOneBit(message.IsDedicated);
        writer.WriteLong(message.LegacyClientCrc);
        writer.WriteWord(message.MaxClasses);
        writer.WriteLong(message.MapCrcOrDigest32);
        writer.WriteByte(message.PlayerSlot);
        writer.WriteByte(message.MaxClients);
        writer.WriteFloat(message.TickInterval);
        writer.WriteByte(message.OperatingSystem);
        writer.WriteString(message.GameDirectory);
        writer.WriteString(message.MapName);
        writer.WriteString(message.SkyName);
        writer.WriteString(message.HostName);
        return writer.ToFrame();
    }

    public static byte[] BuildSendTable(Ps3SourceSvcSendTable message)
    {
        return BuildSendTableFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildSendTableFrame(Ps3SourceSvcSendTable message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcSendTable);
        writer.WriteOneBit(message.NeedsDecoder);
        writer.WriteShort(checked((short)message.DataBitCount));
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildClassInfo(Ps3SourceSvcClassInfo message)
    {
        return BuildClassInfoFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildClassInfoFrame(Ps3SourceSvcClassInfo message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcClassInfo);
        writer.WriteShort(message.NumServerClasses);
        var serverClassBits = Log2(message.NumServerClasses) + 1;
        writer.WriteOneBit(message.CreateOnClient);
        if (!message.CreateOnClient)
        {
            foreach (var serverClass in message.Classes)
            {
                writer.WriteUBitLong(checked((uint)serverClass.ClassId), serverClassBits);
                writer.WriteString(serverClass.ClassName);
                writer.WriteString(serverClass.DataTableName);
            }
        }

        return writer.ToFrame();
    }

    public static byte[] BuildCreateStringTable(Ps3SourceSvcStringTable message)
    {
        return BuildCreateStringTableFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildCreateStringTableFrame(Ps3SourceSvcStringTable message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcCreateStringTable);
        writer.WriteString(message.TableName);
        writer.WriteWord(message.MaxEntries);
        writer.WriteUBitLong(checked((uint)message.NumEntries), Log2(message.MaxEntries) + 1);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.DeltaSizeBits);
        writer.WriteOneBit(message.UserDataFixedSize);
        if (message.UserDataFixedSize)
        {
            writer.WriteUBitLong(checked((uint)message.UserDataSize), 12);
            writer.WriteUBitLong(checked((uint)message.UserDataSizeBits), 4);
        }

        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static byte[] BuildUpdateStringTable(Ps3SourceSvcStringTableUpdate message)
    {
        return BuildUpdateStringTableFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildUpdateStringTableFrame(Ps3SourceSvcStringTableUpdate message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcUpdateStringTable);
        writer.WriteUBitLong(checked((uint)message.TableId), Log2(Ps3SourceNetMessageConstants.MaxStringTables));
        if (message.ChangedEntries == 1)
        {
            writer.WriteOneBit(false);
        }
        else
        {
            writer.WriteOneBit(true);
            writer.WriteWord(checked((ushort)message.ChangedEntries));
        }

        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.DeltaSizeBits);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static Ps3SourceNetMessageFrame BuildStringTableUpdateDataFrame(
        ushort maxEntries,
        IReadOnlyList<Ps3SourceStringTableEntry> entries,
        bool entriesAreCreated = true,
        bool userDataFixedSize = false)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var writer = new Ps3SourceNetBitWriter();
        var entryBits = Log2(maxEntries);
        var lastEntry = -1;
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.UserData);
            if (entry.Index < 0 || entry.Index >= maxEntries)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), $"String-table entry index {entry.Index} is outside 0..{maxEntries - 1}.");
            }

            if (entry.Index <= lastEntry)
            {
                throw new ArgumentException("String-table entries must be strictly increasing by index.", nameof(entries));
            }

            if (entry.UserDataBitCount < 0 || entry.UserDataBitCount > entry.UserData.Length * 8)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "String-table user-data bit count exceeds the supplied data.");
            }

            if (lastEntry + 1 == entry.Index)
            {
                writer.WriteOneBit(true);
            }
            else
            {
                writer.WriteOneBit(false);
                writer.WriteUBitLong(checked((uint)entry.Index), entryBits);
            }

            writer.WriteOneBit(entriesAreCreated);
            if (entriesAreCreated)
            {
                writer.WriteOneBit(false);
                writer.WriteString(entry.Value);
            }

            if (entry.UserDataBitCount == 0)
            {
                writer.WriteOneBit(false);
            }
            else
            {
                writer.WriteOneBit(true);
                if (userDataFixedSize)
                {
                    writer.WriteBits(entry.UserData, entry.UserDataBitCount);
                }
                else
                {
                    if ((entry.UserDataBitCount & 7) != 0)
                    {
                        throw new ArgumentException("Variable string-table user data must be byte-aligned.", nameof(entries));
                    }

                    var userDataByteCount = entry.UserDataBitCount >> 3;
                    writer.WriteUBitLong(checked((uint)userDataByteCount), Ps3SourceNetMessageConstants.StringTableMaxUserDataBits);
                    writer.WriteBits(entry.UserData, entry.UserDataBitCount);
                }
            }

            lastEntry = entry.Index;
        }

        return writer.ToFrame();
    }

    public static byte[] BuildNetSignonState(Ps3SourceNetSignonState message)
    {
        return BuildNetSignonStateFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildNetSignonStateFrame(Ps3SourceNetSignonState message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.NetSignonState);
        writer.WriteByte(message.SignonState);
        writer.WriteLong(message.SpawnCount);
        return writer.ToFrame();
    }

    public static byte[] BuildSetView(Ps3SourceSvcSetView message)
    {
        return BuildSetViewFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildSetViewFrame(Ps3SourceSvcSetView message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcSetView);
        writer.WriteUBitLong(checked((uint)message.EntityIndex), Ps3SourceNetMessageConstants.MaxEdictBits);
        return writer.ToFrame();
    }

    public static byte[] BuildPacketEntities(Ps3SourceSvcPacketEntities message)
    {
        return BuildPacketEntitiesFrame(message).Payload;
    }

    public static Ps3SourceNetMessageFrame BuildPacketEntitiesFrame(Ps3SourceSvcPacketEntities message)
    {
        var writer = new Ps3SourceNetBitWriter();
        WriteMessageType(writer, Ps3SourceNetMessageConstants.SvcPacketEntities);
        writer.WriteUBitLong(checked((uint)message.MaxEntries), Ps3SourceNetMessageConstants.MaxEdictBits);
        writer.WriteOneBit(message.IsDelta);
        if (message.IsDelta)
        {
            writer.WriteLong(message.DeltaFrom);
        }

        writer.WriteUBitLong(checked((uint)message.Baseline), 1);
        writer.WriteUBitLong(checked((uint)message.UpdatedEntries), Ps3SourceNetMessageConstants.MaxEdictBits);
        writer.WriteUBitLong(checked((uint)message.DataBitCount), Ps3SourceNetMessageConstants.DeltaSizeBits);
        writer.WriteOneBit(message.UpdateBaseline);
        writer.WriteBits(message.Data, message.DataBitCount);
        return writer.ToFrame();
    }

    public static bool TryDecodeNetTick(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceNetTick message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.NetTick))
        {
            return false;
        }

        if (!reader.TryReadLong(out var tick))
        {
            return false;
        }

        message = new Ps3SourceNetTick(unchecked((int)tick));
        return true;
    }

    public static bool TryDecodeSvcPrint(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcPrint message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcPrint)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxSvcPrintLength, out var text))
        {
            return false;
        }

        message = new Ps3SourceSvcPrint(text);
        return true;
    }

    public static bool TryDecodeSvcSetPause(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcSetPause message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcSetPause)
            || !reader.TryReadOneBit(out var paused))
        {
            return false;
        }

        message = new Ps3SourceSvcSetPause(paused);
        return true;
    }

    public static bool TryDecodeSvcVoiceInit(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcVoiceInit message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcVoiceInit)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxSvcVoiceCodecLength, out var codec)
            || !reader.TryReadUBitLong(8, out var legacyQuality))
        {
            return false;
        }

        message = new Ps3SourceSvcVoiceInit(codec, checked((byte)legacyQuality));
        return true;
    }

    public static bool TryDecodeSvcVoiceData(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcVoiceData message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcVoiceData)
            || !reader.TryReadUBitLong(8, out var fromClient)
            || !reader.TryReadUBitLong(8, out var proximity)
            || !reader.TryReadUBitLong(16, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcVoiceData(
            checked((byte)fromClient),
            proximity != 0,
            checked((int)dataBitCount),
            data);
        return true;
    }

    public static bool TryDecodeSvcSounds(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcSounds message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcSounds)
            || !reader.TryReadOneBit(out var reliableSound))
        {
            return false;
        }

        byte numSounds;
        uint dataBitCount;
        if (reliableSound)
        {
            numSounds = 1;
            if (!reader.TryReadUBitLong(8, out dataBitCount))
            {
                return false;
            }
        }
        else
        {
            if (!reader.TryReadUBitLong(8, out var rawNumSounds)
                || !reader.TryReadUBitLong(16, out dataBitCount))
            {
                return false;
            }

            numSounds = checked((byte)rawNumSounds);
        }

        if (!reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcSounds(reliableSound, numSounds, checked((int)dataBitCount), data);
        return true;
    }

    public static bool TryDecodeSvcSoundInfos(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out bool reliableSound,
        out IReadOnlyList<Ps3SourceSoundInfo> sounds)
    {
        reliableSound = false;
        sounds = [];
        if (!TryDecodeSvcSounds(payload, includesMessageType, out var message)
            || !TryDecodeSoundInfoPayload(message.Data, message.DataBitCount, message.NumSounds, out sounds))
        {
            return false;
        }

        reliableSound = message.ReliableSound;
        return true;
    }

    public static bool TryDecodeSoundInfoPayload(
        ReadOnlySpan<byte> payload,
        int bitCount,
        int expectedSoundCount,
        out IReadOnlyList<Ps3SourceSoundInfo> sounds)
    {
        sounds = [];
        if (expectedSoundCount <= 0 || expectedSoundCount > byte.MaxValue)
        {
            return false;
        }

        var reader = new Ps3SourceNetBitReader(payload, bitCount);
        var decodedSounds = new List<Ps3SourceSoundInfo>(expectedSoundCount);
        var delta = Ps3SourceSoundInfo.Default;
        for (var i = 0; i < expectedSoundCount; i++)
        {
            if (!TryReadSoundInfoDelta(ref reader, delta, out var sound))
            {
                return false;
            }

            decodedSounds.Add(sound);
            delta = sound;
        }

        if (reader.ConsumedBits != bitCount)
        {
            return false;
        }

        sounds = decodedSounds;
        return true;
    }

    public static bool TryDecodeSvcFixAngle(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcFixAngle message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcFixAngle)
            || !reader.TryReadOneBit(out var relative)
            || !reader.TryReadBitAngle(16, out var x)
            || !reader.TryReadBitAngle(16, out var y)
            || !reader.TryReadBitAngle(16, out var z))
        {
            return false;
        }

        message = new Ps3SourceSvcFixAngle(relative, new Ps3SourceQAngle(x, y, z));
        return true;
    }

    public static bool TryDecodeSvcCrosshairAngle(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcCrosshairAngle message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcCrosshairAngle)
            || !reader.TryReadBitAngle(16, out var x)
            || !reader.TryReadBitAngle(16, out var y)
            || !reader.TryReadBitAngle(16, out var z))
        {
            return false;
        }

        message = new Ps3SourceSvcCrosshairAngle(new Ps3SourceQAngle(x, y, z));
        return true;
    }

    public static bool TryDecodeSvcBspDecal(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcBspDecal message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcBspDecal)
            || !reader.TryReadBitVec3Coord(out var position)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxDecalIndexBits, out var decalTextureIndex)
            || !reader.TryReadOneBit(out var hasEntity))
        {
            return false;
        }

        uint entityIndex = 0;
        uint modelIndex = 0;
        if (hasEntity
            && (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEdictBits, out entityIndex)
                || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.Ps3BspDecalModelIndexBits, out modelIndex)))
        {
            return false;
        }

        if (!reader.TryReadOneBit(out var lowPriority))
        {
            return false;
        }

        message = new Ps3SourceSvcBspDecal(
            position,
            checked((int)decalTextureIndex),
            checked((int)entityIndex),
            checked((int)modelIndex),
            lowPriority);
        return true;
    }

    public static bool TryDecodeSvcUserMessage(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcUserMessage message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcUserMessage)
            || !reader.TryReadUBitLong(8, out var messageType)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.NetMessageLengthBits, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcUserMessage(
            checked((byte)messageType),
            checked((int)dataBitCount),
            data);
        return true;
    }

    public static bool TryDecodeSvcEntityMessage(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcEntityMessage message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcEntityMessage)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEdictBits, out var entityIndex)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxServerClassBits, out var classId)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.NetMessageLengthBits, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcEntityMessage(
            checked((int)entityIndex),
            checked((int)classId),
            checked((int)dataBitCount),
            data);
        return true;
    }

    public static bool TryDecodeSvcGameEvent(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcGameEvent message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcGameEvent)
            || !reader.TryReadUBitLong(11, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcGameEvent(checked((int)dataBitCount), data);
        return true;
    }

    public static bool TryDecodeSvcGameEventInstance(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        IReadOnlyDictionary<int, Ps3SourceGameEventDescriptor> descriptorsByEventId,
        out Ps3SourceGameEventInstance instance)
    {
        instance = default!;
        if (!TryDecodeSvcGameEvent(payload, includesMessageType, out var message))
        {
            return false;
        }

        return TryDecodeGameEventPayload(
            message.Data,
            message.DataBitCount,
            descriptorsByEventId,
            out instance);
    }

    public static bool TryDecodeGameEventPayload(
        ReadOnlySpan<byte> payload,
        int bitCount,
        IReadOnlyDictionary<int, Ps3SourceGameEventDescriptor> descriptorsByEventId,
        out Ps3SourceGameEventInstance instance)
    {
        instance = default!;
        ArgumentNullException.ThrowIfNull(descriptorsByEventId);

        var reader = new Ps3SourceNetBitReader(payload, bitCount);
        if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEventBits, out var rawEventId)
            || !descriptorsByEventId.TryGetValue(checked((int)rawEventId), out var descriptor))
        {
            return false;
        }

        var fields = new List<Ps3SourceGameEventFieldValue>();
        foreach (var field in descriptor.Fields)
        {
            if (field.Type == Ps3SourceGameEventFieldType.Local)
            {
                continue;
            }

            if (!TryReadGameEventFieldValue(ref reader, field, out var value))
            {
                return false;
            }

            fields.Add(value);
        }

        if (reader.ConsumedBits != bitCount)
        {
            return false;
        }

        instance = new Ps3SourceGameEventInstance(checked((int)rawEventId), fields);
        return true;
    }

    public static bool TryDecodeSvcTempEntities(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcTempEntities message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcTempEntities)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.TempEntityEventIndexBits, out var numEntries)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.TempEntityPayloadLengthBits, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcTempEntities(
            checked((byte)numEntries),
            checked((int)dataBitCount),
            data);
        return true;
    }

    public static bool TryDecodeSvcPrefetch(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcPrefetch message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcPrefetch)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxSoundIndexBits, out var soundIndex))
        {
            return false;
        }

        message = new Ps3SourceSvcPrefetch(checked((ushort)soundIndex));
        return true;
    }

    public static bool TryDecodeSvcMenu(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcMenu message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcMenu)
            || !reader.TryReadSBitLong(16, out var dialogType)
            || !reader.TryReadUBitLong(16, out var length)
            || length > Ps3SourceNetMessageConstants.MaxMenuDataBytes
            || !reader.TryReadBytes(checked((int)length), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcMenu(checked((short)dialogType), data);
        return true;
    }

    public static bool TryDecodeSvcMenuKeyValues(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcMenuKeyValues message)
    {
        message = default!;
        if (!TryDecodeSvcMenu(payload, includesMessageType, out var rawMenu)
            || !Ps3SourceKeyValues.TryDecodeBinary(rawMenu.BinaryKeyValues, out var keyValues))
        {
            return false;
        }

        message = new Ps3SourceSvcMenuKeyValues(rawMenu.DialogType, keyValues);
        return true;
    }

    public static bool TryDecodeSvcGameEventList(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcGameEventList message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcGameEventList)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEventBits, out var numEvents)
            || !reader.TryReadUBitLong(Ps3SourceNetMessageConstants.DeltaSizeBits, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceSvcGameEventList(
            checked((int)numEvents),
            checked((int)dataBitCount),
            data);
        return true;
    }

    public static bool TryDecodeSvcGameEventListDescriptors(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors)
    {
        descriptors = [];
        if (!TryDecodeSvcGameEventList(payload, includesMessageType, out var message))
        {
            return false;
        }

        return TryDecodeGameEventDescriptorPayload(
            message.Data,
            message.DataBitCount,
            message.NumEvents,
            out descriptors);
    }

    public static bool TryDecodeGameEventDescriptorPayload(
        ReadOnlySpan<byte> payload,
        int bitCount,
        int expectedEventCount,
        out IReadOnlyList<Ps3SourceGameEventDescriptor> descriptors)
    {
        descriptors = [];
        if (expectedEventCount < 0 || expectedEventCount >= (1 << Ps3SourceNetMessageConstants.MaxEventBits))
        {
            return false;
        }

        var reader = new Ps3SourceNetBitReader(payload, bitCount);
        var decodedDescriptors = new List<Ps3SourceGameEventDescriptor>(expectedEventCount);
        for (var eventIndex = 0; eventIndex < expectedEventCount; eventIndex++)
        {
            if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEventBits, out var rawEventId)
                || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxGameEventDescriptorStringLength, out var eventName))
            {
                return false;
            }

            var fields = new List<Ps3SourceGameEventFieldDescriptor>();
            while (true)
            {
                if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.GameEventFieldTypeBits, out var rawFieldType)
                    || !TryConvertGameEventFieldType(rawFieldType, out var fieldType))
                {
                    return false;
                }

                if (fieldType == Ps3SourceGameEventFieldType.Local)
                {
                    break;
                }

                if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxGameEventDescriptorStringLength, out var fieldName))
                {
                    return false;
                }

                fields.Add(new Ps3SourceGameEventFieldDescriptor(fieldType, fieldName));
            }

            decodedDescriptors.Add(new Ps3SourceGameEventDescriptor(checked((int)rawEventId), eventName, fields));
        }

        if (reader.ConsumedBits != bitCount)
        {
            return false;
        }

        descriptors = decodedDescriptors;
        return true;
    }

    public static bool TryDecodeSvcGetCvarValue(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceSvcGetCvarValue message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.SvcGetCvarValue)
            || !reader.TryReadSBitLong(32, out var cookie)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxSvcCvarNameLength, out var cvarName))
        {
            return false;
        }

        message = new Ps3SourceSvcGetCvarValue(cookie, cvarName);
        return true;
    }

    public static bool TryDecodeNetStringCmd(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceNetStringCmd message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.NetStringCmd)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxNetStringCommandLength, out var command))
        {
            return false;
        }

        message = new Ps3SourceNetStringCmd(command);
        return true;
    }

    public static bool TryDecodeNetSetConVar(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceNetSetConVar message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.NetSetConVar)
            || !reader.TryReadUBitLong(8, out var count))
        {
            return false;
        }

        var countValue = checked((int)count);
        var values = new List<Ps3SourceNetConVarPair>(countValue);
        for (var i = 0; i < countValue; i++)
        {
            if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxSetConVarStringLength, out var name)
                || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxSetConVarStringLength, out var value))
            {
                return false;
            }

            values.Add(new Ps3SourceNetConVarPair(name, value));
        }

        message = new Ps3SourceNetSetConVar(values);
        return true;
    }

    public static bool TryDecodeNetSignonState(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceNetSignonState message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.NetSignonState)
            || !reader.TryReadUBitLong(8, out var signonState)
            || !reader.TryReadLong(out var spawnCount))
        {
            return false;
        }

        message = new Ps3SourceNetSignonState(checked((byte)signonState), unchecked((int)spawnCount));
        return true;
    }

    public static bool TryDecodeClientInfo(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcClientInfo message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcClientInfo))
        {
            return false;
        }

        if (!reader.TryReadLong(out var serverCount)
            || !reader.TryReadLong(out var sendTableCrc)
            || !reader.TryReadOneBit(out var isHltv)
            || !reader.TryReadLong(out var friendsId)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxPlayerNameLength, out var friendsName))
        {
            return false;
        }

        var customFiles = new uint[Ps3SourceNetMessageConstants.MaxCustomFiles];
        for (var i = 0; i < customFiles.Length; i++)
        {
            if (!reader.TryReadOneBit(out var present))
            {
                return false;
            }

            if (present && !reader.TryReadLong(out customFiles[i]))
            {
                return false;
            }
        }

        message = new Ps3SourceClcClientInfo(
            unchecked((int)serverCount),
            sendTableCrc,
            isHltv,
            friendsId,
            friendsName,
            customFiles);
        return true;
    }

    public static bool TryDecodeVoiceData(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcVoiceData message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcVoiceData)
            || !reader.TryReadUBitLong(16, out var dataBitCount)
            || !reader.TryReadBitPayload(checked((int)dataBitCount), out var data))
        {
            return false;
        }

        message = new Ps3SourceClcVoiceData(checked((int)dataBitCount), data);
        return true;
    }

    public static bool TryDecodeBaselineAck(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcBaselineAck message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcBaselineAck))
        {
            return false;
        }

        if (!reader.TryReadLong(out var baselineTick)
            || !reader.TryReadUBitLong(1, out var baselineNumber))
        {
            return false;
        }

        message = new Ps3SourceClcBaselineAck(unchecked((int)baselineTick), checked((int)baselineNumber));
        return true;
    }

    public static bool TryDecodeListenEvents(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcListenEvents message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcListenEvents))
        {
            return false;
        }

        var eventMaskWords = new uint[Ps3SourceNetMessageConstants.ListenEventMaskWordCount];
        for (var i = 0; i < eventMaskWords.Length; i++)
        {
            if (!reader.TryReadLong(out eventMaskWords[i]))
            {
                return false;
            }
        }

        message = new Ps3SourceClcListenEvents(eventMaskWords);
        return true;
    }

    public static bool TryDecodeRespondCvarValue(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcRespondCvarValue message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcRespondCvarValue)
            || !reader.TryReadLong(out var cookie)
            || !reader.TryReadUBitLong(4, out var statusCode)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxRespondCvarStringLength, out var cvarName)
            || !reader.TryReadString(Ps3SourceNetMessageConstants.MaxRespondCvarStringLength, out var cvarValue))
        {
            return false;
        }

        message = new Ps3SourceClcRespondCvarValue(
            cookie,
            SignExtend(statusCode, 4),
            cvarName,
            cvarValue);
        return true;
    }

    public static bool TryDecodeFileCrcCheck(
        ReadOnlySpan<byte> payload,
        bool includesMessageType,
        out Ps3SourceClcFileCrcCheck message)
    {
        message = default!;
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!TryReadExpectedMessageType(ref reader, includesMessageType, Ps3SourceNetMessageConstants.ClcFileCrcCheck)
            || !reader.TryReadOneBit(out var reservedBit)
            || !reader.TryReadUBitLong(2, out var pathIdCodeRaw)
            || pathIdCodeRaw >= CommonFileCrcPathIds.Length)
        {
            return false;
        }

        string pathId;
        if (pathIdCodeRaw == 0)
        {
            if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxFileCrcPathLength, out pathId))
            {
                return false;
            }
        }
        else
        {
            pathId = CommonFileCrcPathIds[checked((int)pathIdCodeRaw)];
        }

        if (!reader.TryReadUBitLong(3, out var filenamePrefixCodeRaw)
            || filenamePrefixCodeRaw >= CommonFileCrcFilenamePrefixes.Length)
        {
            return false;
        }

        string filename;
        if (filenamePrefixCodeRaw == 0)
        {
            if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxFileCrcFilenameLength, out filename))
            {
                return false;
            }
        }
        else
        {
            if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxFileCrcFilenameLength, out var tail))
            {
                return false;
            }

            filename = JoinCommonFileCrcPrefix(CommonFileCrcFilenamePrefixes[checked((int)filenamePrefixCodeRaw)], tail);
        }

        if (!reader.TryReadLong(out var crc))
        {
            return false;
        }

        message = new Ps3SourceClcFileCrcCheck(
            reservedBit,
            checked((int)pathIdCodeRaw),
            pathId,
            checked((int)filenamePrefixCodeRaw),
            filename,
            crc);
        return true;
    }

    public static bool TryReadMessageType(ReadOnlySpan<byte> payload, out int messageType)
    {
        var reader = new Ps3SourceNetBitReader(payload, payload.Length * 8);
        if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.NetMessageTypeBits, out var value))
        {
            messageType = 0;
            return false;
        }

        messageType = checked((int)value);
        return true;
    }

    public static string MessageTypeName(int messageType)
    {
        return messageType switch
        {
            Ps3SourceNetMessageConstants.NetTick => "NET_Tick",
            Ps3SourceNetMessageConstants.NetStringCmd => "NET_StringCmd",
            Ps3SourceNetMessageConstants.NetSetConVar => "NET_SetConVar",
            Ps3SourceNetMessageConstants.NetSignonState => "NET_SignonState",
            Ps3SourceNetMessageConstants.SvcPrint => "SVC_Print",
            Ps3SourceNetMessageConstants.SvcServerInfo => "SVC_ServerInfo",
            Ps3SourceNetMessageConstants.SvcSendTable => "SVC_SendTable",
            Ps3SourceNetMessageConstants.SvcClassInfo => "SVC_ClassInfo",
            Ps3SourceNetMessageConstants.SvcSetPause => "SVC_SetPause",
            Ps3SourceNetMessageConstants.SvcCreateStringTable => "SVC_CreateStringTable",
            Ps3SourceNetMessageConstants.SvcUpdateStringTable => "SVC_UpdateStringTable",
            Ps3SourceNetMessageConstants.SvcVoiceInit => "SVC_VoiceInit",
            Ps3SourceNetMessageConstants.SvcVoiceData => "SVC_VoiceData",
            Ps3SourceNetMessageConstants.SvcSounds => "SVC_Sounds",
            Ps3SourceNetMessageConstants.SvcSetView => "SVC_SetView",
            Ps3SourceNetMessageConstants.SvcFixAngle => "SVC_FixAngle",
            Ps3SourceNetMessageConstants.SvcCrosshairAngle => "SVC_CrosshairAngle",
            Ps3SourceNetMessageConstants.SvcBspDecal => "SVC_BspDecal",
            Ps3SourceNetMessageConstants.SvcUserMessage => "SVC_UserMessage",
            Ps3SourceNetMessageConstants.SvcEntityMessage => "SVC_EntityMessage",
            Ps3SourceNetMessageConstants.SvcGameEvent => "SVC_GameEvent",
            Ps3SourceNetMessageConstants.SvcPacketEntities => "SVC_PacketEntities",
            Ps3SourceNetMessageConstants.SvcTempEntities => "SVC_TempEntities",
            Ps3SourceNetMessageConstants.SvcPrefetch => "SVC_Prefetch",
            Ps3SourceNetMessageConstants.SvcMenu => "SVC_Menu",
            Ps3SourceNetMessageConstants.SvcGameEventList => "SVC_GameEventList",
            Ps3SourceNetMessageConstants.SvcGetCvarValue => "SVC_GetCvarValue",
            _ => $"message-{messageType}"
        };
    }

    private static bool TryReadExpectedMessageType(
        ref Ps3SourceNetBitReader reader,
        bool includesMessageType,
        int expectedMessageType)
    {
        return !includesMessageType
            || (reader.TryReadUBitLong(Ps3SourceNetMessageConstants.NetMessageTypeBits, out var type)
                && type == expectedMessageType);
    }

    private static void WriteMessageType(Ps3SourceNetBitWriter writer, int messageType)
    {
        writer.WriteUBitLong(checked((uint)messageType), Ps3SourceNetMessageConstants.NetMessageTypeBits);
    }

    private static int Log2(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var bits = 0;
        while ((1 << (bits + 1)) <= value)
        {
            bits++;
        }

        return bits;
    }

    internal static int SignExtend(uint value, int bitCount)
    {
        if (bitCount == 32)
        {
            return unchecked((int)value);
        }

        var signBit = 1u << (bitCount - 1);
        var mask = (1u << bitCount) - 1;
        value &= mask;
        return (value & signBit) == 0
            ? checked((int)value)
            : unchecked((int)(value | ~mask));
    }

    private static void ValidateGameEventDescriptorString(
        string value,
        string parameterName,
        int maxBytes,
        string description)
    {
        ArgumentNullException.ThrowIfNull(value);

        var byteCount = Encoding.ASCII.GetByteCount(value);
        if (byteCount >= maxBytes)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{description} is {byteCount} bytes; it must fit in {maxBytes - 1} bytes plus the NUL terminator.");
        }
    }

    private static bool TryConvertGameEventFieldType(uint raw, out Ps3SourceGameEventFieldType type)
    {
        type = raw switch
        {
            (uint)Ps3SourceGameEventFieldType.Local => Ps3SourceGameEventFieldType.Local,
            (uint)Ps3SourceGameEventFieldType.String => Ps3SourceGameEventFieldType.String,
            (uint)Ps3SourceGameEventFieldType.Float => Ps3SourceGameEventFieldType.Float,
            (uint)Ps3SourceGameEventFieldType.Long => Ps3SourceGameEventFieldType.Long,
            (uint)Ps3SourceGameEventFieldType.Short => Ps3SourceGameEventFieldType.Short,
            (uint)Ps3SourceGameEventFieldType.Byte => Ps3SourceGameEventFieldType.Byte,
            (uint)Ps3SourceGameEventFieldType.Bool => Ps3SourceGameEventFieldType.Bool,
            (uint)Ps3SourceGameEventFieldType.UInt64 => Ps3SourceGameEventFieldType.UInt64,
            _ => default,
        };

        return raw <= (uint)Ps3SourceGameEventFieldType.UInt64;
    }

    private static Dictionary<string, Ps3SourceGameEventFieldValue> BuildGameEventValueIndex(
        Ps3SourceGameEventDescriptor descriptor,
        IReadOnlyList<Ps3SourceGameEventFieldValue> values)
    {
        if (descriptor.EventId < 0 || descriptor.EventId >= (1 << Ps3SourceNetMessageConstants.MaxEventBits))
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), $"Game event id {descriptor.EventId} does not fit in {Ps3SourceNetMessageConstants.MaxEventBits} bits.");
        }

        ArgumentNullException.ThrowIfNull(descriptor.Fields);
        var expectedTypes = new Dictionary<string, Ps3SourceGameEventFieldType>(StringComparer.Ordinal);
        foreach (var field in descriptor.Fields)
        {
            if (field is null)
            {
                throw new ArgumentException("Game event descriptor field list contains a null field.", nameof(descriptor));
            }

            if (field.Type == Ps3SourceGameEventFieldType.Local)
            {
                continue;
            }

            if (!expectedTypes.TryAdd(field.Name, field.Type))
            {
                throw new ArgumentException($"Game event descriptor contains duplicate field '{field.Name}'.", nameof(descriptor));
            }
        }

        var valueIndex = new Dictionary<string, Ps3SourceGameEventFieldValue>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentException("Game event value list contains a null value.", nameof(values));
            }

            if (!expectedTypes.TryGetValue(value.Name, out var expectedType))
            {
                throw new ArgumentException($"Game event value '{value.Name}' is not declared by descriptor '{descriptor.Name}'.", nameof(values));
            }

            if (value.Type != expectedType)
            {
                throw new ArgumentException($"Game event value '{value.Name}' has type {value.Type}; descriptor expects {expectedType}.", nameof(values));
            }

            if (!valueIndex.TryAdd(value.Name, value))
            {
                throw new ArgumentException($"Game event value list contains duplicate field '{value.Name}'.", nameof(values));
            }
        }

        return valueIndex;
    }

    private static Ps3SourceGameEventFieldValue CreateDefaultGameEventValue(Ps3SourceGameEventFieldDescriptor field)
    {
        return field.Type switch
        {
            Ps3SourceGameEventFieldType.String => Ps3SourceGameEventFieldValue.String(field.Name, ""),
            Ps3SourceGameEventFieldType.Float => Ps3SourceGameEventFieldValue.Float(field.Name, 0.0f),
            Ps3SourceGameEventFieldType.Long => Ps3SourceGameEventFieldValue.Long(field.Name, 0),
            Ps3SourceGameEventFieldType.Short => Ps3SourceGameEventFieldValue.Short(field.Name, 0),
            Ps3SourceGameEventFieldType.Byte => Ps3SourceGameEventFieldValue.Byte(field.Name, 0),
            Ps3SourceGameEventFieldType.Bool => Ps3SourceGameEventFieldValue.Bool(field.Name, false),
            _ => throw new NotSupportedException($"Game event field type {field.Type} is not networked by the Orange Box event serializer."),
        };
    }

    private static void WriteGameEventFieldValue(Ps3SourceNetBitWriter writer, Ps3SourceGameEventFieldValue value)
    {
        switch (value.Type)
        {
            case Ps3SourceGameEventFieldType.String:
                writer.WriteString(value.StringValue);
                break;
            case Ps3SourceGameEventFieldType.Float:
                writer.WriteFloat(value.FloatValue);
                break;
            case Ps3SourceGameEventFieldType.Long:
                writer.WriteLong(value.IntValue);
                break;
            case Ps3SourceGameEventFieldType.Short:
                writer.WriteShort(checked((short)value.IntValue));
                break;
            case Ps3SourceGameEventFieldType.Byte:
                writer.WriteByte(checked((byte)value.IntValue));
                break;
            case Ps3SourceGameEventFieldType.Bool:
                writer.WriteOneBit(value.BoolValue);
                break;
            default:
                throw new NotSupportedException($"Game event field type {value.Type} is not networked by the Orange Box event serializer.");
        }
    }

    private static bool TryReadGameEventFieldValue(
        ref Ps3SourceNetBitReader reader,
        Ps3SourceGameEventFieldDescriptor field,
        out Ps3SourceGameEventFieldValue value)
    {
        value = default!;
        switch (field.Type)
        {
            case Ps3SourceGameEventFieldType.String:
                if (!reader.TryReadString(Ps3SourceNetMessageConstants.MaxGameEventDescriptorStringLength, out var stringValue))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.String(field.Name, stringValue);
                return true;
            case Ps3SourceGameEventFieldType.Float:
                if (!reader.TryReadLong(out var floatBits))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.Float(field.Name, BitConverter.UInt32BitsToSingle(floatBits));
                return true;
            case Ps3SourceGameEventFieldType.Long:
                if (!reader.TryReadLong(out var longValue))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.Long(field.Name, unchecked((int)longValue));
                return true;
            case Ps3SourceGameEventFieldType.Short:
                if (!reader.TryReadSBitLong(16, out var shortValue))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.Short(field.Name, checked((short)shortValue));
                return true;
            case Ps3SourceGameEventFieldType.Byte:
                if (!reader.TryReadUBitLong(8, out var byteValue))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.Byte(field.Name, checked((byte)byteValue));
                return true;
            case Ps3SourceGameEventFieldType.Bool:
                if (!reader.TryReadOneBit(out var boolValue))
                {
                    return false;
                }

                value = Ps3SourceGameEventFieldValue.Bool(field.Name, boolValue);
                return true;
            default:
                return false;
        }
    }

    private static void WriteSoundInfoDelta(Ps3SourceNetBitWriter writer, Ps3SourceSoundInfo sound, Ps3SourceSoundInfo delta)
    {
        ValidateSoundInfo(sound);

        if (sound.EntityIndex == delta.EntityIndex)
        {
            writer.WriteOneBit(false);
        }
        else
        {
            writer.WriteOneBit(true);
            if (sound.EntityIndex <= 31)
            {
                if (sound.EntityIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(sound), "Sound entity index cannot be negative.");
                }

                writer.WriteOneBit(true);
                writer.WriteUBitLong(checked((uint)sound.EntityIndex), 5);
            }
            else
            {
                writer.WriteOneBit(false);
                writer.WriteUBitLong(checked((uint)sound.EntityIndex), Ps3SourceNetMessageConstants.MaxEdictBits);
            }
        }

        WriteDeltaUInt(writer, sound.SoundNumber, delta.SoundNumber, Ps3SourceNetMessageConstants.MaxSoundIndexBits);
        WriteDeltaUInt(writer, sound.Flags, delta.Flags, Ps3SourceNetMessageConstants.SoundFlagBitsEncode);
        WriteDeltaUInt(writer, sound.Channel, delta.Channel, 3);
        writer.WriteOneBit(sound.IsAmbient);
        writer.WriteOneBit(sound.IsSentence);

        if (sound.Flags == Ps3SourceNetMessageConstants.SoundStopFlag)
        {
            return;
        }

        if (sound.SequenceNumber == delta.SequenceNumber)
        {
            writer.WriteOneBit(true);
        }
        else if (sound.SequenceNumber == delta.SequenceNumber + 1)
        {
            writer.WriteOneBit(false);
            writer.WriteOneBit(true);
        }
        else
        {
            writer.WriteUBitLong(0, 2);
            writer.WriteUBitLong(checked((uint)sound.SequenceNumber), Ps3SourceNetMessageConstants.SoundSequenceNumberBits);
        }

        if (sound.Volume == delta.Volume)
        {
            writer.WriteOneBit(false);
        }
        else
        {
            writer.WriteOneBit(true);
            writer.WriteUBitLong(checked((uint)(int)(sound.Volume * 127.0f)), 7);
        }

        WriteDeltaUInt(writer, sound.SoundLevel, delta.SoundLevel, Ps3SourceNetMessageConstants.MaxSoundLevelBits);
        WriteDeltaUInt(writer, sound.Pitch, delta.Pitch, 8);
        WriteDeltaUInt(writer, sound.SpecialDsp, delta.SpecialDsp, 8);

        if (sound.Delay == delta.Delay)
        {
            writer.WriteOneBit(false);
        }
        else
        {
            writer.WriteOneBit(true);
            var encodedDelay = (int)((sound.Delay + Ps3SourceNetMessageConstants.SoundDelayOffset) * 1000.0f);
            var maxDelay = (1 << (Ps3SourceNetMessageConstants.SoundDelayMsecEncodeBits - 1)) - 1;
            var minDelay = -10 * (1 << (Ps3SourceNetMessageConstants.SoundDelayMsecEncodeBits - 1));
            encodedDelay = Math.Clamp(encodedDelay, minDelay, maxDelay);
            if (encodedDelay < 0)
            {
                encodedDelay /= 10;
            }

            writer.WriteSBitLong(encodedDelay, Ps3SourceNetMessageConstants.SoundDelayMsecEncodeBits);
        }

        WriteDeltaScaledCoord(writer, sound.Origin.X, delta.Origin.X);
        WriteDeltaScaledCoord(writer, sound.Origin.Y, delta.Origin.Y);
        WriteDeltaScaledCoord(writer, sound.Origin.Z, delta.Origin.Z);
        WriteDeltaSInt(writer, sound.SpeakerEntity, delta.SpeakerEntity, Ps3SourceNetMessageConstants.MaxEdictBits + 1);
    }

    private static bool TryReadSoundInfoDelta(
        ref Ps3SourceNetBitReader reader,
        Ps3SourceSoundInfo delta,
        out Ps3SourceSoundInfo sound)
    {
        sound = default!;

        if (!reader.TryReadOneBit(out var entityChanged))
        {
            return false;
        }

        int entityIndex;
        if (!entityChanged)
        {
            entityIndex = delta.EntityIndex;
        }
        else
        {
            if (!reader.TryReadOneBit(out var smallEntity))
            {
                return false;
            }

            if (smallEntity)
            {
                if (!reader.TryReadUBitLong(5, out var rawEntity))
                {
                    return false;
                }

                entityIndex = checked((int)rawEntity);
            }
            else
            {
                if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.MaxEdictBits, out var rawEntity))
                {
                    return false;
                }

                entityIndex = checked((int)rawEntity);
            }
        }

        if (!TryReadDeltaUInt(ref reader, delta.SoundNumber, Ps3SourceNetMessageConstants.MaxSoundIndexBits, out var soundNumber)
            || !TryReadDeltaUInt(ref reader, delta.Flags, Ps3SourceNetMessageConstants.SoundFlagBitsEncode, out var flags)
            || !TryReadDeltaUInt(ref reader, delta.Channel, 3, out var channel)
            || !reader.TryReadOneBit(out var isAmbient)
            || !reader.TryReadOneBit(out var isSentence))
        {
            return false;
        }

        sound = delta with
        {
            EntityIndex = entityIndex,
            SoundNumber = soundNumber,
            Flags = flags,
            Channel = channel,
            IsAmbient = isAmbient,
            IsSentence = isSentence,
        };

        if (flags == Ps3SourceNetMessageConstants.SoundStopFlag)
        {
            sound = sound.ClearStopFields();
            return true;
        }

        int sequenceNumber;
        if (!reader.TryReadOneBit(out var sameSequence))
        {
            return false;
        }

        if (sameSequence)
        {
            sequenceNumber = delta.SequenceNumber;
        }
        else
        {
            if (!reader.TryReadOneBit(out var incrementSequence))
            {
                return false;
            }

            if (incrementSequence)
            {
                sequenceNumber = delta.SequenceNumber + 1;
            }
            else
            {
                if (!reader.TryReadUBitLong(Ps3SourceNetMessageConstants.SoundSequenceNumberBits, out var rawSequence))
                {
                    return false;
                }

                sequenceNumber = checked((int)rawSequence);
            }
        }

        if (!reader.TryReadOneBit(out var volumeChanged))
        {
            return false;
        }

        float volume;
        if (volumeChanged)
        {
            if (!reader.TryReadUBitLong(7, out var rawVolume))
            {
                return false;
            }

            volume = rawVolume / 127.0f;
        }
        else
        {
            volume = delta.Volume;
        }

        if (!TryReadDeltaUInt(ref reader, delta.SoundLevel, Ps3SourceNetMessageConstants.MaxSoundLevelBits, out var soundLevel)
            || !TryReadDeltaUInt(ref reader, delta.Pitch, 8, out var pitch)
            || !TryReadDeltaUInt(ref reader, delta.SpecialDsp, 8, out var specialDsp)
            || !reader.TryReadOneBit(out var delayChanged))
        {
            return false;
        }

        float delay;
        if (delayChanged)
        {
            if (!reader.TryReadSBitLong(Ps3SourceNetMessageConstants.SoundDelayMsecEncodeBits, out var encodedDelay))
            {
                return false;
            }

            delay = encodedDelay / 1000.0f;
            if (delay < 0)
            {
                delay *= 10.0f;
            }

            delay -= Ps3SourceNetMessageConstants.SoundDelayOffset;
        }
        else
        {
            delay = delta.Delay;
        }

        if (!TryReadDeltaScaledCoord(ref reader, delta.Origin.X, out var x)
            || !TryReadDeltaScaledCoord(ref reader, delta.Origin.Y, out var y)
            || !TryReadDeltaScaledCoord(ref reader, delta.Origin.Z, out var z)
            || !TryReadDeltaSInt(ref reader, delta.SpeakerEntity, Ps3SourceNetMessageConstants.MaxEdictBits + 1, out var speakerEntity))
        {
            return false;
        }

        sound = sound with
        {
            SequenceNumber = sequenceNumber,
            Volume = volume,
            SoundLevel = soundLevel,
            Pitch = pitch,
            SpecialDsp = specialDsp,
            Delay = delay,
            Origin = new Ps3SourceVector(x, y, z),
            SpeakerEntity = speakerEntity,
        };
        return true;
    }

    private static void WriteDeltaUInt(Ps3SourceNetBitWriter writer, int value, int deltaValue, int bitCount)
    {
        if (value == deltaValue)
        {
            writer.WriteOneBit(false);
            return;
        }

        writer.WriteOneBit(true);
        writer.WriteUBitLong(checked((uint)value), bitCount);
    }

    private static void WriteDeltaSInt(Ps3SourceNetBitWriter writer, int value, int deltaValue, int bitCount)
    {
        if (value == deltaValue)
        {
            writer.WriteOneBit(false);
            return;
        }

        writer.WriteOneBit(true);
        writer.WriteSBitLong(value, bitCount);
    }

    private static void WriteDeltaScaledCoord(Ps3SourceNetBitWriter writer, float value, float deltaValue)
    {
        if (value == deltaValue)
        {
            writer.WriteOneBit(false);
            return;
        }

        writer.WriteOneBit(true);
        writer.WriteSBitLong((int)(value / 8.0f), Ps3SourceNetMessageConstants.SoundOriginIntegerBits);
    }

    private static bool TryReadDeltaUInt(
        ref Ps3SourceNetBitReader reader,
        int deltaValue,
        int bitCount,
        out int value)
    {
        value = deltaValue;
        if (!reader.TryReadOneBit(out var changed))
        {
            return false;
        }

        if (!changed)
        {
            return true;
        }

        if (!reader.TryReadUBitLong(bitCount, out var rawValue))
        {
            return false;
        }

        value = checked((int)rawValue);
        return true;
    }

    private static bool TryReadDeltaSInt(
        ref Ps3SourceNetBitReader reader,
        int deltaValue,
        int bitCount,
        out int value)
    {
        value = deltaValue;
        if (!reader.TryReadOneBit(out var changed))
        {
            return false;
        }

        if (!changed)
        {
            return true;
        }

        return reader.TryReadSBitLong(bitCount, out value);
    }

    private static bool TryReadDeltaScaledCoord(
        ref Ps3SourceNetBitReader reader,
        float deltaValue,
        out float value)
    {
        value = deltaValue;
        if (!reader.TryReadOneBit(out var changed))
        {
            return false;
        }

        if (!changed)
        {
            return true;
        }

        if (!reader.TryReadSBitLong(Ps3SourceNetMessageConstants.SoundOriginIntegerBits, out var scaled))
        {
            return false;
        }

        value = 8.0f * scaled;
        return true;
    }

    private static void ValidateSoundInfo(Ps3SourceSoundInfo sound)
    {
        if (sound.EntityIndex < 0 || sound.EntityIndex >= (1 << Ps3SourceNetMessageConstants.MaxEdictBits))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound entity index does not fit in MAX_EDICT_BITS.");
        }

        if (sound.SoundNumber < 0 || sound.SoundNumber >= (1 << Ps3SourceNetMessageConstants.MaxSoundIndexBits))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound number does not fit in PS3 MAX_SOUND_INDEX_BITS.");
        }

        if (sound.Flags < 0 || sound.Flags >= (1 << Ps3SourceNetMessageConstants.SoundFlagBitsEncode))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound flags do not fit in SND_FLAG_BITS_ENCODE.");
        }

        if (sound.Channel < 0 || sound.Channel >= (1 << 3))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound channel does not fit in 3 bits.");
        }

        if (sound.SequenceNumber < 0 || sound.SequenceNumber >= (1 << Ps3SourceNetMessageConstants.SoundSequenceNumberBits))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound sequence number does not fit in SOUND_SEQNUMBER_BITS.");
        }

        if (sound.Volume is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound volume must be in the Source packet range 0.0..1.0.");
        }

        if (sound.SoundLevel < 0 || sound.SoundLevel >= (1 << Ps3SourceNetMessageConstants.MaxSoundLevelBits))
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound level does not fit in MAX_SNDLVL_BITS.");
        }

        if (sound.Pitch < 0 || sound.Pitch > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound pitch does not fit in 8 bits.");
        }

        if (sound.SpecialDsp < 0 || sound.SpecialDsp > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound DSP index does not fit in 8 bits.");
        }
    }

    private static string JoinCommonFileCrcPrefix(string prefix, string tail)
    {
        if (tail.Length == 0)
        {
            return prefix;
        }

        return tail[0] is '/' or '\\'
            ? prefix + tail
            : prefix + "/" + tail;
    }
}

public sealed class Ps3SourceNetBitWriter
{
    private readonly List<byte> _bytes = [];
    private int _bitLength;

    public int BitLength => _bitLength;

    public void WriteOneBit(bool value)
    {
        WriteUBitLong(value ? 1u : 0u, 1);
    }

    public void WriteByte(byte value)
    {
        WriteUBitLong(value, 8);
    }

    public void WriteWord(ushort value)
    {
        WriteUBitLong(value, 16);
    }

    public void WriteShort(short value)
    {
        WriteUBitLong(unchecked((ushort)value), 16);
    }

    public void WriteLong(int value)
    {
        WriteUBitLong(unchecked((uint)value), 32);
    }

    public void WriteLong(uint value)
    {
        WriteUBitLong(value, 32);
    }

    public void WriteSBitLong(int value, int bitCount)
    {
        if (bitCount is <= 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        if (bitCount < 32)
        {
            var min = -(1 << (bitCount - 1));
            var max = (1 << (bitCount - 1)) - 1;
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} does not fit in signed {bitCount} bits.");
            }
        }

        var encoded = unchecked((uint)value);
        if (bitCount < 32)
        {
            encoded &= (1u << bitCount) - 1;
        }

        WriteUBitLong(encoded, bitCount);
    }

    public void WriteFloat(float value)
    {
        WriteUBitLong(BitConverter.SingleToUInt32Bits(value), 32);
    }

    public void WriteBitAngle(float value, int bitCount)
    {
        if (bitCount is <= 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        var scale = MathF.Pow(2.0f, bitCount) / 360.0f;
        var encoded = unchecked((uint)(int)(value * scale)) & (bitCount == 32 ? uint.MaxValue : ((1u << bitCount) - 1));
        WriteUBitLong(encoded, bitCount);
    }

    public void WriteBitCoord(float value)
    {
        var absValue = MathF.Abs(value);
        var intValue = (int)absValue;
        var fractValue = Math.Abs((int)(value * Ps3SourceNetBitCoordConstants.Denominator))
            & (Ps3SourceNetBitCoordConstants.Denominator - 1);

        WriteOneBit(intValue != 0);
        WriteOneBit(fractValue != 0);
        if (intValue == 0 && fractValue == 0)
        {
            return;
        }

        WriteOneBit(value <= -Ps3SourceNetBitCoordConstants.Resolution);
        if (intValue != 0)
        {
            WriteUBitLong(checked((uint)(intValue - 1)), Ps3SourceNetBitCoordConstants.IntegerBits);
        }

        if (fractValue != 0)
        {
            WriteUBitLong(checked((uint)fractValue), Ps3SourceNetBitCoordConstants.FractionalBits);
        }
    }

    public void WriteBitVec3Coord(Ps3SourceVector value)
    {
        var xFlag = MathF.Abs(value.X) >= Ps3SourceNetBitCoordConstants.Resolution;
        var yFlag = MathF.Abs(value.Y) >= Ps3SourceNetBitCoordConstants.Resolution;
        var zFlag = MathF.Abs(value.Z) >= Ps3SourceNetBitCoordConstants.Resolution;

        WriteOneBit(xFlag);
        WriteOneBit(yFlag);
        WriteOneBit(zFlag);
        if (xFlag)
        {
            WriteBitCoord(value.X);
        }

        if (yFlag)
        {
            WriteBitCoord(value.Y);
        }

        if (zFlag)
        {
            WriteBitCoord(value.Z);
        }
    }

    public void WriteString(string value)
    {
        foreach (var ch in Encoding.ASCII.GetBytes(value))
        {
            WriteByte(ch);
        }

        WriteByte(0);
    }

    public void WriteBytes(ReadOnlySpan<byte> values)
    {
        foreach (var value in values)
        {
            WriteByte(value);
        }
    }

    public void WriteBits(ReadOnlySpan<byte> values, int bitCount)
    {
        if (bitCount < 0 || bitCount > values.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        for (var i = 0; i < bitCount; i++)
        {
            WriteOneBit(((values[i >> 3] >> (i & 7)) & 1) != 0);
        }
    }

    public void WriteUBitLong(uint value, int bitCount)
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

    public byte[] ToArray()
    {
        return _bytes.ToArray();
    }

    public Ps3SourceNetMessageFrame ToFrame()
    {
        return new Ps3SourceNetMessageFrame(ToArray(), _bitLength);
    }
}

public ref struct Ps3SourceNetBitReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly int _bitCount;

    public Ps3SourceNetBitReader(ReadOnlySpan<byte> data, int bitCount)
    {
        if (bitCount < 0 || bitCount > data.Length * 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        _data = data;
        _bitCount = bitCount;
        ConsumedBits = 0;
    }

    public int ConsumedBits { get; private set; }

    public bool TryReadOneBit(out bool value)
    {
        if (!TryReadUBitLong(1, out var raw))
        {
            value = false;
            return false;
        }

        value = raw != 0;
        return true;
    }

    public bool TryReadLong(out uint value)
    {
        return TryReadUBitLong(32, out value);
    }

    public bool TryReadSBitLong(int bitCount, out int value)
    {
        value = 0;
        if (bitCount is <= 0 or > 32 || !TryReadUBitLong(bitCount, out var raw))
        {
            return false;
        }

        value = bitCount == 32
            ? unchecked((int)raw)
            : Ps3SourceNetMessages.SignExtend(raw, bitCount);
        return true;
    }

    public bool TryReadBitAngle(int bitCount, out float value)
    {
        value = 0;
        if (bitCount is <= 0 or > 32 || !TryReadUBitLong(bitCount, out var raw))
        {
            return false;
        }

        value = raw * (360.0f / MathF.Pow(2.0f, bitCount));
        return true;
    }

    public bool TryReadBitCoord(out float value)
    {
        value = 0;
        if (!TryReadOneBit(out var hasInteger)
            || !TryReadOneBit(out var hasFraction))
        {
            return false;
        }

        if (!hasInteger && !hasFraction)
        {
            return true;
        }

        if (!TryReadOneBit(out var negative))
        {
            return false;
        }

        uint integer = 0;
        uint fraction = 0;
        if (hasInteger
            && !TryReadUBitLong(Ps3SourceNetBitCoordConstants.IntegerBits, out integer))
        {
            return false;
        }

        if (hasFraction
            && !TryReadUBitLong(Ps3SourceNetBitCoordConstants.FractionalBits, out fraction))
        {
            return false;
        }

        value = (hasInteger ? integer + 1 : 0)
            + (fraction * Ps3SourceNetBitCoordConstants.Resolution);
        if (negative)
        {
            value = -value;
        }

        return true;
    }

    public bool TryReadBitVec3Coord(out Ps3SourceVector value)
    {
        value = new Ps3SourceVector(0, 0, 0);
        if (!TryReadOneBit(out var hasX)
            || !TryReadOneBit(out var hasY)
            || !TryReadOneBit(out var hasZ))
        {
            return false;
        }

        float x = 0;
        float y = 0;
        float z = 0;
        if (hasX && !TryReadBitCoord(out x))
        {
            return false;
        }

        if (hasY && !TryReadBitCoord(out y))
        {
            return false;
        }

        if (hasZ && !TryReadBitCoord(out z))
        {
            return false;
        }

        value = new Ps3SourceVector(x, y, z);
        return true;
    }

    public bool TryReadString(int maxBytes, out string value)
    {
        value = "";
        if (maxBytes <= 0)
        {
            return false;
        }

        var bytes = new List<byte>(maxBytes);
        for (var i = 0; i < maxBytes; i++)
        {
            if (!TryReadUBitLong(8, out var raw))
            {
                return false;
            }

            if (raw == 0)
            {
                value = Encoding.ASCII.GetString(bytes.ToArray());
                return true;
            }

            bytes.Add(checked((byte)raw));
        }

        value = Encoding.ASCII.GetString(bytes.ToArray());
        return true;
    }

    public bool TryReadBytes(int byteCount, out byte[] value)
    {
        value = [];
        if (byteCount < 0 || ConsumedBits + byteCount * 8 > _bitCount)
        {
            return false;
        }

        value = new byte[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            if (!TryReadUBitLong(8, out var raw))
            {
                return false;
            }

            value[i] = checked((byte)raw);
        }

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

    public bool TryReadUBitLong(int bitCount, out uint value)
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
}

internal static class Ps3SourceNetBitCoordConstants
{
    public const int IntegerBits = 14;
    public const int FractionalBits = 5;
    public const int Denominator = 1 << FractionalBits;
    public const float Resolution = 1.0f / Denominator;
}
