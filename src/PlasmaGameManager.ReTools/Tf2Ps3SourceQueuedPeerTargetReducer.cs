using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceQueuedPeerTargetReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceQueuedPeerTargetReport> ReduceAsync(
        string peerChannelMapPath,
        string criticalBootstrapRoutePath,
        string objectStreamBootstrapPath,
        string queuedOpaqueReportPath,
        string tunnelGhidraMapPath,
        string outputPath)
    {
        using var peerChannel = JsonDocument.Parse(await File.ReadAllTextAsync(peerChannelMapPath));
        using var criticalBootstrap = JsonDocument.Parse(await File.ReadAllTextAsync(criticalBootstrapRoutePath));
        using var objectStream = JsonDocument.Parse(await File.ReadAllTextAsync(objectStreamBootstrapPath));
        using var queuedOpaque = JsonDocument.Parse(await File.ReadAllTextAsync(queuedOpaqueReportPath));
        using var tunnel = JsonDocument.Parse(await File.ReadAllTextAsync(tunnelGhidraMapPath));

        var peerRoot = peerChannel.RootElement;
        var queuedSummary = queuedOpaque.RootElement.GetProperty("Summary");
        var tunnelSummary = tunnel.RootElement.GetProperty("Summary");
        var peerSummary = peerRoot.GetProperty("Summary");

        var nativeBoundaries = BuildNativeBoundaries(peerRoot).ToArray();
        var objectStreamTargets = BuildObjectStreamTargets(objectStream.RootElement).ToArray();
        var bootstrapBoundary = BuildBootstrapBoundary(criticalBootstrap.RootElement);
        var corpus = new Tf2Ps3SourceQueuedPeerCorpusEvidence(
            ReadInt(queuedSummary, "FileCount"),
            ReadInt(queuedSummary, "ActiveSourceFlowFileCount"),
            ReadInt(queuedSummary, "SourcePacketCount"),
            ReadInt(queuedSummary, "QueuedChunkPacketCount"),
            ReadInt(queuedSummary, "ServerQueuedChunkPacketCount"),
            ReadInt(queuedSummary, "ClientQueuedChunkPacketCount"),
            ReadInt(queuedSummary, "QueuedChunkWithEmbeddedRecordsCount"),
            ReadInt(queuedSummary, "QueuedChunkWithoutEmbeddedRecordsCount"),
            ReadInt(queuedSummary, "OpaquePrefixBytes"),
            ReadInt(queuedSummary, "LzssWrappedPrefixCount"),
            ReadInt(queuedSummary, "StrictSnapshotPrefixCount"),
            ReadCounts(queuedSummary, "TopFamilies").Take(16).ToArray(),
            ReadCounts(queuedSummary, "TopOpaquePrefixLengths").Take(16).ToArray());

        var report = new Tf2Ps3SourceQueuedPeerTargetReport(
            "tf2ps3-source-queued-peer-target-map",
            "Implementation-facing reduction for the TF.elf native queued peer-channel layer. This report exists to replace captured byte-template behavior with native Source bitstream/object-stream generation through the same boundaries TF.elf uses.",
            new Tf2Ps3SourceQueuedPeerInputs(
                peerChannelMapPath,
                criticalBootstrapRoutePath,
                objectStreamBootstrapPath,
                queuedOpaqueReportPath,
                tunnelGhidraMapPath),
            new Tf2Ps3SourceQueuedPeerTargetSummary(
                ReadInt(peerSummary, "TargetFunctionCount"),
                ReadInt(peerSummary, "LocatedFunctionCount"),
                ReadString(nativeBoundaries.FirstOrDefault(static boundary => boundary.Role == "semantic-payload-stager")?.Address),
                ReadString(nativeBoundaries.FirstOrDefault(static boundary => boundary.Role == "source-payload-queue")?.Address),
                ReadString(nativeBoundaries.FirstOrDefault(static boundary => boundary.Role == "direct-peer-channel-entry")?.Address),
                ReadString(nativeBoundaries.FirstOrDefault(static boundary => boundary.Role == "native-fragment-body-builder")?.Address),
                objectStreamTargets.Any(static target => target.Role == "object-stream-envelope-sender"),
                ReadInt(tunnelSummary, "EaTunnelReferenceCount"),
                ReadInt(tunnelSummary, "NeighborhoodReferenceCount"),
                corpus.QueuedChunkPacketCount,
                corpus.LzssWrappedPrefixCount,
                corpus.StrictSnapshotPrefixCount),
            nativeBoundaries,
            BuildNativeQueueContract(nativeBoundaries),
            BuildNativeSendWrapperContract(nativeBoundaries),
            BuildNativeQueueDrainContract(),
            bootstrapBoundary,
            objectStreamTargets,
            corpus,
            BuildConclusions(corpus, tunnelSummary),
            BuildNextImplementationTargets(corpus));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPeerBoundary> BuildNativeBoundaries(JsonElement root)
    {
        var staged = new List<Tf2Ps3SourceQueuedPeerBoundary>();
        foreach (var function in root.GetProperty("Functions").EnumerateArray())
        {
            var role = ReadString(function, "Role");
            if (role is not ("semantic-payload-stager"
                or "source-payload-queue"
                or "direct-peer-channel-entry"
                or "native-fragment-body-builder"
                or "reliable-peer-payload-queue"
                or "local-or-remote-peer-router"))
            {
                continue;
            }

            staged.Add(new Tf2Ps3SourceQueuedPeerBoundary(
                ReadString(function, "Address"),
                role,
                ReadString(function, "Conclusion"),
                StringArray(function, "Calls"),
                StringArray(function, "HexConstants"),
                StringArray(function, "EvidenceTokens")));
        }

        if (!staged.Any(static boundary => boundary.Role == "source-payload-queue")
            && staged.Any(static boundary => boundary.Calls.Contains("_opd_FUN_008b9f70", StringComparer.Ordinal)))
        {
            staged.Add(new Tf2Ps3SourceQueuedPeerBoundary(
                "008b9f70",
                "source-payload-queue",
                "Called by 008bc978 for non-immediate Source payload staging. The current peer-channel report exposes it as call evidence rather than a top-level row; receive-path evidence ties this address to queued payload storage.",
                [],
                [],
                ["_opd_FUN_008b9f70", "called-by:008bc978"]));
        }

        foreach (var boundary in staged)
        {
            yield return boundary;
        }
    }

    private static Tf2Ps3SourceQueuedPeerBootstrapBoundary BuildBootstrapBoundary(JsonElement root)
    {
        var boundaries = root.GetProperty("Boundaries").EnumerateArray()
            .Select(boundary => new Tf2Ps3SourceQueuedPeerNamedEvidence(
                ReadString(boundary, "Name"),
                ReadString(boundary, "Evidence"),
                ReadString(boundary, "Meaning")))
            .ToArray();
        var remaining = root.TryGetProperty("RemainingWork", out var remainingElement)
            ? remainingElement.EnumerateArray().Select(static value => value.GetString() ?? "").ToArray()
            : [];
        return new Tf2Ps3SourceQueuedPeerBootstrapBoundary(boundaries, remaining);
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPeerObjectStreamTarget> BuildObjectStreamTargets(JsonElement root)
    {
        foreach (var target in root.GetProperty("Targets").EnumerateArray())
        {
            var role = ReadString(target, "Role");
            if (role is "source-bootstrap-entry"
                or "source-bootstrap-batch"
                or "server-info-stringtable-batch"
                or "object-stream-envelope-sender")
            {
                yield return new Tf2Ps3SourceQueuedPeerObjectStreamTarget(
                    ReadString(target, "Address"),
                    role,
                    ReadString(target, "Meaning"),
                    target.TryGetProperty("Calls", out var calls)
                        ? calls.EnumerateArray()
                            .Select(call => new Tf2Ps3SourceQueuedPeerNamedEvidence(
                                ReadString(call, "Role"),
                                ReadString(call, "TargetFunction"),
                                ReadString(call, "Statement")))
                            .ToArray()
                        : []);
            }
        }
    }

    private static Tf2Ps3SourceQueuedPeerNativeQueueContract BuildNativeQueueContract(
        Tf2Ps3SourceQueuedPeerBoundary[] nativeBoundaries)
    {
        var queueBoundary = nativeBoundaries.FirstOrDefault(static boundary => boundary.Address == "008b9f70");
        return new Tf2Ps3SourceQueuedPeerNativeQueueContract(
            "008b9f70",
            queueBoundary?.Role ?? "source-payload-queue",
            "Non-immediate Source payloads enter this queue when 008bc978 sees immediate send disabled. The queued cell is a two-word header followed by inline bytes for small payloads, with a heap pointer only for larger payloads.",
            0x228,
            0x808,
            0x801,
            0x17701,
            8,
            "cell[0] = cell + 8; cell[1] = payloadByteLength; memcpy(cell + 8, payload, payloadByteLength)",
            "cell[0] = heap_alloc(payloadByteLength); cell[1] = payloadByteLength; memcpy(cell[0], payload, payloadByteLength)",
            [
                new Tf2Ps3SourceQueuedPeerNativeQueueChannel(
                    1,
                    "param_1 == 1",
                    0x258,
                    0x260,
                    0x268,
                    0x270,
                    "First queue block drained by 008ba628; 008b9f70 appends nodes here when its channel argument is 1."),
                new Tf2Ps3SourceQueuedPeerNativeQueueChannel(
                    0,
                    "param_1 == 0",
                    0x278,
                    0x280,
                    0x288,
                    0x290,
                    "Second queue block drained by 008ba628; 008b9f70 appends nodes here when its channel argument is 0.")
            ],
            [
                "if ((int)param_2 < 0x17701)",
                "FUN_00871958(PTR_DAT_0197336c + 0x228, 0x808)",
                "if ((int)param_2 < 0x801) *cell = cell + 8",
                "cell[1] = param_2",
                "channel 1 uses +0x258/+0x260/+0x268/+0x270",
                "channel 0 uses +0x278/+0x280/+0x288/+0x290"
            ]);
    }

    private static Tf2Ps3SourceQueuedPeerNativeSendWrapperContract BuildNativeSendWrapperContract(
        Tf2Ps3SourceQueuedPeerBoundary[] nativeBoundaries)
    {
        var wrapperBoundary = nativeBoundaries.FirstOrDefault(static boundary => boundary.Address == "008bc978");
        return new Tf2Ps3SourceQueuedPeerNativeSendWrapperContract(
            "008bc978",
            wrapperBoundary?.Role ?? "semantic-payload-stager",
            "Authoritative TF.elf Source payload staging wrapper. It either queues the raw semantic payload through 008b9f70 or builds immediate payload/bit-sidecar buffers before selecting direct or fragmented peer send.",
            "_opd_FUN_008b78a0() == 0",
            "_opd_FUN_008b9f70(param_2, (uint)param_5, param_4)",
            0x1000,
            "*param_3 must be 2 or 3 before the immediate peer path proceeds",
            new Tf2Ps3SourceQueuedPeerBitSidecarContract(
                "param_6 != 0",
                "((*(uint *)(param_6 + 0x0c) + 7) >> 3)",
                "sidecarByteLength = sidecarPayloadBytes + 2",
                "first two sidecar bytes are the sidecar bit count written big-endian",
                "memcpy(sidecar + 2, *(uint *)param_6, sidecarPayloadBytes)",
                "If the compression gate at object +0x614/+0x30 is enabled, 008bc978 tries FUN_0086e7e8 and falls back to the raw sidecar copy."),
            new Tf2Ps3SourceQueuedPeerTransformContract(
                "param_7 != 0",
                "combinedByteLength = mainPayloadBytes + sidecarByteLength + 4",
                "008bc978 reserves a 4-byte leading transform/header area, tries FUN_0086e7e8 for the main payload, then appends the sidecar when present.",
                "If transform setup fails, the wrapper falls back to main payload bytes followed by the sidecar bytes."),
            new Tf2Ps3SourceQueuedPeerSendSelectionContract(
                "if finalByteLength > configuredThreshold",
                "008bc490",
                "008bb058",
                0x10,
                "Both direct and fragment sends receive the copied peer sockaddr from FUN_0086c5d8; 008bb058 then routes through 00925858 rather than raw PC srcds transport."),
            [
                "_opd_FUN_008b78a0",
                "_opd_FUN_008b9f70",
                "sidecar bit count = *(param_6 + 0x0c)",
                "two-byte big-endian bit count",
                "FUN_0086e7e8 transform/compression attempt",
                "_opd_FUN_008bb058 direct send",
                "_opd_FUN_008bc490 fragmented send"
            ]);
    }

    private static Tf2Ps3SourceQueuedPeerNativeQueueDrainContract BuildNativeQueueDrainContract()
    {
        return new Tf2Ps3SourceQueuedPeerNativeQueueDrainContract(
            "008ba628",
            "Drains the two queue blocks written by 008b9f70, wakes on the per-channel event word, frees heap payloads when cell[0] is not the inline cell + 8 pointer, and returns the 0x808 cell to the allocator at PTR_DAT_0197336c + 0x228.",
            [
                0x258,
                0x278
            ],
            "heap payload when cell[0] != 0 and cell[0] != cell + 8",
            "FUN_0086c478(PTR_DAT_0197336c + 0x228, cell)",
            [
                "drain starts at PTR_DAT_0197336c + 0x258",
                "then advances by 0x20 to PTR_DAT_0197336c + 0x278",
                "FUN_0086e4a8 frees heap payloads",
                "FUN_0086c478 returns queue cells to +0x228 pool"
            ]);
    }

    private static string[] BuildConclusions(
        Tf2Ps3SourceQueuedPeerCorpusEvidence corpus,
        JsonElement tunnelSummary)
    {
        var conclusions = new List<string>
        {
            "TF.elf's native path stages Source bitstreams through 008bc978 and queues non-immediate payloads through 008b9f70; captured queued chunks should be treated as native Source peer-channel payloads, not PC srcds datagrams.",
            "008b9f70 is now field-reduced enough for implementation: queue cells are pointer/length records, payloads under 0x801 bytes are inline at cell + 8 inside a 0x808 allocation, larger payloads use a heap pointer, and channel 1/channel 0 occupy queue blocks +0x258/+0x278.",
            "008bc978 confirms the bit-sidecar rule needed by generated PS3 Source frames: the sidecar starts with a two-byte big-endian bit count followed by the raw sidecar bytes, with optional transform/compression attempted before direct or fragmented send.",
            "The official EA server.dll EA Tunnel lead does not currently identify the client-visible packet owner: Ghidra reports zero EA Tunnel references and zero descriptor-neighborhood references in server.dll.",
            "The PCAP corpus rejects a simple queued-prefix transform: no queued opaque prefix validates as native LZSS, and no prefix exposes a strict standalone TF.elf snapshot/entity-delta frame after requiring plausible named Source records.",
            "Queued chunks often append understood PNG object-link records after a high-entropy prefix; the prefix must be generated from native Source bootstrap/snapshot bitstreams instead of copied from captures."
        };

        if (ReadInt(tunnelSummary, "EaTunnelReferenceCount") != 0
            || ReadInt(tunnelSummary, "NeighborhoodReferenceCount") != 0)
        {
            conclusions.Add("EA Tunnel evidence changed: re-open the server.dll tunnel lead before treating it as metadata only.");
        }

        if (corpus.ClientQueuedChunkPacketCount != 0)
        {
            conclusions.Add("The corpus contains client-to-server queued chunks; parse those separately before finalizing the server-only queue model.");
        }

        return conclusions.ToArray();
    }

    private static Tf2Ps3SourceQueuedPeerImplementationTarget[] BuildNextImplementationTargets(
        Tf2Ps3SourceQueuedPeerCorpusEvidence corpus)
    {
        return
        [
            new(
                "native-object-stream-bootstrap",
                "Replace captured setup/map-load byte templates with object-stream bootstrap records built from SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, SVC_CreateStringTable, NET_SignonState, and related TF.elf field-reduced writers.",
                "00a56fc8 -> 00a56cb0 -> 00a567b0 -> 00a55e60 object-stream sender; critical bootstrap route map shows raw five-bit Source messages before native send/queue.",
                "Generated output should produce queued prefixes whose sizes and placement match corpus families such as server-queued-opaque-only-1128 and server-queued-opaque-prefix-1130-records-1 without copying captured bytes."),
            new(
                "queued-peer-submessage-boundaries",
                "Recover the framing that lets opaque prefixes and trailing PNG/COc object records coexist inside one queued peer-channel packet.",
                $"Corpus evidence: {corpus.QueuedChunkWithEmbeddedRecordsCount} queued chunks with embedded records and {corpus.QueuedChunkWithoutEmbeddedRecordsCount} opaque-only chunks.",
                "Implement explicit prefix + embedded-record composition in the responder and reject accidental marker collisions."),
            new(
                "native-snapshot-and-entity-delta-route",
                "Continue mapping the gameplay snapshot path below 00a61150 and 00a5fb80, but do not expect those frames to appear as standalone queued prefixes in current retail captures.",
                "Strict queued-prefix snapshot count is zero after plausible C* object filtering.",
                "Generate snapshots through the recovered message/entity-delta codecs, then place them through the queued peer channel."),
            new(
                "serverdll-source-semantics",
                "Use official server.dll for gameplay semantics, usercmd physics, sendtable field meanings, stats, and lifecycle callbacks; do not use EA Tunnel as packet transport owner without new executable references.",
                "server.dll tunnel report closes EA Tunnel as unreferenced descriptor metadata in current exports.",
                "Keep TF.elf authoritative for the PS3-visible UDP envelope.")
        ];
    }

    private static Tf2Ps3SourceQueuedPeerCount[] ReadCounts(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray()
                .Select(static value => new Tf2Ps3SourceQueuedPeerCount(
                    ReadString(value, "Value"),
                    ReadInt(value, "Count")))
                .ToArray()
            : [];
    }

    private static string[] StringArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetString() ?? "").ToArray()
            : [];
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static string ReadString(string? value)
    {
        return value ?? "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }
}

public sealed record Tf2Ps3SourceQueuedPeerTargetReport(
    string Status,
    string Note,
    Tf2Ps3SourceQueuedPeerInputs Inputs,
    Tf2Ps3SourceQueuedPeerTargetSummary Summary,
    Tf2Ps3SourceQueuedPeerBoundary[] NativeBoundaries,
    Tf2Ps3SourceQueuedPeerNativeQueueContract NativeQueueContract,
    Tf2Ps3SourceQueuedPeerNativeSendWrapperContract NativeSendWrapperContract,
    Tf2Ps3SourceQueuedPeerNativeQueueDrainContract NativeQueueDrainContract,
    Tf2Ps3SourceQueuedPeerBootstrapBoundary BootstrapBoundary,
    Tf2Ps3SourceQueuedPeerObjectStreamTarget[] ObjectStreamTargets,
    Tf2Ps3SourceQueuedPeerCorpusEvidence CorpusEvidence,
    string[] Conclusions,
    Tf2Ps3SourceQueuedPeerImplementationTarget[] NextImplementationTargets);

public sealed record Tf2Ps3SourceQueuedPeerInputs(
    string PeerChannelMap,
    string CriticalBootstrapRouteMap,
    string ObjectStreamBootstrapMap,
    string QueuedOpaqueReport,
    string TunnelGhidraMap);

public sealed record Tf2Ps3SourceQueuedPeerTargetSummary(
    int PeerChannelTargetFunctionCount,
    int PeerChannelLocatedFunctionCount,
    string NativeSendWrapperEntry,
    string NativePayloadQueueEntry,
    string DirectPeerChannelEntry,
    string NativeFragmentBuilderEntry,
    bool HasObjectStreamEnvelopeSender,
    int EaTunnelReferenceCount,
    int EaTunnelNeighborhoodReferenceCount,
    int QueuedChunkPacketCount,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount);

public sealed record Tf2Ps3SourceQueuedPeerBoundary(
    string Address,
    string Role,
    string Conclusion,
    string[] Calls,
    string[] HexConstants,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceQueuedPeerNativeQueueContract(
    string EntryAddress,
    string Role,
    string Meaning,
    int CellAllocatorOffset,
    int CellAllocationSize,
    int InlinePayloadThreshold,
    int MaxPayloadBytesExclusive,
    int InlinePayloadOffset,
    string InlinePayloadRule,
    string HeapPayloadRule,
    Tf2Ps3SourceQueuedPeerNativeQueueChannel[] Channels,
    string[] Evidence);

public sealed record Tf2Ps3SourceQueuedPeerNativeQueueChannel(
    int ParamValue,
    string Name,
    int SentinelOffset,
    int QueueHeadOffset,
    int WakeEventOffset,
    int RecycleListOffset,
    string Meaning);

public sealed record Tf2Ps3SourceQueuedPeerNativeSendWrapperContract(
    string EntryAddress,
    string Role,
    string Meaning,
    string QueueGate,
    string QueueCall,
    int ScratchBufferSize,
    string ImmediatePeerGate,
    Tf2Ps3SourceQueuedPeerBitSidecarContract BitSidecar,
    Tf2Ps3SourceQueuedPeerTransformContract Transform,
    Tf2Ps3SourceQueuedPeerSendSelectionContract SendSelection,
    string[] Evidence);

public sealed record Tf2Ps3SourceQueuedPeerBitSidecarContract(
    string PresenceRule,
    string PayloadByteLengthRule,
    string TotalByteLengthRule,
    string BitCountEncoding,
    string PayloadCopyRule,
    string TransformGate);

public sealed record Tf2Ps3SourceQueuedPeerTransformContract(
    string PresenceRule,
    string TotalByteLengthRule,
    string TransformAttempt,
    string FallbackRule);

public sealed record Tf2Ps3SourceQueuedPeerSendSelectionContract(
    string FragmentCondition,
    string FragmentedSendFunction,
    string DirectSendFunction,
    int SockaddrByteLength,
    string Meaning);

public sealed record Tf2Ps3SourceQueuedPeerNativeQueueDrainContract(
    string EntryAddress,
    string Meaning,
    int[] QueueBlockBaseOffsets,
    string HeapPayloadFreeRule,
    string CellRecycleRule,
    string[] Evidence);

public sealed record Tf2Ps3SourceQueuedPeerBootstrapBoundary(
    Tf2Ps3SourceQueuedPeerNamedEvidence[] Boundaries,
    string[] RemainingWork);

public sealed record Tf2Ps3SourceQueuedPeerObjectStreamTarget(
    string Address,
    string Role,
    string Meaning,
    Tf2Ps3SourceQueuedPeerNamedEvidence[] Calls);

public sealed record Tf2Ps3SourceQueuedPeerCorpusEvidence(
    int FileCount,
    int ActiveSourceFlowFileCount,
    int SourcePacketCount,
    int QueuedChunkPacketCount,
    int ServerQueuedChunkPacketCount,
    int ClientQueuedChunkPacketCount,
    int QueuedChunkWithEmbeddedRecordsCount,
    int QueuedChunkWithoutEmbeddedRecordsCount,
    int OpaquePrefixBytes,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount,
    Tf2Ps3SourceQueuedPeerCount[] TopFamilies,
    Tf2Ps3SourceQueuedPeerCount[] TopOpaquePrefixLengths);

public sealed record Tf2Ps3SourceQueuedPeerNamedEvidence(
    string Name,
    string Evidence,
    string Meaning);

public sealed record Tf2Ps3SourceQueuedPeerImplementationTarget(
    string Name,
    string RequiredWork,
    string Evidence,
    string AcceptanceSignal);

public sealed record Tf2Ps3SourceQueuedPeerCount(string Value, int Count);
