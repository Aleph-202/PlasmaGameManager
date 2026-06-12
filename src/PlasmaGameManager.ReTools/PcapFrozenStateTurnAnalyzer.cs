using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapFrozenStateTurnAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapFrozenStateTurnReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapFrozenStateTurnReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath).ToArray();
        var analyses = files.Select(file => AnalyzeFile(inputPath, file)).ToArray();
        var turns = analyses.SelectMany(static file => file.FrozenStateTurns).ToArray();
        return new PcapFrozenStateTurnReport(
            new PcapFrozenStateTurnSummary(
                files.Length,
                analyses.Count(static file => file.HasActiveSourceFlow),
                turns.Length,
                turns.Count(static turn => turn.IsRetailPostRosterTrioShape),
                CountBy(turns.Select(static turn => turn.ServerPayloadLengthPattern)),
                CountBy(turns.Select(static turn => turn.NextClientPacket?.PayloadLength.ToString())),
                CountBy(turns.SelectMany(static turn => turn.ServerPackets.SelectMany(static packet => packet.Records.Select(static record => record.ObjectId)))),
                CountBy(turns.SelectMany(static turn => turn.ServerPackets.SelectMany(static packet => packet.Records.Select(static record => record.OwnerOrRootObjectId)))),
                CountBy(turns.SelectMany(static turn => turn.ServerPackets.SelectMany(static packet => packet.Records.Select(static record => record.DisplayName)))),
                CountBy(turns.SelectMany(static turn => turn.ServerPackets.Select(static packet => $"{packet.PayloadLength}:{packet.PrefixLength}:{packet.PrefixHex}")))),
            analyses);
    }

    private PcapFrozenStateFileAnalysis AnalyzeFile(string inputPath, string file)
    {
        var relativePath = Path.GetRelativePath(File.Exists(inputPath) ? Path.GetDirectoryName(inputPath)! : inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapFrozenStateFileAnalysis(
                relativePath,
                false,
                "",
                "",
                0,
                0,
                []);
        }

        var packets = BuildPacketViews(replay);
        var turns = BuildTurns(packets)
            .Where(static turn => turn.ServerPackets.Any(static packet => packet.Records.Any(static record => record.Role == nameof(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject))))
            .Select(turn => BuildFrozenStateTurn(turn, packets))
            .ToArray();
        return new PcapFrozenStateFileAnalysis(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.SourcePackets.Length,
            turns.Length,
            turns);
    }

    private static PcapFrozenStatePacketView[] BuildPacketViews(PcapActiveFlowReplay replay)
    {
        var result = new List<PcapFrozenStatePacketView>();
        ushort? lastClientSequence = null;
        ushort? lastServerSequence = null;
        var firstTimestamp = replay.SourcePackets.Length == 0 ? 0 : replay.SourcePackets[0].TimestampMicroseconds;

        for (var i = 0; i < replay.SourcePackets.Length; i++)
        {
            var raw = replay.SourcePackets[i];
            var elapsedMilliseconds = Math.Round((raw.TimestampMicroseconds - firstTimestamp) / 1000.0, 3);
            if (!Ps3SourceTransportPacket.TryDecode(raw.Payload, out var packet))
            {
                result.Add(new PcapFrozenStatePacketView(
                    i,
                    raw.PacketIndex,
                    elapsedMilliseconds,
                    raw.Direction.ToString(),
                    raw.Payload.Length,
                    null,
                    null,
                    null,
                    "",
                    "",
                    [],
                    Convert.ToHexString(raw.Payload).ToLowerInvariant()));
                continue;
            }

            int? delta;
            if (raw.Direction == PcapActiveFlowDirection.ClientToServer)
            {
                delta = lastClientSequence is null ? null : Ps3SourceTransportPacket.SequenceDelta(lastClientSequence.Value, packet.CandidateSequence);
                lastClientSequence = packet.CandidateSequence;
            }
            else
            {
                delta = lastServerSequence is null ? null : Ps3SourceTransportPacket.SequenceDelta(lastServerSequence.Value, packet.CandidateSequence);
                lastServerSequence = packet.CandidateSequence;
            }

            var records = ExtractRecordViews(packet.Body);
            var semantic = Ps3SourcePayloadSemantics.Analyze(packet.Body);
            result.Add(new PcapFrozenStatePacketView(
                i,
                raw.PacketIndex,
                elapsedMilliseconds,
                raw.Direction.ToString(),
                raw.Payload.Length,
                packet.CandidateSequence,
                delta,
                packet.Body.Length,
                Ps3SourceGameplaySession.ClassifyShape(packet).ToString(),
                semantic.Role.ToString(),
                records,
                Convert.ToHexString(packet.Body).ToLowerInvariant()));
        }

        return result.ToArray();
    }

    private static PcapFrozenStateRecordView[] ExtractRecordViews(ReadOnlySpan<byte> body)
    {
        return Ps3SourceEmbeddedObjectRecords.Extract(body)
            .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
            .Select(static record => new PcapFrozenStateRecordView(
                record.Marker,
                record.Offset,
                record.Length,
                record.Role.ToString(),
                Hex(record.FieldA),
                Hex(record.FieldB),
                Hex(record.FieldC),
                record.DisplayName,
                record.PrintableCandidates))
            .ToArray();
    }

    private static PcapFrozenStateTurn BuildFrozenStateTurn(
        PcapFrozenStateTurnPacketGroup group,
        IReadOnlyList<PcapFrozenStatePacketView> packets)
    {
        var nextClientPacket = packets
            .Skip(group.ServerPackets.LastOrDefault()?.SourceStep + 1 ?? group.ClientPackets.LastOrDefault()?.SourceStep + 1 ?? 0)
            .FirstOrDefault(static packet => packet.Direction == PcapActiveFlowDirection.ClientToServer.ToString());
        var serverFrozenPackets = group.ServerPackets
            .Where(static packet => packet.Records.Any(static record => record.Role == nameof(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject)))
            .Select(BuildFrozenStateServerPacket)
            .ToArray();
        var serverPattern = string.Join("+", serverFrozenPackets.Select(static packet => packet.PayloadLength.ToString()));
        return new PcapFrozenStateTurn(
            group.TurnIndex,
            group.ClientPackets,
            serverFrozenPackets,
            nextClientPacket,
            serverPattern,
            serverPattern == "175+80+62",
            ClassifyTurn(group.ClientPackets, serverFrozenPackets, nextClientPacket));
    }

    private static PcapFrozenStateServerPacket BuildFrozenStateServerPacket(PcapFrozenStatePacketView packet)
    {
        var frozenRecords = packet.Records
            .Where(static record => record.Role == nameof(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject))
            .ToArray();
        var prefixLength = frozenRecords.Length == 0 ? packet.BodyLength ?? 0 : frozenRecords[0].Offset;
        var body = Convert.FromHexString(packet.BodyHex);
        var prefixHex = body.Length == 0
            ? ""
            : Convert.ToHexString(body.AsSpan(0, Math.Min(prefixLength, body.Length))).ToLowerInvariant();
        return new PcapFrozenStateServerPacket(
            packet.SourceStep,
            packet.PacketIndex,
            packet.ElapsedMilliseconds,
            packet.PayloadLength,
            packet.Sequence,
            packet.SequenceDelta,
            packet.BodyLength,
            packet.Shape,
            packet.SemanticRole,
            prefixLength,
            prefixHex,
            frozenRecords);
    }

    private static string ClassifyTurn(
        IReadOnlyList<PcapFrozenStatePacketView> clientPackets,
        IReadOnlyList<PcapFrozenStateServerPacket> serverPackets,
        PcapFrozenStatePacketView? nextClientPacket)
    {
        var serverPattern = string.Join("+", serverPackets.Select(static packet => packet.PayloadLength.ToString()));
        var clientPattern = string.Join("+", clientPackets.Select(static packet => packet.PayloadLength.ToString()));
        if (serverPattern == "175+80+62" && nextClientPacket?.PayloadLength == 289)
        {
            return "retail-post-roster-frozen-state-trio-accepted";
        }

        if (serverPattern == "175+80+62")
        {
            return "retail-post-roster-frozen-state-trio";
        }

        if (clientPackets.Any(static packet => packet.Records.Any(static record => record.Role == nameof(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject))))
        {
            return $"frozen-state-reply-after-client-upload-{clientPattern}";
        }

        return "frozen-state-server-refresh";
    }

    private static PcapFrozenStateTurnPacketGroup[] BuildTurns(PcapFrozenStatePacketView[] packets)
    {
        var turns = new List<PcapFrozenStateTurnPacketGroup>();
        var client = new List<PcapFrozenStatePacketView>();
        var server = new List<PcapFrozenStatePacketView>();

        void Flush()
        {
            if (client.Count == 0 && server.Count == 0)
            {
                return;
            }

            turns.Add(new PcapFrozenStateTurnPacketGroup(turns.Count, client.ToArray(), server.ToArray()));
            client.Clear();
            server.Clear();
        }

        foreach (var packet in packets)
        {
            if (packet.Direction == PcapActiveFlowDirection.ClientToServer.ToString())
            {
                if (server.Count > 0)
                {
                    Flush();
                }

                client.Add(packet);
            }
            else
            {
                server.Add(packet);
            }
        }

        Flush();
        return turns.ToArray();
    }

    private static IEnumerable<string> EnumeratePcapInputs(string input)
    {
        if (File.Exists(input))
        {
            yield return input;
            yield break;
        }

        if (!Directory.Exists(input))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Where(static path =>
                path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            yield return path;
        }
    }

    private static PcapFrozenStateCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(64)
            .Select(static group => new PcapFrozenStateCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string? Hex(uint? value)
    {
        return value is null ? null : value.Value.ToString("x8");
    }

    private sealed record PcapFrozenStateTurnPacketGroup(
        int TurnIndex,
        PcapFrozenStatePacketView[] ClientPackets,
        PcapFrozenStatePacketView[] ServerPackets);
}

