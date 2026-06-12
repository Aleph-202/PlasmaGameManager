using System.Text;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public sealed class PcapEaTextAnalyzer
{
    private static readonly string[] KnownMarkers =
    [
        "fsys", "acct", "pnow", "rank", "recp", "asso", "pres", "xmsg", "mtrx",
        "CONN", "USER", "LLST", "LDAT", "GLST", "GDAT", "GDET", "CGAM", "UGAM", "UGDE",
        "EGAM", "EGEG", "EGRQ", "EGRS", "ECNL", "PENT", "UBRA", "RGAM", "PLVT", "PING", "PCNT", "UPLA"
    ];

    public async Task AnalyzeDirectoryAsync(string inputDirectory, string outputPath)
    {
        var files = Directory.EnumerateFiles(inputDirectory, "*.*", SearchOption.AllDirectories)
            .Where(static p => p.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static p => p, StringComparer.Ordinal)
            .Select(file => AnalyzeFile(inputDirectory, file))
            .Where(static file => file.PacketCount > 0)
            .ToArray();

        var summary = new PcapEaTextCorpusSummary(
            files.Length,
            files.Sum(static file => file.PacketCount),
            files.SelectMany(static file => file.Commands)
                .GroupBy(static command => command, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal),
            files.SelectMany(static file => file.Tf2PreferenceMappings)
                .GroupBy(static mapping => $"{mapping.PreferenceName}->{mapping.GameField}", StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new PcapEaTextPreferenceMappingCount(
                    group.First().PreferenceName,
                    group.First().GameField,
                    group.Count()))
                .ToArray(),
            files.SelectMany(static file => file.Tf2GameDataKeys)
                .GroupBy(static key => key, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new PcapEaTextKeyCount(group.Key, group.Count()))
                .ToArray(),
            files.SelectMany(static file => file.Tf2CreateScenarios)
                .GroupBy(static scenario => scenario.MapName, StringComparer.Ordinal)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => new PcapEaTextCreateMapCount(group.Key, group.Count()))
                .ToArray(),
            files.Count(static file => file.Tf2CreateScenarios.Length > 0),
            files.SelectMany(static file => file.Tf2CreateScenarios).Count(static scenario => scenario.JoinHandoffObserved));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new PcapEaTextCorpusReport(summary, files), new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static PcapEaTextFileReport AnalyzeFile(string inputDirectory, string file)
    {
        var packets = CaptureUdpPacketParser.ReadTransportPackets(file)
            .Where(static packet => packet.Payload.Length > 0)
            .SelectMany(ExtractPackets)
            .ToArray();
        var preferenceMappings = packets
            .SelectMany(static packet => ExtractPreferenceMappings(packet.Fields))
            .Distinct()
            .OrderBy(static mapping => mapping.PreferenceName, StringComparer.Ordinal)
            .ThenBy(static mapping => mapping.GameField, StringComparer.Ordinal)
            .ToArray();
        var gameDataKeys = packets
            .SelectMany(static packet => packet.Fields.Keys)
            .Where(static key => key.StartsWith("B-U-", StringComparison.Ordinal) || key is "V" or "B-version")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var relativeFile = CanonicalRelativeFile(Path.GetRelativePath(inputDirectory, file));
        return new PcapEaTextFileReport(
            relativeFile,
            packets.Length,
            packets.Select(static packet => packet.Command).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            preferenceMappings,
            gameDataKeys,
            ExtractTf2CreateScenarios(relativeFile, packets),
            packets);
    }

    private static string CanonicalRelativeFile(string relativeFile)
    {
        var normalized = relativeFile.Replace('\\', '/');
        const string serverCorpusPrefix = "TF2_PS3_network_traffic/packets/server/";
        return normalized.StartsWith(serverCorpusPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[serverCorpusPrefix.Length..]
            : normalized;
    }

    private static IEnumerable<PcapEaTextPacket> ExtractPackets(CaptureTransportPacket transport)
    {
        var payload = transport.Payload;
        foreach (var marker in KnownMarkers)
        {
            var markerBytes = Encoding.ASCII.GetBytes(marker);
            var searchStart = 0;
            while (searchStart <= payload.Length - markerBytes.Length)
            {
                var markerOffset = IndexOf(payload, markerBytes, searchStart);
                if (markerOffset < 0)
                {
                    break;
                }

                searchStart = markerOffset + 1;
                if (!LooksLikeEaHeader(payload, markerOffset, markerBytes.Length))
                {
                    continue;
                }

                var dataOffset = markerOffset + 12;
                var dataLength = ResolveEaPayloadLength(payload, markerOffset, dataOffset);
                if (dataLength <= 0)
                {
                    continue;
                }

                var text = Encoding.ASCII.GetString(payload, dataOffset, dataLength)
                    .TrimEnd('\0');
                var fields = ParseFields(text);
                if (fields.Count == 0)
                {
                    continue;
                }

                yield return new PcapEaTextPacket(
                    transport.PacketIndex,
                    transport.TimestampMicroseconds,
                    transport.Protocol,
                    $"{transport.SourceAddress}:{transport.SourcePort}",
                    $"{transport.DestinationAddress}:{transport.DestinationPort}",
                    marker,
                    markerOffset,
                    text.Length,
                    fields.GetValueOrDefault("TXN") ?? "",
                    fields);
            }
        }
    }

    private static bool LooksLikeEaHeader(byte[] payload, int markerOffset, int markerLength)
    {
        if (markerOffset + 12 > payload.Length)
        {
            return false;
        }

        if (markerOffset > 0 && IsAsciiWordByte(payload[markerOffset - 1]))
        {
            return false;
        }

        var header = payload.AsSpan(markerOffset + markerLength, 8);
        var zeroCount = 0;
        foreach (var value in header)
        {
            if (value == 0)
            {
                zeroCount++;
            }
        }

        return zeroCount >= 2;
    }

    private static int ResolveEaPayloadLength(byte[] payload, int markerOffset, int dataOffset)
    {
        var available = payload.Length - dataOffset;
        if (available <= 0)
        {
            return 0;
        }

        var lengthA = ReadUInt32BigEndian(payload.AsSpan(markerOffset + 4, 4)) - 12;
        var lengthB = ReadUInt32BigEndian(payload.AsSpan(markerOffset + 8, 4)) - 12;
        foreach (var candidate in new[] { lengthA, lengthB })
        {
            if (candidate > 0 && candidate <= available)
            {
                return (int)candidate;
            }
        }

        var nextMarker = KnownMarkers
            .Select(marker => IndexOf(payload, Encoding.ASCII.GetBytes(marker), dataOffset + 1))
            .Where(index => index > dataOffset)
            .DefaultIfEmpty(payload.Length)
            .Min();
        return Math.Max(0, nextMarker - dataOffset);
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = token[..separator];
            var value = token[(separator + 1)..].Trim('"');
            if (key.Length > 0 && IsMostlyFieldKey(key))
            {
                fields[key] = value;
            }
        }

        return fields;
    }

    private static IEnumerable<PcapEaTextPreferenceMapping> ExtractPreferenceMappings(IReadOnlyDictionary<string, string> fields)
    {
        const string preferencePrefix = "players.0.props.{prefToGame-";
        const string filterPrefix = "players.0.props.{filterToGame-";
        foreach (var (key, value) in fields)
        {
            if (key.StartsWith(preferencePrefix, StringComparison.Ordinal) && key.EndsWith('}'))
            {
                yield return new PcapEaTextPreferenceMapping(key[preferencePrefix.Length..^1], value, "preference");
            }
            else if (key.StartsWith(filterPrefix, StringComparison.Ordinal) && key.EndsWith('}'))
            {
                yield return new PcapEaTextPreferenceMapping(key[filterPrefix.Length..^1], value, "filter");
            }
        }
    }

    private static PcapEaTextCreateScenario[] ExtractTf2CreateScenarios(string relativeFile, PcapEaTextPacket[] packets)
    {
        if (!relativeFile.Contains("creation/", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return packets
            .Where(static packet => packet.Command == "GDAT" && packet.Fields.ContainsKey("B-U-MapName"))
            .Select(packet =>
            {
                var fields = packet.Fields;
                var gid = fields.GetValueOrDefault("GID") ?? string.Empty;
                var egeg = packets.FirstOrDefault(candidate =>
                    candidate.Command == "EGEG"
                    && candidate.Fields.GetValueOrDefault("GID") == gid);
                var gdet = packets.FirstOrDefault(candidate =>
                    candidate.Command == "GDET"
                    && candidate.Fields.GetValueOrDefault("GID") == gid);

                var scenario = new PcapEaTextCreateScenario(
                    packet.PacketIndex,
                    gid,
                    fields.GetValueOrDefault("LID") ?? string.Empty,
                    fields.GetValueOrDefault("N") ?? string.Empty,
                    fields.GetValueOrDefault("HN") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-MapName") ?? string.Empty,
                    fields.GetValueOrDefault("MP") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-IsRanked") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-Rating") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-Duration") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-FlagCapture") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-Location") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-MaxGameTime") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-NumRounds") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-ServerPop") ?? string.Empty,
                    fields.GetValueOrDefault("B-U-AutoBalance") ?? string.Empty,
                    fields.GetValueOrDefault("V") ?? string.Empty,
                    fields.GetValueOrDefault("I") ?? string.Empty,
                    fields.GetValueOrDefault("P") ?? string.Empty,
                    fields.GetValueOrDefault("HU") ?? string.Empty,
                    gdet?.Fields.GetValueOrDefault("UGID") ?? string.Empty,
                    egeg is not null,
                    egeg?.Fields.GetValueOrDefault("TICKET") ?? string.Empty,
                    egeg?.Fields.GetValueOrDefault("EKEY") ?? string.Empty,
                    egeg?.Fields.GetValueOrDefault("PID") ?? string.Empty,
                    egeg?.Fields.GetValueOrDefault("INT-IP") ?? string.Empty,
                    egeg?.Fields.GetValueOrDefault("INT-PORT") ?? string.Empty,
                    null!);

                return scenario with
                {
                    SourceLaunchProfile = BuildTf2SourceLaunchProfile(scenario)
                };
            })
            .ToArray();
    }

    private static PcapEaTextTf2SourceLaunchProfile BuildTf2SourceLaunchProfile(PcapEaTextCreateScenario scenario)
    {
        var mapName = scenario.MapName;
        var mode = mapName.StartsWith("cp_", StringComparison.Ordinal)
            ? "control-point"
            : mapName.StartsWith("ctf_", StringComparison.Ordinal)
                ? "capture-the-flag"
                : "unknown";
        var backendHost = scenario.JoinInternalHost.Length > 0 ? scenario.JoinInternalHost : scenario.BackendHost;
        var backendPort = scenario.JoinInternalPort.Length > 0 ? scenario.JoinInternalPort : scenario.GamePort;
        var autoBalance = ParseYesNo(scenario.AutoBalance);
        var ranked = scenario.IsRanked.Equals("Ranked", StringComparison.OrdinalIgnoreCase);
        var sourceArguments = new List<string>
        {
            "-game",
            "tf",
            "+map",
            mapName,
            "+maxplayers",
            scenario.MaxPlayers
        };

        if (!string.IsNullOrWhiteSpace(backendHost))
        {
            sourceArguments.Add("-ip");
            sourceArguments.Add(backendHost);
        }

        if (!string.IsNullOrWhiteSpace(backendPort))
        {
            sourceArguments.Add("-port");
            sourceArguments.Add(backendPort);
        }

        if (TryParseInt(scenario.MaxGameTime, out var maxGameTime))
        {
            sourceArguments.Add("+mp_timelimit");
            sourceArguments.Add(maxGameTime.ToString());
        }

        if (TryParseInt(scenario.NumRounds, out var numRounds))
        {
            sourceArguments.Add("+mp_maxrounds");
            sourceArguments.Add(numRounds.ToString());
        }

        if (autoBalance is not null)
        {
            sourceArguments.Add("+mp_autoteambalance");
            sourceArguments.Add(autoBalance.Value ? "1" : "0");
        }

        if (mode == "capture-the-flag" && TryParseInt(scenario.FlagCapture, out var flagCaptureLimit))
        {
            sourceArguments.Add("+tf_flag_caps_per_round");
            sourceArguments.Add(flagCaptureLimit.ToString());
        }

        if (!string.IsNullOrWhiteSpace(scenario.ServerName))
        {
            sourceArguments.Add("+hostname");
            sourceArguments.Add(scenario.ServerName);
        }

        return new PcapEaTextTf2SourceLaunchProfile(
            mapName,
            mode,
            scenario.ServerName,
            backendHost,
            backendPort,
            TryParseInt(scenario.MaxPlayers, out var maxPlayers) ? maxPlayers : null,
            ranked,
            scenario.IsRanked,
            TryParseInt(scenario.MaxGameTime, out maxGameTime) ? maxGameTime : null,
            TryParseInt(scenario.NumRounds, out numRounds) ? numRounds : null,
            TryParseInt(scenario.FlagCapture, out flagCaptureLimit) ? flagCaptureLimit : null,
            autoBalance,
            scenario.Duration,
            scenario.ServerPop,
            scenario.Version,
            sourceArguments.ToArray(),
            TryParseLong(scenario.LocalId, out var localId) ? localId : null,
            TryParseLong(scenario.GameId, out var gameId) ? gameId : null,
            TryParseInt(scenario.JoinPlayerId, out var preferredPlayerId) ? preferredPlayerId : null,
            scenario.JoinTicket,
            scenario.JoinEncryptionKey,
            scenario.UserGameId,
            TryParseUlong(scenario.HostUserId, out var serverUid) ? serverUid : null);
    }

    private static bool? ParseYesNo(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "yes" or "true" or "1" => true,
            "no" or "false" or "0" => false,
            _ => null
        };
    }

    private static bool TryParseInt(string value, out int parsed)
    {
        return int.TryParse(value, out parsed);
    }

    private static bool TryParseLong(string value, out long parsed)
    {
        return long.TryParse(value, out parsed);
    }

    private static bool TryParseUlong(string value, out ulong parsed)
    {
        return ulong.TryParse(value, out parsed);
    }

    private static bool IsMostlyFieldKey(string value)
    {
        return value.All(static c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or '{' or '}' or '[' or ']');
    }

    private static bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z'
            or >= (byte)'a' and <= (byte)'z'
            or >= (byte)'0' and <= (byte)'9'
            or (byte)'_' or (byte)'-';
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }

        return -1;
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> data)
    {
        return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
    }
}

