using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHandlerRegistrationReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "00a57f48",
        "00a584d0",
        "00a58868",
        "00a58c10",
        "00a5a550",
        "00a5df70"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var lines = await File.ReadAllLinesAsync(cExportPath);
        var allFunctions = ExtractFunctions(lines).ToArray();
        var targetFunctions = allFunctions
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .ToArray();

        var directRegistrationCallsites = allFunctions
            .Where(static function =>
                function.Address != "00a5df70"
                && function.Body.Contains("_opd_FUN_00a5df70(", StringComparison.Ordinal))
            .Select(function => new Tf2Ps3SourceHandlerRegistrationCallsite(
                function.Address,
                function.Name,
                function.StartLine,
                ExtractCalls(function.Body),
                BuildMatchedTokens(function.Body, ["_opd_FUN_00a5df70("]),
                Preview(function.Lines)))
            .ToArray();

        var handlerTableMentions = allFunctions
            .Where(static function => MentionsHandlerTable(function.Body))
            .Select(function => new Tf2Ps3SourceHandlerTableMention(
                function.Address,
                function.Name,
                function.StartLine,
                ClassifyMention(function),
                BuildMatchedTokens(function.Body,
                [
                    "param_1 + 0x1e0c",
                    "param_1 + 0x1e18",
                    "_opd_FUN_00a57f48",
                    "_opd_FUN_00a625e8",
                    "*param_2 + 0x20",
                    "*puStack00000038 + 8"
                ]),
                ExtractCalls(function.Body),
                Preview(function.Lines)))
            .OrderBy(static mention => mention.Address, StringComparer.Ordinal)
            .ToArray();

        var unknowns = new List<Tf2Ps3SourceHandlerRegistrationUnknown>();
        if (directRegistrationCallsites.Length == 0)
        {
            unknowns.Add(new Tf2Ps3SourceHandlerRegistrationUnknown(
                "registered-handler-constructor-xrefs-unresolved",
                "00a5df70",
                "The C export exposes the registration helper but no direct callsites. The handler objects are likely installed through constructor/vtable paths that need Ghidra reference and class/vtable recovery."));
        }

        // This reducer separates lookup from dispatch. The id callback appears in 00a57f48,
        // while parse/execute callbacks appear in 00a58c10.
        if (!targetFunctions.Any(static function =>
                function.Address == "00a57f48"
                && function.EvidenceTokens.Contains("*piVar2 + 0x20", StringComparer.Ordinal)))
        {
            unknowns.Add(new Tf2Ps3SourceHandlerRegistrationUnknown(
                "registered-handler-id-callback-unresolved",
                "00a57f48",
                "Expected handler vtable +0x20 id comparison was not found in the lookup helper."));
        }

        var report = new Tf2Ps3SourceHandlerRegistrationReport(
            "tf2ps3-source-handler-registration-map",
            "Maps the native TF2 PS3 Source payload handler dispatch and registration contract. This report proves the table/dispatcher shape; enumerating the concrete registered handler ids still requires Ghidra vtable/class recovery because the C export does not contain direct registration callsites.",
            cExportPath,
            new Tf2Ps3SourceHandlerRegistrationSummary(
                TargetAddresses.Length,
                targetFunctions.Length,
                targetFunctions.Any(static function => function.Address == "00a58c10"),
                targetFunctions.Any(static function => function.Address == "00a58868"),
                targetFunctions.Any(static function => function.Address == "00a57f48"),
                targetFunctions.Any(static function => function.Address == "00a5df70"),
                directRegistrationCallsites.Length,
                handlerTableMentions.Length,
                unknowns.Count),
            new Tf2Ps3SourcePayloadDispatcherContract(
                "00a58c10",
                "The staged frame-kind-2 payload is a bitstream containing a loop of 5-bit message ids.",
                [
                    new("0", "builtin-no-body-continue", "Handled by 00a58868. Returns success without a body."),
                    new("1", "builtin-string-callback", "Handled by 00a58868. Reads one 0x400-capped string and calls object +0x1e08 vtable +0x0c."),
                    new("2", "builtin-keyed-string-callback", "Handled by 00a58868. Reads a 32-bit key, a 0x400-capped string, and a 1-bit branch selecting object +0x1e08 vtable +0x1c or +0x24."),
                    new(">=3", "registered-handler", "Resolved through 00a57f48 and parsed/executed through the registered handler vtable.")
                ],
                "Lookup or parse failure clears object +0x98 with FUN_00870138(param_1 + 0x26, 0) and aborts the payload."),
            new Tf2Ps3SourceHandlerTableContract(
                "00a57f48",
                "object +0x1e0c",
                "object +0x1e18",
                "handler vtable +0x20",
                "Scans registered handler pointers and returns the handler whose vtable +0x20 id equals the decoded 5-bit message id."),
            [
                new("0x08", "bind-owner", "Called by 00a5df70 after appending the handler to bind it back to the player/session object."),
                new("0x10", "execute", "Called by 00a58c10 after a successful parse while object byte +0x04 marks execution in progress."),
                new("0x14", "parse", "Called by 00a58c10 to parse handler-specific bits from the native payload bit reader."),
                new("0x20", "message-id", "Called by 00a57f48 and 00a5df70 to identify the registered native message id."),
                new("0x24", "result-id", "Called by 00a58c10 and forwarded to player/session vtable +0xd0 with consumed bit count."),
                new("0x28", "pre-or-name", "Called before parse and again for filter/name comparison paths."),
                new("0x30", "cleanup-skip", "Called when a filtered/skipped handler should be cleaned up without executing.")
            ],
            new Tf2Ps3SourceHandlerRegistrationContract(
                "00a5df70",
                "Calls 00a57f48 with handler vtable +0x20 id and rejects duplicate handlers.",
                "Appends through 00a625e8 into object +0x1e0c using current count object +0x1e18.",
                "Calls handler vtable +0x08 with the player/session object after appending."),
            targetFunctions,
            directRegistrationCallsites,
            handlerTableMentions,
            unknowns.ToArray());

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceHandlerRegistrationFunction BuildFunction(ExportedFunction function)
    {
        return new Tf2Ps3SourceHandlerRegistrationFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyRole(function.Address),
            ExtractCalls(function.Body),
            BuildEvidence(function.Body),
            Preview(function.Lines));
    }

    private static string ClassifyRole(string address)
    {
        return address switch
        {
            "00a57f48" => "registered-handler-table-lookup",
            "00a584d0" => "attached-player-state-ack-writer",
            "00a58868" => "builtin-message-id-handler",
            "00a58c10" => "payload-message-dispatcher",
            "00a5a550" => "token-slot-reset",
            "00a5df70" => "registered-handler-install",
            _ => "handler-registration-helper"
        };
    }

    private static string ClassifyMention(ExportedFunction function)
    {
        if (function.Address == "00a57f48")
        {
            return "handler-table-lookup";
        }

        if (function.Address == "00a5df70")
        {
            return "handler-registration";
        }

        if (function.Body.Contains("_opd_FUN_00a57f48", StringComparison.Ordinal))
        {
            return "handler-lookup-caller";
        }

        if (function.Body.Contains("_opd_FUN_00a625e8", StringComparison.Ordinal))
        {
            return "handler-table-append-caller";
        }

        return "handler-table-reference";
    }

    private static bool MentionsHandlerTable(string body)
    {
        return body.Contains("param_1 + 0x1e0c", StringComparison.Ordinal)
            || body.Contains("param_1 + 0x1e18", StringComparison.Ordinal)
            || body.Contains("_opd_FUN_00a57f48", StringComparison.Ordinal)
            || body.Contains("_opd_FUN_00a625e8", StringComparison.Ordinal);
    }

    private static string[] BuildEvidence(string body)
    {
        return BuildMatchedTokens(body,
        [
            "_opd_FUN_00a58868",
            "_opd_FUN_00a57f48",
            "_opd_FUN_00a625e8",
            "_opd_FUN_00a621c8",
            "FUN_00870138(param_1 + 0x26,0)",
            "param_1 + 0x1e0c",
            "param_1 + 0x1e18",
            "*param_2 + 0x20",
            "*piVar2 + 0x20",
            "*piVar11 + 0x28",
            "*piVar11 + 0x14",
            "*piVar11 + 0x24",
            "*piVar11 + 0x10",
            "*piVar11 + 0x30",
            "*param_1 + 0xd0",
            "*param_1 + 0xd8",
            "*puStack00000038 + 8",
            "param_2 == 1",
            "param_2 == 2",
            "0x400",
            "FUN_0086e338",
            "FUN_00870c28((int *)local_60,4)",
            "FUN_0086caf8((int *)local_60,param_2)",
            "_opd_FUN_008b8328"
        ]);
    }

    private static string[] BuildMatchedTokens(string body, IEnumerable<string> tokens)
    {
        return tokens
            .Where(token => body.Contains(token, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
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

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
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

public sealed record Tf2Ps3SourceHandlerRegistrationReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourceHandlerRegistrationSummary Summary,
    Tf2Ps3SourcePayloadDispatcherContract Dispatcher,
    Tf2Ps3SourceHandlerTableContract HandlerTable,
    Tf2Ps3SourceHandlerVtableSlot[] HandlerVtableSlots,
    Tf2Ps3SourceHandlerRegistrationContract Registration,
    Tf2Ps3SourceHandlerRegistrationFunction[] Functions,
    Tf2Ps3SourceHandlerRegistrationCallsite[] DirectRegistrationCallsites,
    Tf2Ps3SourceHandlerTableMention[] HandlerTableMentions,
    Tf2Ps3SourceHandlerRegistrationUnknown[] RemainingUnknowns);

public sealed record Tf2Ps3SourceHandlerRegistrationSummary(
    int TargetFunctionCount,
    int LocatedTargetFunctionCount,
    bool DispatcherMapped,
    bool BuiltinControlsMapped,
    bool HandlerLookupMapped,
    bool RegistrationHelperMapped,
    int DirectRegistrationCallsiteCount,
    int HandlerTableMentionCount,
    int RemainingUnknownCount);

public sealed record Tf2Ps3SourcePayloadDispatcherContract(
    string Address,
    string WireShape,
    Tf2Ps3SourcePayloadMessageIdContract[] MessageIds,
    string FailureBehavior);

public sealed record Tf2Ps3SourcePayloadMessageIdContract(
    string Id,
    string Name,
    string Semantics);

public sealed record Tf2Ps3SourceHandlerTableContract(
    string LookupAddress,
    string PointerArray,
    string CountField,
    string IdCallback,
    string Semantics);

public sealed record Tf2Ps3SourceHandlerVtableSlot(
    string Offset,
    string Role,
    string Semantics);

public sealed record Tf2Ps3SourceHandlerRegistrationContract(
    string Address,
    string DuplicateCheck,
    string AppendBehavior,
    string BindBehavior);

public sealed record Tf2Ps3SourceHandlerRegistrationFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceHandlerRegistrationCallsite(
    string Address,
    string Name,
    int StartLine,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceHandlerTableMention(
    string Address,
    string Name,
    int StartLine,
    string MentionKind,
    string[] EvidenceTokens,
    string[] Calls,
    string Preview);

public sealed record Tf2Ps3SourceHandlerRegistrationUnknown(
    string Id,
    string AddressOrKey,
    string NextReverseEngineeringTarget);
