#!/usr/bin/env sh
set -eu

ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
DOTNET=
PYTHON=${PYTHON:-python3}

ARCADIA_ROOT=${ARCADIA_ROOT:-"$ROOT/arcadia"}
SOURCE_SERVER_ROOT=${SOURCE_SERVER_ROOT:-"$ROOT/Source-Server"}

GAME_HOST=${TF2PS3_GAME_HOST:-127.0.0.1}
PUBLIC_PORT=${TF2PS3_PUBLIC_PORT:-58101}
GAME_PORT=${TF2PS3_GAME_PORT:-27015}
PLASMA_BIND=${PLASMA_BIND:-0.0.0.0}
PUBLIC_ENDPOINT_MODE=${TF2PS3_PUBLIC_ENDPOINT_MODE:-plasma}
SOURCE_HOST=${TF2PS3_SOURCE_HOST:-127.0.0.1}
SOURCE_PORT=${TF2PS3_SOURCE_PORT:-27016}
SOURCE_MAP=${TF2PS3_DEDICATED_MAP:-ctf_2fort}
SOURCE_HOSTNAME=${TF2PS3_DEDICATED_HOSTNAME:-TF2PS3}
SOURCE_MAX_PLAYERS=${TF2PS3_DEDICATED_MAX_PLAYERS:-24}
SOURCE_BACKEND=${TF2PS3_SOURCE_BACKEND:-generated}
PLASMA_SOURCE_PROTOCOL_EFFECTIVE=${PLASMA_SOURCE_PROTOCOL:-}
if [ -z "$PLASMA_SOURCE_PROTOCOL_EFFECTIVE" ]; then
	case "$SOURCE_BACKEND" in
		generated)
			PLASMA_SOURCE_PROTOCOL_EFFECTIVE=ps3-native-generated
			;;
		*)
			PLASMA_SOURCE_PROTOCOL_EFFECTIVE=ps3-native-passthrough
			;;
	esac
fi
BACKEND_OWNER=${TF2PS3_BACKEND_OWNER:-stack}
KILL_STALE_STACK=${TF2PS3_KILL_STALE_STACK:-1}
SOURCE_REPLAY_PCAP=${PLASMA_SOURCE_REPLAY_PCAP:-"$ROOT/.local/input/pcaps/TF2_PS3_network_traffic/packets/server"}
SOURCE_REPLAY_MATCH_MODE=${PLASMA_SOURCE_REPLAY_MATCH_MODE:-loose-transport-shape}
SOURCE_REPLAY_SEARCH_WINDOW=${PLASMA_SOURCE_REPLAY_SEARCH_WINDOW:-80}
SOURCE_REPLAY_PACING=${PLASMA_SOURCE_REPLAY_PACING:-capture-timing}
SOURCE_REPLAY_MAX_DELAY_MS=${PLASMA_SOURCE_REPLAY_MAX_DELAY_MS:-0}
SOURCE_REPLAY_BACKEND_MODE=${PLASMA_SOURCE_REPLAY_BACKEND_MODE:-packet}
SOURCE_REPLAY_PREFERRED_SCRIPT=${PLASMA_SOURCE_REPLAY_PREFERRED_SCRIPT:-}
if [ -z "$SOURCE_REPLAY_PREFERRED_SCRIPT" ]; then
	case "$SOURCE_MAP" in
		ctf_2fort)
			SOURCE_REPLAY_PREFERRED_SCRIPT=quick_match_to_motd_2fort
			;;
		cp_dustbowl|cp_db)
			SOURCE_REPLAY_PREFERRED_SCRIPT=custom_match_joining_cp_db_to_motd
			;;
	esac
fi
SOURCE_LAUNCH_PROFILE=${PLASMA_SOURCE_LAUNCH_PROFILE:-}
NET_TRACE=${TF2PS3_NET_TRACE:-1}
ARCADIA_SDK_VERSION=${ARCADIA_DOTNET_SDK_VERSION:-9.0.314}
CAPTURE_IDENTITY=${TF2PS3_CAPTURE_IDENTITY:-1}
PLASMA_CONTROL_BIND=${PLASMA_CONTROL_BIND:-127.0.0.1}
PLASMA_CONTROL_PORT=${PLASMA_CONTROL_PORT:-27017}
PLASMA_CONTROL_USER=${PLASMA_CONTROL_USER:-FridiNaTor}
PLASMA_CONTROL_PASSWORD=${PLASMA_CONTROL_PASSWORD:-Clockwor1}
PLASMA_PANEL_BIND=${PLASMA_PANEL_BIND:-127.0.0.1}
PLASMA_PANEL_PORT=${PLASMA_PANEL_PORT:-27018}
PLASMA_PANEL_ENABLED=${PLASMA_PANEL_ENABLED:-1}

if [ "$CAPTURE_IDENTITY" = "1" ]; then
	TF2_GAME_GID=${TF2_GAME_GID:-9843}
	TF2_GAME_TICKET=${TF2_GAME_TICKET:-601945780}
	TF2_GAME_PID=${TF2_GAME_PID:-197}
	TF2_GAME_HUID=${TF2_GAME_HUID:-869252542}
	TF2_GAME_UGID=${TF2_GAME_UGID:-57351316-3625-4cee-a1b5-973305deef7b}
	TF2_GAME_EKEY=${TF2_GAME_EKEY:-bRo6P5WLKl6opaCahDoNLA%3d%3d}
else
	TF2_GAME_GID=${TF2_GAME_GID:-0}
	TF2_GAME_TICKET=${TF2_GAME_TICKET:-0}
	TF2_GAME_PID=${TF2_GAME_PID:-0}
	TF2_GAME_HUID=${TF2_GAME_HUID:-100000000}
	TF2_GAME_UGID=${TF2_GAME_UGID:-}
	TF2_GAME_EKEY=${TF2_GAME_EKEY:-}
fi

