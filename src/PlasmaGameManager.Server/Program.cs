using System.Net;
using PlasmaGameManager.Server;

var bind = IPAddress.Any;
var port = ParseInt(Environment.GetEnvironmentVariable("PLASMA_PORT"), 27015);
var ports = ParsePorts(Environment.GetEnvironmentVariable("PLASMA_PORTS"));
var profileName = "tf2-ps3";
string? evidenceLog = null;
var gameLocalId = ParseLong(Environment.GetEnvironmentVariable("PLASMA_GAME_LOCAL_ID"), GameManagerSessionOptions.Default.LocalId);
var gameId = ParseLong(Environment.GetEnvironmentVariable("PLASMA_GAME_ID"), GameManagerSessionOptions.Default.GameId);
var gameMaxPlayers = ParseInt(Environment.GetEnvironmentVariable("PLASMA_GAME_MAX_PLAYERS"), GameManagerSessionOptions.Default.MaxPlayers);
var gameMap = Environment.GetEnvironmentVariable("PLASMA_GAME_MAP") ?? GameManagerSessionOptions.Default.MapName;
var gameName = Environment.GetEnvironmentVariable("PLASMA_GAME_NAME") ?? GameManagerSessionOptions.Default.Name;
var gameAdvertisedHost = Environment.GetEnvironmentVariable("PLASMA_GAME_ADVERTISED_HOST") ?? GameManagerSessionOptions.Default.AdvertisedHost;
var gameAdvertisedPort = ParseInt(Environment.GetEnvironmentVariable("PLASMA_GAME_ADVERTISED_PORT"), GameManagerSessionOptions.Default.AdvertisedPort);
var sourceHost = Environment.GetEnvironmentVariable("PLASMA_SOURCE_HOST") ?? "127.0.0.1";
var sourcePort = int.TryParse(Environment.GetEnvironmentVariable("PLASMA_SOURCE_PORT"), out var parsedSourcePort)
    ? parsedSourcePort
    : 0;
var sourceTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("PLASMA_SOURCE_TIMEOUT_MS"), out var parsedSourceTimeout)
    ? parsedSourceTimeout
    : 250;
