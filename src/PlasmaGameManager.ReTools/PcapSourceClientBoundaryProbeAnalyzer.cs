using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceClientBoundaryProbeAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceClientBoundaryProbeReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceClientBoundaryProbeReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath)
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var packets = files.SelectMany(static file => file.Samples).ToArray();
        var layoutProbes = packets.SelectMany(static packet => packet.LayoutProbes).ToArray();
        var offsetProbes = packets.SelectMany(static packet => packet.OffsetScanProbes).ToArray();
        var matchedLayouts = layoutProbes.Where(static probe => probe.Matched).ToArray();
        var nativeDecoded = packets.Count(static packet => packet.NativeDecodeKind is "CLC_Move" or "NET_Message");
        var hardMarkerless = packets.Length;
        var matchedPacketCount = packets.Count(static packet => packet.LayoutProbes.Any(static probe => probe.Matched));
        var offsetMatchedPacketCount = packets.Count(static packet => packet.OffsetScanProbes.Length > 0);
        var offsetOnlyPacketCount = packets.Count(static packet =>
            packet.LayoutProbes.All(static probe => !probe.Matched)
            && packet.OffsetScanProbes.Length > 0);
        var wrapperOnly = packets.Count(static packet =>
            packet.NativeDecodeKind.Length == 0
            && packet.LayoutProbes.Any(static probe => probe.Matched));
        var noRecoveredWrapper = packets.Count(static packet =>
            packet.NativeDecodeKind.Length == 0
            && packet.LayoutProbes.All(static probe => !probe.Matched));
        var ready = hardMarkerless > 0
            && nativeDecoded == hardMarkerless
            && wrapperOnly == 0
            && noRecoveredWrapper == 0;

        return new PcapSourceClientBoundaryProbeReport(
            "pcap-source-client-boundary-probes",
            "Explains hard markerless TF2 PS3 client Source packets against recovered TF.elf owner-forwarder bitstream wrapper layouts without treating heuristic command intent as native decode.",
            new PcapSourceClientBoundaryProbeSummary(
                files.Length,
                files.Count(static file => file.HasActiveSourceFlow),
                hardMarkerless,
                nativeDecoded,
                packets.Count(static packet => packet.NativeDecodeKind == "CLC_Move"),
                packets.Count(static packet => packet.NativeDecodeKind == "NET_Message"),
                matchedPacketCount,
                offsetMatchedPacketCount,
                offsetOnlyPacketCount,
                wrapperOnly,
                noRecoveredWrapper,
                matchedLayouts.Length,
                offsetProbes.Length,
                ready,
                ready
                    ? "all hard markerless client packets decode through recovered native Source boundaries"
                    : offsetOnlyPacketCount > 0
                        ? "some hard markerless client packets only match recovered TF.elf owner-forwarder layouts after an internal offset scan; recover the missing prefix/transform before treating them as native"
                    : noRecoveredWrapper > 0
                        ? "some hard markerless client packets do not match any recovered TF.elf owner-forwarder bitstream layout"
                        : wrapperOnly > 0
                            ? "hard markerless client packets match recovered wrapper layouts but inner payloads do not decode as official CLC_Move or known Source net messages"
                            : "native hard markerless boundary coverage is partial"),
            CountBy(packets.Select(static packet => packet.NativeDecodeKind.Length == 0 ? packet.Conclusion : packet.NativeDecodeKind)),
            CountBy(matchedLayouts.Select(static probe => probe.Layout)),
            CountBy(offsetProbes.Select(static probe => $"{probe.Layout}@{probe.Offset}")),
            CountBy(matchedLayouts.Select(static probe => probe.FirstNetMessageName.Length == 0 ? "unknown-message-type" : probe.FirstNetMessageName)),
            packets
                .Where(static packet => packet.NativeDecodeKind.Length == 0)
                .OrderBy(static packet => packet.File, StringComparer.Ordinal)
                .ThenBy(static packet => packet.SourceStep)
                .Take(256)
                .ToArray(),
            files);
    }

    private PcapSourceClientBoundaryProbeFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceClientBoundaryProbeFile(relativePath, false, "", "", 0, 0, 0, 0, 0, []);
        }

        var samples = new List<PcapSourceClientBoundaryProbePacket>();
        ushort? previousSequence = null;
        var clientDirectionCount = 0;
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

            samples.Add(BuildPacket(relativePath, sourceStep, raw, transport, info));
        }

        return new PcapSourceClientBoundaryProbeFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            samples.Count,
            samples.Count(static sample => sample.NativeDecodeKind.Length > 0),
            samples.Count(static sample => sample.LayoutProbes.Any(static probe => probe.Matched)),
            samples.Count(static sample => sample.OffsetScanProbes.Length > 0),
            samples.Count(static sample => sample.NativeDecodeKind.Length == 0
                && sample.LayoutProbes.All(static probe => !probe.Matched)),
            samples.ToArray());
    }

    private static PcapSourceClientBoundaryProbePacket BuildPacket(
        string file,
        int sourceStep,
        PcapActiveFlowDatagram raw,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info)
    {
        var nativeDecodeKind = "";
        var nativeDecodeDetail = "";
        var weakDecodeDetail = "";
        if (Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var boundary))
        {
            nativeDecodeKind = "CLC_Move";
            nativeDecodeDetail = $"{boundary.Kind}: new={boundary.Move.NewCommands} backup={boundary.Move.BackupCommands} commands={boundary.Batch.Commands.Count} bits={boundary.Move.TotalBitsConsumed}/{boundary.PayloadBitCount}";
        }
        else if (Ps3SourceClientNetMessageDecoder.TryDecode(transport.Body, out var netMessage))
        {
            var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(
                netMessage,
                transport.Body.AsSpan(netMessage.PayloadOffset, netMessage.PayloadLength));
            if (strength.IsStrong)
            {
                nativeDecodeKind = "NET_Message";
                nativeDecodeDetail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits}/{netMessage.PayloadBitCount}";
            }
            else
            {
                weakDecodeDetail = $"{netMessage.PayloadKind}: {netMessage.MessageName} type={netMessage.MessageType} bits={strength.ConsumedBits?.ToString() ?? "unknown"}/{netMessage.PayloadBitCount}; {strength.Reason}";
            }
        }

        var direct = ProbeDirect(transport.Body);
        var layoutProbes = Enum.GetValues<Ps3SourceOwnerForwarderBitstreamLayout>()
            .Select(layout => ProbeLayout(transport.Body, layout))
            .ToArray();
        var offsetScanProbes = ProbeLayoutOffsets(transport.Body);
        var conclusion = nativeDecodeKind.Length > 0
            ? "native-decoded"
            : weakDecodeDetail.Length > 0
                ? "weak-first-net-message-candidate"
            : layoutProbes.Any(static probe => probe.Matched)
                ? "wrapper-layout-match-inner-undecoded"
                : offsetScanProbes.Length > 0
                    ? "embedded-wrapper-layout-candidate"
                : direct.ClcHeaderCandidate.Length > 0
                    ? "direct-clc-header-only"
                : direct.FirstNetMessageName.Length > 0
                    ? "direct-first-message-id-only"
                    : "no-recovered-wrapper-layout-match";

        return new PcapSourceClientBoundaryProbePacket(
            file,
            sourceStep,
            raw.PacketIndex,
            raw.TimestampMicroseconds,
            transport.CandidateSequence,
            info.SequenceDelta,
            transport.PayloadLength,
            transport.Body.Length,
            info.Role.ToString(),
            info.Shape.ToString(),
            info.BodyPrefixHex,
            nativeDecodeKind,
            nativeDecodeDetail.Length > 0 ? nativeDecodeDetail : weakDecodeDetail,
            conclusion,
            direct,
            layoutProbes,
            offsetScanProbes,
            Prefix(transport.Body, 32));
    }

    private static PcapSourceClientBoundaryDirectProbe ProbeDirect(ReadOnlySpan<byte> body)
    {
        var messageType = Ps3SourceNetMessages.TryReadMessageType(body, out var netType)
            ? (int?)netType
            : null;
        var messageName = messageType is { } type
            ? ClientNetMessageName(type)
            : "";
        var clcHeader = ClcHeaderCandidate(body, body.Length * 8);
        return new PcapSourceClientBoundaryDirectProbe(messageType, messageName, clcHeader);
    }

    private static PcapSourceClientBoundaryLayoutProbe ProbeLayout(
        ReadOnlySpan<byte> body,
        Ps3SourceOwnerForwarderBitstreamLayout layout)
    {
        if (!Ps3SourcePayloadObjectFrame.TryReadOwnerForwarderBitstream(
                body,
                layout,
                out var bitCount,
                out var payloadOffset,
                out var payloadLength,
                out var readerOrPointerFieldOffset)
            || payloadLength <= 0
            || payloadOffset + payloadLength > body.Length)
        {
            return new PcapSourceClientBoundaryLayoutProbe(
                layout.ToString(),
                false,
                0,
                0,
                0,
                0,
                null,
                "",
                "",
                "no valid TF.elf bit-count/payload boundary for this owner-forwarder layout");
        }

        var payload = body.Slice(payloadOffset, payloadLength);
        var messageType = Ps3SourceNetMessages.TryReadMessageType(payload, out var netType)
            ? (int?)netType
            : null;
        var messageName = messageType is { } type
            ? ClientNetMessageName(type)
            : "";
        var clcHeader = ClcHeaderCandidate(payload, bitCount);
        var decodeReason = messageName.Length > 0
            ? "wrapper exposes a known client net-message type"
            : clcHeader.Length > 0
                ? "wrapper exposes a partial CLC_Move header but command batch did not pass official decode"
                : "wrapper bit-count is valid but inner bitstream is not a known client net-message or CLC_Move";

        return new PcapSourceClientBoundaryLayoutProbe(
            layout.ToString(),
            true,
            bitCount,
            payloadOffset,
            payloadLength,
            readerOrPointerFieldOffset,
            messageType,
            messageName,
            clcHeader,
            decodeReason);
    }

    private static PcapSourceClientBoundaryOffsetProbe[] ProbeLayoutOffsets(ReadOnlySpan<byte> body)
    {
        var probes = new List<PcapSourceClientBoundaryOffsetProbe>();
        var maxOffset = Math.Min(64, Math.Max(0, body.Length - 8));
        foreach (var layout in Enum.GetValues<Ps3SourceOwnerForwarderBitstreamLayout>())
        {
            for (var offset = 1; offset <= maxOffset; offset++)
            {
                var probe = ProbeLayout(body[offset..], layout);
                if (!probe.Matched)
                {
                    continue;
                }

                probes.Add(new PcapSourceClientBoundaryOffsetProbe(
                    offset,
                    probe.Layout,
                    probe.BitCount,
                    offset + probe.PayloadOffset,
                    probe.PayloadLength,
                    offset + probe.ReaderOrPointerFieldOffset,
                    probe.FirstNetMessageType,
                    probe.FirstNetMessageName,
                    probe.ClcHeaderCandidate,
                    probe.Reason));
                if (probes.Count >= 8)
                {
                    return probes.ToArray();
                }
            }
        }

        return probes.ToArray();
    }

    private static string ClcHeaderCandidate(ReadOnlySpan<byte> payload, int bitCount)
    {
        var candidates = new List<string>();
        foreach (var includesMessageType in new[] { true, false })
        {
            if (!Ps3SourceClcMoveMessage.TryDecode(payload, includesMessageType, out var move)
                || move.TotalCommands <= 0
                || move.TotalCommands > Ps3SourceClientCommandBatch.OfficialMaxCommandsPerBatch
                || move.TotalBitsConsumed > bitCount)
            {
                continue;
            }

            var batchOk = move.TryDecodeUserCmdBatch(default, out var batch)
                && batch.ConsumedBits == move.CommandDataBitCount;
            candidates.Add($"{(includesMessageType ? "with-type" : "without-type")}:new={move.NewCommands},backup={move.BackupCommands},cmdBits={move.CommandDataBitCount},totalBits={move.TotalBitsConsumed},batch={batchOk}");
        }

        return string.Join("; ", candidates);
    }

    private static string ClientNetMessageName(int messageType) =>
        messageType switch
        {
            Ps3SourceNetMessageConstants.NetTick => "NET_Tick",
            Ps3SourceNetMessageConstants.NetStringCmd => "NET_StringCmd",
            Ps3SourceNetMessageConstants.NetSetConVar => "NET_SetConVar",
            Ps3SourceNetMessageConstants.NetSignonState => "NET_SignonState",
            Ps3SourceNetMessageConstants.ClcClientInfo => "CLC_ClientInfo",
            Ps3SourceNetMessageConstants.ClcVoiceData => "CLC_VoiceData",
            Ps3SourceNetMessageConstants.ClcBaselineAck => "CLC_BaselineAck",
            Ps3SourceNetMessageConstants.ClcListenEvents => "CLC_ListenEvents",
            Ps3SourceNetMessageConstants.ClcRespondCvarValue => "CLC_RespondCvarValue",
            Ps3SourceNetMessageConstants.ClcFileCrcCheck => "CLC_FileCRCCheck",
            _ => ""
        };

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

    private static PcapSourceClientBoundaryProbeCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceClientBoundaryProbeCount(group.Key, group.Count()))
            .ToArray();
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
        return Directory.Exists(inputPath)
            ? Path.GetRelativePath(inputPath, file)
            : Path.GetFileName(file);
    }

    private static string Prefix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[..Math.Min(length, body.Length)]).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record PcapSourceClientBoundaryProbeReport(
    string Status,
    string Note,
    PcapSourceClientBoundaryProbeSummary Summary,
    PcapSourceClientBoundaryProbeCount[] ConclusionCounts,
    PcapSourceClientBoundaryProbeCount[] MatchedLayoutCounts,
    PcapSourceClientBoundaryProbeCount[] EmbeddedOffsetLayoutCounts,
    PcapSourceClientBoundaryProbeCount[] MatchedLayoutFirstMessageCounts,
    PcapSourceClientBoundaryProbePacket[] UndecodedSamples,
    PcapSourceClientBoundaryProbeFile[] Files);