LOG_DIR=${TF2PS3_STACK_LOG_DIR:-"$ROOT/artifacts/live-stack"}
TF2PS3_MAP_METADATA=${TF2PS3_MAP_METADATA:-"$ROOT/artifacts/tf2ps3-map-metadata.json"}
PLASMA_EVIDENCE_LOG=${PLASMA_EVIDENCE_LOG:-"$LOG_DIR/live-gamemanager-events.jsonl"}
ARCADIA_LOG="$LOG_DIR/arcadia.log"
SOURCE_LOG="$LOG_DIR/source-server.log"
PLASMA_LOG="$LOG_DIR/plasma-gamemanager.log"
PANEL_LOG="$LOG_DIR/control-panel.log"
SUMMARY_LOG="$LOG_DIR/live-stack-summary.txt"
SOURCE_REPLAY_LOG="$LOG_DIR/source-replay-events.jsonl"
PUBLIC_DROP_LOG="$LOG_DIR/public-drop.log"
TF2_RANKED_STATS_LOG=${TF2PS3_RANKED_STATS_LOG:-"$LOG_DIR/tf2-ranked-stats.jsonl"}
SOURCE_LAUNCH_PROFILE_GENERATED=0
if [ -z "$SOURCE_LAUNCH_PROFILE" ]; then
	SOURCE_LAUNCH_PROFILE="$LOG_DIR/stack-source-profile.json"
	SOURCE_LAUNCH_PROFILE_GENERATED=1
fi
if [ -z "${ARCADIA_SOURCE_PROFILE_PATH:-}" ]; then
	ARCADIA_SOURCE_PROFILE_PATH="$LOG_DIR/arcadia-source-profile-{gid}-{map}.json"
fi

arcadia_pid=""
source_pid=""
plasma_pid=""
public_udp_pid=""
panel_pid=""
summary_enabled=1

usage() {
	cat <<EOF
Usage: $0 [--check]

Expected local source folders:
  $ROOT/arcadia        Arcadia source tree, containing src/server/Arcadia.csproj
  $ROOT/Source-Server  optional legacy/private Source worktree; unused by the generated default

Useful overrides:
  ARCADIA_ROOT=/path/to/arcadia
  SOURCE_SERVER_ROOT=/path/to/tf2ps3-source-worktree
  TF2PS3_GAME_HOST=127.0.0.1
  TF2PS3_PUBLIC_PORT=58101
  TF2PS3_GAME_PORT=27015
  TF2PS3_PUBLIC_ENDPOINT_MODE=plasma|drop
  TF2PS3_SOURCE_HOST=127.0.0.1
  TF2PS3_SOURCE_PORT=27016
  TF2PS3_DEDICATED_MAP=ctf_2fort
  TF2PS3_DEDICATED_HOSTNAME=TF2PS3
  TF2PS3_DEDICATED_MAX_PLAYERS=24
  TF2PS3_SOURCE_BACKEND=generated|replay|srcds
  TF2PS3_BACKEND_OWNER=stack
  TF2PS3_KILL_STALE_STACK=1
  PLASMA_SOURCE_REPLAY_PCAP=$ROOT/.local/input/pcaps/TF2_PS3_network_traffic/packets/server
  PLASMA_SOURCE_REPLAY_MATCH_MODE=loose-transport-shape
  PLASMA_SOURCE_REPLAY_PACING=capture-timing
  PLASMA_SOURCE_LAUNCH_PROFILE=/path/to/tf2-source-launch-profile.json
    omitted by default; the launcher writes $LOG_DIR/stack-source-profile.json from the selected stack settings
  TF2PS3_NET_TRACE=1
  TF2PS3_CAPTURE_IDENTITY=1
  PLASMA_CONTROL_BIND=127.0.0.1
  PLASMA_CONTROL_PORT=27017
  PLASMA_CONTROL_USER=FridiNaTor
  PLASMA_CONTROL_PASSWORD=Clockwor1
  PLASMA_PANEL_BIND=127.0.0.1
  PLASMA_PANEL_PORT=27018
  PLASMA_PANEL_ENABLED=1
  TF2PS3_STACK_LOG_DIR=$ROOT/artifacts/live-stack
  TF2PS3_MAP_METADATA=$ROOT/artifacts/tf2ps3-map-metadata.json
EOF
}

cleanup() {
	trap - EXIT INT TERM
	if [ "$summary_enabled" = "1" ]; then
		write_live_stack_summary || true
	fi
	for pid in "$panel_pid" "$plasma_pid" "$source_pid" "$arcadia_pid"; do
		if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
			if command -v pkill >/dev/null 2>&1; then
				pkill -TERM -P "$pid" 2>/dev/null || true
			fi
			kill "$pid" 2>/dev/null || true
		fi
	done
	if [ -n "$public_udp_pid" ] && kill -0 "$public_udp_pid" 2>/dev/null; then
		kill "$public_udp_pid" 2>/dev/null || true
	fi
	sleep 1
	for pid in "$panel_pid" "$plasma_pid" "$source_pid" "$arcadia_pid"; do
		if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
			if command -v pkill >/dev/null 2>&1; then
				pkill -KILL -P "$pid" 2>/dev/null || true
			fi
			kill -KILL "$pid" 2>/dev/null || true
		fi
	done
	if [ -n "$public_udp_pid" ] && kill -0 "$public_udp_pid" 2>/dev/null; then
		kill -KILL "$public_udp_pid" 2>/dev/null || true
	fi
	for pid in "$panel_pid" "$plasma_pid" "$source_pid" "$arcadia_pid"; do
		if [ -n "$pid" ]; then
			wait "$pid" 2>/dev/null || true
		fi
	done
	if [ -n "$public_udp_pid" ]; then
		wait "$public_udp_pid" 2>/dev/null || true
	fi
}

trap cleanup EXIT INT TERM

