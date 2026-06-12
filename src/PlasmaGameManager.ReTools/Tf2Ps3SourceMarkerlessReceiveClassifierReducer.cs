using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMarkerlessReceiveClassifierReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceMarkerlessReceiveClassifierReport> ReduceAsync(
        string opaqueWrapperReportPath,
        string receivePathReportPath,
        string nativeMessageContractPath,
        string clcMoveContractPath,
        string netchanRegistrationSetupPath,
        string requiredHandlerConstructorPath,
        string outputPath)
    {
        using var opaqueWrapper = JsonDocument.Parse(await File.ReadAllTextAsync(opaqueWrapperReportPath));
        using var receivePath = JsonDocument.Parse(await File.ReadAllTextAsync(receivePathReportPath));
        using var nativeContract = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));
        using var clcMoveContract = JsonDocument.Parse(await File.ReadAllTextAsync(clcMoveContractPath));
        using var netchanSetup = JsonDocument.Parse(await File.ReadAllTextAsync(netchanRegistrationSetupPath));
        using var requiredConstructor = JsonDocument.Parse(await File.ReadAllTextAsync(requiredHandlerConstructorPath));

        var opaqueSummary = opaqueWrapper.RootElement.GetProperty("Summary");
        var receiveSummary = receivePath.RootElement.GetProperty("Summary");
        var receiveFunctions = receivePath.RootElement.GetProperty("Functions").EnumerateArray().ToArray();
        var receiveFunctionByAddress = receiveFunctions.ToDictionary(
            static function => function.GetProperty("Address").GetString() ?? "",
            StringComparer.Ordinal);

        var clcMove = nativeContract.RootElement.GetProperty("Messages").EnumerateArray()
            .Single(static message => message.GetProperty("ClassName").GetString() == "CLC_Move");
        var clcMoveSummary = clcMoveContract.RootElement.GetProperty("Summary");
        var netchanSummary = netchanSetup.RootElement.GetProperty("Summary");
        var constructorSummary = requiredConstructor.RootElement.GetProperty("Summary");

        var gates = BuildGates(
            opaqueSummary,
            receiveSummary,
            receiveFunctionByAddress,
            clcMove,
            clcMoveSummary,
            netchanSummary,
            constructorSummary);
        var targets = BuildTargets(receiveFunctionByAddress, clcMove, clcMoveSummary);
        var families = opaqueWrapper.RootElement.GetProperty("LengthFamilies").EnumerateArray()
            .Take(12)
            .Select(static family => new Tf2Ps3SourceMarkerlessLengthFamily(
                family.GetProperty("BodyLength").GetInt32(),
                family.GetProperty("Count").GetInt32(),
                family.GetProperty("DistinctFileCount").GetInt32(),
                family.GetProperty("AverageEntropy").GetDouble(),
                TopCount(family, "PhaseCounts"),
                TopCount(family, "SequenceDeltaCounts")))
            .ToArray();

        var markerlessTransformProven = gates.Any(static gate =>
            gate.Id == "markerless-transform-to-native-bitstream" && gate.Status == "proven");
        var report = new Tf2Ps3SourceMarkerlessReceiveClassifierReport(
            "tf2ps3-source-markerless-receive-classifier",
            "Consolidates the remaining direct markerless client receive gap. This is a native reverse-engineering worklist, not packet replay data.",
            new Tf2Ps3SourceMarkerlessReceiveClassifierInputs(
                opaqueWrapperReportPath,
                receivePathReportPath,
                nativeMessageContractPath,
                clcMoveContractPath,
                netchanRegistrationSetupPath,
                requiredHandlerConstructorPath),
            new Tf2Ps3SourceMarkerlessReceiveClassifierSummary(
                opaqueSummary.GetProperty("OpaquePacketCount").GetInt32(),
                opaqueSummary.GetProperty("DistinctBodyLengthCount").GetInt32(),
                opaqueSummary.GetProperty("MinBodyLength").GetInt32(),
                opaqueSummary.GetProperty("MaxBodyLength").GetInt32(),
                opaqueSummary.GetProperty("AverageBodyLength").GetDouble(),
                opaqueSummary.GetProperty("AverageEntropy").GetDouble(),
                opaqueSummary.GetProperty("HighEntropyPacketCount").GetInt32(),
                opaqueSummary.GetProperty("UniquePrefix8Ratio").GetDouble(),
                opaqueSummary.GetProperty("UniqueSuffix8Ratio").GetDouble(),
                families.Length == 0 ? 0 : families[0].BodyLength,
                receiveSummary.GetProperty("HasReceiveDrain").GetBoolean(),
                receiveSummary.GetProperty("ReceiveDrainUsesRecvfrom").GetBoolean(),
                GetFunction(receiveFunctionByAddress, "00a5c2e8") is not null,
                GetFunction(receiveFunctionByAddress, "00a58c10") is not null,
                clcMove.GetProperty("ReadFromBufferEntry").GetString() ?? "",
                clcMove.GetProperty("ProcessEntry").GetString() ?? "",
                clcMoveSummary.GetProperty("ProcessUsercmdsBridgeFunction").GetString() ?? "",
                netchanSummary.GetProperty("TfElfNetMessageTypeBits").GetInt32(),
                constructorSummary.GetProperty("PrimaryConstructorCandidate").GetString() ?? "",
                constructorSummary.GetProperty("PrimaryConstructorRequiredHandlerCount").GetInt32(),
                markerlessTransformProven,
                markerlessTransformProven && gates.All(static gate => gate.Status == "proven"),
                gates.Count(static gate => gate.Status != "proven")),
            gates,
            targets,
            families,
            [
                "The direct markerless client body family remains the dominant native receive blocker after excluding attached frames, bit sidecars, embedded CLC_Move, and decoded non-move NET/CLC candidates.",
                "TF.elf proves the official inner Source message dispatcher and CLC_Move reader, but current evidence does not prove how the direct markerless datagrams are transformed or routed into that bitstream.",
                "A native replacement server must not treat the heuristic movement intent fields as authoritative until the markerless transform-to-bitstream gate is proven.",
                "The next decompile/export pass should focus on the post-recvfrom use of the 008b8d50 buffer and the helper-slice receive candidates 00a5b4f0/00a5d9e0, then connect that path to 00a58c10 or a sibling parser."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceMarkerlessGate[] BuildGates(
        JsonElement opaqueSummary,
        JsonElement receiveSummary,
        IReadOnlyDictionary<string, JsonElement> receiveFunctionByAddress,
        JsonElement clcMove,
        JsonElement clcMoveSummary,
        JsonElement netchanSummary,
        JsonElement constructorSummary)
    {
        return
        [
            new(
                "opaque-markerless-hard-set-isolated",
                "proven",
                "PCAP corpus",
                $"{opaqueSummary.GetProperty("OpaquePacketCount").GetInt32()} client packets remain after excluding exact/embedded CLC_Move, attached frames, bit-sidecars, embedded objects, and decoded non-move NET/CLC candidates.",
                opaqueSummary.GetProperty("BoundaryConclusion").GetString() ?? ""),
            new(
                "udp-gameplay-ingress-proven",
                receiveSummary.GetProperty("HasReceiveDrain").GetBoolean()
                    && receiveSummary.GetProperty("ReceiveDrainUsesRecvfrom").GetBoolean()
                    ? "proven"
                    : "missing",
                "TF.elf 008b8d50",
                "008b8d50 drains gameplay UDP with recvfrom into a 0x800 byte buffer using nonblocking flag 0x80.",
                "Find the code path that consumes the received buffer after the current Ghidra C export's truncated-looking recv loop."),
            new(
                "attached-frame-type2-dispatch-proven",
                HasTokens(receiveFunctionByAddress, "00a5c2e8", "_opd_FUN_00a58c10", "iVar2 == 2")
                    ? "proven"
                    : "missing",
                "TF.elf 00a5c2e8 -> 00a58c10",
                "Attached frame kind 2 reads 16-bit length + 32-bit token, stages payload bytes, then dispatches the bitstream through 00a58c10.",
                "This visible frame path is proven, but the hard markerless PCAP set does not match it."),
            new(
                "inner-source-dispatcher-proven",
                HasTokens(receiveFunctionByAddress, "00a58c10", "_opd_FUN_00a57f48", "_opd_FUN_00a58868", "2 < uVar15")
                    && netchanSummary.GetProperty("TfElfNetMessageTypeBits").GetInt32() == 5
                    ? "proven"
                    : "missing",
                "TF.elf 00a58c10",
                "00a58c10 reads 5-bit message ids, handles built-in 0/1/2 controls, and dispatches ids >=3 through registered handlers.",
                "Keep using the recovered 5-bit message contract for any proven inner Source bitstream."),
            new(
                "clc-move-contract-proven",
                (clcMove.GetProperty("MessageId").GetInt32() == 9
                    && clcMove.GetProperty("ReadFromBufferEntry").GetString() == "0x008c8a50"
                    && clcMoveSummary.GetProperty("ProcessUsercmdsBridgeFunction").GetString() == "00a291c0")
                    ? "proven"
                    : "missing",
                "TF.elf CLC_Move/server.dll usercmd cross-check",
                $"CLC_Move id {clcMove.GetProperty("MessageId").GetInt32()} reads at {clcMove.GetProperty("ReadFromBufferEntry").GetString()}, dispatches at {clcMove.GetProperty("ProcessEntry").GetString()}, and reaches ProcessUsercmds bridge {clcMoveSummary.GetProperty("ProcessUsercmdsBridgeFunction").GetString()}.",
                "The CUserCmd bit layout is not the current blocker; the wrapper that exposes it is."),
            new(
                "required-handler-constructor-proven",
                constructorSummary.GetProperty("PrimaryConstructorRequiredHandlerCount").GetInt32() >= 11
                    ? "proven"
                    : "missing",
                "TF.elf 009fa398",
                $"Constructor {constructorSummary.GetProperty("PrimaryConstructorCandidate").GetString()} allocates/registers {constructorSummary.GetProperty("PrimaryConstructorRequiredHandlerCount").GetInt32()} required client handlers.",
                "Required NET/CLC handler construction is recovered for connection-start messages."),
            new(
                "markerless-transform-to-native-bitstream",
                "missing",
                "TF.elf direct markerless receive path",
                "No current report proves that a direct markerless PCAP body is converted into the 00a58c10 bitstream or directly into CLC_Move::ReadFromBuffer.",
                "Decompile/trace the buffer path immediately after 008b8d50 recvfrom, plus helper-slice receive candidates 00a5b4f0 and 00a5d9e0, until the exact transform/classifier is named."),
            new(
                "native-server-input-ready",
                "missing",
                "implementation gate",
                "A native replacement can safely consume movement/loading-ack packets only after the markerless transform gate is proven.",
                "Until then, any movement fields decoded from opaque markerless bodies are heuristic and must not be treated as exact protocol truth.")
        ];
    }

    private static Tf2Ps3SourceMarkerlessTarget[] BuildTargets(
        IReadOnlyDictionary<string, JsonElement> receiveFunctionByAddress,
        JsonElement clcMove,
        JsonElement clcMoveSummary)
    {
        return
        [
            Target(
                receiveFunctionByAddress,
                "008b8d50",
                "direct UDP ingress",
                "First proven receive point for direct markerless datagrams; current decompile only proves recvfrom shape, not how the buffer is classified.",
                "post-recvfrom buffer consumer/call edge"),
            Target(
                receiveFunctionByAddress,
                "008b9468",
                "accepted-peer association control",
                "Rules out the 5-byte accepted-peer type-4 attach path as the bulk movement path, but keeps association semantics anchored.",
                "association state and accepted socket attach only"),
            Target(
                receiveFunctionByAddress,
                "00a5c2e8",
                "visible attached-frame stream reader",
                "Proven parser for frame kind 1/2/3/4 on attached sockets; hard markerless packets currently bypass this visible framing.",
                "sibling caller or prior wrapper that converts direct bodies into this reader"),
            Target(
                receiveFunctionByAddress,
                "00a58c10",
                "inner Source bitstream dispatcher",
                "Official 5-bit NET/CLC dispatcher once a staged bitstream exists.",
                "proof that markerless bodies reach this dispatcher"),
            new(
                "008c8a50",
                "CLC_Move reader",
                "Recovered from native message contract",
                "Reads new command count, backup count, bit count, and copied usercmd bitstream.",
                "proof that markerless bodies expose this reader's expected boundary",
                [clcMove.GetProperty("ReadFromBufferEntry").GetString() ?? ""]),
            new(
                "008d42e0",
                "CLC_Move handler dispatch",
                "Recovered from native message contract",
                "Dispatches the parsed CLC_Move handler object.",
                "proof that markerless bodies expose a CLC_Move object to this handler",
                [clcMove.GetProperty("ProcessEntry").GetString() ?? ""]),
            new(
                "00a291c0",
                "ProcessUsercmds gameplay bridge",
                "Recovered from source-clc-move-contract",
                "Applies decoded CUserCmd batches to game-side player simulation after CLC_Move parsing.",
                "exact call edge from decoded markerless input into gameplay command application",
                [clcMoveSummary.GetProperty("ProcessUsercmdsBridgeFunction").GetString() ?? ""]),
            new(
                "00a5b4f0",
                "helper-slice receive candidate",
                "Referenced by prior helper-slice maps as a payload-dispatch caller adjacent to the attached reader.",
                "May be a sibling receive/classifier path not covered by the visible attached-frame contract.",
                "targeted Ghidra export/decompile and caller proof",
                []),
            new(
                "00a5d9e0",
                "helper-slice receive candidate",
                "Referenced by prior helper-slice maps as a payload-dispatch caller adjacent to the attached reader.",
                "May own another receive/classifier branch for direct markerless or late map-load packets.",
                "targeted Ghidra export/decompile and caller proof",
                [])
        ];
    }

    private static Tf2Ps3SourceMarkerlessTarget Target(
        IReadOnlyDictionary<string, JsonElement> receiveFunctionByAddress,
        string address,
        string role,
        string why,
        string missingProof)
    {
        var function = GetFunction(receiveFunctionByAddress, address);
        return new Tf2Ps3SourceMarkerlessTarget(
            address,
            role,
            function is null ? "not-in-current-receive-report" : "present-in-source-receive-path-map",
            why,
            missingProof,
            function is null ? [] : StringArray(function.Value, "EvidenceTokens"));
    }

    private static JsonElement? GetFunction(IReadOnlyDictionary<string, JsonElement> functions, string address)
    {
        return functions.TryGetValue(address, out var function) ? function : null;
    }

    private static bool HasTokens(
        IReadOnlyDictionary<string, JsonElement> functions,
        string address,
        params string[] tokens)
    {
        var function = GetFunction(functions, address);
        if (function is null)
        {
            return false;
        }

        var evidence = StringArray(function.Value, "EvidenceTokens").ToHashSet(StringComparer.Ordinal);
        var calls = StringArray(function.Value, "Calls").ToHashSet(StringComparer.Ordinal);
        return tokens.All(token => evidence.Contains(token) || calls.Contains(token));
    }

    private static string[] StringArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(static value => value.GetString() ?? "").ToArray()
            : [];
    }

    private static Tf2Ps3SourceMarkerlessCount? TopCount(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var counts) || counts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = counts.EnumerateArray().FirstOrDefault();
        return first.ValueKind == JsonValueKind.Object
            ? new Tf2Ps3SourceMarkerlessCount(
                first.GetProperty("Value").GetString() ?? "",
                first.GetProperty("Count").GetInt32())
            : null;
    }
}