public sealed record PcapFrozenStateTurnReport(
    PcapFrozenStateTurnSummary Summary,
    PcapFrozenStateFileAnalysis[] Files);

public sealed record PcapFrozenStateTurnSummary(
    int FileCount,
    int ActiveSourceFlowFileCount,
    int FrozenStateTurnCount,
    int RetailPostRosterTrioShapeCount,
    PcapFrozenStateCount[] ServerPayloadLengthPatterns,
    PcapFrozenStateCount[] NextClientPayloadLengthCounts,
    PcapFrozenStateCount[] ServerObjectIdCounts,
    PcapFrozenStateCount[] ServerOwnerOrRootObjectIdCounts,
    PcapFrozenStateCount[] ServerDisplayNameCounts,
    PcapFrozenStateCount[] ServerPrefixPatterns);

public sealed record PcapFrozenStateFileAnalysis(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int FrozenStateTurnCount,
    PcapFrozenStateTurn[] FrozenStateTurns);

public sealed record PcapFrozenStateTurn(
    int TurnIndex,
    PcapFrozenStatePacketView[] ClientPackets,
    PcapFrozenStateServerPacket[] ServerPackets,
    PcapFrozenStatePacketView? NextClientPacket,
    string ServerPayloadLengthPattern,
    bool IsRetailPostRosterTrioShape,
    string Classification);

public sealed record PcapFrozenStatePacketView(
    int SourceStep,
    long PacketIndex,
    double ElapsedMilliseconds,
    string Direction,
    int PayloadLength,
    ushort? Sequence,
    int? SequenceDelta,
    int? BodyLength,
    string Shape,
    string SemanticRole,
    PcapFrozenStateRecordView[] Records,
    string BodyHex);

public sealed record PcapFrozenStateServerPacket(
    int SourceStep,
    long PacketIndex,
    double ElapsedMilliseconds,
    int PayloadLength,
    ushort? Sequence,
    int? SequenceDelta,
    int? BodyLength,
    string Shape,
    string SemanticRole,
    int PrefixLength,
    string PrefixHex,
    PcapFrozenStateRecordView[] Records);

public sealed record PcapFrozenStateRecordView(
    string Marker,
    int Offset,
    int Length,
    string Role,
    string? ObjectId,
    string? OwnerOrRootObjectId,
    string? ClassId,
    string? DisplayName,
    string[] PrintableCandidates);

public sealed record PcapFrozenStateCount(string Value, int Count);