write_live_stack_summary() {
	if [ ! -d "$LOG_DIR" ]; then
		return 0
	fi

	"$PYTHON" - "$ARCADIA_LOG" "$PLASMA_EVIDENCE_LOG" "$SOURCE_LOG" "$SOURCE_REPLAY_LOG" "$PUBLIC_DROP_LOG" "$SUMMARY_LOG" <<'PY'
import json
import os
import sys
from collections import Counter

arcadia_log, evidence_log, source_log, replay_log, public_log, summary_log = sys.argv[1:]

def read_text(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            return handle.read()
    except OSError:
        return ""

def count_contains(text, token):
    return text.count(token)

arcadia = read_text(arcadia_log)
source = read_text(source_log)
public = read_text(public_log)
replay = read_text(replay_log)
fesl_connections = count_contains(arcadia, "Opening connection from")
ps3_login_requests = count_contains(arcadia, "'acct' incoming:TXN=PS3Login")
quick_match_starts = count_contains(arcadia, "'pnow' incoming:TXN=Start")
server_detail_requests = count_contains(arcadia, "'GDAT' incoming:")
enter_game_requests = count_contains(arcadia, "'EGAM' incoming:")
egeg_responses = count_contains(arcadia, "data sent:EGEG")
close_game_requests = count_contains(arcadia, "'ECNL' incoming:")

events = []
event_counts = Counter()
source_roles = Counter()
last_event = ""
last_source_event = ""
if os.path.exists(evidence_log):
    with open(evidence_log, "r", encoding="utf-8", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            events.append(event)
            name = str(event.get("Event", ""))
            event_counts[name] += 1
            last_event = f'{event.get("Timestamp", "")} {name} {event.get("Endpoint", "")} {event.get("Kind", "")} {event.get("Explanation", "")}'
            if name.startswith("source-"):
                last_source_event = last_event
                role = event.get("SourcePayloadSemanticRole")
                if role:
                    source_roles[str(role)] += 1

lines = [
    "TF2 PS3 live stack summary",
    f"Arcadia log: {arcadia_log}",
    f"GameManager evidence log: {evidence_log}",
    f"Source log: {source_log}",
    f"Source replay log: {replay_log}",
    f"Public/drop log: {public_log}",
    "",
    "Arcadia/Theater:",
    f"  FESL connections: {fesl_connections}",
    f"  PS3Login requests: {ps3_login_requests}",
    f"  quick-match starts: {quick_match_starts}",
    f"  server detail requests: {server_detail_requests}",
    f"  enter-game requests: {enter_game_requests}",
    f"  EGEG responses: {egeg_responses}",
    f"  close-game requests: {close_game_requests}",
    "",
    "GameManager/native Source:",
    f"  total events: {len(events)}",
    f"  handoff events: {event_counts['source-handoff']}",
    f"  source traffic events: {event_counts['source-traffic']}",
    f"  generated sends: {event_counts['source-generated-send']}",
    f"  generated drops: {event_counts['source-generated-drop']}",
    f"  source sends: {event_counts['source-send']}",
    f"  last event: {last_event}",
    f"  last source event: {last_source_event}",
    "",
    "Top Source semantic roles:",
]
if source_roles:
    lines.extend(f"  {role}: {count}" for role, count in source_roles.most_common(8))
else:
    lines.append("  none")

lines.extend([
    "",
    "Backend/public listeners:",
    f"  generated backend note present: {'generated PS3-native Source responder' in source}",
    f"  source replay events: {sum(1 for line in replay.splitlines() if line.strip())}",
    f"  public/drop packets: {max(0, len([line for line in public.splitlines() if ' recv ' in line]))}",
])

diagnosis = "unknown"
if ps3_login_requests == 0:
    diagnosis = "client did not reach Arcadia/FESL; check RPCS3 IP/Host switches and RPCN/network settings"
elif egeg_responses == 0:
    diagnosis = "client reached Arcadia, but Theater did not advertise a GameManager endpoint"
elif len(events) == 0:
    diagnosis = "Arcadia advertised a server, but no UDP packets reached PlasmaGameManager; check INT-IP/INT-PORT, host switch target, and port binding"
elif event_counts["source-handoff"] == 0:
    diagnosis = "GameManager traffic arrived, but the join did not reach Source handoff"
elif event_counts["source-traffic"] == 0:
    diagnosis = "Source handoff happened, but no native Source packets arrived after handoff"
elif event_counts["source-generated-send"] == 0:
    diagnosis = "native Source packets arrived, but generated responder produced no packets"
else:
    diagnosis = "native Source responder is active; remaining failure is inside generated map/load payload semantics"

lines.extend(["", f"Likely stop point: {diagnosis}", ""])
summary = "\n".join(lines)
os.makedirs(os.path.dirname(summary_log) or ".", exist_ok=True)
with open(summary_log, "w", encoding="utf-8") as handle:
    handle.write(summary)
print(summary)
PY
}

check_file() {
	path=$1
	description=$2
	if [ ! -f "$path" ]; then
		echo "missing $description: $path" >&2
		return 1
	fi
}

check_path() {
	path=$1
	description=$2
	if [ ! -e "$path" ]; then
		echo "missing $description: $path" >&2
		return 1
	fi
}

check_executable() {
	path=$1
	description=$2
	if [ ! -x "$path" ]; then
		echo "missing executable $description: $path" >&2
		return 1
	fi
}

check_udp_port_free() {
	bind_host=$1
	bind_port=$2
	description=$3
	"$PYTHON" - "$bind_host" "$bind_port" "$description" "$ROOT" "$SOURCE_SERVER_ROOT" "$KILL_STALE_STACK" <<'PY'
import os
import signal
import socket
import sys
import time

host = sys.argv[1]
port = int(sys.argv[2])
description = sys.argv[3]
root = os.path.realpath(sys.argv[4])
source_root = os.path.realpath(sys.argv[5])
kill_stale = sys.argv[6] == "1"

def try_bind():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.bind((host, port))
    except OSError as exc:
        sock.close()
        return exc
    sock.close()
    return None

def udp_owner_pids():
    inodes = set()
    port_hex = f"{port:04X}"
    for table in ("/proc/net/udp", "/proc/net/udp6"):
        try:
            lines = open(table, "r", encoding="ascii", errors="replace").read().splitlines()[1:]
        except OSError:
            continue
        for line in lines:
            parts = line.split()
            if len(parts) < 10:
                continue
            local = parts[1]
            try:
                local_port = local.rsplit(":", 1)[1]
            except IndexError:
                continue
            if local_port.upper() == port_hex:
                inodes.add(parts[9])

    owners = []
    if inodes:
        for pid in filter(str.isdigit, os.listdir("/proc")):
            fd_dir = f"/proc/{pid}/fd"
            try:
                fds = os.listdir(fd_dir)
            except OSError:
                continue
            owns_socket = False
            for fd in fds:
                try:
                    target = os.readlink(f"{fd_dir}/{fd}")
                except OSError:
                    continue
                if target.startswith("socket:[") and target[8:-1] in inodes:
                    owns_socket = True
                    break
            if not owns_socket:
                continue
            try:
                cmdline = open(f"/proc/{pid}/cmdline", "rb").read().replace(b"\0", b" ").decode("utf-8", "replace").strip()
            except OSError:
                cmdline = ""
            try:
                cwd = os.path.realpath(os.readlink(f"/proc/{pid}/cwd"))
            except OSError:
                cwd = ""
            owners.append((int(pid), cmdline or "<unknown>", cwd))
    return owners

def is_stack_owned(cmdline, cwd):
    haystacks = [cmdline, cwd]
    needles = [
        root,
        source_root,
        "PlasmaGameManager.Server",
        "run-source-replay-backend",
        "run_tf2ps3_dedicated",
        "srcds_linux",
    ]
    return any(needle and any(needle in haystack for haystack in haystacks) for needle in needles)

exc = try_bind()
if exc is None:
    sys.exit(0)

owners = udp_owner_pids()
if kill_stale:
    stale = [(pid, cmdline, cwd) for pid, cmdline, cwd in owners if is_stack_owned(cmdline, cwd)]
    if stale:
        print(f"{description} UDP bind {host}:{port} is held by stale stack-owned process(es); terminating them.", file=sys.stderr)
        for pid, cmdline, cwd in stale:
            print(f"  stale owner pid {pid}: {cmdline} cwd={cwd}", file=sys.stderr)
            try:
                os.kill(pid, signal.SIGTERM)
            except OSError:
                pass

        deadline = time.time() + 2.0
        while time.time() < deadline:
            time.sleep(0.1)
            exc = try_bind()
            if exc is None:
                sys.exit(0)

        for pid, _, _ in stale:
            try:
                os.kill(pid, signal.SIGKILL)
            except OSError:
                pass
        time.sleep(0.2)
        exc = try_bind()
        if exc is None:
            sys.exit(0)

print(f"{description} UDP bind {host}:{port} is unavailable: {exc}", file=sys.stderr)

if owners:
    for pid, cmdline, cwd in sorted(owners):
        print(f"  owner pid {pid}: {cmdline} cwd={cwd}", file=sys.stderr)
else:
    print("  no owning process could be resolved from /proc/net/udp", file=sys.stderr)
sys.exit(1)
PY
}

require_process_running() {
	pid=$1
	name=$2
	log=$3
	if ! kill -0 "$pid" 2>/dev/null; then
		echo "$name failed to stay running; see $log" >&2
		if [ -f "$log" ]; then
			echo "---- $name log tail ----" >&2
			tail -n 80 "$log" >&2 || true
			echo "------------------------" >&2
		fi
		exit 1
	fi
}

write_stack_source_launch_profile() {
	if [ "$SOURCE_LAUNCH_PROFILE_GENERATED" != "1" ]; then
		return
	fi

	mkdir -p "$(dirname "$SOURCE_LAUNCH_PROFILE")"
	"$PYTHON" - "$SOURCE_LAUNCH_PROFILE" "$SOURCE_MAP" "$SOURCE_HOSTNAME" "$SOURCE_MAX_PLAYERS" "$SOURCE_HOST" "$SOURCE_PORT" "$TF2_GAME_GID" "$TF2_GAME_PID" "$TF2_GAME_TICKET" "$TF2_GAME_EKEY" "$TF2_GAME_UGID" "$TF2_GAME_HUID" <<'PY'
import json
import sys

path = sys.argv[1]
map_name = sys.argv[2]
host_name = sys.argv[3]
max_players = int(sys.argv[4]) if sys.argv[4].isdigit() else None
backend_host = sys.argv[5]
backend_port = sys.argv[6]
game_id = int(sys.argv[7]) if sys.argv[7].isdigit() and int(sys.argv[7]) > 0 else None
preferred_player_id = int(sys.argv[8]) if sys.argv[8].isdigit() and int(sys.argv[8]) > 0 else None
ticket = sys.argv[9]
ekey = sys.argv[10]
ugid = sys.argv[11]
server_uid = int(sys.argv[12]) if sys.argv[12].isdigit() and int(sys.argv[12]) > 0 else None

game_mode = "control-point" if map_name.lower().startswith("cp_") else "capture-the-flag" if map_name.lower().startswith("ctf_") else "unknown"
time_limit = 15 if game_mode == "control-point" else 30
max_rounds = 5
flag_capture_limit = 3 if game_mode == "capture-the-flag" else None
source_arguments = [
    "-game", "tf",
    "+map", map_name,
    "+maxplayers", str(max_players or 24),
    "-ip", backend_host,
    "-port", backend_port,
    "+mp_timelimit", str(time_limit),
    "+mp_maxrounds", str(max_rounds),
    "+mp_autoteambalance", "1",
]
if flag_capture_limit is not None:
    source_arguments.extend(["+tf_flag_caps_per_round", str(flag_capture_limit)])
source_arguments.extend(["+hostname", host_name])

profile = {
    "MapName": map_name,
    "GameMode": game_mode,
    "HostName": host_name,
    "BackendHost": backend_host,
    "BackendPort": backend_port,
    "MaxPlayers": max_players,
    "IsRanked": True,
    "RankingMode": "Ranked",
    "TimeLimitMinutes": time_limit,
    "MaxRounds": max_rounds,
    "FlagCaptureLimit": flag_capture_limit,
    "AutoBalance": True,
    "DurationPreset": "Low",
    "ServerPopulation": "Low",
    "Version": "6.0",
    "SourceArguments": source_arguments,
    "LocalId": 257,
    "GameId": game_id,
    "PreferredPlayerId": preferred_player_id,
    "JoinTicket": ticket,
    "EncryptionKey": ekey,
    "UniqueGameId": ugid,
    "ServerUid": server_uid,
}

with open(path, "w", encoding="utf-8") as handle:
    json.dump(profile, handle, indent=2)
    handle.write("\n")
PY
}

apply_source_launch_profile() {
	if [ -z "$SOURCE_LAUNCH_PROFILE" ] || [ ! -f "$SOURCE_LAUNCH_PROFILE" ]; then
		return
	fi

	eval "$("$PYTHON" - "$SOURCE_LAUNCH_PROFILE" <<'PY'
import json
import shlex
import sys

path = sys.argv[1]
with open(path, "r", encoding="utf-8") as handle:
    profile = json.load(handle)

def assign(name, value):
    if value is None:
        return
    text = str(value).strip()
    if not text:
        return
    print(f"{name}={shlex.quote(text)}")

assign("SOURCE_MAP", profile.get("MapName"))
assign("SOURCE_HOSTNAME", profile.get("HostName"))
assign("SOURCE_MAX_PLAYERS", profile.get("MaxPlayers"))
assign("SOURCE_PROFILE_GAME_MODE", profile.get("GameMode"))
assign("SOURCE_PROFILE_RANKING_MODE", profile.get("RankingMode"))
assign("SOURCE_PROFILE_TIME_LIMIT", profile.get("TimeLimitMinutes"))
assign("SOURCE_PROFILE_MAX_ROUNDS", profile.get("MaxRounds"))
source_arguments = profile.get("SourceArguments") or []
if "+tf_flag_caps_per_round" in source_arguments or profile.get("GameMode") == "capture-the-flag":
    assign("SOURCE_PROFILE_FLAG_CAPTURE_LIMIT", profile.get("FlagCaptureLimit"))
auto_balance = profile.get("AutoBalance")
if auto_balance is not None:
    assign("SOURCE_PROFILE_AUTO_BALANCE", "1" if auto_balance else "0")
PY
)"
}

check_layout() {
	status=0
	if [ ! -x "$DOTNET" ]; then
		echo "missing dotnet: $DOTNET" >&2
		status=1
	fi
	if ! command -v "$PYTHON" >/dev/null 2>&1; then
		echo "missing python: $PYTHON" >&2
		status=1
	fi
	check_file "$ARCADIA_ROOT/src/server/Arcadia.csproj" "Arcadia project" || status=1
	check_file "$ROOT/src/PlasmaGameManager.Server/PlasmaGameManager.Server.csproj" "PlasmaGameManager server project" || status=1
	check_file "$ROOT/src/PlasmaGameManager.ControlPanel/PlasmaGameManager.ControlPanel.csproj" "PlasmaGameManager control panel project" || status=1
	check_executable "$ROOT/scripts/run-server.sh" "PlasmaGameManager launcher" || status=1
	case "$BACKEND_OWNER" in
		stack)
			case "$SOURCE_BACKEND" in
				replay)
					check_executable "$ROOT/scripts/run-source-replay-backend.sh" "Source replay backend launcher" || status=1
					check_path "$SOURCE_REPLAY_PCAP" "Source replay PCAP or corpus directory" || status=1
					;;
				srcds)
					check_executable "$SOURCE_SERVER_ROOT/tools/run_tf2ps3_dedicated.sh" "Source server launcher" || status=1
					;;
				generated)
					;;
				*)
					echo "unsupported TF2PS3_SOURCE_BACKEND=$SOURCE_BACKEND; use generated, replay, or srcds" >&2
					status=1
					;;
			esac
			;;
		arcadia)
			if [ "$SOURCE_BACKEND" != "srcds" ]; then
				echo "TF2PS3_BACKEND_OWNER=arcadia is a legacy external-backend mode and currently requires TF2PS3_SOURCE_BACKEND=srcds" >&2
				status=1
			fi
			check_executable "$SOURCE_SERVER_ROOT/tools/run_tf2ps3_dedicated.sh" "Source server launcher" || status=1
			;;
		*)
			echo "unsupported TF2PS3_BACKEND_OWNER=$BACKEND_OWNER; use stack or arcadia" >&2
			status=1
			;;
	esac
	if [ -n "$SOURCE_LAUNCH_PROFILE" ]; then
		check_file "$SOURCE_LAUNCH_PROFILE" "TF2 Source launch profile" || status=1
	fi
	if [ "$status" -eq 0 ]; then
		echo "stack layout ok"
		echo "arcadia:       $ARCADIA_ROOT"
		echo "backend owner: $BACKEND_OWNER"
		echo "source backend kind: $SOURCE_BACKEND"
		if [ "$SOURCE_BACKEND" = "srcds" ]; then
			echo "source-server: $SOURCE_SERVER_ROOT"
		elif [ "$SOURCE_BACKEND" = "replay" ]; then
			echo "source replay: $SOURCE_REPLAY_PCAP"
			if [ -n "$SOURCE_REPLAY_PREFERRED_SCRIPT" ]; then
				echo "source replay preferred script: $SOURCE_REPLAY_PREFERRED_SCRIPT"
			fi
		else
			echo "source generated: PlasmaGameManager PS3-native responder"
		fi
		echo "game endpoint: $GAME_HOST:$GAME_PORT"
		echo "source backend:$SOURCE_HOST:$SOURCE_PORT"
		echo "plasma source protocol: $PLASMA_SOURCE_PROTOCOL_EFFECTIVE"
		if [ "$PUBLIC_ENDPOINT_MODE" = "plasma" ]; then
			echo "public endpoint:$GAME_HOST:$PUBLIC_PORT -> PlasmaGameManager"
		else
			echo "public/drop:   $GAME_HOST:$PUBLIC_PORT"
		fi
		echo "control API:   http://$PLASMA_CONTROL_BIND:$PLASMA_CONTROL_PORT/"
		if [ "$PLASMA_PANEL_ENABLED" = "1" ]; then
			echo "control panel: http://$PLASMA_PANEL_BIND:$PLASMA_PANEL_PORT/"
		fi
		echo "game metadata: $SOURCE_HOSTNAME / $SOURCE_MAP / max $SOURCE_MAX_PLAYERS"
		if [ -f "$TF2PS3_MAP_METADATA" ]; then
			echo "map metadata:  $TF2PS3_MAP_METADATA"
		else
			echo "map metadata:  not found; generated map defaults will be used"
		fi
		if [ -n "$SOURCE_LAUNCH_PROFILE" ]; then
			echo "source profile: $SOURCE_LAUNCH_PROFILE"
			echo "source rules:   mode=${SOURCE_PROFILE_GAME_MODE:-} ranked=${SOURCE_PROFILE_RANKING_MODE:-} timelimit=${SOURCE_PROFILE_TIME_LIMIT:-} maxrounds=${SOURCE_PROFILE_MAX_ROUNDS:-} flagcaps=${SOURCE_PROFILE_FLAG_CAPTURE_LIMIT:-} autobalance=${SOURCE_PROFILE_AUTO_BALANCE:-}"
		fi
		echo "logs:          $LOG_DIR"
		echo "arcadia profile path: $ARCADIA_SOURCE_PROFILE_PATH"
		if [ "$CAPTURE_IDENTITY" = "1" ]; then
			echo "capture mode:  GID=$TF2_GAME_GID TICKET=$TF2_GAME_TICKET PID=$TF2_GAME_PID HUID=$TF2_GAME_HUID"
		fi
	fi
	return "$status"
}

