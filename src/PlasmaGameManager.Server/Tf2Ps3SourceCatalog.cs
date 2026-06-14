using System.Text;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public static class Tf2Ps3SourceCatalog
{
    private static readonly string[] OfficialClassModelNames =
    [
        "scout",
        "sniper",
        "soldier",
        "demo",
        "medic",
        "heavy",
        "pyro",
        "spy",
        "engineer"
    ];

    private static readonly string[] OfficialClassViewModelNames =
    [
        "v_scattergun_scout",
        "v_sniperrifle_sniper",
        "v_rocketlauncher_soldier",
        "v_grenadelauncher_demo",
        "v_syringegun_medic",
        "v_minigun_heavy",
        "v_flamethrower_pyro",
        "v_revolver_spy",
        "v_shotgun_engineer"
    ];

    private static readonly string[] OfficialClassWorldModelNames =
    [
        "w_scattergun",
        "w_sniperrifle",
        "w_rocketlauncher",
        "w_grenadelauncher",
        "w_syringegun",
        "w_minigun",
        "w_flamethrower",
        "w_revolver",
        "w_shotgun"
    ];

    public static IReadOnlyList<string> BootstrapModelPrecacheEntries { get; } = DistinctStrings(
    [
        "",
        .. OfficialClassModelNames.Select(static className => $"models/player/{className}.mdl"),
        .. OfficialClassViewModelNames.Select(static modelName => $"models/weapons/v_models/{modelName}.mdl"),
        .. OfficialClassWorldModelNames.Select(static modelName => $"models/weapons/w_models/{modelName}.mdl"),
        "models/flag/briefcase.mdl",
        "models/items/ammopack_small.mdl",
        "models/items/ammopack_medium.mdl",
        "models/items/ammopack_large.mdl",
        "models/items/medkit_small.mdl",
        "models/items/medkit_medium.mdl",
        "models/items/medkit_large.mdl",
        "models/buildables/sentry1.mdl",
        "models/buildables/sentry2.mdl",
        "models/buildables/sentry3.mdl",
        "models/buildables/dispenser.mdl",
        "models/buildables/teleporter.mdl",
        "models/weapons/w_models/w_rocket.mdl",
        "models/weapons/w_models/w_stickybomb.mdl",
        "models/weapons/w_models/w_syringe_proj.mdl"
    ]);

    public static IReadOnlyList<string> BootstrapSoundPrecacheEntries(string mapName)
    {
        mapName = string.IsNullOrWhiteSpace(mapName) ? "ctf_2fort" : mapName;
        return
        [
            "",
            $"media/{mapName}.wav",
            "common/null.wav",
            "ambient/outdoors.wav",
            "ambient/indoors.wav"
        ];
    }

    public static uint PlayerModelPrecacheIndexForClass(byte classNumber)
    {
        return ModelPrecacheIndex($"models/player/{ClassModelNameForClass(classNumber)}.mdl");
    }

    public static uint WeaponViewModelPrecacheIndexForClass(byte classNumber)
    {
        return ModelPrecacheIndex($"models/weapons/v_models/{ClassViewModelNameForClass(classNumber)}.mdl");
    }

    public static uint WeaponWorldModelPrecacheIndexForClass(byte classNumber)
    {
        return ModelPrecacheIndex($"models/weapons/w_models/{ClassWorldModelNameForClass(classNumber)}.mdl");
    }

    public static uint ModelPrecacheIndex(string modelName)
    {
        for (var index = 0; index < BootstrapModelPrecacheEntries.Count; index++)
        {
            if (BootstrapModelPrecacheEntries[index].Equals(modelName, StringComparison.OrdinalIgnoreCase))
            {
                return checked((uint)index);
            }
        }

        throw new InvalidOperationException($"Model {modelName} is not present in the TF2 PS3 bootstrap modelprecache table.");
    }

    private static string ClassModelNameForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => "sniper",
            3 => "soldier",
            4 => "demo",
            5 => "medic",
            6 => "heavy",
            7 => "pyro",
            8 => "spy",
            9 => "engineer",
            _ => "scout"
        };
    }

    private static string ClassViewModelNameForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => "v_sniperrifle_sniper",
            3 => "v_rocketlauncher_soldier",
            4 => "v_grenadelauncher_demo",
            5 => "v_syringegun_medic",
            6 => "v_minigun_heavy",
            7 => "v_flamethrower_pyro",
            8 => "v_revolver_spy",
            9 => "v_shotgun_engineer",
            _ => "v_scattergun_scout"
        };
    }

    private static string ClassWorldModelNameForClass(byte classNumber)
    {
        return classNumber switch
        {
            2 => "w_sniperrifle",
            3 => "w_rocketlauncher",
            4 => "w_grenadelauncher",
            5 => "w_syringegun",
            6 => "w_minigun",
            7 => "w_flamethrower",
            8 => "w_revolver",
            9 => "w_shotgun",
            _ => "w_scattergun"
        };
    }

    private static string[] DistinctStrings(IEnumerable<string> values)
    {
        return values
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> RequiredOfficialSendTables { get; } =
    [
        "DT_Plasma",
        "DT_TFPlayer",
        "DT_TFLocalPlayerExclusive",
        "DT_TFNonLocalPlayerExclusive",
        "DT_TFPlayerResource",
        "DT_TFPlayerShared",
        "DT_TFPlayerSharedLocal",
        "DT_TFPlayerClassShared",
        "DT_TFGameRules",
        "DT_TFGameRulesProxy",
        "DT_TFObjectiveResource",
        "DT_ObjectSentrygun",
        "DT_ObjectTeleporter",
        "DT_ObjectDispenser",
        "DT_TFWeaponBase",
        "DT_TFWeaponBaseGun",
        "DT_TFWeaponBaseMelee",
        "DT_TFWeaponBuilder",
        "DT_TFProjectile_Rocket",
        "DT_TFProjectile_Pipebomb",
        "DT_TFRagdoll"
    ];

    public static IReadOnlyList<string> BootstrapSendTables { get; } = DistinctSendTables(
    [
        "DT_World",
        "DT_BaseEntity",
        "DT_BaseAnimating",
        "DT_BaseAnimatingOverlay",
        "DT_BaseCombatCharacter",
        "DT_BaseCombatWeapon",
        "DT_BasePlayer",
        "DT_BaseTeamObjectiveResource",
        "DT_BaseViewModel",
        "DT_GameRulesProxy",
        "DT_PlayerResource",
        "DT_Team",
        "DT_TeamRoundTimer",
        "DT_TeamplayRoundBasedRules",
        "DT_TeamplayRoundBasedRulesProxy",
        .. RequiredOfficialSendTables,
        "DT_TFBaseProjectile",
        "DT_TFBaseRocket",
        "DT_TFProjectile_SentryRocket",
        "DT_TFScatterGun",
        "DT_TFShotgun",
        "DT_TFShotgun_HWG",
        "DT_TFShotgun_Pyro",
        "DT_TFShotgun_Soldier",
        "DT_TFSniperRifle",
        "DT_TFViewModel",
        "DT_TFWeaponBaseGrenadeProj",
        "DT_TFWeaponBat",
        "DT_TFWeaponBonesaw",
        "DT_TFWeaponBottle",
        "DT_TFWeaponClub",
        "DT_TFWeaponFireAxe",
        "DT_TFWeaponFists",
        "DT_TFWeaponInvis",
        "DT_TFWeaponKnife",
        "DT_TFWeaponPDA",
        "DT_TFWeaponPDA_Engineer_Destroy",
        "DT_TFWeaponPDA_Spy",
        "DT_TFWeaponShovel",
        "DT_TFWeaponWrench",
        "DT_WeaponFlameThrower",
        "DT_WeaponGrenadeLauncher",
        "DT_WeaponMedigun",
        "DT_WeaponMinigun",
        "DT_WeaponPipebombLauncher",
        "DT_WeaponPistol",
        "DT_WeaponPistol_Scout",
        "DT_WeaponRevolver",
        "DT_WeaponRocketLauncher",
        "DT_WeaponSMG",
        "DT_WeaponSyringeGun",
        "DT_AmmoPack",
        "DT_CaptureFlag",
        "DT_CaptureZone",
        "DT_ObjectSapper"
    ]);

    public static IReadOnlyList<Ps3SourceClassInfoEntry> ServerClasses { get; } = BuildServerClasses(
    [
        ("CWorld", "DT_World"),
        ("CPlayerResource", "DT_PlayerResource"),
        ("CTFPlayerResource", "DT_TFPlayerResource"),
        ("CBaseTeamObjectiveResource", "DT_BaseTeamObjectiveResource"),
        ("CTFObjectiveResource", "DT_TFObjectiveResource"),
        ("CTeam", "DT_Team"),
        ("CTFTeam", "DT_TFTeam"),
        ("CTeamRoundTimer", "DT_TeamRoundTimer"),
        ("CTeamplayRoundBasedRulesProxy", "DT_TeamplayRoundBasedRulesProxy"),
        ("CTFGameRulesProxy", "DT_TFGameRulesProxy"),
        ("CTFPlayer", "DT_TFPlayer"),
        ("CTFViewModel", "DT_TFViewModel"),
        ("CTFRagdoll", "DT_TFRagdoll"),
        ("CTFAmmoPack", "DT_AmmoPack"),
        ("CObjectDispenser", "DT_ObjectDispenser"),
        ("CObjectSapper", "DT_ObjectSapper"),
        ("CObjectSentrygun", "DT_ObjectSentrygun"),
        ("CObjectTeleporter", "DT_ObjectTeleporter"),
        ("CTFBaseProjectile", "DT_TFBaseProjectile"),
        ("CTFBaseRocket", "DT_TFBaseRocket"),
        ("CTFProjectile_Rocket", "DT_TFProjectile_Rocket"),
        ("CTFProjectile_SentryRocket", "DT_TFProjectile_SentryRocket"),
        ("CTFGrenadePipebombProjectile", "DT_TFProjectile_Pipebomb"),
        ("CTFWeaponBuilder", "DT_TFWeaponBuilder"),
        ("CTFWeaponPDA", "DT_TFWeaponPDA"),
        ("CTFWeaponPDA_Engineer_Destroy", "DT_TFWeaponPDA_Engineer_Destroy"),
        ("CTFWeaponPDA_Spy", "DT_TFWeaponPDA_Spy"),
        ("CTFScatterGun", "DT_TFScatterGun"),
        ("CTFShotgun", "DT_TFShotgun"),
        ("CTFShotgun_HWG", "DT_TFShotgun_HWG"),
        ("CTFShotgun_Pyro", "DT_TFShotgun_Pyro"),
        ("CTFShotgun_Soldier", "DT_TFShotgun_Soldier"),
        ("CTFSniperRifle", "DT_TFSniperRifle"),
        ("CTFFlameThrower", "DT_WeaponFlameThrower"),
        ("CTFGrenadeLauncher", "DT_WeaponGrenadeLauncher"),
        ("CWeaponMedigun", "DT_WeaponMedigun"),
        ("CTFMinigun", "DT_WeaponMinigun"),
        ("CTFPipebombLauncher", "DT_WeaponPipebombLauncher"),
        ("CTFPistol", "DT_WeaponPistol"),
        ("CTFPistol_Scout", "DT_WeaponPistol_Scout"),
        ("CTFRevolver", "DT_WeaponRevolver"),
        ("CTFRocketLauncher", "DT_WeaponRocketLauncher"),
        ("CTFSMG", "DT_WeaponSMG"),
        ("CTFSyringeGun", "DT_WeaponSyringeGun"),
        ("CTFBat", "DT_TFWeaponBat"),
        ("CTFBonesaw", "DT_TFWeaponBonesaw"),
        ("CTFBottle", "DT_TFWeaponBottle"),
        ("CTFClub", "DT_TFWeaponClub"),
        ("CTFFireAxe", "DT_TFWeaponFireAxe"),
        ("CTFFists", "DT_TFWeaponFists"),
        ("CTFKnife", "DT_TFWeaponKnife"),
        ("CTFShovel", "DT_TFWeaponShovel"),
        ("CTFWrench", "DT_TFWeaponWrench")
    ]);

    public static IReadOnlyList<Ps3SourceGameEventDescriptor> BootstrapGameEventDescriptors { get; } =
    [
        new(0, "server_spawn",
        [
            new(Ps3SourceGameEventFieldType.String, "hostname"),
            new(Ps3SourceGameEventFieldType.String, "address"),
            new(Ps3SourceGameEventFieldType.Short, "port"),
            new(Ps3SourceGameEventFieldType.String, "game"),
            new(Ps3SourceGameEventFieldType.String, "mapname"),
            new(Ps3SourceGameEventFieldType.Long, "maxplayers"),
            new(Ps3SourceGameEventFieldType.String, "os"),
            new(Ps3SourceGameEventFieldType.Bool, "dedicated"),
            new(Ps3SourceGameEventFieldType.Bool, "password")
        ]),
        new(1, "player_connect",
        [
            new(Ps3SourceGameEventFieldType.String, "name"),
            new(Ps3SourceGameEventFieldType.Byte, "index"),
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.String, "networkid"),
            new(Ps3SourceGameEventFieldType.String, "address")
        ]),
        new(2, "player_disconnect",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.String, "reason"),
            new(Ps3SourceGameEventFieldType.String, "name"),
            new(Ps3SourceGameEventFieldType.String, "networkid")
        ]),
        new(3, "player_activate",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid")
        ]),
        new(4, "player_team",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.Byte, "team"),
            new(Ps3SourceGameEventFieldType.Byte, "oldteam"),
            new(Ps3SourceGameEventFieldType.Bool, "disconnect")
        ]),
        new(5, "player_death",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.Short, "attacker"),
            new(Ps3SourceGameEventFieldType.String, "weapon"),
            new(Ps3SourceGameEventFieldType.Long, "damagebits"),
            new(Ps3SourceGameEventFieldType.Short, "customkill"),
            new(Ps3SourceGameEventFieldType.Short, "assister"),
            new(Ps3SourceGameEventFieldType.Short, "dominated"),
            new(Ps3SourceGameEventFieldType.Short, "assister_dominated"),
            new(Ps3SourceGameEventFieldType.Short, "revenge"),
            new(Ps3SourceGameEventFieldType.Short, "assister_revenge")
        ]),
        new(6, "player_hurt",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.Short, "attacker"),
            new(Ps3SourceGameEventFieldType.Byte, "health")
        ]),
        new(7, "player_spawn",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid")
        ]),
        new(8, "player_changeclass",
        [
            new(Ps3SourceGameEventFieldType.Short, "userid"),
            new(Ps3SourceGameEventFieldType.Short, "class")
        ]),
        new(9, "teamplay_round_start",
        [
            new(Ps3SourceGameEventFieldType.Bool, "full_reset")
        ]),
        new(10, "teamplay_round_active", []),
        new(11, "teamplay_round_win",
        [
            new(Ps3SourceGameEventFieldType.Byte, "team"),
            new(Ps3SourceGameEventFieldType.Byte, "winreason"),
            new(Ps3SourceGameEventFieldType.Short, "flagcaplimit"),
            new(Ps3SourceGameEventFieldType.Short, "full_round"),
            new(Ps3SourceGameEventFieldType.Float, "round_time"),
            new(Ps3SourceGameEventFieldType.Short, "losing_team_num_caps")
        ]),
        new(12, "ctf_flag_captured",
        [
            new(Ps3SourceGameEventFieldType.Short, "capping_team"),
            new(Ps3SourceGameEventFieldType.Short, "capping_team_score")
        ]),
        new(13, "controlpoint_initialized", []),
        new(14, "tf_game_over",
        [
            new(Ps3SourceGameEventFieldType.String, "reason")
        ])
    ];

    public static Ps3SourceResourceStringEntry[] BuildBootstrapResourceStringEntries(string mapName, int maxEncodedLength)
    {
        mapName = string.IsNullOrWhiteSpace(mapName) ? "ctf_2fort" : mapName;
        var candidates = new List<Ps3SourceResourceStringEntry>
        {
            new("maps/" + mapName + ".bsp", "GAME"),
            new("motd.txt", "GAME"),
            new("cfg/MODSETTINGS.CFG", "GAME"),
            new("cfg/LISTENSERVER.CFG", "GAME"),
            new("maps/" + mapName + ".nav", "GAME"),
            new("scripts/items/items_game.txt", "GAME"),
            new("scripts/game_sounds_manifest.txt", "GAME"),
            new("resource/ClientScheme.res", "GAME"),
            new("resource/SourceScheme.res", "GAME"),
            new("materials/vgui/maps/menu_thumb_" + mapName + ".vmt", "GAME")
        };
        candidates.InsertRange(
            2,
            BootstrapSendTables.Select(static sendTable => new Ps3SourceResourceStringEntry(sendTable, "SENDTABLE")));

        var entries = new List<Ps3SourceResourceStringEntry>(candidates.Count);
        foreach (var candidate in candidates)
        {
            entries.Add(candidate);
            if (EncodedResourceStringTableLength(entries) <= maxEncodedLength)
            {
                continue;
            }

            entries.RemoveAt(entries.Count - 1);
            break;
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<Tf2Ps3SourcePrecacheStringTable> BuildBootstrapPrecacheStringTables(string mapName)
    {
        mapName = string.IsNullOrWhiteSpace(mapName) ? "ctf_2fort" : mapName;
        var soundscapeName = mapName.StartsWith("ctf_", StringComparison.Ordinal)
            ? mapName["ctf_".Length..]
            : mapName.StartsWith("cp_", StringComparison.Ordinal)
                ? mapName["cp_".Length..]
                : mapName;

        return
        [
            new Tf2Ps3SourcePrecacheStringTable(
                "modelprecache",
                4096,
                BootstrapModelPrecacheEntries),
            new Tf2Ps3SourcePrecacheStringTable(
                "genericprecache",
                4096,
                [
                    "scripts/game_sounds_manifest.txt",
                    "scripts/decals_subrect.txt",
                    "scripts/soundscapes_manifest.txt",
                    $"scripts/soundscapes_{soundscapeName}.txt"
                ]),
            new Tf2Ps3SourcePrecacheStringTable(
                "soundprecache",
                8192,
                BootstrapSoundPrecacheEntries(mapName)),
            new Tf2Ps3SourcePrecacheStringTable(
                "decalprecache",
                512,
                [
                    "decals/bigshot1_subrect",
                    "decals/concrete/shot1_subrect",
                    "decals/metal/shot1_subrect",
                    "decals/blood1_subrect"
                ])
        ];
    }

    private static string[] DistinctSendTables(IEnumerable<string> sendTables)
    {
        return sendTables
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static Ps3SourceClassInfoEntry[] BuildServerClasses(
        IReadOnlyList<(string ClassName, string DataTableName)> classes)
    {
        var entries = new Ps3SourceClassInfoEntry[classes.Count];
        for (var i = 0; i < classes.Count; i++)
        {
            entries[i] = new Ps3SourceClassInfoEntry(i, classes[i].ClassName, classes[i].DataTableName);
        }

        return entries;
    }

    private static int EncodedResourceStringTableLength(IReadOnlyList<Ps3SourceResourceStringEntry> entries)
    {
        var length = 7; // signed -1, message id, entry count
        foreach (var entry in entries)
        {
            length += Encoding.UTF8.GetByteCount(entry.ResourceName) + 1;
            length += Encoding.UTF8.GetByteCount(entry.Classification) + 1;
        }

        return length;
    }
}

public sealed record Tf2Ps3SourcePrecacheStringTable(
    string TableName,
    ushort MaxEntries,
    IReadOnlyList<string> Entries);
