using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMarkerlessParam2BuilderReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Tf2Ps3SourceMarkerlessParam2Target[] Targets =
    [
        new(
            "008bdb88",
            "markerless-connected-recv-fill-and-special-wrapper",
            [
                new("socket-receive-fill", "_opd_FUN_00924dd0", "Receives/fills the payload buffer field from the connected socket layer."),
                new("payload-buffer-field", "piVar4[6]", "Uses payload object word 6 as the writable packet buffer pointer."),
                new("payload-length-field-set", "piVar4[0x10] = uVar9", "Stores the byte length consumed by the downstream bitreader."),
                new("payload-owner-length-field-set", "piVar4[0x11] = uVar9", "Stores the owner/raw receive length beside the reader length."),
                new("max-payload-cap", "0x1770f", "Rejects receives larger than the 0x17710 payload object buffer."),
                new("fragment-wrapper-type-minus2", "local_20a0 == -2", "Routes special type -2 packets into fragment reassembly."),
                new("fragment-reassembly-call", "_opd_FUN_008bd708", "Calls the fragment reassembler for type -2 wrappers."),
                new("repacked-wrapper-type-minus3", "local_20a0 == -3", "Routes special type -3 packets through a local repack/decompression branch."),
                new("payload-submit", "_opd_FUN_008bb468('\\x01'", "Submits the completed payload object into the same queue/dispatch mechanism.")
            ]),
        new(
            "008bd708",
            "fragment-reassembly-special-type-minus2",
            [
                new("fragment-min-length-check", "9 < (uint)param_2[0x10]", "Requires at least a 10-byte fragment wrapper before reassembly."),
                new("fragment-body-offset", "param_2[6] + 10", "Copies fragment bodies after the 10-byte wrapper header."),
                new("fragment-buffer-copy", "FUN_00871708", "Copies fragment data into the per-peer reassembly buffer."),
                new("assembled-length-field-set", "param_2[0x10] = piVar8[0xc5]", "Replaces the payload object length with the reassembled byte count."),
                new("assembled-owner-length-field-set", "param_2[0x11] = piVar8[0xc5]", "Mirrors the reassembled byte count into the owner/raw length slot."),
                new("compressed-repack-helper", "_opd_FUN_008b7950", "Handles the compressed/repacked special wrapper branch before payload replacement."),
                new("checksum-init", "FUN_0086a588", "Initializes checksum state for repacked fragment validation."),
                new("checksum-update", "FUN_0086a948", "Checksums the repacked output buffer."),
                new("checksum-final", "FUN_0086bfd8", "Finalizes checksum before accepting decompressed/repacked data.")
            ]),
        new(
            "008bdff0",
            "payload-object-builder-and-bitreader-init",
            [
                new("payload-object-pool-base", "PTR_DAT_0197336c + 0x6c0", "Initializes and uses the payload object pool at global +0x6c0."),
                new("payload-object-stride", "param_1 * 0x50", "Indexes payload objects by connection id with a 0x50 byte stride."),
                new("payload-buffer-field-set", "puVar8[6] = param_2", "Stores the received buffer pointer at payload object +0x18."),
                new("payload-bitreader-word", "puVar8[7] = uVar1", "Seeds the bitreader object beginning at payload object +0x1c."),
                new("payload-length-field-zeroed", "puVar8[0x10] = 0", "Clears the payload length field before receive/fill."),
                new("payload-owner-length-field-zeroed", "puVar8[0x11] = 0", "Clears the owner/raw length field before receive/fill."),
                new("connected-recv-fill-call", "_opd_FUN_008bdb88", "Calls the receive/fill routine when the native network path is enabled."),
                new("queued-fallback-submit", "_opd_FUN_008bb468('\\0'", "Falls back to queued payload submission if direct fill did not return a payload."),
                new("bitreader-init-param2-plus7", "FUN_0086de68((int)(puVar8 + 7),puVar8[6],puVar8[0x10],0,-1)", "Builds the bitreader consumed as param_2 + 7 by downstream Source handlers.")
            ]),
        new(
            "008be1e8",
            "payload-object-drain-and-dispatch-loop",
            [
                new("payload-builder-call", "_opd_FUN_008bdff0", "Allocates/refills payload objects through the recovered builder."),
                new("payload-validity-gate", "_opd_FUN_00a2fcb0", "Checks whether the payload object should keep draining or be finalized."),
                new("payload-first-word-read", "uVar11 = *(uint *)piVar7[6]", "Reads the first payload word to choose dispatch style."),
                new("associated-object-lookup", "_opd_FUN_008b9ad8", "Maps non--1 payloads to their associated Source object/player channel."),
                new("associated-object-slot90-dispatch", "*piVar7 + 0x90", "Dispatches associated payload objects through virtual slot +0x90."),
                new("payload-bitreader-offset", "uVar5 + 0x1c", "Uses payload object +0x1c as the in-place bitreader for unassociated payloads."),
                new("owner-slot8-dispatch", "*param_2 + 8", "Dispatches unassociated payload objects through owner virtual slot +0x08."),
                new("payload-finalize", "_opd_FUN_00a2ffd8", "Finalizes payload objects after the validity gate stops draining.")
            ]),
        new(
            "008bb468",
            "queued-or-immediate-payload-submit",
            [
                new("queued-submit-symbol", "_opd_FUN_008bb468", "Payload submit helper used by both fresh receive and fallback builder paths.")
            ]),
        new(
            "008b9ad8",
            "source-object-association-from-payload-token",
            [
                new("association-symbol", "_opd_FUN_008b9ad8", "Resolves payload first-word tokens to associated Source/player objects before slot +0x90 dispatch.")
            ])
    ];

    public static async Task<Tf2Ps3SourceMarkerlessParam2BuilderReport> ReduceAsync(
        string cExportPath,
        string recvBitreaderCensusPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var functionMap = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        using var recvCensus = JsonDocument.Parse(await File.ReadAllTextAsync(recvBitreaderCensusPath));

        var entries = Targets
            .Select(target => BuildEntry(target, functionMap))
            .ToArray();

        var summary = recvCensus.RootElement.GetProperty("Summary");
        var visibleRecvPathExhausted =
            ReadBool(summary, "ConnectedRecvDispatcherUnique")
            && ReadInt(summary, "HardMarkerlessRecvBitreaderCandidateCount") == 0
            && ReadBool(summary, "StrictPcapHardSetStillOpaque");
        var payloadObjectBuilderRecovered = IsRecovered(entries, "008bdff0");
        var markerlessRecvFillRecovered = IsRecovered(entries, "008bdb88");
        var fragmentReassemblyRecovered = IsRecovered(entries, "008bd708");
        var drainDispatchLoopRecovered = IsRecovered(entries, "008be1e8");
        var concretePayloadObjectBoundaryRecovered =
            visibleRecvPathExhausted
            && payloadObjectBuilderRecovered
            && markerlessRecvFillRecovered
            && fragmentReassemblyRecovered
            && drainDispatchLoopRecovered;
        var serverImplementationUpdated = false;
        var nativeSourceInputReady = concretePayloadObjectBoundaryRecovered && serverImplementationUpdated;

        var gates = new[]
        {
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "visible-direct-recv-path-exhausted",
                visibleRecvPathExhausted ? "proven" : "needs-review",
                recvBitreaderCensusPath,
                "The prior census proves the hard markerless PCAP body family is not explained by another visible direct recv-to-dispatch path."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "payload-object-builder-recovered",
                payloadObjectBuilderRecovered ? "proven" : "missing",
                "008bdff0",
                "Payload objects are allocated/indexed at global +0x6c0 with 0x50 byte stride and converted into a param_2+7 bitreader."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "markerless-recv-fill-recovered",
                markerlessRecvFillRecovered ? "proven" : "missing",
                "008bdb88",
                "The socket receive/fill routine writes bytes into payload object field +0x18 and lengths into +0x40/+0x44."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "fragment-reassembly-special-wrapper-recovered",
                fragmentReassemblyRecovered ? "proven" : "missing",
                "008bd708",
                "Special type -2 packet wrappers are reassembled and rewritten into the same payload object buffer/length fields."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "payload-object-drain-dispatch-loop-recovered",
                drainDispatchLoopRecovered ? "proven" : "missing",
                "008be1e8",
                "The drain loop dispatches payload objects through associated slot +0x90 or owner slot +0x08 after building the in-place bitreader."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "concrete-markerless-payload-object-boundary",
                concretePayloadObjectBoundaryRecovered ? "candidate-proven" : "missing",
                "008bdb88 -> 008bdff0 -> 008be1e8",
                "The missing boundary is now narrowed to this payload-object wrapper path, but the replacement server still needs to implement it."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "server-implementation-updated",
                serverImplementationUpdated ? "proven" : "missing",
                "Ps3NativeSourceResponder",
                "The native Source responder must consume client markerless bodies through this recovered payload object contract instead of relying on replay/fallback data."),
            new Tf2Ps3SourceMarkerlessParam2BuilderGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "Native Source input is not ready until the recovered payload-object wrapper is implemented and verified against live/client PCAP traffic.")
        };

        var report = new Tf2Ps3SourceMarkerlessParam2BuilderReport(
            "tf2ps3-source-markerless-param2-builder",
            "Recovers the TF.elf payload-object wrapper that bridges markerless/opaque client Source bodies into the param_2+7 bitreader consumed by native Source handlers.",
            new Tf2Ps3SourceMarkerlessParam2BuilderInputs(
                cExportPath,
                recvBitreaderCensusPath),
            new Tf2Ps3SourceMarkerlessParam2BuilderSummary(
                "008bdff0",
                "008bdb88",
                "008bd708",
                "008be1e8",
                "0x50",
                "global+0x6c0",
                "+0x18",
                "+0x1c",
                "+0x40",
                "+0x44",
                visibleRecvPathExhausted,
                payloadObjectBuilderRecovered,
                markerlessRecvFillRecovered,
                fragmentReassemblyRecovered,
                drainDispatchLoopRecovered,
                concretePayloadObjectBoundaryRecovered,
                serverImplementationUpdated,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            entries,
            gates,
            [
                "The raw TF.elf C export now shows the caller-side object builder that the older slot70 report could not recover: 008bdff0 indexes a 0x50-byte payload object, stores buffer/length fields, and initializes a bitreader at object +0x1c.",
                "008bdb88 is the receive/fill side: it writes into payload object word 6 and sets words 0x10/0x11, including special -2 fragment and -3 repack paths.",
                "008be1e8 is the drain/dispatch side: non--1 payloads dispatch through an associated object slot +0x90, while -1 payloads use the in-place bitreader and owner slot +0x08.",
                "This is strong native boundary evidence, but it is not yet a playable implementation. The replacement server still needs to implement this client upload contract and validate it against PCAP/live traffic."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceMarkerlessParam2Entry BuildEntry(
        Tf2Ps3SourceMarkerlessParam2Target target,
        IReadOnlyDictionary<string, ExportedFunction> functionMap)
    {
        if (!functionMap.TryGetValue(target.Address, out var function))
        {
            return new Tf2Ps3SourceMarkerlessParam2Entry(
                target.Address,
                "",
                target.Role,
                "missing",
                [],
                target.RequiredEvidence
                    .Select(static evidence => new Tf2Ps3SourceMarkerlessParam2Evidence(
                        evidence.Id,
                        evidence.Needle,
                        evidence.Meaning,
                        false))
                    .ToArray(),
                [],
                "");
        }

        var evidence = target.RequiredEvidence
            .Select(evidence => new Tf2Ps3SourceMarkerlessParam2Evidence(
                evidence.Id,
                evidence.Needle,
                evidence.Meaning,
                function.Body.Contains(evidence.Needle, StringComparison.Ordinal)))
            .ToArray();
        var missing = evidence.Where(static item => !item.Found).Select(static item => item.Id).ToArray();
        var status = missing.Length == 0 ? "recovered" : "partial";

        return new Tf2Ps3SourceMarkerlessParam2Entry(
            target.Address,
            function.Name,
            target.Role,
            status,
            missing,
            evidence,
            ExtractRelevantLines(function.Body),
            Preview(function));
    }

    private static bool IsRecovered(IEnumerable<Tf2Ps3SourceMarkerlessParam2Entry> entries, string address) =>
        entries.Any(entry => entry.Address == address && entry.Status == "recovered");

    private static string[] ExtractRelevantLines(string body)
    {
        var interesting = new[]
        {
            "_opd_FUN_00924dd0",
            "_opd_FUN_008bd708",
            "_opd_FUN_008bdb88",
            "_opd_FUN_008bdff0",
            "_opd_FUN_008bb468",
            "_opd_FUN_008b9ad8",
            "_opd_FUN_00a2fcb0",
            "_opd_FUN_00a2ffd8",
            "FUN_0086de68",
            "FUN_00871708",
            "FUN_0086a588",
            "FUN_0086a948",
            "FUN_0086bfd8",
            "param_1 * 0x50",
            "param_2[6]",
            "param_2[0x10]",
            "param_2[0x11]",
            "piVar4[6]",
            "piVar4[0x10]",
            "piVar4[0x11]",
            "puVar8[6]",
            "puVar8[7]",
            "puVar8[0x10]",
            "puVar8[0x11]",
            "uVar5 + 0x1c",
            "*piVar7 + 0x90",
            "*param_2 + 8",
            "local_20a0 == -2",
            "local_20a0 == -3",
            "0x1770f"
        };

        return body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => interesting.Any(token => line.Contains(token, StringComparison.Ordinal)))
            .Take(80)
            .ToArray();
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.True;

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : 0;
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExportedFunction>();
        var start = -1;
        var name = "";
        var address = "";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]))
            {
                continue;
            }

            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[i]);
            }

            if (!match.Success)
            {
                continue;
            }

            if (start >= 0)
            {
                functions.Add(BuildExportedFunction(lines, start, i - 1, name, address));
            }

            start = i;
            name = match.Groups["name"].Value;
            address = match.Groups["address"].Value;
        }

        if (start >= 0)
        {
            functions.Add(BuildExportedFunction(lines, start, lines.Length - 1, name, address));
        }

        return functions;
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(ExportedFunction function)
    {
        var text = string.Join('\n', function.Lines.Take(100));
        return text.Length <= 3500 ? text : text[..3500];
    }

    private sealed record Tf2Ps3SourceMarkerlessParam2Target(
        string Address,
        string Role,
        Tf2Ps3SourceMarkerlessParam2RequiredEvidence[] RequiredEvidence);

    private sealed record Tf2Ps3SourceMarkerlessParam2RequiredEvidence(
        string Id,
        string Needle,
        string Meaning);

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceMarkerlessParam2BuilderReport(
    string Status,
    string Note,
    Tf2Ps3SourceMarkerlessParam2BuilderInputs Inputs,
    Tf2Ps3SourceMarkerlessParam2BuilderSummary Summary,
    Tf2Ps3SourceMarkerlessParam2Entry[] Entries,
    Tf2Ps3SourceMarkerlessParam2BuilderGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceMarkerlessParam2BuilderInputs(
    string CExportInput,
    string RecvBitreaderCensusReport);

public sealed record Tf2Ps3SourceMarkerlessParam2BuilderSummary(
    string BuilderFunction,
    string ReceiveFillFunction,
    string FragmentReassemblyFunction,
    string DrainDispatchFunction,
    string PayloadObjectSize,
    string PayloadObjectPoolOffset,
    string PayloadBufferFieldOffset,
    string PayloadBitreaderOffset,
    string PayloadLengthFieldOffset,
    string PayloadOwnerLengthFieldOffset,
    bool VisibleDirectRecvPathExhausted,
    bool PayloadObjectBuilderRecovered,
    bool MarkerlessRecvFillRecovered,
    bool FragmentReassemblyRecovered,
    bool DrainDispatchLoopRecovered,
    bool ConcretePayloadObjectBoundaryRecovered,
    bool ServerImplementationUpdated,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceMarkerlessParam2Entry(
    string Address,
    string Name,
    string Role,
    string Status,
    string[] MissingEvidenceIds,
    Tf2Ps3SourceMarkerlessParam2Evidence[] Evidence,
    string[] RelevantLines,
    string Preview);

public sealed record Tf2Ps3SourceMarkerlessParam2Evidence(
    string Id,
    string Needle,
    string Meaning,
    bool Found);

public sealed record Tf2Ps3SourceMarkerlessParam2BuilderGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