ensure_arcadia_sdk_pin() {
	if [ "$ARCADIA_ROOT" = "$ROOT/arcadia" ] && [ ! -f "$ARCADIA_ROOT/global.json" ]; then
		cat > "$ARCADIA_ROOT/global.json" <<EOF
{
  "sdk": {
    "version": "$ARCADIA_SDK_VERSION",
    "rollForward": "latestMajor"
  }
}
EOF
	fi
}

case "${1:-}" in
	--help|-h)
		usage
		exit 0
		;;
	--check)
		summary_enabled=0
		write_stack_source_launch_profile
		apply_source_launch_profile
		check_layout
		exit $?
		;;
	"")
		;;
	*)
		usage >&2
		exit 2
		;;
esac

write_stack_source_launch_profile
apply_source_launch_profile
check_layout
check_udp_port_free "$PLASMA_BIND" "$GAME_PORT" "PlasmaGameManager"
if [ "$PUBLIC_ENDPOINT_MODE" = "plasma" ]; then
	if [ "$PUBLIC_PORT" != "$GAME_PORT" ]; then
		check_udp_port_free "$PLASMA_BIND" "$PUBLIC_PORT" "PlasmaGameManager public endpoint"
	fi
else
	check_udp_port_free "$PLASMA_BIND" "$PUBLIC_PORT" "Public/drop endpoint"
