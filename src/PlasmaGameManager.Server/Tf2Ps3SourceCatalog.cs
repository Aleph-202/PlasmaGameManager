using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public static class Tf2Ps3SourceCatalog
{
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
}
