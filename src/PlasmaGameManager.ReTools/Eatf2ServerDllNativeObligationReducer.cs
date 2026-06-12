using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllNativeObligationReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly NativeObligationDefinition[] Obligations =
    [
        new(
            "engine-server-interface",
            "Source server DLL interface",
            "Official server.dll exports the Source server DLL surface. The native replacement does not need to be a Windows DLL, but it must implement the same lifecycle semantics.",
            ["ServerGameDLL005", "CServerGameDLL", "IServerGameDLL", "IServerGameEnts", "CreateInterface"],
            ["Ps3NativeSourceResponder", "GameManagerSession", "Tf2SourceLaunchProfile"],
            "partial",
            ["The standalone backend maps the lifecycle into PlasmaGameManager objects, but it is not a full Source engine DLL interface."]),
        new(
            "fesl-gamemanager-bridge",
            "EA FESL/GameManager bridge",
            "Official server.dll imports FESL hub/GameManager services and reports stats through EA-side helpers.",
            ["FeslHubSingle_GetGameManager", "FeslHubSingle_Get", "FeslStatsRetriever", "TFStatsReporter", "feslStatsLogFile"],
            ["Tf2BackendProvisioner", "Tf2SourceLaunchProfile", "GameManagerProfiles", "GameManagerControlService", "Tf2NativeStatsIngestService"],
            "partial",
            ["Arcadia provisions, advertises sessions, and ingests native ranked stats, but GameManager callbacks are not fully driven by complete authoritative gameplay events yet."]),
        new(
            "level-player-lifecycle",
            "Level and player lifecycle",
            "The server must own LevelInit, map changes, player connect/disconnect, and cleanup.",
            ["CBaseGameStats::Event_LevelInit", "CBaseGameStats::Event_LevelShutdown", "CBaseGameStats::Event_MapChange", "CBaseGameStats::Event_PlayerConnected", "CBaseGameStats::Event_PlayerDisconnected", "CTFGameRules: ClientDisconnected"],
            ["PlayerJoinState", "SourceHandoff", "RecordGeneratedSourceServerEvent", "public bool MarkNativeSourcePlayerDisconnected", "private void EmitNativeSourceLevelShutdown"],
            "partial",
            ["Official-style lifecycle events are emitted for level start/shutdown/map change and player connect/disconnect, but full Source entity cleanup, allocation churn, and map script reset are not complete."]),
        new(
            "usercmd-physics",
            "Usercmd and physics simulation",
            "The server must decode PS3 usercmds, apply movement/view/button intent, and simulate player physics.",
            ["CBasePlayer::ProcessUsercmds", "CBasePlayer::PhysicsSimulate", "CTFPlayerMove", "PhysicsSimulate: %s bad movetype"],
            ["Ps3SourceClientCommandIntent", "ApplyClientCommandIntent", "UpdateNativeMovementFlags", "MaxSpeed", "ApplyWorldRules", "Tf2MapMetadata", "Tf2MapEntityParser", "MapMetadataPath"],
            "partial",
            ["Usercmd intent, speed clamping, jump/duck, ground flags, PS3-visible physics fields, and BSP entity metadata-backed spawn/world bounds are implemented, but brush collision, trigger touch, water volumes, ladders, and Source movement prediction are not complete."]),
        new(
            "snapshot-sendtables",
            "PS3 native snapshot/sendtable publication",
            "The server must publish official TF sendtable state through the PS3 native transport.",
            ["DT_TFPlayer", "DT_TFPlayerResource", "DT_TFGameRules", "DT_TFObjectiveResource", "DT_TeamRoundTimer", "DT_Plasma"],
            ["Ps3SourceNativeMessages", "BuildSnapshotFrameBody", "BuildPlayerEntityDelta", "BuildObjectiveResourceDelta", "BuildGameplayRulesDelta"],
            "partial",
            ["The sendtable vocabulary and generated deltas exist, but the values are still fed by a simplified world model rather than a complete authoritative Source simulation."]),
        new(
            "round-objective-state",
            "Round, timer, and objective state",
            "The server must advance TF round rules, timers, control points, flags, and objective-resource state.",
            ["CTeamplayRoundBasedRules::State_Enter_STARTGAME", "CTeamplayRoundBasedRules::State_Enter_RND_RUNNING", "CTeamRoundTimer", "RoundTimerThink", "CTFObjectiveResource", "tf_point_captured"],
            ["Tf2SourceWorldState", "RoundState", "RoundTimer", "ControlPoints", "BuildObjectiveResourceDelta", "Tf2MapControlPoint", "Tf2MapCaptureArea", "Tf2MapFlag"],
            "partial",
            ["Round/timer/objective fields now seed flags, control points, capture-area timing, and bounds from PS3 BSP entity metadata when available, but map script/entity IO and full official capture/flag rules are not fully native yet."]),
        new(
            "combat-weapons-buildings",
            "Combat, weapons, projectiles, and Engineer buildings",
            "The server must simulate firing, damage, ammo, projectiles, weapons, sentries, dispensers, and teleporters.",
            ["CBaseEntity::TakeDamage", "CBaseGameStats::Event_WeaponFired", "CBaseGameStats::Event_WeaponHit", "CTFWeaponBase", "CTFProjectile_Rocket", "CObjectSentrygun", "CObjectTeleporter", "CObjectDispenser"],
            ["ConsumeGeneratedCommandSimulation", "GeneratedProjectileTicks", "AdvanceGeneratedBuildables", "SetSentry", "SetTeleporter", "PrimaryAmmo", "WeaponInReload", "WeaponBuildState", "TfWeaponState"],
            "partial",
            ["The native backend now publishes richer TF weapon, reload, medigun, pipebomb, and builder sendtable state, but it does not yet implement official weapon scripts, projectile physics, hit detection, or building AI."]),
        new(
            "ranked-stats-performance-report",
            "Ranked stats and performance report",
            "The server must emit official stats events only for ranked sessions and feed Arcadia persistence/performance-report data.",
            ["tf_stats_track", "tf_stats_verbose", "CBaseGameStats::Event_PlayerKilled", "CBaseGameStats::Event_PlayerKilledOther", "TFStatsReporter::_PlayerStatsLookupCallback"],
            ["Tf2RankedStatsExporter", "Tf2NativeStatsIngestService", "NativeRankedStatsExportPath", "IsRanked", "GetStats", "Performance", "Score"],
            "partial",
            ["Ranked-only stat mutation now exists through a native export/Arcadia ingest path, but scoring, combat attribution, and per-class event attribution remain simplified until full Source gameplay simulation is complete."])
    ];

    public static async Task<Eatf2ServerDllNativeObligationReport> ReduceAsync(
        string interestingStringsPath,
        string mangledSymbolsPath,
        string ghidraEvidencePath,
        string sourceRoot,
        string outputPath)
    {
        var nativeServerStringsPath = Path.Combine(
            Path.GetDirectoryName(interestingStringsPath) ?? ".",
            "eatf2-serverdll-native-server-strings.txt");
        var officialEvidence = EvidenceIndex.LoadTextFiles(
            interestingStringsPath,
            mangledSymbolsPath,
            ghidraEvidencePath,
            nativeServerStringsPath);
        var implementationEvidence = EvidenceIndex.LoadSourceTree(sourceRoot);
        var obligations = Obligations
            .Select(definition => BuildObligation(definition, officialEvidence, implementationEvidence))
            .ToArray();
        var incomplete = obligations
            .Where(static obligation => !obligation.NativeComplete)
            .Select(static obligation => obligation.Id)
            .ToArray();
        var missingOfficial = obligations
            .Where(static obligation => obligation.MissingOfficialMarkers.Length > 0)
            .SelectMany(static obligation => obligation.MissingOfficialMarkers.Select(marker => $"{obligation.Id}:{marker}"))
            .ToArray();
        var missingImplementation = obligations
            .Where(static obligation => obligation.MissingImplementationMarkers.Length > 0)
            .SelectMany(static obligation => obligation.MissingImplementationMarkers.Select(marker => $"{obligation.Id}:{marker}"))
            .ToArray();

        var report = new Eatf2ServerDllNativeObligationReport(
            "eatf2-serverdll-native-server-obligations",
            "Strict server.dll-derived native server obligation audit. This report distinguishes recovered official evidence from current native implementation completeness.",
            new Eatf2ServerDllNativeObligationInputs(
                interestingStringsPath,
                mangledSymbolsPath,
                ghidraEvidencePath,
                sourceRoot),
            new Eatf2ServerDllNativeObligationSummary(
                obligations.Length,
                obligations.Count(static obligation => obligation.OfficialEvidencePresent),
                obligations.Count(static obligation => obligation.ImplementationEvidencePresent),
                obligations.Count(static obligation => obligation.NativeComplete),
                incomplete.Length,
                missingOfficial.Length,
                missingImplementation.Length),
            obligations,
            incomplete,
            missingOfficial,
            missingImplementation,
            [
                "A partial obligation means the replacement has useful native scaffolding but still lacks official Source server semantics.",
                "Do not treat the backend as native-complete until every obligation is complete and live RPCS3 reaches steady gameplay.",
                "The next implementation focus should be the usercmd/physics and level-player-lifecycle obligations, because live failures are currently inside the Source handoff/load path."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Eatf2ServerDllNativeObligation BuildObligation(
        NativeObligationDefinition definition,
        EvidenceIndex officialEvidence,
        EvidenceIndex implementationEvidence)
    {
        var officialMarkers = definition.OfficialMarkers
            .Select(marker => new NativeObligationMarker(
                marker,
                officialEvidence.Contains(marker),
                officialEvidence.FirstEvidencePath(marker),
                officialEvidence.FirstEvidenceLine(marker)))
            .ToArray();
        var implementationMarkers = definition.ImplementationMarkers
            .Select(marker => new NativeObligationMarker(
                marker,
                implementationEvidence.Contains(marker),
                implementationEvidence.FirstEvidencePath(marker),
                implementationEvidence.FirstEvidenceLine(marker)))
            .ToArray();
        var missingOfficial = officialMarkers
            .Where(static marker => !marker.Present)
            .Select(static marker => marker.Marker)
            .ToArray();
        var missingImplementation = implementationMarkers
            .Where(static marker => !marker.Present)
            .Select(static marker => marker.Marker)
            .ToArray();
        var officialPresent = missingOfficial.Length == 0;
        var implementationPresent = missingImplementation.Length == 0;
        var nativeComplete = officialPresent
            && implementationPresent
            && string.Equals(definition.CurrentStatus, "complete", StringComparison.Ordinal);

        return new Eatf2ServerDllNativeObligation(
            definition.Id,
            definition.Name,
            definition.Requirement,
            definition.CurrentStatus,
            nativeComplete,
            officialPresent,
            implementationPresent,
            officialMarkers,
            implementationMarkers,
            missingOfficial,
            missingImplementation,
            definition.KnownGaps);
    }

    private sealed record NativeObligationDefinition(
        string Id,
        string Name,
        string Requirement,
        string[] OfficialMarkers,
        string[] ImplementationMarkers,
        string CurrentStatus,
        string[] KnownGaps);

    private sealed record EvidenceLine(string Path, string Text);

    private sealed class EvidenceIndex(EvidenceLine[] lines)
    {
        public static EvidenceIndex LoadTextFiles(params string[] paths)
        {
            var lines = new List<EvidenceLine>();
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                foreach (var line in File.ReadLines(path))
                {
                    if (line.Trim().Length > 0)
                    {
                        lines.Add(new EvidenceLine(path, line));
                    }
                }
            }

            return new EvidenceIndex(lines.ToArray());
        }

        public static EvidenceIndex LoadSourceTree(string sourceRoot)
        {
            var lines = new List<EvidenceLine>();
            if (!Directory.Exists(sourceRoot))
            {
                return new EvidenceIndex([]);
            }

            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (!IsSourceEvidenceFile(path))
                {
                    continue;
                }

                foreach (var line in File.ReadLines(path))
                {
                    if (line.Trim().Length > 0)
                    {
                        lines.Add(new EvidenceLine(path, line));
                    }
                }
            }

            return new EvidenceIndex(lines.ToArray());
        }

        public bool Contains(string marker)
        {
            return lines.Any(line => line.Text.Contains(marker, StringComparison.Ordinal));
        }

        public string FirstEvidencePath(string marker)
        {
            return lines.FirstOrDefault(line => line.Text.Contains(marker, StringComparison.Ordinal))?.Path ?? "";
        }

        public string FirstEvidenceLine(string marker)
        {
            return lines.FirstOrDefault(line => line.Text.Contains(marker, StringComparison.Ordinal))?.Text.Trim() ?? "";
        }

        private static bool IsSourceEvidenceFile(string path)
        {
            var extension = Path.GetExtension(path);
            if (extension is not (".cs" or ".fs" or ".sh" or ".json"))
            {
                return false;
            }

            if (string.Equals(Path.GetFileName(path), "Eatf2ServerDllNativeObligationReducer.cs", StringComparison.Ordinal))
            {
                return false;
            }

            return !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}.local{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }
    }
}

