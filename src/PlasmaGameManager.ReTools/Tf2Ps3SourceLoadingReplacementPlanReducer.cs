using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceLoadingReplacementPlanReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceLoadingReplacementPlanReport> ReduceAsync(
        string loadingFrameDebtPath,
        string queuedPrefixContractPath,
        string queuedPeerTargetMapPath,
        string criticalMessageIoContractPath,
        string objectStreamBootstrapMapPath,
        string snapshotPathMapPath,
        string nativeMessageContractPath,
        string outputPath)
    {
        using var loadingFrameDebt = JsonDocument.Parse(await File.ReadAllTextAsync(loadingFrameDebtPath));
        using var queuedPrefixContract = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPrefixContractPath));
        using var queuedPeerTargetMap = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPeerTargetMapPath));
        using var criticalMessageIoContract = JsonDocument.Parse(await File.ReadAllTextAsync(criticalMessageIoContractPath));
        using var objectStreamBootstrapMap = JsonDocument.Parse(await File.ReadAllTextAsync(objectStreamBootstrapMapPath));
        using var snapshotPathMap = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPathMapPath));
        using var nativeMessageContract = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));

        var loadingSummary = loadingFrameDebt.RootElement.GetProperty("Summary");
        var prefixSummary = queuedPrefixContract.RootElement.GetProperty("Summary");
        var peerSummary = queuedPeerTargetMap.RootElement.GetProperty("Summary");
        var objectSummary = objectStreamBootstrapMap.RootElement.GetProperty("Summary");
        var snapshotSummary = snapshotPathMap.RootElement.GetProperty("Summary");
        var nativeMessageSummary = nativeMessageContract.RootElement.GetProperty("Summary");
        var criticalMessageSummary = criticalMessageIoContract.RootElement.GetProperty("Summary");

        var nativeBoundaries = ReadNativeBoundaries(queuedPeerTargetMap.RootElement).ToArray();
        var objectStreamTargets = ReadObjectStreamTargets(objectStreamBootstrapMap.RootElement).ToArray();
        var snapshotFunctions = ReadSnapshotFunctions(snapshotPathMap.RootElement).ToArray();
        var messages = ReadNativeMessages(nativeMessageContract.RootElement).ToArray();
        var prefixDebts = ReadStaticPrefixDebts(queuedPrefixContract.RootElement).ToArray();
        var generatedPrefixDebts = ReadGeneratedPrefixDebts(queuedPrefixContract.RootElement).ToArray();

        var objectStreamMessages = SelectMessages(messages,
            "NET_SignonState",
            "SVC_SetView",
            "SVC_ServerInfo",
            "SVC_SendTable",
            "SVC_ClassInfo",
            "SVC_CreateStringTable",
            "SVC_UpdateStringTable");
        var queuedRecordMessages = SelectMessages(messages, "SVC_UpdateStringTable", "SVC_UserMessage", "SVC_EntityMessage", "SVC_GameEvent");
        var snapshotMessages = SelectMessages(messages, "SVC_PacketEntities", "SVC_TempEntities", "NET_Tick");
        var trackInputs = new Tf2Ps3SourceLoadingReplacementTrackInputs(
            loadingSummary,
            prefixSummary,
            peerSummary,
            objectSummary,
            snapshotSummary,
            nativeMessageSummary,
            criticalMessageSummary,
            nativeBoundaries,
            objectStreamTargets,
            snapshotFunctions,
            objectStreamMessages,
            queuedRecordMessages,
            snapshotMessages,
            prefixDebts,
            generatedPrefixDebts);

        var tracks = BuildTracks(trackInputs).ToArray();
        var blockers = BuildAcceptanceBlockers(loadingSummary, prefixSummary, prefixDebts, generatedPrefixDebts).ToArray();
        var report = new Tf2Ps3SourceLoadingReplacementPlanReport(
            "tf2ps3-source-loading-replacement-plan",
            "Implementation contract for replacing the current fake/generated TF2 PS3 loading and map-load Source payloads with native TF.elf/server.dll writers. This is not a completion claim; it is the next native replacement boundary report.",
            new Tf2Ps3SourceLoadingReplacementPlanInputs(
                loadingFrameDebtPath,
                queuedPrefixContractPath,
                queuedPeerTargetMapPath,
                criticalMessageIoContractPath,
                objectStreamBootstrapMapPath,
                snapshotPathMapPath,
                nativeMessageContractPath),
            new Tf2Ps3SourceLoadingReplacementPlanSummary(
                ReadInt(loadingSummary, "FrameCount"),
                ReadInt(loadingSummary, "ProductionFakeFrameCount"),
                ReadInt(loadingSummary, "NativeRecordWithGeneratedPrefixFrameCount"),
                ReadInt(loadingSummary, "SteadyNativeSnapshotPaddingRiskCount"),
                ReadInt(loadingSummary, "BlockingByteCount"),
                ReadInt(loadingSummary, "StaticHexTemplateCount"),
                ReadInt(prefixSummary, "StaticTemplatePrefixDebtCount"),
                ReadInt(prefixSummary, "StaticTemplatePrefixDebtBytes"),
                ReadInt(prefixSummary, "GeneratedQueuedPrefixDebtCount"),
                ReadInt(prefixSummary, "GeneratedQueuedPrefixDebtBytes"),
                ReadInt(prefixSummary, "GeneratedQueuedPrefixRecordCount"),
                ReadInt(prefixSummary, "OpaquePrefixBytes"),
                ReadInt(nativeMessageSummary, "MessageCount"),
                ReadInt(nativeMessageSummary, "FieldReducedMessageCount"),
                ReadInt(criticalMessageSummary, "FieldCount"),
                nativeBoundaries.Length,
                objectStreamTargets.Length,
                snapshotFunctions.Length,
                tracks.Length,
                tracks.Count(static track => track.Status != "closed-not-a-packet-owner"),
                blockers.Length,
                blockers.Sum(static blocker => blocker.BlockingCount),
                ReadInt(prefixSummary, "EaTunnelReferenceCount"),
                ReadInt(prefixSummary, "EaTunnelNeighborhoodReferenceCount")),
            tracks,
            blockers,
            [
                "Implement the object-stream bootstrap writer first: generate NET_SignonState/SVC_ServerInfo/SVC_SendTable/SVC_ClassInfo/SVC_CreateStringTable/SVC_UpdateStringTable bitstreams and wrap them through the 00a55e60 object-stream envelope.",
                "Replace queued peer submessage prefixes next: generate the control/prefix bytes that surround the already native COc/DSC/PNG records and route the body through 008bc978 -> 008b9f70/008bb058/008bc490.",
                "Replace high-entropy and steady snapshot padding after the bootstrap path: generate SVC_PacketEntities and entity-delta payloads from field writers, then delete PadNativeWrappedPayload from production paths.",
                "Delete static hex templates and deterministic filler methods only after the equivalent native field writers own those bytes; do not move captured bytes into a different literal table.",
                "Keep EA Tunnel closed as a packet-owner theory until server.dll Ghidra evidence has nonzero executable xrefs."
            ],
            [
                "The current map-load failure is not blocked by unknown Plasma/GameManager text; it is blocked by the PS3 Source queued peer/object-stream layer below GameManager.",
                "TF.elf writer evidence is strong enough to name the next native writers and field contracts, but the live responder still has fake/generated bytes in the loading path.",
                "PC srcds packets are still the wrong outer format for TF2 PS3. A playable replacement needs native object-stream/bootstrap/snapshot generation inside the PS3 UDP transport.",
                "This report intentionally keeps acceptance strict: the server is not native while any production fake frame, generated queued-prefix frame, steady padding risk, or static template remains."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static IEnumerable<Tf2Ps3SourceLoadingReplacementTrack> BuildTracks(Tf2Ps3SourceLoadingReplacementTrackInputs inputs)
    {
        var objectStreamDebt = inputs.PrefixDebts
            .Where(static debt => debt.ReplacementTarget == "native-object-stream-bootstrap")
            .ToArray();
        yield return new Tf2Ps3SourceLoadingReplacementTrack(
            "object-stream-bootstrap",
            "open-native-implementation-required",
            "00a56fc8 -> 00a56cb0 -> 00a567b0 -> 00a55e60",
            new Tf2Ps3SourceLoadingReplacementDebt(
                "static bootstrap/map-load templates plus generated high-entropy bootstrap-shaped frames",
                objectStreamDebt.Length,
                objectStreamDebt.Sum(static debt => debt.PrefixByteLength),
                ReadInt(inputs.LoadingSummary, "StaticHexTemplateCount"),
                ReadInt(inputs.LoadingSummary, "ProductionFakeFrameCount")),
            FilterByRoles(inputs.ObjectStreamTargets,
                "source-bootstrap-entry",
                "source-bootstrap-batch",
                "server-info-stringtable-batch",
                "object-stream-envelope-sender"),
            inputs.ObjectStreamMessages,
            [
                "Build raw SVC/NET bitstreams from the recovered TF.elf field contracts instead of captured/static bytes.",
                "Wrap those bitstreams with 00a55e60 semantics: five-bit terminator/control advance, one-byte kind, callback/owner id, 0x4c sidecar, duplicated sequence fields, payload length, payload bytes, and optional flush.",
                "Use semantic server/session/map state for protocol, server count, class count, map CRC/digest, player slot, max clients, game dir, map name, sky, host name, string tables, and class info."
            ],
            [
                "Object stream bootstrap report proves 00a567b0 calls SVC_ServerInfo, SVC_SendTable, SVC_CreateStringTable helper, follow-up class/string writer, and 00a55e60.",
                "Object stream envelope fields name kind/owner, 0x4c sidecar, sequence-a, sequence-b, payload-length, and payload-bytes.",
                "Static template debt still includes bootstrap/map-load templates, so this track remains open."
            ],
            [
                "No object-stream bootstrap or map-load response uses captured/static hex templates.",
                "Generated bootstrap payloads can be decoded into named NET/SVC messages with the same field widths recovered from TF.elf.",
                "PCAP semantic tests explain the server-info/string-table/class-info turns without replay-only bytes."
            ]);

        var queuedDebt = inputs.PrefixDebts
            .Where(static debt => debt.ReplacementTarget == "queued-peer-submessage-boundaries")
            .ToArray();
        var generatedQueuedDebt = inputs.GeneratedPrefixDebts
            .Where(static debt => debt.ReplacementTarget == "queued-peer-submessage-boundaries")
            .ToArray();
        yield return new Tf2Ps3SourceLoadingReplacementTrack(
            "queued-peer-submessage-boundaries",
            "open-native-implementation-required",
            "008bc978 -> 008b9f70/008bb058/008bc490",
            new Tf2Ps3SourceLoadingReplacementDebt(
                "mixed-binary filler and PNG/COc/DSC frames with generated prefix/trailing bytes",
                ReadInt(inputs.LoadingSummary, "MixedBinaryFrameCount") + ReadInt(inputs.LoadingSummary, "NativeRecordWithGeneratedPrefixFrameCount") + generatedQueuedDebt.Length,
                queuedDebt.Sum(static debt => debt.PrefixByteLength) + generatedQueuedDebt.Sum(static debt => debt.PrefixByteLength),
                queuedDebt.Length + generatedQueuedDebt.Length,
                ReadInt(inputs.PrefixSummary, "OpaquePrefixBytes")),
            FilterByRoles(inputs.NativeBoundaries,
                "semantic-payload-stager",
                "source-payload-queue",
                "direct-peer-channel-entry",
                "native-fragment-body-builder",
                "reliable-peer-payload-queue",
                "local-or-remote-peer-router"),
            inputs.QueuedRecordMessages,
            [
                "Recover the native queued prefix/control writer that precedes or surrounds COc/DSC/PNG records inside one peer-channel payload.",
                "Keep Ps3SourceEmbeddedObjectWireRecord and Ps3SourcePlayerStateLinkRecord for the decoded record tails, but stop synthesizing prefix/trailing bytes with deterministic filler.",
                "Route all generated queued bodies through the native send/queue boundary, not through PC Source datagrams."
            ],
            [
                $"{ReadInt(inputs.PrefixSummary, "QueuedChunkWithEmbeddedRecordsCount")} queued chunks have decoded record tails, while {ReadInt(inputs.PrefixSummary, "QueuedChunkWithoutEmbeddedRecordsCount")} are prefix-only.",
                $"{generatedQueuedDebt.Length} generated queued-prefix body/bodies currently replace captured templates but still need the native queued peer-channel prefix writer.",
                "Queued-prefix contract found zero LZSS-wrapped prefixes and zero strict standalone snapshot prefixes, so this is a writer-format problem rather than a simple transform.",
                "Native boundaries include semantic stager 008bc978, queue 008b9f70, direct peer entry 008bb058, and fragment builder 008bc490."
            ],
            [
                "NativeRecordWithGeneratedPrefixFrameCount is zero.",
                "MixedBinary loading frames are replaced by named queued-prefix/control fields.",
                "Decoded record tails remain byte-identical to PNG/COc/DSC grammars while their prefix bytes are produced by native writers."
            ]);

        yield return new Tf2Ps3SourceLoadingReplacementTrack(
            "snapshot-entity-delta",
            "open-native-implementation-required",
            "00a61150 -> 00a5fb80 -> 008bc978",
            new Tf2Ps3SourceLoadingReplacementDebt(
                "high-entropy filler and steady native snapshot bodies padded to PCAP-shaped lengths",
                ReadInt(inputs.LoadingSummary, "HighEntropyFrameCount") + ReadInt(inputs.LoadingSummary, "SteadyNativeSnapshotPaddingRiskCount"),
                ReadInt(inputs.LoadingSummary, "BlockingByteCount"),
                ReadInt(inputs.LoadingSummary, "SteadyNativeSnapshotPaddingRiskCount"),
                ReadInt(inputs.LoadingSummary, "NativeTemplateDebtHighEntropyLoadingFrameCount")),
            FilterByRoles(inputs.SnapshotFunctions,
                "snapshot-frame-builder",
                "source-send-wrapper",
                "fragmented-source-send",
                "direct-source-send-entry",
                "reliable-peer-channel-send-entry"),
            inputs.SnapshotMessages,
            [
                "Generate SVC_PacketEntities fields from TF.elf field widths: max entries, delta flag, delta from, baseline, updated entries, 20-bit delta size, update-baseline flag, and data bits.",
                "Generate the 00a61150 snapshot frame header instead of BuildHighEntropyBinaryBody output.",
                "Remove PadNativeWrappedPayload from production paths once the real entity-delta length is recovered."
            ],
            [
                "Snapshot path report names 00a61150 as the frame builder and 008bc978 as the native send wrapper.",
                "SVC_PacketEntities is field-reduced in the critical message contract and routed as gameplay-snapshot-native-route in the native message contract.",
                "Steady-state frames still carry deterministic padding risk until exact snapshot/entity-delta body lengths are native."
            ],
            [
                "ProductionFakeFrameCount is zero for high-entropy loading frames.",
                "SteadyNativeSnapshotPaddingRiskCount is zero.",
                "SVC_PacketEntities bodies are produced from field writers and entity-delta data, not from deterministic filler."
            ]);

        yield return new Tf2Ps3SourceLoadingReplacementTrack(
            "send-wrapper-fragmentation",
            "partial-wrapper-known-body-generation-still-open",
            "008bc978 -> 008bb058/008bc490 -> 00925858/009252e0",
            new Tf2Ps3SourceLoadingReplacementDebt(
                "outer send/fragment wrappers are mapped, but native bodies still include fake/generated bytes",
                ReadInt(inputs.LoadingSummary, "FrameCount"),
                0,
                ReadInt(inputs.SnapshotSummary, "FragmentHeaderFieldCount"),
                ReadInt(inputs.SnapshotSummary, "SendWrapperFieldCount")),
            FilterByRoles(inputs.NativeBoundaries,
                "semantic-payload-stager",
                "direct-peer-channel-entry",
                "native-fragment-body-builder",
                "local-or-remote-peer-router",
                "reliable-peer-payload-queue"),
            [],
            [
                "Keep Ps3SourceFragmentedSend for the recovered 10-byte 0xfffffffe fragment header, packet counter, packed total/index, and compression flag.",
                "Use wrapper/fragmentation only after the raw payload body is generated from native field writers.",
                "Treat wrapper correctness as necessary but not sufficient for map-load."
            ],
            [
                "The wrapper/fragment header fields are recovered and separately counted in source-snapshot-path-map.",
                "Generated wrapper use does not prove body correctness while fake body generators remain reachable."
            ],
            [
                "All bodies entering the wrapper are produced by object-stream, queued-prefix, or snapshot/entity-delta writers.",
                "No captured replay body or filler body is passed to the native send wrapper in production."
            ]);

        yield return new Tf2Ps3SourceLoadingReplacementTrack(
            "ea-tunnel-closed",
            "closed-not-a-packet-owner",
            "server.dll EA Tunnel metadata is not the client-visible TF2 PS3 Source payload owner in current evidence",
            new Tf2Ps3SourceLoadingReplacementDebt(
                "no executable packet-owner evidence",
                0,
                0,
                ReadInt(inputs.PrefixSummary, "EaTunnelReferenceCount"),
                ReadInt(inputs.PrefixSummary, "EaTunnelNeighborhoodReferenceCount")),
            [],
            [],
            [
                "Do not route the replacement through EA Tunnel unless future Ghidra reports find executable xrefs.",
                "Keep TF.elf queued Source path authoritative for client-visible UDP payloads."
            ],
            [
                "Queued-prefix and loading-frame reports both show zero EA Tunnel string/neighborhood references.",
                "Current map-load debt is explained by Source object-stream/queued-peer/snapshot writers."
            ],
            [
                "Re-open this track only if server.dll Ghidra evidence changes from zero executable references."
            ]);
    }

    private static IEnumerable<Tf2Ps3SourceLoadingReplacementAcceptanceBlocker> BuildAcceptanceBlockers(
        JsonElement loadingSummary,
        JsonElement prefixSummary,
        Tf2Ps3SourceLoadingReplacementPrefixDebt[] prefixDebts,
        Tf2Ps3SourceLoadingReplacementGeneratedPrefixDebt[] generatedPrefixDebts)
    {
        yield return new("production-fake-loading-frames", ReadInt(loadingSummary, "ProductionFakeFrameCount"), "Replace BuildHighEntropyBinaryBody, FillHighEntropyDeterministic, and BuildMixedBinaryBody production callers with native field writers.");
        yield return new("generated-native-record-prefixes", ReadInt(loadingSummary, "NativeRecordWithGeneratedPrefixFrameCount"), "Recover queued prefix/control bytes around native PNG/COc/DSC record tails.");
        yield return new("steady-snapshot-padding-risk", ReadInt(loadingSummary, "SteadyNativeSnapshotPaddingRiskCount"), "Remove PadNativeWrappedPayload from steady snapshot production routes.");
        yield return new("static-hex-template-count", ReadInt(loadingSummary, "StaticHexTemplateCount"), "Delete static response templates after native object-stream/queued-prefix writers replace them.");
        yield return new("static-template-prefix-debt", ReadInt(prefixSummary, "StaticTemplatePrefixDebtBytes"), $"Replace {prefixDebts.Length} static prefix template families with native writers.");
        yield return new("generated-queued-prefix-debt", ReadInt(prefixSummary, "GeneratedQueuedPrefixDebtBytes"), $"Replace {generatedPrefixDebts.Length} generated queued prefix body/bodies with native writers.");
    }

    private static Tf2Ps3SourceLoadingReplacementNativeFunction[] ReadNativeBoundaries(JsonElement root)
    {
        return ReadArray(root, "NativeBoundaries")
            .Select(static boundary => new Tf2Ps3SourceLoadingReplacementNativeFunction(
                ReadString(boundary, "Address"),
                ReadString(boundary, "Role"),
                ReadString(boundary, "Conclusion"),
                StringArray(boundary, "EvidenceTokens")))
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingReplacementNativeFunction[] ReadObjectStreamTargets(JsonElement root)
    {
        return ReadArray(root, "Targets")
            .Select(static target => new Tf2Ps3SourceLoadingReplacementNativeFunction(
                ReadString(target, "Address"),
                ReadString(target, "Role"),
                ReadString(target, "Meaning"),
                ReadArray(target, "FieldEvidence")
                    .Select(static item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
                    .Where(static item => item.Length > 0)
                    .Take(8)
                    .ToArray()))
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingReplacementNativeFunction[] ReadSnapshotFunctions(JsonElement root)
    {
        return ReadArray(root, "Functions")
            .Select(static function => new Tf2Ps3SourceLoadingReplacementNativeFunction(
                ReadString(function, "Address"),
                ReadString(function, "Role"),
                ReadString(function, "Conclusion"),
                ReadArray(function, "FieldEvidence")
                    .Select(static item => ReadString(item, "Field"))
                    .Where(static item => item.Length > 0)
                    .Take(12)
                    .ToArray()))
            .ToArray();
    }

    private static IEnumerable<Tf2Ps3SourceLoadingReplacementMessageContract> ReadNativeMessages(JsonElement root)
    {
        foreach (var message in ReadArray(root, "Messages"))
        {
            var fields = ReadArray(message, "Fields")
                .Select(static field => new Tf2Ps3SourceLoadingReplacementMessageField(
                    ReadString(field, "Name"),
                    ReadString(field, "ObjectOffset"),
                    ReadString(field, "Encoding"),
                    ReadString(field, "TfEvidence")))
                .ToArray();
            yield return new Tf2Ps3SourceLoadingReplacementMessageContract(
                ReadString(message, "ClassName"),
                ReadInt(message, "MessageId"),
                ReadString(message, "WriteToBufferEntry"),
                ReadString(message, "Route"),
                ReadInt(message, "FieldCount"),
                fields);
        }
    }

    private static Tf2Ps3SourceLoadingReplacementPrefixDebt[] ReadStaticPrefixDebts(JsonElement root)
    {
        return ReadArray(root, "StaticTemplatePrefixDebt")
            .Select(static debt => new Tf2Ps3SourceLoadingReplacementPrefixDebt(
                ReadString(debt, "TemplateName"),
                ReadString(debt, "ReplacementTarget"),
                ReadInt(debt, "PrefixByteLength")))
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingReplacementGeneratedPrefixDebt[] ReadGeneratedPrefixDebts(JsonElement root)
    {
        return ReadArray(root, "GeneratedQueuedPrefixDebt")
            .Select(static debt => new Tf2Ps3SourceLoadingReplacementGeneratedPrefixDebt(
                ReadString(debt, "Family"),
                ReadString(debt, "Method"),
                ReadString(debt, "ReplacementTarget"),
                ReadInt(debt, "BodyLength"),
                ReadInt(debt, "PrefixByteLength"),
                ReadInt(debt, "RecordCount")))
            .ToArray();
    }

    private static Tf2Ps3SourceLoadingReplacementNativeFunction[] FilterByRoles(
        Tf2Ps3SourceLoadingReplacementNativeFunction[] functions,
        params string[] roles)
    {
        var wanted = roles.ToHashSet(StringComparer.Ordinal);
        return functions.Where(function => wanted.Contains(function.Role)).ToArray();
    }

    private static Tf2Ps3SourceLoadingReplacementMessageContract[] SelectMessages(
        Tf2Ps3SourceLoadingReplacementMessageContract[] messages,
        params string[] classNames)
    {
        var wanted = classNames.ToHashSet(StringComparer.Ordinal);
        return messages
            .Where(message => wanted.Contains(message.ClassName))
            .OrderBy(static message => message.MessageId)
            .ThenBy(static message => message.ClassName, StringComparer.Ordinal)
            .ToArray();
    }

    private static JsonElement[] ReadArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray()
            : [];
    }

    private static string[] StringArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetString() ?? "").Where(static item => item.Length > 0).ToArray()
            : [];
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }
}

internal sealed record Tf2Ps3SourceLoadingReplacementTrackInputs(
    JsonElement LoadingSummary,
    JsonElement PrefixSummary,
    JsonElement PeerSummary,
    JsonElement ObjectSummary,
    JsonElement SnapshotSummary,
    JsonElement NativeMessageSummary,
    JsonElement CriticalMessageSummary,
    Tf2Ps3SourceLoadingReplacementNativeFunction[] NativeBoundaries,
    Tf2Ps3SourceLoadingReplacementNativeFunction[] ObjectStreamTargets,
    Tf2Ps3SourceLoadingReplacementNativeFunction[] SnapshotFunctions,
    Tf2Ps3SourceLoadingReplacementMessageContract[] ObjectStreamMessages,
    Tf2Ps3SourceLoadingReplacementMessageContract[] QueuedRecordMessages,
    Tf2Ps3SourceLoadingReplacementMessageContract[] SnapshotMessages,
    Tf2Ps3SourceLoadingReplacementPrefixDebt[] PrefixDebts,
    Tf2Ps3SourceLoadingReplacementGeneratedPrefixDebt[] GeneratedPrefixDebts);

public sealed record Tf2Ps3SourceLoadingReplacementPlanReport(
    string Status,
    string Note,
    Tf2Ps3SourceLoadingReplacementPlanInputs Inputs,
    Tf2Ps3SourceLoadingReplacementPlanSummary Summary,
    Tf2Ps3SourceLoadingReplacementTrack[] ReplacementTracks,
    Tf2Ps3SourceLoadingReplacementAcceptanceBlocker[] AcceptanceBlockers,
    string[] NextImplementationOrder,
    string[] Conclusions);

public sealed record Tf2Ps3SourceLoadingReplacementPlanInputs(
    string LoadingFrameDebt,
    string QueuedPrefixContract,
    string QueuedPeerTargetMap,
    string CriticalMessageIoContract,
    string ObjectStreamBootstrapMap,
    string SnapshotPathMap,
    string NativeMessageContract);

public sealed record Tf2Ps3SourceLoadingReplacementPlanSummary(
    int FrameCount,
    int ProductionFakeFrameCount,
    int NativeRecordWithGeneratedPrefixFrameCount,
    int SteadyNativeSnapshotPaddingRiskCount,
    int BlockingByteCount,
    int StaticHexTemplateCount,
    int StaticTemplatePrefixDebtCount,
    int StaticTemplatePrefixDebtBytes,
    int GeneratedQueuedPrefixDebtCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount,
    int QueuedOpaquePrefixBytes,
    int NativeMessageCount,
    int FieldReducedMessageCount,
    int CriticalFieldCount,
    int NativeBoundaryCount,
    int ObjectStreamTargetCount,
    int SnapshotFunctionCount,
    int ReplacementTrackCount,
    int OpenReplacementTrackCount,
    int AcceptanceBlockerCount,
    int AcceptanceBlockingCount,
    int EaTunnelReferenceCount,
    int EaTunnelNeighborhoodReferenceCount);

public sealed record Tf2Ps3SourceLoadingReplacementTrack(
    string TrackId,
    string Status,
    string ReplacementBoundary,
    Tf2Ps3SourceLoadingReplacementDebt CurrentDebt,
    Tf2Ps3SourceLoadingReplacementNativeFunction[] NativeFunctions,
    Tf2Ps3SourceLoadingReplacementMessageContract[] SourceMessages,
    string[] RequiredImplementation,
    string[] Evidence,
    string[] CompletionCriteria);

public sealed record Tf2Ps3SourceLoadingReplacementDebt(
    string Meaning,
    int FrameOrTemplateCount,
    int BlockingByteCount,
    int SecondaryCount,
    int CorpusOrSourceCount);

public sealed record Tf2Ps3SourceLoadingReplacementNativeFunction(
    string Address,
    string Role,
    string Meaning,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceLoadingReplacementMessageContract(
    string ClassName,
    int MessageId,
    string WriteToBufferEntry,
    string Route,
    int FieldCount,
    Tf2Ps3SourceLoadingReplacementMessageField[] Fields);

public sealed record Tf2Ps3SourceLoadingReplacementMessageField(
    string Name,
    string ObjectOffset,
    string Encoding,
    string TfEvidence);

public sealed record Tf2Ps3SourceLoadingReplacementAcceptanceBlocker(
    string Name,
    int BlockingCount,
    string RequiredFix);

public sealed record Tf2Ps3SourceLoadingReplacementPrefixDebt(
    string TemplateName,
    string ReplacementTarget,
    int PrefixByteLength);

public sealed record Tf2Ps3SourceLoadingReplacementGeneratedPrefixDebt(
    string Family,
    string Method,
    string ReplacementTarget,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount);
