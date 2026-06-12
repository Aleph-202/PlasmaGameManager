using System.Text.Json;

namespace PlasmaGameManager.Server;

public sealed record Tf2SourceLaunchProfile(
    string MapName,
    string GameMode,
    string HostName,
    string BackendHost,
    string BackendPort,
    int? MaxPlayers,
    bool IsRanked,
    string RankingMode,
    int? TimeLimitMinutes,
    int? MaxRounds,
    int? FlagCaptureLimit,
    bool? AutoBalance,
    string DurationPreset,
    string ServerPopulation,
    string Version,
    string[] SourceArguments,
    long? LocalId = null,
    long? GameId = null,
    int? PreferredPlayerId = null,
    string? PlayerName = null,
    string? JoinTicket = null,
    string? EncryptionKey = null,
    string? UniqueGameId = null,
    ulong? ServerUid = null)
{
    public static Tf2SourceLaunchProfile LoadFromJsonFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Tf2SourceLaunchProfile>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize TF2 Source launch profile from {path}.");
    }

    public GameManagerSessionOptions ToSessionOptions(GameManagerSessionOptions fallback)
    {
        return fallback with
        {
            LocalId = LocalId ?? fallback.LocalId,
            GameId = GameId ?? fallback.GameId,
            MaxPlayers = MaxPlayers ?? fallback.MaxPlayers,
            MapName = string.IsNullOrWhiteSpace(MapName) ? fallback.MapName : MapName,
            Name = string.IsNullOrWhiteSpace(HostName) ? fallback.Name : HostName,
            PreferredPlayerId = PreferredPlayerId ?? fallback.PreferredPlayerId,
            PreferredPlayerName = string.IsNullOrWhiteSpace(PlayerName) ? fallback.PreferredPlayerName : PlayerName.Trim(),
            JoinTicket = string.IsNullOrWhiteSpace(JoinTicket) ? fallback.JoinTicket : JoinTicket,
            EncryptionKey = string.IsNullOrWhiteSpace(EncryptionKey) ? fallback.EncryptionKey : EncryptionKey,
            UniqueGameId = string.IsNullOrWhiteSpace(UniqueGameId) ? fallback.UniqueGameId : UniqueGameId,
            ServerUid = ServerUid ?? fallback.ServerUid,
            GameMode = string.IsNullOrWhiteSpace(GameMode) ? fallback.GameMode : GameMode,
            RankingMode = string.IsNullOrWhiteSpace(RankingMode) ? fallback.RankingMode : RankingMode,
            IsRanked = IsRanked,
            TimeLimitMinutes = TimeLimitMinutes ?? fallback.TimeLimitMinutes,
            MaxRounds = MaxRounds ?? fallback.MaxRounds,
            FlagCaptureLimit = FlagCaptureLimit ?? fallback.FlagCaptureLimit,
            AutoBalance = AutoBalance ?? fallback.AutoBalance
        };
    }

    public SourceBackendOptions ToSourceBackendOptions(SourceBackendOptions fallback, bool preferProfileEndpoint = true)
    {
        var host = preferProfileEndpoint && !string.IsNullOrWhiteSpace(BackendHost) ? BackendHost : fallback.Host;
        var port = preferProfileEndpoint && int.TryParse(BackendPort, out var parsedPort) ? parsedPort : fallback.Port;
        return fallback with
        {
            Host = host,
            Port = port,
            EnableProxy = port > 0 || fallback.EnableProxy
        };
    }

    public string[] BuildLocalSourceArguments(string? hostOverride = null, int? portOverride = null)
    {
        return BuildSourceArguments(hostOverride, portOverride, null);
    }

    public string[] BuildSourceArguments(
        string? hostOverride = null,
        int? portOverride = null,
        IReadOnlyDictionary<string, string?>? commandOverrides = null)
    {
        var result = new List<string>(SourceArguments.Length);
        for (var index = 0; index < SourceArguments.Length; index++)
        {
            var current = SourceArguments[index];
            if (current is "-ip" && index + 1 < SourceArguments.Length)
            {
                result.Add(current);
                var existing = SourceArguments[++index];
                result.Add(hostOverride ?? existing);
                continue;
            }

            if (current is "-port" && index + 1 < SourceArguments.Length)
            {
                result.Add(current);
                var existing = SourceArguments[++index];
                result.Add((portOverride ?? ParsePort(existing)).ToString());
                continue;
            }

            if (IsPlusCommand(current) && index + 1 < SourceArguments.Length)
            {
                result.Add(current);
                var existing = SourceArguments[++index];
                result.Add(commandOverrides is not null && commandOverrides.TryGetValue(current, out var value) && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : existing);
                continue;
            }

            result.Add(current);
        }

        return result.ToArray();
    }

    public IReadOnlyDictionary<string, string> SourceArgumentMap()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < SourceArguments.Length - 1; index++)
        {
            var current = SourceArguments[index];
            if (IsSourceArgumentKey(current))
            {
                result[current] = SourceArguments[++index];
            }
        }

        return result;
    }

    public IReadOnlyDictionary<string, string> SourceRuleCvars()
    {
        return SourceArgumentMap()
            .Where(static item => item.Key.StartsWith("+", StringComparison.Ordinal)
                && item.Key is not "+map"
                && item.Key is not "+maxplayers"
                && item.Key is not "+hostname")
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
    }

    public string? SourceArgumentValue(string key)
    {
        return SourceArgumentMap().GetValueOrDefault(key);
    }

    private static bool IsSourceArgumentKey(string value)
    {
        return value is "-game" or "-ip" or "-port" || IsPlusCommand(value);
    }

    private static bool IsPlusCommand(string value)
    {
        return value.StartsWith("+", StringComparison.Ordinal);
    }

    private static int ParsePort(string value)
    {
        return int.TryParse(value, out var port) ? port : 0;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
