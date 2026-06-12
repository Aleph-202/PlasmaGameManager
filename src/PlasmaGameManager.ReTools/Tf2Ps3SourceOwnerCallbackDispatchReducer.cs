using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerCallbackDispatchReducer
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

    public static async Task<Tf2Ps3SourceOwnerCallbackDispatchReport> ReduceAsync(
        string cExportPath,
        string ownerCallbackMapPath,
        string slot70FieldContractPath,
        string markerlessDataflowTargetsPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);
        using var ownerCallbackMap = JsonDocument.Parse(await File.ReadAllTextAsync(ownerCallbackMapPath));
        using var slot70FieldContract = JsonDocument.Parse(await File.ReadAllTextAsync(slot70FieldContractPath));
        using var markerlessTargets = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessDataflowTargetsPath));

        var ownerSummary = ownerCallbackMap.RootElement.GetProperty("Summary");
        var fieldSummary = slot70FieldContract.RootElement.GetProperty("Summary");
        var markerlessSummary = markerlessTargets.RootElement.GetProperty("Summary");

        var entries = new[]
        {
            BuildEntry(
                functions,
                "00a5b4f0",
                "queued-global-payload-dispatch-wrapper",
                "PTR_PTR_01977d5c queue virtual slot +0x48 returns a queued payload object; payload bytes begin at object +0x1c.",
                "iVar2 + 0x1c",
                "queued-payload-object-not-markerless-param2-builder"),
            BuildEntry(
                functions,
                "00a5d9e0",
                "slot70-markerless-candidate-dispatch-wrapper",
                "Caller-supplied param_2 object is already a bitreader-like state; dispatch view is param_2 + 7.",
                "param_2 + 7",
                "callee-side-bitreader-wrapper-not-caller-builder"),
            BuildEntry(
                functions,
                "00a5d720",
                "buffered-subpayload-dispatch-wrapper",
                "Builds local_50 through FUN_0086de68 from buffered per-channel subpayload fields before dispatch.",
                "&local_50",
                "nested-buffered-subpayload-builder"),
            BuildEntry(
                functions,
                "00a58868",
                "inline-owner-control-branch-helper",
                "Helper for low native message ids under 00a58c10; reads bounded inline string/control fields and calls owner callbacks directly.",
                "param_3",
                "inline-owner-control-branch-not-markerless-param2-builder"),
            BuildEntry(
                functions,
                "00a58c10",
                "native-source-message-dispatcher",
                "Reads five-bit Source message ids, delegates low ids to 00a58868, finds registered handlers through 00a57f48, and calls handler read/process slots.",
                "param_2",
                "dispatcher-not-markerless-wrapper-builder")
        };

        var queuedWrapper = entries.Single(static entry => entry.Address == "00a5b4f0");
        var slot70Wrapper = entries.Single(static entry => entry.Address == "00a5d9e0");
        var subpayload = entries.Single(static entry => entry.Address == "00a5d720");
        var inlineBranch = entries.Single(static entry => entry.Address == "00a58868");
        var dispatcher = entries.Single(static entry => entry.Address == "00a58c10");

        var queuedSequenceRecovered =
            HasAll(queuedWrapper.EvidenceTokens, "owner-slot-0x14-pre-dispatch", "payload-dispatcher-call", "owner-slot-0x18-post-dispatch", "queued-payload-object-source");
        var slot70SequenceRecovered =
            HasAll(slot70Wrapper.EvidenceTokens, "owner-slot-0x14-pre-dispatch", "payload-dispatcher-call", "owner-slot-0x18-post-dispatch", "slot70-param2-plus-7-dispatch");
        var subpayloadSequenceRecovered =
            HasAll(subpayload.EvidenceTokens, "local-bitreader-init", "payload-dispatcher-call")
            && subpayload.EvidenceTokens.Contains("owner-slot-0x20-subpayload-complete", StringComparer.Ordinal);
        var dispatcherOwnerBranchesRecovered =
            dispatcher.EvidenceTokens.Contains("owner-slot-0x10-error", StringComparer.Ordinal)
            && HasAll(inlineBranch.EvidenceTokens, "owner-slot-0x0c-inline-string", "owner-slot-0x1c-inline-branch", "owner-slot-0x24-inline-branch");
        var ownerCallbackPathConstructsParam2 =
            entries.Any(static entry => entry.EvidenceTokens.Contains("markerless-param2-constructor", StringComparer.Ordinal));
        var concreteMarkerlessEdgeRecovered =
            ownerCallbackPathConstructsParam2
            || ReadBool(markerlessSummary, "ConcreteMarkerlessDataflowEdgeRecovered");
        var nativeSourceInputReady =
            concreteMarkerlessEdgeRecovered
            && ReadBool(markerlessSummary, "NativeSourceInputReady");

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "owner-callback-slot-set-expanded",
                ReadInt(ownerSummary, "CallbackSlotCount") >= 8 ? "proven" : "missing",
                ownerCallbackMapPath,
                "The owner callback interface includes dispatch error, pre/post dispatch, inline branch, and subpayload callbacks."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "queued-wrapper-pre-dispatch-post-sequence",
                queuedSequenceRecovered ? "proven" : "missing",
                "00a5b4f0",
                "The queued global payload wrapper brackets 00a58c10 with owner slots +0x14 and +0x18."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "slot70-wrapper-pre-dispatch-post-sequence",
                slot70SequenceRecovered ? "proven" : "missing",
                "00a5d9e0",
                "The slot +0x70 wrapper brackets 00a58c10 with the same owner slots but receives param_2 already constructed."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "subpayload-owner-complete-sequence",
                subpayloadSequenceRecovered ? "proven" : "missing",
                "00a5d720",
                "Buffered subpayload dispatch builds a local bitreader and reports completion through owner slot +0x20."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "dispatcher-inline-owner-branches",
                dispatcherOwnerBranchesRecovered ? "proven" : "missing",
                "00a58c10 -> 00a58868",
                "The dispatcher delegates low native ids to 00a58868, which has inline owner callbacks at +0x0c and +0x1c/+0x24."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "owner-callback-path-builds-markerless-param2",
                ownerCallbackPathConstructsParam2 ? "proven" : "ruled-out-currently",
                $"{ownerCallbackMapPath}; {slot70FieldContractPath}",
                "Current owner callback evidence brackets or observes dispatch; it does not construct the missing caller-side param_2 object."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "concrete-markerless-dataflow-edge",
                concreteMarkerlessEdgeRecovered ? "proven" : "missing",
                markerlessDataflowTargetsPath,
                "The markerless UDP body to slot +0x70 param_2 edge is still required before native client-upload handling is complete."),
            new Tf2Ps3SourceOwnerCallbackDispatchGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "Native Source input is not complete until owner/dispatch evidence is connected to the markerless packet body.")
        };

        var report = new Tf2Ps3SourceOwnerCallbackDispatchReport(
            "tf2ps3-source-owner-callback-dispatch-map",
            "Reduces the owner-callback dispatch path around 00a5d9e0 to determine whether owner callbacks construct markerless Source input or only bracket/observe dispatch.",
            new Tf2Ps3SourceOwnerCallbackDispatchInputs(
                cExportPath,
                ownerCallbackMapPath,
                slot70FieldContractPath,
                markerlessDataflowTargetsPath),
            new Tf2Ps3SourceOwnerCallbackDispatchSummary(
                ReadInt(ownerSummary, "CallbackSlotCount"),
                entries.Length,
                entries.Count(static entry => entry.Present),
                entries.Count(static entry => entry.PreDispatchOwnerSlot),
                entries.Count(static entry => entry.PostDispatchOwnerSlot),
                entries.Count(static entry => entry.InlineBranchOwnerSlots.Length > 0),
                queuedSequenceRecovered,
                slot70SequenceRecovered,
                subpayloadSequenceRecovered,
                dispatcherOwnerBranchesRecovered,
                ReadInt(fieldSummary, "RecoveredFieldCount"),
                ownerCallbackPathConstructsParam2,
                concreteMarkerlessEdgeRecovered,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "ruled-out-currently")),
            entries,
            gates,
            [
                "The owner callback path is now better understood: it brackets native Source payload dispatch and receives inline dispatcher branches, but current evidence does not show it building the caller-side 00a5d9e0 param_2 object.",
                "00a5d9e0 receives an already-constructed bitreader-like param_2, performs owner pre-dispatch, optional subpayload/lane handling, final 00a58c10(param_2 + 7), then owner post-dispatch.",
                "This demotes owner callbacks from primary param_2-builder candidate to control/notification evidence. The next stronger markerless target is helper-slice virtual dispatch or raw registration/dataflow around slot 0x0180ca30.",
                "Native server input should still stay behind the markerless dataflow gate until that caller-side edge is recovered."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerCallbackDispatchEntry BuildEntry(
        IReadOnlyDictionary<string, ExportedFunction> functions,
        string address,
        string role,
        string sourceBoundary,
        string dispatchArgument,
        string implementationMeaning)
    {
        if (!functions.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceOwnerCallbackDispatchEntry(
                address,
                "",
                role,
                false,
                sourceBoundary,
                dispatchArgument,
                implementationMeaning,
                false,
                false,
                [],
                [],
                [],
                "");
        }

        var evidence = BuildEvidence(function.Body);
        var inlineSlots = new List<string>();
        if (evidence.Contains("owner-slot-0x0c-inline-string", StringComparer.Ordinal))
        {
            inlineSlots.Add("0x0c");
        }

        if (evidence.Contains("owner-slot-0x1c-inline-branch", StringComparer.Ordinal))
        {
            inlineSlots.Add("0x1c");
        }

        if (evidence.Contains("owner-slot-0x24-inline-branch", StringComparer.Ordinal))
        {
            inlineSlots.Add("0x24");
        }

        return new Tf2Ps3SourceOwnerCallbackDispatchEntry(
            address,
            function.Name,
            role,
            true,
            sourceBoundary,
            dispatchArgument,
            implementationMeaning,
            evidence.Contains("owner-slot-0x14-pre-dispatch", StringComparer.Ordinal),
            evidence.Contains("owner-slot-0x18-post-dispatch", StringComparer.Ordinal),
            inlineSlots.ToArray(),
            evidence,
            ExtractCallSequence(function.Body),
            Preview(function));
    }

    private static string[] BuildEvidence(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "PTR_PTR_01977d5c", "queued-payload-global-source");
        AddIf(body, tokens, "+ 0x48))(*(int **)puVar1)", "queued-payload-object-source");
        AddIf(body, tokens, "_opd_FUN_00a5a820", "dispatch-timing-update");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x14", "owner-slot-0x14-pre-dispatch");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x18", "owner-slot-0x18-post-dispatch");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x20", "owner-slot-0x20-subpayload-complete");
        AddIf(body, tokens, "*(int *)param_1[0x782] + 0x10", "owner-slot-0x10-error");
        AddIf(body, tokens, "**(int **)(param_1 + 0x1e08) + 0xc", "owner-slot-0x0c-inline-string");
        AddIf(body, tokens, "*piVar3 + 0x1c", "owner-slot-0x1c-inline-branch");
        AddIf(body, tokens, "*piVar3 + 0x24", "owner-slot-0x24-inline-branch");
        AddIf(body, tokens, "_opd_FUN_00a579d8((int)param_1,1,param_2[0x11] + 0x1c)", "owner-payload-notify");
        AddIf(body, tokens, "_opd_FUN_00a5aa00", "slot70-optional-selector");
        AddIf(body, tokens, "_opd_FUN_00a594e8", "slot70-lane-reader");
        AddIf(body, tokens, "_opd_FUN_00a5d720", "buffered-subpayload-dispatcher");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, tokens, "_opd_FUN_00a58868", "inline-owner-branch-helper-call");
        AddIf(body, tokens, "_opd_FUN_00a58c10(param_1,(int)(param_2 + 7))", "slot70-param2-plus-7-dispatch");
        AddIf(body, tokens, "FUN_0086de68((int)&local_50", "local-bitreader-init");
        AddIf(body, tokens, "_opd_FUN_00a57f48", "registered-handler-lookup");
        AddIf(body, tokens, "*piVar11 + 0x14", "registered-handler-read");
        AddIf(body, tokens, "*piVar11 + 0x10", "registered-handler-process");
        AddIf(body, tokens, "*piVar11 + 0x24", "registered-handler-type");
        AddIf(body, tokens, "param_1[0x788] + 0x34", "post-dispatch-secondary-callback");
        AddIf(body, tokens, "param_2[0xb]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xc]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xd]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xe]", "slot70-param2-field-shape");
        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractCallSequence(string body)
    {
        var interesting = new[]
        {
            "PTR_PTR_01977d5c",
            "_opd_FUN_00a5a820",
            "param_1[0x782] + 0x14",
            "_opd_FUN_00a579d8",
            "_opd_FUN_00a5aa00",
            "_opd_FUN_00a594e8",
            "_opd_FUN_00a5d720",
            "FUN_0086de68",
            "_opd_FUN_00a58c10",
            "param_1[0x782] + 0x18",
            "param_1[0x782] + 0x20",
            "param_1 + 0x1e08",
            "+ 0xc",
            "*piVar3 + 0x1c",
            "*piVar3 + 0x24",
            "_opd_FUN_00a57f48",
            "*piVar11 + 0x14",
            "*piVar11 + 0x10",
            "param_1[0x788] + 0x34"
        };

        return body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => interesting.Any(token => line.Contains(token, StringComparison.Ordinal)))
            .Take(40)
            .ToArray();
    }

    private static bool HasAll(string[] values, params string[] needles) =>
        needles.All(needle => values.Contains(needle, StringComparer.Ordinal));

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
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

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.True;

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceOwnerCallbackDispatchReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerCallbackDispatchInputs Inputs,
    Tf2Ps3SourceOwnerCallbackDispatchSummary Summary,
    Tf2Ps3SourceOwnerCallbackDispatchEntry[] DispatchEntries,
    Tf2Ps3SourceOwnerCallbackDispatchGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerCallbackDispatchInputs(
    string CExportInput,
    string OwnerCallbackMapReport,
    string Slot70FieldContractReport,
    string MarkerlessDataflowTargetsReport);

public sealed record Tf2Ps3SourceOwnerCallbackDispatchSummary(
    int OwnerCallbackSlotCount,
    int DispatchEntryCount,
    int PresentDispatchEntryCount,
    int PreDispatchOwnerWrapperCount,
    int PostDispatchOwnerWrapperCount,
    int InlineBranchOwnerWrapperCount,
    bool QueuedWrapperSequenceRecovered,
    bool Slot70WrapperSequenceRecovered,
    bool SubpayloadSequenceRecovered,
    bool DispatcherOwnerBranchesRecovered,
    int Slot70RecoveredFieldCount,
    bool OwnerCallbackPathConstructsParam2,
    bool ConcreteMarkerlessDataflowEdgeRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceOwnerCallbackDispatchEntry(
    string Address,
    string Name,
    string Role,
    bool Present,
    string SourceBoundary,
    string DispatchArgument,
    string ImplementationMeaning,
    bool PreDispatchOwnerSlot,
    bool PostDispatchOwnerSlot,
    string[] InlineBranchOwnerSlots,
    string[] EvidenceTokens,
    string[] CallSequence,
    string Preview);

public sealed record Tf2Ps3SourceOwnerCallbackDispatchGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
