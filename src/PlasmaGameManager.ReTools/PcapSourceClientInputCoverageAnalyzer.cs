using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapSourceClientInputCoverageAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapSourceClientInputCoverageReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapSourceClientInputCoverageReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath);
        var reports = files
            .Select(file => AnalyzeFile(inputPath, file))
            .ToArray();
        var packets = reports.SelectMany(static file => file.Samples).ToArray();
        return new PcapSourceClientInputCoverageReport(
            "pcap-source-client-input-coverage",
            BuildSummary(reports),
            CountBy(packets.Select(static packet => packet.Outcome)),
            CountBy(packets.Select(static packet => packet.CommandRole)),
            CountBy(packets.Select(static packet => packet.BoundaryKind)),
            CountBy(packets.Select(static packet => packet.NetMessageName)),
            CountBy(packets.Select(static packet => packet.Role)),
            BuildUnknownSamples(packets),
            reports);
    }

    private PcapSourceClientInputCoverageFile AnalyzeFile(string inputPath, string file)
    {
        var relativePath = DisplayPath(inputPath, file);
        var replay = _extractor.Extract(file);
        if (replay is null)
        {
            return new PcapSourceClientInputCoverageFile(
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
                []);
        }

        var samples = new List<PcapSourceClientInputCoveragePacket>();
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
            samples.Add(BuildPacket(relativePath, sourceStep, raw, transport, info));
        }

        return new PcapSourceClientInputCoverageFile(
            relativePath,
            true,
            replay.ClientEndpoint,
            replay.ServerEndpoint,
            samples.Count,
            samples.Count(static sample => sample.IsNativeCovered),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.NativeClcMove),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.NativeClientNetMessage),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.WeakClientNetMessageCandidate),
            samples.Count(static sample => sample.IsHardMarkerless && sample.Outcome == ClientInputOutcomes.WeakClientNetMessageCandidate),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.DocumentedIgnore),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.HeuristicCommandIntent),
            samples.Count(static sample => sample.IsHardMarkerless),
            samples.Count(static sample => sample.IsHardMarkerless && sample.Outcome == ClientInputOutcomes.HeuristicCommandIntent),
            samples.Count(static sample => sample.Outcome == ClientInputOutcomes.Unknown),
            samples.Count(static sample => sample.IsHardMarkerless && sample.Outcome == ClientInputOutcomes.Unknown),
            samples.ToArray());
    }

    private static PcapSourceClientInputCoveragePacket BuildPacket(
        string file,
        int sourceStep,
        PcapActiveFlowDatagram raw,
        Ps3SourceTransportPacket transport,
        Ps3SourceClientPayloadInfo info)
    {
        var hardMarkerless = IsHardMarkerless(info);
        if (Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var boundary))
        {
            return new PcapSourceClientInputCoveragePacket(
                file,
                sourceStep,
                raw.PacketIndex,
                raw.TimestampMicroseconds,
                transport.CandidateSequence,
                info.SequenceDelta,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                info.BodyPrefixHex,
                ClientInputOutcomes.NativeClcMove,
                "CLC_Move",
                "apply-clc-move-usercmd-batch",
                "",
                true,
                hardMarkerless,
                boundary.Kind.ToString(),
                boundary.PayloadOffset,
                boundary.PayloadLength,
                boundary.PayloadBitCount,
                boundary.Move.TotalBitsConsumed,
                boundary.Move.NewCommands,
                boundary.Move.BackupCommands,
                boundary.Move.CommandDataBitCount,
                boundary.Batch.Commands.Count,
                boundary.Batch.ConsumedBits,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Prefix(transport.Body, 24));
        }

        if (IsDocumentedIgnored(info, out var ignoredReason))
        {
            var stateTransition = ignoredReason == "associated-slot90-reset-zero-state"
                ? "tfelf-associated-slot90-reset"
                : "no-native-source-state-transition";
            return new PcapSourceClientInputCoveragePacket(
                file,
                sourceStep,
                raw.PacketIndex,
                raw.TimestampMicroseconds,
                transport.CandidateSequence,
                info.SequenceDelta,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                info.BodyPrefixHex,
                ClientInputOutcomes.DocumentedIgnore,
                ignoredReason,
                stateTransition,
                ignoredReason,
                false,
                hardMarkerless,
                "",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Prefix(transport.Body, 24));
        }

        if (Ps3SourceClientNetMessageDecoder.TryDecode(transport.Body, out var netMessage))
        {
            var strength = Ps3SourceClientNetMessageDecoder.AssessDecodeStrength(
                netMessage,
                transport.Body.AsSpan(netMessage.PayloadOffset, netMessage.PayloadLength));
            if (!strength.IsStrong)
            {
                return new PcapSourceClientInputCoveragePacket(
                    file,
                    sourceStep,
                    raw.PacketIndex,
                    raw.TimestampMicroseconds,
                    transport.CandidateSequence,
                    info.SequenceDelta,
                    transport.PayloadLength,
                    transport.Body.Length,
                    info.Role.ToString(),
                    info.NativeFrameKind.ToString(),
                    info.Shape.ToString(),
                    info.BodyPrefixHex,
                    ClientInputOutcomes.WeakClientNetMessageCandidate,
                    netMessage.MessageName,
                    "weak-first-net-message-not-native-state",
                    strength.Reason,
                    false,
                    hardMarkerless,
                    "",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    netMessage.PayloadKind.ToString(),
                    netMessage.PayloadOffset,
                    netMessage.PayloadLength,
                    netMessage.PayloadBitCount,
                    netMessage.MessageType,
                    netMessage.MessageName,
                    null,
                    Prefix(transport.Body, 24));
            }

            return new PcapSourceClientInputCoveragePacket(
                file,
                sourceStep,
                raw.PacketIndex,
                raw.TimestampMicroseconds,
                transport.CandidateSequence,
                info.SequenceDelta,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                info.BodyPrefixHex,
                ClientInputOutcomes.NativeClientNetMessage,
                netMessage.MessageName,
                StateTransitionForNetMessage(netMessage.MessageName),
                "",
                true,
                hardMarkerless,
                "",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                netMessage.PayloadKind.ToString(),
                netMessage.PayloadOffset,
                netMessage.PayloadLength,
                netMessage.PayloadBitCount,
                netMessage.MessageType,
                netMessage.MessageName,
                null,
                Prefix(transport.Body, 24));
        }

        if (Ps3SourceClientCommandIntent.TryDecode(info, transport.Body, out var intent))
        {
            return new PcapSourceClientInputCoveragePacket(
                file,
                sourceStep,
                raw.PacketIndex,
                raw.TimestampMicroseconds,
                transport.CandidateSequence,
                info.SequenceDelta,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                info.BodyPrefixHex,
                ClientInputOutcomes.HeuristicCommandIntent,
                intent.HasMovement ? "heuristic-movement-intent" : "heuristic-command-intent",
                "heuristic-only-not-native-state",
                "no recovered native CLC_Move/net-message boundary; heuristic intent is diagnostics only",
                false,
                hardMarkerless,
                "",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new PcapSourceClientInputIntent(
                    intent.ForwardMove,
                    intent.SideMove,
                    intent.UpMove,
                    Math.Round(intent.YawDelta, 4),
                    Math.Round(intent.PitchDelta, 4),
                    intent.Buttons,
                    intent.WeaponSlotHint,
                    intent.TeamHint,
                    intent.ClassHint,
                    intent.HasMovement),
                Prefix(transport.Body, 24));
        }

        return new PcapSourceClientInputCoveragePacket(
            file,
            sourceStep,
            raw.PacketIndex,
            raw.TimestampMicroseconds,
            transport.CandidateSequence,
            info.SequenceDelta,
            transport.PayloadLength,
            transport.Body.Length,
            info.Role.ToString(),
            info.NativeFrameKind.ToString(),
            info.Shape.ToString(),
            info.BodyPrefixHex,
            ClientInputOutcomes.Unknown,
            "unknown-client-upload",
            "blocked-native-input-decode",
            UnknownReason(info),
            false,
            hardMarkerless,
            "",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Prefix(transport.Body, 24));
    }

    private static PcapSourceClientInputCoverageSummary BuildSummary(PcapSourceClientInputCoverageFile[] files)
    {
        var active = files.Where(static file => file.HasActiveSourceFlow).ToArray();
        var hardMarkerless = active.Sum(static file => file.HardMarkerlessClientPacketCount);
        var unknownHardMarkerless = active.Sum(static file => file.UnknownHardMarkerlessClientPacketCount);
        var heuristicHardMarkerless = active.Sum(static file => file.HeuristicOnlyHardMarkerlessClientPacketCount);
        var weakHardMarkerless = active.Sum(static file => file.WeakNetMessageCandidateHardMarkerlessPacketCount);
        var nativeCovered = active.Sum(static file => file.NativeCoveredClientPacketCount);
        var decodedClc = active.Sum(static file => file.DecodedClcMovePacketCount);
        var decodedNetMessages = active.Sum(static file => file.DecodedNetMessagePacketCount);
        var ready = active.Length > 0
            && hardMarkerless > 0
            && unknownHardMarkerless == 0
            && heuristicHardMarkerless == 0
            && weakHardMarkerless == 0
            && nativeCovered > 0;
        return new PcapSourceClientInputCoverageSummary(
            files.Length,
            active.Length,
            active.Sum(static file => file.ClientSourcePacketCount),
            nativeCovered,
            decodedClc,
            decodedNetMessages,
            active.Sum(static file => file.WeakNetMessageCandidatePacketCount),
            weakHardMarkerless,
            active.Sum(static file => file.DocumentedIgnoredPacketCount),
            active.Sum(static file => file.HeuristicOnlyPacketCount),
            hardMarkerless,
            heuristicHardMarkerless,
            active.Sum(static file => file.UnknownPacketCount),
            unknownHardMarkerless,
            ready,
            ready
                ? "all hard markerless client uploads decode through native CLC_Move/net-message paths"
                : unknownHardMarkerless > 0
                    ? "hard markerless client uploads still have unknown packet bodies"
                    : weakHardMarkerless > 0
                        ? "hard markerless client uploads still include weak first-message matches with unexplained non-zero trailing bits"
                    : heuristicHardMarkerless > 0
                        ? "hard markerless client uploads still depend on heuristic command-intent fallback"
                        : nativeCovered == 0
                            ? "no native client input decode coverage observed"
                            : "native client input coverage is partial");
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

    private static bool IsDocumentedIgnored(Ps3SourceClientPayloadInfo info, out string reason)
    {
        if (info.PayloadObjectFrameKind == nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken)
            && info.AttachedFrameKind is null
            && info.BitSidecarOffset is null
            && info.Role is Ps3SourceClientPayloadRole.UserCommandCandidate
                or Ps3SourceClientPayloadRole.BinaryControlPayload)
        {
            reason = "associated-slot90-reset-zero-state";
            return true;
        }

        reason = info.Role switch
        {
            Ps3SourceClientPayloadRole.InitialHandoffProbe => "initial-handoff-probe",
            Ps3SourceClientPayloadRole.ReliableAssociationProbe => "reliable-association-probe",
            Ps3SourceClientPayloadRole.ShortControlAck => "short-control-ack",
            Ps3SourceClientPayloadRole.SetupControlPayload => "setup-control-payload",
            Ps3SourceClientPayloadRole.EmbeddedObjectNotice => "embedded-object-notice",
            Ps3SourceClientPayloadRole.FragmentedClientPayload => "fragmented-client-payload-awaits-reassembly",
            Ps3SourceClientPayloadRole.AttachedPlayerControlFrame => "attached-player-control-frame",
            _ => ""
        };
        return reason.Length > 0;
    }

    private static string StateTransitionForNetMessage(string messageName)
    {
        return messageName switch
        {
            "NET_Tick" => "client-tick",
            "NET_StringCmd" => "client-string-command",
            "NET_SetConVar" => "client-convar-update",
            "NET_SignonState" => "client-signon-state",
            "CLC_ClientInfo" => "client-info",
            "CLC_VoiceData" => "client-voice-data",
            "CLC_BaselineAck" => "baseline-ack",
            "CLC_ListenEvents" => "listen-events",
            "CLC_RespondCvarValue" => "cvar-response",
            "CLC_FileCRCCheck" => "file-crc-check",
            _ => "client-net-message"
        };
    }

    private static string UnknownReason(Ps3SourceClientPayloadInfo info)
    {
        if (info.PayloadObjectFrameKind is not null)
        {
            return $"payload object {info.PayloadObjectFrameKind} did not expose a native CLC_Move/net-message payload";
        }

        if (info.AttachedFrameKind is not null)
        {
            return $"attached frame kind {info.AttachedFrameKind} did not decode as native CLC_Move/net-message";
        }

        return $"role {info.Role} bodyLength {info.BodyLength} did not decode through native client input paths";
    }

    private static PcapSourceClientInputCoveragePacket[] BuildUnknownSamples(PcapSourceClientInputCoveragePacket[] packets)
    {
        return packets
            .Where(static packet => packet.Outcome == ClientInputOutcomes.Unknown
                || packet.IsHardMarkerless && packet.Outcome == ClientInputOutcomes.WeakClientNetMessageCandidate
                || packet.IsHardMarkerless && packet.Outcome == ClientInputOutcomes.HeuristicCommandIntent)
            .OrderByDescending(static packet => packet.IsHardMarkerless)
            .ThenBy(static packet => packet.File, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .Take(128)
            .ToArray();
    }

    private static PcapSourceClientInputCoverageCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapSourceClientInputCoverageCount(group.Key, group.Count()))
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

    private static class ClientInputOutcomes
    {
        public const string NativeClcMove = "native-clc-move";
        public const string NativeClientNetMessage = "native-client-net-message";
        public const string WeakClientNetMessageCandidate = "weak-client-net-message-candidate";
        public const string DocumentedIgnore = "documented-ignore";
        public const string HeuristicCommandIntent = "heuristic-command-intent";
        public const string Unknown = "unknown";
    }
}

