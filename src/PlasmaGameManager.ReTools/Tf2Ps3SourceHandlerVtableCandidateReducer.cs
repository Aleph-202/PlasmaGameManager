using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHandlerVtableCandidateReducer
{
    private static readonly (uint VirtualAddress, uint FileOffset, uint Size)[] LoadSegments =
    [
        (0x00010000, 0x00000000, 0x0175bb30),
        (0x01770000, 0x01760000, 0x00254318),
        (0x10000000, 0x019c0000, 0x000e95c8),
        (0x100f0000, 0x01ab0000, 0x00085c10)
    ];

    private static readonly (uint Offset, string Role)[] CandidateTableSlots =
    [
        (0x00, "type-or-super-table"),
        (0x04, "destructor-or-release"),
        (0x08, "registration-callback-or-destructor"),
        (0x0c, "aux-callback"),
        (0x10, "execute"),
        (0x14, "parse-or-ready"),
        (0x18, "aux-callback"),
        (0x1c, "aux-callback"),
        (0x20, "message-id"),
        (0x24, "result-id"),
        (0x28, "pre-or-filter-name"),
        (0x2c, "aux-callback-or-name"),
        (0x30, "cleanup-or-skip"),
        (0x34, "aux-callback-or-null"),
        (0x38, "aux-data-or-secondary-table")
    ];

    private static readonly uint[] RequiredHandlerOffsets =
    [
        0x08,
        0x10,
        0x14,
        0x20,
        0x24,
        0x28,
        0x30
    ];

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
        var elf = await File.ReadAllBytesAsync(elfPath);
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var returnConstants = functions
            .Select(pair => new { pair.Key, Value = TrySingleReturnConstant(pair.Value.Body) })
            .Where(static item => item.Value is not null)
            .ToDictionary(static item => item.Key, static item => item.Value!.Value, StringComparer.Ordinal);

        var candidates = new Dictionary<uint, Tf2Ps3SourceHandlerVtableCandidate>();
        for (var address = 0x01770000u; address < 0x019c4318u - 4; address += 4)
        {
            var idOpd = ReadU32(elf, address);
            if (!IsOpd(elf, idOpd))
            {
                continue;
            }

            var idCode = ReadU32(elf, idOpd);
            if (!returnConstants.TryGetValue(idCode.ToString("x8"), out var messageId)
                || messageId < 3
                || messageId > 31)
            {
                continue;
            }

            var baseAddress = address - 0x20;
            if (baseAddress < 0x01770000u || candidates.ContainsKey(baseAddress))
            {
                continue;
            }

            var slots = BuildSlots(elf, functions, returnConstants, baseAddress);
            if (!RequiredHandlerOffsets.All(offset => slots.Any(slot =>
                    slot.SlotOffset == HexOffset(offset)
                    && slot.Kind == "opd-function")))
            {
                continue;
            }

            var sourceNeighborhood = baseAddress is >= 0x01808000u and < 0x0180d000u;
            var executableSlots = slots.Where(static slot => slot.Kind == "opd-function").ToArray();
            var allCodeInSourceRange = executableSlots.All(static slot =>
                string.CompareOrdinal(slot.FunctionAddress, "00a00000") >= 0
                && string.CompareOrdinal(slot.FunctionAddress, "00a70000") < 0);
            var registrationCallbackSlot = slots.Single(static slot => slot.SlotOffset == "0x08");
            var parseSlot = slots.Single(static slot => slot.SlotOffset == "0x14");
            var filterSlot = slots.Single(static slot => slot.SlotOffset == "0x28");
            var notes = BuildCandidateNotes(sourceNeighborhood, allCodeInSourceRange, registrationCallbackSlot, parseSlot, filterSlot);

            candidates.Add(baseAddress, new Tf2Ps3SourceHandlerVtableCandidate(
                Hex(baseAddress),
                messageId,
                sourceNeighborhood && allCodeInSourceRange ? "source-player-neighborhood" : "structural-candidate",
                sourceNeighborhood,
                allCodeInSourceRange,
                slots,
                notes));
        }

        var orderedCandidates = candidates.Values
            .OrderBy(static candidate => candidate.BaseAddress, StringComparer.Ordinal)
            .ToArray();
        var sourceCandidates = orderedCandidates
            .Where(static candidate => candidate.SourcePlayerNeighborhood)
            .ToArray();

        var report = new Tf2Ps3SourceHandlerVtableCandidateReport(
            "tf2ps3-source-handler-vtable-candidates",
            "Structurally scans TF.elf data for handler-like vtables whose +0x20 callback returns a 5-bit registered Source payload message id. These are candidates, not proof of registration; constructor/reference recovery is still needed to prove which objects are appended through 00a5df70.",
            elfPath,
            cExportPath,
            new Tf2Ps3SourceHandlerVtableCandidateSummary(
                orderedCandidates.Length,
                orderedCandidates.Select(static candidate => candidate.MessageId).Distinct().Order().ToArray(),
                sourceCandidates.Length,
                sourceCandidates.Select(static candidate => candidate.MessageId).Distinct().Order().ToArray()),
            orderedCandidates,
            [
                "The Source/player-neighborhood candidates currently expose ids 3, 4, 5, and 16.",
                "Ids 3/4/5 have tiny ready/result/filter helper functions around object offsets +0x50, +0x114, and +0x1b30 respectively; this looks like state flags or lightweight no-payload commands rather than decoded large map-load bitstreams.",
                "Ids 3/4/5 also have destructor-like +0x08 callbacks, which conflicts with the 00a5df70 registration-callback expectation and is why these remain candidates rather than proven registered handlers.",
                "Id 16 has no-op execute/parse callbacks and constant result/filter values, so it is likely a control/timing handler or a false positive until a constructor path proves registration.",
                "The remaining structural candidates may belong to other engine systems with similar vtable shapes and should not be treated as TF2 PS3 Source payload handlers without a 00a5df70 registration path."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceHandlerVtableCandidateSlot[] BuildSlots(
        byte[] elf,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        IReadOnlyDictionary<string, long> returnConstants,
        uint baseAddress)
    {
        var slots = new List<Tf2Ps3SourceHandlerVtableCandidateSlot>();
        foreach (var (offset, role) in CandidateTableSlots)
        {
            var opdAddress = ReadU32(elf, baseAddress + offset);
            if (!IsOpd(elf, opdAddress))
            {
                slots.Add(new Tf2Ps3SourceHandlerVtableCandidateSlot(
                    HexOffset(offset),
                    role,
                    "raw-data",
                    Hex(opdAddress),
                    "",
                    0,
                    [],
                    [],
                    opdAddress == 0 ? "null" : "non-opd-data",
                    ""));
                continue;
            }

            var functionAddress = ReadU32(elf, opdAddress);
            var functionAddressText = functionAddress.ToString("x8");
            functions.TryGetValue(functionAddressText, out var function);
            slots.Add(new Tf2Ps3SourceHandlerVtableCandidateSlot(
                HexOffset(offset),
                role,
                "opd-function",
                Hex(opdAddress),
                functionAddressText,
                returnConstants.GetValueOrDefault(functionAddressText),
                function == null ? [] : ExtractCalls(function.Body),
                function == null ? [] : BuildEvidence(function.Body),
                function == null ? "" : SummarizeFunctionBody(function.Body),
                function == null ? "" : Preview(function.Lines)));
        }

        return slots.ToArray();
    }

    private static string[] BuildCandidateNotes(
        bool sourceNeighborhood,
        bool allCodeInSourceRange,
        Tf2Ps3SourceHandlerVtableCandidateSlot registrationCallbackSlot,
        Tf2Ps3SourceHandlerVtableCandidateSlot parseSlot,
        Tf2Ps3SourceHandlerVtableCandidateSlot filterSlot)
    {
        var notes = new List<string>();
        if (sourceNeighborhood)
        {
            notes.Add("candidate table is adjacent to the recovered Source-side function-table slice");
        }

        if (allCodeInSourceRange)
        {
            notes.Add("all slot functions are in the 00a0xxxx..00a6xxxx Source-side code range");
        }

        if (registrationCallbackSlot.BodySummary.Contains("sets-vtable-pointer", StringComparison.Ordinal)
            || registrationCallbackSlot.Calls.Contains("FUN_00871968", StringComparer.Ordinal))
        {
            notes.Add("registration-callback slot looks destructor-like; this weakens the candidate until a constructor path proves 00a5df70 registration");
        }

        if (parseSlot.BodySummary.Contains("returns-object-field-pointer", StringComparison.Ordinal))
        {
            notes.Add("parse-or-ready slot returns an object field pointer and does not visibly consume bits");
        }

        if (filterSlot.BodySummary.Contains("writes-object-field", StringComparison.Ordinal)
            || filterSlot.ReturnConstant != 0)
        {
            notes.Add("pre/filter slot shape is not yet a clean name-string callback");
        }

        return notes.ToArray();
    }

    private static string SummarizeFunctionBody(string body)
    {
        var summaries = new List<string>();
        var constant = TrySingleReturnConstant(body);
        if (constant is not null)
        {
            summaries.Add($"returns-constant:{constant.Value}");
        }

        var fieldReturn = Regex.Match(body, @"return\s+param_1\s+\+\s+(0x[0-9a-fA-F]+|\d+)\s*;");
        if (fieldReturn.Success)
        {
            summaries.Add($"returns-object-field-pointer:{fieldReturn.Groups[1].Value}");
        }

        var fieldRead = Regex.Match(body, @"return\s+\*\(undefined1 \*\)\(param_1\s+\+\s+(0x[0-9a-fA-F]+|\d+)\)");
        if (fieldRead.Success)
        {
            summaries.Add($"reads-object-byte:{fieldRead.Groups[1].Value}");
        }

        var fieldWrite = Regex.Match(body, @"\*\(undefined1 \*\)\(param_1\s+\+\s+(0x[0-9a-fA-F]+|\d+)\)\s*=");
        if (fieldWrite.Success)
        {
            summaries.Add($"writes-object-field:{fieldWrite.Groups[1].Value}");
        }

        if (body.Contains("*param_1 = PTR_", StringComparison.Ordinal)
            || body.Contains("*param_1 = DAT_", StringComparison.Ordinal))
        {
            summaries.Add("sets-vtable-pointer");
        }

        if (body.Contains("FUN_0086caf8", StringComparison.Ordinal)
            || body.Contains("FUN_00870c28", StringComparison.Ordinal)
            || body.Contains("FUN_0086e5c8", StringComparison.Ordinal))
        {
            summaries.Add("uses-bitstream-reader-or-writer");
        }

        return summaries.Count == 0 ? "complex-or-unclassified" : string.Join("; ", summaries);
    }

    private static string[] BuildEvidence(string body)
    {
        return BuildMatchedTokens(body,
        [
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a5df70",
            "_opd_FUN_00a57f48",
            "_opd_FUN_008bc978",
            "FUN_0086caf8",
            "FUN_00870c28",
            "FUN_0086e5c8",
            "FUN_0086cf08",
            "FUN_00871968",
            "return param_1 +",
            "undefined1 *)(param_1 +"
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

    private static long? TrySingleReturnConstant(string body)
    {
        if (body.Length > 1400)
        {
            return null;
        }

        var matches = Regex.Matches(body, @"return\s+(0x[0-9a-fA-F]+|\d+)\s*;");
        if (matches.Count != 1)
        {
            return null;
        }

        var value = matches[0].Groups[1].Value;
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(value, 16)
            : Convert.ToInt64(value);
    }

    private static bool IsOpd(byte[] elf, uint virtualAddress)
    {
        var code = ReadU32(elf, virtualAddress);
        var toc = ReadU32(elf, virtualAddress + 4);
        return code is >= 0x00010000 and < 0x0176bb30 && toc == 0x019992b0;
    }

    private static uint ReadU32(byte[] elf, uint virtualAddress)
    {
        var offset = VirtualAddressToFileOffset(virtualAddress);
        if (offset is null || offset.Value + 4 > elf.Length)
        {
            return 0;
        }

        return ((uint)elf[offset.Value] << 24)
            | ((uint)elf[offset.Value + 1] << 16)
            | ((uint)elf[offset.Value + 2] << 8)
            | elf[offset.Value + 3];
    }

    private static int? VirtualAddressToFileOffset(uint virtualAddress)
    {
        foreach (var (segmentVirtualAddress, segmentFileOffset, segmentSize) in LoadSegments)
        {
            if (virtualAddress >= segmentVirtualAddress && virtualAddress < segmentVirtualAddress + segmentSize)
            {
                return checked((int)(segmentFileOffset + (virtualAddress - segmentVirtualAddress)));
            }
        }

        return null;
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
        var text = string.Join('\n', lines.Take(60));
        return text.Length <= 1800 ? text : text[..1800];
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }

    private static string HexOffset(uint value)
    {
        return "0x" + value.ToString("x2");
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceHandlerVtableCandidateReport(
    string Status,
    string Note,
    string ElfInput,
    string CExportInput,
    Tf2Ps3SourceHandlerVtableCandidateSummary Summary,
    Tf2Ps3SourceHandlerVtableCandidate[] Candidates,
    string[] Findings);

public sealed record Tf2Ps3SourceHandlerVtableCandidateSummary(
    int CandidateCount,
    long[] UniqueMessageIds,
    int SourcePlayerNeighborhoodCandidateCount,
    long[] SourcePlayerNeighborhoodMessageIds);

public sealed record Tf2Ps3SourceHandlerVtableCandidate(
    string BaseAddress,
    long MessageId,
    string Classification,
    bool SourcePlayerNeighborhood,
    bool AllSlotFunctionsInSourceRange,
    Tf2Ps3SourceHandlerVtableCandidateSlot[] Slots,
    string[] Notes);

public sealed record Tf2Ps3SourceHandlerVtableCandidateSlot(
    string SlotOffset,
    string Role,
    string Kind,
    string OpdAddress,
    string FunctionAddress,
    long ReturnConstant,
    string[] Calls,
    string[] EvidenceTokens,
    string BodySummary,
    string Preview);
