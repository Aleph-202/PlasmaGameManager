using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceClcMoveContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceClcMoveContractReport> ReduceAsync(
        string ghidraContextPath,
        string sourceEngineRoot,
        string outputPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(ghidraContextPath));
        var root = document.RootElement;
        var addresses = root.GetProperty("addresses").EnumerateArray().ToArray();
        var functions = root.GetProperty("decompiledFunctions").EnumerateArray().ToArray();

        var stringAnchors = new[]
        {
            BuildStringAnchor(addresses, "1005a5b8", "CLC_Move typeinfo name"),
            BuildStringAnchor(addresses, "1005a678", "clc_Move network message name"),
            BuildStringAnchor(addresses, "1005ace8", "CLC_Move ToString formatter"),
            BuildStringAnchor(addresses, "100644c8", "ProcessUsercmds overflow disconnect"),
            BuildStringAnchor(addresses, "10064530", "ProcessUsercmds bit-count mismatch disconnect"),
            BuildStringAnchor(addresses, "1004a858", "CDemo::WriteUserCmd anchor"),
            BuildStringAnchor(addresses, "1004a980", "CDemo::ReadUserCmd anchor"),
            BuildStringAnchor(addresses, "100047c0", "CUserCmd typeinfo name")
        };
        var sourceReference = BuildSourceReference(sourceEngineRoot);
        var fieldLayout = BuildFieldLayout();
        var report = new Tf2Ps3SourceClcMoveContractReport(
            "tf2ps3-source-clc-move-contract",
            "Reduces TF.elf Ghidra evidence for CLC_Move and the ProcessUsercmds bridge. This proves the command-batch boundary contract but does not claim every PS3 wrapper path is mapped.",
            new Tf2Ps3SourceClcMoveInputs(ghidraContextPath, sourceEngineRoot),
            new Tf2Ps3SourceClcMoveSummary(
                stringAnchors.Count(static anchor => anchor.RefCount > 0),
                "008d42d8",
                "008c32c8",
                "008c8a50",
                "008cd168",
                "00a291c0",
                "017fdc30",
                "9",
                5,
                4,
                3,
                16,
                28,
                23,
                fieldLayout.Length,
                sourceReference.HasSourceReference,
                [
                    "live-near-mtu-queued-wrapper-coverage",
                    "exact-client-prediction/world-physics"
                ]),
            stringAnchors,
            new Tf2Ps3SourceClcMoveNameTable(
                "019932bc",
                "1005a678",
                "008d42d8",
                "019933f8",
                "1005ace8",
                "0199334c",
                "017f90c0"),
            BuildVtableContract(),
            new Tf2Ps3SourceClcMoveToStringContract(
                "008c32c8",
                FunctionBody(functions, "008c32c8"),
                "FUN_008708f8(..., \"%s: backup %i, new %i, bytes %i\", GetName(), param_1[5], param_1[4], (param_1[6]+7)>>3)",
                "The legacy Source formatter passes m_nNewCommands before m_nBackupCommands even though the format labels say backup/new. Field roles are therefore pinned by CLC_Move::ReadFromBuffer and ProcessUsercmds, not by the formatter labels."),
            new Tf2Ps3SourceClcMoveReadContract(
                "CLC_Move::ReadFromBuffer",
                "008c8a50",
                FunctionBody(functions, "008c8a50"),
                "m_nNewCommands = ReadUBitLong(4); m_nBackupCommands = ReadUBitLong(3); m_nLength = ReadWord(); m_DataIn = buffer; SeekRelative(m_nLength)",
                "TF.elf vptr 017fdc30 slot +0x14 resolves OPD 01901df0 -> function 008c8a50. Its decompile stores the first 4-bit read at +0x14, the next 3-bit read at +0x10, the 16-bit read at +0x18, copies the bf_read state to +0x1c..+0x3c, then calls FUN_0086c7d8(reader, currentBits + length).",
                "Ps3SourceClcMoveMessage",
                "Ps3SourceClientCommandBatchBoundaryResolver",
                sourceReference),
            new Tf2Ps3SourceProcessUsercmdsBridge(
                "00a291c0",
                FunctionBody(functions, "00a291c0"),
                "param_2 + 0x10",
                "param_2 + 0x14",
                "param_2 + 0x18",
                "param_2 + 0x1c",
                "param_2 + 0x20",
                "PTR_DAT_01996f84 vtable +0x24",
                [
                    "edict/player = param_1[0x20ac]",
                    "bf_read = param_2 + 0x1c",
                    "numcmds/new = *(param_2 + 0x14)",
                    "totalcmds = *(param_2 + 0x10) + *(param_2 + 0x14)",
                    "dropped/sequence = virtual call on param_1[0x36] slot +0xbc",
                    "ignore = global state < 2",
                    "paused = pause/dev gate"
                ],
                "After the game-side call, TF.elf compares actual consumed bits against *(param_2 + 0x18) and disconnects on overflow or mismatch."),
            fieldLayout,
            [
                "Use Ps3SourceNativeToClcMoveBoundaryResolver for direct raw CLC_Move bodies, attached type-2 raw bodies, exact bit-sidecar CLC_Move packets, and the recovered owner-slot +0x08 / 00a52720 bitstream wrapper before falling back to heuristic intent decoding.",
                "Decode total commands (backup + new) with Ps3SourceClientCommandBatch.TryDecodeBatch; pass newCommands separately to the ProcessUsercmds-equivalent simulation step.",
                "Use PcapSourceClcMoveBoundaryAnalyzer to keep exact native CLC_Move wrapper coverage separate from weak embedded offset-scan candidates.",
                "The exact CLC_Move reader is now pinned to TF.elf function 008c8a50; current PCAP corpus evidence has no exact client CLC_Move wrapper hits at the decoded boundary, so the remaining near-MTU work is server-to-client queued generation and native wrapper proof rather than direct client command unwrapping.",
                "Keep the current heuristic command-intent fallback until live packets can be proven to contain this exact CLC_Move boundary."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceClcMoveStringAnchor BuildStringAnchor(
        JsonElement[] addresses,
        string address,
        string role)
    {
        var element = addresses.FirstOrDefault(item => ReadString(item, "address") == address);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new Tf2Ps3SourceClcMoveStringAnchor(address, role, "", [], 0);
        }

        var refs = element.GetProperty("refsTo").EnumerateArray()
            .Select(static reference => new Tf2Ps3SourceClcMoveReference(
                ReadString(reference, "from"),
                ReadString(reference, "type"),
                ReadString(reference, "fromFunction")))
            .ToArray();
        return new Tf2Ps3SourceClcMoveStringAnchor(
            address,
            role,
            element.GetProperty("codeUnit").GetProperty("text").GetString() ?? "",
            refs,
            refs.Length);
    }

    private static Tf2Ps3SourceClcMoveField[] BuildFieldLayout()
    {
        return
        [
            new("+0x10", "backupCommands", "int", "TF.elf CLC_Move::ReadFromBuffer 008c8a50 stores the 3-bit backup command count here; ProcessUsercmds adds it to newCommands for totalcmds.", "high"),
            new("+0x14", "newCommands", "int", "TF.elf CLC_Move::ReadFromBuffer 008c8a50 stores the first 4-bit read here; ProcessUsercmds passes this as numcmds/new command count.", "high"),
            new("+0x18", "commandDataBitCount", "int", "TF.elf CLC_Move::ReadFromBuffer 008c8a50 stores the 16-bit command-data bit count here; TF.elf validates consumed usercmd bits against it after game-side decoding.", "high"),
            new("+0x1c", "commandDataReader", "bf_read", "TF.elf CLC_Move::ReadFromBuffer 008c8a50 copies the embedded bf_read state to +0x1c..+0x3c, then ProcessUsercmds hands +0x1c directly to the game-side decoder.", "high"),
            new("+0x20", "overflowFlag", "byte/bool", "Non-zero triggers the official overflow disconnect string.", "high"),
            new("+0x24", "readerStartBitLimit", "int", "Used with reader cursor fields to compute pre/post consumed-bit positions.", "medium"),
            new("+0x28", "readerBitCursorLow", "int", "Masked with 3 and multiplied by 8 in consumed-bit calculation.", "medium"),
            new("+0x30", "readerRemainingBits", "int", "Part of consumed-bit calculation: 0x20 - remainingBits.", "medium"),
            new("+0x34", "readerEndPointer", "pointer", "Part of consumed-bit calculation against +0x3c.", "medium"),
            new("+0x3c", "readerCurrentPointer", "pointer", "Zero disables consumed-bit calculation; non-zero participates in reader position math.", "medium")
        ];
    }

    private static Tf2Ps3SourceClcMoveVtableContract BuildVtableContract()
    {
        return new Tf2Ps3SourceClcMoveVtableContract(
            "017fdc30",
            "Exact object vptr recovered by resolving PS3 OPD descriptor references for CLC_Move GetName (019017c0 -> 008d42d8) and ToString (01901d08 -> 008c32c8). With this vptr, INetMessage virtual slot +0x28 is GetName and +0x30 is ToString, matching TF.elf callsites and Source public/inetmessage.h.",
            [
                new("+0x00", "destructor0", "01902048", "008d5660", "Destructor wrapper."),
                new("+0x04", "destructor1", "01902040", "008d5628", "Destructor wrapper."),
                new("+0x08", "SetNetChannel", "018f5cd0", "00711a98", "Inherited CNetMessage setter."),
                new("+0x0c", "SetReliable", "018f5cc0", "00711a88", "Inherited CNetMessage setter."),
                new("+0x10", "Process", "019017c8", "008d42e0", "CLC_Move handler dispatch."),
                new("+0x14", "ReadFromBuffer", "01901df0", "008c8a50", "Native CLC_Move reader: 4-bit new, 3-bit backup, 16-bit bit length, bf_read copy, seek relative."),
                new("+0x18", "WriteToBuffer", "01901e80", "008cd168", "Native CLC_Move writer slot; used as the inverse boundary for future server-originated command batches."),
                new("+0x1c", "IsReliable", "018f5cc8", "00711a90", "Inherited reliability accessor."),
                new("+0x20", "GetType", "019017b8", "008d42d0", "Returns clc_Move message type 9."),
                new("+0x24", "GetGroup", "019017d0", "008d4330", "Returns the Source move message group."),
                new("+0x28", "GetName", "019017c0", "008d42d8", "Returns PTR_s_clc_Move_019932bc."),
                new("+0x2c", "GetNetChannel", "018f5cb8", "00711a80", "Inherited netchannel accessor."),
                new("+0x30", "ToString", "01901d08", "008c32c8", "Formats the CLC_Move command counts and byte count.")
            ]);
    }

    private static Tf2Ps3SourceClcMoveSourceReference BuildSourceReference(string sourceEngineRoot)
    {
        var netmessages = Path.Combine(sourceEngineRoot, "common/netmessages.cpp");
        var protocol = Path.Combine(sourceEngineRoot, "common/protocol.h");
        var serverClient = Path.Combine(sourceEngineRoot, "engine/sv_client.cpp");
        return new Tf2Ps3SourceClcMoveSourceReference(
            File.Exists(netmessages) && File.Exists(protocol) && File.Exists(serverClient),
            netmessages,
            protocol,
            serverClient,
            [
                "common/protocol.h: clc_Move = 9, NUM_NEW_COMMAND_BITS = 4, NUM_BACKUP_COMMAND_BITS = 3",
                "common/netmessages.cpp: CLC_Move::ReadFromBuffer reads new, backup, 16-bit length, then seeks length bits",
                "engine/sv_client.cpp: CGameClient::ProcessMove passes newCommands and backup+new totalCommands to ProcessUsercmds"
            ]);
    }

    private static string FunctionBody(JsonElement[] functions, string entry)
    {
        return functions.FirstOrDefault(function => ReadString(function, "entry") == entry)
            .TryGetProperty("body", out var body)
            ? body.GetString() ?? ""
            : "";
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? ""
                : "";
    }
}

