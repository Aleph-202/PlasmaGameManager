using System.Text;
using System.Text.Json;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public sealed class PcapPlainEnglishTraceExporter
{
    private readonly PcapActiveFlowReplayExtractor _extractor = new();

    public async Task<PcapPlainEnglishTraceReport> ExportAsync(string pcapPath, string outputPath)
    {
        var report = Export(pcapPath);
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        var markdownPath = CompanionTurnsMarkdownPath(outputPath);
        await File.WriteAllTextAsync(markdownPath, BuildTurnsMarkdown(report));
        return report;
    }

    public PcapPlainEnglishTraceReport Export(string pcapPath)
    {
        var replay = _extractor.Extract(pcapPath)
            ?? throw new InvalidOperationException($"No active GameManager/native Source flow found in {pcapPath}.");
        var packets = BuildPackets(replay);
        var turns = BuildTurns(packets);
        return new PcapPlainEnglishTraceReport(
            new PcapPlainEnglishTraceSummary(
                replay.Path,
                replay.ClientEndpoint,
                replay.ServerEndpoint,
                replay.ClientHelloPacketIndex,
                replay.ServerHelloPacketIndex,
                replay.FirstSourceClientPacketIndex,
                replay.SourcePackets.Length,
                replay.SourcePackets.Count(static packet => packet.Direction == PcapActiveFlowDirection.ClientToServer),
                replay.SourcePackets.Count(static packet => packet.Direction == PcapActiveFlowDirection.ServerToClient),
                turns.Length,
                packets.Count(static packet => packet.Confidence == "high"),
                packets.Count(static packet => packet.Confidence == "medium"),
                packets.Count(static packet => packet.Confidence == "low"),
                packets.Count(static packet => packet.UnknownFields.Length == 0),
                packets.Count(static packet => packet.UnknownFields.Length > 0),
                packets.Count(static packet => packet.SnapshotFrame is not null),
                packets.Sum(static packet => packet.SnapshotFrame?.EntityDeltaRecordCount ?? 0),
                packets.Count(static packet => packet.QueuedPeerChunk is not null),
                packets.Count(static packet => packet.QueuedPeerChunk?.HasEmbeddedRecords == true),
                packets.Count(static packet => packet.QueuedPeerChunk?.HasEmbeddedRecords == false),
                packets.Sum(static packet => packet.QueuedPeerChunk?.OpaquePrefixLength ?? 0),
                packets.Count(static packet => packet.ClientPayload?.AttachedFrameKind is not null),
                packets.Count(static packet => packet.ClientPayload?.Role == Ps3SourceClientPayloadRole.AttachedPlayerPayloadFrame.ToString()),
                packets.Count(static packet => packet.ClientPayload?.BitSidecarOffset is not null),
                packets.Count(static packet => packet.ClientPayload?.DecodedNetMessageType is not null),
                packets.Count(static packet => packet.ClientPayload?.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString()),
                CountBy(packets.Select(static packet => packet.PlainEnglishKind)),
                CountBy(packets.Select(static packet => packet.SemanticRole)),
                CountBy(packets.Select(static packet => packet.Phase)),
                CountBy(packets.Select(static packet => packet.CompactControl?.Family)),
                CountBy(packets.Select(static packet => packet.MarkerlessPayload?.Family)),
                CountBy(packets.Select(static packet => packet.EmbeddedPrefix?.Family)),
                CountBy(packets.Select(static packet => packet.ClientPayload?.Role)),
                CountBy(packets.Select(static packet => packet.ClientPayload?.AttachedFrameKind?.ToString())),
                CountBy(packets.Select(static packet => packet.ClientPayload?.DecodedNetMessageName))),
            new PcapPlainEnglishHelloTrace(
                replay.ClientHelloPacketIndex,
                Convert.ToHexString(replay.ClientHelloPayload).ToLowerInvariant(),
                "Client opens the retail GameManager UDP handshake before the native Source stream.",
                replay.ServerHelloPacketIndex,
                Convert.ToHexString(replay.ServerHelloPayload).ToLowerInvariant(),
                "Server accepts the GameManager handshake. The next client packet starts the PS3-native Source/gameplay stream on the same UDP endpoint."),
            turns,
            packets);
    }

    private static PcapPlainEnglishTracePacket[] BuildPackets(PcapActiveFlowReplay replay)
    {
        var result = new List<PcapPlainEnglishTracePacket>();
        ushort? lastClientSequence = null;
        ushort? lastServerSequence = null;
        var clientDirectionCount = 0;
        var serverDirectionCount = 0;
        var firstTimestamp = replay.SourcePackets.Length == 0 ? 0 : replay.SourcePackets[0].TimestampMicroseconds;

        for (var i = 0; i < replay.SourcePackets.Length; i++)
        {
            var raw = replay.SourcePackets[i];
            var elapsedMilliseconds = Math.Round((raw.TimestampMicroseconds - firstTimestamp) / 1000.0, 3);
            if (!Ps3SourceTransportPacket.TryDecode(raw.Payload, out var transport))
            {
                result.Add(new PcapPlainEnglishTracePacket(
                    i,
                    raw.PacketIndex,
                    elapsedMilliseconds,
                    raw.Direction.ToString(),
                    raw.Payload.Length,
                    null,
                    null,
                    null,
                    null,
                    "invalid-or-non-native",
                    "Invalid",
                    "Invalid",
                    "Unknown",
                    "low",
                    "This packet does not decode as the current two-byte PS3-native Source transport envelope.",
                    ["transport envelope"],
                    null,
                    "",
                    null,
                    [],
                    null,
                    null,
                    null,
                    null,
                    [],
                    null,
                    Convert.ToHexString(raw.Payload).ToLowerInvariant(),
                    ""));
                continue;
            }

            int? sequenceDelta;
            var directionCount = raw.Direction == PcapActiveFlowDirection.ClientToServer
                ? ++clientDirectionCount
                : ++serverDirectionCount;
            if (raw.Direction == PcapActiveFlowDirection.ClientToServer)
            {
                sequenceDelta = lastClientSequence is null
                    ? null
                    : Ps3SourceTransportPacket.SequenceDelta(lastClientSequence.Value, transport.CandidateSequence);
                lastClientSequence = transport.CandidateSequence;
            }
            else
            {
                sequenceDelta = lastServerSequence is null
                    ? null
                    : Ps3SourceTransportPacket.SequenceDelta(lastServerSequence.Value, transport.CandidateSequence);
                lastServerSequence = transport.CandidateSequence;
            }

            var shape = Ps3SourceGameplaySession.ClassifyShape(transport);
            var frame = transport.ClassifyNativeFrame();
            var semantic = directionCount == 1 && raw.Direction == PcapActiveFlowDirection.ClientToServer
                ? Ps3SourcePayloadSemantics.AnalyzeInitialClientHandoffProbe(transport.Body)
                : Ps3SourcePayloadSemantics.Analyze(transport.Body);
            var extractedRecords = Ps3SourceEmbeddedObjectRecords.Extract(transport.Body)
                .Where(static record => record.Role != Ps3SourceEmbeddedObjectRecordRole.MarkerCollisionNoise)
                .ToArray();
            var embeddedPrefixLength = extractedRecords.Length == 0 ? (int?)null : extractedRecords[0].Offset;
            var embeddedPrefixHex = embeddedPrefixLength is > 0
                ? Convert.ToHexString(transport.Body[..embeddedPrefixLength.Value]).ToLowerInvariant()
                : "";
            var compactControl = Ps3SourceCompactControlFrame.TryAnalyze(transport.Body, extractedRecords);
            var embeddedPrefix = Ps3SourceEmbeddedPrefixFrame.TryAnalyze(
                transport.Body,
                extractedRecords,
                raw.Direction.ToString());
            var records = extractedRecords
                .Select(static record => new PcapPlainEnglishEmbeddedRecord(
                    record.Marker,
                    record.Offset,
                    record.Length,
                    record.Role.ToString(),
                    Hex(record.ObjectId),
                    Hex(record.FieldB),
                    Hex(record.LinkedObjectId),
                    Hex(record.ClassId),
                    record.DisplayName,
                    record.HeaderHex,
                    record.PrintableCandidates))
                .ToArray();
            PcapPlainEnglishClientPayload? clientPayload = null;
            if (raw.Direction == PcapActiveFlowDirection.ClientToServer)
            {
                var clientInfo = Ps3SourceClientPayloadClassifier.Classify(transport, directionCount, sequenceDelta);
                Ps3SourceClientCommandIntent? intent = null;
                Ps3SourceClientCommandIntent.TryDecode(clientInfo, transport.Body, out intent);
                clientPayload = new PcapPlainEnglishClientPayload(
                    clientInfo.Role.ToString(),
                    clientInfo.NativeFrameKind.ToString(),
                    clientInfo.BodyPrefixHex,
                    clientInfo.ReliableAssociationMessageType,
                    Hex(clientInfo.ReliableAssociationNativeToken),
                    clientInfo.AttachedFrameKind,
                    clientInfo.AttachedFrameDeclaredLength,
                    Hex(clientInfo.AttachedFrameNativeToken),
                    clientInfo.BitSidecarOffset,
                    clientInfo.BitSidecarBitCount,
                    clientInfo.BitSidecarPayloadLength,
                    clientInfo.DecodedNetMessageType,
                    clientInfo.DecodedNetMessageName,
                    clientInfo.DecodedNetMessagePayloadKind,
                    clientInfo.DecodedNetMessagePayloadOffset,
                    clientInfo.DecodedNetMessagePayloadLength,
                    clientInfo.DecodedNetMessagePayloadBitCount,
                    intent is null
                        ? null
                        : new PcapPlainEnglishClientCommandIntent(
                            intent.ForwardMove,
                            intent.SideMove,
                            intent.UpMove,
                            Math.Round(intent.YawDelta, 4),
                            Math.Round(intent.PitchDelta, 4),
                            intent.Buttons,
                            intent.WeaponSlotHint,
                            intent.TeamHint,
                            intent.ClassHint,
                            intent.HasMovement));
            }

            var compactControlTrace = compactControl is null
                ? null
                : new PcapPlainEnglishCompactControl(
                    compactControl.Family,
                    compactControl.BodyLength,
                    compactControl.PrefixLength,
                    compactControl.PrefixHex,
                    compactControl.FirstUInt16BigEndian,
                    compactControl.FirstUInt16LittleEndian,
                    compactControl.FirstUInt32BigEndian,
                    compactControl.FirstUInt32LittleEndian,
                    compactControl.LastUInt16BigEndian,
                    compactControl.LastUInt16LittleEndian,
                    compactControl.LastUInt32BigEndian,
                    compactControl.LastUInt32LittleEndian);
            var embeddedPrefixTrace = embeddedPrefix is null
                ? null
                : new PcapPlainEnglishEmbeddedPrefix(
                    embeddedPrefix.Family,
                    embeddedPrefix.PrefixLength,
                    embeddedPrefix.PrefixHex,
                    embeddedPrefix.PrefixEntropy,
                    embeddedPrefix.PrefixPrintableRatio,
                    embeddedPrefix.FirstUInt16BigEndian,
                    embeddedPrefix.FirstUInt16LittleEndian,
                    embeddedPrefix.FirstUInt32BigEndian,
                    embeddedPrefix.FirstUInt32LittleEndian,
                    embeddedPrefix.LastUInt16BigEndian,
                    embeddedPrefix.LastUInt16LittleEndian,
                    embeddedPrefix.LastUInt32BigEndian,
                    embeddedPrefix.LastUInt32LittleEndian,
                    embeddedPrefix.RecordRoleSummary,
                    embeddedPrefix.Meaning,
                    embeddedPrefix.UnknownField);
            var markerlessPayload = PcapPlainEnglishMarkerlessPayload.Analyze(
                raw.Direction,
                i,
                elapsedMilliseconds,
                shape,
                frame,
                semantic,
                compactControlTrace,
                clientPayload,
                records,
                transport.Body);
            var snapshotTrace = PcapPlainEnglishSnapshotFrame.TryAnalyze(transport.Body);
            var queuedPeerChunk = PcapPlainEnglishQueuedPeerChunk.TryAnalyze(
                raw.Direction,
                frame,
                records,
                snapshotTrace,
                transport.Body);
            var plain = Explain(raw.Direction, i, shape, frame, semantic, records, compactControlTrace, markerlessPayload, embeddedPrefixTrace, clientPayload, snapshotTrace, queuedPeerChunk, transport.Body.Length);
            result.Add(new PcapPlainEnglishTracePacket(
                i,
                raw.PacketIndex,
                elapsedMilliseconds,
                raw.Direction.ToString(),
                raw.Payload.Length,
                transport.CandidateSequence,
                sequenceDelta,
                transport.Body.Length,
                PhaseFor(i, elapsedMilliseconds),
                shape.ToString(),
                frame.Kind.ToString(),
                semantic.Kind.ToString(),
                semantic.Role.ToString(),
                plain.Confidence,
                plain.Text,
                plain.UnknownFields,
                embeddedPrefixLength,
                embeddedPrefixHex,
                embeddedPrefixTrace,
                records.Select(RecordSummary).ToArray(),
                compactControlTrace,
                markerlessPayload,
                snapshotTrace,
                queuedPeerChunk,
                records,
                clientPayload,
                Convert.ToHexString(raw.Payload).ToLowerInvariant(),
                Convert.ToHexString(transport.Body).ToLowerInvariant()));
        }

        return result.ToArray();
    }

    private static PcapPlainEnglishTraceTurn[] BuildTurns(PcapPlainEnglishTracePacket[] packets)
    {
        var turns = new List<PcapPlainEnglishTraceTurn>();
        var client = new List<int>();
        var server = new List<int>();

        void Flush()
        {
            if (client.Count == 0 && server.Count == 0)
            {
                return;
            }

            var turnPackets = packets.Where(packet => client.Contains(packet.SourceStep) || server.Contains(packet.SourceStep)).ToArray();
            turns.Add(new PcapPlainEnglishTraceTurn(
                turns.Count,
                client.ToArray(),
                server.ToArray(),
                string.Join("; ", turnPackets
                    .Select(static packet => packet.PlainEnglishKind)
                    .Distinct(StringComparer.Ordinal)
                    .Take(6)),
                turnPackets.Any(static packet => packet.UnknownFields.Length > 0)));
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

                client.Add(packet.SourceStep);
            }
            else
            {
                server.Add(packet.SourceStep);
            }
        }

        Flush();
        return turns.ToArray();
    }

    private static PcapPlainEnglishExplanation Explain(
        PcapActiveFlowDirection direction,
        int sourceStep,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo frame,
        Ps3SourcePayloadSemanticInfo semantic,
        PcapPlainEnglishEmbeddedRecord[] records,
        PcapPlainEnglishCompactControl? compactControl,
        PcapPlainEnglishMarkerlessPayload? markerlessPayload,
        PcapPlainEnglishEmbeddedPrefix? embeddedPrefix,
        PcapPlainEnglishClientPayload? clientPayload,
        PcapPlainEnglishSnapshotFrame? snapshotFrame,
        PcapPlainEnglishQueuedPeerChunk? queuedPeerChunk,
        int bodyLength)
    {
        var unknowns = new List<string>();
        string text;
        string confidence;

        if (direction == PcapActiveFlowDirection.ClientToServer)
        {
            (text, confidence) = ExplainClient(sourceStep, shape, semantic, compactControl, markerlessPayload, embeddedPrefix, clientPayload, records, bodyLength);
        }
        else
        {
            (text, confidence) = ExplainServer(shape, frame, semantic, records, compactControl, markerlessPayload, embeddedPrefix, snapshotFrame, bodyLength);
        }

        if (compactControl is not null)
        {
            unknowns.Add(compactControl.EmbeddedRecordBacked
                ? "semantic meaning of compact embedded-record prefix/control bytes"
                : "semantic meaning of compact native control bytes");
        }

        if (snapshotFrame is not null)
        {
            if (snapshotFrame.TrailingPayloadLength > 0)
            {
                unknowns.Add("snapshot trailing payload after decoded entity-delta prefix");
            }
        }
        else if (queuedPeerChunk?.SnapshotInOpaquePrefix is not null)
        {
            if (queuedPeerChunk.SnapshotInOpaquePrefix.TrailingPayloadLength > 0)
            {
                unknowns.Add("queued peer-channel snapshot trailing payload after decoded entity-delta prefix");
            }
        }
        else if (markerlessPayload is not null)
        {
            unknowns.Add(markerlessPayload.UnknownField);
        }
        else if (semantic.Role is Ps3SourcePayloadSemanticRole.HighEntropyBinary
            or Ps3SourcePayloadSemanticRole.BinaryGameplay
            or Ps3SourcePayloadSemanticRole.MixedTextBinary)
        {
            unknowns.Add("packed native payload field layout");
        }

        if (frame.Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
        {
            if (queuedPeerChunk is null)
            {
                unknowns.Add("queued peer-channel chunk submessage boundaries");
            }
            else if (queuedPeerChunk.HasEmbeddedRecords)
            {
                unknowns.Add("queued peer-channel opaque prefix layout before embedded object/state records");
            }
            else if (queuedPeerChunk.SnapshotInOpaquePrefix is null)
            {
                unknowns.Add("queued peer-channel opaque payload submessage boundaries");
            }
        }

        if (semantic.Role is Ps3SourcePayloadSemanticRole.FrozenStateBatch or Ps3SourcePayloadSemanticRole.FrozenStateObjectBatch)
        {
            if (embeddedPrefix is not null)
            {
                unknowns.Add(embeddedPrefix.UnknownField);
            }

            unknowns.Add("allocator/source for object ids and roster ordering");
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.PlayerStateLinkBatch)
        {
            unknowns.Add(embeddedPrefix?.UnknownField ?? "exact semantic names for PNG linked-object fields");
        }

        if (clientPayload?.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString())
        {
            unknowns.Add("exact usercmd bit layout; current movement/team/class values are heuristic");
        }

        if (bodyLength >= 512 && records.Length == 0)
        {
            unknowns.Add("large loading/MOTD/entity-state payload structure");
        }

        return new PcapPlainEnglishExplanation(text, confidence, unknowns.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static (string Text, string Confidence) ExplainClient(
        int sourceStep,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourcePayloadSemanticInfo semantic,
        PcapPlainEnglishCompactControl? compactControl,
        PcapPlainEnglishMarkerlessPayload? markerlessPayload,
        PcapPlainEnglishEmbeddedPrefix? embeddedPrefix,
        PcapPlainEnglishClientPayload? clientPayload,
        PcapPlainEnglishEmbeddedRecord[] records,
        int bodyLength)
    {
        if (sourceStep == 0)
        {
            return ("Client starts the PS3-native Source/gameplay stream after GameManager hello. This is the native handoff probe, not PC Source connectionless traffic.", "medium");
        }

        if (clientPayload?.Role == Ps3SourceClientPayloadRole.ShortControlAck.ToString())
        {
            return compactControl is null
                ? ("Client sends a short native control/ack packet to advance the reliable stream.", "high")
                : ($"Client sends {compactControl.Family}: compact native control/ack bytes to advance the reliable stream.", "high");
        }

        if (records.Length > 0)
        {
            var summary = string.Join(", ", records.Select(RecordSummary).Take(6));
            return embeddedPrefix is null
                ? ($"Client sends embedded object/state records: {summary}.", "high")
                : ($"Client sends {embeddedPrefix.Family}: {embeddedPrefix.Meaning}; records: {summary}.", "high");
        }

        if (clientPayload?.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString())
        {
            return ("Client sends a compact native user-command/control payload. The direction/timing matches controller input or loading acknowledgement traffic, but the field layout is still partly heuristic.", "low");
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.InitialHandoffClientProbe)
        {
            return ("Client sends an initial markerless setup payload for the native Source stream.", "medium");
        }

        if (markerlessPayload is not null)
        {
            return ($"Client sends {markerlessPayload.Family}: {markerlessPayload.Meaning}.", markerlessPayload.Confidence);
        }

        if (shape == Ps3SourceGameplayPacketShape.LargeBinary)
        {
            return ("Client sends a large native state/upload payload during setup or loading.", "low");
        }

        return ($"Client sends markerless native binary payload ({bodyLength} body bytes) with semantic role {semantic.Role}.", "low");
    }

    private static (string Text, string Confidence) ExplainServer(
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo frame,
        Ps3SourcePayloadSemanticInfo semantic,
        PcapPlainEnglishEmbeddedRecord[] records,
        PcapPlainEnglishCompactControl? compactControl,
        PcapPlainEnglishMarkerlessPayload? markerlessPayload,
        PcapPlainEnglishEmbeddedPrefix? embeddedPrefix,
        PcapPlainEnglishSnapshotFrame? snapshotFrame,
        int bodyLength)
    {
        if (snapshotFrame is not null)
        {
            return ($"Server sends native TF.elf snapshot frame {snapshotFrame.FrameIndex} based on {snapshotFrame.BaseOrAckFrame}, carrying {snapshotFrame.EntityDeltaRecordCount} decoded entity-delta record(s): {string.Join("; ", snapshotFrame.EntityDeltaRecordSummaries.Take(6))}.", "high");
        }

        if (records.Length > 0)
        {
            var summary = string.Join(", ", records.Select(RecordSummary).Take(8));
            if (semantic.Role is Ps3SourcePayloadSemanticRole.FrozenStateBatch or Ps3SourcePayloadSemanticRole.FrozenStateObjectBatch)
            {
                return embeddedPrefix is null
                    ? ($"Server advertises FrozenState/player-like COc objects: {summary}. These records carry object id, owner/root object, class id, and display name.", "high")
                    : ($"Server sends {embeddedPrefix.Family}: {embeddedPrefix.Meaning}; COc records: {summary}. Records carry object id, owner/root object, class id, and display name.", "high");
            }

            if (semantic.Role == Ps3SourcePayloadSemanticRole.PlayerStateLinkBatch)
            {
                return embeddedPrefix is null
                    ? ($"Server sends PNG linked-object/state records: {summary}.", "high")
                    : ($"Server sends {embeddedPrefix.Family}: {embeddedPrefix.Meaning}; PNG links: {summary}.", "high");
            }

            return embeddedPrefix is null
                ? ($"Server sends embedded object records: {summary}.", "high")
                : ($"Server sends {embeddedPrefix.Family}: {embeddedPrefix.Meaning}; records: {summary}.", "high");
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.ShortControl)
        {
            return compactControl is null
                ? ("Server sends a short native control/ack/setup packet.", "high")
                : ($"Server sends {compactControl.Family}: compact native control/setup bytes for the reliable stream.", "high");
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.NativeLzssCompressed)
        {
            return ("Server sends a native LZSS-wrapped payload produced by the PS3 Source send path.", "medium");
        }

        if (semantic.Role is Ps3SourcePayloadSemanticRole.NativeServerInfo49 or Ps3SourcePayloadSemanticRole.NativeResourceStringTable45 or Ps3SourcePayloadSemanticRole.NativePlayerSummary44)
        {
            return ($"Server sends native Source message {semantic.Role}.", "medium");
        }

        if (shape == Ps3SourceGameplayPacketShape.NearMtuFragment || frame.Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
        {
            return ("Server sends a large queued native Source payload. In this phase it likely carries loading, MOTD, string-table, or entity baseline data.", "low");
        }

        if (markerlessPayload is not null)
        {
            return ($"Server sends {markerlessPayload.Family}: {markerlessPayload.Meaning}.", markerlessPayload.Confidence);
        }

        return ($"Server sends markerless native binary payload ({bodyLength} body bytes) with semantic role {semantic.Role}.", "low");
    }

    private static string RecordSummary(PcapPlainEnglishEmbeddedRecord record)
    {
        return record.Role switch
        {
            nameof(Ps3SourceEmbeddedObjectRecordRole.PlayerStateLink) => $"PNG {record.ObjectId}->{record.LinkedObjectId}",
            nameof(Ps3SourceEmbeddedObjectRecordRole.FrozenStateObject) => $"COc {record.ObjectId} owner={record.OwnerOrRootObjectId} class={record.ClassId} name={record.DisplayName ?? ""}",
            nameof(Ps3SourceEmbeddedObjectRecordRole.PlayerObject) => $"COc {record.ObjectId} owner={record.OwnerOrRootObjectId} class={record.ClassId} name={record.DisplayName ?? ""}",
            nameof(Ps3SourceEmbeddedObjectRecordRole.PlayerDescriptor) => $"DSC {record.ObjectId} owner={record.OwnerOrRootObjectId} class={record.ClassId} name={record.DisplayName ?? ""}",
            _ => $"{record.Marker} {record.ObjectId}"
        };
    }

    private static string CompanionTurnsMarkdownPath(string outputPath)
    {
        const string semanticSuffix = ".semantic-trace.json";
        return outputPath.EndsWith(semanticSuffix, StringComparison.OrdinalIgnoreCase)
            ? outputPath[..^semanticSuffix.Length] + ".turns.md"
            : Path.ChangeExtension(outputPath, ".turns.md");
    }

    private static string BuildTurnsMarkdown(PcapPlainEnglishTraceReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Quick Match To MOTD 2Fort: Full Turn Conversation");
        builder.AppendLine();
        builder.AppendLine($"Input capture: `{Path.GetFileName(report.Summary.File)}`.");
        builder.AppendLine();
        builder.AppendLine("This is generated from the active UDP flow after the GameManager hello boundary. It preserves every client burst and server response burst in order. High confidence means the packet role/shape is understood; unknown fields still list byte ranges or semantics that are not fully named yet.");
        builder.AppendLine();
        builder.AppendLine("## Coverage");
        builder.AppendLine();
        builder.AppendLine($"- Native packets: `{report.Summary.NativePacketCount}`");
        builder.AppendLine($"- Turns: `{report.Summary.TurnCount}`");
        builder.AppendLine($"- High-confidence roles: `{report.Summary.HighConfidencePacketCount}`");
        builder.AppendLine($"- Medium-confidence roles: `{report.Summary.MediumConfidencePacketCount}`");
        builder.AppendLine($"- Low-confidence roles: `{report.Summary.LowConfidencePacketCount}`");
        builder.AppendLine($"- Fully field-named packets: `{report.Summary.FullyFieldNamedPacketCount}`");
        builder.AppendLine($"- Packets with unknown fields: `{report.Summary.PacketCountWithUnknownFields}`");
        builder.AppendLine($"- Compact-control packets: `{report.Summary.CompactControlFamilyCounts.Sum(static count => count.Count)}`");
        builder.AppendLine($"- Markerless structural-family packets: `{report.Summary.MarkerlessPayloadFamilyCounts.Sum(static count => count.Count)}`");
        builder.AppendLine($"- Embedded-prefix structural-family packets: `{report.Summary.EmbeddedPrefixFamilyCounts.Sum(static count => count.Count)}`");
        builder.AppendLine($"- Strict native snapshot frames: `{report.Summary.NativeSnapshotFrameCount}`");
        builder.AppendLine($"- Strict native snapshot entity-delta records: `{report.Summary.NativeSnapshotEntityDeltaRecordCount}`");
        builder.AppendLine($"- Queued peer-channel chunks: `{report.Summary.QueuedPeerChunkPacketCount}`");
        builder.AppendLine($"- Queued chunks with embedded records: `{report.Summary.QueuedPeerChunkWithEmbeddedRecordsCount}`");
        builder.AppendLine($"- Queued chunks without embedded records: `{report.Summary.QueuedPeerChunkWithoutEmbeddedRecordsCount}`");
        builder.AppendLine($"- Queued chunk opaque-prefix bytes: `{report.Summary.QueuedPeerChunkOpaquePrefixBytes}`");
        builder.AppendLine($"- Client attached-frame packets: `{report.Summary.ClientAttachedFramePacketCount}`");
        builder.AppendLine($"- Client attached payload frames: `{report.Summary.ClientAttachedPayloadFramePacketCount}`");
        builder.AppendLine($"- Client bit-sidecar packets: `{report.Summary.ClientBitSidecarPacketCount}`");
        builder.AppendLine($"- Client decoded native net message candidates: `{report.Summary.ClientDecodedNetMessagePacketCount}`");
        builder.AppendLine($"- Client user-command/loading-ack candidates: `{report.Summary.ClientUserCommandCandidatePacketCount}`");
        builder.AppendLine();
        if (report.Summary.ClientPayloadRoleCounts.Length > 0)
        {
            builder.AppendLine("Client payload roles:");
            foreach (var count in report.Summary.ClientPayloadRoleCounts.Take(16))
            {
                builder.AppendLine($"- `{count.Value}`: `{count.Count}`");
            }

            builder.AppendLine();
        }

        if (report.Summary.ClientDecodedNetMessageNameCounts.Length > 0)
        {
            builder.AppendLine("Decoded client native message candidate names:");
            foreach (var count in report.Summary.ClientDecodedNetMessageNameCounts.Take(16))
            {
                builder.AppendLine($"- `{count.Value}`: `{count.Count}`");
            }

            builder.AppendLine();
        }

        if (report.Summary.MarkerlessPayloadFamilyCounts.Length > 0)
        {
            builder.AppendLine("Markerless structural families:");
            foreach (var count in report.Summary.MarkerlessPayloadFamilyCounts.Take(16))
            {
                builder.AppendLine($"- `{count.Value}`: `{count.Count}`");
            }

            builder.AppendLine();
        }

        if (report.Summary.EmbeddedPrefixFamilyCounts.Length > 0)
        {
            builder.AppendLine("Embedded-prefix structural families:");
            foreach (var count in report.Summary.EmbeddedPrefixFamilyCounts.Take(16))
            {
                builder.AppendLine($"- `{count.Value}`: `{count.Count}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Turn List");

        foreach (var turn in report.Turns)
        {
            builder.AppendLine();
            builder.AppendLine($"### Turn {turn.TurnIndex}");
            builder.AppendLine();
            builder.AppendLine($"Summary: `{turn.PlainEnglish}`. Contains unknown fields: `{turn.HasUnknownFields.ToString().ToLowerInvariant()}`.");
            builder.AppendLine();
            AppendPacketGroup(builder, "Client burst", turn.ClientSourceSteps, report.Packets);
            builder.AppendLine();
            AppendPacketGroup(builder, "Server response", turn.ServerSourceSteps, report.Packets);
        }

        return builder.ToString();
    }

    private static void AppendPacketGroup(StringBuilder builder, string title, int[] sourceSteps, PcapPlainEnglishTracePacket[] packets)
    {
        builder.AppendLine($"{title}:");
        if (sourceSteps.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var step in sourceSteps)
        {
            var packet = packets.FirstOrDefault(candidate => candidate.SourceStep == step);
            if (packet is null)
            {
                builder.AppendLine($"- step {step}: missing from trace");
                continue;
            }

            builder.Append("- ");
            builder.Append($"step {packet.SourceStep} packet {packet.PacketIndex} {packet.Direction}");
            builder.Append($" seq={packet.Sequence?.ToString() ?? "?"} len={packet.PayloadLength}");
            builder.Append($" role={packet.SemanticRole} confidence={packet.Confidence}: {packet.PlainEnglish}");
            if (packet.EmbeddedPrefixLength is not null)
            {
                builder.Append($" Prefix: len={packet.EmbeddedPrefixLength} hex={PreviewHex(packet.EmbeddedPrefixHex)}.");
            }

            if (packet.EmbeddedPrefix is not null)
            {
                builder.Append($" EmbeddedPrefix: family={packet.EmbeddedPrefix.Family} entropy={packet.EmbeddedPrefix.PrefixEntropy:0.####} printable={packet.EmbeddedPrefix.PrefixPrintableRatio:0.####}.");
            }

            if (packet.EmbeddedRecordSummaries.Length > 0)
            {
                builder.Append($" Records: {string.Join("; ", packet.EmbeddedRecordSummaries)}.");
            }

            if (packet.CompactControl is not null)
            {
                builder.Append($" CompactControl: family={packet.CompactControl.Family} prefixLen={packet.CompactControl.PrefixLength} prefixHex={PreviewHex(packet.CompactControl.PrefixHex)}.");
            }

            if (packet.MarkerlessPayload is not null)
            {
                builder.Append($" Markerless: family={packet.MarkerlessPayload.Family} prefix={PreviewHex(packet.MarkerlessPayload.PrefixHex)} suffix={PreviewHex(packet.MarkerlessPayload.SuffixHex)} entropy={packet.MarkerlessPayload.Entropy:0.####}.");
            }

            if (packet.SnapshotFrame is not null)
            {
                builder.Append($" Snapshot: frame={packet.SnapshotFrame.FrameIndex} base={packet.SnapshotFrame.BaseOrAckFrame} flags=0x{packet.SnapshotFrame.UpdateFlags:x2} records={packet.SnapshotFrame.EntityDeltaRecordCount} trailing={packet.SnapshotFrame.TrailingPayloadLength}.");
            }

            if (packet.QueuedPeerChunk is not null)
            {
                builder.Append($" QueuedChunk: family={packet.QueuedPeerChunk.Family} opaquePrefixLen={packet.QueuedPeerChunk.OpaquePrefixLength} prefixEntropy={packet.QueuedPeerChunk.OpaquePrefixEntropy:0.####} embeddedRecords={packet.QueuedPeerChunk.EmbeddedRecordCount}.");
                if (packet.QueuedPeerChunk.SnapshotInOpaquePrefix is not null)
                {
                    builder.Append($" QueuedSnapshot: frame={packet.QueuedPeerChunk.SnapshotInOpaquePrefix.FrameIndex} records={packet.QueuedPeerChunk.SnapshotInOpaquePrefix.EntityDeltaRecordCount}.");
                }
            }

            if (packet.UnknownFields.Length > 0)
            {
                builder.Append($" Unknown: {string.Join(", ", packet.UnknownFields)}.");
            }

            builder.AppendLine();
        }
    }

    private static string PreviewHex(string value)
    {
        const int maxChars = 96;
        return value.Length <= maxChars
            ? value
            : value[..maxChars] + "...";
    }

    private static string PhaseFor(int sourceStep, double elapsedMilliseconds)
    {
        if (sourceStep < 32 || elapsedMilliseconds <= 2_000)
        {
            return "inferred-source-handoff-setup";
        }

        return elapsedMilliseconds <= 17_000
            ? "inferred-loading-or-motd-transfer"
            : "inferred-gameplay-steady-traffic";
    }

    private static PcapPlainEnglishCount[] CountBy(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new PcapPlainEnglishCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string? Hex(uint? value)
    {
        return value is null ? null : value.Value.ToString("x8");
    }
}

public sealed record PcapPlainEnglishTraceReport(
    PcapPlainEnglishTraceSummary Summary,
    PcapPlainEnglishHelloTrace Hello,
    PcapPlainEnglishTraceTurn[] Turns,
    PcapPlainEnglishTracePacket[] Packets);

public sealed record PcapPlainEnglishTraceSummary(
    string File,
    string ClientEndpoint,
    string ServerEndpoint,
    long ClientHelloPacketIndex,
    long ServerHelloPacketIndex,
    long FirstSourcePacketIndex,
    int NativePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int TurnCount,
    int HighConfidencePacketCount,
    int MediumConfidencePacketCount,
    int LowConfidencePacketCount,
    int FullyFieldNamedPacketCount,
    int PacketCountWithUnknownFields,
    int NativeSnapshotFrameCount,
    int NativeSnapshotEntityDeltaRecordCount,
    int QueuedPeerChunkPacketCount,
    int QueuedPeerChunkWithEmbeddedRecordsCount,
    int QueuedPeerChunkWithoutEmbeddedRecordsCount,
    int QueuedPeerChunkOpaquePrefixBytes,
    int ClientAttachedFramePacketCount,
    int ClientAttachedPayloadFramePacketCount,
    int ClientBitSidecarPacketCount,
    int ClientDecodedNetMessagePacketCount,
    int ClientUserCommandCandidatePacketCount,
    PcapPlainEnglishCount[] PlainEnglishKindCounts,
    PcapPlainEnglishCount[] SemanticRoleCounts,
    PcapPlainEnglishCount[] PhaseCounts,
    PcapPlainEnglishCount[] CompactControlFamilyCounts,
    PcapPlainEnglishCount[] MarkerlessPayloadFamilyCounts,
    PcapPlainEnglishCount[] EmbeddedPrefixFamilyCounts,
    PcapPlainEnglishCount[] ClientPayloadRoleCounts,
    PcapPlainEnglishCount[] ClientAttachedFrameKindCounts,
    PcapPlainEnglishCount[] ClientDecodedNetMessageNameCounts);

public sealed record PcapPlainEnglishHelloTrace(
    long ClientPacketIndex,
    string ClientPayloadHex,
    string ClientPlainEnglish,
    long ServerPacketIndex,
    string ServerPayloadHex,
    string ServerPlainEnglish);

public sealed record PcapPlainEnglishTraceTurn(
    int TurnIndex,
    int[] ClientSourceSteps,
    int[] ServerSourceSteps,
    string PlainEnglish,
    bool HasUnknownFields);

public sealed record PcapPlainEnglishTracePacket(
    int SourceStep,
    long PacketIndex,
    double ElapsedMilliseconds,
    string Direction,
    int PayloadLength,
    ushort? Sequence,
    int? SequenceDelta,
    int? BodyLength,
    string? Phase,
    string PlainEnglishKind,
    string NativeFrameKind,
    string SemanticKind,
    string SemanticRole,
    string Confidence,
    string PlainEnglish,
    string[] UnknownFields,
    int? EmbeddedPrefixLength,
    string EmbeddedPrefixHex,
    PcapPlainEnglishEmbeddedPrefix? EmbeddedPrefix,
    string[] EmbeddedRecordSummaries,
    PcapPlainEnglishCompactControl? CompactControl,
    PcapPlainEnglishMarkerlessPayload? MarkerlessPayload,
    PcapPlainEnglishSnapshotFrame? SnapshotFrame,
    PcapPlainEnglishQueuedPeerChunk? QueuedPeerChunk,
    PcapPlainEnglishEmbeddedRecord[] EmbeddedRecords,
    PcapPlainEnglishClientPayload? ClientPayload,
    string PayloadHex,
    string BodyHex);

public sealed record PcapPlainEnglishCompactControl(
    string Family,
    int BodyLength,
    int PrefixLength,
    string PrefixHex,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    ushort? LastUInt16BigEndian,
    ushort? LastUInt16LittleEndian,
    uint? LastUInt32BigEndian,
    uint? LastUInt32LittleEndian)
{
    public bool EmbeddedRecordBacked => Family.StartsWith("CompactEmbeddedRecordPulse", StringComparison.Ordinal);
}

public sealed record PcapPlainEnglishEmbeddedPrefix(
    string Family,
    int PrefixLength,
    string PrefixHex,
    double PrefixEntropy,
    double PrefixPrintableRatio,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    ushort? LastUInt16BigEndian,
    ushort? LastUInt16LittleEndian,
    uint? LastUInt32BigEndian,
    uint? LastUInt32LittleEndian,
    string RecordRoleSummary,
    string Meaning,
    string UnknownField);

public sealed record PcapPlainEnglishMarkerlessPayload(
    string Family,
    int BodyLength,
    double Entropy,
    double PrintableRatio,
    string PrefixHex,
    string SuffixHex,
    ushort? FirstUInt16BigEndian,
    ushort? FirstUInt16LittleEndian,
    uint? FirstUInt32BigEndian,
    uint? FirstUInt32LittleEndian,
    ushort? LastUInt16BigEndian,
    ushort? LastUInt16LittleEndian,
    uint? LastUInt32BigEndian,
    uint? LastUInt32LittleEndian,
    string Meaning,
    string Confidence,
    string UnknownField)
{
    public static PcapPlainEnglishMarkerlessPayload? Analyze(
        PcapActiveFlowDirection direction,
        int sourceStep,
        double elapsedMilliseconds,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo frame,
        Ps3SourcePayloadSemanticInfo semantic,
        PcapPlainEnglishCompactControl? compactControl,
        PcapPlainEnglishClientPayload? clientPayload,
        IReadOnlyCollection<PcapPlainEnglishEmbeddedRecord> records,
        ReadOnlySpan<byte> body)
    {
        if (body.Length == 0 || compactControl is not null || records.Count > 0)
        {
            return null;
        }

        var phase = PhaseFor(sourceStep, elapsedMilliseconds);
        var prefixLength = Math.Min(body.Length, 16);
        var suffixLength = Math.Min(body.Length, 16);
        var family = FamilyFor(direction, sourceStep, phase, shape, frame, semantic, clientPayload, body.Length);
        var meaning = MeaningFor(direction, family, phase, body.Length);
        var confidence = ConfidenceFor(family);
        return new PcapPlainEnglishMarkerlessPayload(
            family,
            body.Length,
            semantic.Entropy,
            semantic.PrintableRatio,
            Convert.ToHexString(body[..prefixLength]).ToLowerInvariant(),
            Convert.ToHexString(body[^suffixLength..]).ToLowerInvariant(),
            ReadUInt16BigEndian(body, 0),
            ReadUInt16LittleEndian(body, 0),
            ReadUInt32BigEndian(body, 0),
            ReadUInt32LittleEndian(body, 0),
            ReadUInt16BigEndian(body, Math.Max(0, body.Length - 2)),
            ReadUInt16LittleEndian(body, Math.Max(0, body.Length - 2)),
            ReadUInt32BigEndian(body, Math.Max(0, body.Length - 4)),
            ReadUInt32LittleEndian(body, Math.Max(0, body.Length - 4)),
            meaning,
            confidence,
            UnknownFieldFor(family));
    }

    private static string FamilyFor(
        PcapActiveFlowDirection direction,
        int sourceStep,
        string phase,
        Ps3SourceGameplayPacketShape shape,
        Ps3SourceNativeFrameInfo frame,
        Ps3SourcePayloadSemanticInfo semantic,
        PcapPlainEnglishClientPayload? clientPayload,
        int bodyLength)
    {
        if (semantic.Role == Ps3SourcePayloadSemanticRole.InitialHandoffClientProbe)
        {
            return "MarkerlessInitialHandoffProbe";
        }

        if (semantic.Role == Ps3SourcePayloadSemanticRole.NativeLzssCompressed)
        {
            return "MarkerlessNativeLzssWrappedPayload";
        }

        if (semantic.Role is Ps3SourcePayloadSemanticRole.NativeServerInfo49
            or Ps3SourcePayloadSemanticRole.NativeResourceStringTable45
            or Ps3SourcePayloadSemanticRole.NativePlayerSummary44)
        {
            return $"Markerless{semantic.Role}";
        }

        if (clientPayload?.Role == Ps3SourceClientPayloadRole.UserCommandCandidate.ToString())
        {
            return "MarkerlessClientUserCommandOrLoadingAck";
        }

        if (frame.Kind == Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
        {
            return direction == PcapActiveFlowDirection.ServerToClient
                ? "MarkerlessServerQueuedPeerChunk"
                : "MarkerlessClientQueuedPeerChunk";
        }

        if (direction == PcapActiveFlowDirection.ServerToClient && phase == "inferred-source-handoff-setup")
        {
            return bodyLength >= 512
                ? "MarkerlessServerSetupApprovalBlob"
                : $"MarkerlessServerSetupStatePulse{bodyLength}";
        }

        if (direction == PcapActiveFlowDirection.ClientToServer && phase == "inferred-source-handoff-setup")
        {
            return bodyLength >= 256
                ? "MarkerlessClientSetupUpload"
                : $"MarkerlessClientSetupControl{bodyLength}";
        }

        if (shape == Ps3SourceGameplayPacketShape.NearMtuFragment)
        {
            return direction == PcapActiveFlowDirection.ServerToClient
                ? "MarkerlessServerNearMtuLoadingChunk"
                : "MarkerlessClientNearMtuUploadChunk";
        }

        if (bodyLength >= 512)
        {
            return direction == PcapActiveFlowDirection.ServerToClient
                ? "MarkerlessServerLargeLoadingOrEntityState"
                : "MarkerlessClientLargeStateUpload";
        }

        if (direction == PcapActiveFlowDirection.ServerToClient && phase == "inferred-loading-or-motd-transfer")
        {
            return $"MarkerlessServerLoadingStatePulse{bodyLength}";
        }

        if (direction == PcapActiveFlowDirection.ClientToServer && phase == "inferred-loading-or-motd-transfer")
        {
            return $"MarkerlessClientLoadingAckOrUsercmd{bodyLength}";
        }

        return direction == PcapActiveFlowDirection.ServerToClient
            ? $"MarkerlessServerSteadyStatePulse{bodyLength}"
            : $"MarkerlessClientSteadyStateInput{bodyLength}";
    }

    private static string MeaningFor(PcapActiveFlowDirection direction, string family, string phase, int bodyLength)
    {
        return family switch
        {
            "MarkerlessInitialHandoffProbe" => "first markerless native Source setup probe after GameManager handoff",
            "MarkerlessNativeLzssWrappedPayload" => "compressed native Source payload wrapped by the PS3 send path",
            "MarkerlessNativeServerInfo49" => "native server-info message equivalent to the PC Source server-info payload family",
            "MarkerlessNativeResourceStringTable45" => "native resource/string-table message equivalent to the PC Source resource table payload family",
            "MarkerlessNativePlayerSummary44" => "native player-summary message equivalent to the PC Source player list payload family",
            "MarkerlessClientUserCommandOrLoadingAck" => "client-side compact command stream; in loading it behaves as an acknowledgement/input pulse",
            "MarkerlessServerQueuedPeerChunk" => "large server-to-client peer-channel chunk carrying loading, MOTD, string-table, baseline, or entity-state bytes",
            "MarkerlessClientQueuedPeerChunk" => "large client-to-server peer-channel upload chunk",
            "MarkerlessServerSetupApprovalBlob" => "early server setup/approval state blob sent before loading traffic becomes steady",
            "MarkerlessClientSetupUpload" => "early client setup/state upload sent before loading traffic becomes steady",
            "MarkerlessServerNearMtuLoadingChunk" => "near-MTU server loading/entity-state chunk from the native queued send path",
            "MarkerlessClientNearMtuUploadChunk" => "near-MTU client upload chunk from the native queued send path",
            "MarkerlessServerLargeLoadingOrEntityState" => "large server state transfer during map loading or early entity baseline setup",
            "MarkerlessClientLargeStateUpload" => "large client state upload or acknowledgement batch",
            _ when family.StartsWith("MarkerlessServerSetupStatePulse", StringComparison.Ordinal) => "shorter setup pulse that follows the setup approval blob",
            _ when family.StartsWith("MarkerlessClientSetupControl", StringComparison.Ordinal) => "client setup/control pulse before the loading stream settles",
            _ when family.StartsWith("MarkerlessServerLoadingStatePulse", StringComparison.Ordinal) => "server loading-state pulse without embedded object markers",
            _ when family.StartsWith("MarkerlessClientLoadingAckOrUsercmd", StringComparison.Ordinal) => "client loading acknowledgement or compact user-command pulse",
            _ when family.StartsWith("MarkerlessServerSteadyStatePulse", StringComparison.Ordinal) => "server steady-state gameplay/update pulse without embedded object markers",
            _ when family.StartsWith("MarkerlessClientSteadyStateInput", StringComparison.Ordinal) => "client steady-state input/update pulse without embedded object markers",
            _ => $"{direction} markerless native Source payload in {phase} ({bodyLength} body bytes)"
        };
    }

    private static string ConfidenceFor(string family)
    {
        if (family is "MarkerlessInitialHandoffProbe"
            or "MarkerlessNativeLzssWrappedPayload"
            or "MarkerlessNativeServerInfo49"
            or "MarkerlessNativeResourceStringTable45"
            or "MarkerlessNativePlayerSummary44")
        {
            return "medium";
        }

        if (family.Contains("QueuedPeerChunk", StringComparison.Ordinal)
            || family.Contains("NearMtu", StringComparison.Ordinal))
        {
            return "medium";
        }

        return "low";
    }

    private static string UnknownFieldFor(string family)
    {
        if (family.StartsWith("MarkerlessNative", StringComparison.Ordinal))
        {
            return $"field-level layout for {family}";
        }

        if (family.Contains("QueuedPeerChunk", StringComparison.Ordinal)
            || family.Contains("NearMtu", StringComparison.Ordinal))
        {
            return "submessage boundaries inside queued peer-channel payload";
        }

        return $"field-level layout for {family}";
    }

    private static string PhaseFor(int sourceStep, double elapsedMilliseconds)
    {
        if (sourceStep < 32 || elapsedMilliseconds <= 2_000)
        {
            return "inferred-source-handoff-setup";
        }

        return elapsedMilliseconds <= 17_000
            ? "inferred-loading-or-motd-transfer"
            : "inferred-gameplay-steady-traffic";
    }

    private static ushort? ReadUInt16BigEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 2
            ? null
            : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static ushort? ReadUInt16LittleEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 2
            ? null
            : (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    private static uint? ReadUInt32BigEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 4
            ? null
            : ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
    }

    private static uint? ReadUInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
    {
        return bytes.Length < offset + 4
            ? null
            : (uint)(bytes[offset]
                | (bytes[offset + 1] << 8)
                | (bytes[offset + 2] << 16)
                | (bytes[offset + 3] << 24));
    }
}

public sealed record PcapPlainEnglishQueuedPeerChunk(
    string Family,
    int BodyLength,
    int OpaquePrefixLength,
    string OpaquePrefixHex,
    string OpaquePrefixSuffixHex,
    double OpaquePrefixEntropy,
    double OpaquePrefixPrintableRatio,
    bool HasEmbeddedRecords,
    int EmbeddedRecordCount,
    int? FirstEmbeddedRecordOffset,
    bool IsLzssWrapped,
    int? LzssDecodedLength,
    PcapPlainEnglishSnapshotFrame? SnapshotInOpaquePrefix,
    string Meaning,
    string UnknownField)
{
    public static PcapPlainEnglishQueuedPeerChunk? TryAnalyze(
        PcapActiveFlowDirection direction,
        Ps3SourceNativeFrameInfo frame,
        IReadOnlyList<PcapPlainEnglishEmbeddedRecord> records,
        PcapPlainEnglishSnapshotFrame? wholeBodySnapshot,
        ReadOnlySpan<byte> body)
    {
        if (frame.Kind != Ps3SourceNativeFrameKind.QueuedPeerChannelChunkCandidate)
        {
            return null;
        }

        var firstRecordOffset = records.Count == 0
            ? (int?)null
            : records.Min(static record => record.Offset);
        var prefixLength = Math.Clamp(firstRecordOffset ?? body.Length, 0, body.Length);
        var prefix = body[..prefixLength];
        var snapshot = wholeBodySnapshot ?? PcapPlainEnglishSnapshotFrame.TryAnalyze(prefix);
        byte[]? decoded = null;
        var lzssWrapped = Ps3SourceLzss.IsWrapped(prefix)
            && Ps3SourceLzss.TryDecode(prefix, out decoded);
        if (snapshot is null && decoded is not null)
        {
            snapshot = PcapPlainEnglishSnapshotFrame.TryAnalyze(decoded);
        }

        var family = FamilyFor(direction, records.Count, prefixLength, snapshot, lzssWrapped);
        return new PcapPlainEnglishQueuedPeerChunk(
            family,
            body.Length,
            prefixLength,
            Preview(prefix, fromEnd: false),
            Preview(prefix, fromEnd: true),
            Math.Round(Entropy(prefix), 4),
            Math.Round(PrintableRatio(prefix), 4),
            records.Count > 0,
            records.Count,
            firstRecordOffset,
            lzssWrapped,
            decoded?.Length,
            snapshot,
            MeaningFor(direction, records.Count, snapshot, lzssWrapped),
            UnknownFieldFor(records.Count, snapshot));
    }

    private static string FamilyFor(
        PcapActiveFlowDirection direction,
        int embeddedRecordCount,
        int opaquePrefixLength,
        PcapPlainEnglishSnapshotFrame? snapshot,
        bool lzssWrapped)
    {
        var side = direction == PcapActiveFlowDirection.ServerToClient ? "Server" : "Client";
        if (snapshot is not null)
        {
            return $"QueuedPeer{side}SnapshotRecords{snapshot.EntityDeltaRecordCount}";
        }

        if (lzssWrapped)
        {
            return $"QueuedPeer{side}LzssPrefix{opaquePrefixLength}";
        }

        return embeddedRecordCount > 0
            ? $"QueuedPeer{side}OpaquePrefix{opaquePrefixLength}_EmbeddedRecords{embeddedRecordCount}"
            : $"QueuedPeer{side}OpaqueOnly{opaquePrefixLength}";
    }

    private static string MeaningFor(
        PcapActiveFlowDirection direction,
        int embeddedRecordCount,
        PcapPlainEnglishSnapshotFrame? snapshot,
        bool lzssWrapped)
    {
        var side = direction == PcapActiveFlowDirection.ServerToClient ? "server-to-client" : "client-to-server";
        if (snapshot is not null)
        {
            return $"{side} queued peer-channel chunk with a decoded TF.elf snapshot/entity-delta prefix";
        }

        if (lzssWrapped)
        {
            return $"{side} queued peer-channel chunk whose opaque prefix is native LZSS wrapped";
        }

        return embeddedRecordCount > 0
            ? $"{side} queued peer-channel chunk with an opaque native prefix followed by embedded object/state records"
            : $"{side} queued peer-channel chunk carrying opaque loading/MOTD/string-table/baseline/entity-state bytes";
    }

    private static string UnknownFieldFor(int embeddedRecordCount, PcapPlainEnglishSnapshotFrame? snapshot)
    {
        if (snapshot is not null)
        {
            return snapshot.TrailingPayloadLength > 0
                ? "queued snapshot trailing payload after decoded entity-delta prefix"
                : "no remaining queued snapshot fields";
        }

        return embeddedRecordCount > 0
            ? "field-level layout of queued opaque prefix before embedded records"
            : "queued peer-channel opaque payload submessage boundaries";
    }

    private static string Preview(ReadOnlySpan<byte> value, bool fromEnd)
    {
        if (value.IsEmpty)
        {
            return "";
        }

        const int previewLength = 16;
        var count = Math.Min(previewLength, value.Length);
        var slice = fromEnd
            ? value[^count..]
            : value[..count];
        return Convert.ToHexString(slice).ToLowerInvariant();
    }

    private static double PrintableRatio(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        var printable = 0;
        foreach (var b in data)
        {
            if (b is >= 0x20 and <= 0x7e or 0x09 or 0x0a or 0x0d)
            {
                printable++;
            }
        }

        return printable / (double)data.Length;
    }

    private static double Entropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
        {
            counts[b]++;
        }

        var entropy = 0.0;
        foreach (var count in counts)
        {
            if (count == 0)
            {
                continue;
            }

            var p = count / (double)data.Length;
            entropy -= p * Math.Log2(p);
        }

        return entropy;
    }
}

public sealed record PcapPlainEnglishSnapshotFrame(
    int FrameIndex,
    int BaseOrAckFrame,
    byte UpdateFlags,
    byte? PendingCount,
    int EntityDeltaRecordCount,
    string[] EntityDeltaRecordSummaries,
    int EntityDeltaConsumedBits,
    int TrailingPayloadLength,
    string ContractSource)
{
    public static PcapPlainEnglishSnapshotFrame? TryAnalyze(ReadOnlySpan<byte> body)
    {
        if (!Ps3SourceSnapshotFrameBuilder.TryDecode(body, out var snapshot)
            || !snapshot.Header.HasEntityDelta
            || snapshot.EntityDeltaSection is not { } section
            || section.Records.Length == 0
            || !section.Records.Any(IsPlausibleSourceEntityRecord))
        {
            return null;
        }

        var summaries = section.Records
            .Select(static record =>
                record.ObjectName is { Length: > 0 }
                    ? $"group={record.GroupIndex} start={record.StartIndex?.ToString() ?? "?"} count={record.EntityCount?.ToString() ?? "?"} object=0x{record.ObjectId?.ToString("x8") ?? "????????"} name={record.ObjectName} bits={record.BitLength}"
                    : $"group={record.GroupIndex} fullGroup bits={record.BitLength}")
            .ToArray();
        return new PcapPlainEnglishSnapshotFrame(
            snapshot.Header.FrameIndex,
            snapshot.Header.BaseOrAckFrame,
            snapshot.Header.UpdateFlags,
            snapshot.Header.PendingCount,
            section.Records.Length,
            summaries,
            section.ConsumedBits,
            snapshot.TrailingPayload.Length,
            snapshot.ContractSource);
    }

    private static bool IsPlausibleSourceEntityRecord(Ps3SourceEntityDeltaNativeRecord record)
    {
        return record.IsPartialRun
            && record.ObjectId.HasValue
            && record.ObjectName is { Length: > 1 }
            && record.ObjectName[0] == 'C'
            && record.EntityCount is > 0;
    }
}

public sealed record PcapPlainEnglishEmbeddedRecord(
    string Marker,
    int Offset,
    int Length,
    string Role,
    string? ObjectId,
    string? OwnerOrRootObjectId,
    string? LinkedObjectId,
    string? ClassId,
    string? DisplayName,
    string HeaderHex,
    string[] PrintableCandidates);

public sealed record PcapPlainEnglishClientPayload(
    string Role,
    string NativeFrameKind,
    string BodyPrefixHex,
    byte? ReliableAssociationMessageType,
    string? ReliableAssociationNativeToken,
    byte? AttachedFrameKind,
    ushort? AttachedFrameDeclaredLength,
    string? AttachedFrameNativeToken,
    int? BitSidecarOffset,
    int? BitSidecarBitCount,
    int? BitSidecarPayloadLength,
    int? DecodedNetMessageType,
    string? DecodedNetMessageName,
    string? DecodedNetMessagePayloadKind,
    int? DecodedNetMessagePayloadOffset,
    int? DecodedNetMessagePayloadLength,
    int? DecodedNetMessagePayloadBitCount,
    PcapPlainEnglishClientCommandIntent? CommandIntent);

public sealed record PcapPlainEnglishClientCommandIntent(
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

public sealed record PcapPlainEnglishCount(string Value, int Count);

internal sealed record PcapPlainEnglishExplanation(
    string Text,
    string Confidence,
    string[] UnknownFields);
