# PlasmaGameManager

PlasmaGameManager is a reverse-engineered EA Games Plasma/GameManager server. Its first target is reviving Team Fortress 2 on PS3, but the protocol layer should also provide a foundation for other EA titles that used Plasma/GameManager.

This project owns the UDP GameManager/session layer and the current generated PS3-native Source responder. It is intended to run alongside Arcadia for FESL/Theater/login while PlasmaGameManager handles the PS3-visible GameManager and Source/gameplay UDP path.

## How It Works

TF2 PS3 does not connect straight to a normal PC Source server from the server browser. The flow is split into layers:

- **FESL/Theater:** Arcadia handles account login, server listing, matchmaking, and returns the game server endpoint.
- **Plasma/GameManager:** This server handles the PS3 game-session handshake, player reservation, roster, mesh/join state, and the transition into Source/gameplay traffic.
- **Generated native Source responder:** PlasmaGameManager now includes the default PS3-native Source replacement path. It emits PS3-visible setup, roster/object-state, MOTD/loading, heartbeat, and generated snapshot/entity traffic without forwarding to a PC `srcds`.

The goal is a native semantic implementation, not replaying one captured packet sequence. The server decodes client messages into GameManager commands, updates explicit session/player state, and writes responses using the recovered Plasma/GameManager packet model.

## Current Status

This repository is currently aimed at TF2 PS3 research and local RPCS3 testing. Arcadia integration is expected. The generated native Source responder is the default live-test backend; the old external `srcds` experiment is legacy-only and not the recommended path.

## Requirements