public sealed record Tf2Ps3SourceClcMoveContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceClcMoveInputs Inputs,
    Tf2Ps3SourceClcMoveSummary Summary,
    Tf2Ps3SourceClcMoveStringAnchor[] StringAnchors,
    Tf2Ps3SourceClcMoveNameTable NameTable,
    Tf2Ps3SourceClcMoveVtableContract VtableContract,
    Tf2Ps3SourceClcMoveToStringContract ToStringContract,
    Tf2Ps3SourceClcMoveReadContract ReadContract,
    Tf2Ps3SourceProcessUsercmdsBridge ProcessUsercmdsBridge,
    Tf2Ps3SourceClcMoveField[] FieldLayout,
    string[] NativeImplementationGuidance);

public sealed record Tf2Ps3SourceClcMoveInputs(
    string GhidraContextPath,
    string SourceEngineRoot);

public sealed record Tf2Ps3SourceClcMoveSummary(
    int ReferencedStringAnchorCount,
    string GetNameFunction,
    string ToStringFunction,
    string ReadFromBufferFunction,
    string WriteToBufferFunction,
    string ProcessUsercmdsBridgeFunction,
    string ExactObjectVptr,
    string ClcMoveMessageType,
    int NetMessageTypeBits,
    int NewCommandBits,
    int BackupCommandBits,
    int CommandDataLengthBits,
    int HeaderBitsWithMessageType,
    int HeaderBitsWithoutMessageType,
    int FieldLayoutCount,
    bool HasSourceEngineReference,
    string[] RemainingGaps);

