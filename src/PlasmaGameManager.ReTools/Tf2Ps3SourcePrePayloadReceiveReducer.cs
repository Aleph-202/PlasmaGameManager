using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePrePayloadReceiveReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008bb058",
        "008bdb88",
        "008bdff0",
        "008be1e8",
        "00924ae8",
        "00924dd0",
        "009252e0",
        "00925858",
        "009265e0",
        "00926648"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourcePrePayloadReceiveReport> ReduceAsync(
        string cExportPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var functionByAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var queueInsert = functionByAddress.GetValueOrDefault("009252e0");
        var queuePop = functionByAddress.GetValueOrDefault("00924dd0");
        var routeSend = functionByAddress.GetValueOrDefault("00925858");
        var sourceSend = functionByAddress.GetValueOrDefault("008bb058");
        var payloadFill = functionByAddress.GetValueOrDefault("008bdb88");
        var payloadObject = functionByAddress.GetValueOrDefault("008bdff0");
        var payloadDrain = functionByAddress.GetValueOrDefault("008be1e8");

        var queueInsertionCopiesPayload =
            queueInsert?.EvidenceTokens.Contains("allocates-copy-buffer", StringComparer.Ordinal) == true
            && queueInsert.EvidenceTokens.Contains("copies-param3-payload-into-copy-buffer", StringComparer.Ordinal)
            && queueInsert.EvidenceTokens.Contains("appends-copy-buffer-to-peer-queue", StringComparer.Ordinal);
        var queueDrainCopiesQueuedPayload =
            queuePop?.EvidenceTokens.Contains("copies-queued-buffer-to-param2", StringComparer.Ordinal) == true
            && queuePop.EvidenceTokens.Contains("removes-drained-queue-entry", StringComparer.Ordinal);
        var localRouteQueuesPayload =
            routeSend?.EvidenceTokens.Contains("local-port-routes-to-queue-plus-0x28", StringComparer.Ordinal) == true
            && routeSend.EvidenceTokens.Contains("alternate-local-port-routes-to-queue-plus-0x0c", StringComparer.Ordinal)
            && routeSend.Calls.Contains("_opd_FUN_009252e0", StringComparer.Ordinal);
        var externalRouteUsesSocketInterface =
            routeSend?.EvidenceTokens.Contains("remote-address-uses-virtual-send", StringComparer.Ordinal) == true;
        var payloadObjectFilledFromQueue =
            payloadFill?.Calls.Contains("_opd_FUN_00924dd0", StringComparer.Ordinal) == true
            && payloadFill.EvidenceTokens.Contains("payload-length-word-0x10", StringComparer.Ordinal)
            && payloadFill.EvidenceTokens.Contains("payload-length-word-0x11", StringComparer.Ordinal);
        var specialWrappersAfterQueue =
            payloadFill?.EvidenceTokens.Contains("special-wrapper-minus-2", StringComparer.Ordinal) == true
            && payloadFill.EvidenceTokens.Contains("special-wrapper-minus-3", StringComparer.Ordinal);
        var drainDispatchesPayloadObjects =
            payloadDrain?.EvidenceTokens.Contains("associated-object-token-dispatch", StringComparer.Ordinal) == true
            && payloadDrain.EvidenceTokens.Contains("owner-slot-dispatch", StringComparer.Ordinal);

        var gates = new[]
        {
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "local-source-send-reaches-peer-queue",
                sourceSend?.Calls.Contains("_opd_FUN_00925858", StringComparer.Ordinal) == true
                && localRouteQueuesPayload
                    ? "proven"
                    : "missing",
                "008bb058 -> 00925858 -> 009252e0",
                "Local/loopback Source sends are routed into the reliable peer queue rather than directly transformed."),
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "peer-queue-stores-raw-copied-bytes",
                queueInsertionCopiesPayload ? "proven" : "missing",
                "009252e0",
                "The queue insertion helper allocates a byte buffer, copies param_3/param_4 bytes into it, then appends the copy to the per-peer queue."),
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "payload-object-fill-drains-peer-queue",
                queueDrainCopiesQueuedPayload && payloadObjectFilledFromQueue ? "proven" : "missing",
                "00924dd0 -> 008bdb88",
                "The payload object fill path drains the queued buffer into the packet object and records the resulting byte length."),
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "pre-payload-transform-or-decrypt-step",
                queueInsertionCopiesPayload && queueDrainCopiesQueuedPayload ? "ruled-out-visible-c-export" : "needs-review",
                "009252e0 / 00924dd0",
                "The visible C export shows copy/queue/drain operations, not a crypto/decompression transform before the payload-object first word."),
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "payload-object-special-wrappers-after-queue",
                specialWrappersAfterQueue ? "proven" : "missing",
                "008bdb88",
                "Fragment/repack special cases are processed after the queue drain, so they are payload-object grammar, not socket ingress transforms."),
            new Tf2Ps3SourcePrePayloadReceiveGate(
                "payload-object-drain-dispatches-token-and-owner-paths",
                drainDispatchesPayloadObjects ? "proven" : "missing",
                "008be1e8",
                "The drained payload object is dispatched either by associated object token or by the -1 owner control path.")
        };

        var report = new Tf2Ps3SourcePrePayloadReceiveReport(
            "tf2ps3-source-pre-payload-receive-boundary",
            "Reduces the TF.elf path immediately before payload-object dispatch. This report closes the pre-queue decrypt/strip hypothesis and redirects the remaining work to the payload-object/bitstream grammar.",
            cExportPath,
            new Tf2Ps3SourcePrePayloadReceiveSummary(
                TargetAddresses.Length,
                functions.Length,
                queueInsertionCopiesPayload,
                queueDrainCopiesQueuedPayload,
                localRouteQueuesPayload,
                externalRouteUsesSocketInterface,
                payloadObjectFilledFromQueue,
                specialWrappersAfterQueue,
                drainDispatchesPayloadObjects,
                false,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            functions,
            [
                new("source-send-wrapper", "008bb058", "Builds address structs and calls 00925858 for the native Source/gameplay send route."),
                new("route-send", "00925858", "Sends remotely through the socket interface, but queues local/loopback peer payloads into +0x28 or +0x0c channel lists."),
                new("queue-insert", "009252e0", "Allocates a copy of the byte payload and appends it to the per-peer queue keyed by address/port."),
                new("queue-drain", "00924dd0", "Pops a queued record, copies its byte buffer to the caller's packet buffer, frees the queued copy, and returns the copied byte count."),
                new("payload-object-fill", "008bdb88", "Fills the 0x50-byte payload object from 00924dd0, records length fields, then handles -2/-3 special wrappers."),
                new("payload-object-index", "008bdff0", "Allocates/indexes payload objects at global +0x6c0, stride 0x50, and creates the in-place bitreader at +0x1c."),
                new("payload-object-drain", "008be1e8", "Drains filled payload objects and dispatches by associated-object token or owner slot.")
            ],
            gates,
            [
                "The byte buffer entering the payload object is the same queued copy produced by 009252e0; the visible C export does not show a pre-payload decrypt or XOR/RC4 style transform.",
                "The -2 and -3 branches in 008bdb88 are post-drain payload-object wrappers. They can reassemble or repack the already queued body, but they do not explain the dominant markerless family by stripping four bytes.",
                "The remaining native implementation target is the downstream payload-object dispatch grammar: associated object token path, owner-slot path, and the bitreader/helper handlers reached from 008be1e8.",
                "Server work should continue by implementing named payload-object semantics and proving handler-specific reads, not by forwarding PC srcds traffic or replaying captured markerless blobs."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourcePrePayloadFunction BuildFunction(ExportedFunction function) =>
        new(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyRole(function.Address),
            ExtractCalls(function.Body),
            BuildEvidenceTokens(function),
            Preview(function.Lines));

    private static string ClassifyRole(string address) =>
        address switch
        {
            "008bb058" => "source-send-wrapper-to-peer-router",
            "008bdb88" => "payload-object-fill-from-peer-queue",
            "008bdff0" => "payload-object-slot-allocator",
            "008be1e8" => "payload-object-drain-dispatcher",
            "00924ae8" => "peer-queue-container-init",
            "00924dd0" => "peer-queue-drain-copy",
            "009252e0" => "peer-queue-insert-copy",
            "00925858" => "source-peer-route-send-or-local-queue",
            "009265e0" => "queue-vector-append-copy-record",
            "00926648" => "byte-buffer-vector-init",
            _ => "source-pre-payload-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "_opd_FUN_00925858", "calls-source-peer-router");
        AddIf(body, tokens, "_opd_FUN_009252e0", "calls-peer-queue-insert");
        AddIf(body, tokens, "_opd_FUN_00924dd0", "calls-peer-queue-drain");
        AddIf(body, tokens, "_opd_FUN_00924ae8", "initializes-peer-queue-container");
        AddIf(body, tokens, "FUN_0086ff98((int)param_4)", "allocates-copy-buffer");
        AddIf(body, tokens, "FUN_00871708((ulonglong)uVar4,param_3 & 0xffffffff,param_4)", "copies-param3-payload-into-copy-buffer");
        AddIf(body, tokens, "_opd_FUN_009265e0", "appends-copy-buffer-to-peer-queue");
        AddIf(body, tokens, "_opd_FUN_00924680(auStack_70,uVar4,(int)param_4)", "queue-record-holds-buffer-and-length");
        AddIf(body, tokens, "FUN_00871708(param_2 & 0xffffffff,(ulonglong)puVar1[5],(ulonglong)uVar10)", "copies-queued-buffer-to-param2");
        AddIf(body, tokens, "FUN_0086e4a8(uVar2)", "frees-drained-queued-buffer");
        AddIf(body, tokens, "_opd_FUN_009261d8(puVar1,0)", "removes-drained-queue-entry");
        AddIf(body, tokens, "*(undefined2 *)(param_4 + 8)", "copies-peer-port-to-param4");
        AddIf(body, tokens, "*(undefined4 *)(param_4 + 4)", "copies-peer-address-to-param4");
        AddIf(body, tokens, "param_1 + 0x28", "local-port-routes-to-queue-plus-0x28");
        AddIf(body, tokens, "param_1 + 0xc", "alternate-local-port-routes-to-queue-plus-0x0c");
        AddIf(body, tokens, "*(int *)(param_4 + 4) != 0x7f000001", "remote-address-branch");
        AddIf(body, tokens, "uVar5 = (*(code *)*puVar4)(piVar3,param_2,param_3,0,1)", "remote-address-uses-virtual-send");
        AddIf(body, tokens, "piVar4[0x11] = uVar9", "payload-length-word-0x11");
        AddIf(body, tokens, "piVar4[0x10] = uVar9", "payload-length-word-0x10");
        AddIf(body, tokens, "local_20a0 == -2", "special-wrapper-minus-2");
        AddIf(body, tokens, "local_20a0 == -3", "special-wrapper-minus-3");
        AddIf(body, tokens, "_opd_FUN_008bd708", "fragment-wrapper-reassembly");
        AddIf(body, tokens, "FUN_0086cea8", "repacked-wrapper-copy");
        AddIf(body, tokens, "PTR_DAT_0197336c + 0x6c0", "payload-object-table-base");
        AddIf(body, tokens, "param_1 * 0x50", "payload-object-stride-0x50");
        AddIf(body, tokens, "FUN_0086de68((int)(puVar8 + 7)", "payload-object-bitreader-at-plus-0x1c");
        AddIf(body, tokens, "uVar3 != 0xffffffff", "associated-object-token-dispatch");
        AddIf(body, tokens, "(int)param_7 != -1", "associated-object-token-dispatch");
        AddIf(body, tokens, "*piVar7 + 0x90", "associated-object-vslot-0x90");
        AddIf(body, tokens, "*piVar7 + 8", "owner-slot-dispatch");
        AddIf(body, tokens, "*param_2 + 8", "owner-slot-dispatch");

        foreach (var token in new[]
        {
            "recvfrom(",
            "recv(",
            "sendto(",
            "FUN_00871708",
            "FUN_0086a708",
            "FUN_0086cea8",
            "0xfffffffe",
            "0xfffffffd"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                tokens.Add(token);
            }
        }

        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string[] ExtractCalls(string body)
    {
        var matches = Regex.Matches(body, @"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto)\s*\(");
        return matches
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static ExportedFunction[] ExtractFunctions(string[] lines)
    {
        var starts = new List<(int Index, string Name, string Address)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
                continue;
            }

            match = SplitFunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
            }
        }

        var functions = new List<ExportedFunction>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].Index - 1 : lines.Length - 1;
            functions.Add(BuildExportedFunction(lines, start.Index, end, start.Name, start.Address));
        }

        return functions.ToArray();
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(90));
        return text.Length <= 2800 ? text : text[..2800];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourcePrePayloadReceiveReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourcePrePayloadReceiveSummary Summary,
    Tf2Ps3SourcePrePayloadFunction[] Functions,
    Tf2Ps3SourcePrePayloadBoundary[] Boundaries,
    Tf2Ps3SourcePrePayloadReceiveGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourcePrePayloadReceiveSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool QueueInsertionCopiesPayload,
    bool QueueDrainCopiesQueuedPayload,
    bool LocalRouteQueuesPayload,
    bool ExternalRouteUsesSocketInterface,
    bool PayloadObjectFilledFromQueue,
    bool SpecialWrappersAfterQueue,
    bool PayloadDrainDispatchesPayloadObjects,
    bool NativeInputImplementationReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourcePrePayloadFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourcePrePayloadBoundary(
    string Name,
    string Address,
    string Meaning);

public sealed record Tf2Ps3SourcePrePayloadReceiveGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
