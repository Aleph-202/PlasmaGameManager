using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Eatf2ServerDllSimulationReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Eatf2SimulationAreaDefinition[] AreaDefinitions =
    [
        new(
            "server-lifecycle",
            "Map/session lifecycle",
            "The native replacement must own level init/shutdown, map-change, and player connect/disconnect state instead of only answering the UDP handoff.",
            [
                new("CBaseGameStats::Event_LevelInit", "Level init event proves the official server tracks map lifecycle."),
                new("CBaseGameStats::Event_LevelShutdown", "Level shutdown event proves map cleanup exists."),
                new("CBaseGameStats::Event_MapChange", "Map-change event proves map transitions are official server work."),
                new("CBaseGameStats::Event_PlayerConnected", "Player-connected event proves player session entry is server-side."),
                new("CBaseGameStats::Event_PlayerDisconnected", "Player-disconnected event proves server-side disconnect cleanup."),
                new("CTFGameRules: ClientDisconnected", "TF game-rules disconnect hook.")
            ],
            ["m_bConnected", "m_iPlayerRating"]),
        new(
            "player-command-simulation",
            "Usercmd, movement, and player state",
            "The native replacement must decode PS3 user commands into player movement, class/team state, view state, tick base, and alive/dead state.",
            [
                new("CBasePlayer::ProcessUsercmds", "Official server validates and consumes batches of player usercmds."),
                new("CBasePlayer::PhysicsSimulate", "Official server runs player physics simulation."),
                new("CTFPlayer", "TF player entity class exists in the official server module."),
                new("CTFPlayer::ChangeTeam", "TF player team change path exists."),
                new("CTFPlayerMove", "TF-specific movement helper class exists.")
            ],
            ["m_vecOrigin", "m_iTeamNum", "m_lifeState", "m_PlayerClass", "m_iClass", "m_vecViewOffset", "m_nTickBase"]),
        new(
            "round-state-machine",
            "TF round/game-rule state machine",
            "The native replacement must advance TF round states and publish the game-rule values the PS3 client expects while loading and in game.",
            [
                new("CTeamplayRoundBasedRules::State_Enter_STARTGAME", "Official start-game state entry."),
                new("CTeamplayRoundBasedRules::State_Enter_PREROUND", "Official pre-round state entry."),
                new("CTeamplayRoundBasedRules::State_Enter_RND_RUNNING", "Official live-round state entry."),
                new("CTeamplayRoundBasedRules::State_Enter_TEAM_WIN", "Official team-win state entry."),
                new("CTeamplayRoundBasedRules::State_Enter_STALEMATE", "Official stalemate state entry."),
                new("CTeamplayRoundBasedRules::State_Think_STARTGAME", "Official state thinker controls transition timing."),
                new("DT_TeamplayRoundBasedRules", "Networked round-rules table exists."),
                new("CTFGameRules", "TF game-rules class exists.")
            ],
            ["m_iRoundState", "m_nGameType", "m_bInWaitingForPlayers", "m_flNextRespawnWave", "m_TeamRespawnWaveTimes"]),
        new(
            "map-objectives",
            "Map objective, timers, and control points",
            "The native replacement must parse map objectives enough to publish timers, control point/resource state, and capture events.",
            [
                new("CTeamControlPointMaster", "Control point master entity exists."),
                new("CTeamControlPointMasterCPMThink", "Control point master has a server think path."),
                new("CTeamRoundTimer", "Round timer entity exists."),
                new("RoundTimerThink", "Round timer has a server think path."),
                new("RoundTimerSetupThink", "Round timer has setup think path."),
                new("CTFObjectiveResource", "TF objective resource exists."),
                new("DT_TFObjectiveResource", "Objective resource is networked."),
                new("tf_point_captured", "Control point captured event exists.")
            ],
            ["m_flTimerEndTime", "m_bTimerPaused", "m_nFlagCaptures", "m_iCaptures"]),
        new(
            "combat-weapons-projectiles",
            "Combat, weapons, projectiles, and damage",
            "The native replacement must simulate firing, projectiles, damage, ammo, active weapons, and stats-changing combat events.",
            [
                new("CBaseEntity::TakeDamage", "Damage application exists in the server module."),
                new("CBaseGameStats::Event_WeaponFired", "Weapon-fired stats/event path exists."),
                new("CBaseGameStats::Event_WeaponHit", "Weapon-hit stats/event path exists."),
                new("CTFWeaponBase", "TF base weapon class exists."),
                new("CTFWeaponBaseGun", "TF gun base class exists."),
                new("CTFWeaponBaseMelee", "TF melee base class exists."),
                new("CTFProjectile_Rocket", "Rocket projectile class exists."),
                new("CTFGrenadePipebombProjectile", "Pipebomb projectile class exists.")
            ],
            ["m_hMyWeapons", "m_hActiveWeapon", "m_iAmmo", "m_iClip1", "m_iClip2", "m_iHealth", "m_iTotalScore", "m_iKillAssists"]),
        new(
            "engineer-buildings",
            "Engineer buildings",
            "The native replacement must represent buildings enough for Engineer gameplay and sentry/dispenser/teleporter network state.",
            [
                new("CObjectSentrygun", "Sentrygun server class exists."),
                new("CObjectTeleporter", "Teleporter server class exists."),
                new("CObjectDispenser", "Dispenser server class exists."),
                new("DT_ObjectSentrygun", "Sentrygun is networked."),
                new("DT_ObjectTeleporter", "Teleporter is networked."),
                new("DT_ObjectDispenser", "Dispenser is networked.")
            ],
            ["m_iUpgradeLevel", "m_iAmmoShells", "m_iAmmoRockets", "m_flRechargeTime", "m_flYawToExit"]),
        new(
            "ranked-stats",
            "Ranked stats and performance report feeds",
            "Arcadia owns FESL rank storage, but a ranked native Source server must emit the official events/properties that decide what gets persisted.",
            [
                new("tf_stats_track", "TF stats tracking cvar exists."),
                new("tf_stats_verbose", "TF stats verbose cvar exists."),
                new("CBaseGameStats::Event_PlayerKilled", "Player-killed stats event exists."),
                new("CBaseGameStats::Event_PlayerKilledOther", "Player-killed-other stats event exists."),
                new("CBaseGameStats::Event_PlayerDisconnected", "Disconnect event anchors session stat flush.")
            ],
            ["m_iTotalScore", "m_iCaptures", "m_iKillAssists", "m_iPlayerRating"])
    ];

    public static async Task<Eatf2ServerDllSimulationReport> ReduceAsync(
        string interestingStringsPath,
        string mangledSymbolsPath,
        string ghidraEvidencePath,
        string datatablesPath,
        string netpropsPath,
        string sourceFieldContractPath,
        string outputPath)
    {
        var evidence = LoadEvidence(
            interestingStringsPath,
            mangledSymbolsPath,
            ghidraEvidencePath,
            datatablesPath,
            netpropsPath);
        var sourceContract = LoadSourceFieldContract(sourceFieldContractPath);
        var areas = AreaDefinitions
            .Select(area => BuildArea(area, evidence, sourceContract))
            .ToArray();
        var missingMarkers = areas
            .SelectMany(static area => area.Markers)
            .Where(static marker => !marker.PresentInOfficialDll)
            .Select(static marker => $"{marker.Marker} ({marker.Semantics})")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingGeneratedFields = areas
            .SelectMany(static area => area.NativeStateFields)
            .Where(static field => !field.PresentInGeneratedNativeContract)
            .Select(static field => field.Field)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var report = new Eatf2ServerDllSimulationReport(
            "eatf2-serverdll-native-simulation-contract",
            "Official EA TF2 server.dll runtime evidence reduced into the authoritative simulation duties for the native PS3 Source server replacement.",
            new Eatf2ServerDllSimulationInputs(
                interestingStringsPath,
                mangledSymbolsPath,
                ghidraEvidencePath,
                datatablesPath,
                netpropsPath,
                sourceFieldContractPath),
            new Eatf2ServerDllSimulationSummary(
                areas.Length,
                areas.Count(static area => area.PresentInOfficialDll),
                areas.Sum(static area => area.Markers.Length),
                areas.Sum(static area => area.Markers.Count(static marker => marker.PresentInOfficialDll)),
                missingMarkers.Length,
                areas.Sum(static area => area.NativeStateFields.Length),
                areas.Sum(static area => area.NativeStateFields.Count(static field => field.PresentInGeneratedNativeContract)),
                missingGeneratedFields.Length),
            areas,
            missingMarkers,
            missingGeneratedFields,
            [
                "Treat these areas as the minimum native authoritative server loop: accept usercmds, simulate player physics, advance TF round/objective state, run weapon/projectile/building logic, then publish the mapped sendtable fields through the PS3 native snapshot writer.",
                "A generated packet that has the right length but is not backed by one of these simulation areas is only a compatibility shim, not final native server behavior.",
                "Arcadia rank storage should only be updated from ranked sessions after the native simulation emits the corresponding official stats events."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Eatf2ServerDllSimulationArea BuildArea(
        Eatf2SimulationAreaDefinition definition,
        EvidenceIndex evidence,
        SourceFieldContractSnapshot sourceContract)
    {
        var markers = definition.Markers
            .Select(marker => new Eatf2SimulationMarkerCoverage(
                marker.Marker,
                marker.Semantics,
                evidence.Contains(marker.Marker),
                evidence.EvidenceAddress(marker.Marker),
                evidence.EvidenceSource(marker.Marker)))
            .ToArray();
        var fields = definition.NativeStateFields
            .Select(field => new Eatf2SimulationNativeStateField(
                field,
                evidence.Contains(field),
                evidence.EvidenceAddress(field),
                sourceContract.GeneratedProperties.Contains(field)))
            .ToArray();

        return new Eatf2ServerDllSimulationArea(
            definition.Id,
            definition.Name,
            definition.NativeReplacementRequirement,
            markers,
            fields,
            markers.All(static marker => marker.PresentInOfficialDll),
            fields.All(static field => field.PresentInGeneratedNativeContract));
    }

    private static EvidenceIndex LoadEvidence(params string[] paths)
    {
        var entries = new List<EvidenceLine>();
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var source = Path.GetFileName(path);
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                entries.Add(new EvidenceLine(source, trimmed));
            }
        }

        return new EvidenceIndex(entries.ToArray());
    }

    private static SourceFieldContractSnapshot LoadSourceFieldContract(string path)
    {
        if (!File.Exists(path))
        {
            return new SourceFieldContractSnapshot([]);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var properties = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("SourceEntityFieldContracts", out var contracts)
            && contracts.ValueKind == JsonValueKind.Array)
        {
            foreach (var contract in contracts.EnumerateArray())
            {
                AddSourceProperty(contract, properties);
            }
        }

        if (document.RootElement.TryGetProperty("SourceDeltaContracts", out var deltas)
            && deltas.ValueKind == JsonValueKind.Array)
        {
            foreach (var delta in deltas.EnumerateArray())
            {
                AddSourceProperty(delta, properties);
            }
        }

        return new SourceFieldContractSnapshot(properties);
    }

    private static void AddSourceProperty(JsonElement element, HashSet<string> properties)
    {
        if (!element.TryGetProperty("SourceProperty", out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = property.GetString() ?? "";
        if (value.Length == 0)
        {
            return;
        }

        foreach (Match match in SourcePropertyNameRegex().Matches(value))
        {
            properties.Add(match.Value);
        }
    }

    [GeneratedRegex(@"m_[A-Za-z0-9_]+")]
    private static partial Regex SourcePropertyNameRegex();

    private sealed record Eatf2SimulationAreaDefinition(
        string Id,
        string Name,
        string NativeReplacementRequirement,
        Eatf2SimulationMarkerDefinition[] Markers,
        string[] NativeStateFields);

    private sealed record Eatf2SimulationMarkerDefinition(
        string Marker,
        string Semantics);

    private sealed record EvidenceLine(string Source, string Text);

    private sealed class EvidenceIndex(EvidenceLine[] lines)
    {
        public bool Contains(string marker)
        {
            return lines.Any(line => line.Text.Contains(marker, StringComparison.Ordinal));
        }

        public string EvidenceAddress(string marker)
        {
            foreach (var line in lines)
            {
                if (!line.Text.Contains(marker, StringComparison.Ordinal))
                {
                    continue;
                }

                var address = line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (address is { Length: > 0 } && address.All(static c => Uri.IsHexDigit(c)))
                {
                    return address;
                }
            }

            return "";
        }

        public string EvidenceSource(string marker)
        {
            return lines.FirstOrDefault(line => line.Text.Contains(marker, StringComparison.Ordinal))?.Source ?? "";
        }
    }

    private sealed record SourceFieldContractSnapshot(HashSet<string> GeneratedProperties);
}

public sealed record Eatf2ServerDllSimulationReport(
    string Status,
    string Note,
    Eatf2ServerDllSimulationInputs Inputs,
    Eatf2ServerDllSimulationSummary Summary,
    Eatf2ServerDllSimulationArea[] Areas,
    string[] MissingOfficialMarkers,
    string[] MissingGeneratedNativeFields,
    string[] NativeReplacementGuidance);

public sealed record Eatf2ServerDllSimulationInputs(
    string InterestingStrings,
    string MangledSymbols,
    string GhidraEvidence,
    string Datatables,
    string Netprops,
    string SourceFieldContract);

public sealed record Eatf2ServerDllSimulationSummary(
    int AreaCount,
    int AreasPresentInOfficialDll,
    int MarkerCount,
    int MarkersPresentInOfficialDll,
    int MissingOfficialMarkerCount,
    int NativeStateFieldCount,
    int NativeStateFieldsPresentInGeneratedNativeContract,
    int MissingGeneratedNativeFieldCount);

public sealed record Eatf2ServerDllSimulationArea(
    string Id,
    string Name,
    string NativeReplacementRequirement,
    Eatf2SimulationMarkerCoverage[] Markers,
    Eatf2SimulationNativeStateField[] NativeStateFields,
    bool PresentInOfficialDll,
    bool CoveredByGeneratedNativeContract);

public sealed record Eatf2SimulationMarkerCoverage(
    string Marker,
    string Semantics,
    bool PresentInOfficialDll,
    string EvidenceAddress,
    string EvidenceSource);

public sealed record Eatf2SimulationNativeStateField(
    string Field,
    bool PresentInOfficialDll,
    string EvidenceAddress,
    bool PresentInGeneratedNativeContract);