fi
if [ "$SOURCE_BACKEND" != "generated" ]; then
	check_udp_port_free "$SOURCE_HOST" "$SOURCE_PORT" "Source backend"
fi
ensure_arcadia_sdk_pin
mkdir -p "$LOG_DIR"
: > "$PLASMA_EVIDENCE_LOG"
: > "$ARCADIA_LOG"
: > "$SOURCE_LOG"
: > "$PLASMA_LOG"
: > "$PANEL_LOG"
: > "$SUMMARY_LOG"
: > "$SOURCE_REPLAY_LOG"
: > "$PUBLIC_DROP_LOG"
: > "$TF2_RANKED_STATS_LOG"

echo "building Arcadia/FESL/Theater..."
(
	cd "$ARCADIA_ROOT"
	exec "$DOTNET" build src/server/Arcadia.csproj
) > "$ARCADIA_LOG" 2>&1

echo "building PlasmaGameManager..."
"$DOTNET" build "$ROOT/src/PlasmaGameManager.Server/PlasmaGameManager.Server.csproj" > "$PLASMA_LOG" 2>&1
if [ "$PLASMA_PANEL_ENABLED" = "1" ]; then
	echo "building PlasmaGameManager control panel..."
	"$DOTNET" build "$ROOT/src/PlasmaGameManager.ControlPanel/PlasmaGameManager.ControlPanel.csproj" > "$PANEL_LOG" 2>&1