public sealed record Eatf2ServerDllNativeObligationReport(
    string Status,
    string Note,
    Eatf2ServerDllNativeObligationInputs Inputs,
    Eatf2ServerDllNativeObligationSummary Summary,
    Eatf2ServerDllNativeObligation[] Obligations,
    string[] IncompleteObligations,
    string[] MissingOfficialMarkers,
    string[] MissingImplementationMarkers,
    string[] NativeCompletionGuidance);

public sealed record Eatf2ServerDllNativeObligationInputs(
    string InterestingStrings,
    string MangledSymbols,
    string GhidraEvidence,
    string SourceRoot);

public sealed record Eatf2ServerDllNativeObligationSummary(
    int ObligationCount,
    int ObligationsWithOfficialEvidence,
    int ObligationsWithImplementationEvidence,
    int NativeCompleteObligationCount,
    int IncompleteObligationCount,
    int MissingOfficialMarkerCount,
    int MissingImplementationMarkerCount);

public sealed record Eatf2ServerDllNativeObligation(
    string Id,
    string Name,
    string Requirement,
    string CurrentImplementationStatus,
    bool NativeComplete,
    bool OfficialEvidencePresent,
    bool ImplementationEvidencePresent,
    NativeObligationMarker[] OfficialMarkers,
    NativeObligationMarker[] ImplementationMarkers,
    string[] MissingOfficialMarkers,
    string[] MissingImplementationMarkers,
    string[] KnownGaps);

public sealed record NativeObligationMarker(
    string Marker,
    bool Present,
    string EvidencePath,
    string EvidenceLine);
