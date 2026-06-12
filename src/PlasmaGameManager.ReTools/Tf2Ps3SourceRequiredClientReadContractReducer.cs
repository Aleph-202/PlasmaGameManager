using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRequiredClientReadContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceRequiredClientReadContractReport> ReduceAsync(
        string cExportPath,
        string nativeMessageContractPath,
        string netchanRegistrationSetupPath,
        string outputPath)
    {
        var cExport = await File.ReadAllTextAsync(cExportPath);
        using var nativeMessageDocument = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));
        using var setupDocument = JsonDocument.Parse(await File.ReadAllTextAsync(netchanRegistrationSetupPath));

        var nativeMessages = nativeMessageDocument.RootElement.GetProperty("Messages")
            .EnumerateArray()
            .ToDictionary(static item => ReadString(item, "ClassName"), StringComparer.Ordinal);
        var requiredHandlers = setupDocument.RootElement.GetProperty("RequiredHandlers")
            .EnumerateArray()
            .ToDictionary(static item => ReadString(item, "ClassName"), StringComparer.Ordinal);

        var contracts = BuildContracts(cExport, nativeMessages, requiredHandlers);
        var implementationReady = contracts.Count(static contract => contract.ContractStatus == "read-field-reduced");
        var partial = contracts.Count(static contract => contract.ContractStatus == "read-function-located-complex-partial");
        var existingFieldReduced = requiredHandlers.Values.Count(static handler =>
            ReadString(handler, "ContractStatus") is "critical-field-reduced" or "clc-move-field-reduced" or "bootstrap-control-field-reduced");
        var absentRequired = requiredHandlers.Values.Count(static handler => !ReadBool(handler, "PresentInTfElf"));

        var report = new Tf2Ps3SourceRequiredClientReadContractReport(
            "tf2ps3-source-required-client-read-contract",
            "Field-level read contracts for required CBaseClient::ConnectionStart handlers that were previously vtable-only in TF.elf. These contracts are reduced from the TF.elf Ghidra C export and are intended for native server parsing, not packet replay.",
            new Tf2Ps3SourceRequiredClientReadContractInputs(cExportPath, nativeMessageContractPath, netchanRegistrationSetupPath),
            new Tf2Ps3SourceRequiredClientReadContractSummary(
                requiredHandlers.Count,
                existingFieldReduced,
                contracts.Length,
                implementationReady,
                partial,
                absentRequired,
                existingFieldReduced + implementationReady,
                contracts.Count(static contract => contract.MissingEvidenceTokens.Length > 0)),
            contracts,
            [
                "All seven required vtable-only client/read messages present in TF.elf now have implementation-ready read contracts: NET_Tick, NET_StringCmd, NET_SetConVar, CLC_VoiceData, CLC_ListenEvents, CLC_RespondCvarValue, and CLC_FileCRCCheck.",
                "TF.elf CLC_FileCRCCheck is the older PS3/Orange Box CRC-only layout: reserved bit, 2-bit path id code, 3-bit filename prefix code, filename tail, then one 32-bit CRC at +0x218.",
                "Together with the existing field-reduced critical join messages, eleven of the thirteen Source ConnectionStart template messages have usable TF.elf read contracts; the two remaining PC-era messages are absent in this TF.elf.",
                "The native server should accept and decode these implementation-ready contracts semantically once the outer PS3 object/native wrapper delivers NET/CLC bitstreams."
            ],
            [
                "Build protocol parser tests for the newly reduced required read contracts and validate them against PCAP-extracted raw NET/CLC bitstreams once those bitstreams are isolated.",
                "After parser tests are added, feed NET_StringCmd and NET_SetConVar into the native responder so console/config commands affect session state instead of being opaque pulses.",
                "Use CLC_ListenEvents and CLC_RespondCvarValue to handle event-list subscription and cvar challenge responses during late signon."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRequiredClientReadContract[] BuildContracts(
        string cExport,
        IReadOnlyDictionary<string, JsonElement> nativeMessages,
        IReadOnlyDictionary<string, JsonElement> requiredHandlers)
    {
        return
        [
            Contract(cExport, nativeMessages, requiredHandlers, "NET_Tick", "read-field-reduced",
                [
                    Field("tick", "+0x10", "uint32/bf_read_32_bits", "net_Tick m_nTick", "008c6498 stores the 32-bit read result into param_1 + 0x10.")
                ],
                ["*(uint *)(param_1 + 0x10) = uVar6"]),
            Contract(cExport, nativeMessages, requiredHandlers, "NET_StringCmd", "read-field-reduced",
                [
                    Field("commandPointer", "+0x10", "pointer-to-inline-string-buffer", "net_StringCmd m_szCommand", "008c35f0 stores param_1 + 0x14 into param_1 + 0x10 before reading the string."),
                    Field("commandBuffer", "+0x14", "string/max-0x400", "net_StringCmd m_szCommand", "008c35f0 calls FUN_0086e338 with destination param_1 + 0x14 and max length 0x400.")
                ],
                ["*(int *)(param_1 + 0x10) = param_1 + 0x14", "FUN_0086e338(param_2,param_1 + 0x14,0x400"]),
            Contract(cExport, nativeMessages, requiredHandlers, "NET_SetConVar", "read-field-reduced",
                [
                    Field("entryCount", "transient", "uint8/bf_read_8_bits", "net_SetConVar count", "008c6c20 reads an 8-bit pair count into uVar11 before clearing the destination vector."),
                    Field("conVarPairs", "+0x10", "repeated string-pair/max-0x104", "net_SetConVar cvar_t list", "008c6c20 clears param_1 + 0x10, then reads name and value strings of max 0x104 and appends them with FUN_00870c78.")
                ],
                ["FUN_0086f1a8(param_1 + 0x10)", "FUN_0086e338(param_2,(int)auStack_250,0x104", "FUN_0086e338(param_2,(int)auStack_14c,0x104", "FUN_00870c78((uint *)(param_1 + 0x10)"]),
            Contract(cExport, nativeMessages, requiredHandlers, "CLC_VoiceData", "read-field-reduced",
                [
                    Field("dataBitCount", "+0x10", "uint16/bf_read_16_bits", "clc_VoiceData length", "008c87e0 reads a 16-bit voice bit count into param_1 + 0x10."),
                    Field("embeddedReaderState", "+0x14..+0x34", "bf_read snapshot plus skip-by-bitcount", "clc_VoiceData data", "008c87e0 copies bf_read words into +0x14..+0x34, then calls FUN_0086c7d8 to advance by the available voice bit count.")
                ],
                ["*(uint *)(param_1 + 0x10) = uVar8", "*(undefined4 *)(param_1 + 0x14) = *param_2", "*(undefined4 *)(param_1 + 0x34) = param_2[8]", "FUN_0086c7d8((int)param_2,iVar7 + uVar8)"]),
            Contract(cExport, nativeMessages, requiredHandlers, "CLC_ListenEvents", "read-field-reduced",
                [
                    Field("eventMaskWords", "+0x10..+0x4c", "16 x uint32/bf_read_32_bits", "clc_ListenEvents event mask", "008c3db0 loops 16 times and stores each 32-bit word at param_1 + 0x10 + index * 4.")
                ],
                ["lVar9 = 0x10", "*(uint *)(iVar7 + param_1 + 0x10)", "iVar7 = iVar7 + 4"]),
            Contract(cExport, nativeMessages, requiredHandlers, "CLC_RespondCvarValue", "read-field-reduced",
                [
                    Field("cookie", "+0x10", "uint32/bf_read_32_bits", "clc_RespondCvarValue cookie", "008c54e8 stores the first 32-bit read into param_1 + 0x10."),
                    Field("statusCode", "+0x1c", "signed-4-bit", "clc_RespondCvarValue status code", "008c54e8 sign-extends a 4-bit value and stores it at param_1 + 0x1c."),
                    Field("cvarName", "+0x14 pointer / +0x20 buffer", "string/max-0x100", "clc_RespondCvarValue name", "008c54e8 reads a 0x100-byte max string into param_1 + 0x20 and stores the pointer at +0x14."),
                    Field("cvarValue", "+0x18 pointer / +0x120 buffer", "string/max-0x100", "clc_RespondCvarValue value", "008c54e8 reads a 0x100-byte max string into param_1 + 0x120 and stores the pointer at +0x18.")
                ],
                ["*(uint *)(param_1 + 0x10) = uVar5", "*(int *)(param_1 + 0x1c) = iVar6", "FUN_0086e338(param_2,param_1 + 0x20,0x100", "*(int *)(param_1 + 0x14) = param_1 + 0x20", "FUN_0086e338(param_2,param_1 + 0x120,0x100", "*(int *)(param_1 + 0x18) = param_1 + 0x120"]),
            Contract(cExport, nativeMessages, requiredHandlers, "CLC_FileCRCCheck", "read-field-reduced",
                [
                    Field("reservedBit", "transient", "1 bit ignored", "clc_FileCRCCheck reserved bit", "008c3f90 consumes one bit before reading path id; it does not store this value."),
                    Field("pathIdCode", "transient", "uint2: 0=inline, 1=GAME, 2=MOD", "CLC_FileCRCCheck common path id selector", "008c3f90 reads a 2-bit code and rejects values above the two common path IDs."),
                    Field("pathId", "+0x10", "string/max-0x104 or common path table", "CLC_FileCRCCheck m_szPathID", "008c3f90 reads an inline string into +0x10 for code 0, or copies a common path ID into +0x10 for code 1/2."),
                    Field("filenamePrefixCode", "transient", "uint3: 0=inline, 1=materials, 2=models, 3=sounds, 4=scripts", "CLC_FileCRCCheck common filename prefix selector", "008c3f90 reads a 3-bit code and rejects values above the four common filename prefixes."),
                    Field("filename", "+0x114", "string/max-0x104 or prefix+separator+tail", "CLC_FileCRCCheck m_szFilename", "008c3f90 reads an inline filename into +0x114 for prefix 0, or reads a tail string and formats a common prefix into +0x114 for prefix 1..4."),
                    Field("crc", "+0x218", "uint32/bf_read_32_bits", "older CLC_FileCRCCheck m_CRC", "008c3f90 reads one 32-bit CRC into +0x218. This PS3 build does not read the newer MD5/hash-type/file-length tail.")
                ],
                ["byte _opd_FUN_008c3f90", "*(int *)(param_2 + 0x14) + -1", "FUN_0086e338(param_2,iVar2 + 0x10,0x104", "FUN_0086bd68(param_1 + 0x10U", "if (1 < uVar7 - 1)", "FUN_0086e338(param_2,(int)auStack_140,0x104", "FUN_008708f8(param_1 + 0x114U", "FUN_0086e338(param_2,iVar2 + 0x114,0x104", "if (3 < uVar7 - 1)", "*(uint *)(iVar2 + 0x218) = uVar8"])
        ];
    }

    private static Tf2Ps3SourceRequiredClientReadContract Contract(
        string cExport,
        IReadOnlyDictionary<string, JsonElement> nativeMessages,
        IReadOnlyDictionary<string, JsonElement> requiredHandlers,
        string className,
        string status,
        Tf2Ps3SourceRequiredClientReadField[] fields,
        string[] evidenceTokens)
    {
        nativeMessages.TryGetValue(className, out var native);
        requiredHandlers.TryGetValue(className, out var required);
        var missing = evidenceTokens
            .Where(token => !cExport.Contains(token, StringComparison.Ordinal))
            .ToArray();

        return new Tf2Ps3SourceRequiredClientReadContract(
            className,
            required.ValueKind == JsonValueKind.Object ? ReadString(required, "Family") : ReadString(native, "Family"),
            native.ValueKind == JsonValueKind.Object ? ReadInt(native, "MessageId") : -1,
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "ReadFromBufferEntry") : "",
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "NetworkName") : "",
            status,
            fields.Length,
            fields,
            evidenceTokens,
            missing,
            missing.Length == 0 ? "high" : "needs-refresh",
            GuidanceFor(className, status));
    }

    private static Tf2Ps3SourceRequiredClientReadField Field(
        string name,
        string objectOffset,
        string encoding,
        string sourceEquivalent,
        string evidence)
    {
        return new Tf2Ps3SourceRequiredClientReadField(name, objectOffset, encoding, sourceEquivalent, evidence);
    }

    private static string GuidanceFor(string className, string status)
    {
        if (status == "read-function-located-complex-partial")
        {
            return $"{className} is located but not implementation-ready; keep it logged/ignored until selector branches and field names are reduced.";
        }

        return $"{className} is implementation-ready for native parsing once the PS3 wrapper yields the raw NET/CLC bitstream.";
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static bool ReadBool(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.True;
    }
}

