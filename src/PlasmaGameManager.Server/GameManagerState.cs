using System.Net;
using System.Net.Sockets;
using System.Text;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public enum PlayerJoinState
{
    Unknown,
    Connected,
    Reserved,
    Entered,
    RosterSent,
    RosterNoticeProcessing,
    RosterNoticeComplete,
    MeshJoined,
    FullMeshReceived,
    JoinComplete,
    SourceHandoff,
    Left
}

public sealed class GameManagerSession
{
    private static readonly string[] DefaultMapRotation =
    [
        "ctf_2fort",
        "cp_dustbowl",
        "cp_gravelpit",
        "cp_well",
        "ctf_well"
    ];

    private long _nextSourceServerCommandId = 1;

    private long _nextSourceBanId = 1;

    private int _nextBotId = 1;

    private int _nextBotPlayerId = 198;

    public GameManagerSession()
        : this(GameManagerSessionOptions.Default)
    {
    }

    public GameManagerSession(GameManagerSessionOptions options)
    {
        LocalId = options.LocalId;
        GameId = options.GameId;
        MaxPlayers = options.MaxPlayers;
        MapName = options.MapName;
        NextMapName = NextMapAfter(MapName);
        Name = options.Name;
        PreferredPlayerId = options.PreferredPlayerId;
        PreferredPlayerName = options.PreferredPlayerName;
        JoinTicket = options.JoinTicket;
        EncryptionKey = options.EncryptionKey;
        UniqueGameId = options.UniqueGameId;
        ServerUid = options.ServerUid;
        GameMode = options.GameMode;
        RankingMode = options.RankingMode;
        IsRanked = options.IsRanked;
        TimeLimitMinutes = options.TimeLimitMinutes;
        MaxRounds = options.MaxRounds;
        FlagCaptureLimit = options.FlagCaptureLimit;
        AutoBalance = options.AutoBalance;
        AdvertisedHost = options.AdvertisedHost;
        AdvertisedPort = options.AdvertisedPort;
        NativeRankedStatsExportPath = options.NativeRankedStatsExportPath;
        MapMetadataPath = options.MapMetadataPath;
        NativeSourceContentRootPath = options.NativeSourceContentRootPath;
        MapMetadataCatalog = Tf2MapMetadataCatalog.LoadFromJsonFile(MapMetadataPath);
        InitializeSourceCvars();
        InitializeSourceConfigDirectories();
        World.ApplyMapDefaults(MapName, GameMode, TimeLimitMinutes, MaxRounds, FlagCaptureLimit, AutoBalance, Players.Values, CurrentMapMetadata);
    }

    public long LocalId { get; set; }

    public long GameId { get; set; }

    public int MaxPlayers { get; set; }

    public string MapName { get; set; }

    public string NextMapName { get; set; }

    public string Name { get; set; }

    public int PreferredPlayerId { get; set; }

    public string PreferredPlayerName { get; set; } = "";

    public string JoinTicket { get; set; }

    public string EncryptionKey { get; set; }

    public string UniqueGameId { get; set; }

    public ulong ServerUid { get; set; }

    public string GameMode { get; set; }

    public string RankingMode { get; set; }

    public bool IsRanked { get; set; }

    public int TimeLimitMinutes { get; set; }

    public int MaxRounds { get; set; }

    public int FlagCaptureLimit { get; set; }

    public bool AutoBalance { get; set; }

    public string AdvertisedHost { get; set; }

    public int AdvertisedPort { get; set; }

    public string AdvertisedEndpoint => $"{AdvertisedHost}:{AdvertisedPort}";

    public object SyncRoot { get; } = new();

    public Dictionary<string, PlayerSession> Players { get; } = new(StringComparer.Ordinal);

    public Tf2SourceWorldState World { get; } = new();

    public List<PendingSourceServerCommand> PendingSourceServerCommands { get; } = [];

    public List<PendingSourceServerCommand> SourceServerCommandHistory { get; } = [];

    public List<SourceServerEvent> SourceServerEventHistory { get; } = [];

    public List<SourceServerBan> SourceServerBans { get; } = [];

    public SourceServerVote? CurrentSourceVote { get; set; }

    public bool NativeSourceLevelInitEmitted { get; private set; }

    public bool SourceLoggingEnabled { get; private set; }

    public string SourceLogPath { get; private set; } = System.IO.Path.Combine(Environment.CurrentDirectory, "logs", "source-server-events.log");

    public string SourceLogLastError { get; private set; } = "";

    public List<string> SourceLogAddresses { get; } = [];

    public string NativeRankedStatsExportPath { get; private set; } = "";

    public string MapMetadataPath { get; private set; } = "";

    public Tf2MapMetadataCatalog MapMetadataCatalog { get; private set; } = Tf2MapMetadataCatalog.Empty;

    public Tf2MapMetadata? CurrentMapMetadata => MapMetadataCatalog.Find(MapName);

    public string NativeSourceContentRootPath { get; private set; } = "";

    public Dictionary<string, string> SourceCvars { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> SourceConfigDirectories { get; } = [];

    public int ActiveSourcePlayerCount => ActivePlayers().Count(static player =>
        player.State == PlayerJoinState.SourceHandoff
        && player.NativeSourceResponder.SentObjectState);

    public int SourceVisiblePlayerCount => ActivePlayers().Count(static player =>
        player.State == PlayerJoinState.SourceHandoff);

    public int BotCount => ActivePlayers().Count(static player => player.IsBot);

    public IEnumerable<PlayerSession> ActivePlayers()
    {
        return Players.Values.Where(static player => player.State != PlayerJoinState.Left);
    }

    public int ExpireInactiveGeneratedSourcePlayers(DateTimeOffset now, TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            return 0;
        }

        var expired = 0;
        foreach (var player in Players.Values)
        {
            if (player.State != PlayerJoinState.SourceHandoff
                || !player.NativeSourceResponder.SentObjectState
                || now - player.LastSeen <= timeout)
            {
                continue;
            }

            if (MarkNativeSourcePlayerDisconnected(player, "inactive generated Source player timeout"))
            {
                expired++;
            }
        }

        return expired;
    }

    public void ApplySourceLaunchProfile(Tf2SourceLaunchProfile profile)
    {
        var oldMapName = MapName;
        var sourceLevelWasRunning = NativeSourceLevelInitEmitted;
        var options = profile.ToSessionOptions(new GameManagerSessionOptions(
            LocalId,
            GameId,
            MaxPlayers,
            MapName,
            Name,
            PreferredPlayerId,
            PreferredPlayerName,
            JoinTicket,
            EncryptionKey,
            UniqueGameId,
            ServerUid,
            GameMode,
            RankingMode,
            IsRanked,
            TimeLimitMinutes,
            MaxRounds,
            FlagCaptureLimit,
            AutoBalance,
            AdvertisedHost,
            AdvertisedPort,
            NativeRankedStatsExportPath,
            MapMetadataPath,
            NativeSourceContentRootPath));

        LocalId = options.LocalId;
        GameId = options.GameId;
        MaxPlayers = options.MaxPlayers;
        MapName = options.MapName;
        NextMapName = NextMapAfter(MapName);
        Name = options.Name;
        PreferredPlayerId = options.PreferredPlayerId;
        PreferredPlayerName = options.PreferredPlayerName;
        JoinTicket = options.JoinTicket;
        EncryptionKey = options.EncryptionKey;
        UniqueGameId = options.UniqueGameId;
        ServerUid = options.ServerUid;
        GameMode = string.IsNullOrWhiteSpace(profile.GameMode) ? options.GameMode : profile.GameMode;
        RankingMode = string.IsNullOrWhiteSpace(profile.RankingMode) ? options.RankingMode : profile.RankingMode;
        IsRanked = profile.IsRanked;
        TimeLimitMinutes = profile.TimeLimitMinutes ?? options.TimeLimitMinutes;
        MaxRounds = profile.MaxRounds ?? options.MaxRounds;
        FlagCaptureLimit = profile.FlagCaptureLimit ?? options.FlagCaptureLimit;
        AutoBalance = profile.AutoBalance ?? options.AutoBalance;
        AdvertisedHost = options.AdvertisedHost;
        AdvertisedPort = options.AdvertisedPort;
        NativeRankedStatsExportPath = options.NativeRankedStatsExportPath;
        MapMetadataPath = options.MapMetadataPath;
        NativeSourceContentRootPath = options.NativeSourceContentRootPath;
        var mapChanged = !string.Equals(oldMapName, MapName, StringComparison.OrdinalIgnoreCase);
        if (mapChanged && sourceLevelWasRunning)
        {
            EmitNativeSourceLevelShutdown(oldMapName);
        }

        var players = ActivePlayers().ToList();
        World.ApplyMapDefaults(MapName, GameMode, TimeLimitMinutes, MaxRounds, FlagCaptureLimit, AutoBalance, players, CurrentMapMetadata);
        for (var index = 0; index < players.Count; index++)
        {
            players[index].SourceState.ApplyNativeSourceObjectIds(players[index].PlayerId);
            players[index].SourceState.ApplyMapDefaults(MapName, index, forceSpawnReset: true, CurrentMapMetadata);
        }

        World.UpdateTeamMemberCounts(players);
        SyncStructuredCvars();
        if (mapChanged && sourceLevelWasRunning)
        {
            NativeSourceLevelInitEmitted = false;
            EmitNativeSourceLevelStart();
        }
    }

    public void ApplyNativeSourceMapSettings(
        string? mapName = null,
        string? gameMode = null,
        int? timeLimitMinutes = null,
        int? maxRounds = null,
        int? flagCaptureLimit = null,
        bool? autoBalance = null,
        bool resetPlayerSpawns = false)
    {
        var oldMapName = MapName;
        var sourceLevelWasRunning = NativeSourceLevelInitEmitted;
        var mapChanged = !string.IsNullOrWhiteSpace(mapName)
            && !string.Equals(mapName.Trim(), oldMapName, StringComparison.OrdinalIgnoreCase);
        if (mapChanged && sourceLevelWasRunning)
        {
            EmitNativeSourceLevelShutdown(oldMapName);
        }

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            MapName = mapName.Trim();
            GameMode = string.IsNullOrWhiteSpace(gameMode) ? InferGameMode(MapName) : gameMode.Trim();
            NextMapName = NextMapAfter(MapName);
        }
        else if (!string.IsNullOrWhiteSpace(gameMode))
        {
            GameMode = gameMode.Trim();
        }

        if (timeLimitMinutes is { } newTimeLimit)
        {
            TimeLimitMinutes = Math.Clamp(newTimeLimit, 0, 240);
        }

        if (maxRounds is { } newMaxRounds)
        {
            MaxRounds = Math.Clamp(newMaxRounds, 0, 99);
        }

        if (flagCaptureLimit is { } newFlagCaptureLimit)
        {
            FlagCaptureLimit = Math.Clamp(newFlagCaptureLimit, 0, 99);
        }

        if (autoBalance is { } newAutoBalance)
        {
            AutoBalance = newAutoBalance;
        }

        var players = ActivePlayers().ToList();
        World.ApplyMapDefaults(MapName, GameMode, TimeLimitMinutes, MaxRounds, FlagCaptureLimit, AutoBalance, players, CurrentMapMetadata);
        for (var index = 0; index < players.Count; index++)
        {
            players[index].SourceState.ApplyMapDefaults(MapName, index, resetPlayerSpawns, CurrentMapMetadata);
        }

