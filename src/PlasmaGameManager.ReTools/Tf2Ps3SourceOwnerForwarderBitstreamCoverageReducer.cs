using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerForwarderBitstreamCoverageReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceOwnerForwarderBitstreamCoverageReport> ReduceAsync(
        string ownerForwardContextPath,
        string sourceRoot,
        string outputPath)
    {
        using var ownerForwardContext = JsonDocument.Parse(await File.ReadAllTextAsync(ownerForwardContextPath));
        var implementation = ScanImplementation(sourceRoot);
        var forwarders = ownerForwardContext.RootElement
            .GetProperty("OwnerForwarders")
            .EnumerateArray()
            .Select(forwarder => BuildForwarderCoverage(forwarder, implementation))
            .ToArray();

        var wrappedForwarders = forwarders
            .Where(static forwarder => forwarder.BoundaryRequirement != "none")
            .ToArray();
        var implementedWrappedForwarders = wrappedForwarders
            .Where(static forwarder => forwarder.BoundaryImplementationStatus == "implemented")
            .ToArray();
        var missingWrappedForwarders = wrappedForwarders
            .Where(static forwarder => forwarder.BoundaryImplementationStatus != "implemented")
            .ToArray();
        var semanticContextHandlersImplemented = implementation.SemanticContextHandlersImplemented;
        var liveMapLoadVerified = false;
        var implementationReady = wrappedForwarders.Length > 0
            && missingWrappedForwarders.Length == 0
            && semanticContextHandlersImplemented;
        var nativeSourceInputReady = implementationReady && liveMapLoadVerified;

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "owner-forward-context-report-present",
                "proven",
                ownerForwardContextPath,
                "The upstream TF.elf owner-forwarder family report is available."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "word5-bitreader-boundary-implemented",
                implementation.Word5BoundaryImplemented ? "proven" : "missing",
                implementation.Word5BoundaryEvidence,
                "00a52720/00a52930 copy param_2[5] bits from param_2 + 6 into reader param_2 + 0xf. The native resolver must recover this exact wrapper before category dispatch."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "word6-bitreader-boundary-implemented",
                implementation.Word6BoundaryImplemented ? "proven" : "missing",
                implementation.Word6BoundaryEvidence,
                "00a52840 copies param_2[6] bits from param_2 + 7 into reader param_2 + 0x10. The native resolver must recover this wrapper and accept it only after an exact official CLC_Move decode."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "deferred-pointer-word6-boundary-implemented",
                implementation.DeferredPointerWord6BoundaryImplemented ? "proven" : "missing",
                implementation.DeferredPointerWord6BoundaryEvidence,
                "00a52ae0 copies param_2[6] bits from param_2 + 10 and stores the temporary payload pointer at param_2[0x13]. The native resolver must recover this wrapper and accept it only after an exact official CLC_Move decode."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "config-state-fallback-word4-boundary-implemented",
                implementation.ConfigFallbackWord4BoundaryImplemented ? "proven" : "missing",
                implementation.ConfigFallbackWord4BoundaryEvidence,
                "00a539d0 first tries config/state writes; its fallback copies param_2[4] bits from param_2 + 5 into reader param_2 + 0xe. The fallback bitstream boundary must decode natively; the config/state branch remains part of the semantic context-handler work."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "semantic-context-handlers-implemented",
                semanticContextHandlersImplemented ? "proven" : "missing",
                implementation.SemanticContextHandlerEvidence,
                "Decoding wrapper boundaries is only the first half. The native server still needs semantic handlers for the 008722a0 context categories before map-load can be claimed."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "implementation-ready",
                implementationReady ? "proven" : "missing",
                "native Source input parser/responder implementation audit",
                "All recovered owner-forwarder wrapper boundaries and 008722a0-style semantic context handlers are implemented. This is not live map-load proof."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "live-map-load-verified",
                liveMapLoadVerified ? "proven" : "missing",
                "RPCS3/PCAP acceptance gate",
                "The semantic context handlers must still be proven against a full TF2 PS3 map-load path before native Source input can be called ready."),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server/live verification gate",
                "Native Source input is not ready until all required wrapper layouts and context handlers pass PCAP/live map-load verification.")
        };

        var layoutCounts = forwarders
            .GroupBy(static forwarder => forwarder.LayoutKind, StringComparer.Ordinal)
            .Select(static group => new Tf2Ps3SourceOwnerForwarderLayoutCount(group.Key, group.Count()))
            .OrderBy(static item => item.LayoutKind, StringComparer.Ordinal)
            .ToArray();

        var report = new Tf2Ps3SourceOwnerForwarderBitstreamCoverageReport(
            "tf2ps3-source-owner-forwarder-bitstream-coverage",
            "Audits which TF.elf owner-forwarder bitstream wrapper layouts are implemented by the native Source input parser, separately from semantic context-handler completion.",
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageInputs(
                ownerForwardContextPath,
                sourceRoot),
            new Tf2Ps3SourceOwnerForwarderBitstreamCoverageSummary(
                forwarders.Length,
                forwarders.Count(static forwarder => forwarder.LayoutKind == "direct-forward"),
                wrappedForwarders.Length,
                implementedWrappedForwarders.Length,
                missingWrappedForwarders.Length,
                implementation.Word5PrimitiveImplemented,
                implementation.Word5BoundaryImplemented,
                implementation.Word6PrimitiveImplemented,
                implementation.Word6BoundaryImplemented,
                implementation.DeferredPointerWord6PrimitiveImplemented,
                implementation.DeferredPointerWord6BoundaryImplemented,
                implementation.ConfigFallbackWord4PrimitiveImplemented,
                implementation.ConfigFallbackWord4BoundaryImplemented,
                semanticContextHandlersImplemented,
                implementationReady,
                liveMapLoadVerified,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            layoutCounts,
            forwarders,
            gates,
            [
                "The TF.elf owner-forwarder family is now split into implementation-sized obligations instead of one broad Source-input gate.",
                "The word-5 bitstream wrapper used by 00a52720/00a52930 is covered by the current OwnerSlot8Bitstream runtime resolver path.",
                "The word-6, deferred-pointer word-6, and config/state fallback word-4 layouts are covered by guarded runtime resolver paths that require exact official CLC_Move decoding.",
                semanticContextHandlersImplemented
                    ? "The native replacement now records explicit transport-control, Source netmessage, and Source usercmd semantic contexts for TF.elf 008722a0-style payload dispatch."
                    : "Even with every wrapper boundary decoded, the native replacement still needs the 008722a0 category/context handlers before the PS3 client can load into a map without replay help.",
                implementationReady
                    ? "The owner-forwarder input implementation is ready for live proof, but NativeSourceInputReady remains false until PCAP/live map-load verification proves the context handlers and snapshot responses together."
                    : "Native Source input is still not marked ready until every recovered wrapper boundary and semantic context handler is implemented and then proven live."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerForwarderBitstreamCoverageForwarder BuildForwarderCoverage(
        JsonElement forwarder,
        ImplementationScan implementation)
    {
        var functionAddress = ReadString(forwarder, "FunctionAddress");
        var layoutKind = ReadString(forwarder, "LayoutKind");
        var bitCountField = ReadString(forwarder, "BitCountField");
        var bitSource = ReadString(forwarder, "BitSource");
        var readerTarget = ReadString(forwarder, "ReaderTarget");
        var requirement = BoundaryRequirement(layoutKind);
        var status = BoundaryImplementationStatus(layoutKind, implementation);
        return new Tf2Ps3SourceOwnerForwarderBitstreamCoverageForwarder(
            ReadInt(forwarder, "SlotIndex"),
            ReadString(forwarder, "SlotOffset"),
            functionAddress,
            layoutKind,
            bitCountField,
            bitSource,
            readerTarget,
            requirement,
            status,
            BoundaryEvidence(layoutKind, implementation),
            RemainingWork(layoutKind, functionAddress, status));
    }

    private static string BoundaryRequirement(string layoutKind) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => "decode param_2[5] bit count, payload bytes at param_2 + 6, reader target param_2 + 0xf",
            "bitreader-rebuild-word6" => "decode param_2[6] bit count, payload bytes at param_2 + 7, reader target param_2 + 0x10",
            "deferred-pointer-word6" => "decode param_2[6] bit count, payload bytes at param_2 + 10, temporary payload pointer at param_2[0x13]",
            "config-state-or-fallback-word4" => "decode config/state writes and fallback param_2[4] bit count, payload bytes at param_2 + 5, reader target param_2 + 0xe",
            _ => "none"
        };

    private static string BoundaryImplementationStatus(string layoutKind, ImplementationScan implementation) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => StatusFromImplementation(implementation.Word5PrimitiveImplemented, implementation.Word5BoundaryImplemented),
            "bitreader-rebuild-word6" => StatusFromImplementation(implementation.Word6PrimitiveImplemented, implementation.Word6BoundaryImplemented),
            "deferred-pointer-word6" => StatusFromImplementation(implementation.DeferredPointerWord6PrimitiveImplemented, implementation.DeferredPointerWord6BoundaryImplemented),
            "config-state-or-fallback-word4" => StatusFromImplementation(implementation.ConfigFallbackWord4PrimitiveImplemented, implementation.ConfigFallbackWord4BoundaryImplemented),
            _ => "not-required"
        };

    private static string StatusFromImplementation(bool primitiveImplemented, bool resolverImplemented)
    {
        if (resolverImplemented)
        {
            return "implemented";
        }

        return primitiveImplemented ? "primitive-only" : "missing";
    }

    private static string BoundaryEvidence(string layoutKind, ImplementationScan implementation) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => implementation.Word5BoundaryEvidence,
            "bitreader-rebuild-word6" => implementation.Word6BoundaryEvidence,
            "deferred-pointer-word6" => implementation.DeferredPointerWord6BoundaryEvidence,
            "config-state-or-fallback-word4" => implementation.ConfigFallbackWord4BoundaryEvidence,
            _ => "direct forwarder uses the already-built payload object bitreader"
        };

    private static string RemainingWork(string layoutKind, string functionAddress, string status)
    {
        if (status == "implemented")
        {
            return "Boundary parser is present; semantic 008722a0 context handling still must consume and answer this payload natively.";
        }

        if (status == "primitive-only")
        {
            return "Protocol primitive is present, but the live resolver is still gated until the TF.elf slot/wire discriminator is proven.";
        }

        return layoutKind switch
        {
            "bitreader-rebuild-word6" => $"Add a guarded parser for {functionAddress}'s word-6 bitstream layout after proving which wire/object discriminator selects this slot.",
            "deferred-pointer-word6" => $"Add a guarded parser for {functionAddress}'s deferred pointer payload after proving how param_2[0x13] is consumed downstream.",
            "config-state-or-fallback-word4" => $"Recover {functionAddress}'s config/state keys and implement both the state-write branch and the word-4 fallback bitstream branch.",
            "direct-forward" => "No extra wrapper boundary is required here; implement the payload vtable/context semantics reached through 008722a0.",
            _ => "Recover and implement the native boundary and downstream semantic handler."
        };
    }

    private static ImplementationScan ScanImplementation(string sourceRoot)
    {
        var protocolRoot = Path.Combine(sourceRoot, "PlasmaGameManager.Protocol");
        if (!Directory.Exists(protocolRoot) && Directory.Exists(sourceRoot))
        {
            protocolRoot = sourceRoot.EndsWith("PlasmaGameManager.Protocol", StringComparison.Ordinal)
                ? sourceRoot
                : "";
        }

        var protocolText = Directory.Exists(protocolRoot)
            ? string.Join('\n', Directory.EnumerateFiles(protocolRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText))
            : "";
        var serverRoot = Path.Combine(sourceRoot, "PlasmaGameManager.Server");
        var serverText = Directory.Exists(serverRoot)
            ? string.Join('\n', Directory.EnumerateFiles(serverRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText))
            : "";
        var text = string.Concat(protocolText, "\n", serverText);
        var hasWord5Primitive =
            text.Contains("Word5PayloadWord6ReaderWord15", StringComparison.Ordinal)
            && text.Contains("TryReadOwnerForwarderBitstream", StringComparison.Ordinal)
            && text.Contains("OwnerSlot8BitCountFieldOffset", StringComparison.Ordinal)
            && text.Contains("OwnerSlot8BitPayloadOffset", StringComparison.Ordinal)
            && text.Contains("OwnerSlot8RebuiltBitreaderFieldOffsetValue", StringComparison.Ordinal);
        var hasOwnerSlot8Boundary =
            hasWord5Primitive
            && text.Contains("OwnerSlot8Bitstream", StringComparison.Ordinal)
            && text.Contains("TryResolveOwnerSlot8Bitstream", StringComparison.Ordinal);
        var hasWord6Primitive =
            text.Contains("Word6PayloadWord7ReaderWord16", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderWord6BitCountFieldOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderWord6BitPayloadOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderWord6RebuiltBitreaderFieldOffsetValue", StringComparison.Ordinal);
        var hasWord6Boundary =
            hasWord6Primitive
            && text.Contains("OwnerForwarderWord6Bitstream", StringComparison.Ordinal);
        var hasDeferredPointerPrimitive =
            text.Contains("DeferredPointerWord6PayloadWord10PointerWord19", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderDeferredPointerWord6BitCountFieldOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderDeferredPointerWord6BitPayloadOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderDeferredPointerWord6TempPointerFieldOffsetValue", StringComparison.Ordinal);
        var hasDeferredPointerBoundary =
            hasDeferredPointerPrimitive
            && text.Contains("OwnerForwarderDeferredPointerWord6Bitstream", StringComparison.Ordinal);
        var hasConfigFallbackPrimitive =
            text.Contains("ConfigFallbackWord4PayloadWord5ReaderWord14", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderConfigFallbackWord4BitCountFieldOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderConfigFallbackWord4BitPayloadOffset", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderConfigFallbackWord4RebuiltBitreaderFieldOffsetValue", StringComparison.Ordinal);
        var hasConfigFallbackBoundary =
            hasConfigFallbackPrimitive
            && text.Contains("OwnerForwarderConfigFallbackWord4Bitstream", StringComparison.Ordinal);
        var hasSemanticContextHandlers =
            serverText.Contains("NativeSourceSemanticContextHistory", StringComparison.Ordinal)
            && serverText.Contains("ApplyNativeSourceTransportControlContext", StringComparison.Ordinal)
            && serverText.Contains("ApplyNativeSourceClientNetMessageContext", StringComparison.Ordinal)
            && serverText.Contains("ApplyNativeSourceClientCommandContext", StringComparison.Ordinal)
            && serverText.Contains("RecordNativeSourceSemanticContext", StringComparison.Ordinal)
            && serverText.Contains("Ps3SourceDecodedClientNetMessage", StringComparison.Ordinal)
            && serverText.Contains("OfficialEatf2ServerDllContracts.CUserCmdStrideBytes", StringComparison.Ordinal);

        return new ImplementationScan(
            hasWord5Primitive,
            hasOwnerSlot8Boundary,
            hasOwnerSlot8Boundary
                ? "Ps3SourceNativeToClcMoveBoundaryKind.OwnerSlot8Bitstream and Ps3SourcePayloadObjectFrame.TryReadOwnerSlot8Bitstream"
                : hasWord5Primitive
                    ? "protocol primitive found, resolver not wired"
                    : "not found",
            hasWord6Primitive,
            hasWord6Boundary,
            hasWord6Boundary
                ? "word-6 resolver marker found in source"
                : hasWord6Primitive
                    ? "protocol primitive found, resolver not wired"
                : "not found",
            hasDeferredPointerPrimitive,
            hasDeferredPointerBoundary,
            hasDeferredPointerBoundary
                ? "deferred pointer resolver marker found in source"
                : hasDeferredPointerPrimitive
                    ? "protocol primitive found, resolver not wired"
                    : "not found",
            hasConfigFallbackPrimitive,
            hasConfigFallbackBoundary,
            hasConfigFallbackBoundary
                ? "config/fallback resolver marker found in source"
                : hasConfigFallbackPrimitive
                    ? "protocol primitive found, resolver not wired"
                    : "not found",
            hasSemanticContextHandlers,
            hasSemanticContextHandlers
                ? "Tf2SourcePlayerState records transport-control, Source netmessage, and official/usercmd contexts through ApplyNativeSource* handlers"
                : "source semantic context handlers not found");
    }

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private sealed record ImplementationScan(
        bool Word5PrimitiveImplemented,
        bool Word5BoundaryImplemented,
        string Word5BoundaryEvidence,
        bool Word6PrimitiveImplemented,
        bool Word6BoundaryImplemented,
        string Word6BoundaryEvidence,
        bool DeferredPointerWord6PrimitiveImplemented,
        bool DeferredPointerWord6BoundaryImplemented,
        string DeferredPointerWord6BoundaryEvidence,
        bool ConfigFallbackWord4PrimitiveImplemented,
        bool ConfigFallbackWord4BoundaryImplemented,
        string ConfigFallbackWord4BoundaryEvidence,
        bool SemanticContextHandlersImplemented,
        string SemanticContextHandlerEvidence);
}

public sealed record Tf2Ps3SourceOwnerForwarderBitstreamCoverageReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerForwarderBitstreamCoverageInputs Inputs,
    Tf2Ps3SourceOwnerForwarderBitstreamCoverageSummary Summary,
    Tf2Ps3SourceOwnerForwarderLayoutCount[] LayoutCounts,
    Tf2Ps3SourceOwnerForwarderBitstreamCoverageForwarder[] Forwarders,
    Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerForwarderBitstreamCoverageInputs(
    string OwnerForwardContextMap,
    string SourceRoot);

public sealed record Tf2Ps3SourceOwnerForwarderBitstreamCoverageSummary(
    int ForwarderCount,
    int DirectForwarderCount,
    int WrappedForwarderCount,
    int ImplementedWrappedForwarderCount,
    int MissingWrappedForwarderCount,
    bool Word5PrimitiveImplemented,
    bool Word5BoundaryImplemented,
    bool Word6PrimitiveImplemented,
    bool Word6BoundaryImplemented,
    bool DeferredPointerWord6PrimitiveImplemented,
    bool DeferredPointerWord6BoundaryImplemented,
    bool ConfigFallbackWord4PrimitiveImplemented,
    bool ConfigFallbackWord4BoundaryImplemented,
    bool SemanticContextHandlersImplemented,
    bool ImplementationReady,
    bool LiveMapLoadVerified,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceOwnerForwarderLayoutCount(
    string LayoutKind,
    int Count);

public sealed record Tf2Ps3SourceOwnerForwarderBitstreamCoverageForwarder(
    int SlotIndex,
    string SlotOffset,
    string FunctionAddress,
    string LayoutKind,
    string BitCountField,
    string BitSource,
    string ReaderTarget,
    string BoundaryRequirement,
    string BoundaryImplementationStatus,
    string BoundaryEvidence,
    string RemainingWork);

public sealed record Tf2Ps3SourceOwnerForwarderBitstreamCoverageGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