- Linux shell environment.
- .NET SDK. This repo pins SDK `8.0.419` in `global.json`.
- Arcadia, preferably this build: [FridiNaTor1/arcadia](https://github.com/FridiNaTor1/arcadia).
- RPCS3 with network/RPCN support enabled for local testing.
- No external Source server is required for the default live stack. `Source-Server/` may still be used as a private legacy workspace, but the generated native responder is the active target.

The local development machine uses:

```sh
dotnet
```

If your SDK is somewhere else, set `DOTNET` when running scripts:

```sh
DOTNET=/path/to/dotnet ./run-rpcs3-live-stack.sh --check
```

## Repository Layout

Public source lives under `src/`.

The following folders are intentionally gitignored because they are local/private working areas:

- `arcadia/`
- `Source-Server/`
- `docs/`
- `re/`
- `scripts/`
- `tests/`

Putting Arcadia into the ignored `arcadia/` folder makes the easy launcher simpler, but it is not required. You can keep Arcadia elsewhere and point the launcher at it with `ARCADIA_ROOT`. `Source-Server/` is kept for private/legacy experiments and is not used by the default stack.

## Build

From the repository root:

```sh
dotnet build PlasmaGameManager.sln
```

If your `dotnet` is on `PATH`, this also works:

```sh
dotnet build PlasmaGameManager.sln
```

## Easy Run

Use this when Arcadia is placed in the ignored local folder:

```text
arcadia/
```

Expected local layout:

```text
arcadia/src/server/Arcadia.csproj
```

Check the stack:

```sh
./run-rpcs3-live-stack.sh --check
```

Start Arcadia and PlasmaGameManager together. PlasmaGameManager uses its embedded generated native Source responder by default:

```sh
./run-rpcs3-live-stack.sh
```

Defaults:

- Arcadia/FESL/Theater runs from `arcadia/`.
- Public/drop game UDP is advertised on `58101`.
- PlasmaGameManager listens on UDP `27015`.
- The generated native Source responder runs inside PlasmaGameManager; no external Source backend process is started.
- Local test host is `127.0.0.1`.
- Logs are written to `artifacts/live-stack/`.
- The launcher writes `artifacts/live-stack/stack-source-profile.json` from the selected map, rules, endpoint, ticket, game ID, EKEY, and preferred PID. PlasmaGameManager loads that profile automatically.
- Set `TF2PS3_DEDICATED_MAP`, `TF2PS3_DEDICATED_HOSTNAME`, and `TF2PS3_DEDICATED_MAX_PLAYERS` when you want the generated native responder to match a specific custom-create scenario.
- `TF2PS3_SOURCE_BACKEND=generated` is the default. `replay` remains useful for PCAP comparison. `srcds` is legacy-only and not expected to produce a PS3-native gameplay session.

Useful overrides:

```sh
TF2PS3_GAME_HOST=127.0.0.1 \
TF2PS3_PUBLIC_PORT=58101 \
TF2PS3_GAME_PORT=27015 \
TF2PS3_SOURCE_HOST=127.0.0.1 \
TF2PS3_SOURCE_PORT=27016 \
TF2PS3_DEDICATED_MAP=cp_dustbowl \
TF2PS3_DEDICATED_HOSTNAME=TF2PS3 \
TF2PS3_DEDICATED_MAX_PLAYERS=16 \
./run-rpcs3-live-stack.sh
```

## Separate Run

Use this when you want to start each service manually or Arcadia is installed elsewhere.

1. Start Arcadia:

```sh
cd /path/to/arcadia
dotnet run --project src/server/Arcadia.csproj
```

For TF2 PS3 testing, Arcadia must use backend-managed TF2 listings. The launcher sets these automatically, but manual runs should set equivalent values:

```sh
ARCADIA_Tf2BackendSettings__EnableBackendProvisioning=true \
ARCADIA_Tf2BackendSettings__AdvertisedHost=127.0.0.1 \
ARCADIA_Tf2BackendSettings__SourceBackendHost=127.0.0.1 \
ARCADIA_Tf2BackendSettings__PublicPort=58101 \
ARCADIA_Tf2BackendSettings__GameManagerPort=27015 \
ARCADIA_Tf2BackendSettings__SourcePort=27016 \
ARCADIA_Tf2BackendSettings__LaunchGameManager=false \
ARCADIA_Tf2BackendSettings__LaunchSourceServer=false \
ARCADIA_Tf2BackendSettings__WaitForSourceReady=false \
ARCADIA_Tf2BackendSettings__AutoCreateQuickMatchServer=true \
dotnet run --project src/server/Arcadia.csproj
```

When `LaunchGameManager=true`, Arcadia can provision PlasmaGameManager per TF2 custom-create request and passes the recovered preferences through environment variables. PlasmaGameManager receives `PLASMA_GAME_MAP`, `PLASMA_GAME_NAME`, `PLASMA_GAME_MAX_PLAYERS`, `PLASMA_GAME_ID`, and `PLASMA_GAME_LOCAL_ID`. The default stack keeps backend ownership in the launcher and uses one generated native responder.

When `SourceLaunchProfilePath` is set, Arcadia also writes a TF2 Source launch profile containing the recovered map/rules plus the native GameManager identity fields: LID, GID, preferred PID, ticket, EKEY, UGID, and server UID. PlasmaGameManager can load that profile with `PLASMA_SOURCE_LAUNCH_PROFILE`.

TF2 PS3 server details use the same split seen in historical captures: `P/PORT` is the public/drop UDP port, `INT-PORT` is the PlasmaGameManager UDP port, and `SOURCE-PORT` is retained as private profile metadata. The default generated backend does not require a separate process on `SOURCE-PORT`.

2. Start PlasmaGameManager:

```sh
cd /path/to/PlasmaGameManager
dotnet run --project src/PlasmaGameManager.Server/PlasmaGameManager.Server.csproj -- \
  --bind 0.0.0.0 \
  --port 27015 \
  --profile tf2-ps3 \
  --game-map ctf_2fort \
  --game-name TF2PS3 \
  --max-players 24 \
  --source-host 127.0.0.1 \
  --source-port 27016 \
  --source-protocol ps3-native-generated \
  --evidence-log artifacts/live-stack/live-gamemanager-events.jsonl
```

## RPCS3 Configuration

For local testing:

1. Enable network connection and RPCN.
2. Enable UPNP.
3. Set `IP/Hosts switches`.

Default local test switches:

```text
theater.ps3.arcadia=127.0.0.1&&hl2-ps3.fesl.ea.com=127.0.0.1&&messaging.ea.com=127.0.0.1
```

Replace `127.0.0.1` with your server host if RPCS3 is running on another machine.

For example:

```text
theater.ps3.arcadia=192.168.1.50&&hl2-ps3.fesl.ea.com=192.168.1.50&&messaging.ea.com=192.168.1.50
```

## Notes

- Arcadia owns FESL/Theater/login and server advertisement.
- PlasmaGameManager owns the PS3 GameManager UDP session.
- PlasmaGameManager owns the PS3 GameManager UDP session and the current generated native Source gameplay path.
- The public repository intentionally excludes private reverse-engineering notes, local test scripts, PCAPs, binaries, and private Source-server work.
