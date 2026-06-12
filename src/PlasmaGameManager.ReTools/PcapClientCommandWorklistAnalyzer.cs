using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapClientCommandWorklistAnalyzer
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapClientCommandWorklistReport> AnalyzeDirectoryAsync(string inputPath, string outputPath)
    {
        var report = AnalyzeDirectory(inputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    public PcapClientCommandWorklistReport AnalyzeDirectory(string inputPath)
    {
        var files = EnumeratePcapInputs(inputPath);
        var allPackets = new List<ClientCommandPacketProbe>();
        var activeFiles = 0;
        foreach (var file in files)
        {
            var replay = _extractor.Extract(file);
            if (replay is null)
            {
                continue;
            }

            activeFiles++;
            AnalyzeReplay(inputPath, replay, allPackets);
        }

        var commandCandidates = allPackets
            .Where(static packet => packet.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString())
            .ToArray();
        var exactBoundaryHits = allPackets.Count(static packet => packet.ExactBoundaryKind.Length > 0);
        var embeddedCandidates = allPackets.Count(static packet => packet.EmbeddedCandidateOffset is not null);
        var summary = new PcapClientCommandWorklistSummary(
            files.Length,
            activeFiles,
            allPackets.Count,
            commandCandidates.Length,
            exactBoundaryHits,
            embeddedCandidates,
            commandCandidates.Count(static packet => packet.MissReason == MarkerlessOpaqueMissReason),
            commandCandidates.Count(static packet => packet.MissReason == DecodedNonMoveMissReason),
            allPackets.Count(static packet => packet.AttachedFrameKind is not null),
            commandCandidates.Count(static packet => packet.AttachedFrameKind is not null),
            allPackets.Count(static packet => packet.BitSidecarBitCount is not null),
            commandCandidates.Count(static packet => packet.BitSidecarBitCount is not null),
            allPackets.Count(static packet => packet.PayloadObjectFrameKind is not null),
            commandCandidates.Count(static packet => packet.PayloadObjectFrameKind is not null),
            allPackets.Count(static packet => packet.PayloadObjectInnerDecodeCandidate),
            allPackets.Count(static packet => packet.ExactBoundaryKind == Ps3SourceNativeToClcMoveBoundaryKind.PayloadObjectInnerPayload.ToString()),
            allPackets.Count(static packet => packet.DecodedNetMessagePayloadKind == Ps3SourceClientNetMessagePayloadKind.PayloadObjectInnerPayload.ToString()),
            commandCandidates.Count(static packet => packet.DecodedCommandIntent),
            commandCandidates.Count(static packet => packet.HasMovementIntent),
            commandCandidates.Count(static packet => packet.DecodedNetMessageName is not null),
            BuildConclusion(commandCandidates.Length, exactBoundaryHits, embeddedCandidates));

        return new PcapClientCommandWorklistReport(
            "pcap-client-command-worklist",
            summary,
            BuildFamilyGroups(commandCandidates),
            BuildDecodedMessageGroups(commandCandidates),
            BuildMissCounts(commandCandidates),
            BuildSamples(commandCandidates),
            [
                "Official server.dll CUserCmd decoding is recovered, but the PCAP corpus still has zero exact PS3-native CLC_Move wrapper hits.",
                "The dominant live client-command family must be reduced at the wrapper level before applying official usercmd batches to simulation.",
                "Embedded CLC_Move candidates are weak offset-scan hits only; they are not sufficient proof of packet boundaries."
            ]);
    }

    private static void AnalyzeReplay(string inputRoot, PcapActiveFlowReplay replay, List<ClientCommandPacketProbe> output)
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
            Ps3SourceNativeToClcMoveBoundaryResolver.TryResolve(info, transport.Body, out var exactBoundary);
            var hasEmbedded = false;
            PcapSourceEmbeddedClcMoveCandidate? embedded = null;
            if (exactBoundary is null && TryFindEmbeddedClcMove(transport.Body, out var embeddedCandidate))
            {
                hasEmbedded = true;
                embedded = embeddedCandidate;
            }
            Ps3SourceClientCommandIntent.TryDecode(info, transport.Body, out var intent);

            output.Add(new ClientCommandPacketProbe(
                Path.GetRelativePath(inputRoot, replay.Path),
                sourceStep,
                packet.PacketIndex,
                transport.CandidateSequence,
                sequenceDelta,
                packet.TimestampMicroseconds,
                transport.PayloadLength,
                transport.Body.Length,
                info.Role.ToString(),
                info.NativeFrameKind.ToString(),
                info.Shape.ToString(),
                info.BodyPrefixHex,
                Suffix(transport.Body, 8),
                info.AttachedFrameKind,
                info.AttachedFrameDeclaredLength,
                info.BitSidecarOffset,
                info.BitSidecarBitCount,
                info.BitSidecarPayloadLength,
                info.PayloadObjectFrameKind,
                info.PayloadObjectHeaderValue,
                info.PayloadObjectSignedHeaderValue,
                info.PayloadObjectInnerPayloadOffset,
                info.PayloadObjectInnerPayloadLength,
                info.PayloadObjectAssociatedToken,
                info.PayloadObjectFragmentIndex,
                info.PayloadObjectFragmentTotalCount,
                IsPayloadObjectInnerDecodeCandidate(info),
                info.DecodedNetMessageType,
                info.DecodedNetMessageName,
                info.DecodedNetMessagePayloadKind,
                info.DecodedNetMessagePayloadOffset,
                info.DecodedNetMessagePayloadLength,
                info.DecodedNetMessagePayloadBitCount,
                exactBoundary?.Kind.ToString() ?? "",
                exactBoundary?.PayloadOffset,
                exactBoundary?.PayloadLength,
                exactBoundary?.Move.NewCommands,
                exactBoundary?.Move.BackupCommands,
                exactBoundary?.Move.CommandDataBitCount,
                embedded?.Offset,
                embedded?.IncludesMessageType,
                embedded?.NewCommands,
                embedded?.BackupCommands,
                embedded?.CommandDataBitCount,
                intent is not null,
                intent?.HasMovement == true,
                intent?.ForwardMove,
                intent?.SideMove,
                intent?.UpMove,
                intent?.Buttons,
                intent?.WeaponSlotHint,
                intent?.TeamHint,
                intent?.ClassHint,
                MissReason(info, exactBoundary, hasEmbedded)));
        }
    }

    private static PcapClientCommandFamilyGroup[] BuildFamilyGroups(IReadOnlyCollection<ClientCommandPacketProbe> packets)
    {
        return packets
            .GroupBy(static packet => new CommandFamilyKey(
                packet.Role,
                packet.NativeFrameKind,
                packet.BodyLength,
                packet.AttachedFrameKind,
                packet.BitSidecarBitCount),
                CommandFamilyKeyComparer.Instance)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key.Role, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.NativeFrameKind, StringComparer.Ordinal)
            .Take(48)
            .Select(static group => new PcapClientCommandFamilyGroup(
                group.Key.Role,
                group.Key.NativeFrameKind,
                group.Key.BodyLength,
                group.Key.AttachedFrameKind,
                group.Key.BitSidecarBitCount,
                group.Count(),
                group.Select(static packet => packet.File).Distinct(StringComparer.Ordinal).Count(),
                group.Count(static packet => packet.ExactBoundaryKind.Length > 0),
                group.Count(static packet => packet.EmbeddedCandidateOffset is not null),
                group.Count(static packet => packet.DecodedCommandIntent),
                group.Count(static packet => packet.HasMovementIntent),
                CountBy(group.Select(static packet => packet.BodyPrefixHex)).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.BodySuffixHex)).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.HeuristicButtons?.ToString("x2"))).Take(8).ToArray(),
                CountBy(group.Select(static packet => packet.MissReason)).Take(8).ToArray(),
                group
                    .OrderBy(static packet => packet.File, StringComparer.Ordinal)
                    .ThenBy(static packet => packet.SourceStep)
                    .Take(8)
                    .Select(static packet => packet.ToSample())
                    .ToArray()))
            .ToArray();
    }

    private static PcapClientCommandWorklistCount[] BuildMissCounts(IReadOnlyCollection<ClientCommandPacketProbe> packets)
    {
        return CountBy(packets.Select(static packet => packet.MissReason));
    }

    private static PcapClientCommandDecodedMessageGroup[] BuildDecodedMessageGroups(IReadOnlyCollection<ClientCommandPacketProbe> packets)
    {
        return packets
            .Where(static packet => packet.DecodedNetMessageName is not null)
            .GroupBy(static packet => new DecodedMessageKey(
                packet.DecodedNetMessageName!,
                packet.DecodedNetMessagePayloadKind ?? "",
                packet.DecodedNetMessagePayloadOffset,
                packet.DecodedNetMessagePayloadLength,
                packet.DecodedNetMessagePayloadBitCount,
                packet.BodyLength,
                packet.MissReason),
                DecodedMessageKeyComparer.Instance)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key.MessageName, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.BodyLength)
            .Take(64)
            .Select(static group => new PcapClientCommandDecodedMessageGroup(
                group.Key.MessageName,
                group.Key.PayloadKind,
                group.Key.PayloadOffset,
                group.Key.PayloadLength,
                group.Key.PayloadBitCount,
                group.Key.BodyLength,
                group.Key.MissReason,
                group.Count(),
                group.Select(static packet => packet.File).Distinct(StringComparer.Ordinal).Count(),
                CountBy(group.Select(static packet => packet.BodyPrefixHex)).Take(8).ToArray(),
                group
                    .OrderBy(static packet => packet.File, StringComparer.Ordinal)
                    .ThenBy(static packet => packet.SourceStep)
                    .Take(8)
                    .Select(static packet => packet.ToSample())
                    .ToArray()))
            .ToArray();
    }

    private static PcapClientCommandPacketSample[] BuildSamples(IReadOnlyCollection<ClientCommandPacketProbe> packets)
    {
        return packets
            .OrderByDescending(static packet => packet.EmbeddedCandidateOffset is not null)
            .ThenByDescending(static packet => packet.HasMovementIntent)
            .ThenBy(static packet => packet.File, StringComparer.Ordinal)
            .ThenBy(static packet => packet.SourceStep)
            .Take(96)
            .Select(static packet => packet.ToSample())
            .ToArray();
    }

    private static string BuildConclusion(int commandCandidateCount, int exactBoundaryHits, int embeddedCandidates)
    {
        if (commandCandidateCount == 0)
        {
            return "no-client-command-candidates";
        }

        if (exactBoundaryHits > 0)
        {
            return "exact-clc-move-wrapper-observed";
        }

        return embeddedCandidates > 0
            ? "client-command-candidates-are-wrapper-opaque; embedded-offset-scan-candidates-need-tfelf-proof"
            : "client-command-candidates-are-wrapper-opaque; no-clc-move-boundary-observed";
    }

    private static string MissReason(
        Ps3SourceClientPayloadInfo info,
        Ps3SourceNativeToClcMoveBoundary? exactBoundary,
        bool hasEmbedded)
    {
        if (exactBoundary is not null)
        {
            return "exact-clc-move-boundary";
        }

        if (hasEmbedded)
        {
            return "embedded-clc-move-candidate-only";
        }

        if (info.BitSidecarBitCount is not null)
        {
            return "bit-sidecar-present-but-not-clc-move";
        }

        if (info.DecodedNetMessageName is not null)
        {
            return "decoded-non-move-client-net-message";
        }

        return info.Role == Ps3SourceClientPayloadRole.UserCommandCandidate
            ? MarkerlessOpaqueMissReason
            : info.Role.ToString();
    }

    private static bool IsPayloadObjectInnerDecodeCandidate(Ps3SourceClientPayloadInfo info)
    {
        if (info.PayloadObjectFrameKind is null
            || info.PayloadObjectInnerPayloadOffset is null
            || info.PayloadObjectInnerPayloadLength is null)
        {
            return false;
        }

        return info.PayloadObjectFrameKind != nameof(Ps3SourcePayloadObjectFrameKind.AssociatedObjectToken)
            || info.PayloadObjectAssociatedToken is > 0 and <= 0xffff;
    }

    private static bool TryFindEmbeddedClcMove(ReadOnlySpan<byte> body, out PcapSourceEmbeddedClcMoveCandidate candidate)
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

                candidate = new PcapSourceEmbeddedClcMoveCandidate(
                    offset,
                    includesMessageType,
                    move.NewCommands,
                    move.BackupCommands,
                    move.CommandDataBitCount,
                    move.TotalBitsConsumed);
                return true;
            }
        }

        return false;
    }

    private static PcapClientCommandWorklistCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapClientCommandWorklistCount(group.Key, group.Count()))
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

    private static string Suffix(ReadOnlySpan<byte> body, int length)
    {
        return body.IsEmpty
            ? ""
            : Convert.ToHexString(body[^Math.Min(length, body.Length)..]).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private const string MarkerlessOpaqueMissReason = "markerless-usercmd-loading-ack-wrapper-opaque";
    private const string DecodedNonMoveMissReason = "decoded-non-move-client-net-message";

    private sealed record ClientCommandPacketProbe(
        string File,
        int SourceStep,
        long PacketIndex,
        int Sequence,
        int? SequenceDelta,
        long TimestampMicroseconds,
        int PayloadLength,
        int BodyLength,
        string Role,
        string NativeFrameKind,
        string Shape,
        string BodyPrefixHex,
        string BodySuffixHex,
        byte? AttachedFrameKind,
        ushort? AttachedFrameDeclaredLength,
        int? BitSidecarOffset,
        int? BitSidecarBitCount,
        int? BitSidecarPayloadLength,
        string? PayloadObjectFrameKind,
        uint? PayloadObjectHeaderValue,
        int? PayloadObjectSignedHeaderValue,
        int? PayloadObjectInnerPayloadOffset,
        int? PayloadObjectInnerPayloadLength,
        uint? PayloadObjectAssociatedToken,
        byte? PayloadObjectFragmentIndex,
        byte? PayloadObjectFragmentTotalCount,
        bool PayloadObjectInnerDecodeCandidate,
        int? DecodedNetMessageType,
        string? DecodedNetMessageName,
        string? DecodedNetMessagePayloadKind,
        int? DecodedNetMessagePayloadOffset,
        int? DecodedNetMessagePayloadLength,
        int? DecodedNetMessagePayloadBitCount,
        string ExactBoundaryKind,
        int? ExactPayloadOffset,
        int? ExactPayloadLength,
        int? ExactNewCommands,
        int? ExactBackupCommands,
        int? ExactCommandDataBitCount,
        int? EmbeddedCandidateOffset,
        bool? EmbeddedIncludesMessageType,
        int? EmbeddedNewCommands,
        int? EmbeddedBackupCommands,
        int? EmbeddedCommandDataBitCount,
        bool DecodedCommandIntent,
        bool HasMovementIntent,
        short? HeuristicForwardMove,
        short? HeuristicSideMove,
        short? HeuristicUpMove,
        byte? HeuristicButtons,
        byte? HeuristicWeaponSlotHint,
        byte? HeuristicTeamHint,
        byte? HeuristicClassHint,
        string MissReason)
    {
        public PcapClientCommandPacketSample ToSample()
        {
            return new PcapClientCommandPacketSample(
                File,
                SourceStep,
                PacketIndex,
                Sequence,
                SequenceDelta,
                PayloadLength,
                BodyLength,
                Role,
                NativeFrameKind,
                Shape,
                BodyPrefixHex,
                BodySuffixHex,
                AttachedFrameKind,
                AttachedFrameDeclaredLength,
                BitSidecarOffset,
                BitSidecarBitCount,
                BitSidecarPayloadLength,
                PayloadObjectFrameKind,
                PayloadObjectHeaderValue,
                PayloadObjectSignedHeaderValue,
                PayloadObjectInnerPayloadOffset,
                PayloadObjectInnerPayloadLength,
                PayloadObjectAssociatedToken,
                PayloadObjectFragmentIndex,
                PayloadObjectFragmentTotalCount,
                PayloadObjectInnerDecodeCandidate,
                DecodedNetMessageName,
                DecodedNetMessagePayloadKind,
                DecodedNetMessagePayloadOffset,
                DecodedNetMessagePayloadLength,
                DecodedNetMessagePayloadBitCount,
                ExactBoundaryKind,
                ExactPayloadOffset,
                ExactPayloadLength,
                ExactNewCommands,
                ExactBackupCommands,
                ExactCommandDataBitCount,
                EmbeddedCandidateOffset,
                EmbeddedIncludesMessageType,
                EmbeddedNewCommands,
                EmbeddedBackupCommands,
                EmbeddedCommandDataBitCount,
                DecodedCommandIntent,
                HasMovementIntent,
                HeuristicForwardMove,
                HeuristicSideMove,
                HeuristicUpMove,
                HeuristicButtons,
                HeuristicWeaponSlotHint,
                HeuristicTeamHint,
                HeuristicClassHint,
                MissReason);
        }
    }

    private sealed record CommandFamilyKey(
        string Role,
        string NativeFrameKind,
        int BodyLength,
        byte? AttachedFrameKind,
        int? BitSidecarBitCount);

    private sealed record DecodedMessageKey(
        string MessageName,
        string PayloadKind,
        int? PayloadOffset,
        int? PayloadLength,
        int? PayloadBitCount,
        int BodyLength,
        string MissReason);

    private sealed class CommandFamilyKeyComparer : IEqualityComparer<CommandFamilyKey>
    {
        public static readonly CommandFamilyKeyComparer Instance = new();

        public bool Equals(CommandFamilyKey? x, CommandFamilyKey? y)
        {
            return x is not null
                && y is not null
                && x.Role == y.Role
                && x.NativeFrameKind == y.NativeFrameKind
                && x.BodyLength == y.BodyLength
                && x.AttachedFrameKind == y.AttachedFrameKind
                && x.BitSidecarBitCount == y.BitSidecarBitCount;
        }

        public int GetHashCode(CommandFamilyKey obj)
        {
            return HashCode.Combine(obj.Role, obj.NativeFrameKind, obj.BodyLength, obj.AttachedFrameKind, obj.BitSidecarBitCount);
        }
    }

    private sealed class DecodedMessageKeyComparer : IEqualityComparer<DecodedMessageKey>
    {
        public static readonly DecodedMessageKeyComparer Instance = new();

        public bool Equals(DecodedMessageKey? x, DecodedMessageKey? y)
        {
            return x is not null
                && y is not null
                && x.MessageName == y.MessageName
                && x.PayloadKind == y.PayloadKind
                && x.PayloadOffset == y.PayloadOffset
                && x.PayloadLength == y.PayloadLength
                && x.PayloadBitCount == y.PayloadBitCount
                && x.BodyLength == y.BodyLength
                && x.MissReason == y.MissReason;
        }

        public int GetHashCode(DecodedMessageKey obj)
        {
            return HashCode.Combine(
                obj.MessageName,
                obj.PayloadKind,
                obj.PayloadOffset,
                obj.PayloadLength,
                obj.PayloadBitCount,
                obj.BodyLength,
                obj.MissReason);
        }
    }
}