fi

echo "starting Arcadia/FESL/Theater..."
(
	cd "$ARCADIA_ROOT/src/server"
	ARCADIA_ArcadiaSettings__ListenAddress="${ARCADIA_LISTEN_ADDRESS:-0.0.0.0}" \
	ARCADIA_ArcadiaSettings__TheaterAddress="${ARCADIA_THEATER_ADDRESS:-theater.ps3.arcadia}" \
	ARCADIA_ArcadiaSettings__MessengerAddress="${ARCADIA_MESSENGER_ADDRESS:-messaging.ea.com}" \
	ARCADIA_ArcadiaSettings__MessengerPort="${ARCADIA_MESSENGER_PORT:-42069}" \
	ARCADIA_Tf2BackendSettings__EnableBackendProvisioning=true \
	ARCADIA_Tf2BackendSettings__AdvertisedHost="$GAME_HOST" \
	ARCADIA_Tf2BackendSettings__SourceBackendHost="$SOURCE_HOST" \
	ARCADIA_Tf2BackendSettings__PublicPort="$PUBLIC_PORT" \
	ARCADIA_Tf2BackendSettings__GameManagerPort="$GAME_PORT" \
	ARCADIA_Tf2BackendSettings__SourcePort="$SOURCE_PORT" \
	ARCADIA_Tf2BackendSettings__GameManagerSourceTimeoutMilliseconds="${ARCADIA_TF2_GAMEMANAGER_SOURCE_TIMEOUT_MS:-1000}" \
	ARCADIA_Tf2BackendSettings__GameManagerEvidenceLog="$PLASMA_EVIDENCE_LOG" \
	ARCADIA_Tf2BackendSettings__SourceLaunchProfilePath="$ARCADIA_SOURCE_PROFILE_PATH" \
	ARCADIA_Tf2BackendSettings__NativeStatsExportPath="$TF2_RANKED_STATS_LOG" \
	ARCADIA_Tf2BackendSettings__LaunchGameManager="$([ "$BACKEND_OWNER" = "arcadia" ] && echo true || echo false)" \
	ARCADIA_Tf2BackendSettings__LaunchSourceServer="$([ "$BACKEND_OWNER" = "arcadia" ] && echo true || echo false)" \
	ARCADIA_Tf2BackendSettings__GameManagerWorktree="$ROOT" \
	ARCADIA_Tf2BackendSettings__GameManagerLaunchScript="scripts/run-server.sh" \
	ARCADIA_Tf2BackendSettings__SourceWorktree="$SOURCE_SERVER_ROOT" \
	ARCADIA_Tf2BackendSettings__SourceLaunchScript="tools/run_tf2ps3_dedicated.sh" \
	ARCADIA_Tf2BackendSettings__WaitForSourceReady="$([ "$BACKEND_OWNER" = "arcadia" ] && echo true || echo false)" \
	ARCADIA_Tf2BackendSettings__AutoCreateQuickMatchServer=true \
	ARCADIA_Tf2BackendSettings__DefaultMap="$SOURCE_MAP" \
	ARCADIA_Tf2BackendSettings__DefaultHostname="$SOURCE_HOSTNAME" \
	ARCADIA_Tf2BackendSettings__StaticGameEncryptionKey="$TF2_GAME_EKEY" \
	ARCADIA_Tf2BackendSettings__StaticGameUgId="$TF2_GAME_UGID" \
	ARCADIA_Tf2BackendSettings__StaticQuickMatchGameId="$TF2_GAME_GID" \
	ARCADIA_Tf2BackendSettings__StaticQuickMatchTicket="$TF2_GAME_TICKET" \
	ARCADIA_Tf2BackendSettings__StaticQuickMatchPlayerId="$TF2_GAME_PID" \
	ARCADIA_Tf2BackendSettings__QuickMatchServerUid="$TF2_GAME_HUID" \
	ARCADIA_DebugSettings__EnableFileLogging="${ARCADIA_ENABLE_FILE_LOGGING:-true}" \
	ARCADIA_DebugSettings__DisableTheaterJoinTimeout="${ARCADIA_DISABLE_THEATER_JOIN_TIMEOUT:-true}" \
	ARCADIA_DnsSettings__EnableDns="${ARCADIA_ENABLE_DNS:-false}" \
	exec "$DOTNET" run --no-build --project Arcadia.csproj
) >> "$ARCADIA_LOG" 2>&1 &
arcadia_pid=$!

