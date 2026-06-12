using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHelperSliceContractReducer
{
    private const uint HelperSliceBase = 0x0180c9a0;
    private const uint HelperSliceVisibleBase = 0x0180c9c0;
    private const uint HelperSliceEndExclusive = 0x0180ca9c;
    private const uint RegistrationSlotAddress = 0x0180ca04;
    private const uint RegistrationOpdAddress = 0x0190e558;
    private const uint OpdToc = 0x019992b0;

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

    public static async Task ReduceAsync(string elfPath, string cExportPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);

        var slots = BuildSlots(image, functions);
        var registrationSlot = slots.Single(slot => slot.TableAddress == Hex(RegistrationSlotAddress));
        var report = new Tf2Ps3SourceHelperSliceContractReport(
            "tf2ps3-source-helper-slice-contract",
            "Pins the full TF.elf helper/function-table slice around 0180c9a0. This table contains native Source payload registration, receive dispatch, peer attach, and send helpers, but current evidence still does not prove the concrete handler object constructors that populate the registered-handler vector.",
            elfPath,
            cExportPath,
            new Tf2Ps3SourceHelperSliceContractSummary(
                Hex(HelperSliceBase),
                Hex(HelperSliceVisibleBase),
                Hex(HelperSliceEndExclusive),
                slots.Length,
                slots.Count(static slot => slot.Kind == "opd-function"),
                Hex(RegistrationSlotAddress),
                Hex(RegistrationSlotAddress - HelperSliceBase),
                Hex(RegistrationSlotAddress - HelperSliceVisibleBase),
                Hex(RegistrationOpdAddress),
                registrationSlot.FunctionAddress,
                slots.Count(static slot => slot.EvidenceTokens.Contains("payload-dispatcher-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("native-send-builder-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("socket-recv-wrapper-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("socket-send-wrapper-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("handler-registration-function", StringComparer.Ordinal))),
            slots,
            BuildFindings(slots));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceHelperSliceSlot[] BuildSlots(
        Elf64BigEndianImage image,
        IReadOnlyDictionary<string, ExportedFunction> functions)
    {
        var slots = new List<Tf2Ps3SourceHelperSliceSlot>();
        for (var address = HelperSliceBase; address < HelperSliceEndExclusive; address += 4)
        {
            image.TryReadU32(address, out var value);
            if (!TryResolveOpd(image, address, out var resolved))
            {
                slots.Add(new Tf2Ps3SourceHelperSliceSlot(
                    slots.Count,
                    Hex(address - HelperSliceBase),
                    Hex(address - HelperSliceVisibleBase),
                    Hex(address),
                    "raw-data",
                    Hex(value),
                    "",
                    "",
                    "raw-or-non-opd-data",
                    [],
                    [],
                    "raw-or-non-opd-data",
                    ""));
                continue;
            }

            var functionAddress = resolved.FunctionAddress.ToString("x8");
            functions.TryGetValue(functionAddress, out var function);
            var calls = function is null ? [] : ExtractCalls(function.Body);
            var evidence = function is null ? [] : BuildEvidence(functionAddress, function.Body, calls);
            slots.Add(new Tf2Ps3SourceHelperSliceSlot(
                slots.Count,
                Hex(address - HelperSliceBase),
                Hex(address - HelperSliceVisibleBase),
                Hex(address),
                "opd-function",
                Hex(resolved.OpdAddress),
                functionAddress,
                Hex(resolved.Toc),
                ClassifyRole(address, functionAddress, evidence),
                calls,
                evidence,
                function is null ? "function-not-found-in-current-c-export" : SummarizeFunctionBody(function.Body),
                function is null ? "" : Preview(function.Lines)));
        }

        return slots.ToArray();
    }

    private static bool TryResolveOpd(Elf64BigEndianImage image, uint tableAddress, out ResolvedOpd resolved)
    {
        resolved = default;
        if (!image.TryReadU32(tableAddress, out var opdAddress)
            || !image.IsWritableAddress(opdAddress)
            || !image.TryReadU32(opdAddress, out var functionAddress)
            || !image.TryReadU32(opdAddress + 4, out var toc)
            || toc != OpdToc
            || !image.IsExecutableAddress(functionAddress))
        {
            return false;
        }

        resolved = new ResolvedOpd(opdAddress, functionAddress, toc);
        return true;
    }

    private static string ClassifyRole(uint tableAddress, string functionAddress, string[] evidence)
    {
        if (tableAddress == RegistrationSlotAddress || functionAddress == "00a5df70")
        {
            return "handler-registration-slot";
        }

        if (functionAddress == "00a5b6c0")
        {
            return "peer-attach-socket-slot";
        }

        if (functionAddress == "00a5c2e8")
        {
            return "attached-stream-reader-slot";
        }

        if (functionAddress == "00a5b4f0" || functionAddress == "00a5d9e0")
        {
            return "payload-dispatch-wrapper-slot";
        }

        if (functionAddress == "00a61150")
        {
            return "native-send-builder-slot";
        }

        if (functionAddress == "00a5e4b8")
        {
            return "staged-string-payload-writer-slot";
        }

        if (functionAddress == "00a5e2c8")
        {
            return "buffer-resize-slot";
        }

        if (functionAddress == "00a5eed0")
        {
            return "reset-destructor-slot";
        }

        if (evidence.Contains("virtual-slot-0x44-callsite", StringComparer.Ordinal))
        {
            return "virtual-slot-0x44-wrapper-slot";
        }

        if (evidence.Contains("payload-dispatcher-call", StringComparer.Ordinal))
        {
            return "payload-dispatch-adjacent-slot";
        }

        return "helper-slice-slot";
    }

    private static string[] BuildEvidence(string functionAddress, string body, string[] calls)
    {
        var evidence = new List<string>();
        if (functionAddress == "00a5df70")
        {
            evidence.Add("handler-registration-function");
        }

        AddIf(body, evidence, "_opd_FUN_00a57f48", "handler-id-lookup-call");
        AddIf(body, evidence, "_opd_FUN_00a625e8", "handler-vector-append-call");
        AddIf(body, evidence, "param_1 + 0x1e0c", "handler-vector-field");
        AddIf(body, evidence, "param_1 + 0x1e18", "handler-count-field");
        AddIf(body, evidence, "*puStack00000038 + 8", "handler-bind-owner-slot-0x08");
        AddIf(body, evidence, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, evidence, "_opd_FUN_00a584d0", "attached-state-writer-call");
        AddIf(body, evidence, "_opd_FUN_008b82c0", "socket-recv-wrapper-call");
        AddIf(body, evidence, "_opd_FUN_008b8328", "socket-send-wrapper-call");
        AddIf(body, evidence, "_opd_FUN_008b8e70", "socket-connect-or-register-call");
        AddIf(body, evidence, "_opd_FUN_008bc978", "native-send-builder-call");
        AddIf(body, evidence, "_opd_FUN_00a5a550", "token-reset-call");
        AddIf(body, evidence, "param_1[0x24]", "attached-socket-field");
        AddIf(body, evidence, "param_1[0x10c]", "attached-frame-type-field");
        AddIf(body, evidence, "param_1 + 0x151", "attached-payload-buffer-field");
        AddIf(body, evidence, "0x17701", "large-buffer-ceiling-96000");
        AddIf(body, evidence, "96000", "large-buffer-size-96000");
        AddIf(body, evidence, "+ 0x44)", "virtual-slot-0x44-callsite");
        AddIf(body, evidence, "+0x44)", "virtual-slot-0x44-callsite");
        if (calls.Contains("_opd_FUN_00a5df70", StringComparer.Ordinal)
            && functionAddress != "00a5df70")
        {
            evidence.Add("handler-registration-callee");
        }

        return evidence
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceHelperSliceSlot> slots)
    {
        var findings = new List<string>
        {
            "The full helper slice starts at 0180c9a0. The previously used visible base 0180c9c0 is 0x20 bytes into the same table.",
            "The registration entry is exact: table word 0180ca04 contains OPD 0190e558, which resolves to 00a5df70. Its full-slice offset is +0x64 and its visible-base offset is +0x44.",
            "The same table contains peer attach/connect (00a5b6c0), queued/attached receive dispatch (00a5b4f0, 00a5c2e8, 00a5d9e0), native send generation (00a61150), staged string payload writing (00a5e4b8), and buffer resize (00a5e2c8).",
            "This contract proves the helper mechanics, not the concrete registered message ids. Runtime map-load behavior still needs the constructor/setup path that creates handler objects and calls 00a5df70 with each handler.",
            "The installed PTR_PTR_01977dbc table is a separate inline-descriptor table and has CHudMenu-style evidence, so do not merge it with this OPD-pointer helper slice."
        };

        if (slots.Count(static slot => slot.EvidenceTokens.Contains("handler-registration-function", StringComparer.Ordinal)) != 1)
        {
            findings.Add("Unexpected registration evidence count; inspect this report before using it as an implementation gate.");
        }

        return findings.ToArray();
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string SummarizeFunctionBody(string body)
    {
        var summaries = new List<string>();
        if (body.Contains("_opd_FUN_00a57f48", StringComparison.Ordinal)
            && body.Contains("_opd_FUN_00a625e8", StringComparison.Ordinal))
        {
            summaries.Add("installs-registered-handler");
        }

        if (body.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal))
        {
            summaries.Add("calls-payload-dispatcher");
        }

        if (body.Contains("_opd_FUN_008bc978", StringComparison.Ordinal))
        {
            summaries.Add("calls-native-send-builder");
        }

        if (body.Contains("_opd_FUN_008b82c0", StringComparison.Ordinal))
        {
            summaries.Add("reads-attached-socket");
        }

        if (body.Contains("_opd_FUN_008b8328", StringComparison.Ordinal))
        {
            summaries.Add("writes-attached-socket");
        }

        if (body.Contains("+ 0x44)", StringComparison.Ordinal)
            || body.Contains("+0x44)", StringComparison.Ordinal))
        {
            summaries.Add("has-virtual-slot-0x44-call");
        }

        var returnConstant = Regex.Match(body, @"return\s+(0x[0-9a-fA-F]+|\d+)\s*;");
        if (returnConstant.Success && body.Length < 1200)
        {
            summaries.Add($"returns-constant:{returnConstant.Groups[1].Value}");
        }

        return summaries.Count == 0 ? "complex-or-unclassified" : string.Join("; ", summaries);
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
        return new ExportedFunction(name, address, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(70));
        return text.Length <= 2000 ? text : text[..2000];
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }

    private readonly record struct ResolvedOpd(uint OpdAddress, uint FunctionAddress, uint Toc);

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceHelperSliceContractReport(
    string Status,
    string Note,
    string ElfInput,
    string CExportInput,
    Tf2Ps3SourceHelperSliceContractSummary Summary,
    Tf2Ps3SourceHelperSliceSlot[] Slots,
    string[] Findings);

public sealed record Tf2Ps3SourceHelperSliceContractSummary(
    string FullSliceBase,
    string VisibleSliceBase,
    string EndExclusive,
    int SlotCount,
    int OpdFunctionSlotCount,
    string RegistrationSlotAddress,
    string RegistrationFullSliceOffset,
    string RegistrationVisibleSliceOffset,
    string RegistrationOpdAddress,
    string RegistrationFunction,
    int PayloadDispatcherCallerSlotCount,
    int NativeSendBuilderCallerSlotCount,
    int SocketRecvWrapperCallerSlotCount,
    int SocketSendWrapperCallerSlotCount,
    int HandlerRegistrationSlotCount);

public sealed record Tf2Ps3SourceHelperSliceSlot(
    int SlotIndex,
    string FullSliceOffset,
    string VisibleSliceOffset,
    string TableAddress,
    string Kind,
    string OpdAddress,
    string FunctionAddress,
    string Toc,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string BodySummary,
    string Preview);