public sealed record PcapClientCommandWorklistReport(
    string Status,
    PcapClientCommandWorklistSummary Summary,
    PcapClientCommandFamilyGroup[] CommandFamilies,
    PcapClientCommandDecodedMessageGroup[] DecodedMessageFamilies,
    PcapClientCommandWorklistCount[] MissReasonCounts,
    PcapClientCommandPacketSample[] Samples,
    string[] Conclusions);

public sealed record PcapClientCommandWorklistSummary(
    int FileCount,
    int ActiveSourceFlowCount,
    int ClientPacketCount,
    int UserCommandCandidateCount,
    int ExactClcMoveBoundaryHitCount,
    int EmbeddedClcMoveCandidateCount,
    int OpaqueMarkerlessUserCommandCandidateCount,
    int DecodedNonMoveClientNetMessageCandidateCount,
    int AttachedFrameClientPacketCount,
    int AttachedFrameUserCommandCandidateCount,
    int BitSidecarClientPacketCount,
    int BitSidecarUserCommandCandidateCount,
    int PayloadObjectFirstWordClientPacketCount,
    int PayloadObjectFirstWordUserCommandCandidateCount,
    int PayloadObjectInnerDecodeCandidateCount,
    int PayloadObjectInnerClcMoveBoundaryHitCount,
    int PayloadObjectInnerDecodedNetMessageCount,
    int HeuristicCommandIntentCount,
    int HeuristicMovementIntentCount,
    int DecodedClientNetMessageCount,
    string BoundaryConclusion);

