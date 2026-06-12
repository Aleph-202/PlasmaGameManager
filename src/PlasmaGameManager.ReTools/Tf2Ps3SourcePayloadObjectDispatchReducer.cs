using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePayloadObjectDispatchReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "0039e7c0",
        "00876bb8",
        "008b9ad8",
        "008b9c38",
        "008bd158",
        "008bdff0",
        "008be1e8",
        "00a2fcb0",
        "00a2ffd8"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourcePayloadObjectDispatchReport> ReduceAsync(
        string cExportPath,
        string prePayloadReceivePath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        using var prePayload = JsonDocument.Parse(await File.ReadAllTextAsync(prePayloadReceivePath));
        var prePayloadSummary = prePayload.RootElement.GetProperty("Summary");
        var prePayloadBoundaryProven =
            ReadBool(prePayloadSummary, "QueueInsertionCopiesPayload")
            && ReadBool(prePayloadSummary, "QueueDrainCopiesQueuedPayload")
            && ReadBool(prePayloadSummary, "PayloadDrainDispatchesPayloadObjects");

        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var drain = byAddress.GetValueOrDefault("008be1e8");
        var lookup = byAddress.GetValueOrDefault("008b9ad8");
        var filter = byAddress.GetValueOrDefault("00a2fcb0");
        var finalizer = byAddress.GetValueOrDefault("00a2ffd8");
        var creator = byAddress.GetValueOrDefault("008b9c38");
        var updateCaller = byAddress.GetValueOrDefault("00876bb8");

        var dispatchCallsiteCount = functions.Count(static function =>
            function.EvidenceTokens.Contains("payload-object-dispatch-call", StringComparer.Ordinal));
        var ownerSubobjectArgumentRecovered =
            updateCaller?.EvidenceTokens.Contains("owner-subobject-param1-plus-0x69", StringComparer.Ordinal) == true
            && updateCaller.EvidenceTokens.Contains("connection-id-param1-plus-0x6b", StringComparer.Ordinal);
        var firstWordDispatchRecovered =
            drain?.EvidenceTokens.Contains("payload-first-word-read", StringComparer.Ordinal) == true
            && drain.EvidenceTokens.Contains("first-word-big-endian-swap", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("minus-one-owner-control-branch", StringComparer.Ordinal);
        var associatedLookupRecovered =
            lookup?.EvidenceTokens.Contains("source-player-table-scan", StringComparer.Ordinal) == true
            && lookup.EvidenceTokens.Contains("connection-id-callback-slot-0xc0", StringComparer.Ordinal)
            && lookup.EvidenceTokens.Contains("association-comparator", StringComparer.Ordinal);
        var associatedSlot90Recovered =
            drain?.EvidenceTokens.Contains("associated-slot90-dispatch", StringComparer.Ordinal) == true
            && drain.Calls.Contains("_opd_FUN_008b9ad8", StringComparer.Ordinal);
        var ownerBitreaderRecovered =
            drain?.EvidenceTokens.Contains("owner-bitreader-plus-0x1c", StringComparer.Ordinal) == true
            && drain.EvidenceTokens.Contains("owner-bitreader-word-refill", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("owner-slot8-dispatch", StringComparer.Ordinal);
        var filterGateRecovered =
            filter?.EvidenceTokens.Contains("payload-object-word-plus-4-mask-source", StringComparer.Ordinal) == true
            && filter.EvidenceTokens.Contains("global-filter-table-scan", StringComparer.Ordinal);
        var finalizerRecovered =
            finalizer?.Calls.Contains("_opd_FUN_008bd158", StringComparer.Ordinal) == true;
        var creatorReusesLookup =
            creator?.Calls.Contains("_opd_FUN_008b9ad8", StringComparer.Ordinal) == true
            && creator.EvidenceTokens.Contains("creates-source-player-object", StringComparer.Ordinal);

        var gates = new[]
        {
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "pre-payload-queue-boundary-proven",
                prePayloadBoundaryProven ? "proven" : "missing",
                prePayloadReceivePath,
                "The bytes reaching this dispatch loop are the queued payload-object body from the pre-payload receive boundary."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "payload-first-word-dispatch-recovered",
                firstWordDispatchRecovered ? "proven" : "missing",
                "008be1e8",
                "The dispatch loop reads the first payload word, endian-swaps it, and branches non--1 to associated-object dispatch or -1 to owner control."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "associated-object-lookup-contract-recovered",
                associatedLookupRecovered ? "proven" : "missing",
                "008b9ad8",
                "The lookup scans the global Source player table, matches connection id through vtable +0xc0, then compares the payload object against vtable +0xb4 data."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "associated-object-slot90-dispatch-recovered",
                associatedSlot90Recovered ? "proven" : "missing",
                "008be1e8 -> 008b9ad8 -> vtable +0x90",
                "Non--1 payload objects are delivered to the associated object/player through virtual slot +0x90."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "owner-minus1-bitreader-and-slot8-recovered",
                ownerBitreaderRecovered ? "proven" : "missing",
                "008be1e8 payload +0x1c -> owner vtable +0x08",
                "The -1 owner branch uses the in-place bitreader at payload object +0x1c, consumes a control bit/word, then calls owner virtual slot +0x08."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "payload-filter-and-finalizer-named",
                filterGateRecovered && finalizerRecovered ? "proven" : "needs-review",
                "00a2fcb0 / 00a2ffd8",
                "The drain loop's validity/finalization helpers are named enough to keep them out of the unknown message-reader bucket."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "object-creation-reuses-associated-lookup",
                creatorReusesLookup ? "proven" : "needs-review",
                "008b9c38",
                "Object creation/reuse uses the same associated lookup before initializing a player object through 00a5e058/00a5d0c0."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "payload-dispatch-caller-owner-argument-recovered",
                ownerSubobjectArgumentRecovered ? "proven" : "needs-review",
                "00876bb8 -> 008be1e8(param_1[0x6b], param_1 + 0x69)",
                "The live update caller passes the connection id at +0x6b and owner/control subobject at +0x69 into the payload-object dispatch loop."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "native-associated-slot90-handler-implemented",
                "missing",
                "server implementation gate",
                "The native replacement still needs semantic consumers for associated-object slot +0x90 payloads."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "native-owner-slot8-handler-implemented",
                "missing",
                "server implementation gate",
                "The native replacement still needs semantic consumers for owner-slot +0x08 control payloads."),
            new Tf2Ps3SourcePayloadObjectDispatchGate(
                "native-markerless-input-complete",
                "missing",
                "server/live verification gate",
                "This is not complete until these dispatch paths decode the hard markerless PCAP/live client packets into named fields and map-load is proven live.")
        };

        var report = new Tf2Ps3SourcePayloadObjectDispatchReport(
            "tf2ps3-source-payload-object-dispatch-map",
            "Maps TF.elf payload-object dispatch after the queue/drain boundary: associated-object token lookup, owner -1 control dispatch, filter/finalizer helpers, and remaining native server gates.",
            new Tf2Ps3SourcePayloadObjectDispatchInputs(cExportPath, prePayloadReceivePath),
            new Tf2Ps3SourcePayloadObjectDispatchSummary(
                TargetAddresses.Length,
                functions.Length,
                dispatchCallsiteCount,
                prePayloadBoundaryProven,
                ownerSubobjectArgumentRecovered,
                firstWordDispatchRecovered,
                associatedLookupRecovered,
                associatedSlot90Recovered,
                ownerBitreaderRecovered,
                filterGateRecovered,
                finalizerRecovered,
                creatorReusesLookup,
                false,
                false,
                false,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            functions,
            [
                new("dispatch-update-caller", "00876bb8 -> 008be1e8(param_1[0x6b], param_1 + 0x69)", "The recovered caller that supplies the connection id and owner/control object used by 008be1e8."),
                new("source-player-table", "PTR_DAT_0197336c + 0x148 / +0x154", "The object list scanned by 008b9ad8 and the pre-drain part of 008be1e8."),
                new("connection-id-callback", "object vtable +0xc0", "Must equal the connection id/slot passed to 008be1e8 and 008b9ad8."),
                new("association-data-callback", "object vtable +0xb4", "Returns the association data compared against the payload object by FUN_0086fb58."),
                new("associated-object-dispatch", "object vtable +0x90", "Receives non--1 payload objects after 008b9ad8 succeeds."),
                new("owner-control-dispatch", "owner vtable +0x08", "Receives -1 owner-control payload objects after the bitreader control read."),
                new("payload-object-bitreader", "payload object +0x1c", "The in-place bitreader used by the -1 owner branch and slot +0x70 chain.")
            ],
            gates,
            [
                "The hard markerless family is now beyond the queue/drain boundary: pre-payload bytes are copied intact, and 008be1e8 chooses the next parser by the first payload word.",
                "Non--1 payloads are not raw Source bitstreams at the visible boundary. They first resolve a Source player/object through 008b9ad8 and are then delivered through that object's virtual slot +0x90.",
                "-1 payloads use the payload object's in-place bitreader at +0x1c and call the owner object through virtual slot +0x08. This is the next concrete owner-control parser to implement.",
                "00a2fcb0 and 00a2ffd8 are drain gate/finalizer helpers, not the missing CLC_Move field reader.",
                "The live update caller is 00876bb8: it sends param_1[0x6b] as the connection id and param_1 + 0x69 as the owner/control object into 008be1e8.",
                "Native completion remains blocked on implementing the slot +0x90 and owner +0x08 semantic consumers and proving them against PCAP/live RPCS3 traffic."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourcePayloadObjectDispatchFunction BuildFunction(ExportedFunction function) =>
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
            "0039e7c0" => "payload-object-dispatch-c-wrapper",
            "00876bb8" => "payload-object-dispatch-update-caller",
            "008b9ad8" => "associated-object-token-lookup",
            "008b9c38" => "associated-object-create-or-reuse",
            "008bd158" => "fragmented-send-finalizer-route",
            "008bdff0" => "payload-object-builder",
            "008be1e8" => "payload-object-drain-and-dispatch",
            "00a2fcb0" => "payload-filter-drain-gate",
            "00a2ffd8" => "payload-finalizer",
            _ => "payload-object-dispatch-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "PTR_DAT_0197336c + 0x148", "source-player-table");
        AddIf(body, tokens, "puVar3 + 0x148", "source-player-table");
        AddIf(body, tokens, "puVar3 + 0x154", "source-player-table-scan");
        AddIf(body, tokens, "*(int *)(puVar3 + 0x154)", "source-player-table-scan");
        AddIf(body, tokens, "*piVar2 + 0xc0", "connection-id-callback-slot-0xc0");
        AddIf(body, tokens, "*piVar7 + 0xc0", "connection-id-callback-slot-0xc0");
        AddIf(body, tokens, "*piVar2 + 0xb4", "association-data-callback-slot-0xb4");
        AddIf(body, tokens, "FUN_0086fb58(param_2,piVar5,0)", "association-comparator");
        AddIf(body, tokens, "uVar11 = *(uint *)piVar7[6]", "payload-first-word-read");
        AddIf(body, tokens, "uVar11 >> 0x18", "first-word-big-endian-swap");
        AddIf(body, tokens, "(int)param_7 != -1", "associated-token-branch");
        AddIf(body, tokens, "(int)param_7 != -1", "minus-one-owner-control-branch");
        AddIf(body, tokens, "_opd_FUN_008b9ad8(param_1,piVar7)", "associated-lookup-call");
        AddIf(body, tokens, "*piVar7 + 0x90", "associated-slot90-dispatch");
        AddIf(body, tokens, "_opd_FUN_008be1e8(param_1,param_2", "payload-object-dispatch-call");
        AddIf(body, tokens, "_opd_FUN_008be1e8(param_1[0x6b],(int *)(param_1 + 0x69)", "payload-object-dispatch-call");
        AddIf(body, tokens, "param_1[0x6b]", "connection-id-param1-plus-0x6b");
        AddIf(body, tokens, "param_1 + 0x69", "owner-subobject-param1-plus-0x69");
        AddIf(body, tokens, "uVar5 + 0x1c", "owner-bitreader-plus-0x1c");
        AddIf(body, tokens, "iVar8 + 0x14", "owner-bitreader-bit-count");
        AddIf(body, tokens, "uVar3 >> 0x18", "owner-bitreader-word-refill");
        AddIf(body, tokens, "*param_2 + 8", "owner-slot8-dispatch");
        AddIf(body, tokens, "_opd_FUN_00a2fcb0", "payload-filter-gate-call");
        AddIf(body, tokens, "_opd_FUN_00a2ffd8", "payload-finalizer-call");
        AddIf(body, tokens, "*(uint *)(param_1 + 4)", "payload-object-word-plus-4-mask-source");
        AddIf(body, tokens, "puVar3 + 0x48", "global-filter-table-scan");
        AddIf(body, tokens, "puVar3 + 0x54", "global-filter-table-count");
        AddIf(body, tokens, "uVar6 & *puVar5", "filter-mask-compare");
        AddIf(body, tokens, "_opd_FUN_008bd158(1,param_1", "finalizer-fragmented-send-route");
        AddIf(body, tokens, "_opd_FUN_00a5e058", "creates-source-player-object");
        AddIf(body, tokens, "_opd_FUN_00a5d0c0", "initializes-source-player-association");
        AddIf(body, tokens, "_opd_FUN_008bb058", "send-route");
        AddIf(body, tokens, "0xfffffffe", "fragment-wrapper-send");

        foreach (var token in new[]
        {
            "_opd_FUN_008bdff0",
            "_opd_FUN_008b9ad8",
            "_opd_FUN_008bd158",
            "_opd_FUN_00a2fcb0",
            "_opd_FUN_00a2ffd8",
            "FUN_008717b8",
            "FUN_00871968",
            "FUN_0086ff98",
            "FUN_0086fb58"
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

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

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
        var text = string.Join('\n', lines.Take(100));
        return text.Length <= 3200 ? text : text[..3200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourcePayloadObjectDispatchReport(
    string Status,
    string Note,
    Tf2Ps3SourcePayloadObjectDispatchInputs Inputs,
    Tf2Ps3SourcePayloadObjectDispatchSummary Summary,
    Tf2Ps3SourcePayloadObjectDispatchFunction[] Functions,
    Tf2Ps3SourcePayloadObjectDispatchAnchor[] Anchors,
    Tf2Ps3SourcePayloadObjectDispatchGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourcePayloadObjectDispatchInputs(
    string CExport,
    string PrePayloadReceiveBoundary);

public sealed record Tf2Ps3SourcePayloadObjectDispatchSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int DispatchCallsiteCount,
    bool PrePayloadBoundaryProven,
    bool OwnerSubobjectArgumentRecovered,
    bool PayloadFirstWordDispatchRecovered,
    bool AssociatedLookupRecovered,
    bool AssociatedSlot90DispatchRecovered,
    bool OwnerBitreaderDispatchRecovered,
    bool PayloadFilterGateRecovered,
    bool PayloadFinalizerRecovered,
    bool ObjectCreationReusesAssociatedLookup,
    bool NativeAssociatedSlot90HandlerImplemented,
    bool NativeOwnerSlot8HandlerImplemented,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourcePayloadObjectDispatchFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourcePayloadObjectDispatchAnchor(
    string Name,
    string Expression,
    string Meaning);

public sealed record Tf2Ps3SourcePayloadObjectDispatchGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
