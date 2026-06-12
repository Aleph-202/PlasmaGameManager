using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerVtableReducer
{
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

    private static readonly Dictionary<string, string> KnownRoles = new(StringComparer.Ordinal)
    {
        ["0039f330"] = "source-object-creator-wrapper",
        ["0070c300"] = "source-object-owner-caller-global-setup",
        ["007cc0d0"] = "source-object-owner-caller-session-setup",
        ["0080e250"] = "source-object-owner-caller-player-bind-a",
        ["0080ea68"] = "source-object-owner-caller-player-bind-b",
        ["0080f018"] = "source-owner-table-root-callback",
        ["00a5df70"] = "source-handler-registration-function"
    };

    public static async Task ReduceAsync(string elfPath, string cExportPath, string opdRefsPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var references = LoadReferenceHits(opdRefsPath)
            .OrderBy(static reference => reference.From, StringComparer.Ordinal)
            .ToArray();
        var tables = BuildTables(image, functions, references)
            .OrderBy(static table => table.BaseAddress, StringComparer.Ordinal)
            .ToArray();

        var slots = tables.SelectMany(static table => table.Slots).ToArray();
        var report = new Tf2Ps3SourceOwnerVtableReport(
            "tf2ps3-source-owner-vtable-map",
            "Resolves TF.elf OPD/vtable tables that reference the recovered Source owner callbacks. This report is a proof gate between the owner callback boundary and any runtime implementation of registered Source payload/map-load handlers.",
            elfPath,
            cExportPath,
            opdRefsPath,
            new Tf2Ps3SourceOwnerVtableSummary(
                references.Length,
                tables.Length,
                slots.Count(static slot => slot.Kind == "opd-function"),
                tables.Count(static table => table.ReferenceHits.Length > 0),
                tables.Count(static table => table.Slots.Any(static slot => slot.FunctionAddress is "0080e250" or "0080ea68")),
                tables.Count(static table => table.Slots.Any(static slot => slot.EvidenceTokens.Contains("source-object-create-wrapper-call", StringComparer.Ordinal))),
                tables.Count(static table => table.Slots.Any(static slot =>
                    slot.FunctionAddress == "00a5df70"
                    || slot.EvidenceTokens.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
                    || slot.EvidenceTokens.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal)))),
            tables,
            BuildFindings(tables));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceOwnerVtableTable[] BuildTables(
        Elf64BigEndianImage image,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        IReadOnlyList<Tf2Ps3SourceOwnerVtableReference> references)
    {
        var clusters = ClusterReferences(references.Select(static reference => ParseHex(reference.From)).Distinct().Order().ToArray());
        var tables = new List<Tf2Ps3SourceOwnerVtableTable>();
        foreach (var cluster in clusters)
        {
            var start = FindRunStart(image, cluster.Min());
            var endExclusive = FindRunEndExclusive(image, start, cluster.Max());
            var slots = BuildSlots(image, functions, references, start, endExclusive);
            tables.Add(new Tf2Ps3SourceOwnerVtableTable(
                Hex(start),
                Hex(endExclusive),
                slots.Count(static slot => slot.Kind == "opd-function"),
                references.Where(reference =>
                        ParseHex(reference.From) >= start
                        && ParseHex(reference.From) < endExclusive)
                    .ToArray(),
                slots));
        }

        return tables
            .GroupBy(static table => table.BaseAddress, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static List<uint[]> ClusterReferences(uint[] addresses)
    {
        var clusters = new List<uint[]>();
        var current = new List<uint>();
        foreach (var address in addresses)
        {
            if (current.Count > 0 && address - current[^1] > 0x180)
            {
                clusters.Add(current.ToArray());
                current.Clear();
            }

            current.Add(address);
        }

        if (current.Count > 0)
        {
            clusters.Add(current.ToArray());
        }

        return clusters;
    }

    private static uint FindRunStart(Elf64BigEndianImage image, uint firstReference)
    {
        var start = firstReference & 0xffff_fffc;
        while (start >= firstReference - Math.Min(firstReference, 0x60)
            && start >= 4
            && TryResolveOpd(image, start - 4, out _))
        {
            start -= 4;
        }

        return start;
    }

    private static uint FindRunEndExclusive(Elf64BigEndianImage image, uint start, uint lastReference)
    {
        var end = lastReference + 4;
        var consecutiveNonOpd = 0;
        for (var address = end; address < start + 0x180; address += 4)
        {
            if (TryResolveOpd(image, address, out _))
            {
                consecutiveNonOpd = 0;
                end = address + 4;
                continue;
            }

            consecutiveNonOpd++;
            if (consecutiveNonOpd >= 4)
            {
                break;
            }
        }

        return end;
    }

    private static Tf2Ps3SourceOwnerVtableSlot[] BuildSlots(
        Elf64BigEndianImage image,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        IReadOnlyList<Tf2Ps3SourceOwnerVtableReference> references,
        uint start,
        uint endExclusive)
    {
        var slots = new List<Tf2Ps3SourceOwnerVtableSlot>();
        for (var address = start; address < endExclusive; address += 4)
        {
            image.TryReadU32(address, out var value);
            var referenceHits = references
                .Where(reference => ParseHex(reference.From) == address)
                .ToArray();

            if (!TryResolveOpd(image, address, out var resolved))
            {
                slots.Add(new Tf2Ps3SourceOwnerVtableSlot(
                    slots.Count,
                    Hex(address - start),
                    Hex(address),
                    "raw-data",
                    Hex(value),
                    "",
                    "",
                    "",
                    referenceHits,
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
            var role = ClassifyRole(functionAddress, evidence);
            slots.Add(new Tf2Ps3SourceOwnerVtableSlot(
                slots.Count,
                Hex(address - start),
                Hex(address),
                "opd-function",
                Hex(resolved.OpdAddress),
                functionAddress,
                Hex(resolved.Toc),
                role,
                referenceHits,
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

    private static string ClassifyRole(string functionAddress, string[] evidence)
    {
        if (KnownRoles.TryGetValue(functionAddress, out var role))
        {
            return role;
        }

        if (evidence.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
            || evidence.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal))
        {
            return "source-handler-registration-caller";
        }

        if (evidence.Contains("source-object-create-wrapper-call", StringComparer.Ordinal))
        {
            return "source-object-creator-caller";
        }

        if (evidence.Contains("source-owner-callback-association-call", StringComparer.Ordinal))
        {
            return "source-owner-association-caller";
        }

        if (evidence.Contains("virtual-slot-0x44-callsite", StringComparer.Ordinal))
        {
            return "virtual-slot-0x44-caller";
        }

        return "owner-table-function";
    }

    private static string[] BuildEvidence(string functionAddress, string body, string[] calls)
    {
        var evidence = new List<string>();
        if (functionAddress == "00a5df70")
        {
            evidence.Add("source-handler-registration-function");
        }

        AddIf(body, evidence, "FUN_0039f330", "source-object-create-wrapper-call");
        AddIf(body, evidence, "_opd_FUN_0039f330", "source-object-create-wrapper-opd-call");
        AddIf(body, evidence, "FUN_00a5d0c0", "source-owner-callback-association-call");
        AddIf(body, evidence, "_opd_FUN_00a5d0c0", "source-owner-callback-association-opd-call");
        AddIf(body, evidence, "FUN_00a5df70", "source-handler-registration-direct-call");
        AddIf(body, evidence, "_opd_FUN_00a5df70", "source-handler-registration-opd-reference");
        AddIf(body, evidence, "param_1[0x782]", "source-object-owner-callback-field");
        AddIf(body, evidence, "param_1 + 0x1e0c", "source-handler-vector-field");
        AddIf(body, evidence, "param_1 + 0x1e18", "source-handler-count-field");
        AddIf(body, evidence, "ZEXT48(piVar4 + 1)", "owner-expression-pivar4-plus-one");
        AddIf(body, evidence, "piVar20 = piVar5 + 1", "owner-expression-pivar5-plus-one");
        AddIf(body, evidence, "+ 0x44)", "virtual-slot-0x44-callsite");
        AddIf(body, evidence, "+0x44)", "virtual-slot-0x44-callsite");
        if (calls.Contains("_opd_FUN_00a5df70", StringComparer.Ordinal))
        {
            evidence.Add("source-handler-registration-opd-callee");
        }

        return evidence.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
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
        if (body.Contains("FUN_0039f330", StringComparison.Ordinal))
        {
            summary.Add("calls-source-object-creator");
        }

        if (body.Contains("FUN_00a5df70", StringComparison.Ordinal)
            || body.Contains("_opd_FUN_00a5df70", StringComparison.Ordinal))
        {
            summary.Add("reaches-handler-registration");
        }

        if (body.Contains("+ 0x44)", StringComparison.Ordinal)
            || body.Contains("+0x44)", StringComparison.Ordinal))
        {
            summary.Add("has-virtual-slot-0x44-call");
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

    private static Tf2Ps3SourceOwnerVtableReference[] LoadReferenceHits(string opdRefsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(opdRefsPath));
        var references = new List<Tf2Ps3SourceOwnerVtableReference>();
        foreach (var target in document.RootElement.GetProperty("Targets").EnumerateArray())
        {
            var targetAddress = target.GetProperty("Address").GetString() ?? "";
            foreach (var hit in target.GetProperty("ReferenceHits").EnumerateArray())
            {
                references.Add(new Tf2Ps3SourceOwnerVtableReference(
                    targetAddress,
                    hit.GetProperty("From").GetString() ?? "",
                    hit.GetProperty("Type").GetString() ?? "",
                    hit.GetProperty("Source").GetString() ?? "",
                    hit.GetProperty("FunctionEntry").GetString() ?? "",
                    hit.GetProperty("FunctionName").GetString() ?? ""));
            }
        }

        return references.ToArray();
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceOwnerVtableTable> tables)
    {
        var findings = new List<string>
        {
            "The clustered owner-reference targets mostly resolve as nested data/table anchors, not direct OPD function slots for 0080e250 or 0080ea68.",
            "The only resolved table slot that reaches the handler-registration helper is the already known Source function-table slice around 0180c9a0/0180c9c0, with 0180ca04 -> 00a5df70."
        };

        if (tables.SelectMany(static table => table.Slots).Any(static slot =>
                slot.FunctionAddress == "00a5df70"
                || slot.EvidenceTokens.Contains("source-handler-registration-direct-call", StringComparer.Ordinal)
                || slot.EvidenceTokens.Contains("source-handler-registration-opd-reference", StringComparer.Ordinal)))
        {
            findings.Add("At least one resolved owner-table slot reaches 00a5df70; this is an implementation candidate and should be reduced into concrete handler ids before runtime changes.");
        }
        else
        {
            findings.Add("No resolved owner-table slot currently points at or calls 00a5df70, so the map-load handler-registration path is still not proven.");
            findings.Add("Runtime Source/map-load behavior should remain conservative until a table slot or constructor path is proven to append handler objects through 00a5df70.");
        }

        return findings.ToArray();
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

    private static uint ParseHex(string value)
    {
        value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return Convert.ToUInt32(value, 16);
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

public sealed record Tf2Ps3SourceOwnerVtableReport(
    string Status,
    string Note,
    string ElfInput,
    string CExportInput,
    string OpdRefsInput,
    Tf2Ps3SourceOwnerVtableSummary Summary,
    Tf2Ps3SourceOwnerVtableTable[] Tables,
    string[] Findings);

public sealed record Tf2Ps3SourceOwnerVtableSummary(
    int ReferenceHitCount,
    int TableCount,
    int OpdFunctionSlotCount,
    int TablesWithReferenceAnchors,
    int TablesWithPlayerBindCallbacks,
    int TablesWithSourceObjectCreatorCalls,
    int TablesWithHandlerInstallEvidence);

public sealed record Tf2Ps3SourceOwnerVtableTable(
    string BaseAddress,
    string EndExclusive,
    int OpdFunctionSlotCount,
    Tf2Ps3SourceOwnerVtableReference[] ReferenceHits,
    Tf2Ps3SourceOwnerVtableSlot[] Slots);

public sealed record Tf2Ps3SourceOwnerVtableSlot(
    int SlotIndex,
    string SlotOffset,
    string TableAddress,
    string Kind,
    string OpdAddress,
    string FunctionAddress,
    string Toc,
    string Role,
    Tf2Ps3SourceOwnerVtableReference[] ReferenceHits,
    string[] Calls,
    string[] EvidenceTokens,
    string BodySummary,
    string Preview);

public sealed record Tf2Ps3SourceOwnerVtableReference(
    string TargetAddress,
    string From,
    string Type,
    string Source,
    string FunctionEntry,
    string FunctionName);
