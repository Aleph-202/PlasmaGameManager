using System.Globalization;
using System.Text.Json;

namespace PlasmaGameManager.Server;

public static class Tf2RankedStatsExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryAppend(string? path, GameManagerSession game, PlayerSession player, string reason)
    {
        if (string.IsNullOrWhiteSpace(path) || !game.IsRanked || player.IsBot)
        {
            return false;
        }

        var stats = BuildStats(game, player);
        if (stats.Count == 0)
        {
            return false;
        }

        var record = new Tf2RankedStatsExportRecord(
            DateTimeOffset.UtcNow,
            game.GameId,
            game.LocalId,
            game.MapName,
            game.IsRanked,
            game.RankingMode,
            string.IsNullOrWhiteSpace(player.Name) ? game.PreferredPlayerName : player.Name,
            "ps3",
            "HL2",
            reason,
            stats);

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        File.AppendAllText(fullPath, JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine);
        return true;
    }

    public static IReadOnlyDictionary<string, string> BuildStats(GameManagerSession game, PlayerSession player)
    {
        var source = player.SourceState;
        var prefix = ClassPrefix(source.ClassNumber);
        if (prefix.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var playSeconds = checked((uint)Math.Clamp(MathF.Round(source.SimulationTime), 0, uint.MaxValue));
        var kills = EstimatedKills(source);
        var damage = EstimatedDamage(source);

        var stats = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TP"] = U(source.Score),
            ["TC"] = U(source.Captures),
            ["TR"] = "0",
            [$"{prefix}P"] = U(source.Score),
            [$"{prefix}T01"] = U(playSeconds),
            [$"{prefix}T02"] = U(kills),
            [$"{prefix}T03"] = U(source.KillAssists),
            [$"{prefix}T04"] = U(source.Captures),
            [$"{prefix}T05"] = U(source.Defenses),
            [$"{prefix}T06"] = U(damage),
            [$"{prefix}T07"] = U(source.Dominations),
            [$"{prefix}T08"] = U(source.Revenge),
            [$"{prefix}T09"] = U(source.Invulns),
            [$"{prefix}T10"] = U(source.Teleports),
            [$"{prefix}T11"] = U(source.BuildingsDestroyed),
            [$"{prefix}T12"] = U(source.Headshots),
            [$"{prefix}T13"] = U(source.Backstabs),
            [$"{prefix}T14"] = U(source.HealPoints),
            [$"{prefix}T15"] = U(source.ResupplyPoints),
            [$"{prefix}T16"] = U(source.Deaths),
            [$"{prefix}T17"] = U(source.Score),
            [$"{prefix}T18"] = U(source.PrimaryAmmo),
            [$"{prefix}T19"] = U(source.Metal),
            [$"{prefix}T20"] = U(source.SentryGun.UpgradeLevel),
            [$"{prefix}T21"] = U(source.Teleporter.TimesUsed),
            [$"{prefix}M01"] = U(source.Score),
            [$"{prefix}M02"] = U(kills),
            [$"{prefix}M03"] = U(source.KillAssists),
            [$"{prefix}M04"] = U(source.Captures),
            [$"{prefix}M05"] = U(source.Defenses),
            [$"{prefix}M06"] = U(damage),
            [$"{prefix}M07"] = U(source.Dominations),
            [$"{prefix}M08"] = U(source.Revenge),
            [$"{prefix}M09"] = U(source.Invulns),
            [$"{prefix}M10"] = U(source.Teleports),
            [$"{prefix}M11"] = U(source.BuildingsDestroyed),
            [$"{prefix}M12"] = U(source.Headshots),
            [$"{prefix}M13"] = U(source.Backstabs),
            [$"{prefix}M14"] = U(source.HealPoints),
            [$"{prefix}M15"] = U(source.ResupplyPoints),
            [$"{prefix}M16"] = U(source.Deaths),
            [$"{prefix}M17"] = U(source.Score),
            [$"{prefix}M18"] = U(source.PrimaryAmmo),
            [$"{prefix}M19"] = U(source.Metal),
            [$"{prefix}M20"] = U(source.SentryGun.UpgradeLevel),
            [$"{prefix}M21"] = U(source.Teleporter.TimesUsed),
        };

        return stats;
    }

    public static bool IsMaxStatKey(string key)
    {
        return key.Length == 5
            && key[2] == 'M'
            && IsClassPrefix(key.AsSpan(0, 2));
    }

    public static bool IsAdditiveStatKey(string key)
    {
        if (key is "TP" or "TC")
        {
            return true;
        }

        return key.Length == 5
            && key[2] == 'T'
            && IsClassPrefix(key.AsSpan(0, 2));
    }

    private static uint EstimatedKills(Tf2SourcePlayerState source)
    {
        return Math.Max(source.Score / 2U, source.Headshots + source.Backstabs);
    }

    private static uint EstimatedDamage(Tf2SourcePlayerState source)
    {
        return checked((source.Score * 25U)
            + (source.BuildingsDestroyed * 75U)
            + (source.Headshots * 50U)
            + (source.Backstabs * 50U));
    }

    private static string ClassPrefix(byte classNumber)
    {
        return classNumber switch
        {
            1 => "Sc",
            2 => "Sn",
            3 => "So",
            4 => "De",
            5 => "Me",
            6 => "He",
            7 => "Py",
            8 => "Sp",
            9 => "En",
            _ => ""
        };
    }

    private static bool IsClassPrefix(ReadOnlySpan<char> prefix)
    {
        return prefix is "Sc" or "Sn" or "So" or "De" or "Me" or "He" or "Py" or "Sp" or "En";
    }

    private static string U(uint value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed record Tf2RankedStatsExportRecord(
    DateTimeOffset Timestamp,
    long GameId,
    long LocalId,
    string MapName,
    bool IsRanked,
    string RankingMode,
    string Username,
    string Platform,
    string Subdomain,
    string Reason,
    IReadOnlyDictionary<string, string> Stats);
