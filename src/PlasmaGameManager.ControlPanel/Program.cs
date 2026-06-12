using System.Net.Http.Headers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var bind = Environment.GetEnvironmentVariable("PLASMA_PANEL_BIND") ?? "127.0.0.1";
var port = ParseInt(Environment.GetEnvironmentVariable("PLASMA_PANEL_PORT"), 27018);
var apiUrl = Environment.GetEnvironmentVariable("PLASMA_CONTROL_API_URL") ?? "http://127.0.0.1:27017";
var adminUser = Environment.GetEnvironmentVariable("PLASMA_CONTROL_USER") ?? "FridiNaTor";
var adminPassword = Environment.GetEnvironmentVariable("PLASMA_CONTROL_PASSWORD") ?? "Clockwor1";

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--bind" when index + 1 < args.Length:
            bind = args[++index];
            break;
        case "--port" when index + 1 < args.Length:
            port = int.Parse(args[++index]);
            break;
        case "--api-url" when index + 1 < args.Length:
            apiUrl = args[++index].TrimEnd('/');
            break;
        case "--user" when index + 1 < args.Length:
            adminUser = args[++index];
            break;
        case "--password" when index + 1 < args.Length:
            adminPassword = args[++index];
            break;
    }
}

builder.WebHost.UseUrls($"http://{bind}:{port}");
builder.Services.AddHttpClient("control", client =>
{
    client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");
    var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{adminUser}:{adminPassword}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (IsAuthorized(context.Request, adminUser, adminPassword))
    {
        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.Headers.WWWAuthenticate = "Basic realm=\"PlasmaGameManager Control Panel\"";
    await context.Response.WriteAsync("authentication required");
});

app.MapGet("/", () => Results.Content(GetHtml(adminUser), "text/html; charset=utf-8"));

app.MapGet("/api/servers", async (IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("control");
    using var response = await client.GetAsync("api/servers");
    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, response.Content.Headers.ContentType?.ToString() ?? "application/json");
});

app.MapPost("/api/servers/{gameId:long}/commands", async (long gameId, HttpRequest request, IHttpClientFactory httpClientFactory) =>
{
    return await ProxyPostAsync(httpClientFactory, $"api/servers/{gameId}/commands", request);
});

app.MapPost("/api/servers/{gameId:long}/chat", async (long gameId, HttpRequest request, IHttpClientFactory httpClientFactory) =>
{
    return await ProxyPostAsync(httpClientFactory, $"api/servers/{gameId}/chat", request);
});

Console.WriteLine($"PlasmaGameManager control panel listening on http://{bind}:{port}/");
Console.WriteLine($"Control API target: {apiUrl}");
await app.RunAsync();

static async Task<IResult> ProxyPostAsync(IHttpClientFactory httpClientFactory, string path, HttpRequest request)
{
    var client = httpClientFactory.CreateClient("control");
    using var content = new StreamContent(request.Body);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    using var response = await client.PostAsync(path, content);
    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
}

static bool IsAuthorized(HttpRequest request, string user, string password)
{
    var header = request.Headers.Authorization.ToString();
    if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
        var separator = decoded.IndexOf(':');
        return separator > 0
            && string.Equals(decoded[..separator], user, StringComparison.Ordinal)
            && string.Equals(decoded[(separator + 1)..], password, StringComparison.Ordinal);
    }
    catch (FormatException)
    {
        return false;
    }
}

static int ParseInt(string? value, int fallback)
{
    return int.TryParse(value, out var parsed) ? parsed : fallback;
}