        World.UpdateTeamMemberCounts(players);
        SyncStructuredCvars();
        if (mapChanged && sourceLevelWasRunning)
        {
            NativeSourceLevelInitEmitted = false;
            EmitNativeSourceLevelStart();
        }
    }

    public string SetSourceCvar(string name, string value)
    {
        var normalizedName = NormalizeSourceCvarName(name);
        var normalizedValue = value.Trim();
        SourceCvars[normalizedName] = normalizedValue;
        World.ApplySourceCvar(normalizedName, normalizedValue);
        return normalizedName;
    }

    public bool TryGetSourceCvar(string name, out string value)
    {
        return SourceCvars.TryGetValue(NormalizeSourceCvarName(name), out value!);
    }

    public bool ApplyDecodedNativeSourceClientState(PlayerSession player)
    {
        ArgumentNullException.ThrowIfNull(player);

        var changed = false;
        foreach (var pair in player.SourceState.LastClientSetConVars)
        {
            player.SourceClientCvars[pair.Key] = pair.Value;
            if (string.Equals(pair.Key, "name", StringComparison.OrdinalIgnoreCase))
            {
                changed |= TryApplyNativeClientPlayerName(player, pair.Value, "NET_SetConVar");
            }
        }

        if (!string.IsNullOrWhiteSpace(player.SourceState.LastClientInfoFriendsName))
        {
            changed |= TryApplyNativeClientPlayerName(
                player,
                player.SourceState.LastClientInfoFriendsName!,
                "CLC_ClientInfo");
        }

        if (!string.IsNullOrWhiteSpace(player.SourceState.LastClientStringCommand))
        {
            changed |= TryApplyNativeClientStringCommand(player, player.SourceState.LastClientStringCommand!);
        }

        if (changed)
        {
            World.UpdateTeamMemberCounts(ActivePlayers());
        }

        return changed;
    }

    private bool TryApplyNativeClientPlayerName(PlayerSession player, string value, string source)
    {
        var newName = NormalizeNativeClientPlayerName(value);
        if (newName.Length == 0
            || string.Equals(player.Name, newName, StringComparison.Ordinal))
        {
            return false;
        }

        var oldName = player.Name;
        player.Name = newName;
        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            $"PlayerNameChange player={oldName} id={player.PlayerId} newName={player.Name} source={source}",
            targetEndpoint: player.Endpoint,
            issuedBy: "server.dll");
        return true;
    }

    private bool TryApplyNativeClientStringCommand(PlayerSession player, string command)
    {
        var tokens = TokenizeNativeClientCommand(command);
        if (tokens.Count == 0)
        {
            return false;
        }

        switch (tokens[0].ToLowerInvariant())
        {
            case "name" when tokens.Count >= 2:
                return TryApplyNativeClientPlayerName(player, string.Join(' ', tokens.Skip(1)), "NET_StringCmd");

            case "jointeam" when tokens.Count >= 2:
                return TryApplyNativeClientJoinTeam(player, tokens[1]);

            case "joinclass" when tokens.Count >= 2:
                return TryApplyNativeClientJoinClass(player, tokens[1]);

            default:
                return false;
        }
    }

    private bool TryApplyNativeClientJoinTeam(PlayerSession player, string teamText)
    {
        var teamNumber = teamText.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? LeastPopulatedNativeClientTeam(player)
            : TryParseNativeClientTeam(teamText, out var parsedTeam)
                ? parsedTeam
                : uint.MaxValue;
        if (teamNumber == uint.MaxValue
            || player.SourceState.TeamNumber == teamNumber)
        {
            return false;
        }

        player.SourceState.SetTeam(teamNumber);
        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            $"ClientCommand jointeam player={player.Name} id={player.PlayerId} team={NativeTeamName(teamNumber)}",
            targetEndpoint: player.Endpoint,
            issuedBy: "server.dll");
        return true;
    }

    private bool TryApplyNativeClientJoinClass(PlayerSession player, string classText)
    {
        if (!TryParseNativeClientClass(classText, out var classNumber)
            || player.SourceState.ClassNumber == classNumber)
        {
            return false;
        }

        player.SourceState.SetClass(classNumber);
        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            $"ClientCommand joinclass player={player.Name} id={player.PlayerId} class={NativeClassName(classNumber)}",
            targetEndpoint: player.Endpoint,
            issuedBy: "server.dll");
        return true;
    }

    private uint LeastPopulatedNativeClientTeam(PlayerSession player)
    {
        var redCount = ActivePlayers().Count(candidate => candidate != player && candidate.SourceState.TeamNumber == 2);
        var bluCount = ActivePlayers().Count(candidate => candidate != player && candidate.SourceState.TeamNumber == 3);
        return redCount <= bluCount ? 2U : 3U;
    }

    private static string NormalizeNativeClientPlayerName(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return "";
        }

        var builder = new StringBuilder(Math.Min(trimmed.Length, 31));
        var alphaNumericCount = 0;
        var replacementCount = 0;
        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch) || ch < 0x20 || ch > 0x7e)
            {
                continue;
            }

            if (ch == '?')
            {
                replacementCount++;
            }

            if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '[' or ']' or '(' or ')' or ' ')
            {
                alphaNumericCount++;
            }

            builder.Append(ch);
            if (builder.Length >= 31)
            {
                break;
            }
        }

        var normalized = builder.ToString().Trim();
        if (normalized.Length == 0)
        {
            return "";
        }

        // ClientInfo/StringCmd decoding can false-positive on markerless binary
        // source packets. Do not let those bytes replace the roster/player name.
        if (alphaNumericCount < Math.Max(2, normalized.Length / 2)
            || replacementCount > Math.Max(0, normalized.Length / 4))
        {
            return "";
        }

        return normalized;
    }

    private static IReadOnlyList<string> TokenizeNativeClientCommand(string command)
    {
        var tokens = new List<string>();
        var token = new StringBuilder();
        var inQuote = false;
        foreach (var ch in command)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuote)
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }

                continue;
            }

            token.Append(ch);
        }

        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }

        return tokens;
    }

    private static bool TryParseNativeClientTeam(string value, out uint teamNumber)
    {
        teamNumber = 0;
        switch (value.Trim().ToLowerInvariant())
        {
            case "0":
            case "unassigned":
            case "none":
                teamNumber = 0;
                return true;
            case "1":
            case "spectator":
            case "spectate":
            case "spec":
                teamNumber = 1;
                return true;
            case "2":
            case "red":
                teamNumber = 2;
                return true;
            case "3":
            case "blue":
            case "blu":
                teamNumber = 3;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseNativeClientClass(string value, out byte classNumber)
    {
        classNumber = 0;
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "scout":
                classNumber = 1;
                return true;
            case "2":
            case "sniper":
                classNumber = 2;
                return true;
            case "3":
            case "soldier":
                classNumber = 3;
                return true;
            case "4":
            case "demo":
            case "demoman":
                classNumber = 4;
                return true;
            case "5":
            case "medic":
                classNumber = 5;
                return true;
            case "6":
            case "heavy":
            case "heavyweapons":
                classNumber = 6;
                return true;
            case "7":
            case "pyro":
                classNumber = 7;
                return true;
            case "8":
            case "spy":
                classNumber = 8;
                return true;
            case "9":
            case "engineer":
            case "engie":
                classNumber = 9;
                return true;
            default:
                return false;
        }
    }

    private static string NativeTeamName(uint teamNumber)
    {
        return teamNumber switch
        {
            1 => "Spectator",
            2 => "RED",
            3 => "BLU",
            _ => "Unassigned"
        };
    }

    private static string NativeClassName(byte classNumber)
    {
        return classNumber switch
        {
            1 => "Scout",
            2 => "Sniper",
            3 => "Soldier",
            4 => "Demoman",
            5 => "Medic",
            6 => "Heavy",
            7 => "Pyro",
            8 => "Spy",
            9 => "Engineer",
            _ => "Unknown"
        };
    }

    public PlayerSession GetOrAddPlayer(string endpoint)
    {
        if (Players.TryGetValue(endpoint, out var player))
        {
            return player;
        }

        var playerIndex = Players.Count;
        player = new PlayerSession
        {
            Endpoint = endpoint,
            PlayerId = playerIndex == 0 && PreferredPlayerId > 0 ? PreferredPlayerId : playerIndex + 1,
            Name = playerIndex == 0 && !string.IsNullOrWhiteSpace(PreferredPlayerName)
                ? PreferredPlayerName
                : $"player{playerIndex + 1}"
        };
        player.SourceState.ApplyNativeSourceObjectIds(player.PlayerId);
        player.SourceState.ApplyMapDefaults(MapName, playerIndex, forceSpawnReset: true, CurrentMapMetadata);
        Players.Add(endpoint, player);
        World.UpdateTeamMemberCounts(ActivePlayers());
        return player;
    }

    public PlayerSession AddBot(string? name = null, byte? classNumber = null, uint? teamNumber = null)
    {
        var botId = _nextBotId++;
        var endpoint = $"bot:{botId}";
        while (Players.ContainsKey(endpoint))
        {
            botId = _nextBotId++;
            endpoint = $"bot:{botId}";
        }

        var activePlayers = ActivePlayers().ToArray();
        var botPlayerId = NextBotPlayerId();
        var bot = new PlayerSession
        {
            Endpoint = endpoint,
            PlayerId = botPlayerId,
            Name = string.IsNullOrWhiteSpace(name) ? $"TFBot{botId}" : name.Trim(),
            IsBot = true,
            State = PlayerJoinState.SourceHandoff,
            LastSeen = DateTimeOffset.UtcNow
        };
        bot.NativeSourceResponder.SentObjectState = true;
        bot.NativeSourceResponder.SentRosterDescriptorState = true;
        bot.SourceState.ApplyNativeSourceObjectIds(bot.PlayerId);
        bot.SourceState.ApplyMapDefaults(MapName, activePlayers.Length, forceSpawnReset: true, CurrentMapMetadata);
        if (teamNumber is { } team)
        {
            bot.SourceState.SetTeam(team);
        }

        if (classNumber is { } @class)
        {
            bot.SourceState.SetClass(@class);
        }

        Players.Add(endpoint, bot);
        World.UpdateTeamMemberCounts(ActivePlayers());
        SyncBotCvars();
        return bot;
    }

    public int RemoveBots(string? selector = null)
    {
        var removed = 0;
        foreach (var bot in Players.Values)
        {
            if (!bot.IsBot || bot.State == PlayerJoinState.Left)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(selector)
                && !string.Equals(selector, "all", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(bot.Name, selector, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(bot.Endpoint, selector, StringComparison.OrdinalIgnoreCase)
                && bot.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) != selector)
            {
                continue;
            }

            if (MarkNativeSourcePlayerDisconnected(bot, "bot removed"))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            SyncBotCvars();
        }

        return removed;
    }

    public int SetBotQuota(int quota)
    {
        quota = Math.Clamp(quota, 0, Math.Max(0, MaxPlayers - 1));
        while (BotCount < quota)
        {
            AddBot();
        }

        while (BotCount > quota)
        {
            if (RemoveBots(ActivePlayers().LastOrDefault(static player => player.IsBot)?.Endpoint) == 0)
            {
                break;
            }
        }

        SyncBotCvars();
        return BotCount;
    }

    public IReadOnlyList<string> AvailableMaps => DefaultMapRotation;

    public void SetNextMap(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            throw new ArgumentException("Next map cannot be empty.", nameof(mapName));
        }

        NextMapName = mapName.Trim();
        SourceCvars["nextlevel"] = NextMapName;
    }

    public string NextMapAfter(string mapName)
    {
        var index = Array.FindIndex(
            DefaultMapRotation,
            candidate => string.Equals(candidate, mapName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return DefaultMapRotation[0];
        }

        return DefaultMapRotation[(index + 1) % DefaultMapRotation.Length];
    }

    private static string InferGameMode(string mapName)
    {
        var normalized = mapName.Trim().ToLowerInvariant();
        if (normalized.StartsWith("ctf_", StringComparison.Ordinal))
        {
            return "capture-the-flag";
        }

        if (normalized.StartsWith("cp_", StringComparison.Ordinal)
            || normalized.StartsWith("tc_", StringComparison.Ordinal))
        {
            return "control-point";
        }

        if (normalized.StartsWith("arena_", StringComparison.Ordinal))
        {
            return "arena";
        }

        return "unknown";
    }

    private int NextBotPlayerId()
    {
        var minimum = PreferredPlayerId > 0 ? PreferredPlayerId + 1 : 198;
        _nextBotPlayerId = Math.Max(_nextBotPlayerId, minimum);
        while (Players.Values.Any(player => player.PlayerId == _nextBotPlayerId))
        {
            _nextBotPlayerId++;
        }

        return _nextBotPlayerId++;
    }

    public PendingSourceServerCommand QueueSourceServerCommand(
        SourceServerCommandType type,
        string text,
        string? targetEndpoint = null,
        string issuedBy = "admin")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Command text cannot be empty.", nameof(text));
        }

        var command = new PendingSourceServerCommand(
            _nextSourceServerCommandId++,
            DateTimeOffset.UtcNow,
            type,
            text.Trim(),
            string.IsNullOrWhiteSpace(targetEndpoint) ? null : targetEndpoint.Trim(),
            string.IsNullOrWhiteSpace(issuedBy) ? "admin" : issuedBy.Trim());
        PendingSourceServerCommands.Add(command);
        SourceServerCommandHistory.Add(command);
        RecordSourceServerEvent(SourceServerEvent.FromCommand(command));
        if (SourceServerCommandHistory.Count > 256)
        {
            SourceServerCommandHistory.RemoveRange(0, SourceServerCommandHistory.Count - 256);
        }

        return command;
    }

    public void RecordSourceServerEvent(SourceServerEvent serverEvent)
    {
        SourceServerEventHistory.Add(serverEvent);
        if (SourceServerEventHistory.Count > 512)
        {
            SourceServerEventHistory.RemoveRange(0, SourceServerEventHistory.Count - 512);
        }

        if (SourceLoggingEnabled)
        {
            AppendSourceLog(serverEvent);
        }
    }

    public SourceServerEvent RecordGeneratedSourceServerEvent(
        SourceServerCommandType type,
        string text,
        string? targetEndpoint = null,
        string issuedBy = "server")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Event text cannot be empty.", nameof(text));
        }

        var serverEvent = new SourceServerEvent(
            _nextSourceServerCommandId++,
            DateTimeOffset.UtcNow,
            type,
            SourceServerEvent.ChannelFor(type),
            text.Trim(),
            string.IsNullOrWhiteSpace(targetEndpoint) ? null : targetEndpoint.Trim(),
            string.IsNullOrWhiteSpace(issuedBy) ? "server" : issuedBy.Trim());
        RecordSourceServerEvent(serverEvent);
        return serverEvent;
    }

    public void EnsureNativeSourceLifecycle(PlayerSession player)
    {
        EmitNativeSourceLevelStart();

        if (player.NativeSourceLifecycleConnectedEventEmitted)
        {
            return;
        }

        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CBaseGameStats::Event_PlayerConnected [{player.Name}]"),
            targetEndpoint: player.Endpoint,
            issuedBy: "server.dll");
        player.NativeSourceLifecycleConnectedEventEmitted = true;
    }

    public bool MarkNativeSourcePlayerDisconnected(PlayerSession player, string reason)
    {
        if (player.State == PlayerJoinState.Left)
        {
            return false;
        }

        var shouldEmitSourceLifecycle = player.NativeSourceLifecycleConnectedEventEmitted
            || player.State == PlayerJoinState.SourceHandoff
            || player.NativeSourceResponder.SentObjectState;
        if (shouldEmitSourceLifecycle && !player.NativeSourceLifecycleDisconnectedEventEmitted)
        {
            RecordGeneratedSourceServerEvent(
                SourceServerCommandType.GameEvent,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CTFGameRules: ClientDisconnected: {player.Name}"),
                targetEndpoint: player.Endpoint,
                issuedBy: "server.dll");
            RecordGeneratedSourceServerEvent(
                SourceServerCommandType.GameEvent,
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CBaseGameStats::Event_PlayerDisconnected [{player.Name}] reason=\"{reason}\""),
                targetEndpoint: player.Endpoint,
                issuedBy: "server.dll");
            ExportNativeRankedStats(player, reason);
            player.NativeSourceLifecycleDisconnectedEventEmitted = true;
        }

        player.State = PlayerJoinState.Left;
        World.UpdateTeamMemberCounts(ActivePlayers());
        if (player.IsBot)
        {
            SyncBotCvars();
        }

        return true;
    }

    private void EmitNativeSourceLevelStart()
    {
        if (NativeSourceLevelInitEmitted)
        {
            return;
        }

        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CBaseGameStats::Event_LevelInit [{MapName}]"),
            issuedBy: "server.dll");
        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CBaseGameStats::Event_MapChange to [{MapName}]"),
            issuedBy: "server.dll");
        NativeSourceLevelInitEmitted = true;
    }

    private void EmitNativeSourceLevelShutdown(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return;
        }

        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"CBaseGameStats::Event_LevelShutdown [{mapName}] 0.00 elapsed 0 total"),
            issuedBy: "server.dll");
        foreach (var player in ActivePlayers())
        {
            ExportNativeRankedStats(player, "level shutdown");
        }
    }

    public void SetNativeRankedStatsExportPath(string path)
    {
        NativeRankedStatsExportPath = string.IsNullOrWhiteSpace(path)
            ? ""
            : System.IO.Path.GetFullPath(path.Trim());
    }

    public bool ExportNativeRankedStats(PlayerSession player, string reason)
    {
        if (string.IsNullOrWhiteSpace(NativeRankedStatsExportPath)
            || !Tf2RankedStatsExporter.TryAppend(NativeRankedStatsExportPath, this, player, reason))
        {
            return false;
        }

        RecordGeneratedSourceServerEvent(
            SourceServerCommandType.GameEvent,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"TFStatsReporter::_PlayerStatsLookupCallback FeslStatsRetriever ranked stats exported player={player.Name} reason=\"{reason}\" path=\"{NativeRankedStatsExportPath}\""),
            targetEndpoint: player.Endpoint,
            issuedBy: "server.dll");
        return true;
    }

    public void SetSourceLogging(bool enabled)
    {
        SourceLoggingEnabled = enabled;
        if (enabled)
        {
            EnsureSourceLogDirectory();
        }
    }

    public void SetSourceLogPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Log path cannot be empty.", nameof(path));
        }

        SourceLogPath = System.IO.Path.GetFullPath(path.Trim());
        if (SourceLoggingEnabled)
        {
            EnsureSourceLogDirectory();
        }
    }

    public void ClearSourceLog()
    {
        try
        {
            EnsureSourceLogDirectory();
            System.IO.File.WriteAllText(SourceLogPath, string.Empty);
            SourceLogLastError = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            SourceLogLastError = ex.Message;
        }
    }

    public bool AddSourceLogAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Trim();
        if (SourceLogAddresses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        SourceLogAddresses.Add(normalized);
        return true;
    }

    public bool RemoveSourceLogAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Trim();
        return SourceLogAddresses.RemoveAll(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    private void EnsureSourceLogDirectory()
    {
        var directory = System.IO.Path.GetDirectoryName(SourceLogPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
    }

    private void AppendSourceLog(SourceServerEvent serverEvent)
    {
        try
        {
            EnsureSourceLogDirectory();
            var line = FormatSourceLogLine(serverEvent);
            System.IO.File.AppendAllText(SourceLogPath, line + Environment.NewLine);
            SourceLogLastError = ForwardSourceLogLine(line) ?? "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or SocketException)
        {
            SourceLogLastError = ex.Message;
        }
    }

    private static string FormatSourceLogLine(SourceServerEvent serverEvent)
    {
        return string.Join(
            '\t',
            serverEvent.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            serverEvent.Channel,
            serverEvent.Type.ToString(),
            serverEvent.IssuedBy,
            serverEvent.TargetEndpoint ?? "all",
            serverEvent.Text.Replace('\r', ' ').Replace('\n', ' '));
    }

    private string? ForwardSourceLogLine(string line)
    {
        if (SourceLogAddresses.Count == 0)
        {
            return null;
        }

        var payload = Encoding.UTF8.GetBytes(line);
        using var client = new UdpClient();
        string? lastError = null;
        foreach (var address in SourceLogAddresses)
        {
            if (!TryParseHostPort(address, out var host, out var port))
            {
                lastError = $"Invalid logaddress target: {address}";
                continue;
            }

            client.Send(payload, payload.Length, host, port);
        }

        return lastError;
    }

    private static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = "";
        port = 0;
        var separator = value.LastIndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        host = value[..separator].Trim().Trim('[', ']');
        return host.Length > 0
            && int.TryParse(value[(separator + 1)..], out port)
            && port is > 0 and <= IPEndPoint.MaxPort;
    }

    public PendingSourceServerCommand[] DrainPendingSourceServerCommands(string endpoint, int maxCount = 4)
    {
        if (maxCount <= 0 || PendingSourceServerCommands.Count == 0)
        {
            return [];
        }

        var drained = new List<PendingSourceServerCommand>(Math.Min(maxCount, PendingSourceServerCommands.Count));
        for (var index = 0; index < PendingSourceServerCommands.Count && drained.Count < maxCount;)
        {
            var command = PendingSourceServerCommands[index];
            if (command.TargetEndpoint is null
                || string.Equals(command.TargetEndpoint, endpoint, StringComparison.Ordinal))
            {
                drained.Add(command);
                PendingSourceServerCommands.RemoveAt(index);
                continue;
            }

            index++;
        }

        return drained.ToArray();
    }

    public SourceServerBan AddSourceBan(
        string selector,
        PlayerSession? player,
        int durationMinutes,
        string reason,
        string issuedBy)
    {
        var ban = new SourceServerBan(
            _nextSourceBanId++,
            DateTimeOffset.UtcNow,
            durationMinutes <= 0 ? null : durationMinutes,
            selector.Trim(),
            player?.Endpoint,
            player?.PlayerId,
            player?.Name,
            string.IsNullOrWhiteSpace(reason) ? "Banned by server admin" : reason.Trim(),
            string.IsNullOrWhiteSpace(issuedBy) ? "admin" : issuedBy.Trim());
        SourceServerBans.RemoveAll(existing =>
            string.Equals(existing.Selector, ban.Selector, StringComparison.OrdinalIgnoreCase)
            || (ban.Endpoint is not null && string.Equals(existing.Endpoint, ban.Endpoint, StringComparison.OrdinalIgnoreCase))
            || (ban.PlayerId is not null && existing.PlayerId == ban.PlayerId));
        SourceServerBans.Add(ban);
        return ban;
    }

    public bool RemoveSourceBan(string selector, out SourceServerBan removed)
    {
        removed = null!;
        if (string.IsNullOrWhiteSpace(selector))
        {
            return false;
        }

        selector = selector.Trim().Trim('"');
        var index = SourceServerBans.FindIndex(ban =>
            ban.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) == selector
            || string.Equals(ban.Selector, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ban.Endpoint, selector, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ban.PlayerName, selector, StringComparison.OrdinalIgnoreCase)
            || (ban.PlayerId is not null && ban.PlayerId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) == selector));
        if (index < 0)
        {
            return false;
        }

        removed = SourceServerBans[index];
        SourceServerBans.RemoveAt(index);
        return true;
    }

    public void ExpireSourceBans(DateTimeOffset now)
    {
        SourceServerBans.RemoveAll(ban => ban.ExpiresAt is { } expiresAt && expiresAt <= now);
    }

    private void InitializeSourceCvars()
    {
        SourceCvars["hostname"] = Name;
        SourceCvars["mapname"] = MapName;
        SourceCvars["nextlevel"] = NextMapName;
        SourceCvars["maxplayers"] = MaxPlayers.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_timelimit"] = TimeLimitMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_maxrounds"] = MaxRounds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["tf_flag_caps_per_round"] = FlagCaptureLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_autoteambalance"] = AutoBalance ? "1" : "0";
        SourceCvars["mp_teams_unbalance_limit"] = AutoBalance ? "1" : "0";
        SourceCvars["mp_friendlyfire"] = "0";
        SourceCvars["mp_forcecamera"] = "0";
        SourceCvars["mp_disable_respawn_times"] = "0";
        SourceCvars["mp_tournament"] = "0";
        SourceCvars["mp_tournament_readymode"] = "0";
        SourceCvars["mp_tournament_restart"] = "0";
        SourceCvars["tf_weapon_criticals"] = "1";
        SourceCvars["tf_damage_disablespread"] = "0";
        SourceCvars["sv_alltalk"] = "0";
        SourceCvars["sv_cheats"] = "0";
        SourceCvars["sv_gravity"] = "800";
        SourceCvars["sv_lan"] = "1";
        SourceCvars["sv_password"] = "";
        SourceCvars["sv_pure"] = "0";
        SourceCvars["sv_tags"] = "tf2ps3,plasma";
        SourceCvars["tf_bot_quota"] = "0";
        SourceCvars["tf_bot_difficulty"] = "1";
        SourceCvars["tf_bot_join_after_player"] = "0";
    }

    private void InitializeSourceConfigDirectories()
    {
        AddSourceConfigDirectory(System.IO.Path.Combine(Environment.CurrentDirectory, "cfg"));
        AddSourceConfigDirectory(System.IO.Path.Combine(AppContext.BaseDirectory, "cfg"));
    }

    public void AddSourceConfigDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = System.IO.Path.GetFullPath(path.Trim());
        if (!SourceConfigDirectories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
        {
            SourceConfigDirectories.Add(fullPath);
        }
    }

    private void SyncStructuredCvars()
    {
        SourceCvars["hostname"] = Name;
        SourceCvars["mapname"] = MapName;
        SourceCvars["nextlevel"] = NextMapName;
        SourceCvars["maxplayers"] = MaxPlayers.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_timelimit"] = TimeLimitMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_maxrounds"] = MaxRounds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["tf_flag_caps_per_round"] = FlagCaptureLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SourceCvars["mp_autoteambalance"] = AutoBalance ? "1" : "0";
        SourceCvars["mp_teams_unbalance_limit"] = AutoBalance ? "1" : "0";
        SourceCvars["mp_tournament"] = World.TournamentMode ? "1" : "0";
        SourceCvars["mp_tournament_readymode"] = World.TournamentReadymode ? "1" : "0";
    }

    private void SyncBotCvars()
    {
        SourceCvars["tf_bot_quota"] = BotCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string NormalizeSourceCvarName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cvar name cannot be empty.", nameof(name));
        }

        return name.Trim().ToLowerInvariant();
    }
}

public enum SourceServerCommandType
{
    ConsoleCommand,
    Chat,
    TeamChat,
    PrivateChat,
    CenterMessage,
    HintMessage,
    PanelMessage,
    HudMessage,
    Sound,
    GameEvent,
    DeathNotice,
    Vote,
    ClearChat
}

public sealed record PendingSourceServerCommand(
    long Id,
    DateTimeOffset CreatedAt,
    SourceServerCommandType Type,
    string Text,
    string? TargetEndpoint,
    string IssuedBy);

public sealed record SourceServerEvent(
    long Id,
    DateTimeOffset CreatedAt,
    SourceServerCommandType Type,
    string Channel,
    string Text,
    string? TargetEndpoint,
    string IssuedBy)
{
    public static SourceServerEvent FromCommand(PendingSourceServerCommand command)
    {
        return new SourceServerEvent(
            command.Id,
            command.CreatedAt,
            command.Type,
            ChannelFor(command.Type),
            command.Text,
            command.TargetEndpoint,
            command.IssuedBy);
    }

    public static string ChannelFor(SourceServerCommandType type)
    {
        return type switch
        {
            SourceServerCommandType.Chat => "chat",
            SourceServerCommandType.TeamChat => "team-chat",
            SourceServerCommandType.PrivateChat => "private-chat",
            SourceServerCommandType.CenterMessage => "center",
            SourceServerCommandType.HintMessage => "hint",
            SourceServerCommandType.PanelMessage => "panel",
            SourceServerCommandType.HudMessage => "hud",
            SourceServerCommandType.Sound => "sound",
            SourceServerCommandType.GameEvent => "game-event",
            SourceServerCommandType.DeathNotice => "death",
            SourceServerCommandType.Vote => "vote",
            SourceServerCommandType.ClearChat => "clear-chat",
            _ => "console"
        };
    }
}

public sealed class SourceServerVote
{
    public SourceServerVote(long id, DateTimeOffset createdAt, string issue, string question, string? target, string initiatedBy)
    {
        Id = id;
        CreatedAt = createdAt;
        Issue = issue;
        Question = question;
        Target = target;
        InitiatedBy = initiatedBy;
    }

    public long Id { get; }

    public DateTimeOffset CreatedAt { get; }

    public string Issue { get; }

    public string Question { get; }

    public string? Target { get; }

    public string InitiatedBy { get; }

    public Dictionary<string, bool> Ballots { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int YesVotes => Ballots.Values.Count(static value => value);

    public int NoVotes => Ballots.Values.Count(static value => !value);
}

public sealed record SourceServerBan(
    long Id,
    DateTimeOffset CreatedAt,
    int? DurationMinutes,
    string Selector,
    string? Endpoint,
    int? PlayerId,
    string? PlayerName,
    string Reason,
    string IssuedBy)
{
    public DateTimeOffset? ExpiresAt => DurationMinutes is { } minutes
        ? CreatedAt.AddMinutes(minutes)
        : null;
}

public sealed record GameManagerSessionOptions(
    long LocalId = 257,
    long GameId = 800001,
    int MaxPlayers = 24,
    string MapName = "ctf_2fort",
    string Name = "TF2 PS3 Native GM",
    int PreferredPlayerId = 0,
    string PreferredPlayerName = "",
    string JoinTicket = "",
    string EncryptionKey = "",
    string UniqueGameId = "",
    ulong ServerUid = 0,
    string GameMode = "capture-the-flag",
    string RankingMode = "Unranked",
    bool IsRanked = false,
    int TimeLimitMinutes = 30,
    int MaxRounds = 5,
    int FlagCaptureLimit = 3,
    bool AutoBalance = true,
    string AdvertisedHost = "127.0.0.1",
    int AdvertisedPort = 27015,
    string NativeRankedStatsExportPath = "",
    string MapMetadataPath = "",
    string NativeSourceContentRootPath = "")
{
    public static GameManagerSessionOptions Default { get; } = new();
}

public sealed class PlayerSession
{
    public required string Endpoint { get; init; }

    public int PlayerId { get; init; }

    public string Name { get; set; } = "player";

    public bool IsBot { get; init; }

    public bool VoiceMuted { get; set; }

