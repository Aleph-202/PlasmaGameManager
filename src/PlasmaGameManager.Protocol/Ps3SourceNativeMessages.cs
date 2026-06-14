using System.Text;
using System.Runtime.InteropServices;

namespace PlasmaGameManager.Protocol;

public sealed record Ps3SourcePlayerSummaryEntry(
    byte PlayerSlotIndex,
    string DisplayName,
    int ScoreOrStat,
    float FloatValue);

public sealed record Ps3SourceResourceStringEntry(
    string ResourceName,
    string Classification);

public sealed record Ps3SourceServerInfo(
    string ServerName,
    string MapName,
    string GameDirectory,
    string Description,
    short ListenPortOrNetworkShort,
    byte CurrentPlayers,
    byte MaxPlayers,
    byte BotOrReservedCount,
    byte ServerVariantCode,
    byte PlatformCode,
    byte PasswordOrPrivateFlag,
    byte ClientVisibleFlag,
    string ConnectionAddress);

public sealed record Ps3SourceDecodedPlayerSummary(
    byte SummaryHeaderValue,
    Ps3SourcePlayerSummaryEntry[] Entries,
    int ConsumedBits);

public sealed record Ps3SourceDecodedResourceStringTable(
    Ps3SourceResourceStringEntry[] Entries,
    int ConsumedBits);

public sealed record Ps3SourceDecodedServerInfo(
    Ps3SourceServerInfo Info,
    int ConsumedBits);

public sealed record Ps3SourceHudPlayerObjectUpdate(
    int PrimaryValue,
    int? SecondaryValue = null,
    string? Label = null);

public sealed record Ps3SourceDecodedHudPlayerObjectUpdate(
    Ps3SourceHudPlayerObjectUpdate Update,
    int ConsumedBits);

public sealed record Ps3SourceGameplayStatTimesUsed(
    int VersionOrKind,
    int State,
    int Value,
    string ObjectName,
    string Classification,
    string? ExtraName = null);

public sealed record Ps3SourceDecodedGameplayStatTimesUsed(
    Ps3SourceGameplayStatTimesUsed Update,
    int ConsumedBits);

public sealed record Ps3SourcePlayerResourceDelta(
    byte PlayerSlotIndex,
    ushort Health,
    byte Rating,
    sbyte RatingDelta,
    bool Connected,
    uint ObjectId,
    string StatusText,
    uint Ping = 0,
    uint Score = 0,
    uint Deaths = 0,
    uint Team = 0,
    bool Alive = true);

public sealed record Ps3SourcePlayerEntityDelta(
    byte PlayerSlotIndex,
    uint ObjectId,
    byte TeamNumber,
    ushort Health,
    byte LifeState,
    uint Flags,
    byte ClassNumber,
    float OriginX,
    float OriginY,
    float OriginZ,
    float RotationPitch,
    float RotationYaw,
    float RotationRoll,
    float EyePitch,
    float EyeYaw,
    float SimulationTime = 0,
    uint ModelIndex = 0,
    uint Effects = 0,
    byte RenderMode = 0,
    byte RenderFx = 0,
    uint RenderColor = 0xffffffff,
    uint CollisionGroup = 0,
    float Elasticity = 0,
    float ShadowCastDistance = 0,
    uint OwnerEntityHandle = 0,
    uint EffectEntityHandle = 0,
    uint MoveParent = 0,
    byte ParentAttachment = 0,
    byte MoveType = 2,
    byte MoveCollide = 0,
    uint TextureFrameIndex = 0,
    float CollisionMinsX = 0,
    float CollisionMinsY = 0,
    float CollisionMinsZ = 0,
    float CollisionMaxsX = 0,
    float CollisionMaxsY = 0,
    float CollisionMaxsZ = 0,
    byte SolidType = 0,
    ushort SolidFlags = 0,
    byte SurroundType = 0,
    byte TriggerBloat = 0,
    float SpecifiedSurroundingMinsX = 0,
    float SpecifiedSurroundingMinsY = 0,
    float SpecifiedSurroundingMinsZ = 0,
    float SpecifiedSurroundingMaxsX = 0,
    float SpecifiedSurroundingMaxsY = 0,
    float SpecifiedSurroundingMaxsZ = 0,
    uint PredictableId = 0,
    byte IsPlayerSimulated = 0,
    byte SimulatedEveryTick = 1,
    byte AnimatedEveryTick = 1,
    byte AlternateSorting = 0,
    uint Sequence = 0,
    uint ForceBone = 0,
    float ForceX = 0,
    float ForceY = 0,
    float ForceZ = 0,
    uint Skin = 0,
    uint Body = 0,
    uint HitboxSet = 0,
    float ModelWidthScale = 1,
    float[]? PoseParameters = null,
    float PlaybackRate = 1,
    float[]? EncodedControllers = null,
    byte ClientSideAnimation = 0,
    byte ClientSideFrameReset = 0,
    uint NewSequenceParity = 0,
    uint ResetEventsParity = 0,
    byte MuzzleFlashParity = 0,
    uint LightingOriginHandle = 0,
    uint LightingOriginRelativeHandle = 0,
    float ServerAnimationCycle = 0,
    float FadeMinDistance = 0,
    float FadeMaxDistance = 0,
    float FadeScale = 1);

public sealed record Ps3SourceGameplayStateDelta(
    byte PlayerSlotIndex,
    uint ObjectId,
    uint[] Ammo,
    uint Fov,
    uint FovStart,
    float FovTime,
    uint DefaultFov,
    uint ObserverMode,
    uint ObserverTargetHandle,
    uint[] ViewModelHandles,
    float ViewOffsetX,
    float ViewOffsetY,
    float ViewOffsetZ,
    float Friction,
    uint TickBase,
    uint NextThinkTick,
    uint GroundEntityHandle,
    ushort MaxHealth,
    byte PlayerClass,
    uint RoundState,
    uint WinningTeam,
    bool InSetup,
    bool InOvertime,
    uint GameType,
    uint[] WeaponHandles,
    uint ActiveWeaponHandle,
    uint LastWeaponHandle,
    uint ObjectUpgradeLevel,
    uint ObjectState,
    uint ObjectAmmoShells,
    uint ObjectAmmoRockets,
    uint ObjectUpgradeMetal,
    uint TeleporterState,
    float TeleporterRechargeTime,
    uint TeleporterTimesUsed,
    float TeleporterYawToExit,
    uint ZoomOwnerHandle = 0,
    uint VehicleHandle = 0,
    uint UseEntityHandle = 0,
    float MaxSpeed = 0,
    string LastPlaceName = "",
    byte NoInterpParity = 0,
    byte OnTarget = 0,
    float VelocityX = 0,
    float VelocityY = 0,
    float VelocityZ = 0,
    float BaseVelocityX = 0,
    float BaseVelocityY = 0,
    float BaseVelocityZ = 0,
    uint ConstraintEntityHandle = 0,
    float ConstraintCenterX = 0,
    float ConstraintCenterY = 0,
    float ConstraintCenterZ = 0,
    float ConstraintRadius = 0,
    float ConstraintWidth = 0,
    float ConstraintSpeedFactor = 0,
    float DeathTime = 0,
    byte WaterLevel = 0,
    float LaggedMovementValue = 1,
    byte Ducked = 0,
    byte Ducking = 0,
    byte InDuckJump = 0,
    float DuckTime = 0,
    float DuckJumpTime = 0,
    float JumpTime = 0,
    float FallVelocity = 0,
    float PunchAngleX = 0,
    float PunchAngleY = 0,
    float PunchAngleZ = 0,
    float PunchAngleVelocityX = 0,
    float PunchAngleVelocityY = 0,
    float PunchAngleVelocityZ = 0,
    byte DrawViewModel = 1,
    byte WearingSuit = 0,
    byte Poisoned = 0,
    float StepSize = 18,
    byte AllowAutoMovement = 1,
    byte SaveMeParity = 0,
    uint RagdollHandle = 0,
    uint ItemHandle = 0,
    uint SpawnCounter = 0,
    float NextAttack = 0,
    uint TotalScore = 0,
    uint Deaths = 0,
    uint Captures = 0,
    uint Defenses = 0,
    uint Dominations = 0,
    uint Revenge = 0,
    uint BuildingsDestroyed = 0,
    uint Headshots = 0,
    uint Backstabs = 0,
    uint HealPoints = 0,
    uint Invulns = 0,
    uint Teleports = 0,
    uint ResupplyPoints = 0,
    uint KillAssists = 0,
    byte InWaitingForPlayers = 0,
    byte SwitchedTeamsThisRound = 0,
    byte AwaitingReadyRestart = 0,
    float RestartRoundTime = 0,
    float MapResetTime = 0,
    float[]? NextRespawnWave = null,
    float[]? TeamRespawnWaveTimes = null,
    string TeamGoalStringRed = "",
    string TeamGoalStringBlue = "",
    uint SourceTeamNumber = 0,
    uint SourceTeamScore = 0,
    uint SourceTeamRoundsWon = 0,
    string SourceTeamName = "",
    uint TeamFlagCaptures = 0,
    uint TeamRole = 0,
    uint PlayerCondition = 0,
    byte Jumping = 0,
    uint PlayerState = 0,
    uint DesiredPlayerClass = 0,
    uint DisguiseTeam = 0,
    uint DisguiseClass = 0,
    uint DisguiseTargetIndex = 0,
    uint DisguiseHealth = 0,
    uint DesiredDisguiseTeam = 0,
    uint DesiredDisguiseClass = 0,
    float CloakMeter = 100,
    float TfLocalOriginX = 0,
    float TfLocalOriginY = 0,
    float TfLocalOriginZ = 0,
    uint PlayerObjectArrayElement = 0,
    float TfNonLocalOriginX = 0,
    float TfNonLocalOriginY = 0,
    float TfNonLocalOriginZ = 0);

public sealed record Ps3SourceWeaponEntityDelta(
    uint ObjectId,
    uint OwnerHandle,
    uint State,
    uint ViewModelIndex,
    uint WorldModelIndex,
    float NextPrimaryAttack,
    float NextSecondaryAttack,
    float TimeWeaponIdle,
    uint PrimaryAmmoType,
    uint SecondaryAmmoType,
    uint Clip1,
    uint Clip2,
    byte Lowered = 0,
    uint ReloadMode = 0,
    byte ResetParity = 0,
    byte ReloadedThroughAnimEvent = 0,
    byte InReload = 0,
    byte FireOnEmpty = 0,
    float NextEmptySoundTime = 0,
    uint BuildState = 0,
    uint BuildObjectType = 0,
    uint ObjectBeingBuiltHandle = 0,
    uint TfWeaponState = 0,
    byte CritFire = 0,
    byte Healing = 0,
    byte Attacking = 0,
    byte ChargeRelease = 0,
    byte Holstered = 0,
    uint HealingTargetHandle = 0,
    float HealEffectLifetime = 0,
    float ChargeLevel = 0,
    byte BottleBroken = 0,
    uint PipebombCount = 0,
    float ChargeBeginTime = 0,
    float SoonestPrimaryAttack = 0,
    byte MinigunCritShot = 0);

public sealed record Ps3SourceTeamRoundTimerDelta(
    uint ObjectId,
    bool TimerPaused,
    float TimeRemaining,
    float TimerEndTime,
    uint TimerMaxLength,
    bool IsDisabled,
    bool ShowInHud,
    uint TimerLength,
    uint TimerInitialLength,
    bool AutoCountdown,
    uint SetupTimeLength,
    uint State,
    bool StartPaused);

public sealed record Ps3SourceObjectiveResourceDelta(
    uint ObjectId,
    uint TimerToShowInHud,
    uint NumControlPoints,
    bool PlayingMiniRounds,
    bool ControlPointsReset,
    uint UpdateCapHudParity,
    IReadOnlyList<float> CpPositions,
    IReadOnlyList<byte> CpIsVisible,
    IReadOnlyList<float> LazyCapPercentages,
    IReadOnlyList<uint> TeamIcons,
    IReadOnlyList<uint> TeamOverlays,
    IReadOnlyList<uint> TeamRequiredCappers,
    IReadOnlyList<float> TeamCapTimes,
    IReadOnlyList<uint> PreviousPoints,
    IReadOnlyList<byte> TeamCanCap,
    IReadOnlyList<uint> TeamBaseIcons,
    IReadOnlyList<uint> BaseControlPoints,
    IReadOnlyList<byte> InMiniRound,
    IReadOnlyList<byte> WarnOnCap,
    IReadOnlyList<uint> NumTeamMembers,
    IReadOnlyList<uint> CappingTeam,
    IReadOnlyList<uint> TeamInZone,
    IReadOnlyList<byte> Blocked,
    IReadOnlyList<uint> Owner,
    string CapLayoutInHud);

