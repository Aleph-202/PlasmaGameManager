using System.Net;
using PlasmaGameManager.Protocol;
using PlasmaGameManager.ReTools;

var command = args.Length > 0 ? args[0] : "help";
var repoRoot = FindRepoRoot();

switch (command)
{
    case "sync-inputs":
    {
        var report = LocalInputSync.Sync(repoRoot);
        Console.WriteLine($"local inputs synced into .local/input: {report.OverallStatus} ({report.Summary.RequiredPresentCount}/{report.Summary.RequiredInputCount} required)");
        Console.WriteLine($"TF2 PS3 content: {(report.Summary.ContentReady ? "ready" : "partial/missing")} ({report.Summary.RequiredMapPresentCount}/{report.Summary.RequiredMapCount} maps, {report.Summary.RecommendedResourcePresentCount}/{report.Summary.RecommendedResourceCount} resources)");
        Console.WriteLine($"  content root: {report.Summary.Tf2Ps3SourceContentRoot}");
        Console.WriteLine($"  map root:     {report.Summary.Tf2Ps3MapRoot}");
        break;
    }
    case "validate-inputs":
    {
        var report = LocalInputSync.ValidateSynced(repoRoot);
        Console.WriteLine($"local input status: {report.OverallStatus} ({report.Summary.RequiredPresentCount}/{report.Summary.RequiredInputCount} required)");
        Console.WriteLine($"TF2 PS3 content: {(report.Summary.ContentReady ? "ready" : "partial/missing")} ({report.Summary.RequiredMapPresentCount}/{report.Summary.RequiredMapCount} maps, {report.Summary.RecommendedResourcePresentCount}/{report.Summary.RecommendedResourceCount} resources)");
        Console.WriteLine($"  content root: {report.Summary.Tf2Ps3SourceContentRoot}");
        Console.WriteLine($"  map root:     {report.Summary.Tf2Ps3MapRoot}");
        break;
    }
    case "validate-tf2ps3-source-content":
    {
        var contentRoot = args.Length > 1 ? args[1] : null;
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/tf2ps3-source-content-validation.json");
        var mapRoot = args.Length > 3 ? args[3] : null;
        var report = Tf2Ps3SourceContentValidator.Validate(repoRoot, contentRoot, mapRoot);
        Tf2Ps3SourceContentValidator.WriteReport(output, report);
        Console.WriteLine($"TF2 PS3 native Source content: {report.OverallStatus} ({report.Summary.RequiredReferencePresentCount}/{report.Summary.RequiredReferenceCount} required references)");
        Console.WriteLine($"  content root: {report.Summary.ContentRoot}");
        Console.WriteLine($"  map root:     {report.Summary.MapRoot}");
        Console.WriteLine($"  generated refs: {report.Summary.GeneratedResourceReferencePresentCount}/{report.Summary.GeneratedResourceReferenceCount} resolved, {report.Summary.VirtualServerOnlyReferenceCount} virtual");
        Console.WriteLine($"  game events: {report.Summary.LoadedGameEventDescriptorCount} loaded, fallback={report.Summary.UsesFallbackGameEventDescriptors}");
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-pcaps":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-semantic-summary.json");
        var dispatcherMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        await new PcapSemanticAnalyzer().AnalyzeDirectoryAsync(input, output, dispatcherMap);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-pcap-corpus":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-corpus-coverage.json");
        var dispatcherMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        await new PcapCorpusCoverageAnalyzer().AnalyzeDirectoryAsync(input, output, dispatcherMap);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-ea-text-pcaps":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-ea-text-summary.json");
        await new PcapEaTextAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "extract-tf2-source-launch-profile":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/pcap-ea-text-summary.json");
        var scenarioFile = args.Length > 2 ? args[2] : "creation/creating_and_join_cp_db_unranked_1.pcapng";
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/tf2-source-launch-profile.json");
        var profile = await Tf2SourceLaunchProfileExtractor.ExtractAsync(input, scenarioFile, output);
        Console.WriteLine($"wrote {output}: {profile.MapName} {profile.BackendHost}:{profile.BackendPort}");
        break;
    }
    case "analyze-handoff-topology":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-handoff-topology.json");
        var dispatcherMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        await new PcapHandoffTopologyAnalyzer().AnalyzeDirectoryAsync(input, output, dispatcherMap);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-gamemanager-handoff-boundary":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-gamemanager-handoff-boundary.json");
        await new PcapGameManagerHandoffBoundaryAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-gamemanager-hello":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-gamemanager-hello.json");
        await new PcapGameManagerHelloAnalyzer().AnalyzeAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-streams":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-streams.json");
        await new PcapSourceStreamAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-gameplay-phases":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-gameplay-phases.json");
        await new PcapSourceGameplayPhaseAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "dump-active-flow":
    {
        var input = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps/TF2_PS3_network_traffic/packets/server/connections/quick_match_to_motd_2fort_1.pcapng");
        var start = args.Length > 2 ? int.Parse(args[2]) : 0;
        var count = args.Length > 3 ? int.Parse(args[3]) : 12;
        var replay = new PcapActiveFlowReplayExtractor().Extract(input);
        if (replay is null)
        {
            Console.Error.WriteLine($"no active flow found: {input}");
            Environment.ExitCode = 1;
            break;
        }

        Console.WriteLine($"file={input}");
        Console.WriteLine($"client={replay.ClientEndpoint}");
        Console.WriteLine($"server={replay.ServerEndpoint}");
        Console.WriteLine($"clientHelloIndex={replay.ClientHelloPacketIndex}");
        Console.WriteLine($"serverHelloIndex={replay.ServerHelloPacketIndex}");
        Console.WriteLine($"clientHello={Convert.ToHexString(replay.ClientHelloPayload).ToLowerInvariant()}");
        Console.WriteLine($"serverHello={Convert.ToHexString(replay.ServerHelloPayload).ToLowerInvariant()}");
        Console.WriteLine($"firstSourceClientIndex={replay.FirstSourceClientPacketIndex}");
        Console.WriteLine($"firstSourceClientKind={replay.FirstSourceClientKind}");
        Console.WriteLine($"firstSourceClient={Convert.ToHexString(replay.FirstSourceClientPayload).ToLowerInvariant()}");
        foreach (var item in replay.SourcePackets.Select((packet, index) => (packet, index)).Skip(start).Take(count))
        {
            Console.WriteLine(FormatActiveFlowPacket(item.index, item.packet));
        }
        break;
    }
    case "dump-active-flow-packet":
    {
        var input = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps/TF2_PS3_network_traffic/packets/server/connections/quick_match_to_motd_2fort_1.pcapng");
        var sourceStep = args.Length > 2 ? int.Parse(args[2]) : 0;
        var replay = new PcapActiveFlowReplayExtractor().Extract(input);
        if (replay is null)
        {
            Console.Error.WriteLine($"no active flow found: {input}");
            Environment.ExitCode = 1;
            break;
        }

        if ((uint)sourceStep >= (uint)replay.SourcePackets.Length)
        {
            Console.Error.WriteLine($"source step out of range: {sourceStep}; packet count={replay.SourcePackets.Length}");
            Environment.ExitCode = 1;
            break;
        }

        var packet = replay.SourcePackets[sourceStep];
        Console.WriteLine($"file={input}");
        Console.WriteLine(FormatActiveFlowPacket(sourceStep, packet));
        Console.WriteLine($"payloadHex={Convert.ToHexString(packet.Payload).ToLowerInvariant()}");
        if (Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
        {
            Console.WriteLine($"bodyHex={Convert.ToHexString(transport.Body).ToLowerInvariant()}");
            var semantic = Ps3SourcePayloadSemantics.Analyze(transport.Body);
            Console.WriteLine($"semantic={semantic.Kind}/{semantic.Role}");
            Console.WriteLine($"markers={string.Join(",", semantic.EmbeddedMarkers.Select(static marker => marker.ToString()))}");
            Console.WriteLine($"ascii={semantic.AsciiPreview}");
        }

        break;
    }
    case "export-pcap-plain-english-trace":
    {
        var input = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps/TF2_PS3_network_traffic/packets/server/connections/quick_match_to_motd_2fort_1.pcapng");
        var output = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "docs/quick-match-to-motd-2fort-1.semantic-trace.json");
        var report = await new PcapPlainEnglishTraceExporter().ExportAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"packets={report.Summary.NativePacketCount} turns={report.Summary.TurnCount} high={report.Summary.HighConfidencePacketCount} medium={report.Summary.MediumConfidencePacketCount} low={report.Summary.LowConfidencePacketCount} unknownPackets={report.Summary.PacketCountWithUnknownFields}");
        break;
    }
    case "export-pcap-plain-english-corpus":
    {
        var input = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "docs/pcap-packet-language-corpus.json");
        var report = await new PcapPlainEnglishCorpusExporter().ExportAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"captures={report.Summary.ActiveCaptureCount} skipped={report.Summary.SkippedInputCount} packets={report.Summary.NativePacketCount} unknownPackets={report.Summary.PacketCountWithUnknownFields}");
        break;
    }
    case "search-active-flow-client":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var length = args.Length > 2 ? int.Parse(args[2]) : 235;
        var context = args.Length > 3 ? int.Parse(args[3]) : 4;
        var limit = args.Length > 4 ? int.Parse(args[4]) : 25;
        var hits = 0;
        foreach (var pcap in EnumeratePcapInputs(input))
        {
            var replay = new PcapActiveFlowReplayExtractor().Extract(pcap);
            if (replay is null)
            {
                continue;
            }

            for (var i = 0; i < replay.SourcePackets.Length; i++)
            {
                var packet = replay.SourcePackets[i];
                if (packet.Direction != PcapActiveFlowDirection.ClientToServer
                    || packet.Payload.Length != length)
                {
                    continue;
                }

                hits++;
                Console.WriteLine($"hit={hits} file={Path.GetRelativePath(repoRoot, pcap)} sourceStep={i} packetIndex={packet.PacketIndex}");
                var start = Math.Max(0, i - context);
                var end = Math.Min(replay.SourcePackets.Length - 1, i + context);
                for (var j = start; j <= end; j++)
                {
                    Console.WriteLine(FormatActiveFlowPacket(j, replay.SourcePackets[j]));
                }

                Console.WriteLine();
                if (hits >= limit)
                {
                    Console.WriteLine($"hit limit reached: {limit}");
                    goto SearchActiveFlowClientDone;
                }
            }
        }