public sealed record Tf2Ps3SourceRequiredClientReadContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceRequiredClientReadContractInputs Inputs,
    Tf2Ps3SourceRequiredClientReadContractSummary Summary,
    Tf2Ps3SourceRequiredClientReadContract[] Contracts,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Tf2Ps3SourceRequiredClientReadContractInputs(
    string CExport,
    string NativeMessageContract,
    string NetchanRegistrationSetup);

public sealed record Tf2Ps3SourceRequiredClientReadContractSummary(
    int RequiredConnectionStartMessageCount,
    int ExistingFieldReducedRequiredHandlerCount,
    int NewlyLocatedRequiredHandlerCount,
    int ImplementationReadyNewReadContractCount,
    int PartialComplexReadContractCount,
    int AbsentRequiredHandlerCount,
    int TotalRequiredHandlersWithUsableReadContract,
    int ContractWithMissingEvidenceTokenCount);

public sealed record Tf2Ps3SourceRequiredClientReadContract(
    string ClassName,
    string Family,
    int MessageId,
    string ReadFromBufferEntry,
    string NetworkName,
    string ContractStatus,
    int FieldCount,
    Tf2Ps3SourceRequiredClientReadField[] Fields,
    string[] EvidenceTokens,
    string[] MissingEvidenceTokens,
    string Confidence,
    string NativeImplementationGuidance);

public sealed record Tf2Ps3SourceRequiredClientReadField(
    string Name,
    string ObjectOffset,
    string Encoding,
    string SourceEquivalent,
    string TfEvidence);
