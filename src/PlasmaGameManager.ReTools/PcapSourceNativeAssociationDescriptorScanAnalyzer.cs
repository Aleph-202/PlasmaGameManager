using System.Buffers.Binary;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceNativeAssociationDescriptorScanAnalyzer
{
    private const int MaxSamples = 160;
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceNativeAssociationDescriptorScanReport> AnalyzeDirectoryAsync(
        string inputPath,
        string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceNativeAssociationDescriptorScanReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var hardMarkerless = files.Sum(static file => file.HardMarkerlessPacketCount);
        var beOffset0 = files.Sum(static file => file.BigEndianDescriptorAtOffset0PacketCount);
        var leOffset0 = files.Sum(static file => file.LittleEndianDescriptorAtOffset0PacketCount);
        var beAny = files.Sum(static file => file.BigEndianDescriptorAnyOffsetPacketCount);
        var leAny = files.Sum(static file => file.LittleEndianDescriptorAnyOffsetPacketCount);
        var rawBeOffset0 = files.Sum(static file => file.RawBigEndianDescriptorAtOffset0PacketCount);
        var rawBeOffset1 = files.Sum(static file => file.RawBigEndianDescriptorAtOffset1PacketCount);
        var rawBeOffset2 = files.Sum(static file => file.RawBigEndianDescriptorAtOffset2PacketCount);
        var rawBeAny = files.Sum(static file => file.RawBigEndianDescriptorAnyOffsetPacketCount);
        var rawLeOffset0 = files.Sum(static file => file.RawLittleEndianDescriptorAtOffset0PacketCount);
        var rawLeOffset1 = files.Sum(static file => file.RawLittleEndianDescriptorAtOffset1PacketCount);
        var rawLeOffset2 = files.Sum(static file => file.RawLittleEndianDescriptorAtOffset2PacketCount);
        var rawLeAny = files.Sum(static file => file.RawLittleEndianDescriptorAnyOffsetPacketCount);
        var beType1 = files.Sum(static file => file.BigEndianType1PacketCount);
        var beType2 = files.Sum(static file => file.BigEndianType2PacketCount);
        var beType3 = files.Sum(static file => file.BigEndianType3PacketCount);
        var beType3Endpoint = files.Sum(static file => file.BigEndianType3NonZeroEndpointPacketCount);
        var samples = files.SelectMany(static file => file.Samples).Take(MaxSamples).ToArray();
        var offsetCounts = files
            .SelectMany(static file => file.BigEndianOffsetCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourceNativeAssociationDescriptorScanCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();
        var leOffsetCounts = files
            .SelectMany(static file => file.LittleEndianOffsetCounts)
            .GroupBy(static count => count.Value, StringComparer.Ordinal)
            .Select(static group => new PcapSourceNativeAssociationDescriptorScanCount(
                group.Key,
                group.Sum(static count => count.Count)))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();

        var ready = hardMarkerless > 0 && beOffset0 == hardMarkerless;
        var conclusion = hardMarkerless == 0
            ? "no hard markerless client Source packets were present in the selected corpus"
            : ready
                ? "every hard markerless body starts with a TF.elf native association descriptor"
                : beOffset0 == 0 && leOffset0 == 0 && beAny == 0 && leAny == 0
                    ? "hard markerless bodies contain no direct TF.elf native association descriptor at offset 0 or embedded offsets; upstream associated-object dispatch is recovered, so continue with slot +0xac state-triple grammar instead of a generic wire-transform search"
                    : beOffset0 == 0
                        ? "hard markerless bodies do not start with TF.elf native association descriptors; embedded descriptor-shaped hits are transform leads only"
                        : "only part of the hard markerless corpus starts with TF.elf native association descriptors; recover the missing wrapper/transform before promoting native input";

        return new PcapSourceNativeAssociationDescriptorScanReport(
            "pcap-source-native-association-descriptor-scan",
            "Scans hard markerless TF2 PS3 Source client uploads for the native association descriptor grammar consumed by TF.elf 008b9ad8 and compared by 0134e230: descriptor type 1/2/3 at the payload-object start, with type 3 carrying IPv4 address and port.",
            new PcapSourceNativeAssociationDescriptorScanSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                beOffset0,
                leOffset0,
                beAny,
                leAny,
                rawBeOffset0,
                rawBeOffset1,
                rawBeOffset2,
                rawBeAny,
                rawLeOffset0,
                rawLeOffset1,
                rawLeOffset2,
                rawLeAny,
                beType1,
                beType2,
                beType3,
                beType3Endpoint,
                offsetCounts.FirstOrDefault()?.Value ?? "",
                offsetCounts.FirstOrDefault()?.Count ?? 0,
                ready,
                conclusion),
            offsetCounts,
            leOffsetCounts,
            samples,
            files);
    }

    private PcapSourceNativeAssociationDescriptorScanFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceNativeAssociationDescriptorScanFile(
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
                0,
                0,
                0,
                0,
                0,
                [],
                [],
                []);
        }

        var samples = new List<PcapSourceNativeAssociationDescriptorScanSample>();
        var beOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var leOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
        var hardMarkerless = 0;
        var beOffset0 = 0;
        var leOffset0 = 0;
        var beAny = 0;
        var leAny = 0;
        var rawBeOffset0 = 0;
        var rawBeOffset1 = 0;
        var rawBeOffset2 = 0;
        var rawBeAny = 0;
        var rawLeOffset0 = 0;
        var rawLeOffset1 = 0;
        var rawLeOffset2 = 0;
        var rawLeAny = 0;
        var beType1 = 0;
        var beType2 = 0;
        var beType3 = 0;
        var beType3Endpoint = 0;

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
            var rawBeMatches = Scan(raw.Payload, littleEndian: false).ToArray();
            var rawLeMatches = Scan(raw.Payload, littleEndian: true).ToArray();
            if (rawBeMatches.Length > 0)
            {
                rawBeAny++;
            }
            if (rawLeMatches.Length > 0)
            {
                rawLeAny++;
            }
            if (rawBeMatches.Any(static match => match.Offset == 0))
            {
                rawBeOffset0++;
            }
            if (rawBeMatches.Any(static match => match.Offset == 1))
            {
                rawBeOffset1++;
            }
            if (rawBeMatches.Any(static match => match.Offset == 2))
            {
                rawBeOffset2++;
            }
            if (rawLeMatches.Any(static match => match.Offset == 0))
            {
                rawLeOffset0++;
            }
            if (rawLeMatches.Any(static match => match.Offset == 1))
            {
                rawLeOffset1++;
            }
            if (rawLeMatches.Any(static match => match.Offset == 2))
            {
                rawLeOffset2++;
            }

            var beMatches = Scan(transport.Body, littleEndian: false).ToArray();
            var leMatches = Scan(transport.Body, littleEndian: true).ToArray();
            if (beMatches.Length > 0)
            {
                beAny++;
                foreach (var match in beMatches)
                {
                    Increment(beOffsets, match.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            if (leMatches.Length > 0)
            {
                leAny++;
                foreach (var match in leMatches)
                {
                    Increment(leOffsets, match.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            var be0 = beMatches.FirstOrDefault(static match => match.Offset == 0);
            if (be0 is not null)
            {
                beOffset0++;
                switch (be0.Type)
                {
                    case 1:
                        beType1++;
                        break;
                    case 2:
                        beType2++;
                        break;
                    case 3:
                        beType3++;
                        if (be0.HasNonZeroEndpoint)
                        {
                            beType3Endpoint++;
                        }
                        break;
                }
            }

            if (leMatches.Any(static match => match.Offset == 0))
            {
                leOffset0++;
            }

            if (samples.Count < MaxSamples
                && (beMatches.Length > 0 || leMatches.Length > 0 || samples.Count < 24))
            {
                var best = beMatches.FirstOrDefault()
                    ?? leMatches.FirstOrDefault();
                samples.Add(new PcapSourceNativeAssociationDescriptorScanSample(
                    relativePath,
                    sourceStep,
                    raw.PacketIndex,
                    raw.TimestampMicroseconds,
                    transport.CandidateSequence,
                    sequenceDelta,
                    transport.Body.Length,
                    best?.Endian ?? "",
                    best?.Offset,
                    best?.Type,
                    best?.AddressHex ?? "",
                    best?.AddressText ?? "",
                    best?.Port,
                    best?.HasNonZeroEndpoint,
                    Prefix(transport.Body, 32)));
            }
        }

        return new PcapSourceNativeAssociationDescriptorScanFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            hardMarkerless,
            beOffset0,
            leOffset0,
            beAny,
            leAny,
            rawBeOffset0,
            rawBeOffset1,
            rawBeOffset2,
            rawBeAny,
            rawLeOffset0,
            rawLeOffset1,
            rawLeOffset2,
            rawLeAny,
            beType1,
            beType2,
            beType3,
            beType3Endpoint,
            ToCounts(beOffsets),
            ToCounts(leOffsets),
            samples.ToArray());
    }

    private static DescriptorMatch[] Scan(ReadOnlyMemory<byte> body, bool littleEndian)
    {
        var span = body.Span;
        var matches = new List<DescriptorMatch>();
        if (span.Length < 4)
        {
            return [];
        }

        for (var offset = 0; offset <= span.Length - 4; offset++)
        {
            var type = littleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4))
                : BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset, 4));
            if (type is not (1 or 2 or 3))
            {
                continue;
            }

            uint address = 0;
            ushort port = 0;
            if (offset + 10 <= span.Length)
            {
                address = littleEndian
                    ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset + 4, 4))
                    : BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 4, 4));
                port = littleEndian
                    ? BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset + 8, 2))
                    : BinaryPrimitives.ReadUInt16BigEndian(span.Slice(offset + 8, 2));
            }

            matches.Add(new DescriptorMatch(
                littleEndian ? "little" : "big",
                offset,
                (int)type,
                address,
                port));
        }

        return matches.ToArray();
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

    private static PcapSourceNativeAssociationDescriptorScanCount[] ToCounts(Dictionary<string, int> counts)
    {
        return counts
            .Select(static pair => new PcapSourceNativeAssociationDescriptorScanCount(pair.Key, pair.Value))
            .OrderByDescending(static count => count.Count)
            .ThenBy(static count => count.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private static string Prefix(ReadOnlyMemory<byte> data, int maxBytes)
    {
        var span = data.Span;
        return span.IsEmpty
            ? ""
            : Convert.ToHexString(span[..Math.Min(maxBytes, span.Length)]).ToLowerInvariant();
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

    private sealed record DescriptorMatch(
        string Endian,
        int Offset,
        int Type,
        uint Address,
        ushort Port)
    {
        public bool HasNonZeroEndpoint => Address != 0 && Port != 0;
        public string AddressHex => $"0x{Address:x8}";
        public string AddressText => $"{(Address >> 24) & 0xff}.{(Address >> 16) & 0xff}.{(Address >> 8) & 0xff}.{Address & 0xff}";
    }
}

public sealed record PcapSourceNativeAssociationDescriptorScanReport(
    string Status,
    string Note,
    PcapSourceNativeAssociationDescriptorScanSummary Summary,
    PcapSourceNativeAssociationDescriptorScanCount[] BigEndianOffsetCounts,
    PcapSourceNativeAssociationDescriptorScanCount[] LittleEndianOffsetCounts,
    PcapSourceNativeAssociationDescriptorScanSample[] Samples,
    PcapSourceNativeAssociationDescriptorScanFile[] Files);

public sealed record PcapSourceNativeAssociationDescriptorScanSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int BigEndianDescriptorAtOffset0PacketCount,
    int LittleEndianDescriptorAtOffset0PacketCount,
    int BigEndianDescriptorAnyOffsetPacketCount,
    int LittleEndianDescriptorAnyOffsetPacketCount,
    int RawBigEndianDescriptorAtOffset0PacketCount,
    int RawBigEndianDescriptorAtOffset1PacketCount,
    int RawBigEndianDescriptorAtOffset2PacketCount,
    int RawBigEndianDescriptorAnyOffsetPacketCount,
    int RawLittleEndianDescriptorAtOffset0PacketCount,
    int RawLittleEndianDescriptorAtOffset1PacketCount,
    int RawLittleEndianDescriptorAtOffset2PacketCount,
    int RawLittleEndianDescriptorAnyOffsetPacketCount,
    int BigEndianType1PacketCount,
    int BigEndianType2PacketCount,
    int BigEndianType3PacketCount,
    int BigEndianType3NonZeroEndpointPacketCount,
    string MostCommonBigEndianDescriptorOffset,
    int MostCommonBigEndianDescriptorOffsetCount,
    bool NativeDescriptorBoundaryReady,
    string Conclusion);

public sealed record PcapSourceNativeAssociationDescriptorScanCount(
    string Value,
    int Count);

public sealed record PcapSourceNativeAssociationDescriptorScanSample(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    ushort Sequence,
    int? SequenceDelta,
    int BodyLength,
    string MatchEndian,
    int? MatchOffset,
    int? MatchType,
    string MatchAddressHex,
    string MatchAddressText,
    ushort? MatchPort,
    bool? MatchHasNonZeroEndpoint,
    string BodyPrefix32Hex);

public sealed record PcapSourceNativeAssociationDescriptorScanFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int BigEndianDescriptorAtOffset0PacketCount,
    int LittleEndianDescriptorAtOffset0PacketCount,
    int BigEndianDescriptorAnyOffsetPacketCount,
    int LittleEndianDescriptorAnyOffsetPacketCount,
    int RawBigEndianDescriptorAtOffset0PacketCount,
    int RawBigEndianDescriptorAtOffset1PacketCount,
    int RawBigEndianDescriptorAtOffset2PacketCount,
    int RawBigEndianDescriptorAnyOffsetPacketCount,
    int RawLittleEndianDescriptorAtOffset0PacketCount,
    int RawLittleEndianDescriptorAtOffset1PacketCount,
    int RawLittleEndianDescriptorAtOffset2PacketCount,
    int RawLittleEndianDescriptorAnyOffsetPacketCount,
    int BigEndianType1PacketCount,
    int BigEndianType2PacketCount,
    int BigEndianType3PacketCount,
    int BigEndianType3NonZeroEndpointPacketCount,
    PcapSourceNativeAssociationDescriptorScanCount[] BigEndianOffsetCounts,
    PcapSourceNativeAssociationDescriptorScanCount[] LittleEndianOffsetCounts,
    PcapSourceNativeAssociationDescriptorScanSample[] Samples);
