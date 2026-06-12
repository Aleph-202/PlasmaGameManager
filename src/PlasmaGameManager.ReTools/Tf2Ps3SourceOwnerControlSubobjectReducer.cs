using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerControlSubobjectReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008722a0",
        "00876bb8",
        "00876d78",
        "00a52720",
        "00a53320",
        "00a54d80",
        "00a55108",
        "00a556b0",
        "00a55d38",
        "00a578c8",
        "00a58418"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceOwnerControlSubobjectReport> ReduceAsync(
        string cExportPath,
        string ownerVtablePath,
        string playerVtablePath,
        string payloadObjectDispatchPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        using var ownerVtableDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerVtablePath));
        using var playerVtableDoc = JsonDocument.Parse(await File.ReadAllTextAsync(playerVtablePath));
        using var payloadObjectDoc = JsonDocument.Parse(await File.ReadAllTextAsync(payloadObjectDispatchPath));

        var ownerTable = FindOwnerTable(ownerVtableDoc.RootElement, "0x0180c81c");
        var ownerSubobjectVptrSlot = FindSlot(ownerTable, "0x000000ec");
        var ownerSlot8 = FindSlot(ownerTable, "0x000000f4");
        var ownerThirdVptrSlot = FindSlot(ownerTable, "0x00000100");
        var associatedSlot90 = FindSlot(playerVtableDoc.RootElement, "0x00000090");
        var associatedSlotAc = FindSlot(playerVtableDoc.RootElement, "0x000000ac");
        var associatedSlotB4 = FindSlot(playerVtableDoc.RootElement, "0x000000b4");
        var associatedSlotC0 = FindSlot(playerVtableDoc.RootElement, "0x000000c0");

        var payloadSummary = payloadObjectDoc.RootElement.GetProperty("Summary");
        var payloadObjectDispatchRecovered =
            ReadBool(payloadSummary, "OwnerBitreaderDispatchRecovered")
            && ReadBool(payloadSummary, "AssociatedSlot90DispatchRecovered")
            && ReadBool(payloadSummary, "OwnerSubobjectArgumentRecovered")
            && ReadBool(payloadSummary, "PayloadFirstWordDispatchRecovered");

        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var initializer = byAddress.GetValueOrDefault("00a53320");
        var constructor = byAddress.GetValueOrDefault("00876d78");
        var updateCaller = byAddress.GetValueOrDefault("00876bb8");
        var ownerSlot8Thunk = byAddress.GetValueOrDefault("00a55d38");
        var ownerSlot8Forwarder = byAddress.GetValueOrDefault("00a52720");
        var associatedSlot90Handler = byAddress.GetValueOrDefault("00a58418");
        var associatedSlotAcSetter = byAddress.GetValueOrDefault("00a578c8");

        var ownerInitializerRecovered =
            initializer?.EvidenceTokens.Contains("owner-table-base-ptr-ptr-01977c14", StringComparer.Ordinal) == true
            && initializer.EvidenceTokens.Contains("owner-subobject-vptr-plus-0xec", StringComparer.Ordinal)
            && initializer.EvidenceTokens.Contains("owner-third-subobject-vptr-plus-0x100", StringComparer.Ordinal);
        var ownerConstructorRecovered =
            constructor?.EvidenceTokens.Contains("owner-control-subobject-init-param1-plus-0x68", StringComparer.Ordinal) == true
            && constructor.EvidenceTokens.Contains("source-runtime-object-init-param1-plus-0x12f0", StringComparer.Ordinal);
        var ownerDispatchCallerRecovered =
            updateCaller?.EvidenceTokens.Contains("payload-object-dispatch-owner-param1-plus-0x69", StringComparer.Ordinal) == true
            && updateCaller.EvidenceTokens.Contains("post-dispatch-owner-update-00a55108", StringComparer.Ordinal)
            && updateCaller.EvidenceTokens.Contains("post-dispatch-owner-timer-00a556b0", StringComparer.Ordinal);
        var ownerSlot8TargetRecovered =
            ownerSlot8.FunctionAddress == "00a55d38"
            && ownerSlot8Thunk?.EvidenceTokens.Contains("owner-slot8-thunk-to-00a52720", StringComparer.Ordinal) == true;
        var ownerSlot8ForwarderRecovered =
            ownerSlot8Forwarder?.EvidenceTokens.Contains("payload-bit-count-param2-5", StringComparer.Ordinal) == true
            && ownerSlot8Forwarder.EvidenceTokens.Contains("payload-byte-copy-param2-plus-6", StringComparer.Ordinal)
            && ownerSlot8Forwarder.EvidenceTokens.Contains("payload-bitreader-reset-param2-plus-0xf", StringComparer.Ordinal)
            && ownerSlot8Forwarder.EvidenceTokens.Contains("owner-forward-to-008722a0", StringComparer.Ordinal);
        var associatedSlot90CandidateRecovered =
            associatedSlot90.FunctionAddress == "00a58418"
            && associatedSlot90Handler?.EvidenceTokens.Contains("associated-slot90-state-reset-param1-plus-0x11", StringComparer.Ordinal) == true
            && associatedSlot90Handler.EvidenceTokens.Contains("associated-slot90-calls-slot-ac", StringComparer.Ordinal);
        var associatedSlotAcSetterRecovered =
            associatedSlotAc.FunctionAddress == "00a578c8"
            && associatedSlotAcSetter?.EvidenceTokens.Contains("associated-slot-ac-state-field-0x08", StringComparer.Ordinal) == true
            && associatedSlotAcSetter.EvidenceTokens.Contains("associated-slot-ac-state-field-0x0c", StringComparer.Ordinal)
            && associatedSlotAcSetter.EvidenceTokens.Contains("associated-slot-ac-state-field-0x10", StringComparer.Ordinal);

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "payload-object-dispatch-upstream-recovered",
                payloadObjectDispatchRecovered ? "proven" : "missing",
                payloadObjectDispatchPath,
                "The upstream payload-object report must prove the first-word split, owner argument, and associated/owner dispatch branches."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "owner-control-subobject-layout-recovered",
                ownerInitializerRecovered && ownerConstructorRecovered ? "proven" : "missing",
                "00876d78 -> 00a53320(param_1 + 0x68)",
                "TF.elf initializes the owner/control subobject at +0x68, then uses +0x69 as a vptr-adjusted subobject for owner-control dispatch."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "owner-control-slot8-target-recovered",
                ownerSlot8TargetRecovered ? "proven" : "missing",
                "owner table 0x0180c81c + 0xf4 -> 00a55d38 -> 00a52720",
                "The -1 owner-control branch's virtual +0x08 target is concrete for the +0x69 subobject."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "owner-control-slot8-forwarder-recovered",
                ownerSlot8ForwarderRecovered ? "proven" : "missing",
                "00a52720",
                "The owner +0x08 consumer copies param_2 bit payload, rebuilds the bitreader at param_2 + 0xf, and forwards to 008722a0."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "associated-slot90-helper-candidate-recovered",
                associatedSlot90CandidateRecovered && associatedSlotAcSetterRecovered ? "candidate" : "missing",
                "source player/helper vtable 0x0180c9c0 + 0x90 -> 00a58418",
                "The associated-object +0x90 slot candidate resets state then calls +0xac. It is not yet enough to decode all map-load semantics."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "native-owner-slot8-consumer-implemented",
                "missing",
                "server implementation gate",
                "The native replacement still needs to decode the 00a52720/008722a0 owner-control payload fields and respond semantically."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "native-associated-slot90-consumer-implemented",
                "missing",
                "server implementation gate",
                "The native replacement still needs to implement the associated-object +0x90 consumer semantics, not just identify the slot."),
            new Tf2Ps3SourceOwnerControlSubobjectGate(
                "native-source-input-ready",
                "missing",
                "server/live verification gate",
                "This remains incomplete until owner/control and associated-object consumers decode live/PCAP uploads and the client reaches map load.")
        };

        var report = new Tf2Ps3SourceOwnerControlSubobjectReport(
            "tf2ps3-source-owner-control-subobject-map",
            "Resolves TF.elf owner/control subobject layout and the concrete owner vtable +0x08 target used by -1 payload-object dispatch, while keeping associated +0x90 as a candidate until native semantics are implemented.",
            new Tf2Ps3SourceOwnerControlSubobjectInputs(cExportPath, ownerVtablePath, playerVtablePath, payloadObjectDispatchPath),
            new Tf2Ps3SourceOwnerControlSubobjectSummary(
                TargetAddresses.Length,
                functions.Length,
                payloadObjectDispatchRecovered,
                ownerInitializerRecovered,
                ownerConstructorRecovered,
                ownerDispatchCallerRecovered,
                ownerSlot8TargetRecovered,
                ownerSlot8ForwarderRecovered,
                associatedSlot90CandidateRecovered,
                associatedSlotAcSetterRecovered,
                false,
                false,
                false,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            new Tf2Ps3SourceOwnerControlSubobjectSlotMap(
                ownerTable.BaseAddress,
                ownerSubobjectVptrSlot.TableAddress,
                ownerSubobjectVptrSlot.SlotOffset,
                ownerSubobjectVptrSlot.FunctionAddress,
                ownerSlot8.TableAddress,
                ownerSlot8.SlotOffset,
                ownerSlot8.FunctionAddress,
                "00a52720",
                ownerThirdVptrSlot.TableAddress,
                ownerThirdVptrSlot.SlotOffset,
                ownerThirdVptrSlot.FunctionAddress,
                associatedSlot90.TableAddress,
                associatedSlot90.SlotOffset,
                associatedSlot90.FunctionAddress,
                associatedSlotAc.TableAddress,
                associatedSlotAc.SlotOffset,
                associatedSlotAc.FunctionAddress,
                associatedSlotB4.TableAddress,
                associatedSlotB4.SlotOffset,
                associatedSlotB4.FunctionAddress,
                associatedSlotC0.TableAddress,
                associatedSlotC0.SlotOffset,
                associatedSlotC0.FunctionAddress),
            functions,
            gates,
            [
                "The owner/control subobject route is now concrete: 00876d78 initializes param_1 + 0x68 through 00a53320, 00876bb8 passes param_1 + 0x69 into 008be1e8, and the +0x69 subobject's virtual +0x08 slot resolves to 00a55d38 -> 00a52720.",
                "00a52720 treats param_2[5] as the bit count, copies bits from param_2 + 6, rebuilds the bitreader at param_2 + 0xf, then forwards the payload object to 008722a0 through *(param_1 + 0x4a18).",
                "The associated-object +0x90 handler candidate from the player/helper vtable slice resolves to 00a58418, which resets param_1 + 0x11 state and calls virtual +0xac; that candidate still needs constructor-level proof and field-level implementation.",
                "This report does not make the server native-complete. It narrows the next implementation target to 00a52720/008722a0 owner-control payload semantics and the associated-object +0x90 consumer."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerControlSubobjectFunction BuildFunction(ExportedFunction function) =>
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
            "008722a0" => "owner-control-forward-target",
            "00876bb8" => "payload-object-live-update-caller",
            "00876d78" => "source-runtime-owner-constructor",
            "00a52720" => "owner-control-slot8-payload-forwarder",
            "00a53320" => "owner-control-subobject-vptr-initializer",
            "00a54d80" => "owner-post-dispatch-update-helper",
            "00a55108" => "owner-post-dispatch-update-entry",
            "00a556b0" => "owner-post-dispatch-timer-state-entry",
            "00a55d38" => "owner-control-subobject-slot8-thunk",
            "00a578c8" => "associated-slot-ac-state-setter",
            "00a58418" => "associated-slot90-helper-candidate",
            _ => "source-owner-control-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "param_1 + 0x69", "payload-object-dispatch-owner-param1-plus-0x69");
        AddIf(body, tokens, "param_1[0x6b]", "payload-object-dispatch-connection-id-param1-plus-0x6b");
        AddIf(body, tokens, "_opd_FUN_008be1e8(param_1[0x6b],(int *)(param_1 + 0x69)", "payload-object-dispatch-call");
        AddIf(body, tokens, "_opd_FUN_00a55108((int *)(param_1 + 0x68))", "post-dispatch-owner-update-00a55108");
        AddIf(body, tokens, "_opd_FUN_00a556b0((int)(param_1 + 0x68))", "post-dispatch-owner-timer-00a556b0");
        AddIf(body, tokens, "_opd_FUN_00a56760(param_1 + 0x12f0", "source-runtime-object-init-param1-plus-0x12f0");
        AddIf(body, tokens, "_opd_FUN_00a53320(param_1 + 0x68)", "owner-control-subobject-init-param1-plus-0x68");
        AddIf(body, tokens, "PTR_PTR_01977c14", "owner-table-base-ptr-ptr-01977c14");
        AddIf(body, tokens, "PTR_PTR_01977c14 + 0xec", "owner-subobject-vptr-plus-0xec");
        AddIf(body, tokens, "PTR_PTR_01977c14 + 0x100", "owner-third-subobject-vptr-plus-0x100");
        AddIf(body, tokens, "param_1[1] = puVar2", "owner-subobject-vptr-store-param1-1");
        AddIf(body, tokens, "param_1[2] = PTR_PTR_01977c14 + 0x100", "owner-third-subobject-vptr-store-param1-2");
        AddIf(body, tokens, "FUN_0086ec18(param_1)", "owner-initializer-calls-0086ec18");
        AddIf(body, tokens, "_opd_FUN_00a52720(param_1 + -8,param_2)", "owner-slot8-thunk-to-00a52720");
        AddIf(body, tokens, "iVar1 = param_2[5]", "payload-bit-count-param2-5");
        AddIf(body, tokens, "FUN_0086acb8((int)(param_2 + 6)", "payload-byte-copy-param2-plus-6");
        AddIf(body, tokens, "FUN_00870968(param_2 + 0xf", "payload-bitreader-reset-param2-plus-0xf");
        AddIf(body, tokens, "_opd_FUN_008722a0(*(int *)(param_1 + 0x4a18),param_2)", "owner-forward-to-008722a0");
        AddIf(body, tokens, "FUN_0086ff38((int)(param_1 + 0x11))", "associated-slot90-state-reset-param1-plus-0x11");
        AddIf(body, tokens, "*param_1 + 0xac", "associated-slot90-calls-slot-ac");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 8) = param_2", "associated-slot-ac-state-field-0x08");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 0xc) = param_3", "associated-slot-ac-state-field-0x0c");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 0x10) = param_4", "associated-slot-ac-state-field-0x10");
        AddIf(body, tokens, "param_2[5]", "payload-object-field-param2-5");
        AddIf(body, tokens, "param_2 + 6", "payload-object-field-param2-plus-6");
        AddIf(body, tokens, "param_2 + 0xf", "payload-object-field-param2-plus-0xf");

        foreach (var token in new[]
        {
            "_opd_FUN_008722a0",
            "_opd_FUN_008be1e8",
            "_opd_FUN_00a52720",
            "_opd_FUN_00a53320",
            "_opd_FUN_00a55108",
            "_opd_FUN_00a556b0",
            "FUN_0086acb8",
            "FUN_00870968",
            "FUN_0086ff38"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                tokens.Add(token);
            }
        }

        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static OwnerTable FindOwnerTable(JsonElement root, string baseAddress)
    {
        foreach (var table in root.GetProperty("Tables").EnumerateArray())
        {
            if (ReadString(table, "BaseAddress").Equals(baseAddress, StringComparison.OrdinalIgnoreCase))
            {
                return new OwnerTable(ReadString(table, "BaseAddress"), table);
            }
        }

        throw new InvalidOperationException($"Owner vtable {baseAddress} not found.");
    }

    private static Tf2Ps3SourceOwnerControlSubobjectSlot FindSlot(OwnerTable table, string slotOffset) =>
        FindSlot(table.Element, slotOffset);

    private static Tf2Ps3SourceOwnerControlSubobjectSlot FindSlot(JsonElement root, string slotOffset)
    {
        var slots = root.TryGetProperty("Slots", out var directSlots)
            ? directSlots.EnumerateArray()
            : root.GetProperty("Tables").EnumerateArray().SelectMany(static table => table.GetProperty("Slots").EnumerateArray());

        foreach (var slot in slots)
        {
            if (ReadString(slot, "SlotOffset").Equals(slotOffset, StringComparison.OrdinalIgnoreCase))
            {
                return new Tf2Ps3SourceOwnerControlSubobjectSlot(
                    ReadInt(slot, "SlotIndex"),
                    ReadString(slot, "SlotOffset"),
                    ReadString(slot, "TableAddress"),
                    ReadString(slot, "Kind"),
                    ReadString(slot, "OpdAddress"),
                    ReadString(slot, "FunctionAddress"),
                    ReadString(slot, "Role"));
            }
        }

        throw new InvalidOperationException($"Slot {slotOffset} not found.");
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

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

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

    private sealed record OwnerTable(
        string BaseAddress,
        JsonElement Element);

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceOwnerControlSubobjectReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerControlSubobjectInputs Inputs,
    Tf2Ps3SourceOwnerControlSubobjectSummary Summary,
    Tf2Ps3SourceOwnerControlSubobjectSlotMap SlotMap,
    Tf2Ps3SourceOwnerControlSubobjectFunction[] Functions,
    Tf2Ps3SourceOwnerControlSubobjectGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerControlSubobjectInputs(
    string CExport,
    string OwnerVtableMap,
    string PlayerVtableMap,
    string PayloadObjectDispatchMap);

public sealed record Tf2Ps3SourceOwnerControlSubobjectSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool PayloadObjectDispatchRecovered,
    bool OwnerInitializerRecovered,
    bool OwnerConstructorRecovered,
    bool OwnerDispatchCallerRecovered,
    bool OwnerSlot8TargetRecovered,
    bool OwnerSlot8ForwarderRecovered,
    bool AssociatedSlot90CandidateRecovered,
    bool AssociatedSlotAcSetterRecovered,
    bool NativeOwnerSlot8ConsumerImplemented,
    bool NativeAssociatedSlot90ConsumerImplemented,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceOwnerControlSubobjectSlotMap(
    string OwnerTableBase,
    string OwnerSubobjectVptrTableAddress,
    string OwnerSubobjectVptrOffset,
    string OwnerSubobjectVptrFunction,
    string OwnerSlot8TableAddress,
    string OwnerSlot8Offset,
    string OwnerSlot8Function,
    string OwnerSlot8ThunkTarget,
    string OwnerThirdSubobjectVptrTableAddress,
    string OwnerThirdSubobjectVptrOffset,
    string OwnerThirdSubobjectVptrFunction,
    string AssociatedSlot90TableAddress,
    string AssociatedSlot90Offset,
    string AssociatedSlot90Function,
    string AssociatedSlotAcTableAddress,
    string AssociatedSlotAcOffset,
    string AssociatedSlotAcFunction,
    string AssociatedSlotB4TableAddress,
    string AssociatedSlotB4Offset,
    string AssociatedSlotB4Function,
    string AssociatedSlotC0TableAddress,
    string AssociatedSlotC0Offset,
    string AssociatedSlotC0Function);

public sealed record Tf2Ps3SourceOwnerControlSubobjectFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceOwnerControlSubobjectSlot(
    int SlotIndex,
    string SlotOffset,
    string TableAddress,
    string Kind,
    string OpdAddress,
    string FunctionAddress,
    string Role);

public sealed record Tf2Ps3SourceOwnerControlSubobjectGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