public sealed record PcapSourceClientBoundaryProbeSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int HardMarkerlessPacketCount,
    int NativeDecodedPacketCount,
    int NativeClcMovePacketCount,
    int NativeNetMessagePacketCount,
    int WrapperLayoutMatchedPacketCount,
    int EmbeddedOffsetLayoutMatchedPacketCount,
    int EmbeddedOffsetOnlyLayoutMatchedPacketCount,
    int WrapperLayoutMatchedButUndecodedPacketCount,
    int NoRecoveredWrapperLayoutPacketCount,
    int MatchedLayoutProbeCount,
    int EmbeddedOffsetProbeCount,
    bool NativeSourceInputReady,
    string Conclusion);

public sealed record PcapSourceClientBoundaryProbeCount(
    string Value,
    int Count);

public sealed record PcapSourceClientBoundaryProbeFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int HardMarkerlessPacketCount,
    int NativeDecodedPacketCount,
    int WrapperLayoutMatchedPacketCount,
    int EmbeddedOffsetLayoutMatchedPacketCount,
    int NoRecoveredWrapperLayoutPacketCount,
    PcapSourceClientBoundaryProbePacket[] Samples);

public sealed record PcapSourceClientBoundaryProbePacket(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string Shape,
    string BodyPrefixHex,
    string NativeDecodeKind,
    string NativeDecodeDetail,
    string Conclusion,
    PcapSourceClientBoundaryDirectProbe DirectProbe,
    PcapSourceClientBoundaryLayoutProbe[] LayoutProbes,
    PcapSourceClientBoundaryOffsetProbe[] OffsetScanProbes,
    string BodyPrefix32Hex);

public sealed record PcapSourceClientBoundaryDirectProbe(
    int? FirstNetMessageType,
    string FirstNetMessageName,
    string ClcHeaderCandidate);

public sealed record PcapSourceClientBoundaryLayoutProbe(
    string Layout,
    bool Matched,
    int BitCount,
    int PayloadOffset,
    int PayloadLength,
    int ReaderOrPointerFieldOffset,
    int? FirstNetMessageType,
    string FirstNetMessageName,
    string ClcHeaderCandidate,
    string Reason);

public sealed record PcapSourceClientBoundaryOffsetProbe(
    int Offset,
    string Layout,
    int BitCount,
    int PayloadOffset,
    int PayloadLength,
    int ReaderOrPointerFieldOffset,
    int? FirstNetMessageType,
    string FirstNetMessageName,
    string ClcHeaderCandidate,
    string Reason);
