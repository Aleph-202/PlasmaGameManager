using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Eatf2ServerDllUserCmdLayoutReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UserCmdFieldExpectation[] ExpectedFields =
    [
        new("vtable", "0x00", 4, "CUserCmd::vftable", "engine object discriminator; not client-controlled"),
        new("command_number", "0x04", 4, "0", "monotonic command id"),
        new("tick_count", "0x08", 4, "0", "client simulation tick"),
        new("viewangles.x", "0x0c", 4, "0.0f", "pitch-like view angle"),
        new("viewangles.y", "0x10", 4, "0.0f", "yaw-like view angle"),
        new("viewangles.z", "0x14", 4, "0.0f", "roll-like view angle"),
        new("forwardmove", "0x18", 4, "0.0f", "forward movement wish speed"),
        new("sidemove", "0x1c", 4, "0.0f", "side movement wish speed"),
        new("upmove", "0x20", 4, "0.0f", "vertical movement wish speed"),
        new("buttons", "0x24", 4, "0", "IN_* button bitmask"),
        new("impulse", "0x28", 1, "0", "single-byte impulse command"),
        new("weaponselect", "0x2c", 4, "0", "selected weapon entity/id"),
        new("weaponsubtype", "0x30", 4, "0", "selected weapon subtype"),
        new("random_seed", "0x34", 4, "0", "prediction random seed"),
        new("mousedx", "0x38", 2, "0", "mouse/controller x delta"),
        new("mousedy", "0x3a", 2, "0", "mouse/controller y delta"),
        new("hasbeenpredicted", "0x3c", 1, "0", "prediction state flag")
    ];

    public static async Task ReduceAsync(string targetFunctionsPath, string runtimeContractPath, string outputPath)
    {
        using var targetDocument = JsonDocument.Parse(await File.ReadAllTextAsync(targetFunctionsPath));
        using var runtimeDocument = JsonDocument.Parse(await File.ReadAllTextAsync(runtimeContractPath));
        var processUsercmds = FindTargetFunction(targetDocument.RootElement, "10125420");
        var instructions = processUsercmds.GetProperty("instructions")
            .EnumerateArray()
            .Select(static instruction => new Instruction(
                instruction.GetProperty("address").GetString() ?? "",
                instruction.GetProperty("text").GetString() ?? ""))
            .ToArray();
        var runtimeUserCmd = runtimeDocument.RootElement.GetProperty("UserCmdBatch");

        var fields = ExpectedFields
            .Select(field => BuildFieldEvidence(field, instructions))
            .ToArray();
        var decodeCall = BuildDecodeCall(instructions);
        var batch = BuildBatchEvidence(instructions, runtimeUserCmd);

        var report = new Eatf2ServerDllUserCmdLayoutReport(
            "eatf2-serverdll-usercmd-layout",
            "Official EA TF2 server.dll CUserCmd batch layout recovered from CBasePlayer::ProcessUsercmds. This gives the native replacement an explicit semantic target for PS3 command-payload decoding.",
            targetFunctionsPath,
            runtimeContractPath,
            new Eatf2ServerDllUserCmdLayoutSummary(
                "10125420",
                runtimeUserCmd.GetProperty("CommandDecodeFunction").GetString() ?? "1021c080",
                runtimeUserCmd.GetProperty("MaxCommandsPerBatch").GetInt32(),
                runtimeUserCmd.GetProperty("CUserCmdStrideBytes").GetString() ?? "0x40",
                ExpectedFields.Length,
                fields.Count(static field => field.Present),
                decodeCall.Present,
                batch.BackwardDecodeLoopPresent),
            batch,
            decodeCall,
            fields,
            BuildFindings(fields, decodeCall, batch));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static JsonElement FindTargetFunction(JsonElement root, string entry)
    {
        foreach (var function in root.GetProperty("targetFunctions").EnumerateArray())
        {
            if (function.GetProperty("entry").GetString() == entry)
            {
                return function;
            }
        }

        throw new InvalidOperationException($"Missing target function {entry}");
    }

    private static Eatf2ServerDllUserCmdField BuildFieldEvidence(
        UserCmdFieldExpectation field,
        IReadOnlyList<Instruction> instructions)
    {
        var offset = Convert.ToInt32(field.OffsetHex, 16);
        var stackOffset = 0x14 + offset;
        var stackCandidate = $"[ESP + 0x{stackOffset:x}]";
        var loopCandidate = $"[EAX + -0x{0x40 - offset:x}]";
        var evidence = instructions.FirstOrDefault(instruction =>
            instruction.Text.Contains(stackCandidate, StringComparison.OrdinalIgnoreCase)
            && IsInitializerForSize(instruction.Text, field.SizeBytes));
        evidence ??= instructions.FirstOrDefault(instruction =>
            instruction.Text.Contains(loopCandidate, StringComparison.OrdinalIgnoreCase)
            && IsInitializerForSize(instruction.Text, field.SizeBytes));

        return new Eatf2ServerDllUserCmdField(
            field.Name,
            field.OffsetHex,
            field.SizeBytes,
            field.Initializer,
            field.Semantics,
            evidence is not null,
            evidence?.Address ?? "",
            evidence?.Text ?? "",
            NativeMappingFor(field.Name));
    }

    private static bool IsInitializerForSize(string instruction, int sizeBytes)
    {
        if (instruction.Contains("CUserCmd::vftable", StringComparison.Ordinal)
            || instruction.Contains("0x1038ec5c", StringComparison.Ordinal))
        {
            return true;
        }

        return sizeBytes switch
        {
            1 => instruction.Contains("byte ptr", StringComparison.OrdinalIgnoreCase),
            2 => instruction.Contains("word ptr", StringComparison.OrdinalIgnoreCase),
            4 => instruction.Contains("dword ptr", StringComparison.OrdinalIgnoreCase)
                || instruction.Contains("float ptr", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static Eatf2ServerDllUserCmdDecodeCall BuildDecodeCall(IReadOnlyList<Instruction> instructions)
    {
        var index = instructions.ToList().FindIndex(static instruction => instruction.Text == "CALL 0x1021c080");
        if (index < 3)
        {
            return new Eatf2ServerDllUserCmdDecodeCall(false, "1021c080", "", [], []);
        }

        var pushes = instructions.Skip(index - 3).Take(3).ToArray();
        var parameters = new[]
        {
            new Eatf2ServerDllUserCmdDecodeParameter("param_1", "input/reader context", pushes[2].Text, pushes[2].Address),
            new Eatf2ServerDllUserCmdDecodeParameter("param_2", "destination CUserCmd record", pushes[1].Text, pushes[1].Address),
            new Eatf2ServerDllUserCmdDecodeParameter("param_3", "previous/default CUserCmd record", pushes[0].Text, pushes[0].Address)
        };

        return new Eatf2ServerDllUserCmdDecodeCall(
            true,
            "1021c080",
            instructions[index].Address,
            pushes.Select(static instruction => $"{instruction.Address}: {instruction.Text}").Append($"{instructions[index].Address}: {instructions[index].Text}").ToArray(),
            parameters);
    }

    private static Eatf2ServerDllUserCmdBatchEvidence BuildBatchEvidence(
        IReadOnlyList<Instruction> instructions,
        JsonElement runtimeUserCmd)
    {
        var hasLimitCheck = instructions.Any(static instruction => instruction.Text == "CMP EDI,0x1c")
            && instructions.Any(static instruction => instruction.Text == "JA 0x10125666");
        var hasStrideShift = instructions.Any(static instruction => instruction.Text == "SHL ECX,0x6");
        var hasBackwardStep = instructions.Any(static instruction => instruction.Text == "SUB ESI,0x40");
        var hasDispatch = instructions.Any(static instruction => instruction.Text.Contains("[EDX + 0x5b4]", StringComparison.Ordinal));
        var hasReturnedFrameTime = instructions.Any(static instruction => instruction.Text.Contains("[EAX + 0x1c]", StringComparison.Ordinal));

        return new Eatf2ServerDllUserCmdBatchEvidence(
            runtimeUserCmd.GetProperty("MaxCommandsPerBatch").GetInt32(),
            runtimeUserCmd.GetProperty("CUserCmdStrideBytes").GetString() ?? "0x40",
            "29 stack records are initialized; 28 is the accepted client command maximum and one default/previous command seed is carried through the decode loop.",
            hasLimitCheck,
            hasStrideShift,
            hasBackwardStep,
            hasStrideShift && hasBackwardStep,
            hasDispatch,
            hasReturnedFrameTime,
            Evidence(instructions, "CMP EDI,0x1c", "JA 0x10125666", "SHL ECX,0x6", "SUB ESI,0x40", "[EDX + 0x5b4]", "[EAX + 0x1c]"));
    }

    private static string[] Evidence(IReadOnlyList<Instruction> instructions, params string[] needles)
    {
        return needles
            .SelectMany(needle => instructions
                .Where(instruction => instruction.Text.Contains(needle, StringComparison.Ordinal))
                .Select(instruction => $"{instruction.Address}: {instruction.Text}"))
            .Distinct()
            .ToArray();
    }

    private static string NativeMappingFor(string field)
    {
        return field switch
        {
            "viewangles.x" => "Tf2SourcePlayerState.Pitch",
            "viewangles.y" => "Tf2SourcePlayerState.Yaw",
            "forwardmove" => "LastClientCommandForwardMove / VelocityX",
            "sidemove" => "LastClientCommandSideMove / VelocityY",
            "upmove" => "LastClientCommandUpMove / jump impulse",
            "buttons" => "LastClientCommandButtons",
            "weaponselect" => "LastClientCommandWeaponSlotHint",
            "mousedx" => "LastClientCommandYawDelta",
            "mousedy" => "LastClientCommandPitchDelta",
            _ => ""
        };
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Eatf2ServerDllUserCmdField> fields,
        Eatf2ServerDllUserCmdDecodeCall decodeCall,
        Eatf2ServerDllUserCmdBatchEvidence batch)
    {
        var findings = new List<string>
        {
            "Official CUserCmd layout is 0x40 bytes and matches the classic Source command model: command/tick numbers, three view angles, three movement floats, buttons, impulse, weapon selection, random seed, mouse deltas, and prediction flag.",
            "CBasePlayer::ProcessUsercmds decodes command batches backwards through 1021c080, passing the input context, destination CUserCmd, and previous/default CUserCmd to the decoder.",
            "The native server should parse PS3 payloads into this CUserCmd field model before applying prediction, instead of treating payload bytes as one opaque movement hint."
        };

        if (fields.Any(static field => !field.Present))
        {
            findings.Add($"Fields still lacking direct initializer evidence: {string.Join(", ", fields.Where(static field => !field.Present).Select(static field => field.Name))}.");
        }

        if (!decodeCall.Present || !batch.BackwardDecodeLoopPresent)
        {
            findings.Add("Batch decode evidence is incomplete; export 1021c080 directly before treating the field mapping as parser-complete.");
        }

        return findings.ToArray();
    }

    private sealed record UserCmdFieldExpectation(
        string Name,
        string OffsetHex,
        int SizeBytes,
        string Initializer,
        string Semantics);

    private sealed record Instruction(string Address, string Text);
}

public sealed record Eatf2ServerDllUserCmdLayoutReport(
    string Status,
    string Note,
    string TargetFunctionsInput,
    string RuntimeContractInput,
    Eatf2ServerDllUserCmdLayoutSummary Summary,
    Eatf2ServerDllUserCmdBatchEvidence Batch,
    Eatf2ServerDllUserCmdDecodeCall DecodeCall,
    Eatf2ServerDllUserCmdField[] Fields,
    string[] Findings);

public sealed record Eatf2ServerDllUserCmdLayoutSummary(
    string ProcessUsercmdsEntry,
    string CommandDecodeFunction,
    int MaxCommandsPerBatch,
    string CUserCmdStrideBytes,
    int FieldCount,
    int FieldsWithInitializerEvidence,
    bool DecodeCallPresent,
    bool BackwardDecodeLoopPresent);

public sealed record Eatf2ServerDllUserCmdBatchEvidence(
    int MaxCommandsPerBatch,
    string CUserCmdStrideBytes,
    string StackRecordNote,
    bool HasMaxCommandLimitCheck,
    bool HasStrideShift,
    bool HasBackwardRecordStep,
    bool BackwardDecodeLoopPresent,
    bool PlayerProcessUsercmdsDispatchPresent,
    bool ReturnedFrameTimePresent,
    string[] Evidence);

public sealed record Eatf2ServerDllUserCmdDecodeCall(
    bool Present,
    string Function,
    string Address,
    string[] Evidence,
    Eatf2ServerDllUserCmdDecodeParameter[] Parameters);

public sealed record Eatf2ServerDllUserCmdDecodeParameter(
    string Name,
    string Meaning,
    string PushInstruction,
    string EvidenceAddress);

public sealed record Eatf2ServerDllUserCmdField(
    string Name,
    string OffsetHex,
    int SizeBytes,
    string Initializer,
    string Semantics,
    bool Present,
    string EvidenceAddress,
    string EvidenceInstruction,
    string NativeStateMapping);
