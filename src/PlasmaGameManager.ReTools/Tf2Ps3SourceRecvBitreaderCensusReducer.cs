using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRecvBitreaderCensusReducer
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

    public static async Task<Tf2Ps3SourceRecvBitreaderCensusReport> ReduceAsync(
        string cExportPath,
        string connectedWrapperBoundaryPath,
        string helperReceiveSiblingsPath,
        string markerlessBoundaryPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        using var connectedWrapper = JsonDocument.Parse(await File.ReadAllTextAsync(connectedWrapperBoundaryPath));
        using var helperSiblings = JsonDocument.Parse(await File.ReadAllTextAsync(helperReceiveSiblingsPath));
        using var markerlessBoundary = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessBoundaryPath));

        var entries = functions
            .Select(BuildEntry)
            .Where(static entry => entry.Relevant)
            .OrderBy(static entry => entry.Address, StringComparer.Ordinal)
            .ToArray();

        var connectedRecvCallers = entries
            .Where(static entry => entry.EvidenceTokens.Contains("connected-recv-wrapper-call", StringComparer.Ordinal))
            .ToArray();
        var connectedRecvAndBitreader = connectedRecvCallers
            .Where(static entry => entry.EvidenceTokens.Contains("bitreader-init-helper", StringComparer.Ordinal))
            .ToArray();
        var connectedRecvAndSourceDispatch = connectedRecvCallers
            .Where(static entry => entry.EvidenceTokens.Contains("payload-dispatcher-call", StringComparer.Ordinal))
            .ToArray();
        var recvfromFunctions = entries
            .Where(static entry => entry.EvidenceTokens.Contains("raw-recvfrom-call", StringComparer.Ordinal))
            .ToArray();
        var rawRecvFunctions = entries
            .Where(static entry => entry.EvidenceTokens.Contains("raw-recv-call", StringComparer.Ordinal))
            .ToArray();
        var hardMarkerlessCandidates = entries
            .Where(static entry => entry.HardMarkerlessCandidate)
            .ToArray();

        var wrapperSummary = connectedWrapper.RootElement.GetProperty("Summary");
        var helperSummary = helperSiblings.RootElement.GetProperty("Summary");
        var markerlessSummary = markerlessBoundary.RootElement.GetProperty("Summary");

        var connectedWrapperProven =
            ReadBool(wrapperSummary, "ConnectedSocketAttachedReaderPathProven")
            || ReadBool(wrapperSummary, "AttachedFrameReaderContractProven");
        var helperSiblingBoundaryProven =
            ReadBool(helperSummary, "AttachedType2DispatchProven")
            && ReadBool(helperSummary, "Slot70ConsumesCallerBitreader");
        var strictPcapHardSetStillOpaque =
            ReadInt(markerlessSummary, "HardOpaqueMarkerlessPacketCount") > 0
            && !ReadBool(markerlessSummary, "NativeMarkerlessBoundaryReady");
        var connectedRecvDispatcherUnique =
            connectedRecvAndSourceDispatch.Length == 1
            && connectedRecvAndSourceDispatch[0].Address == "00a5c2e8";
        var markerlessRecvBitreaderCandidateRecovered = hardMarkerlessCandidates.Length > 0;
        var nativeSourceInputReady =
            markerlessRecvBitreaderCandidateRecovered
            && ReadBool(markerlessSummary, "NativeMarkerlessBoundaryReady");

        var gates = new[]
        {
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "connected-recv-callers-censused",
                connectedRecvCallers.Length == 2 ? "proven" : "needs-review",
                cExportPath,
                "The current C export has two direct callers of the connected recv wrapper: association control and attached Source frames."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "only-attached-reader-reaches-source-dispatch",
                connectedRecvDispatcherUnique ? "proven" : "needs-review",
                "00a5c2e8",
                "Only the attached-frame reader combines connected recv, bitreader construction, and 00a58c10 dispatch."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "association-control-read-separated",
                entries.Any(static entry => entry.Address == "008b9468"
                    && entry.EvidenceTokens.Contains("association-five-byte-read", StringComparer.Ordinal)
                    && !entry.EvidenceTokens.Contains("payload-dispatcher-call", StringComparer.Ordinal))
                    ? "proven" : "missing",
                "008b9468",
                "The 5-byte association/control reader is not the native Source message dispatcher."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "recvfrom-drain-has-no-bitreader-dispatch",
                recvfromFunctions.Any(static entry => entry.Address == "008b8d50")
                && recvfromFunctions.All(static entry => !entry.EvidenceTokens.Contains("payload-dispatcher-call", StringComparer.Ordinal))
                    ? "proven-negative" : "needs-review",
                "008b8d50",
                "The unconnected recvfrom path remains a drain/discard loop with no Source bitreader dispatch."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "connected-wrapper-contract-agrees",
                connectedWrapperProven ? "proven" : "missing",
                connectedWrapperBoundaryPath,
                "The connected-wrapper report agrees that object socket +0x90 feeds the attached-frame reader."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "helper-sibling-contract-agrees",
                helperSiblingBoundaryProven ? "proven" : "missing",
                helperReceiveSiblingsPath,
                "The helper-sibling report agrees that 00a5c2e8 is framed input while 00a5d9e0 is a caller-bitreader consumer."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "strict-pcap-hard-markerless-set-remains-opaque",
                strictPcapHardSetStillOpaque ? "proven" : "needs-review",
                markerlessBoundaryPath,
                "The strict PCAP hard markerless set still has no decoded attached-frame, CLC_Move, fragment, LZSS, or bit-sidecar boundary."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "hard-markerless-recv-bitreader-candidate",
                markerlessRecvBitreaderCandidateRecovered ? "proven" : "missing",
                "recv/bitreader census",
                "No direct socket-read function in the C export explains the hard markerless client datagram family."),
            new Tf2Ps3SourceRecvBitreaderCensusGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "Native Source input is still gated until the hard markerless datagram wrapper is recovered and implemented.")
        };

        var report = new Tf2Ps3SourceRecvBitreaderCensusReport(
            "tf2ps3-source-recv-bitreader-census",
            "Censuses TF.elf functions that combine socket reads, bitreader construction, and native Source dispatch to ensure the hard markerless client-input blocker is not hidden in another visible recv path.",
            new Tf2Ps3SourceRecvBitreaderCensusInputs(
                cExportPath,
                connectedWrapperBoundaryPath,
                helperReceiveSiblingsPath,
                markerlessBoundaryPath),
            new Tf2Ps3SourceRecvBitreaderCensusSummary(
                functions.Count,
                entries.Length,
                rawRecvFunctions.Length,
                recvfromFunctions.Length,
                connectedRecvCallers.Length,
                connectedRecvAndBitreader.Length,
                connectedRecvAndSourceDispatch.Length,
                connectedRecvDispatcherUnique,
                hardMarkerlessCandidates.Length,
                connectedWrapperProven,
                helperSiblingBoundaryProven,
                strictPcapHardSetStillOpaque,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            entries,
            gates,
            [
                "The visible recv-to-bitreader path is exhausted in the current C export: 008b9468 handles association/control and 00a5c2e8 handles attached Source frames.",
                "The only connected recv path that reaches 00a58c10 is 00a5c2e8, and that path requires the visible attached-frame grammar already ruled out for the strict hard markerless PCAP set.",
                "008b8d50 remains a recvfrom drain/discard path and is not the command parser.",
                "The remaining markerless input target is not another direct recv caller in this export. It is likely an engine wrapper/transform before visible recv framing, or an indirect path not rendered as a recv-to-dispatch body by Ghidra's C export."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRecvBitreaderCensusEntry BuildEntry(ExportedFunction function)
    {
        var evidence = BuildEvidence(function.Body, function.Address);
        var relevant = evidence.Any(static token => token is
            "raw-recv-call" or
            "raw-recvfrom-call" or
            "connected-recv-wrapper-call" or
            "payload-dispatcher-call");
        var hardMarkerlessCandidate =
            evidence.Contains("connected-recv-wrapper-call", StringComparer.Ordinal)
            && evidence.Contains("bitreader-init-helper", StringComparer.Ordinal)
            && evidence.Contains("payload-dispatcher-call", StringComparer.Ordinal)
            && !evidence.Contains("attached-frame-kind-state", StringComparer.Ordinal)
            && !evidence.Contains("association-five-byte-read", StringComparer.Ordinal);

        return new Tf2Ps3SourceRecvBitreaderCensusEntry(
            function.Address,
            function.Name,
            RoleFor(function.Address, evidence),
            relevant,
            hardMarkerlessCandidate,
            evidence,
            ExtractReadCalls(function.Body),
            ExtractFieldTokens(function.Body),
            ExtractCallSequence(function.Body),
            Preview(function));
    }

    private static string[] BuildEvidence(string body, string address)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "recv(", "raw-recv-call");
        AddIf(body, tokens, "recvfrom(", "raw-recvfrom-call");
        if (address != "008b82c0")
        {
            AddIf(body, tokens, "_opd_FUN_008b82c0", "connected-recv-wrapper-call");
        }
        AddIf(body, tokens, "FUN_0086de68", "bitreader-init-helper");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, tokens, "auStack_b0,5,0", "association-five-byte-read");
        AddIf(body, tokens, "local_280,1,0", "attached-frame-kind-byte-read");
        AddIf(body, tokens, "auStack_234,6,0", "attached-type2-six-byte-header");
        AddIf(body, tokens, "param_1[0x24]", "object-connected-socket-field-0x90");
        AddIf(body, tokens, "param_1[0x10c]", "attached-frame-kind-state");
        AddIf(body, tokens, "param_1[0x10d]", "attached-frame-token-field");
        AddIf(body, tokens, "param_1[0x10e]", "attached-frame-length-field");
        AddIf(body, tokens, "param_1[0x10f]", "attached-frame-progress-field");
        AddIf(body, tokens, "param_1[0x151]", "attached-frame-payload-buffer-0x544");
        AddIf(body, tokens, "auStack_83c,0x800,0x80", "drain-stack-buffer-0x800");
        AddIf(body, tokens, "param_2 + 7", "slot70-param2-dispatch-view");
        AddIf(body, tokens, "param_2[0xb]", "slot70-param2-field-shape");

        if (address == "008b82c0")
        {
            tokens.Add("connected-recv-wrapper-body");
        }

        if (address == "008b8d50")
        {
            tokens.Add("known-unconnected-drain");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractReadCalls(string body)
    {
        return body.Split('\n')
            .Select(static line => line.Trim())
            .Where(static line =>
                line.Contains("recv(", StringComparison.Ordinal)
                || line.Contains("recvfrom(", StringComparison.Ordinal)
                || line.Contains("_opd_FUN_008b82c0", StringComparison.Ordinal))
            .Take(24)
            .ToArray();
    }

    private static string[] ExtractFieldTokens(string body)
    {
        var tokens = new[]
        {
            "param_1[0x24]",
            "param_1[0x10c]",
            "param_1[0x10d]",
            "param_1[0x10e]",
            "param_1[0x10f]",
            "param_1[0x151]",
            "auStack_b0",
            "auStack_234",
            "auStack_83c",
            "local_280",
            "param_2 + 7",
            "param_2[0xb]"
        };

        return tokens
            .Where(token => body.Contains(token, StringComparison.Ordinal))
            .ToArray();
    }

    private static string[] ExtractCallSequence(string body)
    {
        var interesting = new[]
        {
            "recv(",
            "recvfrom(",
            "_opd_FUN_008b82c0",
            "FUN_0086de68",
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a584d0",
            "_opd_FUN_00a5a550",
            "param_1[0x24]",
            "param_1[0x10c]",
            "param_1[0x10e]",
            "param_1[0x151]"
        };

        return body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => interesting.Any(token => line.Contains(token, StringComparison.Ordinal)))
            .Take(42)
            .ToArray();
    }

    private static string RoleFor(string address, string[] evidence) => address switch
    {
        "008b82c0" => "connected-recv-wrapper",
        "008b8d50" => "unconnected-recvfrom-drain",
        "008b9468" => "association-control-record-reader",
        "00a5c2e8" => "attached-source-frame-reader",
        "00a5d9e0" => "slot70-bitreader-consumer",
        _ when evidence.Contains("connected-recv-wrapper-call", StringComparer.Ordinal) => "connected-recv-caller",
        _ when evidence.Contains("raw-recvfrom-call", StringComparer.Ordinal) => "raw-recvfrom-caller",
        _ when evidence.Contains("payload-dispatcher-call", StringComparer.Ordinal) => "source-payload-dispatcher-adjacent",
        _ => "socket-or-bitreader-adjacent"
    };

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
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
        var text = string.Join('\n', function.Lines.Take(90));
        return text.Length <= 3000 ? text : text[..3000];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceRecvBitreaderCensusReport(
    string Status,
    string Note,
    Tf2Ps3SourceRecvBitreaderCensusInputs Inputs,
    Tf2Ps3SourceRecvBitreaderCensusSummary Summary,
    Tf2Ps3SourceRecvBitreaderCensusEntry[] Entries,
    Tf2Ps3SourceRecvBitreaderCensusGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceRecvBitreaderCensusInputs(
    string CExportInput,
    string ConnectedWrapperBoundaryReport,
    string HelperReceiveSiblingsReport,
    string MarkerlessBoundaryHypothesesReport);

public sealed record Tf2Ps3SourceRecvBitreaderCensusSummary(
    int ScannedFunctionCount,
    int RelevantFunctionCount,
    int RawRecvFunctionCount,
    int RecvfromFunctionCount,
    int ConnectedRecvWrapperCallerCount,
    int ConnectedRecvAndBitreaderFunctionCount,
    int ConnectedRecvAndSourceDispatcherFunctionCount,
    bool ConnectedRecvDispatcherUnique,
    int HardMarkerlessRecvBitreaderCandidateCount,
    bool ConnectedWrapperContractProven,
    bool HelperSiblingBoundaryProven,
    bool StrictPcapHardSetStillOpaque,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceRecvBitreaderCensusEntry(
    string Address,
    string Name,
    string Role,
    bool Relevant,
    bool HardMarkerlessCandidate,
    string[] EvidenceTokens,
    string[] ReadCalls,
    string[] FieldTokens,
    string[] CallSequence,
    string Preview);

public sealed record Tf2Ps3SourceRecvBitreaderCensusGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