public sealed record Ps3SourceFireBulletsEvent(
    uint ObjectId,
    float OriginX,
    float OriginY,
    float OriginZ,
    float AnglePitch,
    float AngleYaw,
    uint WeaponId,
    uint Mode,
    uint Seed,
    uint PlayerIndex,
    float Spread);

public sealed record Ps3SourceTfRagdollDelta(
    uint ObjectId,
    float OriginX,
    float OriginY,
    float OriginZ,
    uint PlayerIndex,
    float ForceX,
    float ForceY,
    float ForceZ,
    float VelocityX,
    float VelocityY,
    float VelocityZ,
    uint ForceBone,
    bool Gib,
    bool Burning,
    uint Team,
    uint Class);

public sealed record Ps3SourceTfExplosionEvent(
    uint ObjectId,
    float OriginX,
    float OriginY,
    float OriginZ,
    float NormalX,
    float NormalY,
    float NormalZ,
    uint WeaponId,
    uint EntityIndex);

public sealed record Ps3SourcePlayerAnimEvent(
    uint ObjectId,
    uint PlayerIndex,
    uint EventId,
    uint Data);

public static class Ps3SourceNativeMessages
{
    public static bool TryDecodePlayerSummary(
        ReadOnlySpan<byte> payload,
        out Ps3SourceDecodedPlayerSummary summary,
        int? bitCount = null)
    {
        summary = new Ps3SourceDecodedPlayerSummary(0, [], 0);
        var reader = new NativeBitReader(payload, bitCount ?? payload.Length * 8);
        if (!reader.TryReadSigned32(out var sentinel)
            || sentinel != -1
            || !reader.TryReadByte(out var messageId)
            || messageId != 0x44
            || !reader.TryReadByte(out var header))
        {
            return false;
        }

        var entries = new List<Ps3SourcePlayerSummaryEntry>(header);
        for (var i = 0; i < header; i++)
        {
            if (!reader.TryReadByte(out var playerSlotIndex)
                || !reader.TryReadStringZ(256, out var displayName)
                || !reader.TryReadSigned32(out var scoreOrStat)
                || !reader.TryReadFloat(out var floatValue))
            {
                return false;
            }

            entries.Add(new Ps3SourcePlayerSummaryEntry(playerSlotIndex, displayName, scoreOrStat, floatValue));
        }

        summary = new Ps3SourceDecodedPlayerSummary(header, entries.ToArray(), reader.ConsumedBits);
        return true;
    }

    public static bool TryDecodeResourceStringTable(
        ReadOnlySpan<byte> payload,
        out Ps3SourceDecodedResourceStringTable table,
        int? bitCount = null)
    {
        table = new Ps3SourceDecodedResourceStringTable([], 0);
        var reader = new NativeBitReader(payload, bitCount ?? payload.Length * 8);
        if (!reader.TryReadSigned32(out var sentinel)
            || sentinel != -1
            || !reader.TryReadByte(out var messageId)
            || messageId != 0x45
            || !reader.TryReadSigned16(out var count)
            || count < 0)
        {
            return false;
        }

        var entries = new List<Ps3SourceResourceStringEntry>(count);
        for (var i = 0; i < count; i++)
        {
            if (!reader.TryReadStringZ(260, out var resourceName)
                || !reader.TryReadStringZ(260, out var classification))
            {
                return false;
            }

            entries.Add(new Ps3SourceResourceStringEntry(resourceName, classification));
        }

        table = new Ps3SourceDecodedResourceStringTable(entries.ToArray(), reader.ConsumedBits);
        return true;
    }

    public static bool TryDecodeServerInfo(
        ReadOnlySpan<byte> payload,
        out Ps3SourceDecodedServerInfo serverInfo,
        int? bitCount = null)
    {
        serverInfo = new Ps3SourceDecodedServerInfo(
            new Ps3SourceServerInfo("", "", "", "", 0, 0, 0, 0, 0, 0, 0, 0, ""),
            0);
        var reader = new NativeBitReader(payload, bitCount ?? payload.Length * 8);
        if (!reader.TryReadSigned32(out var sentinel)
            || sentinel != -1
            || !reader.TryReadByte(out var messageId)
            || messageId != 0x49
            || !reader.TryReadByte(out var version)
            || version != 8
            || !reader.TryReadStringZ(260, out var serverName)
            || !reader.TryReadStringZ(260, out var mapName)
            || !reader.TryReadStringZ(260, out var gameDirectory)
            || !reader.TryReadStringZ(260, out var description)
            || !reader.TryReadSigned16(out var listenPortOrNetworkShort)
            || !reader.TryReadByte(out var currentPlayers)
            || !reader.TryReadByte(out var maxPlayers)
            || !reader.TryReadByte(out var botOrReservedCount)
            || !reader.TryReadByte(out var serverVariantCode)
            || !reader.TryReadByte(out var platformCode)
            || !reader.TryReadByte(out var passwordOrPrivateFlag)
            || !reader.TryReadByte(out var clientVisibleFlag)
            || !reader.TryReadStringZ(64, out var connectionAddress))
        {
            return false;
        }

        serverInfo = new Ps3SourceDecodedServerInfo(
            new Ps3SourceServerInfo(
                serverName,
                mapName,
                gameDirectory,
                description,
                listenPortOrNetworkShort,
                currentPlayers,
                maxPlayers,
                botOrReservedCount,
                serverVariantCode,
                platformCode,
                passwordOrPrivateFlag,
                clientVisibleFlag,
                connectionAddress),
            reader.ConsumedBits);
        return true;
    }

    public static bool TryDecodeHudPlayerObjectUpdate(
        ReadOnlySpan<byte> payload,
        out Ps3SourceDecodedHudPlayerObjectUpdate update,
        int? bitCount = null)
    {
        update = new Ps3SourceDecodedHudPlayerObjectUpdate(new Ps3SourceHudPlayerObjectUpdate(0), 0);
        var reader = new NativeBitReader(payload, bitCount ?? payload.Length * 8);
        if (!reader.TryReadSigned32(out var sentinel)
            || sentinel != -1
            || !reader.TryReadByte(out var messageId)
            || messageId != 0x41
            || !reader.TryReadSigned32(out var primaryValue))
        {
            return false;
        }

        int? secondaryValue = null;
        string? label = null;
        if (reader.RemainingBits > 0)
        {
            if (!reader.TryReadSigned32(out var secondary))
            {
                return false;
            }

            secondaryValue = secondary;
        }

        if (reader.RemainingBits > 0)
        {
            if (!reader.TryReadStringZ(260, out var decodedLabel))
            {
                return false;
            }

            label = decodedLabel;
        }

        update = new Ps3SourceDecodedHudPlayerObjectUpdate(
            new Ps3SourceHudPlayerObjectUpdate(primaryValue, secondaryValue, label),
            reader.ConsumedBits);
        return true;
    }

    public static bool TryDecodeGameplayStatTimesUsed(
        ReadOnlySpan<byte> payload,
        out Ps3SourceDecodedGameplayStatTimesUsed stat,
        int? bitCount = null)
    {
        stat = new Ps3SourceDecodedGameplayStatTimesUsed(
            new Ps3SourceGameplayStatTimesUsed(0, 0, 0, "", ""),
            0);
        var reader = new NativeBitReader(payload, bitCount ?? payload.Length * 8);
        if (!reader.TryReadSigned32(out var sentinel)
            || sentinel != -1
            || !reader.TryReadByte(out var messageId)
            || messageId != 0x6b
            || !reader.TryReadSigned32(out var versionOrKind)
            || !reader.TryReadSigned32(out var state)
            || !reader.TryReadSigned32(out var value)
            || !reader.TryReadStringZ(260, out var objectName)
            || !reader.TryReadStringZ(260, out var classification))
        {
            return false;
        }

        string? extraName = null;
        if (state == 2 && reader.RemainingBits > 0)
        {
            if (!reader.TryReadStringZ(260, out var decodedExtraName))
            {
                return false;
            }

            extraName = decodedExtraName;
        }

        stat = new Ps3SourceDecodedGameplayStatTimesUsed(
            new Ps3SourceGameplayStatTimesUsed(
                versionOrKind,
                state,
                value,
                objectName,
                classification,
                extraName),
            reader.ConsumedBits);
        return true;
    }

    public static byte[] BuildPlayerSummary(byte summaryHeaderValue, IReadOnlyList<Ps3SourcePlayerSummaryEntry> entries)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteByte(0x44);
        writer.WriteByte(summaryHeaderValue);
        foreach (var entry in entries)
        {
            writer.WriteByte(entry.PlayerSlotIndex);
            writer.WriteStringZ(entry.DisplayName);
            writer.WriteSigned32(entry.ScoreOrStat);
            writer.WriteFloat(entry.FloatValue);
        }

