using System.Buffers.Binary;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourcePayloadObjectFirstWordAnalyzer
{
    private const int MaxSamples = 160;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourcePayloadObjectFirstWordReport> AnalyzeDirectoryAsync(
        string inputPath,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourcePayloadObjectFirstWordReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var decoded = files.Sum(static file => file.PayloadObjectFirstWordDecodedCount);
        var owner = files.Sum(static file => file.OwnerSlot8Count);
        var fragmented = files.Sum(static file => file.FragmentedSpecialWrapperCount);
        var repacked = files.Sum(static file => file.RepackedSpecialWrapperCount);
        var associated = files.Sum(static file => file.AssociatedObjectTokenCount);
        var associatedSmall = files.Sum(static file => file.AssociatedObjectSmallTokenCount);
        var descriptorType1 = files.Sum(static file => file.AssociationDescriptorType1Count);
        var descriptorType2 = files.Sum(static file => file.AssociationDescriptorType2Count);
        var descriptorType3 = files.Sum(static file => file.AssociationDescriptorType3Count);
        var nativeAssociationDescriptors = descriptorType1 + descriptorType2 + descriptorType3;
        var samples = files.SelectMany(static file => file.Samples).Take(MaxSamples).ToArray();

        var allTokenCounts = files
            .SelectMany(static file => file.AssociatedObjectTokenCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourcePayloadObjectFirstWordCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();
        var associatedRepeated = allTokenCounts
            .Where(static count => count.Count > 1)
            .Sum(static count => count.Count);

        var bodyLengthCounts = files
            .SelectMany(static file => file.BodyLengthCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourcePayloadObjectFirstWordCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();

        var dominantLength = bodyLengthCounts.FirstOrDefault()?.Value ?? "";
        var conclusion = hardMarkerless == 0
            ? "no hard markerless client Source packets were present in the selected corpus"
            : owner + fragmented + repacked == hardMarkerless
                ? "hard markerless bodies all start with explicit TF.elf owner/special wrapper sentinels"
                : associated == hardMarkerless && nativeAssociationDescriptors == 0
                    ? "hard markerless bodies all enter TF.elf's non--1 associated-object path; none use small descriptor type values 1/2/3 at the first word, so recover the vtable +0xb4 descriptor compare and slot +0x90 field grammar before treating them as CLC_Move"
                : associated == hardMarkerless
                    ? "hard markerless bodies all enter TF.elf's associated-object descriptor path; recover slot +0x90 semantics before treating them as CLC_Move"
                    : "hard markerless bodies mix associated-token and owner/special wrapper families; route by TF.elf first-word dispatch before decoding inner payloads";

        return new PcapSourcePayloadObjectFirstWordReport(
            "pcap-source-payload-object-first-word",
            "Classifies hard markerless TF2 PS3 Source client uploads by the TF.elf payload-object first word used by 008be1e8: -1 owner slot +0x08, -2 fragment reassembly, -3 repack, or non--1 associated object slot +0x90 dispatch through 008b9ad8.",
            new PcapSourcePayloadObjectFirstWordSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                decoded,
                owner,
                fragmented,
                repacked,
                associated,
                associatedSmall,
                allTokenCounts.Length,
                associatedRepeated,
                nativeAssociationDescriptors,
                descriptorType1,
                descriptorType2,
                descriptorType3,
                dominantLength,
                hardMarkerless > 0 && decoded == hardMarkerless,
                false,
                conclusion),
            [
                new PcapSourcePayloadObjectFirstWordCount(nameof(Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control), owner),
                new PcapSourcePayloadObjectFirstWordCount(nameof(Ps3SourcePayloadObjectFrameKind.FragmentedSpecialWrapper), fragmented),
                new PcapSourcePayloadObjectFirstWordCount(nameof(Ps3SourcePayloadObjectFrameKind.RepackedSpecialWrapper), repacked),
                new PcapSourcePayloadObjectFirstWordCount(nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken), associated)
            ],
            bodyLengthCounts,
            allTokenCounts.Take(64).ToArray(),
            samples,
            files);
    }

    private PcapSourcePayloadObjectFirstWordFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourcePayloadObjectFirstWordFile(
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

        var samples = new List<PcapSourcePayloadObjectFirstWordSample>();
        var tokenCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var bodyLengthCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var decoded = 0;
        var owner = 0;
        var fragmented = 0;
        var repacked = 0;
        var associated = 0;
        var associatedSmall = 0;
        var descriptorType1 = 0;
        var descriptorType2 = 0;
        var descriptorType3 = 0;

        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var raw = replay.SourcePackets[sourceStep];
            if (raw.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(raw.Payload, out var transport))
            {
                continue;
            }

            clientDirectionCount++;
            int? sequenceDelta = previousSequence is null
                ? null
                : Ps3SourceTransportPacket.SequenceDelta(previousSequence.Value, transport.CandidateSequence);
            previousSequence = transport.CandidateSequence;
            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientDirectionCount, sequenceDelta);
            if (!IsHardMarkerless(info))
            {
                continue;
            }

            hardMarkerless++;
            Increment(bodyLengthCounts, transport.Body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!Ps3SourcePayloadObjectFrame.TryDecode(transport.Body, out var frame)
                || frame is null)
            {
                continue;
            }

            decoded++;
            var tokenHex = $"0x{frame.HeaderValue:x8}";
            switch (frame.Kind)
            {
                case Ps3SourcePayloadObjectFrameKind.OwnerSlot8Control:
                    owner++;
                    break;
                case Ps3SourcePayloadObjectFrameKind.FragmentedSpecialWrapper:
                    fragmented++;
                    break;
                case Ps3SourcePayloadObjectFrameKind.RepackedSpecialWrapper:
                    repacked++;
                    break;
                case Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken:
                    associated++;
                    Increment(tokenCounts, tokenHex);
                    if (frame.HeaderValue is > 0 and <= 0xffff)
                    {
                        associatedSmall++;
                    }
                    switch (frame.HeaderValue)
                    {
                        case 1:
                            descriptorType1++;
                            break;
                        case 2:
                            descriptorType2++;
                            break;
                        case 3:
                            descriptorType3++;
                            break;
                    }
                    break;
            }

            if (samples.Count < MaxSamples)
            {
                var firstWordLe = transport.Body.Length >= 4
                    ? BinaryPrimitives.ReadUInt32LittleEndian(transport.Body.AsSpan(0, 4))
                    : 0;
                samples.Add(new PcapSourcePayloadObjectFirstWordSample(
                    relativePath,
                    sourceStep,
                    raw.PacketIndex,
                    raw.TimestampMicroseconds,
                    transport.CandidateSequence,
                    sequenceDelta,
                    transport.PayloadLength,
                    transport.Body.Length,
                    frame.Kind.ToString(),
                    tokenHex,
                    unchecked((int)frame.HeaderValue),
                    $"0x{firstWordLe:x8}",
                    unchecked((int)firstWordLe),
                    frame.InnerPayloadOffset,
                    frame.InnerPayloadLength,
                    frame.PayloadObjectBitreaderFieldOffset,
                    frame.FragmentIndex,
                    frame.FragmentTotalCount,
                    frame.FragmentPacketCounter,
                    frame.FragmentWrappedOrCompressed,
                    Prefix(transport.Body, 32)));
            }
        }

        var repeatedAssociatedPackets = tokenCounts
            .Where(static pair => pair.Value > 1)
            .Sum(static pair => pair.Value);

        return new PcapSourcePayloadObjectFirstWordFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            hardMarkerless,
            decoded,
            owner,
            fragmented,
            repacked,
            associated,
            associatedSmall,
            repeatedAssociatedPackets,
            descriptorType1 + descriptorType2 + descriptorType3,
            descriptorType1,
            descriptorType2,
            descriptorType3,
            ToCounts(bodyLengthCounts),
            ToCounts(tokenCounts),
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

    private static PcapSourcePayloadObjectFirstWordCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourcePayloadObjectFirstWordCount(group.Key, group.Count()))
            .ToArray();
    }

    private static PcapSourcePayloadObjectFirstWordCount[] ToCounts(Dictionary<string, int> counts)
    {
        return counts
            .Select(static pair => new PcapSourcePayloadObjectFirstWordCount(pair.Key, pair.Value))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

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

public sealed record PcapSourcePayloadObjectFirstWordReport(
    string Status,
    string Note,
    PcapSourcePayloadObjectFirstWordSummary Summary,
    PcapSourcePayloadObjectFirstWordCount[] KindCounts,
    PcapSourcePayloadObjectFirstWordCount[] BodyLengthCounts,
    PcapSourcePayloadObjectFirstWordCount[] TopAssociatedObjectTokens,
    PcapSourcePayloadObjectFirstWordSample[] Samples,
    PcapSourcePayloadObjectFirstWordFile[] Files);

public sealed record PcapSourcePayloadObjectFirstWordSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int PayloadObjectFirstWordDecodedCount,
    int OwnerSlot8Count,
    int FragmentedSpecialWrapperCount,
    int RepackedSpecialWrapperCount,
    int AssociatedObjectTokenCount,
    int AssociatedObjectSmallTokenCount,
    int DistinctAssociatedObjectTokenCount,
    int AssociatedObjectRepeatedTokenPacketCount,
    int NativeAssociationDescriptorCount,
    int AssociationDescriptorType1Count,
    int AssociationDescriptorType2Count,
    int AssociationDescriptorType3Count,
    string DominantBodyLength,
    bool PayloadObjectFirstWordDispatchCoversHardMarkerlessCorpus,
    bool NativeSourceInputReady,
    string Conclusion);

