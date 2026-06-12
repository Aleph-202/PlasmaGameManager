using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceConnectedWrapperBoundaryReducer
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

    public static async Task<Tf2Ps3SourceConnectedWrapperBoundaryReport> ReduceAsync(
        string cExportPath,
        string markerlessBoundaryHypothesesPath,
        string udpIngressCorrectionPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.OrdinalIgnoreCase);
        using var markerlessBoundary = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessBoundaryHypothesesPath));
        using var udpIngress = JsonDocument.Parse(await File.ReadAllTextAsync(udpIngressCorrectionPath));

        var attached = functions.GetValueOrDefault("00a5c2e8");
        var bitreader = functions.GetValueOrDefault("00a5d9e0");
        var socketAttach = functions.GetValueOrDefault("00a5b6c0");
        var connect = functions.GetValueOrDefault("008b8e70");
        var recv = functions.GetValueOrDefault("008b82c0");
        var markerlessSummary = markerlessBoundary.RootElement.GetProperty("Summary");
        var udpSummary = udpIngress.RootElement.GetProperty("Summary");

        var attachedTokens = BuildAttachedReaderTokens(attached);
        var bitreaderTokens = BuildBitreaderTokens(bitreader);
        var socketAttachTokens = BuildSocketAttachTokens(socketAttach);
        var connectedSocketPathProven = ReadBool(udpSummary, "ConnectedSocketOpenStorePathProven")
            && ReadBool(udpSummary, "ConnectedSocketAttachedReaderPathProven")
            && socketAttachTokens.Contains("stores-connected-socket-object-0x90", StringComparer.Ordinal)
            && connect?.Body.Contains("connect(", StringComparison.Ordinal) == true
            && recv?.Body.Contains("recv(", StringComparison.Ordinal) == true;
        var attachedFrameContractProven = attachedTokens.Contains("reads-frame-kind-byte-object-0x430", StringComparer.Ordinal)
            && attachedTokens.Contains("type2-reads-six-byte-header", StringComparer.Ordinal)
            && attachedTokens.Contains("type2-dispatches-staged-bitstream", StringComparer.Ordinal)
            && attachedTokens.Contains("payload-length-cap-96000", StringComparer.Ordinal);
        var bitreaderSiblingRecovered = bitreaderTokens.Contains("bitreader-state-param2-0xb-0xe", StringComparer.Ordinal)
            && bitreaderTokens.Contains("dispatches-param2-plus-7", StringComparer.Ordinal)
            && bitreaderTokens.Contains("subpayload-helper-chain", StringComparer.Ordinal);
        var hardBodiesAtVisibleBoundary = ReadInt(markerlessSummary, "HardRawAttachedType2LittleEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardRawAttachedType2BigEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardEmbeddedAttachedType2LittleEndianExactHitCount") > 0
            || ReadInt(markerlessSummary, "HardEmbeddedAttachedType2BigEndianExactHitCount") > 0;

        var frameKinds = new[]
        {
            new Tf2Ps3ConnectedFrameKind(
                1,
                "attached-ready-state",
                "Sets object +0x42e / byte at object +0x42e, recurses through vtable +0x6c, then calls 00a584d0 with object +0x1e00 / param_1[0x780].",
                "no extra body bytes beyond the frame kind",
                attached?.Body.Contains("iVar2 == 1", StringComparison.Ordinal) == true ? "recovered" : "missing"),
            new Tf2Ps3ConnectedFrameKind(
                2,
                "native-source-bitstream-payload",
                "Reads a 6-byte header from object +0x90 with 008b82c0, extracts a 16-bit payload length into object +0x438 / param_1[0x10e], extracts a 32-bit token into object +0x434 / param_1[0x10d], stages payload bytes at object +0x544 / param_1[0x151], then dispatches a bitreader through 00a58c10.",
                "1-byte kind + 6-byte header + declared payload bytes, length capped at 96000",
                attachedFrameContractProven ? "recovered" : "missing"),
            new Tf2Ps3ConnectedFrameKind(
                3,
                "gated-control-state",
                "Requires object +0x440 / byte at param_1 + 0x440 to be nonzero; otherwise the reader fails the connection.",
                "1-byte kind plus state gate, no recovered payload body in this branch",
                attached?.Body.Contains("iVar2 == 3", StringComparison.Ordinal) == true ? "recovered" : "missing"),
            new Tf2Ps3ConnectedFrameKind(
                4,
                "token-sync-or-close-control",
                "Reads a 4-byte token into object +0x434 / param_1[0x10d], compares it with the attached owner token, calls 00a5a550 on match or resets the peer address on mismatch, then recurses through vtable +0x6c.",
                "1-byte kind + 4-byte token",
                attached?.Body.Contains("iVar2 == 4", StringComparison.Ordinal) == true ? "recovered" : "missing")
        };

        var functionsReport = new[]
        {
            new Tf2Ps3ConnectedWrapperFunction("008b82c0", "connected-socket-recv-wrapper", recv is not null ? "recovered" : "missing", BuildRecvTokens(recv), Preview(recv)),
            new Tf2Ps3ConnectedWrapperFunction("008b8e70", "connected-udp-socket-open-connect", connect is not null ? "recovered" : "missing", BuildConnectTokens(connect), Preview(connect)),
            new Tf2Ps3ConnectedWrapperFunction("00a5b6c0", "source-object-connected-socket-open-store", socketAttach is not null ? "recovered" : "missing", socketAttachTokens, Preview(socketAttach)),
            new Tf2Ps3ConnectedWrapperFunction("00a5c2e8", "stateful-attached-frame-reader", attachedFrameContractProven ? "recovered" : "needs-review", attachedTokens, Preview(attached)),
            new Tf2Ps3ConnectedWrapperFunction("00a5d9e0", "sibling-bitreader-wrapper-candidate", bitreaderSiblingRecovered ? "candidate-recovered" : "missing", bitreaderTokens, Preview(bitreader))
        };

        var gates = new[]
        {
            new Tf2Ps3ConnectedWrapperGate(
                "connected-socket-object-path",
                connectedSocketPathProven ? "proven" : "missing",
                "008b8e70 -> 00a5b6c0 -> object +0x90 -> 008b82c0",
                "The connected UDP socket is opened, connected, stored on the Source object, and read through the connected recv wrapper."),
            new Tf2Ps3ConnectedWrapperGate(
                "visible-attached-frame-reader-contract",
                attachedFrameContractProven ? "proven" : "missing",
                "00a5c2e8",
                "The stateful attached-frame reader has four recovered frame kinds and dispatches type-2 payloads into 00a58c10."),
            new Tf2Ps3ConnectedWrapperGate(
                "sibling-bitreader-wrapper-candidate",
                bitreaderSiblingRecovered ? "candidate-proven" : "missing",
                "00a5d9e0",
                "A sibling helper-slice path consumes bitreader state and can dispatch remaining bits through 00a58c10, but its PCAP-visible ingress is not proven."),
            new Tf2Ps3ConnectedWrapperGate(
                "hard-pcap-bodies-at-visible-frame-boundary",
                hardBodiesAtVisibleBoundary ? "candidate" : "ruled-out",
                "artifacts/pcap-markerless-boundary-hypotheses.json",
                "The 11,874 hard opaque markerless bodies do not align with the visible 00a5c2e8 frame grammar at offset 0 or offsets 1..32."),
            new Tf2Ps3ConnectedWrapperGate(
                "hard-markerless-body-to-connected-wrapper-boundary",
                "missing",
                "PCAP hard body -> connected object +0x90 reader / 00a5d9e0 bitreader",
                "Still missing the native transform or call edge that turns hard markerless client bodies into the connected attached-frame or sibling bitreader stream."),
            new Tf2Ps3ConnectedWrapperGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "The replacement server cannot claim native client input completion until the markerless body boundary is reconstructed and implemented.")
        };

        var report = new Tf2Ps3SourceConnectedWrapperBoundaryReport(
            "tf2ps3-source-connected-wrapper-boundary",
            "Names the connected Source receive frame grammar recovered from TF.elf and cross-checks it against the hard markerless PCAP set. This narrows the remaining native input gap without packet replay.",
            new Tf2Ps3ConnectedWrapperInputs(cExportPath, markerlessBoundaryHypothesesPath, udpIngressCorrectionPath),
            new Tf2Ps3ConnectedWrapperSummary(
                ReadInt(markerlessSummary, "HardOpaqueMarkerlessPacketCount"),
                connectedSocketPathProven,
                attachedFrameContractProven,
                frameKinds.Count(static kind => kind.Status == "recovered"),
                "object +0x90 / param_1[0x24]",
                "object +0x430 / param_1[0x10c]",
                "object +0x434 / param_1[0x10d]",
                "object +0x438 / param_1[0x10e]",
                "object +0x43c / param_1[0x10f]",
                "object +0x544 / param_1[0x151]",
                6,
                96000,
                bitreaderSiblingRecovered,
                false,
                false,
                gates.Count(static gate => gate.Status is "missing" or "candidate" or "candidate-proven")),
            functionsReport,
            frameKinds,
            gates,
            [
                "00a5c2e8 is now a named stateful connected-frame reader, not a generic opaque receive loop.",
                "The type-2 connected frame grammar is explicit: kind byte, 6-byte length/token header, capped staged payload, then 00a58c10 bitstream dispatch.",
                "00a5d9e0 remains the best recovered sibling bitreader wrapper candidate, but this report still does not prove how the 11,874 hard markerless PCAP bodies enter it.",
                "Implementation consequence: native server input must keep a markerless-wrapper gate until the hard body -> connected wrapper boundary is found."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static string[] BuildRecvTokens(ExportedFunction? function) => Tokens(function,
        ("recv(", "recv-call"),
        ("0x23", "transient-einprogress-or-wouldblock"),
        ("0x39", "transient-no-data"),
        ("sVar2 = 0", "maps-transient-errors-to-zero-read"));

    private static string[] BuildConnectTokens(ExportedFunction? function) => Tokens(function,
        ("_opd_FUN_008b8668", "opens-bound-udp-socket"),
        ("connect(", "connect-call"),
        ("+ 0xc", "slot-0x0c-connected-socket-field"),
        ("_opd_FUN_008b83a8", "closes-existing-connected-socket"));

    private static string[] BuildSocketAttachTokens(ExportedFunction? function) => Tokens(function,
        ("_opd_FUN_008b8e70", "connected-socket-open-call"),
        ("param_1[0x24] = iVar2", "stores-connected-socket-object-0x90"),
        ("param_1[0x151]", "staging-buffer-field-object-0x544"),
        ("96000", "payload-buffer-size-96000"),
        ("FUN_0086ba38", "buffer-allocator-or-resizer"));

    private static string[] BuildAttachedReaderTokens(ExportedFunction? function) => Tokens(function,
        ("param_1[0x24]", "attached-socket-field-object-0x90"),
        ("param_1[0x10c]", "frame-kind-field-object-0x430"),
        ("_opd_FUN_008b82c0(param_1[0x24],local_280,1,0)", "reads-frame-kind-byte-object-0x430"),
        ("iVar2 == 1", "frame-kind-1-branch"),
        ("iVar2 == 2", "frame-kind-2-branch"),
        ("iVar2 == 3", "frame-kind-3-branch"),
        ("iVar2 == 4", "frame-kind-4-branch"),
        ("_opd_FUN_008b82c0(param_1[0x24],auStack_234,6,0)", "type2-reads-six-byte-header"),
        ("param_1[0x10e] = uVar3", "type2-length-field-object-0x438"),
        ("param_1[0x10d] = uVar3", "type2-token-field-object-0x434"),
        ("96000 < param_1[0x10e]", "payload-length-cap-96000"),
        ("param_1[0x10f]", "payload-progress-field-object-0x43c"),
        ("param_1[0x151]", "payload-staging-buffer-object-0x544"),
        ("FUN_0086de68((int)&local_258,param_1[0x151],iVar2,0,-1)", "bitreader-init-over-staged-payload"),
        ("_opd_FUN_00a58c10(param_1,(int)&local_258)", "type2-dispatches-staged-bitstream"),
        ("_opd_FUN_008b82c0(param_1[0x24],auStack_234,4,0)", "type4-reads-four-byte-token"),
        ("_opd_FUN_00a5a550", "type4-token-match-control"),
        ("0x42e", "frame-kind-1-associated-state-byte"));

    private static string[] BuildBitreaderTokens(ExportedFunction? function) => Tokens(function,
        ("FUN_0086d848(param_1 + 0x26)", "local-peer-address-validity-check"),
        ("FUN_0086fb58(param_2,param_1 + 0x26,0)", "payload-peer-address-compare"),
        ("param_2[0x11] + 0x1c", "payload-offset-to-owner-callback"),
        ("_opd_FUN_00a579d8", "pre-dispatch-owner-callback"),
        ("_opd_FUN_00a5aa00", "optional-subpayload-selector"),
        ("param_2[0xb]", "bitreader-state-param2-0xb-0xe"),
        ("param_2[0xc]", "bitreader-state-param2-0xb-0xe"),
        ("param_2[0xd]", "bitreader-state-param2-0xb-0xe"),
        ("param_2[0xe]", "bitreader-state-param2-0xb-0xe"),
        ("_opd_FUN_00a594e8", "subpayload-helper-chain"),
        ("_opd_FUN_00a5d720", "subpayload-helper-chain"),
        ("_opd_FUN_00a58c10(param_1,(int)(param_2 + 7))", "dispatches-param2-plus-7"),
        ("param_2[0xf]", "bitreader-start-pointer-field"),
        ("param_2[9]", "bit-count-limit-field"));

    private static string[] Tokens(ExportedFunction? function, params (string Needle, string Token)[] probes)
    {
        if (function is null)
        {
            return [];
        }

        return probes
            .Where(probe => function.Body.Contains(probe.Needle, StringComparison.Ordinal))
            .Select(static probe => probe.Token)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Preview(ExportedFunction? function)
    {
        if (function is null)
        {
            return "";
        }

        var text = string.Join('\n', function.Lines.Take(85));
        return text.Length <= 2600 ? text : text[..2600];
    }

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

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

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceConnectedWrapperBoundaryReport(
    string Status,
    string Note,
    Tf2Ps3ConnectedWrapperInputs Inputs,
    Tf2Ps3ConnectedWrapperSummary Summary,
    Tf2Ps3ConnectedWrapperFunction[] Functions,
    Tf2Ps3ConnectedFrameKind[] FrameKinds,
    Tf2Ps3ConnectedWrapperGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3ConnectedWrapperInputs(
    string CExportInput,
    string MarkerlessBoundaryHypothesesReport,
    string UdpIngressCorrectionReport);

public sealed record Tf2Ps3ConnectedWrapperSummary(
    int HardOpaqueMarkerlessPacketCount,
    bool ConnectedSocketObjectPathProven,
    bool AttachedFrameReaderContractProven,
    int RecoveredAttachedFrameKindCount,
    string ConnectedSocketField,
    string FrameKindField,
    string FrameTokenField,
    string FramePayloadLengthField,
    string FramePayloadProgressField,
    string FramePayloadBufferField,
    int Type2HeaderBytes,
    int MaxAttachedPayloadBytes,
    bool SiblingBitreaderWrapperRecovered,
    bool DirectHardOpaqueBoundaryProven,
    bool NativeMarkerlessBoundaryReady,
    int OpenGateCount);

public sealed record Tf2Ps3ConnectedWrapperFunction(
    string Address,
    string Role,
    string Status,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3ConnectedFrameKind(
    int Kind,
    string Name,
    string Semantics,
    string WireShape,
    string Status);

public sealed record Tf2Ps3ConnectedWrapperGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
