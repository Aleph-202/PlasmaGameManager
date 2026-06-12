using System.Security.Cryptography;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapGameManagerHelloAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapGameManagerHelloReport> AnalyzeAsync(string input, string outputPath)
    {
        var report = Analyze(input);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return report;
    }

    public PcapGameManagerHelloReport Analyze(string input)
    {
        var files = EnumeratePcaps(input);
        var flows = files
            .Select(file => AnalyzeFile(input, file))
            .ToArray();

        return new PcapGameManagerHelloReport(
            "pcap-gamemanager-hello",
            BuildSummary(files.Length, flows),
            BuildGroups(flows, static flow => flow.ClientHelloPrefix6)
                .Select(static group => new PcapGameManagerHelloGroup(group.Key, group.Files))
                .ToArray(),
            BuildGroups(flows, static flow => flow.ServerHelloBodySignature)
                .Select(static group => new PcapGameManagerHelloGroup(group.Key, group.Files))
                .ToArray(),
            BuildGroups(flows, static flow => flow.FirstSourceBodySignature)
                .Select(static group => new PcapGameManagerHelloGroup(group.Key, group.Files))
                .ToArray(),
            flows);
    }

    private PcapGameManagerHelloFlow AnalyzeFile(string input, string file)
    {
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return PcapGameManagerHelloFlow.NoActiveFlow(RelativePath(input, file));
        }

        var clientSequence = ReadSequence(replay.ClientHelloPayload);
        var serverSequence = ReadSequence(replay.ServerHelloPayload);
        var firstSourceSequence = ReadSequence(replay.FirstSourceClientPayload);
        var serverBody = Body(replay.ServerHelloPayload);
        var firstSourceBody = Body(replay.FirstSourceClientPayload);
        var sourceTransport = Ps3SourceTransportPacket.TryDecode(replay.FirstSourceClientPayload, out var transport)
            ? transport
            : null;
        var firstSourceSemantic = sourceTransport is null
            ? null
            : Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(sourceTransport.Body);

        return new PcapGameManagerHelloFlow(
            RelativePath(input, file),
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            replay.ServerPort,
            replay.ClientHelloPacketIndex,
            replay.ServerHelloPacketIndex,
            replay.FirstSourceClientPacketIndex,
            replay.ServerHelloPacketIndex - replay.ClientHelloPacketIndex,
            replay.FirstSourceClientPacketIndex - replay.ServerHelloPacketIndex,
            clientSequence,
            serverSequence,
            clientSequence is null || serverSequence is null ? null : unchecked((ushort)(serverSequence.Value - clientSequence.Value)),
            firstSourceSequence,
            serverSequence is not null && firstSourceSequence == serverSequence,
            replay.ClientHelloPayload.Length,
            replay.ServerHelloPayload.Length,
            replay.FirstSourceClientPayload.Length,
            Hex(replay.ClientHelloPayload),
            Hex(replay.ServerHelloPayload),
            Hex(replay.FirstSourceClientPayload),
            HexPrefix(replay.ClientHelloPayload, 6),
            HexPrefix(replay.ServerHelloPayload, 6),
            HexPrefix(replay.FirstSourceClientPayload, 6),
            Hash(replay.ClientHelloPayload),
            Hash(replay.ServerHelloPayload),
            Hash(replay.FirstSourceClientPayload),
            Hash(serverBody),
            Hash(firstSourceBody),
            CommonPrefixLength(replay.ClientHelloPayload, replay.ServerHelloPayload),
            CommonPrefixLength(replay.ServerHelloPayload, replay.FirstSourceClientPayload),
            CommonPrefixLength(serverBody, firstSourceBody),
            XorHex(replay.ClientHelloPayload, replay.ServerHelloPayload, 24),
            XorHex(replay.ServerHelloPayload, replay.FirstSourceClientPayload, 24),
            sourceTransport is not null,
            sourceTransport?.CandidateSequence,
            sourceTransport?.PayloadLength,
            sourceTransport?.Body.Length,
            sourceTransport is null ? "" : Ps3SourceGameplaySession.ClassifyShape(sourceTransport).ToString(),
            sourceTransport is null ? "" : HexPrefix(sourceTransport.Body.ToArray(), 24),
            sourceTransport is null ? "" : Hash(sourceTransport.Body),
            firstSourceSemantic?.Kind.ToString() ?? "",
            firstSourceSemantic?.Role.ToString() ?? "",
            replay.FirstSourceClientKind.ToString(),
            replay.FirstSourceAsciiPreview,
            BuildConclusion(clientSequence, serverSequence, firstSourceSequence, replay.ServerHelloPacketIndex, replay.FirstSourceClientPacketIndex, sourceTransport));
    }

    private static PcapGameManagerHelloSummary BuildSummary(int pcapFileCount, PcapGameManagerHelloFlow[] flows)
    {
        var active = flows.Where(static flow => flow.HasActiveFlow).ToArray();
        var sequenceDeltas = active
            .Where(static flow => flow.SequenceDelta is not null)
            .GroupBy(static flow => flow.SequenceDelta!.Value)
            .OrderBy(static group => group.Key)
            .Select(static group => new PcapGameManagerHelloCount(group.Key.ToString(), group.Count()))
            .ToArray();
        var packetGaps = active
            .GroupBy(static flow => flow.FirstSourceAfterServerHelloPacketGap)
            .OrderBy(static group => group.Key)
            .Select(static group => new PcapGameManagerHelloCount(group.Key.ToString(), group.Count()))
            .ToArray();
        var firstSourceShapes = active
            .GroupBy(static flow => flow.FirstSourceShape, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapGameManagerHelloCount(group.Key, group.Count()))
            .ToArray();
        var firstSourceSemanticRoles = active
            .GroupBy(static flow => flow.FirstSourcePayloadSemanticRole, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapGameManagerHelloCount(group.Key, group.Count()))
            .ToArray();

        return new PcapGameManagerHelloSummary(
            pcapFileCount,
            active.Length,
            flows.Length - active.Length,
            active.Count(static flow => flow.SequenceDelta == 6),
            active.Count(static flow => flow.FirstSourceSequenceEqualsServerHelloSequence),
            active.Count(static flow => flow.FirstSourceTransportDecoded),
            active.Select(static flow => flow.ClientHelloPrefix6).Distinct(StringComparer.Ordinal).Count(),
            active.Select(static flow => flow.ServerHelloBodySignature).Distinct(StringComparer.Ordinal).Count(),
            active.Select(static flow => flow.FirstSourceBodySignature).Distinct(StringComparer.Ordinal).Count(),
            sequenceDeltas,
            packetGaps,
            firstSourceShapes,
            firstSourceSemanticRoles,
            BuildSummaryConclusion(active));
    }

    private static string BuildSummaryConclusion(PcapGameManagerHelloFlow[] active)
    {
        if (active.Length == 0)
        {
            return "no-active-gamemanager-source-flows";
        }

        var stableDelta = active.All(static flow => flow.SequenceDelta == 6);
        var sourceReusesSequence = active.All(static flow => flow.FirstSourceSequenceEqualsServerHelloSequence);
        var allDecoded = active.All(static flow => flow.FirstSourceTransportDecoded);
        if (stableDelta && sourceReusesSequence && allDecoded)
        {
            return "stable-sequence-delta-and-source-sequence-reuse-observed-server-hello-body-remains-family-specific";
        }

        return "mixed-hello-boundary-semantics-review-flow-details";
    }

    private static string BuildConclusion(
        ushort? clientSequence,
        ushort? serverSequence,
        ushort? firstSourceSequence,
        long serverHelloPacketIndex,
        long firstSourcePacketIndex,
        Ps3SourceTransportPacket? firstSource)
    {
        var parts = new List<string>();
        if (clientSequence is not null && serverSequence is not null)
        {
            parts.Add(unchecked((ushort)(serverSequence.Value - clientSequence.Value)) == 6
                ? "server-hello-sequence-is-client-plus-6"
                : "server-hello-sequence-delta-varies");
        }

        if (serverSequence is not null && firstSourceSequence == serverSequence)
        {
            parts.Add("first-source-client-reuses-server-hello-sequence");
        }

        parts.Add(firstSourcePacketIndex == serverHelloPacketIndex + 1
            ? "source-handoff-starts-next-packet"
            : "source-handoff-has-intervening-packets");

        parts.Add(firstSource is null
            ? "first-source-client-not-ps3-source-transport"
            : $"first-source-client-transport-shape={Ps3SourceGameplaySession.ClassifyShape(firstSource)}");

        return string.Join("; ", parts);
    }

    private static (string Key, string[] Files)[] BuildGroups(
        PcapGameManagerHelloFlow[] flows,
        Func<PcapGameManagerHelloFlow, string> keySelector)
    {
        return flows
            .Where(static flow => flow.HasActiveFlow)
            .GroupBy(keySelector, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => (
                group.Key,
                group.Select(static flow => flow.File).Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    private static string[] EnumeratePcaps(string input)
    {
        if (File.Exists(input))
        {
            return [input];
        }

        if (!Directory.Exists(input))
        {
            throw new DirectoryNotFoundException(input);
        }

        return Directory.EnumerateFiles(input, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RelativePath(string input, string file)
    {
        return Directory.Exists(input) ? Path.GetRelativePath(input, file) : Path.GetFileName(file);
    }

    private static ushort? ReadSequence(ReadOnlySpan<byte> payload)
    {
        return payload.Length < 2 ? null : PlasmaIntegerCodec.ReadUInt16BigEndian(payload);
    }

    private static byte[] Body(ReadOnlySpan<byte> payload)
    {
        return payload.Length <= 2 ? [] : payload[2..].ToArray();
    }

    private static string Hex(byte[] payload)
    {
        return Convert.ToHexString(payload).ToLowerInvariant();
    }

    private static string HexPrefix(byte[] payload, int length)
    {
        return Convert.ToHexString(payload.AsSpan(0, Math.Min(length, payload.Length))).ToLowerInvariant();
    }

    private static string Hash(byte[] payload)
    {
        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }

    private static int CommonPrefixLength(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            if (left[index] != right[index])
            {
                return index;
            }
        }

        return length;
    }

    private static string XorHex(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, int maxLength)
    {
        var length = Math.Min(Math.Min(left.Length, right.Length), maxLength);
        var result = new byte[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = (byte)(left[index] ^ right[index]);
        }

        return Convert.ToHexString(result).ToLowerInvariant();
    }
}

public sealed record PcapGameManagerHelloReport(
    string Status,
    PcapGameManagerHelloSummary Summary,
    PcapGameManagerHelloGroup[] ClientHelloPrefixGroups,
    PcapGameManagerHelloGroup[] ServerHelloBodyGroups,
    PcapGameManagerHelloGroup[] FirstSourceBodyGroups,
    PcapGameManagerHelloFlow[] Flows);

public sealed record PcapGameManagerHelloSummary(
    int PcapFileCount,
    int ActiveFlowCount,
    int NoActiveFlowCount,
    int ServerHelloSequenceDeltaPlusSixCount,
    int FirstSourceSequenceEqualsServerHelloSequenceCount,
    int FirstSourceTransportDecodedCount,
    int UniqueClientHelloPrefix6Count,
    int UniqueServerHelloBodySignatureCount,
    int UniqueFirstSourceBodySignatureCount,
    PcapGameManagerHelloCount[] SequenceDeltaDistribution,
    PcapGameManagerHelloCount[] FirstSourcePacketGapDistribution,
    PcapGameManagerHelloCount[] FirstSourceShapeDistribution,
    PcapGameManagerHelloCount[] FirstSourcePayloadSemanticRoleDistribution,
    string Conclusion);

public sealed record PcapGameManagerHelloGroup(
    string Key,
    string[] Files);

public sealed record PcapGameManagerHelloCount(
    string Value,
    int Count);

public sealed record PcapGameManagerHelloFlow(
    string File,
    bool HasActiveFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int ServerPort,
    long ClientHelloPacketIndex,
    long ServerHelloPacketIndex,
    long FirstSourceClientPacketIndex,
    long ServerHelloAfterClientPacketGap,
    long FirstSourceAfterServerHelloPacketGap,
    ushort? ClientSequence,
    ushort? ServerSequence,
    ushort? SequenceDelta,
    ushort? FirstSourceSequence,
    bool FirstSourceSequenceEqualsServerHelloSequence,
    int ClientHelloLength,
    int ServerHelloLength,
    int FirstSourceClientLength,
    string ClientHelloHex,
    string ServerHelloHex,
    string FirstSourceClientHex,
    string ClientHelloPrefix6,
    string ServerHelloPrefix6,
    string FirstSourcePrefix6,
    string ClientHelloSignature,
    string ServerHelloSignature,
    string FirstSourceSignature,
    string ServerHelloBodySignature,
    string FirstSourceBodySignature,
    int ClientServerCommonPrefixBytes,
    int ServerFirstSourceCommonPrefixBytes,
    int ServerBodyFirstSourceBodyCommonPrefixBytes,
    string ClientServerXorPrefix,
    string ServerFirstSourceXorPrefix,
    bool FirstSourceTransportDecoded,
    int? FirstSourceTransportSequence,
    int? FirstSourceTransportPayloadLength,
    int? FirstSourceTransportBodyLength,
    string FirstSourceShape,
    string FirstSourceTransportBodyPrefix,
    string FirstSourceTransportBodySignature,
    string FirstSourcePayloadSemanticKind,
    string FirstSourcePayloadSemanticRole,
    string FirstSourceKind,
    string FirstSourceAsciiPreview,
    string Conclusion)
{
    public static PcapGameManagerHelloFlow NoActiveFlow(string file)
    {
        return new PcapGameManagerHelloFlow(
            file,
            false,
            "",
            "",
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            false,
            0,
            0,
            0,
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            0,
            0,
            0,
            "",
            "",
            false,
            null,
            null,
            null,
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "no-active-flow");
    }
}