public sealed record PcapSourceClientInputCoverageReport(
    string Status,
    PcapSourceClientInputCoverageSummary Summary,
    PcapSourceClientInputCoverageCount[] OutcomeCounts,
    PcapSourceClientInputCoverageCount[] CommandRoleCounts,
    PcapSourceClientInputCoverageCount[] BoundaryKindCounts,
    PcapSourceClientInputCoverageCount[] NetMessageCounts,
    PcapSourceClientInputCoverageCount[] ClientRoleCounts,
    PcapSourceClientInputCoveragePacket[] UnknownSamples,
    PcapSourceClientInputCoverageFile[] Files);

public sealed record PcapSourceClientInputCoverageSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ClientSourcePacketCount,
    int NativeCoveredClientPacketCount,
    int DecodedClcMovePacketCount,
    int DecodedNetMessagePacketCount,
    int WeakNetMessageCandidatePacketCount,
    int WeakNetMessageCandidateHardMarkerlessPacketCount,
    int DocumentedIgnoredPacketCount,
    int HeuristicOnlyPacketCount,
    int HardMarkerlessClientPacketCount,
    int HeuristicOnlyHardMarkerlessClientPacketCount,
    int UnknownPacketCount,
    int UnknownHardMarkerlessClientPacketCount,
    bool NativeSourceInputReady,
    string CoverageConclusion);

