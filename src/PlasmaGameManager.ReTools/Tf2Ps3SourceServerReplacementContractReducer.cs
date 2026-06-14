using System.Text.Json;
using System.Text.RegularExpressions;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceServerReplacementContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceServerReplacementContractReport> ReduceAsync(
        string nativeLifecyclePath,
        string officialServerDllRuntimePath,
        string officialServerDllObligationsPath,
        string nativeMessageContractPath,
        string objectStreamBootstrapPath,
        string queuedPrefixContractPath,
        string loadingReplacementPlanPath,
        string responderSourcePath,
        string outputPath,
        string? generatedPrefixRetailCrossmapPath = null,
        string? userCmdPhysicsAuditPath = null,
        string? sourceSendCallsiteMapPath = null)
    {
        using var lifecycle = JsonDocument.Parse(await File.ReadAllTextAsync(nativeLifecyclePath));
        using var runtime = JsonDocument.Parse(await File.ReadAllTextAsync(officialServerDllRuntimePath));
        using var obligations = JsonDocument.Parse(await File.ReadAllTextAsync(officialServerDllObligationsPath));
        using var nativeMessages = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));
        using var objectStream = JsonDocument.Parse(await File.ReadAllTextAsync(objectStreamBootstrapPath));
        using var queuedPrefix = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));
        using var loadingPlan = JsonDocument.Parse(await File.ReadAllTextAsync(loadingReplacementPlanPath));
        generatedPrefixRetailCrossmapPath ??= Path.Combine(
            Path.GetDirectoryName(loadingReplacementPlanPath) ?? ".",
            "source-generated-prefix-retail-crossmap.json");
        using var generatedPrefixRetailCrossmap = File.Exists(generatedPrefixRetailCrossmapPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(generatedPrefixRetailCrossmapPath))
            : null;
        userCmdPhysicsAuditPath ??= Path.Combine(
            Path.GetDirectoryName(loadingReplacementPlanPath) ?? ".",
            "eatf2-serverdll-usercmd-physics-audit.json");
        using var userCmdPhysicsAudit = File.Exists(userCmdPhysicsAuditPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(userCmdPhysicsAuditPath))
            : null;
        sourceSendCallsiteMapPath ??= Path.Combine(
            Path.GetDirectoryName(loadingReplacementPlanPath) ?? ".",
            "source-send-callsite-map.json");
        using var sourceSendCallsiteMap = File.Exists(sourceSendCallsiteMapPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(sourceSendCallsiteMapPath))
            : null;

        var lifecycleSummary = lifecycle.RootElement.GetProperty("Summary");
        var runtimeSummary = runtime.RootElement.GetProperty("Summary");
        var obligationSummary = obligations.RootElement.GetProperty("Summary");
        var nativeMessageSummary = nativeMessages.RootElement.GetProperty("Summary");
        var objectStreamSummary = objectStream.RootElement.GetProperty("Summary");
        var queuedPrefixSummary = queuedPrefix.RootElement.GetProperty("Summary");
        var loadingSummary = loadingPlan.RootElement.GetProperty("Summary");
        var generatedPrefixRetailSummary = generatedPrefixRetailCrossmap?.RootElement.GetProperty("Summary") ?? default;
        var userCmdPhysicsSummary = userCmdPhysicsAudit?.RootElement.GetProperty("Summary") ?? default;
        var sourceSendCallsiteSummary = sourceSendCallsiteMap?.RootElement.GetProperty("Summary") ?? default;
        var generatedPrefixRetailEvidence = ReadGeneratedPrefixRetailEvidence(generatedPrefixRetailCrossmap?.RootElement).ToArray();
        var nativeQueueContract = ReadObject(queuedPrefix.RootElement, "NativeQueueContract");
        var nativeSendWrapperContract = ReadObject(queuedPrefix.RootElement, "NativeSendWrapperContract");
        var nativeQueueContractPresent = ReadString(nativeQueueContract, "EntryAddress") == "008b9f70";
        var nativeSendWrapperContractPresent = ReadString(nativeSendWrapperContract, "EntryAddress") == "008bc978";
        var initialBootstrapBatchCount = ReadInt(objectStreamSummary, "InitialBootstrapBatchCount");
        var initialBootstrapRawAppendCount = ReadInt(objectStreamSummary, "InitialBootstrapRawAppendCount");
        var postBootstrapRawAppendCount = ReadInt(objectStreamSummary, "PostBootstrapRawAppendCount");

        var source = File.Exists(responderSourcePath)
            ? await File.ReadAllTextAsync(responderSourcePath)
            : string.Empty;
        var nativeMessageSourcePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(responderSourcePath) ?? ".",
            "..",
            "PlasmaGameManager.Protocol",
            "Ps3SourceNativeMessages.cs"));
        var nativeMessageSource = File.Exists(nativeMessageSourcePath)
            ? await File.ReadAllTextAsync(nativeMessageSourcePath)
            : string.Empty;
        var payloadSemanticSourcePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(responderSourcePath) ?? ".",
            "..",
            "PlasmaGameManager.Protocol",
            "Ps3SourcePayloadSemantics.cs"));
        var payloadSemanticSource = File.Exists(payloadSemanticSourcePath)
            ? await File.ReadAllTextAsync(payloadSemanticSourcePath)
            : string.Empty;
        var classInfoFollowUpBody = ExtractMethodBody(source, "BuildNativeClassInfoFollowUpRawAppendFrame");
        var sourceAudit = new Tf2Ps3SourceServerReplacementSourceAudit(
            HasObjectStreamEncoderUse: source.Contains("Ps3SourceObjectStream.Encode", StringComparison.Ordinal),
            HasDiagnosticBootstrapBatches: source.Contains("BuildDiagnosticCriticalSourceObjectStreamBootstrapBatches", StringComparison.Ordinal),
            ObjectStreamBootstrapCallSiteCount: CountCallSites(source, "AddObjectStreamBootstrapResponses"),
            SnapshotFrameBodyCallSiteCount: CountCallSites(source, "BuildSnapshotFrameBody"),
            HighEntropyGeneratorCallSiteCount: CountCallSites(source, "BuildHighEntropyBinaryBody"),
            MixedBinaryGeneratorCallSiteCount: CountCallSites(source, "BuildMixedBinaryBody"),
            StaticTemplateFieldCount: StaticTemplateFieldRegex().Matches(source).Count,
            NativeDirectMessageBuilderCount: CountNativeDirectMessageMethods(nativeMessageSource, "Build"),
            NativeDirectMessageDecoderCount: CountNativeDirectMessageMethods(nativeMessageSource, "TryDecode"),
            NativeObjectStreamSemanticDecoderCount: CountNativeObjectStreamSemanticDecoders(payloadSemanticSource),
            HasClassInfoFollowUpRawAppendHook: source.Contains("BuildNativeClassInfoFollowUpRawAppendFrame", StringComparison.Ordinal),
            ClassInfoFollowUpUsesStringTableApproximation: classInfoFollowUpBody.Contains("BuildUpdateStringTableFrame", StringComparison.Ordinal),
            ClassInfoFollowUpUsesGeneratedServerSignonBuffer: classInfoFollowUpBody.Contains("BuildNativeServerSignonBufferFrame", StringComparison.Ordinal)
                && source.Contains("BuildGameEventListFrame(gameEventDescriptors)", StringComparison.Ordinal),
            ClassInfoFollowUpSupportsResourceGameEventList: source.Contains("Tf2SourceGameEventResourceCatalog.LoadOrDefault", StringComparison.Ordinal),
            ClassInfoFollowUpSupportsNativeInitSounds: source.Contains("BuildNativeServerSignonMapInitFrames", StringComparison.Ordinal)
                && source.Contains("BuildSoundsFrame(", StringComparison.Ordinal),
            ClassInfoFollowUpSupportsNativeBspDecals: source.Contains("BuildNativeServerSignonMapInitFrames", StringComparison.Ordinal)
                && source.Contains("BuildBspDecalFrame(", StringComparison.Ordinal));

        var productionFakeFrames = ReadInt(loadingSummary, "ProductionFakeFrameCount");
        var nativeRecordGeneratedPrefixFrames = ReadInt(loadingSummary, "NativeRecordWithGeneratedPrefixFrameCount");
        var generatedQueuedPrefixBytes = ReadInt(loadingSummary, "GeneratedQueuedPrefixDebtBytes");
        var steadyPaddingRisk = ReadInt(loadingSummary, "SteadyNativeSnapshotPaddingRiskCount");
        var staticTemplates = ReadInt(loadingSummary, "StaticHexTemplateCount");
        var loadingAcceptanceBlocking = ReadInt(loadingSummary, "AcceptanceBlockingCount");
        var nativeMapLoadReady = productionFakeFrames == 0
            && nativeRecordGeneratedPrefixFrames == 0
            && generatedQueuedPrefixBytes == 0
            && steadyPaddingRisk == 0
            && staticTemplates == 0
            && loadingAcceptanceBlocking == 0;

        var routes = new[]
        {
            new Tf2Ps3SourceServerReplacementRoute(
                "client-input-usercmd",
                "client-to-server",
                "CLC_Move / CUserCmd -> server.dll ProcessUsercmds / PhysicsSimulate",
                "TF.elf CLC_Move contract plus official EA server.dll usercmd decoder",
                ReadInt(runtimeSummary, "RuntimeContractsWithEvidence") > 0 && ReadInt(nativeMessageSummary, "FieldReducedMessageCount") > 0,
                "native-decoder-present-authoritative-simulation-partial",
                "Input is no longer the main map-load blocker. The remaining work is full Source movement/collision/combat fidelity after the client reaches gameplay."),
            new Tf2Ps3SourceServerReplacementRoute(
                "signon-object-stream-bootstrap",
                "server-to-client",
                "CBaseClient::SendSignonData / SendServerInfo -> NET_SignonState/SVC_ServerInfo/SVC_SendTable/SVC_ClassInfo/SVC_CreateStringTable/SVC_UpdateStringTable -> 00a55e60 object stream",
                "TF.elf object-stream bootstrap map and critical message I/O contract",
                ReadBool(objectStreamSummary, "BootstrapUsesObjectStreamSender")
                    && ReadInt(objectStreamSummary, "ObjectStreamEnvelopeFieldCount") == 9
                    && sourceAudit.HasObjectStreamEncoderUse,
                productionFakeFrames == 0
                    ? "native-production-ready"
                    : "native-encoder-present-production-loading-still-fake",
                "This route must replace the early map-load bytes. It is distinct from steady snapshots; injecting snapshot frames here regressed the responder tests."),
            new Tf2Ps3SourceServerReplacementRoute(
                "queued-peer-prefix-control",
                "server-to-client",
                "008bc978 -> 008b9f70 / 008bb058 / 008bc490 queued peer-channel prefix and control bytes around COc/PNG/DSC records",
                "TF.elf queued-prefix reports plus PCAP corpus",
                ReadInt(queuedPrefixSummary, "QueuedChunkPacketCount") > 0,
                generatedQueuedPrefixBytes == 0 && ReadInt(queuedPrefixSummary, "OpaquePrefixBytes") == 0
                    ? "native-production-ready"
                    : "native-tail-records-present-prefix-control-incomplete",
                "The record tails are mostly decoded, but the prefix/control writer still needs native field generation instead of deterministic or captured-looking bytes."),
            new Tf2Ps3SourceServerReplacementRoute(
                "steady-gameplay-snapshot",
                "server-to-client",
                "CBaseClient::SendSnapshot -> 00a61150 -> 00a5fb80 -> 008bc978",
                "TF.elf snapshot placement and packet-entities placement reports",
                ReadBool(lifecycleSummary, "NativeGameplayRouteIdentified"),
                steadyPaddingRisk == 0
                    ? "native-production-ready"
                    : "native-snapshot-generator-present-padding-risk-remains",
                "Steady gameplay output is the correct home for entity deltas, but any padded or filler packet body still blocks a fully native claim."),
            new Tf2Ps3SourceServerReplacementRoute(
                "official-gameplay-state",
                "server-side",
                "Official EA server.dll TF rules, entities, weapons, objectives, stats, and GameManager bridge",
                "server.dll runtime/native-obligation reports",
                ReadInt(obligationSummary, "ObligationsWithOfficialEvidence") == ReadInt(obligationSummary, "ObligationCount"),
                ReadInt(obligationSummary, "IncompleteObligationCount") == 0
                    ? "native-complete"
                    : "official-contract-known-implementation-partial",
                "A native replacement can load only after signon bytes are accepted; a playable replacement still needs these Source game rules to become authoritative.")
        };
        var rawAppendCacheContracts = BuildRawAppendCacheContracts();

        var report = new Tf2Ps3SourceServerReplacementContractReport(
            "tf2ps3-native-source-server-replacement-contract",
            "Combines TF.elf Source transport reverse engineering with official EA TF2 server.dll obligations. This report separates the native replacement into input, signon bootstrap, queued-prefix/control, steady snapshot, and authoritative gameplay routes.",
            new Tf2Ps3SourceServerReplacementSummary(
                TfElfAnchorsPresent: ReadInt(lifecycleSummary, "TfElfAnchorsPresent"),
                OfficialServerDllAnchorsPresent: ReadInt(lifecycleSummary, "OfficialServerDllAnchorsPresent"),
                FieldReducedNativeMessages: ReadInt(nativeMessageSummary, "FieldReducedMessageCount"),
                ObjectStreamEnvelopeFields: ReadInt(objectStreamSummary, "ObjectStreamEnvelopeFieldCount"),
                ProductionFakeLoadingFrames: productionFakeFrames,
                NativeRecordWithGeneratedPrefixFrameCount: nativeRecordGeneratedPrefixFrames,
                GeneratedQueuedPrefixDebtBytes: generatedQueuedPrefixBytes,
                SteadySnapshotPaddingRiskFrames: steadyPaddingRisk,
                StaticTemplateCount: staticTemplates,
                LoadingAcceptanceBlockingCount: loadingAcceptanceBlocking,
                ObjectStreamInitialBootstrapBatches: initialBootstrapBatchCount,
                ObjectStreamInitialRawAppends: initialBootstrapRawAppendCount,
                ObjectStreamPostBootstrapRawAppends: postBootstrapRawAppendCount,
                NativeDirectMessageBuilders: sourceAudit.NativeDirectMessageBuilderCount,
                NativeDirectMessageDecoders: sourceAudit.NativeDirectMessageDecoderCount,
                NativeObjectStreamSemanticDecoders: sourceAudit.NativeObjectStreamSemanticDecoderCount,
                GeneratedQueuedPrefixExactRetailShapeTargets: ReadInt(generatedPrefixRetailSummary, "ExactRetailShapeTargetCount"),
                GeneratedQueuedPrefixOpenRetailShapeTargets: ReadInt(generatedPrefixRetailSummary, "TargetWithoutExactRetailShapeCount"),
                GeneratedQueuedPrefixExactRetailSampleCount: ReadInt(generatedPrefixRetailSummary, "ExactRetailDirectDatagramSampleCount"),
                SourceSendCallsiteCount: ReadInt(sourceSendCallsiteSummary, "SourceSendCallsiteCount"),
                SourceSendUpstreamWriterOwnedCallsites: ReadInt(sourceSendCallsiteSummary, "UpstreamWriterOwnedCallsiteCount"),
                SourceSendTransportOnlyCallsites: ReadInt(sourceSendCallsiteSummary, "TransportOnlyCallsiteCount"),
                SourceSnapshotFrameFunctionCount: ReadInt(sourceSendCallsiteSummary, "SnapshotFrameFunctionCount"),
                OfficialUserCmdPhysicsCompleteItems: ReadInt(userCmdPhysicsSummary, "CompleteItemCount"),
                OfficialUserCmdPhysicsPartialItems: ReadInt(userCmdPhysicsSummary, "PartialItemCount"),
                OfficialUserCmdPhysicsMissingItems: ReadInt(userCmdPhysicsSummary, "MissingItemCount"),
                QueuedPeerNativeQueueContractPresent: nativeQueueContractPresent,
                QueuedPeerInlinePayloadThreshold: ReadInt(nativeQueueContract, "InlinePayloadThreshold"),
                QueuedPeerMaxPayloadBytesExclusive: ReadInt(nativeQueueContract, "MaxPayloadBytesExclusive"),
                QueuedPeerSendWrapperContractPresent: nativeSendWrapperContractPresent,
                ImmediateSendChannelZeroPort: Ps3SourceSendWrapper.ImmediateSendChannelZeroPort,
                ImmediateSendChannelOnePort: Ps3SourceSendWrapper.ImmediateSendChannelOnePort,
                ReliablePeerPrimaryQueueOffset: Ps3SourceSendWrapper.ReliablePeerPrimaryQueueOffset,
                ReliablePeerSecondaryQueueOffset: Ps3SourceSendWrapper.ReliablePeerSecondaryQueueOffset,
                ReliablePeerLoopbackAddress: Ps3SourceSendWrapper.ReliablePeerLoopbackAddress,
                ReliablePeerPortMismatchError: Ps3SourceSendWrapper.ReliablePeerPortMismatchError,
                OfficialServerDllIncompleteObligations: ReadInt(obligationSummary, "IncompleteObligationCount"),
                NativeMapLoadReady: nativeMapLoadReady),
            sourceAudit,
            generatedPrefixRetailEvidence,
            rawAppendCacheContracts,
            routes,
            [
                "Do not replace early signon/bootstrap bytes with steady snapshot frames; TF.elf routes them through 00a55e60 object-stream records.",
                "The recovered initial bootstrap is three kind-1 object-stream batches; batch 2 must append the server signon init buffer at +0xd8/+0xe4 after SVC_ClassInfo.",
                "The current client-input/usercmd path is substantially reduced from TF.elf and server.dll; live map-load failures are now on server-to-client Source output.",
                nativeMapLoadReady
                    ? $"Direct native Source message families 0x41/0x44/0x45/0x49/0x6b now have {sourceAudit.NativeDirectMessageBuilderCount} builders and {sourceAudit.NativeDirectMessageDecoderCount} decoders; generated loading/map-load payload debt is zero."
                    : $"Direct native Source message families 0x41/0x44/0x45/0x49/0x6b now have {sourceAudit.NativeDirectMessageBuilderCount} builders and {sourceAudit.NativeDirectMessageDecoderCount} decoders; remaining map-load debt is outside those field-order contracts.",
                $"TF.elf object-stream bootstrap packets now have {sourceAudit.NativeObjectStreamSemanticDecoderCount} semantic decoder(s) in live evidence; remaining bootstrap risk is field completeness and generated filler around them.",
                GeneratedPrefixRetailFinding(generatedPrefixRetailEvidence),
                SourceSendCallsiteFinding(sourceSendCallsiteSummary),
                UserCmdPhysicsFinding(userCmdPhysicsSummary),
                nativeMapLoadReady
                    ? "The queued peer stream no longer has generated native-record prefix/trailing debt in the live responder; remaining work is native field completeness, gameplay, and client-input behavior."
                    : "The queued peer stream still has native COc/PNG/DSC tails but non-native prefix/control debt, so it is not a pure packet replay problem and not a PC srcds forwarding problem.",
                $"TF.elf reliable peer routing is now reduced to channel ports 0x{Ps3SourceSendWrapper.ImmediateSendChannelZeroPort:x}/0x{Ps3SourceSendWrapper.ImmediateSendChannelOnePort:x}, local queue offsets +0x{Ps3SourceSendWrapper.ReliablePeerPrimaryQueueOffset:x}/+0x{Ps3SourceSendWrapper.ReliablePeerSecondaryQueueOffset:x}, loopback 0x{Ps3SourceSendWrapper.ReliablePeerLoopbackAddress:x8}, and mismatch error 0x{Ps3SourceSendWrapper.ReliablePeerPortMismatchError:x8}.",
                ClassInfoFollowUpFinding(sourceAudit),
                nativeMapLoadReady
                    ? "Native map-load readiness is true: production fake loading frames, generated native-record prefixes, generated queued prefixes, static templates, steady snapshot padding risk, and loading acceptance blockers are all zero. Live testing should now focus on the remaining gameplay/native-field obligations."
                    : $"Native map-load readiness is false: production fake loading frames={productionFakeFrames}, generated native-record-prefix frames={nativeRecordGeneratedPrefixFrames}, generated queued-prefix debt bytes={generatedQueuedPrefixBytes}, static templates={staticTemplates}, steady snapshot padding risk={steadyPaddingRisk}, loading acceptance blockers={loadingAcceptanceBlocking}."
            ],
            [
                "Implement production object-stream bootstrap at the early map-load handoff using the recovered NET/SVC field writers and Ps3SourceObjectStream envelope.",
                "Continue reducing the 00a56cb0 +0xd8/+0xe4 signon buffer producer until the generated SVC_VoiceInit/SVC_GameEventList/SVC_Sounds/SVC_BSPDecal path also covers any remaining map-specific init messages.",
                "Recover the queued peer prefix/control writer around the already decoded COc/PNG/DSC record tails.",
                "Remove deterministic high-entropy/mixed-binary production generators after the corresponding native writers are in place.",
                "Tighten steady snapshot generation so every packet body is semantic entity delta data, with no padding or filler risk.",
                "After map load reaches gameplay, expand the official server.dll obligations from partial simulation to authoritative TF rules, weapons, objectives, and ranked stats."
            ]
        );

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static int CountCallSites(string source, string methodName)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        var matches = Regex.Matches(source, $@"\b{Regex.Escape(methodName)}\s*\(").Count;
        return Math.Max(0, matches - 1);
    }

    private static int CountNativeDirectMessageMethods(string source, string prefix)
    {
        if (source.Length == 0)
        {
            return 0;
        }

        var count = 0;
        foreach (var suffix in new[] { "HudPlayerObjectUpdate", "PlayerSummary", "ResourceStringTable", "ServerInfo", "GameplayStatTimesUsed" })
        {
            if (source.Contains($"{prefix}{suffix}(", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static string ExtractMethodBody(string source, string methodName)
    {
        if (source.Length == 0)
        {
            return string.Empty;
        }

        var methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        if (methodIndex < 0)
        {
            return string.Empty;
        }

        var bodyStart = source.IndexOf('{', methodIndex);
        if (bodyStart < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            switch (source[i])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return source[bodyStart..(i + 1)];
                    }

                    break;
            }
        }

        return source[bodyStart..];
    }

    private static int CountNativeObjectStreamSemanticDecoders(string source)
    {
        return source.Contains("NativeObjectStreamBootstrap", StringComparison.Ordinal)
            && source.Contains("Ps3SourceObjectStream.TryDecode", StringComparison.Ordinal)
            && source.Contains("NativeObjectStreamSummary", StringComparison.Ordinal)
            ? 1
            : 0;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }

    private static JsonElement ReadObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return default;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedPrefixRetailReplacementEvidence> ReadGeneratedPrefixRetailEvidence(JsonElement? root)
    {
        if (root is null
            || root.Value.ValueKind != JsonValueKind.Object
            || !root.Value.TryGetProperty("Targets", out var targets)
            || targets.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var target in targets.EnumerateArray())
        {
            var family = ReadString(target, "Family");
            if (family.Length == 0)
            {
                continue;
            }

            yield return new Tf2Ps3SourceGeneratedPrefixRetailReplacementEvidence(
                family,
                ReadString(target, "Method"),
                ReadString(target, "RetailShapeKey"),
                ReadInt(target, "PrefixByteLength"),
                ReadInt(target, "RecordCount"),
                ReadInt(target, "ExactRetailSampleCount"),
                ReadString(target, "Conclusion"),
                ReadString(target, "NativeNextAction"));
        }
    }

    private static string GeneratedPrefixRetailFinding(
        Tf2Ps3SourceGeneratedPrefixRetailReplacementEvidence[] evidence)
    {
        if (evidence.Length == 0)
        {
            return "No generated queued-prefix retail targets remain in this contract report; current generated-prefix debt is zero.";
        }

        var exact = evidence.Count(static item => item.ExactRetailSampleCount > 0);
        var open = evidence.Where(static item => item.ExactRetailSampleCount == 0).Select(static item => item.Family).ToArray();
        return open.Length == 0
            ? $"All {exact} generated queued-prefix debt families have exact retail packet-shape evidence; remaining work is native writer implementation, not shape discovery."
            : $"{exact} generated queued-prefix debt families have exact retail packet-shape evidence; {open.Length} still lacks an exact retail shape ({string.Join(", ", open)}) and needs TF.elf/server.dll writer recovery next.";
    }

    private static string UserCmdPhysicsFinding(JsonElement summary)
    {
        if (summary.ValueKind != JsonValueKind.Object)
        {
            return "Official server.dll usercmd/PhysicsSimulate audit was unavailable; authoritative gameplay completeness is unknown in this contract report.";
        }

        var complete = ReadInt(summary, "CompleteItemCount");
        var partial = ReadInt(summary, "PartialItemCount");
        var missing = ReadInt(summary, "MissingItemCount");
        return $"Official server.dll usercmd/PhysicsSimulate audit is {complete} complete, {partial} partial, {missing} missing; input decoding is mostly recovered, but exact Source movement/world physics remains partial.";
    }

    private static string SourceSendCallsiteFinding(JsonElement summary)
    {
        if (summary.ValueKind != JsonValueKind.Object)
        {
            return "TF.elf Source send callsite map was unavailable; cannot distinguish payload writer debt from transport wrapper debt in this report.";
        }

        var total = ReadInt(summary, "SourceSendCallsiteCount");
        var upstreamOwned = ReadInt(summary, "UpstreamWriterOwnedCallsiteCount");
        var transportOnly = ReadInt(summary, "TransportOnlyCallsiteCount");
        var snapshot = ReadInt(summary, "SnapshotFrameFunctionCount");
        var wrap = ReadInt(summary, "CallsitesRequestingWrapOrCompression");
        return $"TF.elf Source send callsite map classifies {total} 008bc978 callsite(s): {upstreamOwned} upstream-payload-writer owned, {transportOnly} transport-wrapper only, {snapshot} snapshot frame writer(s), {wrap} wrap/compression gate(s). The long map-load prefix/control bitstream is therefore a 00a61150/00a5fb80 snapshot writer target, not an 008bc978/008b9f70 queue target.";
    }

    private static string ClassInfoFollowUpFinding(Tf2Ps3SourceServerReplacementSourceAudit sourceAudit)
    {
        if (!sourceAudit.HasClassInfoFollowUpRawAppendHook)
        {
            return "TF.elf 00a56cb0 class-info follow-up append is still hidden inside bootstrap code; isolate it before continuing object-stream native recovery.";
        }

        return sourceAudit.ClassInfoFollowUpUsesStringTableApproximation
            ? "TF.elf 00a56cb0 class-info follow-up append is isolated as a named native hook, but its +0xd8/+0xe4 raw buffer is still approximated as a userinfo update-string-table payload."
            : sourceAudit.ClassInfoFollowUpUsesGeneratedServerSignonBuffer
                ? sourceAudit.ClassInfoFollowUpSupportsResourceGameEventList
                    ? sourceAudit.ClassInfoFollowUpSupportsNativeInitSounds && sourceAudit.ClassInfoFollowUpSupportsNativeBspDecals
                        ? "TF.elf 00a56cb0 class-info follow-up append is isolated as a named native hook and now emits a generated CBaseClient::SendSignonData-style server signon buffer with resource-backed event descriptors plus native init sound and static decal messages."
                        : "TF.elf 00a56cb0 class-info follow-up append is isolated as a named native hook and now emits a generated CBaseClient::SendSignonData-style server signon buffer with resource-backed event descriptors."
                    : "TF.elf 00a56cb0 class-info follow-up append is isolated as a named native hook and now emits a generated CBaseClient::SendSignonData-style server signon buffer."
            : "TF.elf 00a56cb0 class-info follow-up append is isolated as a named native hook and no longer uses the userinfo update-string-table approximation.";
    }

    private static Tf2Ps3SourceRawAppendCacheContract[] BuildRawAppendCacheContracts()
    {
        return
        [
            new(
                "class-info-followup-raw-buffer",
                "00a56cb0",
                "kind-1 object-stream bootstrap batch 2",
                "after SVC_ClassInfo and before NET_SignonState(4)",
                "0xd8",
                "0xe4",
                "*(iVar5 + 0xd8)",
                "*(iVar5 + 0xe4)",
                "Exact TF.elf decompile evidence: FUN_0086e5c8(bitbuffer, *(byte **)(iVar5 + 0xd8), *(int *)(iVar5 + 0xe4)). Source-engine crossmap: CGameClient::SendSignonData calls CBaseClient::SendSignonData, which sends m_Server->m_Signon after SVC_ClassInfo; SV_CreateBaseline writes SVC_VoiceInit and SVC_GameEventList into that signon buffer, while other init sounds/decals can also enter it."),
            new(
                "update-prelude-raw-buffer",
                "00a56a50",
                "kind-2 update/baseline send prelude",
                "before SVC_UpdateStringTable helper 008da9e0",
                "0x134",
                "0x140",
                "*(param_2 + 0x134)",
                "*(param_2 + 0x140)",
                "TF.elf appends this cache only when length is non-zero, then emits the update-string-table path."),
            new(
                "post-baseline-raw-buffer-a",
                "00a56a50",
                "kind-2 update/baseline send tail",
                "after 00a2c8a8/FUN_0086ba98 baseline helpers",
                "0x17c",
                "0x188",
                "*(param_2 + 0x17c)",
                "*(param_2 + 0x188)",
                "One of three cached raw tails appended after the baseline/send helpers."),
            new(
                "post-baseline-raw-buffer-b",
                "00a56a50",
                "kind-2 update/baseline send tail",
                "after 00a2c8a8/FUN_0086ba98 baseline helpers",
                "0x164",
                "0x170",
                "*(param_2 + 0x164)",
                "*(param_2 + 0x170)",
                "One of three cached raw tails appended after the baseline/send helpers."),
            new(
                "post-baseline-inline-buffer",
                "00a56a50",
                "kind-2 update/baseline send tail",
                "after 00a2c8a8/FUN_0086ba98 baseline helpers",
                "0x14c",
                "0x158",
                "*(param_2 + 0x14c)",
                "*((param_2 + 0x14c) + 0xc)",
                "TF.elf treats +0x14c as an inline buffer descriptor and appends its pointer plus descriptor length when non-zero.")
        ];
    }

    [GeneratedRegex(@"private\s+static\s+readonly\s+byte\[\]\s+\w+\s*=\s*Convert\.FromHexString", RegexOptions.Compiled)]
    private static partial Regex StaticTemplateFieldRegex();
}

public sealed record Tf2Ps3SourceServerReplacementContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceServerReplacementSummary Summary,
    Tf2Ps3SourceServerReplacementSourceAudit CurrentResponderSourceAudit,
    Tf2Ps3SourceGeneratedPrefixRetailReplacementEvidence[] GeneratedQueuedPrefixRetailEvidence,
    Tf2Ps3SourceRawAppendCacheContract[] RawAppendCacheContracts,
    Tf2Ps3SourceServerReplacementRoute[] Routes,
    string[] Findings,
    string[] NextImplementationOrder);

