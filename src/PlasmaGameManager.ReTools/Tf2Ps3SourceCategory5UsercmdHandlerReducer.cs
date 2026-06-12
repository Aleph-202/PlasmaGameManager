using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceCategory5UsercmdHandlerReducer
{
    private const uint VirtualFileBias = 0x10000;
    private const uint TableStart = 0x0180b730;
    private const uint TableEndExclusive = 0x0180b830;
    private const uint ExpectedHandlerFunction = 0x00a2bd18;
    private const uint ExpectedProcessUsercmdsBridge = 0x00a291c0;
    private const uint ExpectedDynamicSlotOffset = 0x98;

    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceCategory5UsercmdHandlerReport> ReduceAsync(
        string cExportPath,
        string tfElfPath,
        string clcMoveContextPath,
        string clcMoveContractPath,
        string serverDllUsercmdDecoderPath,
        string outputPath)
    {
        var cLines = await File.ReadAllLinesAsync(cExportPath);
        var functions = ExtractFunctions(cLines)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First(),
                StringComparer.Ordinal);

        var elfBytes = await File.ReadAllBytesAsync(tfElfPath);
        var handlerOpd = FindUniqueWordReference(elfBytes, ExpectedHandlerFunction);
        var processBridgeOpd = FindUniqueWordReference(elfBytes, ExpectedProcessUsercmdsBridge);
        var handlerOpdReferences = handlerOpd == 0
            ? []
            : FindWordReferences(elfBytes, handlerOpd).ToArray();
        var handlerTableCell = handlerOpdReferences.SingleOrDefault(static address => address is >= TableStart and < TableEndExclusive);
        var inferredDispatchBase = handlerTableCell == 0 ? 0u : handlerTableCell - ExpectedDynamicSlotOffset;

        var tableSlots = BuildTableSlots(elfBytes, functions, handlerOpd, processBridgeOpd);
        var category5Consumer = functions.GetValueOrDefault("00a2b060");
        var wrapper = functions.GetValueOrDefault("00a2bd18");
        var processBridge = functions.GetValueOrDefault("00a291c0");

        using var clcMoveContract = JsonDocument.Parse(await File.ReadAllTextAsync(clcMoveContractPath));
        using var serverDllDecoder = JsonDocument.Parse(await File.ReadAllTextAsync(serverDllUsercmdDecoderPath));
        using var clcContext = File.Exists(clcMoveContextPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(clcMoveContextPath))
            : null;

        var category5Tokens = BuildCategory5Tokens(category5Consumer?.Body ?? "");
        var wrapperTokens = BuildWrapperTokens(wrapper?.Body ?? "", clcContext?.RootElement);
        var bridgeTokens = BuildBridgeTokens(processBridge?.Body ?? "");
        var clcSummary = clcMoveContract.RootElement.GetProperty("Summary");
        var serverDllSummary = serverDllDecoder.RootElement.GetProperty("Summary");

        var category5RouteRecovered = category5Tokens.Contains("dynamic-handler-slot-0x98", StringComparer.Ordinal)
            && category5Tokens.Contains("lookup-context-category-5", StringComparer.Ordinal);
        var handlerOpdRecovered = handlerOpd != 0 && handlerTableCell != 0;
        var callConventionMatches = wrapperTokens.Contains("subtracts-eight-from-dispatch-object", StringComparer.Ordinal)
            && wrapperTokens.Contains("calls-process-usercmds-bridge", StringComparer.Ordinal);
        var clcMoveReadContractRecovered =
            ReadString(clcSummary, "ReadFromBufferFunction") == "008c8a50"
            && ReadString(clcSummary, "ProcessUsercmdsBridgeFunction") == "00a291c0"
            && ReadInt(clcSummary, "NewCommandBits") == 4
            && ReadInt(clcSummary, "BackupCommandBits") == 3
            && ReadInt(clcSummary, "CommandDataLengthBits") == 16;
        var serverDllUsercmdDecoderRecovered =
            ReadString(serverDllSummary, "DecoderEntry") == "1021c080"
            && ReadString(serverDllSummary, "CallerEntry") == "10125420"
            && ReadInt(serverDllSummary, "MaxCommandsPerBatch") == 28
            && ReadFlexibleInt(serverDllSummary, "CUserCmdStrideBytes") == 0x40;
        var exactVptrWriteRecovered = FindWordReferences(elfBytes, inferredDispatchBase).Any();

        var gates = new[]
        {
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "category-5-consumer-route-recovered",
                category5RouteRecovered ? "proven" : "missing",
                "00a2b060",
                "00a2b060 must route category 5 context from 00872460 into an indirect +0x98 handler."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "category-5-handler-table-cell-recovered",
                handlerOpdRecovered ? "proven" : "missing",
                $"0x{handlerTableCell:x8} -> 0x{handlerOpd:x8} -> 00a2bd18",
                "The BLES TF.elf table cell for the usercmd wrapper must be found in the executable."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "handler-call-convention-matches-category-5-dispatch-object",
                callConventionMatches ? "proven" : "missing",
                "00a2bd18",
                "The wrapper subtracts 8 from the dispatch object before calling 00a291c0, matching the 00a2b060 iVar9 + 8 call convention."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "clc-move-read-contract-recovered",
                clcMoveReadContractRecovered ? "proven" : "missing",
                clcMoveContractPath,
                "The client CLC_Move object must expose new/backup command counts and command-data bit count."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "official-serverdll-usercmd-decoder-recovered",
                serverDllUsercmdDecoderRecovered ? "proven" : "missing",
                serverDllUsercmdDecoderPath,
                "The official EA server.dll usercmd decoder must provide the server-side CUserCmd field layout."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "exact-category-5-vptr-write-recovered",
                exactVptrWriteRecovered ? "proven" : "missing",
                inferredDispatchBase == 0 ? "unknown" : $"0x{inferredDispatchBase:x8}",
                "A constructor/object install write for the inferred vptr base is still needed before this is a fully proven vtable binding."),
            new Tf2Ps3SourceCategory5UsercmdHandlerGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "The native replacement server still needs to consume markerless category-5 packets through this contract and pass live/PCAP map-load verification.")
        };

        var report = new Tf2Ps3SourceCategory5UsercmdHandlerReport(
            "tf2ps3-source-category5-usercmd-handler-map",
            "Pins the strongest recovered TF.elf category-5 Source/usercmd handler path. This resolves the CLC/usercmd handler body and table cell, while keeping native-readiness blocked on the remaining vptr-install proof and server implementation.",
            new Tf2Ps3SourceCategory5UsercmdHandlerInputs(
                cExportPath,
                tfElfPath,
                clcMoveContextPath,
                clcMoveContractPath,
                serverDllUsercmdDecoderPath),
            new Tf2Ps3SourceCategory5UsercmdHandlerSummary(
                $"0x{handlerOpd:x8}",
                $"0x{processBridgeOpd:x8}",
                handlerTableCell == 0 ? "" : $"0x{handlerTableCell:x8}",
                inferredDispatchBase == 0 ? "" : $"0x{inferredDispatchBase:x8}",
                $"0x{ExpectedDynamicSlotOffset:x}",
                tableSlots.Length,
                category5RouteRecovered,
                handlerOpdRecovered,
                callConventionMatches,
                clcMoveReadContractRecovered,
                serverDllUsercmdDecoderRecovered,
                exactVptrWriteRecovered,
                false,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            new Tf2Ps3SourceCategory5UsercmdHandlerConsumer(
                "00a2b060",
                "category-5-source-usercmd-consumer",
                "00872460(..., 5)",
                "*(iVar9 + 8) + 0x98",
                category5Tokens,
                Preview(category5Consumer?.Lines ?? [])),
            new Tf2Ps3SourceCategory5UsercmdHandlerWrapper(
                "00a2bd18",
                $"0x{handlerOpd:x8}",
                handlerTableCell == 0 ? "" : $"0x{handlerTableCell:x8}",
                "00a291c0",
                wrapperTokens,
                Preview(wrapper?.Lines ?? [])),
            new Tf2Ps3SourceCategory5UsercmdProcessBridge(
                "00a291c0",
                $"0x{processBridgeOpd:x8}",
                bridgeTokens,
                Preview(processBridge?.Lines ?? [])),
            new Tf2Ps3SourceCategory5UsercmdKnownContracts(
                ReadString(clcSummary, "ReadFromBufferFunction"),
                ReadString(clcSummary, "WriteToBufferFunction"),
                ReadInt(clcSummary, "NewCommandBits"),
                ReadInt(clcSummary, "BackupCommandBits"),
                ReadInt(clcSummary, "CommandDataLengthBits"),
                ReadString(serverDllSummary, "DecoderEntry"),
                ReadString(serverDllSummary, "CallerEntry"),
                ReadInt(serverDllSummary, "MaxCommandsPerBatch"),
                $"0x{ReadFlexibleInt(serverDllSummary, "CUserCmdStrideBytes"):x}"),
            tableSlots,
            gates,
            [
                "The markerless Source input issue was not a GameManager packet problem. It is the native PS3 Source movement/input route.",
                "The BLES TF.elf table cell 0180b80c points to OPD 0190cdc8, whose entry is 00a2bd18.",
                "00a2bd18 subtracts 8 from the dispatch object and calls 00a291c0, matching the 00a2b060 dynamic call object shape iVar9 + 8.",
                "00a291c0 is the recovered ProcessUsercmds bridge. It consumes CLC_Move command counts and bitstream state, then calls the game client interface.",
                "The exact constructor/vptr write for inferred base 0180b774 is still not recovered from the current static scan. That is why this report upgrades the handler path but does not mark native Source input ready.",
                "Next implementation work should route markerless category-5 client payloads into the CLC_Move/usercmd decoder and produce server state/snapshot responses from native field contracts, not PCAP byte replay."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceCategory5UsercmdHandlerTableSlot[] BuildTableSlots(
        byte[] elfBytes,
        IReadOnlyDictionary<string, ExportedFunction> functions,
        uint handlerOpd,
        uint processBridgeOpd)
    {
        var slots = new List<Tf2Ps3SourceCategory5UsercmdHandlerTableSlot>();
        for (var address = TableStart; address < TableEndExclusive; address += 4)
        {
            var raw = ReadU32(elfBytes, address);
            var kind = "raw-word";
            string entry = "";
            string name = "";
            if (raw is >= 0x01800000 and < 0x01c00000 && CanReadVirtual(elfBytes, raw))
            {
                var functionEntry = ReadU32(elfBytes, raw);
                var key = $"{functionEntry:x8}";
                if (functions.TryGetValue(key, out var function))
                {
                    kind = "opd-function";
                    entry = key;
                    name = function.Name;
                }
                else if (raw == 0x0180b838)
                {
                    kind = "rtti-or-typeinfo-pointer";
                }
            }
            else if ((raw & 0xffff0000) == 0xffff0000)
            {
                kind = "offset-to-top";
            }

            slots.Add(new Tf2Ps3SourceCategory5UsercmdHandlerTableSlot(
                $"0x{address:x8}",
                $"0x{raw:x8}",
                kind,
                entry,
                name,
                RoleForSlot(address, raw, handlerOpd, processBridgeOpd)));
        }

        return slots.ToArray();
    }

    private static string RoleForSlot(uint address, uint raw, uint handlerOpd, uint processBridgeOpd)
    {
        if (raw == handlerOpd)
        {
            return "category-5-usercmd-handler-wrapper-slot";
        }

        if (raw == processBridgeOpd)
        {
            return "direct-process-usercmds-bridge-slot";
        }

        if (address == 0x0180b748 || address == 0x0180b7e8 || address == 0x0180b824)
        {
            return "vtable-offset-to-top-marker";
        }

        if (raw == 0x0180b838)
        {
            return "shared-rtti-or-typeinfo-pointer";
        }

        return "";
    }

    private static string[] BuildCategory5Tokens(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "param_1[0x20b0]", "source-queued-client-command-count-20b0");
        AddIf(body, tokens, "FUN_00870968(auStack_80,auStack_177d0,96000,0,-1)", "stack-bitreader-init-96000");
        AddIf(body, tokens, "_opd_FUN_00a2ae20", "populate-local-clc-move-reader");
        AddIf(body, tokens, "_opd_FUN_008722a0(*piVar1,&local_c0)", "route-local-bitreader-through-008722a0");
        AddIf(body, tokens, "_opd_FUN_00872460(iVar9,5)", "lookup-context-category-5");
        AddIf(body, tokens, "+ 0x98", "dynamic-handler-slot-0x98");
        AddIf(body, tokens, "(*(code *)*puVar8)(iVar9 + 8", "dispatch-object-iVar9-plus8");
        AddIf(body, tokens, "iVar10", "passes-context5-to-dynamic-handler");
        AddIf(body, tokens, "_opd_FUN_00874458", "post-router-call-00874458");
        return tokens.ToArray();
    }

    private static string[] BuildWrapperTokens(string body, JsonElement? clcContextRoot)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "param_1 + -8", "subtracts-eight-from-dispatch-object");
        AddIf(body, tokens, "_opd_FUN_00a291c0", "calls-process-usercmds-bridge");
        AddIf(body, tokens, "param_2 + 0x1c", "passes-clc-move-command-data-reader");
        AddIf(body, tokens, "param_2 + 0x14", "reads-new-command-count");
        AddIf(body, tokens, "param_2 + 0x10", "reads-backup-command-count");
        AddIf(body, tokens, "param_2 + 0x18", "validates-command-data-bit-count");

        if (clcContextRoot is not null)
        {
            var contextBlob = clcContextRoot.Value.GetRawText();
            AddIf(contextBlob, tokens, "\"entry\": \"00a2bd18\"", "ghidra-context-exported-wrapper");
            AddIf(contextBlob, tokens, "\"from\": \"00a2bd1c\"", "ghidra-branch-from-wrapper-to-process-usercmds");
        }

        return tokens.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] BuildBridgeTokens(string body)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "param_2 + 0x1c", "passes-clc-move-command-data-reader");
        AddIf(body, tokens, "*(undefined4 *)(param_2 + 0x14)", "passes-new-command-count");
        AddIf(body, tokens, "iVar3 + iVar7", "passes-total-command-count");
        AddIf(body, tokens, "param_2 + 0x18", "validates-command-data-bit-count");
        AddIf(body, tokens, "PTR_DAT_019771e4", "calls-game-client-interface");
        AddIf(body, tokens, "ProcessUsercmds", "process-usercmds-error-string");
        return tokens.ToArray();
    }

    private static IEnumerable<uint> FindWordReferences(byte[] bytes, uint value)
    {
        var needle = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(needle, value);
        for (var i = 0; i <= bytes.Length - needle.Length; i++)
        {
            if (bytes[i] == needle[0]
                && bytes[i + 1] == needle[1]
                && bytes[i + 2] == needle[2]
                && bytes[i + 3] == needle[3])
            {
                yield return checked((uint)i + VirtualFileBias);
            }
        }
    }

    private static uint FindUniqueWordReference(byte[] bytes, uint value)
    {
        var refs = FindWordReferences(bytes, value).ToArray();
        return refs.Length == 1 ? refs[0] : 0;
    }

    private static uint ReadU32(byte[] bytes, uint virtualAddress)
    {
        var offset = checked((int)(virtualAddress - VirtualFileBias));
        return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    }

    private static bool CanReadVirtual(byte[] bytes, uint virtualAddress)
    {
        if (virtualAddress < VirtualFileBias)
        {
            return false;
        }

        var offset = virtualAddress - VirtualFileBias;
        return offset <= bytes.Length - 4;
    }

    private static void AddIf(string haystack, List<string> tokens, string needle, string token)
    {
        if (haystack.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var starts = new List<(int Index, string Name, string Address)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            var match = FunctionDefinitionRegex.Match(trimmed);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(trimmed);
                if (match.Success && trimmed.EndsWith(';'))
                {
                    match = Match.Empty;
                }
            }

            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
            }
        }

        var functions = new List<ExportedFunction>(starts.Count);
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i].Index;
            var endExclusive = i + 1 < starts.Count ? starts[i + 1].Index : lines.Length;
            var functionLines = lines[start..endExclusive];
            functions.Add(new ExportedFunction(
                starts[i].Address,
                starts[i].Name,
                start + 1,
                endExclusive,
                string.Join('\n', functionLines),
                functionLines));
        }

        return functions;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;
    }

    private static int ReadFlexibleInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.GetInt32();
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString() ?? "";
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
            {
                return hex;
            }

            if (int.TryParse(text, out var decimalValue))
            {
                return decimalValue;
            }
        }

        return 0;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static string Preview(string[] lines)
    {
        var text = string.Join('\n', lines.Take(80));
        return text.Length <= 5000 ? text : text[..5000];
    }

    private sealed record ExportedFunction(
        string Address,
        string Name,
        int StartLine,
        int EndLine,
        string Body,
        string[] Lines);
}

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerReport(
    string Status,
    string Note,
    Tf2Ps3SourceCategory5UsercmdHandlerInputs Inputs,
    Tf2Ps3SourceCategory5UsercmdHandlerSummary Summary,
    Tf2Ps3SourceCategory5UsercmdHandlerConsumer Category5Consumer,
    Tf2Ps3SourceCategory5UsercmdHandlerWrapper HandlerWrapper,
    Tf2Ps3SourceCategory5UsercmdProcessBridge ProcessUsercmdsBridge,
    Tf2Ps3SourceCategory5UsercmdKnownContracts KnownContracts,
    Tf2Ps3SourceCategory5UsercmdHandlerTableSlot[] TableNeighborhood,
    Tf2Ps3SourceCategory5UsercmdHandlerGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerInputs(
    string CExport,
    string TfElf,
    string ClcMoveContext,
    string ClcMoveContract,
    string ServerDllUsercmdDecoder);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerSummary(
    string HandlerWrapperOpdAddress,
    string ProcessUsercmdsBridgeOpdAddress,
    string HandlerTableCellAddress,
    string InferredDispatchTableBaseForSlot98,
    string DynamicSlotOffset,
    int TableNeighborhoodSlotCount,
    bool Category5RouteRecovered,
    bool HandlerOpdTableCellRecovered,
    bool HandlerCallConventionMatchesDispatchObject,
    bool ClcMoveReadContractRecovered,
    bool OfficialServerDllUsercmdDecoderRecovered,
    bool ExactCategory5VptrWriteRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerConsumer(
    string Address,
    string Role,
    string ContextLookup,
    string DynamicHandlerExpression,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerWrapper(
    string Address,
    string OpdAddress,
    string TableCellAddress,
    string Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceCategory5UsercmdProcessBridge(
    string Address,
    string OpdAddress,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceCategory5UsercmdKnownContracts(
    string ClcMoveReadFromBufferFunction,
    string ClcMoveWriteToBufferFunction,
    int NewCommandBits,
    int BackupCommandBits,
    int CommandDataLengthBits,
    string ServerDllDecoderEntry,
    string ServerDllCallerEntry,
    int MaxCommandsPerBatch,
    string CUserCmdStrideBytes);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerTableSlot(
    string Address,
    string RawValue,
    string Kind,
    string FunctionEntry,
    string FunctionName,
    string Role);

public sealed record Tf2Ps3SourceCategory5UsercmdHandlerGate(
    string Id,
    string Status,
    string Evidence,
    string Requirement);