var sourceProtocol = ParseSourceProtocol(Environment.GetEnvironmentVariable("PLASMA_SOURCE_PROTOCOL"));
var sourceLaunchProfilePath = Environment.GetEnvironmentVariable("PLASMA_SOURCE_LAUNCH_PROFILE");
var sourceLaunchProfileGlob = Environment.GetEnvironmentVariable("PLASMA_SOURCE_LAUNCH_PROFILE_GLOB");
var tf2RankedStatsExportPath = Environment.GetEnvironmentVariable("PLASMA_TF2_RANKED_STATS_EXPORT");
var mapMetadataPath = Environment.GetEnvironmentVariable("PLASMA_TF2_MAP_METADATA") ?? "";
var controlBind = IPAddress.Parse(Environment.GetEnvironmentVariable("PLASMA_CONTROL_BIND") ?? "127.0.0.1");
var controlPort = ParseInt(Environment.GetEnvironmentVariable("PLASMA_CONTROL_PORT"), 27017);
var controlUser = Environment.GetEnvironmentVariable("PLASMA_CONTROL_USER") ?? "FridiNaTor";
var controlPassword = Environment.GetEnvironmentVariable("PLASMA_CONTROL_PASSWORD") ?? "Clockwor1";
var controlEnabled = !string.Equals(Environment.GetEnvironmentVariable("PLASMA_CONTROL_ENABLED"), "0", StringComparison.OrdinalIgnoreCase)
    && !string.Equals(Environment.GetEnvironmentVariable("PLASMA_CONTROL_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
var sourceProxyEnabled = sourcePort > 0
    || string.Equals(Environment.GetEnvironmentVariable("PLASMA_SOURCE_PROXY"), "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("PLASMA_SOURCE_PROXY"), "true", StringComparison.OrdinalIgnoreCase);

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--bind" when i + 1 < args.Length:
            bind = IPAddress.Parse(args[++i]);
            break;
        case "--port" when i + 1 < args.Length:
            port = int.Parse(args[++i]);
            ports = Array.Empty<int>();
            break;
        case "--ports" when i + 1 < args.Length:
            ports = ParsePorts(args[++i]);
            break;
        case "--profile" when i + 1 < args.Length:
            profileName = args[++i];
            break;
        case "--evidence-log" when i + 1 < args.Length:
            evidenceLog = args[++i];
            break;
        case "--game-local-id" when i + 1 < args.Length:
            gameLocalId = long.Parse(args[++i]);
            break;
        case "--game-id" when i + 1 < args.Length:
            gameId = long.Parse(args[++i]);
            break;
        case "--game-map" when i + 1 < args.Length:
            gameMap = args[++i];
            break;
        case "--game-name" when i + 1 < args.Length:
            gameName = args[++i];
            break;
        case "--max-players" when i + 1 < args.Length:
            gameMaxPlayers = int.Parse(args[++i]);
            break;
        case "--advertised-host" when i + 1 < args.Length:
            gameAdvertisedHost = args[++i];
            break;
        case "--advertised-port" when i + 1 < args.Length:
            gameAdvertisedPort = int.Parse(args[++i]);
            break;
        case "--source-host" when i + 1 < args.Length:
            sourceHost = args[++i];
            break;
        case "--source-port" when i + 1 < args.Length:
            sourcePort = int.Parse(args[++i]);
            sourceProxyEnabled = true;
            break;
        case "--source-timeout-ms" when i + 1 < args.Length:
            sourceTimeoutMs = int.Parse(args[++i]);
            break;
        case "--source-protocol" when i + 1 < args.Length:
            sourceProtocol = ParseSourceProtocol(args[++i]);
            break;
        case "--source-launch-profile" when i + 1 < args.Length:
            sourceLaunchProfilePath = args[++i];
            break;
        case "--source-launch-profile-glob" when i + 1 < args.Length:
            sourceLaunchProfileGlob = args[++i];
            break;
        case "--tf2-ranked-stats-export" when i + 1 < args.Length:
            tf2RankedStatsExportPath = args[++i];
            break;
        case "--map-metadata" when i + 1 < args.Length:
            mapMetadataPath = args[++i];
            break;
        case "--control-bind" when i + 1 < args.Length:
            controlBind = IPAddress.Parse(args[++i]);
            controlEnabled = true;
            break;
        case "--control-port" when i + 1 < args.Length:
            controlPort = int.Parse(args[++i]);
            controlEnabled = true;
            break;
        case "--control-user" when i + 1 < args.Length:
            controlUser = args[++i];
            break;
        case "--control-password" when i + 1 < args.Length:
            controlPassword = args[++i];
            break;
        case "--no-control":
            controlEnabled = false;
            break;
        case "--help":
            Console.WriteLine("Usage: PlasmaGameManager.Server --bind 0.0.0.0 --port 27015 --profile tf2-ps3 [--ports 27015,27016] [--game-map ctf_2fort] [--game-name TF2PS3] [--max-players 24] [--advertised-host 127.0.0.1] [--advertised-port 27015] [--game-id 800001] [--game-local-id 257] [--evidence-log logs/gamemanager-events.jsonl] [--source-host 127.0.0.1 --source-port 27016] [--source-protocol ps3-native-passthrough|pc-source-connectionless-only|ps3-native-generated] [--source-launch-profile artifacts/custom-server-profile.json] [--source-launch-profile-glob 'artifacts/arcadia-source-profile-{gid}-{map}.json'] [--tf2-ranked-stats-export artifacts/live-stack/tf2-ranked-stats.jsonl] [--map-metadata artifacts/tf2ps3-map-metadata.json] [--control-bind 127.0.0.1 --control-port 27017 --control-user FridiNaTor --control-password Clockwor1] [--no-control]");
            return;
    }
}

var listenPorts = ports.Length > 0 ? ports : new[] { port };

var profile = GameManagerProfileFactory.Create(profileName);
IGameManagerEventSink? eventSink = null;
if (evidenceLog is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidenceLog)) ?? ".");
    eventSink = new JsonLineGameManagerEventSink(File.AppendText(evidenceLog));
    Console.WriteLine($"writing GameManager evidence events to {evidenceLog}");
}