sleep "${TF2PS3_ARCADIA_STARTUP_DELAY:-2}"
require_process_running "$arcadia_pid" "Arcadia/FESL/Theater" "$ARCADIA_LOG"

if [ "$PUBLIC_ENDPOINT_MODE" = "plasma" ]; then
	cat > "$PUBLIC_DROP_LOG" <<EOF
public endpoint mode is plasma.
No public/drop listener is running; PlasmaGameManager listens on $PUBLIC_PORT and $GAME_PORT.
Use TF2PS3_PUBLIC_ENDPOINT_MODE=drop to restore the diagnostic drop logger.
EOF
else
echo "starting public/drop UDP listener on $PLASMA_BIND:$PUBLIC_PORT..."
"$PYTHON" - "$PLASMA_BIND" "$PUBLIC_PORT" "$PUBLIC_DROP_LOG" <<'PY' &
import socket
import sys
import time

host = sys.argv[1]
port = int(sys.argv[2])
log_path = sys.argv[3]

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((host, port))
sock.settimeout(1.0)

with open(log_path, "a", encoding="utf-8") as log:
    log.write(f"public/drop UDP listener on {host}:{port}\n")
    log.flush()
    while True:
        try:
            data, addr = sock.recvfrom(4096)
        except socket.timeout:
            continue
        now = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        log.write(f"{now} recv {len(data)} bytes from {addr[0]}:{addr[1]} {data.hex()}\n")
        log.flush()
PY
	public_udp_pid=$!
	sleep 1
	require_process_running "$public_udp_pid" "Public/drop UDP listener" "$PUBLIC_DROP_LOG"
fi

if [ "$BACKEND_OWNER" = "arcadia" ]; then
	cat <<EOF
stack running
  Arcadia pid:       $arcadia_pid  log: $ARCADIA_LOG
  Public endpoint:   $PUBLIC_ENDPOINT_MODE log: $PUBLIC_DROP_LOG
  Arcadia will launch PlasmaGameManager and Source backend from TF2 create/quick-match requests.
  Evidence log:      $PLASMA_EVIDENCE_LOG
  Arcadia profile:   $ARCADIA_SOURCE_PROFILE_PATH

RPCS3 IP/Host switch target:
  theater.ps3.arcadia=$GAME_HOST&&hl2-ps3.fesl.ea.com=$GAME_HOST&&messaging.ea.com=$GAME_HOST

Press Ctrl+C to stop the stack.
EOF
	wait "$arcadia_pid"
	exit $?
fi

if [ "$SOURCE_BACKEND" = "generated" ]; then
	echo "using generated PS3-native Source responder inside PlasmaGameManager"
	cat > "$SOURCE_LOG" <<EOF
generated PS3-native Source responder is hosted inside PlasmaGameManager.
No separate Source backend process is expected in this mode.
Native Source traffic and generated responses are recorded in:
  $PLASMA_EVIDENCE_LOG
EOF
else
	echo "starting Source backend ($SOURCE_BACKEND) on $SOURCE_HOST:$SOURCE_PORT..."
fi
case "$SOURCE_BACKEND" in
	replay)
		(
			cd "$ROOT"
			exec scripts/run-source-replay-backend.sh \
				"$SOURCE_REPLAY_PCAP" \
				"$SOURCE_HOST" \
				"$SOURCE_PORT" \
				"$SOURCE_REPLAY_LOG" \
				"$SOURCE_REPLAY_MATCH_MODE" \
				"$SOURCE_REPLAY_SEARCH_WINDOW" \
				"$SOURCE_REPLAY_PACING" \
				"$SOURCE_REPLAY_MAX_DELAY_MS" \
				"$SOURCE_REPLAY_BACKEND_MODE" \
				"$SOURCE_REPLAY_PREFERRED_SCRIPT"
		) > "$SOURCE_LOG" 2>&1 &
		;;
	srcds)
		(
			cd "$SOURCE_SERVER_ROOT"
			TF2PS3_DEDICATED_PORT="$SOURCE_PORT" \
			TF2PS3_DEDICATED_MAP="$SOURCE_MAP" \
			TF2PS3_DEDICATED_HOSTNAME="$SOURCE_HOSTNAME" \
			TF2PS3_DEDICATED_MAX_PLAYERS="$SOURCE_MAX_PLAYERS" \
			TF2PS3_DEDICATED_GAME_MODE="${SOURCE_PROFILE_GAME_MODE:-}" \
			TF2PS3_DEDICATED_RANKING_MODE="${SOURCE_PROFILE_RANKING_MODE:-}" \
			TF2PS3_DEDICATED_TIME_LIMIT="${SOURCE_PROFILE_TIME_LIMIT:-}" \
			TF2PS3_DEDICATED_MAX_ROUNDS="${SOURCE_PROFILE_MAX_ROUNDS:-}" \
			TF2PS3_DEDICATED_FLAG_CAPTURE_LIMIT="${SOURCE_PROFILE_FLAG_CAPTURE_LIMIT:-}" \
			TF2PS3_DEDICATED_AUTO_BALANCE="${SOURCE_PROFILE_AUTO_BALANCE:-}" \
			TF2PS3_NET_TRACE="$NET_TRACE" \
			TF2PS3_DEDICATED_LOGGING="${TF2PS3_DEDICATED_LOGGING:-1}" \
			exec tools/run_tf2ps3_dedicated.sh
		) > "$SOURCE_LOG" 2>&1 &
		;;
	generated)
		:
		;;
