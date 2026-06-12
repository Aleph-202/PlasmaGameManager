using System.Net;
using System.Text;
using System.Text.Json;

namespace PlasmaGameManager.Server;

public sealed class GameManagerControlService
{
    private readonly GameManagerSession _game;

    public GameManagerControlService(GameManagerSession game)
    {
        _game = game;
    }

    public long GameId => _game.GameId;

    public GameManagerControlSnapshot GetSnapshot()
    {
        lock (_game.SyncRoot)
        {
            _game.World.UpdateTeamMemberCounts(_game.ActivePlayers());
            return new GameManagerControlSnapshot(
                [
                    new GameManagerControlServerSummary(
                        _game.GameId,
                        _game.LocalId,
                        _game.Name,
                        _game.MapName,
                        _game.NextMapName,
                        _game.GameMode,
                        _game.RankingMode,
                        _game.IsRanked,
                        _game.AdvertisedHost,
                        _game.AdvertisedPort,
                        _game.MaxPlayers,
                        _game.SourceVisiblePlayerCount,
                        _game.ActiveSourcePlayerCount,
                        _game.World.RoundState,
                        _game.World.WinningTeam,
                        _game.World.TournamentMode,
                        _game.World.TournamentReadymode,
                        _game.World.AwaitingReadyRestart,
                        _game.World.FriendlyFire,
                        _game.World.ForceCamera,
                        _game.World.DisableRespawnTimes,
                        _game.World.WeaponCriticals,
                        _game.World.DamageSpreadDisabled,
                        _game.World.AllTalk,
                        _game.World.Cheats,
                        _game.World.Gravity,
                        _game.World.Timer.TimerPaused,
                        _game.World.Timer.TimeRemainingSeconds,
                        _game.World.Teams.Values
                            .OrderBy(static team => team.TeamNumber)
                            .Select(static team => new GameManagerControlTeamSummary(
                                team.TeamNumber,
                                team.Name,
                                team.MemberCount,
                                team.Score,
                                team.RoundsWon,
                                team.FlagCaptures))
                            .ToArray(),
                        _game.World.ControlPoints
                            .OrderBy(static point => point.Index)
                            .Select(static point => new GameManagerControlObjectiveSummary(
                                point.Index,
                                point.OwnerTeam,
                                point.CappingTeam,
                                point.TeamInZone,
                                point.LazyCapPercentage,
                                point.Blocked,
                                point.X,
                                point.Y,
                                point.Z))
                            .ToArray(),
                        _game.World.Flags
                            .OrderBy(static flag => flag.TeamNumber)
                            .Select(static flag => new GameManagerControlFlagSummary(
                                flag.TeamNumber,
                                flag.State.ToString(),
                                flag.CarrierPlayerId,
                                flag.CarrierName,
                                flag.X,
                                flag.Y,
                                flag.Z,
                                flag.LastChangeSeconds))
                            .ToArray(),
                        _game.SourceCvars
                            .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase),
                        _game.Players.Values
                            .OrderBy(static player => player.PlayerId)
                            .Select(static player => new GameManagerControlPlayerSummary(
                                player.Endpoint,
                                player.PlayerId,
                                player.Name,
                                player.IsBot,
                                player.VoiceMuted,
                                player.ChatGagged,
                                player.TournamentReady,
                                player.State.ToString(),
                                player.LastSeen,
                                player.NativeSourceResponder.ClientPacketCount,
                                player.NativeSourceResponder.ServerPacketCount,
                                player.NativeSourceResponder.SentRosterDescriptorState,
                                player.SourceState.TeamNumber,
                                player.SourceState.ClassNumber,
                                player.SourceState.Health,
                                player.SourceState.MaxHealth,
                                player.SourceState.Alive,
                                player.SourceState.MovementFrozen,
                                player.SourceState.NoClip,
                                player.SourceState.OriginX,
                                player.SourceState.OriginY,
                                player.SourceState.OriginZ,
                                player.SourceState.Yaw,
                                player.SourceState.Pitch,
                                player.SourceState.LaggedMovementValue,
                                player.SourceState.GravityScale,
                                player.SourceState.GeneratedRespawnDelaySeconds,
                                player.SourceState.Fov,
                                player.SourceState.PrimaryAmmo,
                                player.SourceState.SecondaryAmmo,
                                player.SourceState.Metal,
                                player.SourceState.WeaponClip1,
                                player.SourceState.PlayerCondition,
                                player.SourceState.Effects,
                                player.SourceState.RenderMode,
                                player.SourceState.RenderFx,
                                player.SourceState.RenderColor,
                                player.SourceState.ModelIndex,
                                player.SourceState.TextureFrameIndex,
                                player.SourceState.Sequence,
                                player.SourceState.Skin,
                                player.SourceState.Body,
                                player.SourceState.HitboxSet,
                                player.SourceState.ModelWidthScale,
                                player.SourceState.PlaybackRate,
                                player.SourceState.ServerAnimationCycle,
                                player.SourceState.NewSequenceParity,
                                player.SourceState.ResetEventsParity,
                                player.SourceState.MuzzleFlashParity,
                                player.SourceState.CloakMeter,
                                player.SourceState.DisguiseTeam,
                                player.SourceState.DisguiseClass,
                                player.SourceState.SentryGun.UpgradeLevel,
                                player.SourceState.SentryGun.State,
                                player.SourceState.SentryGun.AmmoShells,
                                player.SourceState.SentryGun.AmmoRockets,
                                player.SourceState.Teleporter.State,
                                player.SourceState.Teleporter.RechargeTime,
                                player.SourceState.Teleporter.TimesUsed,
                                player.SourceState.Score,
                                player.SourceState.Deaths,
                                player.SourceState.Captures,
                                player.SourceState.Defenses,
                                player.SourceState.Dominations,
                                player.SourceState.Revenge,
                                player.SourceState.BuildingsDestroyed,
                                player.SourceState.Headshots,
                                player.SourceState.Backstabs,
                                player.SourceState.HealPoints,
                                player.SourceState.Invulns,
                                player.SourceState.Teleports,
                                player.SourceState.ResupplyPoints,
                                player.SourceState.KillAssists))
                            .ToArray(),
                        _game.SourceServerBans
                            .OrderBy(static ban => ban.Id)
                            .Select(static ban => new GameManagerControlBanSummary(
                                ban.Id,
                                ban.CreatedAt,
                                ban.ExpiresAt,
                                ban.Selector,
                                ban.Endpoint,
                                ban.PlayerId,
                                ban.PlayerName,
                                ban.Reason,
                                ban.IssuedBy))
                            .ToArray(),
                        _game.PendingSourceServerCommands.ToArray(),
                        _game.SourceServerCommandHistory.TakeLast(32).ToArray(),
                        _game.SourceServerEventHistory.TakeLast(64).ToArray(),
                        _game.CurrentSourceVote is null
                            ? null
                            : new GameManagerControlVoteSummary(
                                _game.CurrentSourceVote.Id,
                                _game.CurrentSourceVote.CreatedAt,
                                _game.CurrentSourceVote.Issue,
                                _game.CurrentSourceVote.Question,
                                _game.CurrentSourceVote.Target,
                                _game.CurrentSourceVote.InitiatedBy,
                                _game.CurrentSourceVote.YesVotes,
                                _game.CurrentSourceVote.NoVotes),
                        _game.SourceLoggingEnabled,
                        _game.SourceLogPath,
                        _game.SourceLogLastError,
                        _game.SourceLogAddresses.ToArray())
                ]);
        }
    }

    public SourceServerCommandResult QueueConsoleCommand(string command, string? targetEndpoint, string issuedBy)
    {
        lock (_game.SyncRoot)
        {
            return SourceServerCommandProcessor.Execute(
                _game,
                command,
                targetEndpoint,
                issuedBy);
        }
    }

    public PendingSourceServerCommand QueueChat(string message, string? targetEndpoint, string issuedBy)
    {
        lock (_game.SyncRoot)
        {
            return _game.QueueSourceServerCommand(
                string.IsNullOrWhiteSpace(targetEndpoint)
                    ? SourceServerCommandType.Chat
                    : SourceServerCommandType.PrivateChat,
                message,
                targetEndpoint,
                issuedBy);
        }
    }
}

