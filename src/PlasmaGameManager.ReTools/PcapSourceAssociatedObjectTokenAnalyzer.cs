using System.Buffers.Binary;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceAssociatedObjectTokenAnalyzer
{
    private const int MaxSamples = 160;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceAssociatedObjectTokenReport> AnalyzeDirectoryAsync(
        string inputPath,
        string contractPath,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputPath, contractPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceAssociatedObjectTokenReport AnalyzeDirectory(
        string inputPath,
        string contractPath)
    {
        var contractRecovered = TryReadContractRecovered(contractPath);
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();

        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var associated = files.Sum(static file => file.AssociatedObjectTokenPacketCount);
        var associatedWithoutProbeToken = files.Sum(static file => file.AssociatedObjectTokenWithoutProbeTokenMatchCount);
        var probeCount = files.Sum(static file => file.AssociationProbeCount);
        var probeTokenMatch = files.Sum(static file => file.AssociatedObjectTokenEqualsProbeTokenPacketCount);
        var repeatedAssociated = files.Sum(static file => file.RepeatedAssociatedObjectTokenPacketCount);
        var distinctAssociated = files
            .SelectMany(static file => file.AssociatedObjectTokenCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Count();
        var distinctProbeTokens = files
            .SelectMany(static file => file.AssociationProbeTokenCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Count();
        var topAssociated = files
            .SelectMany(static file => file.AssociatedObjectTokenCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourceAssociatedObjectTokenCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        var topProbeTokens = files
            .SelectMany(static file => file.AssociationProbeTokenCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourceAssociatedObjectTokenCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        var samples = files.SelectMany(static file => file.Samples).Take(MaxSamples).ToArray();
        var coversHardMarkerless = hardMarkerless > 0 && associated == hardMarkerless;
        var probeHandshakePresent = probeCount > 0;
        var probeTokenIsPayloadFirstWord = probeTokenMatch > 0;

        var conclusion = hardMarkerless == 0
            ? "no hard markerless client Source uploads were present in the selected corpus"
            : coversHardMarkerless && contractRecovered && probeHandshakePresent && !probeTokenIsPayloadFirstWord
                ? "hard markerless client uploads all enter TF.elf's associated-object slot +0x90 path; their first word is not the 5-byte accepted-peer token, so the remaining work is slot +0x90 descriptor/field grammar rather than a generic decrypt transform"
                : coversHardMarkerless && contractRecovered
                    ? "hard markerless client uploads enter TF.elf's associated-object slot +0x90 path; correlate the payload first word against vtable +0xb4 descriptor semantics before decoding CLC_Move/usercmd fields"
                    : "associated-object token coverage is incomplete; rerun contract and PCAP analyzers before promoting native Source input";

        return new PcapSourceAssociatedObjectTokenReport(
            "pcap-source-associated-object-token-correlation",
            "Correlates hard markerless TF2 PS3 Source client uploads with the TF.elf associated-object token/descriptor contract recovered from 008b9468, 008b9ad8, and 008be1e8.",
            new PcapSourceAssociatedObjectTokenInputs(inputPath, contractPath),
            new PcapSourceAssociatedObjectTokenSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                associated,
                distinctAssociated,
                repeatedAssociated,
                probeCount,
                distinctProbeTokens,
                probeTokenMatch,
                associatedWithoutProbeToken,
                contractRecovered,
                coversHardMarkerless,
                probeHandshakePresent,
                probeTokenIsPayloadFirstWord,
                false,
                conclusion),
            topAssociated,
            topProbeTokens,
            samples,
            files);
    }

    private PcapSourceAssociatedObjectTokenFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceAssociatedObjectTokenFile(
                relativePath,
                false,
                "",
                "",
                0,
                0,
            0,
            0,
            0,
            0,
            [],
            [],
            []);
        }

        var samples = new List<PcapSourceAssociatedObjectTokenSample>();
        var associatedTokenCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var probeTokenCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var probeTokens = new HashSet<uint>();
        ushort? previousClientSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var associated = 0;
        var tokenEqualsProbe = 0;
        var tokenWithoutProbeMatch = 0;
        var probeCount = 0;

        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var raw = replay.SourcePackets[sourceStep];
            if (raw.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(raw.Payload, out var transport))
            {
                continue;
            }

            clientDirectionCount++;
            int? sequenceDelta = previousClientSequence is null
                ? null
                : Ps3SourceTransportPacket.SequenceDelta(previousClientSequence.Value, transport.CandidateSequence);
            previousClientSequence = transport.CandidateSequence;
            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientDirectionCount, sequenceDelta);

            if (info.Role == Ps3SourceClientPayloadRole.ReliableAssociationProbe
                && info.ReliableAssociationMessageType == 4
                && info.ReliableAssociationNativeToken is { } probeToken)
            {
                probeCount++;
                probeTokens.Add(probeToken);
                Increment(probeTokenCounts, Hex(probeToken));
                if (samples.Count < MaxSamples)
                {
                    samples.Add(new PcapSourceAssociatedObjectTokenSample(
                        relativePath,
                        sourceStep,
                        raw.PacketIndex,
                        raw.TimestampMicroseconds,
                        transport.CandidateSequence,
                        sequenceDelta,
                        "accepted-peer-association-probe",
                        transport.Body.Length,
                        Hex(probeToken),
                        true,
                        "",
                        Prefix(transport.Body, 32)));
                }

                continue;
            }

            if (!IsHardMarkerless(info)
                || !Ps3SourcePayloadObjectFrame.TryDecode(transport.Body, out var frame)
                || frame is not { Kind: Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken }
                || frame.AssociatedObjectToken is not { } token)
            {
                continue;
            }

            hardMarkerless++;
            associated++;
            Increment(associatedTokenCounts, Hex(token));
            var matchesProbeToken = probeTokens.Contains(token);
            if (matchesProbeToken)
            {
                tokenEqualsProbe++;
            }
            else
            {
                tokenWithoutProbeMatch++;
            }

            if (samples.Count < MaxSamples)
            {
                samples.Add(new PcapSourceAssociatedObjectTokenSample(
                    relativePath,
                    sourceStep,
                    raw.PacketIndex,
                    raw.TimestampMicroseconds,
                    transport.CandidateSequence,
                    sequenceDelta,
                    "associated-object-slot90-payload",
                    transport.Body.Length,
                    Hex(token),
                    matchesProbeToken,
                    frame.InnerPayloadLength >= 4
                        ? Hex(BinaryPrimitives.ReadUInt32BigEndian(transport.Body.AsSpan(frame.InnerPayloadOffset, 4)))
                        : "",
                    Prefix(transport.Body, 32)));
            }
        }

        var repeatedAssociated = associatedTokenCounts
            .Where(static pair => pair.Value > 1)
            .Sum(static pair => pair.Value);

        return new PcapSourceAssociatedObjectTokenFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            hardMarkerless,
            associated,
            repeatedAssociated,
            probeCount,
            tokenEqualsProbe,
            tokenWithoutProbeMatch,
            ToCounts(associatedTokenCounts),
            ToCounts(probeTokenCounts),
            samples.ToArray());
    }

    private static bool IsHardMarkerless(Ps3SourceClientPayloadInfo info)
    {
        if (info.AttachedFrameKind is not null
            || info.BitSidecarOffset is not null
            || info.Role is Ps3SourceClientPayloadRole.InitialHandoffProbe
                or Ps3SourceClientPayloadRole.ReliableAssociationProbe
                or Ps3SourceClientPayloadRole.ShortControlAck
                or Ps3SourceClientPayloadRole.SetupControlPayload
                or Ps3SourceClientPayloadRole.EmbeddedObjectNotice
                or Ps3SourceClientPayloadRole.FragmentedClientPayload)
        {
            return false;
        }

        return info.Role == Ps3SourceClientPayloadRole.UserCommandCandidate
            || info.Role == Ps3SourceClientPayloadRole.BinaryControlPayload && info.BodyLength >= 32;
    }

    private static bool TryReadContractRecovered(string contractPath)
    {
        if (!File.Exists(contractPath))
        {
            return false;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(contractPath));
        return document.RootElement.TryGetProperty("Summary", out var summary)
            && summary.TryGetProperty("AssociatedObjectTokenContractRecovered", out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static PcapSourceAssociatedObjectTokenCount[] ToCounts(Dictionary<string, int> counts)
    {
        return counts
            .Select(static pair => new PcapSourceAssociatedObjectTokenCount(pair.Key, pair.Value))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static string Hex(uint value) => $"0x{value:x8}";

    private static string Prefix(ReadOnlySpan<byte> data, int maxBytes)
    {
        return data.IsEmpty
            ? ""
            : Convert.ToHexString(data[..Math.Min(maxBytes, data.Length)]).ToLowerInvariant();
    }

    private static string[] EnumeratePcapInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return [inputPath];
        }

        if (!Directory.Exists(inputPath))
        {
            throw new DirectoryNotFoundException(inputPath);
        }

        return Directory.EnumerateFiles(inputPath, "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string DisplayPath(string inputPath, string file)
    {
        var root = File.Exists(inputPath)
            ? Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? Directory.GetCurrentDirectory()
            : Path.GetFullPath(inputPath);
        return Path.GetRelativePath(root, Path.GetFullPath(file));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapSourceAssociatedObjectTokenReport(
    string Status,
    string Note,
    PcapSourceAssociatedObjectTokenInputs Inputs,
    PcapSourceAssociatedObjectTokenSummary Summary,
    PcapSourceAssociatedObjectTokenCount[] TopAssociatedObjectTokens,
    PcapSourceAssociatedObjectTokenCount[] AssociationProbeTokens,
    PcapSourceAssociatedObjectTokenSample[] Samples,
    PcapSourceAssociatedObjectTokenFile[] Files);

public sealed record PcapSourceAssociatedObjectTokenInputs(
    string PcapInput,
    string AssociatedObjectTokenContract);

public sealed record PcapSourceAssociatedObjectTokenSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int AssociatedObjectTokenPacketCount,
    int DistinctAssociatedObjectTokenCount,
    int RepeatedAssociatedObjectTokenPacketCount,
    int AssociationProbeCount,
    int DistinctAssociationProbeTokenCount,
    int AssociatedObjectTokenEqualsProbeTokenPacketCount,
    int AssociatedObjectTokenWithoutProbeTokenMatchCount,
    bool AssociatedObjectTokenContractRecovered,
    bool AssociatedObjectDispatchCoversHardMarkerlessCorpus,
    bool AcceptedPeerProbePresent,
    bool AssociationProbeTokenIsPayloadFirstWord,
    bool NativeSourceInputReady,
    string Conclusion);

public sealed record PcapSourceAssociatedObjectTokenCount(
    string Value,
    int Count);

public sealed record PcapSourceAssociatedObjectTokenSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    ushort Sequence,
    int? SequenceDelta,
    string Role,
    int BodyLength,
    string TokenHex,
    bool TokenEqualsAcceptedPeerProbeToken,
    string InnerFirstWordBigEndianHex,
    string BodyPrefix32Hex);

public sealed record PcapSourceAssociatedObjectTokenFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int AssociatedObjectTokenPacketCount,
    int RepeatedAssociatedObjectTokenPacketCount,
    int AssociationProbeCount,
    int AssociatedObjectTokenEqualsProbeTokenPacketCount,
    int AssociatedObjectTokenWithoutProbeTokenMatchCount,
    PcapSourceAssociatedObjectTokenCount[] AssociatedObjectTokenCounts,
    PcapSourceAssociatedObjectTokenCount[] AssociationProbeTokenCounts,
    PcapSourceAssociatedObjectTokenSample[] Samples);