public sealed record Tf2Ps3SourceClcMoveStringAnchor(
    string Address,
    string Role,
    string Text,
    Tf2Ps3SourceClcMoveReference[] References,
    int RefCount);

public sealed record Tf2Ps3SourceClcMoveReference(
    string From,
    string Type,
    string FromFunction);

public sealed record Tf2Ps3SourceClcMoveNameTable(
    string MoveNamePointer,
    string MoveNameString,
    string GetNameFunction,
    string FormatterPointer,
    string FormatterString,
    string ClassVtablePointerCandidate,
    string ClassVtableCandidate);

public sealed record Tf2Ps3SourceClcMoveVtableContract(
    string ObjectVptr,
    string Evidence,
    Tf2Ps3SourceClcMoveVtableSlot[] Slots);

public sealed record Tf2Ps3SourceClcMoveVtableSlot(
    string Offset,
    string Role,
    string Opd,
    string Entry,
    string Evidence);

public sealed record Tf2Ps3SourceClcMoveToStringContract(
    string Entry,
    string DecompiledBody,
    string RecoveredFormatCall,
    string FieldRoleCaution);

public sealed record Tf2Ps3SourceClcMoveReadContract(
    string SourceFunction,
    string TfElfFunction,
    string TfElfDecompiledBody,
    string ReadOrder,
    string NativeEvidence,
    string ImplementedMessageModel,
    string ImplementedBoundaryResolver,
    Tf2Ps3SourceClcMoveSourceReference SourceReference);

public sealed record Tf2Ps3SourceClcMoveSourceReference(
    bool HasSourceReference,
    string NetmessagesCpp,
    string ProtocolH,
    string SvClientCpp,
    string[] Evidence);

public sealed record Tf2Ps3SourceProcessUsercmdsBridge(
    string Entry,
    string DecompiledBody,
    string BackupCommandsField,
    string NewCommandsField,
    string CommandDataBitCountField,
    string CommandDataReaderField,
    string OverflowFlagField,
    string GameClientInterfaceCall,
    string[] GameClientInterfaceArguments,
    string Validation);

public sealed record Tf2Ps3SourceClcMoveField(
    string Offset,
    string Name,
    string Type,
    string Evidence,
    string Confidence);