public sealed record PcapSourceClientInputCoverageFile(
    string File,
    bool HasActiveSourceFlow,
    string ClientEndpoint,
    string ServerEndpoint,
    int ClientSourcePacketCount,
    int NativeCoveredClientPacketCount,
    int DecodedClcMovePacketCount,
    int DecodedNetMessagePacketCount,
    int WeakNetMessageCandidatePacketCount,
    int WeakNetMessageCandidateHardMarkerlessPacketCount,
    int DocumentedIgnoredPacketCount,
    int HeuristicOnlyPacketCount,
    int HardMarkerlessClientPacketCount,
    int HeuristicOnlyHardMarkerlessClientPacketCount,
    int UnknownPacketCount,
    int UnknownHardMarkerlessClientPacketCount,
    PcapSourceClientInputCoveragePacket[] Samples);

public sealed record PcapSourceClientInputCoverageCount(
    string Value,
    int Count);

public sealed record PcapSourceClientInputCoveragePacket(
    string File,
    int SourceStep,
    long PacketIndex,
    long TimestampMicroseconds,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string NativeFrameKind,
    string Shape,
    string BodyPrefixHex,
    string Outcome,
    string CommandRole,
    string StateTransition,
    string FallbackReason,
    bool IsNativeCovered,
    bool IsHardMarkerless,
    string BoundaryKind,
    int? BoundaryPayloadOffset,
    int? BoundaryPayloadLength,
    int? BoundaryPayloadBitCount,
    int? BoundaryConsumedBits,
    int? ClcMoveNewCommands,
    int? ClcMoveBackupCommands,
    int? ClcMoveCommandDataBitCount,
    int? DecodedUserCmdCount,
    int? DecodedUserCmdConsumedBits,
    string? NetMessagePayloadKind,
    int? NetMessagePayloadOffset,
    int? NetMessagePayloadLength,
    int? NetMessagePayloadBitCount,
    int? NetMessageType,
    string? NetMessageName,
    PcapSourceClientInputIntent? HeuristicIntent,
    string BodyPrefix24Hex);

public sealed record PcapSourceClientInputIntent(
    short ForwardMove,
    short SideMove,
    short UpMove,
    double YawDelta,
    double PitchDelta,
    byte Buttons,
    byte WeaponSlotHint,
    byte TeamHint,
    byte ClassHint,
    bool HasMovement);
