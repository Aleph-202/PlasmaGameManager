using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceInstalledObjectVtableReducer
{
    private const uint VtablePointerSymbolAddress = 0x01977dbc;
    private const int MaxBytesToScan = 0x240;

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

        if (!image.TryReadU32(VtablePointerSymbolAddress, out var vtableAddress))
        {
            throw new InvalidDataException($"Could not read PTR_PTR_01977dbc at {Hex(VtablePointerSymbolAddress)}.");
        }

        var slots = BuildSlots(image, functions, vtableAddress);
        var report = new Tf2Ps3SourceInstalledObjectVtableReport(
            "tf2ps3-source-installed-object-vtable-map",
            "Resolves the actual function table installed by 00a5e058 through PTR_PTR_01977dbc. This is the concrete vtable on the 0x1e28 Source-side object, distinct from the nearby OPD-pointer helper slice around 0180c9c0.",
            elfPath,
            cExportPath,
            Hex(VtablePointerSymbolAddress),
            Hex(vtableAddress),
            new Tf2Ps3SourceInstalledObjectVtableSummary(
                slots.Length,
                slots.Count(static slot => slot.Kind == "inline-function-descriptor"),
                slots.Count(static slot => slot.EvidenceTokens.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
                    || slot.EvidenceTokens.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal)
                    || slot.FunctionAddress == "00a5df70"),
                slots.Count(static slot => slot.EvidenceTokens.Contains("source-object-create-wrapper-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("payload-dispatcher-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("native-send-builder-call", StringComparer.Ordinal)),
                slots.Count(static slot => slot.EvidenceTokens.Contains("hud-menu-string-evidence", StringComparer.Ordinal)
                    || slot.EvidenceTokens.Contains("hud-menu-global-evidence", StringComparer.Ordinal))),
            slots,
            BuildFindings(vtableAddress, slots));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceInstalledObjectVtableSlot[] BuildSlots(
        Elf64BigEndianImage image,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        uint vtableAddress)
    {
        var slots = new List<Tf2Ps3SourceInstalledObjectVtableSlot>();
        for (var offset = 0u; offset < MaxBytesToScan; offset += 8)
        {
            var descriptorAddress = vtableAddress + offset;
            if (!image.TryReadU32(descriptorAddress, out var functionAddress)
                || !image.TryReadU32(descriptorAddress + 4, out var toc)
                || !image.IsExecutableAddress(functionAddress)
                || !image.IsWritableAddress(toc))
            {
                break;
            }

            var functionAddressText = functionAddress.ToString("x8");
            functions.TryGetValue(functionAddressText, out var function);
            var calls = function is null ? [] : ExtractCalls(function.Body);
            var evidence = function is null ? [] : BuildEvidence(functionAddressText, function.Body, calls);
            slots.Add(new Tf2Ps3SourceInstalledObjectVtableSlot(
                slots.Count,
                Hex(offset),
                Hex(descriptorAddress),
                "inline-function-descriptor",
                functionAddressText,
                Hex(toc),
                ClassifyRole(offset, functionAddressText, evidence),
                calls,
                evidence,
                function is null ? "function-not-found-in-current-c-export" : SummarizeFunctionBody(function.Body),
                function is null ? "" : Preview(function.Lines)));
        }

        return slots.ToArray();
    }

    private static string ClassifyRole(uint offset, string functionAddress, string[] evidence)
    {
        if (functionAddress == "00a5df70")
        {
            return "source-handler-registration-function";
        }

        if (evidence.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
            || evidence.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal))
        {
            return "source-handler-registration-caller";
        }

        if (evidence.Contains("payload-dispatcher-call", StringComparer.Ordinal))
        {
            return "payload-dispatcher-caller";
        }

        if (evidence.Contains("native-send-builder-call", StringComparer.Ordinal))
        {
            return "native-send-builder-caller";
        }

        return offset switch
        {
            0x68 => "known-object-slot-0x68-caller-target",
            0x70 => "rate-or-time-slot-0x70-target",
            0x80 => "reuse-check-slot-0x80-target",
            0xf0 => "buffer-resize-slot-0xf0-target",
            _ => "installed-source-object-vtable-slot"
        };
    }

    private static string[] BuildEvidence(string functionAddress, string body, string[] calls)
    {
        var evidence = new List<string>();
        if (functionAddress == "00a5df70")
        {
            evidence.Add("source-handler-registration-function");
        }

        AddIf(body, evidence, "FUN_00a5df70", "source-handler-registration-direct-call");
        AddIf(body, evidence, "_opd_FUN_00a5df70", "source-handler-registration-opd-reference");
        AddIf(body, evidence, "FUN_0039f330", "source-object-create-wrapper-call");
        AddIf(body, evidence, "_opd_FUN_00a58c10", "payload-dispatcher-call");
        AddIf(body, evidence, "_opd_FUN_008bc978", "native-send-builder-call");
        AddIf(body, evidence, "_opd_FUN_008b82c0", "socket-recv-wrapper-call");
        AddIf(body, evidence, "_opd_FUN_008b8328", "socket-send-wrapper-call");
        AddIf(body, evidence, "PTR_s_CHudMenu_01977dd0", "hud-menu-string-evidence");
        AddIf(body, evidence, "PTR_DAT_01977de0", "hud-menu-global-evidence");
        AddIf(body, evidence, "PTR_DAT_01977dec", "hud-menu-global-evidence");
        AddIf(body, evidence, "PTR_DAT_01977df0", "hud-menu-global-evidence");
        AddIf(body, evidence, "param_1 + 0x1e0c", "source-handler-vector-field");
        AddIf(body, evidence, "param_1 + 0x1e18", "source-handler-count-field");
        if (calls.Contains("_opd_FUN_00a5df70", StringComparer.Ordinal))
        {
            evidence.Add("source-handler-registration-opd-callee");
        }

        return evidence.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] BuildFindings(uint vtableAddress, IReadOnlyCollection<Tf2Ps3SourceInstalledObjectVtableSlot> slots)
    {
        var findings = new List<string>
        {
            $"PTR_PTR_01977dbc resolves to {Hex(vtableAddress)}, and 00a5e058 writes that value directly into the constructed 0x1e28 Source object.",
            "The installed table is an inline PPC64 function-descriptor table, not the OPD-pointer helper slice around 0180c9c0.",
            "The installed object vtable uses 8-byte function descriptors; offsets such as +0x70, +0x80, and +0xf0 line up with virtual calls observed in the Source object lifecycle."
        };

        if (slots.Any(static slot => slot.FunctionAddress == "00a5df70"
                || slot.EvidenceTokens.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
                || slot.EvidenceTokens.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal)))
        {
            findings.Add("At least one installed object vtable slot reaches 00a5df70; reduce this slot's caller contract before implementing runtime handler registration.");
        }
        else
        {
            findings.Add("No installed object vtable slot in the scanned range points at or calls 00a5df70, so the registered payload-handler install path is still elsewhere.");
        }

        if (slots.Any(static slot => slot.EvidenceTokens.Contains("hud-menu-string-evidence", StringComparer.Ordinal)
                || slot.EvidenceTokens.Contains("hud-menu-global-evidence", StringComparer.Ordinal)))
        {
            findings.Add("Several slots reference CHudMenu globals/strings, so this inline descriptor table is not safe evidence for native Source handler registration or map-load packet ids.");
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
        var summary = new List<string>();
        if (body.Contains("FUN_00a5df70", StringComparison.Ordinal)
            || body.Contains("_opd_FUN_00a5df70", StringComparison.Ordinal))
        {
            summary.Add("reaches-handler-registration");
        }

        if (body.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal))
        {
            summary.Add("calls-payload-dispatcher");
        }

        if (body.Contains("_opd_FUN_008bc978", StringComparison.Ordinal))
        {
            summary.Add("calls-native-send-builder");
        }

        if (body.Contains("PTR_s_CHudMenu_01977dd0", StringComparison.Ordinal))
        {
            summary.Add("references-chudmenu");
        }

        var returnConstant = Regex.Match(body, @"return\s+(0x[0-9a-fA-F]+|\d+)\s*;");
        if (returnConstant.Success && body.Length < 1200)
        {
            summary.Add($"returns-constant:{returnConstant.Groups[1].Value}");
        }

        return summary.Count == 0 ? "complex-or-unclassified" : string.Join("; ", summary);
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
        var text = string.Join('\n', lines.Take(60));
        return text.Length <= 1800 ? text : text[..1800];
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceInstalledObjectVtableReport(
    string Status,
    string Note,
    string ElfInput,
    string CExportInput,
    string VtablePointerSymbol,
    string VtableAddress,
    Tf2Ps3SourceInstalledObjectVtableSummary Summary,
    Tf2Ps3SourceInstalledObjectVtableSlot[] Slots,
    string[] Findings);

public sealed record Tf2Ps3SourceInstalledObjectVtableSummary(
    int SlotCount,
    int InlineDescriptorSlotCount,
    int HandlerRegistrationEvidenceSlotCount,
    int SourceObjectCreatorCallSlotCount,
    int PayloadDispatcherCallSlotCount,
    int NativeSendBuilderCallSlotCount,
    int HudMenuEvidenceSlotCount);

public sealed record Tf2Ps3SourceInstalledObjectVtableSlot(
    int SlotIndex,
    string SlotOffset,
    string DescriptorAddress,
    string Kind,
    string FunctionAddress,
    string Toc,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string BodySummary,
    string Preview);