public sealed record Tf2Ps3SourceServerReplacementSummary(
    int TfElfAnchorsPresent,
    int OfficialServerDllAnchorsPresent,
    int FieldReducedNativeMessages,
    int ObjectStreamEnvelopeFields,
    int ProductionFakeLoadingFrames,
    int NativeRecordWithGeneratedPrefixFrameCount,
    int GeneratedQueuedPrefixDebtBytes,
    int SteadySnapshotPaddingRiskFrames,
    int StaticTemplateCount,
    int LoadingAcceptanceBlockingCount,
    int ObjectStreamInitialBootstrapBatches,
    int ObjectStreamInitialRawAppends,
    int ObjectStreamPostBootstrapRawAppends,
    int NativeDirectMessageBuilders,
    int NativeDirectMessageDecoders,
    int NativeObjectStreamSemanticDecoders,
    int GeneratedQueuedPrefixExactRetailShapeTargets,
    int GeneratedQueuedPrefixOpenRetailShapeTargets,
    int GeneratedQueuedPrefixExactRetailSampleCount,
    int SourceSendCallsiteCount,
    int SourceSendUpstreamWriterOwnedCallsites,
    int SourceSendTransportOnlyCallsites,
    int SourceSnapshotFrameFunctionCount,
    int OfficialUserCmdPhysicsCompleteItems,
    int OfficialUserCmdPhysicsPartialItems,
    int OfficialUserCmdPhysicsMissingItems,
    bool QueuedPeerNativeQueueContractPresent,
    int QueuedPeerInlinePayloadThreshold,
    int QueuedPeerMaxPayloadBytesExclusive,
    bool QueuedPeerSendWrapperContractPresent,
    int ImmediateSendChannelZeroPort,
    int ImmediateSendChannelOnePort,
    int ReliablePeerPrimaryQueueOffset,
    int ReliablePeerSecondaryQueueOffset,
    uint ReliablePeerLoopbackAddress,
    uint ReliablePeerPortMismatchError,
    int OfficialServerDllIncompleteObligations,
    bool NativeMapLoadReady);