SearchActiveFlowClientDone:
        Console.WriteLine($"hits={hits}");
        break;
    }
    case "analyze-source-transport":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-transport-semantics.json");
        await new PcapSourceTransportSemanticsAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-transport-fields":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-transport-fields.json");
        await new PcapSourceTransportFieldAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-clc-move-boundaries":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-clc-move-boundaries.json");
        await new PcapSourceClcMoveBoundaryAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-client-input-coverage":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-client-input-coverage.json");
        var report = await new PcapSourceClientInputCoverageAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"clientPackets={report.Summary.ClientSourcePacketCount} nativeCovered={report.Summary.NativeCoveredClientPacketCount} hardMarkerless={report.Summary.HardMarkerlessClientPacketCount} unknownHardMarkerless={report.Summary.UnknownHardMarkerlessClientPacketCount} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "analyze-source-client-boundary-probes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-client-boundary-probes.json");
        var report = await new PcapSourceClientBoundaryProbeAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} nativeDecoded={report.Summary.NativeDecodedPacketCount} wrapperMatched={report.Summary.WrapperLayoutMatchedPacketCount} noWrapper={report.Summary.NoRecoveredWrapperLayoutPacketCount} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "analyze-markerless-transform-probes-strict":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-markerless-transform-probes-strict.json");
        var report = await new PcapMarkerlessTransformProbeAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} packetHits={report.Summary.PacketWithStrictTransformHitCount} strictHits={report.Summary.StrictTransformHitCount} nativeReady={report.Summary.NativeMarkerlessTransformReady}");
        break;
    }
    case "analyze-markerless-session-key-probes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var eaText = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-ea-text-summary.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-markerless-session-key-probes.json");
        var report = await new PcapMarkerlessSessionKeyProbeAnalyzer().AnalyzeDirectoryAsync(input, eaText, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} keyedPackets={report.Summary.PacketsWithSessionKeys} packetHits={report.Summary.PacketWithAcceptedTransformHitCount} clcHits={report.Summary.ClcMoveHitCount} netHits={report.Summary.NetMessageHitCount} queueHits={report.Summary.QueueDeltaExactHitCount} nativeReady={report.Summary.NativeMarkerlessTransformReady}");
        break;
    }
    case "analyze-source-usercmd-record-candidates":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-usercmd-record-candidates.json");
        var report = await new PcapSourceUsercmdRecordCandidateAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"recordMultiples={report.Summary.ExactRecordMultiplePacketCount} decoded={report.Summary.NativeDecodedExactRecordMultiplePacketCount} undecoded={report.Summary.UndecodedExactRecordMultiplePacketCount} twoRecord={report.Summary.ExactTwoRecordPacketCount}");
        break;
    }
    case "analyze-source-usercmd-queue-delta-tail":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/pcap-source-usercmd-record-candidates.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-usercmd-queue-delta-tail.json");
        var report = await new PcapSourceUsercmdQueueDeltaTailAnalyzer().AnalyzeAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"candidates={report.Summary.CandidatePacketCount} twoRecord={report.Summary.ExactTwoRecordPacketCount} nonZeroTrailing={report.Summary.DecodedWithNonZeroTrailingPacketCount} fixedTrailerRuledOut={report.Summary.FixedTrailerHypothesisRuledOut} nativeReady={report.Summary.NativeBoundaryReady}");
        break;
    }
    case "analyze-source-payload-object-first-word":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-payload-object-first-word.json");
        var report = await new PcapSourcePayloadObjectFirstWordAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} ownerSlot8={report.Summary.OwnerSlot8Count} fragments={report.Summary.FragmentedSpecialWrapperCount} repacked={report.Summary.RepackedSpecialWrapperCount} associated={report.Summary.AssociatedObjectTokenCount} distinctAssociated={report.Summary.DistinctAssociatedObjectTokenCount} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "analyze-source-associated-object-tokens":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var contract = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-token-contract.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-source-associated-object-tokens.json");
        var report = await new PcapSourceAssociatedObjectTokenAnalyzer().AnalyzeDirectoryAsync(input, contract, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} associated={report.Summary.AssociatedObjectTokenPacketCount} probes={report.Summary.AssociationProbeCount} probeTokenMatches={report.Summary.AssociatedObjectTokenEqualsProbeTokenPacketCount} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "analyze-associated-object-token-transform-probes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-associated-object-token-transform-probes.json");
        var report = await new PcapAssociatedObjectTokenTransformProbeAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} associated={report.Summary.AssociatedObjectTokenPacketCount} packetHits={report.Summary.PacketWithAcceptedTransformHitCount} clcHits={report.Summary.ClcMoveHitCount} netHits={report.Summary.NetMessageHitCount} queueHits={report.Summary.QueueDeltaExactHitCount} nativeReady={report.Summary.NativeAssociatedObjectTokenTransformReady}");
        break;
    }
    case "analyze-source-native-association-descriptors":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-native-association-descriptors.json");
        var report = await new PcapSourceNativeAssociationDescriptorScanAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardMarkerless={report.Summary.HardMarkerlessPacketCount} beOffset0={report.Summary.BigEndianDescriptorAtOffset0PacketCount} beAny={report.Summary.BigEndianDescriptorAnyOffsetPacketCount} leOffset0={report.Summary.LittleEndianDescriptorAtOffset0PacketCount} leAny={report.Summary.LittleEndianDescriptorAnyOffsetPacketCount} nativeReady={report.Summary.NativeDescriptorBoundaryReady}");
        break;
    }
    case "analyze-embedded-clc-move-candidates":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-embedded-clc-move-candidates.json");
        var report = await new PcapEmbeddedClcMoveCandidateAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"embedded={report.Summary.EmbeddedCandidateCount} usercmd={report.Summary.UserCommandCandidateCount} hardMarkerless={report.Summary.HardMarkerlessCandidateCount} nativeReady={report.Summary.NativeBoundaryReady}");
        break;
    }
    case "analyze-client-command-worklist":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-client-command-worklist.json");
        var report = await new PcapClientCommandWorklistAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"clientPackets={report.Summary.ClientPacketCount} usercmdCandidates={report.Summary.UserCommandCandidateCount} exactClcMove={report.Summary.ExactClcMoveBoundaryHitCount} embeddedCandidates={report.Summary.EmbeddedClcMoveCandidateCount}");
        break;
    }
    case "analyze-opaque-markerless-command-wrapper":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-opaque-markerless-command-wrapper.json");
        var report = await new PcapOpaqueMarkerlessCommandWrapperAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"opaquePackets={report.Summary.OpaquePacketCount} lengths={report.Summary.DistinctBodyLengthCount} avgEntropy={report.Summary.AverageEntropy} uniquePrefixRatio={report.Summary.UniquePrefix8Ratio}");
        break;
    }
    case "analyze-markerless-boundary-hypotheses":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var clientWorklist = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-client-command-worklist.json");
        var opaqueWrapper = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-opaque-markerless-command-wrapper.json");
        var udpIngressCorrection = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-udp-ingress-correction.json");
        var serverDllTunnel = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-map.json");
        var serverDllUserCmd = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-decoder.json");
        var output = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "artifacts/pcap-markerless-boundary-hypotheses.json");
        var report = await new PcapMarkerlessBoundaryHypothesisAnalyzer().AnalyzeAsync(
            input,
            clientWorklist,
            opaqueWrapper,
            udpIngressCorrection,
            serverDllTunnel,
            serverDllUserCmd,
            output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"hardOpaque={report.Summary.HardOpaqueMarkerlessPacketCount} rawAttached={report.Summary.HardRawAttachedType2LittleEndianExactHitCount + report.Summary.HardRawAttachedType2BigEndianExactHitCount} prefixAttached={report.Summary.HardEmbeddedAttachedType2LittleEndianExactHitCount + report.Summary.HardEmbeddedAttachedType2BigEndianExactHitCount} nativeReady={report.Summary.NativeMarkerlessBoundaryReady}");
        break;
    }
    case "analyze-source-queued-peer-turns":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-turns.json");
        await new PcapSourceQueuedPeerTurnAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-queued-peer-opaque":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-opaque.json");
        var report = await new PcapSourceQueuedPeerOpaqueAnalyzer().AnalyzePathAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"queued={report.Summary.QueuedChunkPacketCount} withRecords={report.Summary.QueuedChunkWithEmbeddedRecordsCount} opaqueOnly={report.Summary.QueuedChunkWithoutEmbeddedRecordsCount} lzss={report.Summary.LzssWrappedPrefixCount} snapshots={report.Summary.StrictSnapshotPrefixCount}");
        break;
    }
    case "analyze-source-packet-shapes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-packet-shapes.json");
        await new PcapSourcePacketShapeAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-embedded-objects":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-embedded-objects.json");
        await new PcapSourceEmbeddedObjectAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-frozen-state-turns":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-frozen-state-turns.json");
        var report = await new PcapFrozenStateTurnAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"files={report.Summary.FileCount} active={report.Summary.ActiveSourceFlowFileCount} frozenTurns={report.Summary.FrozenStateTurnCount} retailTrio={report.Summary.RetailPostRosterTrioShapeCount}");
        break;
    }
    case "analyze-source-replay-corpus":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-replay-corpus.json");
        await new PcapSourceReplayCorpusAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-turn-contract":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-turn-contract.json");
        await new PcapSourceTurnContractAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-native-builder-correlation":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-native-builder-correlation.json");
        var sourceNetworkAnchorMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-network-anchor-map.json");
        await new PcapSourceNativeBuilderCorrelationAnalyzer().AnalyzeDirectoryAsync(input, output, sourceNetworkAnchorMap);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-bridge-contract":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-bridge-contract.json");
        await new PcapSourceBridgeContractAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-client-visible-source-endpoints":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-client-visible-source-endpoints.json");
        await new PcapClientVisibleSourceEndpointAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-backend-boundary":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-backend-boundary.json");
        var sourceNetworkAnchorMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-network-anchor-map.json");
        await new PcapSourceBackendBoundaryAnalyzer().AnalyzeDirectoryAsync(input, output, sourceNetworkAnchorMap);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-source-translation-readiness":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-translation-readiness.json");
        await new PcapSourceTranslationReadinessAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-generated-source-responder-shapes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-generated-source-responder-shapes.json");
        await new PcapGeneratedSourceResponderShapeAnalyzer().AnalyzeDirectoryAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-generated-source-transform-probes":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-generated-source-transform-probes.json");
        var ekey = args.Length > 3
            ? args[3]
            : Environment.GetEnvironmentVariable("TF2_GAME_EKEY");
        await new PcapGeneratedSourceTransformProbeAnalyzer().AnalyzeDirectoryAsync(input, output, ekey);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "reduce-tf2ps3-source-field-contract":
    {
        var builderMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-builder-map.json");
        var embeddedObjects = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-embedded-objects.json");
        var gameplayPhases = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-source-gameplay-phases.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-field-contract.json");
        await Tf2Ps3SourceFieldContractReducer.ReduceAsync(builderMap, embeddedObjects, gameplayPhases, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-contract":
    {
        var datatables = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-datatables.txt");
        var netprops = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-netprops.txt");
        var ghidraEvidence = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-ghidra-evidence.txt");
        var sourceFieldContract = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-field-contract.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-contract.json");
        await Eatf2ServerDllContractReducer.ReduceAsync(datatables, netprops, ghidraEvidence, sourceFieldContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-simulation":
    {
        var interestingStrings = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-interesting-strings.txt");
        var mangledSymbols = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-mangled-symbols.txt");
        var ghidraEvidence = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-ghidra-evidence.txt");
        var datatables = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-datatables.txt");
        var netprops = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-netprops.txt");
        var sourceFieldContract = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-field-contract.json");
        var output = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-simulation-contract.json");
        await Eatf2ServerDllSimulationReducer.ReduceAsync(interestingStrings, mangledSymbols, ghidraEvidence, datatables, netprops, sourceFieldContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-native-obligations":
    {
        var interestingStrings = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-interesting-strings.txt");
        var mangledSymbols = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-mangled-symbols.txt");
        var ghidraEvidence = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-ghidra-evidence.txt");
        var sourceRoot = args.Length > 4 ? args[4] : repoRoot;
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-native-obligations.json");
        await Eatf2ServerDllNativeObligationReducer.ReduceAsync(interestingStrings, mangledSymbols, ghidraEvidence, sourceRoot, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-target-functions":
    {
        var targetFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-functions.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-function-map.json");
        await Eatf2ServerDllTargetFunctionReducer.ReduceAsync(targetFunctions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-runtime-contract":
    {
        var targetFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-functions.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-runtime-contract.json");
        await Eatf2ServerDllRuntimeContractReducer.ReduceAsync(targetFunctions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-tunnel-map":
    {
        var serverDll = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/EATF2ServerDLL/server.dll");
        var targetFunctions = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-functions.json");
        var ghidraEvidence = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/eatf2-serverdll-ghidra-evidence.txt");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-map.json");
        await Eatf2ServerDllTunnelReducer.ReduceAsync(serverDll, targetFunctions, ghidraEvidence, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-tunnel-ghidra":
    {
        var ghidraEvidence = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-ghidra.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-ghidra-map.json");
        await Eatf2ServerDllTunnelGhidraReducer.ReduceAsync(ghidraEvidence, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-usercmd-layout":
    {
        var targetFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-functions.json");
        var runtimeContract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-runtime-contract.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-layout.json");
        await Eatf2ServerDllUserCmdLayoutReducer.ReduceAsync(targetFunctions, runtimeContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-usercmd-decoder":
    {
        var targetFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-target-functions.json");
        var layout = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-layout.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-decoder.json");
        await Eatf2ServerDllUserCmdDecoderReducer.ReduceAsync(targetFunctions, layout, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-eatf2-serverdll-usercmd-physics-audit":
    {
        var runtimeContract = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-runtime-contract.json");
        var sourceRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "src");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-physics-audit.json");
        await Eatf2ServerDllUserCmdPhysicsAuditReducer.ReduceAsync(runtimeContract, sourceRoot, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-clc-move-contract":
    {
        var ghidraContext = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-context.json");
        var sourceEngineRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/source-engine-master");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-contract.json");
        await Tf2Ps3SourceClcMoveContractReducer.ReduceAsync(ghidraContext, sourceEngineRoot, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-snapshot-path":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-snapshot-path-map.json");
        await Tf2Ps3SourceSnapshotPathReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-peer-channel":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var generatedShapeReport = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "artifacts/pcap-generated-source-responder-shapes-all.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-peer-channel-map.json");
        await Tf2Ps3SourcePeerChannelReducer.ReduceAsync(cExport, generatedShapeReport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-snapshot-delta":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-snapshot-delta-map.json");
        await Tf2Ps3SourceSnapshotDeltaReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-receive-path":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-path-map.json");
        await Tf2Ps3SourceReceivePathReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-reliable-peer-attach":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-reliable-peer-attach.json");
        var report = await Tf2Ps3SourceReliablePeerAttachReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"attach={report.Summary.AttachChainRecovered} nativeInput={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-pre-payload-receive":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-pre-payload-receive-boundary.json");
        var report = await Tf2Ps3SourcePrePayloadReceiveReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"queueInsert={report.Summary.QueueInsertionCopiesPayload} queueDrain={report.Summary.QueueDrainCopiesQueuedPayload} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-payload-object-dispatch":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var prePayload = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-pre-payload-receive-boundary.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-object-dispatch-map.json");
        var sourceRoot = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "src");
        var report = await Tf2Ps3SourcePayloadObjectDispatchReducer.ReduceAsync(cExport, prePayload, output, sourceRoot);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"associated={report.Summary.AssociatedSlot90DispatchRecovered} owner={report.Summary.OwnerBitreaderDispatchRecovered} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-associated-object-token-contract":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-token-contract.json");
        var report = await Tf2Ps3SourceAssociatedObjectTokenContractReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"contractRecovered={report.Summary.AssociatedObjectTokenContractRecovered} slot90={report.Summary.AssociatedSlot90DispatchRecovered} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "reduce-tf2ps3-source-associated-object-slot90":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var playerVtable = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-player-vtable-map.json");
        var tokenContract = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-token-contract.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-map.json");
        var registerFunctions = args.Length > 5
            ? args[5]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-register-functions.json");
        var callsiteContext = args.Length > 6
            ? args[6]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-callsite-context.json");
        var outputBuilderFunctions = args.Length > 7
            ? args[7]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slotac-output-builder-functions.json");
        var report = await Tf2Ps3SourceAssociatedObjectSlot90Reducer.ReduceAsync(
            cExport,
            playerVtable,
            tokenContract,
            output,
            registerFunctions,
            callsiteContext,
            outputBuilderFunctions);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"slot90={report.Summary.Slot90EntryRecovered} slotAc={report.Summary.SlotAcStateTripleRecovered} slotB4={report.Summary.SlotB4DescriptorPredicateRecovered} registers={report.RegisterContract.AssociatedSlot90DispatchRegistersRecovered} outputStates={string.Join(',', report.OutputBuilderRegisterCensus.StateConstants)} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "reduce-tf2ps3-source-associated-slotac-provenance":
    {
        var outputBuilderFunctions = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slotac-output-builder-functions.json");
        var slot90 = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-map.json");
        var output = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-slotac-provenance.json");
        var report = await Tf2Ps3SourceAssociatedSlotAcProvenanceReducer.ReduceAsync(outputBuilderFunctions, slot90, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"slotAc={report.Summary.SlotAcCallsiteCount} serverOutput={report.Summary.ProvenServerOutputStateCallsiteCount} clientCandidates={report.Summary.ClientUploadDecoderCandidateCount} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "reduce-tf2ps3-source-associated-lane-role":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var payloadObjectDispatch = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-payload-object-dispatch-map.json");
        var slot90 = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-map.json");
        var slotAcProvenance = args.Length > 4
            ? args[4]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-slotac-provenance.json");
        var ownerForwardContext = args.Length > 5
            ? args[5]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-context-map.json");
        var category5Usercmd = args.Length > 6
            ? args[6]
            : Path.Combine(repoRoot, "re/tf2ps3/source-category5-usercmd-handler-map.json");
        var output = args.Length > 7
            ? args[7]
            : Path.Combine(repoRoot, "re/tf2ps3/source-associated-lane-role.json");
        var report = await Tf2Ps3SourceAssociatedLaneRoleReducer.ReduceAsync(
            cExport,
            payloadObjectDispatch,
            slot90,
            slotAcProvenance,
            ownerForwardContext,
            category5Usercmd,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"laneSplit={report.Summary.PayloadDrainLaneSplitRecovered} associatedNotClc={report.Summary.AssociatedLaneRejectedAsClcMoveBoundary} ownerUsercmd={report.Summary.OwnerLaneIsUsercmdRoute} nativeReady={report.Summary.NativeSourceInputReady}");
        break;
    }
    case "reduce-tf2ps3-source-owner-control-subobject":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var ownerVtable = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-vtable-map.json");
        var playerVtable = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-player-vtable-map.json");
        var payloadObjectDispatch = args.Length > 4
            ? args[4]
            : Path.Combine(repoRoot, "re/tf2ps3/source-payload-object-dispatch-map.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-control-subobject-map.json");
        var sourceRoot = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "src");
        var report = await Tf2Ps3SourceOwnerControlSubobjectReducer.ReduceAsync(cExport, ownerVtable, playerVtable, payloadObjectDispatch, output, sourceRoot);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"ownerSlot8={report.Summary.OwnerSlot8TargetRecovered} ownerForwarder={report.Summary.OwnerSlot8ForwarderRecovered} associatedCandidate={report.Summary.AssociatedSlot90CandidateRecovered} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-owner-forward-target":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var ownerControlSubobject = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-control-subobject-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-target-map.json");
        var report = await Tf2Ps3SourceOwnerForwardTargetReducer.ReduceAsync(cExport, ownerControlSubobject, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"router={report.Summary.OwnerForwardTargetRecovered} categories={report.Summary.CategoryRulesRecovered} contexts={report.Summary.ContextLookupRecovered} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-owner-forward-context":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var ownerVtable = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-vtable-map.json");
        var ownerForwardTarget = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-target-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-context-map.json");
        var sourceRoot = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "src");
        var report = await Tf2Ps3SourceOwnerForwardContextReducer.ReduceAsync(cExport, ownerVtable, ownerForwardTarget, output, sourceRoot);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"forwarders={report.Summary.OwnerForwarderSlotCount} category5={report.Summary.Category5UsercmdRouteCandidateRecovered} dynamicHandler={report.Summary.Context5PassedToDynamicHandler} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-owner-forwarder-bitstream-coverage":
    {
        var ownerForwardContext = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-context-map.json");
        var sourceRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "src");
        var output = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forwarder-bitstream-coverage.json");
        var report = await Tf2Ps3SourceOwnerForwarderBitstreamCoverageReducer.ReduceAsync(
            ownerForwardContext,
            sourceRoot,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"wrapped={report.Summary.ImplementedWrappedForwarderCount}/{report.Summary.WrappedForwarderCount} word5={report.Summary.Word5BoundaryImplemented} word6={report.Summary.Word6BoundaryImplemented} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-owner-forward-wrapper-variants":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var sourceRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "src");
        var output = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-wrapper-variants.json");
        var report = await Tf2Ps3SourceOwnerForwardWrapperVariantReducer.ReduceAsync(
            cExport,
            sourceRoot,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"wrappers={report.Summary.ShapeRecoveredPrimaryWrapperCount}/{report.Summary.PrimaryWrapperCount} implemented={report.Summary.ImplementedOrNotRequiredWrapperCount}/{report.Summary.PrimaryWrapperCount} thunks={report.Summary.MatchingThunkWrapperCount}/{report.Summary.ThunkWrapperCount} ready={report.Summary.NativeWrapperVariantCoverageReady}");
        break;
    }
    case "reduce-tf2ps3-source-category5-usercmd-handler":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var tfElf = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var clcMoveContext = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-context.json");
        var clcMoveContract = args.Length > 4
            ? args[4]
            : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-contract.json");
        var serverDllUsercmdDecoder = args.Length > 5
            ? args[5]
            : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-decoder.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-category5-usercmd-handler-map.json");
        var ghidraXrefs = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-category5-ghidra-xrefs.json");
        var report = await Tf2Ps3SourceCategory5UsercmdHandlerReducer.ReduceAsync(cExport, tfElf, clcMoveContext, clcMoveContract, serverDllUsercmdDecoder, output, ghidraXrefs);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"handlerSlot={report.Summary.HandlerTableCellAddress} wrapper={report.Summary.HandlerWrapperOpdAddress} callConvention={report.Summary.HandlerCallConventionMatchesDispatchObject} ghidraChain={report.Summary.GhidraHandlerChainConfirmed} vptrWrite={report.Summary.ExactCategory5VptrWriteRecovered} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-usercmd-queue-record":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var pcapBoundaryProbe = args.Length > 2
            ? args[2]
            : Path.Combine(repoRoot, "artifacts/pcap-source-client-boundary-probes.json");
        var output = args.Length > 3
            ? args[3]
            : Path.Combine(repoRoot, "re/tf2ps3/source-usercmd-queue-record-map.json");
        var serverDllUsercmdDecoder = args.Length > 4
            ? args[4]
            : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-decoder.json");
        var pcapRecordCandidates = args.Length > 5
            ? args[5]
            : Path.Combine(repoRoot, "artifacts/pcap-source-usercmd-record-candidates.json");
        var tfElfBinary = args.Length > 6
            ? args[6]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var report = await Tf2Ps3SourceUsercmdQueueRecordReducer.ReduceAsync(
            cExport,
            pcapBoundaryProbe,
            output,
            serverDllUsercmdDecoder,
            pcapRecordCandidates,
            tfElfBinary);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"queueRecord={report.Summary.QueueRecordLayerRecovered} fields={report.Summary.DeltaFieldCount} serverDllCandidates={report.Summary.CandidateServerDllFieldMatchCount} descriptorRefs={report.Summary.QueueInsertDescriptorReferenceCount} queueDeltaExact={report.Summary.TfElfQueueDeltaExactZeroTrailingPacketCount} rawRecordCandidates={report.Summary.PcapRawRecordLengthCandidateCount} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-markerless-receive-classifier":
    {
        var opaqueWrapperReport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/pcap-opaque-markerless-command-wrapper.json");
        var receivePathReport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-path-map.json");
        var nativeMessageContract = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var clcMoveContract = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-contract.json");
        var netchanRegistrationSetup = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-registration-setup-map.json");
        var requiredHandlerConstructor = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-function-map.json");
        var output = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-receive-classifier.json");
        var report = await Tf2Ps3SourceMarkerlessReceiveClassifierReducer.ReduceAsync(
            opaqueWrapperReport,
            receivePathReport,
            nativeMessageContract,
            clcMoveContract,
            netchanRegistrationSetup,
            requiredHandlerConstructor,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"opaquePackets={report.Summary.OpaquePacketCount} markerlessTransformProven={report.Summary.MarkerlessTransformProven} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-markerless-transform-candidates":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var classifier = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-receive-classifier.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-transform-candidates.json");
        var report = await Tf2Ps3SourceMarkerlessTransformCandidateReducer.ReduceAsync(cExport, classifier, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"best={report.Summary.BestTransformCandidateAddress} candidateRecovered={report.Summary.BitreaderTransformCandidateRecovered} ingressProven={report.Summary.DirectIngressLinkProven} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-client-receive-contract":
    {
        var receivePath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-path-map.json");
        var helperSlice = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var classifier = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-receive-classifier.json");
        var transform = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-transform-candidates.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-client-receive-contract.json");
        var report = await Tf2Ps3SourceClientReceiveContractReducer.ReduceAsync(
            receivePath,
            helperSlice,
            classifier,
            transform,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"acceptedPeer={report.Summary.AcceptedPeerAttachProven} attachedType2={report.Summary.AttachedType2FrameDispatchProven} markerlessIngress={report.Summary.DirectMarkerlessIngressProven} nativeReady={report.Summary.NativeInputServerReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-receive-dispatch-slots":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var objectLifecycle = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-object-lifecycle-map.json");
        var clientReceiveContract = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-client-receive-contract.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-dispatch-slots.json");
        var report = await Tf2Ps3SourceReceiveDispatchSlotReducer.ReduceAsync(
            cExport,
            objectLifecycle,
            clientReceiveContract,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"slot6cAttach={report.Summary.Slot6cAcceptedPeerAttachProven} slot68Setup={report.Summary.Slot68SourceSetupCandidateRecovered} slot70Setup={report.Summary.Slot70SourceSetupCandidateRecovered} markerlessIngress={report.Summary.DirectMarkerlessIngressSlot70Proven} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-slot70-callsite-census":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-callsite-census.json");
        var report = await Tf2Ps3SourceSlot70CallsiteCensusReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"slot70={report.Summary.ExactSlot70CallsiteFunctionCount} networkOrSocket={report.Summary.NetworkOrSocketSlot70CallsiteCount} payloadDispatch={report.Summary.PayloadDispatcherSlot70CallsiteCount} directMarkerless={report.Summary.DirectMarkerlessIngressCandidateCount} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-markerless-dataflow-targets":
    {
        var helperSlice = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var ownerCallback = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-map.json");
        var ownerVtable = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-vtable-map.json");
        var registrationCallsite = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-callsite-map.json");
        var registrationBinary = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-binary-reference-map.json");
        var requiredHandlerToc = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-function-map.json");
        var slot70Census = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-callsite-census.json");
        var slot70Builder = args.Length > 8 ? args[8] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-builder.json");
        var slot70Field = args.Length > 9 ? args[9] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-field-contract.json");
        var bitstreamHelper = args.Length > 10 ? args[10] : Path.Combine(repoRoot, "re/tf2ps3/source-bitstream-helper-contract.json");
        var output = args.Length > 11 ? args[11] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-dataflow-targets.json");
        var report = await Tf2Ps3SourceMarkerlessDataflowTargetReducer.ReduceAsync(
            helperSlice,
            ownerCallback,
            ownerVtable,
            registrationCallsite,
            registrationBinary,
            requiredHandlerToc,
            slot70Census,
            slot70Builder,
            slot70Field,
            bitstreamHelper,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"targets={report.Summary.RankedTargetCount} exactSlot70RuledOut={report.Summary.ExactVisibleSlot70IngressRuledOut} edgeRecovered={report.Summary.ConcreteMarkerlessDataflowEdgeRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-udp-ingress-correction":
    {
        var receivePath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-path-map.json");
        var networkAnchors = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-network-anchor-map.json");
        var helperSlice = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var dispatchSlots = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-dispatch-slots.json");
        var instructionContext = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-instruction-context.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-udp-ingress-correction.json");
        var report = await Tf2Ps3SourceUdpIngressCorrectionReducer.ReduceAsync(
            receivePath,
            networkAnchors,
            helperSlice,
            dispatchSlots,
            instructionContext,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"drainDiscard={report.Summary.UdpDrainDiscardPathProven} connectedOpen={report.Summary.ConnectedSocketOpenStorePathProven} attachedReader={report.Summary.ConnectedSocketAttachedReaderPathProven} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-connected-wrapper-boundary":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var markerlessBoundary = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-markerless-boundary-hypotheses.json");
        var udpIngress = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-udp-ingress-correction.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-connected-wrapper-boundary.json");
        var report = await Tf2Ps3SourceConnectedWrapperBoundaryReducer.ReduceAsync(
            cExport,
            markerlessBoundary,
            udpIngress,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"connectedPath={report.Summary.ConnectedSocketObjectPathProven} attachedFrame={report.Summary.AttachedFrameReaderContractProven} siblingBitreader={report.Summary.SiblingBitreaderWrapperRecovered} hardBoundary={report.Summary.DirectHardOpaqueBoundaryProven} nativeReady={report.Summary.NativeMarkerlessBoundaryReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-payload-dispatch-boundary":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var playerVtable = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-player-vtable-map.json");
        var connectedWrapper = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-connected-wrapper-boundary.json");
        var markerlessBoundary = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "artifacts/pcap-markerless-boundary-hypotheses.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-dispatch-boundary.json");
        var report = await Tf2Ps3SourcePayloadDispatchBoundaryReducer.ReduceAsync(
            cExport,
            playerVtable,
            connectedWrapper,
            markerlessBoundary,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"dispatchers={report.Summary.DirectPayloadDispatcherCallerCount} directD9e0Callers={report.Summary.DirectBitreaderTransformCallerCount} slot70={report.Summary.Slot70FunctionOffset} param2Builder={report.Summary.Slot70Param2ConstructionSiteRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-slot70-param2-builder":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var helperSlice = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var helperSliceOpdRefs = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-opd-refs.json");
        var receiveSlots = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-receive-dispatch-slots.json");
        var payloadBoundary = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-dispatch-boundary.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-builder.json");
        var report = await Tf2Ps3SourceSlot70Param2BuilderReducer.ReduceAsync(
            cExport,
            helperSlice,
            helperSliceOpdRefs,
            receiveSlots,
            payloadBoundary,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"slot70={report.Summary.Slot70FunctionAddress} opdRefs={report.Summary.DirectOpdReferenceCount} ordinaryCallers={report.Summary.DirectOrdinaryCallerCount} setupCallsites={report.Summary.SetupOnlySourceRelevantSlot70CallsiteCount}/{report.Summary.SourceRelevantSlot70CallsiteCount} param2Builder={report.Summary.Param2ConstructionSiteRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-slot70-param2-field-contract":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var slot70Param2Builder = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-builder.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-field-contract.json");
        var report = await Tf2Ps3SourceSlot70Param2FieldContractReducer.ReduceAsync(
            cExport,
            slot70Param2Builder,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"fields={report.Summary.RecoveredFieldCount}/{report.Summary.FieldCount} operations={report.Summary.RecoveredOperationCount}/{report.Summary.OperationCount} evidenceMissing={report.Summary.RequiredEvidenceMissingCount} callerBuilder={report.Summary.CallerSideParam2BuilderRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-bitstream-helper-contract":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var slot70Param2FieldContract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-field-contract.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-bitstream-helper-contract.json");
        var report = await Tf2Ps3SourceBitstreamHelperContractReducer.ReduceAsync(
            cExport,
            slot70Param2FieldContract,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"primitives={report.Summary.RecoveredPrimitiveCount}/{report.Summary.PrimitiveCount} addressState={report.Summary.RecoveredAddressStateHelperCount}/{report.Summary.AddressStateHelperCount} wrappers={report.Summary.RecoveredWrapperLinkCount}/{report.Summary.WrapperLinkCount} callerBuilder={report.Summary.CallerSideParam2BuilderRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-handler-registrations":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-registration-map.json");
        await Tf2Ps3SourceHandlerRegistrationReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-player-vtable":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-player-vtable-map.json");
        await Tf2Ps3SourcePlayerVtableReducer.ReduceAsync(elf, cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-handler-vtable-candidates":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-vtable-candidates.json");
        await Tf2Ps3SourceHandlerVtableCandidateReducer.ReduceAsync(elf, cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-handler-registration-proof":
    {
        var candidates = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-vtable-candidates.json");
        var refs = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-vtable-candidate-refs.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-registration-proof-map.json");
        await Tf2Ps3SourceHandlerRegistrationProofReducer.ReduceAsync(candidates, refs, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-handler-candidate-toc-access":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var candidates = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-vtable-candidates.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-candidate-toc-access.json");
        await Tf2Ps3SourceHandlerCandidateTocAccessReducer.ReduceAsync(elf, candidates, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-registration-callsites":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-callsite-map.json");
        await Tf2Ps3SourceRegistrationCallsiteReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-object-lifecycle":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-object-lifecycle-map.json");
        await Tf2Ps3SourceObjectLifecycleReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-installed-object-vtable":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-installed-object-vtable-map.json");
        await Tf2Ps3SourceInstalledObjectVtableReducer.ReduceAsync(elf, cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-object-vtable-lifecycle":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var refs = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-object-lifecycle-ghidra-refs.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-object-vtable-lifecycle-map.json");
        await Tf2Ps3SourceObjectVtableLifecycleReducer.ReduceAsync(cExport, refs, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-registration-binary-refs":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var candidates = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-vtable-candidates.json");
        var refs = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-binary-reference-ghidra-refs.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-binary-reference-map.json");
        await Tf2Ps3SourceRegistrationBinaryReferenceReducer.ReduceAsync(elf, candidates, refs, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-owner-callback":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-map.json");
        await Tf2Ps3SourceOwnerCallbackReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-owner-callback-dispatch":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var ownerCallback = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-map.json");
        var slot70Field = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-field-contract.json");
        var markerlessDataflow = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-dataflow-targets.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-dispatch-map.json");
        var report = await Tf2Ps3SourceOwnerCallbackDispatchReducer.ReduceAsync(
            cExport,
            ownerCallback,
            slot70Field,
            markerlessDataflow,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"entries={report.Summary.DispatchEntryCount} ownerSlots={report.Summary.OwnerCallbackSlotCount} slot70Sequence={report.Summary.Slot70WrapperSequenceRecovered} ownerBuildsParam2={report.Summary.OwnerCallbackPathConstructsParam2} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-helper-slice-receive-siblings":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var helperSlice = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var slot70Builder = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-builder.json");
        var ownerDispatch = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-dispatch-map.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-receive-siblings.json");
        var report = await Tf2Ps3SourceHelperSliceReceiveSiblingReducer.ReduceAsync(
            cExport,
            helperSlice,
            slot70Builder,
            ownerDispatch,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"siblings={report.Summary.PresentReceiveSiblingCount}/{report.Summary.ReceiveSiblingCount} attachedType2={report.Summary.AttachedType2DispatchProven} slot70ReadsSocket={report.Summary.Slot70ReadsAttachedSocket} siblingBuildsParam2={report.Summary.SiblingPathConstructsParam2} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-recv-bitreader-census":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var connectedWrapper = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-connected-wrapper-boundary.json");
        var helperSiblings = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-receive-siblings.json");
        var markerlessBoundary = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "artifacts/pcap-markerless-boundary-hypotheses.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-recv-bitreader-census.json");
        var report = await Tf2Ps3SourceRecvBitreaderCensusReducer.ReduceAsync(
            cExport,
            connectedWrapper,
            helperSiblings,
            markerlessBoundary,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"connectedRecv={report.Summary.ConnectedRecvWrapperCallerCount} connectedRecvDispatch={report.Summary.ConnectedRecvAndSourceDispatcherFunctionCount} markerlessCandidates={report.Summary.HardMarkerlessRecvBitreaderCandidateCount} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-raw-udp-control-probe":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var pcapInput = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/pcaps");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-raw-udp-control-probe.json");
        var report = await Tf2Ps3SourceRawUdpControlProbeReducer.ReduceAsync(
            cExport,
            pcapInput,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"probeRecovered={report.Summary.RawUdpControlProbeRecovered} pcap6e01={report.Summary.Pcap6e01ControlProbeCount} excludedFromMarkerless={report.Summary.ExcludedFromHardMarkerlessSourceInputBoundary} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-embedded-clc-move-proof":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var embeddedCandidates = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-embedded-clc-move-candidates.json");
        var ownerDispatch = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-dispatch-map.json");
        var helperSiblings = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-receive-siblings.json");
        var recvCensus = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-recv-bitreader-census.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-embedded-clc-move-proof-worklist.json");
        var report = await Tf2Ps3SourceEmbeddedClcMoveProofReducer.ReduceAsync(
            cExport,
            embeddedCandidates,
            ownerDispatch,
            helperSiblings,
            recvCensus,
            output);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"embedded={report.Summary.EmbeddedCandidateCount} hard={report.Summary.HardMarkerlessCandidateCount} exact={report.Summary.ExactBoundaryCandidateCount} nativeReady={report.Summary.NativeBoundaryReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-markerless-param2-builder":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var recvCensus = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-recv-bitreader-census.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-param2-builder.json");
        var implementationRoot = args.Length > 4 ? args[4] : repoRoot;
        var report = await Tf2Ps3SourceMarkerlessParam2BuilderReducer.ReduceAsync(
            cExport,
            recvCensus,
            output,
            implementationRoot);
        Console.WriteLine($"updated {output}");
        Console.WriteLine($"builder={report.Summary.PayloadObjectBuilderRecovered} recvFill={report.Summary.MarkerlessRecvFillRecovered} fragment={report.Summary.FragmentReassemblyRecovered} drain={report.Summary.DrainDispatchLoopRecovered} boundary={report.Summary.ConcretePayloadObjectBoundaryRecovered} nativeReady={report.Summary.NativeSourceInputReady} openGates={report.Summary.OpenGateCount}");
        break;
    }
    case "reduce-tf2ps3-source-owner-vtables":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var refs = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-opd-reference-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-vtable-map.json");
        await Tf2Ps3SourceOwnerVtableReducer.ReduceAsync(elf, cExport, refs, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-helper-slice-contract":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        await Tf2Ps3SourceHelperSliceContractReducer.ReduceAsync(elf, cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-netchan-static-anchor":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-static-anchor.json");
        await Tf2Ps3SourceNetchanStaticAnchorReducer.ReduceAsync(elf, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-netchan-source-crossmap":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var sourceEngineRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/source-engine-master");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-source-crossmap.json");
        await Tf2Ps3SourceNetchanSourceCrossmapReducer.ReduceAsync(cExport, sourceEngineRoot, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-message-string-catalog":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-message-string-catalog.json");
        await Tf2Ps3SourceMessageStringCatalogReducer.ReduceAsync(elf, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-message-vtable-catalog":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var catalog = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-message-string-catalog.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-message-vtable-catalog.json");
        await Tf2Ps3SourceMessageVtableCatalogReducer.ReduceAsync(elf, catalog, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-critical-message-io-contract":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var sourceEngineRoot = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/source-engine-master");
        var vtableCatalog = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-message-vtable-catalog.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-message-io-contract.json");
        await Tf2Ps3SourceCriticalMessageIoContractReducer.ReduceAsync(cExport, sourceEngineRoot, vtableCatalog, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-critical-bootstrap-route":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var criticalContract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-message-io-contract.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-bootstrap-route-map.json");
        await Tf2Ps3SourceCriticalBootstrapRouteReducer.ReduceAsync(cExport, criticalContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-object-stream-bootstrap":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var criticalRoute = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-bootstrap-route-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-object-stream-bootstrap-map.json");
        await Tf2Ps3SourceObjectStreamBootstrapReducer.ReduceAsync(cExport, criticalRoute, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-bootstrap-control-messages":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var vtableCatalog = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-message-vtable-catalog.json");
        var objectStreamBootstrap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-object-stream-bootstrap-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-bootstrap-control-message-map.json");
        await Tf2Ps3SourceBootstrapControlMessageReducer.ReduceAsync(cExport, vtableCatalog, objectStreamBootstrap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-packet-entities-placement":
    {
        var criticalRoute = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-bootstrap-route-map.json");
        var snapshotPath = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-snapshot-path-map.json");
        var snapshotDelta = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-snapshot-delta-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-packet-entities-placement-map.json");
        await Tf2Ps3SourcePacketEntitiesPlacementReducer.ReduceAsync(criticalRoute, snapshotPath, snapshotDelta, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-native-message-contract":
    {
        var vtableCatalog = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-message-vtable-catalog.json");
        var criticalIo = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-message-io-contract.json");
        var clcMove = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-clc-move-contract.json");
        var bootstrapControl = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-bootstrap-control-message-map.json");
        var packetEntitiesPlacement = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-packet-entities-placement-map.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        await Tf2Ps3SourceNativeMessageContractReducer.ReduceAsync(vtableCatalog, criticalIo, clcMove, bootstrapControl, packetEntitiesPlacement, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-native-source-lifecycle":
    {
        var tfElf = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var officialServerDll = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/EATF2ServerDLL/server.dll");
        var nativeMessageContract = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var packetEntitiesPlacement = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-packet-entities-placement-map.json");
        var loadingReplacementPlan = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-replacement-plan.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/native-source-lifecycle-contract.json");
        await Tf2Ps3NativeSourceLifecycleReducer.ReduceAsync(
            tfElf,
            officialServerDll,
            nativeMessageContract,
            packetEntitiesPlacement,
            loadingReplacementPlan,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-server-replacement-contract":
    {
        var nativeLifecycle = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/native-source-lifecycle-contract.json");
        var serverDllRuntime = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-runtime-contract.json");
        var serverDllObligations = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-native-obligations.json");
        var nativeMessageContract = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var objectStreamBootstrap = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-object-stream-bootstrap-map.json");
        var queuedPrefixContract = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        var loadingReplacementPlan = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-replacement-plan.json");
        var responderSource = args.Length > 8 ? args[8] : Path.Combine(repoRoot, "src/PlasmaGameManager.Server/Ps3NativeSourceResponder.cs");
        var output = args.Length > 9 ? args[9] : Path.Combine(repoRoot, "re/tf2ps3/source-server-replacement-contract.json");
        var generatedPrefixRetailCrossmap = args.Length > 10 ? args[10] : Path.Combine(repoRoot, "re/tf2ps3/source-generated-prefix-retail-crossmap.json");
        var userCmdPhysicsAudit = args.Length > 11 ? args[11] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-usercmd-physics-audit.json");
        var sourceSendCallsiteMap = args.Length > 12 ? args[12] : Path.Combine(repoRoot, "re/tf2ps3/source-send-callsite-map.json");
        await Tf2Ps3SourceServerReplacementContractReducer.ReduceAsync(
            nativeLifecycle,
            serverDllRuntime,
            serverDllObligations,
            nativeMessageContract,
            objectStreamBootstrap,
            queuedPrefixContract,
            loadingReplacementPlan,
            responderSource,
            output,
            generatedPrefixRetailCrossmap,
            userCmdPhysicsAudit,
            sourceSendCallsiteMap);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-queued-peer-targets":
    {
        var peerChannelMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-peer-channel-map.json");
        var criticalRoute = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-bootstrap-route-map.json");
        var objectStreamBootstrap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-object-stream-bootstrap-map.json");
        var queuedOpaqueReport = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-opaque.json");
        var tunnelGhidraMap = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-ghidra-map.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-peer-target-map.json");
        await Tf2Ps3SourceQueuedPeerTargetReducer.ReduceAsync(peerChannelMap, criticalRoute, objectStreamBootstrap, queuedOpaqueReport, tunnelGhidraMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-native-template-debt":
    {
        var responderSource = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "src/PlasmaGameManager.Server/Ps3NativeSourceResponder.cs");
        var queuedPeerTargetMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-peer-target-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-native-template-debt.json");
        await Tf2Ps3SourceNativeTemplateDebtReducer.ReduceAsync(responderSource, queuedPeerTargetMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-template-patch-layout":
    {
        var responderSource = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "src/PlasmaGameManager.Server/Ps3NativeSourceResponder.cs");
        var nativeTemplateDebt = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-native-template-debt.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-template-patch-layout.json");
        await Tf2Ps3SourceTemplatePatchLayoutReducer.ReduceAsync(responderSource, nativeTemplateDebt, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-state-link-grammar":
    {
        var templatePatchLayout = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-template-patch-layout.json");
        var embeddedObjects = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-embedded-objects.json");
        var queuedPeerOpaque = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-opaque.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-state-link-grammar.json");
        await Tf2Ps3SourceStateLinkGrammarReducer.ReduceAsync(templatePatchLayout, embeddedObjects, queuedPeerOpaque, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-embedded-object-grammar":
    {
        var embeddedObjects = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/pcap-source-embedded-objects.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-embedded-object-grammar.json");
        await Tf2Ps3SourceEmbeddedObjectGrammarReducer.ReduceAsync(embeddedObjects, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-queued-prefix-contract":
    {
        var queuedPeerTargetMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-peer-target-map.json");
        var queuedOpaqueReport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-opaque.json");
        var templatePatchLayout = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-template-patch-layout.json");
        var stateLinkGrammar = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-state-link-grammar.json");
        var embeddedObjectGrammar = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-embedded-object-grammar.json");
        var tunnelGhidraMap = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-ghidra-map.json");
        var output = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        await Tf2Ps3SourceQueuedPrefixContractReducer.ReduceAsync(
            queuedPeerTargetMap,
            queuedOpaqueReport,
            templatePatchLayout,
            stateLinkGrammar,
            embeddedObjectGrammar,
            tunnelGhidraMap,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-generated-prefix-retail-crossmap":
    {
        var queuedPrefixContract = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        var semanticTraceDirectory = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "docs");
        var queuedOpaqueReport = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-source-queued-peer-opaque.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-generated-prefix-retail-crossmap.json");
        var sourceLoadingFrameDebt = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-frame-debt.json");
        await Tf2Ps3SourceGeneratedPrefixRetailCrossmapReducer.ReduceAsync(
            queuedPrefixContract,
            semanticTraceDirectory,
            queuedOpaqueReport,
            output,
            sourceLoadingFrameDebt);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-generated-prefix-field-probe":
    {
        var generatedPrefixRetailCrossmap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-generated-prefix-retail-crossmap.json");
        var semanticTraceDirectory = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "docs");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-generated-prefix-field-probe.json");
        await Tf2Ps3SourceGeneratedPrefixFieldProbeReducer.ReduceAsync(
            generatedPrefixRetailCrossmap,
            semanticTraceDirectory,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-native-debt-priority":
    {
        var nativeTemplateDebt = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-native-template-debt.json");
        var templatePatchLayout = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-template-patch-layout.json");
        var queuedPrefixContract = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-native-debt-priority.json");
        await Tf2Ps3SourceNativeDebtPriorityReducer.ReduceAsync(
            nativeTemplateDebt,
            templatePatchLayout,
            queuedPrefixContract,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-loading-frame-debt":
    {
        var responderSource = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "src/PlasmaGameManager.Server/Ps3NativeSourceResponder.cs");
        var nativeTemplateDebt = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-native-template-debt.json");
        var queuedPrefixContract = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        var tunnelGhidraMap = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-tunnel-ghidra-map.json");
        var output = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-frame-debt.json");
        await Tf2Ps3SourceLoadingFrameDebtReducer.ReduceAsync(
            responderSource,
            nativeTemplateDebt,
            queuedPrefixContract,
            tunnelGhidraMap,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-loading-replacement-plan":
    {
        var loadingFrameDebt = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-frame-debt.json");
        var queuedPrefixContract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-prefix-contract.json");
        var queuedPeerTargetMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-queued-peer-target-map.json");
        var criticalMessageIoContract = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-critical-message-io-contract.json");
        var objectStreamBootstrapMap = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-object-stream-bootstrap-map.json");
        var snapshotPathMap = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-snapshot-path-map.json");
        var nativeMessageContract = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var output = args.Length > 8 ? args[8] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-replacement-plan.json");
        await Tf2Ps3SourceLoadingReplacementPlanReducer.ReduceAsync(
            loadingFrameDebt,
            queuedPrefixContract,
            queuedPeerTargetMap,
            criticalMessageIoContract,
            objectStreamBootstrapMap,
            snapshotPathMap,
            nativeMessageContract,
            output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-netchan-registration-setup":
    {
        var netchanCrossmap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-source-crossmap.json");
        var helperSlice = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-contract.json");
        var registrationCallsite = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-callsite-map.json");
        var objectLifecycle = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-object-lifecycle-map.json");
        var objectVtableLifecycle = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-object-vtable-lifecycle-map.json");
        var ownerVtable = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-vtable-map.json");
        var handlerRegistration = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-registration-map.json");
        var handlerRegistrationProof = args.Length > 8 ? args[8] : Path.Combine(repoRoot, "re/tf2ps3/source-handler-registration-proof-map.json");
        var binaryReference = args.Length > 9 ? args[9] : Path.Combine(repoRoot, "re/tf2ps3/source-registration-binary-reference-map.json");
        var nativeMessageContract = args.Length > 10 ? args[10] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var output = args.Length > 11 ? args[11] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-registration-setup-map.json");
        await Tf2Ps3SourceNetchanRegistrationSetupReducer.ReduceAsync(netchanCrossmap, helperSlice, registrationCallsite, objectLifecycle, objectVtableLifecycle, ownerVtable, handlerRegistration, handlerRegistrationProof, binaryReference, nativeMessageContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-required-client-read-contract":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var nativeMessageContract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-native-message-contract.json");
        var netchanRegistrationSetup = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-registration-setup-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-required-client-read-contract.json");
        await Tf2Ps3SourceRequiredClientReadContractReducer.ReduceAsync(cExport, nativeMessageContract, netchanRegistrationSetup, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-required-handler-constructor-probe":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var cExport = args.Length > 2 ? args[2] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var messageVtableCatalog = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-message-vtable-catalog.json");
        var netchanRegistrationSetup = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-netchan-registration-setup-map.json");
        var requiredReadContract = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/source-required-client-read-contract.json");
        var output = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-constructor-probe.json");
        await Tf2Ps3SourceRequiredHandlerConstructorProbeReducer.ReduceAsync(elf, cExport, messageVtableCatalog, netchanRegistrationSetup, requiredReadContract, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-required-handler-table-neighborhood":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var constructorProbe = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-constructor-probe.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-neighborhood.json");
        await Tf2Ps3SourceRequiredHandlerTableNeighborhoodReducer.ReduceAsync(elf, constructorProbe, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-required-handler-table-toc-access":
    {
        var elf = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var tableNeighborhood = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-neighborhood.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-access.json");
        await Tf2Ps3SourceRequiredHandlerTableTocAccessReducer.ReduceAsync(elf, tableNeighborhood, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-required-handler-table-toc-functions":
    {
        var tocAccess = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-access.json");
        var instructionContext = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-instruction-context.json");
        var focusedFunctions = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-focused-functions.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/source-required-handler-table-toc-function-map.json");
        await Tf2Ps3SourceRequiredHandlerTableTocFunctionReducer.ReduceAsync(tocAccess, instructionContext, focusedFunctions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-virtual-slot44-scan":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-virtual-slot44-scan.json");
        await Tf2Ps3SourceVirtualSlot44ScanReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-acceptance-gates":
    {
        var bfbc2Dispatcher = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/dispatcher-table.json");
        var tf2Dispatcher = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        var pcapCorpus = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/pcap-corpus-coverage.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/native-acceptance-gates.json");
        var liveHandoffEvidence = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "artifacts/live-handoff-evidence.json");
        var sourceBridgeContract = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "artifacts/pcap-source-bridge-contract.json");
        var pcapEaText = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "artifacts/pcap-ea-text-summary.json");
        var sourceGameplayPhases = args.Length > 8 ? args[8] : Path.Combine(repoRoot, "artifacts/pcap-source-gameplay-phases.json");
        var gameManagerHello = args.Length > 9 ? args[9] : Path.Combine(repoRoot, "artifacts/pcap-gamemanager-hello.json");
        var serverDllSimulation = args.Length > 10 ? args[10] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-simulation-contract.json");
        var serverDllNativeObligations = args.Length > 11 ? args[11] : Path.Combine(repoRoot, "re/tf2ps3/eatf2-serverdll-native-obligations.json");
        var sourceLoadingFrameDebt = args.Length > 12 ? args[12] : Path.Combine(repoRoot, "re/tf2ps3/source-loading-frame-debt.json");
        var sourceConnectedWrapperBoundary = args.Length > 13 ? args[13] : Path.Combine(repoRoot, "re/tf2ps3/source-connected-wrapper-boundary.json");
        var sourceSlot70Param2Builder = args.Length > 14 ? args[14] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-builder.json");
        var sourceSlot70Param2FieldContract = args.Length > 15 ? args[15] : Path.Combine(repoRoot, "re/tf2ps3/source-slot70-param2-field-contract.json");
        var sourceBitstreamHelperContract = args.Length > 16 ? args[16] : Path.Combine(repoRoot, "re/tf2ps3/source-bitstream-helper-contract.json");
        var sourceMarkerlessParam2Builder = args.Length > 17 ? args[17] : Path.Combine(repoRoot, "re/tf2ps3/source-markerless-param2-builder.json");
        var sourcePayloadObjectDispatch = args.Length > 18 ? args[18] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-object-dispatch-map.json");
        var sourceOwnerControlSubobject = args.Length > 19 ? args[19] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-control-subobject-map.json");
        var sourceOwnerForwardContext = args.Length > 20 ? args[20] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-context-map.json");
        var sourceOwnerForwarderBitstreamCoverage = args.Length > 21 ? args[21] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forwarder-bitstream-coverage.json");
        var sourceOwnerForwardWrapperVariants = args.Length > 22 ? args[22] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-forward-wrapper-variants.json");
        var sourceCategory5UsercmdHandler = args.Length > 23 ? args[23] : Path.Combine(repoRoot, "re/tf2ps3/source-category5-usercmd-handler-map.json");
        var sourceClientInputCoverage = args.Length > 24 ? args[24] : Path.Combine(repoRoot, "artifacts/pcap-source-client-input-coverage.json");
        var sourceClientBoundaryProbe = args.Length > 25 ? args[25] : Path.Combine(repoRoot, "artifacts/pcap-source-client-boundary-probes.json");
        var sourceMarkerlessTransformProbe = args.Length > 26 ? args[26] : Path.Combine(repoRoot, "artifacts/pcap-markerless-transform-probes-strict.json");
        var sourceUsercmdQueueRecord = args.Length > 27 ? args[27] : Path.Combine(repoRoot, "re/tf2ps3/source-usercmd-queue-record-map.json");
        var sourceOwnerCallbackDispatch = args.Length > 28 ? args[28] : Path.Combine(repoRoot, "re/tf2ps3/source-owner-callback-dispatch-map.json");
        var sourceHelperSliceReceiveSiblings = args.Length > 29 ? args[29] : Path.Combine(repoRoot, "re/tf2ps3/source-helper-slice-receive-siblings.json");
        var sourceRecvBitreaderCensus = args.Length > 30 ? args[30] : Path.Combine(repoRoot, "re/tf2ps3/source-recv-bitreader-census.json");
        var sourceEmbeddedClcMoveCandidates = args.Length > 31 ? args[31] : Path.Combine(repoRoot, "artifacts/pcap-embedded-clc-move-candidates.json");
        var sourceMarkerlessSessionKeyProbe = args.Length > 32 ? args[32] : Path.Combine(repoRoot, "artifacts/pcap-markerless-session-key-probes.json");
        var sourceContentValidation = args.Length > 33 ? args[33] : Path.Combine(repoRoot, "artifacts/tf2ps3-source-content-validation.json");
        var sourceUsercmdQueueDeltaTail = args.Length > 34 ? args[34] : Path.Combine(repoRoot, "artifacts/pcap-source-usercmd-queue-delta-tail.json");
        var sourceRawUdpControlProbe = args.Length > 35 ? args[35] : Path.Combine(repoRoot, "re/tf2ps3/source-raw-udp-control-probe.json");
        var sourcePayloadObjectFirstWord = args.Length > 36 ? args[36] : Path.Combine(repoRoot, "artifacts/pcap-source-payload-object-first-word.json");
        var sourceNativeAssociationDescriptorScan = args.Length > 37 ? args[37] : Path.Combine(repoRoot, "artifacts/pcap-source-native-association-descriptors.json");
        var sourceAssociatedObjectTokenContract = args.Length > 38 ? args[38] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-token-contract.json");
        var pcapSourceAssociatedObjectTokens = args.Length > 39 ? args[39] : Path.Combine(repoRoot, "artifacts/pcap-source-associated-object-tokens.json");
        var sourceAssociatedObjectSlot90 = args.Length > 40 ? args[40] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-object-slot90-map.json");
        var associatedObjectTokenTransformProbe = args.Length > 41 ? args[41] : Path.Combine(repoRoot, "artifacts/pcap-associated-object-token-transform-probes.json");
        var sourceReliablePeerAttach = args.Length > 42 ? args[42] : Path.Combine(repoRoot, "re/tf2ps3/source-reliable-peer-attach.json");
        var sourceAssociatedSlotAcProvenance = args.Length > 43 ? args[43] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-slotac-provenance.json");
        var sourceAssociatedLaneRole = args.Length > 44 ? args[44] : Path.Combine(repoRoot, "re/tf2ps3/source-associated-lane-role.json");
        await NativeAcceptanceGateReducer.ReduceAsync(
            bfbc2Dispatcher,
            tf2Dispatcher,
            pcapCorpus,
            output,
            liveHandoffEvidence,
            sourceBridgeContract,
            pcapEaText,
            sourceGameplayPhases,
            gameManagerHello,
            serverDllSimulation,
            serverDllNativeObligations,
            sourceLoadingFrameDebt,
            sourceConnectedWrapperBoundary,
            sourceSlot70Param2Builder,
            sourceSlot70Param2FieldContract,
            sourceBitstreamHelperContract,
            sourceMarkerlessParam2Builder,
            sourcePayloadObjectDispatch,
            sourceOwnerControlSubobject,
            sourceOwnerForwardContext,
            sourceOwnerForwarderBitstreamCoverage,
            sourceOwnerForwardWrapperVariants,
            sourceCategory5UsercmdHandler,
            sourceClientInputCoverage,
            sourceClientBoundaryProbe,
            sourceMarkerlessTransformProbe,
            sourceUsercmdQueueRecord,
            sourceOwnerCallbackDispatch,
            sourceHelperSliceReceiveSiblings,
            sourceRecvBitreaderCensus,
            sourceEmbeddedClcMoveCandidates,
            sourceMarkerlessSessionKeyProbe,
            sourceContentValidation,
            sourceUsercmdQueueDeltaTail,
            sourceRawUdpControlProbe,
            sourcePayloadObjectFirstWord,
            sourceNativeAssociationDescriptorScan,
            sourceAssociatedObjectTokenContract,
            pcapSourceAssociatedObjectTokens,
            sourceAssociatedObjectSlot90,
            associatedObjectTokenTransformProbe,
            sourceReliablePeerAttach,
            sourceAssociatedSlotAcProvenance,
            sourceAssociatedLaneRole);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "rebuild-native-reports":
    {
        var output = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/native-report-pipeline.json");
        var continueOnFailure = args.Any(static arg => arg == "--continue-on-failure");
        await NativeReportPipeline.RunAsync(repoRoot, output, continueOnFailure);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "analyze-live-handoff":
    {
        var eventLog = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "artifacts/live-gamemanager-events.jsonl");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/live-handoff-evidence.json");
        var sourceEvidence = args.Skip(3).ToArray();
        await new LiveHandoffEvidenceAnalyzer().AnalyzeAsync(eventLog, output, sourceEvidence);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "match-live-source-turns":
    {
        var eventLog = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "logs/gamemanager-events.jsonl");
        var contract = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/pcap-source-turn-contract.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/live-source-turn-contract-match.json");
        await new LiveSourceTurnContractMatcher().MatchAsync(eventLog, contract, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "compare-live-frozen-state-retail":
    {
        var eventLog = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "logs/gamemanager-events.jsonl");
        var retail = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "docs/pcap-frozen-state-turns.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "artifacts/live-frozen-state-retail-comparison.json");
        var report = await new LiveFrozenStateRetailComparator().CompareAsync(eventLog, retail, output);
        Console.WriteLine($"wrote {output}");
        Console.WriteLine($"liveTurns={report.LiveFrozenStateTurnCount} retailTurns={report.RetailFrozenStateTurnCount} exactLengthPatterns={report.ExactLengthPatternMatchCount} status={report.GateStatus}");
        break;
    }
    case "run-source-replay-backend":
    {
        var pcap = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps/TF2_PS3_network_traffic/packets/server");
        var bind = IPAddress.Parse(args.Length > 2 ? args[2] : "127.0.0.1");
        var port = args.Length > 3 ? int.Parse(args[3]) : 27016;
        var evidenceLog = args.Length > 4 ? args[4] : "";
        var matchMode = args.Length > 5
            ? ParseReplayMatchMode(args[5])
            : ParseReplayMatchMode(Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_MATCH_MODE") ?? "exact");
        var clientSearchWindow = args.Length > 6
            ? int.Parse(args[6])
            : int.Parse(Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_SEARCH_WINDOW") ?? "0");
        var pacingMode = args.Length > 7
            ? ParseReplayPacingMode(args[7])
            : ParseReplayPacingMode(Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_PACING") ?? "none");
        var maxReplayDelayMilliseconds = args.Length > 8
            ? int.Parse(args[8])
            : int.Parse(Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_MAX_DELAY_MS") ?? "250");
        var backendMode = args.Length > 9
            ? ParseReplayBackendMode(args[9])
            : ParseReplayBackendMode(Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_BACKEND_MODE") ?? "packet");
        var preferredScript = args.Length > 10
            ? args[10]
            : Environment.GetEnvironmentVariable("PLASMA_SOURCE_REPLAY_PREFERRED_SCRIPT");
        if (!string.IsNullOrWhiteSpace(evidenceLog))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidenceLog)) ?? ".");
            await using var writer = File.AppendText(evidenceLog);
            await new PcapSourceReplayBackend().RunAsync(pcap, bind, port, CancellationToken.None, writer, matchMode, clientSearchWindow, pacingMode, maxReplayDelayMilliseconds, backendMode, preferredScript);
        }
        else
        {
            await new PcapSourceReplayBackend().RunAsync(pcap, bind, port, CancellationToken.None, matchMode: matchMode, clientSearchWindow: clientSearchWindow, pacingMode: pacingMode, maxReplayDelayMilliseconds: maxReplayDelayMilliseconds, backendMode: backendMode, preferredScript: preferredScript);
        }
        break;
    }
    case "run-source-turn-contract-backend":
    {
        var pcap = args.Length > 1
            ? args[1]
            : Path.Combine(repoRoot, ".local/input/pcaps");
        var bind = IPAddress.Parse(args.Length > 2 ? args[2] : "127.0.0.1");
        var port = args.Length > 3 ? int.Parse(args[3]) : 27016;
        var evidenceLog = args.Length > 4 ? args[4] : "";
        if (!string.IsNullOrWhiteSpace(evidenceLog))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidenceLog)) ?? ".");
            await using var writer = File.AppendText(evidenceLog);
            await new PcapSourceTurnContractBackend().RunAsync(pcap, bind, port, CancellationToken.None, writer);
        }
        else
        {
            await new PcapSourceTurnContractBackend().RunAsync(pcap, bind, port, CancellationToken.None);
        }
        break;
    }
    case "write-report-templates":
        await ReportTemplateWriter.WriteAsync(repoRoot);
        Console.WriteLine("wrote re/bfbc2 and re/tf2ps3 report templates");
        break;
    case "bfbc2-log-evidence":
    {
        var input = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/BFBC2_R34/RuntimeLog_FRIDIS-STEAMMAC_server.log");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/runtime-log-evidence.json");
        await Bfbc2LogEvidence.ExportAsync(input, output);
        Console.WriteLine($"wrote {output}");
        break;
    }
    case "reduce-bfbc2-evidence":
    {
        var focused = Path.Combine(repoRoot, "re/bfbc2/game-manager-focused-evidence.json");
        var fast = Path.Combine(repoRoot, "re/bfbc2/game-manager-evidence.json");
        var input = args.Length > 1 ? args[1] : File.Exists(focused) ? focused : fast;
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2");
        await Bfbc2EvidenceReducer.ReduceAsync(input, output);
        Console.WriteLine($"updated BFBC2 reports in {output}");
        break;
    }
    case "reduce-bfbc2-decompiles":
    {
        var handlers = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/handlers.json");
        var decompiles = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/server-function-decompiles.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/class-map.json");
        await Bfbc2DecompileReducer.ReduceAsync(handlers, decompiles, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-pointer-evidence":
    {
        var handlers = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/handlers.json");
        var pointers = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/function-pointer-evidence.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/dispatcher-table.json");
        await Bfbc2PointerEvidenceReducer.ReduceAsync(handlers, pointers, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-dispatcher-table":
    {
        var listenerComplete = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-complete-map.json");
        var handleMessage = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/handle-message-branch-map.json");
        var phaseMap = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/gamemanager-phase-map.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/bfbc2/dispatcher-table.json");
        await Bfbc2DispatcherTableReducer.ReduceAsync(listenerComplete, handleMessage, phaseMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-caller-decompiles":
    {
        var dispatcher = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/dispatcher-table.json");
        var callers = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/caller-function-decompiles.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/caller-layer-summary.json");
        await Bfbc2CallerDecompileReducer.ReduceAsync(dispatcher, callers, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-recovered-callsites":
    {
        var recovered = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/recovered-callsite-functions.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/recovered-callsite-summary.json");
        await Bfbc2RecoveredCallsiteReducer.ReduceAsync(recovered, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-recovered-pointers":
    {
        var recovered = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/recovered-callsite-summary.json");
        var pointers = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/recovered-function-pointer-evidence.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/callback-table-map.json");
        await Bfbc2RecoveredPointerReducer.ReduceAsync(recovered, pointers, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-server-send":
    {
        var sendFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/server-send-functions.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/send-path-map.json");
        await Bfbc2ServerSendReducer.ReduceAsync(sendFunctions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-transport-map":
    {
        var sendPointers = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/send-pointer-evidence.json");
        var lowLevelSend = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/low-level-send-decompiles.json");
        var packetParser = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/packet-parser-decompiles.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/bfbc2/transport-map.json");
        await Bfbc2TransportMapReducer.ReduceAsync(sendPointers, lowLevelSend, packetParser, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-gamemanager-phases":
    {
        var logFunctions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/gamemanager-log-functions.json");
        var builderFunctions = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/gamemanager-builder-decompiles.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/gamemanager-phase-map.json");
        await Bfbc2GameManagerPhaseReducer.ReduceAsync(logFunctions, builderFunctions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-server-gamemanager-listener":
    {
        var functions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-functions.json");
        var pointers = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-pointer-evidence.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-map.json");
        await Bfbc2ServerGameManagerListenerReducer.ReduceAsync(functions, pointers, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-server-gamemanager-listener-complete":
    {
        var exe = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/BFBC2_R34/Frost.Game.Main_Win32_Final.exe");
        var listenerMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-map.json");
        var missingSlots = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/listener-missing-slot-decompiles.json");
        var output = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-complete-map.json");
        await Bfbc2ServerGameManagerListenerCompleteReducer.ReduceAsync(exe, listenerMap, missingSlots, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-handle-message":
    {
        var decompiles = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/listener-missing-slot-decompiles.json");
        var listenerMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/server-gamemanager-listener-complete-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/handle-message-branch-map.json");
        await Bfbc2HandleMessageReducer.ReduceAsync(decompiles, listenerMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-handle-message-callees":
    {
        var decompiles = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/handle-message-callee-decompiles.json");
        var handleMessageMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/handle-message-branch-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/handle-message-callee-map.json");
        await Bfbc2HandleMessageCalleeReducer.ReduceAsync(decompiles, handleMessageMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-switch-squad-mutation":
    {
        var decompiles = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/switch-squad-mutation-decompiles.json");
        var calleeMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/handle-message-callee-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/switch-squad-mutation-map.json");
        await Bfbc2SwitchSquadMutationReducer.ReduceAsync(decompiles, calleeMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-bfbc2-join-chain":
    {
        var functions = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/bfbc2/join-chain-functions.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/bfbc2/join-chain-map.json");
        await Bfbc2JoinChainReducer.ReduceAsync(functions, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-gamemanager":
    {
        var evidence = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/game-manager-analyzed-evidence.json");
        var functions = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/gamemanager-function-decompiles.json");
        var bfbc2Phases = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/gamemanager-phase-map.json");
        var outputDir = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3");
        await Tf2Ps3GameManagerReducer.ReduceAsync(evidence, functions, bfbc2Phases, outputDir);
        Console.WriteLine($"updated TF2 PS3 GameManager reports in {outputDir}");
        break;
    }
    case "reduce-tf2ps3-data-neighborhood":
    {
        var elf = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        var handlerMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/handler-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/data-neighborhood-map.json");
        await Tf2Ps3DataNeighborhoodReducer.ReduceAsync(elf, handlerMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-dispatcher-map":
    {
        var handlerMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/handler-map.json");
        var dataNeighborhood = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/data-neighborhood-map.json");
        var bfbc2Dispatcher = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/bfbc2/dispatcher-table.json");
        var defaultHelperCallerMap = Path.Combine(repoRoot, "re/tf2ps3/helper-caller-map.json");
        var helperCallerMap = args.Length > 5 ? args[4] : File.Exists(defaultHelperCallerMap) ? defaultHelperCallerMap : "";
        var output = args.Length > 5
            ? args[5]
            : args.Length > 4
                ? args[4]
                : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        await Tf2Ps3DispatcherMapReducer.ReduceAsync(handlerMap, dataNeighborhood, bfbc2Dispatcher, output, helperCallerMap);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-anchor-table":
    {
        var dataNeighborhood = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/data-neighborhood-map.json");
        var dispatcherMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/anchor-table-map.json");
        await Tf2Ps3AnchorTableReducer.ReduceAsync(dataNeighborhood, dispatcherMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-anchor-context":
    {
        var anchorContext = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/anchor-context.json");
        var anchorTable = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/anchor-table-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/anchor-context-map.json");
        await Tf2Ps3AnchorContextReducer.ReduceAsync(anchorContext, anchorTable, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-reader-functions":
    {
        var anchorContextMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/anchor-context-map.json");
        var dispatcherMap = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/reader-function-map.json");
        await Tf2Ps3ReaderFunctionReducer.ReduceAsync(anchorContextMap, dispatcherMap, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-reader-helpers":
    {
        var helperDecompiles = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/reader-helper-function-decompiles.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/reader-helper-map.json");
        await Tf2Ps3ReaderHelperReducer.ReduceAsync(helperDecompiles, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-second-level-helpers":
    {
        var helperDecompiles = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/second-level-helper-function-decompiles.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/second-level-helper-map.json");
        await Tf2Ps3SecondLevelHelperReducer.ReduceAsync(helperDecompiles, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-helper-callers":
    {
        var callerContext = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/helper-caller-context.json");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/helper-caller-map.json");
        await Tf2Ps3HelperCallerReducer.ReduceAsync(callerContext, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-unresolved-targets":
    {
        var dispatcherMap = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/dispatcher-map.json");
        var dataNeighborhood = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/data-neighborhood-map.json");
        var anchorTable = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/anchor-table-map.json");
        var anchorContext = args.Length > 4 ? args[4] : Path.Combine(repoRoot, "re/tf2ps3/anchor-context-map.json");
        var readerFunctions = args.Length > 5 ? args[5] : Path.Combine(repoRoot, "re/tf2ps3/reader-function-map.json");
        var helperCallers = args.Length > 6 ? args[6] : Path.Combine(repoRoot, "re/tf2ps3/helper-caller-map.json");
        var output = args.Length > 7 ? args[7] : Path.Combine(repoRoot, "re/tf2ps3/unresolved-targets.json");
        await Tf2Ps3UnresolvedTargetReducer.ReduceAsync(dispatcherMap, dataNeighborhood, anchorTable, anchorContext, readerFunctions, helperCallers, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-unresolved-function-context":
    {
        var functionContext = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/unresolved-function-context.json");
        var unresolvedTargets = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/unresolved-targets.json");
        var output = args.Length > 3 ? args[3] : Path.Combine(repoRoot, "re/tf2ps3/unresolved-function-context-map.json");
        await Tf2Ps3UnresolvedFunctionContextReducer.ReduceAsync(functionContext, unresolvedTargets, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-network-anchors":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-network-anchor-map.json");
        await Tf2Ps3SourceNetworkAnchorReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-payload-builders":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-payload-builder-map.json");
        var elf = args.Length > 3 ? args[3] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf");
        await Tf2Ps3SourcePayloadBuilderReducer.ReduceAsync(cExport, output, elf);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "reduce-tf2ps3-source-send-callsite-map":
    {
        var cExport = args.Length > 1 ? args[1] : Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf.c");
        var output = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "re/tf2ps3/source-send-callsite-map.json");
        await Tf2Ps3SourceSendCallsiteMapReducer.ReduceAsync(cExport, output);
        Console.WriteLine($"updated {output}");
        break;
    }
    case "write-tf2ps3-unresolved-export-plan":
    {
        var unresolvedTargets = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "re/tf2ps3/unresolved-targets.json");
        var outputDir = args.Length > 2 ? args[2] : Path.Combine(repoRoot, "artifacts/ghidra");
        await Tf2Ps3UnresolvedExportPlanWriter.WriteAsync(unresolvedTargets, outputDir);
        Console.WriteLine($"wrote TF2 unresolved export targets to {outputDir}");
        break;
    }
    case "ghidra-commands":
        PrintGhidraCommands(repoRoot);
        break;
    default:
        Console.WriteLine("""
            Usage:
              PlasmaGameManager.ReTools sync-inputs
              PlasmaGameManager.ReTools validate-inputs
              PlasmaGameManager.ReTools validate-tf2ps3-source-content [content-root] [output-json] [map-root]
              PlasmaGameManager.ReTools analyze-pcaps [input-dir] [output-json] [tf2-dispatcher-map-json]
              PlasmaGameManager.ReTools analyze-pcap-corpus [input-dir] [output-json] [tf2-dispatcher-map-json]
              PlasmaGameManager.ReTools analyze-ea-text-pcaps [input-dir] [output-json]
              PlasmaGameManager.ReTools extract-tf2-source-launch-profile [pcap-ea-text-summary-json] [scenario-file] [output-json]
              PlasmaGameManager.ReTools analyze-handoff-topology [input-dir] [output-json] [tf2-dispatcher-map-json]
              PlasmaGameManager.ReTools analyze-gamemanager-handoff-boundary [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-gamemanager-hello [pcap-or-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-streams [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-gameplay-phases [input-dir] [output-json]
              PlasmaGameManager.ReTools dump-active-flow [pcap] [source-flow-start] [count]
              PlasmaGameManager.ReTools export-pcap-plain-english-trace [pcap] [output-json]
              PlasmaGameManager.ReTools search-active-flow-client [pcap-or-dir] [client-payload-len] [context] [limit]
              PlasmaGameManager.ReTools analyze-source-packet-shapes [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-embedded-objects [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-replay-corpus [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-turn-contract [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-native-builder-correlation [input-dir] [output-json] [source-network-anchor-map-json]
              PlasmaGameManager.ReTools analyze-source-transport [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-transport-fields [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-clc-move-boundaries [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-client-input-coverage [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-client-boundary-probes [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-markerless-transform-probes-strict [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-markerless-session-key-probes [input-dir] [pcap-ea-text-summary-json] [output-json]
              PlasmaGameManager.ReTools analyze-source-usercmd-record-candidates [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-usercmd-queue-delta-tail [pcap-source-usercmd-record-candidates-json] [output-json]
              PlasmaGameManager.ReTools analyze-source-payload-object-first-word [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-associated-object-tokens [input-dir] [associated-object-contract-json] [output-json]
              PlasmaGameManager.ReTools analyze-associated-object-token-transform-probes [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-native-association-descriptors [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-embedded-clc-move-candidates [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-client-command-worklist [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-opaque-markerless-command-wrapper [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-markerless-boundary-hypotheses [input-dir] [client-worklist-json] [opaque-markerless-json] [udp-ingress-json] [serverdll-tunnel-json] [serverdll-usercmd-decoder-json] [output-json]
              PlasmaGameManager.ReTools analyze-source-queued-peer-turns [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-queued-peer-opaque [pcap-or-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-bridge-contract [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-client-visible-source-endpoints [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-frozen-state-turns [input-dir] [output-json]
              PlasmaGameManager.ReTools analyze-source-backend-boundary [input-dir] [output-json] [source-network-anchor-map-json]
              PlasmaGameManager.ReTools analyze-source-translation-readiness [input-dir] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-field-contract [source-payload-builder-map-json] [pcap-source-embedded-objects-json] [pcap-source-gameplay-phases-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-snapshot-path [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-snapshot-delta [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-reliable-peer-attach [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools analyze-live-handoff [gamemanager-events-jsonl] [output-json] [source-log-or-pcap ...]
              PlasmaGameManager.ReTools match-live-source-turns [gamemanager-events-jsonl] [turn-contract-json] [output-json]
              PlasmaGameManager.ReTools compare-live-frozen-state-retail [gamemanager-events-jsonl] [pcap-frozen-state-turns-json] [output-json]
              PlasmaGameManager.ReTools run-source-replay-backend [pcap] [bind-ip] [port] [evidence-jsonl] [exact|transport-shape|loose-transport-shape] [client-search-window] [none|capture-timing] [max-delay-ms] [packet|turn]
              PlasmaGameManager.ReTools run-source-turn-contract-backend [pcap-or-dir] [bind-ip] [port] [evidence-jsonl]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-native-obligations [interesting-strings] [mangled-symbols] [ghidra-evidence] [source-root] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-target-functions [target-functions-json] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-runtime-contract [target-functions-json] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-tunnel-map [server-dll] [target-functions-json] [ghidra-evidence] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-tunnel-ghidra [ghidra-tunnel-evidence-json] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-usercmd-layout [target-functions-json] [runtime-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-usercmd-decoder [target-functions-json] [usercmd-layout-json] [output-json]
              PlasmaGameManager.ReTools reduce-eatf2-serverdll-usercmd-physics-audit [runtime-contract-json] [source-root] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-clc-move-contract [ghidra-context-json] [source-engine-root] [output-json]
              PlasmaGameManager.ReTools reduce-acceptance-gates [bfbc2-dispatcher-json] [tf2-dispatcher-json] [pcap-corpus-json] [output-json] [live-handoff-evidence-json] [source-bridge-contract-json] [pcap-ea-text-json] [source-gameplay-phases-json] [gamemanager-hello-json] [eatf2-serverdll-simulation-json] [eatf2-serverdll-native-obligations-json] [source-loading-frame-debt-json] [source-connected-wrapper-boundary-json] [source-slot70-param2-builder-json] [source-slot70-param2-field-contract-json] [source-bitstream-helper-contract-json] [source-markerless-param2-builder-json] [source-payload-object-dispatch-json] [source-owner-control-subobject-json] [source-owner-forward-context-json] [source-owner-forwarder-bitstream-coverage-json] [source-owner-forward-wrapper-variants-json] [source-category5-usercmd-handler-json] [pcap-source-client-input-coverage-json] [pcap-source-client-boundary-probes-json] [pcap-markerless-transform-probes-strict-json] [source-usercmd-queue-record-json] [source-owner-callback-dispatch-json] [source-helper-slice-receive-siblings-json] [source-recv-bitreader-census-json] [pcap-embedded-clc-move-candidates-json] [pcap-markerless-session-key-probes-json] [tf2ps3-source-content-validation-json] [pcap-source-usercmd-queue-delta-tail-json] [source-raw-udp-control-probe-json] [pcap-source-payload-object-first-word-json] [pcap-source-native-association-descriptors-json] [source-associated-object-token-contract-json] [pcap-source-associated-object-tokens-json] [source-associated-object-slot90-json] [pcap-associated-object-token-transform-probes-json] [source-reliable-peer-attach-json] [source-associated-slotac-provenance-json] [source-associated-lane-role-json]
              PlasmaGameManager.ReTools rebuild-native-reports [output-json] [--continue-on-failure]
              PlasmaGameManager.ReTools bfbc2-log-evidence [input-log] [output-json]
              PlasmaGameManager.ReTools reduce-bfbc2-evidence [evidence-json] [report-dir]
              PlasmaGameManager.ReTools reduce-bfbc2-decompiles [handlers-json] [decompiles-json] [class-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-pointer-evidence [handlers-json] [pointer-evidence-json] [dispatcher-table-json]
              PlasmaGameManager.ReTools reduce-bfbc2-dispatcher-table [listener-complete-json] [handle-message-json] [phase-map-json] [dispatcher-table-json]
              PlasmaGameManager.ReTools reduce-bfbc2-caller-decompiles [dispatcher-table-json] [caller-decompiles-json] [caller-summary-json]
              PlasmaGameManager.ReTools reduce-bfbc2-recovered-callsites [recovered-callsite-functions-json] [recovered-callsite-summary-json]
              PlasmaGameManager.ReTools reduce-bfbc2-recovered-pointers [recovered-callsite-summary-json] [recovered-function-pointer-evidence-json] [callback-table-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-server-send [server-send-functions-json] [send-path-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-transport-map [send-pointer-evidence-json] [low-level-send-decompiles-json] [packet-parser-decompiles-json] [transport-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-gamemanager-phases [gamemanager-log-functions-json] [gamemanager-builder-decompiles-json] [gamemanager-phase-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-server-gamemanager-listener [listener-functions-json] [listener-pointer-evidence-json] [listener-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-server-gamemanager-listener-complete [bfbc2-exe] [listener-map-json] [missing-slot-decompiles-json] [output-json]
              PlasmaGameManager.ReTools reduce-bfbc2-handle-message [missing-slot-decompiles-json] [listener-complete-map-json] [handle-message-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-handle-message-callees [callee-decompiles-json] [handle-message-map-json] [callee-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-switch-squad-mutation [mutation-decompiles-json] [callee-map-json] [mutation-map-json]
              PlasmaGameManager.ReTools reduce-bfbc2-join-chain [join-chain-functions-json] [join-chain-map-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-gamemanager [tf-evidence-json] [tf-function-decompiles-json] [bfbc2-phase-map-json] [tf-output-dir]
              PlasmaGameManager.ReTools reduce-tf2ps3-data-neighborhood [tf-elf] [handler-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-dispatcher-map [handler-map-json] [data-neighborhood-json] [bfbc2-dispatcher-json] [helper-caller-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-anchor-table [data-neighborhood-json] [dispatcher-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-anchor-context [anchor-context-json] [anchor-table-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-reader-functions [anchor-context-map-json] [dispatcher-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-reader-helpers [helper-decompiles-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-second-level-helpers [helper-decompiles-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-helper-callers [caller-context-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-unresolved-targets [dispatcher-map-json] [data-neighborhood-json] [anchor-table-json] [anchor-context-map-json] [reader-function-map-json] [helper-caller-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-unresolved-function-context [function-context-json] [unresolved-targets-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-network-anchors [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-payload-builders [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-send-callsite-map [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-snapshot-path [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-snapshot-delta [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-receive-path [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-pre-payload-receive [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-payload-object-dispatch [tf-elf-c-export] [source-pre-payload-receive-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-associated-object-token-contract [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-associated-object-slot90 [tf-elf-c-export] [source-player-vtable-json] [source-associated-object-token-contract-json] [output-json] [slot90-register-functions-json] [slot90-callsite-context-json] [slotac-output-builder-functions-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-associated-slotac-provenance [slotac-output-builder-functions-json] [source-associated-object-slot90-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-associated-lane-role [tf-elf-c-export] [source-payload-object-dispatch-json] [source-associated-object-slot90-json] [source-associated-slotac-provenance-json] [source-owner-forward-context-json] [source-category5-usercmd-handler-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-control-subobject [tf-elf-c-export] [source-owner-vtable-json] [source-player-vtable-json] [source-payload-object-dispatch-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-forward-target [tf-elf-c-export] [source-owner-control-subobject-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-forward-context [tf-elf-c-export] [source-owner-vtable-json] [source-owner-forward-target-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-forwarder-bitstream-coverage [source-owner-forward-context-json] [source-root] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-forward-wrapper-variants [tf-elf-c-export] [source-root] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-category5-usercmd-handler [tf-elf-c-export] [tf-elf-binary] [source-clc-move-context-json] [source-clc-move-contract-json] [eatf2-serverdll-usercmd-decoder-json] [output-json] [ghidra-xrefs-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-usercmd-queue-record [tf-elf-c-export] [pcap-source-client-boundary-probes-json] [output-json] [eatf2-serverdll-usercmd-decoder-json] [pcap-source-usercmd-record-candidates-json] [tf-elf-binary]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-markerless-receive-classifier [pcap-opaque-markerless-command-wrapper-json] [source-receive-path-json] [source-native-message-contract-json] [source-clc-move-contract-json] [source-netchan-registration-setup-json] [source-required-handler-table-toc-function-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-markerless-transform-candidates [tf-elf-c-export] [source-markerless-receive-classifier-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-client-receive-contract [source-receive-path-json] [source-helper-slice-contract-json] [source-markerless-receive-classifier-json] [source-markerless-transform-candidates-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-receive-dispatch-slots [tf-elf-c-export] [source-object-lifecycle-map-json] [source-client-receive-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-slot70-callsite-census [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-connected-wrapper-boundary [tf-elf-c-export] [pcap-markerless-boundary-hypotheses-json] [source-udp-ingress-correction-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-payload-dispatch-boundary [tf-elf-c-export] [source-player-vtable-map-json] [source-connected-wrapper-boundary-json] [pcap-markerless-boundary-hypotheses-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-slot70-param2-builder [tf-elf-c-export] [source-helper-slice-contract-json] [source-helper-slice-opd-refs-json] [source-receive-dispatch-slots-json] [source-payload-dispatch-boundary-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-slot70-param2-field-contract [tf-elf-c-export] [source-slot70-param2-builder-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-bitstream-helper-contract [tf-elf-c-export] [source-slot70-param2-field-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-markerless-dataflow-targets [helper-slice-contract-json] [owner-callback-map-json] [owner-vtable-map-json] [registration-callsite-map-json] [registration-binary-reference-map-json] [required-handler-toc-function-map-json] [slot70-callsite-census-json] [slot70-param2-builder-json] [slot70-param2-field-contract-json] [bitstream-helper-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-handler-registrations [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-player-vtable [tf-elf] [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-handler-vtable-candidates [tf-elf] [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-handler-registration-proof [candidate-report-json] [ghidra-refs-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-handler-candidate-toc-access [tf-elf] [candidate-report-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-registration-callsites [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-object-lifecycle [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-installed-object-vtable [tf-elf] [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-object-vtable-lifecycle [tf-elf-c-export] [ghidra-refs-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-registration-binary-refs [tf-elf] [handler-candidate-report-json] [ghidra-refs-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-callback [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-callback-dispatch [tf-elf-c-export] [owner-callback-map-json] [slot70-param2-field-contract-json] [markerless-dataflow-targets-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-helper-slice-receive-siblings [tf-elf-c-export] [source-helper-slice-contract-json] [source-slot70-param2-builder-json] [source-owner-callback-dispatch-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-recv-bitreader-census [tf-elf-c-export] [source-connected-wrapper-boundary-json] [source-helper-slice-receive-siblings-json] [pcap-markerless-boundary-hypotheses-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-raw-udp-control-probe [tf-elf-c-export] [pcap-input] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-embedded-clc-move-proof [tf-elf-c-export] [pcap-embedded-clc-move-candidates-json] [source-owner-callback-dispatch-json] [source-helper-slice-receive-siblings-json] [source-recv-bitreader-census-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-markerless-param2-builder [tf-elf-c-export] [source-recv-bitreader-census-json] [output-json] [implementation-root]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-owner-vtables [tf-elf] [tf-elf-c-export] [opd-refs-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-helper-slice-contract [tf-elf] [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-netchan-static-anchor [tf-elf] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-netchan-source-crossmap [tf-elf-c-export] [source-engine-root] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-message-string-catalog [tf-elf] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-message-vtable-catalog [tf-elf] [source-message-string-catalog-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-critical-message-io-contract [tf-elf-c-export] [source-engine-root] [source-message-vtable-catalog-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-critical-bootstrap-route [tf-elf-c-export] [source-critical-message-io-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-packet-entities-placement [source-critical-bootstrap-route-json] [source-snapshot-path-json] [source-snapshot-delta-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-native-message-contract [source-message-vtable-catalog-json] [source-critical-message-io-contract-json] [source-clc-move-contract-json] [source-bootstrap-control-message-map-json] [source-packet-entities-placement-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-server-replacement-contract [native-source-lifecycle-json] [eatf2-serverdll-runtime-contract-json] [eatf2-serverdll-native-obligations-json] [source-native-message-contract-json] [source-object-stream-bootstrap-json] [source-queued-prefix-contract-json] [source-loading-replacement-plan-json] [ps3-native-source-responder-cs] [output-json] [source-generated-prefix-retail-crossmap-json] [eatf2-serverdll-usercmd-physics-audit-json] [source-send-callsite-map-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-queued-peer-targets [source-peer-channel-map-json] [source-critical-bootstrap-route-map-json] [source-object-stream-bootstrap-map-json] [pcap-source-queued-peer-opaque-json] [eatf2-serverdll-tunnel-ghidra-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-native-template-debt [ps3-native-source-responder-cs] [source-queued-peer-target-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-template-patch-layout [ps3-native-source-responder-cs] [source-native-template-debt-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-state-link-grammar [source-template-patch-layout-json] [pcap-source-embedded-objects-json] [pcap-source-queued-peer-opaque-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-embedded-object-grammar [pcap-source-embedded-objects-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-queued-prefix-contract [source-queued-peer-target-map-json] [pcap-source-queued-peer-opaque-json] [source-template-patch-layout-json] [source-state-link-grammar-json] [source-embedded-object-grammar-json] [eatf2-serverdll-tunnel-ghidra-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-generated-prefix-retail-crossmap [source-queued-prefix-contract-json] [semantic-trace-directory] [pcap-source-queued-peer-opaque-json] [output-json] [source-loading-frame-debt-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-generated-prefix-field-probe [source-generated-prefix-retail-crossmap-json] [semantic-trace-directory] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-native-debt-priority [source-native-template-debt-json] [source-template-patch-layout-json] [source-queued-prefix-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-loading-frame-debt [ps3-native-source-responder-cs] [source-native-template-debt-json] [source-queued-prefix-contract-json] [eatf2-serverdll-tunnel-ghidra-map-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-loading-replacement-plan [source-loading-frame-debt-json] [source-queued-prefix-contract-json] [source-queued-peer-target-map-json] [source-critical-message-io-contract-json] [source-object-stream-bootstrap-map-json] [source-snapshot-path-map-json] [source-native-message-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-netchan-registration-setup [source-netchan-source-crossmap-json] [source-helper-slice-contract-json] [source-registration-callsite-map-json] [source-object-lifecycle-map-json] [source-object-vtable-lifecycle-map-json] [source-owner-vtable-map-json] [source-handler-registration-map-json] [source-handler-registration-proof-map-json] [source-registration-binary-reference-map-json] [source-native-message-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-required-client-read-contract [tf-elf-c-export] [source-native-message-contract-json] [source-netchan-registration-setup-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-required-handler-constructor-probe [tf-elf] [tf-elf-c-export] [source-message-vtable-catalog-json] [source-netchan-registration-setup-json] [source-required-client-read-contract-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-required-handler-table-neighborhood [tf-elf] [source-required-handler-constructor-probe-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-required-handler-table-toc-access [tf-elf] [source-required-handler-table-neighborhood-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-required-handler-table-toc-functions [source-required-handler-table-toc-access-json] [source-required-handler-table-toc-instruction-context-json] [source-required-handler-table-toc-focused-functions-json] [output-json]
              PlasmaGameManager.ReTools reduce-tf2ps3-source-virtual-slot44-scan [tf-elf-c-export] [output-json]
              PlasmaGameManager.ReTools write-tf2ps3-unresolved-export-plan [unresolved-targets-json] [output-dir]
              PlasmaGameManager.ReTools write-report-templates
              PlasmaGameManager.ReTools ghidra-commands
            """);
        break;
}

static Ps3SourceGameplayReplayMatchMode ParseReplayMatchMode(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "" or "exact" or "exact-payload" => Ps3SourceGameplayReplayMatchMode.ExactPayload,
        "shape" or "transport-shape" or "ps3-transport-shape" => Ps3SourceGameplayReplayMatchMode.TransportShape,
        "loose" or "loose-shape" or "loose-transport-shape" or "ps3-loose-transport-shape" => Ps3SourceGameplayReplayMatchMode.LooseTransportShape,
        _ => throw new ArgumentException($"Unsupported Source replay match mode: {value}")
    };
}

static PcapSourceReplayPacingMode ParseReplayPacingMode(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "" or "none" or "off" => PcapSourceReplayPacingMode.None,
        "pcap" or "capture" or "capture-timing" => PcapSourceReplayPacingMode.CaptureTiming,
        _ => throw new ArgumentException($"Unsupported Source replay pacing mode: {value}")
    };
}

static PcapSourceReplayBackendMode ParseReplayBackendMode(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "" or "packet" or "packet-replay" => PcapSourceReplayBackendMode.Packet,
        "turn" or "turn-replay" or "burst" or "client-turn" => PcapSourceReplayBackendMode.Turn,
        _ => throw new ArgumentException($"Unsupported Source replay backend mode: {value}")
    };
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static IEnumerable<string> EnumeratePcapInputs(string input)
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

static string FormatActiveFlowPacket(int sourceStep, PcapActiveFlowDatagram packet)
{
    if (!Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
    {
        return $"sourceStep={sourceStep} packetIndex={packet.PacketIndex} {packet.Direction} {packet.Kind} len={packet.Payload.Length} transport=false hex={Convert.ToHexString(packet.Payload.AsSpan(0, Math.Min(24, packet.Payload.Length))).ToLowerInvariant()} ascii={packet.AsciiPreview}";
    }

    var shape = Ps3SourceGameplaySession.ClassifyShape(transport);
    var bodyPrefix = Convert.ToHexString(transport.Body.AsSpan(0, Math.Min(24, transport.Body.Length))).ToLowerInvariant();
    return $"sourceStep={sourceStep} packetIndex={packet.PacketIndex} {packet.Direction} {packet.Kind} len={packet.Payload.Length} seq={transport.CandidateSequence} bodyLen={transport.Body.Length} shape={shape} body={bodyPrefix} ascii={packet.AsciiPreview}";
}

static void PrintGhidraCommands(string repoRoot)
{
    var ghidraProject = Path.Combine(repoRoot, "local-ghidra");
    Console.WriteLine($"""
        BFBC2 headless import target:
          analyzeHeadless "{ghidraProject}" BFBC2_R34 -import "{repoRoot}/.local/input/BFBC2_R34/Frost.Game.Main_Win32_Final.exe" -overwrite -analysisTimeoutPerFile 1800

        TF2 PS3 import target:
          Use a local checkout of Ps3GhidraScripts/
          Language: PowerISA-Altivec-64-32addr, big-endian.
          Apply ppc_64_32.cspec r2 unaffected-register fix first.
          Run AnalyzePs3Binary.java before auto-analysis, then DefinePs3Syscalls.java after.
          analyzeHeadless "{ghidraProject}" TF2_PS3 -import "{repoRoot}/.local/input/TF2PS3/TF.elf" -processor PowerISA-Altivec-64-32addr -overwrite

        OOAnalyzer host route:
          flatpak-spawn --host podman --version
          Input binary: {repoRoot}/.local/input/BFBC2_R34/Frost.Game.Main_Win32_Final.exe
        """);
}