public sealed record PcapClientCommandFamilyGroup(
    string Role,
    string NativeFrameKind,
    int BodyLength,
    byte? AttachedFrameKind,
    int? BitSidecarBitCount,
    int Count,
    int DistinctFileCount,
    int ExactBoundaryHitCount,
    int EmbeddedCandidateCount,
    int HeuristicCommandIntentCount,
    int HeuristicMovementIntentCount,
    PcapClientCommandWorklistCount[] TopBodyPrefixes,
    PcapClientCommandWorklistCount[] TopBodySuffixes,
    PcapClientCommandWorklistCount[] TopHeuristicButtons,
    PcapClientCommandWorklistCount[] MissReasonCounts,
    PcapClientCommandPacketSample[] Samples);

public sealed record PcapClientCommandDecodedMessageGroup(
    string MessageName,
    string PayloadKind,
    int? PayloadOffset,
    int? PayloadLength,
    int? PayloadBitCount,
    int BodyLength,
    string MissReason,
    int Count,
    int DistinctFileCount,
    PcapClientCommandWorklistCount[] TopBodyPrefixes,
    PcapClientCommandPacketSample[] Samples);

public sealed record PcapClientCommandWorklistCount(
    string Value,
    int Count);

public sealed record PcapClientCommandPacketSample(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    string NativeFrameKind,
    string Shape,
    string BodyPrefixHex,
    string BodySuffixHex,
    byte? AttachedFrameKind,
    ushort? AttachedFrameDeclaredLength,
    int? BitSidecarOffset,
    int? BitSidecarBitCount,
    int? BitSidecarPayloadLength,
    string? PayloadObjectFrameKind,
    uint? PayloadObjectHeaderValue,
    int? PayloadObjectSignedHeaderValue,
    int? PayloadObjectInnerPayloadOffset,
    int? PayloadObjectInnerPayloadLength,
    uint? PayloadObjectAssociatedToken,
    byte? PayloadObjectFragmentIndex,
    byte? PayloadObjectFragmentTotalCount,
    bool PayloadObjectInnerDecodeCandidate,
    string? DecodedNetMessageName,
    string? DecodedNetMessagePayloadKind,
    int? DecodedNetMessagePayloadOffset,
    int? DecodedNetMessagePayloadLength,
    int? DecodedNetMessagePayloadBitCount,
    string ExactBoundaryKind,
    int? ExactPayloadOffset,
    int? ExactPayloadLength,
    int? ExactNewCommands,
    int? ExactBackupCommands,
    int? ExactCommandDataBitCount,
    int? EmbeddedCandidateOffset,
    bool? EmbeddedIncludesMessageType,
    int? EmbeddedNewCommands,
    int? EmbeddedBackupCommands,
    int? EmbeddedCommandDataBitCount,
    bool DecodedCommandIntent,
    bool HasMovementIntent,
    short? HeuristicForwardMove,
    short? HeuristicSideMove,
    short? HeuristicUpMove,
    byte? HeuristicButtons,
    byte? HeuristicWeaponSlotHint,
    byte? HeuristicTeamHint,
    byte? HeuristicClassHint,
    string MissReason);