using (eventSink)
{
    var sourceBackend = new SourceBackendOptions(sourceHost, sourcePort, sourceProxyEnabled, sourceTimeoutMs, sourceProtocol);
    var sessionOptions = new GameManagerSessionOptions(
        gameLocalId,
        gameId,
        gameMaxPlayers,
        string.IsNullOrWhiteSpace(gameMap) ? GameManagerSessionOptions.Default.MapName : gameMap,
        string.IsNullOrWhiteSpace(gameName) ? GameManagerSessionOptions.Default.Name : gameName,
        PreferredPlayerName: GameManagerSessionOptions.Default.PreferredPlayerName,
        AdvertisedHost: string.IsNullOrWhiteSpace(gameAdvertisedHost) ? GameManagerSessionOptions.Default.AdvertisedHost : gameAdvertisedHost,
        AdvertisedPort: gameAdvertisedPort,
        MapMetadataPath: mapMetadataPath);
    if (!string.IsNullOrWhiteSpace(sourceLaunchProfilePath))
    {
        var launchProfile = Tf2SourceLaunchProfile.LoadFromJsonFile(sourceLaunchProfilePath);
        sessionOptions = launchProfile.ToSessionOptions(sessionOptions);
        sourceBackend = launchProfile.ToSourceBackendOptions(sourceBackend, preferProfileEndpoint: !sourceBackend.IsEnabled);
        Console.WriteLine($"loaded TF2 Source launch profile from {sourceLaunchProfilePath}: map={launchProfile.MapName} mode={launchProfile.GameMode} endpoint={sourceBackend.Endpoint}");
    }

    if (sourceBackend.Protocol == SourceBackendProtocol.Ps3NativeGenerated)
    {
        sourceBackend = sourceBackend with { EnableProxy = false };
    }

    if (!string.IsNullOrWhiteSpace(tf2RankedStatsExportPath))
    {
        sessionOptions = sessionOptions with { NativeRankedStatsExportPath = tf2RankedStatsExportPath };
    }

    Console.WriteLine($"GameManager session LID={sessionOptions.LocalId} GID={sessionOptions.GameId} map={sessionOptions.MapName} name=\"{sessionOptions.Name}\" maxPlayers={sessionOptions.MaxPlayers} advertised={sessionOptions.AdvertisedHost}:{sessionOptions.AdvertisedPort}");
    if (!string.IsNullOrWhiteSpace(sessionOptions.MapMetadataPath))
    {
        Console.WriteLine($"loading TF2 PS3 map metadata from {sessionOptions.MapMetadataPath}");
    }
    if (!string.IsNullOrWhiteSpace(sessionOptions.NativeRankedStatsExportPath))
    {
        Console.WriteLine($"exporting ranked TF2 stats to {sessionOptions.NativeRankedStatsExportPath}");
    }
    if (sourceBackend.IsEnabled)
    {
        Console.WriteLine($"proxying post-handoff Source UDP to {sourceBackend.Host}:{sourceBackend.Port} protocol={sourceBackend.ProtocolName} timeout={sourceBackend.TimeoutMilliseconds}ms");
    }
    else if (sourceBackend.Protocol == SourceBackendProtocol.Ps3NativeGenerated)
    {
        Console.WriteLine("generating post-handoff PS3-native Source UDP responses inside PlasmaGameManager");
        Console.WriteLine($"PS3-native Source responder={Ps3NativeSourceResponder.ImplementationLabel} firstSnapshotClientPacket={Ps3NativeSourceResponder.FirstCommandSnapshotClientPacketCount} snapshotInterval={Ps3NativeSourceResponder.CommandSnapshotClientPacketInterval} lateLoadingSequence={Ps3NativeSourceResponder.SparseQuickMatchLateLoadingSequence}");
    }

    var server = new UdpGameManagerServer(profile, eventSink, sourceBackend, sessionOptions, sourceLaunchProfilePath, sourceLaunchProfileGlob);
    await using var controlServer = controlEnabled && controlPort > 0
        ? new GameManagerControlHttpServer(
            server.Control,
            new GameManagerControlOptions(controlBind, controlPort, controlUser, controlPassword, Enabled: true))
        : null;
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var tasks = new List<Task> { server.RunAsync(bind, listenPorts, cts.Token) };
    if (controlServer is not null)
    {
        tasks.Add(controlServer.RunAsync(cts.Token));
    }

    await Task.WhenAll(tasks);
}

static int[] ParsePorts(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return Array.Empty<int>();
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(int.Parse)
        .Distinct()
        .OrderBy(static parsed => parsed)
        .ToArray();
}

static SourceBackendProtocol ParseSourceProtocol(string? value)
{
    return value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "ps3-native" or "ps3-native-passthrough" or "passthrough" =>
            SourceBackendProtocol.Ps3NativePassthrough,
        "pc-source" or "pc-source-connectionless-only" or "connectionless-only" =>
            SourceBackendProtocol.PcSourceConnectionlessOnly,
        "ps3-generated" or "ps3-native-generated" or "generated" =>
            SourceBackendProtocol.Ps3NativeGenerated,
        _ => throw new ArgumentException($"Unsupported Source backend protocol '{value}'. Use ps3-native-passthrough, pc-source-connectionless-only, or ps3-native-generated.")
    };
}

static int ParseInt(string? value, int fallback)
{
    return int.TryParse(value, out var parsed) ? parsed : fallback;
}

static long ParseLong(string? value, long fallback)
{
    return long.TryParse(value, out var parsed) ? parsed : fallback;
}