public sealed record GameManagerControlSnapshot(GameManagerControlServerSummary[] Servers);

public sealed record GameManagerControlServerSummary(
    long GameId,
    long LocalId,
    string Name,
    string MapName,
    string NextMapName,
    string GameMode,
    string RankingMode,
    bool IsRanked,
    string AdvertisedHost,
    int AdvertisedPort,
    int MaxPlayers,
    int SourceVisiblePlayerCount,
    int ActiveSourcePlayerCount,
    uint RoundState,
    uint WinningTeam,
    bool TournamentMode,
    bool TournamentReadymode,
    bool AwaitingReadyRestart,
    bool FriendlyFire,
    int ForceCamera,
    bool DisableRespawnTimes,
    bool WeaponCriticals,
    bool DamageSpreadDisabled,
    bool AllTalk,
    bool Cheats,
    float Gravity,
    bool TimerPaused,
    float TimeRemainingSeconds,
    GameManagerControlTeamSummary[] Teams,
    GameManagerControlObjectiveSummary[] Objectives,
    GameManagerControlFlagSummary[] Flags,
    IReadOnlyDictionary<string, string> SourceCvars,
    GameManagerControlPlayerSummary[] Players,
    GameManagerControlBanSummary[] Bans,
    PendingSourceServerCommand[] PendingCommands,
    PendingSourceServerCommand[] RecentCommands,
    SourceServerEvent[] RecentEvents,
    GameManagerControlVoteSummary? CurrentVote,
    bool SourceLoggingEnabled,
    string SourceLogPath,
    string SourceLogLastError,
    string[] SourceLogAddresses);