    public bool ChatGagged { get; set; }

    public bool TournamentReady { get; set; }

    public Dictionary<string, string> SourceClientCvars { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int LastTransactionId { get; set; }

    public PlayerJoinState State { get; set; }

    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    public int ExpectedRosterNoticeCount { get; set; } = 1;

    public int ProcessedRosterNoticeCount { get; set; }

    public bool HostRosterAckSent { get; set; }

    public Ps3SourceGameplaySession SourceGameplay { get; } = new();

    public Ps3NativeSourceResponderState NativeSourceResponder { get; } = new();

    public Tf2SourcePlayerState SourceState { get; } = new();

    public bool NativeSourceLifecycleConnectedEventEmitted { get; set; }

    public bool NativeSourceLifecycleDisconnectedEventEmitted { get; set; }
}

public sealed class Ps3NativeSourceResponderState
{
    public bool IsSeeded { get; set; }

    public ushort NextServerSequence { get; set; }

    public int ClientPacketCount { get; set; }

    public int ServerPacketCount { get; set; }

    public bool SentInitialSetup { get; set; }

    public int InitialSetupVariant { get; set; }

    public int InitialSetupContinuationStage { get; set; }

    public int SuppressInitialSetupContinuationResponses { get; set; }

    public bool SentServerInfo { get; set; }

    public bool SentCriticalSourceNetMessageBootstrap { get; set; }

    public bool SentQuickMatchSetupContinuation { get; set; }

    public bool SawInitialClientFrozenStateUpload { get; set; }

    public bool PendingInitialClientFrozenStateUpload { get; set; }

    public bool SentObjectState { get; set; }

    public int ObjectStateIntroBatchIndex { get; set; }

    public int SuppressObjectStateIntroResponses { get; set; }

    public int LoadingStateLinkHeartbeatIndex { get; set; }

    public bool SentLoadingMotdEvent { get; set; }

    public bool SentLoadingStateLinkBurst { get; set; }

    public int LoadingStateLinkBurstClientPacketCount { get; set; }

    public bool SentLoadingPostBurstContinuation { get; set; }

    public int LoadingContinuationStage { get; set; }

    public int SuppressLoadingContinuationResponses { get; set; }

    public bool SentRosterDescriptorState { get; set; }

    public int SteadyStateLinkHeartbeatIndex { get; set; }

    public bool SentSteadySemanticBootstrapSnapshot { get; set; }

    public int PostRosterContinuationStage { get; set; }

    public bool PendingPostRosterFrozenStateUpload { get; set; }

    public bool SentPostRosterFrozenStateBatches { get; set; }

    public bool WaitingForPostRosterFrozenStateContinuation { get; set; }

    public bool SentPostRosterMapLoadClientBatchAck { get; set; }

    public bool SentPostRosterShortGameplayAck { get; set; }

    public bool SentLateLargeCommandPreamble { get; set; }

    public bool SentLateLargeCommandContinuation { get; set; }

    public bool SentLateLargeCommandFollowup { get; set; }

    public bool SentQuickMatchTerminalMapLoad { get; set; }

    public int QuickMatchTerminalPromptStage { get; set; }

    public int QuickMatchTerminalPrompt1WaitClientPacketCount { get; set; }

    public int LateLargeCommandFollowupClientPacketCount { get; set; }

    public int PostTerminalMapLoadClientPacketCount { get; set; }

    public bool SentPostTerminalMapLoadStateAck { get; set; }

    public uint? LastAcknowledgedReliableAssociationToken { get; set; }

    public Dictionary<(uint BaseCounter, byte TotalCount), Ps3NativeSourceClientFragmentAssembly> ClientFragmentAssemblies { get; } = [];

    public int SnapshotFrameIndex { get; set; } = 1;

    public int SnapshotBaseFrame { get; set; }

    public int LastCommandSnapshotClientPacketCount { get; set; }

    public uint FragmentSequenceCounter { get; set; } = 1;

    public string LastUrgentSourceStateSignature { get; set; } = "";

    public string LastScoreboardSourceStateSignature { get; set; } = "";
}

public sealed class Ps3NativeSourceClientFragmentAssembly
{
    private readonly byte[][] _fragments;

    public Ps3NativeSourceClientFragmentAssembly(byte totalCount, bool wrappedOrCompressed)
    {
        TotalCount = totalCount;
        WrappedOrCompressed = wrappedOrCompressed;
        _fragments = new byte[totalCount][];
    }

    public byte TotalCount { get; }

    public bool WrappedOrCompressed { get; private set; }

    public int ReceivedCount { get; private set; }

    public bool Add(byte fragmentIndex, ReadOnlySpan<byte> payload, bool wrappedOrCompressed)
    {
        if (fragmentIndex >= TotalCount)
        {
            return false;
        }

        WrappedOrCompressed |= wrappedOrCompressed;
        if (_fragments[fragmentIndex] is null)
        {
            ReceivedCount++;
        }

        _fragments[fragmentIndex] = payload.ToArray();
        return ReceivedCount == TotalCount;
    }

    public byte[] Reassemble()
    {
        var length = 0;
        foreach (var fragment in _fragments)
        {
            length += fragment?.Length ?? 0;
        }

        var result = new byte[length];
        var offset = 0;
        foreach (var fragment in _fragments)
        {
            if (fragment is null)
            {
                continue;
            }

            fragment.CopyTo(result.AsSpan(offset));
            offset += fragment.Length;
        }

        return result;
    }
}

public sealed class Tf2SourcePlayerState
{
    public const uint DefaultRootObjectId = 0x000000a1;

    public const uint DefaultLocalPlayerObjectId = 0x00000086;

    private const uint SourceFlagOnGround = 1U << 0;

    private const uint SourceFlagDucking = 1U << 1;

    private const uint SourceFlagFrozen = 1U << 5;

    private const uint SourceFlagClient = 1U << 7;

    private const uint SourceFlagInWater = 1U << 9;

    private const uint WorldGroundEntityHandle = 0;

    public Tf2SourcePlayerState()
    {
        ModelIndex = Tf2Ps3SourceCatalog.PlayerModelPrecacheIndexForClass(ClassNumber);
        WeaponViewModelIndex = Tf2Ps3SourceCatalog.WeaponViewModelPrecacheIndexForClass(ClassNumber);
        WeaponWorldModelIndex = Tf2Ps3SourceCatalog.WeaponWorldModelPrecacheIndexForClass(ClassNumber);
    }

    public uint RootObjectId { get; set; } = DefaultRootObjectId;

    public uint LocalPlayerObjectId { get; set; } = DefaultLocalPlayerObjectId;

    public uint[] FrozenStatePeerObjectIds { get; private set; } = [0x9f, 0x93, 0x95, 0x9c, 0x6d];

    public void ApplyFrozenStateClientUpload(uint rootObjectId, IReadOnlyList<uint> requestedObjectIds)
    {
        if (rootObjectId != 0)
        {
            RootObjectId = rootObjectId;
        }

        var peers = new List<uint>();
        foreach (var objectId in requestedObjectIds)
        {
            AddPeer(peers, objectId);
        }

        if (peers.Count == 0)
        {
            return;
        }

        if (peers.Contains(LocalPlayerObjectId))
        {
            FrozenStatePeerObjectIds = [.. peers.Where(objectId => objectId != LocalPlayerObjectId)];
            return;
        }

        FrozenStatePeerObjectIds = [.. peers];
    }

    public uint TeamNumber { get; set; } = 2;

    public byte ClassNumber { get; set; } = 1;

    public ushort Health { get; set; } = 125;

    public ushort MaxHealth { get; set; } = 125;

    public uint Score { get; set; }

    public uint Deaths { get; set; }

    public uint Captures { get; set; }

    public uint Defenses { get; set; }

    public uint KillAssists { get; set; }

    public uint Dominations { get; set; }

    public uint Revenge { get; set; }

    public uint BuildingsDestroyed { get; set; }

    public uint Headshots { get; set; }

    public uint Backstabs { get; set; }

    public uint HealPoints { get; set; }

    public uint Invulns { get; set; }

    public uint Teleports { get; set; }

    public uint ResupplyPoints { get; set; }

    public bool Alive { get; set; } = true;

    public bool MovementFrozen { get; set; }

    public bool NoClip { get; private set; }

    public byte MoveType { get; private set; } = 2;

    public byte MoveCollide { get; private set; }

    public uint CollisionGroup { get; private set; } = 5;

    public byte SolidType { get; private set; } = 2;

    public ushort SolidFlags { get; private set; }

    public uint Flags { get; private set; } = SourceFlagClient | SourceFlagOnGround;

    public uint Effects { get; private set; }

    public byte RenderMode { get; private set; }

    public byte RenderFx { get; private set; }

    public uint RenderColor { get; private set; } = 0xffffffff;

    public uint ModelIndex { get; private set; }

    public uint TextureFrameIndex { get; private set; }

    public uint Sequence { get; private set; }

    public uint ForceBone { get; private set; }

    public float ForceX { get; private set; }

    public float ForceY { get; private set; }

    public float ForceZ { get; private set; }

    public uint Skin { get; private set; }

    public uint Body { get; private set; }

    public uint HitboxSet { get; private set; }

    public float ModelWidthScale { get; private set; } = 1.0f;

    public float PlaybackRate { get; private set; } = 1.0f;

    public byte ClientSideAnimation { get; private set; }

    public byte ClientSideFrameReset { get; private set; }

    public uint NewSequenceParity { get; private set; }

    public uint ResetEventsParity { get; private set; }

    public byte MuzzleFlashParity { get; private set; }

    public uint LightingOriginHandle { get; private set; }

    public uint LightingOriginRelativeHandle { get; private set; }

    public float ServerAnimationCycle { get; private set; }

    public float FadeMinDistance { get; private set; }

    public float FadeMaxDistance { get; private set; }

    public float FadeScale { get; private set; } = 1.0f;

    public float OriginX { get; set; }

    public float OriginY { get; set; }

    public float OriginZ { get; set; }

    public float GroundZ { get; private set; }

    public float Yaw { get; set; }

    public float Pitch { get; set; }

    public float VelocityX { get; set; }

    public float VelocityY { get; set; }

    public float VelocityZ { get; set; }

    public float BaseVelocityX { get; set; }

    public float BaseVelocityY { get; set; }

    public float BaseVelocityZ { get; set; }

    public uint Fov { get; set; } = 75;

    public uint FovStart { get; set; } = 75;

    public float FovTime { get; set; }

    public uint DefaultFov { get; set; } = 75;

    public uint ObserverMode { get; set; }

    public uint ObserverTargetHandle { get; set; }

    public uint ViewModelHandle0 { get; set; }

    public uint ViewModelHandle1 { get; set; }

    public float ViewOffsetX { get; set; }

    public float ViewOffsetY { get; set; }

    public float ViewOffsetZ { get; set; } = 64;

    public float Friction { get; set; } = 1;

    public uint NextThinkTick { get; set; }

    public uint GroundEntityHandle { get; set; }

    public uint ZoomOwnerHandle { get; set; }

    public uint VehicleHandle { get; set; }

    public uint UseEntityHandle { get; set; }

    public byte NoInterpParity { get; set; }

    public byte OnTarget { get; set; }

    public uint ConstraintEntityHandle { get; set; }

    public float ConstraintCenterX { get; set; }

    public float ConstraintCenterY { get; set; }

    public float ConstraintCenterZ { get; set; }

    public float ConstraintRadius { get; set; }

    public float ConstraintWidth { get; set; }

    public float ConstraintSpeedFactor { get; set; }

    public float DeathTime { get; set; }

    public byte WaterLevel { get; set; }

    public string LastTouchedMapVolumeClass { get; private set; } = "";

    public string LastTouchedMapVolumeTarget { get; private set; } = "";

    public byte InRespawnRoom { get; private set; }

    public byte InRegenerationVolume { get; private set; }

    public byte InNoBuildVolume { get; private set; }

    public byte InCaptureArea { get; private set; }

    public byte InHurtVolume { get; private set; }

    public byte InSolidMapBrush { get; private set; }

    public ushort LastHurtVolumeDamage { get; private set; }

    public float LaggedMovementValue { get; set; } = 1;

    public byte Ducked { get; set; }

    public byte Ducking { get; set; }

    public byte InDuckJump { get; set; }

    public float DuckTime { get; set; }

    public float DuckJumpTime { get; set; }

    public float JumpTime { get; set; }

    public float FallVelocity { get; set; }

    public float PunchAngleX { get; set; }

    public float PunchAngleY { get; set; }

    public float PunchAngleZ { get; set; }

    public float PunchAngleVelocityX { get; set; }

    public float PunchAngleVelocityY { get; set; }

    public float PunchAngleVelocityZ { get; set; }

    public byte DrawViewModel { get; set; } = 1;

    public byte WearingSuit { get; set; }

    public byte Poisoned { get; set; }

    public float StepSize { get; set; } = 18;

    public byte AllowAutoMovement { get; set; } = 1;

    public byte SaveMeParity { get; set; }

    public uint RagdollHandle { get; set; }

    public uint ItemHandle { get; set; }

    public uint ActiveWeaponHandle { get; set; } = 0xa7;

    public uint PrimaryAmmo { get; set; } = 32;

    public uint SecondaryAmmo { get; set; } = 0;

    public uint Metal { get; set; } = 200;

    public uint WeaponClip1 { get; set; } = 6;

    public uint WeaponClip2 { get; set; }

    public uint WeaponState { get; set; } = 2;

    public uint WeaponViewModelIndex { get; set; } = 1;

    public uint WeaponWorldModelIndex { get; set; } = 1;

    public uint PrimaryAmmoType { get; set; }

    public uint SecondaryAmmoType { get; set; } = 1;

    public byte WeaponLowered { get; set; }

    public uint WeaponReloadMode { get; set; }

    public byte WeaponResetParity { get; set; }

    public byte WeaponReloadedThroughAnimEvent { get; set; }

    public byte WeaponInReload { get; set; }

    public byte WeaponFireOnEmpty { get; set; }

    public float WeaponNextEmptySoundTime { get; set; }

    public uint WeaponBuildState { get; set; }

    public uint WeaponBuildObjectType { get; set; }

    public uint WeaponObjectBeingBuiltHandle { get; set; }

    public uint TfWeaponState { get; set; }

    public byte WeaponCritFire { get; set; }

    public byte WeaponHealing { get; set; }

    public byte WeaponAttacking { get; set; }

    public byte WeaponChargeRelease { get; set; }

    public byte WeaponHolstered { get; set; }

    public uint WeaponHealingTargetHandle { get; set; }

    public float WeaponHealEffectLifetime { get; set; }

    public float WeaponChargeLevel { get; set; }

    public byte WeaponBottleBroken { get; set; }

    public uint WeaponPipebombCount { get; set; }

    public float WeaponChargeBeginTime { get; set; }

    public float WeaponSoonestPrimaryAttack { get; set; }

    public byte WeaponMinigunCritShot { get; set; }

    public uint PlayerCondition { get; set; }

    public uint PlayerState { get; set; }

    public byte Jumping { get; set; }

    public float MaxSpeed => ClassMaxSpeed(ClassNumber) * Math.Max(LaggedMovementValue, 0.0f);

    public uint DisguiseTeam { get; set; }

    public uint DisguiseClass { get; set; }

    public uint DisguiseTargetIndex { get; set; }

    public uint DisguiseHealth { get; set; }

    public uint DesiredDisguiseTeam { get; set; }

    public uint DesiredDisguiseClass { get; set; }

    public Tf2EngineerObjectState SentryGun { get; } = new();

    public Tf2EngineerObjectState Teleporter { get; } = new();

    public float CloakMeter { get; set; } = 100;

    public uint SpawnCounter { get; set; } = 1;

    public float SimulationTime { get; private set; }

    public uint TickBase { get; private set; }

    public uint LastPhysicsSimulationTick { get; private set; }

    public uint PhysicsSimulateTickGate { get; private set; } = OfficialEatf2ServerDllContracts.PhysicsSimulateTickGateOffset;

    public float NextPrimaryAttack { get; private set; }

    public float NextSecondaryAttack { get; private set; }

    public float TimeWeaponIdle { get; private set; }

    public ushort LastClientSourceSequence { get; private set; }

    public int? LastClientSourceSequenceDelta { get; private set; }

    public int LastClientSourcePacketCount { get; private set; }

    public int LastClientSourceBodyLength { get; private set; }

    public uint LastClientSourceBodyHash { get; private set; }

    public Ps3SourceClientPayloadRole LastClientSourceRole { get; private set; } = Ps3SourceClientPayloadRole.BinaryControlPayload;

    public Ps3SourceGameplayPacketShape LastClientSourceShape { get; private set; } = Ps3SourceGameplayPacketShape.Invalid;

    public Ps3SourceNativeFrameKind LastClientSourceNativeFrameKind { get; private set; } = Ps3SourceNativeFrameKind.EmptyBody;

    public string LastClientSourceBodyPrefixHex { get; private set; } = "";

    public ushort? LastClientSourceFirstUInt16BigEndian { get; private set; }

    public ushort? LastClientSourceFirstUInt16LittleEndian { get; private set; }

    public ushort? LastClientSourceLastUInt16BigEndian { get; private set; }

    public byte? LastClientSourceReliableAssociationMessageType { get; private set; }

    public uint? LastClientSourceReliableAssociationNativeToken { get; private set; }

    public uint? LastClientSourceReliableAssociationTokenBigEndian { get; private set; }

    public uint? LastClientSourceReliableAssociationTokenLittleEndian { get; private set; }

    public byte? LastClientSourceAttachedFrameKind { get; private set; }

    public ushort? LastClientSourceAttachedFrameDeclaredLength { get; private set; }

    public uint? LastClientSourceAttachedFrameNativeToken { get; private set; }

    public uint? LastClientSourceAttachedFrameTokenBigEndian { get; private set; }

    public uint? LastClientSourceAttachedFrameTokenLittleEndian { get; private set; }

    public int? LastClientSourceBitSidecarOffset { get; private set; }

    public int? LastClientSourceBitSidecarBitCount { get; private set; }

    public int? LastClientSourceBitSidecarPayloadLength { get; private set; }

    public string? LastClientSourcePayloadObjectFrameKind { get; private set; }

    public uint? LastClientSourcePayloadObjectHeaderValue { get; private set; }

    public int? LastClientSourcePayloadObjectSignedHeaderValue { get; private set; }

    public int? LastClientSourcePayloadObjectInnerPayloadOffset { get; private set; }

    public int? LastClientSourcePayloadObjectInnerPayloadLength { get; private set; }

    public int? LastClientSourcePayloadObjectBitreaderFieldOffset { get; private set; }

    public uint? LastClientSourcePayloadObjectAssociatedToken { get; private set; }

    public byte? LastClientSourcePayloadObjectFragmentIndex { get; private set; }

    public byte? LastClientSourcePayloadObjectFragmentTotalCount { get; private set; }

    public uint? LastClientSourcePayloadObjectFragmentPacketCounter { get; private set; }

    public bool? LastClientSourcePayloadObjectFragmentWrappedOrCompressed { get; private set; }

    public int? LastClientDecodedNetMessageType { get; private set; }

    public string? LastClientDecodedNetMessageName { get; private set; }

    public string? LastClientDecodedNetMessagePayloadKind { get; private set; }

    public int? LastClientDecodedNetMessagePayloadOffset { get; private set; }

    public int? LastClientDecodedNetMessagePayloadLength { get; private set; }

    public int? LastClientDecodedNetMessagePayloadBitCount { get; private set; }

    public int? LastClientNetTick { get; private set; }

    public string? LastClientStringCommand { get; private set; }

    public IReadOnlyDictionary<string, string> LastClientSetConVars => _lastClientSetConVars;

    public byte? LastClientSignonState { get; private set; }

    public int? LastClientSignonSpawnCount { get; private set; }

    public int? LastClientInfoServerCount { get; private set; }

    public uint? LastClientInfoSendTableCrc { get; private set; }

    public bool? LastClientInfoIsHltv { get; private set; }

    public uint? LastClientInfoFriendsId { get; private set; }

    public string? LastClientInfoFriendsName { get; private set; }

    public IReadOnlyList<uint> LastClientInfoCustomFiles => _lastClientInfoCustomFiles;

    public int? LastClientVoiceDataBitCount { get; private set; }

    public IReadOnlyList<byte> LastClientVoiceData => _lastClientVoiceData;

    public int? LastClientBaselineAckTick { get; private set; }

    public int? LastClientBaselineAckNumber { get; private set; }

    public IReadOnlyList<uint> LastClientListenEventMaskWords => _lastClientListenEventMaskWords;

    public uint? LastClientRespondCvarCookie { get; private set; }

    public int? LastClientRespondCvarStatusCode { get; private set; }

    public string? LastClientRespondCvarName { get; private set; }

    public string? LastClientRespondCvarValue { get; private set; }

    public bool? LastClientFileCrcReservedBit { get; private set; }

    public int? LastClientFileCrcPathIdCode { get; private set; }

    public string? LastClientFileCrcPathId { get; private set; }