esac
if [ "$SOURCE_BACKEND" != "generated" ]; then
	source_pid=$!

	sleep "${TF2PS3_SOURCE_STARTUP_DELAY:-4}"
	require_process_running "$source_pid" "Source backend" "$SOURCE_LOG"
fi

if [ "$PUBLIC_ENDPOINT_MODE" = "plasma" ] && [ "$PUBLIC_PORT" != "$GAME_PORT" ]; then
	PLASMA_LISTEN_PORTS="$GAME_PORT,$PUBLIC_PORT"
else
	PLASMA_LISTEN_PORTS="$GAME_PORT"
fi

echo "starting PlasmaGameManager on $GAME_HOST:$PLASMA_LISTEN_PORTS -> source $SOURCE_HOST:$SOURCE_PORT..."
(
	cd "$ROOT"
	PLASMA_PROFILE="${PLASMA_PROFILE:-tf2-ps3}" \
	PLASMA_TF2_MAP_METADATA="$([ -f "$TF2PS3_MAP_METADATA" ] && printf '%s' "$TF2PS3_MAP_METADATA" || true)" \
	PLASMA_SOURCE_PROXY="$([ "$SOURCE_BACKEND" = "generated" ] && echo 0 || echo 1)" \
	PLASMA_SOURCE_PROTOCOL="$PLASMA_SOURCE_PROTOCOL_EFFECTIVE" \
	PLASMA_SOURCE_LAUNCH_PROFILE="$SOURCE_LAUNCH_PROFILE" \
	PLASMA_SOURCE_LAUNCH_PROFILE_GLOB="$ARCADIA_SOURCE_PROFILE_PATH" \
	exec "$DOTNET" run --no-build --project src/PlasmaGameManager.Server/PlasmaGameManager.Server.csproj -- \
		--bind "$PLASMA_BIND" \
		--ports "$PLASMA_LISTEN_PORTS" \
		--profile "${PLASMA_PROFILE:-tf2-ps3}" \
		--game-local-id 257 \
		--game-id "$TF2_GAME_GID" \
		--game-map "$SOURCE_MAP" \
		--game-name "$SOURCE_HOSTNAME" \
		--max-players "$SOURCE_MAX_PLAYERS" \
		--advertised-host "$GAME_HOST" \
		--advertised-port "$PUBLIC_PORT" \
		--source-host "$SOURCE_HOST" \
		--source-port "$SOURCE_PORT" \
		--source-timeout-ms "${PLASMA_SOURCE_TIMEOUT_MS:-250}" \
		--source-launch-profile "$SOURCE_LAUNCH_PROFILE" \
		--source-launch-profile-glob "$ARCADIA_SOURCE_PROFILE_PATH" \
		--tf2-ranked-stats-export "$TF2_RANKED_STATS_LOG" \
		--control-bind "$PLASMA_CONTROL_BIND" \
		--control-port "$PLASMA_CONTROL_PORT" \
		--control-user "$PLASMA_CONTROL_USER" \
		--control-password "$PLASMA_CONTROL_PASSWORD" \
		--evidence-log "$PLASMA_EVIDENCE_LOG"
) > "$PLASMA_LOG" 2>&1 &
plasma_pid=$!

sleep "${TF2PS3_PLASMA_STARTUP_DELAY:-1}"
require_process_running "$plasma_pid" "PlasmaGameManager" "$PLASMA_LOG"

if [ "$PLASMA_PANEL_ENABLED" = "1" ]; then
	echo "starting PlasmaGameManager control panel on $PLASMA_PANEL_BIND:$PLASMA_PANEL_PORT..."
	(
		cd "$ROOT"
		PLASMA_CONTROL_API_URL="http://$PLASMA_CONTROL_BIND:$PLASMA_CONTROL_PORT" \
		PLASMA_CONTROL_USER="$PLASMA_CONTROL_USER" \
		PLASMA_CONTROL_PASSWORD="$PLASMA_CONTROL_PASSWORD" \
		exec "$DOTNET" run --no-build --project src/PlasmaGameManager.ControlPanel/PlasmaGameManager.ControlPanel.csproj -- \
			--bind "$PLASMA_PANEL_BIND" \
			--port "$PLASMA_PANEL_PORT" \
			--api-url "http://$PLASMA_CONTROL_BIND:$PLASMA_CONTROL_PORT" \
			--user "$PLASMA_CONTROL_USER" \
			--password "$PLASMA_CONTROL_PASSWORD"
	) > "$PANEL_LOG" 2>&1 &
	panel_pid=$!

	sleep "${TF2PS3_PANEL_STARTUP_DELAY:-1}"
	require_process_running "$panel_pid" "PlasmaGameManager control panel" "$PANEL_LOG"
fi

cat <<EOF
stack running
  Arcadia pid:       $arcadia_pid  log: $ARCADIA_LOG
  Public endpoint:   $PUBLIC_ENDPOINT_MODE log: $PUBLIC_DROP_LOG
  Source pid:        ${source_pid:-generated}   log: $SOURCE_LOG
  PlasmaGameManager: $plasma_pid   log: $PLASMA_LOG ports: $PLASMA_LISTEN_PORTS
  Control panel:     ${panel_pid:-disabled}   log: $PANEL_LOG
  Evidence log:      $PLASMA_EVIDENCE_LOG
  Source replay log: $SOURCE_REPLAY_LOG
  Summary log:       $SUMMARY_LOG
  Control API:       http://$PLASMA_CONTROL_BIND:$PLASMA_CONTROL_PORT/
  Control panel URL: http://$PLASMA_PANEL_BIND:$PLASMA_PANEL_PORT/
  Admin login:       $PLASMA_CONTROL_USER / $PLASMA_CONTROL_PASSWORD

RPCS3 IP/Host switch target:
  theater.ps3.arcadia=$GAME_HOST&&hl2-ps3.fesl.ea.com=$GAME_HOST&&messaging.ea.com=$GAME_HOST

Press Ctrl+C to stop the stack.
EOF

if [ -n "$source_pid" ] && [ -n "$panel_pid" ]; then
	wait "$arcadia_pid" "$source_pid" "$plasma_pid" "$panel_pid"
elif [ -n "$source_pid" ]; then
	wait "$arcadia_pid" "$source_pid" "$plasma_pid"
elif [ -n "$panel_pid" ]; then
	wait "$arcadia_pid" "$plasma_pid" "$panel_pid"
else
	wait "$arcadia_pid" "$plasma_pid"
fi
