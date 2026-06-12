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
        string? sourceOwnerForwardContextPath = null)
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
            SourceMarkerlessInputBoundaryGate(sourceConnectedWrapperBoundaryDoc?.RootElement, sourceSlot70Param2BuilderDoc?.RootElement, sourceBitstreamHelperContractDoc?.RootElement, sourceMarkerlessParam2BuilderDoc?.RootElement, sourcePayloadObjectDispatchDoc?.RootElement, sourceOwnerControlSubobjectDoc?.RootElement, sourceOwnerForwardContextDoc?.RootElement),
            LiveRpcs3Gate(liveDoc?.RootElement),
            LiveRpcs3SteadyGameplayGate(liveDoc?.RootElement)
        };

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
                SourceOwnerForwardContext = sourceOwnerForwardContextPath ?? ""
            },
            summary = new
            {
                GateCount = gates.Length,
                PassedGates = gates.Count(static gate => gate.Status == "passed"),
                IncompleteGates = gates.Count(static gate => gate.Status == "incomplete"),
                MissingEvidenceGates = gates.Count(static gate => gate.Status == "missing-evidence")
            },
            gates
        }, JsonOptions));
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
            var hasNativeSourceEvidence = HasNativeSourceEvidence(sourceEvidence);
            var highestPhase = HighestSourcePhase(sourceEvidence);
            var hasSteadyGameplay = sourceEvidence.Any(static evidence =>
                evidence.TryGetProperty("HasSteadyGameplayTraffic", out var value)
                && value.ValueKind == JsonValueKind.True);
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
                    GameManagerEvents = root.Value.GetProperty("GameManagerEvents"),
                    SourceEvidence = root.Value.GetProperty("SourceEvidence"),
                    HighestSourceSessionPhase = highestPhase,
                    HasSteadyGameplayTraffic = hasSteadyGameplay,
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
        var nativeComplete = summary.GetProperty("NativeCompleteObligationCount").GetInt32();
        var incomplete = summary.GetProperty("IncompleteObligationCount").GetInt32();
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
                NativeCompleteObligationCount = nativeComplete,
                IncompleteObligationCount = incomplete,
                MissingOfficialMarkerCount = missingOfficial,
                MissingImplementationMarkerCount = missingImplementation,
                IncompleteObligations = incompleteObligations
            },
            passed
                ? Array.Empty<string>()
                : incompleteObligations
                    .Select(static obligation => $"Complete native EA server.dll obligation: {obligation}.")
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
            && generatedQueuedPrefixDebtBytes == 0
            && steadyPaddingRisk == 0
            && staticTemplates == 0;

        return new AcceptanceGate(
            "tf2ps3-source-loading-mapload-has-no-fake-payloads",
            passed ? "passed" : "incomplete",
            "TF2 PS3 loading/map-load Source packets are generated from named native TF.elf/server.dll fields, not captured/static templates, deterministic filler, generated PlayerStateLink prefixes, or padded snapshots.",
            new
            {
                FrameCount = frameCount,
                EarlyLoadingFrameCount = summary.GetProperty("EarlyLoadingFrameCount").GetInt32(),
                SteadyFrameCount = summary.GetProperty("SteadyFrameCount").GetInt32(),
                ProductionFakeFrameCount = productionFake,
                NativeRecordWithGeneratedPrefixFrameCount = nativeRecordGeneratedPrefix,
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
                    "Replace BuildHighEntropyBinaryBody/BuildMixedBinaryBody loading payloads with native TF.elf queued Source field writers.",
                    "Replace generated PlayerStateLink prefix/trailing bytes with the queued-prefix/control writer around native PNG records.",
                    "Replace generated queued-prefix bodies with the native TF.elf peer-channel prefix/control writer.",
                    "Remove PadNativeWrappedPayload from steady snapshot/entity-delta production output by recovering exact native body sizing and field layout.",
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
        JsonElement? ownerForwardContextRoot)
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
            && ownerForwardContextNativeReady;
        var passed = oldVisibleBoundaryReady || payloadObjectBoundaryReady;

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
                NativeSourceInputReady = passed
            },
            passed
                ? Array.Empty<string>()
                : new[]
                {
                    payloadObjectDispatchRecovered
                        ? "Implement the recovered associated-object vtable +0x90 and owner vtable +0x08 payload-object consumers in the native Source responder."
                        : concretePayloadObjectBoundary
                            ? "Reduce 008be1e8 payload-object dispatch into concrete associated/owner consumer contracts."
                        : "Recover the engine wrapper/transform that maps the 11,874 hard markerless PCAP bodies into the connected attached-frame reader or slot +0x70 bitreader.",
                    ownerForwardContextCategory5Recovered
                        ? "Resolve the exact dynamic category-5 handler behind *(iVar9 + 8) + 0x98 and implement the 008722a0 context handlers."
                        : "Reduce the 008722a0 owner-forward context handlers and identify the category-5 usercmd/CLC move route.",
                    param2ConstructionSite || concretePayloadObjectBoundary
                        ? "Verify recovered caller-side payload objects against PCAP/live client uploads."
                        : "Recover the caller-side param_2 construction site that fills the object consumed by 00a5d9e0.",
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
            var highestPhase = HighestSourcePhase(sourceEvidence);
            var hasSteadyGameplay = sourceEvidence.Any(static evidence =>
                evidence.TryGetProperty("HasSteadyGameplayTraffic", out var value)
                && value.ValueKind == JsonValueKind.True);
            var hasSourceMotd = root.Value.GetProperty("HasSourceMotdTraffic").GetBoolean();
            var hasNativeSourceEvidence = HasNativeSourceEvidence(sourceEvidence);
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
