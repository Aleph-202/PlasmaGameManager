using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHelperSliceReceiveSiblingReducer
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

    private static readonly string[] SiblingFunctions =
    [
        "00a5b6c0",
        "00a5b4f0",
        "00a5c2e8",
        "00a5d9e0"
    ];

    public static async Task<Tf2Ps3SourceHelperSliceReceiveSiblingReport> ReduceAsync(
        string cExportPath,
        string helperSliceContractPath,
        string slot70Param2BuilderPath,
        string ownerCallbackDispatchPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);
        using var helperSlice = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceContractPath));
        using var slot70Builder = JsonDocument.Parse(await File.ReadAllTextAsync(slot70Param2BuilderPath));
        using var ownerDispatch = JsonDocument.Parse(await File.ReadAllTextAsync(ownerCallbackDispatchPath));

        var entries = SiblingFunctions
            .Select(function => BuildEntry(functions, helperSlice.RootElement, function))
            .ToArray();

        var attach = entries.Single(static entry => entry.FunctionAddress == "00a5b6c0");
        var queued = entries.Single(static entry => entry.FunctionAddress == "00a5b4f0");
        var attached = entries.Single(static entry => entry.FunctionAddress == "00a5c2e8");
        var slot70 = entries.Single(static entry => entry.FunctionAddress == "00a5d9e0");

        var receiveSlotsPinned = entries.All(static entry => entry.Present && entry.TableAddress.Length > 0);
        var attachedSocketPathProven =
            HasAll(attach.EvidenceTokens, "connects-and-stores-object-socket-0x90", "resets-attached-reader-slot-0x6c")
            && HasAll(attached.EvidenceTokens, "reads-object-socket-0x90", "socket-recv-wrapper", "attached-frame-kind-state");
        var attachedType2DispatchProven =
            HasAll(attached.EvidenceTokens, "type2-six-byte-header", "staged-payload-buffer-0x544", "local-bitreader-init", "payload-dispatcher-call");
        var queuedDispatchProven =
            HasAll(queued.EvidenceTokens, "global-queued-payload-source", "queued-payload-dispatch-argument", "payload-dispatcher-call");
        var slot70ConsumesCallerBitreader =
            HasAll(slot70.EvidenceTokens, "slot70-param2-plus-7-dispatch", "slot70-param2-field-shape", "payload-dispatcher-call");
        var slot70ReadsAttachedSocket =
            slot70.EvidenceTokens.Contains("reads-object-socket-0x90", StringComparer.Ordinal)
            || slot70.EvidenceTokens.Contains("socket-recv-wrapper", StringComparer.Ordinal);
        var siblingPathConstructsParam2 =
            entries.Any(static entry => entry.EvidenceTokens.Contains("slot70-param2-construction-site", StringComparer.Ordinal));
        var slot70BuilderSummary = slot70Builder.RootElement.GetProperty("Summary");
        var ownerDispatchSummary = ownerDispatch.RootElement.GetProperty("Summary");
        var concreteMarkerlessEdgeRecovered =
            siblingPathConstructsParam2
            || ReadBool(slot70BuilderSummary, "Param2ConstructionSiteRecovered")
            || ReadBool(ownerDispatchSummary, "ConcreteMarkerlessDataflowEdgeRecovered");
        var nativeSourceInputReady =
            concreteMarkerlessEdgeRecovered
            && ReadBool(slot70BuilderSummary, "NativeSourceInputReady")
            && ReadBool(ownerDispatchSummary, "NativeSourceInputReady");

        var gates = new[]
        {
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "helper-receive-sibling-slots-pinned",
                receiveSlotsPinned ? "proven" : "missing",
                helperSliceContractPath,
                "The helper slice contains the attach, queued dispatch, attached-frame dispatch, and slot +0x70 bitreader siblings."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "attached-socket-visible-frame-path",
                attachedSocketPathProven ? "proven" : "missing",
                "00a5b6c0 -> 00a5c2e8",
                "The visible connected path stores a socket at object +0x90 and reads framed records from that same socket."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "attached-type2-dispatches-native-messages",
                attachedType2DispatchProven ? "proven" : "missing",
                "00a5c2e8",
                "Attached frame kind 2 reads a six-byte length/token header, stages payload bytes, and dispatches a local bitreader to 00a58c10."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "queued-drain-dispatches-native-messages",
                queuedDispatchProven ? "proven" : "missing",
                "00a5b4f0",
                "The queued sibling drains payload objects from PTR_PTR_01977d5c and dispatches payload +0x1c to 00a58c10."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "slot70-sibling-consumes-caller-bitreader",
                slot70ConsumesCallerBitreader ? "proven" : "missing",
                "00a5d9e0",
                "Slot +0x70 is a downstream bitreader consumer: it dispatches param_2 + 7 and reads the known param_2 field contract."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "slot70-sibling-is-not-attached-socket-reader",
                !slot70ReadsAttachedSocket ? "proven-negative" : "needs-review",
                "00a5d9e0",
                "Slot +0x70 does not read object socket +0x90 and is not the visible attached-frame reader."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "sibling-path-builds-slot70-param2",
                siblingPathConstructsParam2 ? "proven" : "missing",
                "helper-slice receive siblings",
                "None of the visible helper-slice siblings construct the caller-side param_2 object for 00a5d9e0."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "concrete-markerless-dataflow-edge",
                concreteMarkerlessEdgeRecovered ? "proven" : "missing",
                $"{slot70Param2BuilderPath}; {ownerCallbackDispatchPath}",
                "The markerless client packet body still needs a concrete edge into a native bitreader or attached-frame path."),
            new Tf2Ps3SourceHelperSliceReceiveSiblingGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "Native Source input remains incomplete until the markerless wrapper edge is recovered and implemented.")
        };

        var report = new Tf2Ps3SourceHelperSliceReceiveSiblingReport(
            "tf2ps3-source-helper-slice-receive-siblings",
            "Classifies the receive-adjacent helper-slice siblings so the remaining markerless input target stays on the missing wrapper edge, not the already-proven attached-frame reader.",
            new Tf2Ps3SourceHelperSliceReceiveSiblingInputs(
                cExportPath,
                helperSliceContractPath,
                slot70Param2BuilderPath,
                ownerCallbackDispatchPath),
            new Tf2Ps3SourceHelperSliceReceiveSiblingSummary(
                entries.Length,
                entries.Count(static entry => entry.Present),
                attach.FunctionAddress,
                queued.FunctionAddress,
                attached.FunctionAddress,
                slot70.FunctionAddress,
                receiveSlotsPinned,
                attachedSocketPathProven,
                attachedType2DispatchProven,
                queuedDispatchProven,
                slot70ConsumesCallerBitreader,
                slot70ReadsAttachedSocket,
                siblingPathConstructsParam2,
                concreteMarkerlessEdgeRecovered,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            entries,
            gates,
            [
                "00a5b6c0 and 00a5c2e8 explain the visible attached-frame path: connect/store object socket +0x90, read frame kind, then type-2 length/token/payload into 00a58c10.",
                "00a5b4f0 explains queued/staged payload drain through payload +0x1c.",
                "00a5d9e0 remains the slot +0x70 bitreader consumer and does not itself read the attached socket or construct param_2.",
                "The remaining native target is now one layer earlier than these siblings: the wrapper/dataflow that turns hard markerless client datagrams into either an attached-frame stream or the slot +0x70 param_2 bitreader."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceHelperSliceReceiveSiblingEntry BuildEntry(
        IReadOnlyDictionary<string, ExportedFunction> functions,
        JsonElement helperSlice,
        string address)
    {
        var slot = FindSlot(helperSlice, address);
        if (!functions.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceHelperSliceReceiveSiblingEntry(
                address,
                "",
                ReadString(slot, "Role"),
                false,
                ReadString(slot, "TableAddress"),
                ReadString(slot, "OpdAddress"),
                ReadString(slot, "VisibleSliceOffset"),
                ReadString(slot, "FullSliceOffset"),
                [],
                [],
                [],
                "");
        }

        return new Tf2Ps3SourceHelperSliceReceiveSiblingEntry(
            address,
            function.Name,
            RoleFor(address, ReadString(slot, "Role")),
            true,
            ReadString(slot, "TableAddress"),
            ReadString(slot, "OpdAddress"),
            ReadString(slot, "VisibleSliceOffset"),
            ReadString(slot, "FullSliceOffset"),
            BuildEvidence(function.Body, address),
            ExtractFieldTokens(function.Body),
            ExtractCallSequence(function.Body),
            Preview(function));
    }

    private static string[] BuildEvidence(string body, string address)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "_opd_FUN_008b8e70", "socket-connect-wrapper");
        AddIf(body, tokens, "param_1[0x24] = iVar2", "connects-and-stores-object-socket-0x90");
        AddIf(body, tokens, "param_1[0x24] == 0", "reads-object-socket-0x90");
        AddIf(body, tokens, "_opd_FUN_008b82c0(param_1[0x24]", "socket-recv-wrapper");
        AddIf(body, tokens, "param_1[0x10c]", "attached-frame-kind-state");
        AddIf(body, tokens, "param_1[0x10d]", "attached-frame-token-field");
        AddIf(body, tokens, "param_1[0x10e]", "attached-frame-length-field");
        AddIf(body, tokens, "param_1[0x10f]", "attached-frame-progress-field");
        AddIf(body, tokens, "param_1[0x151]", "staged-payload-buffer-0x544");
        AddIf(body, tokens, "auStack_234,6,0", "type2-six-byte-header");
        AddIf(body, tokens, "FUN_0086de68((int)&local_258", "local-bitreader-init");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, tokens, "_opd_FUN_00a584d0", "attached-state-advance");
        AddIf(body, tokens, "_opd_FUN_00a5a550", "token-sync-success-handler");
        AddIf(body, tokens, "PTR_PTR_01977d5c", "global-queued-payload-source");
        AddIf(body, tokens, "iVar2 + 0x1c", "queued-payload-dispatch-argument");
        AddIf(body, tokens, "param_2 + 7", "slot70-param2-plus-7-dispatch");
        AddIf(body, tokens, "param_2[0xb]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xc]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xd]", "slot70-param2-field-shape");
        AddIf(body, tokens, "param_2[0xe]", "slot70-param2-field-shape");
        AddIf(body, tokens, "_opd_FUN_00a5d720", "slot70-buffered-subpayload-dispatcher");
        AddIf(body, tokens, "(*(code *)**(undefined4 **)(*param_1 + 0x6c))(param_1)", "resets-attached-reader-slot-0x6c");

        if (address == "00a5d9e0"
            && body.Contains("FUN_0086de68", StringComparison.Ordinal)
            && body.Contains("param_2", StringComparison.Ordinal))
        {
            tokens.Add("slot70-param2-construction-site");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
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
            "param_1[0x780]",
            "param_2 + 7",
            "param_2[0xb]",
            "param_2[0xc]",
            "param_2[0xd]",
            "param_2[0xe]",
            "param_2[0x11]"
        };

        return tokens
            .Where(token => body.Contains(token, StringComparison.Ordinal))
            .ToArray();
    }

    private static string[] ExtractCallSequence(string body)
    {
        var interesting = new[]
        {
            "_opd_FUN_008b8e70",
            "_opd_FUN_008b82c0",
            "FUN_0086de68",
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a584d0",
            "_opd_FUN_00a5a550",
            "_opd_FUN_00a5d720",
            "PTR_PTR_01977d5c",
            "param_1[0x24]",
            "param_1[0x10c]",
            "param_1[0x10e]",
            "param_1[0x151]",
            "param_2 + 7"
        };

        return body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => interesting.Any(token => line.Contains(token, StringComparison.Ordinal)))
            .Take(36)
            .ToArray();
    }

    private static JsonElement FindSlot(JsonElement helperSlice, string functionAddress)
    {
        foreach (var slot in helperSlice.GetProperty("Slots").EnumerateArray())
        {
            if (ReadString(slot, "FunctionAddress") == functionAddress)
            {
                return slot;
            }
        }

        return default;
    }

    private static string RoleFor(string address, string fallback) => address switch
    {
        "00a5b6c0" => "peer-attach-connected-socket",
        "00a5b4f0" => "queued-payload-dispatch-sibling",
        "00a5c2e8" => "attached-frame-reader-sibling",
        "00a5d9e0" => "slot70-caller-bitreader-consumer-sibling",
        _ => fallback
    };

    private static bool HasAll(string[] values, params string[] needles) =>
        needles.All(needle => values.Contains(needle, StringComparer.Ordinal));

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Undefined
            || !element.TryGetProperty(propertyName, out var property))
        {
            return "";
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : property.ToString();
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.True;

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
        return text.Length <= 3200 ? text : text[..3200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceHelperSliceReceiveSiblingReport(
    string Status,
    string Note,
    Tf2Ps3SourceHelperSliceReceiveSiblingInputs Inputs,
    Tf2Ps3SourceHelperSliceReceiveSiblingSummary Summary,
    Tf2Ps3SourceHelperSliceReceiveSiblingEntry[] Siblings,
    Tf2Ps3SourceHelperSliceReceiveSiblingGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceHelperSliceReceiveSiblingInputs(
    string CExportInput,
    string HelperSliceContractReport,
    string Slot70Param2BuilderReport,
    string OwnerCallbackDispatchReport);

public sealed record Tf2Ps3SourceHelperSliceReceiveSiblingSummary(
    int ReceiveSiblingCount,
    int PresentReceiveSiblingCount,
    string AttachSocketFunction,
    string QueuedPayloadFunction,
    string AttachedFrameReaderFunction,
    string Slot70BitreaderConsumerFunction,
    bool HelperReceiveSiblingSlotsPinned,
    bool AttachedSocketPathProven,
    bool AttachedType2DispatchProven,
    bool QueuedDispatchProven,
    bool Slot70ConsumesCallerBitreader,
    bool Slot70ReadsAttachedSocket,
    bool SiblingPathConstructsParam2,
    bool ConcreteMarkerlessDataflowEdgeRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceHelperSliceReceiveSiblingEntry(
    string FunctionAddress,
    string FunctionName,
    string Role,
    bool Present,
    string TableAddress,
    string OpdAddress,
    string VisibleSliceOffset,
    string FullSliceOffset,
    string[] EvidenceTokens,
    string[] FieldTokens,
    string[] CallSequence,
    string Preview);

public sealed record Tf2Ps3SourceHelperSliceReceiveSiblingGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
