using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllUserCmdDecoderReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UserCmdDecoderFieldExpectation[] ExpectedFields =
    [
        new("command_number", "0x04", "int32", 32, "previous + 1", "decoded when presence bit is set"),
        new("tick_count", "0x08", "int32", 32, "previous + 1", "decoded when presence bit is set"),
        new("viewangles.x", "0x0c", "uint32/raw-angle", 32, "previous/default copy", "raw 32-bit angle storage"),
        new("viewangles.y", "0x10", "uint32/raw-angle", 32, "previous/default copy", "raw 32-bit angle storage"),
        new("viewangles.z", "0x14", "uint32/raw-angle", 32, "previous/default copy", "raw 32-bit angle storage"),
        new("forwardmove", "0x18", "float-from-signed16", 16, "previous/default copy", "signed 16-bit value converted to float"),
        new("sidemove", "0x1c", "float-from-signed16", 16, "previous/default copy", "signed 16-bit value converted to float"),
        new("upmove", "0x20", "float-from-signed16", 16, "previous/default copy", "signed 16-bit value converted to float"),
        new("buttons", "0x24", "uint32", 32, "previous/default copy", "button bitmask"),
        new("impulse", "0x28", "byte", 8, "previous/default copy", "single-byte impulse command"),
        new("weaponselect", "0x2c", "uint11", 11, "previous/default copy", "decoded only when weapon presence bit is set"),
        new("weaponsubtype", "0x30", "uint6", 6, "previous/default copy", "nested under weaponselect decode block"),
        new("random_seed", "0x34", "derived", 0, "FUN_102b5490(command_number) & 0x7fffffff", "not present-bit encoded"),
        new("mousedx", "0x38", "int16", 16, "previous/default copy", "controller/mouse x delta"),
        new("mousedy", "0x3a", "int16", 16, "previous/default copy", "controller/mouse y delta"),
        new("hasbeenpredicted", "0x3c", "engine-owned-byte", 0, "previous/default copy or engine-side initialization", "not decoded by 1021c080")
    ];

    public static async Task ReduceAsync(string targetFunctionsPath, string layoutPath, string outputPath)
    {
        using var targetDocument = JsonDocument.Parse(await File.ReadAllTextAsync(targetFunctionsPath));
        using var layoutDocument = JsonDocument.Parse(await File.ReadAllTextAsync(layoutPath));
        var decoder = FindTargetFunction(targetDocument.RootElement, "1021c080");
        var body = decoder.GetProperty("body").GetString() ?? "";
        var lines = body.Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .ToArray();

        var fields = ExpectedFields
            .Select(field => BuildField(field, lines))
            .ToArray();
        var bitReader = BuildBitReader(lines);
        var copyPrevious = lines.Any(static line => line.Contains("FUN_1019be50(param_3)", StringComparison.Ordinal));
        var randomSeed = lines.Any(static line => line.Contains("FUN_102b5490(*(undefined4 *)(param_2 + 4))", StringComparison.Ordinal)
            && line.Contains("FUN_102b5490", StringComparison.Ordinal))
            || lines.Any(static line => line.Contains("FUN_102b5490", StringComparison.Ordinal));
        var layoutSummary = layoutDocument.RootElement.GetProperty("Summary");

        var report = new Eatf2ServerDllUserCmdDecoderReport(
            "eatf2-serverdll-usercmd-decoder-contract",
            "Field-level contract for official server.dll function 1021c080, the delta-compressed CUserCmd decoder called by CBasePlayer::ProcessUsercmds.",
            targetFunctionsPath,
            layoutPath,
            new Eatf2ServerDllUserCmdDecoderSummary(
                "1021c080",
                "10125420",
                layoutSummary.GetProperty("MaxCommandsPerBatch").GetInt32(),
                layoutSummary.GetProperty("CUserCmdStrideBytes").GetString() ?? "0x40",
                fields.Length,
                fields.Count(static field => field.DecodeKind == "presence-bit"),
                fields.Count(static field => field.DecodeKind == "derived"),
                fields.Count(static field => field.DecodeKind == "not-decoded"),
                fields.Count(static field => field.Present),
                copyPrevious,
                bitReader.HasBitBufferState,
                randomSeed),
            bitReader,
            fields,
            BuildFindings(fields, copyPrevious, randomSeed));

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

    private static Eatf2ServerDllUserCmdDecodedField BuildField(
        UserCmdDecoderFieldExpectation field,
        IReadOnlyList<string> lines)
    {
        var writeNeedle = field.OffsetHex switch
        {
            "0x04" => "param_2 + 4",
            "0x08" => "param_2 + 8",
            _ => $"param_2 + {NormalizeHexOffset(field.OffsetHex)}"
        };
        var evidence = lines
            .Where(line => line.Contains(writeNeedle, StringComparison.Ordinal)
                || (field.Name == "random_seed" && line.Contains("FUN_102b5490", StringComparison.Ordinal)))
            .Take(6)
            .ToArray();
        var decodeKind = field.BitWidth == 0
            ? field.Name == "random_seed" ? "derived" : "not-decoded"
            : "presence-bit";
        var present = field.Name == "hasbeenpredicted"
            ? evidence.Length == 0
            : evidence.Length > 0;

        return new Eatf2ServerDllUserCmdDecodedField(
            field.Name,
            field.OffsetHex,
            field.ValueType,
            field.BitWidth,
            decodeKind,
            field.AbsentBehavior,
            field.Notes,
            present,
            evidence,
            MaskFor(field.BitWidth));
    }

    private static string MaskFor(int bitWidth)
    {
        return bitWidth switch
        {
            32 => "_DAT_104041f8 / 0xffffffff",
            16 => "_DAT_104041b8 / 0xffff",
            11 => "_DAT_104041a4 / 0x7ff",
            8 => "_DAT_10404198 / 0xff",
            6 => "_DAT_10404190 / 0x3f",
            _ => ""
        };
    }

    private static string NormalizeHexOffset(string offset)
    {
        return $"0x{Convert.ToInt32(offset, 16):x}";
    }

    private static Eatf2ServerDllUserCmdBitReaderContract BuildBitReader(IReadOnlyList<string> lines)
    {
        return new Eatf2ServerDllUserCmdBitReaderContract(
            "param_1 + 0x04",
            "param_1 + 0x10",
            "param_1 + 0x14",
            "param_1 + 0x18",
            "param_1 + 0x1c",
            lines.Any(static line => line.Contains("param_1 + 4", StringComparison.Ordinal)
                || line.Contains("iVar7 + 4", StringComparison.Ordinal)),
            lines.Any(static line => line.Contains("param_1 + 0x10", StringComparison.Ordinal)
                || line.Contains("iVar7 + 0x10", StringComparison.Ordinal)),
            lines.Any(static line => line.Contains("param_1 + 0x14", StringComparison.Ordinal)
                || line.Contains("iVar7 + 0x14", StringComparison.Ordinal)),
            lines.Any(static line => line.Contains("param_1 + 0x18", StringComparison.Ordinal)
                || line.Contains("iVar7 + 0x18", StringComparison.Ordinal)),
            lines.Any(static line => line.Contains("param_1 + 0x1c", StringComparison.Ordinal)
                || line.Contains("iVar7 + 0x1c", StringComparison.Ordinal)),
            [
                "Each field starts by consuming one presence bit from currentWord/currentBits.",
                "When the bit buffer empties, the decoder loads the next 32-bit word from cursor unless cursor has reached end.",
                "On cursor/end overrun, failed flag is set and decoded field values fall back to zero for the active read.",
                "Fixed-width reads use mask table 10404178 plus specialized masks at 104041f8/104041b8/104041a4/10404198/10404190."
            ]);
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Eatf2ServerDllUserCmdDecodedField> fields,
        bool copyPrevious,
        bool randomSeed)
    {
        var findings = new List<string>
        {
            "1021c080 is not a raw PC Source packet parser; it is the official CUserCmd delta decoder used after the engine message layer has supplied a bitstream reader context.",
            "The decoder first copies the previous/default CUserCmd into the destination, then applies presence-bit guarded overrides.",
            "The native PS3 server should decode incoming client command payloads into this field model before movement and snapshot publication."
        };

        if (copyPrevious)
        {
            findings.Add("Absent fields inherit from the previous/default command, except command_number and tick_count which increment by one when absent.");
        }

        if (randomSeed)
        {
            findings.Add("random_seed is derived from command_number through FUN_102b5490 and masked with 0x7fffffff; it is not transmitted as a separate field.");
        }

        var notPresent = fields.Where(static field => !field.Present).Select(static field => field.Name).ToArray();
        if (notPresent.Length > 0)
        {
            findings.Add($"Fields lacking decoder evidence: {string.Join(", ", notPresent)}.");
        }

        return findings.ToArray();
    }

    private sealed record UserCmdDecoderFieldExpectation(
        string Name,
        string OffsetHex,
        string ValueType,
        int BitWidth,
        string AbsentBehavior,
        string Notes);
}