public sealed record PcapEaTextCorpusReport(
    PcapEaTextCorpusSummary Summary,
    PcapEaTextFileReport[] Files);

public sealed record PcapEaTextCorpusSummary(
    int FileCount,
    int PacketCount,
    IReadOnlyDictionary<string, int> CommandCounts,
    PcapEaTextPreferenceMappingCount[] Tf2PreferenceMappingCounts,
    PcapEaTextKeyCount[] Tf2GameDataKeyCounts,
    PcapEaTextCreateMapCount[] Tf2CreateMapCounts,
    int Tf2CreateScenarioFileCount,
    int Tf2CreateJoinHandoffCount);

public sealed record PcapEaTextFileReport(
    string File,
    int PacketCount,
    string[] Commands,
    PcapEaTextPreferenceMapping[] Tf2PreferenceMappings,
    string[] Tf2GameDataKeys,
    PcapEaTextCreateScenario[] Tf2CreateScenarios,
    PcapEaTextPacket[] Packets);

public sealed record PcapEaTextPacket(
    long PacketIndex,
    long TimestampMicroseconds,
    string Transport,
    string Source,
    string Destination,
    string Command,
    int MarkerOffset,
    int TextLength,
    string Transaction,
    IReadOnlyDictionary<string, string> Fields);