public sealed record Tf2Ps3SourceMarkerlessReceiveClassifierReport(
    string Status,
    string Note,
    Tf2Ps3SourceMarkerlessReceiveClassifierInputs Inputs,
    Tf2Ps3SourceMarkerlessReceiveClassifierSummary Summary,
    Tf2Ps3SourceMarkerlessGate[] Gates,
    Tf2Ps3SourceMarkerlessTarget[] ReverseEngineeringTargets,
    Tf2Ps3SourceMarkerlessLengthFamily[] DominantOpaqueLengthFamilies,
    string[] Conclusions);

public sealed record Tf2Ps3SourceMarkerlessReceiveClassifierInputs(
    string OpaqueWrapperReport,
    string ReceivePathReport,
    string NativeMessageContract,
    string ClcMoveContract,
    string NetchanRegistrationSetup,
    string RequiredHandlerConstructor);

public sealed record Tf2Ps3SourceMarkerlessReceiveClassifierSummary(
    int OpaquePacketCount,
    int DistinctBodyLengthCount,
    int MinBodyLength,
    int MaxBodyLength,
    double AverageBodyLength,
    double AverageEntropy,
    int HighEntropyPacketCount,
    double UniquePrefix8Ratio,
    double UniqueSuffix8Ratio,
    int DominantBodyLength,
    bool HasUdpReceiveDrain,
    bool UdpReceiveDrainUsesRecvfrom,
    bool HasAttachedFrameReader,
    bool HasInnerSourceDispatcher,
    string ClcMoveReadEntry,
    string ClcMoveProcessEntry,
    string ProcessUsercmdsBridgeEntry,
    int TfElfNetMessageTypeBits,
    string RequiredHandlerPrimaryConstructor,
    int RequiredHandlerPrimaryConstructorCount,
    bool MarkerlessTransformProven,
    bool NativeInputImplementationReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceMarkerlessGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Evidence,
    string RemainingWork);

public sealed record Tf2Ps3SourceMarkerlessTarget(
    string Address,
    string Role,
    string EvidenceStatus,
    string WhyItMatters,
    string MissingProof,
    string[] CurrentEvidenceTokens);

public sealed record Tf2Ps3SourceMarkerlessLengthFamily(
    int BodyLength,
    int Count,
    int DistinctFileCount,
    double AverageEntropy,
    Tf2Ps3SourceMarkerlessCount? TopPhase,
    Tf2Ps3SourceMarkerlessCount? TopSequenceDelta);

public sealed record Tf2Ps3SourceMarkerlessCount(
    string Value,
    int Count);
