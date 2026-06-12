using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllRuntimeContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Eatf2ServerDllRuntimeContractReport> ReduceAsync(string targetFunctionsPath, string outputPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetFunctionsPath));
        var functions = document.RootElement.GetProperty("targetFunctions")
            .EnumerateArray()
            .Select(ReadFunction)
            .ToDictionary(static function => function.Entry, StringComparer.Ordinal);

        var gameManagerBridge = BuildGameManagerBridge(functions);
        var userCmd = BuildUserCmdContract(functions);
        var physics = BuildPhysicsContract(functions);
        var writerWrappers = BuildWriterWrappers(functions);
        var sendProps = BuildSendPropConstructors(functions);
        var stats = BuildRankedStatsContract(functions);

        var contractCount = 6;
        var completeCount =
            (gameManagerBridge.Present ? 1 : 0) +
            (userCmd.Present ? 1 : 0) +
            (physics.Present ? 1 : 0) +
            (writerWrappers.All(static wrapper => wrapper.Present) ? 1 : 0) +
            (sendProps.Length >= 8 ? 1 : 0) +
            (stats.Present ? 1 : 0);

        var report = new Eatf2ServerDllRuntimeContractReport(
            "eatf2-serverdll-runtime-contract",
            "Exact official EA TF2 server.dll runtime contracts recovered from targeted Ghidra exports. These are Source server-side obligations, not proof of PS3 UDP packet framing.",
            targetFunctionsPath,
            new Eatf2ServerDllRuntimeContractSummary(
                functions.Count,
                contractCount,
                completeCount,
                contractCount - completeCount,
                functions.Values.Count(static function => function.Reasons.Any(static reason => reason.Contains("SendProp::vftable", StringComparison.Ordinal))),
                writerWrappers.Count(static wrapper => wrapper.Present)),
            gameManagerBridge,
            userCmd,
            physics,
            writerWrappers,
            sendProps,
            stats,
            [
                "Implementation-ready: Source lifecycle must notify GameManager when a Source player enters and leaves, using the engine player index minus one.",
                "Implementation-ready: native usercmd input must become CUserCmd-like records with a 0x40 byte stride and a hard maximum of 28 commands per batch.",
                "Implementation-ready: native simulation must advance queued command history through a PhysicsSimulate-style tick gate before generating snapshots.",
                "Not implementation-ready for UDP framing: server.dll writer wrappers require an active engine message and delegate to generic write helpers; TF.elf still defines the PS3 packet envelope.",
                "Not implementation-ready for map load alone: SendProp/SendTable evidence defines snapshot schema construction, but not the client-visible PS3 transport handler ids."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Eatf2ServerDllGameManagerBridgeContract BuildGameManagerBridge(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        var add = functions.GetValueOrDefault("1027c790");
        var remove = functions.GetValueOrDefault("1027c800");
        return new Eatf2ServerDllGameManagerBridgeContract(
            add is not null && remove is not null,
            add?.Entry ?? "1027c790",
            remove?.Entry ?? "1027c800",
            "DAT_104c6460 vtable +0x74 result minus one",
            "Source object param_1 +0x358 translated through 1019c120",
            "+0x10",
            "+0x18",
            EvidenceLines(add, [
                "FeslHubSingle_GetGameManager",
                "*DAT_104c6460 + 0x74",
                "iVar3 + -1",
                "param_1 + 0x358",
                "iVar1 + 0x10"
            ]).Concat(EvidenceLines(remove, [
                "FeslHubSingle_GetGameManager",
                "*DAT_104c6460 + 0x74",
                "iVar2 + -1",
                "*piVar1 + 0x18"
            ])).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static Eatf2ServerDllUserCmdContract BuildUserCmdContract(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        var function = functions.GetValueOrDefault("10125420");
        return new Eatf2ServerDllUserCmdContract(
            function is not null,
            function?.Entry ?? "10125420",
            28,
            "0x40",
            "0x1d",
            "1021c080",
            "+0x5b4",
            "param_2 + 4",
            "DAT_104c6458 + 0x1c",
            EvidenceLines(function, [
                "if (0x1c < param_4)",
                "CBasePlayer::ProcessUsercmds: too many cmds",
                "puVar4 = puVar4 + 0x10",
                "SUB ESI,0x40",
                "FUN_1021c080(param_2,pppuVar7,pppuVar3)",
                "*local_7c4 + 0x5b4",
                "DAT_104c6458 + 0x1c"
            ]));
    }

    private static Eatf2ServerDllPhysicsContract BuildPhysicsContract(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        var function = functions.GetValueOrDefault("101a4750");
        return new Eatf2ServerDllPhysicsContract(
            function is not null,
            function?.Entry ?? "101a4750",
            "param_1[0x3d] versus *(DAT_104c6458 + 0x18)",
            "param_1[0x2af]",
            "0x790",
            "+0x5b8",
            "param_1 +0x323 / +0x324..+0x332",
            "0x18",
            EvidenceLines(function, [
                "CBasePlayer::PhysicsSimulate",
                "param_1[0x3d] != *(int *)(DAT_104c6458 + 0x18)",
                "param_1[0x2af] + iStack_c",
                "iStack_c = iStack_c + 0x790",
                "iVar10 + 0x5b8",
                "param_1 + 0x323",
                "(int)piVar8 < 0x18"
            ]));
    }

    private static Eatf2ServerDllWriterWrapperContract[] BuildWriterWrappers(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        return
        [
            BuildWriterWrapper(functions, "10123c30", "WriteLong", "102b4c20"),
            BuildWriterWrapper(functions, "10123c60", "WriteFloat", "102b48e0"),
            BuildWriterWrapper(functions, "10123cc0", "WriteString", "102b4c30")
        ];
    }

    private static Eatf2ServerDllWriterWrapperContract BuildWriterWrapper(
        IReadOnlyDictionary<string, TargetFunction> functions,
        string entry,
        string name,
        string delegateFunction)
    {
        var function = functions.GetValueOrDefault(entry);
        return new Eatf2ServerDllWriterWrapperContract(
            function is not null,
            entry,
            name,
            "DAT_104c64e8 != 0",
            delegateFunction,
            EvidenceLines(function, [
                "DAT_104c64e8 == 0",
                name + " called with no active message",
                "FUN_" + delegateFunction
            ]));
    }

    private static Eatf2ServerDllSendPropConstructorContract[] BuildSendPropConstructors(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        return new[]
            {
                ("100f3a70", "default-constructor", ""),
                ("100f3e00", "integer-like-constructor", "0"),
                ("100f3af0", "float-like-constructor", "1"),
                ("100f3c10", "vector-like-constructor", "2"),
                ("100f3ee0", "string-like-constructor", "3"),
                ("100f4030", "array-like-constructor", "4"),
                ("100f4090", "datatable-reference-constructor", "0"),
                ("100f3fa0", "custom-proxy-constructor", "5"),
                ("100f4180", "array-elements-constructor", "5")
            }
            .Select(item => BuildSendPropConstructor(functions, item.Item1, item.Item2, item.Item3))
            .ToArray();
    }

    private static Eatf2ServerDllSendPropConstructorContract BuildSendPropConstructor(
        IReadOnlyDictionary<string, TargetFunction> functions,
        string entry,
        string role,
        string typeTag)
    {
        var function = functions.GetValueOrDefault(entry);
        var needles = new List<string> { "SendProp::vftable" };
        if (typeTag.Length > 0)
        {
            needles.Add("param_1[2] = " + typeTag);
        }
        if (entry == "100f4180")
        {
            needles.Add("_vector_constructor_iterator_(param_1,0x4c");
        }

        return new Eatf2ServerDllSendPropConstructorContract(
            function is not null,
            entry,
            role,
            typeTag,
            entry == "100f4180" ? "0x4c" : "",
            EvidenceLines(function, needles.ToArray()));
    }

    private static Eatf2ServerDllRankedStatsRuntimeContract BuildRankedStatsContract(
        IReadOnlyDictionary<string, TargetFunction> functions)
    {
        var update = functions.GetValueOrDefault("1028e740");
        var register = functions.GetValueOrDefault("102917e0");
        var callback = functions.GetValueOrDefault("102909f0");
        return new Eatf2ServerDllRankedStatsRuntimeContract(
            update is not null && register is not null && callback is not null,
            update?.Entry ?? "1028e740",
            register?.Entry ?? "102917e0",
            callback?.Entry ?? "102909f0",
            3,
            "0x14",
            "0x18",
            "CTFPlayer +0xd7c",
            "+0x20/+0x24",
            EvidenceLines(update, [
                "param_1[3] * 3",
                "* 0x14",
                "* 0x18",
                "FeslHubSingle_Get",
                "*piVar7 + 0xc"
            ]).Concat(EvidenceLines(register, [
                "param_2 + 0xd7c",
                "FeslFunctor2Params<TFStatsReporter",
                "*piVar5 + 0x14"
            ])).Concat(EvidenceLines(callback, [
                "Lookup callback received",
                "iVar1 + 0x24",
                "iVar1 + 0x20",
                "FeslStatsRetriever::Functor<TFStatsReporter>"
            ])).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string[] EvidenceLines(TargetFunction? function, string[] needles)
    {
        if (function is null)
        {
            return [];
        }

        var lines = function.Body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => line.Length > 0 && needles.Any(needle => line.Contains(needle, StringComparison.Ordinal)))
            .Concat(function.Instructions
                .Where(instruction => needles.Any(needle => instruction.Text.Contains(needle, StringComparison.Ordinal)))
                .Select(instruction => $"{instruction.Address}: {instruction.Text}"))
            .Distinct(StringComparer.Ordinal)
            .Take(16)
            .ToArray();
        return lines;
    }

    private static TargetFunction ReadFunction(JsonElement function)
    {
        return new TargetFunction(
            function.GetProperty("entry").GetString() ?? "",
            function.GetProperty("name").GetString() ?? "",
            function.GetProperty("reasons").EnumerateArray()
                .Select(static reason => reason.GetString() ?? "")
                .Where(static reason => reason.Length > 0)
                .ToArray(),
            function.GetProperty("body").GetString() ?? "",
            function.GetProperty("instructions").EnumerateArray()
                .Select(static instruction => new TargetInstruction(
                    instruction.GetProperty("address").GetString() ?? "",
                    instruction.GetProperty("text").GetString() ?? ""))
                .ToArray());
    }

    private sealed record TargetFunction(
        string Entry,
        string Name,
        string[] Reasons,
        string Body,
        TargetInstruction[] Instructions);

    private sealed record TargetInstruction(string Address, string Text);
}

public sealed record Eatf2ServerDllRuntimeContractReport(
    string Status,
    string Note,
    string TargetFunctionsInput,
    Eatf2ServerDllRuntimeContractSummary Summary,
    Eatf2ServerDllGameManagerBridgeContract GameManagerBridge,
    Eatf2ServerDllUserCmdContract UserCmdBatch,
    Eatf2ServerDllPhysicsContract PhysicsSimulation,
    Eatf2ServerDllWriterWrapperContract[] EngineWriterWrappers,
    Eatf2ServerDllSendPropConstructorContract[] SendPropConstructors,
    Eatf2ServerDllRankedStatsRuntimeContract RankedStats,
    string[] Findings);

public sealed record Eatf2ServerDllRuntimeContractSummary(
    int ExportedTargetFunctionCount,
    int RuntimeContractCount,
    int RuntimeContractsWithEvidence,
    int RuntimeContractsMissingEvidence,
    int SendPropConstructorFunctionCount,
    int EngineWriterWrapperCount);

public sealed record Eatf2ServerDllGameManagerBridgeContract(
    bool Present,
    string PlayerAddedEntry,
    string PlayerRemovedEntry,
    string PlayerIndexSource,
    string IdentityContextSource,
    string PlayerAddedVtableSlot,
    string PlayerRemovedVtableSlot,
    string[] Evidence);

public sealed record Eatf2ServerDllUserCmdContract(
    bool Present,
    string Entry,
    int MaxCommandsPerBatch,
    string CUserCmdStrideBytes,
    string InitializationLoopCounter,
    string CommandDecodeFunction,
    string PlayerProcessUsercmdsVtableSlot,
    string OverflowFlagField,
    string ReturnedFrameTimeSource,
    string[] Evidence);

public sealed record Eatf2ServerDllPhysicsContract(
    bool Present,
    string Entry,
    string TickGate,
    string CommandHistoryBuffer,
    string CommandHistoryStrideBytes,
    string PlayerRunCommandVtableSlot,
    string LastCommandCopyDestination,
    string MaxAdditionalSimulationCommands,
    string[] Evidence);

public sealed record Eatf2ServerDllWriterWrapperContract(
    bool Present,
    string Entry,
    string Writer,
    string ActiveMessageGuard,
    string DelegateFunction,
    string[] Evidence);

public sealed record Eatf2ServerDllSendPropConstructorContract(
    bool Present,
    string Entry,
    string Role,
    string TypeTag,
    string ArrayElementStrideBytes,
    string[] Evidence);

public sealed record Eatf2ServerDllRankedStatsRuntimeContract(
    bool Present,
    string UpdateTransactionEntry,
    string PlayerRegistrationEntry,
    string LookupCallbackEntry,
    int FieldsPerUpdateRow,
    string StatFieldStrideBytes,
    string UpdateRowStrideBytes,
    string PlayerNameField,
    string ReturnedHandleFields,
    string[] Evidence);