public sealed record Eatf2ServerDllUserCmdDecoderReport(
    string Status,
    string Note,
    string TargetFunctionsInput,
    string UserCmdLayoutInput,
    Eatf2ServerDllUserCmdDecoderSummary Summary,
    Eatf2ServerDllUserCmdBitReaderContract BitReader,
    Eatf2ServerDllUserCmdDecodedField[] Fields,
    string[] Findings);

public sealed record Eatf2ServerDllUserCmdDecoderSummary(
    string DecoderEntry,
    string CallerEntry,
    int MaxCommandsPerBatch,
    string CUserCmdStrideBytes,
    int FieldCount,
    int PresenceBitFieldCount,
    int DerivedFieldCount,
    int NotDecodedFieldCount,
    int FieldsWithEvidence,
    bool CopiesPreviousCommand,
    bool HasBitReaderState,
    bool DerivesRandomSeed);

public sealed record Eatf2ServerDllUserCmdBitReaderContract(
    string FailedFlagOffset,
    string CurrentWordOffset,
    string RemainingBitsOffset,
    string CursorOffset,
    string EndOffset,
    bool HasFailedFlagEvidence,
    bool HasCurrentWordEvidence,
    bool HasRemainingBitsEvidence,
    bool HasCursorEvidence,
    bool HasEndEvidence,
    string[] Semantics)
{
    public bool HasBitBufferState =>
        HasFailedFlagEvidence
        && HasCurrentWordEvidence
        && HasRemainingBitsEvidence
        && HasCursorEvidence
        && HasEndEvidence;
}

public sealed record Eatf2ServerDllUserCmdDecodedField(
    string Name,
    string OffsetHex,
    string ValueType,
    int BitWidth,
    string DecodeKind,
    string AbsentBehavior,
    string Notes,
    bool Present,
    string[] Evidence,
    string MaskEvidence);
