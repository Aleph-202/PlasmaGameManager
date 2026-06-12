using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public sealed class LiveSourceTurnContractMatcher
{
    public async Task<LiveSourceTurnContractMatchReport> MatchAsync(
        string eventLogPath,
        string contractPath,
        string outputPath)
    {
        var report = Match(eventLogPath, contractPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public LiveSourceTurnContractMatchReport Match(string eventLogPath, string contractPath)
    {
        var contract = JsonSerializer.Deserialize<PcapSourceTurnContractReport>(File.ReadAllText(contractPath))
            ?? throw new InvalidOperationException($"Unable to read source turn contract: {contractPath}");
        var candidates = BuildCandidateMap(contract);
        var shapeCandidates = BuildShapeCandidateMap(contract);
        var events = ReadEvents(eventLogPath);
        var turns = BuildLiveTurns(events);
        var matches = turns.Select(turn => MatchTurn(turn, candidates, shapeCandidates)).ToArray();
        var exact = matches.Count(static match => match.MatchStatus == "matched");
        var shapeMatched = matches.Count(static match => match.MatchStatus == "shape-matched");
        var ambiguous = matches.Count(static match => match.MatchStatus is "ambiguous" or "shape-ambiguous");
        var unmatched = matches.Count(static match => match.MatchStatus == "unmatched");

        return new LiveSourceTurnContractMatchReport(
            "live-source-turn-contract-match",
            eventLogPath,
            contractPath,
            contract.Summary.TurnCount,
            candidates.Count,
            turns.Length,
            exact,
            shapeMatched,
            ambiguous,
            unmatched,
            unmatched == 0 && ambiguous == 0 && turns.Length > 0
                ? (exact == turns.Length ? "matched" : "shape-matched")
                : "needs-investigation",
            matches);
    }

    private static Dictionary<string, PcapSourceTurnContractCandidate[]> BuildCandidateMap(PcapSourceTurnContractReport contract)
    {
        return contract.Files
            .Where(static file => file.HasActiveSourceFlow)
            .SelectMany(static file => file.SampleTurns.Select(turn => new PcapSourceTurnContractCandidate(
                file.File,
                turn.TurnIndex,
                turn.ClientPacketCount,
                turn.ServerResponsePacketCount,
                turn.ClientBodySignature,
                turn.ServerBodySignature,
                turn.TurnBodySignature,
                turn.ClientShapeSignature,
                turn.ServerShapeSignature,
                turn.ClientPacketBodySignatures,
                turn.ServerResponseBodySignatures)))
            .GroupBy(static candidate => SignatureKey(candidate.ClientPacketBodySignatures), StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToArray(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, PcapSourceTurnContractCandidate[]> BuildShapeCandidateMap(PcapSourceTurnContractReport contract)
    {
        return contract.Files
            .Where(static file => file.HasActiveSourceFlow)
            .SelectMany(static file => file.SampleTurns.Select(turn => new PcapSourceTurnContractCandidate(
                file.File,
                turn.TurnIndex,
                turn.ClientPacketCount,
                turn.ServerResponsePacketCount,
                turn.ClientBodySignature,
                turn.ServerBodySignature,
                turn.TurnBodySignature,
                turn.ClientShapeSignature,
                turn.ServerShapeSignature,
                turn.ClientPacketBodySignatures,
                turn.ServerResponseBodySignatures)))
            .GroupBy(static candidate => candidate.ClientShapeSignature, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToArray(),
                StringComparer.Ordinal);
    }

    private static LiveSourceTurnContractMatch MatchTurn(
        LiveSourceTurn turn,
        IReadOnlyDictionary<string, PcapSourceTurnContractCandidate[]> candidates,
        IReadOnlyDictionary<string, PcapSourceTurnContractCandidate[]> shapeCandidates)
    {
        var key = SignatureKey(turn.ClientPacketBodySignatures);
        if (!candidates.TryGetValue(key, out var matches))
        {
            if (turn.ClientShapeSignature.Length > 0
                && shapeCandidates.TryGetValue(turn.ClientShapeSignature, out var shapeMatches))
            {
                var shapeStatus = shapeMatches.Length == 1 ? "shape-matched" : "shape-ambiguous";
                return new LiveSourceTurnContractMatch(
                    turn.TurnIndex,
                    turn.ClientPacketCount,
                    turn.ServerResponsePacketCount,
                    turn.FirstEventIndex,
                    turn.LastEventIndex,
                    shapeStatus,
                    "",
                    turn.ClientShapeSignature,
                    turn.ServerShapeSignature,
                    shapeMatches[0],
                    shapeMatches.Length == 1 ? Array.Empty<PcapSourceTurnContractCandidate>() : shapeMatches);
            }

            return new LiveSourceTurnContractMatch(
                turn.TurnIndex,
                turn.ClientPacketCount,
                turn.ServerResponsePacketCount,
                turn.FirstEventIndex,
                turn.LastEventIndex,
                "unmatched",
                "",
                turn.ClientShapeSignature,
                turn.ServerShapeSignature,
                null,
                Array.Empty<PcapSourceTurnContractCandidate>());
        }

        var status = matches.Length == 1 ? "matched" : "ambiguous";
        return new LiveSourceTurnContractMatch(
            turn.TurnIndex,
            turn.ClientPacketCount,
            turn.ServerResponsePacketCount,
            turn.FirstEventIndex,
            turn.LastEventIndex,
            status,
            matches[0].ServerBodySignature,
            turn.ClientShapeSignature,
            turn.ServerShapeSignature,
            matches[0],
            matches.Length == 1 ? Array.Empty<PcapSourceTurnContractCandidate>() : matches);
    }

    private static LiveSourceTurn[] BuildLiveTurns(LiveSourceEvent[] events)
    {
        var preferProxyForward = events.Any(static e => e.Event == "source-proxy-forward");
        var clientEvents = preferProxyForward
            ? new HashSet<string>(["source-proxy-forward"], StringComparer.Ordinal)
            : new HashSet<string>(["source-traffic"], StringComparer.Ordinal);
        var serverEvents = new HashSet<string>(["source-proxy-send", "source-send", "source-generated-send"], StringComparer.Ordinal);
        var turns = new List<LiveSourceTurn>();
        var clientSignatures = new List<string>();
        var serverSignatures = new List<string>();
        var clientShapes = new List<(string Shape, int BodyLength)>();
        var serverShapes = new List<(string Shape, int BodyLength)>();
        var turnIndex = 0;
        var firstEventIndex = -1;
        var lastEventIndex = -1;
        var sawServerForCurrentTurn = false;

        foreach (var item in events)
        {
            if (item.SourceBodySignature.Length == 0)
            {
                continue;
            }

            if (clientEvents.Contains(item.Event))
            {
                if (clientSignatures.Count > 0 && sawServerForCurrentTurn)
                {
                    turns.Add(new LiveSourceTurn(
                        turnIndex++,
                        clientSignatures.Count,
                        serverSignatures.Count,
                        firstEventIndex,
                        lastEventIndex,
                        ShapeRunSignature(clientShapes),
                        ShapeRunSignature(serverShapes),
                        clientSignatures.ToArray(),
                        serverSignatures.ToArray()));
                    clientSignatures.Clear();
                    serverSignatures.Clear();
                    clientShapes.Clear();
                    serverShapes.Clear();
                    sawServerForCurrentTurn = false;
                    firstEventIndex = -1;
                }

                if (firstEventIndex < 0)
                {
                    firstEventIndex = item.EventIndex;
                }

                clientSignatures.Add(item.SourceBodySignature);
                if (item.SourcePacketShape.Length > 0 && item.SourceBodyLength >= 0)
                {
                    clientShapes.Add((item.SourcePacketShape, item.SourceBodyLength));
                }

                lastEventIndex = item.EventIndex;
                continue;
            }

            if (serverEvents.Contains(item.Event) && clientSignatures.Count > 0)
            {
                serverSignatures.Add(item.SourceBodySignature);
                if (item.SourcePacketShape.Length > 0 && item.SourceBodyLength >= 0)
                {
                    serverShapes.Add((item.SourcePacketShape, item.SourceBodyLength));
                }

                sawServerForCurrentTurn = true;
                lastEventIndex = item.EventIndex;
            }
        }

        if (clientSignatures.Count > 0)
        {
            turns.Add(new LiveSourceTurn(
                turnIndex,
                clientSignatures.Count,
                serverSignatures.Count,
                firstEventIndex,
                lastEventIndex,
                ShapeRunSignature(clientShapes),
                ShapeRunSignature(serverShapes),
                clientSignatures.ToArray(),
                serverSignatures.ToArray()));
        }

        return turns.ToArray();
    }

    private static LiveSourceEvent[] ReadEvents(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<LiveSourceEvent>();
        }

        var result = new List<LiveSourceEvent>();
        var index = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            result.Add(new LiveSourceEvent(
                index++,
                ReadString(root, "Event"),
                ReadString(root, "Endpoint"),
                ReadString(root, "SourceBodySignature"),
                ReadString(root, "SourcePacketShape"),
                ReadInt(root, "SourceBodyLength")));
        }

        return result.ToArray();
    }

    private static string ShapeRunSignature(IReadOnlyList<(string Shape, int BodyLength)> packets)
    {
        if (packets.Count == 0)
        {
            return "";
        }

        var bytes = new List<byte>(packets.Count * 8);
        foreach (var (shape, bodyLength) in packets)
        {
            AppendInt32(bytes, ShapeValue(shape));
            AppendInt32(bytes, bodyLength);
        }

        return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
    }

    private static int ShapeValue(string shape)
    {
        return shape switch
        {
            "Invalid" => 0,
            "ClassicConnectionless" => 1,
            "ShortControl" => 2,
            "MediumBinary" => 3,
            "LargeBinary" => 4,
            "NearMtuFragment" => 5,
            "HighEntropyBinary" => 6,
            _ => 0
        };
    }

    private static void AppendInt32(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        bytes.AddRange(buffer);
    }

    private static string SignatureKey(IReadOnlyList<string> signatures)
    {
        return string.Join('\n', signatures);
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.ToString() : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : -1;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

internal sealed record LiveSourceEvent(
    int EventIndex,
    string Event,
    string Endpoint,
    string SourceBodySignature,
    string SourcePacketShape,
    int SourceBodyLength);

public sealed record LiveSourceTurn(
    int TurnIndex,
    int ClientPacketCount,
    int ServerResponsePacketCount,
    int FirstEventIndex,
    int LastEventIndex,
    string ClientShapeSignature,
    string ServerShapeSignature,
    string[] ClientPacketBodySignatures,
    string[] ServerResponseBodySignatures);

public sealed record PcapSourceTurnContractCandidate(
    string File,
    int TurnIndex,
    int ClientPacketCount,
    int ServerResponsePacketCount,
    string ClientBodySignature,
    string ServerBodySignature,
    string TurnBodySignature,
    string ClientShapeSignature,
    string ServerShapeSignature,
    string[] ClientPacketBodySignatures,
    string[] ServerResponseBodySignatures);

public sealed record LiveSourceTurnContractMatch(
    int LiveTurnIndex,
    int ClientPacketCount,
    int ServerResponsePacketCount,
    int FirstEventIndex,
    int LastEventIndex,
    string MatchStatus,
    string ExpectedServerBodySignature,
    string LiveClientShapeSignature,
    string LiveServerShapeSignature,
    PcapSourceTurnContractCandidate? BestMatch,
    PcapSourceTurnContractCandidate[] AmbiguousMatches);

public sealed record LiveSourceTurnContractMatchReport(
    string Status,
    string EventLogPath,
    string ContractPath,
    int ContractTurnCount,
    int ContractCandidateKeyCount,
    int LiveTurnCount,
    int MatchedTurnCount,
    int ShapeMatchedTurnCount,
    int AmbiguousTurnCount,
    int UnmatchedTurnCount,
    string GateStatus,
    LiveSourceTurnContractMatch[] Matches);