public sealed record PcapEaTextPreferenceMapping(
    string PreferenceName,
    string GameField,
    string Kind);

public sealed record PcapEaTextPreferenceMappingCount(
    string PreferenceName,
    string GameField,
    int Count);

public sealed record PcapEaTextKeyCount(
    string Key,
    int Count);

public sealed record PcapEaTextCreateMapCount(
    string MapName,
    int Count);

public sealed record PcapEaTextCreateScenario(
    long ListingPacketIndex,
    string GameId,
    string LocalId,
    string ServerName,
    string HostName,
    string MapName,
    string MaxPlayers,
    string IsRanked,
    string Rating,
    string Duration,
    string FlagCapture,
    string Location,
    string MaxGameTime,
    string NumRounds,
    string ServerPop,
    string AutoBalance,
    string Version,
    string BackendHost,
    string GamePort,
    string HostUserId,
    string UserGameId,
    bool JoinHandoffObserved,
    string JoinTicket,
    string JoinEncryptionKey,
    string JoinPlayerId,
    string JoinInternalHost,
    string JoinInternalPort,
    PcapEaTextTf2SourceLaunchProfile SourceLaunchProfile);

public sealed record PcapEaTextTf2SourceLaunchProfile(
    string MapName,
    string GameMode,
    string HostName,
    string BackendHost,
    string BackendPort,
    int? MaxPlayers,
    bool IsRanked,
    string RankingMode,
    int? TimeLimitMinutes,
    int? MaxRounds,
    int? FlagCaptureLimit,
    bool? AutoBalance,
    string DurationPreset,
    string ServerPopulation,
    string Version,
    string[] SourceArguments,
    long? LocalId = null,
    long? GameId = null,
    int? PreferredPlayerId = null,
    string? JoinTicket = null,
    string? EncryptionKey = null,
    string? UniqueGameId = null,
    ulong? ServerUid = null);