public sealed record PcapSourcePayloadObjectFirstWordCount(
    string Value,
    int Count);

public sealed record PcapSourcePayloadObjectFirstWordSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    ushort Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Kind,
    string FirstWordBigEndianHex,
    int FirstWordBigEndianSigned,
    string FirstWordLittleEndianHex,
    int FirstWordLittleEndianSigned,
    int InnerPayloadOffset,
    int InnerPayloadLength,
    int PayloadObjectBitreaderFieldOffset,
    byte? FragmentIndex,
    byte? FragmentTotalCount,
    uint? FragmentPacketCounter,
    bool? FragmentWrappedOrCompressed,
    string BodyPrefix32Hex);

public sealed record PcapSourcePayloadObjectFirstWordFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int PayloadObjectFirstWordDecodedCount,
    int OwnerSlot8Count,
    int FragmentedSpecialWrapperCount,
    int RepackedSpecialWrapperCount,
    int AssociatedObjectTokenCount,
    int AssociatedObjectSmallTokenCount,
    int AssociatedObjectRepeatedTokenPacketCount,
    int NativeAssociationDescriptorCount,
    int AssociationDescriptorType1Count,
    int AssociationDescriptorType2Count,
    int AssociationDescriptorType3Count,
    PcapSourcePayloadObjectFirstWordCount[] BodyLengthCounts,
    PcapSourcePayloadObjectFirstWordCount[] AssociatedObjectTokenCounts,
    PcapSourcePayloadObjectFirstWordSample[] Samples);