public sealed record Tf2Ps3SourceServerReplacementSourceAudit(
    bool HasObjectStreamEncoderUse,
    bool HasDiagnosticBootstrapBatches,
    int ObjectStreamBootstrapCallSiteCount,
    int SnapshotFrameBodyCallSiteCount,
    int HighEntropyGeneratorCallSiteCount,
    int MixedBinaryGeneratorCallSiteCount,
    int StaticTemplateFieldCount,
    int NativeDirectMessageBuilderCount,
    int NativeDirectMessageDecoderCount,
    int NativeObjectStreamSemanticDecoderCount,
    bool HasClassInfoFollowUpRawAppendHook,
    bool ClassInfoFollowUpUsesStringTableApproximation,
    bool ClassInfoFollowUpUsesGeneratedServerSignonBuffer,
    bool ClassInfoFollowUpSupportsResourceGameEventList,
    bool ClassInfoFollowUpSupportsNativeInitSounds,
    bool ClassInfoFollowUpSupportsNativeBspDecals);

public sealed record Tf2Ps3SourceGeneratedPrefixRetailReplacementEvidence(
    string Family,
    string Method,
    string RetailShapeKey,
    int PrefixByteLength,
    int RecordCount,
    int ExactRetailSampleCount,
    string Conclusion,
    string NativeNextAction);

public sealed record Tf2Ps3SourceRawAppendCacheContract(
    string Name,
    string FunctionAddress,
    string Route,
    string Placement,
    string PointerOffset,
    string LengthOffset,
    string PointerExpression,
    string LengthExpression,
    string Evidence);

public sealed record Tf2Ps3SourceServerReplacementRoute(
    string RouteId,
    string Direction,
    string NativePath,
    string Evidence,
    bool EvidenceRecovered,
    string ImplementationStatus,
    string Meaning);
