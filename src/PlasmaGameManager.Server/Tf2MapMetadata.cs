using System.Globalization;
using System.Text.Json;

namespace PlasmaGameManager.Server;

public sealed record Tf2MapMetadata(
    string MapName,
    Tf2MapBounds? Bounds,
    IReadOnlyList<Tf2MapSpawnPoint> SpawnPoints,
    IReadOnlyList<Tf2MapFlag> Flags,
    IReadOnlyList<Tf2MapControlPoint> ControlPoints,
    IReadOnlyList<Tf2MapCaptureArea> CaptureAreas);

public sealed record Tf2MapBounds(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ);

public sealed record Tf2MapSpawnPoint(
    uint TeamNumber,
    float X,
    float Y,
    float Z,
    float Yaw,
    string TargetName,
    bool Enabled);

public sealed record Tf2MapFlag(
    uint TeamNumber,
    float X,
    float Y,
    float Z,
    float Yaw,
    string TargetName);

public sealed record Tf2MapControlPoint(
    uint Index,
    uint DefaultOwnerTeam,
    float X,
    float Y,
    float Z,
    string TargetName,
    string PrintName);

public sealed record Tf2MapCaptureArea(
    string ControlPointTargetName,
    float CapTimeSeconds,
    bool RedCanCap,
    bool BlueCanCap,
    uint RequiredCappersRed,
    uint RequiredCappersBlue,
    string TargetName);

public sealed class Tf2MapMetadataCatalog
{
    private readonly Dictionary<string, Tf2MapMetadata> _maps;