        return writer.ToArray();
    }

    public static byte[] BuildResourceStringTable(IReadOnlyList<Ps3SourceResourceStringEntry> entries)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteByte(0x45);
        writer.WriteSigned16((short)Math.Clamp(entries.Count, short.MinValue, short.MaxValue));
        foreach (var entry in entries)
        {
            writer.WriteStringZ(entry.ResourceName);
            writer.WriteStringZ(entry.Classification);
        }

        return writer.ToArray();
    }

    public static byte[] BuildServerInfo(Ps3SourceServerInfo info)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteByte(0x49);
        writer.WriteByte(8);
        writer.WriteStringZ(info.ServerName);
        writer.WriteStringZ(info.MapName);
        writer.WriteStringZ(info.GameDirectory);
        writer.WriteStringZ(info.Description);
        writer.WriteSigned16(info.ListenPortOrNetworkShort);
        writer.WriteByte(info.CurrentPlayers);
        writer.WriteByte(info.MaxPlayers);
        writer.WriteByte(info.BotOrReservedCount);
        writer.WriteByte(info.ServerVariantCode);
        writer.WriteByte(info.PlatformCode);
        writer.WriteByte(info.PasswordOrPrivateFlag);
        writer.WriteByte(info.ClientVisibleFlag);
        writer.WriteStringZ(info.ConnectionAddress);
        return writer.ToArray();
    }

    public static byte[] BuildHudPlayerObjectUpdate(Ps3SourceHudPlayerObjectUpdate update)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteByte(0x41);
        writer.WriteSigned32(update.PrimaryValue);
        if (update.SecondaryValue is { } secondaryValue)
        {
            writer.WriteSigned32(secondaryValue);
        }

        if (update.Label is { } label)
        {
            writer.WriteStringZ(label);
        }

        return writer.ToArray();
    }

    public static byte[] BuildGameplayStatTimesUsed(Ps3SourceGameplayStatTimesUsed update)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteByte(0x6b);
        writer.WriteSigned32(update.VersionOrKind);
        writer.WriteSigned32(update.State);
        writer.WriteSigned32(update.Value);
        writer.WriteStringZ(update.ObjectName);
        writer.WriteStringZ(update.Classification);
        if (update.State == 2 && update.ExtraName is { } extraName)
        {
            writer.WriteStringZ(extraName);
        }

        return writer.ToArray();
    }

    public static byte[] BuildPlayerResourceDelta(Ps3SourcePlayerResourceDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'R',
            (byte)'D',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_PlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iPing", delta.Ping);
        WritePropertyNameValue(semanticPayload, "m_iScore", delta.Score);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WritePropertyNameValue(semanticPayload, "m_bConnected", delta.Connected ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_iTeam", delta.Team);
        WritePropertyNameValue(semanticPayload, "m_bAlive", delta.Alive ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_iPlayerRating", delta.Rating);
        WritePropertyNameValue(semanticPayload, "m_iRatingDelta", unchecked((byte)delta.RatingDelta));
        WriteStringZ(semanticPayload, delta.StatusText);
        semanticPayload.Add(0);

        var descriptor = Ps3SourceEntityDeltaFrameBuilder.BuildQueuedBitstreamDescriptor(
            CollectionsMarshal.AsSpan(semanticPayload),
            semanticPayload.Count * 8,
            (int)(delta.ObjectId & 0x03ffffff));

        var groups = new Ps3SourceEntityDeltaGroupState[]
        {
            new Ps3SourceEntityDeltaGroupState(
                GroupIndex: 3,
                Ps3SourceEntityDeltaGroupWriteState.Inactive,
                StartIndex: 0,
                EntityCount: 0,
                ObjectId: delta.ObjectId,
                ObjectName: "CPlayerResource",
                Descriptor: null,
                LastWrittenFrameIndex: 0)
                .MarkActive(descriptor, 0, NativePartialChunkCount(descriptor))
        };

        return Ps3SourceEntityDeltaFrameBuilder.EncodeActiveGroupsNativePartialWindows(groups, frameIndex: 0).Payload;
    }

    public static byte[] BuildTinyPlayerResourceDelta(Ps3SourcePlayerResourceDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'R',
            (byte)'T',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_PlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iScore", delta.Score);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WritePropertyNameValue(semanticPayload, "m_iTeam", delta.Team);
        WritePropertyNameValue(semanticPayload, "m_bAlive", delta.Alive ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(3, delta.PlayerSlotIndex, 1, delta.ObjectId, "CPlayerResource", semanticPayload);
    }

    public static byte[] BuildMicroPlayerResourceDelta(Ps3SourcePlayerResourceDelta delta)
    {
        var semanticPayload = new List<byte>();
        WriteStringZ(semanticPayload, "DT_PlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iScore", delta.Score);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WritePropertyNameValue(semanticPayload, "m_iTeam", delta.Team);
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_bAlive", delta.Alive ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_lifeState", delta.Alive ? (byte)0 : (byte)1);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.Alive ? (uint)0 : 1);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(3, delta.PlayerSlotIndex, 1, delta.ObjectId, "CPlayerResource", semanticPayload);
    }

    public static byte[] BuildPlayerEntityDelta(Ps3SourcePlayerEntityDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'E',
            (byte)'D',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_BaseEntity");
        WriteFloatProperty(semanticPayload, "m_flSimulationTime", delta.SimulationTime);
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WriteVector3Property(semanticPayload, "m_angRotation", delta.RotationPitch, delta.RotationYaw, delta.RotationRoll);
        WritePropertyNameValue(semanticPayload, "m_nModelIndex", delta.ModelIndex);
        WritePropertyNameValue(semanticPayload, "m_fEffects", delta.Effects);
        WritePropertyNameValue(semanticPayload, "m_nRenderMode", delta.RenderMode);
        WritePropertyNameValue(semanticPayload, "m_nRenderFX", delta.RenderFx);
        WritePropertyNameValue(semanticPayload, "m_clrRender", delta.RenderColor);
        WritePropertyNameValue(semanticPayload, "m_iTeamNum", delta.TeamNumber);
        WritePropertyNameValue(semanticPayload, "m_CollisionGroup", delta.CollisionGroup);
        WriteFloatProperty(semanticPayload, "m_flElasticity", delta.Elasticity);
        WriteFloatProperty(semanticPayload, "m_flShadowCastDistance", delta.ShadowCastDistance);
        WritePropertyNameValue(semanticPayload, "m_hOwnerEntity", delta.OwnerEntityHandle);
        WritePropertyNameValue(semanticPayload, "m_hEffectEntity", delta.EffectEntityHandle);
        WritePropertyNameValue(semanticPayload, "moveparent", delta.MoveParent);
        WritePropertyNameValue(semanticPayload, "m_iParentAttachment", delta.ParentAttachment);
        WritePropertyNameValue(semanticPayload, "movetype", delta.MoveType);
        WritePropertyNameValue(semanticPayload, "movecollide", delta.MoveCollide);
        WriteStringZ(semanticPayload, "m_Collision");
        WriteVector3Property(semanticPayload, "m_vecMins", delta.CollisionMinsX, delta.CollisionMinsY, delta.CollisionMinsZ);
        WriteVector3Property(semanticPayload, "m_vecMaxs", delta.CollisionMaxsX, delta.CollisionMaxsY, delta.CollisionMaxsZ);
        WritePropertyNameValue(semanticPayload, "m_nSolidType", delta.SolidType);
        WritePropertyNameValue(semanticPayload, "m_usSolidFlags", delta.SolidFlags);
        WritePropertyNameValue(semanticPayload, "m_nSurroundType", delta.SurroundType);
        WritePropertyNameValue(semanticPayload, "m_triggerBloat", delta.TriggerBloat);
        WriteVector3Property(semanticPayload, "m_vecSpecifiedSurroundingMins", delta.SpecifiedSurroundingMinsX, delta.SpecifiedSurroundingMinsY, delta.SpecifiedSurroundingMinsZ);
        WriteVector3Property(semanticPayload, "m_vecSpecifiedSurroundingMaxs", delta.SpecifiedSurroundingMaxsX, delta.SpecifiedSurroundingMaxsY, delta.SpecifiedSurroundingMaxsZ);
        WritePropertyNameValue(semanticPayload, "m_iTextureFrameIndex", delta.TextureFrameIndex);
        WriteStringZ(semanticPayload, "predictable_id");
        WritePropertyNameValue(semanticPayload, "m_PredictableID", delta.PredictableId);
        WritePropertyNameValue(semanticPayload, "m_bIsPlayerSimulated", delta.IsPlayerSimulated);
        WritePropertyNameValue(semanticPayload, "m_bSimulatedEveryTick", delta.SimulatedEveryTick);
        WritePropertyNameValue(semanticPayload, "m_bAnimatedEveryTick", delta.AnimatedEveryTick);
        WritePropertyNameValue(semanticPayload, "m_bAlternateSorting", delta.AlternateSorting);
        WriteStringZ(semanticPayload, "DT_BaseAnimating");
        WritePropertyNameValue(semanticPayload, "m_nSequence", delta.Sequence);
        WritePropertyNameValue(semanticPayload, "m_nForceBone", delta.ForceBone);
        WriteVector3Property(semanticPayload, "m_vecForce", delta.ForceX, delta.ForceY, delta.ForceZ);
        WritePropertyNameValue(semanticPayload, "m_nSkin", delta.Skin);
        WritePropertyNameValue(semanticPayload, "m_nBody", delta.Body);
        WritePropertyNameValue(semanticPayload, "m_nHitboxSet", delta.HitboxSet);
        WriteFloatProperty(semanticPayload, "m_flModelWidthScale", delta.ModelWidthScale);
        WriteFloatArrayProperty(semanticPayload, "m_flPoseParameter", delta.PoseParameters ?? [], maxCount: 0x18);
        WriteFloatProperty(semanticPayload, "m_flPlaybackRate", delta.PlaybackRate);
        WriteFloatArrayProperty(semanticPayload, "m_flEncodedController", delta.EncodedControllers ?? [], maxCount: 4);
        WritePropertyNameValue(semanticPayload, "m_bClientSideAnimation", delta.ClientSideAnimation);
        WritePropertyNameValue(semanticPayload, "m_bClientSideFrameReset", delta.ClientSideFrameReset);
        WritePropertyNameValue(semanticPayload, "m_nNewSequenceParity", delta.NewSequenceParity);
        WritePropertyNameValue(semanticPayload, "m_nResetEventsParity", delta.ResetEventsParity);
        WritePropertyNameValue(semanticPayload, "m_nMuzzleFlashParity", delta.MuzzleFlashParity);
        WritePropertyNameValue(semanticPayload, "m_hLightingOrigin", delta.LightingOriginHandle);
        WritePropertyNameValue(semanticPayload, "m_hLightingOriginRelative", delta.LightingOriginRelativeHandle);
        WriteStringZ(semanticPayload, "serveranimdata");
        WriteFloatProperty(semanticPayload, "m_flCycle", delta.ServerAnimationCycle);
        WriteFloatProperty(semanticPayload, "m_fadeMinDist", delta.FadeMinDistance);
        WriteFloatProperty(semanticPayload, "m_fadeMaxDist", delta.FadeMaxDistance);
        WriteFloatProperty(semanticPayload, "m_flFadeScale", delta.FadeScale);
        WriteStringZ(semanticPayload, "DT_BasePlayer");
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_lifeState", delta.LifeState);
        WritePropertyNameValue(semanticPayload, "m_fFlags", delta.Flags);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WriteFloatProperty(semanticPayload, "m_angEyeAngles[0]", delta.EyePitch);
        WriteFloatProperty(semanticPayload, "m_angEyeAngles[1]", delta.EyeYaw);
        WriteStringZ(semanticPayload, "m_PlayerClass");
        WritePropertyNameValue(semanticPayload, "m_iClass", delta.ClassNumber);
        semanticPayload.Add(0);

        var descriptor = Ps3SourceEntityDeltaFrameBuilder.BuildQueuedBitstreamDescriptor(
            CollectionsMarshal.AsSpan(semanticPayload),
            semanticPayload.Count * 8,
            (int)(delta.ObjectId & 0x03ffffff));

        var groups = new Ps3SourceEntityDeltaGroupState[]
        {
            new Ps3SourceEntityDeltaGroupState(
                GroupIndex: 4,
                Ps3SourceEntityDeltaGroupWriteState.Inactive,
                StartIndex: 0,
                EntityCount: 0,
                ObjectId: delta.ObjectId,
                ObjectName: "CTFPlayer",
                Descriptor: null,
                LastWrittenFrameIndex: 0)
                .MarkActive(descriptor, 0, NativePartialChunkCount(descriptor))
        };

        return Ps3SourceEntityDeltaFrameBuilder.EncodeActiveGroupsNativePartialWindows(groups, frameIndex: 0).Payload;
    }

    public static byte[] BuildCompactPlayerEntityDelta(Ps3SourcePlayerEntityDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'E',
            (byte)'D',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_BaseEntity");
        WriteFloatProperty(semanticPayload, "m_flSimulationTime", delta.SimulationTime);
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WriteVector3Property(semanticPayload, "m_angRotation", delta.RotationPitch, delta.RotationYaw, delta.RotationRoll);
        WritePropertyNameValue(semanticPayload, "m_iTeamNum", delta.TeamNumber);
        WritePropertyNameValue(semanticPayload, "movetype", delta.MoveType);
        WritePropertyNameValue(semanticPayload, "movecollide", delta.MoveCollide);
        WritePropertyNameValue(semanticPayload, "m_CollisionGroup", delta.CollisionGroup);
        WriteStringZ(semanticPayload, "DT_CollisionProperty");
        WritePropertyNameValue(semanticPayload, "m_nSolidType", delta.SolidType);
        WritePropertyNameValue(semanticPayload, "m_usSolidFlags", delta.SolidFlags);
        WriteStringZ(semanticPayload, "DT_BasePlayer");
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_lifeState", delta.LifeState);
        WritePropertyNameValue(semanticPayload, "m_fFlags", delta.Flags);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WriteFloatProperty(semanticPayload, "m_angEyeAngles[0]", delta.EyePitch);
        WriteFloatProperty(semanticPayload, "m_angEyeAngles[1]", delta.EyeYaw);
        WriteStringZ(semanticPayload, "m_PlayerClass");
        WritePropertyNameValue(semanticPayload, "m_iClass", delta.ClassNumber);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(4, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFPlayer", semanticPayload);
    }

    public static byte[] BuildTinyPlayerEntityDelta(Ps3SourcePlayerEntityDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'E',
            (byte)'T',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_BasePlayer");
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_lifeState", delta.LifeState);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_iClass", delta.ClassNumber);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(4, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFPlayer", semanticPayload);
    }

    public static byte[] BuildMicroPlayerEntityDelta(Ps3SourcePlayerEntityDelta delta)
    {
        var semanticPayload = new List<byte>();
        WriteStringZ(semanticPayload, "DT_BaseEntity");
        WriteFloatProperty(semanticPayload, "m_flSimulationTime", delta.SimulationTime);
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WritePropertyNameValue(semanticPayload, "movetype", delta.MoveType);
        WritePropertyNameValue(semanticPayload, "movecollide", delta.MoveCollide);
        WritePropertyNameValue(semanticPayload, "m_CollisionGroup", delta.CollisionGroup);
        WriteStringZ(semanticPayload, "DT_CollisionProperty");
        WritePropertyNameValue(semanticPayload, "m_nSolidType", delta.SolidType);
        WritePropertyNameValue(semanticPayload, "m_usSolidFlags", delta.SolidFlags);
        WriteStringZ(semanticPayload, "DT_BasePlayer");
        WritePropertyNameValue(semanticPayload, "m_iHealth", delta.Health);
        WritePropertyNameValue(semanticPayload, "m_lifeState", delta.LifeState);
        WritePropertyNameValue(semanticPayload, "m_fFlags", delta.Flags);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(4, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFPlayer", semanticPayload);
    }

    public static byte[] BuildGameplayStateDelta(Ps3SourceGameplayStateDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'G',
            (byte)'S',
            (byte)'D',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_BasePlayerLocalData");
        WritePropertyNameValue(semanticPayload, "m_iFOV", delta.Fov);
        WritePropertyNameValue(semanticPayload, "m_iFOVStart", delta.FovStart);
        WriteFloatProperty(semanticPayload, "m_flFOVTime", delta.FovTime);
        WritePropertyNameValue(semanticPayload, "m_iDefaultFOV", delta.DefaultFov);
        WritePropertyNameValue(semanticPayload, "m_hZoomOwner", delta.ZoomOwnerHandle);
        WritePropertyNameValue(semanticPayload, "m_hVehicle", delta.VehicleHandle);
        WritePropertyNameValue(semanticPayload, "m_hUseEntity", delta.UseEntityHandle);
        WriteFloatProperty(semanticPayload, "m_flMaxspeed", delta.MaxSpeed);
        WritePropertyNameValue(semanticPayload, "m_iObserverMode", delta.ObserverMode);
        WritePropertyNameValue(semanticPayload, "m_hObserverTarget", delta.ObserverTargetHandle);
        WriteUIntArrayProperty(semanticPayload, "m_hViewModel", delta.ViewModelHandles, maxCount: 2);
        WriteStringProperty(semanticPayload, "m_szLastPlaceName", delta.LastPlaceName, maxBytes: 18);
        WritePropertyNameValue(semanticPayload, "m_ubEFNoInterpParity", delta.NoInterpParity);
        WriteVector3Property(semanticPayload, "m_vecViewOffset", delta.ViewOffsetX, delta.ViewOffsetY, delta.ViewOffsetZ);
        WriteFloatProperty(semanticPayload, "m_flFriction", delta.Friction);
        WriteUIntArrayProperty(semanticPayload, "m_iAmmo", delta.Ammo, maxCount: 32);
        WritePropertyNameValue(semanticPayload, "m_fOnTarget", delta.OnTarget);
        WritePropertyNameValue(semanticPayload, "m_nTickBase", delta.TickBase);
        WritePropertyNameValue(semanticPayload, "m_nNextThinkTick", delta.NextThinkTick);
        WritePropertyNameValue(semanticPayload, "m_hLastWeapon", delta.LastWeaponHandle);
        WritePropertyNameValue(semanticPayload, "m_hGroundEntity", delta.GroundEntityHandle);
        WriteVector3Property(semanticPayload, "m_vecVelocity", delta.VelocityX, delta.VelocityY, delta.VelocityZ);
        WriteVector3Property(semanticPayload, "m_vecBaseVelocity", delta.BaseVelocityX, delta.BaseVelocityY, delta.BaseVelocityZ);
        WritePropertyNameValue(semanticPayload, "m_hConstraintEntity", delta.ConstraintEntityHandle);
        WriteVector3Property(semanticPayload, "m_vecConstraintCenter", delta.ConstraintCenterX, delta.ConstraintCenterY, delta.ConstraintCenterZ);
        WriteFloatProperty(semanticPayload, "m_flConstraintRadius", delta.ConstraintRadius);
        WriteFloatProperty(semanticPayload, "m_flConstraintWidth", delta.ConstraintWidth);
        WriteFloatProperty(semanticPayload, "m_flConstraintSpeedFactor", delta.ConstraintSpeedFactor);
        WriteFloatProperty(semanticPayload, "m_flDeathTime", delta.DeathTime);
        WritePropertyNameValue(semanticPayload, "m_nWaterLevel", delta.WaterLevel);
        WriteFloatProperty(semanticPayload, "m_flLaggedMovementValue", delta.LaggedMovementValue);
        WriteStringZ(semanticPayload, "DT_Local");
        WritePropertyNameValue(semanticPayload, "m_bDucked", delta.Ducked);
        WritePropertyNameValue(semanticPayload, "m_bDucking", delta.Ducking);
        WritePropertyNameValue(semanticPayload, "m_bInDuckJump", delta.InDuckJump);
        WriteFloatProperty(semanticPayload, "m_flDucktime", delta.DuckTime);
        WriteFloatProperty(semanticPayload, "m_flDuckJumpTime", delta.DuckJumpTime);
        WriteFloatProperty(semanticPayload, "m_flJumpTime", delta.JumpTime);
        WriteFloatProperty(semanticPayload, "m_flFallVelocity", delta.FallVelocity);
        WriteVector3Property(semanticPayload, "m_vecPunchAngle", delta.PunchAngleX, delta.PunchAngleY, delta.PunchAngleZ);
        WriteVector3Property(semanticPayload, "m_vecPunchAngleVel", delta.PunchAngleVelocityX, delta.PunchAngleVelocityY, delta.PunchAngleVelocityZ);
        WritePropertyNameValue(semanticPayload, "m_bDrawViewmodel", delta.DrawViewModel);
        WritePropertyNameValue(semanticPayload, "m_bWearingSuit", delta.WearingSuit);
        WritePropertyNameValue(semanticPayload, "m_bPoisoned", delta.Poisoned);
        WriteFloatProperty(semanticPayload, "m_flStepSize", delta.StepSize);
        WritePropertyNameValue(semanticPayload, "m_bAllowAutoMovement", delta.AllowAutoMovement);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_bSaveMeParity", delta.SaveMeParity);
        WritePropertyNameValue(semanticPayload, "m_nWaterLevel", delta.WaterLevel);
        WritePropertyNameValue(semanticPayload, "m_hRagdoll", delta.RagdollHandle);
        WriteStringZ(semanticPayload, "m_Shared");
        WriteStringZ(semanticPayload, "DT_TFPlayerShared");
        WritePropertyNameValue(semanticPayload, "m_nPlayerCond", delta.PlayerCondition);
        WritePropertyNameValue(semanticPayload, "m_bJumping", delta.Jumping);
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.PlayerState);
        WritePropertyNameValue(semanticPayload, "m_iDesiredPlayerClass", delta.DesiredPlayerClass);
        WritePropertyNameValue(semanticPayload, "m_nDisguiseTeam", delta.DisguiseTeam);
        WritePropertyNameValue(semanticPayload, "m_nDisguiseClass", delta.DisguiseClass);
        WritePropertyNameValue(semanticPayload, "m_iDisguiseTargetIndex", delta.DisguiseTargetIndex);
        WritePropertyNameValue(semanticPayload, "m_iDisguiseHealth", delta.DisguiseHealth);
        WritePropertyNameValue(semanticPayload, "m_nDesiredDisguiseTeam", delta.DesiredDisguiseTeam);
        WritePropertyNameValue(semanticPayload, "m_nDesiredDisguiseClass", delta.DesiredDisguiseClass);
        WriteFloatProperty(semanticPayload, "m_flCloakMeter", delta.CloakMeter);
        WritePropertyNameValue(semanticPayload, "m_hItem", delta.ItemHandle);
        WriteStringZ(semanticPayload, "tflocaldata");
        WriteStringZ(semanticPayload, "DT_TFLocalPlayerExclusive");
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.TfLocalOriginX, delta.TfLocalOriginY, delta.TfLocalOriginZ);
        WritePropertyNameValue(semanticPayload, "player_object_array_element", delta.PlayerObjectArrayElement);
        WriteStringZ(semanticPayload, "_player_object_array_");
        WriteStringZ(semanticPayload, "tfnonlocaldata");
        WriteStringZ(semanticPayload, "DT_TFNonLocalPlayerExclusive");
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.TfNonLocalOriginX, delta.TfNonLocalOriginY, delta.TfNonLocalOriginZ);
        WritePropertyNameValue(semanticPayload, "m_iSpawnCounter", delta.SpawnCounter);
        WriteStringZ(semanticPayload, "DT_BaseCombatCharacter");
        WritePropertyNameValue(semanticPayload, "m_hActiveWeapon", delta.ActiveWeaponHandle);
        WriteUIntArrayProperty(semanticPayload, "m_hMyWeapons", delta.WeaponHandles, maxCount: 48);
        WriteStringZ(semanticPayload, "DT_BCCLocalPlayerExclusive");
        WriteFloatProperty(semanticPayload, "m_flNextAttack", delta.NextAttack);
        WriteStringZ(semanticPayload, "DT_TFPlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iTotalScore", delta.TotalScore);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", delta.Captures);
        WritePropertyNameValue(semanticPayload, "m_iDefenses", delta.Defenses);
        WritePropertyNameValue(semanticPayload, "m_iDominations", delta.Dominations);
        WritePropertyNameValue(semanticPayload, "m_iRevenge", delta.Revenge);
        WritePropertyNameValue(semanticPayload, "m_iBuildingsDestroyed", delta.BuildingsDestroyed);
        WritePropertyNameValue(semanticPayload, "m_iHeadshots", delta.Headshots);
        WritePropertyNameValue(semanticPayload, "m_iBackstabs", delta.Backstabs);
        WritePropertyNameValue(semanticPayload, "m_iHealPoints", delta.HealPoints);
        WritePropertyNameValue(semanticPayload, "m_iInvulns", delta.Invulns);
        WritePropertyNameValue(semanticPayload, "m_iTeleports", delta.Teleports);
        WritePropertyNameValue(semanticPayload, "m_iResupplyPoints", delta.ResupplyPoints);
        WritePropertyNameValue(semanticPayload, "m_iKillAssists", delta.KillAssists);
        WritePropertyNameValue(semanticPayload, "m_iMaxHealth", delta.MaxHealth);
        WritePropertyNameValue(semanticPayload, "m_iPlayerClass", delta.PlayerClass);
        WriteStringZ(semanticPayload, "DT_Team");
        WritePropertyNameValue(semanticPayload, "m_iTeamNum", delta.SourceTeamNumber);
        WritePropertyNameValue(semanticPayload, "m_iScore", delta.SourceTeamScore);
        WritePropertyNameValue(semanticPayload, "m_iRoundsWon", delta.SourceTeamRoundsWon);
        WriteStringProperty(semanticPayload, "m_szTeamname", delta.SourceTeamName, maxBytes: 0x20);
        WriteStringZ(semanticPayload, "_player_array");
        WriteStringZ(semanticPayload, "DT_TFTeam");
        WritePropertyNameValue(semanticPayload, "m_nFlagCaptures", delta.TeamFlagCaptures);
        WritePropertyNameValue(semanticPayload, "m_iRole", delta.TeamRole);
        WriteStringZ(semanticPayload, "DT_ObjectSentrygun");
        WritePropertyNameValue(semanticPayload, "m_iUpgradeLevel", delta.ObjectUpgradeLevel);
        WritePropertyNameValue(semanticPayload, "m_iAmmoShells", delta.ObjectAmmoShells);
        WritePropertyNameValue(semanticPayload, "m_iAmmoRockets", delta.ObjectAmmoRockets);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.ObjectState);
        WritePropertyNameValue(semanticPayload, "m_iUpgradeMetal", delta.ObjectUpgradeMetal);
        WriteStringZ(semanticPayload, "DT_ObjectTeleporter");
        WritePropertyNameValue(semanticPayload, "m_iState", delta.TeleporterState);
        WriteFloatProperty(semanticPayload, "m_flRechargeTime", delta.TeleporterRechargeTime);
        WritePropertyNameValue(semanticPayload, "m_iTimesUsed", delta.TeleporterTimesUsed);
        WriteFloatProperty(semanticPayload, "m_flYawToExit", delta.TeleporterYawToExit);
        WriteStringZ(semanticPayload, "DT_TeamplayRoundBasedRules");
        WritePropertyNameValue(semanticPayload, "m_iRoundState", delta.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iWinningTeam", delta.WinningTeam);
        WritePropertyNameValue(semanticPayload, "m_bInWaitingForPlayers", delta.InWaitingForPlayers);
        WritePropertyNameValue(semanticPayload, "m_bInSetup", delta.InSetup ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bInOvertime", delta.InOvertime ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bSwitchedTeamsThisRound", delta.SwitchedTeamsThisRound);
        WritePropertyNameValue(semanticPayload, "m_bAwaitingReadyRestart", delta.AwaitingReadyRestart);
        WriteFloatProperty(semanticPayload, "m_flRestartRoundTime", delta.RestartRoundTime);
        WriteFloatProperty(semanticPayload, "m_flMapResetTime", delta.MapResetTime);
        WriteFloatArrayProperty(semanticPayload, "m_flNextRespawnWave", delta.NextRespawnWave ?? [], maxCount: 32);
        WriteFloatArrayProperty(semanticPayload, "m_TeamRespawnWaveTimes", delta.TeamRespawnWaveTimes ?? [], maxCount: 32);
        WriteStringZ(semanticPayload, "DT_TFGameRules");
        WritePropertyNameValue(semanticPayload, "m_nGameType", delta.GameType);
        WriteStringProperty(semanticPayload, "m_pszTeamGoalStringRed", delta.TeamGoalStringRed, maxBytes: 0x100);
        WriteStringProperty(semanticPayload, "m_pszTeamGoalStringBlue", delta.TeamGoalStringBlue, maxBytes: 0x100);
        semanticPayload.Add(0);

        var descriptor = Ps3SourceEntityDeltaFrameBuilder.BuildQueuedBitstreamDescriptor(
            CollectionsMarshal.AsSpan(semanticPayload),
            semanticPayload.Count * 8,
            (int)(delta.ObjectId & 0x03ffffff));

        var groups = new Ps3SourceEntityDeltaGroupState[]
        {
            new Ps3SourceEntityDeltaGroupState(
                GroupIndex: 5,
                Ps3SourceEntityDeltaGroupWriteState.Inactive,
                StartIndex: 0,
                EntityCount: 0,
                ObjectId: delta.ObjectId,
                ObjectName: "CTFGameRulesProxy",
                Descriptor: null,
                LastWrittenFrameIndex: 0)
                .MarkActive(descriptor, 0, NativePartialChunkCount(descriptor))
        };

        return Ps3SourceEntityDeltaFrameBuilder.EncodeActiveGroupsNativePartialWindows(groups, frameIndex: 0).Payload;
    }

    public static byte[] BuildCompactGameplayRulesDelta(Ps3SourceGameplayStateDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'G',
            (byte)'R',
            (byte)'D',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_TFPlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iTotalScore", delta.TotalScore);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", delta.Captures);
        WritePropertyNameValue(semanticPayload, "m_iDefenses", delta.Defenses);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WritePropertyNameValue(semanticPayload, "m_iKillAssists", delta.KillAssists);
        WritePropertyNameValue(semanticPayload, "m_iBuildingsDestroyed", delta.BuildingsDestroyed);
        WritePropertyNameValue(semanticPayload, "m_iHeadshots", delta.Headshots);
        WritePropertyNameValue(semanticPayload, "m_iBackstabs", delta.Backstabs);
        WriteStringZ(semanticPayload, "DT_TFTeam");
        WritePropertyNameValue(semanticPayload, "m_iTeamNum", delta.SourceTeamNumber);
        WritePropertyNameValue(semanticPayload, "m_iScore", delta.SourceTeamScore);
        WritePropertyNameValue(semanticPayload, "m_iRoundsWon", delta.SourceTeamRoundsWon);
        WritePropertyNameValue(semanticPayload, "m_nFlagCaptures", delta.TeamFlagCaptures);
        WriteStringZ(semanticPayload, "DT_TeamplayRoundBasedRules");
        WritePropertyNameValue(semanticPayload, "m_iRoundState", delta.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iWinningTeam", delta.WinningTeam);
        WritePropertyNameValue(semanticPayload, "m_bInWaitingForPlayers", delta.InWaitingForPlayers);
        WritePropertyNameValue(semanticPayload, "m_bInSetup", delta.InSetup ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bInOvertime", delta.InOvertime ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bAwaitingReadyRestart", delta.AwaitingReadyRestart);
        WriteFloatProperty(semanticPayload, "m_flRestartRoundTime", delta.RestartRoundTime);
        WriteFloatProperty(semanticPayload, "m_flMapResetTime", delta.MapResetTime);
        WriteFloatProperty(semanticPayload, "m_flDeathTime", delta.DeathTime);
        WriteStringZ(semanticPayload, "DT_TFGameRules");
        WritePropertyNameValue(semanticPayload, "m_nGameType", delta.GameType);
        WriteStringZ(semanticPayload, "DT_BasePlayerLocalData");
        WritePropertyNameValue(semanticPayload, "m_iFOV", delta.Fov);
        WritePropertyNameValue(semanticPayload, "m_iFOVStart", delta.FovStart);
        WriteFloatProperty(semanticPayload, "m_flFOVTime", delta.FovTime);
        WriteFloatProperty(semanticPayload, "m_flMaxspeed", delta.MaxSpeed);
        WritePropertyNameValue(semanticPayload, "m_iObserverMode", delta.ObserverMode);
        WritePropertyNameValue(semanticPayload, "m_hObserverTarget", delta.ObserverTargetHandle);
        WriteUIntArrayProperty(semanticPayload, "m_hViewModel", delta.ViewModelHandles, maxCount: 2);
        WriteVector3Property(semanticPayload, "m_vecViewOffset", delta.ViewOffsetX, delta.ViewOffsetY, delta.ViewOffsetZ);
        WriteFloatProperty(semanticPayload, "m_flFriction", delta.Friction);
        WriteUIntArrayProperty(semanticPayload, "m_iAmmo", delta.Ammo, maxCount: 8);
        WritePropertyNameValue(semanticPayload, "m_nTickBase", delta.TickBase);
        WritePropertyNameValue(semanticPayload, "m_hGroundEntity", delta.GroundEntityHandle);
        WriteStringZ(semanticPayload, "DT_BaseCombatCharacter");
        WritePropertyNameValue(semanticPayload, "m_hActiveWeapon", delta.ActiveWeaponHandle);
        WriteUIntArrayProperty(semanticPayload, "m_hMyWeapons", delta.WeaponHandles, maxCount: 8);
        WriteVector3Property(semanticPayload, "m_vecVelocity", delta.VelocityX, delta.VelocityY, delta.VelocityZ);
        WriteVector3Property(semanticPayload, "m_vecBaseVelocity", delta.BaseVelocityX, delta.BaseVelocityY, delta.BaseVelocityZ);
        WriteStringZ(semanticPayload, "DT_Local");
        WritePropertyNameValue(semanticPayload, "m_bDucked", delta.Ducked);
        WritePropertyNameValue(semanticPayload, "m_bDucking", delta.Ducking);
        WritePropertyNameValue(semanticPayload, "m_bInDuckJump", delta.InDuckJump);
        WriteFloatProperty(semanticPayload, "m_flFallVelocity", delta.FallVelocity);
        WritePropertyNameValue(semanticPayload, "m_bDrawViewmodel", delta.DrawViewModel);
        WritePropertyNameValue(semanticPayload, "m_bAllowAutoMovement", delta.AllowAutoMovement);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_hRagdoll", delta.RagdollHandle);
        WritePropertyNameValue(semanticPayload, "m_iSpawnCounter", delta.SpawnCounter);
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.PlayerState);
        WritePropertyNameValue(semanticPayload, "m_iMaxHealth", delta.MaxHealth);
        WriteStringZ(semanticPayload, "DT_TFPlayerShared");
        WritePropertyNameValue(semanticPayload, "m_bJumping", delta.Jumping);
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.PlayerState);
        WriteStringZ(semanticPayload, "DT_ObjectSentrygun");
        WritePropertyNameValue(semanticPayload, "m_iUpgradeLevel", delta.ObjectUpgradeLevel);
        WritePropertyNameValue(semanticPayload, "m_iAmmoShells", delta.ObjectAmmoShells);
        WritePropertyNameValue(semanticPayload, "m_iAmmoRockets", delta.ObjectAmmoRockets);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.ObjectState);
        WritePropertyNameValue(semanticPayload, "m_iUpgradeMetal", delta.ObjectUpgradeMetal);
        WriteStringZ(semanticPayload, "DT_ObjectTeleporter");
        WritePropertyNameValue(semanticPayload, "m_iState", delta.TeleporterState);
        WriteFloatProperty(semanticPayload, "m_flRechargeTime", delta.TeleporterRechargeTime);
        WritePropertyNameValue(semanticPayload, "m_iTimesUsed", delta.TeleporterTimesUsed);
        WriteFloatProperty(semanticPayload, "m_flYawToExit", delta.TeleporterYawToExit);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(5, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFGameRulesProxy", semanticPayload);
    }

    public static byte[] BuildTinyGameplayRulesDelta(Ps3SourceGameplayStateDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'G',
            (byte)'R',
            (byte)'T',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_TeamplayRoundBasedRules");
        WritePropertyNameValue(semanticPayload, "m_iRoundState", delta.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iWinningTeam", delta.WinningTeam);
        WritePropertyNameValue(semanticPayload, "m_bInWaitingForPlayers", delta.InWaitingForPlayers);
        WritePropertyNameValue(semanticPayload, "m_bInSetup", delta.InSetup ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bInOvertime", delta.InOvertime ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bAwaitingReadyRestart", delta.AwaitingReadyRestart);
        WriteFloatProperty(semanticPayload, "m_flDeathTime", delta.DeathTime);
        WriteStringZ(semanticPayload, "DT_TFGameRules");
        WritePropertyNameValue(semanticPayload, "m_nGameType", delta.GameType);
        WriteStringZ(semanticPayload, "DT_TFPlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iTotalScore", delta.TotalScore);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", delta.Captures);
        WritePropertyNameValue(semanticPayload, "m_iDefenses", delta.Defenses);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WritePropertyNameValue(semanticPayload, "m_iKillAssists", delta.KillAssists);
        WritePropertyNameValue(semanticPayload, "m_iBuildingsDestroyed", delta.BuildingsDestroyed);
        WritePropertyNameValue(semanticPayload, "m_iHeadshots", delta.Headshots);
        WritePropertyNameValue(semanticPayload, "m_iBackstabs", delta.Backstabs);
        WritePropertyNameValue(semanticPayload, "m_iKillAssists", delta.KillAssists);
        WriteStringZ(semanticPayload, "DT_TFTeam");
        WritePropertyNameValue(semanticPayload, "m_iRoundsWon", delta.SourceTeamRoundsWon);
        WritePropertyNameValue(semanticPayload, "m_nFlagCaptures", delta.TeamFlagCaptures);
        WriteStringZ(semanticPayload, "DT_BasePlayerLocalData");
        WriteFloatProperty(semanticPayload, "m_flMaxspeed", delta.MaxSpeed);
        WritePropertyNameValue(semanticPayload, "m_iObserverMode", delta.ObserverMode);
        WriteUIntArrayProperty(semanticPayload, "m_iAmmo", delta.Ammo, maxCount: 8);
        WritePropertyNameValue(semanticPayload, "m_nTickBase", delta.TickBase);
        WritePropertyNameValue(semanticPayload, "m_hGroundEntity", delta.GroundEntityHandle);
        WriteStringZ(semanticPayload, "DT_Local");
        WritePropertyNameValue(semanticPayload, "m_bDucked", delta.Ducked);
        WritePropertyNameValue(semanticPayload, "m_bDucking", delta.Ducking);
        WritePropertyNameValue(semanticPayload, "m_bInDuckJump", delta.InDuckJump);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_hRagdoll", delta.RagdollHandle);
        WritePropertyNameValue(semanticPayload, "m_iSpawnCounter", delta.SpawnCounter);
        WriteStringZ(semanticPayload, "DT_TFPlayerShared");
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.PlayerState);
        WriteStringZ(semanticPayload, "DT_ObjectSentrygun");
        WritePropertyNameValue(semanticPayload, "m_iUpgradeLevel", delta.ObjectUpgradeLevel);
        WritePropertyNameValue(semanticPayload, "m_iAmmoShells", delta.ObjectAmmoShells);
        WritePropertyNameValue(semanticPayload, "m_iAmmoRockets", delta.ObjectAmmoRockets);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.ObjectState);
        WritePropertyNameValue(semanticPayload, "m_iUpgradeMetal", delta.ObjectUpgradeMetal);
        WriteStringZ(semanticPayload, "DT_ObjectTeleporter");
        WritePropertyNameValue(semanticPayload, "m_iState", delta.TeleporterState);
        WriteFloatProperty(semanticPayload, "m_flRechargeTime", delta.TeleporterRechargeTime);
        WritePropertyNameValue(semanticPayload, "m_iTimesUsed", delta.TeleporterTimesUsed);
        WriteFloatProperty(semanticPayload, "m_flYawToExit", delta.TeleporterYawToExit);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(5, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFGameRulesProxy", semanticPayload);
    }

    public static byte[] BuildMicroGameplayRulesDelta(Ps3SourceGameplayStateDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'G',
            (byte)'R',
            (byte)'M',
            delta.PlayerSlotIndex
        };
        WriteStringZ(semanticPayload, "DT_TeamplayRoundBasedRules");
        WritePropertyNameValue(semanticPayload, "m_iRoundState", delta.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iWinningTeam", delta.WinningTeam);
        WritePropertyNameValue(semanticPayload, "m_bInWaitingForPlayers", delta.InWaitingForPlayers);
        WritePropertyNameValue(semanticPayload, "m_bInSetup", delta.InSetup ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bInOvertime", delta.InOvertime ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bAwaitingReadyRestart", delta.AwaitingReadyRestart);
        WriteFloatProperty(semanticPayload, "m_flRestartRoundTime", delta.RestartRoundTime);
        WriteFloatProperty(semanticPayload, "m_flMapResetTime", delta.MapResetTime);
        WriteFloatProperty(semanticPayload, "m_flDeathTime", delta.DeathTime);
        WriteStringZ(semanticPayload, "DT_TFGameRules");
        WritePropertyNameValue(semanticPayload, "m_nGameType", delta.GameType);
        WriteStringZ(semanticPayload, "DT_BasePlayerLocalData");
        WritePropertyNameValue(semanticPayload, "m_iFOV", delta.Fov);
        WritePropertyNameValue(semanticPayload, "m_iFOVStart", delta.FovStart);
        WriteFloatProperty(semanticPayload, "m_flFOVTime", delta.FovTime);
        WriteFloatProperty(semanticPayload, "m_flMaxspeed", delta.MaxSpeed);
        WritePropertyNameValue(semanticPayload, "m_iObserverMode", delta.ObserverMode);
        WritePropertyNameValue(semanticPayload, "m_hObserverTarget", delta.ObserverTargetHandle);
        WriteUIntArrayProperty(semanticPayload, "m_hViewModel", delta.ViewModelHandles, maxCount: 2);
        WriteVector3Property(semanticPayload, "m_vecViewOffset", delta.ViewOffsetX, delta.ViewOffsetY, delta.ViewOffsetZ);
        WriteFloatProperty(semanticPayload, "m_flFriction", delta.Friction);
        WriteUIntArrayProperty(semanticPayload, "m_iAmmo", delta.Ammo, maxCount: 8);
        WritePropertyNameValue(semanticPayload, "m_nTickBase", delta.TickBase);
        WritePropertyNameValue(semanticPayload, "m_nNextThinkTick", delta.NextThinkTick);
        WritePropertyNameValue(semanticPayload, "m_hGroundEntity", delta.GroundEntityHandle);
        WriteVector3Property(semanticPayload, "m_vecVelocity", delta.VelocityX, delta.VelocityY, delta.VelocityZ);
        WriteVector3Property(semanticPayload, "m_vecBaseVelocity", delta.BaseVelocityX, delta.BaseVelocityY, delta.BaseVelocityZ);
        WritePropertyNameValue(semanticPayload, "m_nWaterLevel", delta.WaterLevel);
        WriteStringZ(semanticPayload, "DT_Local");
        WritePropertyNameValue(semanticPayload, "m_bDucked", delta.Ducked);
        WritePropertyNameValue(semanticPayload, "m_bDucking", delta.Ducking);
        WritePropertyNameValue(semanticPayload, "m_bInDuckJump", delta.InDuckJump);
        WriteFloatProperty(semanticPayload, "m_flFallVelocity", delta.FallVelocity);
        WriteVector3Property(semanticPayload, "m_vecPunchAngle", delta.PunchAngleX, delta.PunchAngleY, delta.PunchAngleZ);
        WriteVector3Property(semanticPayload, "m_vecPunchAngleVel", delta.PunchAngleVelocityX, delta.PunchAngleVelocityY, delta.PunchAngleVelocityZ);
        WritePropertyNameValue(semanticPayload, "m_bDrawViewmodel", delta.DrawViewModel);
        WritePropertyNameValue(semanticPayload, "m_bAllowAutoMovement", delta.AllowAutoMovement);
        WriteStringZ(semanticPayload, "DT_TFPlayerResource");
        WritePropertyNameValue(semanticPayload, "m_iTotalScore", delta.TotalScore);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", delta.Captures);
        WritePropertyNameValue(semanticPayload, "m_iDeaths", delta.Deaths);
        WriteStringZ(semanticPayload, "DT_TFTeam");
        WritePropertyNameValue(semanticPayload, "m_iRoundsWon", delta.SourceTeamRoundsWon);
        WritePropertyNameValue(semanticPayload, "m_nFlagCaptures", delta.TeamFlagCaptures);
        WriteStringZ(semanticPayload, "DT_TFPlayer");
        WritePropertyNameValue(semanticPayload, "m_hRagdoll", delta.RagdollHandle);
        WritePropertyNameValue(semanticPayload, "m_iSpawnCounter", delta.SpawnCounter);
        WriteStringZ(semanticPayload, "DT_TFPlayerShared");
        WritePropertyNameValue(semanticPayload, "m_bJumping", delta.Jumping);
        WritePropertyNameValue(semanticPayload, "m_nPlayerState", delta.PlayerState);
        WritePropertyNameValue(semanticPayload, "m_nDisguiseTeam", delta.DisguiseTeam);
        WritePropertyNameValue(semanticPayload, "m_nDisguiseClass", delta.DisguiseClass);
        WritePropertyNameValue(semanticPayload, "m_iDisguiseTargetIndex", delta.DisguiseTargetIndex);
        WritePropertyNameValue(semanticPayload, "m_iDisguiseHealth", delta.DisguiseHealth);
        WriteStringZ(semanticPayload, "DT_ObjectSentrygun");
        WritePropertyNameValue(semanticPayload, "m_iUpgradeLevel", delta.ObjectUpgradeLevel);
        WritePropertyNameValue(semanticPayload, "m_iAmmoShells", delta.ObjectAmmoShells);
        WritePropertyNameValue(semanticPayload, "m_iAmmoRockets", delta.ObjectAmmoRockets);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.ObjectState);
        WritePropertyNameValue(semanticPayload, "m_iUpgradeMetal", delta.ObjectUpgradeMetal);
        WriteStringZ(semanticPayload, "DT_ObjectTeleporter");
        WritePropertyNameValue(semanticPayload, "m_iState", delta.TeleporterState);
        WriteFloatProperty(semanticPayload, "m_flRechargeTime", delta.TeleporterRechargeTime);
        WritePropertyNameValue(semanticPayload, "m_iTimesUsed", delta.TeleporterTimesUsed);
        WriteFloatProperty(semanticPayload, "m_flYawToExit", delta.TeleporterYawToExit);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(5, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFGameRulesProxy", semanticPayload);
    }

    public static byte[] BuildNanoGameplayRulesDelta(Ps3SourceGameplayStateDelta delta)
    {
        var semanticPayload = new List<byte>();
        WriteStringZ(semanticPayload, "TF_RulesPulse");
        WriteStringZ(semanticPayload, "DT_BasePlayerLocalData");
        WritePropertyNameValue(semanticPayload, "m_nTickBase", delta.TickBase);
        WriteUIntArrayProperty(semanticPayload, "m_iAmmo", delta.Ammo, maxCount: 8);
        WritePropertyNameValue(semanticPayload, "m_iRoundState", delta.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", delta.Captures);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(5, delta.PlayerSlotIndex, 1, delta.ObjectId, "CTFGameRulesProxy", semanticPayload);
    }

    public static byte[] BuildWeaponEntityDelta(Ps3SourceWeaponEntityDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'W',
            (byte)'E',
            (byte)'D'
        };
        WriteStringZ(semanticPayload, "DT_BaseCombatWeapon");
        WritePropertyNameValue(semanticPayload, "m_hOwner", delta.OwnerHandle);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.State);
        WritePropertyNameValue(semanticPayload, "m_iViewModelIndex", delta.ViewModelIndex);
        WritePropertyNameValue(semanticPayload, "m_iWorldModelIndex", delta.WorldModelIndex);
        WritePropertyNameValue(semanticPayload, "m_nViewModelIndex", delta.ViewModelIndex);
        WriteStringZ(semanticPayload, "DT_LocalWeaponData");
        WriteFloatProperty(semanticPayload, "m_flNextPrimaryAttack", delta.NextPrimaryAttack);
        WriteFloatProperty(semanticPayload, "m_flNextSecondaryAttack", delta.NextSecondaryAttack);
        WriteFloatProperty(semanticPayload, "m_flTimeWeaponIdle", delta.TimeWeaponIdle);
        WritePropertyNameValue(semanticPayload, "m_iPrimaryAmmoType", delta.PrimaryAmmoType);
        WritePropertyNameValue(semanticPayload, "m_iSecondaryAmmoType", delta.SecondaryAmmoType);
        WritePropertyNameValue(semanticPayload, "m_iClip1", delta.Clip1);
        WritePropertyNameValue(semanticPayload, "m_iClip2", delta.Clip2);
        WriteStringZ(semanticPayload, "DT_LocalActiveWeaponData");
        WritePropertyNameValue(semanticPayload, "m_bInReload", delta.InReload);
        WritePropertyNameValue(semanticPayload, "m_bFireOnEmpty", delta.FireOnEmpty);
        WriteFloatProperty(semanticPayload, "m_flNextEmptySoundTime", delta.NextEmptySoundTime);
        WriteStringZ(semanticPayload, "DT_TFWeaponBase");
        WritePropertyNameValue(semanticPayload, "m_bLowered", delta.Lowered);
        WritePropertyNameValue(semanticPayload, "m_iReloadMode", delta.ReloadMode);
        WritePropertyNameValue(semanticPayload, "m_bResetParity", delta.ResetParity);
        WritePropertyNameValue(semanticPayload, "m_bReloadedThroughAnimEvent", delta.ReloadedThroughAnimEvent);
        WriteStringZ(semanticPayload, "DT_TFWeaponBuilder");
        WritePropertyNameValue(semanticPayload, "m_iBuildState", delta.BuildState);
        WriteStringZ(semanticPayload, "BuilderLocalData");
        WritePropertyNameValue(semanticPayload, "m_iObjectType", delta.BuildObjectType);
        WritePropertyNameValue(semanticPayload, "m_hObjectBeingBuilt", delta.ObjectBeingBuiltHandle);
        WriteStringZ(semanticPayload, "DT_BuilderLocalData");
        WriteStringZ(semanticPayload, "DT_WeaponFlameThrower");
        WritePropertyNameValue(semanticPayload, "m_iWeaponState", delta.TfWeaponState);
        WritePropertyNameValue(semanticPayload, "m_bCritFire", delta.CritFire);
        WriteStringZ(semanticPayload, "DT_WeaponMedigun");
        WritePropertyNameValue(semanticPayload, "m_bHealing", delta.Healing);
        WritePropertyNameValue(semanticPayload, "m_bAttacking", delta.Attacking);
        WritePropertyNameValue(semanticPayload, "m_bHolstered", delta.Holstered);
        WritePropertyNameValue(semanticPayload, "m_hHealingTarget", delta.HealingTargetHandle);
        WriteFloatProperty(semanticPayload, "m_flHealEffectLifetime", delta.HealEffectLifetime);
        WriteFloatProperty(semanticPayload, "m_flChargeLevel", delta.ChargeLevel);
        WritePropertyNameValue(semanticPayload, "m_bChargeRelease", delta.ChargeRelease);
        WriteStringZ(semanticPayload, "DT_TFWeaponBottle");
        WritePropertyNameValue(semanticPayload, "m_bBroken", delta.BottleBroken);
        WriteStringZ(semanticPayload, "DT_WeaponPipebombLauncher");
        WriteStringZ(semanticPayload, "PipebombLauncherLocalData");
        WritePropertyNameValue(semanticPayload, "m_iPipebombCount", delta.PipebombCount);
        WriteStringZ(semanticPayload, "DT_PipebombLauncherLocalData");
        WriteFloatProperty(semanticPayload, "m_flChargeBeginTime", delta.ChargeBeginTime);
        WriteStringZ(semanticPayload, "DT_WeaponPistol");
        WriteStringZ(semanticPayload, "PistolLocalData");
        WriteFloatProperty(semanticPayload, "m_flSoonestPrimaryAttack", delta.SoonestPrimaryAttack);
        WriteStringZ(semanticPayload, "DT_PistolLocalData");
        WriteStringZ(semanticPayload, "DT_WeaponMinigun");
        WritePropertyNameValue(semanticPayload, "m_bCritShot", delta.MinigunCritShot);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFWeaponBase", semanticPayload);
    }

    public static byte[] BuildTinyWeaponEntityDelta(Ps3SourceWeaponEntityDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'W',
            (byte)'E',
            (byte)'T'
        };
        WriteStringZ(semanticPayload, "DT_BaseCombatWeapon");
        WritePropertyNameValue(semanticPayload, "m_hOwner", delta.OwnerHandle);
        WritePropertyNameValue(semanticPayload, "m_iState", delta.State);
        WriteStringZ(semanticPayload, "DT_LocalWeaponData");
        WritePropertyNameValue(semanticPayload, "m_iClip1", delta.Clip1);
        WritePropertyNameValue(semanticPayload, "m_iClip2", delta.Clip2);
        WriteStringZ(semanticPayload, "DT_LocalActiveWeaponData");
        WritePropertyNameValue(semanticPayload, "m_bInReload", delta.InReload);
        WritePropertyNameValue(semanticPayload, "m_bFireOnEmpty", delta.FireOnEmpty);
        WriteStringZ(semanticPayload, "DT_TFWeaponBase");
        WritePropertyNameValue(semanticPayload, "m_bLowered", delta.Lowered);
        WritePropertyNameValue(semanticPayload, "m_iReloadMode", delta.ReloadMode);
        WriteStringZ(semanticPayload, "DT_TFWeaponBuilder");
        WritePropertyNameValue(semanticPayload, "m_iBuildState", delta.BuildState);
        WriteStringZ(semanticPayload, "BuilderLocalData");
        WritePropertyNameValue(semanticPayload, "m_iObjectType", delta.BuildObjectType);
        WritePropertyNameValue(semanticPayload, "m_hObjectBeingBuilt", delta.ObjectBeingBuiltHandle);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFWeaponBase", semanticPayload);
    }

    public static byte[] BuildTeamRoundTimerDelta(Ps3SourceTeamRoundTimerDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'R',
            (byte)'T',
            (byte)'D'
        };
        WriteStringZ(semanticPayload, "DT_TeamRoundTimer");
        WritePropertyNameValue(semanticPayload, "m_bTimerPaused", delta.TimerPaused ? (byte)1 : (byte)0);
        WriteFloatProperty(semanticPayload, "m_flTimeRemaining", delta.TimeRemaining);
        WriteFloatProperty(semanticPayload, "m_flTimerEndTime", delta.TimerEndTime);
        WritePropertyNameValue(semanticPayload, "m_nTimerMaxLength", delta.TimerMaxLength);
        WritePropertyNameValue(semanticPayload, "m_bIsDisabled", delta.IsDisabled ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bShowInHUD", delta.ShowInHud ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_nTimerLength", delta.TimerLength);
        WritePropertyNameValue(semanticPayload, "m_nTimerInitialLength", delta.TimerInitialLength);
        WritePropertyNameValue(semanticPayload, "m_bAutoCountdown", delta.AutoCountdown ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_nSetupTimeLength", delta.SetupTimeLength);
        WritePropertyNameValue(semanticPayload, "m_nState", delta.State);
        WritePropertyNameValue(semanticPayload, "m_bStartPaused", delta.StartPaused ? (byte)1 : (byte)0);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTeamRoundTimer", semanticPayload);
    }

    public static byte[] BuildMicroTeamRoundTimerDelta(Ps3SourceTeamRoundTimerDelta delta)
    {
        var semanticPayload = new List<byte>();
        WriteStringZ(semanticPayload, "DT_TeamRoundTimer");
        WritePropertyNameValue(semanticPayload, "m_nState", delta.State);
        WritePropertyNameValue(semanticPayload, "m_bTimerPaused", delta.TimerPaused ? (byte)1 : (byte)0);
        WriteFloatProperty(semanticPayload, "m_flTimeRemaining", delta.TimeRemaining);
        WriteFloatProperty(semanticPayload, "m_flTimerEndTime", delta.TimerEndTime);
        WritePropertyNameValue(semanticPayload, "m_nTimerMaxLength", delta.TimerMaxLength);
        WritePropertyNameValue(semanticPayload, "m_bIsDisabled", delta.IsDisabled ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bShowInHUD", delta.ShowInHud ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_nTimerLength", delta.TimerLength);
        WritePropertyNameValue(semanticPayload, "m_nTimerInitialLength", delta.TimerInitialLength);
        WritePropertyNameValue(semanticPayload, "m_bAutoCountdown", delta.AutoCountdown ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_nSetupTimeLength", delta.SetupTimeLength);
        WritePropertyNameValue(semanticPayload, "m_bStartPaused", delta.StartPaused ? (byte)1 : (byte)0);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTeamRoundTimer", semanticPayload);
    }

    public static byte[] BuildObjectiveResourceDelta(Ps3SourceObjectiveResourceDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'O',
            (byte)'R',
            (byte)'D'
        };
        WriteStringZ(semanticPayload, "DT_BaseTeamObjectiveResource");
        WriteStringZ(semanticPayload, "DT_TFObjectiveResource");
        WritePropertyNameValue(semanticPayload, "m_iTimerToShowInHUD", delta.TimerToShowInHud);
        WritePropertyNameValue(semanticPayload, "m_iNumControlPoints", delta.NumControlPoints);
        WritePropertyNameValue(semanticPayload, "m_bPlayingMiniRounds", delta.PlayingMiniRounds ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bControlPointsReset", delta.ControlPointsReset ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_iUpdateCapHudParity", delta.UpdateCapHudParity);
        WriteFloatArrayProperty(semanticPayload, "m_vCPPositions", delta.CpPositions, maxCount: 24);
        WriteByteArrayProperty(semanticPayload, "m_bCPIsVisible", delta.CpIsVisible, maxCount: 8);
        WriteFloatArrayProperty(semanticPayload, "m_flLazyCapPerc", delta.LazyCapPercentages, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamIcons", delta.TeamIcons, maxCount: 0x40);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamOverlays", delta.TeamOverlays, maxCount: 0x40);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamReqCappers", delta.TeamRequiredCappers, maxCount: 0x40);
        WriteFloatArrayProperty(semanticPayload, "m_flTeamCapTime", delta.TeamCapTimes, maxCount: 0x40);
        WriteUIntArrayProperty(semanticPayload, "m_iPreviousPoints", delta.PreviousPoints, maxCount: 0xc0);
        WriteByteArrayProperty(semanticPayload, "m_bTeamCanCap", delta.TeamCanCap, maxCount: 0x40);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamBaseIcons", delta.TeamBaseIcons, maxCount: 0x20);
        WriteUIntArrayProperty(semanticPayload, "m_iBaseControlPoints", delta.BaseControlPoints, maxCount: 0x20);
        WriteByteArrayProperty(semanticPayload, "m_bInMiniRound", delta.InMiniRound, maxCount: 8);
        WriteByteArrayProperty(semanticPayload, "m_bWarnOnCap", delta.WarnOnCap, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iNumTeamMembers", delta.NumTeamMembers, maxCount: 0x40);
        WriteUIntArrayProperty(semanticPayload, "m_iCappingTeam", delta.CappingTeam, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamInZone", delta.TeamInZone, maxCount: 8);
        WriteByteArrayProperty(semanticPayload, "m_bBlocked", delta.Blocked, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iOwner", delta.Owner, maxCount: 8);
        WriteStringProperty(semanticPayload, "m_pszCapLayoutInHUD", delta.CapLayoutInHud, maxBytes: 0x20);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFObjectiveResource", semanticPayload);
    }

    public static byte[] BuildCompactObjectiveResourceDelta(Ps3SourceObjectiveResourceDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'O',
            (byte)'R',
            (byte)'C'
        };
        WriteStringZ(semanticPayload, "DT_TFObjectiveResource");
        WritePropertyNameValue(semanticPayload, "m_iTimerToShowInHUD", delta.TimerToShowInHud);
        WritePropertyNameValue(semanticPayload, "m_iNumControlPoints", delta.NumControlPoints);
        WritePropertyNameValue(semanticPayload, "m_iUpdateCapHudParity", delta.UpdateCapHudParity);
        WriteByteArrayProperty(semanticPayload, "m_bCPIsVisible", delta.CpIsVisible, maxCount: 8);
        WriteFloatArrayProperty(semanticPayload, "m_flLazyCapPerc", delta.LazyCapPercentages, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iCappingTeam", delta.CappingTeam, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamInZone", delta.TeamInZone, maxCount: 8);
        WriteByteArrayProperty(semanticPayload, "m_bBlocked", delta.Blocked, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iOwner", delta.Owner, maxCount: 8);
        WriteStringProperty(semanticPayload, "m_pszCapLayoutInHUD", delta.CapLayoutInHud, maxBytes: 0x20);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFObjectiveResource", semanticPayload);
    }

    public static byte[] BuildTinyObjectiveResourceDelta(Ps3SourceObjectiveResourceDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'O',
            (byte)'R',
            (byte)'T'
        };
        WriteStringZ(semanticPayload, "DT_BaseTeamObjectiveResource");
        WriteFloatArrayProperty(semanticPayload, "m_flLazyCapPerc", delta.LazyCapPercentages, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iCappingTeam", delta.CappingTeam, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamInZone", delta.TeamInZone, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iOwner", delta.Owner, maxCount: 8);
        WriteByteArrayProperty(semanticPayload, "m_bBlocked", delta.Blocked, maxCount: 8);
        WriteStringZ(semanticPayload, "DT_TFObjectiveResource");
        WritePropertyNameValue(semanticPayload, "m_iNumControlPoints", delta.NumControlPoints);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFObjectiveResource", semanticPayload);
    }

    public static byte[] BuildMicroObjectiveResourceDelta(Ps3SourceObjectiveResourceDelta delta)
    {
        var semanticPayload = new List<byte>();
        WriteStringZ(semanticPayload, "DT_BaseTeamObjectiveResource");
        WriteFloatArrayProperty(semanticPayload, "m_flLazyCapPerc", delta.LazyCapPercentages, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iCappingTeam", delta.CappingTeam, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamInZone", delta.TeamInZone, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iOwner", delta.Owner, maxCount: 8);
        WriteByteArrayProperty(semanticPayload, "m_bBlocked", delta.Blocked, maxCount: 8);
        WriteStringZ(semanticPayload, "DT_TFObjectiveResource");
        WritePropertyNameValue(semanticPayload, "m_iNumControlPoints", delta.NumControlPoints);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, delta.ObjectId, "CTFObjectiveResource", semanticPayload);
    }

    public static byte[] BuildTinyObjectiveGameplayDelta(
        Ps3SourceObjectiveResourceDelta objective,
        Ps3SourceGameplayStateDelta gameplay)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'O',
            (byte)'R',
            (byte)'G'
        };
        WriteStringZ(semanticPayload, "DT_BaseTeamObjectiveResource");
        WriteFloatArrayProperty(semanticPayload, "m_flLazyCapPerc", objective.LazyCapPercentages, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iCappingTeam", objective.CappingTeam, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iTeamInZone", objective.TeamInZone, maxCount: 8);
        WriteUIntArrayProperty(semanticPayload, "m_iOwner", objective.Owner, maxCount: 8);
        WriteStringZ(semanticPayload, "DT_TeamplayRoundBasedRules");
        WritePropertyNameValue(semanticPayload, "m_iRoundState", gameplay.RoundState);
        WritePropertyNameValue(semanticPayload, "m_iWinningTeam", gameplay.WinningTeam);
        WritePropertyNameValue(semanticPayload, "m_iCaptures", gameplay.Captures);
        WriteStringZ(semanticPayload, "DT_TFGameRules");
        WritePropertyNameValue(semanticPayload, "m_nGameType", gameplay.GameType);
        WriteStringZ(semanticPayload, "DT_TFTeam");
        WritePropertyNameValue(semanticPayload, "m_iRoundsWon", gameplay.SourceTeamRoundsWon);
        WritePropertyNameValue(semanticPayload, "m_nFlagCaptures", gameplay.TeamFlagCaptures);
        WriteStringZ(semanticPayload, "DT_TFObjectiveResource");
        WritePropertyNameValue(semanticPayload, "m_iNumControlPoints", objective.NumControlPoints);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(6, 0, 1, objective.ObjectId, "CTFObjectiveResource", semanticPayload);
    }

    public static byte[] BuildFireBulletsEvent(Ps3SourceFireBulletsEvent delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'T',
            (byte)'F',
            (byte)'B'
        };
        WriteStringZ(semanticPayload, "DT_TEFireBullets");
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WriteFloatProperty(semanticPayload, "m_vecAngles[0]", delta.AnglePitch);
        WriteFloatProperty(semanticPayload, "m_vecAngles[1]", delta.AngleYaw);
        WritePropertyNameValue(semanticPayload, "m_iWeaponID", delta.WeaponId);
        WritePropertyNameValue(semanticPayload, "m_iMode", delta.Mode);
        WritePropertyNameValue(semanticPayload, "m_iSeed", delta.Seed);
        WritePropertyNameValue(semanticPayload, "m_iPlayer", delta.PlayerIndex);
        WriteFloatProperty(semanticPayload, "m_flSpread", delta.Spread);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(7, 0, 1, delta.ObjectId, "CTEFireBullets", semanticPayload);
    }

    public static byte[] BuildTfRagdollDelta(Ps3SourceTfRagdollDelta delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'R',
            (byte)'G',
            (byte)'D'
        };
        WriteStringZ(semanticPayload, "DT_TFRagdoll");
        WriteVector3Property(semanticPayload, "m_vecRagdollOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WritePropertyNameValue(semanticPayload, "m_iPlayerIndex", delta.PlayerIndex);
        WriteVector3Property(semanticPayload, "m_vecForce", delta.ForceX, delta.ForceY, delta.ForceZ);
        WriteVector3Property(semanticPayload, "m_vecRagdollVelocity", delta.VelocityX, delta.VelocityY, delta.VelocityZ);
        WritePropertyNameValue(semanticPayload, "m_nForceBone", delta.ForceBone);
        WritePropertyNameValue(semanticPayload, "m_bGib", delta.Gib ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_bBurning", delta.Burning ? (byte)1 : (byte)0);
        WritePropertyNameValue(semanticPayload, "m_iTeam", delta.Team);
        WritePropertyNameValue(semanticPayload, "m_iClass", delta.Class);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(7, 0, 1, delta.ObjectId, "CTFRagdoll", semanticPayload);
    }

    public static byte[] BuildTfExplosionEvent(Ps3SourceTfExplosionEvent delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'T',
            (byte)'F',
            (byte)'X'
        };
        WriteStringZ(semanticPayload, "DT_TETFExplosion");
        WriteVector3Property(semanticPayload, "m_vecOrigin", delta.OriginX, delta.OriginY, delta.OriginZ);
        WriteVector3Property(semanticPayload, "m_vecNormal", delta.NormalX, delta.NormalY, delta.NormalZ);
        WritePropertyNameValue(semanticPayload, "m_iWeaponID", delta.WeaponId);
        WritePropertyNameValue(semanticPayload, "entindex", delta.EntityIndex);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(7, 0, 1, delta.ObjectId, "CTETFExplosion", semanticPayload);
    }

    public static byte[] BuildPlayerAnimEvent(Ps3SourcePlayerAnimEvent delta)
    {
        var semanticPayload = new List<byte>
        {
            (byte)'P',
            (byte)'A',
            (byte)'E'
        };
        WriteStringZ(semanticPayload, "DT_TEPlayerAnimEvent");
        WritePropertyNameValue(semanticPayload, "m_iPlayerIndex", delta.PlayerIndex);
        WritePropertyNameValue(semanticPayload, "m_iEvent", delta.EventId);
        WritePropertyNameValue(semanticPayload, "m_nData", delta.Data);
        semanticPayload.Add(0);

        return BuildEntityDeltaFrame(7, 0, 1, delta.ObjectId, "CTEPlayerAnimEvent", semanticPayload);
    }

    public static byte[] BuildFormattedTextEvent(string text)
    {
        var writer = new NativeBitWriter();
        writer.WriteSigned32(-1);
        writer.WriteStringZ(text);
        return writer.ToArray();
    }

    private static void WritePropertyNameValue(List<byte> destination, string name, ushort value)
    {
        WriteStringZ(destination, name);
        WriteUInt16BigEndian(destination, value);
    }

    private static void WritePropertyNameValue(List<byte> destination, string name, uint value)
    {
        WriteStringZ(destination, name);
        WriteUInt32BigEndian(destination, value);
    }

    private static void WritePropertyNameValue(List<byte> destination, string name, byte value)
    {
        WriteStringZ(destination, name);
        destination.Add(value);
    }

    private static void WriteVector3Property(List<byte> destination, string name, float x, float y, float z)
    {
        WriteStringZ(destination, name);
        WriteSingleBigEndian(destination, x);
        WriteSingleBigEndian(destination, y);
        WriteSingleBigEndian(destination, z);
    }

    private static void WriteFloatProperty(List<byte> destination, string name, float value)
    {
        WriteStringZ(destination, name);
        WriteSingleBigEndian(destination, value);
    }

    private static void WriteStringProperty(List<byte> destination, string name, string value, int maxBytes)
    {
        WriteStringZ(destination, name);
        var bytes = Encoding.ASCII.GetBytes(value);
        var count = Math.Min(bytes.Length, Math.Max(maxBytes - 1, 0));
        for (var i = 0; i < count; i++)
        {
            destination.Add(bytes[i]);
        }

        destination.Add(0);
    }

    private static void WriteUIntArrayProperty(List<byte> destination, string name, IReadOnlyList<uint> values, int maxCount)
    {
        WriteStringZ(destination, name);
        var count = Math.Min(values.Count, maxCount);
        destination.Add((byte)count);
        for (var i = 0; i < count; i++)
        {
            WriteUInt32BigEndian(destination, values[i]);
        }
    }

    private static void WriteFloatArrayProperty(List<byte> destination, string name, IReadOnlyList<float> values, int maxCount)
    {
        WriteStringZ(destination, name);
        var count = Math.Min(values.Count, maxCount);
        destination.Add((byte)count);
        for (var i = 0; i < count; i++)
        {
            WriteSingleBigEndian(destination, values[i]);
        }
    }

    private static void WriteByteArrayProperty(List<byte> destination, string name, IReadOnlyList<byte> values, int maxCount)
    {
        WriteStringZ(destination, name);
        var count = Math.Min(values.Count, maxCount);
        destination.Add((byte)count);
        for (var i = 0; i < count; i++)
        {
            destination.Add(values[i]);
        }
    }

    private static void WriteUInt16BigEndian(List<byte> destination, ushort value)
    {
        destination.Add((byte)(value >> 8));
        destination.Add((byte)value);
    }

    private static void WriteUInt32BigEndian(List<byte> destination, uint value)
    {
        destination.Add((byte)(value >> 24));
        destination.Add((byte)(value >> 16));
        destination.Add((byte)(value >> 8));
        destination.Add((byte)value);
    }

    private static void WriteSingleBigEndian(List<byte> destination, float value)
    {
        WriteUInt32BigEndian(destination, BitConverter.SingleToUInt32Bits(value));
    }

    private static void WriteStringZ(List<byte> destination, string value)
    {
        foreach (var item in Encoding.ASCII.GetBytes(value))
        {
            destination.Add(item);
        }

        destination.Add(0);
    }

    private static byte[] BuildEntityDeltaFrame(
        byte groupIndex,
        byte startIndex,
        byte entityCount,
        uint objectId,
        string objectName,
        List<byte> semanticPayload)
    {
        var descriptor = Ps3SourceEntityDeltaFrameBuilder.BuildQueuedBitstreamDescriptor(
            CollectionsMarshal.AsSpan(semanticPayload),
            semanticPayload.Count * 8,
            (int)(objectId & 0x03ffffff));

        var groups = new Ps3SourceEntityDeltaGroupState[]
        {
            new Ps3SourceEntityDeltaGroupState(
                groupIndex,
                Ps3SourceEntityDeltaGroupWriteState.Inactive,
                StartIndex: 0,
                EntityCount: 0,
                ObjectId: objectId,
                ObjectName: objectName,
                Descriptor: null,
                LastWrittenFrameIndex: 0)
                .MarkActive(descriptor, 0, NativePartialChunkCount(descriptor))
        };

        return Ps3SourceEntityDeltaFrameBuilder.EncodeActiveGroupsNativePartialWindows(groups, frameIndex: 0).Payload;
    }

    private static byte NativePartialChunkCount(Ps3SourceQueuedBitstreamDescriptor descriptor)
    {
        return checked((byte)Math.Clamp((int)descriptor.ChunkCount, 1, 7));
    }

    private sealed class NativeBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _bitLength;

        public void WriteByte(byte value)
        {
            WriteBits(value, 8);
        }

        public void WriteSigned16(short value)
        {
            WriteSigned(value, 16);
        }

        public void WriteSigned32(int value)
        {
            WriteSigned(value, 32);
        }

        public void WriteFloat(float value)
        {
            WriteBits(BitConverter.SingleToUInt32Bits(value), 32);
        }

        public void WriteStringZ(string value)
        {
            foreach (var ch in Encoding.ASCII.GetBytes(value))
            {
                WriteByte(ch);
            }

            WriteByte(0);
        }

        public byte[] ToArray()
        {
            return _bytes.ToArray();
        }

        private void WriteSigned(int value, int width)
        {
            var payloadBits = width - 1;
            var payloadMask = payloadBits == 31 ? 0x7fffffffu : (1u << payloadBits) - 1u;
            var encoded = (uint)value & payloadMask;
            WriteBits(encoded, payloadBits);
            WriteBits(value < 0 ? 1u : 0u, 1);
        }

        private void WriteBits(uint value, int bitCount)
        {
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
    }

    private ref struct NativeBitReader
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private readonly int _bitCount;

        public NativeBitReader(ReadOnlySpan<byte> bytes, int bitCount)
        {
            if (bitCount < 0 || bitCount > bytes.Length * 8)
            {
                throw new ArgumentOutOfRangeException(nameof(bitCount));
            }

            _bytes = bytes;
            _bitCount = bitCount;
            ConsumedBits = 0;
        }

        public int ConsumedBits { get; private set; }

        public int RemainingBits => _bitCount - ConsumedBits;

        public bool TryReadByte(out byte value)
        {
            if (!TryReadBits(8, out var raw))
            {
                value = 0;
                return false;
            }

            value = checked((byte)raw);
            return true;
        }

        public bool TryReadSigned16(out short value)
        {
            if (!TryReadSigned(16, out var decoded))
            {
                value = 0;
                return false;
            }

            value = checked((short)decoded);
            return true;
        }

        public bool TryReadSigned32(out int value)
        {
            return TryReadSigned(32, out value);
        }

        public bool TryReadFloat(out float value)
        {
            if (!TryReadBits(32, out var raw))
            {
                value = 0;
                return false;
            }

            value = BitConverter.UInt32BitsToSingle(raw);
            return true;
        }

        public bool TryReadStringZ(int maxBytes, out string value)
        {
            value = "";
            if (maxBytes <= 0)
            {
                return false;
            }

            var bytes = new List<byte>(Math.Min(maxBytes, 256));
            for (var i = 0; i < maxBytes; i++)
            {
                if (!TryReadSigned(8, out var raw))
                {
                    return false;
                }

                var current = unchecked((byte)raw);
                if (current == 0)
                {
                    value = Encoding.ASCII.GetString(CollectionsMarshal.AsSpan(bytes));
                    return true;
                }

                bytes.Add(current);
            }

            return false;
        }

        private bool TryReadSigned(int width, out int value)
        {
            value = 0;
            if (width is <= 1 or > 32
                || !TryReadBits(width - 1, out var payload)
                || !TryReadBits(1, out var sign))
            {
                return false;
            }

            value = sign == 0
                ? checked((int)payload)
                : checked((int)((long)payload - (1L << (width - 1))));
            return true;
        }

        private bool TryReadBits(int bitCount, out uint value)
        {
            value = 0;
            if (bitCount is < 0 or > 32 || ConsumedBits + bitCount > _bitCount)
            {
                return false;
            }

            for (var i = 0; i < bitCount; i++)
            {
                if (((_bytes[(ConsumedBits + i) >> 3] >> ((ConsumedBits + i) & 7)) & 1) != 0)
                {
                    value |= 1u << i;
                }
            }

            ConsumedBits += bitCount;
            return true;
        }
    }
}
