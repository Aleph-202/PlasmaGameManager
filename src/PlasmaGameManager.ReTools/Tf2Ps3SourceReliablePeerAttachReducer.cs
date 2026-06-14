using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceReliablePeerAttachReducer
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

    private static readonly string[] TargetAddresses =
    [
        "008b82c0",
        "008b83a8",
        "008b8e70",
        "008b9468",
        "008bfa88",
        "00a584d0",
        "00a58c10",
        "00a5b6c0",
        "00a5c2e8",
        "00a5d9e0"
    ];

    public static async Task<Tf2Ps3SourceReliablePeerAttachReport> ReduceAsync(
        string cExportPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .ToArray();

        var functionMap = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var attachControl = Find(functionMap, "008b9468");
        var connectedOpen = Find(functionMap, "008b8e70");
        var attachSocket = Find(functionMap, "00a5b6c0");
        var attachedFrameReader = Find(functionMap, "00a5c2e8");
        var stateAckWriter = Find(functionMap, "00a584d0");
        var slot70Consumer = Find(functionMap, "00a5d9e0");

        var attachControlRecovered = attachControl is not null
            && HasAll(
                attachControl.EvidenceTokens,
                "reads-five-byte-accepted-peer-control",
                "decodes-first-eight-bit-message-type",
                "message-type-four-is-association",
                "peer-id-callback-plus-c0-match",
                "token-callback-plus-c4-match",
                "requires-empty-attached-socket-field",
                "copies-accepted-peer-address-into-player",
                "sets-associated-byte-plus-42e",
                "stores-accepted-socket-into-player-plus-90",
                "calls-player-post-attach-slot-plus-6c",
                "removes-accepted-peer-record");
        var connectedSocketOpenRecovered = connectedOpen is not null
            && HasAll(
                connectedOpen.EvidenceTokens,
                "slot-connected-socket-field-plus-0c",
                "uses-connect",
                "stores-connected-socket");
        var attachSocketRecovered = attachSocket is not null
            && HasAll(
                attachSocket.EvidenceTokens,
                "calls-connected-socket-open",
                "stores-object-socket-plus-90",
                "allocates-96000-byte-staging-buffer");
        var attachedFrameReaderRecovered = attachedFrameReader is not null
            && HasAll(
                attachedFrameReader.EvidenceTokens,
                "requires-attached-socket-plus-90",
                "reads-one-byte-frame-kind",
                "frame-kind-two-reads-six-byte-header",
                "stages-payload-buffer-plus-544",
                "dispatches-complete-type2-payload",
                "writes-attached-state-ack");
        var stateAckWriterRecovered = stateAckWriter is not null
            && HasAll(
                stateAckWriter.EvidenceTokens,
                "writes-control-value-four",
                "writes-token-id",
                "sends-on-attached-socket");
        var slot70ConsumesCallerBitreader = slot70Consumer is not null
            && HasAll(
                slot70Consumer.EvidenceTokens,
                "slot70-dispatches-param2-plus-seven",
                "slot70-consumes-caller-bitreader")
            && !slot70Consumer.EvidenceTokens.Contains("requires-attached-socket-plus-90", StringComparer.Ordinal);
        var attachChainRecovered = attachControlRecovered
            && attachedFrameReaderRecovered
            && stateAckWriterRecovered;
        var nativeSourceInputReady = false;

        var gates = new[]
        {
            Gate(
                "accepted-peer-type4-control-recovered",
                attachControlRecovered ? "proven" : "missing",
                "TF.elf 008b9468",
                "Pending accepted sockets send a 5-byte control record: 8-bit type 4, then a 32-bit association token matched against the Source player object."),
            Gate(
                "connected-socket-open-path-recovered",
                connectedSocketOpenRecovered ? "proven" : "missing",
                "TF.elf 008b8e70",
                "The client-side connected socket helper stores a socket in the Source slot +0x0c field after connect()."),
            Gate(
                "source-player-socket-attach-recovered",
                attachSocketRecovered ? "proven" : "missing",
                "TF.elf 00a5b6c0",
                "The Source player object stores the connected/attached socket at object +0x90 and allocates the type-2 staging buffer."),
            Gate(
                "attached-frame-reader-recovered",
                attachedFrameReaderRecovered ? "proven" : "missing",
                "TF.elf 00a5c2e8",
                "After association, frame kind 2 reads a six-byte length/token header, stages payload bytes, dispatches 00a58c10, then writes a state ack."),
            Gate(
                "attached-state-ack-writer-recovered",
                stateAckWriterRecovered ? "proven" : "missing",
                "TF.elf 00a584d0",
                "The ack writer emits control value 4 and a 32-bit token on the attached socket."),
            Gate(
                "slot70-still-downstream-consumer",
                slot70ConsumesCallerBitreader ? "proven-negative" : "needs-review",
                "TF.elf 00a5d9e0",
                "Slot +0x70 consumes a caller-built bitreader; it is not the connected socket reader and does not explain the hard markerless datagram wrapper."),
            Gate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "remaining TF.elf ingress target",
                "This attach contract proves how the connected stream starts, but the hard markerless client upload wrapper still needs its caller-side transform/dataflow edge.")
        };

        var report = new Tf2Ps3SourceReliablePeerAttachReport(
            "tf2ps3-source-reliable-peer-attach",
            "Narrows the TF.elf native Source receive side around accepted-peer attach, attached-frame reads, and state acks. This is strict evidence, not packet replay.",
            cExportPath,
            new Tf2Ps3SourceReliablePeerAttachSummary(
                functions.Length,
                attachControlRecovered,
                connectedSocketOpenRecovered,
                attachSocketRecovered,
                attachedFrameReaderRecovered,
                stateAckWriterRecovered,
                slot70ConsumesCallerBitreader,
                attachChainRecovered,
                nativeSourceInputReady,
                "008b9468",
                4,
                5,
                "8-bit message type, then 32-bit association token",
                "object +0x90 / piVar6[0x24]",
                "object +0x42e",
                "00a5c2e8",
                "00a584d0",
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            functions,
            gates,
            [
                "008b9468 is the accepted-peer attach loop: it reads exactly five bytes from pending accepted sockets, decodes type 4 plus token, matches peer id through vtable +0xc0 and token through +0xc4, then grafts socket/address/state onto the Source player object.",
                "00a5c2e8 is the attached-frame reader after that graft. Its type 2 frame is the first concrete native Source payload stream: one-byte kind, six-byte length/token header, payload staged at object +0x544, then 00a58c10 dispatch and 00a584d0 ack.",
                "00a5d9e0 remains downstream: it consumes a caller-built bitreader and dispatches param_2 + 7. It does not read the attached socket or build the hard markerless upload wrapper.",
                "The remaining map-load blocker is upstream of the proven attach/frame reader contract: the transform/dataflow edge that turns the observed hard markerless client datagrams into a native attached-frame payload or slot +0x70 bitreader."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceReliablePeerAttachFunction? Find(
        IReadOnlyDictionary<string, Tf2Ps3SourceReliablePeerAttachFunction> functions,
        string address) =>
        functions.TryGetValue(address, out var function) ? function : null;

    private static Tf2Ps3SourceReliablePeerAttachGate Gate(
        string id,
        string status,
        string evidence,
        string meaning) =>
        new(id, status, evidence, meaning);

    private static Tf2Ps3SourceReliablePeerAttachFunction BuildFunction(ExportedFunction function)
    {
        return new Tf2Ps3SourceReliablePeerAttachFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            RoleFor(function.Address),
            ExtractCalls(function.Body),
            BuildEvidence(function.Body, function.Address),
            Preview(function.Lines));
    }

    private static string RoleFor(string address)
    {
        return address switch
        {
            "008b82c0" => "connected-socket-recv-wrapper",
            "008b83a8" => "socket-close-slot-clear",
            "008b8e70" => "connected-socket-open",
            "008b9468" => "accepted-peer-type4-association-control",
            "008bfa88" => "accepted-peer-record-remove-one",
            "00a584d0" => "attached-state-ack-writer",
            "00a58c10" => "attached-payload-message-dispatcher",
            "00a5b6c0" => "source-player-connected-socket-attach",
            "00a5c2e8" => "attached-frame-reader",
            "00a5d9e0" => "slot70-caller-bitreader-consumer",
            _ => "source-receive-helper"
        };
    }

    private static string[] BuildEvidence(string body, string address)
    {
        var evidence = new List<string>();

        AddIf(body, evidence, "_opd_FUN_008b82c0(*piVar20,auStack_b0,5,0)", "reads-five-byte-accepted-peer-control");
        AddIf(body, evidence, "FUN_0086de68((int)&local_a8,auStack_b0,5,0,-1)", "decodes-first-eight-bit-message-type");
        AddIf(body, evidence, "uVar12 == 4", "message-type-four-is-association");
        AddIf(body, evidence, "*piVar6 + 0xc0", "peer-id-callback-plus-c0");
        AddIf(body, evidence, "piVar20[1] == iVar11", "peer-id-callback-plus-c0-match");
        AddIf(body, evidence, "*piVar6 + 0xc4", "token-callback-plus-c4");
        AddIf(body, evidence, "uVar13 == uVar12", "token-callback-plus-c4-match");
        AddIf(body, evidence, "piVar6[0x24] == 0", "requires-empty-attached-socket-field");
        AddIf(body, evidence, "FUN_0086fb58(piVar18,piVar6 + 0x26,1)", "copies-accepted-peer-address-into-player");
        AddIf(body, evidence, "*(undefined1 *)((int)piVar6 + 0x42e) = 1", "sets-associated-byte-plus-42e");
        AddIf(body, evidence, "piVar6[0x24] = iVar19", "stores-accepted-socket-into-player-plus-90");
        AddIf(body, evidence, "*piVar6 + 0x6c", "calls-player-post-attach-slot-plus-6c");
        AddIf(body, evidence, "_opd_FUN_008bfa88((uint *)(puVar7 + 0x1b0)", "removes-accepted-peer-record");
        AddIf(body, evidence, "_opd_FUN_008b83a8(*piVar20,-1)", "closes-unmatched-accepted-peer-socket");
        AddIf(body, evidence, "PTR_DAT_0197336c + 0x1b0", "accepted-peer-table-base");
        AddIf(body, evidence, "PTR_DAT_0197336c + 0x1bc", "accepted-peer-count");
        AddIf(body, evidence, "PTR_DAT_0197336c + 0x148", "source-player-object-table");
        AddIf(body, evidence, "PTR_DAT_0197336c + 0x154", "source-player-object-count");
        AddIf(body, evidence, "iVar16 = iVar16 + 0x18", "accepted-peer-record-stride-0x18");

        AddIf(body, evidence, "connect(iVar4,asStack_4c,0x10)", "uses-connect");
        AddIf(body, evidence, "*(int *)(iVar3 + 0xc)", "slot-connected-socket-field-plus-0c");
        AddIf(body, evidence, "*(int *)(iVar3 + 0xc) = iVar4", "stores-connected-socket");

        AddIf(body, evidence, "_opd_FUN_008b8e70(param_1[0x23],param_1 + 0x26)", "calls-connected-socket-open");
        AddIf(body, evidence, "param_1[0x24] = iVar2", "stores-object-socket-plus-90");
        AddIf(body, evidence, "param_1[0x152] < 96000", "allocates-96000-byte-staging-buffer");

        AddIf(body, evidence, "param_1[0x24] == 0", "requires-attached-socket-plus-90");
        AddIf(body, evidence, "_opd_FUN_008b82c0(param_1[0x24],local_280,1,0)", "reads-one-byte-frame-kind");
        AddIf(body, evidence, "param_1[0x10c]", "frame-kind-cache-plus-430");
        AddIf(body, evidence, "iVar2 == 2", "frame-kind-two");
        AddIf(body, evidence, "_opd_FUN_008b82c0(param_1[0x24],auStack_234,6,0)", "frame-kind-two-reads-six-byte-header");
        AddIf(body, evidence, "param_1[0x10e] = uVar3", "stores-type2-payload-length-plus-438");
        AddIf(body, evidence, "param_1[0x10d] = uVar3", "stores-type2-token-plus-434");
        AddIf(body, evidence, "param_1[0x151]", "stages-payload-buffer-plus-544");
        AddIf(body, evidence, "_opd_FUN_00a58c10(param_1,(int)&local_258)", "dispatches-complete-type2-payload");
        AddIf(body, evidence, "_opd_FUN_00a584d0((int)param_1,uVar5)", "writes-attached-state-ack");
        AddIf(body, evidence, "_opd_FUN_00a584d0((int)param_1,param_1[0x780])", "writes-type1-state-ack");

        AddIf(body, evidence, "FUN_00870c28((int *)local_60,4)", "writes-control-value-four");
        AddIf(body, evidence, "FUN_0086caf8((int *)local_60,param_2)", "writes-token-id");
        AddIf(body, evidence, "_opd_FUN_008b8328", "sends-on-attached-socket");

        AddIf(body, evidence, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, evidence, "param_2[0x11] + 0x1c", "slot70-dispatches-param2-plus-seven");
        AddIf(body, evidence, "_opd_FUN_00a579d8((int)param_1,1,param_2[0x11] + 0x1c)", "slot70-consumes-caller-bitreader");

        if (address == "00a5d9e0" && !body.Contains("param_1[0x24]", StringComparison.Ordinal))
        {
            evidence.Add("does-not-read-attached-socket-plus-90");
        }

        return evidence.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AddIf(string body, List<string> evidence, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            evidence.Add(token);
        }
    }

    private static bool HasAll(IEnumerable<string> values, params string[] required)
    {
        var set = values.ToHashSet(StringComparer.Ordinal);
        return required.All(set.Contains);
    }

    private static string[] ExtractCalls(string body)
    {
        return Regex.Matches(body, @"\b(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto|connect|socket")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
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

    private static ExportedFunction BuildExportedFunction(
        string[] lines,
        int start,
        int end,
        string name,
        string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(80));
        return text.Length <= 2400 ? text : text[..2400];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceReliablePeerAttachReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourceReliablePeerAttachSummary Summary,
    Tf2Ps3SourceReliablePeerAttachFunction[] Functions,
    Tf2Ps3SourceReliablePeerAttachGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceReliablePeerAttachSummary(
    int TargetFunctionCount,
    bool AttachControlRecovered,
    bool ConnectedSocketOpenRecovered,
    bool AttachSocketRecovered,
    bool AttachedFrameReaderRecovered,
    bool StateAckWriterRecovered,
    bool Slot70ConsumesCallerBitreader,
    bool AttachChainRecovered,
    bool NativeSourceInputReady,
    string AttachControlFunction,
    int AttachControlMessageType,
    int AttachControlByteCount,
    string AttachControlWireShape,
    string AttachedSocketField,
    string AssociatedFlagField,
    string AttachedFrameReaderFunction,
    string AttachedStateAckWriterFunction,
    int OpenGateCount);

public sealed record Tf2Ps3SourceReliablePeerAttachFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceReliablePeerAttachGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
