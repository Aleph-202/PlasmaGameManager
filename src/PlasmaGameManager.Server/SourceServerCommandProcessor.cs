using System.Globalization;
using System.IO;
using System.Text;

namespace PlasmaGameManager.Server;

public static class SourceServerCommandProcessor
{
    private const int MaxExecCommands = 128;
    private const uint BurningConditionMask = 1U << 10;
    private const uint InvulnerableConditionMask = 1U << 5;
    private const uint TauntingConditionMask = 1U << 7;
    private const uint StunnedConditionMask = 1U << 14;
    private const uint BleedingConditionMask = 1U << 15;

    private static readonly HashSet<string> KnownSourceCvars = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp_autoteambalance",
        "mp_disable_respawn_times",
        "mp_forcecamera",
        "mp_friendlyfire",
        "mp_fraglimit",
        "mp_maxrounds",
        "mp_teams_unbalance_limit",
        "mp_timelimit",
        "mp_tournament",
        "mp_tournament_readymode",
        "mp_tournament_restart",
        "mp_waitingforplayers_time",
        "mp_winlimit",
        "nextlevel",
        "sv_alltalk",
        "sv_cheats",
        "sv_gravity",
        "sv_lan",
        "sv_password",
        "sv_pure",
        "sv_tags",
        "tf_arena_use_queue",
        "tf_bot_difficulty",
        "tf_bot_join_after_player",
        "tf_bot_quota",
        "tf_ctf_bonus_time",
        "tf_damage_disablespread",
        "tf_flag_caps_per_round",
        "tf_weapon_criticals"
    };

    public static SourceServerCommandResult Execute(
        GameManagerSession game,
        string commandText,
        string? targetEndpoint,
        string issuedBy)
    {
        var tokens = Tokenize(commandText);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("Command text cannot be empty.", nameof(commandText));
        }

        var command = tokens[0].ToLowerInvariant();
        return command switch
        {
            "say" or "sm_say" or "sm_chat" => QueueChat(game, JoinTail(tokens, 1), null, issuedBy),
            "say_team" => QueueMessage(game, SourceServerCommandType.TeamChat, JoinTail(tokens, 1), targetEndpoint, issuedBy, "say_team"),
            "tell" or "psay" or "msg" or "whisper" or "sm_psay" => QueuePrivateMessage(game, tokens, targetEndpoint, issuedBy),
            "csay" or "centerprint" or "sm_csay" => QueueMessage(game, SourceServerCommandType.CenterMessage, JoinTail(tokens, 1), targetEndpoint, issuedBy, command),
            "hint" or "sm_hint" or "hsay" or "sm_hsay" => QueueMessage(game, SourceServerCommandType.HintMessage, JoinTail(tokens, 1), targetEndpoint, issuedBy, command),
            "msay" or "sm_msay" or "panel" or "sm_panel" => QueueMessage(game, SourceServerCommandType.PanelMessage, JoinTail(tokens, 1), targetEndpoint, issuedBy, command),
            "tsay" or "sm_tsay" or "sm_hud" or "hud" => QueueMessage(game, SourceServerCommandType.HudMessage, JoinTail(tokens, 1), targetEndpoint, issuedBy, command),
            "announce" or "sm_announce" or "tf2_announce" or "broadcast" or "sm_broadcast" => Announce(game, JoinTail(tokens, 1), targetEndpoint, issuedBy),
            "alert" or "sm_alert" or "tf2_alert" => Alert(game, JoinTail(tokens, 1), targetEndpoint, issuedBy),
            "clearchat" or "sm_clearchat" or "tf2_clearchat" => ClearChat(game, issuedBy),
            "play" or "sm_play" => PlaySound(game, tokens, targetEndpoint, issuedBy),
            "callvote" or "vote" or "sm_vote" => StartVote(game, tokens, issuedBy),
            "votemap" or "sm_votemap" => StartMapVote(game, tokens, issuedBy),
            "votekick" or "sm_votekick" => StartKickVote(game, tokens, targetEndpoint, issuedBy),
            "voteyes" or "vote_yes" or "sm_voteyes" => CastVote(game, targetEndpoint, issuedBy, yes: true),
            "voteno" or "vote_no" or "sm_voteno" => CastVote(game, targetEndpoint, issuedBy, yes: false),
            "cancelvote" or "sm_cancelvote" => CancelVote(game, issuedBy),
            "passvote" or "sm_passvote" => CompleteVote(game, issuedBy, passed: true),
            "failvote" or "sm_failvote" => CompleteVote(game, issuedBy, passed: false),
            "echo" => Echo(game, JoinTail(tokens, 1), issuedBy),
            "help" or "sm_help" => Help(game, issuedBy),
            "cvarlist" => CvarList(game, issuedBy),
            "log" => LogCommand(game, tokens, issuedBy),
            "logaddress_add" => LogAddressAdd(game, tokens, issuedBy),
            "logaddress_del" or "logaddress_delall" => LogAddressDelete(game, tokens, issuedBy),
            "logaddress_list" => LogAddressList(game, issuedBy),
            "tf2_logpath" or "logpath" => SetLogPath(game, tokens, issuedBy),
            "tf2_logclear" or "logclear" => ClearLog(game, issuedBy),
            "exec" => ExecConfig(game, tokens, issuedBy),
            "sm_execcfg" => SourceModExecConfig(game, tokens, issuedBy),
            "hostname" or "host_name" or "sv_hostname" => SetHostname(game, JoinTail(tokens, 1), issuedBy),
            "changelevel" or "map" or "sm_map" => ChangeLevel(game, tokens, issuedBy),
            "maxplayers" or "sv_visiblemaxplayers" => SetMaxPlayers(game, tokens, issuedBy),
            "mp_timelimit" => SetIntRule(game, tokens, issuedBy, "mp_timelimit"),
            "mp_maxrounds" or "mp_winlimit" => SetIntRule(game, tokens, issuedBy, command),
            "tf_flag_caps_per_round" or "tf_ctf_bonus_time" => SetIntRule(game, tokens, issuedBy, command),
            "mp_autoteambalance" or "mp_teams_unbalance_limit" => SetBoolRule(game, tokens, issuedBy, command),
            "mp_tournament" or "mp_tournament_readymode" => SetTournamentBoolRule(game, tokens, issuedBy, command),
            "mp_tournament_restart" or "tournament_restart" or "tf2_tournament_restart" or "sm_tournament_restart" => TournamentRestart(game, issuedBy),
            "ready" or "sm_ready" or "tf2_ready" or "tournament_ready" => SetPlayerReady(game, tokens, targetEndpoint, issuedBy, ready: true),
            "unready" or "notready" or "sm_unready" or "tf2_unready" or "tournament_unready" => SetPlayerReady(game, tokens, targetEndpoint, issuedBy, ready: false),
            "readylist" or "sm_readylist" or "tf2_readylist" => ReadyList(game, issuedBy),
            "status" => Status(game, issuedBy),
            "users" or "players" or "sm_who" => Users(game, issuedBy),
            "scoreboard" or "sm_scoreboard" or "tf2_scoreboard" => Scoreboard(game, issuedBy),
            "timeleft" or "sm_timeleft" => TimeLeft(game, issuedBy),
            "nextmap" or "sm_nextmap" => NextMap(game, issuedBy),
            "maps" or "maplist" or "sm_maps" or "sm_maplist" => MapList(game, issuedBy),
            "setnextmap" or "sm_setnextmap" or "nextlevel" => SetNextMap(game, tokens, issuedBy),
            "kick" or "kickid" or "sm_kick" => Kick(game, tokens, targetEndpoint, issuedBy),
            "ban" or "banid" or "banip" => Ban(game, tokens, targetEndpoint, issuedBy),
            "sm_ban" or "sm_banip" => SourceModBan(game, tokens, targetEndpoint, issuedBy),
            "unban" or "removeid" or "removeip" or "sm_unban" => RemoveBan(game, tokens, issuedBy),
            "listid" or "listip" => ListBans(game, issuedBy),
            "mute" or "sm_mute" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: true, chatGagged: null, "muted"),
            "unmute" or "sm_unmute" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: false, chatGagged: null, "unmuted"),
            "gag" or "sm_gag" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: null, chatGagged: true, "gagged"),
            "ungag" or "sm_ungag" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: null, chatGagged: false, "ungagged"),
            "silence" or "sm_silence" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: true, chatGagged: true, "silenced"),
            "unsilence" or "sm_unsilence" => SetPlayerComms(game, tokens, targetEndpoint, issuedBy, voiceMuted: false, chatGagged: false, "unsilenced"),
            "listmutes" or "listgags" or "comms" => ListComms(game, issuedBy),
            "tf_bot_add" or "bot_add" => AddBots(game, tokens, issuedBy),
            "tf_bot_kick" or "bot_kick" => KickBots(game, tokens, issuedBy),
            "tf_bot_quota" => SetBotQuota(game, tokens, issuedBy),
            "tf2_rename" or "rename" or "sm_rename" => RenamePlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_setteam" or "setteam" or "team" or "sm_team" => SetPlayerTeam(game, tokens, targetEndpoint, issuedBy),
            "tf2_spectate" or "spectate" or "sm_spec" or "sm_spectate" => SpectatePlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_swapteam" or "swapteam" or "sm_swap" => SwapPlayerTeam(game, tokens, targetEndpoint, issuedBy),
            "tf2_setclass" or "setclass" or "class" => SetPlayerClass(game, tokens, targetEndpoint, issuedBy),
            "tf2_sethealth" or "sethealth" or "heal" => SetPlayerHealth(game, tokens, targetEndpoint, issuedBy),
            "tf2_setmaxhealth" or "setmaxhealth" or "sm_setmaxhealth" => SetPlayerMaxHealth(game, tokens, targetEndpoint, issuedBy),
            "tf2_heal" or "sm_heal" or "sm_addhealth" or "tf2_addhealth" => HealPlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_overheal" or "overheal" or "sm_overheal" => OverhealPlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_damage" or "damage" or "hurt" or "sm_damage" or "sm_hurt" => DamagePlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_kill" or "slay" or "killplayer" or "sm_slay" => KillPlayer(game, tokens, targetEndpoint, issuedBy),
            "kill" => targetEndpoint is null && tokens.Length < 2
                ? QueueRawCommand(game, commandText, targetEndpoint, issuedBy, "queued passthrough command: kill")
                : KillPlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_respawn" or "respawn" or "sm_respawn" => RespawnPlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_getpos" or "getpos" or "sm_getpos" => GetPlayerPosition(game, tokens, targetEndpoint, issuedBy),
            "tf2_setpos" or "setpos" or "sm_setpos" => SetPlayerPosition(game, tokens, targetEndpoint, issuedBy),
            "tf2_teleport" or "teleport" or "sm_teleport" => TeleportPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_bring" or "bring" => BringPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_goto" or "goto" => GotoPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_beacon" or "beacon" => BeaconPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_slap" or "slap" => SlapPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_burn" or "burn" => BurnPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_extinguish" or "extinguish" => ExtinguishPlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_bleed" or "bleed" or "tf2_bleed" => AddTimedPlayerCondition(game, tokens, targetEndpoint, issuedBy, BleedingConditionMask, "bleeding"),
            "sm_stun" or "stun" or "tf2_stun" => AddTimedPlayerCondition(game, tokens, targetEndpoint, issuedBy, StunnedConditionMask, "stunned"),
            "sm_taunt" or "taunt" or "tf2_taunt" => AddTimedPlayerCondition(game, tokens, targetEndpoint, issuedBy, TauntingConditionMask, "taunting"),
            "sm_god" or "god" => SetPlayerGodMode(game, tokens, targetEndpoint, issuedBy, enabled: true),
            "sm_ungod" or "ungod" => SetPlayerGodMode(game, tokens, targetEndpoint, issuedBy, enabled: false),
            "sm_noclip" or "noclip" => SetPlayerNoClip(game, tokens, targetEndpoint, issuedBy, enabled: true),
            "sm_clip" or "clip" => SetPlayerNoClip(game, tokens, targetEndpoint, issuedBy, enabled: false),
            "tf2_freeze" or "freeze" or "sm_freeze" => SetPlayerFrozen(game, tokens, targetEndpoint, issuedBy, frozen: true),
            "tf2_unfreeze" or "unfreeze" or "sm_unfreeze" => SetPlayerFrozen(game, tokens, targetEndpoint, issuedBy, frozen: false),
            "tf2_setspeed" or "setspeed" or "sm_speed" or "sm_setspeed" => SetPlayerSpeed(game, tokens, targetEndpoint, issuedBy),
            "tf2_setgravity" or "setgravity" or "sm_gravity" or "sm_setgravity" => SetPlayerGravity(game, tokens, targetEndpoint, issuedBy),
            "tf2_resetgravity" or "resetgravity" or "sm_resetgravity" => ResetPlayerGravity(game, tokens, targetEndpoint, issuedBy),
            "tf2_setfov" or "setfov" or "sm_fov" or "sm_setfov" => SetPlayerFov(game, tokens, targetEndpoint, issuedBy),
            "tf2_viewmodel" or "viewmodel" or "sm_viewmodel" => SetPlayerViewModel(game, tokens, targetEndpoint, issuedBy, forcedEnabled: null),
            "tf2_hideweapon" or "hideweapon" or "sm_hideweapon" => SetPlayerViewModel(game, tokens, targetEndpoint, issuedBy, forcedEnabled: false),
            "tf2_showweapon" or "showweapon" or "sm_showweapon" => SetPlayerViewModel(game, tokens, targetEndpoint, issuedBy, forcedEnabled: true),
            "sm_blind" or "blind" or "tf2_blind" => SetPlayerBlind(game, tokens, targetEndpoint, issuedBy, enabled: true),
            "sm_unblind" or "unblind" or "tf2_unblind" => SetPlayerBlind(game, tokens, targetEndpoint, issuedBy, enabled: false),
            "sm_shake" or "shake" or "tf2_shake" => ShakePlayer(game, tokens, targetEndpoint, issuedBy),
            "sm_color" or "sm_rendercolor" or "tf2_color" or "tf2_rendercolor" => SetPlayerRenderColor(game, tokens, targetEndpoint, issuedBy),
            "sm_alpha" or "sm_renderalpha" or "tf2_alpha" => SetPlayerRenderAlpha(game, tokens, targetEndpoint, issuedBy),
            "sm_rendermode" or "tf2_rendermode" => SetPlayerRenderMode(game, tokens, targetEndpoint, issuedBy),
            "sm_renderfx" or "tf2_renderfx" => SetPlayerRenderFx(game, tokens, targetEndpoint, issuedBy),
            "sm_effects" or "tf2_effects" => SetPlayerEffects(game, tokens, targetEndpoint, issuedBy),
            "sm_addeffect" or "tf2_addeffect" => AddPlayerEffects(game, tokens, targetEndpoint, issuedBy),
            "sm_removeeffect" or "sm_deleffect" or "tf2_removeeffect" => RemovePlayerEffects(game, tokens, targetEndpoint, issuedBy),
            "sm_resetrender" or "tf2_resetrender" => ResetPlayerRenderState(game, tokens, targetEndpoint, issuedBy),
            "sm_modelindex" or "tf2_modelindex" => SetPlayerModelIndex(game, tokens, targetEndpoint, issuedBy),
            "sm_textureframe" or "tf2_textureframe" => SetPlayerTextureFrameIndex(game, tokens, targetEndpoint, issuedBy),
            "sm_sequence" or "sm_anim" or "tf2_sequence" or "tf2_anim" => SetPlayerAnimation(game, tokens, targetEndpoint, issuedBy),
            "sm_skinbody" or "tf2_skinbody" => SetPlayerSkinBody(game, tokens, targetEndpoint, issuedBy),
            "sm_modelscale" or "tf2_modelscale" => SetPlayerModelScale(game, tokens, targetEndpoint, issuedBy),
            "sm_forcevector" or "tf2_forcevector" => SetPlayerForceVector(game, tokens, targetEndpoint, issuedBy),
            "sm_animflags" or "tf2_animflags" => SetPlayerAnimationFlags(game, tokens, targetEndpoint, issuedBy),
            "sm_lightingorigin" or "tf2_lightingorigin" => SetPlayerLightingOrigin(game, tokens, targetEndpoint, issuedBy),
            "sm_fade" or "tf2_fade" => SetPlayerFade(game, tokens, targetEndpoint, issuedBy),
            "sm_muzzleflash" or "tf2_muzzleflash" => BumpPlayerMuzzleFlash(game, tokens, targetEndpoint, issuedBy),
            "sm_resetanim" or "tf2_resetanim" => ResetPlayerAnimationState(game, tokens, targetEndpoint, issuedBy),
            "tf2_refill" or "refill" or "sm_refill" => RefillPlayer(game, tokens, targetEndpoint, issuedBy),
            "tf2_setammo" or "setammo" or "sm_setammo" or "sm_giveammo" => SetPlayerAmmo(game, tokens, targetEndpoint, issuedBy),
            "tf2_setsentry" or "setsentry" or "sm_sentry" or "sm_setsentry" => SetPlayerSentry(game, tokens, targetEndpoint, issuedBy),
            "tf2_setteleporter" or "setteleporter" or "sm_teleporter" or "sm_setteleporter" => SetPlayerTeleporter(game, tokens, targetEndpoint, issuedBy),
            "tf2_destroybuildings" or "destroybuildings" or "sm_destroybuildings" or "sm_destroy" => DestroyPlayerBuildables(game, tokens, targetEndpoint, issuedBy),
            "tf2_addcond" or "addcond" or "sm_addcond" => AddPlayerCondition(game, tokens, targetEndpoint, issuedBy),
            "tf2_removecond" or "removecond" or "sm_removecond" => RemovePlayerCondition(game, tokens, targetEndpoint, issuedBy),
            "tf2_setcond" or "setcond" or "sm_setcond" => SetPlayerCondition(game, tokens, targetEndpoint, issuedBy),
            "tf2_clearcond" or "clearcond" or "sm_clearcond" => ClearPlayerCondition(game, tokens, targetEndpoint, issuedBy),
            "tf2_disguise" or "disguise" or "sm_disguise" => SetPlayerDisguise(game, tokens, targetEndpoint, issuedBy),
            "tf2_cleardisguise" or "cleardisguise" or "sm_cleardisguise" => ClearPlayerDisguise(game, tokens, targetEndpoint, issuedBy),
            "tf2_cloak" or "cloak" or "sm_cloak" => SetPlayerCloak(game, tokens, targetEndpoint, issuedBy, 100.0f),
            "tf2_uncloak" or "uncloak" or "sm_uncloak" => SetPlayerCloak(game, tokens, targetEndpoint, issuedBy, 0.0f),
            "tf2_setscore" or "setscore" => SetPlayerScore(game, tokens, targetEndpoint, issuedBy),
            "tf2_addscore" or "addscore" => AddPlayerScore(game, tokens, targetEndpoint, issuedBy),
            "tf2_setcaptures" or "setcaptures" => SetPlayerCaptures(game, tokens, targetEndpoint, issuedBy),
            "tf2_addcaptures" or "addcaptures" => AddPlayerCaptures(game, tokens, targetEndpoint, issuedBy),
            "tf2_setdeaths" or "setdeaths" => SetPlayerDeaths(game, tokens, targetEndpoint, issuedBy),
            "tf2_adddeaths" or "adddeaths" => AddPlayerDeaths(game, tokens, targetEndpoint, issuedBy),
            "tf2_setassists" or "setassists" => SetPlayerAssists(game, tokens, targetEndpoint, issuedBy),
            "tf2_addassists" or "addassists" => AddPlayerAssists(game, tokens, targetEndpoint, issuedBy),
            "tf2_setdefenses" or "setdefenses" or "tf2_setdefences" or "setdefences" => SetPlayerDefenses(game, tokens, targetEndpoint, issuedBy),
            "tf2_adddefenses" or "adddefenses" or "tf2_adddefences" or "adddefences" => AddPlayerDefenses(game, tokens, targetEndpoint, issuedBy),
            "tf2_setdominations" or "setdominations" => SetPlayerDominations(game, tokens, targetEndpoint, issuedBy),
            "tf2_adddominations" or "adddominations" => AddPlayerDominations(game, tokens, targetEndpoint, issuedBy),
            "tf2_setrevenge" or "setrevenge" => SetPlayerRevenge(game, tokens, targetEndpoint, issuedBy),
            "tf2_addrevenge" or "addrevenge" => AddPlayerRevenge(game, tokens, targetEndpoint, issuedBy),
            "tf2_setdestruction" or "tf2_setdestructions" or "tf2_setbuildingsdestroyed" or "setdestruction" => SetPlayerBuildingsDestroyed(game, tokens, targetEndpoint, issuedBy),
            "tf2_adddestruction" or "tf2_adddestructions" or "tf2_addbuildingsdestroyed" or "adddestruction" => AddPlayerBuildingsDestroyed(game, tokens, targetEndpoint, issuedBy),
            "tf2_setheadshots" or "setheadshots" => SetPlayerHeadshots(game, tokens, targetEndpoint, issuedBy),
            "tf2_addheadshots" or "addheadshots" => AddPlayerHeadshots(game, tokens, targetEndpoint, issuedBy),
            "tf2_setbackstabs" or "setbackstabs" => SetPlayerBackstabs(game, tokens, targetEndpoint, issuedBy),
            "tf2_addbackstabs" or "addbackstabs" => AddPlayerBackstabs(game, tokens, targetEndpoint, issuedBy),
            "tf2_sethealing" or "tf2_sethealpoints" or "sethealing" => SetPlayerHealPoints(game, tokens, targetEndpoint, issuedBy),
            "tf2_addhealing" or "tf2_addhealpoints" or "addhealing" => AddPlayerHealPoints(game, tokens, targetEndpoint, issuedBy),
            "tf2_setinvulns" or "setinvulns" => SetPlayerInvulns(game, tokens, targetEndpoint, issuedBy),
            "tf2_addinvulns" or "addinvulns" => AddPlayerInvulns(game, tokens, targetEndpoint, issuedBy),
            "tf2_setteleports" or "setteleports" => SetPlayerTeleports(game, tokens, targetEndpoint, issuedBy),
            "tf2_addteleports" or "addteleports" => AddPlayerTeleports(game, tokens, targetEndpoint, issuedBy),
            "tf2_setresupplies" or "tf2_setresupply" or "setresupplies" => SetPlayerResupplyPoints(game, tokens, targetEndpoint, issuedBy),
            "tf2_addresupplies" or "tf2_addresupply" or "addresupplies" => AddPlayerResupplyPoints(game, tokens, targetEndpoint, issuedBy),
            "tf2_resetstats" or "resetstats" or "sm_resetstats" => ResetPlayerStats(game, tokens, targetEndpoint, issuedBy),
            "tf2_setteamscore" or "setteamscore" => SetTeamScore(game, tokens, issuedBy),
            "tf2_addteamscore" or "addteamscore" => AddTeamScore(game, tokens, issuedBy),
            "tf2_setteamcaptures" or "setteamcaptures" => SetTeamCaptures(game, tokens, issuedBy),
            "tf2_listflags" or "listflags" or "sm_flags" => ListFlags(game, issuedBy),
            "tf2_takeflag" or "takeflag" or "sm_takeflag" => TakeFlag(game, tokens, targetEndpoint, issuedBy),
            "tf2_dropflag" or "dropflag" or "sm_dropflag" => DropFlag(game, tokens, targetEndpoint, issuedBy),
            "tf2_returnflag" or "returnflag" or "sm_returnflag" => ReturnFlag(game, tokens, issuedBy),
            "tf2_captureflag" or "captureflag" or "sm_captureflag" or "sm_capflag" => CaptureFlag(game, tokens, targetEndpoint, issuedBy),
            "tf2_listpoints" or "listpoints" or "sm_points" => ListControlPoints(game, issuedBy),
            "tf2_setpointowner" or "setpointowner" or "sm_setpoint" => SetControlPointOwner(game, tokens, issuedBy),
            "tf2_capturepoint" or "capturepoint" or "sm_capturepoint" or "sm_cap" => CaptureControlPoint(game, tokens, issuedBy),
            "tf2_roundstate" or "roundstate" or "sm_roundstate" => SetRoundState(game, tokens, issuedBy),
            "pause" or "setpause" or "tf2_pause_timer" => SetTimerPaused(game, paused: true, issuedBy),
            "unpause" or "tf2_resume_timer" => SetTimerPaused(game, paused: false, issuedBy),
            "tf2_settime" or "settime" => SetTimeRemaining(game, tokens, issuedBy),
            "tf2_winround" or "tf2_forcewin" or "mp_forcewin" => ForceRoundWin(game, tokens, issuedBy),
            "mp_switchteams" or "tf2_switchteams" or "switchteams" => SwitchTeams(game, issuedBy),
            "mp_scrambleteams" or "tf2_scrambleteams" or "scrambleteams" => ScrambleTeams(game, issuedBy),
            "mp_balanceteams" or "tf2_balanceteams" or "balanceteams" or "sm_balance" => BalanceTeams(game, issuedBy),
            "restart" or "mp_restartgame" => RestartRound(game, issuedBy),
            "sm_cvar" => SourceModCvar(game, tokens, targetEndpoint, issuedBy),
            "sm_rcon" => SourceModRcon(game, tokens, targetEndpoint, issuedBy),
            _ when IsKnownSourceCvar(command) => SetOrGetSourceCvar(game, tokens, command, issuedBy),
            _ => QueueRawCommand(game, commandText, targetEndpoint, issuedBy, $"queued passthrough command: {commandText.Trim()}")
        };
    }

    private static SourceServerCommandResult QueueChat(
        GameManagerSession game,
        string message,
        string? targetEndpoint,
        string issuedBy)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SourceServerCommandResult(false, "say requires a message", []);
        }

        var queued = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            message,
            targetEndpoint,
            issuedBy);
        return new SourceServerCommandResult(true, $"queued chat: {message}", [queued]);
    }

    private static SourceServerCommandResult QueueMessage(
        GameManagerSession game,
        SourceServerCommandType type,
        string message,
        string? targetEndpoint,
        string issuedBy,
        string commandName)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SourceServerCommandResult(false, $"{commandName} requires a message", []);
        }

        var queued = game.QueueSourceServerCommand(
            type,
            message,
            targetEndpoint,
            issuedBy);
        return new SourceServerCommandResult(true, $"queued {commandName}: {message}", [queued]);
    }

    private static SourceServerCommandResult QueuePrivateMessage(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            return QueueMessage(game, SourceServerCommandType.PrivateChat, JoinTail(tokens, 1), targetEndpoint, issuedBy, tokens[0]);
        }

        if (tokens.Length < 3)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and message", []);
        }

        var player = FindPlayer(game, tokens[1]);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{tokens[1]}'", []);
        }

        return QueueMessage(game, SourceServerCommandType.PrivateChat, JoinTail(tokens, 2), player.Endpoint, issuedBy, tokens[0]);
    }

    private static SourceServerCommandResult Announce(
        GameManagerSession game,
        string message,
        string? targetEndpoint,
        string issuedBy)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SourceServerCommandResult(false, "announce requires a message", []);
        }

        var text = message.Trim();
        var chat = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"[ADMIN] {text}",
            targetEndpoint,
            issuedBy);
        var hint = game.QueueSourceServerCommand(
            SourceServerCommandType.HintMessage,
            text,
            targetEndpoint,
            issuedBy);
        var hud = game.QueueSourceServerCommand(
            SourceServerCommandType.HudMessage,
            text,
            targetEndpoint,
            issuedBy);
        return new SourceServerCommandResult(true, $"announced: {text}", [chat, hint, hud]);
    }

    private static SourceServerCommandResult Alert(
        GameManagerSession game,
        string message,
        string? targetEndpoint,
        string issuedBy)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SourceServerCommandResult(false, "alert requires a message", []);
        }

        var text = message.Trim();
        var center = game.QueueSourceServerCommand(
            SourceServerCommandType.CenterMessage,
            text,
            targetEndpoint,
            issuedBy);
        var chat = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"[ALERT] {text}",
            targetEndpoint,
            issuedBy);
        var sound = game.QueueSourceServerCommand(
            SourceServerCommandType.Sound,
            "ui/buttonclick.wav",
            targetEndpoint,
            issuedBy);
        return new SourceServerCommandResult(true, $"alerted: {text}", [center, chat, sound]);
    }

    private static SourceServerCommandResult ClearChat(GameManagerSession game, string issuedBy)
    {
        var clear = game.QueueSourceServerCommand(
            SourceServerCommandType.ClearChat,
            "clear",
            targetEndpoint: null,
            issuedBy);
        var notice = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"chat cleared by {issuedBy}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, "chat cleared", [clear, notice]);
    }

    private static SourceServerCommandResult PlaySound(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        string? target = null;
        string sound;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            sound = JoinTail(tokens, 1);
            target = targetEndpoint;
        }
        else if (tokens.Length >= 3 && FindPlayer(game, tokens[1]) is { } player)
        {
            sound = JoinTail(tokens, 2);
            target = player.Endpoint;
        }
        else
        {
            sound = JoinTail(tokens, 1);
        }

        if (string.IsNullOrWhiteSpace(sound))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a sound path", []);
        }

        var text = target is null
            ? sound
            : $"{sound} target={FindPlayer(game, target)?.Name ?? target}";
        var queued = game.QueueSourceServerCommand(
            SourceServerCommandType.Sound,
            text,
            target,
            issuedBy);
        return new SourceServerCommandResult(true, $"queued sound: {sound}", [queued]);
    }

    private static SourceServerCommandResult StartVote(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires an issue or question", []);
        }

        if (string.Equals(tokens[0], "callvote", StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3)
        {
            if (tokens[1].Equals("kick", StringComparison.OrdinalIgnoreCase))
            {
                return StartKickVote(game, ["votekick", .. tokens.Skip(2)], targetEndpoint: null, issuedBy);
            }

            if (tokens[1].Equals("map", StringComparison.OrdinalIgnoreCase)
                || tokens[1].Equals("changelevel", StringComparison.OrdinalIgnoreCase)
                || tokens[1].Equals("nextlevel", StringComparison.OrdinalIgnoreCase))
            {
                return StartMapVote(game, ["votemap", .. tokens.Skip(2)], issuedBy);
            }
        }

        return BeginVote(game, "custom", JoinTail(tokens, 1), target: null, issuedBy);
    }

    private static SourceServerCommandResult StartMapVote(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a map name", []);
        }

        var mapName = tokens[1].Trim();
        return BeginVote(game, "map", $"Change map to {mapName}?", mapName, issuedBy);
    }

    private static SourceServerCommandResult StartKickVote(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var player = FindPlayer(game, selector);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        return BeginVote(game, "kick", $"Kick {player.Name}?", player.Endpoint, issuedBy);
    }

    private static SourceServerCommandResult BeginVote(
        GameManagerSession game,
        string issue,
        string question,
        string? target,
        string issuedBy)
    {
        question = question.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(question))
        {
            return new SourceServerCommandResult(false, "vote requires a non-empty question", []);
        }

        if (game.CurrentSourceVote is not null)
        {
            return new SourceServerCommandResult(false, $"vote already running: {game.CurrentSourceVote.Question}", []);
        }

        var vote = new SourceServerVote(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DateTimeOffset.UtcNow,
            issue,
            question.Length > 160 ? question[..160] : question,
            target,
            issuedBy);
        game.CurrentSourceVote = vote;
        var voteEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.Vote,
            $"VoteStart issue={vote.Issue} question=\"{EscapeVoteText(vote.Question)}\" target=\"{EscapeVoteText(vote.Target ?? "")}\" by={issuedBy}",
            targetEndpoint: null,
            issuedBy);
        var center = game.QueueSourceServerCommand(
            SourceServerCommandType.CenterMessage,
            $"Vote: {vote.Question}",
            targetEndpoint: null,
            issuedBy);
        var chat = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"vote started by {issuedBy}: {vote.Question}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"vote started: {vote.Question}", [voteEvent, center, chat]);
    }

    private static SourceServerCommandResult CastVote(
        GameManagerSession game,
        string? targetEndpoint,
        string issuedBy,
        bool yes)
    {
        var vote = game.CurrentSourceVote;
        if (vote is null)
        {
            return new SourceServerCommandResult(false, "no vote is running", []);
        }

        var voter = !string.IsNullOrWhiteSpace(targetEndpoint)
            ? FindPlayer(game, targetEndpoint)
            : FindAdminPlayer(game, issuedBy);
        var voterKey = voter?.Endpoint ?? issuedBy;
        var voterName = voter?.Name ?? issuedBy;
        vote.Ballots[voterKey] = yes;
        var feedbackText = $"{voterName} voted {(yes ? "YES" : "NO")} ({vote.YesVotes}/{vote.NoVotes})";
        var voteEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.Vote,
            $"VoteCast issue={vote.Issue} voter=\"{EscapeVoteText(voterName)}\" choice={(yes ? "yes" : "no")} yes={vote.YesVotes} no={vote.NoVotes}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [voteEvent, feedback]);
    }

    private static SourceServerCommandResult CancelVote(GameManagerSession game, string issuedBy)
    {
        var vote = game.CurrentSourceVote;
        if (vote is null)
        {
            return new SourceServerCommandResult(false, "no vote is running", []);
        }

        game.CurrentSourceVote = null;
        var voteEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.Vote,
            $"VoteCancelled issue={vote.Issue} question=\"{EscapeVoteText(vote.Question)}\" by={issuedBy}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"vote cancelled: {vote.Question}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"vote cancelled: {vote.Question}", [voteEvent, feedback]);
    }

    private static SourceServerCommandResult CompleteVote(GameManagerSession game, string issuedBy, bool passed)
    {
        var vote = game.CurrentSourceVote;
        if (vote is null)
        {
            return new SourceServerCommandResult(false, "no vote is running", []);
        }

        game.CurrentSourceVote = null;
        var appliedAction = "";
        if (passed)
        {
            appliedAction = ApplyPassedVote(game, vote, issuedBy);
        }

        var resultText = passed ? "passed" : "failed";
        var suffix = string.IsNullOrWhiteSpace(appliedAction) ? "" : $"; {appliedAction}";
        var voteEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.Vote,
            $"Vote{(passed ? "Passed" : "Failed")} issue={vote.Issue} yes={vote.YesVotes} no={vote.NoVotes} question=\"{EscapeVoteText(vote.Question)}\" by={issuedBy}{suffix}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"vote {resultText}: {vote.Question}{suffix}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"vote {resultText}: {vote.Question}{suffix}", [voteEvent, feedback]);
    }

    private static string ApplyPassedVote(GameManagerSession game, SourceServerVote vote, string issuedBy)
    {
        if (vote.Issue.Equals("map", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(vote.Target))
        {
            game.ApplyNativeSourceMapSettings(mapName: vote.Target, resetPlayerSpawns: true);
            return $"changed level to {game.MapName}";
        }

        if (vote.Issue.Equals("kick", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(vote.Target)
            && FindPlayer(game, vote.Target) is { } player)
        {
            game.MarkNativeSourcePlayerDisconnected(player, $"vote kick by {issuedBy}");
            return $"kicked {player.Name}";
        }

        return "";
    }

    private static SourceServerCommandResult Echo(GameManagerSession game, string message, string issuedBy)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new SourceServerCommandResult(false, "echo requires a message", []);
        }

        var queued = game.QueueSourceServerCommand(
            SourceServerCommandType.ConsoleCommand,
            $"echo {message}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, message, [queued]);
    }

    private static SourceServerCommandResult SetHostname(GameManagerSession game, string hostname, string issuedBy)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return new SourceServerCommandResult(false, "hostname requires a value", []);
        }

        game.Name = hostname.Trim();
        game.SetSourceCvar("hostname", game.Name);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"hostname changed to {game.Name}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"hostname changed to {game.Name}", [feedback]);
    }

    private static SourceServerCommandResult ChangeLevel(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a map name", []);
        }

        game.ApplyNativeSourceMapSettings(mapName: tokens[1], resetPlayerSpawns: true);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"changing level to {game.MapName}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"changed level to {game.MapName}", [feedback]);
    }

    private static SourceServerCommandResult SetMaxPlayers(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (!TryReadInt(tokens, out var value))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a number", []);
        }

        game.MaxPlayers = Math.Clamp(value, 1, 32);
        game.SetSourceCvar("maxplayers", game.MaxPlayers.ToString(CultureInfo.InvariantCulture));
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"maxplayers set to {game.MaxPlayers}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"maxplayers set to {game.MaxPlayers}", [feedback]);
    }

    private static SourceServerCommandResult SetIntRule(GameManagerSession game, string[] tokens, string issuedBy, string rule)
    {
        if (!TryReadInt(tokens, out var value))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a number", []);
        }

        switch (rule)
        {
            case "mp_timelimit":
                game.ApplyNativeSourceMapSettings(timeLimitMinutes: value);
                break;
            case "mp_winlimit":
            case "mp_maxrounds":
                game.ApplyNativeSourceMapSettings(maxRounds: value);
                break;
            case "tf_flag_caps_per_round":
                game.ApplyNativeSourceMapSettings(flagCaptureLimit: value);
                break;
            case "tf_ctf_bonus_time":
                game.SetSourceCvar(rule, value.ToString(CultureInfo.InvariantCulture));
                break;
        }

        game.SetSourceCvar(rule, value.ToString(CultureInfo.InvariantCulture));
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"{rule} set to {value}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"{rule} set to {value}", [feedback]);
    }

    private static SourceServerCommandResult SetBoolRule(GameManagerSession game, string[] tokens, string issuedBy, string rule)
    {
        if (!TryReadBool(tokens, out var value))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires 0/1, true/false, yes/no, or on/off", []);
        }

        switch (rule)
        {
            case "mp_autoteambalance":
                game.ApplyNativeSourceMapSettings(autoBalance: value);
                break;
            case "mp_teams_unbalance_limit":
                game.ApplyNativeSourceMapSettings(autoBalance: value);
                break;
        }

        game.SetSourceCvar(rule, value ? "1" : "0");
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"{rule} set to {(value ? 1 : 0)}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"{rule} set to {(value ? 1 : 0)}", [feedback]);
    }

    private static SourceServerCommandResult SetTournamentBoolRule(GameManagerSession game, string[] tokens, string issuedBy, string rule)
    {
        if (!TryReadBool(tokens, out var value))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires 0/1, true/false, yes/no, or on/off", []);
        }

        if (rule.Equals("mp_tournament", StringComparison.OrdinalIgnoreCase))
        {
            game.World.SetTournamentMode(value);
            game.SetSourceCvar("mp_tournament", value ? "1" : "0");
            if (!value)
            {
                game.SetSourceCvar("mp_tournament_readymode", "0");
                foreach (var player in game.ActivePlayers())
                {
                    player.TournamentReady = false;
                }
            }
        }
        else
        {
            game.World.SetTournamentMode(value || game.World.TournamentMode);
            game.World.SetTournamentReadymode(value);
            game.SetSourceCvar("mp_tournament", game.World.TournamentMode ? "1" : "0");
            game.SetSourceCvar("mp_tournament_readymode", value ? "1" : "0");
            foreach (var player in game.ActivePlayers())
            {
                player.TournamentReady = false;
            }
        }

        var feedbackText = $"{rule} set to {(value ? 1 : 0)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"ReadyMode tournament={game.World.TournamentMode} readymode={game.World.TournamentReadymode} awaiting={game.World.AwaitingReadyRestart}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerReady(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool ready)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        if (!game.World.TournamentMode)
        {
            game.World.SetTournamentMode(true);
            game.SetSourceCvar("mp_tournament", "1");
        }

        if (!game.World.TournamentReadymode)
        {
            game.World.SetTournamentReadymode(true);
            game.SetSourceCvar("mp_tournament_readymode", "1");
        }

        player.TournamentReady = ready;
        var readyPlayers = TournamentReadyPlayers(game).ToArray();
        var readyCount = readyPlayers.Count(static player => player.TournamentReady);
        var totalCount = readyPlayers.Length;
        var feedbackText = string.Create(
            CultureInfo.InvariantCulture,
            $"{player.Name} is {(ready ? "ready" : "not ready")} ({readyCount}/{totalCount})");
        var queued = new List<PendingSourceServerCommand>
        {
            game.QueueSourceServerCommand(
                SourceServerCommandType.Chat,
                feedbackText,
                targetEndpoint: null,
                issuedBy),
            game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"ReadyState player={player.Name} ready={player.TournamentReady} readyCount={readyCount} total={totalCount} awaiting={game.World.AwaitingReadyRestart}",
                targetEndpoint: null,
                issuedBy)
        };

        if (totalCount > 0 && readyCount == totalCount && game.World.AwaitingReadyRestart)
        {
            foreach (var readyPlayer in readyPlayers)
            {
                readyPlayer.TournamentReady = false;
            }

            game.World.RestartRound();
            game.ApplyNativeSourceMapSettings(resetPlayerSpawns: true);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"TournamentRestart readyCount={readyCount} total={totalCount}",
                targetEndpoint: null,
                issuedBy));
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.Chat,
                "all players ready; round restarted",
                targetEndpoint: null,
                issuedBy));
        }

        return new SourceServerCommandResult(true, feedbackText, queued.ToArray());
    }

    private static SourceServerCommandResult ReadyList(GameManagerSession game, string issuedBy)
    {
        var players = TournamentReadyPlayers(game).ToArray();
        var feedbackText = players.Length == 0
            ? "readylist: no active human players"
            : $"readylist ({players.Count(static player => player.TournamentReady)}/{players.Length}): {string.Join(", ", players.Select(static player => $"{player.Name}={(player.TournamentReady ? "ready" : "not ready")}"))}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult TournamentRestart(GameManagerSession game, string issuedBy)
    {
        foreach (var player in game.ActivePlayers())
        {
            player.TournamentReady = false;
        }

        game.World.SetTournamentMode(true);
        game.World.SetTournamentReadymode(true);
        game.SetSourceCvar("mp_tournament", "1");
        game.SetSourceCvar("mp_tournament_readymode", "1");
        game.SetSourceCvar("mp_tournament_restart", "1");
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            "tournament restart waiting for ready",
            targetEndpoint: null,
            issuedBy);
        game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            "TournamentRestartWaitingForReady",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, "tournament restart waiting for ready", [feedback]);
    }

    private static SourceServerCommandResult Status(GameManagerSession game, string issuedBy)
    {
        var activePlayers = game.ActivePlayers().ToArray();
        var playerText = activePlayers.Length == 0
            ? "no players"
            : string.Join(", ", activePlayers.Select(static player =>
                $"#{player.PlayerId} {player.Name} {player.Endpoint} {TeamName(player.SourceState.TeamNumber)}/{ClassName(player.SourceState.ClassNumber)}"));
        var feedbackText = string.Create(
            CultureInfo.InvariantCulture,
            $"{game.Name} | {game.MapName} | players {activePlayers.Length}/{game.MaxPlayers} | mode {game.GameMode} | ranked {game.RankingMode} | bans {game.SourceServerBans.Count} | {playerText}");
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult Users(GameManagerSession game, string issuedBy)
    {
        var players = game.ActivePlayers()
            .OrderBy(static player => player.PlayerId)
            .Select(static player => $"#{player.PlayerId} \"{player.Name}\" {player.Endpoint} {player.State} {TeamName(player.SourceState.TeamNumber)} {ClassName(player.SourceState.ClassNumber)}")
            .ToArray();
        var feedbackText = players.Length == 0
            ? "users: no connected players"
            : $"users ({players.Length}): {string.Join("; ", players)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult TimeLeft(GameManagerSession game, string issuedBy)
    {
        var remaining = TimeSpan.FromSeconds(Math.Max(0, game.World.Timer.TimeRemainingSeconds));
        var feedbackText = game.TimeLimitMinutes <= 0
            ? "No time limit"
            : string.Create(CultureInfo.InvariantCulture, $"Time left: {(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}");
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult NextMap(GameManagerSession game, string issuedBy)
    {
        var feedbackText = $"Next map: {game.NextMapName}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult MapList(GameManagerSession game, string issuedBy)
    {
        var feedbackText = $"maps ({game.AvailableMaps.Count}): {string.Join(", ", game.AvailableMaps)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetNextMap(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a map name", []);
        }

        game.SetNextMap(tokens[1]);
        var feedbackText = $"next map set to {game.NextMapName}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult Help(GameManagerSession game, string issuedBy)
    {
        var feedbackText = "commands: status/sm_who, sm_scoreboard, users, ready/unready, readylist, mp_tournament, mp_tournament_readymode, mp_tournament_restart, timeleft/sm_timeleft, nextmap/sm_nextmap, maps/sm_maps, setnextmap/sm_setnextmap, say/sm_say, say_team, tell/psay/sm_psay, csay/sm_csay, sm_hsay/sm_hint, sm_msay/sm_panel, sm_tsay/sm_hud, sm_play, sm_vote/callvote, sm_votemap, sm_votekick, sm_voteyes/sm_voteno, sm_passvote/sm_failvote/sm_cancelvote, sm_beacon, sm_slap, sm_damage/sm_hurt, sm_heal, sm_overheal, sm_setmaxhealth, sm_burn, sm_extinguish, sm_bleed, sm_stun, sm_taunt, sm_god/sm_ungod, sm_noclip/sm_clip, echo, log on/off/status, logaddress_add/list/del, logpath, logclear, exec/sm_execcfg, sm_cvar, sm_rcon, hostname, map/changelevel/sm_map, maxplayers, kick/sm_kick, banid/sm_ban, removeid/sm_unban, listid, mute/unmute, gag/ungag, silence/unsilence, listmutes, tf_bot_add, tf_bot_kick, tf_bot_quota, tf2_rename/sm_rename, tf2_respawn/sm_respawn, tf2_kill/sm_slay, tf2_setteam/sm_team, sm_spec, sm_swap, tf2_setclass, tf2_sethealth, tf2_getpos, tf2_setpos, sm_bring, sm_goto, sm_freeze, sm_unfreeze, sm_speed, sm_gravity/sm_resetgravity, sm_fov, sm_viewmodel/sm_hideweapon/sm_showweapon, sm_blind/sm_unblind, sm_shake, sm_color/sm_alpha, sm_rendermode/sm_renderfx, sm_effects/sm_addeffect/sm_removeeffect, sm_resetrender, sm_modelindex, sm_sequence/sm_anim, sm_skinbody, sm_modelscale, sm_textureframe, sm_forcevector, sm_animflags, sm_lightingorigin, sm_fade, sm_muzzleflash, sm_resetanim, sm_refill, sm_setammo, sm_sentry, sm_teleporter, sm_destroybuildings, sm_addcond, sm_removecond, sm_disguise, sm_cloak, tf2_setscore/addscore, tf2_setcaptures/addcaptures, tf2_setdeaths/adddeaths, tf2_setassists/addassists, tf2_setdefenses/adddefenses, tf2_setdominations/adddominations, tf2_setrevenge/addrevenge, tf2_setdestruction/adddestruction, tf2_setheadshots/addheadshots, tf2_setbackstabs/addbackstabs, tf2_sethealing/addhealing, tf2_setinvulns/addinvulns, tf2_setteleports/addteleports, tf2_setresupplies/addresupplies, sm_resetstats, tf2_setteamscore, tf2_setteamcaptures, sm_flags, sm_takeflag, sm_dropflag, sm_returnflag, sm_captureflag, sm_points, sm_setpoint, sm_capturepoint, sm_roundstate, mp_switchteams, mp_scrambleteams, sm_balance, tf2_settime, tf2_winround, pause/unpause, cvarlist";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult LogCommand(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.Equals(tokens[1], "status", StringComparison.OrdinalIgnoreCase))
        {
            var status = game.SourceLoggingEnabled ? "on" : "off";
            var error = string.IsNullOrWhiteSpace(game.SourceLogLastError) ? "" : $" error=\"{game.SourceLogLastError}\"";
            var feedbackText = $"log is {status}; path=\"{game.SourceLogPath}\"; logaddresses={game.SourceLogAddresses.Count}{error}";
            var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
            return new SourceServerCommandResult(true, feedbackText, [feedback]);
        }

        if (TryParseBoolValue(tokens[1], out var enabled)
            || string.Equals(tokens[1], "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tokens[1], "off", StringComparison.OrdinalIgnoreCase))
        {
            enabled = tokens[1].Equals("on", StringComparison.OrdinalIgnoreCase) || enabled;
            game.SetSourceLogging(enabled);
            var feedbackText = enabled
                ? $"log enabled: {game.SourceLogPath}"
                : "log disabled";
            var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
            return new SourceServerCommandResult(true, feedbackText, [feedback]);
        }

        return new SourceServerCommandResult(false, "log requires on/off/status", []);
    }

    private static SourceServerCommandResult LogAddressAdd(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2)
        {
            return new SourceServerCommandResult(false, "logaddress_add requires host:port", []);
        }

        var address = JoinTail(tokens, 1);
        var added = game.AddSourceLogAddress(address);
        var feedbackText = added ? $"logaddress added {address}" : $"logaddress already exists {address}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult LogAddressDelete(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (string.Equals(tokens[0], "logaddress_delall", StringComparison.OrdinalIgnoreCase))
        {
            var count = game.SourceLogAddresses.Count;
            game.SourceLogAddresses.Clear();
            var clearText = $"removed {count} logaddress target(s)";
            var clearFeedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, clearText, targetEndpoint: null, issuedBy);
            return new SourceServerCommandResult(true, clearText, [clearFeedback]);
        }

        if (tokens.Length < 2)
        {
            return new SourceServerCommandResult(false, "logaddress_del requires host:port", []);
        }

        var address = JoinTail(tokens, 1);
        var removed = game.RemoveSourceLogAddress(address);
        var feedbackText = removed ? $"logaddress removed {address}" : $"logaddress not found {address}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult LogAddressList(GameManagerSession game, string issuedBy)
    {
        var feedbackText = game.SourceLogAddresses.Count == 0
            ? "logaddress list is empty"
            : $"logaddresses ({game.SourceLogAddresses.Count}): {string.Join(", ", game.SourceLogAddresses)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetLogPath(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2)
        {
            var queryText = $"log path: {game.SourceLogPath}";
            var query = game.QueueSourceServerCommand(SourceServerCommandType.Chat, queryText, targetEndpoint: null, issuedBy);
            return new SourceServerCommandResult(true, queryText, [query]);
        }

        game.SetSourceLogPath(JoinTail(tokens, 1));
        var feedbackText = $"log path set to {game.SourceLogPath}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ClearLog(GameManagerSession game, string issuedBy)
    {
        game.ClearSourceLog();
        var feedbackText = string.IsNullOrWhiteSpace(game.SourceLogLastError)
            ? $"log cleared: {game.SourceLogPath}"
            : $"log clear failed: {game.SourceLogLastError}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(string.IsNullOrWhiteSpace(game.SourceLogLastError), feedbackText, [feedback]);
    }

    private static SourceServerCommandResult CvarList(GameManagerSession game, string issuedBy)
    {
        var names = game.SourceCvars.Keys
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var feedbackText = $"cvars ({names.Length}): {string.Join(", ", names)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ExecConfig(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, "exec requires a cfg file name", []);
        }

        if (!TryResolveConfigPath(game, tokens[1], out var configPath))
        {
            return new SourceServerCommandResult(false, $"cfg not found: {tokens[1]}", []);
        }

        var commands = ReadConfigCommands(configPath).Take(MaxExecCommands + 1).ToArray();
        if (commands.Length > MaxExecCommands)
        {
            return new SourceServerCommandResult(false, $"cfg {tokens[1]} has more than {MaxExecCommands} commands", []);
        }

        var queued = new List<PendingSourceServerCommand>();
        var applied = 0;
        var failures = new List<string>();
        foreach (var command in commands)
        {
            var commandTokens = Tokenize(command);
            if (commandTokens.Length == 0)
            {
                continue;
            }

            if (string.Equals(commandTokens[0], "exec", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("nested exec skipped");
                continue;
            }

            var result = Execute(game, command, targetEndpoint: null, issuedBy);
            if (result.Applied)
            {
                applied++;
                queued.AddRange(result.QueuedCommands);
            }
            else
            {
                failures.Add($"{command}: {result.Feedback}");
            }
        }

        var feedbackText = failures.Count == 0
            ? $"executed {applied} command(s) from {Path.GetFileName(configPath)}"
            : $"executed {applied} command(s) from {Path.GetFileName(configPath)}; {failures.Count} skipped/failed";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, queued.ToArray());
    }

    private static SourceServerCommandResult SourceModExecConfig(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, "sm_execcfg requires a cfg file name", []);
        }

        return ExecConfig(game, ["exec", tokens[1]], issuedBy);
    }

    private static SourceServerCommandResult SourceModCvar(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, "sm_cvar requires a cvar name", []);
        }

        return Execute(game, JoinTail(tokens, 1), targetEndpoint, issuedBy);
    }

    private static SourceServerCommandResult SourceModRcon(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (tokens.Length < 2 || string.IsNullOrWhiteSpace(tokens[1]))
        {
            return new SourceServerCommandResult(false, "sm_rcon requires a command", []);
        }

        if (string.Equals(tokens[1], "sm_rcon", StringComparison.OrdinalIgnoreCase))
        {
            return new SourceServerCommandResult(false, "nested sm_rcon is not allowed", []);
        }

        return Execute(game, JoinTail(tokens, 1), targetEndpoint, issuedBy);
    }

    private static SourceServerCommandResult SourceModBan(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? tokens[1] : targetEndpoint ?? "";
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player, id, endpoint, or IP", []);
        }

        var durationText = "0";
        var reasonStartIndex = 2;
        if (tokens.Length >= 3
            && int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration))
        {
            durationText = Math.Max(0, parsedDuration).ToString(CultureInfo.InvariantCulture);
            reasonStartIndex = 3;
        }

        var reason = tokens.Length > reasonStartIndex
            ? JoinTail(tokens, reasonStartIndex)
            : "Banned by server admin";
        return Ban(game, ["banid", durationText, selector, reason], targetEndpoint, issuedBy);
    }

    private static SourceServerCommandResult SetOrGetSourceCvar(
        GameManagerSession game,
        string[] tokens,
        string command,
        string issuedBy)
    {
        if (tokens.Length < 2)
        {
            var value = game.TryGetSourceCvar(command, out var existing) ? existing : "";
            var queryFeedback = $"{command} = \"{value}\"";
            var queryCommand = game.QueueSourceServerCommand(
                SourceServerCommandType.Chat,
                queryFeedback,
                targetEndpoint: null,
                issuedBy);
            return new SourceServerCommandResult(true, queryFeedback, [queryCommand]);
        }

        var valueText = JoinTail(tokens, 1);
        if (IsBooleanCvar(command) && !TryParseBoolValue(valueText, out _))
        {
            return new SourceServerCommandResult(false, $"{command} requires 0/1, true/false, yes/no, or on/off", []);
        }

        if (IsIntegerCvar(command)
            && !int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return new SourceServerCommandResult(false, $"{command} requires a number", []);
        }

        var normalizedValue = IsBooleanCvar(command)
            ? (TryParseBoolValue(valueText, out var boolValue) && boolValue ? "1" : "0")
            : valueText.Trim();
        var normalizedName = game.SetSourceCvar(command, normalizedValue);
        var feedbackText = $"{normalizedName} set to \"{normalizedValue}\"";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult Kick(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        foreach (var player in players)
        {
            game.MarkNativeSourcePlayerDisconnected(player, $"kicked by {issuedBy}");
        }

        var targetText = players.Length == 1
            ? players[0].Name
            : $"{players.Length} players";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            $"{targetText} kicked by {issuedBy}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"kicked {targetText}", [feedback]);
    }

    private static SourceServerCommandResult Ban(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var durationMinutes = 0;
        var selectorIndex = 1;
        if (tokens.Length >= 3 && int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDuration))
        {
            durationMinutes = Math.Max(0, parsedDuration);
            selectorIndex = 2;
        }

        var selector = tokens.Length > selectorIndex
            ? tokens[selectorIndex]
            : targetEndpoint ?? "";
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player, id, endpoint, or IP", []);
        }

        var reason = tokens.Length > selectorIndex + 1
            ? JoinTail(tokens, selectorIndex + 1)
            : "Banned by server admin";
        var player = FindPlayer(game, selector);
        var ban = game.AddSourceBan(selector, player, durationMinutes, reason, issuedBy);
        if (player is not null)
        {
            game.MarkNativeSourcePlayerDisconnected(player, $"banned by {issuedBy}: {reason}");
        }

        var durationText = ban.DurationMinutes is { } minutes
            ? $"{minutes} minute(s)"
            : "permanent";
        var displayName = ban.PlayerName ?? ban.Selector;
        var feedbackText = $"banned {displayName} ({durationText}): {ban.Reason}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult RemoveBan(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a ban id, player id, endpoint, name, or selector", []);
        }

        var selector = JoinTail(tokens, 1);
        if (!game.RemoveSourceBan(selector, out var removed))
        {
            return new SourceServerCommandResult(false, $"no ban matches '{selector}'", []);
        }

        var feedbackText = $"removed ban {removed.Id} ({removed.PlayerName ?? removed.Selector})";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ListBans(GameManagerSession game, string issuedBy)
    {
        game.ExpireSourceBans(DateTimeOffset.UtcNow);
        var bans = game.SourceServerBans
            .OrderBy(static ban => ban.Id)
            .Select(static ban =>
            {
                var duration = ban.ExpiresAt is { } expiresAt
                    ? $"expires {expiresAt:O}"
                    : "permanent";
                return $"#{ban.Id} {ban.PlayerName ?? ban.Selector} {duration} by {ban.IssuedBy}";
            })
            .ToArray();
        var feedbackText = bans.Length == 0
            ? "ban list is empty"
            : $"bans ({bans.Length}): {string.Join("; ", bans)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerComms(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool? voiceMuted,
        bool? chatGagged,
        string actionText)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        foreach (var player in players)
        {
            if (voiceMuted is { } muted)
            {
                player.VoiceMuted = muted;
            }

            if (chatGagged is { } gagged)
            {
                player.ChatGagged = gagged;
            }
        }

        var targetText = players.Length == 1
            ? players[0].Name
            : $"{players.Length} players";
        var stateText = players.Length == 1
            ? (players[0].VoiceMuted || players[0].ChatGagged
                ? $"voice={(players[0].VoiceMuted ? "muted" : "open")} chat={(players[0].ChatGagged ? "gagged" : "open")}"
                : "voice/chat open")
            : GroupCommsStateText(players);
        var feedbackText = $"{targetText} {actionText} by {issuedBy} ({stateText})";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ListComms(GameManagerSession game, string issuedBy)
    {
        var players = game.ActivePlayers()
            .Where(static player => player.VoiceMuted || player.ChatGagged)
            .OrderBy(static player => player.PlayerId)
            .Select(static player =>
                $"#{player.PlayerId} {player.Name} voice={(player.VoiceMuted ? "muted" : "open")} chat={(player.ChatGagged ? "gagged" : "open")}")
            .ToArray();
        var feedbackText = players.Length == 0
            ? "no muted or gagged players"
            : $"comms restrictions ({players.Length}): {string.Join("; ", players)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddBots(GameManagerSession game, string[] tokens, string issuedBy)
    {
        var count = 1;
        byte? classNumber = null;
        uint? teamNumber = null;
        string? name = null;
        var index = 1;

        if (tokens.Length > index
            && int.TryParse(tokens[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount))
        {
            count = Math.Clamp(parsedCount, 1, Math.Max(1, game.MaxPlayers));
            index++;
        }

        if (tokens.Length > index && TryParseClass(tokens[index], out var parsedClass))
        {
            classNumber = parsedClass;
            index++;
        }

        if (tokens.Length > index && TryParseTeam(tokens[index], out var parsedTeam))
        {
            teamNumber = parsedTeam is 2 or 3 ? parsedTeam : null;
            index++;
        }

        if (tokens.Length > index)
        {
            name = JoinTail(tokens, index);
        }

        var added = new List<PlayerSession>(count);
        for (var i = 0; i < count && game.SourceVisiblePlayerCount < game.MaxPlayers; i++)
        {
            added.Add(game.AddBot(name is null || count > 1 ? null : name, classNumber, teamNumber));
        }

        if (added.Count == 0)
        {
            return new SourceServerCommandResult(false, "server is full; no bots added", []);
        }

        var names = string.Join(", ", added.Select(static bot => bot.Name));
        var feedbackText = $"added {added.Count} bot(s): {names}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult KickBots(GameManagerSession game, string[] tokens, string issuedBy)
    {
        var selector = tokens.Length > 1 ? JoinTail(tokens, 1) : "all";
        var removed = game.RemoveBots(selector);
        if (removed == 0)
        {
            return new SourceServerCommandResult(false, $"no bots match '{selector}'", []);
        }

        var feedbackText = selector.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? $"kicked {removed} bot(s)"
            : $"kicked {removed} bot(s) matching {selector}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetBotQuota(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2)
        {
            var queryText = $"tf_bot_quota = \"{game.BotCount}\"";
            var queryFeedback = game.QueueSourceServerCommand(
                SourceServerCommandType.Chat,
                queryText,
                targetEndpoint: null,
                issuedBy);
            return new SourceServerCommandResult(true, queryText, [queryFeedback]);
        }

        if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quota))
        {
            return new SourceServerCommandResult(false, "tf_bot_quota requires a number", []);
        }

        var actualQuota = game.SetBotQuota(quota);
        var feedbackText = $"tf_bot_quota set to {actualQuota}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerTeam(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var players = Array.Empty<PlayerSession>();
        string teamText;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out teamText))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and team", []);
            }

            players = [player];
        }
        else
        {
            if (tokens.Length < 3)
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and team", []);
            }

            players = SelectPlayers(game, tokens[1]).ToArray();
            teamText = tokens[2];
            if (players.Length == 0)
            {
                return new SourceServerCommandResult(false, $"no player matches '{tokens[1]}'", []);
            }
        }

        if (!TryParseTeam(teamText, out var teamNumber))
        {
            return new SourceServerCommandResult(false, $"unknown team '{teamText}'", []);
        }

        foreach (var player in players)
        {
            player.SourceState.SetTeam(teamNumber);
        }

        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var targetText = players.Length == 1
            ? players[0].Name
            : $"{players.Length} players";
        var feedbackText = $"{targetText} moved to {TeamName(teamNumber)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult RenamePlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        PlayerSession? player;
        string newName;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            player = FindPlayer(game, targetEndpoint);
            newName = JoinTail(tokens, 1);
        }
        else
        {
            if (tokens.Length < 3)
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and new name", []);
            }

            player = FindPlayer(game, tokens[1]);
            newName = JoinTail(tokens, 2);
        }

        if (player is null)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a valid player", []);
        }

        newName = newName.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(newName))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a non-empty new name", []);
        }

        var oldName = player.Name;
        player.Name = newName.Length > 31 ? newName[..31] : newName;
        var eventText = $"PlayerNameChange player={oldName} id={player.PlayerId} newName={player.Name}";
        var renameEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            eventText,
            targetEndpoint: null,
            issuedBy);
        var feedbackText = $"{oldName} renamed to {player.Name}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [renameEvent, feedback]);
    }

    private static SourceServerCommandResult SpectatePlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var player = FindPlayer(game, selector);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        player.SourceState.SetTeam(1);
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} moved to Spectator";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SwapPlayerTeam(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var player = FindPlayer(game, selector);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        var newTeam = OpposingTeam(player.SourceState.TeamNumber)
            ?? LeastPopulatedPlayableTeam(game, excluding: player);
        player.SourceState.SetTeam(newTeam);
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} swapped to {TeamName(newTeam)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerClass(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupValue(game, tokens, targetEndpoint, out var players, out var classText))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and class", []);
        }

        if (!TryParseClass(classText, out var classNumber))
        {
            return new SourceServerCommandResult(false, $"unknown class '{classText}'", []);
        }

        foreach (var player in players)
        {
            player.SourceState.SetClass(classNumber);
        }

        var feedbackText = $"{TargetText(players)} changed class to {ClassName(classNumber)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerHealth(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupValue(game, tokens, targetEndpoint, out var players, out var healthText))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and health", []);
        }

        if (!ushort.TryParse(healthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var health))
        {
            return new SourceServerCommandResult(false, $"invalid health '{healthText}'", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.SetHealth(health);
            if (health == 0)
            {
                queued.Add(QueueDeathNotice(game, player, issuedBy, "world"));
            }
        }

        var feedbackText = health == 0
            ? $"{TargetText(players)} killed by {issuedBy}"
            : $"{TargetText(players)} health set to {health}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerMaxHealth(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var healthText)
            || !ushort.TryParse(healthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxHealth)
            || maxHealth == 0)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and positive max health", []);
        }

        player.SourceState.MaxHealth = maxHealth;
        if (player.SourceState.Health > maxHealth)
        {
            player.SourceState.Health = maxHealth;
        }

        var feedbackText = $"{player.Name} max health set to {maxHealth}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"MaxHealth player={player.Name} id={player.PlayerId} maxHealth={player.SourceState.MaxHealth} health={player.SourceState.Health}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult HealPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        ushort? explicitAmount = null;
        if (args.Length > 0)
        {
            if (!ushort.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAmount))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} amount must be an unsigned integer", []);
            }

            explicitAmount = parsedAmount;
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        var firstPrevious = players[0].SourceState.Health;
        uint firstAmount = explicitAmount ?? players[0].SourceState.MaxHealth;
        foreach (var player in players)
        {
            var amount = explicitAmount ?? player.SourceState.MaxHealth;
            var previousHealth = player.SourceState.Health;
            var healed = Math.Min(player.SourceState.MaxHealth, checked((uint)previousHealth + amount));
            player.SourceState.SetHealth(checked((ushort)healed));
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"Heal player={player.Name} id={player.PlayerId} amount={amount} health={player.SourceState.Health}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} healed by {firstAmount} ({firstPrevious}->{players[0].SourceState.Health})"
            : $"healed {players.Length} players by {(explicitAmount?.ToString(CultureInfo.InvariantCulture) ?? "max health")}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult OverhealPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        ushort overheal;
        if (args.Length == 0)
        {
            overheal = checked((ushort)Math.Min((uint)ushort.MaxValue, player.SourceState.MaxHealth + (player.SourceState.MaxHealth / 2U)));
        }
        else if (!ushort.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out overheal) || overheal == 0)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} health must be a positive unsigned integer", []);
        }

        player.SourceState.SetHealth(Math.Min(overheal, player.SourceState.MaxHealth));
        player.SourceState.Health = overheal;
        var feedbackText = $"{player.Name} overhealed to {player.SourceState.Health}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"Overheal player={player.Name} id={player.PlayerId} health={player.SourceState.Health} maxHealth={player.SourceState.MaxHealth}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult DamagePlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var players, out var args)
            || !ushort.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var damage))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and damage", []);
        }

        var firstPrevious = players[0].SourceState.Health;
        var queued = new List<PendingSourceServerCommand>((players.Length * 2) + 1);
        foreach (var player in players)
        {
            var previousHealth = player.SourceState.Health;
            if (damage > 0 && player.SourceState.Alive)
            {
                if (damage >= previousHealth)
                {
                    player.SourceState.ForceKill();
                }
                else
                {
                    player.SourceState.SetHealth((ushort)(previousHealth - damage));
                }
            }

            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"Damage player={player.Name} id={player.PlayerId} damage={damage} health={player.SourceState.Health} alive={player.SourceState.Alive}",
                targetEndpoint: null,
                issuedBy));
            if (previousHealth > 0 && !player.SourceState.Alive)
            {
                queued.Add(QueueDeathNotice(game, player, issuedBy, "damage"));
            }
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} damaged for {damage} ({firstPrevious}->{players[0].SourceState.Health})"
            : $"damaged {players.Length} players for {damage}";
        queued.Add(game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy));
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult KillPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.ForceKill();
            queued.Add(QueueDeathNotice(game, player, issuedBy, "world"));
        }

        var feedbackText = $"{TargetText(players)} killed by {issuedBy}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult RespawnPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.ForceRespawn();
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"PlayerSpawn player={player.Name} id={player.PlayerId} team={player.SourceState.TeamNumber} class={ClassName(player.SourceState.ClassNumber)} spawnCounter={player.SourceState.SpawnCounter}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} respawned"
            : $"{players.Length} players respawned";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult GetPlayerPosition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        var feedbackText = $"{player.Name} pos {FormatPosition(player.SourceState)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerPosition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedPosition(game, tokens, targetEndpoint, issuedBy, out var player, out var x, out var y, out var z, out var yaw, out var pitch))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and x y z [yaw] [pitch]", []);
        }

        player.SourceState.SetPosition(x, y, z, yaw, pitch);
        var feedbackText = $"{player.Name} moved to {FormatPosition(player.SourceState)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult TeleportPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        PlayerSession? player;
        PlayerSession? destination;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            player = FindPlayer(game, targetEndpoint);
            destination = tokens.Length >= 2 ? FindPlayer(game, JoinTail(tokens, 1)) : FindPlayer(game, issuedBy);
        }
        else
        {
            player = tokens.Length >= 2 ? FindPlayer(game, tokens[1]) : FindPlayer(game, issuedBy);
            destination = tokens.Length >= 3 ? FindPlayer(game, JoinTail(tokens, 2)) : null;
        }

        if (player is null)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        if (destination is null)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a destination player", []);
        }

        CopyPosition(player, destination);
        var feedbackText = $"{player.Name} teleported to {destination.Name} at {FormatPosition(player.SourceState)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult BringPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var player = FindPlayer(game, selector);
        var admin = FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        if (admin is null)
        {
            return new SourceServerCommandResult(false, $"no in-game admin player matches '{issuedBy}'", []);
        }

        CopyPosition(player, admin);
        var feedbackText = $"{player.Name} brought to {admin.Name} at {FormatPosition(player.SourceState)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult GotoPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var destination = FindPlayer(game, selector);
        var admin = FindAdminPlayer(game, issuedBy);
        if (destination is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        if (admin is null)
        {
            return new SourceServerCommandResult(false, $"no in-game admin player matches '{issuedBy}'", []);
        }

        CopyPosition(admin, destination);
        var feedbackText = $"{admin.Name} moved to {destination.Name} at {FormatPosition(admin.SourceState)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult BeaconPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var center = game.QueueSourceServerCommand(
            SourceServerCommandType.CenterMessage,
            $"Beacon: {player.Name}",
            player.Endpoint,
            issuedBy);
        var sound = game.QueueSourceServerCommand(
            SourceServerCommandType.Sound,
            $"buttons/blip1.wav target={player.Name}",
            player.Endpoint,
            issuedBy);
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"Beacon player={player.Name} id={player.PlayerId}",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, $"beaconed {player.Name}", [center, sound, gameplay]);
    }

    private static SourceServerCommandResult SlapPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var damage = 5;
        if (args.Length > 0
            && (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out damage) || damage < 0))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} damage must be a non-negative number", []);
        }

        var previousHealth = player.SourceState.Health;
        if (damage > 0 && player.SourceState.Alive)
        {
            if (damage >= previousHealth)
            {
                player.SourceState.ForceKill();
            }
            else
            {
                player.SourceState.SetHealth((ushort)(previousHealth - damage));
            }
        }

        var feedbackText = $"{player.Name} slapped for {damage} damage ({previousHealth}->{player.SourceState.Health})";
        var queued = new List<PendingSourceServerCommand>(3);
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"Slap player={player.Name} id={player.PlayerId} damage={damage} health={player.SourceState.Health} alive={player.SourceState.Alive}",
            targetEndpoint: null,
            issuedBy);
        queued.Add(gameplay);
        if (previousHealth > 0 && !player.SourceState.Alive)
        {
            queued.Add(QueueDeathNotice(game, player, issuedBy, "slap"));
        }

        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static PendingSourceServerCommand QueueDeathNotice(
        GameManagerSession game,
        PlayerSession victim,
        string attacker,
        string weapon)
    {
        return game.QueueSourceServerCommand(
            SourceServerCommandType.DeathNotice,
            $"victim={victim.Name} victimId={victim.PlayerId} attacker={attacker} weapon={weapon} team={victim.SourceState.TeamNumber} class={ClassName(victim.SourceState.ClassNumber)} deaths={victim.SourceState.Deaths}",
            targetEndpoint: null,
            attacker);
    }

    private static SourceServerCommandResult BurnPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var duration = 10;
        if (args.Length > 0
            && (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out duration) || duration < 0))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} duration must be a non-negative number", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.AddTimedPlayerCondition(BurningConditionMask, duration);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"Burn player={player.Name} id={player.PlayerId} duration={duration} conditionMask=0x{player.SourceState.PlayerCondition:x8}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} ignited for {duration} second(s)"
            : $"{TargetText(players)} ignited for {duration} second(s)";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult ExtinguishPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.RemovePlayerCondition(BurningConditionMask);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"Extinguish player={player.Name} id={player.PlayerId} conditionMask=0x{player.SourceState.PlayerCondition:x8}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} extinguished"
            : $"{TargetText(players)} extinguished";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult AddTimedPlayerCondition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        uint conditionMask,
        string conditionName)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var duration = 10;
        if (args.Length > 0
            && (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out duration) || duration < 0))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} duration must be a non-negative number", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.AddTimedPlayerCondition(conditionMask, duration);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"Condition player={player.Name} id={player.PlayerId} name={conditionName} duration={duration} conditionMask=0x{player.SourceState.PlayerCondition:x8}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} {conditionName} for {duration} second(s)"
            : $"{TargetText(players)} {conditionName} for {duration} second(s)";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerGodMode(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool enabled)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            if (enabled)
            {
                player.SourceState.AddPlayerCondition(InvulnerableConditionMask);
            }
            else
            {
                player.SourceState.RemovePlayerCondition(InvulnerableConditionMask);
            }

            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"GodMode player={player.Name} id={player.PlayerId} enabled={enabled} conditionMask=0x{player.SourceState.PlayerCondition:x8}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = enabled
            ? $"{TargetText(players)} god mode enabled"
            : $"{TargetText(players)} god mode disabled";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerFrozen(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool frozen)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        foreach (var player in players)
        {
            player.SourceState.SetMovementFrozen(frozen);
        }

        var feedbackText = frozen ? $"{TargetText(players)} frozen" : $"{TargetText(players)} unfrozen";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerNoClip(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool enabled)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? "";
        var players = SelectPlayers(game, selector).ToArray();
        if (players.Length == 0)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.SetNoClip(enabled);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"NoClip player={player.Name} id={player.PlayerId} enabled={enabled} movetype={player.SourceState.MoveType} solid={player.SourceState.SolidType}"),
                targetEndpoint: player.Endpoint,
                issuedBy));
        }

        var feedbackText = enabled ? $"{TargetText(players)} noclip enabled" : $"{TargetText(players)} noclip disabled";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerSpeed(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var players, out var args)
            || !TryParseFloat(args[0], out var speed))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and speed", []);
        }

        foreach (var player in players)
        {
            player.SourceState.SetMovementSpeed(speed);
        }

        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"{TargetText(players)} speed set to {speed:0.##}");
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerGravity(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var players, out var args)
            || !TryParseFloat(args[0], out var gravityScale))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and gravity scale", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.SetPersonalGravityScale(gravityScale);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                string.Create(CultureInfo.InvariantCulture, $"Gravity player={player.Name} id={player.PlayerId} scale={player.SourceState.GravityScale:0.##}"),
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"{TargetText(players)} gravity scale set to {gravityScale:0.##}");
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult ResetPlayerGravity(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var players, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.SetPersonalGravityScale(null);
            player.SourceState.ApplyWorldRules(game.World);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                string.Create(CultureInfo.InvariantCulture, $"Gravity player={player.Name} id={player.PlayerId} scale={player.SourceState.GravityScale:0.##} reset=true"),
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = players.Length == 1
            ? $"{players[0].Name} gravity scale reset to {players[0].SourceState.GravityScale:0.##}"
            : $"{TargetText(players)} gravity scale reset";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerFov(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var players, out var args)
            || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fov))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and fov", []);
        }

        foreach (var player in players)
        {
            player.SourceState.SetFov(fov);
        }

        var feedbackText = $"{TargetText(players)} fov set to {fov}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerViewModel(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool? forcedEnabled)
    {
        var minArgumentCount = forcedEnabled.HasValue ? 0 : 1;
        if (!TryReadTargetedGroupArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount, out var players, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and on/off value", []);
        }

        if (!forcedEnabled.HasValue)
        {
            if (args.Length == 0 || !TryParseBoolValue(args[0], out var parsedEnabled))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and on/off value", []);
            }

            forcedEnabled = parsedEnabled;
        }

        var enabled = forcedEnabled.Value;
        var queued = new List<PendingSourceServerCommand>(players.Length + 1);
        foreach (var player in players)
        {
            player.SourceState.SetDrawViewModel(enabled);
            queued.Add(game.QueueSourceServerCommand(
                SourceServerCommandType.GameEvent,
                $"ViewModel player={player.Name} id={player.PlayerId} enabled={enabled}",
                targetEndpoint: null,
                issuedBy));
        }

        var feedbackText = enabled
            ? $"{TargetText(players)} viewmodel shown"
            : $"{TargetText(players)} viewmodel hidden";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        queued.Add(feedback);
        return new SourceServerCommandResult(true, feedbackText, [.. queued]);
    }

    private static SourceServerCommandResult SetPlayerBlind(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        bool enabled)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.SetBlind(enabled);
        var feedbackText = enabled
            ? $"{player.Name} blinded"
            : $"{player.Name} unblinded";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"Blind player={player.Name} id={player.PlayerId} enabled={enabled} fov={player.SourceState.Fov} drawViewModel={player.SourceState.DrawViewModel}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult ShakePlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out var args))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        var amplitude = 8.0f;
        if (args.Length > 0 && !TryParseFloat(args[0], out amplitude))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} amplitude must be a number", []);
        }

        player.SourceState.SetViewShake(amplitude);
        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"{player.Name} shaken with amplitude {Math.Clamp(amplitude, 0.0f, 32.0f):0.##}");
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Shake player={player.Name} id={player.PlayerId} amplitude={Math.Clamp(amplitude, 0.0f, 32.0f):0.##} punch=({player.SourceState.PunchAngleX:0.##},{player.SourceState.PunchAngleY:0.##},{player.SourceState.PunchAngleZ:0.##})"),
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerRenderColor(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRenderColor(args, out var red, out var green, out var blue, out var alpha))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and color: r g b [a] or #RRGGBB[AA]", []);
        }

        player.SourceState.SetRenderColor(red, green, blue, alpha);
        var feedbackText = $"{player.Name} render color set to rgba({red},{green},{blue},{alpha})";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"RenderColor player={player.Name} id={player.PlayerId} rgba={red},{green},{blue},{alpha} value=0x{player.SourceState.RenderColor:x8}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerRenderAlpha(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseByteValue(args[0], out var alpha))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and alpha 0-255", []);
        }

        player.SourceState.SetRenderAlpha(alpha);
        if (alpha < 255 && player.SourceState.RenderMode == 0)
        {
            player.SourceState.SetRenderMode(1);
        }

        var feedbackText = $"{player.Name} render alpha set to {alpha}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"RenderAlpha player={player.Name} id={player.PlayerId} alpha={alpha} mode={player.SourceState.RenderMode} value=0x{player.SourceState.RenderColor:x8}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerRenderMode(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseByteValue(args[0], out var renderMode))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and render mode 0-255", []);
        }

        player.SourceState.SetRenderMode(renderMode);
        var feedbackText = $"{player.Name} render mode set to {renderMode}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"RenderMode player={player.Name} id={player.PlayerId} mode={renderMode}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerRenderFx(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseByteValue(args[0], out var renderFx))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and render fx 0-255", []);
        }

        player.SourceState.SetRenderFx(renderFx);
        var feedbackText = $"{player.Name} render fx set to {renderFx}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"RenderFx player={player.Name} id={player.PlayerId} fx={renderFx}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerEffects(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var effects))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and effect mask", []);
        }

        player.SourceState.SetEffects(effects);
        return QueueEffectsFeedback(game, player, issuedBy, "set");
    }

    private static SourceServerCommandResult AddPlayerEffects(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var effects))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and effect mask", []);
        }

        player.SourceState.AddEffects(effects);
        return QueueEffectsFeedback(game, player, issuedBy, "added");
    }

    private static SourceServerCommandResult RemovePlayerEffects(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var effects))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and effect mask", []);
        }

        player.SourceState.RemoveEffects(effects);
        return QueueEffectsFeedback(game, player, issuedBy, "removed");
    }

    private static SourceServerCommandResult ResetPlayerRenderState(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.ClearRenderState();
        var feedbackText = $"{player.Name} render state reset";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"RenderReset player={player.Name} id={player.PlayerId}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult SetPlayerModelIndex(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var modelIndex))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and model index", []);
        }

        player.SourceState.SetModelIndex(modelIndex);
        return QueueAnimationFeedback(game, player, issuedBy, $"model index set to {modelIndex}");
    }

    private static SourceServerCommandResult SetPlayerTextureFrameIndex(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var textureFrameIndex))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and texture frame index", []);
        }

        player.SourceState.SetTextureFrameIndex(textureFrameIndex);
        return QueueAnimationFeedback(game, player, issuedBy, $"texture frame set to {textureFrameIndex}");
    }

    private static SourceServerCommandResult SetPlayerAnimation(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var sequence))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and sequence [playbackRate] [cycle]", []);
        }

        float? playbackRate = null;
        if (args.Length >= 2)
        {
            if (!TryParseFloat(args[1], out var parsedPlaybackRate))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} playback rate must be a number", []);
            }

            playbackRate = parsedPlaybackRate;
        }

        float? cycle = null;
        if (args.Length >= 3)
        {
            if (!TryParseFloat(args[2], out var parsedCycle))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} cycle must be a number", []);
            }

            cycle = parsedCycle;
        }

        player.SourceState.SetAnimation(sequence, playbackRate, cycle);
        return QueueAnimationFeedback(
            game,
            player,
            issuedBy,
            string.Create(
                CultureInfo.InvariantCulture,
                $"animation sequence={player.SourceState.Sequence} rate={player.SourceState.PlaybackRate:0.###} cycle={player.SourceState.ServerAnimationCycle:0.###}"));
    }

    private static SourceServerCommandResult SetPlayerSkinBody(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var skin))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and skin [body] [hitboxSet]", []);
        }

        uint? body = null;
        if (args.Length >= 2)
        {
            if (!TryParseRawUInt(args[1], out var parsedBody))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} body must be an integer", []);
            }

            body = parsedBody;
        }

        uint? hitboxSet = null;
        if (args.Length >= 3)
        {
            if (!TryParseRawUInt(args[2], out var parsedHitboxSet))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} hitbox set must be an integer", []);
            }

            hitboxSet = parsedHitboxSet;
        }

        player.SourceState.SetSkinBody(skin, body, hitboxSet);
        return QueueAnimationFeedback(
            game,
            player,
            issuedBy,
            $"skin/body set to {player.SourceState.Skin}/{player.SourceState.Body}/{player.SourceState.HitboxSet}");
    }

    private static SourceServerCommandResult SetPlayerModelScale(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseFloat(args[0], out var scale))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and scale", []);
        }

        player.SourceState.SetModelWidthScale(scale);
        return QueueAnimationFeedback(
            game,
            player,
            issuedBy,
            string.Create(CultureInfo.InvariantCulture, $"model scale set to {player.SourceState.ModelWidthScale:0.###}"));
    }

    private static SourceServerCommandResult SetPlayerForceVector(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 3, out var player, out var args)
            || !TryParseFloat(args[0], out var x)
            || !TryParseFloat(args[1], out var y)
            || !TryParseFloat(args[2], out var z))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and x y z [forceBone]", []);
        }

        var forceBone = 0U;
        if (args.Length >= 4 && !TryParseRawUInt(args[3], out forceBone))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} force bone must be an integer", []);
        }

        player.SourceState.SetForceVector(x, y, z, forceBone);
        return QueueAnimationFeedback(
            game,
            player,
            issuedBy,
            string.Create(CultureInfo.InvariantCulture, $"force vector set to ({player.SourceState.ForceX:0.##},{player.SourceState.ForceY:0.##},{player.SourceState.ForceZ:0.##}) bone={player.SourceState.ForceBone}"));
    }

    private static SourceServerCommandResult SetPlayerAnimationFlags(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 2, out var player, out var args)
            || !TryParseByteValue(args[0], out var clientSideAnimation)
            || !TryParseByteValue(args[1], out var clientSideFrameReset))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and clientSideAnimation clientSideFrameReset", []);
        }

        player.SourceState.SetAnimationFlags(clientSideAnimation, clientSideFrameReset);
        return QueueAnimationFeedback(game, player, issuedBy, $"animation flags set to {clientSideAnimation}/{clientSideFrameReset}");
    }

    private static SourceServerCommandResult SetPlayerLightingOrigin(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 2, out var player, out var args)
            || !TryParseRawUInt(args[0], out var handle)
            || !TryParseRawUInt(args[1], out var relativeHandle))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and lightingOriginHandle relativeHandle", []);
        }

        player.SourceState.SetLightingOriginHandles(handle, relativeHandle);
        return QueueAnimationFeedback(game, player, issuedBy, $"lighting origin set to {handle}/{relativeHandle}");
    }

    private static SourceServerCommandResult SetPlayerFade(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 3, out var player, out var args)
            || !TryParseFloat(args[0], out var minDistance)
            || !TryParseFloat(args[1], out var maxDistance)
            || !TryParseFloat(args[2], out var scale))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and min max scale", []);
        }

        player.SourceState.SetFade(minDistance, maxDistance, scale);
        return QueueAnimationFeedback(
            game,
            player,
            issuedBy,
            string.Create(CultureInfo.InvariantCulture, $"fade set to {player.SourceState.FadeMinDistance:0.##}/{player.SourceState.FadeMaxDistance:0.##}/{player.SourceState.FadeScale:0.###}"));
    }

    private static SourceServerCommandResult BumpPlayerMuzzleFlash(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.BumpMuzzleFlash();
        return QueueAnimationFeedback(game, player, issuedBy, $"muzzle flash parity bumped to {player.SourceState.MuzzleFlashParity}");
    }

    private static SourceServerCommandResult ResetPlayerAnimationState(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 0, out var player, out _))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.ResetAnimationState();
        return QueueAnimationFeedback(game, player, issuedBy, "animation state reset");
    }

    private static SourceServerCommandResult QueueAnimationFeedback(
        GameManagerSession game,
        PlayerSession player,
        string issuedBy,
        string action)
    {
        var feedbackText = $"{player.Name} {action}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Animation player={player.Name} id={player.PlayerId} action=\"{action}\" model={player.SourceState.ModelIndex} seq={player.SourceState.Sequence} skin={player.SourceState.Skin} body={player.SourceState.Body} hitbox={player.SourceState.HitboxSet} scale={player.SourceState.ModelWidthScale:0.###}"),
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult QueueEffectsFeedback(
        GameManagerSession game,
        PlayerSession player,
        string issuedBy,
        string action)
    {
        var feedbackText = $"effects {action} for {player.Name}: 0x{player.SourceState.Effects:x8}";
        var gameplay = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"Effects player={player.Name} id={player.PlayerId} action={action} mask=0x{player.SourceState.Effects:x8}",
            targetEndpoint: null,
            issuedBy);
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [gameplay, feedback]);
    }

    private static SourceServerCommandResult RefillPlayer(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        player.SourceState.RefillResources();
        var feedbackText = $"{player.Name} refilled";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerAmmo(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var primary))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and primary [secondary] [metal] [clip]", []);
        }

        if (!TryReadOptionalUInt(args, 1, player.SourceState.SecondaryAmmo, out var secondary)
            || !TryReadOptionalUInt(args, 2, player.SourceState.Metal, out var metal)
            || !TryReadOptionalUInt(args, 3, player.SourceState.WeaponClip1, out var clip))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} ammo values must be unsigned integers", []);
        }

        player.SourceState.SetAmmo(primary, secondary, metal, clip);
        var feedbackText = $"{player.Name} ammo set to primary={player.SourceState.PrimaryAmmo} secondary={player.SourceState.SecondaryAmmo} metal={player.SourceState.Metal} clip={player.SourceState.WeaponClip1}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerSentry(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and level [state] [shells] [rockets]", []);
        }

        if (!TryReadOptionalUInt(args, 1, 1, out var state)
            || !TryReadOptionalUInt(args, 2, 200, out var shells)
            || !TryReadOptionalUInt(args, 3, level >= 3 ? 20U : 0U, out var rockets))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} sentry values must be unsigned integers", []);
        }

        player.SourceState.SetSentry(level, state, shells, rockets);
        var feedbackText = $"{player.Name} sentry level={player.SourceState.SentryGun.UpgradeLevel} state={player.SourceState.SentryGun.State} shells={player.SourceState.SentryGun.AmmoShells} rockets={player.SourceState.SentryGun.AmmoRockets}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerTeleporter(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !uint.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var state))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and state [recharge] [times-used]", []);
        }

        var recharge = 0.0f;
        if (args.Length >= 2 && !TryParseFloat(args[1], out recharge))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} recharge must be numeric", []);
        }

        if (!TryReadOptionalUInt(args, 2, player.SourceState.Teleporter.TimesUsed, out var timesUsed))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} times-used must be an unsigned integer", []);
        }

        player.SourceState.SetTeleporter(state, recharge, timesUsed);
        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"{player.Name} teleporter state={player.SourceState.Teleporter.State} recharge={player.SourceState.Teleporter.RechargeTime:0.##} uses={player.SourceState.Teleporter.TimesUsed}");
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult DestroyPlayerBuildables(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        player.SourceState.DestroyBuildables();
        var feedbackText = $"{player.Name} buildables destroyed";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddPlayerCondition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseConditionMask(args[0], out var conditionMask))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and condition id/name", []);
        }

        player.SourceState.AddPlayerCondition(conditionMask);
        var feedbackText = $"{player.Name} condition mask now 0x{player.SourceState.PlayerCondition:x8}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult RemovePlayerCondition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseConditionMask(args[0], out var conditionMask))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and condition id/name", []);
        }

        player.SourceState.RemovePlayerCondition(conditionMask);
        var feedbackText = $"{player.Name} condition mask now 0x{player.SourceState.PlayerCondition:x8}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerCondition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 1, out var player, out var args)
            || !TryParseRawUInt(args[0], out var conditionMask))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and raw condition mask", []);
        }

        player.SourceState.SetPlayerConditionMask(conditionMask);
        var feedbackText = $"{player.Name} condition mask set to 0x{player.SourceState.PlayerCondition:x8}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ClearPlayerCondition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        player.SourceState.SetPlayerConditionMask(0);
        var feedbackText = $"{player.Name} conditions cleared";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerDisguise(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedArguments(game, tokens, targetEndpoint, issuedBy, minArgumentCount: 2, out var player, out var args)
            || !TryParseTeam(args[0], out var teamNumber)
            || !TryParseClass(args[1], out var classNumber))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player, team, and class", []);
        }

        var targetIndex = 0U;
        if (args.Length >= 3 && !uint.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out targetIndex))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} target index must be an unsigned integer", []);
        }

        player.SourceState.SetDisguise(teamNumber, classNumber, targetIndex);
        var feedbackText = $"{player.Name} disguised as {TeamName(teamNumber)} {ClassName(classNumber)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ClearPlayerDisguise(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"no player matches '{selector}'", []);
        }

        player.SourceState.ClearDisguise();
        var feedbackText = $"{player.Name} disguise cleared";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerCloak(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        float defaultCloak)
    {
        PlayerSession? player;
        float cloak = defaultCloak;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            player = FindPlayer(game, targetEndpoint);
            if (tokens.Length >= 2 && !TryParseFloat(tokens[1], out cloak))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} cloak value must be numeric", []);
            }
        }
        else if (tokens.Length >= 2)
        {
            player = FindPlayer(game, tokens[1]);
            if (player is null && TryParseFloat(tokens[1], out var parsedCloak))
            {
                player = FindAdminPlayer(game, issuedBy);
                cloak = parsedCloak;
            }
            else if (tokens.Length >= 3 && !TryParseFloat(tokens[2], out cloak))
            {
                return new SourceServerCommandResult(false, $"{tokens[0]} cloak value must be numeric", []);
            }
        }
        else
        {
            player = FindAdminPlayer(game, issuedBy);
        }

        if (player is null)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.SetCloak(cloak);
        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"{player.Name} cloak set to {player.SourceState.CloakMeter:0.##}");
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerScore(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var scoreText)
            || !uint.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and score", []);
        }

        player.SourceState.Score = score;
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} score set to {score}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddPlayerScore(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var scoreText)
            || !int.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and score delta", []);
        }

        var newScore = Math.Clamp((long)player.SourceState.Score + delta, 0, uint.MaxValue);
        player.SourceState.Score = checked((uint)newScore);
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} score changed by {delta} to {player.SourceState.Score}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerCaptures(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var capturesText)
            || !uint.TryParse(capturesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var captures))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and capture count", []);
        }

        player.SourceState.Captures = captures;
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} captures set to {captures}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddPlayerCaptures(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return AddPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "capture count",
            "captures",
            static player => player.SourceState.Captures,
            static (player, value) => player.SourceState.Captures = value,
            updateTeams: true,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult SetPlayerDeaths(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return SetPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "death count",
            "deaths",
            static player => player.SourceState.Deaths,
            static (player, value) => player.SourceState.Deaths = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult AddPlayerDeaths(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return AddPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "death count",
            "deaths",
            static player => player.SourceState.Deaths,
            static (player, value) => player.SourceState.Deaths = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult SetPlayerAssists(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return SetPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "assist count",
            "assists",
            static player => player.SourceState.KillAssists,
            static (player, value) => player.SourceState.KillAssists = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult AddPlayerAssists(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return AddPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "assist count",
            "assists",
            static player => player.SourceState.KillAssists,
            static (player, value) => player.SourceState.KillAssists = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult SetPlayerDefenses(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return SetPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "defense count",
            "defenses",
            static player => player.SourceState.Defenses,
            static (player, value) => player.SourceState.Defenses = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult AddPlayerDefenses(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        return AddPlayerUIntStat(
            game,
            tokens,
            targetEndpoint,
            "defense count",
            "defenses",
            static player => player.SourceState.Defenses,
            static (player, value) => player.SourceState.Defenses = value,
            updateTeams: false,
            issuedBy: issuedBy);
    }

    private static SourceServerCommandResult SetPlayerDominations(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "domination count", "dominations", static player => player.SourceState.Dominations, static (player, value) => player.SourceState.Dominations = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerDominations(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "domination count", "dominations", static player => player.SourceState.Dominations, static (player, value) => player.SourceState.Dominations = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerRevenge(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "revenge count", "revenge", static player => player.SourceState.Revenge, static (player, value) => player.SourceState.Revenge = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerRevenge(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "revenge count", "revenge", static player => player.SourceState.Revenge, static (player, value) => player.SourceState.Revenge = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerBuildingsDestroyed(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "destruction count", "destruction", static player => player.SourceState.BuildingsDestroyed, static (player, value) => player.SourceState.BuildingsDestroyed = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerBuildingsDestroyed(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "destruction count", "destruction", static player => player.SourceState.BuildingsDestroyed, static (player, value) => player.SourceState.BuildingsDestroyed = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerHeadshots(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "headshot count", "headshots", static player => player.SourceState.Headshots, static (player, value) => player.SourceState.Headshots = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerHeadshots(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "headshot count", "headshots", static player => player.SourceState.Headshots, static (player, value) => player.SourceState.Headshots = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerBackstabs(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "backstab count", "backstabs", static player => player.SourceState.Backstabs, static (player, value) => player.SourceState.Backstabs = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerBackstabs(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "backstab count", "backstabs", static player => player.SourceState.Backstabs, static (player, value) => player.SourceState.Backstabs = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerHealPoints(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "healing point count", "healing", static player => player.SourceState.HealPoints, static (player, value) => player.SourceState.HealPoints = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerHealPoints(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "healing point count", "healing", static player => player.SourceState.HealPoints, static (player, value) => player.SourceState.HealPoints = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerInvulns(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "invulnerability count", "invulns", static player => player.SourceState.Invulns, static (player, value) => player.SourceState.Invulns = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerInvulns(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "invulnerability count", "invulns", static player => player.SourceState.Invulns, static (player, value) => player.SourceState.Invulns = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerTeleports(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "teleport count", "teleports", static player => player.SourceState.Teleports, static (player, value) => player.SourceState.Teleports = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerTeleports(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "teleport count", "teleports", static player => player.SourceState.Teleports, static (player, value) => player.SourceState.Teleports = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult SetPlayerResupplyPoints(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return SetPlayerUIntStat(game, tokens, targetEndpoint, "resupply count", "resupplies", static player => player.SourceState.ResupplyPoints, static (player, value) => player.SourceState.ResupplyPoints = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult AddPlayerResupplyPoints(GameManagerSession game, string[] tokens, string? targetEndpoint, string issuedBy)
    {
        return AddPlayerUIntStat(game, tokens, targetEndpoint, "resupply count", "resupplies", static player => player.SourceState.ResupplyPoints, static (player, value) => player.SourceState.ResupplyPoints = value, updateTeams: false, issuedBy);
    }

    private static SourceServerCommandResult ResetPlayerStats(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        var selector = tokens.Length >= 2 ? JoinTail(tokens, 1) : targetEndpoint ?? issuedBy;
        var player = FindPlayer(game, selector) ?? FindAdminPlayer(game, issuedBy);
        if (player is null)
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player", []);
        }

        player.SourceState.Score = 0;
        player.SourceState.Deaths = 0;
        player.SourceState.Captures = 0;
        player.SourceState.Defenses = 0;
        player.SourceState.Dominations = 0;
        player.SourceState.Revenge = 0;
        player.SourceState.BuildingsDestroyed = 0;
        player.SourceState.Headshots = 0;
        player.SourceState.Backstabs = 0;
        player.SourceState.HealPoints = 0;
        player.SourceState.Invulns = 0;
        player.SourceState.Teleports = 0;
        player.SourceState.ResupplyPoints = 0;
        player.SourceState.KillAssists = 0;
        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"{player.Name} scoreboard stats reset";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        var gameEvent = game.QueueSourceServerCommand(
            SourceServerCommandType.GameEvent,
            $"PlayerStatsReset player={player.Name} playerId={player.PlayerId}",
            targetEndpoint: player.Endpoint,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback, gameEvent]);
    }

    private static SourceServerCommandResult Scoreboard(GameManagerSession game, string issuedBy)
    {
        var players = game.ActivePlayers()
            .OrderByDescending(static player => player.SourceState.Score)
            .ThenBy(static player => player.SourceState.TeamNumber)
            .ThenBy(static player => player.PlayerId)
            .Select(static player => string.Create(
                CultureInfo.InvariantCulture,
                $"{player.Name}:{TeamName(player.SourceState.TeamNumber)}/{ClassName(player.SourceState.ClassNumber)} {player.SourceState.Score}p {player.SourceState.Deaths}d {player.SourceState.Captures}c {player.SourceState.Defenses}def {player.SourceState.KillAssists}ast {player.SourceState.BuildingsDestroyed}dest {player.SourceState.Headshots}hs {player.SourceState.Backstabs}bs {player.SourceState.HealPoints}heal"))
            .ToArray();
        var feedbackText = players.Length == 0 ? "scoreboard: no active players" : $"scoreboard: {string.Join("; ", players)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText.Length <= 240 ? feedbackText : feedbackText[..240],
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetPlayerUIntStat(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string requiredValueName,
        string displayName,
        Func<PlayerSession, uint> getValue,
        Action<PlayerSession, uint> setValue,
        bool updateTeams,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var valueText)
            || !uint.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and {requiredValueName}", []);
        }

        setValue(player, value);
        if (updateTeams)
        {
            game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        }

        var feedbackText = $"{player.Name} {displayName} set to {getValue(player)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddPlayerUIntStat(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string requiredValueName,
        string displayName,
        Func<PlayerSession, uint> getValue,
        Action<PlayerSession, uint> setValue,
        bool updateTeams,
        string issuedBy)
    {
        if (!TryReadTargetedValue(game, tokens, targetEndpoint, out var player, out var valueText)
            || !int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and {requiredValueName} delta", []);
        }

        var current = getValue(player);
        var updated = Math.Clamp((long)current + delta, 0L, uint.MaxValue);
        setValue(player, checked((uint)updated));
        if (updateTeams)
        {
            game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        }

        var feedbackText = $"{player.Name} {displayName} changed by {delta} to {getValue(player)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetTeamScore(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 3
            || !TryParseTeam(tokens[1], out var teamNumber)
            || teamNumber is not (2 or 3)
            || !uint.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires red/blu and score", []);
        }

        game.World.SetTeamScore(teamNumber, score, game.ActivePlayers());
        var feedbackText = $"{TeamName(teamNumber)} score set to {score}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult AddTeamScore(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 3
            || !TryParseTeam(tokens[1], out var teamNumber)
            || teamNumber is not (2 or 3)
            || !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var delta))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires red/blu and score delta", []);
        }

        game.World.AddTeamScore(teamNumber, delta, game.ActivePlayers());
        var feedbackText = $"{TeamName(teamNumber)} score changed by {delta} to {game.World.Team(teamNumber).Score}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetTeamCaptures(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 3
            || !TryParseTeam(tokens[1], out var teamNumber)
            || teamNumber is not (2 or 3)
            || !uint.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var captures))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires red/blu and capture count", []);
        }

        game.World.SetTeamFlagCaptures(teamNumber, captures, game.ActivePlayers());
        var feedbackText = $"{TeamName(teamNumber)} captures set to {captures}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ListFlags(GameManagerSession game, string issuedBy)
    {
        var feedbackText = game.World.Flags.Count == 0
            ? "no flags on current map"
            : string.Join(", ", game.World.Flags.Select(static flag =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{TeamName(flag.TeamNumber)} flag={flag.State} carrier={flag.CarrierName ?? "none"} pos={flag.X:0.#},{flag.Y:0.#},{flag.Z:0.#}")));
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText.Length <= 240 ? feedbackText : feedbackText[..240], targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult TakeFlag(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedOptionalTeam(game, tokens, targetEndpoint, out var player, out var flagTeam))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and optional red/blu flag team", []);
        }

        flagTeam ??= OpposingTeam(player.SourceState.TeamNumber);
        if (flagTeam is not (2 or 3) || !game.World.TakeFlag(flagTeam.Value, player))
        {
            return new SourceServerCommandResult(false, $"cannot make {player.Name} take {TeamName(flagTeam ?? 0)} flag", []);
        }

        var feedbackText = $"{player.Name} took {TeamName(flagTeam.Value)} flag";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult DropFlag(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedOptionalTeam(game, tokens, targetEndpoint, out var player, out var flagTeam))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and optional red/blu flag team", []);
        }

        flagTeam ??= OpposingTeam(player.SourceState.TeamNumber);
        if (flagTeam is not (2 or 3) || !game.World.DropFlag(flagTeam.Value, player))
        {
            return new SourceServerCommandResult(false, $"cannot drop {TeamName(flagTeam ?? 0)} flag", []);
        }

        var feedbackText = $"{player.Name} dropped {TeamName(flagTeam.Value)} flag";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ReturnFlag(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || !TryParseTeam(tokens[1], out var flagTeam) || flagTeam is not (2 or 3))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires red or blu", []);
        }

        if (!game.World.ReturnFlag(flagTeam))
        {
            return new SourceServerCommandResult(false, $"cannot return {TeamName(flagTeam)} flag", []);
        }

        var feedbackText = $"{TeamName(flagTeam)} flag returned";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult CaptureFlag(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy)
    {
        if (!TryReadTargetedOptionalTeam(game, tokens, targetEndpoint, out var player, out var flagTeam))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires a player and optional red/blu flag team", []);
        }

        flagTeam ??= OpposingTeam(player.SourceState.TeamNumber);
        if (flagTeam is not (2 or 3) || !game.World.CaptureFlag(player, flagTeam.Value, game.ActivePlayers()))
        {
            return new SourceServerCommandResult(false, $"cannot capture {TeamName(flagTeam ?? 0)} flag for {player.Name}", []);
        }

        var team = game.World.Team(player.SourceState.TeamNumber);
        var feedbackText = $"{player.Name} captured {TeamName(flagTeam.Value)} flag; {TeamName(player.SourceState.TeamNumber)} captures={team.FlagCaptures}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ListControlPoints(GameManagerSession game, string issuedBy)
    {
        var feedbackText = game.World.ControlPoints.Count == 0
            ? "no control points on current map"
            : string.Join(", ", game.World.ControlPoints.Select(static point =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"#{point.Index} owner={TeamName(point.OwnerTeam)} cap={point.LazyCapPercentage:0.##} capping={TeamName(point.CappingTeam)}")));
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetControlPointOwner(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 3
            || !uint.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pointIndex)
            || !TryParseTeam(tokens[2], out var teamNumber))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires point-index and red/blu/unassigned", []);
        }

        var capturePercentage = 1.0f;
        if (tokens.Length >= 4 && !TryParseFloat(tokens[3], out capturePercentage))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} capture percentage must be numeric", []);
        }

        if (!game.World.SetControlPointOwner(pointIndex, teamNumber, capturePercentage))
        {
            return new SourceServerCommandResult(false, $"cannot set point {pointIndex} to {tokens[2]}", []);
        }

        var feedbackText = string.Create(CultureInfo.InvariantCulture, $"point {pointIndex} owner set to {TeamName(teamNumber)} cap={Math.Clamp(capturePercentage, 0.0f, 1.0f):0.##}");
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult CaptureControlPoint(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 3
            || !uint.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pointIndex)
            || !TryParseTeam(tokens[2], out var teamNumber)
            || teamNumber is not (2 or 3))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires point-index and red/blu", []);
        }

        if (!game.World.CaptureControlPoint(pointIndex, teamNumber))
        {
            return new SourceServerCommandResult(false, $"cannot capture point {pointIndex} for {tokens[2]}", []);
        }

        var feedbackText = $"point {pointIndex} captured by {TeamName(teamNumber)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetRoundState(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || !TryParseRoundState(tokens[1], out var roundState))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires init/setup/running/teamwin/gameover or numeric state", []);
        }

        var winningTeam = 0U;
        if (tokens.Length >= 3 && (!TryParseTeam(tokens[2], out winningTeam) || winningTeam is not (0 or 2 or 3)))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} winning team must be red/blu/unassigned", []);
        }

        if (!game.World.SetRoundState(roundState, winningTeam))
        {
            return new SourceServerCommandResult(false, $"cannot set round state {tokens[1]}", []);
        }

        var feedbackText = $"round state set to {game.World.RoundState} winning={TeamName(game.World.WinningTeam)}";
        var feedback = game.QueueSourceServerCommand(SourceServerCommandType.Chat, feedbackText, targetEndpoint: null, issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetTimerPaused(GameManagerSession game, bool paused, string issuedBy)
    {
        game.World.SetTimerPaused(paused);
        var feedbackText = paused ? "round timer paused" : "round timer resumed";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SetTimeRemaining(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || !TryParseTimeSeconds(tokens[1], out var seconds))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires seconds or mm:ss", []);
        }

        game.World.SetTimeRemaining(seconds);
        var feedbackText = $"time remaining set to {FormatTime(seconds)}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ForceRoundWin(GameManagerSession game, string[] tokens, string issuedBy)
    {
        if (tokens.Length < 2 || !TryParseTeam(tokens[1], out var teamNumber) || teamNumber is not (2 or 3))
        {
            return new SourceServerCommandResult(false, $"{tokens[0]} requires red or blu", []);
        }

        if (!game.World.ForceRoundWin(teamNumber))
        {
            return new SourceServerCommandResult(false, $"cannot force win for team {tokens[1]}", []);
        }

        var feedbackText = $"{TeamName(teamNumber)} wins the round";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult SwitchTeams(GameManagerSession game, string issuedBy)
    {
        foreach (var player in game.ActivePlayers())
        {
            if (player.SourceState.TeamNumber == 2)
            {
                player.SourceState.SetTeam(3);
            }
            else if (player.SourceState.TeamNumber == 3)
            {
                player.SourceState.SetTeam(2);
            }
        }

        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = "teams switched";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult ScrambleTeams(GameManagerSession game, string issuedBy)
    {
        var players = game.ActivePlayers()
            .Where(static player => player.SourceState.TeamNumber is 2 or 3)
            .OrderBy(static player => player.PlayerId)
            .ToArray();
        for (var index = 0; index < players.Length; index++)
        {
            players[index].SourceState.SetTeam(index % 2 == 0 ? 2U : 3U);
        }

        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var feedbackText = $"scrambled {players.Length} player(s)";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult BalanceTeams(GameManagerSession game, string issuedBy)
    {
        var moved = 0;
        while (true)
        {
            var red = game.ActivePlayers()
                .Where(static player => player.SourceState.TeamNumber == 2)
                .OrderByDescending(static player => player.IsBot)
                .ThenByDescending(static player => player.PlayerId)
                .ToArray();
            var blu = game.ActivePlayers()
                .Where(static player => player.SourceState.TeamNumber == 3)
                .OrderByDescending(static player => player.IsBot)
                .ThenByDescending(static player => player.PlayerId)
                .ToArray();
            var delta = red.Length - blu.Length;
            if (Math.Abs(delta) <= 1)
            {
                break;
            }

            var player = delta > 0 ? red[0] : blu[0];
            player.SourceState.SetTeam(delta > 0 ? 3U : 2U);
            moved++;
        }

        game.World.UpdateTeamMemberCounts(game.ActivePlayers());
        var redCount = game.ActivePlayers().Count(static player => player.SourceState.TeamNumber == 2);
        var bluCount = game.ActivePlayers().Count(static player => player.SourceState.TeamNumber == 3);
        var feedbackText = $"teams balanced; moved {moved} player(s); RED={redCount} BLU={bluCount}";
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            feedbackText,
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, feedbackText, [feedback]);
    }

    private static SourceServerCommandResult RestartRound(GameManagerSession game, string issuedBy)
    {
        game.World.RestartRound();
        game.ApplyNativeSourceMapSettings(resetPlayerSpawns: true);
        var feedback = game.QueueSourceServerCommand(
            SourceServerCommandType.Chat,
            "round restarted",
            targetEndpoint: null,
            issuedBy);
        return new SourceServerCommandResult(true, "round restarted", [feedback]);
    }

    private static SourceServerCommandResult QueueRawCommand(
        GameManagerSession game,
        string commandText,
        string? targetEndpoint,
        string issuedBy,
        string feedback)
    {
        var queued = game.QueueSourceServerCommand(
            SourceServerCommandType.ConsoleCommand,
            commandText.Trim(),
            targetEndpoint,
            issuedBy);
        return new SourceServerCommandResult(true, feedback, [queued]);
    }

    private static PlayerSession? FindPlayer(GameManagerSession game, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return null;
        }

        selector = selector.Trim().Trim('"');
        return game.Players.Values.FirstOrDefault(player =>
            player.State != PlayerJoinState.Left
            && (string.Equals(player.Endpoint, selector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(player.Name, selector, StringComparison.OrdinalIgnoreCase)
                || player.PlayerId.ToString(CultureInfo.InvariantCulture) == selector));
    }

    private static IEnumerable<PlayerSession> SelectPlayers(GameManagerSession game, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            yield break;
        }

        selector = selector.Trim().Trim('"');
        var activePlayers = game.ActivePlayers().ToArray();
        IEnumerable<PlayerSession>? selected = selector.ToLowerInvariant() switch
        {
            "@all" or "all" => activePlayers,
            "@humans" or "humans" => activePlayers.Where(static player => !player.IsBot),
            "@bots" or "bots" => activePlayers.Where(static player => player.IsBot),
            "@red" or "red" => activePlayers.Where(static player => player.SourceState.TeamNumber == 2),
            "@blue" or "@blu" or "blue" or "blu" => activePlayers.Where(static player => player.SourceState.TeamNumber == 3),
            "@spec" or "@spectators" or "spectators" => activePlayers.Where(static player => player.SourceState.TeamNumber == 1),
            _ => null
        };

        if (selected is not null)
        {
            foreach (var player in selected)
            {
                yield return player;
            }

            yield break;
        }

        if (FindPlayer(game, selector) is { } singlePlayer)
        {
            yield return singlePlayer;
        }
    }

    private static string GroupCommsStateText(IReadOnlyCollection<PlayerSession> players)
    {
        var voiceMuted = players.Count(static player => player.VoiceMuted);
        var chatGagged = players.Count(static player => player.ChatGagged);
        return $"voiceMuted={voiceMuted}/{players.Count} chatGagged={chatGagged}/{players.Count}";
    }

    private static string TargetText(IReadOnlyCollection<PlayerSession> players)
    {
        return players.Count == 1
            ? players.First().Name
            : $"{players.Count} players";
    }

    private static PlayerSession? FindAdminPlayer(GameManagerSession game, string issuedBy)
    {
        return FindPlayer(game, issuedBy)
            ?? FindPlayer(game, $"The_{issuedBy}");
    }

    private static IEnumerable<PlayerSession> TournamentReadyPlayers(GameManagerSession game)
    {
        return game.ActivePlayers()
            .Where(static player => !player.IsBot && player.State == PlayerJoinState.SourceHandoff);
    }

    private static bool TryReadTargetedPosition(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        out PlayerSession player,
        out float x,
        out float y,
        out float z,
        out float? yaw,
        out float? pitch)
    {
        player = null!;
        x = 0;
        y = 0;
        z = 0;
        yaw = null;
        pitch = null;

        int offset;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            var target = FindPlayer(game, targetEndpoint);
            if (target is null || tokens.Length < 4)
            {
                return false;
            }

            player = target;
            offset = 1;
        }
        else if (tokens.Length >= 5)
        {
            var target = FindPlayer(game, tokens[1]);
            if (target is null)
            {
                return false;
            }

            player = target;
            offset = 2;
        }
        else if (tokens.Length >= 4)
        {
            var target = FindAdminPlayer(game, issuedBy);
            if (target is null)
            {
                return false;
            }

            player = target;
            offset = 1;
        }
        else
        {
            return false;
        }

        if (!TryParseFloat(tokens[offset], out x)
            || !TryParseFloat(tokens[offset + 1], out y)
            || !TryParseFloat(tokens[offset + 2], out z))
        {
            return false;
        }

        if (tokens.Length > offset + 3)
        {
            if (!TryParseFloat(tokens[offset + 3], out var parsedYaw))
            {
                return false;
            }

            yaw = parsedYaw;
        }

        if (tokens.Length > offset + 4)
        {
            if (!TryParseFloat(tokens[offset + 4], out var parsedPitch))
            {
                return false;
            }

            pitch = parsedPitch;
        }

        return true;
    }

    private static bool TryReadTargetedArguments(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        int minArgumentCount,
        out PlayerSession player,
        out string[] args)
    {
        player = null!;
        args = [];
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            var target = FindPlayer(game, targetEndpoint);
            if (target is null || tokens.Length - 1 < minArgumentCount)
            {
                return false;
            }

            player = target;
            args = tokens[1..];
            return true;
        }

        if (tokens.Length >= minArgumentCount + 2)
        {
            var target = FindPlayer(game, tokens[1]);
            if (target is null)
            {
                return false;
            }

            player = target;
            args = tokens[2..];
            return true;
        }

        if (tokens.Length >= minArgumentCount + 1)
        {
            var target = FindAdminPlayer(game, issuedBy);
            if (target is null)
            {
                return false;
            }

            player = target;
            args = tokens[1..];
            return true;
        }

        return false;
    }

    private static bool TryReadTargetedGroupValue(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        out PlayerSession[] players,
        out string value)
    {
        players = [];
        value = "";
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            if (tokens.Length < 2)
            {
                return false;
            }

            players = SelectPlayers(game, targetEndpoint).ToArray();
            if (players.Length == 0)
            {
                return false;
            }

            value = tokens[1];
            return true;
        }

        if (tokens.Length < 3)
        {
            return false;
        }

        players = SelectPlayers(game, tokens[1]).ToArray();
        if (players.Length == 0)
        {
            return false;
        }

        value = tokens[2];
        return true;
    }

    private static bool TryReadTargetedGroupArguments(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        string issuedBy,
        int minArgumentCount,
        out PlayerSession[] players,
        out string[] args)
    {
        players = [];
        args = [];
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            if (tokens.Length - 1 < minArgumentCount)
            {
                return false;
            }

            players = SelectPlayers(game, targetEndpoint).ToArray();
            if (players.Length == 0)
            {
                return false;
            }

            args = tokens[1..];
            return true;
        }

        if (tokens.Length >= minArgumentCount + 2)
        {
            players = SelectPlayers(game, tokens[1]).ToArray();
            if (players.Length > 0)
            {
                args = tokens[2..];
                return true;
            }
        }

        if (tokens.Length >= minArgumentCount + 1)
        {
            players = SelectPlayers(game, issuedBy).ToArray();
            if (players.Length == 0)
            {
                players = SelectPlayers(game, $"The_{issuedBy}").ToArray();
            }

            if (players.Length > 0)
            {
                args = tokens[1..];
                return true;
            }
        }

        return false;
    }

    private static bool TryReadOptionalUInt(string[] values, int index, uint fallback, out uint value)
    {
        value = fallback;
        return values.Length <= index
            || uint.TryParse(values[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseRawUInt(string value, out uint parsed)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
        }

        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryParseByteValue(string value, out byte parsed)
    {
        parsed = 0;
        if (!TryParseRawUInt(value, out var raw) || raw > byte.MaxValue)
        {
            return false;
        }

        parsed = (byte)raw;
        return true;
    }

    private static bool TryParseRenderColor(string[] values, out byte red, out byte green, out byte blue, out byte alpha)
    {
        red = 0;
        green = 0;
        blue = 0;
        alpha = 255;

        if (values.Length == 1)
        {
            var raw = values[0].Trim();
            if (raw.StartsWith('#'))
            {
                raw = raw[1..];
            }
            else if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw[2..];
            }
            else
            {
                return false;
            }

            if (raw.Length is not (6 or 8)
                || !uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color))
            {
                return false;
            }

            if (raw.Length == 6)
            {
                color = (color << 8) | 0xff;
            }

            red = (byte)(color >> 24);
            green = (byte)(color >> 16);
            blue = (byte)(color >> 8);
            alpha = (byte)color;
            return true;
        }

        if (values.Length is < 3 or > 4)
        {
            return false;
        }

        if (!TryParseByteValue(values[0], out red)
            || !TryParseByteValue(values[1], out green)
            || !TryParseByteValue(values[2], out blue))
        {
            return false;
        }

        return values.Length == 3 || TryParseByteValue(values[3], out alpha);
    }

    private static bool TryParseConditionMask(string value, out uint conditionMask)
    {
        conditionMask = 0;
        switch (value.Trim().ToLowerInvariant())
        {
            case "crit":
            case "crits":
            case "critical":
                conditionMask = 1U << 0;
                return true;
            case "zoomed":
            case "scoped":
                conditionMask = 1U << 1;
                return true;
            case "disguised":
            case "disguise":
                conditionMask = 1U << 2;
                return true;
            case "cloak":
            case "cloaked":
            case "invis":
            case "invisible":
                conditionMask = 1U << 4;
                return true;
            case "uber":
            case "invuln":
            case "invulnerable":
                conditionMask = 1U << 5;
                return true;
            case "taunt":
            case "taunting":
                conditionMask = 1U << 7;
                return true;
            case "burn":
            case "burning":
            case "fire":
                conditionMask = 1U << 10;
                return true;
            case "stun":
            case "stunned":
                conditionMask = 1U << 14;
                return true;
            case "bleed":
            case "bleeding":
                conditionMask = 1U << 15;
                return true;
            default:
                if (!TryParseRawUInt(value, out var parsed))
                {
                    return false;
                }

                conditionMask = parsed < 32 ? 1U << (int)parsed : parsed;
                return true;
        }
    }

    private static bool TryParseFloat(string value, out float parsed)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }

    private static void CopyPosition(PlayerSession player, PlayerSession destination)
    {
        player.SourceState.SetPosition(
            destination.SourceState.OriginX,
            destination.SourceState.OriginY,
            destination.SourceState.OriginZ,
            destination.SourceState.Yaw,
            destination.SourceState.Pitch);
    }

    private static string FormatPosition(Tf2SourcePlayerState state)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"x={state.OriginX:0.##} y={state.OriginY:0.##} z={state.OriginZ:0.##} yaw={state.Yaw:0.##} pitch={state.Pitch:0.##}");
    }

    private static bool TryReadTargetedValue(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        out PlayerSession player,
        out string value)
    {
        player = null!;
        value = "";
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            if (tokens.Length < 2)
            {
                return false;
            }

            var target = FindPlayer(game, targetEndpoint);
            if (target is null)
            {
                return false;
            }

            player = target;
            value = tokens[1];
            return true;
        }

        if (tokens.Length < 3)
        {
            return false;
        }

        var selectedPlayer = FindPlayer(game, tokens[1]);
        if (selectedPlayer is null)
        {
            return false;
        }

        player = selectedPlayer;
        value = tokens[2];
        return true;
    }

    private static bool TryReadTargetedOptionalTeam(
        GameManagerSession game,
        string[] tokens,
        string? targetEndpoint,
        out PlayerSession player,
        out uint? teamNumber)
    {
        player = null!;
        teamNumber = null;
        if (!string.IsNullOrWhiteSpace(targetEndpoint))
        {
            var selectedPlayer = FindPlayer(game, targetEndpoint);
            if (selectedPlayer is null)
            {
                return false;
            }

            player = selectedPlayer;
            if (tokens.Length >= 2)
            {
                if (!TryParseTeam(tokens[1], out var parsedTeam))
                {
                    return false;
                }

                teamNumber = parsedTeam;
            }

            return true;
        }

        if (tokens.Length < 2)
        {
            return false;
        }

        var target = FindPlayer(game, tokens[1]);
        if (target is null)
        {
            return false;
        }

        player = target;
        if (tokens.Length >= 3)
        {
            if (!TryParseTeam(tokens[2], out var parsedTeam))
            {
                return false;
            }

            teamNumber = parsedTeam;
        }

        return true;
    }

    private static bool TryParseTeam(string value, out uint teamNumber)
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

    private static bool TryParseClass(string value, out byte classNumber)
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

    private static string TeamName(uint teamNumber)
    {
        return teamNumber switch
        {
            1 => "Spectator",
            2 => "RED",
            3 => "BLU",
            _ => "Unassigned"
        };
    }

    private static uint? OpposingTeam(uint teamNumber)
    {
        return teamNumber switch
        {
            2 => 3,
            3 => 2,
            _ => null
        };
    }

    private static uint LeastPopulatedPlayableTeam(GameManagerSession game, PlayerSession? excluding = null)
    {
        var redCount = game.ActivePlayers().Count(player => player != excluding && player.SourceState.TeamNumber == 2);
        var bluCount = game.ActivePlayers().Count(player => player != excluding && player.SourceState.TeamNumber == 3);
        return redCount <= bluCount ? 2U : 3U;
    }

    private static string ClassName(byte classNumber)
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

    private static bool TryParseRoundState(string value, out uint roundState)
    {
        roundState = 0;
        switch (value.Trim().ToLowerInvariant())
        {
            case "0":
            case "init":
            case "idle":
                roundState = 0;
                return true;
            case "3":
            case "setup":
            case "pregame":
                roundState = 3;
                return true;
            case "4":
            case "run":
            case "running":
            case "live":
                roundState = 4;
                return true;
            case "5":
            case "teamwin":
            case "win":
            case "roundwin":
                roundState = 5;
                return true;
            case "8":
            case "gameover":
            case "end":
                roundState = 8;
                return true;
            default:
                return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out roundState)
                    && roundState <= 8;
        }
    }

    private static bool TryReadInt(string[] tokens, out int value)
    {
        value = 0;
        return tokens.Length >= 2
            && int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBool(string[] tokens, out bool value)
    {
        value = false;
        if (tokens.Length < 2)
        {
            return false;
        }

        return TryParseBoolValue(tokens[1], out value);
    }

    private static bool TryParseTimeSeconds(string text, out int seconds)
    {
        seconds = 0;
        var value = text.Trim();
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator > 0)
        {
            if (int.TryParse(value[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                && int.TryParse(value[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var remainingSeconds)
                && remainingSeconds is >= 0 and < 60)
            {
                seconds = checked(Math.Max(0, minutes) * 60 + remainingSeconds);
                return true;
            }

            return false;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)
            && seconds >= 0;
    }

    private static string FormatTime(int seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }

    private static bool TryResolveConfigPath(GameManagerSession game, string configName, out string configPath)
    {
        configPath = "";
        var requested = configName.Trim().Trim('"').Replace('\\', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(requested)
            || Path.IsPathRooted(requested)
            || requested.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(static part => part == ".."))
        {
            return false;
        }

        if (!requested.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
        {
            requested += ".cfg";
        }

        foreach (var directory in game.SourceConfigDirectories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var root = Path.GetFullPath(directory);
            var candidate = Path.GetFullPath(Path.Combine(root, requested));
            var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(candidate))
            {
                continue;
            }

            configPath = candidate;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> ReadConfigCommands(string configPath)
    {
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = StripConfigComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            foreach (var command in SplitConfigCommands(line))
            {
                var trimmed = command.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }
    }

    private static string StripConfigComment(string line)
    {
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                continue;
            }

            if (ch == '#')
            {
                return line[..index];
            }

            if (ch == '/' && index + 1 < line.Length && line[index + 1] == '/')
            {
                return line[..index];
            }
        }

        return line;
    }

    private static IEnumerable<string> SplitConfigCommands(string line)
    {
        var inQuotes = false;
        var start = 0;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (line[index] != ';' || inQuotes)
            {
                continue;
            }

            yield return line[start..index];
            start = index + 1;
        }

        yield return line[start..];
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

    private static string JoinTail(string[] tokens, int startIndex)
    {
        return startIndex >= tokens.Length
            ? ""
            : string.Join(' ', tokens.Skip(startIndex));
    }

    private static string EscapeVoteText(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static bool IsKnownSourceCvar(string command)
    {
        return KnownSourceCvars.Contains(command);
    }

    private static bool IsBooleanCvar(string command)
    {
        return command is "mp_autoteambalance"
            or "mp_disable_respawn_times"
            or "mp_friendlyfire"
            or "mp_teams_unbalance_limit"
            or "mp_tournament"
            or "mp_tournament_readymode"
            or "sv_alltalk"
            or "sv_cheats"
            or "sv_lan"
            or "tf_arena_use_queue"
            or "tf_bot_join_after_player"
            or "tf_damage_disablespread"
            or "tf_weapon_criticals";
    }

    private static bool IsIntegerCvar(string command)
    {
        return command is "mp_forcecamera"
            or "mp_fraglimit"
            or "mp_maxrounds"
            or "mp_waitingforplayers_time"
            or "mp_timelimit"
            or "mp_winlimit"
            or "sv_gravity"
            or "sv_pure"
            or "tf_bot_difficulty"
            or "tf_bot_quota"
            or "tf_ctf_bonus_time"
            or "tf_flag_caps_per_round";
    }

    private static string[] Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }
}

public sealed record SourceServerCommandResult(
    bool Applied,
    string Feedback,
    PendingSourceServerCommand[] QueuedCommands);
