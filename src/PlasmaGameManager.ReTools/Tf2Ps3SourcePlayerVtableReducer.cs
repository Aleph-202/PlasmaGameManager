using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePlayerVtableReducer
{
    private const uint DefaultVtableBase = 0x0180c9c0;
    private const uint DefaultVtableEndExclusive = 0x0180caa0;
    private const uint DefaultRegistrationSlot = 0x0180ca04;
    private const uint ElfVirtualToFileOffsetBias = 0x10000;

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

        var slots = new List<Tf2Ps3SourcePlayerVtableSlot>();
        for (var address = DefaultVtableBase; address < DefaultVtableEndExclusive; address += 4)
        {
            var opdAddress = ReadU32(elf, address);
            if (opdAddress == 0)
            {
                slots.Add(new Tf2Ps3SourcePlayerVtableSlot(
                    slots.Count,
                    Hex(address - DefaultVtableBase),
                    Hex(address),
                    "0x00000000",
                    "",
                    "null-terminator",
                    [],
                    [],
                    ""));
                break;
            }

            var functionAddress = ReadU32(elf, opdAddress);
            var functionAddressText = functionAddress.ToString("x8");
            functions.TryGetValue(functionAddressText, out var function);
            var calls = function == null ? [] : ExtractCalls(function.Body);
            var evidenceTokens = function == null ? [] : BuildEvidence(function.Body);
            slots.Add(new Tf2Ps3SourcePlayerVtableSlot(
                slots.Count,
                Hex(address - DefaultVtableBase),
                Hex(address),
                Hex(opdAddress),
                functionAddressText,
                ClassifyRole(functionAddressText, calls, evidenceTokens),
                calls,
                evidenceTokens,
                function == null ? "" : Preview(function.Lines)));
        }

        var nonNullSlots = slots.Where(static slot => slot.OpdAddress != "0x00000000").ToArray();
        var registrationSlot = slots.Single(slot => slot.TableAddress == Hex(DefaultRegistrationSlot));
        var report = new Tf2Ps3SourcePlayerVtableReport(
            "tf2ps3-source-player-vtable-map",
            "Maps the native TF2 PS3 Source-side function-table slice containing handler registration, attached-stream readers, payload-dispatch callers, and native send-builder callers. This slice is evidence for the late Source/map-load helper cluster, but it is not by itself proof of a recovered player/session class constructor.",
            elfPath,
            cExportPath,
            new Tf2Ps3SourcePlayerVtableSummary(
                Hex(DefaultVtableBase),
                Hex(DefaultVtableEndExclusive),
                nonNullSlots.Length,
                registrationSlot.SlotOffset,
                registrationSlot.FunctionAddress,
                nonNullSlots.Count(static slot => slot.Calls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal)),
                nonNullSlots.Count(static slot => slot.Calls.Contains("_opd_FUN_008bc978", StringComparer.Ordinal)),
                nonNullSlots.Count(static slot => slot.Calls.Contains("_opd_FUN_008b82c0", StringComparer.Ordinal)),
                nonNullSlots.Count(static slot => slot.Role.Contains("buffered-writer", StringComparison.Ordinal))),
            slots.ToArray(),
            [
                "Slot +0x44 is the 00a5df70 handler-registration entry inside this Source-side function-table slice.",
                "Slots +0x68, +0x70, +0x78, and neighboring methods call 00a58c10, which ties staged native payload dispatch to this same helper cluster.",
                "Slot +0x8c calls 008bc978, the native PS3 Source send builder, so this cluster also reaches late Source/map-load response generation.",
                "The table base has no current Ghidra reference proof and nearby early slots contain material/HUD string evidence; treat this as a helper slice until constructor/object setup refs are recovered."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static uint ReadU32(byte[] bytes, uint virtualAddress)
    {
        var offset = checked((int)(virtualAddress - ElfVirtualToFileOffsetBias));
        return ((uint)bytes[offset] << 24)
            | ((uint)bytes[offset + 1] << 16)
            | ((uint)bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

    private static string ClassifyRole(string address, string[] calls, string[] evidenceTokens)
    {
        if (address == "00a5df70")
        {
            return "handler-registration-vtable-slot";
        }

        if (address == "00a5c2e8")
        {
            return "attached-stream-reader-vtable-slot";
        }

        if (calls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal))
        {
            return "payload-dispatch-caller-vtable-slot";
        }

        if (calls.Contains("_opd_FUN_008bc978", StringComparer.Ordinal))
        {
            return "native-send-builder-vtable-slot";
        }

        if (calls.Contains("_opd_FUN_008b82c0", StringComparer.Ordinal))
        {
            return "socket-recv-wrapper-caller-vtable-slot";
        }

        if (calls.Contains("_opd_FUN_008b8328", StringComparer.Ordinal))
        {
            return "socket-send-wrapper-caller-vtable-slot";
        }

        if (evidenceTokens.Contains("FUN_0086d918", StringComparer.Ordinal))
        {
            return "buffered-writer-vtable-slot";
        }

        if (evidenceTokens.Contains("param_1 + 0x98", StringComparer.Ordinal))
        {
            return "connection-state-helper-vtable-slot";
        }

        return "source-function-table-slot";
    }

    private static string[] BuildEvidence(string body)
    {
        return BuildMatchedTokens(body,
        [
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a5df70",
            "_opd_FUN_00a5c2e8",
            "_opd_FUN_008b82c0",
            "_opd_FUN_008b8328",
            "_opd_FUN_008bc978",
            "_opd_FUN_00a5b920",
            "_opd_FUN_00a625e8",
            "_opd_FUN_00a57f48",
            "_opd_FUN_00a584d0",
            "_opd_FUN_00a5a550",
            "FUN_0086d918",
            "FUN_00870c28",
            "FUN_0086caf8",
            "param_1 + 0x1e0c",
            "param_1 + 0x1e18",
            "param_1 + 0x98",
            "param_1[0x24]",
            "param_1[0x10c]",
            "param_1[0x151]",
            "0x544",
            "0x440",
            "0x428",
            "0x400"
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
        var text = string.Join('\n', lines.Take(70));
        return text.Length <= 2000 ? text : text[..2000];
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourcePlayerVtableReport(
    string Status,
    string Note,
    string ElfInput,
    string CExportInput,
    Tf2Ps3SourcePlayerVtableSummary Summary,
    Tf2Ps3SourcePlayerVtableSlot[] Slots,
    string[] Findings);

public sealed record Tf2Ps3SourcePlayerVtableSummary(
    string VtableBase,
    string VtableEndExclusive,
    int NonNullSlotCount,
    string RegistrationSlotOffset,
    string RegistrationFunction,
    int PayloadDispatchCallerSlotCount,
    int NativeSendBuilderCallerSlotCount,
    int SocketRecvWrapperCallerSlotCount,
    int BufferedWriterSlotCount);

public sealed record Tf2Ps3SourcePlayerVtableSlot(
    int SlotIndex,
    string SlotOffset,
    string TableAddress,
    string OpdAddress,
    string FunctionAddress,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);
