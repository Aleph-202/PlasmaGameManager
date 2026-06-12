using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceBitstreamHelperContractReducer
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

    public static async Task<Tf2Ps3SourceBitstreamHelperContractReport> ReduceAsync(
        string cExportPath,
        string slot70Param2FieldContractPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        using var fieldContract = JsonDocument.Parse(await File.ReadAllTextAsync(slot70Param2FieldContractPath));
        var fieldSummary = fieldContract.RootElement.GetProperty("Summary");

        var primitives = BuildPrimitives(functions);
        var addressStateHelpers = BuildAddressStateHelpers(functions);
        var wrapperLinks = BuildWrapperLinks(functions);
        var gates = BuildGates(
            fieldSummary,
            primitives,
            addressStateHelpers,
            wrapperLinks);

        var nativeSourceInputReady = false;
        var report = new Tf2Ps3SourceBitstreamHelperContractReport(
            "tf2ps3-source-bitstream-helper-contract",
            "Names the reusable TF.elf bitstream/address-state helpers that sit under the slot +0x70 Source input wrapper. This narrows native payload encoding/decoding, but does not recover the caller-side markerless-packet-to-param_2 builder.",
            new Tf2Ps3SourceBitstreamHelperContractInputs(cExportPath, slot70Param2FieldContractPath),
            new Tf2Ps3SourceBitstreamHelperContractSummary(
                primitives.Length,
                primitives.Count(static primitive => primitive.Status == "recovered"),
                addressStateHelpers.Length,
                addressStateHelpers.Count(static helper => helper.Status == "recovered"),
                wrapperLinks.Length,
                wrapperLinks.Count(static link => link.Status == "recovered"),
                ReadBool(fieldSummary, "CallerSideParam2BuilderRecovered"),
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            BuildCanonicalStateFields(),
            primitives,
            addressStateHelpers,
            wrapperLinks,
            gates,
            [
                "The compact bitstream object initialized by 01335158 is a 0x18-ish state with base pointer, byte length, total bits, bit cursor, error flag, optional owner/context, and a byte at +0x11.",
                "Reader helpers consume bits least-significant-position first from a big-endian-swapped 32-bit backing word; crossing a word boundary loads the next backing word and applies the same byte swap.",
                "Writer helpers mirror that contract: they mask the big-endian-swapped 32-bit word, OR the new value shifted by bitIndex & 0x1f, swap back, and advance the bit cursor.",
                "013391e8 writes NUL-terminated strings through the 8-bit writer; 013378e0 reads bounded NUL-terminated strings through the 8-bit reader.",
                "Slot +0x70 uses separate address-state helpers at 0134e4c0 and 0134e230 to gate whether the incoming param_2 state should be processed.",
                "The server can now reproduce the primitive bit packing contract. The remaining blocker is still the caller-side transform that builds the slot +0x70 param_2 object from markerless UDP payloads."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceBitstreamStateField[] BuildCanonicalStateFields() =>
    [
        new("+0x00", "param_1[0]", "base-pointer", "uint8*/uint32*", "Backing buffer pointer."),
        new("+0x04", "param_1[1]", "byte-length", "int32", "Input byte count used by init wrappers and external callers."),
        new("+0x08", "param_1[2]", "total-bit-count", "int32", "Explicit bit count, or byte-length << 3 when init receives -1."),
        new("+0x0c", "param_1[3]", "bit-index", "int32", "Current bit cursor advanced by read/write helpers."),
        new("+0x10", "*(undefined1 *)(param_1 + 4)", "overflow-or-error-flag", "byte", "Set when a helper would read/write beyond total-bit-count."),
        new("+0x14", "param_1[5]", "owner-or-context", "pointer/int32", "Optional context pointer stored by alternate init wrappers."),
        new("+0x11", "*(undefined1 *)((int)param_1 + 0x11)", "enabled-or-owned-byte", "byte", "Set to 1 by wrapper initializers before calling 01335158.")
    ];

    private static Tf2Ps3SourceBitstreamPrimitive[] BuildPrimitives(IReadOnlyList<ExportedFunction> functions)
    {
        return
        [
            Primitive(
                functions,
                "013350f8",
                "_opd_FUN_013350f8",
                "reset-default-state",
                "state",
                "Clears the compact bitstream state and initializes total-bit-count to 0xffffffff with the +0x11 byte set.",
                [
                    "param_1[5] = 0",
                    "param_1[2] = 0xffffffff",
                    "*(undefined1 *)((int)param_1 + 0x11) = 1",
                    "*(undefined1 *)(param_1 + 4) = 0"
                ]),
            Primitive(
                functions,
                "01335158",
                "_opd_FUN_01335158",
                "init-view",
                "state",
                "Initializes base pointer, byte length, bit cursor, total bits, and error flag. A param_5 value of -1 means total bits are byteLength << 3.",
                [
                    "*param_1 = param_2",
                    "param_1[1] = param_3",
                    "param_1[2] = param_5",
                    "param_1[3] = param_4",
                    "param_1[2] = param_3 << 3",
                    "*(undefined1 *)(param_1 + 4) = 0"
                ]),
            Primitive(
                functions,
                "013366f0",
                "_opd_FUN_013366f0",
                "read-u32",
                "read",
                "Reads 32 bits from the current bit cursor, byte-swapping the backing word before shifting by bitIndex & 0x1f.",
                [
                    "param_1[2] < param_1[3] + 0x20",
                    "param_1[3] = uVar1 + 0x20",
                    "uVar2 >> 0x18 | uVar2 >> 8 & 0xff00",
                    "(uVar1 & 0x1f)"
                ]),
            Primitive(
                functions,
                "01336ad8",
                "_opd_FUN_01336ad8",
                "read-u16",
                "read",
                "Reads 16 bits using the same big-endian-swapped backing word and boundary-crossing mask table.",
                [
                    "param_1[2] < param_1[3] + 0x10",
                    "param_1[3] = uVar1 + 0x10",
                    "uVar2 >> 0x18 | uVar2 >> 8 & 0xff00",
                    "PTR_s_env_sprite_01979a08 + 0x40"
                ]),
            Primitive(
                functions,
                "01336c10",
                "_opd_FUN_01336c10",
                "read-bits-to-bytes",
                "read",
                "Copies an arbitrary bit count from the bitstream into a byte buffer, byte at a time plus a final partial byte.",
                [
                    "void _opd_FUN_01336c10(int *param_1,byte *param_2,ulonglong param_3)",
                    "param_3 = param_3 - 8",
                    "*param_2 = bVar10",
                    "param_1[3] = iVar4"
                ]),
            Primitive(
                functions,
                "013370f0",
                "_opd_FUN_013370f0",
                "read-bytes",
                "read",
                "Byte-count wrapper over 01336c10; converts bytes to bits with param_3 << 3 and returns !error.",
                [
                    "_opd_FUN_01336c10(param_1,param_2,(longlong)(param_3 << 3))",
                    "return *(byte *)(param_1 + 4) ^ 1"
                ]),
            Primitive(
                functions,
                "013376e8",
                "_opd_FUN_013376e8",
                "read-signed-nbits",
                "read",
                "Reads param_2 - 1 value bits followed by one sign bit, subtracting the sign magnitude mask when the sign bit is set.",
                [
                    "iVar3 = param_2 + -1",
                    "1 << (uVar4 & 7)",
                    "return uVar5 - *(int *)(PTR_PTR_019799f8 + (iVar3 * 4 & 0x7c))"
                ]),
            Primitive(
                functions,
                "013378e0",
                "_opd_FUN_013378e0",
                "read-string",
                "read",
                "Reads bytes through 013378d8 until NUL, newline if requested, or output capacity, then returns success only when no overflow happened.",
                [
                    "uVar2 = _opd_FUN_013378d8(param_1)",
                    "cVar3 == '\\0'",
                    "param_3 + -1 <= iVar4",
                    "*(undefined1 *)(iVar4 + param_2) = 0"
                ]),
            Primitive(
                functions,
                "0133ba68",
                "_opd_FUN_0133ba68",
                "write-bits",
                "write",
                "Writes an arbitrary bit count into the backing buffer, modifying big-endian-swapped 32-bit words and advancing bit-index.",
                [
                    "byte _opd_FUN_0133ba68(int *param_1,byte *param_2,longlong param_3)",
                    "uVar4 >> 0x18 | uVar4 >> 8 & 0xff00",
                    "(uint)bVar1 << (int)uVar3",
                    "param_1[3] = param_1[3] + 8",
                    "return bVar1 ^ 1"
                ]),
            Primitive(
                functions,
                "0133c198",
                "_opd_FUN_0133c198",
                "write-bytes",
                "write",
                "Byte-count wrapper over 0133ba68; converts bytes to bits with param_3 << 3.",
                [
                    "bVar1 = _opd_FUN_0133ba68(param_1,param_2,(longlong)(param_3 << 3))",
                    "return bVar1"
                ]),
            Primitive(
                functions,
                "01338ce0",
                "_opd_FUN_01338ce0",
                "write-signed-nbits",
                "write",
                "Writes signed integer values using param_3 - 1 value bits and a trailing sign bit.",
                [
                    "if ((int)param_2 < 0)",
                    "param_2 + 0x80000000 << (int)uVar7",
                    "param_2 << (int)uVar7",
                    "param_1[3] = (int)uVar7"
                ]),
            Primitive(
                functions,
                "013391d0",
                "_opd_FUN_013391d0",
                "write-u32",
                "write",
                "32-bit wrapper over 01338ce0.",
                ["_opd_FUN_01338ce0(param_1,param_2,0x20)"]),
            Primitive(
                functions,
                "013391d8",
                "_opd_FUN_013391d8",
                "write-u16",
                "write",
                "16-bit wrapper over 01338ce0.",
                ["_opd_FUN_01338ce0(param_1,param_2,0x10)"]),
            Primitive(
                functions,
                "013391e0",
                "_opd_FUN_013391e0",
                "write-u8",
                "write",
                "8-bit wrapper over 01338ce0.",
                ["_opd_FUN_01338ce0(param_1,param_2,8)"]),
            Primitive(
                functions,
                "013391e8",
                "_opd_FUN_013391e8",
                "write-c-string",
                "write",
                "Writes an ASCII/NUL string by repeatedly calling the 8-bit writer, including the terminating NUL.",
                [
                    "if (param_2 == (char *)0x0)",
                    "_opd_FUN_013391e0(param_1,(int)*param_2)",
                    "while (cVar1 != '\\0')",
                    "return *(byte *)(param_1 + 4) ^ 1"
                ]),
            Primitive(
                functions,
                "01357070",
                "_opd_FUN_01357070",
                "bounded-copy-zero-terminate",
                "support",
                "Copies param_3 bytes and forces the last byte at destination+param_3-1 to NUL. Used by string/tree helpers around the bitstream code.",
                [
                    "_opd_FUN_01504338(param_1 & 0xffffffff,param_2 & 0xffffffff,(ulonglong)param_3)",
                    "*(undefined1 *)((int)param_1 + param_3 + -1) = 0"
                ]),
            Primitive(
                functions,
                "00130af8",
                "_opd_FUN_00130af8",
                "byte-vector-allocate",
                "support",
                "Initializes a byte vector descriptor and allocates backing storage when the requested element count is nonzero.",
                [
                    "param_1[1] = param_3",
                    "param_1[2] = param_2",
                    "if ((1 < param_2 + 1) && (0x80 < param_2))",
                    "(*(code *)**(undefined4 **)(*piVar1 + 8))(piVar1,param_1[1])"
                ])
        ];
    }

    private static Tf2Ps3SourceAddressStateHelper[] BuildAddressStateHelpers(IReadOnlyList<ExportedFunction> functions)
    {
        return
        [
            AddressHelper(
                functions,
                "0134e4c0",
                "_opd_FUN_0134e4c0",
                "state-valid-check",
                "Returns true when address/state has nonzero kind, nonzero port/short at +8, and nonzero address/value at +4.",
                [
                    "*(short *)(param_1 + 2) != 0",
                    "*param_1 != 0",
                    "param_1[1] != 0"
                ]),
            AddressHelper(
                functions,
                "0134e230",
                "_opd_FUN_0134e230",
                "state-compatible-compare",
                "Compares two address/state objects. Kinds 1/2 match by kind; kind 3 also checks address and optionally port.",
                [
                    "iVar1 = *param_1",
                    "iVar1 == *param_2",
                    "iVar1 == 1 || (iVar1 == 2)",
                    "*(short *)(param_1 + 2) == *(short *)(param_2 + 2)",
                    "param_2[1] == param_1[1]"
                ]),
            AddressHelper(
                functions,
                "0134e3d8",
                "_opd_FUN_0134e3d8",
                "state-clear",
                "Clears kind, address, port, and byte fields.",
                [
                    "*(undefined1 *)(param_1 + 1) = 0",
                    "*param_1 = 0",
                    "*(undefined2 *)(param_1 + 2) = 0"
                ]),
            AddressHelper(
                functions,
                "0134e400",
                "_opd_FUN_0134e400",
                "state-set-ipv4-bytes",
                "Writes four individual byte fields at +4..+7.",
                [
                    "*(undefined1 *)(param_1 + 7) = param_5",
                    "*(undefined1 *)(param_1 + 4) = param_2",
                    "*(undefined1 *)(param_1 + 5) = param_3",
                    "*(undefined1 *)(param_1 + 6) = param_4"
                ]),
            AddressHelper(
                functions,
                "0134e450",
                "_opd_FUN_0134e450",
                "state-from-sockaddr",
                "Parses a sockaddr-like object only when family byte is 2; stores kind 3, address +4, and port at +8.",
                [
                    "*(char *)(param_2 + 1) != '\\x02'",
                    "*param_1 = 3",
                    "param_1[1] = *(undefined4 *)(param_2 + 4)",
                    "*(undefined2 *)(param_1 + 2) = *(undefined2 *)(param_2 + 2)"
                ])
        ];
    }

    private static Tf2Ps3SourceBitstreamWrapperLink[] BuildWrapperLinks(IReadOnlyList<ExportedFunction> functions)
    {
        return
        [
            WrapperLink(functions, "FUN_00870968", "01335158", "init-view", "_opd_FUN_01335158(param_1,param_2,param_3,param_4,param_5)"),
            WrapperLink(functions, "FUN_0086e5c8", "0133ba68", "write-bits", "_opd_FUN_0133ba68(param_1,param_2,param_3)"),
            WrapperLink(functions, "FUN_0086d918", "013391e8", "write-c-string", "_opd_FUN_013391e8(param_1,param_2)"),
            WrapperLink(functions, "FUN_0086bd68", "01357070", "bounded-copy-zero-terminate", "_opd_FUN_01357070(param_1,param_2,param_3)"),
            WrapperLink(functions, "FUN_0086bb48", "00130af8", "byte-vector-allocate", "_opd_FUN_00130af8(param_1,param_2,param_3)"),
            WrapperLink(functions, "FUN_0086d848", "0134e4c0", "address-state-valid-check", "_opd_FUN_0134e4c0(param_1)"),
            WrapperLink(functions, "FUN_0086fb58", "0134e230", "address-state-compare", "_opd_FUN_0134e230(param_1,param_2,param_3)")
        ];
    }

    private static Tf2Ps3SourceBitstreamHelperGate[] BuildGates(
        JsonElement fieldSummary,
        Tf2Ps3SourceBitstreamPrimitive[] primitives,
        Tf2Ps3SourceAddressStateHelper[] addressStateHelpers,
        Tf2Ps3SourceBitstreamWrapperLink[] wrapperLinks)
    {
        var fieldContractRecovered = ReadInt(fieldSummary, "RecoveredFieldCount") == ReadInt(fieldSummary, "FieldCount")
            && ReadInt(fieldSummary, "RecoveredOperationCount") == ReadInt(fieldSummary, "OperationCount");
        var callerBuilderRecovered = ReadBool(fieldSummary, "CallerSideParam2BuilderRecovered");
        return
        [
            new Tf2Ps3SourceBitstreamHelperGate(
                "slot70-field-contract-input",
                fieldContractRecovered ? "proven" : "needs-review",
                "source-slot70-param2-field-contract.json",
                "The slot +0x70 callee field layout is already recovered and this report names the underlying helpers."),
            new Tf2Ps3SourceBitstreamHelperGate(
                "bitstream-primitive-contract",
                primitives.All(static primitive => primitive.Status == "recovered") ? "proven" : "needs-review",
                "TF.elf C export helper bodies around 01335158/0133ba68/013391e8",
                "Initializer, reader, writer, string, and support helper bodies were located and token-checked."),
            new Tf2Ps3SourceBitstreamHelperGate(
                "address-state-helper-contract",
                addressStateHelpers.All(static helper => helper.Status == "recovered") ? "proven" : "needs-review",
                "TF.elf C export helper bodies around 0134e230/0134e4c0",
                "The address/state helpers used by the slot +0x70 state gate were located and token-checked."),
            new Tf2Ps3SourceBitstreamHelperGate(
                "source-wrapper-links",
                wrapperLinks.All(static link => link.Status == "recovered") ? "proven" : "needs-review",
                "FUN_0086*/FUN_00870968 wrapper bodies",
                "The TOC-preserving wrappers used by Source code are linked to their underlying primitives."),
            new Tf2Ps3SourceBitstreamHelperGate(
                "caller-side-param2-builder",
                callerBuilderRecovered ? "proven" : "missing",
                "source-slot70-param2-builder.json/source-slot70-param2-field-contract.json",
                "The native route from markerless client UDP payload into the slot +0x70 param_2 object is still the remaining reverse-engineering blocker."),
            new Tf2Ps3SourceBitstreamHelperGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "The primitive bitstream contract is now named, but the live markerless packet transform still is not recovered.")
        ];
    }

    private static Tf2Ps3SourceBitstreamPrimitive Primitive(
        IReadOnlyList<ExportedFunction> functions,
        string address,
        string name,
        string role,
        string direction,
        string meaning,
        string[] tokens)
    {
        var function = FunctionByAddress(functions, address);
        return new Tf2Ps3SourceBitstreamPrimitive(
            address,
            name,
            role,
            direction,
            tokens.All(token => function.Body.Contains(token, StringComparison.Ordinal)) ? "recovered" : "partial",
            tokens,
            tokens.Where(token => function.Body.Contains(token, StringComparison.Ordinal)).ToArray(),
            meaning);
    }

    private static Tf2Ps3SourceAddressStateHelper AddressHelper(
        IReadOnlyList<ExportedFunction> functions,
        string address,
        string name,
        string role,
        string meaning,
        string[] tokens)
    {
        var function = FunctionByAddress(functions, address);
        return new Tf2Ps3SourceAddressStateHelper(
            address,
            name,
            role,
            tokens.All(token => function.Body.Contains(token, StringComparison.Ordinal)) ? "recovered" : "partial",
            tokens,
            tokens.Where(token => function.Body.Contains(token, StringComparison.Ordinal)).ToArray(),
            meaning);
    }

    private static Tf2Ps3SourceBitstreamWrapperLink WrapperLink(
        IReadOnlyList<ExportedFunction> functions,
        string wrapperName,
        string underlyingAddress,
        string role,
        string callToken)
    {
        var wrapper = functions.Single(function => function.Name == wrapperName);
        return new Tf2Ps3SourceBitstreamWrapperLink(
            wrapper.Address,
            wrapperName,
            underlyingAddress,
            role,
            wrapper.Body.Contains(callToken, StringComparison.Ordinal) ? "recovered" : "partial",
            callToken);
    }

    private static ExportedFunction FunctionByAddress(IReadOnlyList<ExportedFunction> functions, string address) =>
        functions.Single(function => function.Address == address);

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetInt32(out var value)
            ? value
            : 0;

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

public sealed record Tf2Ps3SourceBitstreamHelperContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceBitstreamHelperContractInputs Inputs,
    Tf2Ps3SourceBitstreamHelperContractSummary Summary,
    Tf2Ps3SourceBitstreamStateField[] CanonicalStateFields,
    Tf2Ps3SourceBitstreamPrimitive[] Primitives,
    Tf2Ps3SourceAddressStateHelper[] AddressStateHelpers,
    Tf2Ps3SourceBitstreamWrapperLink[] WrapperLinks,
    Tf2Ps3SourceBitstreamHelperGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceBitstreamHelperContractInputs(
    string CExportInput,
    string Slot70Param2FieldContractReport);

public sealed record Tf2Ps3SourceBitstreamHelperContractSummary(
    int PrimitiveCount,
    int RecoveredPrimitiveCount,
    int AddressStateHelperCount,
    int RecoveredAddressStateHelperCount,
    int WrapperLinkCount,
    int RecoveredWrapperLinkCount,
    bool CallerSideParam2BuilderRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceBitstreamStateField(
    string Offset,
    string Expression,
    string Name,
    string Type,
    string Meaning);

public sealed record Tf2Ps3SourceBitstreamPrimitive(
    string Address,
    string Name,
    string Role,
    string Direction,
    string Status,
    string[] RequiredTokens,
    string[] FoundTokens,
    string Meaning);

public sealed record Tf2Ps3SourceAddressStateHelper(
    string Address,
    string Name,
    string Role,
    string Status,
    string[] RequiredTokens,
    string[] FoundTokens,
    string Meaning);

public sealed record Tf2Ps3SourceBitstreamWrapperLink(
    string WrapperAddress,
    string WrapperName,
    string UnderlyingAddress,
    string Role,
    string Status,
    string CallToken);

public sealed record Tf2Ps3SourceBitstreamHelperGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