static string GetHtml(string adminUser)
{
    var encodedAdminUser = System.Text.Json.JsonSerializer.Serialize(adminUser);
    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PlasmaGameManager Control</title>
  <style>
    :root { color-scheme: dark; font-family: system-ui, sans-serif; background: #151617; color: #f1eee7; }
    body { margin: 0; }
    header { padding: 16px 20px; border-bottom: 1px solid #34383d; background: #202326; }
    main { max-width: 1180px; margin: 0 auto; padding: 20px; display: grid; gap: 16px; }
    h1 { font-size: 20px; margin: 0; }
    h2 { font-size: 16px; margin: 0 0 10px; }
    section, .server { border: 1px solid #34383d; border-radius: 6px; padding: 14px; background: #1d2023; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 10px; }
    .label { color: #a9b0b7; font-size: 12px; text-transform: uppercase; }
    .value { font-size: 15px; }
    table { width: 100%; border-collapse: collapse; margin-top: 8px; }
    th, td { border-bottom: 1px solid #33383d; padding: 7px; text-align: left; font-size: 13px; }
    input, textarea, select, button { font: inherit; border-radius: 5px; border: 1px solid #42474d; background: #111315; color: #f1eee7; padding: 8px; }
    textarea { min-height: 72px; resize: vertical; }
    button { background: #2f6f9f; border-color: #4284b7; cursor: pointer; }
    button:hover { background: #397fac; }
    .row-actions { display: flex; gap: 6px; flex-wrap: wrap; align-items: center; }
    .row-actions button, .row-actions select { padding: 5px 7px; font-size: 12px; }
    .compact { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 6px 12px; margin-top: 10px; }
    .compact div { font-size: 12px; color: #cfd4d8; }
    .compact code { color: #f6df9c; }
    form { display: grid; gap: 8px; }
    .actions { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .status { color: #94d38c; min-height: 20px; }
    @media (max-width: 720px) { .actions { grid-template-columns: 1fr; } }
  </style>
</head>
<body>
  <header><h1>PlasmaGameManager Control</h1></header>
  <main>
    <section>
      <div class="status" id="status"></div>
      <button id="refresh">Refresh Servers</button>
    </section>
    <div id="servers"></div>
  </main>
  <script>
    const adminUser = {{encodedAdminUser}};
    const statusEl = document.querySelector('#status');
    const serversEl = document.querySelector('#servers');
    document.querySelector('#refresh').addEventListener('click', load);

    async function load() {
      statusEl.textContent = 'Loading...';
      const response = await fetch('/api/servers');
      const data = await response.json();
      serversEl.innerHTML = '';
      for (const server of data.servers ?? []) renderServer(server);
      statusEl.textContent = `Loaded ${(data.servers ?? []).length} server(s)`;
    }

    function renderServer(server) {
      const root = document.createElement('section');
      root.className = 'server';
      root.innerHTML = `
        <h2>${escapeHtml(server.name)} (${server.gameId})</h2>
        <div class="grid">
          <div><div class="label">Map</div><div class="value">${escapeHtml(server.mapName)}</div></div>
          <div><div class="label">Next Map</div><div class="value">${escapeHtml(server.nextMapName)}</div></div>
          <div><div class="label">Endpoint</div><div class="value">${escapeHtml(server.advertisedHost)}:${server.advertisedPort}</div></div>
          <div><div class="label">Players</div><div class="value">${server.sourceVisiblePlayerCount}/${server.maxPlayers}</div></div>
          <div><div class="label">Ranked</div><div class="value">${server.isRanked ? 'Yes' : 'No'}</div></div>
          <div><div class="label">Round</div><div class="value">${server.roundState} ${teamLabel(server.winningTeam)}</div></div>
          <div><div class="label">Tournament</div><div class="value">${server.tournamentMode ? 'On' : 'Off'} / ready ${server.tournamentReadymode ? 'On' : 'Off'}${server.awaitingReadyRestart ? ' / waiting' : ''}</div></div>
          <div><div class="label">Rules</div><div class="value">ff ${server.friendlyFire ? 'on' : 'off'} / crits ${server.weaponCriticals ? 'on' : 'off'} / respawn ${server.disableRespawnTimes ? 'fast' : 'waves'}</div></div>
          <div><div class="label">Physics</div><div class="value">gravity ${formatNumber(server.gravity)} / camera ${server.forceCamera}</div></div>
          <div><div class="label">Voice/Admin</div><div class="value">alltalk ${server.allTalk ? 'on' : 'off'} / cheats ${server.cheats ? 'on' : 'off'}</div></div>
          <div><div class="label">Timer</div><div class="value">${formatSeconds(server.timeRemainingSeconds)}${server.timerPaused ? ' paused' : ''}</div></div>
          <div><div class="label">Event Log</div><div class="value">${server.sourceLoggingEnabled ? 'on' : 'off'}</div></div>
          <div><div class="label">Log Targets</div><div class="value">${(server.sourceLogAddresses ?? []).length}</div></div>
        </div>
        <div class="row-actions" style="margin-top: 10px">
          <button type="button" data-server-command="tf_bot_add">Add Bot</button>
          <button type="button" data-server-command="tf_bot_add 4">Add 4 Bots</button>
          <button type="button" data-server-command="tf_bot_quota 6">6 Bot Quota</button>
          <button type="button" data-server-command="tf_bot_kick all">Kick Bots</button>
          <button type="button" data-server-command="pause">Pause Timer</button>
          <button type="button" data-server-command="unpause">Resume Timer</button>
          <button type="button" data-server-command="tf2_winround red">RED Win</button>
          <button type="button" data-server-command="tf2_winround blu">BLU Win</button>
          <button type="button" data-server-command="sm_roundstate running">Round Running</button>
          <button type="button" data-server-command="sm_roundstate teamwin red">Round RED Win</button>
          <button type="button" data-server-command="sm_roundstate teamwin blu">Round BLU Win</button>
          <button type="button" data-server-command="mp_switchteams">Switch Teams</button>
          <button type="button" data-server-command="mp_scrambleteams">Scramble Teams</button>
          <button type="button" data-server-command="sm_balance">Balance Teams</button>
          <button type="button" data-server-command="sm_team @all red">All RED</button>
          <button type="button" data-server-command="sm_team @all blu">All BLU</button>
          <button type="button" data-server-command="sm_mute @all">Mute All</button>
          <button type="button" data-server-command="sm_unsilence @all">Unsilence All</button>
          <button type="button" data-server-command="sm_gag @bots">Gag Bots</button>
          <button type="button" data-server-command="sm_kick @bots">Kick Bots</button>
          <button type="button" data-server-command="sm_heal @all">Heal All</button>
          <button type="button" data-server-command="sm_respawn @all">Respawn All</button>
          <button type="button" data-server-command="sm_slay @bots">Slay Bots</button>
          <button type="button" data-server-command="tf2_setclass @all soldier">All Soldier</button>
          <button type="button" data-server-command="mp_restartgame">Restart Round</button>
          <button type="button" data-server-command="mp_tournament 1">Tournament On</button>
          <button type="button" data-server-command="mp_tournament 0">Tournament Off</button>
          <button type="button" data-server-command="mp_tournament_readymode 1">Ready Mode On</button>
          <button type="button" data-server-command="mp_tournament_readymode 0">Ready Mode Off</button>
          <button type="button" data-server-command="mp_tournament_restart">Tournament Restart</button>
          <button type="button" data-server-command="readylist">Ready List</button>
          <button type="button" data-server-command="mp_friendlyfire ${server.friendlyFire ? '0' : '1'}">Friendly Fire ${server.friendlyFire ? 'Off' : 'On'}</button>
          <button type="button" data-server-command="tf_weapon_criticals ${server.weaponCriticals ? '0' : '1'}">Crits ${server.weaponCriticals ? 'Off' : 'On'}</button>
          <button type="button" data-server-command="mp_disable_respawn_times ${server.disableRespawnTimes ? '0' : '1'}">Fast Respawn ${server.disableRespawnTimes ? 'Off' : 'On'}</button>
          <button type="button" data-server-command="sv_alltalk ${server.allTalk ? '0' : '1'}">All Talk ${server.allTalk ? 'Off' : 'On'}</button>
          <button type="button" data-server-command="sv_gravity 800">Gravity 800</button>
          <button type="button" data-server-command="sv_gravity 400">Gravity 400</button>
          <button type="button" data-server-command="setnextmap ctf_2fort">Next 2Fort</button>
          <button type="button" data-server-command="setnextmap cp_dustbowl">Next Dustbowl</button>
          <button type="button" data-server-command="sm_votemap ctf_2fort">Vote 2Fort</button>
          <button type="button" data-server-command="sm_votemap cp_dustbowl">Vote Dustbowl</button>
          <button type="button" data-server-command="sm_passvote">Pass Vote</button>
          <button type="button" data-server-command="sm_failvote">Fail Vote</button>
          <button type="button" data-server-command="sm_cancelvote">Cancel Vote</button>
          <button type="button" data-server-command="exec server">Exec server.cfg</button>
          <button type="button" data-server-command="exec MODSETTINGS">Exec MODSETTINGS.CFG</button>
          <button type="button" data-server-command="sm_who">SM Who</button>
          <button type="button" data-server-command="sm_scoreboard">Scoreboard</button>
          <button type="button" data-server-command="sm_cvar sv_gravity 800">SM Gravity 800</button>
          <button type="button" data-server-command="sm_rcon status">SM RCON Status</button>
          <button type="button" data-server-command="sm_announce Server announcement from panel">Announce</button>
          <button type="button" data-server-command="sm_alert Admin alert from panel">Alert</button>
          <button type="button" data-server-command="sm_clearchat">Clear Chat</button>
          <button type="button" data-server-command="sm_hsay Native hint test">Hint Test</button>
          <button type="button" data-server-command="sm_msay Native panel test">Panel Test</button>
          <button type="button" data-server-command="sm_tsay Native HUD test">HUD Test</button>
          <button type="button" data-server-command="sm_play ui/buttonclick.wav">Play Test Sound</button>
          <button type="button" data-server-command="log ${server.sourceLoggingEnabled ? 'off' : 'on'}">Log ${server.sourceLoggingEnabled ? 'Off' : 'On'}</button>
          <button type="button" data-server-command="log status">Log Status</button>
          <button type="button" data-server-command="logclear">Clear Log</button>
        </div>
        <h2>Teams</h2>
        <table>
          <thead><tr><th>Team</th><th>Players</th><th>Score</th><th>Rounds</th><th>Captures</th></tr></thead>
          <tbody>${teamRows(server.teams)}</tbody>
        </table>
        <h2>Objectives</h2>
        <table>
          <thead><tr><th>Point</th><th>Owner</th><th>Capping</th><th>Progress</th><th>Position</th><th>Actions</th></tr></thead>
          <tbody>${objectiveRows(server.objectives, server.gameId)}</tbody>
        </table>
        <h2>CTF Flags</h2>
        <table>
          <thead><tr><th>Flag</th><th>State</th><th>Carrier</th><th>Position</th><th>Actions</th></tr></thead>
          <tbody>${flagRows(server.flags)}</tbody>
        </table>
        <h2>Server Cvars</h2>
        <div class="compact">${cvarList(server.sourceCvars)}</div>
        <table>
          <thead><tr><th>Player</th><th>Endpoint</th><th>State</th><th>Team</th><th>Class</th><th>Health</th><th>Position</th><th>Resources</th><th>Comms</th><th>Stats</th><th>Packets C/S</th><th>Actions</th></tr></thead>
          <tbody>${(server.players ?? []).map(player => `
            <tr>
              <td>${escapeHtml(player.name)} #${player.playerId}${player.isBot ? ' <code>BOT</code>' : ''}</td>
              <td>${escapeHtml(player.endpoint)}</td>
              <td>${escapeHtml(player.state)}</td>
	              <td>${player.teamNumber}</td>
	              <td>${player.classNumber}</td>
	              <td>${player.alive ? 'alive' : 'dead'} ${player.health}/${player.maxHealth}</td>
	              <td>${player.noClip ? 'noclip' : (player.movementFrozen ? 'frozen' : 'mobile')}<br>${formatNumber(player.originX)}, ${formatNumber(player.originY)}, ${formatNumber(player.originZ)}<br>yaw ${formatNumber(player.yaw)} pitch ${formatNumber(player.pitch)}</td>
	              <td>ammo ${player.primaryAmmo}/${player.secondaryAmmo} clip ${player.weaponClip1}<br>metal ${player.metal} fov ${player.fov} speed ${formatNumber(player.movementSpeed)} g ${formatNumber(player.gravityScale)}<br>respawn ${formatNumber(player.generatedRespawnDelaySeconds)}s cond ${formatHex(player.playerCondition)} cloak ${formatNumber(player.cloakMeter)}<br>render c ${formatHex(player.renderColor)} m ${player.renderMode} fx ${player.renderFx} e ${formatHex(player.effects)}<br>anim model ${player.modelIndex} seq ${player.sequence} skin ${player.skin}/${player.body}/${player.hitboxSet} scale ${formatNumber(player.modelWidthScale)} rate ${formatNumber(player.playbackRate)} cycle ${formatNumber(player.serverAnimationCycle)} parity ${player.newSequenceParity}/${player.resetEventsParity}/${player.muzzleFlashParity}<br>sentry L${player.sentryLevel}/S${player.sentryState} ${player.sentryShells}/${player.sentryRockets}<br>tele ${player.teleporterState} ${formatNumber(player.teleporterRechargeTime)}s/${player.teleporterTimesUsed}</td>
	              <td>${player.voiceMuted ? 'voice muted' : 'voice open'}<br>${player.chatGagged ? 'chat gagged' : 'chat open'}<br>${player.tournamentReady ? 'ready' : 'not ready'}</td>
              <td>${player.score} pts, ${player.deaths} d, ${player.captures} cap, ${player.defenses} def, ${player.killAssists} ast<br>${player.buildingsDestroyed} dest, ${player.headshots} hs, ${player.backstabs} bs<br>${player.healPoints} heal, ${player.invulns} inv, ${player.teleports} tele, ${player.resupplyPoints} resup<br>${player.dominations} dom, ${player.revenge} rev</td>
              <td>${player.nativeSourceClientPacketCount}/${player.nativeSourceServerPacketCount}</td>
              <td><div class="row-actions">
	                <button type="button" data-player-command="tf2_respawn" data-endpoint="${escapeHtml(player.endpoint)}">Respawn</button>
	                <button type="button" data-player-command="tf2_kill" data-endpoint="${escapeHtml(player.endpoint)}">Kill</button>
	                <button type="button" data-player-command="tf2_getpos" data-endpoint="${escapeHtml(player.endpoint)}">Get Pos</button>
	                <button type="button" data-player-command="${player.movementFrozen ? 'sm_unfreeze' : 'sm_freeze'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.movementFrozen ? 'Unfreeze' : 'Freeze'}</button>
	                <button type="button" data-player-command="${player.noClip ? 'sm_clip' : 'sm_noclip'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.noClip ? 'Clip' : 'Noclip'}</button>
	                <button type="button" data-player-command="${player.tournamentReady ? 'unready' : 'ready'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.tournamentReady ? 'Unready' : 'Ready'}</button>
	                <button type="button" data-player-command="sm_spec" data-endpoint="${escapeHtml(player.endpoint)}">Spectate</button>
	                <button type="button" data-player-command="sm_swap" data-endpoint="${escapeHtml(player.endpoint)}">Swap Team</button>
	                <button type="button" data-player-command="sm_bring" data-endpoint="${escapeHtml(player.endpoint)}">Bring</button>
	                <button type="button" data-player-command="sm_goto" data-endpoint="${escapeHtml(player.endpoint)}">Goto</button>
	                <button type="button" data-player-command="sm_beacon" data-endpoint="${escapeHtml(player.endpoint)}">Beacon</button>
	                <button type="button" data-player-command="sm_slap 5" data-endpoint="${escapeHtml(player.endpoint)}">Slap 5</button>
	                <button type="button" data-player-command="sm_damage 25" data-endpoint="${escapeHtml(player.endpoint)}">Damage 25</button>
	                <button type="button" data-player-command="sm_heal" data-endpoint="${escapeHtml(player.endpoint)}">Heal</button>
	                <button type="button" data-player-command="sm_overheal" data-endpoint="${escapeHtml(player.endpoint)}">Overheal</button>
	                <button type="button" data-player-command="sm_setmaxhealth 300" data-endpoint="${escapeHtml(player.endpoint)}">Max HP 300</button>
	                <button type="button" data-player-command="sm_burn 10" data-endpoint="${escapeHtml(player.endpoint)}">Burn</button>
	                <button type="button" data-player-command="sm_extinguish" data-endpoint="${escapeHtml(player.endpoint)}">Extinguish</button>
	                <button type="button" data-player-command="sm_bleed 10" data-endpoint="${escapeHtml(player.endpoint)}">Bleed</button>
	                <button type="button" data-player-command="sm_stun 5" data-endpoint="${escapeHtml(player.endpoint)}">Stun</button>
	                <button type="button" data-player-command="sm_taunt 5" data-endpoint="${escapeHtml(player.endpoint)}">Taunt</button>
	                <button type="button" data-player-command="${(player.playerCondition & 32) ? 'sm_ungod' : 'sm_god'}" data-endpoint="${escapeHtml(player.endpoint)}">${(player.playerCondition & 32) ? 'Ungod' : 'God'}</button>
	                <button type="button" data-player-command="sm_refill" data-endpoint="${escapeHtml(player.endpoint)}">Refill</button>
	                <button type="button" data-player-command="sm_setammo 32 32 200 6" data-endpoint="${escapeHtml(player.endpoint)}">Ammo</button>
	                <button type="button" data-player-command="sm_sentry 3 1 200 20" data-endpoint="${escapeHtml(player.endpoint)}">Sentry 3</button>
	                <button type="button" data-player-command="sm_teleporter 1 0 0" data-endpoint="${escapeHtml(player.endpoint)}">Teleporter</button>
	                <button type="button" data-player-command="sm_destroybuildings" data-endpoint="${escapeHtml(player.endpoint)}">Destroy Buildings</button>
	                <button type="button" data-player-command="sm_speed 1" data-endpoint="${escapeHtml(player.endpoint)}">Speed 1</button>
	                <button type="button" data-player-command="sm_speed 1.5" data-endpoint="${escapeHtml(player.endpoint)}">Speed 1.5</button>
	                <button type="button" data-player-command="sm_gravity 0.5" data-endpoint="${escapeHtml(player.endpoint)}">Low Grav</button>
	                <button type="button" data-player-command="sm_gravity 1" data-endpoint="${escapeHtml(player.endpoint)}">Grav 1</button>
	                <button type="button" data-player-command="sm_gravity 2" data-endpoint="${escapeHtml(player.endpoint)}">High Grav</button>
	                <button type="button" data-player-command="sm_resetgravity" data-endpoint="${escapeHtml(player.endpoint)}">Reset Grav</button>
	                <button type="button" data-player-command="sm_fov 75" data-endpoint="${escapeHtml(player.endpoint)}">FOV 75</button>
	                <button type="button" data-player-command="sm_fov 90" data-endpoint="${escapeHtml(player.endpoint)}">FOV 90</button>
	                <button type="button" data-player-command="sm_hideweapon" data-endpoint="${escapeHtml(player.endpoint)}">Hide Weapon</button>
	                <button type="button" data-player-command="sm_showweapon" data-endpoint="${escapeHtml(player.endpoint)}">Show Weapon</button>
	                <button type="button" data-player-command="sm_blind" data-endpoint="${escapeHtml(player.endpoint)}">Blind</button>
	                <button type="button" data-player-command="sm_unblind" data-endpoint="${escapeHtml(player.endpoint)}">Unblind</button>
	                <button type="button" data-player-command="sm_shake 8" data-endpoint="${escapeHtml(player.endpoint)}">Shake</button>
	                <button type="button" data-player-command="sm_color 255 64 64 255" data-endpoint="${escapeHtml(player.endpoint)}">Red Tint</button>
	                <button type="button" data-player-command="sm_color 64 128 255 255" data-endpoint="${escapeHtml(player.endpoint)}">Blue Tint</button>
	                <button type="button" data-player-command="sm_alpha 96" data-endpoint="${escapeHtml(player.endpoint)}">Alpha 96</button>
	                <button type="button" data-player-command="sm_rendermode 1" data-endpoint="${escapeHtml(player.endpoint)}">Trans Render</button>
	                <button type="button" data-player-command="sm_renderfx 3" data-endpoint="${escapeHtml(player.endpoint)}">Render FX</button>
	                <button type="button" data-player-command="sm_addeffect 32" data-endpoint="${escapeHtml(player.endpoint)}">Add EF32</button>
	                <button type="button" data-player-command="sm_resetrender" data-endpoint="${escapeHtml(player.endpoint)}">Reset Render</button>
	                <button type="button" data-player-command="sm_sequence 1 1 0" data-endpoint="${escapeHtml(player.endpoint)}">Seq 1</button>
	                <button type="button" data-player-command="sm_sequence 0 1 0" data-endpoint="${escapeHtml(player.endpoint)}">Seq 0</button>
	                <button type="button" data-player-command="sm_skinbody 1 0 0" data-endpoint="${escapeHtml(player.endpoint)}">Skin 1</button>
	                <button type="button" data-player-command="sm_modelscale 1.25" data-endpoint="${escapeHtml(player.endpoint)}">Scale 1.25</button>
	                <button type="button" data-player-command="sm_modelscale 1" data-endpoint="${escapeHtml(player.endpoint)}">Scale 1</button>
	                <button type="button" data-player-command="sm_muzzleflash" data-endpoint="${escapeHtml(player.endpoint)}">Muzzle Flash</button>
	                <button type="button" data-player-command="sm_resetanim" data-endpoint="${escapeHtml(player.endpoint)}">Reset Anim</button>
	                <button type="button" data-player-command="sm_addcond crit" data-endpoint="${escapeHtml(player.endpoint)}">Crit</button>
	                <button type="button" data-player-command="sm_addcond uber" data-endpoint="${escapeHtml(player.endpoint)}">Uber</button>
	                <button type="button" data-player-command="sm_clearcond" data-endpoint="${escapeHtml(player.endpoint)}">Clear Cond</button>
	                <button type="button" data-player-command="${player.cloakMeter > 0 ? 'sm_uncloak' : 'sm_cloak'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.cloakMeter > 0 ? 'Uncloak' : 'Cloak'}</button>
	                <button type="button" data-player-command="tf2_addscore 5" data-endpoint="${escapeHtml(player.endpoint)}">+5</button>
	                <button type="button" data-player-command="tf2_addcaptures 1" data-endpoint="${escapeHtml(player.endpoint)}">+Cap</button>
	                <button type="button" data-player-command="tf2_adddeaths 1" data-endpoint="${escapeHtml(player.endpoint)}">+Death</button>
	                <button type="button" data-player-command="tf2_addassists 1" data-endpoint="${escapeHtml(player.endpoint)}">+Assist</button>
	                <button type="button" data-player-command="tf2_adddefenses 1" data-endpoint="${escapeHtml(player.endpoint)}">+Defense</button>
	                <button type="button" data-player-command="tf2_adddestruction 1" data-endpoint="${escapeHtml(player.endpoint)}">+Dest</button>
	                <button type="button" data-player-command="tf2_addheadshots 1" data-endpoint="${escapeHtml(player.endpoint)}">+Headshot</button>
	                <button type="button" data-player-command="tf2_addbackstabs 1" data-endpoint="${escapeHtml(player.endpoint)}">+Backstab</button>
	                <button type="button" data-player-command="tf2_addhealing 25" data-endpoint="${escapeHtml(player.endpoint)}">+Heal 25</button>
	                <button type="button" data-player-command="tf2_addteleports 1" data-endpoint="${escapeHtml(player.endpoint)}">+Teleport</button>
	                <button type="button" data-player-command="sm_resetstats" data-endpoint="${escapeHtml(player.endpoint)}">Reset Stats</button>
	                <button type="button" data-player-command="sm_takeflag" data-endpoint="${escapeHtml(player.endpoint)}">Take Flag</button>
	                <button type="button" data-player-command="sm_dropflag" data-endpoint="${escapeHtml(player.endpoint)}">Drop Flag</button>
	                <button type="button" data-player-command="sm_captureflag" data-endpoint="${escapeHtml(player.endpoint)}">Capture Flag</button>
	                <button type="button" data-player-command="sm_votekick" data-endpoint="${escapeHtml(player.endpoint)}">Vote Kick</button>
                <button type="button" data-player-command="${player.voiceMuted ? 'unmute' : 'mute'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.voiceMuted ? 'Unmute' : 'Mute'}</button>
                <button type="button" data-player-command="${player.chatGagged ? 'ungag' : 'gag'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.chatGagged ? 'Ungag' : 'Gag'}</button>
                <button type="button" data-player-command="${player.voiceMuted && player.chatGagged ? 'unsilence' : 'silence'}" data-endpoint="${escapeHtml(player.endpoint)}">${player.voiceMuted && player.chatGagged ? 'Unsilence' : 'Silence'}</button>
                <button type="button" data-player-command="kick" data-endpoint="${escapeHtml(player.endpoint)}">Kick</button>
                <button type="button" data-player-command="banid" data-endpoint="${escapeHtml(player.endpoint)}">Ban</button>
                <select data-player-team data-endpoint="${escapeHtml(player.endpoint)}">${teamOptions(player.teamNumber)}</select>
                <select data-player-class data-endpoint="${escapeHtml(player.endpoint)}">${classOptions(player.classNumber)}</select>
              </div></td>
            </tr>`).join('')}</tbody>
        </table>
        ${loggingStatus(server)}
        ${voteStatus(server)}
        <div class="actions">
          <form data-action="chat">
            <h2>Chat</h2>
            <select name="target"><option value="">All players</option>${playerOptions(server.players)}</select>
            <textarea name="message" placeholder="Message"></textarea>
            <button>Send Chat</button>
          </form>
          <form data-action="commands">
            <h2>Command</h2>
            <select name="target"><option value="">All players</option>${playerOptions(server.players)}</select>
            <textarea name="command" placeholder="Command, for example: sm_say hello, sm_cvar sv_gravity 800, sm_kick player"></textarea>
            <button>Send Command</button>
          </form>
        </div>`;
      for (const form of root.querySelectorAll('form')) {
        form.addEventListener('submit', event => submitAction(event, server.gameId, form));
      }
      for (const button of root.querySelectorAll('[data-player-command]')) {
        button.addEventListener('click', () => sendPlayerCommand(server.gameId, button.dataset.endpoint, button.dataset.playerCommand));
      }
      for (const button of root.querySelectorAll('[data-server-command]')) {
        button.addEventListener('click', () => sendPlayerCommand(server.gameId, null, button.dataset.serverCommand));
      }
      for (const select of root.querySelectorAll('[data-player-team]')) {
        select.addEventListener('change', () => sendPlayerCommand(server.gameId, select.dataset.endpoint, `tf2_setteam ${select.value}`));
      }
      for (const select of root.querySelectorAll('[data-player-class]')) {
        select.addEventListener('change', () => sendPlayerCommand(server.gameId, select.dataset.endpoint, `tf2_setclass ${select.value}`));
      }
      serversEl.append(root);
      root.insertAdjacentHTML('beforeend', banList(server));
      root.insertAdjacentHTML('beforeend', eventFeed(server));
      root.insertAdjacentHTML('beforeend', commandHistory(server));
    }

    async function sendPlayerCommand(gameId, targetEndpoint, command) {
      const response = await fetch(`/api/servers/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ command, targetEndpoint, issuedBy: adminUser })
      });
      statusEl.textContent = response.ok ? `Sent ${command}` : await response.text();
      await load();
    }

    async function submitAction(event, gameId, form) {
      event.preventDefault();
      const formData = new FormData(form);
      const action = form.dataset.action;
      const payload = {
        targetEndpoint: formData.get('target') || null,
        issuedBy: adminUser
      };
      if (action === 'chat') payload.message = formData.get('message');
      else payload.command = formData.get('command');
      const response = await fetch(`/api/servers/${gameId}/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      statusEl.textContent = response.ok ? 'Queued' : await response.text();
      if (response.ok) form.reset();
      await load();
    }

    function playerOptions(players) {
      return (players ?? []).map(player => `<option value="${escapeHtml(player.endpoint)}">${escapeHtml(player.name)} #${player.playerId}</option>`).join('');
    }

    function teamOptions(current) {
      const teams = [['2', 'RED'], ['3', 'BLU'], ['1', 'Spectator'], ['0', 'Unassigned']];
      return teams.map(([value, label]) => `<option value="${value}" ${String(current) === value ? 'selected' : ''}>${label}</option>`).join('');
    }

    function classOptions(current) {
      const classes = [['1', 'Scout'], ['2', 'Sniper'], ['3', 'Soldier'], ['4', 'Demoman'], ['5', 'Medic'], ['6', 'Heavy'], ['7', 'Pyro'], ['8', 'Spy'], ['9', 'Engineer']];
      return classes.map(([value, label]) => `<option value="${value}" ${String(current) === value ? 'selected' : ''}>${label}</option>`).join('');
    }

	    function teamRows(teams) {
      const rows = (teams ?? []).filter(team => team.teamNumber > 0).map(team => `
        <tr>
          <td>${escapeHtml(team.name)} (${team.teamNumber})</td>
          <td>${team.memberCount}</td>
          <td>${team.score}</td>
          <td>${team.roundsWon}</td>
          <td>${team.flagCaptures}</td>
        </tr>`).join('');
	      return rows || '<tr><td colspan="5">No team state reported</td></tr>';
	    }

	    function objectiveRows(objectives) {
	      const rows = (objectives ?? []).map(point => `
	        <tr>
	          <td>#${point.index}</td>
	          <td>${teamLabel(point.ownerTeam) || point.ownerTeam}</td>
	          <td>${teamLabel(point.cappingTeam) || point.cappingTeam}</td>
	          <td>${formatNumber((point.capturePercentage ?? 0) * 100)}%${point.blocked ? ' blocked' : ''}</td>
	          <td>${formatNumber(point.x)}, ${formatNumber(point.y)}, ${formatNumber(point.z)}</td>
	          <td><div class="row-actions">
	            <button type="button" data-server-command="sm_setpoint ${point.index} red">RED Own</button>
	            <button type="button" data-server-command="sm_setpoint ${point.index} blu">BLU Own</button>
	            <button type="button" data-server-command="sm_capturepoint ${point.index} red">RED Cap</button>
	            <button type="button" data-server-command="sm_capturepoint ${point.index} blu">BLU Cap</button>
	          </div></td>
	        </tr>`).join('');
	      return rows || '<tr><td colspan="6">No control points on this map</td></tr>';
	    }

	    function flagRows(flags) {
	      const rows = (flags ?? []).map(flag => `
	        <tr>
	          <td>${teamLabel(flag.teamNumber) || flag.teamNumber}</td>
	          <td>${escapeHtml(flag.state)}</td>
	          <td>${escapeHtml(flag.carrierName ?? 'none')}${flag.carrierPlayerId ? ` #${flag.carrierPlayerId}` : ''}</td>
	          <td>${formatNumber(flag.x)}, ${formatNumber(flag.y)}, ${formatNumber(flag.z)}</td>
	          <td><div class="row-actions">
	            <button type="button" data-server-command="sm_returnflag ${flag.teamNumber}">Return</button>
	            <button type="button" data-server-command="sm_flags">List</button>
	          </div></td>
	        </tr>`).join('');
	      return rows || '<tr><td colspan="5">No CTF flags on this map</td></tr>';
	    }

    function teamLabel(teamNumber) {
      if (teamNumber === 2) return 'RED';
      if (teamNumber === 3) return 'BLU';
      return '';
    }

	    function formatSeconds(value) {
	      const seconds = Math.max(0, Math.floor(Number(value) || 0));
	      const minutes = Math.floor(seconds / 60);
	      return `${String(minutes).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
	    }

	    function formatNumber(value) {
	      return Number(value ?? 0).toFixed(1);
	    }

	    function formatHex(value) {
	      return `0x${Number(value ?? 0).toString(16).padStart(8, '0')}`;
	    }

    function cvarList(cvars) {
      const entries = Object.entries(cvars ?? {}).sort(([a], [b]) => a.localeCompare(b));
      if (!entries.length) return '<div>No cvars reported</div>';
      return entries.map(([key, value]) => `<div><code>${escapeHtml(key)}</code> ${escapeHtml(value)}</div>`).join('');
    }

    function banList(server) {
      const rows = (server.bans ?? []).map(ban => `
        <tr>
          <td>${ban.id}</td>
          <td>${escapeHtml(ban.playerName ?? ban.selector)}</td>
          <td>${escapeHtml(ban.endpoint ?? '')}</td>
          <td>${escapeHtml(ban.expiresAt ?? 'permanent')}</td>
          <td>${escapeHtml(ban.reason)}</td>
          <td><button type="button" data-unban="${ban.id}">Unban</button></td>
        </tr>`).join('') || '<tr><td colspan="6">No bans</td></tr>';
      setTimeout(() => {
        for (const button of document.querySelectorAll(`[data-unban]`)) {
          button.onclick = () => sendPlayerCommand(server.gameId, null, `removeid ${button.dataset.unban}`);
        }
      }, 0);
      return `
        <section>
          <h2>Bans</h2>
          <table><thead><tr><th>ID</th><th>Player</th><th>Endpoint</th><th>Expires</th><th>Reason</th><th>Action</th></tr></thead><tbody>${rows}</tbody></table>
        </section>`;
    }

    function loggingStatus(server) {
      const targets = (server.sourceLogAddresses ?? []).map(address => `<code>${escapeHtml(address)}</code>`).join(', ') || 'none';
      const error = server.sourceLogLastError ? `<div><span class="label">Last Error</span> ${escapeHtml(server.sourceLogLastError)}</div>` : '';
      return `
        <section>
          <h2>Source Event Logging</h2>
          <div class="compact">
            <div><span class="label">Status</span> ${server.sourceLoggingEnabled ? 'on' : 'off'}</div>
            <div><span class="label">Path</span> <code>${escapeHtml(server.sourceLogPath ?? '')}</code></div>
            <div><span class="label">Logaddress</span> ${targets}</div>
            ${error}
          </div>
          <div class="row-actions" style="margin-top: 10px">
            <button type="button" data-server-command="log on">Enable Log</button>
            <button type="button" data-server-command="log off">Disable Log</button>
            <button type="button" data-server-command="log status">Status</button>
            <button type="button" data-server-command="logclear">Clear File</button>
          </div>
        </section>`;
    }

    function voteStatus(server) {
      const vote = server.currentVote;
      if (!vote) {
        return `
          <section>
            <h2>Vote</h2>
            <div class="compact"><div>No active vote</div></div>
            <div class="row-actions" style="margin-top: 10px">
              <button type="button" data-server-command="sm_vote Restart the round?">Vote Restart</button>
              <button type="button" data-server-command="sm_votemap ctf_2fort">Vote 2Fort</button>
              <button type="button" data-server-command="sm_votemap cp_dustbowl">Vote Dustbowl</button>
            </div>
          </section>`;
      }

      return `
        <section>
          <h2>Vote</h2>
          <div class="compact">
            <div><span class="label">Issue</span> ${escapeHtml(vote.issue)}</div>
            <div><span class="label">Question</span> ${escapeHtml(vote.question)}</div>
            <div><span class="label">Target</span> ${escapeHtml(vote.target ?? '')}</div>
            <div><span class="label">By</span> ${escapeHtml(vote.initiatedBy)}</div>
            <div><span class="label">Votes</span> yes ${vote.yesVotes} / no ${vote.noVotes}</div>
          </div>
          <div class="row-actions" style="margin-top: 10px">
            <button type="button" data-server-command="sm_voteyes">Vote Yes</button>
            <button type="button" data-server-command="sm_voteno">Vote No</button>
            <button type="button" data-server-command="sm_passvote">Pass Vote</button>
            <button type="button" data-server-command="sm_failvote">Fail Vote</button>
            <button type="button" data-server-command="sm_cancelvote">Cancel Vote</button>
          </div>
        </section>`;
    }

    function commandHistory(server) {
      const pending = commandRows(server.pendingCommands, 'No pending commands');
      const recent = commandRows(server.recentCommands, 'No recent commands');
      return `
        <div class="actions">
          <section>
            <h2>Pending Commands</h2>
            <table><thead><tr><th>ID</th><th>Type</th><th>Target</th><th>Text</th></tr></thead><tbody>${pending}</tbody></table>
          </section>
          <section>
            <h2>Recent Commands</h2>
            <table><thead><tr><th>ID</th><th>By</th><th>Target</th><th>Text</th></tr></thead><tbody>${recent}</tbody></table>
          </section>
        </div>`;
    }

    function eventFeed(server) {
      const rows = (server.recentEvents ?? []).slice(-24).reverse().map(event => `
        <tr>
          <td>${escapeHtml(event.createdAt ?? '')}</td>
          <td>${escapeHtml(event.channel ?? event.type)}</td>
          <td>${escapeHtml(event.issuedBy)}</td>
          <td>${escapeHtml(event.targetEndpoint ?? 'all')}</td>
          <td>${escapeHtml(event.text)}</td>
        </tr>`).join('') || '<tr><td colspan="5">No server events</td></tr>';
      return `
        <section>
          <h2>Server Event Feed</h2>
          <table><thead><tr><th>Time</th><th>Channel</th><th>By</th><th>Target</th><th>Text</th></tr></thead><tbody>${rows}</tbody></table>
        </section>`;
    }

    function commandRows(commands, emptyText) {
      if (!(commands ?? []).length) return `<tr><td colspan="4">${emptyText}</td></tr>`;
      return commands.slice(-12).reverse().map(command => `
        <tr>
          <td>${command.id}</td>
          <td>${escapeHtml(command.type ?? command.issuedBy)}</td>
          <td>${escapeHtml(command.targetEndpoint ?? 'all')}</td>
          <td>${escapeHtml(command.text)}</td>
        </tr>`).join('');
    }

    function escapeHtml(value) {
      return String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
    }

    load();
  </script>
</body>
</html>
""";
}