    public int? LastClientFileCrcFilenamePrefixCode { get; private set; }

    public string? LastClientFileCrcFilename { get; private set; }

    public uint? LastClientFileCrc { get; private set; }

    public bool LastClientCommandDecoded { get; private set; }

    public short LastClientCommandForwardMove { get; private set; }

    public short LastClientCommandSideMove { get; private set; }

    public short LastClientCommandUpMove { get; private set; }

    public byte LastClientCommandButtons { get; private set; }

    public byte LastClientCommandWeaponSlotHint { get; private set; }

    public uint GeneratedObjectiveTicks { get; private set; }

    public uint GeneratedProjectileTicks { get; private set; }

    public uint GeneratedBuildableTicks { get; private set; }

    public float? GeneratedRespawnAtSimulationTime { get; private set; }

    public float GeneratedDamageGraceUntilSimulationTime { get; private set; }

    public int LastGeneratedSourceEventPacketCount { get; private set; }

    public float GeneratedRespawnDelaySeconds { get; set; } = 5.0f;

    public float GravityScale { get; private set; } = 1.0f;

    public float? PersonalGravityScale { get; private set; }

    public bool EnableClientCommandIntent { get; set; } = true;

    public bool EnableHeuristicClientCommandIntent { get; set; }

    public IReadOnlyList<Tf2SourceClientCommandHistoryEntry> NativeClientCommandHistory => _nativeClientCommandHistory;

    public IReadOnlyList<Tf2SourceClientSemanticContextEntry> NativeSourceSemanticContextHistory => _nativeSourceSemanticContextHistory;

    public int NativeClientCommandHistoryCapacity => OfficialEatf2ServerDllContracts.MaxAdditionalPhysicsSimulationCommands;

    private int _lastSimulatedClientCommandPacketCount;

    private Ps3SourceUserCmd _previousOfficialClientCommand;

    private readonly List<Tf2SourceClientCommandHistoryEntry> _nativeClientCommandHistory = new();

    private readonly List<Tf2SourceClientSemanticContextEntry> _nativeSourceSemanticContextHistory = new();

    private readonly Dictionary<string, string> _lastClientSetConVars = new(StringComparer.OrdinalIgnoreCase);

    private uint[] _lastClientInfoCustomFiles = [];

    private byte[] _lastClientVoiceData = [];

    private uint[] _lastClientListenEventMaskWords = [];

    private readonly Dictionary<uint, float> _timedConditionExpiresAt = new();

    public void ObserveClientSourcePacket(ushort sequence, ReadOnlySpan<byte> body, int packetCount)
    {
        var sequenceDelta = packetCount <= 1
            ? (int?)null
            : Ps3SourceTransportPacket.SequenceDelta(LastClientSourceSequence, sequence);
        var packet = new Ps3SourceTransportPacket(sequence, body.ToArray(), body.Length + Ps3SourceTransportPacket.HeaderBytes);
        var clientPayload = Ps3SourceClientPayloadClassifier.Classify(packet, packetCount, sequenceDelta);
        LastClientSourceSequence = sequence;
        LastClientSourceSequenceDelta = sequenceDelta;
        LastClientSourcePacketCount = packetCount;
        LastClientSourceBodyLength = body.Length;
        LastClientSourceBodyHash = Fnva32(body);
        LastClientSourceRole = clientPayload.Role;
        LastClientSourceShape = clientPayload.Shape;
        LastClientSourceNativeFrameKind = clientPayload.NativeFrameKind;
        LastClientSourceBodyPrefixHex = clientPayload.BodyPrefixHex;
        LastClientSourceFirstUInt16BigEndian = clientPayload.FirstUInt16BigEndian;
        LastClientSourceFirstUInt16LittleEndian = clientPayload.FirstUInt16LittleEndian;
        LastClientSourceLastUInt16BigEndian = clientPayload.LastUInt16BigEndian;
        LastClientSourceReliableAssociationMessageType = clientPayload.ReliableAssociationMessageType;
        LastClientSourceReliableAssociationNativeToken = clientPayload.ReliableAssociationNativeToken;
        LastClientSourceReliableAssociationTokenBigEndian = clientPayload.ReliableAssociationTokenBigEndian;
        LastClientSourceReliableAssociationTokenLittleEndian = clientPayload.ReliableAssociationTokenLittleEndian;
        LastClientSourceAttachedFrameKind = clientPayload.AttachedFrameKind;
        LastClientSourceAttachedFrameDeclaredLength = clientPayload.AttachedFrameDeclaredLength;
        LastClientSourceAttachedFrameNativeToken = clientPayload.AttachedFrameNativeToken;
        LastClientSourceAttachedFrameTokenBigEndian = clientPayload.AttachedFrameTokenBigEndian;
        LastClientSourceAttachedFrameTokenLittleEndian = clientPayload.AttachedFrameTokenLittleEndian;
        LastClientSourceBitSidecarOffset = clientPayload.BitSidecarOffset;
        LastClientSourceBitSidecarBitCount = clientPayload.BitSidecarBitCount;
        LastClientSourceBitSidecarPayloadLength = clientPayload.BitSidecarPayloadLength;
        LastClientSourcePayloadObjectFrameKind = clientPayload.PayloadObjectFrameKind;
        LastClientSourcePayloadObjectHeaderValue = clientPayload.PayloadObjectHeaderValue;
        LastClientSourcePayloadObjectSignedHeaderValue = clientPayload.PayloadObjectSignedHeaderValue;
        LastClientSourcePayloadObjectInnerPayloadOffset = clientPayload.PayloadObjectInnerPayloadOffset;
        LastClientSourcePayloadObjectInnerPayloadLength = clientPayload.PayloadObjectInnerPayloadLength;
        LastClientSourcePayloadObjectBitreaderFieldOffset = clientPayload.PayloadObjectBitreaderFieldOffset;
        LastClientSourcePayloadObjectAssociatedToken = clientPayload.PayloadObjectAssociatedToken;
        LastClientSourcePayloadObjectFragmentIndex = clientPayload.PayloadObjectFragmentIndex;
        LastClientSourcePayloadObjectFragmentTotalCount = clientPayload.PayloadObjectFragmentTotalCount;
        LastClientSourcePayloadObjectFragmentPacketCounter = clientPayload.PayloadObjectFragmentPacketCounter;
        LastClientSourcePayloadObjectFragmentWrappedOrCompressed = clientPayload.PayloadObjectFragmentWrappedOrCompressed;
        ApplyNativeSourceTransportControlContext(clientPayload);
        ApplyDecodedClientNetMessage(clientPayload, body);
        ApplyClientCommandIntent(clientPayload, body);
    }

    public void AdvanceSnapshot(int snapshotFrameIndex)
    {
        TickBase = checked((uint)Math.Max(snapshotFrameIndex, 0));
        LastPhysicsSimulationTick = TickBase;
        PhysicsSimulateTickGate = checked(TickBase + (uint)OfficialEatf2ServerDllContracts.PhysicsSimulateTickGateOffset);
        SimulationTime = TickBase / 30.0f;
        ExpireTimedConditions();
        TryGeneratedRespawn();
        NextPrimaryAttack = SimulationTime + 0.10f;
        NextSecondaryAttack = SimulationTime + 0.25f;
        TimeWeaponIdle = SimulationTime + 1.0f;
    }

    public void ApplyWorldRules(Tf2SourceWorldState world)
    {
        GeneratedRespawnDelaySeconds = world.DisableRespawnTimes ? 0.5f : 5.0f;
        GravityScale = PersonalGravityScale ?? Math.Clamp(world.Gravity / 800.0f, 0.0f, 4.0f);
        InSolidMapBrush = 0;
        ApplyMapBoundsCollision(world);
        ApplyBrushVolumeRules(world, applyHurtDamage: false);
        if (!Alive)
        {
            UpdateNativeMovementFlags();
            return;
        }

        if (NoClip || MovementFrozen)
        {
            UpdateNativeMovementFlags();
            return;
        }

        if (OriginZ > GroundZ + 0.01f)
        {
            VelocityZ -= 800.0f * GravityScale / 30.0f;
            OriginZ += VelocityZ / 30.0f;
            FallVelocity = Math.Min(Math.Max(FallVelocity, MathF.Abs(VelocityZ)), 3500.0f);
            if (OriginZ <= GroundZ)
            {
                OriginZ = GroundZ;
                VelocityZ = 0;
                FallVelocity = 0;
                Jumping = 0;
                InDuckJump = 0;
            }
        }
        else
        {
            OriginZ = GroundZ;
            VelocityZ = 0;
            FallVelocity = 0;
            Jumping = 0;
        }

        VelocityX *= Math.Clamp(Friction, 0.0f, 1.0f);
        VelocityY *= Math.Clamp(Friction, 0.0f, 1.0f);
        ApplyMapBoundsCollision(world);
        ApplyBrushVolumeRules(world, applyHurtDamage: true);
        UpdateNativeMovementFlags();
    }

    private void ApplyMapBoundsCollision(Tf2SourceWorldState world)
    {
        if (world.MapBounds is not { } bounds)
        {
            return;
        }

        var clampedX = Math.Clamp(OriginX, bounds.MinX, bounds.MaxX);
        if (MathF.Abs(clampedX - OriginX) > 0.001f)
        {
            OriginX = clampedX;
            VelocityX = 0;
        }

        var clampedY = Math.Clamp(OriginY, bounds.MinY, bounds.MaxY);
        if (MathF.Abs(clampedY - OriginY) > 0.001f)
        {
            OriginY = clampedY;
            VelocityY = 0;
        }

        var clampedZ = Math.Clamp(OriginZ, bounds.MinZ, bounds.MaxZ);
        if (MathF.Abs(clampedZ - OriginZ) > 0.001f)
        {
            OriginZ = clampedZ;
            VelocityZ = 0;
            FallVelocity = 0;
        }

        GroundZ = Math.Clamp(GroundZ, bounds.MinZ, bounds.MaxZ);
        if (OriginZ < GroundZ)
        {
            OriginZ = GroundZ;
            VelocityZ = 0;
            FallVelocity = 0;
        }
    }

    private void ApplyBrushVolumeRules(Tf2SourceWorldState world, bool applyHurtDamage)
    {
        var eyeZ = OriginZ + MathF.Max(1.0f, ViewOffsetZ * 0.5f);
        var touched = world.BrushVolumes
            .Where(static volume => volume.Enabled)
            .Where(volume => volume.Contains(OriginX, OriginY, OriginZ)
                || volume.Contains(OriginX, OriginY, eyeZ))
            .ToArray();

        ResolveSolidBrushCollisions(world, touched);

        var first = touched.FirstOrDefault();
        LastTouchedMapVolumeClass = first?.ClassName ?? "";
        LastTouchedMapVolumeTarget = first?.TargetName ?? "";
        InRespawnRoom = touched.Any(static volume => IsVolumeClass(volume, "func_respawnroom")) ? (byte)1 : (byte)0;
        InRegenerationVolume = touched.Any(static volume => IsVolumeClass(volume, "func_regenerate")) ? (byte)1 : (byte)0;
        InNoBuildVolume = touched.Any(static volume => IsVolumeClass(volume, "func_nobuild")) ? (byte)1 : (byte)0;
        InCaptureArea = touched.Any(static volume => IsVolumeClass(volume, "trigger_capture_area") || IsVolumeClass(volume, "func_capturezone")) ? (byte)1 : (byte)0;
        var hurtVolumes = touched.Where(static volume => IsVolumeClass(volume, "trigger_hurt")).ToArray();
        InHurtVolume = hurtVolumes.Length > 0 ? (byte)1 : (byte)0;
        LastHurtVolumeDamage = 0;

        if (InRespawnRoom != 0 || InRegenerationVolume != 0)
        {
            Health = MaxHealth;
            ApplyClassWeaponDefaults(resetAmmo: true);
        }

        if (!applyHurtDamage)
        {
            return;
        }

        foreach (var hurt in hurtVolumes)
        {
            var damage = DamagePerPhysicsTick(hurt);
            if (damage == 0)
            {
                continue;
            }

            LastHurtVolumeDamage = Math.Max(LastHurtVolumeDamage, damage);
            ApplyGeneratedDamage(damage);
            if (!Alive)
            {
                break;
            }
        }
    }

    private void ResolveSolidBrushCollisions(Tf2SourceWorldState world, IReadOnlyList<Tf2MapBrushVolume> touched)
    {
        foreach (var volume in touched.Where(IsSolidCollisionVolume))
        {
            if (!ResolveAabbPenetration(volume.Bounds))
            {
                continue;
            }

            InSolidMapBrush = 1;
            ApplyMapBoundsCollision(world);
        }
    }

    private bool ResolveAabbPenetration(Tf2MapBounds bounds)
    {
        if (OriginX < bounds.MinX || OriginX > bounds.MaxX
            || OriginY < bounds.MinY || OriginY > bounds.MaxY
            || OriginZ < bounds.MinZ || OriginZ > bounds.MaxZ)
        {
            return false;
        }

        var pushMinX = MathF.Abs(OriginX - bounds.MinX);
        var pushMaxX = MathF.Abs(bounds.MaxX - OriginX);
        var pushMinY = MathF.Abs(OriginY - bounds.MinY);
        var pushMaxY = MathF.Abs(bounds.MaxY - OriginY);
        var pushMinZ = MathF.Abs(OriginZ - bounds.MinZ);
        var pushMaxZ = MathF.Abs(bounds.MaxZ - OriginZ);
        var minimum = MathF.Min(
            MathF.Min(MathF.Min(pushMinX, pushMaxX), MathF.Min(pushMinY, pushMaxY)),
            MathF.Min(pushMinZ, pushMaxZ));
        const float epsilon = 0.03125f;

        if (minimum == pushMinX)
        {
            OriginX = bounds.MinX - epsilon;
            VelocityX = Math.Min(0, VelocityX);
            return true;
        }

        if (minimum == pushMaxX)
        {
            OriginX = bounds.MaxX + epsilon;
            VelocityX = Math.Max(0, VelocityX);
            return true;
        }

        if (minimum == pushMinY)
        {
            OriginY = bounds.MinY - epsilon;
            VelocityY = Math.Min(0, VelocityY);
            return true;
        }

        if (minimum == pushMaxY)
        {
            OriginY = bounds.MaxY + epsilon;
            VelocityY = Math.Max(0, VelocityY);
            return true;
        }

        if (minimum == pushMinZ)
        {
            OriginZ = bounds.MinZ - epsilon;
            VelocityZ = Math.Min(0, VelocityZ);
            return true;
        }

        OriginZ = bounds.MaxZ + epsilon;
        GroundZ = Math.Max(GroundZ, bounds.MaxZ + epsilon);
        VelocityZ = Math.Max(0, VelocityZ);
        FallVelocity = 0;
        return true;
    }

    private bool IsSolidCollisionVolume(Tf2MapBrushVolume volume)
    {
        if (!volume.Enabled)
        {
            return false;
        }

        if (IsVolumeClass(volume, "func_respawnroomvisualizer"))
        {
            return volume.TeamNumber != 0 && volume.TeamNumber != TeamNumber;
        }

        return IsVolumeClass(volume, "func_brush") && volume.Solidity == 2
            || IsVolumeClass(volume, "func_door")
            || IsVolumeClass(volume, "func_door_rotating")
            || IsVolumeClass(volume, "func_tracktrain");
    }

    private static ushort DamagePerPhysicsTick(Tf2MapBrushVolume volume)
    {
        var rawDamage = volume.Damage <= 0 ? 1.0f : volume.Damage / 30.0f;
        if (volume.DamageCap > 0)
        {
            rawDamage = MathF.Min(rawDamage, volume.DamageCap);
        }

        return checked((ushort)Math.Clamp(MathF.Ceiling(rawDamage), 0, ushort.MaxValue));
    }

    private static bool IsVolumeClass(Tf2MapBrushVolume volume, string className)
    {
        return string.Equals(volume.ClassName, className, StringComparison.OrdinalIgnoreCase);
    }

