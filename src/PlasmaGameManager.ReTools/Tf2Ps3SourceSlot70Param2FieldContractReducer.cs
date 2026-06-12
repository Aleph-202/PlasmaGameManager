using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceSlot70Param2FieldContractReducer
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

    public static async Task<Tf2Ps3SourceSlot70Param2FieldContractReport> ReduceAsync(
        string cExportPath,
        string slot70Param2BuilderPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        using var slot70Builder = JsonDocument.Parse(await File.ReadAllTextAsync(slot70Param2BuilderPath));
        var slot70Function = functions.Single(static function => function.Address == "00a5d9e0");
        var body = slot70Function.Body;
        var requiredEvidence = BuildRequiredEvidence(body);
        var fields = BuildFields(body);
        var operations = BuildOperations(body);
        var helpers = BuildHelpers(body);
        var gates = BuildGates(slot70Builder.RootElement, requiredEvidence, fields, operations);

        var report = new Tf2Ps3SourceSlot70Param2FieldContractReport(
            "tf2ps3-source-slot70-param2-field-contract",
            "Names the field-level contract consumed by TF.elf slot +0x70 wrapper 00a5d9e0. This is the callee-side bitreader contract; the caller-side param_2 construction site remains tracked separately.",
            new Tf2Ps3SourceSlot70Param2FieldContractInputs(cExportPath, slot70Param2BuilderPath),
            new Tf2Ps3SourceSlot70Param2FieldContractSummary(
                "00a5d9e0",
                fields.Length,
                fields.Count(static field => field.Status == "recovered"),
                operations.Length,
                operations.Count(static operation => operation.Status == "recovered"),
                helpers.Length,
                requiredEvidence.Count(static item => item.Found),
                requiredEvidence.Count(static item => !item.Found),
                ReadBool(slot70Builder.RootElement.GetProperty("Summary"), "Param2ConstructionSiteRecovered"),
                false,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            new Tf2Ps3SourceSlot70Param2Function(
                slot70Function.Address,
                slot70Function.Name,
                "player/helper vtable slot +0x70",
                "Consumes caller-supplied bitreader object, optional two-channel subpayload controls, then dispatches remaining bits through the native Source message dispatcher.",
                Preview(slot70Function)),
            fields,
            operations,
            helpers,
            requiredEvidence,
            gates,
            [
                "The incoming object is a bitreader-like structure whose active read view starts at param_2 + 7.",
                "The wrapper reads big-endian 32-bit words from [0x34] until [0x38], byte-swaps each word into the current-word cache at [0x2c], and tracks remaining cached bits at [0x30].",
                "If the optional selector is present, the wrapper reads two per-channel bits, calls 00a594e8 for set bits, toggles param_1[6] using the selected channel mask, and then invokes 00a5d720 for both buffered subpayload lanes.",
                "Only after optional subpayload handling does the wrapper compute remaining unread bits and call 00a58c10(param_1, param_2 + 7).",
                "The server can implement this callee-side field contract, but native client upload is still not complete until the caller-side builder that fills these fields is recovered."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceSlot70RequiredEvidence[] BuildRequiredEvidence(string body)
    {
        return
        [
            Evidence("state-compatible-gate", "FUN_0086d848(param_1 + 0x26)", body),
            Evidence("incoming-state-compare", "FUN_0086fb58(param_2,param_1 + 0x26,0)", body),
            Evidence("owner-payload-notify", "_opd_FUN_00a579d8((int)param_1,1,param_2[0x11] + 0x1c)", body),
            Evidence("optional-selector", "_opd_FUN_00a5aa00((int)param_1,(int)param_2)", body),
            Evidence("optional-bit-handler", "_opd_FUN_00a594e8((int)param_1,(int)(param_2 + 7),iVar16)", body),
            Evidence("buffered-subpayload-dispatch", "_opd_FUN_00a5d720(param_1,iVar16", body),
            Evidence("final-source-dispatch", "_opd_FUN_00a58c10(param_1,(int)(param_2 + 7))", body),
            Evidence("current-word-field", "param_2[0xb]", body),
            Evidence("cached-bit-count-field", "param_2[0xc]", body),
            Evidence("cursor-field", "param_2[0xd]", body),
            Evidence("end-field", "param_2[0xe]", body),
            Evidence("base-field", "param_2[0xf]", body),
            Evidence("total-bits-field", "param_2[9]", body),
            Evidence("initial-bit-offset-field", "param_2[10]", body),
            Evidence("owner-payload-field", "param_2[0x11]", body),
            Evidence("error-flag-field", "*(undefined1 *)(param_2 + 8)", body),
            Evidence("byte-swap-word-load", "uVar4 >> 0x18 | uVar4 >> 8 & 0xff00", body),
            Evidence("remaining-bit-count", "(0x20 - param_2[0xc]) + (param_2[10] & 3U) * 8", body)
        ];
    }

    private static Tf2Ps3SourceSlot70RequiredEvidence Evidence(string id, string token, string body) =>
        new(id, token, body.Contains(token, StringComparison.Ordinal));

    private static Tf2Ps3SourceSlot70Param2Field[] BuildFields(string body)
    {
        return
        [
            Field("+0x1c", "param_2 + 7", "bitreader-dispatch-view", "pointer", "Start of the bitreader view passed to optional bit handlers and the final Source message dispatcher.", "recovered", body),
            Field("+0x20", "*(undefined1 *)(param_2 + 8)", "overflow-or-error-flag", "byte", "Set when the cursor reaches or passes the end pointer; prevents trusting further cached-word reads.", "recovered", body),
            Field("+0x24", "param_2[9]", "total-bit-count", "int32", "Upper bound used when computing whether unread payload bits remain before calling 00a58c10.", "recovered", body),
            Field("+0x28", "param_2[10]", "initial-bit-offset-low-bits", "int32", "Low two bits are multiplied by 8 in the remaining-bit calculation, matching byte/word alignment state.", "recovered", body),
            Field("+0x2c", "param_2[0xb]", "current-word-cache", "uint32 big-endian-swapped", "Current 32-bit source word after byte swap; shifted down as bits are consumed.", "recovered", body),
            Field("+0x30", "param_2[0xc]", "cached-bit-count", "int32", "Number of unread bits left in current-word-cache. Refilled to 0x20 on word load.", "recovered", body),
            Field("+0x34", "param_2[0xd]", "cursor-word-pointer", "uint32*", "Cursor pointer advanced by four bytes when a new source word is loaded.", "recovered", body),
            Field("+0x38", "param_2[0xe]", "end-word-pointer", "uint32*", "End pointer compared against cursor; reaching or passing it sets the overflow/error flag.", "recovered", body),
            Field("+0x3c", "param_2[0xf]", "base-word-pointer", "uint32*", "Base pointer used with cursor and cached-bit-count to compute unread remaining bits.", "recovered", body),
            Field("+0x44", "param_2[0x11]", "owner-payload-object", "pointer", "Object pointer whose payload body at +0x1c is notified through 00a579d8 before reads continue.", "recovered", body),
            Field("param_1+0x18", "param_1[6]", "subpayload-channel-mask", "uint32", "Toggled with the selector mask before dispatching the two buffered subpayload lanes.", "recovered", body),
            Field("param_1+0x1e08", "param_1[0x782]", "owner-callback-interface", "pointer", "Callback interface used for per-frame/timing callbacks at slots +0x14 and +0x18.", "recovered", body)
        ];
    }

    private static Tf2Ps3SourceSlot70Param2Field Field(
        string offset,
        string expression,
        string name,
        string type,
        string meaning,
        string status,
        string body) =>
        new(offset, expression, name, type, meaning, status, body.Contains(expression, StringComparison.Ordinal));

    private static Tf2Ps3SourceSlot70Operation[] BuildOperations(string body)
    {
        return
        [
            Operation(
                0,
                "state-gate",
                "Checks whether the Source object's stored bitstream state is initialized and compatible with incoming param_2.",
                ["FUN_0086d848", "FUN_0086fb58"],
                "Proceed when the object state is absent or the incoming bitreader differs/needs processing.",
                body),
            Operation(
                1,
                "owner-payload-notify",
                "Notifies the owner path using param_2[0x11] + 0x1c before optional bit handling.",
                ["_opd_FUN_00a579d8", "param_2[0x11] + 0x1c"],
                "This is the first visible side effect on the incoming payload object.",
                body),
            Operation(
                2,
                "optional-selector",
                "When param_3 is nonzero, reads an optional selector through 00a5aa00; 0xffffffff aborts processing, low bit enables subpayload handling.",
                ["_opd_FUN_00a5aa00", "0xffffffff", "uVar6 & 1"],
                "This branch controls whether the two subpayload lanes are parsed before normal Source dispatch.",
                body),
            Operation(
                3,
                "word-refill",
                "Reads source words from cursor [0x34] up to end [0x38], byte-swaps them, and refills current-word/cache-bit fields.",
                ["param_2[0xd]", "param_2[0xe]", "param_2[0xb]", "param_2[0xc]", "uVar4 >> 0x18"],
                "This is the concrete endian and cursor contract the server-side decoder must match.",
                body),
            Operation(
                4,
                "two-lane-optional-bit-handling",
                "Reads two one-bit lane flags and calls 00a594e8(param_1, param_2 + 7, lane) for each set lane.",
                ["_opd_FUN_00a594e8", "param_2 + 7", "iVar16 != 1"],
                "The wrapper has exactly two optional buffered subpayload lanes.",
                body),
            Operation(
                5,
                "channel-mask-toggle",
                "Toggles param_1[6] with the selected channel mask before buffered subpayload dispatch.",
                ["param_1[6]", "uVar6 | (uint)uVar15", "uVar6 & ~(uint)uVar15"],
                "Channel mask state changes are visible side effects and should be represented in native server state.",
                body),
            Operation(
                6,
                "buffered-subpayload-dispatch",
                "Calls 00a5d720 for both lanes after optional-bit handling.",
                ["_opd_FUN_00a5d720", "iVar16 != 1"],
                "Each lane may dispatch a buffered subpayload through the normal Source payload dispatcher.",
                body),
            Operation(
                7,
                "remaining-bit-dispatch",
                "Computes unread bit count from total bits, base pointer, cursor pointer, cached-bit-count, and initial offset; dispatches param_2 + 7 through 00a58c10 when bits remain.",
                ["param_2[9]", "param_2[0xf]", "param_2[0xd]", "param_2[0xc]", "param_2[10]", "_opd_FUN_00a58c10"],
                "This is the final handoff to the official Source message dispatcher.",
                body),
            Operation(
                8,
                "owner-post-dispatch-callback",
                "Runs owner callback slot +0x18 and may call param_1[0x788] slot +0x34 after successful dispatch.",
                ["param_1[0x782]", "+ 0x18", "param_1[0x788]", "+ 0x34"],
                "Post-dispatch callback behavior remains part of Source object state, not packet bytes.",
                body)
        ];
    }

    private static Tf2Ps3SourceSlot70Operation Operation(
        int order,
        string role,
        string meaning,
        string[] tokens,
        string implementationNote,
        string body) =>
        new(
            order,
            role,
            tokens.All(token => body.Contains(token, StringComparison.Ordinal)) ? "recovered" : "partial",
            tokens,
            tokens.Where(token => body.Contains(token, StringComparison.Ordinal)).ToArray(),
            meaning,
            implementationNote);

    private static Tf2Ps3SourceSlot70Helper[] BuildHelpers(string body)
    {
        return
        [
            Helper("FUN_0086d848", "state-is-initialized-check", "Checks param_1 + 0x26 stored bitstream state before comparing incoming param_2.", body),
            Helper("FUN_0086fb58", "bitreader-state-compare-or-copy-check", "Compares incoming bitreader state to the object state and gates processing.", body),
            Helper("_opd_FUN_00a579d8", "owner-payload-notify", "Receives param_2[0x11] + 0x1c before optional subpayload handling.", body),
            Helper("_opd_FUN_00a5aa00", "optional-selector-read", "Returns 0xffffffff to abort or a low-bit selector that enables subpayload handling.", body),
            Helper("_opd_FUN_00a594e8", "optional-subpayload-bit-handler", "Consumes param_2 + 7 for a selected lane before buffered dispatch.", body),
            Helper("_opd_FUN_00a5d720", "buffered-subpayload-dispatcher", "Dispatches buffered lane subpayloads and can call 00a58c10 with a local bitreader.", body),
            Helper("_opd_FUN_00a58c10", "official-source-message-dispatcher", "Consumes remaining bits from param_2 + 7 and dispatches native Source messages.", body)
        ];
    }

    private static Tf2Ps3SourceSlot70Helper Helper(string addressOrName, string role, string meaning, string body) =>
        new(addressOrName, role, body.Contains(addressOrName, StringComparison.Ordinal) ? "referenced" : "missing", meaning);

    private static Tf2Ps3SourceSlot70Param2FieldGate[] BuildGates(
        JsonElement slot70BuilderRoot,
        Tf2Ps3SourceSlot70RequiredEvidence[] requiredEvidence,
        Tf2Ps3SourceSlot70Param2Field[] fields,
        Tf2Ps3SourceSlot70Operation[] operations)
    {
        var builderSummary = slot70BuilderRoot.GetProperty("Summary");
        var builderRecovered = ReadBool(builderSummary, "Param2ConstructionSiteRecovered");
        return
        [
            new Tf2Ps3SourceSlot70Param2FieldGate(
                "callee-side-field-contract",
                requiredEvidence.All(static item => item.Found) && fields.All(static field => field.EvidenceFound) ? "proven" : "needs-review",
                "00a5d9e0 decompiled body",
                "The fields consumed by the slot +0x70 wrapper are named and tied to decompile tokens."),
            new Tf2Ps3SourceSlot70Param2FieldGate(
                "operation-sequence",
                operations.All(static operation => operation.Status == "recovered") ? "proven" : "needs-review",
                "00a5d9e0 decompiled body",
                "The wrapper's gate, optional selector, two-lane subpayload handling, and final Source dispatch sequence are named."),
            new Tf2Ps3SourceSlot70Param2FieldGate(
                "caller-side-param2-builder",
                builderRecovered ? "proven" : "missing",
                "source-slot70-param2-builder.json",
                "The construction site that fills the incoming param_2 object is still required for fully native client-upload decoding."),
            new Tf2Ps3SourceSlot70Param2FieldGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "The callee field contract is known, but the live markerless packet-to-param_2 transform remains unresolved.")
        ];
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static string Preview(ExportedFunction function)
    {
        var text = string.Join('\n', function.Lines.Take(190));
        return text.Length <= 7200 ? text : text[..7200];
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

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceSlot70Param2FieldContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceSlot70Param2FieldContractInputs Inputs,
    Tf2Ps3SourceSlot70Param2FieldContractSummary Summary,
    Tf2Ps3SourceSlot70Param2Function Function,
    Tf2Ps3SourceSlot70Param2Field[] Fields,
    Tf2Ps3SourceSlot70Operation[] Operations,
    Tf2Ps3SourceSlot70Helper[] Helpers,
    Tf2Ps3SourceSlot70RequiredEvidence[] RequiredEvidence,
    Tf2Ps3SourceSlot70Param2FieldGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceSlot70Param2FieldContractInputs(
    string CExportInput,
    string Slot70Param2BuilderReport);

public sealed record Tf2Ps3SourceSlot70Param2FieldContractSummary(
    string FunctionAddress,
    int FieldCount,
    int RecoveredFieldCount,
    int OperationCount,
    int RecoveredOperationCount,
    int HelperCount,
    int RequiredEvidenceFoundCount,
    int RequiredEvidenceMissingCount,
    bool CallerSideParam2BuilderRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceSlot70Param2Function(
    string Address,
    string Name,
    string Boundary,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceSlot70Param2Field(
    string Offset,
    string Expression,
    string Name,
    string Type,
    string Meaning,
    string Status,
    bool EvidenceFound);

public sealed record Tf2Ps3SourceSlot70Operation(
    int Order,
    string Role,
    string Status,
    string[] RequiredTokens,
    string[] FoundTokens,
    string Meaning,
    string ImplementationNote);

public sealed record Tf2Ps3SourceSlot70Helper(
    string AddressOrName,
    string Role,
    string Status,
    string Meaning);

public sealed record Tf2Ps3SourceSlot70RequiredEvidence(
    string Id,
    string Token,
    bool Found);

public sealed record Tf2Ps3SourceSlot70Param2FieldGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
