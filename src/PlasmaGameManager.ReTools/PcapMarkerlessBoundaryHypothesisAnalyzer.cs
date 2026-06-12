using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapMarkerlessBoundaryHypothesisAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapMarkerlessBoundaryHypothesisReport> AnalyzeAsync(
        string inputPath,
        string clientCommandWorklistPath,
        string opaqueMarkerlessReportPath,
        string udpIngressCorrectionPath,
        string serverDllTunnelMapPath,
        string serverDllUserCmdDecoderPath,
        string outputPath)
    {
        var report = Analyze(
            inputPath,
            clientCommandWorklistPath,
            opaqueMarkerlessReportPath,
            udpIngressCorrectionPath,
            serverDllTunnelMapPath,
            serverDllUserCmdDecoderPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapMarkerlessBoundaryHypothesisReport Analyze(
        string inputPath,
        string clientCommandWorklistPath,
        string opaqueMarkerlessReportPath,
        string udpIngressCorrectionPath,
        string serverDllTunnelMapPath,
        string serverDllUserCmdDecoderPath)
    {
        using var clientWorklist = JsonDocument.Parse(File.ReadAllText(clientCommandWorklistPath));
        using var opaqueWrapper = JsonDocument.Parse(File.ReadAllText(opaqueMarkerlessReportPath));
        using var udpCorrection = JsonDocument.Parse(File.ReadAllText(udpIngressCorrectionPath));
        using var serverDllTunnel = JsonDocument.Parse(File.ReadAllText(serverDllTunnelMapPath));
        using var userCmdDecoder = JsonDocument.Parse(File.ReadAllText(serverDllUserCmdDecoderPath));

        var files = EnumeratePcapInputs(inputPath);
        var packets = new List<PcapMarkerlessBoundaryPacketProbe>();
        var activeSourceFlowCount = 0;
        foreach (var file in files)
        {
            var replay = _extractor.Extract(file);
            if (replay is null)
            {
                continue;
            }

            activeSourceFlowCount++;
            AnalyzeReplay(inputPath, replay, packets);
        }

        var hardOpaque = packets
            .Where(static packet => packet.IsHardOpaqueMarkerless)
            .ToArray();
        var clientSummary = clientWorklist.RootElement.GetProperty("Summary");
        var opaqueSummary = opaqueWrapper.RootElement.GetProperty("Summary");
        var udpSummary = udpCorrection.RootElement.GetProperty("Summary");
        var tunnelSummary = serverDllTunnel.RootElement.GetProperty("Summary");
        var userCmdSummary = userCmdDecoder.RootElement.GetProperty("Summary");

        var rawAttachedLe = hardOpaque.Count(static packet => packet.RawAttachedType2LittleEndianExact);
        var rawAttachedBe = hardOpaque.Count(static packet => packet.RawAttachedType2BigEndianExact);
        var prefixAttachedLe = hardOpaque.Count(static packet => packet.EmbeddedAttachedType2LittleEndianOffset is not null);
        var prefixAttachedBe = hardOpaque.Count(static packet => packet.EmbeddedAttachedType2BigEndianOffset is not null);
        var hardEmbeddedClcMove = hardOpaque.Count(static packet => packet.EmbeddedClcMoveOffset is not null);
        var hardExactClcMove = hardOpaque.Count(static packet => packet.ExactClcMoveBoundaryKind.Length > 0);
        var hardBitSidecar = hardOpaque.Count(static packet => packet.BitSidecarBitCount is not null);
        var hardDecodedNetMessage = hardOpaque.Count(static packet => packet.DecodedNetMessageName is not null);
        var hardFragmentHeader = hardOpaque.Count(static packet => packet.FragmentHeader);
        var hardLzssWrapped = hardOpaque.Count(static packet => packet.LzssWrapped);
        var hardEmbeddedObjectMarker = hardOpaque.Count(static packet => packet.EmbeddedMarkerCount > 0);

        var hypotheses = new[]
        {
            new PcapMarkerlessBoundaryHypothesis(
                "raw-body-is-official-clc-move",
                hardExactClcMove == 0 && ReadInt(clientSummary, "ExactClcMoveBoundaryHitCount") == 0 ? "ruled-out" : "candidate",
                hardExactClcMove,
                "Ps3SourceNativeToClcMoveBoundaryResolver against the raw hard markerless body",
                "No hard markerless packet, and no user-command candidate in the broader worklist, decodes as the official CLC_Move bitstream at body offset 0."),
            new PcapMarkerlessBoundaryHypothesis(
                "embedded-offset-is-official-clc-move",
                hardEmbeddedClcMove == 0 ? "not-present-in-hard-set" : "weak-candidate",
                hardEmbeddedClcMove,
                "byte-offset scan inside hard markerless bodies",
                "The hard opaque set has no embedded CLC_Move offset hits. The broader worklist still records weak embedded offset-scan hits, but those were excluded from this strict set and need TF.elf proof before use."),
            new PcapMarkerlessBoundaryHypothesis(
                "raw-body-is-visible-attached-type2-frame",
                rawAttachedLe == 0 && rawAttachedBe == 0 ? "ruled-out" : "candidate",
                rawAttachedLe + rawAttachedBe,
                "00a5c2e8 frame-kind 2 shape at body offset 0",
                "The hard markerless user-command/loading bodies are not visible attached type-2 frames at the UDP body boundary."),
            new PcapMarkerlessBoundaryHypothesis(
                "small-prefix-before-visible-attached-type2-frame",
                prefixAttachedLe == 0 && prefixAttachedBe == 0 ? "ruled-out-for-prefix-1-to-32" : "weak-candidate",
                prefixAttachedLe + prefixAttachedBe,
                "scan offsets 1..32 for exact attached type-2 declared length",
                "A simple fixed small prefix before the 00a5c2e8 type-2 frame header is not supported across the hard opaque set."),
            new PcapMarkerlessBoundaryHypothesis(
                "tail-bit-sidecar-carries-official-clc-move",
                hardBitSidecar == 0 ? "ruled-out-in-hard-set" : "candidate",
                hardBitSidecar,
                "TF.elf 008bc978 bit-sidecar decoder",
                "The strict hard opaque set excludes visible bit-sidecar packets; any bit-sidecar command traffic must be handled separately from these markerless bodies."),
            new PcapMarkerlessBoundaryHypothesis(
                "native-fragment-header-wrapper",
                hardFragmentHeader == 0 ? "ruled-out" : "candidate",
                hardFragmentHeader,
                "008bc490 0xfffffffe fragment header",
                "The hard markerless client bodies are not native fragmented-send records."),
            new PcapMarkerlessBoundaryHypothesis(
                "native-lzss-wrapper",
                hardLzssWrapped == 0 ? "ruled-out" : "candidate",
                hardLzssWrapped,
                "Ps3SourceLzss wrapped-payload sentinel",
                "The hard markerless client bodies do not expose the currently recovered native LZSS wrapper."),
            new PcapMarkerlessBoundaryHypothesis(
                "008b8d50-drain-buffer-is-live-command-ingress",
                ReadBool(udpSummary, "UdpDrainDiscardPathProven") && !ReadBool(udpSummary, "DrainHasPostRecvConsumer") ? "ruled-out" : "needs-review",
                0,
                "re/tf2ps3/source-udp-ingress-correction.json",
                "TF.elf evidence proves 008b8d50 as an unconnected slot +0x08 drain/discard loop, not the markerless command consumer."),
            new PcapMarkerlessBoundaryHypothesis(
                "ea-server-dll-owns-winsock-tunnel",
                !ReadBool(tunnelSummary, "DirectWinsockImportPresent") ? "ruled-out" : "candidate",
                ReadBool(tunnelSummary, "DirectWinsockImportPresent") ? 1 : 0,
                "re/tf2ps3/eatf2-serverdll-tunnel-map.json",
                "The official EA TF2 server.dll has no direct Winsock import surface in the current report; it supplies server/game/usercmd semantics, while the engine owns the network tunnel."),
            new PcapMarkerlessBoundaryHypothesis(
                "engine-connected-wrapper-before-attached-reader-or-bitreader",
                "leading-native-target",
                hardOpaque.Length,
                "connected path 008b8e70 -> 00a5b6c0 -> 00a5c2e8 plus 00a5d9e0 bitreader candidate",
                "The remaining viable explanation is an engine-level wrapper/transform before the connected attached-frame/bitreader stream. This is the next TF.elf target.")
        };

        var summary = new PcapMarkerlessBoundaryHypothesisSummary(
            files.Length,
            activeSourceFlowCount,
            packets.Count,
            hardOpaque.Length,
            ReadInt(opaqueSummary, "OpaquePacketCount"),
            ReadInt(clientSummary, "UserCommandCandidateCount"),
            ReadInt(clientSummary, "ExactClcMoveBoundaryHitCount"),
            ReadInt(clientSummary, "EmbeddedClcMoveCandidateCount"),
            ReadInt(clientSummary, "AttachedFrameUserCommandCandidateCount"),
            ReadInt(clientSummary, "BitSidecarUserCommandCandidateCount"),
            rawAttachedLe,
            rawAttachedBe,
            prefixAttachedLe,
            prefixAttachedBe,
            hardEmbeddedClcMove,
            hardBitSidecar,
            hardDecodedNetMessage,
            hardFragmentHeader,
            hardLzssWrapped,
            hardEmbeddedObjectMarker,
            ReadBool(udpSummary, "UdpDrainDiscardPathProven"),
            ReadBool(udpSummary, "ConnectedSocketAttachedReaderPathProven"),
            ReadBool(tunnelSummary, "DirectWinsockImportPresent"),
            ReadString(userCmdSummary, "DecoderEntry"),
            ReadInt(userCmdSummary, "MaxCommandsPerBatch"),
            false,
            "engine-connected-wrapper-boundary-still-missing");

        return new PcapMarkerlessBoundaryHypothesisReport(
            "pcap-markerless-boundary-hypotheses",
            "Cross-checks hard markerless client command/loading bodies against TF.elf connected receive evidence and EA server.dll usercmd/tunnel evidence. This narrows the remaining native boundary without packet replay.",
            new PcapMarkerlessBoundaryHypothesisInputs(
                inputPath,
                clientCommandWorklistPath,
                opaqueMarkerlessReportPath,
                udpIngressCorrectionPath,
                serverDllTunnelMapPath,
                serverDllUserCmdDecoderPath),
            summary,
            hypotheses,
            BuildLengthFamilies(hardOpaque),
            BuildSamples(hardOpaque),
            [
                "The hard markerless client bodies are not raw CLC_Move, not visible attached type-2 frames, not simple prefix+attached-frame packets, not fragmented-send records, and not the 008b8d50 drain buffer.",
                "EA server.dll usercmd decoding is useful after the boundary is found, but current server.dll evidence does not make it the network tunnel owner.",
                "The next native decompile target is the engine wrapper that feeds the connected object +0x90 reader / helper-slice bitreader path before official usercmd decoding."
            ]);
    }

    private void AnalyzeReplay(string inputRoot, PcapActiveFlowReplay replay, List<PcapMarkerlessBoundaryPacketProbe> output)
    {
        ushort? previousSequence = null;
        var clientPacketCount = 0;
        for (var sourceStep = 0; sourceStep < replay.SourcePackets.Length; sourceStep++)
        {
            var packet = replay.SourcePackets[sourceStep];
            if (packet.Direction != PcapActiveFlowDirection.ClientToServer
                || !Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
            {
                continue;
            }

            clientPacketCount++;
            int? sequenceDelta = previousSequence is null
                ? null
                : Ps3SourceTransportPacket.SequenceDelta(previousSequence.Value, transport.CandidateSequence);
            previousSequence = transport.CandidateSequence;
            var info = Ps3SourceClientPayloadClassifier.Classify(transport, clientPacketCount, sequenceDelta);
            var exactClcMoveKind = Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var exactClcMove)
                ? exactClcMove.Kind.ToString()
                : "";
            var embeddedClcMoveOffset = TryFindEmbeddedClcMove(transport.Body, out var embeddedClcMove)
                ? embeddedClcMove.Offset
                : (int?)null;
            var semantic = Ps3SourcePayloadSemantics.Analyze(transport.Body);
            var isHardOpaque = info.Role == Ps3SourceClientPayloadRole.UserCommandCandidate
                && info.AttachedFrameKind is null
                && info.BitSidecarBitCount is null
                && info.DecodedNetMessageName is null
                && exactClcMoveKind.Length == 0
                && embeddedClcMoveOffset is null;

            output.Add(new PcapMarkerlessBoundaryPacketProbe(
                Path.GetRelativePath(inputRoot, replay.Path),
                sourceStep,
                packet.PacketIndex,
                transport.CandidateSequence,
                sequenceDelta,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                isHardOpaque,
                exactClcMoveKind,
                embeddedClcMoveOffset,
                info.AttachedFrameKind,
                info.BitSidecarBitCount,
                info.DecodedNetMessageName,
                StrictAttachedType2Exact(transport.Body, 0, littleEndian: true),
                StrictAttachedType2Exact(transport.Body, 0, littleEndian: false),
                FindStrictAttachedType2Offset(transport.Body, littleEndian: true, 1, 32),
                FindStrictAttachedType2Offset(transport.Body, littleEndian: false, 1, 32),
                Ps3SourceFragmentHeader.TryDecode(transport.Body, out _),
                Ps3SourceLzss.IsWrapped(transport.Body),
                semantic.EmbeddedMarkers.Length,
                Math.Round(semantic.Entropy, 4),
                Prefix(transport.Body, 16),
                Suffix(transport.Body, 8)));
        }
    }

    private static bool StrictAttachedType2Exact(ReadOnlySpan<byte> body, int offset, bool littleEndian)
    {
        if (offset < 0 || body.Length < offset + 7 || body[offset] != 2)
        {
            return false;
        }

        var declaredLength = littleEndian
            ? body[offset + 1] | (body[offset + 2] << 8)
            : (body[offset + 1] << 8) | body[offset + 2];
        return declaredLength > 0 && declaredLength == body.Length - offset - 7;
    }

    private static int? FindStrictAttachedType2Offset(ReadOnlySpan<byte> body, bool littleEndian, int minOffset, int maxOffset)
    {
        for (var offset = minOffset; offset <= maxOffset && offset < body.Length; offset++)
        {
            if (StrictAttachedType2Exact(body, offset, littleEndian))
            {
                return offset;
            }
        }

        return null;
    }

    private static bool TryFindEmbeddedClcMove(ReadOnlySpan<byte> body, out PcapMarkerlessBoundaryEmbeddedClcMoveCandidate candidate)
    {
        candidate = default!;
        for (var offset = 0; offset < body.Length; offset++)
        {
            var slice = body[offset..];
            foreach (var includesMessageType in new[] { true, false })
            {
                if (!Ps3SourceClcMoveMessage.TryDecode(slice, includesMessageType, out var move)
                    || move.TotalCommands == 0
                    || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
                    || move.CommandDataBitCount <= 0
                    || !move.TryDecodeUserCmdBatch(default, out var batch)
                    || batch.ConsumedBits != move.CommandDataBitCount)
                {
                    continue;
                }

                candidate = new PcapMarkerlessBoundaryEmbeddedClcMoveCandidate(
                    offset,
                    includesMessageType,
                    move.NewCommands,
                    move.BackupCommands,
                    move.CommandDataBitCount);
                return true;
            }
        }

        return false;
    }

    private static PcapMarkerlessBoundaryLengthFamily[] BuildLengthFamilies(IReadOnlyCollection<PcapMarkerlessBoundaryPacketProbe> packets)
    {
        return packets
            .GroupBy(static packet => packet.BodyLength)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Take(24)
            .Select(static group => new PcapMarkerlessBoundaryLengthFamily(
                group.Key,
                group.Count(),
                group.Select(static packet => packet.File).Distinct(StringComparer.Ordinal).Count(),
                Math.Round(group.Average(static packet => packet.Entropy), 4),
                CountBy(group.Select(static packet => packet.SequenceDelta?.ToString())).Take(8).ToArray(),
                group
                    .OrderBy(static packet => packet.File, StringComparer.Ordinal)
                    .ThenBy(static packet => packet.SourceStep)
                    .Take(4)
                    .Select(static packet => packet.ToSample())
                    .ToArray()))
            .ToArray();
    }

    private static PcapMarkerlessBoundarySample[] BuildSamples(IReadOnlyCollection<PcapMarkerlessBoundaryPacketProbe> packets)
    {
        return packets
            .OrderByDescending(static packet => packet.BodyLength)
            .ThenBy(static packet => packet.File, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .Take(64)
            .Select(static packet => packet.ToSample())
            .ToArray();
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
            ? value
            : 0;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static string ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

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

    private static PcapMarkerlessBoundaryCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapMarkerlessBoundaryCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length) =>
        body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();

    private static string Suffix(ReadOnlySpan<byte> body, int length) =>
        body.IsEmpty
            ? ""
            : Convert.ToHexString(body[^Math.Min(length, body.Length)..]).ToLowerInvariant();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapMarkerlessBoundaryHypothesisReport(
    string Status,
    string Note,
    PcapMarkerlessBoundaryHypothesisInputs Inputs,
    PcapMarkerlessBoundaryHypothesisSummary Summary,
    PcapMarkerlessBoundaryHypothesis[] Hypotheses,
    PcapMarkerlessBoundaryLengthFamily[] HardOpaqueLengthFamilies,
    PcapMarkerlessBoundarySample[] HardOpaqueSamples,
    string[] Conclusions);

public sealed record PcapMarkerlessBoundaryHypothesisInputs(
    string PcapInput,
    string ClientCommandWorklistReport,
    string OpaqueMarkerlessReport,
    string SourceUdpIngressCorrectionReport,
    string ServerDllTunnelMapReport,
    string ServerDllUserCmdDecoderReport);

public sealed record PcapMarkerlessBoundaryHypothesisSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ClientPacketProbeCount,
    int HardOpaqueMarkerlessPacketCount,
    int OpaqueReportPacketCount,
    int UserCommandCandidateCount,
    int WorklistExactClcMoveBoundaryHitCount,
    int WorklistEmbeddedClcMoveCandidateCount,
    int WorklistAttachedFrameUserCommandCandidateCount,
    int WorklistBitSidecarUserCommandCandidateCount,
    int HardRawAttachedType2LittleEndianExactHitCount,
    int HardRawAttachedType2BigEndianExactHitCount,
    int HardEmbeddedAttachedType2LittleEndianExactHitCount,
    int HardEmbeddedAttachedType2BigEndianExactHitCount,
    int HardEmbeddedClcMoveCandidateCount,
    int HardBitSidecarCandidateCount,
    int HardDecodedNetMessageCandidateCount,
    int HardFragmentHeaderCandidateCount,
    int HardNativeLzssWrappedCandidateCount,
    int HardEmbeddedObjectMarkerCandidateCount,
    bool UdpDrainDiscardPathProven,
    bool ConnectedSocketAttachedReaderPathProven,
    bool ServerDllDirectWinsockImportPresent,
    string ServerDllUserCmdDecoderEntry,
    int ServerDllMaxCommandsPerBatch,
    bool NativeMarkerlessBoundaryReady,
    string BoundaryConclusion);

public sealed record PcapMarkerlessBoundaryHypothesis(
    string Id,
    string Status,
    int SupportingPacketCount,
    string EvidenceSource,
    string Meaning);

public sealed record PcapMarkerlessBoundaryLengthFamily(
    int BodyLength,
    int Count,
    int DistinctFileCount,
    double AverageEntropy,
    PcapMarkerlessBoundaryCount[] SequenceDeltaCounts,
    PcapMarkerlessBoundarySample[] Samples);

public sealed record PcapMarkerlessBoundaryCount(string Value, int Count);

public sealed record PcapMarkerlessBoundarySample(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string NativeFrameKind,
    double Entropy,
    string BodyPrefix16Hex,
    string BodySuffix8Hex);

internal sealed record PcapMarkerlessBoundaryPacketProbe(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string NativeFrameKind,
    bool IsHardOpaqueMarkerless,
    string ExactClcMoveBoundaryKind,
    int? EmbeddedClcMoveOffset,
    byte? AttachedFrameKind,
    int? BitSidecarBitCount,
    string? DecodedNetMessageName,
    bool RawAttachedType2LittleEndianExact,
    bool RawAttachedType2BigEndianExact,
    int? EmbeddedAttachedType2LittleEndianOffset,
    int? EmbeddedAttachedType2BigEndianOffset,
    bool FragmentHeader,
    bool LzssWrapped,
    int EmbeddedMarkerCount,
    double Entropy,
    string BodyPrefix16Hex,
    string BodySuffix8Hex)
{
    public PcapMarkerlessBoundarySample ToSample() => new(
        File,
        SourceStep,
        PacketIndex,
        Sequence,
        SequenceDelta,
        PayloadLength,
        BodyLength,
        NativeFrameKind,
        Entropy,
        BodyPrefix16Hex,
        BodySuffix8Hex);
}

internal sealed record PcapMarkerlessBoundaryEmbeddedClcMoveCandidate(
    int Offset,
    bool IncludesMessageType,
    int NewCommands,
    int BackupCommands,
    int CommandDataBitCount);