public sealed record GameManagerControlVoteSummary(
    long Id,
    DateTimeOffset CreatedAt,
    string Issue,
    string Question,
    string? Target,
    string InitiatedBy,
    int YesVotes,
    int NoVotes);

public sealed record GameManagerControlPlayerSummary(
    string Endpoint,
    int PlayerId,
    string Name,
    bool IsBot,
    bool VoiceMuted,
    bool ChatGagged,
    bool TournamentReady,
    string State,
    DateTimeOffset LastSeen,
    int NativeSourceClientPacketCount,
    int NativeSourceServerPacketCount,
    bool NativeSourceRosterReady,
    uint TeamNumber,
    uint ClassNumber,
    uint Health,
    uint MaxHealth,
    bool Alive,
    bool MovementFrozen,
    bool NoClip,
    float OriginX,
    float OriginY,
    float OriginZ,
    float Yaw,
    float Pitch,
    float MovementSpeed,
    float GravityScale,
    float GeneratedRespawnDelaySeconds,
    uint Fov,
    uint PrimaryAmmo,
    uint SecondaryAmmo,
    uint Metal,
    uint WeaponClip1,
    uint PlayerCondition,
    uint Effects,
    byte RenderMode,
    byte RenderFx,
    uint RenderColor,
    uint ModelIndex,
    uint TextureFrameIndex,
    uint Sequence,
    uint Skin,
    uint Body,
    uint HitboxSet,
    float ModelWidthScale,
    float PlaybackRate,
    float ServerAnimationCycle,
    uint NewSequenceParity,
    uint ResetEventsParity,
    byte MuzzleFlashParity,
    float CloakMeter,
    uint DisguiseTeam,
    uint DisguiseClass,
    uint SentryLevel,
    uint SentryState,
    uint SentryShells,
    uint SentryRockets,
    uint TeleporterState,
    float TeleporterRechargeTime,
    uint TeleporterTimesUsed,
    uint Score,
    uint Deaths,
    uint Captures,
    uint Defenses,
    uint Dominations,
    uint Revenge,
    uint BuildingsDestroyed,
    uint Headshots,
    uint Backstabs,
    uint HealPoints,
    uint Invulns,
    uint Teleports,
    uint ResupplyPoints,
    uint KillAssists);

public sealed record GameManagerControlTeamSummary(
    uint TeamNumber,
    string Name,
    uint MemberCount,
    uint Score,
    uint RoundsWon,
    uint FlagCaptures);

public sealed record GameManagerControlObjectiveSummary(
    uint Index,
    uint OwnerTeam,
    uint CappingTeam,
    uint TeamInZone,
    float CapturePercentage,
    bool Blocked,
    float X,
    float Y,
    float Z);

