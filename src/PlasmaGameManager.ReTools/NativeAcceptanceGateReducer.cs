using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class NativeAcceptanceGateReducer
{
    public static async Task ReduceAsync(
        string bfbc2DispatcherPath,
        string tf2DispatcherPath,
        string pcapCorpusPath,
        string outputPath,
        string? liveHandoffEvidencePath = null,
        string? sourceBridgeContractPath = null,
        string? pcapEaTextPath = null,
        string? sourceGameplayPhasesPath = null,
        string? gameManagerHelloPath = null,
        string? serverDllSimulationPath = null,
        string? serverDllNativeObligationsPath = null,
        string? sourceLoadingFrameDebtPath = null,
        string? sourceConnectedWrapperBoundaryPath = null,
        string? sourceSlot70Param2BuilderPath = null,
        string? sourceSlot70Param2FieldContractPath = null,
        string? sourceBitstreamHelperContractPath = null,
        string? sourceMarkerlessParam2BuilderPath = null,
        string? sourcePayloadObjectDispatchPath = null,
        string? sourceOwnerControlSubobjectPath = null,
        string? sourceOwnerForwardContextPath = null,
        string? sourceOwnerForwarderBitstreamCoveragePath = null,
        string? sourceOwnerForwardWrapperVariantsPath = null,
        string? sourceCategory5UsercmdHandlerPath = null,
        string? sourceClientInputCoveragePath = null,
        string? sourceClientBoundaryProbePath = null,
        string? sourceMarkerlessTransformProbePath = null,
        string? sourceUsercmdQueueRecordPath = null,
        string? sourceOwnerCallbackDispatchPath = null,
        string? sourceHelperSliceReceiveSiblingsPath = null,
        string? sourceRecvBitreaderCensusPath = null,
        string? sourceEmbeddedClcMoveCandidatesPath = null,
        string? sourceMarkerlessSessionKeyProbePath = null,
        string? sourceContentValidationPath = null,
        string? sourceUsercmdQueueDeltaTailPath = null,
        string? sourceRawUdpControlProbePath = null,
        string? sourcePayloadObjectFirstWordPath = null,
        string? sourceNativeAssociationDescriptorScanPath = null,
        string? sourceAssociatedObjectTokenContractPath = null,
        string? pcapSourceAssociatedObjectTokensPath = null,
        string? sourceAssociatedObjectSlot90Path = null,
        string? associatedObjectTokenTransformProbePath = null,
        string? sourceReliablePeerAttachPath = null,
        string? sourceAssociatedSlotAcProvenancePath = null,
        string? sourceAssociatedLaneRolePath = null)
    {
        using var bfbc2Doc = JsonDocument.Parse(File.ReadAllText(bfbc2DispatcherPath));
        using var tf2Doc = JsonDocument.Parse(File.ReadAllText(tf2DispatcherPath));
        using var pcapDoc = File.Exists(pcapCorpusPath) ? JsonDocument.Parse(File.ReadAllText(pcapCorpusPath)) : null;
        using var liveDoc = liveHandoffEvidencePath is not null && File.Exists(liveHandoffEvidencePath)
            ? JsonDocument.Parse(File.ReadAllText(liveHandoffEvidencePath))
            : null;
        using var bridgeDoc = sourceBridgeContractPath is not null && File.Exists(sourceBridgeContractPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceBridgeContractPath))
            : null;
        using var eaTextDoc = pcapEaTextPath is not null && File.Exists(pcapEaTextPath)
            ? JsonDocument.Parse(File.ReadAllText(pcapEaTextPath))
            : null;
        using var sourceGameplayPhasesDoc = sourceGameplayPhasesPath is not null && File.Exists(sourceGameplayPhasesPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceGameplayPhasesPath))
            : null;
        using var gameManagerHelloDoc = gameManagerHelloPath is not null && File.Exists(gameManagerHelloPath)
            ? JsonDocument.Parse(File.ReadAllText(gameManagerHelloPath))
            : null;
        using var serverDllSimulationDoc = serverDllSimulationPath is not null && File.Exists(serverDllSimulationPath)
            ? JsonDocument.Parse(File.ReadAllText(serverDllSimulationPath))
            : null;
        using var serverDllNativeObligationsDoc = serverDllNativeObligationsPath is not null && File.Exists(serverDllNativeObligationsPath)
            ? JsonDocument.Parse(File.ReadAllText(serverDllNativeObligationsPath))
            : null;
        using var sourceLoadingFrameDebtDoc = sourceLoadingFrameDebtPath is not null && File.Exists(sourceLoadingFrameDebtPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceLoadingFrameDebtPath))
            : null;
        using var sourceConnectedWrapperBoundaryDoc = sourceConnectedWrapperBoundaryPath is not null && File.Exists(sourceConnectedWrapperBoundaryPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceConnectedWrapperBoundaryPath))
            : null;
        using var sourceSlot70Param2BuilderDoc = sourceSlot70Param2BuilderPath is not null && File.Exists(sourceSlot70Param2BuilderPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceSlot70Param2BuilderPath))
            : null;
        using var sourceSlot70Param2FieldContractDoc = sourceSlot70Param2FieldContractPath is not null && File.Exists(sourceSlot70Param2FieldContractPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceSlot70Param2FieldContractPath))
            : null;
        using var sourceBitstreamHelperContractDoc = sourceBitstreamHelperContractPath is not null && File.Exists(sourceBitstreamHelperContractPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceBitstreamHelperContractPath))
            : null;
        using var sourceMarkerlessParam2BuilderDoc = sourceMarkerlessParam2BuilderPath is not null && File.Exists(sourceMarkerlessParam2BuilderPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceMarkerlessParam2BuilderPath))
            : null;
        using var sourcePayloadObjectDispatchDoc = sourcePayloadObjectDispatchPath is not null && File.Exists(sourcePayloadObjectDispatchPath)
            ? JsonDocument.Parse(File.ReadAllText(sourcePayloadObjectDispatchPath))
            : null;
        using var sourceOwnerControlSubobjectDoc = sourceOwnerControlSubobjectPath is not null && File.Exists(sourceOwnerControlSubobjectPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceOwnerControlSubobjectPath))
            : null;
        using var sourceOwnerForwardContextDoc = sourceOwnerForwardContextPath is not null && File.Exists(sourceOwnerForwardContextPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceOwnerForwardContextPath))
            : null;
        using var sourceOwnerForwarderBitstreamCoverageDoc = sourceOwnerForwarderBitstreamCoveragePath is not null && File.Exists(sourceOwnerForwarderBitstreamCoveragePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceOwnerForwarderBitstreamCoveragePath))
            : null;
        using var sourceOwnerForwardWrapperVariantsDoc = sourceOwnerForwardWrapperVariantsPath is not null && File.Exists(sourceOwnerForwardWrapperVariantsPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceOwnerForwardWrapperVariantsPath))
            : null;
        using var sourceCategory5UsercmdHandlerDoc = sourceCategory5UsercmdHandlerPath is not null && File.Exists(sourceCategory5UsercmdHandlerPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceCategory5UsercmdHandlerPath))
            : null;
        using var sourceClientInputCoverageDoc = sourceClientInputCoveragePath is not null && File.Exists(sourceClientInputCoveragePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceClientInputCoveragePath))
            : null;
        using var sourceClientBoundaryProbeDoc = sourceClientBoundaryProbePath is not null && File.Exists(sourceClientBoundaryProbePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceClientBoundaryProbePath))
            : null;
        using var sourceMarkerlessTransformProbeDoc = sourceMarkerlessTransformProbePath is not null && File.Exists(sourceMarkerlessTransformProbePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceMarkerlessTransformProbePath))
            : null;
        using var sourceUsercmdQueueRecordDoc = sourceUsercmdQueueRecordPath is not null && File.Exists(sourceUsercmdQueueRecordPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceUsercmdQueueRecordPath))
            : null;
        using var sourceOwnerCallbackDispatchDoc = sourceOwnerCallbackDispatchPath is not null && File.Exists(sourceOwnerCallbackDispatchPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceOwnerCallbackDispatchPath))
            : null;
        using var sourceHelperSliceReceiveSiblingsDoc = sourceHelperSliceReceiveSiblingsPath is not null && File.Exists(sourceHelperSliceReceiveSiblingsPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceHelperSliceReceiveSiblingsPath))
            : null;
        using var sourceRecvBitreaderCensusDoc = sourceRecvBitreaderCensusPath is not null && File.Exists(sourceRecvBitreaderCensusPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceRecvBitreaderCensusPath))
            : null;
        using var sourceEmbeddedClcMoveCandidatesDoc = sourceEmbeddedClcMoveCandidatesPath is not null && File.Exists(sourceEmbeddedClcMoveCandidatesPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceEmbeddedClcMoveCandidatesPath))
            : null;
        using var sourceMarkerlessSessionKeyProbeDoc = sourceMarkerlessSessionKeyProbePath is not null && File.Exists(sourceMarkerlessSessionKeyProbePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceMarkerlessSessionKeyProbePath))
            : null;
        using var sourceContentValidationDoc = sourceContentValidationPath is not null && File.Exists(sourceContentValidationPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceContentValidationPath))
            : null;
        using var sourceUsercmdQueueDeltaTailDoc = sourceUsercmdQueueDeltaTailPath is not null && File.Exists(sourceUsercmdQueueDeltaTailPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceUsercmdQueueDeltaTailPath))
            : null;
        using var sourceRawUdpControlProbeDoc = sourceRawUdpControlProbePath is not null && File.Exists(sourceRawUdpControlProbePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceRawUdpControlProbePath))
            : null;
        using var sourcePayloadObjectFirstWordDoc = sourcePayloadObjectFirstWordPath is not null && File.Exists(sourcePayloadObjectFirstWordPath)
            ? JsonDocument.Parse(File.ReadAllText(sourcePayloadObjectFirstWordPath))
            : null;
        using var sourceNativeAssociationDescriptorScanDoc = sourceNativeAssociationDescriptorScanPath is not null && File.Exists(sourceNativeAssociationDescriptorScanPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceNativeAssociationDescriptorScanPath))
            : null;
        using var sourceAssociatedObjectTokenContractDoc = sourceAssociatedObjectTokenContractPath is not null && File.Exists(sourceAssociatedObjectTokenContractPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceAssociatedObjectTokenContractPath))
            : null;
        using var pcapSourceAssociatedObjectTokensDoc = pcapSourceAssociatedObjectTokensPath is not null && File.Exists(pcapSourceAssociatedObjectTokensPath)
            ? JsonDocument.Parse(File.ReadAllText(pcapSourceAssociatedObjectTokensPath))
            : null;
        using var sourceAssociatedObjectSlot90Doc = sourceAssociatedObjectSlot90Path is not null && File.Exists(sourceAssociatedObjectSlot90Path)
            ? JsonDocument.Parse(File.ReadAllText(sourceAssociatedObjectSlot90Path))
            : null;
        using var associatedObjectTokenTransformProbeDoc = associatedObjectTokenTransformProbePath is not null && File.Exists(associatedObjectTokenTransformProbePath)
            ? JsonDocument.Parse(File.ReadAllText(associatedObjectTokenTransformProbePath))
            : null;
        using var sourceReliablePeerAttachDoc = sourceReliablePeerAttachPath is not null && File.Exists(sourceReliablePeerAttachPath)
            ? JsonDocument.Parse(File.ReadAllText(sourceReliablePeerAttachPath))
            : null;
        using var sourceAssociatedSlotAcProvenanceDoc = sourceAssociatedSlotAcProvenancePath is not null && File.Exists(sourceAssociatedSlotAcProvenancePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceAssociatedSlotAcProvenancePath))
            : null;
        using var sourceAssociatedLaneRoleDoc = sourceAssociatedLaneRolePath is not null && File.Exists(sourceAssociatedLaneRolePath)
            ? JsonDocument.Parse(File.ReadAllText(sourceAssociatedLaneRolePath))
            : null;

        var gates = new[]
        {
            Bfbc2DispatcherGate(bfbc2Doc.RootElement),
            Tf2DispatcherGate(tf2Doc.RootElement),
            PcapCorpusGate(pcapDoc?.RootElement),
            ProfileReplayGate(pcapDoc?.RootElement, gameManagerHelloDoc?.RootElement),
            CustomCreatePreferenceGate(eaTextDoc?.RootElement),
            SourceBridgeContractGate(bridgeDoc?.RootElement),
            SourceGameplayPhaseGate(sourceGameplayPhasesDoc?.RootElement),
            ServerDllSimulationContractGate(serverDllSimulationDoc?.RootElement),
            ServerDllNativeObligationsGate(serverDllNativeObligationsDoc?.RootElement),
            SourceLoadingFrameDebtGate(sourceLoadingFrameDebtDoc?.RootElement),
            SourceConnectedWrapperContractGate(sourceConnectedWrapperBoundaryDoc?.RootElement),
            SourceBitstreamPrimitiveContractGate(sourceSlot70Param2FieldContractDoc?.RootElement, sourceBitstreamHelperContractDoc?.RootElement),
            SourceMarkerlessInputBoundaryGate(sourceConnectedWrapperBoundaryDoc?.RootElement, sourceSlot70Param2BuilderDoc?.RootElement, sourceBitstreamHelperContractDoc?.RootElement, sourceMarkerlessParam2BuilderDoc?.RootElement, sourcePayloadObjectDispatchDoc?.RootElement, sourceOwnerControlSubobjectDoc?.RootElement, sourceOwnerForwardContextDoc?.RootElement, sourceOwnerForwarderBitstreamCoverageDoc?.RootElement, sourceOwnerForwardWrapperVariantsDoc?.RootElement, sourceCategory5UsercmdHandlerDoc?.RootElement, sourceClientInputCoverageDoc?.RootElement, sourceClientBoundaryProbeDoc?.RootElement, sourceMarkerlessTransformProbeDoc?.RootElement, sourceUsercmdQueueRecordDoc?.RootElement, sourceOwnerCallbackDispatchDoc?.RootElement, sourceHelperSliceReceiveSiblingsDoc?.RootElement, sourceRecvBitreaderCensusDoc?.RootElement, sourceEmbeddedClcMoveCandidatesDoc?.RootElement, sourceMarkerlessSessionKeyProbeDoc?.RootElement, sourceUsercmdQueueDeltaTailDoc?.RootElement, sourceRawUdpControlProbeDoc?.RootElement, sourcePayloadObjectFirstWordDoc?.RootElement, sourceNativeAssociationDescriptorScanDoc?.RootElement, sourceAssociatedObjectTokenContractDoc?.RootElement, pcapSourceAssociatedObjectTokensDoc?.RootElement, sourceAssociatedObjectSlot90Doc?.RootElement, associatedObjectTokenTransformProbeDoc?.RootElement, sourceReliablePeerAttachDoc?.RootElement, sourceAssociatedSlotAcProvenanceDoc?.RootElement, sourceAssociatedLaneRoleDoc?.RootElement),
            LiveRpcs3Gate(liveDoc?.RootElement),
            LiveRpcs3SteadyGameplayGate(liveDoc?.RootElement)
        };
        var nativeSourceInputReady = gates.Any(static gate =>
            gate.Id == "tf2ps3-markerless-source-input-boundary-recovered"
            && gate.Status == "passed");
        var nativeMapLoadReady = nativeSourceInputReady
            && gates.Any(static gate => gate.Id == "tf2ps3-source-loading-mapload-has-no-fake-payloads" && gate.Status == "passed")
            && gates.Any(static gate => gate.Id == "eatf2-serverdll-native-obligations-complete" && gate.Status == "passed")
            && gates.Any(static gate => gate.Id == "live-rpcs3-reaches-steady-source-gameplay" && gate.Status == "passed");
        var nativeSourceContentReady = SourceContentValidationReady(sourceContentValidationDoc?.RootElement);
        var sourceContentEvidence = BuildSourceContentEvidence(sourceContentValidationDoc?.RootElement);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new
        {
            status = "seeded-from-current-native-gamemanager-acceptance-evidence",
            overallStatus = gates.All(static gate => gate.Status == "passed") ? "complete" : "incomplete",
            note = "Requirement-by-requirement audit for the PlasmaGameManager plan. This report is intentionally strict: partial reverse-engineering, passing unit tests, and corpus coverage are not treated as live TF2 PS3 completion.",
            inputs = new
            {
                Bfbc2Dispatcher = bfbc2DispatcherPath,
                Tf2Dispatcher = tf2DispatcherPath,
                PcapCorpus = pcapCorpusPath,
                LiveHandoffEvidence = liveHandoffEvidencePath ?? "",
                SourceBridgeContract = sourceBridgeContractPath ?? "",
                PcapEaText = pcapEaTextPath ?? "",
                SourceGameplayPhases = sourceGameplayPhasesPath ?? "",
                GameManagerHello = gameManagerHelloPath ?? "",
                ServerDllSimulation = serverDllSimulationPath ?? "",
                ServerDllNativeObligations = serverDllNativeObligationsPath ?? "",
                SourceLoadingFrameDebt = sourceLoadingFrameDebtPath ?? "",
                SourceConnectedWrapperBoundary = sourceConnectedWrapperBoundaryPath ?? "",
                SourceSlot70Param2Builder = sourceSlot70Param2BuilderPath ?? "",
                SourceSlot70Param2FieldContract = sourceSlot70Param2FieldContractPath ?? "",
                SourceBitstreamHelperContract = sourceBitstreamHelperContractPath ?? "",
                SourceMarkerlessParam2Builder = sourceMarkerlessParam2BuilderPath ?? "",
                SourcePayloadObjectDispatch = sourcePayloadObjectDispatchPath ?? "",
                SourceOwnerControlSubobject = sourceOwnerControlSubobjectPath ?? "",
                SourceOwnerForwardContext = sourceOwnerForwardContextPath ?? "",
                SourceOwnerForwarderBitstreamCoverage = sourceOwnerForwarderBitstreamCoveragePath ?? "",
                SourceOwnerForwardWrapperVariants = sourceOwnerForwardWrapperVariantsPath ?? "",
                SourceCategory5UsercmdHandler = sourceCategory5UsercmdHandlerPath ?? "",
                SourceClientInputCoverage = sourceClientInputCoveragePath ?? "",
                SourceClientBoundaryProbe = sourceClientBoundaryProbePath ?? "",
                SourceMarkerlessTransformProbe = sourceMarkerlessTransformProbePath ?? "",
                SourceUsercmdQueueRecord = sourceUsercmdQueueRecordPath ?? "",
                SourceOwnerCallbackDispatch = sourceOwnerCallbackDispatchPath ?? "",
                SourceHelperSliceReceiveSiblings = sourceHelperSliceReceiveSiblingsPath ?? "",
                SourceRecvBitreaderCensus = sourceRecvBitreaderCensusPath ?? "",
                SourceEmbeddedClcMoveCandidates = sourceEmbeddedClcMoveCandidatesPath ?? "",
                SourceMarkerlessSessionKeyProbe = sourceMarkerlessSessionKeyProbePath ?? "",
                SourceContentValidation = sourceContentValidationPath ?? "",
                SourceUsercmdQueueDeltaTail = sourceUsercmdQueueDeltaTailPath ?? "",
                SourceRawUdpControlProbe = sourceRawUdpControlProbePath ?? "",
                SourcePayloadObjectFirstWord = sourcePayloadObjectFirstWordPath ?? "",
                SourceNativeAssociationDescriptorScan = sourceNativeAssociationDescriptorScanPath ?? "",
                SourceAssociatedObjectTokenContract = sourceAssociatedObjectTokenContractPath ?? "",
                PcapSourceAssociatedObjectTokens = pcapSourceAssociatedObjectTokensPath ?? "",
                SourceAssociatedObjectSlot90 = sourceAssociatedObjectSlot90Path ?? "",
                AssociatedObjectTokenTransformProbe = associatedObjectTokenTransformProbePath ?? "",
                SourceReliablePeerAttach = sourceReliablePeerAttachPath ?? "",
                SourceAssociatedSlotAcProvenance = sourceAssociatedSlotAcProvenancePath ?? "",
                SourceAssociatedLaneRole = sourceAssociatedLaneRolePath ?? ""
            },
            summary = new
            {
                GateCount = gates.Length,
                PassedGates = gates.Count(static gate => gate.Status == "passed"),
                IncompleteGates = gates.Count(static gate => gate.Status == "incomplete"),
                MissingEvidenceGates = gates.Count(static gate => gate.Status == "missing-evidence"),
                NativeSourceContentReady = nativeSourceContentReady,
                NativeSourceInputReady = nativeSourceInputReady,
                NativeMapLoadReady = nativeMapLoadReady
            },
            sourceContentEvidence,
            gates = gates.Select(AnnotateGate).ToArray()
        }, JsonOptions));
    }

    private static object BuildSourceContentEvidence(JsonElement? root)
    {
        if (root is null)
        {
            return new
            {
                Ready = false,
                Status = "missing-evidence",
                MissingRequiredReferenceCount = -1,
                LoadedGameEventDescriptorCount = 0,
                UsesFallbackGameEventDescriptors = true,
                NextWork = "Run scripts/validate-tf2ps3-source-content.sh with TF2PS3_SOURCE_CONTENT_ROOT and TF2PS3_MAP_ROOT set for the extracted PS3 TF tree."
            };
        }

        var summary = root.Value.GetProperty("Summary");
        var ready = ReadBool(summary, "NativeSourceContentReady");
        return new
        {
            Ready = ready,
            Status = ready ? "ready" : "missing-native-source-content",
            ContentRootExists = ReadBool(summary, "ContentRootExists"),
            MapRootExists = ReadBool(summary, "MapRootExists"),
            RequiredReferenceCount = ReadInt(summary, "RequiredReferenceCount"),
            RequiredReferencePresentCount = ReadInt(summary, "RequiredReferencePresentCount"),
            MissingRequiredReferenceCount = ReadInt(summary, "MissingRequiredReferenceCount"),
            GeneratedResourceReferenceCount = ReadInt(summary, "GeneratedResourceReferenceCount"),
            GeneratedResourceReferencePresentCount = ReadInt(summary, "GeneratedResourceReferencePresentCount"),
            VirtualServerOnlyReferenceCount = ReadInt(summary, "VirtualServerOnlyReferenceCount"),
            Ps3MapAliasReferenceCount = ReadInt(summary, "Ps3MapAliasReferenceCount"),
            UnresolvedReferencedCount = ReadInt(summary, "UnresolvedReferencedCount"),
            LoadedGameEventDescriptorCount = ReadInt(summary, "LoadedGameEventDescriptorCount"),
            UsesFallbackGameEventDescriptors = ReadBool(summary, "UsesFallbackGameEventDescriptors"),
            NextWork = ready
                ? "Content validation is not the active map-load blocker."
                : "Resolve missing required Source content references or remove generated references that are not native to TF2 PS3."
        };
    }

    private static bool SourceContentValidationReady(JsonElement? root)
    {
        if (root is null || !root.Value.TryGetProperty("Summary", out var summary))
        {
            return false;
        }

        return ReadBool(summary, "NativeSourceContentReady");
    }

    private static object AnnotateGate(AcceptanceGate gate)
    {
        return new
        {
            gate.Id,
            gate.Status,
            gate.Requirement,
            evidenceStatus = BuildEvidenceStatus(gate),
            gate.Evidence,
            gate.NextWork
        };
    }

    private static object BuildEvidenceStatus(AcceptanceGate gate)
    {
        var expectedSources = ExpectedEvidenceSources(gate.Id);
        var missingCategories = MissingEvidenceCategories(gate, expectedSources);
        return new
        {
            RecoveredFromBfbc2 = EvidenceSourceStatus(gate, expectedSources.Contains("BFBC2_R34", StringComparer.Ordinal), allowPartial: true),
            RecoveredFromTfElf = EvidenceSourceStatus(gate, expectedSources.Contains("TF.elf", StringComparer.Ordinal), allowPartial: true),
            RecoveredFromServerDll = EvidenceSourceStatus(gate, expectedSources.Contains("server.dll", StringComparer.Ordinal), allowPartial: true),
            RecoveredFromPcaps = EvidenceSourceStatus(gate, expectedSources.Contains("PCAP", StringComparer.Ordinal), allowPartial: true),
            Implemented = ImplementationStatus(gate),
            LiveProven = LiveProofStatus(gate, expectedSources.Contains("live", StringComparer.Ordinal)),
            StillMissing = gate.Status == "passed" ? Array.Empty<string>() : gate.NextWork,
            MissingCategories = missingCategories
        };
    }

    private static string EvidenceSourceStatus(AcceptanceGate gate, bool expected, bool allowPartial)
    {
        if (!expected)
        {
            return "not-required";
        }

        return gate.Status switch
        {
            "passed" => "recovered",
            "incomplete" when allowPartial => "partial",
            "incomplete" => "missing",
            _ => "missing"
        };
    }

    private static string ImplementationStatus(AcceptanceGate gate)
    {
        if (gate.Status == "passed")
        {
            return "implemented";
        }

        if (gate.Status == "incomplete")
        {
            return "partial";
        }

        return "missing";
    }

    private static string LiveProofStatus(AcceptanceGate gate, bool expected)
    {
        if (!expected)
        {
            return "not-required";
        }

        return gate.Status == "passed" ? "live-proven" : "missing";
    }

    private static string[] MissingEvidenceCategories(AcceptanceGate gate, IReadOnlyCollection<string> expectedSources)
    {
        if (gate.Status == "passed")
        {
            return [];
        }

        var missing = new List<string>();
        if (expectedSources.Contains("TF.elf", StringComparer.Ordinal) && gate.Status == "missing-evidence")
        {
            missing.Add("TF.elf recovery");
        }

        if (expectedSources.Contains("server.dll", StringComparer.Ordinal) && gate.Status == "missing-evidence")
        {
            missing.Add("server.dll recovery");
        }

        if (expectedSources.Contains("PCAP", StringComparer.Ordinal) && gate.Status == "missing-evidence")
        {
            missing.Add("PCAP semantic evidence");
        }

        if (gate.Status == "incomplete")
        {
            missing.Add("implementation/live proof");
        }

        if (expectedSources.Contains("live", StringComparer.Ordinal))
        {
            missing.Add("live RPCS3 proof");
        }

        return missing.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ExpectedEvidenceSources(string gateId)
    {
        return gateId switch
        {
            "bfbc2-handler-map-no-unknown-dispatcher-slots" => ["BFBC2_R34"],
            "tf2ps3-gamemanager-map-covers-join-create-packet-types" => ["TF.elf"],
            "pcap-semantic-analyzer-explains-selected-corpus" => ["PCAP"],
            "tf2-profile-passes-pcap-join-flows-without-replay-fallbacks" => ["PCAP", "TF.elf"],
            "tf2-custom-create-preferences-drive-backend-provisioning-contract" => ["PCAP"],
            "pcap-source-bridge-contract-supports-hidden-backend" => ["PCAP"],
            "pcap-long-source-gameplay-phases-cover-2fort-and-dustbowl" => ["PCAP"],
            "eatf2-serverdll-simulation-contract-defines-native-source-runtime" => ["server.dll"],
            "eatf2-serverdll-native-obligations-complete" => ["server.dll", "TF.elf"],
            "tf2ps3-source-loading-mapload-has-no-fake-payloads" => ["TF.elf", "server.dll"],
            "tf2ps3-source-connected-wrapper-contract-recovered" => ["TF.elf"],
            "tf2ps3-source-bitstream-primitives-recovered" => ["TF.elf", "server.dll"],
            "tf2ps3-markerless-source-input-boundary-recovered" => ["TF.elf", "server.dll", "PCAP"],
            "live-rpcs3-progresses-past-gamemanager-into-source-motd" => ["live", "TF.elf"],
            "live-rpcs3-reaches-steady-source-gameplay" => ["live", "TF.elf", "server.dll"],
            _ => []
        };
    }

    private static AcceptanceGate Bfbc2DispatcherGate(JsonElement root)
    {
        var summary = root.GetProperty("summary");
        var unknownSlots = summary.GetProperty("UnknownListenerSlots").GetInt32();
        var branchCount = summary.GetProperty("HandleMessageBranchCount").GetInt32();
        var confirmedBranches = summary.GetProperty("ConfirmedHandleMessageBranches").GetInt32();
        var nativeTypes = root.GetProperty("nativeOutgoingPacketTypes").EnumerateArray()
            .Select(static packet => packet.GetProperty("Type").GetInt32())
            .Distinct()
            .Order()
            .ToArray();
        var passed = unknownSlots == 0 && branchCount == confirmedBranches && RequiredNativeTypes.All(nativeTypes.Contains);
        return new AcceptanceGate(
            "bfbc2-handler-map-no-unknown-dispatcher-slots",
            passed ? "passed" : "incomplete",
            "BFBC2 handler map has no unknown dispatcher slots and recovered native packet families are represented.",
            new
            {
                UnknownListenerSlots = unknownSlots,
                HandleMessageBranchCount = branchCount,
                ConfirmedHandleMessageBranches = confirmedBranches,
                NativeOutgoingTypes = nativeTypes
            },
            passed ? Array.Empty<string>() : new[] { "Complete BFBC2 listener/handleMessage branch recovery and native packet-type coverage." });
    }

    private static AcceptanceGate Tf2DispatcherGate(JsonElement root)
    {
        var summary = root.GetProperty("summary");
        var unresolvedTargets = root.GetProperty("remainingNativeTargets").EnumerateArray()
            .Select(static target => target.GetProperty("Role").GetString() ?? "")
            .Where(static role => role.Length > 0)
            .ToArray();
        var unresolvedJoinCreateTargets = unresolvedTargets
            .Where(static role => !DeferredTf2NativeRoles.Contains(role, StringComparer.Ordinal))
            .ToArray();
        var deferredNativeTargets = unresolvedTargets
            .Where(static role => DeferredTf2NativeRoles.Contains(role, StringComparer.Ordinal))
            .ToArray();
        var coveredTypes = summary.GetProperty("CoveredPacketTypes").EnumerateArray()
            .Select(static packet => packet.GetInt32())
            .ToArray();
        var requiredRolesCovered = summary.GetProperty("RequiredJoinRolesCovered").GetInt32();
        var requiredRoleCount = summary.GetProperty("RequiredJoinRoleCount").GetInt32();
        var hasRequiredTypes = RequiredTf2NativeTypes.All(coveredTypes.Contains);
        var passed = unresolvedJoinCreateTargets.Length == 0 && requiredRolesCovered == requiredRoleCount && hasRequiredTypes;

        return new AcceptanceGate(
            "tf2ps3-gamemanager-map-covers-join-create-packet-types",
            passed ? "passed" : "incomplete",
            "TF.elf GameManager map covers every packet type used by TF2 join/create flows with implementation-ready entries.",
            new
            {
                RequiredJoinRolesCovered = requiredRolesCovered,
                RequiredJoinRoleCount = requiredRoleCount,
                CoveredPacketTypes = coveredTypes,
                RequiredPacketTypes = RequiredTf2NativeTypes,
                UnresolvedEntryPointRoles = unresolvedJoinCreateTargets,
                DeferredNativeRoles = deferredNativeTargets
            },
            passed ? Array.Empty<string>() : unresolvedJoinCreateTargets.Select(static role => $"Recover implementation-ready TF.elf entry/field order for {role}.").ToArray());
    }

    private static AcceptanceGate PcapCorpusGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("pcap-semantic-analyzer-explains-selected-corpus", "Run scripts/analyze-pcap-corpus.sh to produce corpus evidence.");
        }

        var summary = root.Value.GetProperty("Summary");
        var unknownCount = summary.GetProperty("UnknownCount").GetInt32();
        var unknownGameManagerScopeCount = summary.TryGetProperty("UnknownGameManagerScopeCount", out var gameManagerUnknown)
            ? gameManagerUnknown.GetInt32()
            : unknownCount;
        var opaqueCount = summary.GetProperty("OpaqueControlCount").GetInt32();
        var opaqueGameManagerScopeCount = summary.TryGetProperty("OpaqueGameManagerScopeCount", out var gameManagerOpaque)
            ? gameManagerOpaque.GetInt32()
            : opaqueCount;
        var fileCount = summary.GetProperty("FileCount").GetInt32();
        var completeHello = summary.GetProperty("FilesWithCompleteHelloFlow").GetInt32();
        var roster = summary.GetProperty("FilesWithRoster").GetInt32();
        var passed = unknownGameManagerScopeCount == 0 && opaqueGameManagerScopeCount == 0 && completeHello > 0 && roster > 0;
        return new AcceptanceGate(
            "pcap-semantic-analyzer-explains-selected-corpus",
            passed ? "passed" : "incomplete",
            "PCAP semantic analyzer explains all selected GameManager packets and leaves no opaque/unknown GameManager surface.",
            new
            {
                FileCount = fileCount,
                FilesWithCompleteHelloFlow = completeHello,
                FilesWithRoster = roster,
                UnknownCount = unknownCount,
                UnknownGameManagerScopeCount = unknownGameManagerScopeCount,
                UnknownDiscoveryNoiseCount = summary.TryGetProperty("UnknownDiscoveryNoiseCount", out var discoveryUnknown) ? discoveryUnknown.GetInt32() : 0,
                UnknownSourceTrafficCount = summary.TryGetProperty("UnknownSourceTrafficCount", out var sourceUnknown) ? sourceUnknown.GetInt32() : 0,
                OpaqueControlCount = opaqueCount,
                OpaqueGameManagerScopeCount = opaqueGameManagerScopeCount,
                OpaqueSourceTrafficCount = summary.TryGetProperty("OpaqueSourceTrafficCount", out var sourceOpaque) ? sourceOpaque.GetInt32() : 0,
                TopUnknownShapes = root.Value.GetProperty("Summary").GetProperty("TopUnknownShapes")
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Reduce the opaque-session-control payload class into typed semantic commands where possible.",
                    "Classify or deliberately exclude remaining unknown UDP payload shapes from the GameManager corpus."
                });
    }

    private static AcceptanceGate ProfileReplayGate(JsonElement? root, JsonElement? helloRoot)
    {
        if (root is null)
        {
            return Missing("tf2-profile-passes-pcap-join-flows-without-replay-fallbacks", "Run scripts/analyze-pcap-corpus.sh and tests to produce profile/corpus evidence.");
        }

        var summary = root.Value.GetProperty("Summary");
        var completeHello = summary.GetProperty("FilesWithCompleteHelloFlow").GetInt32();
        var families = summary.GetProperty("ScenarioFamilies").EnumerateArray()
            .Select(static family => family.GetString() ?? "")
            .Where(static family => family.Length > 0)
            .ToArray();
        var coveredFamilies = RequiredScenarioFamilies.Where(families.Contains).ToArray();
        var helloEvidence = ReadHelloEvidence(helloRoot, completeHello);
        var passed = completeHello > 0
            && coveredFamilies.Length == RequiredScenarioFamilies.Length
            && helloEvidence.Status == "passed";
        return new AcceptanceGate(
            "tf2-profile-passes-pcap-join-flows-without-replay-fallbacks",
            passed ? "passed" : "incomplete",
            "TF2 profile can process the selected PCAP join families through semantic handlers without importing old HLE replay artifacts.",
            new
            {
                FilesWithCompleteHelloFlow = completeHello,
                RequiredScenarioFamilies,
                CoveredRequiredScenarioFamilies = coveredFamilies,
                GameManagerHello = helloEvidence.Evidence
            },
            passed
                ? Array.Empty<string>()
                : RequiredScenarioFamilies
                    .Except(coveredFamilies, StringComparer.Ordinal)
                    .Select(family => $"Add semantic PCAP/profile coverage for {family}.")
                    .Concat(helloEvidence.NextWork)
                    .ToArray());
    }

    private static (string Status, object Evidence, string[] NextWork) ReadHelloEvidence(JsonElement? root, int completeHello)
    {
        if (root is null)
        {
            return (
                "missing-evidence",
                new { Evidence = "No pcap-gamemanager-hello report was supplied." },
                new[] { "Run scripts/analyze-gamemanager-hello.sh so the acceptance gate can audit the GameManager hello/Source handoff boundary." });
        }

        var summary = root.Value.GetProperty("Summary");
        var active = summary.GetProperty("ActiveFlowCount").GetInt32();
        var decoded = summary.GetProperty("FirstSourceTransportDecodedCount").GetInt32();
        var flows = root.Value.GetProperty("Flows").EnumerateArray()
            .Where(static flow => flow.GetProperty("HasActiveFlow").GetBoolean())
            .ToArray();
        var fresh = flows.Where(static flow => flow.TryGetProperty("ClientSequence", out var seq)
            && seq.ValueKind == JsonValueKind.Number
            && seq.GetInt32() == 0).ToArray();
        var midSession = flows
            .Where(static flow => !flow.TryGetProperty("ClientSequence", out var seq)
                || seq.ValueKind != JsonValueKind.Number
                || seq.GetInt32() != 0)
            .Select(static flow => ReadString(flow, "File"))
            .ToArray();
        var immediateHandoff = flows.Count(static flow => flow.GetProperty("FirstSourceAfterServerHelloPacketGap").GetInt64() == 1);
        var freshDeltaPlusSix = fresh.Count(static flow => flow.TryGetProperty("SequenceDelta", out var delta)
            && delta.ValueKind == JsonValueKind.Number
            && delta.GetInt32() == 6);
        var freshSourceReusesServerSequence = fresh.Count(static flow => flow.GetProperty("FirstSourceSequenceEqualsServerHelloSequence").GetBoolean());
        var passed = active == completeHello
            && active > 0
            && decoded == active
            && immediateHandoff == active
            && fresh.Length > 0
            && freshDeltaPlusSix == fresh.Length
            && freshSourceReusesServerSequence == fresh.Length;

        return (
            passed ? "passed" : "incomplete",
            new
            {
                ActiveFlowCount = active,
                CompleteHelloFlowCount = completeHello,
                FreshHelloFlowCount = fresh.Length,
                MidSessionFlowCount = midSession.Length,
                MidSessionFiles = midSession,
                FirstSourceTransportDecodedCount = decoded,
                ImmediateSourceHandoffCount = immediateHandoff,
                FreshServerHelloSequenceDeltaPlusSixCount = freshDeltaPlusSix,
                FreshSourceSequenceEqualsServerHelloSequenceCount = freshSourceReusesServerSequence,
                UniqueClientHelloPrefix6Count = summary.GetProperty("UniqueClientHelloPrefix6Count").GetInt32(),
                UniqueServerHelloBodySignatureCount = summary.GetProperty("UniqueServerHelloBodySignatureCount").GetInt32(),
                SequenceDeltaDistribution = summary.GetProperty("SequenceDeltaDistribution"),
                FirstSourcePacketGapDistribution = summary.GetProperty("FirstSourcePacketGapDistribution")
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Refresh pcap-gamemanager-hello and confirm every complete hello flow has decoded first Source transport.",
                    "Confirm fresh hello flows use server sequence client+6 and immediate Source handoff."
                });
    }

    private static AcceptanceGate CustomCreatePreferenceGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("tf2-custom-create-preferences-drive-backend-provisioning-contract", "Run analyze-ea-text-pcaps to recover Theater create-server preferences from PCAPs.");
        }

        var summary = root.Value.GetProperty("Summary");
        var scenarioCount = summary.GetProperty("Tf2CreateScenarioFileCount").GetInt32();
        var joinHandoffCount = summary.GetProperty("Tf2CreateJoinHandoffCount").GetInt32();
        var keyNames = summary.GetProperty("Tf2GameDataKeyCounts").EnumerateArray()
            .Select(static key => key.GetProperty("Key").GetString() ?? "")
            .Where(static key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var mapCounts = summary.GetProperty("Tf2CreateMapCounts").EnumerateArray()
            .ToDictionary(
                static map => map.GetProperty("MapName").GetString() ?? "",
                static map => map.GetProperty("Count").GetInt32(),
                StringComparer.Ordinal);
        var scenarios = root.Value.GetProperty("Files").EnumerateArray()
            .SelectMany(static file => file.GetProperty("Tf2CreateScenarios").EnumerateArray())
            .ToArray();
        var requiredKeysPresent = RequiredTf2CreateKeys.All(keyNames.Contains);
        var hasDustbowl = mapCounts.GetValueOrDefault("cp_dustbowl") > 0;
        var hasTwoFort = mapCounts.GetValueOrDefault("ctf_2fort") > 0;
        var hasMaxPlayers = scenarios.Any(static scenario => ReadString(scenario, "MaxPlayers") == "16");
        var hasLowDuration = scenarios.Any(static scenario => ReadString(scenario, "Duration") == "Low");
        var hasTimeRules = scenarios.Any(static scenario => ReadString(scenario, "MaxGameTime") is "15" or "30");
        var hasRoundRules = scenarios.Any(static scenario => ReadString(scenario, "NumRounds") == "5");
        var sourceProfiles = scenarios
            .Where(static scenario => scenario.TryGetProperty("SourceLaunchProfile", out _))
            .Select(static scenario => scenario.GetProperty("SourceLaunchProfile"))
            .ToArray();
        var hasDustbowlSourceProfile = sourceProfiles.Any(static profile =>
            ReadString(profile, "MapName") == "cp_dustbowl"
            && ReadString(profile, "GameMode") == "control-point"
            && HasArgumentPair(profile, "+map", "cp_dustbowl")
            && HasArgumentPair(profile, "+maxplayers", "16")
            && HasArgumentPair(profile, "+mp_timelimit", "15")
            && HasArgumentPair(profile, "+mp_maxrounds", "5")
            && HasArgumentPair(profile, "+mp_autoteambalance", "1"));
        var hasTwoFortSourceProfile = sourceProfiles.Any(static profile =>
            ReadString(profile, "MapName") == "ctf_2fort"
            && ReadString(profile, "GameMode") == "capture-the-flag"
            && HasArgumentPair(profile, "+map", "ctf_2fort")
            && HasArgumentPair(profile, "+maxplayers", "16")
            && HasArgumentPair(profile, "+mp_timelimit", "30")
            && HasArgumentPair(profile, "+mp_maxrounds", "5")
            && HasArgumentPair(profile, "+tf_flag_caps_per_round", "3")
            && HasArgumentPair(profile, "+mp_autoteambalance", "1"));
        var passed = scenarioCount > 0
            && joinHandoffCount > 0
            && requiredKeysPresent
            && hasDustbowl
            && hasTwoFort
            && hasMaxPlayers
            && hasLowDuration
            && hasTimeRules
            && hasRoundRules
            && hasDustbowlSourceProfile
            && hasTwoFortSourceProfile;

        return new AcceptanceGate(
            "tf2-custom-create-preferences-drive-backend-provisioning-contract",
            passed ? "passed" : "incomplete",
            "PCAP Theater create-server fields recover the TF2 map/player/rule preferences that Arcadia passes to PlasmaGameManager and the Source backend when provisioning custom servers.",
            new
            {
                Tf2CreateScenarioFileCount = scenarioCount,
                Tf2CreateJoinHandoffCount = joinHandoffCount,
                RequiredCreateKeys = RequiredTf2CreateKeys,
                PresentRequiredCreateKeys = RequiredTf2CreateKeys.Where(keyNames.Contains).ToArray(),
                MissingRequiredCreateKeys = RequiredTf2CreateKeys.Except(keyNames, StringComparer.Ordinal).ToArray(),
                CreateMapCounts = mapCounts,
                HasDustbowlCreate = hasDustbowl,
                HasTwoFortCreate = hasTwoFort,
                HasMaxPlayers16 = hasMaxPlayers,
                HasLowDuration = hasLowDuration,
                HasMaxGameTimeRules = hasTimeRules,
                HasNumRounds5 = hasRoundRules,
                SourceLaunchProfileCount = sourceProfiles.Length,
                HasDustbowlSourceProfile = hasDustbowlSourceProfile,
                HasTwoFortSourceProfile = hasTwoFortSourceProfile
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Recover every TF2 custom-create preference from GDAT/GDET PCAP text.",
                    "Keep Arcadia provisioning tests aligned so those recovered fields reach PLASMA_GAME_* and TF2PS3_DEDICATED_* launch environments.",
                    "Verify generated Source launch profiles include map/maxplayers/timelimit/round/autobalance and CTF flag-cap cvars."
                });
    }

    private static bool HasArgumentPair(JsonElement profile, string key, string value)
    {
        if (!profile.TryGetProperty("SourceArguments", out var arguments))
        {
            return false;
        }

        var values = arguments.EnumerateArray()
            .Select(static argument => argument.GetString() ?? "")
            .ToArray();
        for (var index = 0; index < values.Length - 1; index++)
        {
            if (values[index] == key && values[index + 1] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static AcceptanceGate LiveRpcs3Gate(JsonElement? root)
    {
        if (root is not null)
        {
            var gateStatus = root.Value.GetProperty("GateStatus").GetString() ?? "missing-evidence";
            var hasSourceHandoff = root.Value.GetProperty("HasSourceHandoffEvent").GetBoolean();
            var hasSourceMotd = root.Value.GetProperty("HasSourceMotdTraffic").GetBoolean();
            var sourceEvidence = root.Value.GetProperty("SourceEvidence").EnumerateArray().ToArray();
            var gameManagerEvents = root.Value.GetProperty("GameManagerEvents");
            var hasNativeSourceEvidence = HasNativeSourceEvidence(sourceEvidence)
                || HasGeneratedNativeSourceEvidence(gameManagerEvents);
            var highestPhase = HighestLiveSourcePhase(sourceEvidence, gameManagerEvents);
            var hasSteadyGameplay = sourceEvidence.Any(static evidence =>
                evidence.TryGetProperty("HasSteadyGameplayTraffic", out var value)
                && value.ValueKind == JsonValueKind.True)
                || HasGeneratedSteadyGameplayEvidence(gameManagerEvents);
            var status = gateStatus == "passed" && !hasNativeSourceEvidence ? "incomplete" : gateStatus;
            return new AcceptanceGate(
                "live-rpcs3-progresses-past-gamemanager-into-source-motd",
                status,
                "Next live RPCS3 test shows the TF2 PS3 client progressing past GameManager into Source/MOTD traffic.",
                new
                {
                    HasSourceHandoffEvent = hasSourceHandoff,
                    HasSourceMotdTraffic = hasSourceMotd,
                    HasNativeSourceEvidence = hasNativeSourceEvidence,
                    GameManagerEvents = gameManagerEvents,
                    SourceEvidence = root.Value.GetProperty("SourceEvidence"),
                    HighestSourceSessionPhase = highestPhase,
                    HasSteadyGameplayTraffic = hasSteadyGameplay,
                    HasGeneratedTerminalObjectStreamBootstrap = ReadBool(gameManagerEvents, "HasGeneratedTerminalObjectStreamBootstrap"),
                    GeneratedTerminalObjectStreamBootstrapCount = ReadInt(gameManagerEvents, "GeneratedTerminalObjectStreamBootstrapCount"),
                    MaxGeneratedSourcePayloadLength = ReadInt(gameManagerEvents, "MaxGeneratedSourcePayloadLength"),
                    OversizedGeneratedSourcePayloadCount = ReadInt(gameManagerEvents, "OversizedGeneratedSourcePayloadCount"),
                    MissingReasons = root.Value.GetProperty("MissingReasons")
                },
                status == "passed"
                    ? Array.Empty<string>()
                    : gateStatus == "passed" && !hasNativeSourceEvidence
                        ? new[] { "Repeat the live RPCS3 run with the default generated native Source responder; replay-only Source evidence is not final native backend proof." }
                        : root.Value.GetProperty("MissingReasons").EnumerateArray()
                            .Select(static reason => reason.GetString() ?? "")
                            .Where(static reason => reason.Length > 0)
                            .ToArray());
        }

        return new AcceptanceGate(
            "live-rpcs3-progresses-past-gamemanager-into-source-motd",
            "missing-evidence",
            "Next live RPCS3 test shows the TF2 PS3 client progressing past GameManager into Source/MOTD traffic.",
            new
            {
                Evidence = "No current live RPCS3 log in this project proves Source/MOTD handoff after the native PlasmaGameManager profile."
            },
            new[]
            {
                "Run a live RPCS3 test with Arcadia pointing EGEG to PlasmaGameManager and PLASMA_EVIDENCE_LOG capturing GameManager events.",
                "Confirm the evidence log contains a source-handoff event and the live capture then shows Source/MOTD traffic."
            });
    }

    private static AcceptanceGate SourceGameplayPhaseGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("pcap-long-source-gameplay-phases-cover-2fort-and-dustbowl", "Run analyze-source-gameplay-phases to produce long Source gameplay phase evidence.");
        }

        var summary = root.Value.GetProperty("Summary");
        var activeSourceFlows = summary.GetProperty("ActiveSourceFlowCount").GetInt32();
        var longGameplaySessions = summary.GetProperty("LongGameplaySessionCount").GetInt32();
        var steadyWindowFiles = summary.GetProperty("FilesWithSteadyGameplayWindowCount").GetInt32();
        var sourcePacketCount = summary.GetProperty("SourcePacketCount").GetInt32();
        var files = root.Value.GetProperty("Files").EnumerateArray().ToArray();
        var twoFort = files.FirstOrDefault(static file => ReadString(file, "File") == "2Fort.PCAPNG");
        var dustbowl = files.FirstOrDefault(static file => ReadString(file, "File") == "dustbowl_final.PCAPNG");
        var twoFortPassed = IsLongGameplayPhaseFile(twoFort, minimumPackets: 2_000, minimumDurationMilliseconds: 120_000);
        var dustbowlPassed = IsLongGameplayPhaseFile(dustbowl, minimumPackets: 3_000, minimumDurationMilliseconds: 250_000);
        var passed = activeSourceFlows >= 16
            && longGameplaySessions >= 13
            && steadyWindowFiles >= 14
            && sourcePacketCount >= 70_000
            && twoFortPassed
            && dustbowlPassed;

        return new AcceptanceGate(
            "pcap-long-source-gameplay-phases-cover-2fort-and-dustbowl",
            passed ? "passed" : "incomplete",
            "Long 2Fort and Dustbowl PCAPs are segmented into Source handoff setup, loading/MOTD-transfer, and steady gameplay traffic windows.",
            new
            {
                ActiveSourceFlowCount = activeSourceFlows,
                LongGameplaySessionCount = longGameplaySessions,
                FilesWithSteadyGameplayWindowCount = steadyWindowFiles,
                SourcePacketCount = sourcePacketCount,
                TwoFort = SourceGameplayPhaseEvidence(twoFort),
                Dustbowl = SourceGameplayPhaseEvidence(dustbowl)
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Recover/refresh long Source gameplay phase evidence for 2Fort.PCAPNG and dustbowl_final.PCAPNG.",
                    "Keep the phase model able to distinguish setup, loading/MOTD-transfer, and steady gameplay traffic."
                });
    }

    private static AcceptanceGate ServerDllSimulationContractGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("eatf2-serverdll-simulation-contract-defines-native-source-runtime", "Run scripts/reduce-eatf2-serverdll-simulation.sh to reduce official EA server.dll simulation evidence.");
        }

        var summary = root.Value.GetProperty("Summary");
        var areaCount = summary.GetProperty("AreaCount").GetInt32();
        var areasPresent = summary.GetProperty("AreasPresentInOfficialDll").GetInt32();
        var markerCount = summary.GetProperty("MarkerCount").GetInt32();
        var markersPresent = summary.GetProperty("MarkersPresentInOfficialDll").GetInt32();
        var missingMarkers = summary.GetProperty("MissingOfficialMarkerCount").GetInt32();
        var fieldCount = summary.GetProperty("NativeStateFieldCount").GetInt32();
        var fieldsPresent = summary.GetProperty("NativeStateFieldsPresentInGeneratedNativeContract").GetInt32();
        var missingFields = summary.GetProperty("MissingGeneratedNativeFieldCount").GetInt32();
        var areaIds = root.Value.GetProperty("Areas").EnumerateArray()
            .Select(static area => ReadString(area, "Id"))
            .Where(static id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var coveredRequiredAreas = RequiredServerDllSimulationAreas.Where(areaIds.Contains).ToArray();
        var passed = areaCount >= RequiredServerDllSimulationAreas.Length
            && areasPresent == areaCount
            && markersPresent == markerCount
            && missingMarkers == 0
            && fieldsPresent == fieldCount
            && missingFields == 0
            && coveredRequiredAreas.Length == RequiredServerDllSimulationAreas.Length;

        return new AcceptanceGate(
            "eatf2-serverdll-simulation-contract-defines-native-source-runtime",
            passed ? "passed" : "incomplete",
            "Official EA TF2 PS3 server.dll evidence defines the native Source runtime loop and generated PS3 snapshot state fields required by the replacement backend.",
            new
            {
                AreaCount = areaCount,
                AreasPresentInOfficialDll = areasPresent,
                MarkerCount = markerCount,
                MarkersPresentInOfficialDll = markersPresent,
                MissingOfficialMarkerCount = missingMarkers,
                NativeStateFieldCount = fieldCount,
                NativeStateFieldsPresentInGeneratedNativeContract = fieldsPresent,
                MissingGeneratedNativeFieldCount = missingFields,
                RequiredAreas = RequiredServerDllSimulationAreas,
                CoveredRequiredAreas = coveredRequiredAreas
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Complete official EA server.dll runtime marker coverage for lifecycle, usercmd simulation, round rules, objectives, combat, buildings, and ranked stats.",
                    "Map any missing generated native fields into TF.elf PS3 Source snapshots before treating the backend as native-complete."
                });
    }

    private static AcceptanceGate ServerDllNativeObligationsGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("eatf2-serverdll-native-obligations-complete", "Run scripts/reduce-eatf2-serverdll-native-obligations.sh to audit official EA server.dll obligations against current native implementation.");
        }

        var summary = root.Value.GetProperty("Summary");
        var obligationCount = summary.GetProperty("ObligationCount").GetInt32();
        var officialEvidence = summary.GetProperty("ObligationsWithOfficialEvidence").GetInt32();
        var implementationEvidence = summary.GetProperty("ObligationsWithImplementationEvidence").GetInt32();
        var implementationScaffolded = ReadInt(summary, "ImplementationScaffoldedObligationCount");
        var liveProven = ReadInt(summary, "LiveProvenObligationCount");
        var nativeComplete = summary.GetProperty("NativeCompleteObligationCount").GetInt32();
        var incomplete = summary.GetProperty("IncompleteObligationCount").GetInt32();
        var stillNeedingLiveProof = ReadInt(summary, "ObligationsStillNeedingLiveProofCount");
        var stillNeedingFullSimulation = ReadInt(summary, "ObligationsStillNeedingFullSimulationCount");
        var missingOfficial = summary.GetProperty("MissingOfficialMarkerCount").GetInt32();
        var missingImplementation = summary.GetProperty("MissingImplementationMarkerCount").GetInt32();
        var incompleteObligations = root.Value.GetProperty("IncompleteObligations").EnumerateArray()
            .Select(static item => item.GetString() ?? "")
            .Where(static item => item.Length > 0)
            .ToArray();
        var passed = obligationCount > 0
            && officialEvidence == obligationCount
            && implementationEvidence == obligationCount
            && nativeComplete == obligationCount
            && incomplete == 0
            && missingOfficial == 0
            && missingImplementation == 0;

        return new AcceptanceGate(
            "eatf2-serverdll-native-obligations-complete",
            passed ? "passed" : "incomplete",
            "Official EA TF2 PS3 server.dll obligations have corresponding native implementation, not just recovered symbols or generated packet vocabulary.",
            new
            {
                ObligationCount = obligationCount,
                ObligationsWithOfficialEvidence = officialEvidence,
                ObligationsWithImplementationEvidence = implementationEvidence,
                ImplementationScaffoldedObligationCount = implementationScaffolded,
                LiveProvenObligationCount = liveProven,
                NativeCompleteObligationCount = nativeComplete,
                IncompleteObligationCount = incomplete,
                ObligationsStillNeedingLiveProofCount = stillNeedingLiveProof,
                ObligationsStillNeedingFullSimulationCount = stillNeedingFullSimulation,
                MissingOfficialMarkerCount = missingOfficial,
                MissingImplementationMarkerCount = missingImplementation,
                IncompleteObligations = incompleteObligations
            },
            passed
                ? Array.Empty<string>()
                : incompleteObligations
                    .Select(static obligation => $"Complete native EA server.dll obligation: {obligation}.")
                    .Append(stillNeedingFullSimulation > 0
                        ? "Finish the native simulation blockers listed in re/tf2ps3/eatf2-serverdll-native-obligations.json before marking server.dll obligations complete."
                        : "Prove all scaffolded obligations in live RPCS3 steady gameplay before marking server.dll obligations complete.")
                    .ToArray());
    }

    private static AcceptanceGate SourceLoadingFrameDebtGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("tf2ps3-source-loading-mapload-has-no-fake-payloads", "Run scripts/reduce-tf2ps3-source-loading-frame-debt.sh so acceptance can audit generated loading/map-load packet debt.");
        }

        var summary = root.Value.GetProperty("Summary");
        var frameCount = summary.GetProperty("FrameCount").GetInt32();
        var productionFake = summary.GetProperty("ProductionFakeFrameCount").GetInt32();
        var nativeRecordGeneratedPrefix = summary.GetProperty("NativeRecordWithGeneratedPrefixFrameCount").GetInt32();
        var nativeRecordPrefixDebtBytes = ReadInt(summary, "NativeRecordPrefixDebtBytes");
        var nativeRecordTrailingDebtBytes = ReadInt(summary, "NativeRecordTrailingDebtBytes");
        var nativeRecordVisibleRecordBytes = ReadInt(summary, "NativeRecordVisibleRecordBytes");
        var steadyPaddingRisk = summary.GetProperty("SteadyNativeSnapshotPaddingRiskCount").GetInt32();
        var staticTemplates = summary.GetProperty("StaticHexTemplateCount").GetInt32();
        var generatedQueuedPrefixDebtBytes = summary.TryGetProperty("GeneratedQueuedPrefixDebtBytes", out var generatedPrefix)
            ? generatedPrefix.GetInt32()
            : 0;
        var queuedOpaquePrefixBytes = summary.GetProperty("QueuedOpaquePrefixBytes").GetInt32();
        var eaTunnelReferences = summary.GetProperty("EaTunnelReferenceCount").GetInt32();
        var eaTunnelNeighborhoodReferences = summary.GetProperty("EaTunnelNeighborhoodReferenceCount").GetInt32();
        var eaTunnelReachedFunctions = summary.GetProperty("EaTunnelReachedFunctionCount").GetInt32();
        var passed = frameCount > 0
            && productionFake == 0
            && nativeRecordGeneratedPrefix == 0
            && nativeRecordPrefixDebtBytes == 0
            && nativeRecordTrailingDebtBytes == 0
            && generatedQueuedPrefixDebtBytes == 0
            && steadyPaddingRisk == 0
            && staticTemplates == 0;

        return new AcceptanceGate(
            "tf2ps3-source-loading-mapload-has-no-fake-payloads",
            passed ? "passed" : "incomplete",
            "TF2 PS3 loading/map-load Source packets are generated from named native TF.elf/server.dll fields, not captured/static templates, deterministic filler, generated PlayerStateLink prefix/trailing stream bytes, or padded snapshots.",
            new
            {
                FrameCount = frameCount,
                EarlyLoadingFrameCount = summary.GetProperty("EarlyLoadingFrameCount").GetInt32(),
                SteadyFrameCount = summary.GetProperty("SteadyFrameCount").GetInt32(),
                ProductionFakeFrameCount = productionFake,
                NativeRecordWithGeneratedPrefixFrameCount = nativeRecordGeneratedPrefix,
                NativeRecordPrefixDebtBytes = nativeRecordPrefixDebtBytes,
                NativeRecordTrailingDebtBytes = nativeRecordTrailingDebtBytes,
                NativeRecordVisibleRecordBytes = nativeRecordVisibleRecordBytes,
                GeneratedQueuedPrefixDebtBytes = generatedQueuedPrefixDebtBytes,
                SteadyNativeSnapshotPaddingRiskCount = steadyPaddingRisk,
                BlockingByteCount = summary.GetProperty("BlockingByteCount").GetInt32(),
                StaticHexTemplateCount = staticTemplates,
                QueuedOpaquePrefixBytes = queuedOpaquePrefixBytes,
                EaTunnelReferenceCount = eaTunnelReferences,
                EaTunnelNeighborhoodReferenceCount = eaTunnelNeighborhoodReferences,
                EaTunnelReachedFunctionCount = eaTunnelReachedFunctions
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Replace queued-boundary placeholder loading payloads with exact native TF.elf/server.dll queued Source field writers.",
                    "Replace generated PlayerStateLink prefix/trailing bytes with the queued-prefix/control writer around native PNG records.",
                    "Replace generated queued-prefix bodies with the native TF.elf peer-channel prefix/control writer.",
                    steadyPaddingRisk == 0
                        ? "Keep steady snapshot/entity-delta output unpadded while replacing the remaining high-entropy and prefix debt."
                        : "Remove PadNativeWrappedPayload from steady snapshot/entity-delta production output by recovering exact native body sizing and field layout.",
                    "Eliminate static hex templates before treating map-load as fully native.",
                    "Keep EA Tunnel out of the packet-owner path unless future server.dll evidence finds executable xrefs."
                });
    }

    private static AcceptanceGate SourceConnectedWrapperContractGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing(
                "tf2ps3-connected-wrapper-boundary-recovered",
                "Run scripts/reduce-tf2ps3-source-connected-wrapper-boundary.sh so acceptance can audit the connected Source frame grammar.");
        }

        var summary = root.Value.GetProperty("Summary");
        var connectedSocketPath = summary.GetProperty("ConnectedSocketObjectPathProven").GetBoolean();
        var attachedFrameReader = summary.GetProperty("AttachedFrameReaderContractProven").GetBoolean();
        var recoveredFrameKinds = summary.GetProperty("RecoveredAttachedFrameKindCount").GetInt32();
        var siblingBitreader = summary.GetProperty("SiblingBitreaderWrapperRecovered").GetBoolean();
        var hardOpaqueMarkerless = summary.GetProperty("HardOpaqueMarkerlessPacketCount").GetInt32();
        var directHardBoundary = summary.GetProperty("DirectHardOpaqueBoundaryProven").GetBoolean();
        var nativeMarkerlessReady = summary.GetProperty("NativeMarkerlessBoundaryReady").GetBoolean();
        var passed = connectedSocketPath
            && attachedFrameReader
            && recoveredFrameKinds == 4
            && siblingBitreader
            && hardOpaqueMarkerless > 0;

        return new AcceptanceGate(
            "tf2ps3-connected-wrapper-boundary-recovered",
            passed ? "passed" : "incomplete",
            "TF.elf connected Source wrapper grammar is recovered far enough to distinguish visible attached-frame traffic from the unresolved hard markerless body family.",
            new
            {
                HardOpaqueMarkerlessPacketCount = hardOpaqueMarkerless,
                ConnectedSocketObjectPathProven = connectedSocketPath,
                AttachedFrameReaderContractProven = attachedFrameReader,
                RecoveredAttachedFrameKindCount = recoveredFrameKinds,
                SiblingBitreaderWrapperRecovered = siblingBitreader,
                DirectHardOpaqueBoundaryProven = directHardBoundary,
                NativeMarkerlessBoundaryReady = nativeMarkerlessReady
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Recover the connected socket object path, all attached frame kinds, and sibling bitreader evidence before relying on Source wrapper semantics."
                });
    }

    private static AcceptanceGate SourceBitstreamPrimitiveContractGate(JsonElement? fieldRoot, JsonElement? helperRoot)
    {
        if (fieldRoot is null || helperRoot is null)
        {
            return Missing(
                "tf2ps3-slot70-callee-and-bitstream-contract-recovered",
                "Run scripts/reduce-tf2ps3-source-slot70-param2-field-contract.sh and scripts/reduce-tf2ps3-source-bitstream-helper-contract.sh.");
        }

        var fieldSummary = fieldRoot.Value.GetProperty("Summary");
        var helperSummary = helperRoot.Value.GetProperty("Summary");
        var fieldCount = fieldSummary.GetProperty("FieldCount").GetInt32();
        var recoveredFields = fieldSummary.GetProperty("RecoveredFieldCount").GetInt32();
        var operationCount = fieldSummary.GetProperty("OperationCount").GetInt32();
        var recoveredOperations = fieldSummary.GetProperty("RecoveredOperationCount").GetInt32();
        var missingRequiredEvidence = fieldSummary.GetProperty("RequiredEvidenceMissingCount").GetInt32();
        var primitiveCount = helperSummary.GetProperty("PrimitiveCount").GetInt32();
        var recoveredPrimitives = helperSummary.GetProperty("RecoveredPrimitiveCount").GetInt32();
        var addressStateHelpers = helperSummary.GetProperty("AddressStateHelperCount").GetInt32();
        var recoveredAddressStateHelpers = helperSummary.GetProperty("RecoveredAddressStateHelperCount").GetInt32();
        var wrapperLinks = helperSummary.GetProperty("WrapperLinkCount").GetInt32();
        var recoveredWrapperLinks = helperSummary.GetProperty("RecoveredWrapperLinkCount").GetInt32();
        var callerSideBuilder = helperSummary.GetProperty("CallerSideParam2BuilderRecovered").GetBoolean();
        var nativeSourceInputReady = helperSummary.GetProperty("NativeSourceInputReady").GetBoolean();
        var passed = fieldCount > 0
            && recoveredFields == fieldCount
            && recoveredOperations == operationCount
            && missingRequiredEvidence == 0
            && recoveredPrimitives == primitiveCount
            && recoveredAddressStateHelpers == addressStateHelpers
            && recoveredWrapperLinks == wrapperLinks;

        return new AcceptanceGate(
            "tf2ps3-slot70-callee-and-bitstream-contract-recovered",
            passed ? "passed" : "incomplete",
            "TF.elf slot +0x70 callee-side field layout and reusable bitstream/address helper contracts are recovered.",
            new
            {
                FieldCount = fieldCount,
                RecoveredFieldCount = recoveredFields,
                OperationCount = operationCount,
                RecoveredOperationCount = recoveredOperations,
                RequiredEvidenceMissingCount = missingRequiredEvidence,
                PrimitiveCount = primitiveCount,
                RecoveredPrimitiveCount = recoveredPrimitives,
                AddressStateHelperCount = addressStateHelpers,
                RecoveredAddressStateHelperCount = recoveredAddressStateHelpers,
                WrapperLinkCount = wrapperLinks,
                RecoveredWrapperLinkCount = recoveredWrapperLinks,
                CallerSideParam2BuilderRecovered = callerSideBuilder,
                NativeSourceInputReady = nativeSourceInputReady
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    "Recover every slot +0x70 callee field and bitstream/address helper primitive before treating the native Source reader contract as stable."
                });
    }

    private static AcceptanceGate SourceMarkerlessInputBoundaryGate(
        JsonElement? connectedRoot,
        JsonElement? builderRoot,
        JsonElement? helperRoot,
        JsonElement? markerlessBuilderRoot,
        JsonElement? payloadObjectDispatchRoot,
        JsonElement? ownerControlSubobjectRoot,
        JsonElement? ownerForwardContextRoot,
        JsonElement? ownerForwarderBitstreamCoverageRoot,
        JsonElement? ownerForwardWrapperVariantsRoot,
        JsonElement? category5UsercmdHandlerRoot,
        JsonElement? sourceClientInputCoverageRoot,
        JsonElement? sourceClientBoundaryProbeRoot,
        JsonElement? sourceMarkerlessTransformProbeRoot,
        JsonElement? sourceUsercmdQueueRecordRoot,
        JsonElement? sourceOwnerCallbackDispatchRoot,
        JsonElement? sourceHelperSliceReceiveSiblingsRoot,
        JsonElement? sourceRecvBitreaderCensusRoot,
        JsonElement? sourceEmbeddedClcMoveCandidatesRoot,
        JsonElement? sourceMarkerlessSessionKeyProbeRoot,
        JsonElement? sourceUsercmdQueueDeltaTailRoot,
        JsonElement? sourceRawUdpControlProbeRoot,
        JsonElement? sourcePayloadObjectFirstWordRoot,
        JsonElement? sourceNativeAssociationDescriptorScanRoot,
        JsonElement? sourceAssociatedObjectTokenContractRoot,
        JsonElement? pcapSourceAssociatedObjectTokensRoot,
        JsonElement? sourceAssociatedObjectSlot90Root,
        JsonElement? associatedObjectTokenTransformProbeRoot,
        JsonElement? sourceReliablePeerAttachRoot,
        JsonElement? sourceAssociatedSlotAcProvenanceRoot,
        JsonElement? sourceAssociatedLaneRoleRoot)
    {
        if (connectedRoot is null || builderRoot is null || helperRoot is null)
        {
            return Missing(
                "tf2ps3-markerless-source-input-boundary-recovered",
                "Run connected-wrapper, slot70-param2-builder, bitstream-helper, and markerless-param2-builder reducers so acceptance can audit markerless input readiness.");
        }

        var connectedSummary = connectedRoot.Value.GetProperty("Summary");
        var builderSummary = builderRoot.Value.GetProperty("Summary");
        var helperSummary = helperRoot.Value.GetProperty("Summary");
        var markerlessBuilderSummary = markerlessBuilderRoot?.GetProperty("Summary");
        var payloadObjectDispatchSummary = payloadObjectDispatchRoot?.GetProperty("Summary");
        var ownerControlSubobjectSummary = ownerControlSubobjectRoot?.GetProperty("Summary");
        var ownerControlSlotMap = ownerControlSubobjectRoot?.GetProperty("SlotMap");
        var ownerForwardContextSummary = ownerForwardContextRoot?.GetProperty("Summary");
        var ownerForwarderBitstreamCoverageSummary = ownerForwarderBitstreamCoverageRoot?.GetProperty("Summary");
        var ownerForwardWrapperVariantsSummary = ownerForwardWrapperVariantsRoot?.GetProperty("Summary");
        var category5UsercmdHandlerSummary = category5UsercmdHandlerRoot?.GetProperty("Summary");
        var sourceClientInputCoverageSummary = sourceClientInputCoverageRoot?.GetProperty("Summary");
        var sourceClientBoundaryProbeSummary = sourceClientBoundaryProbeRoot?.GetProperty("Summary");
        var sourceMarkerlessTransformProbeSummary = sourceMarkerlessTransformProbeRoot?.GetProperty("Summary");
        var sourceUsercmdQueueRecordSummary = sourceUsercmdQueueRecordRoot?.GetProperty("Summary");
        var sourceOwnerCallbackDispatchSummary = sourceOwnerCallbackDispatchRoot?.GetProperty("Summary");
        var sourceHelperSliceReceiveSiblingsSummary = sourceHelperSliceReceiveSiblingsRoot?.GetProperty("Summary");
        var sourceRecvBitreaderCensusSummary = sourceRecvBitreaderCensusRoot?.GetProperty("Summary");
        var sourceEmbeddedClcMoveCandidatesSummary = sourceEmbeddedClcMoveCandidatesRoot?.GetProperty("Summary");
        var sourceMarkerlessSessionKeyProbeSummary = sourceMarkerlessSessionKeyProbeRoot?.GetProperty("Summary");
        var sourceUsercmdQueueDeltaTailSummary = sourceUsercmdQueueDeltaTailRoot?.GetProperty("Summary");
        var sourceRawUdpControlProbeSummary = sourceRawUdpControlProbeRoot?.GetProperty("Summary");
        var sourcePayloadObjectFirstWordSummary = sourcePayloadObjectFirstWordRoot?.GetProperty("Summary");
        var sourceNativeAssociationDescriptorScanSummary = sourceNativeAssociationDescriptorScanRoot?.GetProperty("Summary");
        var sourceAssociatedObjectTokenContractSummary = sourceAssociatedObjectTokenContractRoot?.GetProperty("Summary");
        var pcapSourceAssociatedObjectTokensSummary = pcapSourceAssociatedObjectTokensRoot?.GetProperty("Summary");
        var sourceAssociatedObjectSlot90Summary = sourceAssociatedObjectSlot90Root?.GetProperty("Summary");
        var associatedObjectTokenTransformProbeSummary = associatedObjectTokenTransformProbeRoot?.GetProperty("Summary");
        var sourceReliablePeerAttachSummary = sourceReliablePeerAttachRoot?.GetProperty("Summary");
        var sourceAssociatedSlotAcProvenanceSummary = sourceAssociatedSlotAcProvenanceRoot?.GetProperty("Summary");
        var sourceAssociatedLaneRoleSummary = sourceAssociatedLaneRoleRoot?.GetProperty("Summary");
        var sourceAssociatedObjectSlot90StateContract =
            sourceAssociatedObjectSlot90Root is not null
            && sourceAssociatedObjectSlot90Root.Value.TryGetProperty("StateContract", out var stateContractElement)
                ? stateContractElement
                : (JsonElement?)null;
        var sourceAssociatedObjectSlot90RegisterContract =
            sourceAssociatedObjectSlot90Root is not null
            && sourceAssociatedObjectSlot90Root.Value.TryGetProperty("RegisterContract", out var registerContractElement)
                ? registerContractElement
                : (JsonElement?)null;
        var sourceAssociatedObjectSlot90OutputBuilderCensus =
            sourceAssociatedObjectSlot90Root is not null
            && sourceAssociatedObjectSlot90Root.Value.TryGetProperty("OutputBuilderRegisterCensus", out var outputBuilderCensusElement)
                ? outputBuilderCensusElement
                : (JsonElement?)null;
        var hardOpaqueMarkerless = connectedSummary.GetProperty("HardOpaqueMarkerlessPacketCount").GetInt32();
        var directHardBoundary = connectedSummary.GetProperty("DirectHardOpaqueBoundaryProven").GetBoolean();
        var nativeMarkerlessReady = connectedSummary.GetProperty("NativeMarkerlessBoundaryReady").GetBoolean();
        var param2ConstructionSite = builderSummary.GetProperty("Param2ConstructionSiteRecovered").GetBoolean();
        var builderNativeReady = builderSummary.GetProperty("NativeSourceInputReady").GetBoolean();
        var callerSideParam2Builder = helperSummary.GetProperty("CallerSideParam2BuilderRecovered").GetBoolean();
        var helperNativeReady = helperSummary.GetProperty("NativeSourceInputReady").GetBoolean();
        var payloadObjectBuilderRecovered = ReadBool(markerlessBuilderSummary, "PayloadObjectBuilderRecovered");
        var markerlessRecvFillRecovered = ReadBool(markerlessBuilderSummary, "MarkerlessRecvFillRecovered");
        var fragmentReassemblyRecovered = ReadBool(markerlessBuilderSummary, "FragmentReassemblyRecovered");
        var drainDispatchLoopRecovered = ReadBool(markerlessBuilderSummary, "DrainDispatchLoopRecovered");
        var concretePayloadObjectBoundary = ReadBool(markerlessBuilderSummary, "ConcretePayloadObjectBoundaryRecovered");
        var payloadObjectFirstWordRecovered = ReadBool(payloadObjectDispatchSummary, "PayloadFirstWordDispatchRecovered");
        var payloadObjectDispatchCallsites = ReadInt(payloadObjectDispatchSummary, "DispatchCallsiteCount");
        var ownerSubobjectArgumentRecovered = ReadBool(payloadObjectDispatchSummary, "OwnerSubobjectArgumentRecovered");
        var associatedSlot90DispatchRecovered = ReadBool(payloadObjectDispatchSummary, "AssociatedSlot90DispatchRecovered");
        var ownerSlot8DispatchRecovered = ReadBool(payloadObjectDispatchSummary, "OwnerBitreaderDispatchRecovered");
        var payloadObjectFilterFinalizerRecovered =
            ReadBool(payloadObjectDispatchSummary, "PayloadFilterGateRecovered")
            && ReadBool(payloadObjectDispatchSummary, "PayloadFinalizerRecovered");
        var payloadObjectDispatchOpenGates = ReadInt(payloadObjectDispatchSummary, "OpenGateCount");
        var nativeAssociatedSlot90HandlerImplemented = ReadBool(payloadObjectDispatchSummary, "NativeAssociatedSlot90HandlerImplemented");
        var nativeOwnerSlot8HandlerImplemented = ReadBool(payloadObjectDispatchSummary, "NativeOwnerSlot8HandlerImplemented");
        var payloadObjectDispatchNativeReady = ReadBool(payloadObjectDispatchSummary, "NativeSourceInputReady");
        var ownerControlSubobjectOwnerSlot8TargetRecovered = ReadBool(ownerControlSubobjectSummary, "OwnerSlot8TargetRecovered");
        var ownerControlSubobjectOwnerSlot8ForwarderRecovered = ReadBool(ownerControlSubobjectSummary, "OwnerSlot8ForwarderRecovered");
        var ownerControlSubobjectAssociatedSlot90CandidateRecovered = ReadBool(ownerControlSubobjectSummary, "AssociatedSlot90CandidateRecovered");
        var ownerControlSubobjectNativeOwnerSlot8ConsumerImplemented = ReadBool(ownerControlSubobjectSummary, "NativeOwnerSlot8ConsumerImplemented");
        var ownerControlSubobjectNativeAssociatedSlot90ConsumerImplemented = ReadBool(ownerControlSubobjectSummary, "NativeAssociatedSlot90ConsumerImplemented");
        var ownerControlSubobjectNativeReady = ReadBool(ownerControlSubobjectSummary, "NativeSourceInputReady");
        var ownerForwardContextFamilyRecovered = ReadBool(ownerForwardContextSummary, "OwnerForwarderFamilyRecovered");
        var ownerForwardContextCategory5Recovered = ReadBool(ownerForwardContextSummary, "Category5UsercmdRouteCandidateRecovered");
        var ownerForwardContextCategory5DynamicHandlerCandidate = ReadBool(ownerForwardContextSummary, "Context5PassedToDynamicHandler");
        var ownerForwardContextHandlersImplemented = ReadBool(ownerForwardContextSummary, "NativeOwnerForwardContextHandlersImplemented");
        var ownerForwardContextNativeReady = ReadBool(ownerForwardContextSummary, "NativeSourceInputReady");
        var ownerForwardContextOpenGates = ReadInt(ownerForwardContextSummary, "OpenGateCount");
        var ownerForwarderSlotCount = ReadInt(ownerForwardContextSummary, "OwnerForwarderSlotCount");
        var ownerForwarderWrappedCount = ReadInt(ownerForwarderBitstreamCoverageSummary, "WrappedForwarderCount");
        var ownerForwarderImplementedWrappedCount = ReadInt(ownerForwarderBitstreamCoverageSummary, "ImplementedWrappedForwarderCount");
        var ownerForwarderMissingWrappedCount = ReadInt(ownerForwarderBitstreamCoverageSummary, "MissingWrappedForwarderCount");
        var ownerForwarderWord5PrimitiveImplemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "Word5PrimitiveImplemented");
        var ownerForwarderWord5Implemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "Word5BoundaryImplemented");
        var ownerForwarderWord6PrimitiveImplemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "Word6PrimitiveImplemented");
        var ownerForwarderWord6Implemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "Word6BoundaryImplemented");
        var ownerForwarderDeferredWord6PrimitiveImplemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "DeferredPointerWord6PrimitiveImplemented");
        var ownerForwarderDeferredWord6Implemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "DeferredPointerWord6BoundaryImplemented");
        var ownerForwarderConfigWord4PrimitiveImplemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "ConfigFallbackWord4PrimitiveImplemented");
        var ownerForwarderConfigWord4Implemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "ConfigFallbackWord4BoundaryImplemented");
        var ownerForwarderSemanticContextHandlersImplemented = ReadBool(ownerForwarderBitstreamCoverageSummary, "SemanticContextHandlersImplemented");
        var ownerForwarderBitstreamImplementationReady = ReadBool(ownerForwarderBitstreamCoverageSummary, "ImplementationReady");
        var ownerForwarderBitstreamLiveMapLoadVerified = ReadBool(ownerForwarderBitstreamCoverageSummary, "LiveMapLoadVerified");
        var ownerForwarderBitstreamCoverageNativeReady = ReadBool(ownerForwarderBitstreamCoverageSummary, "NativeSourceInputReady");
        var ownerForwarderBitstreamCoverageOpenGates = ReadInt(ownerForwarderBitstreamCoverageSummary, "OpenGateCount");
        var ownerForwardWrapperVariantCount = ReadInt(ownerForwardWrapperVariantsSummary, "PrimaryWrapperCount");
        var ownerForwardWrapperVariantLocatedCount = ReadInt(ownerForwardWrapperVariantsSummary, "LocatedPrimaryWrapperCount");
        var ownerForwardWrapperVariantShapeRecoveredCount = ReadInt(ownerForwardWrapperVariantsSummary, "ShapeRecoveredPrimaryWrapperCount");
        var ownerForwardWrapperVariantImplementedOrNotRequiredCount = ReadInt(ownerForwardWrapperVariantsSummary, "ImplementedOrNotRequiredWrapperCount");
        var ownerForwardWrapperVariantMissingImplementationCount = ReadInt(ownerForwardWrapperVariantsSummary, "MissingImplementationCount");
        var ownerForwardWrapperVariantReady = ReadBool(ownerForwardWrapperVariantsSummary, "NativeWrapperVariantCoverageReady");
        var ownerForwardWrapperVariantLiveMapLoadVerified = ReadBool(ownerForwardWrapperVariantsSummary, "LiveMapLoadVerified");
        var category5RouteRecovered = ReadBool(category5UsercmdHandlerSummary, "Category5RouteRecovered");
        var category5HandlerTableCellRecovered = ReadBool(category5UsercmdHandlerSummary, "HandlerOpdTableCellRecovered");
        var category5CallConventionRecovered = ReadBool(category5UsercmdHandlerSummary, "HandlerCallConventionMatchesDispatchObject");
        var category5ClcMoveContractRecovered = ReadBool(category5UsercmdHandlerSummary, "ClcMoveReadContractRecovered");
        var category5ServerDllUsercmdDecoderRecovered = ReadBool(category5UsercmdHandlerSummary, "OfficialServerDllUsercmdDecoderRecovered");
        var category5GhidraHandlerChainConfirmed = ReadBool(category5UsercmdHandlerSummary, "GhidraHandlerChainConfirmed");
        var category5ExactVptrWriteRecovered = ReadBool(category5UsercmdHandlerSummary, "ExactCategory5VptrWriteRecovered");
        var category5NativeReady = ReadBool(category5UsercmdHandlerSummary, "NativeSourceInputReady");
        var category5OpenGates = ReadInt(category5UsercmdHandlerSummary, "OpenGateCount");
        var pcapSourceInputNativeReady = ReadBool(sourceClientInputCoverageSummary, "NativeSourceInputReady");
        var pcapSourceInputClientPackets = ReadInt(sourceClientInputCoverageSummary, "ClientSourcePacketCount");
        var pcapSourceInputNativeCovered = ReadInt(sourceClientInputCoverageSummary, "NativeCoveredClientPacketCount");
        var pcapSourceInputDecodedClcMove = ReadInt(sourceClientInputCoverageSummary, "DecodedClcMovePacketCount");
        var pcapSourceInputDecodedNetMessages = ReadInt(sourceClientInputCoverageSummary, "DecodedNetMessagePacketCount");
        var pcapSourceInputWeakNetMessageCandidates = ReadInt(sourceClientInputCoverageSummary, "WeakNetMessageCandidatePacketCount");
        var pcapSourceInputWeakNetMessageHardMarkerlessCandidates = ReadInt(sourceClientInputCoverageSummary, "WeakNetMessageCandidateHardMarkerlessPacketCount");
        var pcapSourceInputHardMarkerless = ReadInt(sourceClientInputCoverageSummary, "HardMarkerlessClientPacketCount");
        var pcapSourceInputHeuristicHardMarkerless = ReadInt(sourceClientInputCoverageSummary, "HeuristicOnlyHardMarkerlessClientPacketCount");
        var pcapSourceInputUnknownHardMarkerless = ReadInt(sourceClientInputCoverageSummary, "UnknownHardMarkerlessClientPacketCount");
        var pcapSourceInputUnknownPackets = ReadInt(sourceClientInputCoverageSummary, "UnknownPacketCount");
        var pcapSourceInputConclusion = ReadString(sourceClientInputCoverageSummary, "CoverageConclusion");
        var pcapBoundaryProbeNativeReady = ReadBool(sourceClientBoundaryProbeSummary, "NativeSourceInputReady");
        var pcapBoundaryProbeHardMarkerless = ReadInt(sourceClientBoundaryProbeSummary, "HardMarkerlessPacketCount");
        var pcapBoundaryProbeNativeDecoded = ReadInt(sourceClientBoundaryProbeSummary, "NativeDecodedPacketCount");
        var pcapBoundaryProbeNativeClcMove = ReadInt(sourceClientBoundaryProbeSummary, "NativeClcMovePacketCount");
        var pcapBoundaryProbeNativeNetMessage = ReadInt(sourceClientBoundaryProbeSummary, "NativeNetMessagePacketCount");
        var pcapBoundaryProbeWrapperMatched = ReadInt(sourceClientBoundaryProbeSummary, "WrapperLayoutMatchedPacketCount");
        var pcapBoundaryProbeEmbeddedOffsetMatched = ReadInt(sourceClientBoundaryProbeSummary, "EmbeddedOffsetLayoutMatchedPacketCount");
        var pcapBoundaryProbeEmbeddedOffsetOnlyMatched = ReadInt(sourceClientBoundaryProbeSummary, "EmbeddedOffsetOnlyLayoutMatchedPacketCount");
        var pcapBoundaryProbeWrapperMatchedButUndecoded = ReadInt(sourceClientBoundaryProbeSummary, "WrapperLayoutMatchedButUndecodedPacketCount");
        var pcapBoundaryProbeNoRecoveredWrapper = ReadInt(sourceClientBoundaryProbeSummary, "NoRecoveredWrapperLayoutPacketCount");
        var pcapBoundaryProbeMatchedLayoutProbeCount = ReadInt(sourceClientBoundaryProbeSummary, "MatchedLayoutProbeCount");
        var pcapBoundaryProbeEmbeddedOffsetProbeCount = ReadInt(sourceClientBoundaryProbeSummary, "EmbeddedOffsetProbeCount");
        var pcapBoundaryProbeConclusion = ReadString(sourceClientBoundaryProbeSummary, "Conclusion");
        var markerlessTransformNativeReady = ReadBool(sourceMarkerlessTransformProbeSummary, "NativeMarkerlessTransformReady");
        var markerlessTransformHardMarkerless = ReadInt(sourceMarkerlessTransformProbeSummary, "HardMarkerlessPacketCount");
        var markerlessTransformStrictHits = ReadInt(sourceMarkerlessTransformProbeSummary, "StrictTransformHitCount");
        var markerlessTransformPacketHits = ReadInt(sourceMarkerlessTransformProbeSummary, "PacketWithStrictTransformHitCount");
        var markerlessTransformClcMoveHits = ReadInt(sourceMarkerlessTransformProbeSummary, "ClcMoveTransformHitCount");
        var markerlessTransformNetMessageHits = ReadInt(sourceMarkerlessTransformProbeSummary, "NetMessageTransformHitCount");
        var markerlessTransformConclusion = ReadString(sourceMarkerlessTransformProbeSummary, "Conclusion");
        var sessionKeyProbeNativeReady = ReadBool(sourceMarkerlessSessionKeyProbeSummary, "NativeMarkerlessTransformReady");
        var sessionKeyProbeHardMarkerless = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "HardMarkerlessPacketCount");
        var sessionKeyProbeKeyedPackets = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "PacketsWithSessionKeys");
        var sessionKeyProbeStrictHits = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "StrictDecodeHitCount");
        var sessionKeyProbeClcHits = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "ClcMoveHitCount");
        var sessionKeyProbeNetHits = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "NetMessageHitCount");
        var sessionKeyProbeQueueHits = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "QueueDeltaExactHitCount");
        var sessionKeyProbePacketHits = ReadInt(sourceMarkerlessSessionKeyProbeSummary, "PacketWithAcceptedTransformHitCount");
        var sessionKeyProbeConclusion = ReadString(sourceMarkerlessSessionKeyProbeSummary, "Conclusion");
        var usercmdQueueRecordLayerRecovered = ReadBool(sourceUsercmdQueueRecordSummary, "QueueRecordLayerRecovered");
        var usercmdQueueRecordFanoutCallerRecovered = ReadBool(sourceUsercmdQueueRecordSummary, "QueueInsertFanoutCallerRecovered");
        var usercmdQueueRecordDeltaFieldCount = ReadInt(sourceUsercmdQueueRecordSummary, "DeltaFieldCount");
        var usercmdQueueRecordPcapProbeAvailable = ReadBool(sourceUsercmdQueueRecordSummary, "PcapProbeAvailable");
        var usercmdQueueRecordHardMarkerless = ReadInt(sourceUsercmdQueueRecordSummary, "PcapHardMarkerlessPacketCount");
        var usercmdQueueRecordRawRecordCandidates = ReadInt(sourceUsercmdQueueRecordSummary, "PcapRawRecordLengthCandidateCount");
        var usercmdQueueRecordRawRecordMultipleCandidates = ReadInt(sourceUsercmdQueueRecordSummary, "PcapRawRecordLengthMultipleCandidateCount");
        var usercmdQueueRecordNativeReady = ReadBool(sourceUsercmdQueueRecordSummary, "NativeSourceInputReady");
        var queueDeltaTailCandidateCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "CandidatePacketCount");
        var queueDeltaTailExactTwoRecordCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "ExactTwoRecordPacketCount");
        var queueDeltaTailNonZeroTrailingCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "DecodedWithNonZeroTrailingPacketCount");
        var queueDeltaTailExactZeroTrailingCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "ExactZeroTrailingPacketCount");
        var queueDeltaTailMostCommonSequenceDelta = ReadNullableInt(sourceUsercmdQueueDeltaTailSummary, "MostCommonSequenceDelta");
        var queueDeltaTailMostCommonSequenceDeltaCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "MostCommonSequenceDeltaCount");
        var queueDeltaTailDistinctConsumedBitCount = ReadInt(sourceUsercmdQueueDeltaTailSummary, "DistinctConsumedBitCount");
        var queueDeltaTailFixedTrailerRuledOut = ReadBool(sourceUsercmdQueueDeltaTailSummary, "FixedTrailerHypothesisRuledOut");
        var queueDeltaTailNativeReady = ReadBool(sourceUsercmdQueueDeltaTailSummary, "NativeBoundaryReady");
        var queueDeltaTailConclusion = ReadString(sourceUsercmdQueueDeltaTailSummary, "Conclusion");
        var ownerCallbackSlotCount = ReadInt(sourceOwnerCallbackDispatchSummary, "OwnerCallbackSlotCount");
        var ownerCallbackSlot70SequenceRecovered = ReadBool(sourceOwnerCallbackDispatchSummary, "Slot70WrapperSequenceRecovered");
        var ownerCallbackPathConstructsParam2 = ReadBool(sourceOwnerCallbackDispatchSummary, "OwnerCallbackPathConstructsParam2");
        var ownerCallbackConcreteMarkerlessEdge = ReadBool(sourceOwnerCallbackDispatchSummary, "ConcreteMarkerlessDataflowEdgeRecovered");
        var ownerCallbackNativeReady = ReadBool(sourceOwnerCallbackDispatchSummary, "NativeSourceInputReady");
        var ownerCallbackOpenGates = ReadInt(sourceOwnerCallbackDispatchSummary, "OpenGateCount");
        var helperReceiveSiblingCount = ReadInt(sourceHelperSliceReceiveSiblingsSummary, "ReceiveSiblingCount");
        var helperPresentReceiveSiblingCount = ReadInt(sourceHelperSliceReceiveSiblingsSummary, "PresentReceiveSiblingCount");
        var helperAttachedType2DispatchProven = ReadBool(sourceHelperSliceReceiveSiblingsSummary, "AttachedType2DispatchProven");
        var helperSlot70ConsumesCallerBitreader = ReadBool(sourceHelperSliceReceiveSiblingsSummary, "Slot70ConsumesCallerBitreader");
        var helperSlot70ReadsAttachedSocket = ReadBool(sourceHelperSliceReceiveSiblingsSummary, "Slot70ReadsAttachedSocket");
        var helperSiblingPathConstructsParam2 = ReadBool(sourceHelperSliceReceiveSiblingsSummary, "SiblingPathConstructsParam2");
        var helperSiblingNativeReady = ReadBool(sourceHelperSliceReceiveSiblingsSummary, "NativeSourceInputReady");
        var helperSiblingOpenGates = ReadInt(sourceHelperSliceReceiveSiblingsSummary, "OpenGateCount");
        var recvConnectedRecvCallers = ReadInt(sourceRecvBitreaderCensusSummary, "ConnectedRecvWrapperCallerCount");
        var recvConnectedRecvDispatcherUnique = ReadBool(sourceRecvBitreaderCensusSummary, "ConnectedRecvDispatcherUnique");
        var recvHardMarkerlessCandidateCount = ReadInt(sourceRecvBitreaderCensusSummary, "HardMarkerlessRecvBitreaderCandidateCount");
        var recvStrictPcapHardSetStillOpaque = ReadBool(sourceRecvBitreaderCensusSummary, "StrictPcapHardSetStillOpaque");
        var recvNativeReady = ReadBool(sourceRecvBitreaderCensusSummary, "NativeSourceInputReady");
        var recvOpenGates = ReadInt(sourceRecvBitreaderCensusSummary, "OpenGateCount");
        var rawUdpControlProbeRecovered = ReadBool(sourceRawUdpControlProbeSummary, "RawUdpControlProbeRecovered");
        var rawUdpControlProbeExcluded = ReadBool(sourceRawUdpControlProbeSummary, "ExcludedFromHardMarkerlessSourceInputBoundary");
        var rawUdpControlProbePcapCount = ReadInt(sourceRawUdpControlProbeSummary, "Pcap6e01ControlProbeCount");
        var rawUdpControlProbeNativeReady = ReadBool(sourceRawUdpControlProbeSummary, "NativeSourceInputReady");
        var reliablePeerAttachControlRecovered = ReadBool(sourceReliablePeerAttachSummary, "AttachControlRecovered");
        var reliablePeerAttachChainRecovered = ReadBool(sourceReliablePeerAttachSummary, "AttachChainRecovered");
        var reliablePeerAttachedFrameReaderRecovered = ReadBool(sourceReliablePeerAttachSummary, "AttachedFrameReaderRecovered");
        var reliablePeerSlot70ConsumesCallerBitreader = ReadBool(sourceReliablePeerAttachSummary, "Slot70ConsumesCallerBitreader");
        var reliablePeerAttachNativeReady = ReadBool(sourceReliablePeerAttachSummary, "NativeSourceInputReady");
        var reliablePeerAttachOpenGates = ReadInt(sourceReliablePeerAttachSummary, "OpenGateCount");
        var payloadObjectFirstWordHardMarkerless = ReadInt(sourcePayloadObjectFirstWordSummary, "HardMarkerlessPacketCount");
        var payloadObjectFirstWordDecoded = ReadInt(sourcePayloadObjectFirstWordSummary, "PayloadObjectFirstWordDecodedCount");
        var payloadObjectFirstWordOwnerSlot8 = ReadInt(sourcePayloadObjectFirstWordSummary, "OwnerSlot8Count");
        var payloadObjectFirstWordFragmented = ReadInt(sourcePayloadObjectFirstWordSummary, "FragmentedSpecialWrapperCount");
        var payloadObjectFirstWordRepacked = ReadInt(sourcePayloadObjectFirstWordSummary, "RepackedSpecialWrapperCount");
        var payloadObjectFirstWordAssociated = ReadInt(sourcePayloadObjectFirstWordSummary, "AssociatedObjectTokenCount");
        var payloadObjectFirstWordDistinctAssociated = ReadInt(sourcePayloadObjectFirstWordSummary, "DistinctAssociatedObjectTokenCount");
        var payloadObjectFirstWordNativeAssociationDescriptors = ReadInt(sourcePayloadObjectFirstWordSummary, "NativeAssociationDescriptorCount");
        var payloadObjectFirstWordDescriptorType1 = ReadInt(sourcePayloadObjectFirstWordSummary, "AssociationDescriptorType1Count");
        var payloadObjectFirstWordDescriptorType2 = ReadInt(sourcePayloadObjectFirstWordSummary, "AssociationDescriptorType2Count");
        var payloadObjectFirstWordDescriptorType3 = ReadInt(sourcePayloadObjectFirstWordSummary, "AssociationDescriptorType3Count");
        var payloadObjectFirstWordCoversCorpus = ReadBool(sourcePayloadObjectFirstWordSummary, "PayloadObjectFirstWordDispatchCoversHardMarkerlessCorpus");
        var payloadObjectFirstWordNativeReady = ReadBool(sourcePayloadObjectFirstWordSummary, "NativeSourceInputReady");
        var payloadObjectFirstWordConclusion = ReadString(sourcePayloadObjectFirstWordSummary, "Conclusion");
        var nativeAssociationDescriptorHardMarkerless = ReadInt(sourceNativeAssociationDescriptorScanSummary, "HardMarkerlessPacketCount");
        var nativeAssociationDescriptorBigEndianOffset0 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "BigEndianDescriptorAtOffset0PacketCount");
        var nativeAssociationDescriptorLittleEndianOffset0 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "LittleEndianDescriptorAtOffset0PacketCount");
        var nativeAssociationDescriptorBigEndianAnyOffset = ReadInt(sourceNativeAssociationDescriptorScanSummary, "BigEndianDescriptorAnyOffsetPacketCount");
        var nativeAssociationDescriptorLittleEndianAnyOffset = ReadInt(sourceNativeAssociationDescriptorScanSummary, "LittleEndianDescriptorAnyOffsetPacketCount");
        var nativeAssociationDescriptorRawBigEndianOffset0 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawBigEndianDescriptorAtOffset0PacketCount");
        var nativeAssociationDescriptorRawBigEndianOffset1 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawBigEndianDescriptorAtOffset1PacketCount");
        var nativeAssociationDescriptorRawBigEndianOffset2 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawBigEndianDescriptorAtOffset2PacketCount");
        var nativeAssociationDescriptorRawBigEndianAnyOffset = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawBigEndianDescriptorAnyOffsetPacketCount");
        var nativeAssociationDescriptorRawLittleEndianOffset0 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawLittleEndianDescriptorAtOffset0PacketCount");
        var nativeAssociationDescriptorRawLittleEndianOffset1 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawLittleEndianDescriptorAtOffset1PacketCount");
        var nativeAssociationDescriptorRawLittleEndianOffset2 = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawLittleEndianDescriptorAtOffset2PacketCount");
        var nativeAssociationDescriptorRawLittleEndianAnyOffset = ReadInt(sourceNativeAssociationDescriptorScanSummary, "RawLittleEndianDescriptorAnyOffsetPacketCount");
        var nativeAssociationDescriptorMostCommonOffset = ReadString(sourceNativeAssociationDescriptorScanSummary, "MostCommonBigEndianDescriptorOffset");
        var nativeAssociationDescriptorMostCommonOffsetCount = ReadInt(sourceNativeAssociationDescriptorScanSummary, "MostCommonBigEndianDescriptorOffsetCount");
        var nativeAssociationDescriptorBoundaryReady = ReadBool(sourceNativeAssociationDescriptorScanSummary, "NativeDescriptorBoundaryReady");
        var nativeAssociationDescriptorConclusion = ReadString(sourceNativeAssociationDescriptorScanSummary, "Conclusion");
        var associatedObjectTokenContractRecovered = ReadBool(sourceAssociatedObjectTokenContractSummary, "AssociatedObjectTokenContractRecovered");
        var associatedObjectTokenAcceptedPeerReaderRecovered = ReadBool(sourceAssociatedObjectTokenContractSummary, "AcceptedPeerReaderRecovered");
        var associatedObjectTokenAcceptedPeerObjectMatchRecovered = ReadBool(sourceAssociatedObjectTokenContractSummary, "AcceptedPeerObjectMatchRecovered");
        var associatedObjectTokenAcceptedPeerAttachesObjectSocket = ReadBool(sourceAssociatedObjectTokenContractSummary, "AcceptedPeerAttachesObjectSocket");
        var associatedObjectTokenLookupRecovered = ReadBool(sourceAssociatedObjectTokenContractSummary, "AssociatedLookupRecovered");
        var associatedObjectTokenSlot90Recovered = ReadBool(sourceAssociatedObjectTokenContractSummary, "AssociatedSlot90DispatchRecovered");
        var pcapAssociatedObjectHardMarkerless = ReadInt(pcapSourceAssociatedObjectTokensSummary, "HardMarkerlessPacketCount");
        var pcapAssociatedObjectTokenPackets = ReadInt(pcapSourceAssociatedObjectTokensSummary, "AssociatedObjectTokenPacketCount");
        var pcapAssociatedObjectDistinctTokens = ReadInt(pcapSourceAssociatedObjectTokensSummary, "DistinctAssociatedObjectTokenCount");
        var pcapAssociatedObjectProbeCount = ReadInt(pcapSourceAssociatedObjectTokensSummary, "AssociationProbeCount");
        var pcapAssociatedObjectDispatchCoversHardMarkerless = ReadBool(pcapSourceAssociatedObjectTokensSummary, "AssociatedObjectDispatchCoversHardMarkerlessCorpus");
        var pcapAssociatedObjectNativeReady = ReadBool(pcapSourceAssociatedObjectTokensSummary, "NativeSourceInputReady");
        var pcapAssociatedObjectConclusion = ReadString(pcapSourceAssociatedObjectTokensSummary, "Conclusion");
        var associatedObjectTokenTransformHardMarkerless = ReadInt(associatedObjectTokenTransformProbeSummary, "HardMarkerlessPacketCount");
        var associatedObjectTokenTransformAssociatedPackets = ReadInt(associatedObjectTokenTransformProbeSummary, "AssociatedObjectTokenPacketCount");
        var associatedObjectTokenTransformStrictHits = ReadInt(associatedObjectTokenTransformProbeSummary, "StrictDecodeHitCount");
        var associatedObjectTokenTransformClcHits = ReadInt(associatedObjectTokenTransformProbeSummary, "ClcMoveHitCount");
        var associatedObjectTokenTransformNetHits = ReadInt(associatedObjectTokenTransformProbeSummary, "NetMessageHitCount");
        var associatedObjectTokenTransformQueueHits = ReadInt(associatedObjectTokenTransformProbeSummary, "QueueDeltaExactHitCount");
        var associatedObjectTokenTransformPacketHits = ReadInt(associatedObjectTokenTransformProbeSummary, "PacketWithAcceptedTransformHitCount");
        var associatedObjectTokenTransformNativeReady = ReadBool(associatedObjectTokenTransformProbeSummary, "NativeAssociatedObjectTokenTransformReady");
        var associatedObjectTokenTransformConclusion = ReadString(associatedObjectTokenTransformProbeSummary, "Conclusion");
        var associatedObjectSlot90ContractRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "AssociatedObjectSlot90ContractRecovered");
        var associatedObjectSlot90EntryRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "Slot90EntryRecovered");
        var associatedObjectSlot90StateTripleRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "SlotAcStateTripleRecovered");
        var associatedObjectSlot90DescriptorPredicateRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "SlotB4DescriptorPredicateRecovered");
        var associatedObjectSlot90PendingCounterRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "PendingCounterRecovered");
        var associatedObjectSlot90ConnectionTokenPredicatesRecovered = ReadBool(sourceAssociatedObjectSlot90Summary, "ConnectionTokenPredicatesRecovered");
        var associatedObjectSlot90NativeReady = ReadBool(sourceAssociatedObjectSlot90Summary, "NativeSourceInputReady");
        var associatedObjectSlot90OpenGates = ReadInt(sourceAssociatedObjectSlot90Summary, "OpenGateCount");
        var associatedObjectSlot90StorageContractRecovered = ReadBool(sourceAssociatedObjectSlot90StateContract, "StorageContractRecovered");
        var associatedObjectSlot90ResetBufferOffset = ReadString(sourceAssociatedObjectSlot90StateContract, "Slot90ResetBufferOffset");
        var associatedObjectSlot90DispatchRegistersRecovered = ReadBool(sourceAssociatedObjectSlot90RegisterContract, "AssociatedSlot90DispatchRegistersRecovered");
        var associatedObjectSlot90DelegatesToSlotAcRegistersRecovered = ReadBool(sourceAssociatedObjectSlot90RegisterContract, "Slot90DelegatesToSlotAcRegistersRecovered");
        var associatedObjectSlot90DoesNotSetR5R6 = ReadBool(sourceAssociatedObjectSlot90RegisterContract, "Slot90DoesNotExplicitlySetR5OrR6");
        var associatedObjectSlot90ResetCanClobberVolatileRegisters = ReadBool(sourceAssociatedObjectSlot90RegisterContract, "Slot90ResetCanClobberVolatileRegisters");
        var associatedObjectSlot90SetterStoresRecovered = ReadBool(sourceAssociatedObjectSlot90RegisterContract, "SlotAcSetterStoresRecovered");
        var associatedObjectSlot90OutputBuilderFunctionsAvailable = ReadBool(sourceAssociatedObjectSlot90OutputBuilderCensus, "OutputBuilderFunctionsAvailable");
        var associatedObjectSlot90OutputBuilderSlotAcCallsiteCount = ReadInt(sourceAssociatedObjectSlot90OutputBuilderCensus, "SlotAcCallsiteCount");
        var associatedObjectSlot90OutputBuilderStateConstants =
            sourceAssociatedObjectSlot90OutputBuilderCensus is not null
            && sourceAssociatedObjectSlot90OutputBuilderCensus.Value.TryGetProperty("StateConstants", out var outputBuilderStateConstants)
            && outputBuilderStateConstants.ValueKind == JsonValueKind.Array
                ? outputBuilderStateConstants.EnumerateArray()
                    .Select(static item => item.ToString())
                    .Where(static item => item.Length > 0)
                    .ToArray()
                : Array.Empty<string>();
        var associatedObjectSlot90StateFieldOffsets =
            sourceAssociatedObjectSlot90StateContract is not null
            && sourceAssociatedObjectSlot90StateContract.Value.TryGetProperty("Fields", out var stateFields)
            && stateFields.ValueKind == JsonValueKind.Array
                ? stateFields.EnumerateArray()
                    .Select(static field => ReadString(field, "ObjectOffset"))
                    .Where(static offset => offset.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
        var associatedObjectSlot90MissingSemantics =
            sourceAssociatedObjectSlot90StateContract is not null
            && sourceAssociatedObjectSlot90StateContract.Value.TryGetProperty("MissingSemantics", out var missingSemantics)
            && missingSemantics.ValueKind == JsonValueKind.Array
                ? missingSemantics.EnumerateArray()
                    .Select(static item => item.ToString())
                    .Where(static item => item.Length > 0)
                    .ToArray()
                : Array.Empty<string>();
        var associatedSlotAcProvenanceCallsiteCount = ReadInt(sourceAssociatedSlotAcProvenanceSummary, "SlotAcCallsiteCount");
        var associatedSlotAcProvenanceServerOutputCallsiteCount = ReadInt(sourceAssociatedSlotAcProvenanceSummary, "ProvenServerOutputStateCallsiteCount");
        var associatedSlotAcProvenanceClientCandidateCount = ReadInt(sourceAssociatedSlotAcProvenanceSummary, "ClientUploadDecoderCandidateCount");
        var associatedSlotAcProvenanceRejectsFocusedCallsites = ReadBool(sourceAssociatedSlotAcProvenanceSummary, "AllFocusedSlotAcCallsitesRejectedAsClientInput");
        var associatedSlotAcProvenanceNativeReady = ReadBool(sourceAssociatedSlotAcProvenanceSummary, "NativeSourceInputReady");
        var associatedSlotAcProvenanceStateConstants =
            sourceAssociatedSlotAcProvenanceSummary is not null
            && sourceAssociatedSlotAcProvenanceSummary.Value.TryGetProperty("ProvenStateConstants", out var provenanceStateConstants)
            && provenanceStateConstants.ValueKind == JsonValueKind.Array
                ? provenanceStateConstants.EnumerateArray()
                    .Select(static item => item.ToString())
                    .Where(static item => item.Length > 0)
                    .ToArray()
                : Array.Empty<string>();
        var associatedLaneRoleSplitRecovered = ReadBool(sourceAssociatedLaneRoleSummary, "PayloadDrainLaneSplitRecovered");
        var associatedLaneRoleAssociatedNotClc = ReadBool(sourceAssociatedLaneRoleSummary, "AssociatedLaneRejectedAsClcMoveBoundary");
        var associatedLaneRoleOwnerUsercmd = ReadBool(sourceAssociatedLaneRoleSummary, "OwnerLaneIsUsercmdRoute");
        var associatedLaneRoleNativeReady = ReadBool(sourceAssociatedLaneRoleSummary, "NativeSourceInputReady");
        var embeddedClcCandidateCount = ReadInt(sourceEmbeddedClcMoveCandidatesSummary, "EmbeddedCandidateCount");
        var embeddedClcHardMarkerlessCandidateCount = ReadInt(sourceEmbeddedClcMoveCandidatesSummary, "HardMarkerlessCandidateCount");
        var embeddedClcExactBoundaryCandidateCount = ReadInt(sourceEmbeddedClcMoveCandidatesSummary, "ExactBoundaryCandidateCount");
        var embeddedClcNativeReady = ReadBool(sourceEmbeddedClcMoveCandidatesSummary, "NativeBoundaryReady");
        var embeddedClcConclusion = ReadString(sourceEmbeddedClcMoveCandidatesSummary, "BoundaryConclusion");
        var category5StaticHandlerChainRecovered = category5RouteRecovered
            && category5HandlerTableCellRecovered
            && category5CallConventionRecovered
            && category5ClcMoveContractRecovered
            && category5ServerDllUsercmdDecoderRecovered
            && category5GhidraHandlerChainConfirmed;
        var ownerSlot8Function = ReadString(ownerControlSlotMap, "OwnerSlot8Function");
        var ownerSlot8ThunkTarget = ReadString(ownerControlSlotMap, "OwnerSlot8ThunkTarget");
        var associatedSlot90Function = ReadString(ownerControlSlotMap, "AssociatedSlot90Function");
        var markerlessBuilderImplementationUpdated = ReadBool(markerlessBuilderSummary, "ServerImplementationUpdated");
        var markerlessBuilderNativeReady = ReadBool(markerlessBuilderSummary, "NativeSourceInputReady");
        var oldVisibleBoundaryReady = directHardBoundary
            && nativeMarkerlessReady
            && param2ConstructionSite
            && builderNativeReady
            && callerSideParam2Builder
            && helperNativeReady;
        var payloadObjectDispatchRecovered = payloadObjectFirstWordRecovered
            && ownerSubobjectArgumentRecovered
            && associatedSlot90DispatchRecovered
            && ownerSlot8DispatchRecovered
            && payloadObjectFilterFinalizerRecovered;
        var payloadObjectBoundaryReady = concretePayloadObjectBoundary
            && payloadObjectDispatchRecovered
            && markerlessBuilderImplementationUpdated
            && markerlessBuilderNativeReady
            && nativeAssociatedSlot90HandlerImplemented
            && nativeOwnerSlot8HandlerImplemented
            && payloadObjectDispatchNativeReady
            && ownerForwardContextNativeReady
            && ownerForwarderBitstreamCoverageNativeReady;
        var pcapSourceInputCoverageReady = sourceClientInputCoverageRoot is not null && pcapSourceInputNativeReady;
        var associatedLaneRoleCoverageReady =
            associatedLaneRoleAssociatedNotClc
            && associatedLaneRoleOwnerUsercmd
            && payloadObjectDispatchRecovered
            && nativeAssociatedSlot90HandlerImplemented
            && nativeOwnerSlot8HandlerImplemented
            && ownerForwarderBitstreamImplementationReady
            && ownerForwardWrapperVariantReady
            && category5StaticHandlerChainRecovered
            && payloadObjectFirstWordCoversCorpus
            && payloadObjectFirstWordAssociated == payloadObjectFirstWordHardMarkerless
            && payloadObjectFirstWordHardMarkerless > 0
            && pcapAssociatedObjectDispatchCoversHardMarkerless
            && pcapSourceInputUnknownHardMarkerless == 0;
        var passed = (oldVisibleBoundaryReady || payloadObjectBoundaryReady || associatedLaneRoleCoverageReady)
            && (pcapSourceInputCoverageReady || associatedLaneRoleCoverageReady);

        return new AcceptanceGate(
            "tf2ps3-markerless-source-input-boundary-recovered",
            passed ? "passed" : "incomplete",
            "The hard markerless client Source body family is mapped into the recovered connected-wrapper or slot +0x70 bitreader path, so client uploads can be decoded natively instead of by replay/fallback.",
            new
            {
                HardOpaqueMarkerlessPacketCount = hardOpaqueMarkerless,
                DirectHardOpaqueBoundaryProven = directHardBoundary,
                NativeMarkerlessBoundaryReady = nativeMarkerlessReady,
                Param2ConstructionSiteRecovered = param2ConstructionSite,
                BuilderNativeSourceInputReady = builderNativeReady,
                CallerSideParam2BuilderRecovered = callerSideParam2Builder,
                HelperNativeSourceInputReady = helperNativeReady,
                PayloadObjectBuilderRecovered = payloadObjectBuilderRecovered,
                MarkerlessRecvFillRecovered = markerlessRecvFillRecovered,
                FragmentReassemblyRecovered = fragmentReassemblyRecovered,
                DrainDispatchLoopRecovered = drainDispatchLoopRecovered,
                ConcretePayloadObjectBoundaryRecovered = concretePayloadObjectBoundary,
                PayloadObjectDispatchCallsiteCount = payloadObjectDispatchCallsites,
                PayloadObjectOwnerSubobjectArgumentRecovered = ownerSubobjectArgumentRecovered,
                PayloadObjectFirstWordDispatchRecovered = payloadObjectFirstWordRecovered,
                PayloadObjectAssociatedSlot90DispatchRecovered = associatedSlot90DispatchRecovered,
                PayloadObjectOwnerSlot8DispatchRecovered = ownerSlot8DispatchRecovered,
                PayloadObjectFilterFinalizerRecovered = payloadObjectFilterFinalizerRecovered,
                PayloadObjectDispatchOpenGates = payloadObjectDispatchOpenGates,
                PayloadObjectServerImplementationUpdated = markerlessBuilderImplementationUpdated,
                PayloadObjectNativeSourceInputReady = markerlessBuilderNativeReady,
                NativeAssociatedSlot90HandlerImplemented = nativeAssociatedSlot90HandlerImplemented,
                NativeOwnerSlot8HandlerImplemented = nativeOwnerSlot8HandlerImplemented,
                PayloadObjectDispatchNativeSourceInputReady = payloadObjectDispatchNativeReady,
                OwnerControlSubobjectOwnerSlot8TargetRecovered = ownerControlSubobjectOwnerSlot8TargetRecovered,
                OwnerControlSubobjectOwnerSlot8ForwarderRecovered = ownerControlSubobjectOwnerSlot8ForwarderRecovered,
                OwnerControlSubobjectAssociatedSlot90CandidateRecovered = ownerControlSubobjectAssociatedSlot90CandidateRecovered,
                OwnerControlSubobjectOwnerSlot8Function = ownerSlot8Function,
                OwnerControlSubobjectOwnerSlot8ThunkTarget = ownerSlot8ThunkTarget,
                OwnerControlSubobjectAssociatedSlot90Function = associatedSlot90Function,
                OwnerControlSubobjectNativeOwnerSlot8ConsumerImplemented = ownerControlSubobjectNativeOwnerSlot8ConsumerImplemented,
                OwnerControlSubobjectNativeAssociatedSlot90ConsumerImplemented = ownerControlSubobjectNativeAssociatedSlot90ConsumerImplemented,
                OwnerControlSubobjectNativeSourceInputReady = ownerControlSubobjectNativeReady,
                OwnerForwardContextOwnerForwarderSlotCount = ownerForwarderSlotCount,
                OwnerForwardContextFamilyRecovered = ownerForwardContextFamilyRecovered,
                OwnerForwardContextCategory5UsercmdRouteCandidateRecovered = ownerForwardContextCategory5Recovered,
                OwnerForwardContextCategory5DynamicHandlerCandidate = ownerForwardContextCategory5DynamicHandlerCandidate,
                OwnerForwardContextHandlersImplemented = ownerForwardContextHandlersImplemented,
                OwnerForwardContextNativeSourceInputReady = ownerForwardContextNativeReady,
                OwnerForwardContextOpenGates = ownerForwardContextOpenGates,
                OwnerForwarderBitstreamWrappedForwarderCount = ownerForwarderWrappedCount,
                OwnerForwarderBitstreamImplementedWrappedForwarderCount = ownerForwarderImplementedWrappedCount,
                OwnerForwarderBitstreamMissingWrappedForwarderCount = ownerForwarderMissingWrappedCount,
                OwnerForwarderBitstreamWord5PrimitiveImplemented = ownerForwarderWord5PrimitiveImplemented,
                OwnerForwarderBitstreamWord5BoundaryImplemented = ownerForwarderWord5Implemented,
                OwnerForwarderBitstreamWord6PrimitiveImplemented = ownerForwarderWord6PrimitiveImplemented,
                OwnerForwarderBitstreamWord6BoundaryImplemented = ownerForwarderWord6Implemented,
                OwnerForwarderBitstreamDeferredWord6PrimitiveImplemented = ownerForwarderDeferredWord6PrimitiveImplemented,
                OwnerForwarderBitstreamDeferredWord6BoundaryImplemented = ownerForwarderDeferredWord6Implemented,
                OwnerForwarderBitstreamConfigFallbackWord4PrimitiveImplemented = ownerForwarderConfigWord4PrimitiveImplemented,
                OwnerForwarderBitstreamConfigFallbackWord4BoundaryImplemented = ownerForwarderConfigWord4Implemented,
                OwnerForwarderBitstreamSemanticContextHandlersImplemented = ownerForwarderSemanticContextHandlersImplemented,
                OwnerForwarderBitstreamImplementationReady = ownerForwarderBitstreamImplementationReady,
                OwnerForwarderBitstreamLiveMapLoadVerified = ownerForwarderBitstreamLiveMapLoadVerified,
                OwnerForwarderBitstreamNativeSourceInputReady = ownerForwarderBitstreamCoverageNativeReady,
                OwnerForwarderBitstreamOpenGates = ownerForwarderBitstreamCoverageOpenGates,
                OwnerForwardWrapperVariantPrimaryWrapperCount = ownerForwardWrapperVariantCount,
                OwnerForwardWrapperVariantLocatedPrimaryWrapperCount = ownerForwardWrapperVariantLocatedCount,
                OwnerForwardWrapperVariantShapeRecoveredPrimaryWrapperCount = ownerForwardWrapperVariantShapeRecoveredCount,
                OwnerForwardWrapperVariantImplementedOrNotRequiredWrapperCount = ownerForwardWrapperVariantImplementedOrNotRequiredCount,
                OwnerForwardWrapperVariantMissingImplementationCount = ownerForwardWrapperVariantMissingImplementationCount,
                OwnerForwardWrapperVariantCoverageReady = ownerForwardWrapperVariantReady,
                OwnerForwardWrapperVariantLiveMapLoadVerified = ownerForwardWrapperVariantLiveMapLoadVerified,
                Category5RouteRecovered = category5RouteRecovered,
                Category5HandlerTableCellRecovered = category5HandlerTableCellRecovered,
                Category5HandlerCallConventionMatchesDispatchObject = category5CallConventionRecovered,
                Category5ClcMoveReadContractRecovered = category5ClcMoveContractRecovered,
                Category5OfficialServerDllUsercmdDecoderRecovered = category5ServerDllUsercmdDecoderRecovered,
                Category5GhidraHandlerChainConfirmed = category5GhidraHandlerChainConfirmed,
                Category5ExactVptrWriteRecovered = category5ExactVptrWriteRecovered,
                Category5StaticHandlerChainRecovered = category5StaticHandlerChainRecovered,
                Category5NativeSourceInputReady = category5NativeReady,
                Category5OpenGates = category5OpenGates,
                PcapSourceClientInputCoverageNativeReady = pcapSourceInputNativeReady,
                PcapSourceClientInputPacketCount = pcapSourceInputClientPackets,
                PcapSourceClientInputNativeCoveredPacketCount = pcapSourceInputNativeCovered,
                PcapSourceClientInputDecodedClcMovePacketCount = pcapSourceInputDecodedClcMove,
                PcapSourceClientInputDecodedNetMessagePacketCount = pcapSourceInputDecodedNetMessages,
                PcapSourceClientInputWeakNetMessageCandidatePacketCount = pcapSourceInputWeakNetMessageCandidates,
                PcapSourceClientInputWeakNetMessageCandidateHardMarkerlessPacketCount = pcapSourceInputWeakNetMessageHardMarkerlessCandidates,
                PcapSourceClientInputHardMarkerlessPacketCount = pcapSourceInputHardMarkerless,
                PcapSourceClientInputHeuristicOnlyHardMarkerlessPacketCount = pcapSourceInputHeuristicHardMarkerless,
                PcapSourceClientInputUnknownHardMarkerlessPacketCount = pcapSourceInputUnknownHardMarkerless,
                PcapSourceClientInputUnknownPacketCount = pcapSourceInputUnknownPackets,
                PcapSourceClientInputCoverageConclusion = pcapSourceInputConclusion,
                PcapSourceClientBoundaryProbeNativeReady = pcapBoundaryProbeNativeReady,
                PcapSourceClientBoundaryProbeHardMarkerlessPacketCount = pcapBoundaryProbeHardMarkerless,
                PcapSourceClientBoundaryProbeNativeDecodedPacketCount = pcapBoundaryProbeNativeDecoded,
                PcapSourceClientBoundaryProbeNativeClcMovePacketCount = pcapBoundaryProbeNativeClcMove,
                PcapSourceClientBoundaryProbeNativeNetMessagePacketCount = pcapBoundaryProbeNativeNetMessage,
                PcapSourceClientBoundaryProbeWrapperLayoutMatchedPacketCount = pcapBoundaryProbeWrapperMatched,
                PcapSourceClientBoundaryProbeEmbeddedOffsetLayoutMatchedPacketCount = pcapBoundaryProbeEmbeddedOffsetMatched,
                PcapSourceClientBoundaryProbeEmbeddedOffsetOnlyLayoutMatchedPacketCount = pcapBoundaryProbeEmbeddedOffsetOnlyMatched,
                PcapSourceClientBoundaryProbeWrapperLayoutMatchedButUndecodedPacketCount = pcapBoundaryProbeWrapperMatchedButUndecoded,
                PcapSourceClientBoundaryProbeNoRecoveredWrapperLayoutPacketCount = pcapBoundaryProbeNoRecoveredWrapper,
                PcapSourceClientBoundaryProbeMatchedLayoutProbeCount = pcapBoundaryProbeMatchedLayoutProbeCount,
                PcapSourceClientBoundaryProbeEmbeddedOffsetProbeCount = pcapBoundaryProbeEmbeddedOffsetProbeCount,
                PcapSourceClientBoundaryProbeConclusion = pcapBoundaryProbeConclusion,
                PcapMarkerlessTransformProbeNativeReady = markerlessTransformNativeReady,
                PcapMarkerlessTransformProbeHardMarkerlessPacketCount = markerlessTransformHardMarkerless,
                PcapMarkerlessTransformProbeStrictTransformHitCount = markerlessTransformStrictHits,
                PcapMarkerlessTransformProbePacketWithStrictTransformHitCount = markerlessTransformPacketHits,
                PcapMarkerlessTransformProbeClcMoveTransformHitCount = markerlessTransformClcMoveHits,
                PcapMarkerlessTransformProbeNetMessageTransformHitCount = markerlessTransformNetMessageHits,
                PcapMarkerlessTransformProbeConclusion = markerlessTransformConclusion,
                PcapMarkerlessSessionKeyProbeNativeReady = sessionKeyProbeNativeReady,
                PcapMarkerlessSessionKeyProbeHardMarkerlessPacketCount = sessionKeyProbeHardMarkerless,
                PcapMarkerlessSessionKeyProbePacketsWithSessionKeys = sessionKeyProbeKeyedPackets,
                PcapMarkerlessSessionKeyProbeStrictDecodeHitCount = sessionKeyProbeStrictHits,
                PcapMarkerlessSessionKeyProbeClcMoveHitCount = sessionKeyProbeClcHits,
                PcapMarkerlessSessionKeyProbeNetMessageHitCount = sessionKeyProbeNetHits,
                PcapMarkerlessSessionKeyProbeQueueDeltaExactHitCount = sessionKeyProbeQueueHits,
                PcapMarkerlessSessionKeyProbePacketWithAcceptedTransformHitCount = sessionKeyProbePacketHits,
                PcapMarkerlessSessionKeyProbeConclusion = sessionKeyProbeConclusion,
                UsercmdQueueRecordLayerRecovered = usercmdQueueRecordLayerRecovered,
                UsercmdQueueRecordFanoutCallerRecovered = usercmdQueueRecordFanoutCallerRecovered,
                UsercmdQueueRecordDeltaFieldCount = usercmdQueueRecordDeltaFieldCount,
                UsercmdQueueRecordPcapProbeAvailable = usercmdQueueRecordPcapProbeAvailable,
                UsercmdQueueRecordHardMarkerlessPacketCount = usercmdQueueRecordHardMarkerless,
                UsercmdQueueRecordRawRecordLengthCandidateCount = usercmdQueueRecordRawRecordCandidates,
                UsercmdQueueRecordRawRecordLengthMultipleCandidateCount = usercmdQueueRecordRawRecordMultipleCandidates,
                UsercmdQueueRecordNativeSourceInputReady = usercmdQueueRecordNativeReady,
                QueueDeltaTailCandidatePacketCount = queueDeltaTailCandidateCount,
                QueueDeltaTailExactTwoRecordPacketCount = queueDeltaTailExactTwoRecordCount,
                QueueDeltaTailDecodedWithNonZeroTrailingPacketCount = queueDeltaTailNonZeroTrailingCount,
                QueueDeltaTailExactZeroTrailingPacketCount = queueDeltaTailExactZeroTrailingCount,
                QueueDeltaTailMostCommonSequenceDelta = queueDeltaTailMostCommonSequenceDelta,
                QueueDeltaTailMostCommonSequenceDeltaCount = queueDeltaTailMostCommonSequenceDeltaCount,
                QueueDeltaTailDistinctConsumedBitCount = queueDeltaTailDistinctConsumedBitCount,
                QueueDeltaTailFixedTrailerHypothesisRuledOut = queueDeltaTailFixedTrailerRuledOut,
                QueueDeltaTailNativeBoundaryReady = queueDeltaTailNativeReady,
                QueueDeltaTailConclusion = queueDeltaTailConclusion,
                OwnerCallbackDispatchSlotCount = ownerCallbackSlotCount,
                OwnerCallbackDispatchSlot70SequenceRecovered = ownerCallbackSlot70SequenceRecovered,
                OwnerCallbackDispatchPathConstructsParam2 = ownerCallbackPathConstructsParam2,
                OwnerCallbackDispatchConcreteMarkerlessEdgeRecovered = ownerCallbackConcreteMarkerlessEdge,
                OwnerCallbackDispatchNativeSourceInputReady = ownerCallbackNativeReady,
                OwnerCallbackDispatchOpenGates = ownerCallbackOpenGates,
                HelperSliceReceiveSiblingCount = helperReceiveSiblingCount,
                HelperSlicePresentReceiveSiblingCount = helperPresentReceiveSiblingCount,
                HelperSliceAttachedType2DispatchProven = helperAttachedType2DispatchProven,
                HelperSliceSlot70ConsumesCallerBitreader = helperSlot70ConsumesCallerBitreader,
                HelperSliceSlot70ReadsAttachedSocket = helperSlot70ReadsAttachedSocket,
                HelperSliceSiblingPathConstructsParam2 = helperSiblingPathConstructsParam2,
                HelperSliceReceiveSiblingsNativeSourceInputReady = helperSiblingNativeReady,
                HelperSliceReceiveSiblingsOpenGates = helperSiblingOpenGates,
                RecvBitreaderConnectedRecvWrapperCallerCount = recvConnectedRecvCallers,
                RecvBitreaderConnectedRecvDispatcherUnique = recvConnectedRecvDispatcherUnique,
                RecvBitreaderHardMarkerlessRecvBitreaderCandidateCount = recvHardMarkerlessCandidateCount,
                RecvBitreaderStrictPcapHardSetStillOpaque = recvStrictPcapHardSetStillOpaque,
                RecvBitreaderNativeSourceInputReady = recvNativeReady,
                RecvBitreaderOpenGates = recvOpenGates,
                RawUdpControlProbeRecovered = rawUdpControlProbeRecovered,
                RawUdpControlProbeExcludedFromMarkerlessBoundary = rawUdpControlProbeExcluded,
                RawUdpControlProbePcap6e01Count = rawUdpControlProbePcapCount,
                RawUdpControlProbeNativeSourceInputReady = rawUdpControlProbeNativeReady,
                ReliablePeerAttachControlRecovered = reliablePeerAttachControlRecovered,
                ReliablePeerAttachChainRecovered = reliablePeerAttachChainRecovered,
                ReliablePeerAttachedFrameReaderRecovered = reliablePeerAttachedFrameReaderRecovered,
                ReliablePeerSlot70ConsumesCallerBitreader = reliablePeerSlot70ConsumesCallerBitreader,
                ReliablePeerAttachNativeSourceInputReady = reliablePeerAttachNativeReady,
                ReliablePeerAttachOpenGates = reliablePeerAttachOpenGates,
                PayloadObjectFirstWordHardMarkerlessPacketCount = payloadObjectFirstWordHardMarkerless,
                PayloadObjectFirstWordDecodedPacketCount = payloadObjectFirstWordDecoded,
                PayloadObjectFirstWordOwnerSlot8Count = payloadObjectFirstWordOwnerSlot8,
                PayloadObjectFirstWordFragmentedSpecialWrapperCount = payloadObjectFirstWordFragmented,
                PayloadObjectFirstWordRepackedSpecialWrapperCount = payloadObjectFirstWordRepacked,
                PayloadObjectFirstWordAssociatedObjectTokenCount = payloadObjectFirstWordAssociated,
                PayloadObjectFirstWordDistinctAssociatedObjectTokenCount = payloadObjectFirstWordDistinctAssociated,
                PayloadObjectFirstWordNativeAssociationDescriptorCount = payloadObjectFirstWordNativeAssociationDescriptors,
                PayloadObjectFirstWordAssociationDescriptorType1Count = payloadObjectFirstWordDescriptorType1,
                PayloadObjectFirstWordAssociationDescriptorType2Count = payloadObjectFirstWordDescriptorType2,
                PayloadObjectFirstWordAssociationDescriptorType3Count = payloadObjectFirstWordDescriptorType3,
                PayloadObjectFirstWordDispatchCoversHardMarkerlessCorpus = payloadObjectFirstWordCoversCorpus,
                PayloadObjectFirstWordNativeSourceInputReady = payloadObjectFirstWordNativeReady,
                PayloadObjectFirstWordConclusion = payloadObjectFirstWordConclusion,
                NativeAssociationDescriptorHardMarkerlessPacketCount = nativeAssociationDescriptorHardMarkerless,
                NativeAssociationDescriptorBigEndianOffset0PacketCount = nativeAssociationDescriptorBigEndianOffset0,
                NativeAssociationDescriptorLittleEndianOffset0PacketCount = nativeAssociationDescriptorLittleEndianOffset0,
                NativeAssociationDescriptorBigEndianAnyOffsetPacketCount = nativeAssociationDescriptorBigEndianAnyOffset,
                NativeAssociationDescriptorLittleEndianAnyOffsetPacketCount = nativeAssociationDescriptorLittleEndianAnyOffset,
                NativeAssociationDescriptorRawBigEndianOffset0PacketCount = nativeAssociationDescriptorRawBigEndianOffset0,
                NativeAssociationDescriptorRawBigEndianOffset1PacketCount = nativeAssociationDescriptorRawBigEndianOffset1,
                NativeAssociationDescriptorRawBigEndianOffset2PacketCount = nativeAssociationDescriptorRawBigEndianOffset2,
                NativeAssociationDescriptorRawBigEndianAnyOffsetPacketCount = nativeAssociationDescriptorRawBigEndianAnyOffset,
                NativeAssociationDescriptorRawLittleEndianOffset0PacketCount = nativeAssociationDescriptorRawLittleEndianOffset0,
                NativeAssociationDescriptorRawLittleEndianOffset1PacketCount = nativeAssociationDescriptorRawLittleEndianOffset1,
                NativeAssociationDescriptorRawLittleEndianOffset2PacketCount = nativeAssociationDescriptorRawLittleEndianOffset2,
                NativeAssociationDescriptorRawLittleEndianAnyOffsetPacketCount = nativeAssociationDescriptorRawLittleEndianAnyOffset,
                NativeAssociationDescriptorMostCommonBigEndianOffset = nativeAssociationDescriptorMostCommonOffset,
                NativeAssociationDescriptorMostCommonBigEndianOffsetCount = nativeAssociationDescriptorMostCommonOffsetCount,
                NativeAssociationDescriptorBoundaryReady = nativeAssociationDescriptorBoundaryReady,
                NativeAssociationDescriptorConclusion = nativeAssociationDescriptorConclusion,
                AssociatedObjectTokenContractRecovered = associatedObjectTokenContractRecovered,
                AssociatedObjectTokenAcceptedPeerReaderRecovered = associatedObjectTokenAcceptedPeerReaderRecovered,
                AssociatedObjectTokenAcceptedPeerObjectMatchRecovered = associatedObjectTokenAcceptedPeerObjectMatchRecovered,
                AssociatedObjectTokenAcceptedPeerAttachesObjectSocket = associatedObjectTokenAcceptedPeerAttachesObjectSocket,
                AssociatedObjectTokenLookupRecovered = associatedObjectTokenLookupRecovered,
                AssociatedObjectTokenSlot90Recovered = associatedObjectTokenSlot90Recovered,
                PcapAssociatedObjectTokenHardMarkerlessPacketCount = pcapAssociatedObjectHardMarkerless,
                PcapAssociatedObjectTokenPacketCount = pcapAssociatedObjectTokenPackets,
                PcapAssociatedObjectDistinctTokenCount = pcapAssociatedObjectDistinctTokens,
                PcapAssociatedObjectAssociationProbeCount = pcapAssociatedObjectProbeCount,
                PcapAssociatedObjectDispatchCoversHardMarkerlessCorpus = pcapAssociatedObjectDispatchCoversHardMarkerless,
                PcapAssociatedObjectTokenNativeSourceInputReady = pcapAssociatedObjectNativeReady,
                PcapAssociatedObjectTokenConclusion = pcapAssociatedObjectConclusion,
                AssociatedObjectTokenTransformProbeNativeReady = associatedObjectTokenTransformNativeReady,
                AssociatedObjectTokenTransformProbeHardMarkerlessPacketCount = associatedObjectTokenTransformHardMarkerless,
                AssociatedObjectTokenTransformProbeAssociatedPacketCount = associatedObjectTokenTransformAssociatedPackets,
                AssociatedObjectTokenTransformProbeStrictDecodeHitCount = associatedObjectTokenTransformStrictHits,
                AssociatedObjectTokenTransformProbeClcMoveHitCount = associatedObjectTokenTransformClcHits,
                AssociatedObjectTokenTransformProbeNetMessageHitCount = associatedObjectTokenTransformNetHits,
                AssociatedObjectTokenTransformProbeQueueDeltaExactHitCount = associatedObjectTokenTransformQueueHits,
                AssociatedObjectTokenTransformProbePacketWithAcceptedTransformHitCount = associatedObjectTokenTransformPacketHits,
                AssociatedObjectTokenTransformProbeConclusion = associatedObjectTokenTransformConclusion,
                AssociatedObjectSlot90ContractRecovered = associatedObjectSlot90ContractRecovered,
                AssociatedObjectSlot90EntryRecovered = associatedObjectSlot90EntryRecovered,
                AssociatedObjectSlot90StateTripleRecovered = associatedObjectSlot90StateTripleRecovered,
                AssociatedObjectSlot90DescriptorPredicateRecovered = associatedObjectSlot90DescriptorPredicateRecovered,
                AssociatedObjectSlot90PendingCounterRecovered = associatedObjectSlot90PendingCounterRecovered,
                AssociatedObjectSlot90ConnectionTokenPredicatesRecovered = associatedObjectSlot90ConnectionTokenPredicatesRecovered,
                AssociatedObjectSlot90StorageContractRecovered = associatedObjectSlot90StorageContractRecovered,
                AssociatedObjectSlot90ResetBufferOffset = associatedObjectSlot90ResetBufferOffset,
                AssociatedObjectSlot90DispatchRegistersRecovered = associatedObjectSlot90DispatchRegistersRecovered,
                AssociatedObjectSlot90DelegatesToSlotAcRegistersRecovered = associatedObjectSlot90DelegatesToSlotAcRegistersRecovered,
                AssociatedObjectSlot90DoesNotExplicitlySetR5OrR6 = associatedObjectSlot90DoesNotSetR5R6,
                AssociatedObjectSlot90ResetCanClobberVolatileRegisters = associatedObjectSlot90ResetCanClobberVolatileRegisters,
                AssociatedObjectSlot90SetterStoresRecovered = associatedObjectSlot90SetterStoresRecovered,
                AssociatedObjectSlot90OutputBuilderFunctionsAvailable = associatedObjectSlot90OutputBuilderFunctionsAvailable,
                AssociatedObjectSlot90OutputBuilderSlotAcCallsiteCount = associatedObjectSlot90OutputBuilderSlotAcCallsiteCount,
                AssociatedObjectSlot90OutputBuilderStateConstants = associatedObjectSlot90OutputBuilderStateConstants,
                AssociatedSlotAcProvenanceCallsiteCount = associatedSlotAcProvenanceCallsiteCount,
                AssociatedSlotAcProvenanceServerOutputCallsiteCount = associatedSlotAcProvenanceServerOutputCallsiteCount,
                AssociatedSlotAcProvenanceClientCandidateCount = associatedSlotAcProvenanceClientCandidateCount,
                AssociatedSlotAcProvenanceRejectsFocusedCallsites = associatedSlotAcProvenanceRejectsFocusedCallsites,
                AssociatedSlotAcProvenanceStateConstants = associatedSlotAcProvenanceStateConstants,
                AssociatedSlotAcProvenanceNativeSourceInputReady = associatedSlotAcProvenanceNativeReady,
                AssociatedLaneRoleSplitRecovered = associatedLaneRoleSplitRecovered,
                AssociatedLaneRoleAssociatedRejectedAsClcMoveBoundary = associatedLaneRoleAssociatedNotClc,
                AssociatedLaneRoleOwnerLaneIsUsercmdRoute = associatedLaneRoleOwnerUsercmd,
                AssociatedLaneRoleNativeSourceInputReady = associatedLaneRoleNativeReady,
                AssociatedLaneRoleCoverageReady = associatedLaneRoleCoverageReady,
                AssociatedObjectSlot90StateFieldOffsets = associatedObjectSlot90StateFieldOffsets,
                AssociatedObjectSlot90MissingSemantics = associatedObjectSlot90MissingSemantics,
                AssociatedObjectSlot90OpenGates = associatedObjectSlot90OpenGates,
                AssociatedObjectSlot90NativeSourceInputReady = associatedObjectSlot90NativeReady,
                EmbeddedClcMoveCandidateCount = embeddedClcCandidateCount,
                EmbeddedClcMoveHardMarkerlessCandidateCount = embeddedClcHardMarkerlessCandidateCount,
                EmbeddedClcMoveExactBoundaryCandidateCount = embeddedClcExactBoundaryCandidateCount,
                EmbeddedClcMoveNativeBoundaryReady = embeddedClcNativeReady,
                EmbeddedClcMoveConclusion = embeddedClcConclusion,
                NativeSourceInputReady = passed
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    payloadObjectDispatchRecovered && nativeAssociatedSlot90HandlerImplemented && nativeOwnerSlot8HandlerImplemented
                        ? "Verify the implemented associated-object +0x90 and owner +0x08 consumers against hard markerless PCAP/live client uploads."
                        : payloadObjectDispatchRecovered
                        ? "Implement the recovered associated-object vtable +0x90 and owner vtable +0x08 payload-object consumers in the native Source responder."
                        : concretePayloadObjectBoundary
                            ? "Reduce 008be1e8 payload-object dispatch into concrete associated/owner consumer contracts."
                        : "Recover the engine wrapper/transform that maps the 11,874 hard markerless PCAP bodies into the connected attached-frame reader or slot +0x70 bitreader.",
                    sourceAssociatedObjectSlot90Root is null
                        ? "Run the associated-object slot +0x90 reducer so the gate can distinguish recovered TF.elf dispatch from the still-missing field grammar."
                        : associatedObjectSlot90NativeReady
                            ? "Promote associated-object slot +0x90 field grammar into PCAP/live client upload verification."
                        : associatedObjectSlot90DispatchRegistersRecovered
                                && associatedObjectSlot90DelegatesToSlotAcRegistersRecovered
                                && associatedObjectSlot90DoesNotSetR5R6
                                ? associatedLaneRoleAssociatedNotClc && associatedLaneRoleOwnerUsercmd
                                    ? "Associated non--1 lane is role-recovered as association/state reset rather than CLC_Move; concentrate PCAP/live markerless decoding on the -1 owner slot +0x08/category-5 CLC_Move route and map-load output obligations."
                                    : associatedSlotAcProvenanceRejectsFocusedCallsites
                                    ? "Focused +0xac output-builder callsites are provenance-rejected as client-upload decoders; continue with the remaining associated-object slot +0x90 payload grammar and downstream consumers for +0x08/+0x0c/+0x10 state words."
                                    : "Continue from source-associated-object-slot90-map: slot +0x90 register flow and source-output +0xac state 0xc are proven; recover downstream consumers that give r5/r6 stable meaning, then map client uploads and map-load states to server.dll obligations."
                                : associatedObjectSlot90ContractRecovered
                                    ? "Continue from source-associated-object-slot90-map: recover the downstream +0xac state-triple field grammar and map it to server.dll/usercmd/map-load obligations."
                                : "Finish recovering the associated-object +0x90/+0xac/+0xb4 TF.elf contract before treating hard markerless uploads as native input.",
                    category5StaticHandlerChainRecovered && !category5NativeReady
                        ? "Category-5 usercmd static path is recovered (008722a0 category 5 -> +0x98 -> 00a2bd18 -> 00a291c0); consume those markerless uploads through the server.dll CUserCmd decoder and live/PCAP verify them."
                        : ownerForwardContextHandlersImplemented
                        ? "Resolve/prove the exact dynamic category-5 handler behind *(iVar9 + 8) + 0x98 against PCAP/live uploads."
                        : ownerForwardContextCategory5Recovered
                            ? "Resolve the exact dynamic category-5 handler behind *(iVar9 + 8) + 0x98 and implement the 008722a0 context handlers."
                        : "Reduce the 008722a0 owner-forward context handlers and identify the category-5 usercmd/CLC move route.",
                    ownerForwarderBitstreamCoverageRoot is null
                        ? "Run the owner-forwarder bitstream coverage reducer so the gate can audit word-5/word-6/deferred/config wrapper implementation."
                        : ownerForwarderMissingWrappedCount > 0
                            ? "Implement guarded native parsers for the missing owner-forwarder bitstream wrappers: word-6, deferred-pointer word-6, and config/state fallback word-4 as reported."
                            : ownerForwarderSemanticContextHandlersImplemented
                                ? "Prove the decoded owner-forwarder semantic contexts against PCAP/live uploads and close the 008722a0 category-handler gate."
                                : "Wire decoded owner-forwarder bitstream wrappers into semantic 008722a0 context handlers.",
                    ownerForwardWrapperVariantsRoot is null
                        ? "Run the direct TF.elf owner-forward wrapper variant reducer so the gate can audit wrapper siblings outside the owner vtable slot table."
                        : ownerForwardWrapperVariantMissingImplementationCount > 0
                            ? "Implement the missing direct TF.elf owner-forward wrapper variant families reported by source-owner-forward-wrapper-variants.json."
                            : ownerForwardWrapperVariantReady
                                ? "All concrete TF.elf owner-forward wrapper variants are recovered and implementation-covered; continue upstream markerless packet-to-wrapper transform proof and live verification."
                                : "Recover the remaining concrete TF.elf owner-forward wrapper variant shapes before treating 008722a0 wrapper coverage as complete.",
                    param2ConstructionSite || concretePayloadObjectBoundary
                        ? "Verify recovered caller-side payload objects against PCAP/live client uploads."
                        : "Recover the caller-side param_2 construction site that fills the object consumed by 00a5d9e0.",
                    sourceClientInputCoverageRoot is null
                        ? "Run the PCAP Source client-input coverage analyzer so the gate can list every decoded, heuristic-only, and unknown hard markerless upload."
                        : pcapSourceInputNativeReady
                            ? "Promote PCAP Source client-input coverage to live RPCS3 proof."
                            : pcapSourceInputUnknownHardMarkerless > 0
                                ? "Decode or document the remaining hard markerless PCAP client uploads reported by pcap-source-client-input-coverage."
                                : pcapSourceInputHeuristicHardMarkerless > 0
                                    ? "Replace heuristic-only hard markerless client-upload handling with native CLC_Move/net-message decoding."
                                    : "Keep PCAP Source client-input coverage regenerated while closing the remaining native TF.elf/server.dll proof gates.",
                    sourceClientBoundaryProbeRoot is null
                        ? "Run the PCAP Source client boundary probe analyzer so the gate can tell whether hard markerless uploads match recovered TF.elf owner-forwarder layouts."
                        : pcapBoundaryProbeNoRecoveredWrapper > 0
                            ? "Recover the pre-owner-forwarder transform/wrapper: the boundary probe shows hard markerless uploads do not match any recovered owner-forwarder bitstream layout yet."
                            : pcapBoundaryProbeWrapperMatchedButUndecoded > 0
                                ? "Decode the inner payloads for hard markerless packets that now match recovered owner-forwarder wrapper layouts."
                                : pcapBoundaryProbeNativeReady
                                    ? "Promote boundary-probe PCAP coverage to live RPCS3 proof."
                                    : "Keep boundary-probe evidence regenerated while closing the remaining markerless Source input gates.",
                    sourceMarkerlessTransformProbeRoot is null
                        ? "Run the strict markerless transform probe so the gate can separate shallow byte/bit/endian mistakes from a missing TF.elf connected-wrapper dataflow edge."
                        : markerlessTransformNativeReady
                            ? "Promote strict markerless transform coverage into the native client input decoder and live RPCS3 proof."
                            : markerlessTransformClcMoveHits > 0
                                ? "Investigate strict CLC_Move transform hits and promote only if one transform consistently covers the hard markerless family."
                                : markerlessTransformStrictHits > 0
                                    ? "Treat the scattered strict net-message transform hits as weak leads only; no CLC_Move transform was recovered and coverage remains partial."
                                    : "No shallow transform explains hard markerless packets; recover the connected wrapper/bitreader dataflow in TF.elf/server.dll.",
                    sourceMarkerlessSessionKeyProbeRoot is null
                        ? "Run the EKEY/LKEY/TICKET session-key markerless probe so the gate can rule in or rule out keyed wrapping before deeper TF.elf ingress work."
                        : sessionKeyProbeNativeReady
                            ? "Promote the accepted EKEY-derived transform into the native client input decoder and live RPCS3 proof."
                            : sessionKeyProbePacketHits > 0
                                ? "Investigate partial EKEY-derived transform hits; do not promote until one transform family covers hard markerless uploads under exact TF.elf/server.dll decoders."
                                : "EKEY/LKEY/TICKET-derived transforms did not explain the hard markerless bodies; continue TF.elf/server.dll ingress reverse engineering before the queue/usercmd layer.",
                    sourceUsercmdQueueRecordRoot is null
                        ? "Run the TF.elf usercmd queue-record reducer so the gate can audit the official 0x58-byte CUserCmd queue layer."
                        : usercmdQueueRecordLayerRecovered && !usercmdQueueRecordNativeReady
                            ? usercmdQueueRecordRawRecordMultipleCandidates > 0
                                ? sourceUsercmdQueueDeltaTailRoot is null
                                    ? "Run the queue-delta tail analyzer for exact 0x58-multiple hard markerless packets before deciding whether they are wrapped/delta-compressed usercmd records."
                                    : queueDeltaTailFixedTrailerRuledOut
                                        ? "Do not promote the exact 0x58-multiple packet family as native input: queue-delta prefixes leave variable non-zero trailing data, so recover the upstream TF.elf wrapper/transform before 00a2ae20."
                                        : "Finish classifying exact 0x58-multiple hard markerless packets, then recover whether they are wrapped/delta-compressed usercmd records or another pre-00a2ae20 ingress transform."
                                : "Recover the ingress transform that fills the TF.elf 0x58-byte usercmd queue records before 00a2ae20 drains them into the category-5 delta bitstream."
                            : usercmdQueueRecordRawRecordCandidates > 0
                                ? "Inspect raw 0x58-sized hard markerless packet bodies before treating them as native CUserCmd records."
                                : "Keep the TF.elf usercmd queue-record report regenerated while closing live markerless Source input proof.",
                    sourceOwnerCallbackDispatchRoot is null
                        ? "Run the owner-callback dispatch reducer so the gate can distinguish owner dispatch notification from the missing caller-side bitreader builder."
                        : ownerCallbackPathConstructsParam2
                            ? "Promote the owner-callback param_2 builder into the native boundary resolver and prove it against hard markerless PCAP/live packets."
                            : "Owner callbacks are now proven to bracket/observe Source dispatch, not construct markerless param_2; continue upstream of owner callbacks.",
                    sourceHelperSliceReceiveSiblingsRoot is null
                        ? "Run the helper-slice receive-sibling reducer so the gate can audit the sibling receive paths around 00a5c2e8 and 00a5d9e0."
                        : helperSiblingPathConstructsParam2
                            ? "Promote the helper-sibling param_2 construction path into the native boundary resolver and prove it against hard markerless PCAP/live packets."
                            : "Helper-slice receive siblings prove 00a5d9e0 consumes caller-built bitreaders; they do not build the hard markerless body wrapper.",
                    sourceRecvBitreaderCensusRoot is null
                        ? "Run the recv-bitreader census so the gate can rule in/out any remaining visible TF.elf recv-to-Source-dispatch path."
                        : recvHardMarkerlessCandidateCount > 0
                            ? "Reduce the recv-bitreader candidate functions into exact markerless boundary constructors and implement that parser."
                            : "The visible recv-to-bitreader census has no hard markerless candidate; the missing transform is before visible dispatch or indirect in Ghidra output.",
                    sourceRawUdpControlProbeRoot is null
                        ? "Run the raw UDP control probe reducer so 0088a208 is either ruled in or excluded from markerless Source input."
                        : rawUdpControlProbeRecovered && rawUdpControlProbeExcluded
                            ? "The 0088a208 raw UDP control probe is recovered and excluded from the hard markerless Source input boundary; continue upstream markerless transform work."
                            : "Review the 0088a208 raw UDP control probe before using it as any markerless Source input evidence.",
                    sourceReliablePeerAttachRoot is null
                        ? "Run the reliable peer attach reducer so the gate can separate the 5-byte accepted-peer attach contract from the remaining markerless payload wrapper."
                        : reliablePeerAttachChainRecovered && !reliablePeerAttachNativeReady
                            ? "Reliable peer attach and attached frame reading are recovered from TF.elf; continue upstream of the attach/frame reader to recover the hard markerless wrapper or caller-side bitreader transform."
                            : reliablePeerAttachNativeReady
                                ? "Promote reliable peer attach only together with live markerless Source input proof."
                                : "Review the reliable peer attach report before treating accepted-peer traffic as a native Source payload boundary.",
                    sourcePayloadObjectFirstWordRoot is null
                        ? "Run the payload-object first-word analyzer so the gate can classify hard markerless uploads through TF.elf's 008be1e8 dispatch."
                        : associatedLaneRoleAssociatedNotClc && payloadObjectFirstWordCoversCorpus && payloadObjectFirstWordAssociated == payloadObjectFirstWordHardMarkerless && payloadObjectFirstWordHardMarkerless > 0
                            ? "Payload-object first-word PCAP coverage now maps hard markerless packets to the associated state/reset lane; keep that lane implemented, but do not treat it as CLC_Move without the -1 owner/category route."
                        : payloadObjectFirstWordCoversCorpus && payloadObjectFirstWordAssociated == payloadObjectFirstWordHardMarkerless && payloadObjectFirstWordNativeAssociationDescriptors == 0 && payloadObjectFirstWordHardMarkerless > 0
                            ? "Hard markerless packets all enter TF.elf's non--1 associated-object path; recover the vtable +0xb4 descriptor compare and slot +0x90 field grammar before treating them as CLC_Move."
                            : payloadObjectFirstWordCoversCorpus && payloadObjectFirstWordAssociated == payloadObjectFirstWordHardMarkerless && payloadObjectFirstWordHardMarkerless > 0
                                ? "All hard markerless packets enter TF.elf associated-object slot +0x90 dispatch; recover association descriptor and slot +0x90 field grammar before treating them as CLC_Move."
                            : payloadObjectFirstWordNativeReady
                                ? "Promote payload-object first-word coverage only after live RPCS3 verifies the associated/owner field grammar."
                                : "Classify hard markerless payload-object first words until every packet has a recovered TF.elf dispatch family.",
                    sourceNativeAssociationDescriptorScanRoot is null
                        ? "Run the native association descriptor scanner so the gate can test hard markerless uploads against TF.elf 0134e230 descriptor matching."
                        : nativeAssociationDescriptorBoundaryReady
                            ? "Promote native association descriptor coverage only after slot +0x90 handler semantics are verified against PCAP/live uploads."
                            : associatedLaneRoleAssociatedNotClc && nativeAssociationDescriptorBigEndianOffset0 == 0 && nativeAssociationDescriptorLittleEndianOffset0 == 0
                                ? "Native association descriptor scans are negative at the packet surface, which matches the recovered associated state/reset lane; continue with owner/category-5 live input proof instead of generic descriptor transforms."
                            : nativeAssociationDescriptorBigEndianOffset0 == 0 && nativeAssociationDescriptorLittleEndianOffset0 == 0
                                ? "No hard markerless packet starts with a small descriptor type 1/2/3 value; use the associated-object token contract report to recover the vtable +0xb4 descriptor/state comparison instead of treating this as a required wire transform."
                                : "Only part of the hard markerless corpus starts with small descriptor values; recover how those descriptors relate to vtable +0xb4 association state before promoting native input.",
                    sourceAssociatedObjectTokenContractRoot is null
                        ? "Run the associated-object token contract reducer so the gate can audit 008b9468, 008b9ad8, and 008be1e8 as the native non--1 payload-object route."
                        : associatedLaneRoleAssociatedNotClc && associatedObjectTokenContractRecovered && pcapAssociatedObjectDispatchCoversHardMarkerless && !pcapAssociatedObjectNativeReady
                            ? "Associated-object token dispatch is recovered and lane-role classified as association/state reset; keep the implementation covered and focus input readiness on the owner -1/category-5 route."
                        : associatedObjectTokenContractRecovered && pcapAssociatedObjectDispatchCoversHardMarkerless && !pcapAssociatedObjectNativeReady
                            ? "Associated-object token dispatch is recovered and covers the hard markerless PCAP corpus; continue with the concrete vtable +0x90 consumer and vtable +0xb4 descriptor/state field grammar."
                            : associatedObjectTokenContractRecovered
                                ? "Correlate associated-object token dispatch against the PCAP corpus and live RPCS3 uploads before promoting Source input readiness."
                                : "Finish recovering the accepted-peer/object association token contract from TF.elf before decoding slot +0x90 payload fields.",
                    associatedObjectTokenTransformProbeRoot is null
                        ? "Run the associated-object token transform probe so the gate can test whether the per-packet +0xb4 descriptor/token word keys the remaining markerless payload transform."
                        : associatedObjectTokenTransformNativeReady
                            ? "Promote the associated-object token-derived transform into the native client input decoder and live RPCS3 proof."
                        : associatedObjectTokenTransformPacketHits > 0
                            ? "Investigate partial associated-object token-derived transform hits; do not promote until one token-derived family covers the hard markerless corpus."
                                : associatedLaneRoleAssociatedNotClc
                                    ? "Per-packet associated-object token transforms are negative and the associated lane is not CLC_Move; preserve this as negative evidence and continue with owner/category-5 input proof."
                                    : "Per-packet associated-object token-derived transforms did not explain the hard markerless bodies; continue TF.elf/server.dll slot +0x90 field-grammar recovery.",
                    sourceEmbeddedClcMoveCandidatesRoot is null
                        ? "Run the embedded CLC_Move candidate analyzer so weak offset-scan hits are documented but kept out of readiness."
                        : embeddedClcHardMarkerlessCandidateCount > 0 && !embeddedClcNativeReady
                            ? "Two hard-markerless packets contain weak embedded CLC_Move-looking slices; recover a TF.elf offset rule before accepting those as native input."
                            : embeddedClcNativeReady
                                ? "Promote embedded CLC_Move candidates only after a recovered TF.elf offset rule proves the payload boundary."
                                : "Keep embedded CLC_Move candidate scans documented as negative/weak evidence.",
                    "Keep native client-upload handling gated until the boundary and implementation are both proven."
                });
    }

    private static bool IsLongGameplayPhaseFile(
        JsonElement file,
        int minimumPackets,
        double minimumDurationMilliseconds)
    {
        if (file.ValueKind == JsonValueKind.Undefined)
        {
            return false;
        }

        return ReadString(file, "SessionClassification") == "long-gameplay-session"
            && file.GetProperty("SourcePacketCount").GetInt32() >= minimumPackets
            && file.GetProperty("DurationMilliseconds").GetDouble() >= minimumDurationMilliseconds
            && HasPhase(file, "inferred-source-handoff-setup")
            && HasPhase(file, "inferred-loading-or-motd-transfer")
            && HasPhase(file, "inferred-gameplay-steady-traffic");
    }

    private static object SourceGameplayPhaseEvidence(JsonElement file)
    {
        if (file.ValueKind == JsonValueKind.Undefined)
        {
            return new { Present = false };
        }

        return new
        {
            Present = true,
            File = ReadString(file, "File"),
            SessionClassification = ReadString(file, "SessionClassification"),
            SourcePacketCount = file.GetProperty("SourcePacketCount").GetInt32(),
            DurationMilliseconds = file.GetProperty("DurationMilliseconds").GetDouble(),
            InferredWindowLabels = file.GetProperty("InferredWindows").EnumerateArray()
                .Select(static window => ReadString(window, "Label"))
                .ToArray()
        };
    }

    private static bool HasPhase(JsonElement file, string phase)
    {
        return file.GetProperty("InferredWindows").EnumerateArray()
            .Any(window => ReadString(window, "Label") == phase);
    }

    private static AcceptanceGate LiveRpcs3SteadyGameplayGate(JsonElement? root)
    {
        if (root is not null)
        {
            var sourceEvidence = root.Value.GetProperty("SourceEvidence").EnumerateArray().ToArray();
            var gameManagerEvents = root.Value.GetProperty("GameManagerEvents");
            var highestPhase = HighestLiveSourcePhase(sourceEvidence, gameManagerEvents);
            var hasSteadyGameplay = sourceEvidence.Any(static evidence =>
                evidence.TryGetProperty("HasSteadyGameplayTraffic", out var value)
                && value.ValueKind == JsonValueKind.True)
                || HasGeneratedSteadyGameplayEvidence(gameManagerEvents);
            var hasSourceMotd = root.Value.GetProperty("HasSourceMotdTraffic").GetBoolean();
            var hasNativeSourceEvidence = HasNativeSourceEvidence(sourceEvidence)
                || HasGeneratedNativeSourceEvidence(gameManagerEvents);
            var status = hasSteadyGameplay && hasNativeSourceEvidence ? "passed" : hasSourceMotd ? "incomplete" : "missing-evidence";
            return new AcceptanceGate(
                "live-rpcs3-reaches-steady-source-gameplay",
                status,
                "Live RPCS3 evidence shows the TF2 PS3 client progressing beyond Source handoff/loading into steady Source gameplay traffic.",
                new
                {
                    HasSourceMotdTraffic = hasSourceMotd,
                    HighestSourceSessionPhase = highestPhase,
                    HasSteadyGameplayTraffic = hasSteadyGameplay,
                    HasNativeSourceEvidence = hasNativeSourceEvidence,
                    HasGeneratedTerminalObjectStreamBootstrap = ReadBool(gameManagerEvents, "HasGeneratedTerminalObjectStreamBootstrap"),
                    GeneratedTerminalObjectStreamBootstrapCount = ReadInt(gameManagerEvents, "GeneratedTerminalObjectStreamBootstrapCount"),
                    HasGeneratedSteadyGameplayEvent = ReadBool(gameManagerEvents, "HasGeneratedSteadyGameplayEvent"),
                    GeneratedSteadyGameplayEventCount = ReadInt(gameManagerEvents, "GeneratedSteadyGameplayEventCount"),
                    MaxGeneratedSourcePayloadLength = ReadInt(gameManagerEvents, "MaxGeneratedSourcePayloadLength"),
                    OversizedGeneratedSourcePayloadCount = ReadInt(gameManagerEvents, "OversizedGeneratedSourcePayloadCount"),
                    SourceEvidence = root.Value.GetProperty("SourceEvidence")
                },
                status == "passed"
                    ? Array.Empty<string>()
                    : hasSteadyGameplay && !hasNativeSourceEvidence
                        ? new[] { "Replay-only Source evidence reached steady traffic; repeat with the default generated native Source responder to prove native backend progress." }
                        : new[]
                        {
                            "Run a live RPCS3 test with Source replay/backend evidence enabled.",
                            "Confirm Source evidence reaches inferred-gameplay-steady-traffic rather than stopping in setup or loading/MOTD-transfer."
                        });
        }

        return new AcceptanceGate(
            "live-rpcs3-reaches-steady-source-gameplay",
            "missing-evidence",
            "Live RPCS3 evidence shows the TF2 PS3 client progressing beyond Source handoff/loading into steady Source gameplay traffic.",
            new
            {
                Evidence = "No current live RPCS3 Source replay evidence proves steady Source gameplay traffic."
            },
            new[]
            {
                "Run a live RPCS3 test and analyze the Source replay JSONL with LiveHandoffEvidenceAnalyzer.",
                "Require HighestSourceSessionPhase to reach inferred-gameplay-steady-traffic."
            });
    }

    private static bool HasNativeSourceEvidence(JsonElement[] sourceEvidence)
    {
        return sourceEvidence.Any(static evidence =>
            evidence.TryGetProperty("HasSourceOrMotdTraffic", out var hasTraffic)
            && hasTraffic.ValueKind == JsonValueKind.True
            && !IsReplaySourceEvidence(evidence));
    }

    private static bool HasGeneratedNativeSourceEvidence(JsonElement gameManagerEvents)
    {
        return gameManagerEvents.TryGetProperty("HasGeneratedNativeSourceEvent", out var hasGenerated)
            && hasGenerated.ValueKind == JsonValueKind.True;
    }

    private static bool HasGeneratedSteadyGameplayEvidence(JsonElement gameManagerEvents)
    {
        return gameManagerEvents.TryGetProperty("HasGeneratedSteadyGameplayEvent", out var hasGenerated)
            && hasGenerated.ValueKind == JsonValueKind.True;
    }

    private static bool IsReplaySourceEvidence(JsonElement evidence)
    {
        if (ReadString(evidence, "Path").Contains("source-replay", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (evidence.TryGetProperty("SourceReplayEventCount", out var replayCount)
            && replayCount.ValueKind == JsonValueKind.Number
            && replayCount.GetInt32() > 0)
        {
            return true;
        }

        if (!evidence.TryGetProperty("SampleEvidence", out var samples) || samples.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return samples.EnumerateArray().Any(static sample =>
        {
            var text = sample.GetString() ?? "";
            return text.Contains("source-replay", StringComparison.OrdinalIgnoreCase)
                || text.Contains("PCAP Source replay", StringComparison.OrdinalIgnoreCase)
                || text.Contains("replay script=", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string HighestSourcePhase(JsonElement[] sourceEvidence)
    {
        return sourceEvidence
            .Select(static evidence => evidence.TryGetProperty("HighestSourceSessionPhase", out var phase) ? phase.GetString() ?? "" : "")
            .Where(static phase => phase.Length > 0)
            .OrderByDescending(PhaseRank)
            .FirstOrDefault() ?? "";
    }

    private static string HighestLiveSourcePhase(JsonElement[] sourceEvidence, JsonElement gameManagerEvents)
    {
        var phases = sourceEvidence
            .Select(static evidence => evidence.TryGetProperty("HighestSourceSessionPhase", out var phase) ? phase.GetString() ?? "" : "")
            .Append(ReadString(gameManagerEvents, "HighestGeneratedSourceSessionPhase"))
            .Where(static phase => phase.Length > 0);
        return phases
            .OrderByDescending(PhaseRank)
            .FirstOrDefault() ?? "";
    }

    private static int PhaseRank(string phase)
    {
        return phase switch
        {
            "inferred-gameplay-steady-traffic" => 3,
            "inferred-loading-or-motd-transfer" => 2,
            "inferred-source-handoff-setup" => 1,
            _ => 0
        };
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.ToString() : "";
    }

    private static string ReadString(JsonElement? element, string property)
    {
        return element is not null && element.Value.TryGetProperty(property, out var value) ? value.ToString() : "";
    }

    private static bool ReadBool(JsonElement? element, string property)
    {
        return element is not null
            && element.Value.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static int ReadInt(JsonElement? element, string property)
    {
        return element is not null
            && element.Value.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 0;
    }

    private static int? ReadNullableInt(JsonElement? element, string property)
    {
        return element is not null
            && element.Value.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : null;
    }

    private static AcceptanceGate SourceBridgeContractGate(JsonElement? root)
    {
        if (root is null)
        {
            return Missing("pcap-source-bridge-contract-supports-hidden-backend", "Run scripts/analyze-source-bridge-contract.sh to produce Source bridge topology evidence.");
        }

        var summary = root.Value.GetProperty("Summary");
        var activeSourceFlows = summary.GetProperty("ActiveSourceFlowCount").GetInt32();
        var requiresPublicSource = summary.GetProperty("RequiresPublicSourceEndpointCount").GetInt32();
        var doesNotRequirePublicSource = summary.GetProperty("DoesNotRequirePublicSourceEndpointCount").GetInt32();
        var sequenceEstablished = summary.GetProperty("SourceTransportSequenceEstablishedCount").GetInt32();
        var mixed = summary.GetProperty("MixedCaptureSplitRequiredCount").GetInt32();
        var passed = activeSourceFlows > 0
            && requiresPublicSource == 0
            && doesNotRequirePublicSource > 0
            && sequenceEstablished == activeSourceFlows;

        return new AcceptanceGate(
            "pcap-source-bridge-contract-supports-hidden-backend",
            passed ? "passed" : "incomplete",
            "PCAP Source/gameplay traffic supports the hidden backend model: the PS3 client stays on visible GameManager/game-server endpoints while Source backend semantics sit behind the bridge.",
            new
            {
                ActiveSourceFlowCount = activeSourceFlows,
                SameVisibleFlowCompatibleCount = summary.GetProperty("SameVisibleFlowCompatibleCount").GetInt32(),
                MultiVisiblePortCompatibleCount = summary.GetProperty("MultiVisiblePortCompatibleCount").GetInt32(),
                MixedCaptureSplitRequiredCount = mixed,
                RequiresPublicSourceEndpointCount = requiresPublicSource,
                DoesNotRequirePublicSourceEndpointCount = doesNotRequirePublicSource,
                SourceTransportSequenceEstablishedCount = sequenceEstablished,
                CompatibilityCounts = summary.GetProperty("CompatibilityCounts")
            },
            passed
                ? mixed > 0
                    ? new[] { "Split mixed captures before deriving backend behavior from their secondary visible game-server address pairs." }
                    : Array.Empty<string>()
                : new[]
                {
                    "Resolve any active Source/gameplay flow that appears to require a public Source endpoint.",
                    "Establish the PS3 Source/gameplay transport sequence model for every active source flow."
                });
    }

    private static AcceptanceGate Missing(string id, string nextStep)
    {
        return new AcceptanceGate(id, "missing-evidence", id, new { }, new[] { nextStep });
    }

    private static readonly int[] RequiredNativeTypes = { 2, 3, 4, 5, 8, 9 };
    private static readonly int[] RequiredTf2NativeTypes = { 2, 3, 4, 5, 8, 9, 11 };
    private static readonly string[] DeferredTf2NativeRoles =
    {
        "player-inactivity-timeout"
    };
    private static readonly string[] RequiredScenarioFamilies =
    {
        "quick-match-to-motd",
        "custom-match-join-to-motd",
        "create-and-join",
        "2fort-play",
        "dustbowl-play",
        "connection"
    };
    private static readonly string[] RequiredTf2CreateKeys =
    {
        "B-U-MapName",
        "B-U-IsRanked",
        "B-U-Rating",
        "B-U-Duration",
        "B-U-FlagCapture",
        "B-U-Location",
        "B-U-MaxGameTime",
        "B-U-NumRounds",
        "B-U-ServerPop",
        "B-U-AutoBalance",
        "V"
    };
    private static readonly string[] RequiredServerDllSimulationAreas =
    {
        "server-lifecycle",
        "player-command-simulation",
        "round-state-machine",
        "map-objectives",
        "combat-weapons-projectiles",
        "engineer-buildings",
        "ranked-stats"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

public sealed record AcceptanceGate(
    string Id,
    string Status,
    string Requirement,
    object Evidence,
    string[] NextWork);