    public void SetTeam(uint teamNumber)
    {
        TeamNumber = Math.Min(teamNumber, 3U);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetClass(byte classNumber, bool resetHealth = true)
    {
        ClassNumber = (byte)Math.Clamp((int)classNumber, 1, 9);
        MaxHealth = MaxHealthForClass(ClassNumber);
        Health = resetHealth ? MaxHealth : Math.Min(Health, MaxHealth);
        DisguiseClass = 0;
        DesiredDisguiseClass = 0;
        ModelIndex = Tf2Ps3SourceCatalog.PlayerModelPrecacheIndexForClass(ClassNumber);
        ApplyClassWeaponDefaults(resetAmmo: resetHealth);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetHealth(ushort health)
    {
        if (health == 0)
        {
            ForceKill();
            return;
        }

        Health = Math.Min(health, MaxHealth);
        Alive = true;
        DeathTime = 0;
        RagdollHandle = 0;
        PlayerState = 0;
        ObserverMode = 0;
        ObserverTargetHandle = 0;
        ActiveWeaponHandle = ActiveWeaponHandle == 0 ? 0xa7U : ActiveWeaponHandle;
        GeneratedRespawnAtSimulationTime = null;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetMovementFrozen(bool frozen)
    {
        MovementFrozen = frozen;
        if (frozen)
        {
            VelocityX = 0;
            VelocityY = 0;
            VelocityZ = 0;
            BaseVelocityX = 0;
            BaseVelocityY = 0;
            BaseVelocityZ = 0;
            LaggedMovementValue = 0;
        }
        else
        {
            LaggedMovementValue = 1;
        }

        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetNoClip(bool enabled)
    {
        NoClip = enabled;
        MoveType = enabled ? (byte)8 : (byte)2;
        MoveCollide = 0;
        CollisionGroup = enabled ? 0U : 5U;
        SolidType = enabled ? (byte)0 : (byte)2;
        SolidFlags = 0;
        if (enabled)
        {
            MovementFrozen = false;
            if (LaggedMovementValue <= 0.0f)
            {
                LaggedMovementValue = 1.0f;
            }
        }

        UpdateNativeMovementFlags();
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetPosition(float x, float y, float z, float? yaw = null, float? pitch = null)
    {
        OriginX = x;
        OriginY = y;
        OriginZ = Math.Max(0, z);
        GroundZ = OriginZ;
        VelocityX = 0;
        VelocityY = 0;
        VelocityZ = 0;
        BaseVelocityX = 0;
        BaseVelocityY = 0;
        BaseVelocityZ = 0;
        FallVelocity = 0;
        if (yaw is { } yawValue)
        {
            Yaw = NormalizeAngle(yawValue);
        }

        if (pitch is { } pitchValue)
        {
            Pitch = Math.Clamp(pitchValue, -89.0f, 89.0f);
        }

        UpdateNativeMovementFlags();
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetMovementSpeed(float speed)
    {
        LaggedMovementValue = Math.Clamp(speed, 0.0f, 5.0f);
        MovementFrozen = LaggedMovementValue <= 0.0f;
        if (MovementFrozen)
        {
            VelocityX = 0;
            VelocityY = 0;
            VelocityZ = 0;
            BaseVelocityX = 0;
            BaseVelocityY = 0;
            BaseVelocityZ = 0;
        }

        UpdateNativeMovementFlags();
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetFov(uint fov)
    {
        FovStart = Fov;
        Fov = Math.Clamp(fov, 1U, 179U);
        DefaultFov = Fov;
        FovTime = SimulationTime;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetPersonalGravityScale(float? gravityScale)
    {
        PersonalGravityScale = gravityScale is { } value
            ? Math.Clamp(value, 0.0f, 4.0f)
            : null;
        GravityScale = PersonalGravityScale ?? GravityScale;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetDrawViewModel(bool enabled)
    {
        DrawViewModel = enabled ? (byte)1 : (byte)0;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetBlind(bool enabled)
    {
        if (enabled)
        {
            FovStart = Fov;
            Fov = 1;
            DrawViewModel = 0;
        }
        else
        {
            Fov = DefaultFov == 1 ? 75U : Math.Clamp(DefaultFov, 1U, 179U);
            DrawViewModel = 1;
        }

        FovTime = SimulationTime;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetViewShake(float amplitude)
    {
        var clamped = Math.Clamp(amplitude, 0.0f, 32.0f);
        PunchAngleX = Math.Clamp(PunchAngleX + clamped, -32.0f, 32.0f);
        PunchAngleY = Math.Clamp(PunchAngleY + (clamped * 0.5f), -32.0f, 32.0f);
        PunchAngleZ = Math.Clamp(PunchAngleZ - (clamped * 0.25f), -32.0f, 32.0f);
        PunchAngleVelocityX = clamped * 4.0f;
        PunchAngleVelocityY = clamped * -2.0f;
        PunchAngleVelocityZ = clamped;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetRenderColor(byte red, byte green, byte blue, byte alpha)
    {
        RenderColor = ((uint)red << 24)
            | ((uint)green << 16)
            | ((uint)blue << 8)
            | alpha;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetRenderAlpha(byte alpha)
    {
        RenderColor = (RenderColor & 0xffffff00U) | alpha;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetRenderMode(byte renderMode)
    {
        RenderMode = renderMode;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetRenderFx(byte renderFx)
    {
        RenderFx = renderFx;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetEffects(uint effects)
    {
        Effects = effects;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void AddEffects(uint effects)
    {
        Effects |= effects;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void RemoveEffects(uint effects)
    {
        Effects &= ~effects;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void ClearRenderState()
    {
        Effects = 0;
        RenderMode = 0;
        RenderFx = 0;
        RenderColor = 0xffffffff;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetModelIndex(uint modelIndex)
    {
        ModelIndex = modelIndex;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetAnimation(uint sequence, float? playbackRate = null, float? cycle = null)
    {
        Sequence = sequence;
        PlaybackRate = playbackRate is { } rate ? Math.Clamp(rate, 0.0f, 8.0f) : PlaybackRate;
        ServerAnimationCycle = cycle is { } cycleValue ? Math.Clamp(cycleValue, 0.0f, 1.0f) : ServerAnimationCycle;
        NewSequenceParity++;
        ResetEventsParity++;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetSkinBody(uint? skin = null, uint? body = null, uint? hitboxSet = null)
    {
        if (skin is { } skinValue)
        {
            Skin = skinValue;
        }

        if (body is { } bodyValue)
        {
            Body = bodyValue;
        }

        if (hitboxSet is { } hitboxSetValue)
        {
            HitboxSet = hitboxSetValue;
        }

        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetModelWidthScale(float scale)
    {
        ModelWidthScale = Math.Clamp(scale, 0.1f, 4.0f);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetTextureFrameIndex(uint textureFrameIndex)
    {
        TextureFrameIndex = textureFrameIndex;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetForceVector(float x, float y, float z, uint forceBone = 0)
    {
        ForceX = Math.Clamp(x, -4096.0f, 4096.0f);
        ForceY = Math.Clamp(y, -4096.0f, 4096.0f);
        ForceZ = Math.Clamp(z, -4096.0f, 4096.0f);
        ForceBone = forceBone;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetAnimationFlags(byte clientSideAnimation, byte clientSideFrameReset)
    {
        ClientSideAnimation = clientSideAnimation;
        ClientSideFrameReset = clientSideFrameReset;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetLightingOriginHandles(uint lightingOriginHandle, uint lightingOriginRelativeHandle)
    {
        LightingOriginHandle = lightingOriginHandle;
        LightingOriginRelativeHandle = lightingOriginRelativeHandle;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetFade(float minDistance, float maxDistance, float scale)
    {
        FadeMinDistance = Math.Max(0.0f, minDistance);
        FadeMaxDistance = Math.Max(FadeMinDistance, maxDistance);
        FadeScale = Math.Clamp(scale, 0.0f, 4.0f);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void BumpMuzzleFlash()
    {
        MuzzleFlashParity++;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void ResetAnimationState()
    {
        ModelIndex = 0;
        TextureFrameIndex = 0;
        Sequence = 0;
        ForceBone = 0;
        ForceX = 0;
        ForceY = 0;
        ForceZ = 0;
        Skin = 0;
        Body = 0;
        HitboxSet = 0;
        ModelWidthScale = 1.0f;
        PlaybackRate = 1.0f;
        ClientSideAnimation = 0;
        ClientSideFrameReset = 0;
        NewSequenceParity++;
        ResetEventsParity++;
        MuzzleFlashParity = 0;
        LightingOriginHandle = 0;
        LightingOriginRelativeHandle = 0;
        ServerAnimationCycle = 0;
        FadeMinDistance = 0;
        FadeMaxDistance = 0;
        FadeScale = 1.0f;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void RefillResources()
    {
        Health = MaxHealth;
        Alive = true;
        WeaponClip1 = 6;
        WeaponClip2 = 0;
        PrimaryAmmo = 32;
        SecondaryAmmo = 32;
        Metal = 200;
        CloakMeter = 100;
        WeaponInReload = 0;
        WeaponFireOnEmpty = 0;
        WeaponReloadMode = 0;
        WeaponReloadedThroughAnimEvent = 0;
        WeaponLowered = 0;
        WeaponHolstered = 0;
        WeaponAttacking = 0;
        WeaponHealing = 0;
        WeaponChargeRelease = 0;
        TfWeaponState = 0;
        ResupplyPoints++;
        SentryGun.AmmoShells = 200;
        SentryGun.AmmoRockets = SentryGun.UpgradeLevel >= 3 ? 20U : SentryGun.AmmoRockets;
        SentryGun.UpgradeMetal = 200;
        DeathTime = 0;
        RagdollHandle = 0;
        PlayerState = 0;
        ObserverMode = 0;
        ActiveWeaponHandle = ActiveWeaponHandle == 0 ? 0xa7U : ActiveWeaponHandle;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetAmmo(uint primaryAmmo, uint secondaryAmmo, uint metal, uint clip1)
    {
        PrimaryAmmo = Math.Min(primaryAmmo, 999U);
        SecondaryAmmo = Math.Min(secondaryAmmo, 999U);
        Metal = Math.Min(metal, 999U);
        WeaponClip1 = Math.Min(clip1, 999U);
        WeaponClip2 = Math.Min(secondaryAmmo, 999U);
        SentryGun.UpgradeMetal = Math.Min(Metal, 200U);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetSentry(uint level, uint state, uint shells, uint rockets)
    {
        SentryGun.State = Math.Min(state, 3U);
        SentryGun.UpgradeLevel = Math.Clamp(level, 0U, 3U);
        SentryGun.AmmoShells = Math.Min(shells, 200U);
        SentryGun.AmmoRockets = Math.Min(rockets, 20U);
        SentryGun.UpgradeMetal = Math.Min(Metal, 200U);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetTeleporter(uint state, float rechargeTime, uint timesUsed)
    {
        Teleporter.State = Math.Min(state, 3U);
        Teleporter.RechargeTime = Math.Clamp(rechargeTime, 0.0f, 60.0f);
        Teleporter.TimesUsed = Math.Min(timesUsed, 9999U);
        Teleporter.YawToExit = Yaw;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void DestroyBuildables()
    {
        SentryGun.State = 0;
        SentryGun.UpgradeLevel = 0;
        SentryGun.AmmoShells = 0;
        SentryGun.AmmoRockets = 0;
        SentryGun.UpgradeMetal = 0;
        Teleporter.State = 0;
        Teleporter.RechargeTime = 0;
        Teleporter.TimesUsed = 0;
        Teleporter.UpgradeMetal = 0;
        WeaponBuildState = 0;
        WeaponBuildObjectType = 0;
        WeaponObjectBeingBuiltHandle = 0;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void ApplyClassWeaponDefaults(bool resetAmmo = false)
    {
        ActiveWeaponHandle = Alive ? (ActiveWeaponHandle == 0 ? 0xa7U : ActiveWeaponHandle) : 0U;
        WeaponState = Alive ? 2U : 0U;
        WeaponLowered = 0;
        WeaponHolstered = Alive ? (byte)0 : (byte)1;
        WeaponAttacking = 0;
        WeaponHealing = 0;
        WeaponChargeRelease = 0;
        WeaponCritFire = 0;
        WeaponMinigunCritShot = 0;
        WeaponBuildState = 0;
        WeaponObjectBeingBuiltHandle = 0;
        TfWeaponState = 0;
        WeaponViewModelIndex = Tf2Ps3SourceCatalog.WeaponViewModelPrecacheIndexForClass(ClassNumber);
        WeaponWorldModelIndex = Tf2Ps3SourceCatalog.WeaponWorldModelPrecacheIndexForClass(ClassNumber);
        PrimaryAmmoType = ClassNumber switch
        {
            5 or 8 => 0,
            9 => 3,
            _ => 1
        };
        SecondaryAmmoType = ClassNumber switch
        {
            9 => 3,
            4 or 6 => 1,
            _ => 2
        };
        WeaponBuildObjectType = ClassNumber == 9 ? 2U : 0U;
        if (resetAmmo)
        {
            WeaponClip1 = DefaultClip1ForClass(ClassNumber);
            WeaponClip2 = DefaultClip2ForClass(ClassNumber);
            PrimaryAmmo = DefaultPrimaryAmmoForClass(ClassNumber);
            SecondaryAmmo = DefaultSecondaryAmmoForClass(ClassNumber);
            Metal = ClassNumber == 9 ? 200U : Metal;
        }

        WeaponReloadMode = 0;
        WeaponInReload = 0;
        WeaponFireOnEmpty = 0;
        WeaponReloadedThroughAnimEvent = 0;
        WeaponNextEmptySoundTime = 0;
        WeaponSoonestPrimaryAttack = NextPrimaryAttack;
    }

    public void SetPlayerConditionMask(uint conditionMask)
    {
        PlayerCondition = conditionMask;
        ClearExpiredConditionTimersNotInMask();
        NoInterpParity++;
        SaveMeParity++;
    }

    public void AddPlayerCondition(uint conditionMask)
    {
        PlayerCondition |= conditionMask;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void AddTimedPlayerCondition(uint conditionMask, float durationSeconds)
    {
        PlayerCondition |= conditionMask;
        if (durationSeconds > 0.0f)
        {
            var expiresAt = SimulationTime + durationSeconds;
            foreach (var bit in ConditionBits(conditionMask))
            {
                _timedConditionExpiresAt[bit] = expiresAt;
            }
        }

        NoInterpParity++;
        SaveMeParity++;
    }

    public void RemovePlayerCondition(uint conditionMask)
    {
        PlayerCondition &= ~conditionMask;
        foreach (var bit in ConditionBits(conditionMask))
        {
            _timedConditionExpiresAt.Remove(bit);
        }

        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetDisguise(uint teamNumber, byte classNumber, uint targetIndex = 0)
    {
        DisguiseTeam = Math.Clamp(teamNumber, 0U, 3U);
        DisguiseClass = (uint)Math.Clamp(classNumber, (byte)0, (byte)9);
        DesiredDisguiseTeam = DisguiseTeam;
        DesiredDisguiseClass = DisguiseClass;
        DisguiseTargetIndex = targetIndex;
        DisguiseHealth = DisguiseClass == 0 ? 0U : MaxHealthForClass((byte)DisguiseClass);
        NoInterpParity++;
        SaveMeParity++;
    }

    public void ClearDisguise()
    {
        DisguiseTeam = 0;
        DisguiseClass = 0;
        DesiredDisguiseTeam = 0;
        DesiredDisguiseClass = 0;
        DisguiseTargetIndex = 0;
        DisguiseHealth = 0;
        NoInterpParity++;
        SaveMeParity++;
    }

    public void SetCloak(float cloakMeter)
    {
        CloakMeter = Math.Clamp(cloakMeter, 0.0f, 100.0f);
        if (CloakMeter > 0)
        {
            AddPlayerCondition(1U << 4);
        }
        else
        {
            RemovePlayerCondition(1U << 4);
        }
    }

    private void ExpireTimedConditions()
    {
        if (_timedConditionExpiresAt.Count == 0)
        {
            return;
        }

        var expiredMask = 0U;
        foreach (var (conditionBit, expiresAt) in _timedConditionExpiresAt.ToArray())
        {
            if (SimulationTime >= expiresAt)
            {
                expiredMask |= conditionBit;
                _timedConditionExpiresAt.Remove(conditionBit);
            }
        }

        if (expiredMask == 0)
        {
            return;
        }

        PlayerCondition &= ~expiredMask;
        NoInterpParity++;
        SaveMeParity++;
    }

    private void ClearExpiredConditionTimersNotInMask()
    {
        if (_timedConditionExpiresAt.Count == 0)
        {
            return;
        }

        foreach (var conditionBit in _timedConditionExpiresAt.Keys.ToArray())
        {
            if ((PlayerCondition & conditionBit) == 0)
            {
                _timedConditionExpiresAt.Remove(conditionBit);
            }
        }
    }

    private static IEnumerable<uint> ConditionBits(uint conditionMask)
    {
        for (var bit = 0; bit < 32; bit++)
        {
            var conditionBit = 1U << bit;
            if ((conditionMask & conditionBit) != 0)
            {
                yield return conditionBit;
            }
        }
    }

    public void ForceKill()
    {
        ApplyGeneratedDeath();
        NoInterpParity++;
        SaveMeParity++;
    }

    public void ForceRespawn()
    {
        Alive = true;
        Health = MaxHealth;
        DeathTime = 0;
        RagdollHandle = 0;
        PlayerState = 0;
        ObserverMode = 0;
        ObserverTargetHandle = 0;
        ActiveWeaponHandle = 0xa7;
        WeaponClip1 = WeaponClip1 == 0 ? 6U : WeaponClip1;
        ApplyClassWeaponDefaults(resetAmmo: false);
        OriginZ = GroundZ;
        VelocityZ = 0;
        FallVelocity = 0;
        SpawnCounter++;
        GeneratedRespawnAtSimulationTime = null;
        GeneratedDamageGraceUntilSimulationTime = SimulationTime + 1.0f;
        UpdateNativeMovementFlags();
        NoInterpParity++;
        SaveMeParity++;
    }

    public Tf2SourceCommandSimulation ConsumeGeneratedCommandSimulation()
    {
        if (!LastClientCommandDecoded || LastClientSourcePacketCount <= _lastSimulatedClientCommandPacketCount)
        {
            return Tf2SourceCommandSimulation.Inactive;
        }

        _lastSimulatedClientCommandPacketCount = LastClientSourcePacketCount;
        if (!Alive)
        {
            return Tf2SourceCommandSimulation.Inactive;
        }

        var fired = (LastClientCommandButtons & 0x01) != 0;
        var used = (LastClientCommandButtons & 0x08) != 0;
        var moved = LastClientCommandForwardMove != 0
            || LastClientCommandSideMove != 0
            || LastClientCommandUpMove != 0;
        var active = fired || used || moved;
        if (!active)
        {
            return Tf2SourceCommandSimulation.Inactive;
        }

        GeneratedObjectiveTicks++;
        Score += fired ? 2U : 1U;
        if (fired)
        {
            GeneratedProjectileTicks++;
            PrimaryAmmo = PrimaryAmmo > 0 ? PrimaryAmmo - 1 : 0;
            PunchAngleX = Math.Clamp(PunchAngleX + 0.25f, 0.0f, 3.0f);
            PunchAngleVelocityX = 1.5f;
            PunchAngleVelocityY = (GeneratedProjectileTicks % 3) - 1;
            PlayerCondition = 1;
            if (GeneratedProjectileTicks % 4 == 0)
            {
                KillAssists++;
                if (ClassNumber == 2)
                {
                    Headshots++;
                }

                if (ClassNumber == 8)
                {
                    Backstabs++;
                }
            }

            if (GeneratedProjectileTicks % 12 == 0)
            {
                Defenses++;
                BuildingsDestroyed++;
                ApplyGeneratedDamage(15);
            }

            BeginGeneratedReload();
        }
        else
        {
            PlayerCondition = 0;
            PunchAngleX = Math.Max(0.0f, PunchAngleX - 0.10f);
            PunchAngleVelocityX = 0.0f;
            CompleteGeneratedReloadIfReady();
        }

        var buildableIntent = used || LastClientCommandWeaponSlotHint >= 5 || ClassNumber == 9;
        if (buildableIntent)
        {
            AdvanceGeneratedBuildables(used);
        }

        if (ClassNumber == 5 && (used || fired))
        {
            HealPoints += used ? 3U : 1U;
            WeaponHealing = 1;
            WeaponHealEffectLifetime = SimulationTime + 0.35f;
            WeaponChargeLevel = Math.Min(1.0f, WeaponChargeLevel + 0.01f);
            if (GeneratedObjectiveTicks % 24 == 0)
            {
                Invulns++;
            }
        }

        return new Tf2SourceCommandSimulation(
            Active: true,
            Fired: fired,
            Used: used,
            Moved: moved,
            TeamNumber: TeamNumber);
    }

    public bool TryConsumeGeneratedSourceEvent(out string eventText)
    {
        eventText = "";
        if (!LastClientCommandDecoded
            || LastClientSourcePacketCount <= LastGeneratedSourceEventPacketCount)
        {
            return false;
        }

        LastGeneratedSourceEventPacketCount = LastClientSourcePacketCount;
        var actions = new List<string>(4);
        if (LastClientCommandForwardMove != 0 || LastClientCommandSideMove != 0 || LastClientCommandUpMove != 0)
        {
            actions.Add("move");
        }

        if ((LastClientCommandButtons & 0x01) != 0)
        {
            actions.Add("fire");
        }

        if ((LastClientCommandButtons & 0x02) != 0)
        {
            actions.Add("jump");
        }

        if ((LastClientCommandButtons & 0x04) != 0)
        {
            actions.Add("duck");
        }

        if ((LastClientCommandButtons & 0x08) != 0)
        {
            actions.Add("use");
        }

        var actionText = actions.Count == 0 ? "idle" : string.Join('+', actions);
        eventText = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"CBasePlayer::ProcessUsercmds CBasePlayer::PhysicsSimulate ClientCommand packet={LastClientSourcePacketCount} seq={LastClientSourceSequence} action={actionText} buttons=0x{LastClientCommandButtons:x2} move=({LastClientCommandForwardMove},{LastClientCommandSideMove},{LastClientCommandUpMove}) yaw={Yaw:0.##} pitch={Pitch:0.##} weaponHint={LastClientCommandWeaponSlotHint} team={TeamNumber} class={ClassNumber} flags=0x{Flags:x} ground=0x{GroundEntityHandle:x} officialMaxUserCmds={OfficialEatf2ServerDllContracts.MaxUserCmdsPerBatch} cusercmdStride=0x{OfficialEatf2ServerDllContracts.CUserCmdStrideBytes:x} decode=0x{OfficialEatf2ServerDllContracts.UserCmdDecodeFunction:x} processSlot=0x{OfficialEatf2ServerDllContracts.PlayerProcessUsercmdsVtableSlot:x} physicsSlot=0x{OfficialEatf2ServerDllContracts.PlayerRunCommandVtableSlot:x} physicsHistoryStride=0x{OfficialEatf2ServerDllContracts.PhysicsCommandHistoryStrideBytes:x} physicsTickGateOffset=0x{OfficialEatf2ServerDllContracts.PhysicsSimulateTickGateOffset:x} LastPhysicsSimulationTick={LastPhysicsSimulationTick} PhysicsSimulateTickGate={PhysicsSimulateTickGate}");
        return true;
    }

    private void AdvanceGeneratedBuildables(bool used)
    {
        GeneratedBuildableTicks++;
        WeaponBuildState = used ? 2U : 1U;
        WeaponBuildObjectType = GeneratedBuildableTicks % 2 == 0 ? 1U : 2U;
        WeaponObjectBeingBuiltHandle = WeaponBuildObjectType == 1 ? 0xa8U : 0xa9U;
        if (Metal > 0)
        {
            Metal -= Math.Min(Metal, used ? 10U : 4U);
        }

        SentryGun.State = 1;
        SentryGun.UpgradeLevel = Math.Min(3U, 1U + (GeneratedBuildableTicks / 4U));
        SentryGun.AmmoShells = Math.Min(200U, SentryGun.AmmoShells + 12U);
        SentryGun.AmmoRockets = SentryGun.UpgradeLevel >= 3
            ? Math.Min(20U, SentryGun.AmmoRockets + 2U)
            : 0U;
        SentryGun.UpgradeMetal = Math.Min(200U, SentryGun.UpgradeMetal + (used ? 25U : 8U));

        Teleporter.State = 1;
        Teleporter.RechargeTime = Math.Max(0.0f, 10.0f - (GeneratedBuildableTicks * 0.5f));
        Teleporter.YawToExit = Yaw;
        if (used || GeneratedBuildableTicks % 4 == 0)
        {
            Teleporter.TimesUsed++;
            Teleports++;
        }
    }

    private void ApplyNativeWeaponIntent(bool fired, bool used, byte weaponSlotHint)
    {
        WeaponState = Alive ? 2U : 0U;
        WeaponLowered = Alive ? (byte)0 : (byte)1;
        WeaponHolstered = Alive ? (byte)0 : (byte)1;
        WeaponAttacking = fired ? (byte)1 : (byte)0;
        WeaponHealing = ClassNumber == 5 && (fired || used) ? (byte)1 : (byte)0;
        WeaponChargeRelease = ClassNumber == 5 && used ? (byte)1 : (byte)0;
        WeaponCritFire = fired && (PlayerCondition & 1U) != 0 ? (byte)1 : (byte)0;
        WeaponMinigunCritShot = ClassNumber == 6 && WeaponCritFire != 0 ? (byte)1 : (byte)0;
        TfWeaponState = ClassNumber switch
        {
            5 when WeaponHealing != 0 => 1,
            6 when fired => 2,
            7 when fired => 1,
            _ => 0
        };

        if (ClassNumber == 5 && WeaponHealing != 0)
        {
            WeaponHealEffectLifetime = SimulationTime + 0.35f;
            WeaponChargeLevel = Math.Min(1.0f, WeaponChargeLevel + 0.005f);
        }

        if (ClassNumber == 4 && fired)
        {
            WeaponPipebombCount = Math.Min(8U, WeaponPipebombCount + 1U);
            WeaponChargeBeginTime = SimulationTime;
        }

        if (fired)
        {
            WeaponResetParity ^= 1;
            if (WeaponClip1 > 0)
            {
                WeaponClip1--;
                WeaponInReload = 0;
                WeaponFireOnEmpty = 0;
                WeaponReloadMode = 0;
                WeaponReloadedThroughAnimEvent = 0;
                WeaponSoonestPrimaryAttack = NextPrimaryAttack;
            }
            else
            {
                WeaponFireOnEmpty = 1;
                WeaponNextEmptySoundTime = SimulationTime + 0.45f;
                BeginGeneratedReload();
            }
        }
        else
        {
            CompleteGeneratedReloadIfReady();
        }

        if (ClassNumber == 9 && (used || weaponSlotHint >= 5))
        {
            WeaponBuildState = used ? 2U : 1U;
            WeaponBuildObjectType = WeaponBuildObjectType == 0 ? 2U : WeaponBuildObjectType;
            WeaponObjectBeingBuiltHandle = WeaponObjectBeingBuiltHandle == 0 ? 0xa8U : WeaponObjectBeingBuiltHandle;
        }
        else if (WeaponBuildState != 0 && SentryGun.State == 0 && Teleporter.State == 0)
        {
            WeaponBuildState = 0;
            WeaponObjectBeingBuiltHandle = 0;
        }
    }

    private void BeginGeneratedReload()
    {
        if (WeaponClip1 > 0 || PrimaryAmmo == 0 || WeaponInReload != 0)
        {
            return;
        }

        WeaponInReload = 1;
        WeaponReloadMode = 1;
        NextPrimaryAttack = Math.Max(NextPrimaryAttack, SimulationTime + 0.8f);
        NextSecondaryAttack = Math.Max(NextSecondaryAttack, SimulationTime + 0.8f);
        TimeWeaponIdle = Math.Max(TimeWeaponIdle, SimulationTime + 1.0f);
    }

    private void CompleteGeneratedReloadIfReady()
    {
        if (WeaponInReload == 0 || SimulationTime < NextPrimaryAttack)
        {
            return;
        }

        var maxClip = DefaultClip1ForClass(ClassNumber);
        if (maxClip == 0 || PrimaryAmmo == 0)
        {
            WeaponInReload = 0;
            WeaponReloadMode = 0;
            return;
        }

        var needed = maxClip > WeaponClip1 ? maxClip - WeaponClip1 : 0;
        var loaded = Math.Min(needed, PrimaryAmmo);
        WeaponClip1 += loaded;
        PrimaryAmmo -= loaded;
        WeaponInReload = 0;
        WeaponReloadMode = 0;
        WeaponReloadedThroughAnimEvent = loaded > 0 ? (byte)1 : (byte)0;
        WeaponFireOnEmpty = 0;
        WeaponSoonestPrimaryAttack = NextPrimaryAttack;
    }

    private void ApplyGeneratedDamage(ushort amount)
    {
        if (!Alive || amount == 0)
        {
            return;
        }

        if (SimulationTime < GeneratedDamageGraceUntilSimulationTime)
        {
            return;
        }

        if (Health <= amount)
        {
            ApplyGeneratedDeath();
            return;
        }

        Health -= amount;
    }

    private void ApplyGeneratedDeath()
    {
        if (!Alive)
        {
            return;
        }

        Alive = false;
        Health = 0;
        Deaths++;
        DeathTime = SimulationTime;
        RagdollHandle = 0xa4;
        PlayerState = 1;
        ObserverMode = 2;
        ObserverTargetHandle = RagdollHandle;
        ActiveWeaponHandle = 0;
        WeaponState = 0;
        WeaponLowered = 1;
        WeaponHolstered = 1;
        WeaponAttacking = 0;
        WeaponHealing = 0;
        WeaponChargeRelease = 0;
        TfWeaponState = 0;
        GeneratedRespawnAtSimulationTime = SimulationTime + GeneratedRespawnDelaySeconds;
    }

    private void TryGeneratedRespawn()
    {
        if (Alive || GeneratedRespawnAtSimulationTime is not { } respawnAt || SimulationTime < respawnAt)
        {
            return;
        }

        Alive = true;
        Health = MaxHealth;
        DeathTime = 0;
        RagdollHandle = 0;
        PlayerState = 0;
        ObserverMode = 0;
        ObserverTargetHandle = 0;
        ActiveWeaponHandle = 0xa7;
        WeaponClip1 = WeaponClip1 == 0 ? 6U : WeaponClip1;
        ApplyClassWeaponDefaults(resetAmmo: false);
        OriginZ = GroundZ;
        VelocityZ = 0;
        FallVelocity = 0;
        SpawnCounter++;
        GeneratedRespawnAtSimulationTime = null;
        GeneratedDamageGraceUntilSimulationTime = SimulationTime + 1.0f;
        UpdateNativeMovementFlags();
    }

    private void ApplyDecodedClientNetMessage(Ps3SourceClientPayloadInfo clientPayload, ReadOnlySpan<byte> body)
    {
        ClearDecodedClientNetMessageState();
        if (clientPayload.DecodedNetMessageType is null
            || !Ps3SourceClientNetMessageDecoder.TryDecode(body, out var decoded))
        {
            return;
        }

        LastClientDecodedNetMessageType = decoded.MessageType;
        LastClientDecodedNetMessageName = decoded.MessageName;
        LastClientDecodedNetMessagePayloadKind = decoded.PayloadKind.ToString();
        LastClientDecodedNetMessagePayloadOffset = decoded.PayloadOffset;
        LastClientDecodedNetMessagePayloadLength = decoded.PayloadLength;
        LastClientDecodedNetMessagePayloadBitCount = decoded.PayloadBitCount;
        ApplyNativeSourceClientNetMessageContext(clientPayload, decoded);

        switch (decoded.Message)
        {
            case Ps3SourceNetTick tick:
                LastClientNetTick = tick.Tick;
                break;

            case Ps3SourceNetStringCmd stringCmd:
                LastClientStringCommand = stringCmd.Command;
                break;

            case Ps3SourceNetSetConVar setConVar:
                foreach (var pair in setConVar.Values)
                {
                    _lastClientSetConVars[pair.Name] = pair.Value;
                }
                break;

            case Ps3SourceNetSignonState signonState:
                LastClientSignonState = signonState.SignonState;
                LastClientSignonSpawnCount = signonState.SpawnCount;
                break;

            case Ps3SourceClcClientInfo clientInfo:
                LastClientInfoServerCount = clientInfo.ServerCount;
                LastClientInfoSendTableCrc = clientInfo.SendTableCrc;
                LastClientInfoIsHltv = clientInfo.IsHltv;
                LastClientInfoFriendsId = clientInfo.FriendsId;
                LastClientInfoFriendsName = clientInfo.FriendsName;
                _lastClientInfoCustomFiles = clientInfo.CustomFiles.ToArray();
                break;

            case Ps3SourceClcVoiceData voiceData:
                LastClientVoiceDataBitCount = voiceData.DataBitCount;
                _lastClientVoiceData = voiceData.Data.ToArray();
                break;

            case Ps3SourceClcBaselineAck baselineAck:
                LastClientBaselineAckTick = baselineAck.BaselineTick;
                LastClientBaselineAckNumber = baselineAck.BaselineNumber;
                break;

            case Ps3SourceClcListenEvents listenEvents:
                _lastClientListenEventMaskWords = listenEvents.EventMaskWords.ToArray();
                break;

            case Ps3SourceClcRespondCvarValue cvarValue:
                LastClientRespondCvarCookie = cvarValue.Cookie;
                LastClientRespondCvarStatusCode = cvarValue.StatusCode;
                LastClientRespondCvarName = cvarValue.CvarName;
                LastClientRespondCvarValue = cvarValue.CvarValue;
                break;

            case Ps3SourceClcFileCrcCheck fileCrc:
                LastClientFileCrcReservedBit = fileCrc.ReservedBit;
                LastClientFileCrcPathIdCode = fileCrc.PathIdCode;
                LastClientFileCrcPathId = fileCrc.PathId;
                LastClientFileCrcFilenamePrefixCode = fileCrc.FilenamePrefixCode;
                LastClientFileCrcFilename = fileCrc.Filename;
                LastClientFileCrc = fileCrc.Crc;
                break;
        }
    }

    private void ClearDecodedClientNetMessageState()
    {
        LastClientDecodedNetMessageType = null;
        LastClientDecodedNetMessageName = null;
        LastClientDecodedNetMessagePayloadKind = null;
        LastClientDecodedNetMessagePayloadOffset = null;
        LastClientDecodedNetMessagePayloadLength = null;
        LastClientDecodedNetMessagePayloadBitCount = null;
        LastClientNetTick = null;
        LastClientStringCommand = null;
        _lastClientSetConVars.Clear();
        LastClientSignonState = null;
        LastClientSignonSpawnCount = null;
        LastClientInfoServerCount = null;
        LastClientInfoSendTableCrc = null;
        LastClientInfoIsHltv = null;
        LastClientInfoFriendsId = null;
        LastClientInfoFriendsName = null;
        _lastClientInfoCustomFiles = [];
        LastClientVoiceDataBitCount = null;
        _lastClientVoiceData = [];
        LastClientBaselineAckTick = null;
        LastClientBaselineAckNumber = null;
        _lastClientListenEventMaskWords = [];
        LastClientRespondCvarCookie = null;
        LastClientRespondCvarStatusCode = null;
        LastClientRespondCvarName = null;
        LastClientRespondCvarValue = null;
        LastClientFileCrcReservedBit = null;
        LastClientFileCrcPathIdCode = null;
        LastClientFileCrcPathId = null;
        LastClientFileCrcFilenamePrefixCode = null;
        LastClientFileCrcFilename = null;
        LastClientFileCrc = null;
    }

    private void ApplyClientCommandIntent(Ps3SourceClientPayloadInfo clientPayload, ReadOnlySpan<byte> body)
    {
        if (!EnableClientCommandIntent)
        {
            LastClientCommandDecoded = false;
            return;
        }

        if (TryApplyOfficialClientCommand(clientPayload, body))
        {
            return;
        }

        if (!EnableHeuristicClientCommandIntent)
        {
            LastClientCommandDecoded = false;
            return;
        }

        LastClientCommandDecoded = Ps3SourceClientCommandIntent.TryDecode(
            clientPayload,
            body,
            out var command,
            allowMarkerlessAssociatedObject: EnableHeuristicClientCommandIntent
                && !IsAssociatedSlot90ResetCandidate(clientPayload));
        if (!LastClientCommandDecoded)
        {
            return;
        }

        ApplyDecodedClientCommand(clientPayload, command, isHeuristic: true);
    }

    private bool TryApplyOfficialClientCommand(Ps3SourceClientPayloadInfo clientPayload, ReadOnlySpan<byte> body)
    {
        if (IsAssociatedSlot90ResetCandidate(clientPayload))
        {
            return false;
        }

        if (TryApplyOfficialClcMoveClientCommands(clientPayload, body))
        {
            return true;
        }

        if (!Ps3SourceClientCommandBatch.TryDecodeSingleBitSidecarCommand(
                clientPayload,
                body,
                _previousOfficialClientCommand,
                out var batch)
            || batch.LatestCommand is not { } officialCommand)
        {
            return false;
        }

        var previous = _previousOfficialClientCommand;
        _previousOfficialClientCommand = officialCommand;
        var command = ConvertOfficialClientCommand(previous, officialCommand);
        LastClientCommandDecoded = true;
        ApplyDecodedClientCommand(clientPayload, command);
        return true;
    }

    private bool TryApplyOfficialClcMoveClientCommands(Ps3SourceClientPayloadInfo clientPayload, ReadOnlySpan<byte> body)
    {
        if (!Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(
                clientPayload,
                body,
                out var boundary))
        {
            return false;
        }

        var move = boundary.Move;
        if (!move.TryDecodeUserCmdBatch(_previousOfficialClientCommand, out var batch)
            || batch.ConsumedBits != move.CommandDataBitCount)
        {
            return false;
        }

        var commandCount = Math.Min(move.NewCommands, batch.Commands.Count);
        if (commandCount <= 0)
        {
            return false;
        }

        ApplyNativeSourceClcMoveBoundaryContext(clientPayload, boundary, commandCount);

        // Source stores the most recent new command at index 0, then runs new
        // commands backward so simulation advances oldest-to-newest.
        var previous = _previousOfficialClientCommand;
        for (var i = commandCount - 1; i >= 0; i--)
        {
            var officialCommand = batch.Commands[i];
            var command = ConvertOfficialClientCommand(previous, officialCommand);
            _previousOfficialClientCommand = officialCommand;
            LastClientCommandDecoded = true;
            ApplyDecodedClientCommand(clientPayload, command);
            previous = officialCommand;
        }

        return true;
    }

    private Ps3SourceClientCommandIntent ConvertOfficialClientCommand(
        Ps3SourceUserCmd previous,
        Ps3SourceUserCmd officialCommand)
    {
        var yawDelta = officialCommand.ViewAngleYRaw != previous.ViewAngleYRaw
            ? ShortestAngleDelta(Yaw, DecodeOfficialViewAngle(officialCommand.ViewAngleYRaw, Yaw))
            : officialCommand.MouseDx / 32768.0f * 6.0f;
        var pitchDelta = officialCommand.ViewAngleXRaw != previous.ViewAngleXRaw
            ? Math.Clamp(DecodeOfficialViewAngle(officialCommand.ViewAngleXRaw, Pitch), -89.0f, 89.0f) - Pitch
            : officialCommand.MouseDy / 32768.0f * 3.0f;
        return new Ps3SourceClientCommandIntent(
            ClampCommandAxis(officialCommand.ForwardMove),
            ClampCommandAxis(officialCommand.SideMove),
            ClampCommandAxis(officialCommand.UpMove),
            yawDelta,
            pitchDelta,
            checked((byte)(officialCommand.Buttons & 0xff)),
            checked((byte)(officialCommand.WeaponSelect & 0xff)),
            checked((byte)Math.Clamp((int)TeamNumber, 0, byte.MaxValue)),
            checked((byte)Math.Clamp((int)ClassNumber, 0, byte.MaxValue)));
    }

    private void ApplyDecodedClientCommand(
        Ps3SourceClientPayloadInfo clientPayload,
        Ps3SourceClientCommandIntent command,
        bool isHeuristic = false)
    {
        LastClientCommandForwardMove = MovementFrozen ? (short)0 : command.ForwardMove;
        LastClientCommandSideMove = MovementFrozen ? (short)0 : command.SideMove;
        LastClientCommandUpMove = MovementFrozen ? (short)0 : command.UpMove;
        LastClientCommandButtons = command.Buttons;
        LastClientCommandWeaponSlotHint = command.WeaponSlotHint;
        ApplyNativeSourceClientCommandContext(clientPayload, command, isHeuristic);
        RecordNativeClientCommand(clientPayload, command);

        Yaw = NormalizeAngle(Yaw + command.YawDelta);
        Pitch = Math.Clamp(Pitch + command.PitchDelta, -89.0f, 89.0f);
        var yawRadians = Yaw * MathF.PI / 180.0f;
        var forwardX = MathF.Cos(yawRadians);
        var forwardY = MathF.Sin(yawRadians);
        var sideX = -forwardY;
        var sideY = forwardX;
        var desiredVelocityX = (forwardX * LastClientCommandForwardMove) + (sideX * LastClientCommandSideMove);
        var desiredVelocityY = (forwardY * LastClientCommandForwardMove) + (sideY * LastClientCommandSideMove);
        var desiredSpeed = MathF.Sqrt((desiredVelocityX * desiredVelocityX) + (desiredVelocityY * desiredVelocityY));
        var maxSpeed = MaxSpeed;
        if (desiredSpeed > maxSpeed && desiredSpeed > 0.001f)
        {
            var scale = maxSpeed / desiredSpeed;
            desiredVelocityX *= scale;
            desiredVelocityY *= scale;
        }

        VelocityX = desiredVelocityX;
        VelocityY = desiredVelocityY;
        OriginX += VelocityX / 30.0f;
        OriginY += VelocityY / 30.0f;

        if ((command.Buttons & 0x02) != 0)
        {
            Jumping = 1;
            if (!NoClip && GroundEntityHandle != 0xffffffff && OriginZ <= GroundZ + 0.01f)
            {
                VelocityZ = 268.0f;
                OriginZ += VelocityZ / 30.0f;
                FallVelocity = 0;
                JumpTime = SimulationTime;
                InDuckJump = Ducked;
            }
        }
        else
        {
            Jumping = 0;
        }

        if ((command.Buttons & 0x04) != 0)
        {
            Ducked = 1;
            Ducking = 1;
            ViewOffsetZ = 45.0f;
            DuckTime = SimulationTime;
        }
        else
        {
            Ducked = 0;
            Ducking = 0;
            InDuckJump = 0;
            ViewOffsetZ = 64.0f;
        }

        if ((command.Buttons & 0x01) != 0 && WeaponClip1 > 0)
        {
            ApplyNativeWeaponIntent(fired: true, used: (command.Buttons & 0x08) != 0, command.WeaponSlotHint);
        }
        else
        {
            ApplyNativeWeaponIntent(fired: (command.Buttons & 0x01) != 0, used: (command.Buttons & 0x08) != 0, command.WeaponSlotHint);
        }

        NoInterpParity++;
        SaveMeParity++;
        OnTarget = (byte)((command.Buttons & 0x01) != 0 ? 1 : 0);
        UpdateNativeMovementFlags();
    }

    private static short ClampCommandAxis(float value)
    {
        return checked((short)Math.Clamp(MathF.Round(value), short.MinValue, short.MaxValue));
    }

    private static float DecodeOfficialViewAngle(uint raw, float fallback)
    {
        var value = BitConverter.UInt32BitsToSingle(raw);
        return float.IsFinite(value) ? value : fallback;
    }

    private static float ShortestAngleDelta(float current, float target)
    {
        var delta = NormalizeAngle(target) - NormalizeAngle(current);
        while (delta > 180.0f)
        {
            delta -= 360.0f;
        }

        while (delta < -180.0f)
        {
            delta += 360.0f;
        }

        return delta;
    }

    private void RecordNativeClientCommand(Ps3SourceClientPayloadInfo clientPayload, Ps3SourceClientCommandIntent command)
    {
        _nativeClientCommandHistory.Add(new Tf2SourceClientCommandHistoryEntry(
            LastClientSourcePacketCount,
            LastClientSourceSequence,
            SimulationTime,
            LastClientSourceBodyLength,
            clientPayload.Role.ToString(),
            clientPayload.Shape.ToString(),
            command.ForwardMove,
            command.SideMove,
            command.UpMove,
            command.YawDelta,
            command.PitchDelta,
            command.Buttons,
            command.WeaponSlotHint,
            command.TeamHint,
            command.ClassHint,
            OfficialEatf2ServerDllContracts.CUserCmdStrideBytes,
            OfficialEatf2ServerDllContracts.PhysicsCommandHistoryStrideBytes,
            LastPhysicsSimulationTick,
            PhysicsSimulateTickGate));

        var capacity = NativeClientCommandHistoryCapacity;
        if (_nativeClientCommandHistory.Count > capacity)
        {
            _nativeClientCommandHistory.RemoveRange(0, _nativeClientCommandHistory.Count - capacity);
        }
    }

    private void ApplyNativeSourceTransportControlContext(Ps3SourceClientPayloadInfo clientPayload)
    {
        if (IsAssociatedSlot90ResetCandidate(clientPayload))
        {
            RecordNativeSourceSemanticContext(
                clientPayload,
                "associated-slot90-reset",
                $"tfelf=008be1e8->008b9ad8->00a58418 reset=+0x44 zeroState=+0xac token=0x{clientPayload.PayloadObjectAssociatedToken.GetValueOrDefault():x8} bodyIgnoredByClientPath=true");
        }

        switch (clientPayload.Role)
        {
            case Ps3SourceClientPayloadRole.InitialHandoffProbe:
            case Ps3SourceClientPayloadRole.ReliableAssociationProbe:
            case Ps3SourceClientPayloadRole.AttachedPlayerControlFrame:
            case Ps3SourceClientPayloadRole.ShortControlAck:
            case Ps3SourceClientPayloadRole.SetupControlPayload:
            case Ps3SourceClientPayloadRole.EmbeddedObjectNotice:
                RecordNativeSourceSemanticContext(
                    clientPayload,
                    "transport-control",
                    $"role={clientPayload.Role} semantic={clientPayload.PayloadSemanticRole} reliableType={clientPayload.ReliableAssociationMessageType?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"} attachedKind={clientPayload.AttachedFrameKind?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}");
                break;
        }
    }

    private static bool IsAssociatedSlot90ResetCandidate(Ps3SourceClientPayloadInfo clientPayload)
    {
        return clientPayload.PayloadObjectFrameKind == nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken)
            && clientPayload.AttachedFrameKind is null
            && clientPayload.BitSidecarOffset is null
            && clientPayload.Role is Ps3SourceClientPayloadRole.UserCommandCandidate
                or Ps3SourceClientPayloadRole.BinaryControlPayload;
    }

    private void ApplyNativeSourceClientNetMessageContext(
        Ps3SourceClientPayloadInfo clientPayload,
        Ps3SourceDecodedClientNetMessage decoded)
    {
        RecordNativeSourceSemanticContext(
            clientPayload,
            "source-netmessage",
            $"message={decoded.MessageName} type={decoded.MessageType} payload={decoded.PayloadKind} bits={decoded.PayloadBitCount}");
    }

    private void ApplyNativeSourceClientCommandContext(
        Ps3SourceClientPayloadInfo clientPayload,
        Ps3SourceClientCommandIntent command,
        bool isHeuristic)
    {
        RecordNativeSourceSemanticContext(
            clientPayload,
            isHeuristic ? "source-usercmd-heuristic" : "source-usercmd",
            $"buttons=0x{command.Buttons:x2} move=({command.ForwardMove},{command.SideMove},{command.UpMove}) weaponHint={command.WeaponSlotHint} cusercmdStride=0x{OfficialEatf2ServerDllContracts.CUserCmdStrideBytes:x} physicsHistoryStride=0x{OfficialEatf2ServerDllContracts.PhysicsCommandHistoryStrideBytes:x} LastPhysicsSimulationTick={LastPhysicsSimulationTick} PhysicsSimulateTickGate={PhysicsSimulateTickGate} heuristic={isHeuristic.ToString().ToLowerInvariant()}");
    }

    private void ApplyNativeSourceClcMoveBoundaryContext(
        Ps3SourceClientPayloadInfo clientPayload,
        Ps3SourceNativeToClcMoveBoundary boundary,
        int commandCount)
    {
        var detail =
            $"boundary={boundary.Kind} offset={boundary.PayloadOffset} bytes={boundary.PayloadLength} bits={boundary.PayloadBitCount} commands={commandCount}";
        switch (boundary.Kind)
        {
            case Ps3SourceNativeToClcMoveBoundaryKind.OwnerSlot8Bitstream:
                RecordNativeSourceSemanticContext(
                    clientPayload,
                    "owner-slot8-control",
                    $"tfelf=008be1e8->00a55d38->00a52720->008722a0 {detail}");
                break;
            case Ps3SourceNativeToClcMoveBoundaryKind.OwnerForwarderWord6Bitstream:
            case Ps3SourceNativeToClcMoveBoundaryKind.OwnerForwarderDeferredPointerWord6Bitstream:
            case Ps3SourceNativeToClcMoveBoundaryKind.OwnerForwarderConfigFallbackWord4Bitstream:
                RecordNativeSourceSemanticContext(
                    clientPayload,
                    "owner-forward-context",
                    $"tfelf=008722a0 layout={boundary.Kind} {detail}");
                break;
            case Ps3SourceNativeToClcMoveBoundaryKind.PayloadObjectInnerPayload
                when clientPayload.PayloadObjectFrameKind == nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken):
                RecordNativeSourceSemanticContext(
                    clientPayload,
                    "associated-slot90",
                    $"tfelf=008be1e8->008b9ad8->vtable+0x90 token=0x{clientPayload.PayloadObjectAssociatedToken.GetValueOrDefault():x8} {detail}");
                break;
        }
    }

    private void RecordNativeSourceSemanticContext(
        Ps3SourceClientPayloadInfo clientPayload,
        string contextKind,
        string detail)
    {
        _nativeSourceSemanticContextHistory.Add(new Tf2SourceClientSemanticContextEntry(
            LastClientSourcePacketCount,
            LastClientSourceSequence,
            SimulationTime,
            contextKind,
            detail,
            clientPayload.Role.ToString(),
            clientPayload.Shape.ToString(),
            clientPayload.NativeFrameKind.ToString(),
            clientPayload.BodyLength,
            LastClientSourceBodyHash));

        const int capacity = 128;
        if (_nativeSourceSemanticContextHistory.Count > capacity)
        {
            _nativeSourceSemanticContextHistory.RemoveRange(0, _nativeSourceSemanticContextHistory.Count - capacity);
        }
    }

    private static float NormalizeAngle(float value)
    {
        while (value < 0)
        {
            value += 360.0f;
        }

        while (value >= 360.0f)
        {
            value -= 360.0f;
        }

        return value;
    }

    private void UpdateNativeMovementFlags()
    {
        var flags = SourceFlagClient;
        if (!NoClip && OriginZ <= GroundZ + 0.01f)
        {
            if (GroundEntityHandle == 0xffffffff)
            {
                GroundEntityHandle = WorldGroundEntityHandle;
            }

            flags |= SourceFlagOnGround;
        }
        else
        {
            if (!NoClip)
            {
                GroundEntityHandle = 0xffffffff;
            }
        }

        if (Ducked != 0 || Ducking != 0)
        {
            flags |= SourceFlagDucking;
        }

        if (MovementFrozen)
        {
            flags |= SourceFlagFrozen;
        }

        if (!NoClip)
        {
            WaterLevel = OriginZ < GroundZ - 24.0f ? (byte)2 : (byte)0;
        }

        if (WaterLevel > 0)
        {
            flags |= SourceFlagInWater;
        }

        Flags = flags;
    }

    private static float ClassMaxSpeed(byte classNumber)
    {
        return classNumber switch
        {
            1 => 400.0f,
            2 => 300.0f,
            3 => 240.0f,
            4 => 320.0f,
            5 => 320.0f,
            6 => 300.0f,
            7 => 300.0f,
            8 => 320.0f,
            9 => 300.0f,
            _ => 320.0f
        };
    }

    public void ApplyMapDefaults(
        string mapName,
        int playerIndex,
        bool forceSpawnReset,
        Tf2MapMetadata? mapMetadata = null)
    {
        ApplyObjectGraphDefaults(playerIndex);
        TeamNumber = (uint)(playerIndex % 2 == 0 ? 2 : 3);
        ClassNumber = (byte)(playerIndex % 9 + 1);
        MaxHealth = MaxHealthForClass(ClassNumber);
        Health = MaxHealth;
        ApplyClassWeaponDefaults(resetAmmo: forceSpawnReset);
        if (!forceSpawnReset)
        {
            return;
        }

        if (TryApplyMetadataSpawn(mapMetadata, playerIndex))
        {
            return;
        }

        var normalized = mapName.ToLowerInvariant();
        var teamSign = TeamNumber == 2 ? -1.0f : 1.0f;
        if (normalized.StartsWith("cp_dustbowl", StringComparison.Ordinal) || normalized.StartsWith("cp_db", StringComparison.Ordinal))
        {
            OriginX = teamSign * 760.0f;
            OriginY = TeamNumber == 2 ? -420.0f : 420.0f;
            OriginZ = 96.0f;
            GroundZ = OriginZ;
            Yaw = TeamNumber == 2 ? 0.0f : 180.0f;
            UpdateNativeMovementFlags();
            return;
        }

        OriginX = teamSign * 1600.0f;
        OriginY = TeamNumber == 2 ? -256.0f : 256.0f;
        OriginZ = 128.0f;
        GroundZ = OriginZ;
        Yaw = TeamNumber == 2 ? 0.0f : 180.0f;
        UpdateNativeMovementFlags();
    }

    private bool TryApplyMetadataSpawn(Tf2MapMetadata? mapMetadata, int playerIndex)
    {
        if (mapMetadata is null || mapMetadata.SpawnPoints.Count == 0)
        {
            return false;
        }

        var teamSpawns = mapMetadata.SpawnPoints
            .Where(spawn => spawn.Enabled && spawn.TeamNumber == TeamNumber)
            .ToArray();
        if (teamSpawns.Length == 0)
        {
            teamSpawns = mapMetadata.SpawnPoints
                .Where(static spawn => spawn.Enabled)
                .ToArray();
        }

        if (teamSpawns.Length == 0)
        {
            return false;
        }

        var spawn = teamSpawns[Math.Abs(playerIndex) % teamSpawns.Length];
        OriginX = spawn.X;
        OriginY = spawn.Y;
        OriginZ = spawn.Z;
        GroundZ = OriginZ;
        Yaw = spawn.Yaw;
        UpdateNativeMovementFlags();
        return true;
    }

    public void ApplyObjectGraphDefaults(int playerIndex)
    {
        var root = RootObjectId == 0 ? DefaultRootObjectId : RootObjectId;
        var localPlayer = LocalPlayerObjectId == 0 ? DefaultLocalPlayerObjectId : LocalPlayerObjectId;
        if (playerIndex == 0 && root == DefaultRootObjectId && localPlayer == DefaultLocalPlayerObjectId)
        {
            FrozenStatePeerObjectIds = [0x9f, 0x93, 0x95, 0x9c, 0x6d];
            return;
        }

        var peers = new List<uint>();
        AddPeer(peers, checked(root - 0x02));
        AddPeer(peers, 0x0000009f);
        AddPeer(peers, 0x00000093);
        AddPeer(peers, 0x00000095);
        AddPeer(peers, 0x0000009c);
        AddPeer(peers, 0x0000006d);
        FrozenStatePeerObjectIds = [.. peers];
    }

    public void ApplyNativeSourceObjectIds(int playerId)
    {
        if (playerId > 36)
        {
            RootObjectId = checked((uint)(playerId - 36));
            LocalPlayerObjectId = RootObjectId > 0x1b
                ? RootObjectId - 0x1b
                : DefaultLocalPlayerObjectId;
        }
        else
        {
            RootObjectId = DefaultRootObjectId;
            LocalPlayerObjectId = DefaultLocalPlayerObjectId;
        }

        ApplyObjectGraphDefaults(playerIndex: 0);
    }

    private static void AddPeer(List<uint> peers, uint objectId)
    {
        if (!peers.Contains(objectId))
        {
            peers.Add(objectId);
        }
    }

    private static ushort MaxHealthForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => 125,
            3 => 200,
            4 => 175,
            5 => 150,
            6 => 300,
            7 => 175,
            8 => 125,
            9 => 125,
            _ => 125
        };
    }

    private static uint DefaultClip1ForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => 25,
            4 => 4,
            6 => 200,
            7 => 200,
            8 => 6,
            _ => 6
        };
    }

    private static uint DefaultClip2ForClass(byte classNumber)
    {
        return classNumber switch
        {
            4 => 8,
            6 => 0,
            9 => 0,
            _ => 0
        };
    }

    private static uint DefaultPrimaryAmmoForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => 75,
            4 => 16,
            5 => 0,
            6 => 200,
            7 => 200,
            8 => 24,
            9 => 32,
            _ => 32
        };
    }

    private static uint DefaultSecondaryAmmoForClass(byte classNumber)
    {
        return classNumber switch
        {
            4 => 24,
            6 => 0,
            9 => 200,
            _ => 32
        };
    }

    private static uint Fnva32(ReadOnlySpan<byte> data)
    {
        var hash = 2166136261u;
        foreach (var b in data)
        {
            hash ^= b;
            hash *= 16777619u;
        }

        return hash;
    }
}

public sealed class Tf2EngineerObjectState
{
    public uint UpgradeLevel { get; set; }

    public uint State { get; set; }

    public uint AmmoShells { get; set; }

    public uint AmmoRockets { get; set; }

    public uint UpgradeMetal { get; set; }

    public float RechargeTime { get; set; }

    public uint TimesUsed { get; set; }

    public float YawToExit { get; set; }
}

public readonly record struct Tf2SourceCommandSimulation(
    bool Active,
    bool Fired,
    bool Used,
    bool Moved,
    uint TeamNumber)
{
    public static Tf2SourceCommandSimulation Inactive { get; } = new(false, false, false, false, 0);
}

public readonly record struct Tf2SourceClientCommandHistoryEntry(
    int PacketCount,
    ushort Sequence,
    float SimulationTime,
    int BodyLength,
    string PayloadRole,
    string PayloadShape,
    short ForwardMove,
    short SideMove,
    short UpMove,
    float YawDelta,
    float PitchDelta,
    byte Buttons,
    byte WeaponSlotHint,
    byte TeamHint,
    byte ClassHint,
    int CUserCmdStrideBytes,
    int PhysicsCommandHistoryStrideBytes,
    uint LastPhysicsSimulationTick,
    uint PhysicsSimulateTickGate);

public readonly record struct Tf2SourceClientSemanticContextEntry(
    int PacketCount,
    ushort Sequence,
    float SimulationTime,
    string ContextKind,
    string Detail,
    string PayloadRole,
    string PayloadShape,
    string NativeFrameKind,
    int BodyLength,
    uint BodyHash);

public sealed class Tf2SourceWorldState
{
    private const uint RoundStateRunning = 4;

    private const uint RoundStateTeamWin = 5;

    private const uint RoundStateGameOver = 8;

    private string _mapName = "ctf_2fort";

    private string _gameMode = "capture-the-flag";

    private Tf2MapMetadata? _mapMetadata;

    private float? _roundWinStartedSeconds;

    private readonly Dictionary<uint, Tf2SourceTeamState> _teams = new()
    {
        [0] = new Tf2SourceTeamState(0, "Unassigned"),
        [2] = new Tf2SourceTeamState(2, "RED"),
        [3] = new Tf2SourceTeamState(3, "BLU")
    };

    public uint GameType { get; private set; }

    public uint RoundState { get; set; } = 3;

    public bool AutoAdvanceRoundState { get; set; } = true;

    public uint WinningTeam { get; set; }

    public bool InSetup { get; set; }

    public bool InOvertime { get; set; }

    public bool InWaitingForPlayers { get; private set; } = true;

    public int TimeLimitMinutes { get; private set; } = 30;

    public int MaxRounds { get; private set; } = 5;

    public int FlagCaptureLimit { get; private set; } = 3;

    public bool AutoBalance { get; private set; } = true;

    public bool FriendlyFire { get; private set; }

    public int ForceCamera { get; private set; }

    public bool DisableRespawnTimes { get; private set; }

    public bool WeaponCriticals { get; private set; } = true;

    public bool DamageSpreadDisabled { get; private set; }

    public bool AllTalk { get; private set; }

    public bool Cheats { get; private set; }

    public float Gravity { get; private set; } = 800.0f;

    public Tf2MapBounds? MapBounds { get; private set; }

    public bool TournamentMode { get; private set; }

    public bool TournamentReadymode { get; private set; }

    public bool AwaitingReadyRestart { get; private set; }

    public uint UpdateCapHudParity { get; private set; }

    public float ElapsedSeconds { get; private set; }

    public int RoundLengthSeconds => Math.Max(0, TimeLimitMinutes * 60);

    public Tf2RoundTimerState Timer { get; } = new();

    public IReadOnlyDictionary<uint, Tf2SourceTeamState> Teams => _teams;

    public List<Tf2ObjectivePointState> ControlPoints { get; } = [];

    public List<Tf2FlagState> Flags { get; } = [];

    public IReadOnlyList<Tf2MapBrushVolume> BrushVolumes => _mapMetadata?.BrushVolumes ?? [];

    public void ApplyMapDefaults(
        string mapName,
        string gameMode,
        int timeLimitMinutes,
        int maxRounds,
        int flagCaptureLimit,
        bool autoBalance,
        IEnumerable<PlayerSession> players,
        Tf2MapMetadata? mapMetadata = null)
    {
        _mapName = string.IsNullOrWhiteSpace(mapName) ? "ctf_2fort" : mapName;
        _gameMode = string.IsNullOrWhiteSpace(gameMode) ? "capture-the-flag" : gameMode;
        _mapMetadata = mapMetadata;
        MapBounds = mapMetadata?.Bounds;
        TimeLimitMinutes = Math.Max(0, timeLimitMinutes);
        MaxRounds = Math.Max(0, maxRounds);
        FlagCaptureLimit = Math.Max(0, flagCaptureLimit);
        AutoBalance = autoBalance;
        TournamentReadymode = false;
        AwaitingReadyRestart = false;
        GameType = SourceGameType(_mapName, _gameMode);
        RoundState = 3;
        WinningTeam = 0;
        InSetup = false;
        InOvertime = false;
        _roundWinStartedSeconds = null;
        Timer.ApplyRoundLength(RoundLengthSeconds);
        BuildObjectiveDefaults(_mapName, _gameMode, _mapMetadata);
        BuildFlagDefaults(_mapName, _gameMode, _mapMetadata);
        UpdateTeamMemberCounts(players);
    }

    public void AdvanceSnapshot(int snapshotFrameIndex, IEnumerable<PlayerSession> players)
    {
        var playerList = players.Where(static player => player.State != PlayerJoinState.Left).ToArray();
        UpdateCapHudParity = checked((uint)Math.Max(snapshotFrameIndex, 0));
        ElapsedSeconds = UpdateCapHudParity / 30.0f;
        if (RoundState == RoundStateTeamWin
            && _roundWinStartedSeconds is { } roundWinStartedSeconds
            && ElapsedSeconds - roundWinStartedSeconds >= 5.0f)
        {
            RestartGeneratedRound();
        }

        if (AutoAdvanceRoundState && RoundState < RoundStateRunning && snapshotFrameIndex >= 2)
        {
            RoundState = RoundStateRunning;
            InSetup = false;
            InOvertime = false;
            Timer.State = 1;
        }

        foreach (var player in playerList)
        {
            player.SourceState.ApplyWorldRules(this);
        }

        AdvanceGeneratedGameplay(playerList);
        Timer.Advance(ElapsedSeconds);
        UpdateTeamMemberCounts(playerList);
    }

    public Tf2SourceTeamState Team(uint teamNumber)
    {
        if (_teams.TryGetValue(teamNumber, out var team))
        {
            return team;
        }

        return _teams[0];
    }

    public void RestartRound()
    {
        RestartGeneratedRound();
        AwaitingReadyRestart = false;
    }

    public void SetTournamentMode(bool enabled)
    {
        TournamentMode = enabled;
        if (!enabled)
        {
            SetTournamentReadymode(false);
        }
    }

    public void SetTournamentReadymode(bool enabled)
    {
        TournamentReadymode = enabled;
        AwaitingReadyRestart = enabled;
        AutoAdvanceRoundState = !enabled;
        if (enabled)
        {
            InWaitingForPlayers = true;
            Timer.State = 0;
        }
        else
        {
            AwaitingReadyRestart = false;
        }
    }

    public void SetAwaitingReadyRestart(bool enabled)
    {
        AwaitingReadyRestart = enabled;
        if (enabled)
        {
            AutoAdvanceRoundState = false;
            InWaitingForPlayers = true;
            Timer.State = 0;
        }
    }

    public bool ForceRoundWin(uint teamNumber)
    {
        if (teamNumber is not (2 or 3))
        {
            return false;
        }

        BeginGeneratedRoundWin(teamNumber);
        return true;
    }

    public void SetTimerPaused(bool paused)
    {
        Timer.TimerPaused = paused;
        Timer.StartPaused = paused;
    }

    public void SetTimeRemaining(int seconds)
    {
        Timer.SetTimeRemaining(seconds, ElapsedSeconds);
    }

    public void ApplySourceCvar(string normalizedName, string value)
    {
        switch (normalizedName)
        {
            case "mp_friendlyfire" when TryParseBoolValue(value, out var friendlyFire):
                FriendlyFire = friendlyFire;
                break;
            case "mp_forcecamera" when int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var forceCamera):
                ForceCamera = Math.Clamp(forceCamera, 0, 3);
                break;
            case "mp_disable_respawn_times" when TryParseBoolValue(value, out var disableRespawnTimes):
                DisableRespawnTimes = disableRespawnTimes;
                break;
            case "tf_weapon_criticals" when TryParseBoolValue(value, out var weaponCriticals):
                WeaponCriticals = weaponCriticals;
                break;
            case "tf_damage_disablespread" when TryParseBoolValue(value, out var damageSpreadDisabled):
                DamageSpreadDisabled = damageSpreadDisabled;
                break;
            case "sv_alltalk" when TryParseBoolValue(value, out var allTalk):
                AllTalk = allTalk;
                break;
            case "sv_cheats" when TryParseBoolValue(value, out var cheats):
                Cheats = cheats;
                break;
            case "sv_gravity" when float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gravity):
                Gravity = Math.Clamp(gravity, 0.0f, 2400.0f);
                break;
        }
    }

    private static bool TryParseBoolValue(string valueText, out bool value)
    {
        value = false;
        switch (valueText.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                value = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                value = false;
                return true;
            default:
                return false;
        }
    }

    public bool SetTeamScore(uint teamNumber, uint score, IEnumerable<PlayerSession> players)
    {
        if (teamNumber is not (2 or 3))
        {
            return false;
        }

        var team = Team(teamNumber);
        var playerScore = players
            .Where(player => player.State != PlayerJoinState.Left && player.SourceState.TeamNumber == teamNumber)
            .Aggregate(0UL, static (sum, player) => sum + player.SourceState.Score);
        team.ScoreBase = checked((uint)Math.Clamp((long)score - (long)Math.Min(playerScore, uint.MaxValue), 0L, uint.MaxValue));
        UpdateTeamMemberCounts(players);
        return true;
    }

    public bool AddTeamScore(uint teamNumber, int delta, IEnumerable<PlayerSession> players)
    {
        if (teamNumber is not (2 or 3))
        {
            return false;
        }

        UpdateTeamMemberCounts(players);
        var team = Team(teamNumber);
        var score = Math.Clamp((long)team.Score + delta, 0, uint.MaxValue);
        return SetTeamScore(teamNumber, checked((uint)score), players);
    }

    public bool SetTeamFlagCaptures(uint teamNumber, uint captures, IEnumerable<PlayerSession> players)
    {
        if (teamNumber is not (2 or 3))
        {
            return false;
        }

        var team = Team(teamNumber);
        var playerCaptures = players
            .Where(player => player.State != PlayerJoinState.Left && player.SourceState.TeamNumber == teamNumber)
            .Aggregate(0UL, static (sum, player) => sum + player.SourceState.Captures);
        team.FlagCapturesBase = checked((uint)Math.Clamp((long)captures - (long)Math.Min(playerCaptures, uint.MaxValue), 0L, uint.MaxValue));
        UpdateTeamMemberCounts(players);
        return true;
    }

    public bool TakeFlag(uint flagTeam, PlayerSession carrier)
    {
        if (flagTeam is not (2 or 3) || carrier.SourceState.TeamNumber is not (2 or 3))
        {
            return false;
        }

        if (carrier.SourceState.TeamNumber == flagTeam)
        {
            return false;
        }

        var flag = Flag(flagTeam);
        if (flag is null)
        {
            return false;
        }

        flag.State = Tf2FlagStateKind.Carried;
        flag.CarrierPlayerId = carrier.PlayerId;
        flag.CarrierName = carrier.Name;
        flag.X = carrier.SourceState.OriginX;
        flag.Y = carrier.SourceState.OriginY;
        flag.Z = carrier.SourceState.OriginZ;
        flag.LastChangeSeconds = ElapsedSeconds;
        UpdateCapHudParity++;
        return true;
    }

    public bool DropFlag(uint flagTeam, PlayerSession? carrier = null)
    {
        if (flagTeam is not (2 or 3))
        {
            return false;
        }

        var flag = Flag(flagTeam);
        if (flag is null)
        {
            return false;
        }

        flag.State = Tf2FlagStateKind.Dropped;
        if (carrier is not null)
        {
            flag.CarrierPlayerId = carrier.PlayerId;
            flag.CarrierName = carrier.Name;
            flag.X = carrier.SourceState.OriginX;
            flag.Y = carrier.SourceState.OriginY;
            flag.Z = carrier.SourceState.OriginZ;
        }

        flag.LastChangeSeconds = ElapsedSeconds;
        UpdateCapHudParity++;
        return true;
    }

    public bool ReturnFlag(uint flagTeam)
    {
        if (flagTeam is not (2 or 3))
        {
            return false;
        }

        var flag = Flag(flagTeam);
        if (flag is null)
        {
            return false;
        }

        flag.ReturnHome(ElapsedSeconds);
        UpdateCapHudParity++;
        return true;
    }

    public bool CaptureFlag(PlayerSession carrier, uint flagTeam, IEnumerable<PlayerSession> players)
    {
        if (flagTeam is not (2 or 3) || carrier.SourceState.TeamNumber is not (2 or 3))
        {
            return false;
        }

        if (carrier.SourceState.TeamNumber == flagTeam)
        {
            return false;
        }

        var flag = Flag(flagTeam);
        if (flag is null)
        {
            return false;
        }

        carrier.SourceState.Captures++;
        carrier.SourceState.Score += 5;
        flag.ReturnHome(ElapsedSeconds);
        UpdateCapHudParity++;
        UpdateTeamMemberCounts(players);
        if (FlagCaptureLimit > 0 && Team(carrier.SourceState.TeamNumber).FlagCaptures >= FlagCaptureLimit)
        {
            BeginGeneratedRoundWin(carrier.SourceState.TeamNumber);
        }

        return true;
    }

    public bool SetControlPointOwner(uint pointIndex, uint teamNumber, float capturePercentage = 1.0f)
    {
        if (teamNumber is not (0 or 2 or 3))
        {
            return false;
        }

        var point = ControlPoints.FirstOrDefault(point => point.Index == pointIndex);
        if (point is null)
        {
            return false;
        }

        point.OwnerTeam = teamNumber;
        point.CappingTeam = 0;
        point.TeamInZone = teamNumber;
        point.Blocked = false;
        point.LazyCapPercentage = Math.Clamp(capturePercentage, 0.0f, 1.0f);
        UpdateCapHudParity++;
        return true;
    }

    public bool CaptureControlPoint(uint pointIndex, uint teamNumber)
    {
        if (!SetControlPointOwner(pointIndex, teamNumber, 1.0f))
        {
            return false;
        }

        if (ControlPoints.Count > 0 && ControlPoints.All(point => point.OwnerTeam == teamNumber))
        {
            BeginGeneratedRoundWin(teamNumber);
        }

        return true;
    }

    public bool SetRoundState(uint roundState, uint winningTeam = 0)
    {
        if (roundState > RoundStateGameOver || winningTeam is not (0 or 2 or 3))
        {
            return false;
        }

        RoundState = roundState;
        WinningTeam = winningTeam;
        InSetup = roundState == 3;
        InOvertime = false;
        Timer.State = roundState switch
        {
            4 => 1,
            5 or 8 => 2,
            _ => 0
        };
        if (roundState is 5 or 8)
        {
            _roundWinStartedSeconds = ElapsedSeconds;
        }
        else
        {
            _roundWinStartedSeconds = null;
        }

        return true;
    }

    public float[] NextRespawnWaveBuckets()
    {
        return
        [
            0,
            0,
            Team(2).NextRespawnWaveSeconds,
            Team(3).NextRespawnWaveSeconds
        ];
    }

    public float[] TeamRespawnWaveTimeBuckets()
    {
        return
        [
            0,
            0,
            Team(2).RespawnWaveTimeSeconds,
            Team(3).RespawnWaveTimeSeconds
        ];
    }

    public uint[] TeamMemberBuckets()
    {
        return
        [
            Team(0).MemberCount,
            0,
            Team(2).MemberCount,
            Team(3).MemberCount
        ];
    }

    public void UpdateTeamMemberCounts(IEnumerable<PlayerSession> players)
    {
        foreach (var team in _teams.Values)
        {
            team.MemberCount = 0;
            team.Score = team.ScoreBase;
            team.FlagCaptures = team.FlagCapturesBase;
        }

        var playerCount = 0;
        foreach (var player in players)
        {
            playerCount++;
            var team = Team(player.SourceState.TeamNumber);
            team.MemberCount++;
            team.Score += player.SourceState.Score;
            team.FlagCaptures += player.SourceState.Captures;
        }

        InWaitingForPlayers = playerCount == 0 || (AutoAdvanceRoundState && RoundState < 4 && playerCount < 2);
    }

    private void AdvanceGeneratedGameplay(IEnumerable<PlayerSession> players)
    {
        foreach (var player in players)
        {
            if (RoundState >= RoundStateTeamWin)
            {
                continue;
            }

            var simulation = player.SourceState.ConsumeGeneratedCommandSimulation();
            if (!simulation.Active || simulation.TeamNumber is not (2 or 3))
            {
                continue;
            }

            if (GameType == 1)
            {
                AdvanceGeneratedCtf(player, simulation);
            }
            else if (GameType == 2)
            {
                AdvanceGeneratedControlPoint(player, simulation);
            }
        }
    }

    private void AdvanceGeneratedCtf(PlayerSession player, Tf2SourceCommandSimulation simulation)
    {
        var capEveryTicks = FlagCaptureLimit == 1
            ? 12U
            : simulation.Fired ? 48U : 72U;
        if (player.SourceState.GeneratedObjectiveTicks % capEveryTicks != 0)
        {
            return;
        }

        if (FlagCaptureLimit > 0 && player.SourceState.Captures >= FlagCaptureLimit)
        {
            return;
        }

        player.SourceState.Captures++;
        player.SourceState.Score += 5;
        if (FlagCaptureLimit > 0 && player.SourceState.Captures >= FlagCaptureLimit)
        {
            BeginGeneratedRoundWin(simulation.TeamNumber);
        }
    }

    private void AdvanceGeneratedControlPoint(PlayerSession player, Tf2SourceCommandSimulation simulation)
    {
        var point = ControlPoints.FirstOrDefault(point => point.OwnerTeam != simulation.TeamNumber)
            ?? ControlPoints.FirstOrDefault();
        if (point is null)
        {
            return;
        }

        point.TeamInZone = simulation.TeamNumber;
        point.CappingTeam = simulation.TeamNumber;
        point.Blocked = false;
        point.LazyCapPercentage = Math.Clamp(
            point.LazyCapPercentage + (simulation.Fired ? 0.20f : 0.10f),
            0.0f,
            1.0f);
        if (point.LazyCapPercentage < 1.0f)
        {
            return;
        }

        if (point.OwnerTeam != simulation.TeamNumber)
        {
            point.OwnerTeam = simulation.TeamNumber;
            player.SourceState.Captures++;
            player.SourceState.Score += 5;
            UpdateCapHudParity++;
        }

        if (ControlPoints.Count > 0 && ControlPoints.All(point => point.OwnerTeam == simulation.TeamNumber))
        {
            BeginGeneratedRoundWin(simulation.TeamNumber);
        }
    }

    private void BeginGeneratedRoundWin(uint teamNumber)
    {
        if (RoundState >= RoundStateTeamWin)
        {
            return;
        }

        WinningTeam = teamNumber;
        var team = Team(teamNumber);
        team.RoundsWon++;
        RoundState = MaxRounds > 0 && team.RoundsWon >= MaxRounds
            ? RoundStateGameOver
            : RoundStateTeamWin;
        _roundWinStartedSeconds = ElapsedSeconds;
        InSetup = false;
        InOvertime = false;
        Timer.State = 2;
        var restartAt = Math.Max(ElapsedSeconds + 5.0f, 5.0f);
        Timer.RestartRoundTimeSeconds = restartAt;
        Timer.MapResetTimeSeconds = restartAt + 5.0f;
    }

    private void RestartGeneratedRound()
    {
        RoundState = 3;
        WinningTeam = 0;
        InSetup = false;
        InOvertime = false;
        _roundWinStartedSeconds = null;
        Timer.ApplyRoundLength(RoundLengthSeconds);
        BuildObjectiveDefaults(_mapName, _gameMode, _mapMetadata);
        BuildFlagDefaults(_mapName, _gameMode, _mapMetadata);
    }

    private void BuildObjectiveDefaults(string mapName, string gameMode, Tf2MapMetadata? mapMetadata)
    {
        ControlPoints.Clear();
        if (mapMetadata is not null && mapMetadata.ControlPoints.Count > 0)
        {
            foreach (var controlPoint in mapMetadata.ControlPoints.OrderBy(static point => point.Index))
            {
                var point = new Tf2ObjectivePointState(controlPoint.Index, controlPoint.X, controlPoint.Y, controlPoint.Z)
                {
                    OwnerTeam = controlPoint.DefaultOwnerTeam,
                    PreviousPoint = controlPoint.Index,
                    TeamBaseIcon = controlPoint.DefaultOwnerTeam is 2 ? 0U : controlPoint.DefaultOwnerTeam is 3 ? 1U : 0U,
                    BaseControlPoint = controlPoint.Index
                };
                ApplyCaptureAreaDefaults(point, controlPoint, mapMetadata.CaptureAreas);
                ControlPoints.Add(point);
            }

            return;
        }

        if (!IsControlPointMap(mapName, gameMode))
        {
            return;
        }

        var normalized = mapName.ToLowerInvariant();
        if (normalized.StartsWith("cp_dustbowl", StringComparison.Ordinal) || normalized.StartsWith("cp_db", StringComparison.Ordinal))
        {
            ControlPoints.Add(new Tf2ObjectivePointState(0, -640, 0, 64)
            {
                OwnerTeam = 2,
                PreviousPoint = 0,
                TeamBaseIcon = 0,
                BaseControlPoint = 0
            });
            ControlPoints.Add(new Tf2ObjectivePointState(1, 640, 0, 64)
            {
                OwnerTeam = 3,
                PreviousPoint = 1,
                TeamBaseIcon = 1,
                BaseControlPoint = 1
            });
            return;
        }

        ControlPoints.Add(new Tf2ObjectivePointState(0, -512, 0, 64)
        {
            OwnerTeam = 2,
            PreviousPoint = 0
        });
        ControlPoints.Add(new Tf2ObjectivePointState(1, 512, 0, 64)
        {
            OwnerTeam = 3,
            PreviousPoint = 1
        });
    }

    private static void ApplyCaptureAreaDefaults(
        Tf2ObjectivePointState point,
        Tf2MapControlPoint controlPoint,
        IReadOnlyList<Tf2MapCaptureArea> captureAreas)
    {
        var area = captureAreas.FirstOrDefault(area =>
            string.Equals(area.ControlPointTargetName, controlPoint.TargetName, StringComparison.OrdinalIgnoreCase));
        if (area is null)
        {
            return;
        }

        point.TeamCanCapRed = area.RedCanCap;
        point.TeamCanCapBlue = area.BlueCanCap;
        point.RequiredCappersRed = area.RequiredCappersRed;
        point.RequiredCappersBlue = area.RequiredCappersBlue;
        point.CapTimeRed = area.CapTimeSeconds;
        point.CapTimeBlue = area.CapTimeSeconds;
    }

    private void BuildFlagDefaults(string mapName, string gameMode, Tf2MapMetadata? mapMetadata)
    {
        Flags.Clear();
        if (mapMetadata is not null && mapMetadata.Flags.Count > 0)
        {
            foreach (var flag in mapMetadata.Flags.Where(static flag => flag.TeamNumber is 2 or 3))
            {
                Flags.Add(new Tf2FlagState(flag.TeamNumber, flag.X, flag.Y, flag.Z));
            }

            return;
        }

        if (!IsCtfMap(mapName, gameMode))
        {
            return;
        }

        Flags.Add(new Tf2FlagState(2, -512, 0, 96));
        Flags.Add(new Tf2FlagState(3, 512, 0, 96));
    }

    private Tf2FlagState? Flag(uint teamNumber)
    {
        return Flags.FirstOrDefault(flag => flag.TeamNumber == teamNumber);
    }

    private static uint SourceGameType(string mapName, string gameMode)
    {
        var normalizedMode = gameMode.ToLowerInvariant();
        var normalizedMap = mapName.ToLowerInvariant();
        if (normalizedMode.Contains("capture", StringComparison.Ordinal)
            || normalizedMode.Contains("flag", StringComparison.Ordinal)
            || normalizedMap.StartsWith("ctf_", StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedMode.Contains("control", StringComparison.Ordinal)
            || normalizedMode.Contains("point", StringComparison.Ordinal)
            || normalizedMap.StartsWith("cp_", StringComparison.Ordinal))
        {
            return 2;
        }

        return 0;
    }

    private static bool IsControlPointMap(string mapName, string gameMode)
    {
        return mapName.StartsWith("cp_", StringComparison.OrdinalIgnoreCase)
            || gameMode.Contains("control", StringComparison.OrdinalIgnoreCase)
            || gameMode.Contains("point", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCtfMap(string mapName, string gameMode)
    {
        return mapName.StartsWith("ctf_", StringComparison.OrdinalIgnoreCase)
            || gameMode.Contains("capture", StringComparison.OrdinalIgnoreCase)
            || gameMode.Contains("flag", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class Tf2RoundTimerState
{
    public bool TimerPaused { get; set; }

    public bool IsDisabled { get; set; }

    public bool ShowInHud { get; set; } = true;

    public bool AutoCountdown { get; set; }

    public bool StartPaused { get; set; }

    public uint SetupTimeLengthSeconds { get; set; }

    public uint State { get; set; }

    public float TimeRemainingSeconds { get; private set; }

    public float TimerEndTimeSeconds { get; private set; }

    public uint TimerMaxLengthSeconds { get; private set; }

    public uint TimerLengthSeconds { get; private set; }

    public uint TimerInitialLengthSeconds { get; private set; }

    public float RestartRoundTimeSeconds { get; set; }

    public float MapResetTimeSeconds { get; set; }

    public void ApplyRoundLength(int roundLengthSeconds)
    {
        var length = checked((uint)Math.Max(0, roundLengthSeconds));
        TimerMaxLengthSeconds = length;
        TimerLengthSeconds = length;
        TimerInitialLengthSeconds = length;
        TimeRemainingSeconds = length;
        TimerEndTimeSeconds = length;
        RestartRoundTimeSeconds = length;
        MapResetTimeSeconds = length;
        TimerPaused = false;
        IsDisabled = false;
        ShowInHud = true;
        AutoCountdown = false;
        StartPaused = false;
        SetupTimeLengthSeconds = 0;
        State = 0;
    }

    public void SetTimeRemaining(int seconds, float elapsedSeconds)
    {
        var remaining = Math.Max(0, seconds);
        var length = checked((uint)Math.Ceiling(Math.Max(remaining, elapsedSeconds + remaining)));
        TimerMaxLengthSeconds = Math.Max(TimerMaxLengthSeconds, length);
        TimerLengthSeconds = length;
        TimerInitialLengthSeconds = Math.Max(TimerInitialLengthSeconds, length);
        TimeRemainingSeconds = remaining;
        TimerEndTimeSeconds = elapsedSeconds + remaining;
        RestartRoundTimeSeconds = TimerEndTimeSeconds;
        MapResetTimeSeconds = TimerEndTimeSeconds;
        State = remaining > 0 ? 1U : State;
    }

    public void Advance(float elapsedSeconds)
    {
        if (IsDisabled)
        {
            TimeRemainingSeconds = 0;
            TimerEndTimeSeconds = 0;
            return;
        }

        TimerEndTimeSeconds = TimerLengthSeconds;
        if (!TimerPaused)
        {
            TimeRemainingSeconds = Math.Max(0, TimerLengthSeconds - elapsedSeconds);
        }
    }
}

public sealed class Tf2SourceTeamState(uint teamNumber, string name)
{
    public uint TeamNumber { get; } = teamNumber;

    public string Name { get; set; } = name;

    public uint Score { get; set; }

    public uint ScoreBase { get; set; }

    public uint RoundsWon { get; set; }

    public uint FlagCaptures { get; set; }

    public uint FlagCapturesBase { get; set; }

    public uint MemberCount { get; set; }

    public float NextRespawnWaveSeconds { get; set; } = 30;

    public float RespawnWaveTimeSeconds { get; set; } = 10;
}

public enum Tf2FlagStateKind : uint
{
    Home = 0,
    Carried = 1,
    Dropped = 2
}

public sealed class Tf2FlagState(uint teamNumber, float homeX, float homeY, float homeZ)
{
    public uint TeamNumber { get; } = teamNumber;

    public Tf2FlagStateKind State { get; set; } = Tf2FlagStateKind.Home;

    public int? CarrierPlayerId { get; set; }

    public string? CarrierName { get; set; }

    public float HomeX { get; } = homeX;

    public float HomeY { get; } = homeY;

    public float HomeZ { get; } = homeZ;

    public float X { get; set; } = homeX;

    public float Y { get; set; } = homeY;

    public float Z { get; set; } = homeZ;

    public float LastChangeSeconds { get; set; }

    public void ReturnHome(float elapsedSeconds)
    {
        State = Tf2FlagStateKind.Home;
        CarrierPlayerId = null;
        CarrierName = null;
        X = HomeX;
        Y = HomeY;
        Z = HomeZ;
        LastChangeSeconds = elapsedSeconds;
    }
}

public sealed class Tf2ObjectivePointState(uint index, float x, float y, float z)
{
    public uint Index { get; } = index;

    public float X { get; set; } = x;

    public float Y { get; set; } = y;

    public float Z { get; set; } = z;

    public bool Visible { get; set; } = true;

    public float LazyCapPercentage { get; set; }

    public uint OwnerTeam { get; set; }

    public uint CappingTeam { get; set; }

    public uint TeamInZone { get; set; }

    public bool Blocked { get; set; }

    public uint PreviousPoint { get; set; }

    public bool TeamCanCapRed { get; set; } = true;

    public bool TeamCanCapBlue { get; set; } = true;

    public uint RequiredCappersRed { get; set; } = 1;

    public uint RequiredCappersBlue { get; set; } = 1;

    public float CapTimeRed { get; set; } = 4;

    public float CapTimeBlue { get; set; } = 4;

    public uint TeamBaseIcon { get; set; }

    public uint BaseControlPoint { get; set; }

    public bool InMiniRound { get; set; }

    public bool WarnOnCap { get; set; } = true;
}
