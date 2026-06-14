using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceAssociatedObjectTokenContractReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008b9468",
        "008b9ad8",
        "008b9c38",
        "008bd510",
        "008be1e8"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceAssociatedObjectTokenContractReport> ReduceAsync(
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

        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var acceptedPeer = byAddress.GetValueOrDefault("008b9468");
        var lookup = byAddress.GetValueOrDefault("008b9ad8");
        var createOrReuse = byAddress.GetValueOrDefault("008b9c38");
        var localAssociation = byAddress.GetValueOrDefault("008bd510");
        var dispatch = byAddress.GetValueOrDefault("008be1e8");

        var acceptedPeerReaderRecovered =
            acceptedPeer?.EvidenceTokens.Contains("accepted-peer-list-scan", StringComparer.Ordinal) == true
            && acceptedPeer.EvidenceTokens.Contains("five-byte-control-read", StringComparer.Ordinal)
            && acceptedPeer.EvidenceTokens.Contains("message-type-4-association-request", StringComparer.Ordinal);
        var acceptedPeerObjectMatchRecovered =
            acceptedPeer?.EvidenceTokens.Contains("connection-id-callback-slot-0xc0", StringComparer.Ordinal) == true
            && acceptedPeer.EvidenceTokens.Contains("association-token-callback-slot-0xc4", StringComparer.Ordinal)
            && acceptedPeer.EvidenceTokens.Contains("accepted-peer-address-compare", StringComparer.Ordinal);
        var acceptedPeerAttachRecovered =
            acceptedPeer?.EvidenceTokens.Contains("object-socket-field-0x24", StringComparer.Ordinal) == true
            && acceptedPeer.EvidenceTokens.Contains("object-attached-byte-0x42e", StringComparer.Ordinal)
            && acceptedPeer.EvidenceTokens.Contains("attached-reader-slot-0x6c", StringComparer.Ordinal);
        var associatedLookupRecovered =
            lookup?.EvidenceTokens.Contains("source-player-table-scan", StringComparer.Ordinal) == true
            && lookup.EvidenceTokens.Contains("connection-id-callback-slot-0xc0", StringComparer.Ordinal)
            && lookup.EvidenceTokens.Contains("association-data-callback-slot-0xb4", StringComparer.Ordinal)
            && lookup.EvidenceTokens.Contains("association-descriptor-compare", StringComparer.Ordinal);
        var associatedSlot90DispatchRecovered =
            dispatch?.EvidenceTokens.Contains("payload-first-word-big-endian-dispatch", StringComparer.Ordinal) == true
            && dispatch.EvidenceTokens.Contains("associated-lookup-call", StringComparer.Ordinal)
            && dispatch.EvidenceTokens.Contains("associated-slot-0x90-dispatch", StringComparer.Ordinal);
        var creationUsesSameLookup =
            createOrReuse?.EvidenceTokens.Contains("associated-lookup-call", StringComparer.Ordinal) == true
            && createOrReuse.EvidenceTokens.Contains("player-object-constructor", StringComparer.Ordinal)
            && createOrReuse.EvidenceTokens.Contains("player-association-initializer", StringComparer.Ordinal);
        var localAssociationTableRecovered =
            localAssociation?.EvidenceTokens.Contains("local-association-table-scan", StringComparer.Ordinal) == true
            && localAssociation.EvidenceTokens.Contains("association-descriptor-compare", StringComparer.Ordinal);
        var contractRecovered =
            acceptedPeerReaderRecovered
            && acceptedPeerObjectMatchRecovered
            && acceptedPeerAttachRecovered
            && associatedLookupRecovered
            && associatedSlot90DispatchRecovered
            && creationUsesSameLookup;

        var gates = new[]
        {
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "accepted-peer-control-reader-recovered",
                acceptedPeerReaderRecovered ? "proven" : "missing",
                "008b9468",
                "TF.elf reads accepted-peer 5-byte connected control records and treats message type 4 as the association request."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "accepted-peer-object-match-recovered",
                acceptedPeerObjectMatchRecovered ? "proven" : "missing",
                "008b9468 -> vtable +0xc0/+0xc4 and FUN_0086fb58",
                "The type-4 request matches connection id, association token, and peer address against a Source object."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "accepted-peer-attaches-object-socket",
                acceptedPeerAttachRecovered ? "proven" : "missing",
                "008b9468 -> object +0x24/+0x42e -> vtable +0x6c",
                "After a match, TF.elf stores the accepted socket, marks the object attached, and invokes the attached-frame reader slot."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "associated-lookup-contract-recovered",
                associatedLookupRecovered ? "proven" : "missing",
                "008b9ad8",
                "The payload-object path scans the same Source object table, matches connection id, then compares the payload object with object vtable +0xb4 association data."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "associated-slot90-dispatch-recovered",
                associatedSlot90DispatchRecovered ? "proven" : "missing",
                "008be1e8 -> 008b9ad8 -> vtable +0x90",
                "Non--1 payload-object bodies dispatch through the associated object's slot +0x90 after lookup succeeds."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "object-creation-reuses-associated-lookup",
                creationUsesSameLookup ? "proven" : "missing",
                "008b9c38",
                "Object creation/reuse calls 008b9ad8 before creating and initializing the Source player association."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "local-association-table-helper-recovered",
                localAssociationTableRecovered ? "supporting-evidence" : "not-required",
                "008bd510",
                "A separate local association-table helper uses the same address-state comparator pattern; useful support, not required for the main contract."),
            new Tf2Ps3SourceAssociatedObjectTokenContractGate(
                "native-source-input-ready",
                "missing",
                "slot +0x90 field grammar and live map-load proof",
                "This contract proves the association-token dispatch layer only. Native readiness still requires decoding the slot +0x90 packet fields and proving live map-load.")
        };

        var report = new Tf2Ps3SourceAssociatedObjectTokenContractReport(
            "tf2ps3-source-associated-object-token-contract",
            "Recovers the TF.elf associated-object token/descriptor path used by markerless Source payload objects: accepted-peer association, object lookup, and vtable +0x90 dispatch.",
            new Tf2Ps3SourceAssociatedObjectTokenContractSummary(
                TargetAddresses.Length,
                functions.Length,
                acceptedPeerReaderRecovered,
                acceptedPeerObjectMatchRecovered,
                acceptedPeerAttachRecovered,
                associatedLookupRecovered,
                associatedSlot90DispatchRecovered,
                creationUsesSameLookup,
                localAssociationTableRecovered,
                contractRecovered,
                false,
                gates.Count(static gate => gate.Status is "missing")),
            functions,
            [
                new("accepted-peer-list", "PTR_DAT_0197336c + 0x1b0 / +0x1bc", "Pending accepted connected peer sockets scanned by 008b9468."),
                new("source-object-list", "PTR_DAT_0197336c + 0x148 / +0x154", "Source player/object list scanned by 008b9468 and 008b9ad8."),
                new("association-request", "5-byte control record: message type 4 + little-endian 32-bit token", "Accepted peer attach request decoded by 008b9468."),
                new("connection-id", "object vtable +0xc0", "Must match the connection id used by the payload-object drain loop."),
                new("association-token", "object vtable +0xc4", "Must match the token from the accepted-peer type-4 request."),
                new("association-data", "object vtable +0xb4", "Descriptor/state compared to the payload object by FUN_0086fb58 in 008b9ad8."),
                new("associated-dispatch", "object vtable +0x90", "Consumes non--1 payload objects after 008b9ad8 resolves the object.")
            ],
            gates,
            [
                "The hard markerless first word should no longer be treated as proof of an unexplained decrypt/wire transform just because it is not descriptor type 1/2/3.",
                "TF.elf has a native non--1 payload-object path: 008be1e8 calls 008b9ad8, which compares the payload object against association data returned by vtable +0xb4, then dispatches the same payload object through vtable +0x90.",
                "The 5-byte type-4 association request and the later non--1 payload-object path are related but not identical fields: the type-4 request uses vtable +0xc4 token matching, while 008b9ad8 uses vtable +0xb4 descriptor/state matching.",
                "The next strict-native implementation target is therefore slot +0x90 field grammar and category/usercmd semantics, not a generic pre-payload XOR/RC4/decrypt search."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceAssociatedObjectTokenContractFunction BuildFunction(ExportedFunction function) =>
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
            "008b9468" => "accepted-peer-association-reader",
            "008b9ad8" => "associated-object-lookup",
            "008b9c38" => "associated-object-create-or-reuse",
            "008bd510" => "local-association-table-helper",
            "008be1e8" => "payload-object-drain-dispatch",
            _ => "associated-object-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "puVar7 + 0x1b0", "accepted-peer-list-scan");
        AddIf(body, tokens, "puVar7 + 0x1bc", "accepted-peer-list-count");
        AddIf(body, tokens, "_opd_FUN_008b82c0(*piVar20,auStack_b0,5,0)", "five-byte-control-read");
        AddIf(body, tokens, "FUN_0086de68((int)&local_a8,auStack_b0,5,0,-1)", "five-byte-bitreader-init");
        AddIf(body, tokens, "if (uVar12 == 4)", "message-type-4-association-request");
        AddIf(body, tokens, "*piVar6 + 0xc0", "connection-id-callback-slot-0xc0");
        AddIf(body, tokens, "*piVar6 + 0xc4", "association-token-callback-slot-0xc4");
        AddIf(body, tokens, "piVar20[1] == iVar11", "accepted-peer-connection-id-match");
        AddIf(body, tokens, "uVar13 == uVar12", "accepted-peer-token-match");
        AddIf(body, tokens, "FUN_0086fb58(piVar18,piVar6 + 0x26,1)", "accepted-peer-address-compare");
        AddIf(body, tokens, "piVar6[0x24] = iVar19", "object-socket-field-0x24");
        AddIf(body, tokens, "+ 0x42e", "object-attached-byte-0x42e");
        AddIf(body, tokens, "*piVar6 + 0x6c", "attached-reader-slot-0x6c");
        AddIf(body, tokens, "puVar3 + 0x148", "source-player-table");
        AddIf(body, tokens, "puVar3 + 0x154", "source-player-table-scan");
        AddIf(body, tokens, "*piVar2 + 0xc0", "connection-id-callback-slot-0xc0");
        AddIf(body, tokens, "*piVar2 + 0xb4", "association-data-callback-slot-0xb4");
        AddIf(body, tokens, "FUN_0086fb58(param_2,piVar5,0)", "association-descriptor-compare");
        AddIf(body, tokens, "_opd_FUN_008b9ad8(iVar2,(int *)param_2)", "associated-lookup-call");
        AddIf(body, tokens, "_opd_FUN_008b9ad8(param_1,piVar7)", "associated-lookup-call");
        AddIf(body, tokens, "_opd_FUN_00a5e058", "player-object-constructor");
        AddIf(body, tokens, "_opd_FUN_00a5d0c0", "player-association-initializer");
        AddIf(body, tokens, "uVar11 = *(uint *)piVar7[6]", "payload-first-word-read");
        AddIf(body, tokens, "uVar11 >> 0x18", "payload-first-word-big-endian-dispatch");
        AddIf(body, tokens, "*piVar7 + 0x90", "associated-slot-0x90-dispatch");
        AddIf(body, tokens, "FUN_0086fb58(param_2,(int *)(iVar9 + uVar3),0)", "local-association-table-scan");
        AddIf(body, tokens, "FUN_0086fb58(param_2,(int *)(iVar9 + param_1[1]),0)", "local-association-table-scan");

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
        return text.Length <= 3000 ? text : text[..3000];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceAssociatedObjectTokenContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceAssociatedObjectTokenContractSummary Summary,
    Tf2Ps3SourceAssociatedObjectTokenContractFunction[] Functions,
    Tf2Ps3SourceAssociatedObjectTokenContractAnchor[] Anchors,
    Tf2Ps3SourceAssociatedObjectTokenContractGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedObjectTokenContractSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool AcceptedPeerReaderRecovered,
    bool AcceptedPeerObjectMatchRecovered,
    bool AcceptedPeerAttachesObjectSocket,
    bool AssociatedLookupRecovered,
    bool AssociatedSlot90DispatchRecovered,
    bool ObjectCreationReusesAssociatedLookup,
    bool LocalAssociationTableHelperRecovered,
    bool AssociatedObjectTokenContractRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceAssociatedObjectTokenContractFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceAssociatedObjectTokenContractAnchor(
    string Name,
    string Expression,
    string Meaning);

public sealed record Tf2Ps3SourceAssociatedObjectTokenContractGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