    public Tf2MapMetadataCatalog(IEnumerable<Tf2MapMetadata> maps)
    {
        _maps = maps
            .Where(static map => !string.IsNullOrWhiteSpace(map.MapName))
            .GroupBy(static map => NormalizeMapName(map.MapName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public static Tf2MapMetadataCatalog Empty { get; } = new([]);

    public bool IsEmpty => _maps.Count == 0;

    public int Count => _maps.Count;

    public static Tf2MapMetadataCatalog LoadFromJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Empty;
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        using var document = JsonDocument.Parse(json);
        var maps = document.RootElement.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<Tf2MapMetadata[]>(json, options) ?? []
            : JsonSerializer.Deserialize<Tf2MapMetadataCatalogDocument>(json, options)?.Maps ?? [];
        return new Tf2MapMetadataCatalog(maps);
    }

    public Tf2MapMetadata? Find(string mapName)
    {
        return _maps.TryGetValue(NormalizeMapName(mapName), out var metadata)
            ? metadata
            : null;
    }

    public static string NormalizeMapName(string mapName)
    {
        var name = Path.GetFileName(mapName.Trim()).ToLowerInvariant();
        if (name.EndsWith(".bsp", StringComparison.Ordinal))
        {
            name = name[..^4];
        }

        if (name.EndsWith(".ps3", StringComparison.Ordinal))
        {
            name = name[..^4];
        }

        return name;
    }

    private sealed record Tf2MapMetadataCatalogDocument(Tf2MapMetadata[] Maps);
}

public static class Tf2MapEntityParser
{
    public static Tf2MapMetadata ParseEntityText(string mapName, string entityText)
    {
        var entities = ParseEntities(entityText).ToArray();
        var bounds = ParseBounds(entities);
        var spawns = entities
            .Where(static entity => IsClass(entity, "info_player_teamspawn"))
            .Select(ParseSpawn)
            .Where(static spawn => spawn is not null)
            .Cast<Tf2MapSpawnPoint>()
            .OrderBy(static spawn => spawn.TeamNumber)
            .ThenBy(static spawn => spawn.TargetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static spawn => spawn.X)
            .ThenBy(static spawn => spawn.Y)
            .ToArray();
        var flags = entities
            .Where(static entity => IsClass(entity, "item_teamflag"))
            .Select(ParseFlag)
            .Where(static flag => flag is not null)
            .Cast<Tf2MapFlag>()
            .OrderBy(static flag => flag.TeamNumber)
            .ToArray();
        var controlPoints = entities
            .Where(static entity => IsClass(entity, "team_control_point"))
            .Select(ParseControlPoint)
            .Where(static point => point is not null)
            .Cast<Tf2MapControlPoint>()
            .OrderBy(static point => point.Index)
            .ToArray();
        var captureAreas = entities
            .Where(static entity => IsClass(entity, "trigger_capture_area"))
            .Select(ParseCaptureArea)
            .Where(static area => area is not null)
            .Cast<Tf2MapCaptureArea>()
            .ToArray();
        return new Tf2MapMetadata(mapName, bounds, spawns, flags, controlPoints, captureAreas);
    }

    private static IEnumerable<Dictionary<string, string>> ParseEntities(string entityText)
    {
        var index = 0;
        while (index < entityText.Length)
        {
            var open = entityText.IndexOf('{', index);
            if (open < 0)
            {
                yield break;
            }

            var close = FindEntityClose(entityText, open + 1);
            if (close < 0)
            {
                yield break;
            }

            var entity = ParseKeyValueBlock(entityText.AsSpan(open + 1, close - open - 1));
            if (entity.Count > 0)
            {
                yield return entity;
            }

            index = close + 1;
        }
    }

    private static int FindEntityClose(string text, int index)
    {
        var inQuote = false;
        for (var i = index; i < text.Length; i++)
        {
            if (text[i] == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && text[i] == '}')
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<string, string> ParseKeyValueBlock(ReadOnlySpan<char> block)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (TryReadQuoted(block, ref index, out var key))
        {
            if (!TryReadQuoted(block, ref index, out var value))
            {
                break;
            }

            result[key] = value;
        }

        return result;
    }

    private static bool TryReadQuoted(ReadOnlySpan<char> text, ref int index, out string value)
    {
        value = "";
        while (index < text.Length && text[index] != '"')
        {
            index++;
        }

        if (index >= text.Length)
        {
            return false;
        }

        index++;
        var start = index;
        while (index < text.Length)
        {
            if (text[index] == '"' && (index == start || text[index - 1] != '\\'))
            {
                value = text[start..index].ToString();
                index++;
                return true;
            }

            index++;
        }

        return false;
    }

    private static Tf2MapBounds? ParseBounds(IReadOnlyList<Dictionary<string, string>> entities)
    {
        var world = entities.FirstOrDefault(static entity => IsClass(entity, "worldspawn"));
        if (world is null
            || !TryVector(world.GetValueOrDefault("world_mins"), out var mins)
            || !TryVector(world.GetValueOrDefault("world_maxs"), out var maxs))
        {
            return null;
        }

        return new Tf2MapBounds(mins.X, mins.Y, mins.Z, maxs.X, maxs.Y, maxs.Z);
    }

    private static Tf2MapSpawnPoint? ParseSpawn(Dictionary<string, string> entity)
    {
        if (!TryVector(entity.GetValueOrDefault("origin"), out var origin))
        {
            return null;
        }

        var team = ParseTeam(entity.GetValueOrDefault("TeamNum") ?? entity.GetValueOrDefault("team_no"));
        return new Tf2MapSpawnPoint(
            team,
            origin.X,
            origin.Y,
            origin.Z,
            ParseYaw(entity.GetValueOrDefault("angles")),
            entity.GetValueOrDefault("targetname") ?? "",
            !string.Equals(entity.GetValueOrDefault("StartDisabled"), "1", StringComparison.Ordinal));
    }

    private static Tf2MapFlag? ParseFlag(Dictionary<string, string> entity)
    {
        if (!TryVector(entity.GetValueOrDefault("origin"), out var origin))
        {
            return null;
        }

        var team = ParseTeam(entity.GetValueOrDefault("TeamNum") ?? entity.GetValueOrDefault("team_no"));
        return new Tf2MapFlag(
            team,
            origin.X,
            origin.Y,
            origin.Z,
            ParseYaw(entity.GetValueOrDefault("angles")),
            entity.GetValueOrDefault("targetname") ?? "");
    }

    private static Tf2MapControlPoint? ParseControlPoint(Dictionary<string, string> entity)
    {
        if (!TryVector(entity.GetValueOrDefault("origin"), out var origin))
        {
            return null;
        }

        var index = ParseUInt(entity.GetValueOrDefault("point_index"), 0);
        return new Tf2MapControlPoint(
            index,
            ParseTeam(entity.GetValueOrDefault("point_default_owner")),
            origin.X,
            origin.Y,
            origin.Z,
            entity.GetValueOrDefault("targetname") ?? "",
            entity.GetValueOrDefault("point_printname") ?? "");
    }

    private static Tf2MapCaptureArea? ParseCaptureArea(Dictionary<string, string> entity)
    {
        var pointTarget = entity.GetValueOrDefault("area_cap_point") ?? "";
        if (string.IsNullOrWhiteSpace(pointTarget))
        {
            return null;
        }

        return new Tf2MapCaptureArea(
            pointTarget,
            ParseFloat(entity.GetValueOrDefault("area_time_to_cap"), 4.0f),
            !string.Equals(entity.GetValueOrDefault("team_cancap_2"), "0", StringComparison.Ordinal),
            !string.Equals(entity.GetValueOrDefault("team_cancap_3"), "0", StringComparison.Ordinal),
            Math.Max(1, ParseUInt(entity.GetValueOrDefault("team_numcap_2"), 1)),
            Math.Max(1, ParseUInt(entity.GetValueOrDefault("team_numcap_3"), 1)),
            entity.GetValueOrDefault("targetname") ?? "");
    }

    private static bool IsClass(Dictionary<string, string> entity, string className)
    {
        return string.Equals(entity.GetValueOrDefault("classname"), className, StringComparison.OrdinalIgnoreCase);
    }

    private static uint ParseTeam(string? value)
    {
        return ParseUInt(value, 0);
    }

    private static uint ParseUInt(string? value, uint fallback)
    {
        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static float ParseFloat(string? value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static float ParseYaw(string? angles)
    {
        if (string.IsNullOrWhiteSpace(angles))
        {
            return 0.0f;
        }

        var parts = angles.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? ParseFloat(parts[1], 0.0f) : 0.0f;
    }

    private static bool TryVector(string? value, out (float X, float Y, float Z) vector)
    {
        vector = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        vector = (
            ParseFloat(parts[0], 0.0f),
            ParseFloat(parts[1], 0.0f),
            ParseFloat(parts[2], 0.0f));
        return true;
    }
}
