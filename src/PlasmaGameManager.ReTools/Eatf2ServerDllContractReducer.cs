using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Eatf2ServerDllContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] CoreSendTables =
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

    private static readonly Eatf2OfficialNetpropRequirement[] RequiredNetprops =
    [
        new("DT_PlayerResource", "m_bConnected", "player-resource", "Per-slot connected bit for the performance report/player list."),
        new("DT_PlayerResource", "m_iPlayerRating", "player-resource", "Per-slot EA/TF rating shown in PS3 player/resource state."),
        new("DT_TFPlayer", "m_vecOrigin", "player-entity", "Player origin required once the client transitions from loading into a live entity."),
        new("DT_TFPlayer", "m_iTeamNum", "player-entity", "Player team assignment."),
        new("DT_TFPlayer", "m_lifeState", "player-entity", "Alive/dead state."),
        new("DT_TFPlayerClassShared", "m_iClass", "player-class", "Current TF class enum."),
        new("DT_TFPlayer", "m_PlayerClass", "player-class", "Nested player class sendtable."),
        new("DT_TFLocalPlayerExclusive", "m_iFOV", "local-player", "Local player FOV."),
        new("DT_TFLocalPlayerExclusive", "m_hViewModel", "local-player", "Local player view-model handles."),
        new("DT_TFLocalPlayerExclusive", "m_vecViewOffset", "local-player", "Local player eye/view offset."),
        new("DT_TFLocalPlayerExclusive", "m_nTickBase", "local-player", "Local player command tick base."),
        new("DT_TFLocalPlayerExclusive", "m_iAmmo", "local-player", "Local ammo array."),
        new("DT_TFPlayer", "m_hMyWeapons", "player-weapons", "Weapon handle array."),
        new("DT_TFPlayer", "m_iMaxHealth", "player-entity", "Class/current max health."),
        new("DT_ObjectSentrygun", "m_iAmmoShells", "buildings", "Sentry shell count."),
        new("DT_ObjectSentrygun", "m_iAmmoRockets", "buildings", "Sentry rocket count."),
        new("DT_TFGameRules", "m_iRoundState", "game-rules", "Round state used during loading/gameplay transition."),
        new("DT_TFGameRules", "m_nGameType", "game-rules", "TF game type.")
    ];

    public static async Task<Eatf2ServerDllContractReport> ReduceAsync(
        string datatablesPath,
        string netpropsPath,
        string ghidraEvidencePath,
        string sourceFieldContractPath,
        string outputPath)
    {
        var datatables = ReadDistinctLines(datatablesPath)
            .Where(static value => value.StartsWith("DT_", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var netprops = ReadDistinctLines(netpropsPath)
            .Where(static value => value.StartsWith("m_", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ghidraEvidence = File.Exists(ghidraEvidencePath)
            ? await File.ReadAllLinesAsync(ghidraEvidencePath)
            : [];
        var sourceContract = LoadSourceFieldContract(sourceFieldContractPath);

        var officialSendTables = CoreSendTables
            .Select(table =>
            {
                var evidenceAddress = EvidenceAddress(ghidraEvidence, table);
                return new Eatf2OfficialSendTable(
                    table,
                    datatables.Contains(table, StringComparer.Ordinal) || evidenceAddress.Length > 0,
                    evidenceAddress);
            })
            .ToArray();
        var officialNetprops = RequiredNetprops
            .Select(requirement => new Eatf2OfficialNetpropCoverage(
                requirement.SendTable,
                requirement.Property,
                requirement.Role,
                requirement.Semantics,
                netprops.Contains(requirement.Property, StringComparer.Ordinal),
                EvidenceAddress(ghidraEvidence, requirement.Property),
                sourceContract.GeneratedProperties.Contains(requirement.Property),
                sourceContract.GeneratedTables.Contains(requirement.SendTable)))
            .ToArray();
        var missingSendTables = officialSendTables.Where(static table => !table.PresentInOfficialDll).Select(static table => table.SendTable).ToArray();
        var missingNetprops = officialNetprops.Where(static prop => !prop.PresentInOfficialDll).Select(static prop => prop.Property).ToArray();
        var missingGeneratedNetprops = officialNetprops
            .Where(static prop => prop.PresentInOfficialDll && !prop.PresentInGeneratedNativePayloads)
            .Select(static prop => prop.Property)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var report = new Eatf2ServerDllContractReport(
            "eatf2-serverdll-native-source-contract",
            "Official EA TF2 server.dll sendtable/netprop evidence reduced into a native Source replacement contract.",
            new Eatf2ServerDllInputs(datatablesPath, netpropsPath, ghidraEvidencePath, sourceFieldContractPath),
            new Eatf2ServerDllSummary(
                datatables.Length,
                netprops.Length,
                CoreSendTables.Length,
                officialSendTables.Count(static table => table.PresentInOfficialDll),
                RequiredNetprops.Length,
                officialNetprops.Count(static prop => prop.PresentInOfficialDll),
                officialNetprops.Count(static prop => prop.PresentInGeneratedNativePayloads),
                missingSendTables.Length,
                missingNetprops.Length,
                missingGeneratedNetprops.Length),
            officialSendTables,
            officialNetprops,
            missingSendTables,
            missingNetprops,
            missingGeneratedNetprops,
            new[]
            {
                "Use the official sendtable list as the minimum native entity vocabulary for the PS3 Source replacement.",
                "Fields present in server.dll but absent from generated native payloads are implementation gaps, not optional packet filler.",
                "The current live failure remains in PS3-native Source payload semantics, so TF.elf transport readers and this DLL sendtable contract should drive future generated frames."
            });

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static string[] ReadDistinctLines(string path)
    {
        return File.Exists(path)
            ? File.ReadLines(path)
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];
    }

    private static SourceFieldContractSnapshot LoadSourceFieldContract(string path)
    {
        if (!File.Exists(path))
        {
            return new SourceFieldContractSnapshot([], []);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var tables = new HashSet<string>(StringComparer.Ordinal);
        var properties = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("SourceEntityFieldContracts", out var contracts)
            && contracts.ValueKind == JsonValueKind.Array)
        {
            foreach (var contract in contracts.EnumerateArray())
            {
                AddIfPresent(contract, "SendTable", tables);
                AddIfPresent(contract, "SourceProperty", properties);
            }
        }

        if (document.RootElement.TryGetProperty("SourceDeltaContracts", out var deltas)
            && deltas.ValueKind == JsonValueKind.Array)
        {
            foreach (var delta in deltas.EnumerateArray())
            {
                if (!delta.TryGetProperty("SourceProperty", out var property)
                    || property.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                foreach (Match match in SourcePropertyNameRegex().Matches(property.GetString() ?? ""))
                {
                    properties.Add(match.Value);
                }
            }
        }

        return new SourceFieldContractSnapshot(tables, properties);
    }

    private static void AddIfPresent(JsonElement element, string propertyName, HashSet<string> values)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.GetString() is { Length: > 0 } value)
        {
            values.Add(value);
        }
    }

    private static string EvidenceAddress(string[] ghidraEvidence, string needle)
    {
        foreach (var line in ghidraEvidence)
        {
            if (!line.Contains(needle, StringComparison.Ordinal))
            {
                continue;
            }

            var address = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (address is { Length: > 0 } && address.All(static c => Uri.IsHexDigit(c)))
            {
                return address;
            }
        }

        return "";
    }

    [GeneratedRegex(@"m_[A-Za-z0-9_]+", RegexOptions.CultureInvariant)]
    private static partial Regex SourcePropertyNameRegex();

    private sealed record SourceFieldContractSnapshot(
        HashSet<string> GeneratedTables,
        HashSet<string> GeneratedProperties);
}

public sealed record Eatf2ServerDllContractReport(
    string Status,
    string Note,
    Eatf2ServerDllInputs Inputs,
    Eatf2ServerDllSummary Summary,
    Eatf2OfficialSendTable[] OfficialSendTables,
    Eatf2OfficialNetpropCoverage[] OfficialNetprops,
    string[] MissingOfficialSendTables,
    string[] MissingOfficialNetprops,
    string[] MissingGeneratedNativeNetprops,
    string[] NativeReplacementGuidance);

public sealed record Eatf2ServerDllInputs(
    string Datatables,
    string Netprops,
    string GhidraEvidence,
    string SourceFieldContract);

public sealed record Eatf2ServerDllSummary(
    int OfficialDatatableCount,
    int OfficialNetpropCount,
    int RequiredSendTableCount,
    int RequiredSendTablesPresent,
    int RequiredNetpropCount,
    int RequiredNetpropsPresent,
    int RequiredNetpropsPresentInGeneratedNativePayloads,
    int MissingRequiredSendTableCount,
    int MissingRequiredNetpropCount,
    int MissingGeneratedNativeNetpropCount);

public sealed record Eatf2OfficialSendTable(
    string SendTable,
    bool PresentInOfficialDll,
    string EvidenceAddress);

public sealed record Eatf2OfficialNetpropCoverage(
    string SendTable,
    string Property,
    string Role,
    string Semantics,
    bool PresentInOfficialDll,
    string EvidenceAddress,
    bool PresentInGeneratedNativePayloads,
    bool SendTablePresentInGeneratedNativeContract);

public sealed record Eatf2OfficialNetpropRequirement(
    string SendTable,
    string Property,
    string Role,
    string Semantics);