public sealed record GameManagerControlFlagSummary(
    uint TeamNumber,
    string State,
    int? CarrierPlayerId,
    string? CarrierName,
    float X,
    float Y,
    float Z,
    float LastChangeSeconds);

public sealed record GameManagerControlBanSummary(
    long Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string Selector,
    string? Endpoint,
    int? PlayerId,
    string? PlayerName,
    string Reason,
    string IssuedBy);

public sealed record GameManagerControlCommandRequest(
    string? Command = null,
    string? Message = null,
    string? TargetEndpoint = null,
    string? IssuedBy = null);

public sealed record GameManagerControlOptions(
    IPAddress Bind,
    int Port,
    string User,
    string Password,
    bool Enabled)
{
    public static GameManagerControlOptions Disabled { get; } = new(IPAddress.Loopback, 0, "FridiNaTor", "Clockwor1", false);
}

public sealed class GameManagerControlHttpServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly HttpListener _listener = new();
    private readonly GameManagerControlService _control;
    private readonly GameManagerControlOptions _options;

    public GameManagerControlHttpServer(GameManagerControlService control, GameManagerControlOptions options)
    {
        _control = control;
        _options = options;
        _listener.Prefixes.Add($"http://{options.Bind}:{options.Port}/");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        Console.WriteLine($"GameManager control API listening on http://{_options.Bind}:{_options.Port}/ user={_options.User}");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleAsync(context), CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }

    public ValueTask DisposeAsync()
    {
        _listener.Close();
        return ValueTask.CompletedTask;
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (!IsAuthorized(context.Request))
            {
                context.Response.StatusCode = 401;
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"PlasmaGameManager\"";
                await WriteTextAsync(context.Response, "authentication required");
                return;
            }

            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (context.Request.HttpMethod == "GET" && (path.Length == 0 || path == "/"))
            {
                await WriteTextAsync(context.Response, "PlasmaGameManager control API\nGET /api/servers\nPOST /api/servers/{gameId}/commands\nPOST /api/servers/{gameId}/chat\n");
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/api/servers")
            {
                await WriteJsonAsync(context.Response, _control.GetSnapshot());
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryParseServerAction(path, out var gameId, out var action))
            {
                if (gameId != _control.GameId)
                {
                    context.Response.StatusCode = 404;
                    await WriteTextAsync(context.Response, $"server {gameId} not found");
                    return;
                }

                var request = await JsonSerializer.DeserializeAsync<GameManagerControlCommandRequest>(
                    context.Request.InputStream,
                    JsonOptions) ?? new GameManagerControlCommandRequest();
                var issuedBy = string.IsNullOrWhiteSpace(request.IssuedBy) ? _options.User : request.IssuedBy!;
                var result = action switch
                {
                    "commands" => _control.QueueConsoleCommand(
                        request.Command ?? request.Message ?? "",
                        request.TargetEndpoint,
                        issuedBy),
                    "chat" => DirectChatResult(_control.QueueChat(
                            request.Message ?? request.Command ?? "",
                            request.TargetEndpoint,
                            issuedBy)),
                    _ => throw new InvalidOperationException($"Unsupported action {action}.")
                };
                await WriteJsonAsync(context.Response, result);
                return;
            }

            context.Response.StatusCode = 404;
            await WriteTextAsync(context.Response, "not found");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteTextAsync(context.Response, ex.Message);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var header = request.Headers["Authorization"];
        if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
            var separator = decoded.IndexOf(':');
            return separator > 0
                && string.Equals(decoded[..separator], _options.User, StringComparison.Ordinal)
                && string.Equals(decoded[(separator + 1)..], _options.Password, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static SourceServerCommandResult DirectChatResult(PendingSourceServerCommand queued)
    {
        return new SourceServerCommandResult(true, $"queued chat: {queued.Text}", [queued]);
    }

    private static bool TryParseServerAction(string path, out long gameId, out string action)
    {
        gameId = 0;
        action = "";
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not ["api", "servers", var idText, var actionText]
            || !long.TryParse(idText, out gameId)
            || actionText is not ("commands" or "chat"))
        {
            return false;
        }

        action = actionText;
        return true;
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object value)
    {
        response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(response.OutputStream, value, JsonOptions);
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, string value)
    {
        response.ContentType = "text/plain; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(value);
        await response.OutputStream.WriteAsync(bytes);
    }
}
