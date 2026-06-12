using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceCriticalMessageIoContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly CriticalMessageSpec[] Specs =
    [
        new(
            "CLC_ClientInfo",
            "client-join-read",
            "008c58e8",
            "008d0c18",
            "Read client info before signon/map-load acceptance.",
            [
                Field("m_nServerCount", "+0x14", "ReadLong / 32 bits", "m_nServerCount = buffer.ReadLong()", "*(uint *)(param_1 + 0x14)"),
                Field("m_nSendTableCRC", "+0x10", "ReadLong / 32 bits", "m_nSendTableCRC = buffer.ReadLong()", "*(uint *)(param_1 + 0x10)"),
                Field("m_bIsHLTV", "+0x18", "ReadOneBit", "m_bIsHLTV = buffer.ReadOneBit()!=0", "*(byte *)(param_1 + 0x18)"),
                Field("m_nFriendsID", "+0x1c", "ReadLong / 32 bits", "m_nFriendsID = buffer.ReadLong()", "*(uint *)(param_1 + 0x1c)"),
                Field("m_FriendsName", "+0x20", "ReadString 0x20 bytes", "buffer.ReadString(m_FriendsName, sizeof(m_FriendsName))", "FUN_0086e338(param_2,param_1 + 0x20,0x20"),
                Field("m_nCustomFiles[4]", "+0x40..+0x4c", "4 x optional ReadOneBit + ReadLong", "for MAX_CUSTOM_FILES: presence bit then 32-bit CRC", "lVar11 = 4")
            ],
            [
                "FUN_0086e338(param_2,param_1 + 0x20,0x20",
                "lVar11 = 4",
                "puVar4 = (uint *)(param_1 + 0x40)"
            ],
            [
                "m_nServerCount = buffer.ReadLong();",
                "m_nSendTableCRC = buffer.ReadLong();",
                "m_bIsHLTV = buffer.ReadOneBit()!=0;",
                "m_nFriendsID = buffer.ReadLong();",
                "buffer.ReadString( m_FriendsName, sizeof(m_FriendsName) );",
                "for ( int i=0; i<MAX_CUSTOM_FILES; i++ )"
            ]),
        new(
            "CLC_BaselineAck",
            "client-join-read",
            "008c6138",
            "008d0740",
            "Read client baseline acknowledgement before entity baselines can safely advance.",
            [
                Field("m_nBaselineTick", "+0x10", "ReadLong / 32 bits", "m_nBaselineTick = buffer.ReadLong()", "*(uint *)(param_1 + 0x10)"),
                Field("m_nBaselineNr", "+0x14", "ReadUBitLong(1)", "m_nBaselineNr = buffer.ReadUBitLong(1)", "*(uint *)(param_1 + 0x14)")
            ],
            [
                "*(uint *)(param_1 + 0x10)",
                "*(uint *)(param_1 + 0x14)"
            ],
            [
                "m_nBaselineTick = buffer.ReadLong();",
                "m_nBaselineNr = buffer.ReadUBitLong( 1 );"
            ]),
        new(
            "SVC_ServerInfo",
            "server-map-load-write",
            "008c76b8",
            "008cec08",
            "Write initial server/map metadata; TF.elf uses the old PS3 5-bit message id and a PS3-era map CRC/digest layout.",
            [
                Field("m_nProtocol", "+0x10", "WriteShort / 16 bits", "buffer.WriteShort(m_nProtocol)", "FUN_0086c9d8(param_2,param_1[4])"),
                Field("m_nServerCount", "+0x14", "WriteLong / 32 bits", "buffer.WriteLong(m_nServerCount)", "FUN_0086caf8(param_2,param_1[5])"),
                Field("m_bIsHLTV", "+0x19", "WriteOneBit", "buffer.WriteOneBit(m_bIsHLTV)", "*(char *)((int)param_1 + 0x19)"),
                Field("m_bIsDedicated", "+0x18", "WriteOneBit", "buffer.WriteOneBit(m_bIsDedicated)", "*(char *)(param_1 + 6)"),
                Field("legacyClientCrcOrMapDigest", "+0x20", "WriteLong / 32 bits", "Source writes legacy client CRC; PS3 object carries a 32-bit value here", "FUN_0086caf8(param_2,param_1[8])"),
                Field("m_nMaxClasses", "+0x28", "WriteWord / 16 bits", "buffer.WriteWord(m_nMaxClasses)", "FUN_0086c708(param_2,param_1[10])"),
                Field("m_nMapCRCOrDigest32", "+0x1c", "WriteLong / 32 bits", "PS3-era divergence from newer Source MD5-bytes path", "FUN_0086caf8(param_2,param_1[7])"),
                Field("m_nPlayerSlot", "+0x2c", "WriteByte / 8 bits", "buffer.WriteByte(m_nPlayerSlot)", "FUN_00870c28(param_2,param_1[0xb])"),
                Field("m_nMaxClients", "+0x24", "WriteByte / 8 bits", "buffer.WriteByte(m_nMaxClients)", "FUN_00870c28(param_2,param_1[9])"),
                Field("m_fTickInterval", "+0x30", "WriteFloat", "buffer.WriteFloat(m_fTickInterval)", "FUN_0086c7e8((double)(float)param_1[0xc],param_2)"),
                Field("m_cOS", "+0x1a", "WriteChar / 8-bit signed", "buffer.WriteChar(m_cOS)", "FUN_00871aa8(param_2,(int)*(char *)((int)param_1 + 0x1a))"),
                Field("m_szGameDir", "+0x34", "WriteString", "buffer.WriteString(m_szGameDir)", "FUN_0086d918(param_2,(char *)param_1[0xd])"),
                Field("m_szMapName", "+0x38", "WriteString", "buffer.WriteString(m_szMapName)", "FUN_0086d918(param_2,(char *)param_1[0xe])"),
                Field("m_szSkyName", "+0x3c", "WriteString", "buffer.WriteString(m_szSkyName)", "FUN_0086d918(param_2,(char *)param_1[0xf])"),
                Field("m_szHostName", "+0x40", "WriteString", "buffer.WriteString(m_szHostName)", "FUN_0086d918(param_2,(char *)param_1[0x10])")
            ],
            [
                "FUN_0086c9d8(param_2,param_1[4])",
                "FUN_0086caf8(param_2,param_1[5])",
                "FUN_0086c708(param_2,param_1[10])",
                "FUN_0086d918(param_2,(char *)param_1[0x10])"
            ],
            [
                "buffer.WriteShort ( m_nProtocol );",
                "buffer.WriteLong  ( m_nServerCount );",
                "buffer.WriteOneBit( m_bIsHLTV?1:0);",
                "buffer.WriteOneBit( m_bIsDedicated?1:0);",
                "buffer.WriteWord  ( m_nMaxClasses );",
                "buffer.WriteByte  ( m_nPlayerSlot );",
                "buffer.WriteFloat ( m_fTickInterval );",
                "buffer.WriteString( m_szHostName );"
            ]),
        new(
            "SVC_SendTable",
            "server-map-load-write",
            "008cba28",
            "008ce360",
            "Write one serialized send-table payload.",
            [
                Field("m_bNeedsDecoder", "+0x10", "WriteOneBit", "buffer.WriteOneBit(m_bNeedsDecoder)", "*(byte *)(param_1 + 0x10)"),
                Field("m_nLength", "+0x14", "WriteShort / 16 bits", "buffer.WriteShort(m_nLength)", "FUN_0086c9d8(param_2,param_1[5])"),
                Field("m_DataOut", "+0x3c/+0x14", "WriteBits(m_nLength)", "buffer.WriteBits(m_DataOut.GetData(), m_nLength)", "FUN_0086e5c8(param_2,(byte *)param_1[0xf],(longlong)param_1[5])")
            ],
            [
                "_opd_FUN_008d3ef0(param_2,uVar3,5)",
                "FUN_0086c9d8(param_2,param_1[5])",
                "FUN_0086e5c8(param_2,(byte *)param_1[0xf],(longlong)param_1[5])"
            ],
            [
                "buffer.WriteOneBit( m_bNeedsDecoder?1:0 );",
                "buffer.WriteShort( m_nLength );",
                "buffer.WriteBits( m_DataOut.GetData(), m_nLength );"
            ]),
        new(
            "SVC_ClassInfo",
            "server-map-load-write",
            "008cde00",
            "008ce770",
            "Write server class list or client-created class marker.",
            [
                Field("m_nNumServerClasses", "+0x28", "WriteShort / 16 bits", "buffer.WriteShort(m_nNumServerClasses)", "FUN_0086c9d8(param_2,param_1[10])"),
                Field("m_bCreateOnClient", "+0x10", "WriteOneBit", "buffer.WriteOneBit(m_bCreateOnClient)", "*(char *)(param_1 + 4)"),
                Field("classID", "class stride +0x00", "WriteUBitLong(Q_log2(numClasses)+1)", "buffer.WriteUBitLong(serverclass->classID, serverClassBits)", "_opd_FUN_008d3ef0(param_2,*puVar5,(longlong)(iVar3 + 1))"),
                Field("classname", "class stride +0x104", "WriteString", "buffer.WriteString(serverclass->classname)", "FUN_0086d918(param_2,(char *)(puVar5 + 0x41))"),
                Field("datatablename", "class stride +0x04", "WriteString", "buffer.WriteString(serverclass->datatablename)", "FUN_0086d918(param_2,(char *)(puVar5 + 1))")
            ],
            [
                "FUN_0086c9d8(param_2,param_1[10])",
                "iVar7 = iVar7 + 0x204",
                "FUN_0086d918(param_2,(char *)(puVar5 + 0x41))",
                "FUN_0086d918(param_2,(char *)(puVar5 + 1))"
            ],
            [
                "buffer.WriteShort( m_nNumServerClasses );",
                "buffer.WriteOneBit( m_bCreateOnClient?1:0 );",
                "buffer.WriteUBitLong( serverclass->classID, serverClassBits );",
                "buffer.WriteString( serverclass->classname );",
                "buffer.WriteString( serverclass->datatablename );"
            ]),
        new(
            "SVC_CreateStringTable",
            "server-map-load-write",
            "008ca0b8",
            "008d1470",
            "Write a complete string table. TF.elf uses a 20-bit table-data length in this PS3 build.",
            [
                Field("m_szTableName", "+0x10", "WriteString", "buffer.WriteString(m_szTableName)", "FUN_0086d918(param_2,(char *)param_1[4])"),
                Field("m_nMaxEntries", "+0x14", "WriteWord / 16 bits", "buffer.WriteWord(m_nMaxEntries)", "FUN_0086c708(param_2,param_1[5])"),
                Field("m_nNumEntries", "+0x18", "WriteUBitLong(Q_log2(maxEntries)+1)", "buffer.WriteUBitLong(m_nNumEntries, encodeBits+1)", "uVar10 = param_1[6]"),
                Field("m_nLength", "+0x2c", "WriteUBitLong(20) in TF.elf", "newer Source uses WriteVarInt32(m_nLength)", "uVar12 = param_1[0xb]"),
                Field("m_bUserDataFixedSize", "+0x1c", "WriteOneBit", "buffer.WriteOneBit(m_bUserDataFixedSize)", "cVar13 = *(char *)(param_1 + 7)"),
                Field("m_nUserDataSize", "+0x20", "WriteUBitLong(12) when fixed-size", "buffer.WriteUBitLong(m_nUserDataSize, 12)", "uVar5 = param_1[8]"),
                Field("m_nUserDataSizeBits", "+0x24", "WriteUBitLong(4) when fixed-size", "buffer.WriteUBitLong(m_nUserDataSizeBits, 4)", "uVar10 = param_1[9]"),
                Field("m_DataOut", "+0x54", "WriteBits(m_nLength)", "buffer.WriteBits(m_DataOut.GetData(), m_nLength)", "FUN_0086e5c8(param_2,(byte *)param_1[0x15],(longlong)param_1[0xb])")
            ],
            [
                "FUN_0086d918(param_2,(char *)param_1[4])",
                "FUN_0086c708(param_2,param_1[5])",
                "uVar12 = param_1[0xb]",
                "FUN_0086e5c8(param_2,(byte *)param_1[0x15],(longlong)param_1[0xb])"
            ],
            [
                "buffer.WriteString( m_szTableName );",
                "buffer.WriteWord( m_nMaxEntries );",
                "buffer.WriteUBitLong( m_nNumEntries, encodeBits+1 );",
                "buffer.WriteVarInt32( m_nLength );",
                "buffer.WriteOneBit( m_bUserDataFixedSize ? 1 : 0 );",
                "buffer.WriteBits( m_DataOut.GetData(), m_nLength );"
            ]),
        new(
            "SVC_UpdateStringTable",
            "server-map-load-write",
            "008c99f8",
            "008d2008",
            "Write string-table delta updates.",
            [
                Field("m_nTableID", "+0x10", "WriteUBitLong(Q_log2(MAX_TABLES))", "buffer.WriteUBitLong(m_nTableID, Q_log2(MAX_TABLES))", "uVar11 = param_1[4]"),
                Field("m_nChangedEntries", "+0x14", "WriteOneBit + optional WriteWord", "one entry is encoded as bit 0; otherwise bit 1 + WriteWord", "uVar11 = param_1[5]"),
                Field("m_nLength", "+0x18", "WriteUBitLong(20)", "buffer.WriteUBitLong(m_nLength, 20)", "param_1[6] = param_1[0x13]"),
                Field("m_DataOut", "+0x40", "WriteBits(m_nLength)", "buffer.WriteBits(m_DataOut.GetData(), m_nLength)", "FUN_0086e5c8(param_2,(byte *)param_1[0x10],(longlong)param_1[6])")
            ],
            [
                "param_1[6] = param_1[0x13]",
                "FUN_0086e5c8(param_2,(byte *)param_1[0x10],(longlong)param_1[6])"
            ],
            [
                "buffer.WriteUBitLong( m_nTableID, Q_log2( MAX_TABLES ) );",
                "buffer.WriteWord( m_nChangedEntries );",
                "buffer.WriteUBitLong( m_nLength, 20 );",
                "buffer.WriteBits( m_DataOut.GetData(), m_nLength );"
            ]),
        new(
            "SVC_PacketEntities",
            "server-map-load-write",
            "008cc300",
            "008ce4c0",
            "Write entity delta frames; this is the key server-to-client map/world-state payload after send tables are installed.",
            [
                Field("m_nMaxEntries", "+0x10", "WriteUBitLong(11) / MAX_EDICT_BITS", "buffer.WriteUBitLong(m_nMaxEntries, MAX_EDICT_BITS)", "_opd_FUN_008d3ef0(param_2,param_1[4],0xb)"),
                Field("m_bIsDelta", "+0x18", "WriteOneBit", "buffer.WriteOneBit(m_bIsDelta)", "cVar1 = *(char *)(param_1 + 6)"),
                Field("m_nDeltaFrom", "+0x20", "WriteLong if m_bIsDelta", "if delta: buffer.WriteLong(m_nDeltaFrom)", "FUN_0086caf8(param_2,param_1[8])"),
                Field("m_nBaseline", "+0x1c", "WriteUBitLong(1)", "buffer.WriteUBitLong(m_nBaseline, 1)", "_opd_FUN_008d3ef0(param_2,param_1[7],1)"),
                Field("m_nUpdatedEntries", "+0x14", "WriteUBitLong(11) / MAX_EDICT_BITS", "buffer.WriteUBitLong(m_nUpdatedEntries, MAX_EDICT_BITS)", "_opd_FUN_008d3ef0(param_2,param_1[5],0xb)"),
                Field("m_nLength", "+0x24", "WriteUBitLong(20) / DELTASIZE_BITS", "buffer.WriteUBitLong(m_nLength, DELTASIZE_BITS)", "_opd_FUN_008d3ef0(param_2,param_1[9],0x14)"),
                Field("m_bUpdateBaseline", "+0x19", "WriteOneBit", "buffer.WriteOneBit(m_bUpdateBaseline)", "*(char *)((int)param_1 + 0x19)"),
                Field("m_DataOut", "+0x4c", "WriteBits(m_nLength)", "buffer.WriteBits(m_DataOut.GetData(), m_nLength)", "FUN_0086e5c8(param_2,(byte *)param_1[0x13],(longlong)param_1[9])")
            ],
            [
                "_opd_FUN_008d3ef0(param_2,param_1[4],0xb)",
                "_opd_FUN_008d3ef0(param_2,param_1[9],0x14)",
                "FUN_0086e5c8(param_2,(byte *)param_1[0x13],(longlong)param_1[9])"
            ],
            [
                "buffer.WriteUBitLong( m_nMaxEntries, MAX_EDICT_BITS );",
                "buffer.WriteOneBit( m_bIsDelta?1:0 );",
                "buffer.WriteLong( m_nDeltaFrom );",
                "buffer.WriteUBitLong( m_nBaseline, 1 );",
                "buffer.WriteUBitLong( m_nUpdatedEntries, MAX_EDICT_BITS );",
                "buffer.WriteUBitLong( m_nLength, DELTASIZE_BITS );",
                "buffer.WriteOneBit( m_bUpdateBaseline?1:0 );",
                "buffer.WriteBits( m_DataOut.GetData(), m_nLength );"
            ])
    ];

    public static async Task ReduceAsync(
        string cExportPath,
        string sourceEngineRoot,
        string vtableCatalogPath,
        string outputPath)
    {
        var cExport = await File.ReadAllTextAsync(cExportPath);
        using var vtableDocument = JsonDocument.Parse(await File.ReadAllTextAsync(vtableCatalogPath));
        var messages = vtableDocument.RootElement.GetProperty("Messages").EnumerateArray().ToArray();
        var contracts = Specs
            .Select(spec => BuildContract(cExport, sourceEngineRoot, messages, spec))
            .ToArray();

        var report = new Tf2Ps3SourceCriticalMessageIoContractReport(
            "tf2ps3-source-critical-message-io-contract",
            "Pins TF.elf critical Source join/map-load ReadFromBuffer and WriteToBuffer contracts against structural vtables and local Source field order. This is the implementation target for the native server; it is intentionally not a blanket claim that every field is already simulated.",
            new Tf2Ps3SourceCriticalMessageIoInputs(cExportPath, sourceEngineRoot, vtableCatalogPath),
            new Tf2Ps3SourceCriticalMessageIoSummary(
                contracts.Length,
                contracts.Count(static contract => contract.VtableMatched),
                contracts.Count(static contract => contract.TfReadFunctionPresent),
                contracts.Count(static contract => contract.TfWriteFunctionPresent),
                contracts.Sum(static contract => contract.Fields.Length),
                contracts.Sum(static contract => contract.MissingTfEvidenceTokens.Length),
                contracts.Sum(static contract => contract.MissingSourceEvidenceTokens.Length)),
            contracts,
            [
                "The native server map-load path must generate the SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, SVC_CreateStringTable, SVC_UpdateStringTable, and SVC_PacketEntities payloads with TF.elf's 5-bit message ids and PS3-era field widths.",
                "The client-to-server join path must parse CLC_ClientInfo and CLC_BaselineAck before treating the session as fully ready for entity baselines.",
                "CLC_Move remains the already-reduced usercmd contract; this report adds the surrounding client-info/baseline and server map-load contracts.",
                "Fields marked as PS3-era divergence should be driven by TF.elf evidence first and local Source only as a semantic naming aid."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceCriticalMessageIoContract BuildContract(
        string cExport,
        string sourceEngineRoot,
        JsonElement[] messages,
        CriticalMessageSpec spec)
    {
        var message = messages.Single(item => ReadString(item, "ClassName") == spec.ClassName);
        var table = message.GetProperty("CandidateVtables").EnumerateArray().Single();
        var slots = table.GetProperty("Slots").EnumerateArray().ToArray();
        var readEntry = EntryFor(slots, "ReadFromBuffer");
        var writeEntry = EntryFor(slots, "WriteToBuffer");
        var readBody = ExtractFunctionBody(cExport, spec.ReadFunction);
        var writeBody = ExtractFunctionBody(cExport, spec.WriteFunction);
        var sourceEvidence = LoadSourceEvidence(sourceEngineRoot, spec.ClassName);
        var missingTf = spec.RequiredTfTokens
            .Where(token => !readBody.Contains(token, StringComparison.Ordinal) && !writeBody.Contains(token, StringComparison.Ordinal))
            .ToArray();
        var missingSource = spec.RequiredSourceTokens
            .Where(token => !sourceEvidence.Contains(token, StringComparison.Ordinal))
            .ToArray();

        return new Tf2Ps3SourceCriticalMessageIoContract(
            spec.ClassName,
            spec.Role,
            ReadString(message, "ImplementationPriority"),
            ReadString(table, "ObjectVptr"),
            readEntry,
            writeEntry,
            spec.ReadFunction,
            spec.WriteFunction,
            readEntry == "0x" + spec.ReadFunction,
            writeEntry == "0x" + spec.WriteFunction,
            readBody.Length > 0,
            writeBody.Length > 0,
            spec.Description,
            spec.Fields,
            spec.RequiredTfTokens,
            missingTf,
            spec.RequiredSourceTokens,
            missingSource);
    }

    private static string EntryFor(JsonElement[] slots, string role)
    {
        return slots.Single(slot => ReadString(slot, "Role") == role).GetProperty("EntryAddress").GetString() ?? "";
    }

    private static string ExtractFunctionBody(string cExport, string entry)
    {
        var marker = "_opd_FUN_" + entry;
        var start = -1;
        foreach (var prefix in new[] { "\nbyte ", "\nuint ", "\nvoid ", "\nundefined1 ", "\nundefined4 ", "\nundefined8 " })
        {
            start = cExport.IndexOf(prefix + marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                start++;
                break;
            }
        }

        if (start < 0)
        {
            return "";
        }

        var next = cExport.IndexOf("\n\n\n", start, StringComparison.Ordinal);
        return next < 0 ? cExport[start..] : cExport[start..next];
    }

    private static string LoadSourceEvidence(string sourceEngineRoot, string className)
    {
        var netmessagesCpp = Path.Combine(sourceEngineRoot, "common/netmessages.cpp");
        var netmessagesH = Path.Combine(sourceEngineRoot, "common/netmessages.h");
        var parts = new List<string>();
        if (File.Exists(netmessagesCpp))
        {
            parts.Add(File.ReadAllText(netmessagesCpp));
        }

        if (File.Exists(netmessagesH))
        {
            parts.Add(File.ReadAllText(netmessagesH));
        }

        return string.Join('\n', parts);
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? ""
                : "";
    }

    private static Tf2Ps3SourceCriticalMessageField Field(
        string name,
        string tfObjectOffset,
        string encoding,
        string sourceEquivalent,
        string tfEvidence)
    {
        return new Tf2Ps3SourceCriticalMessageField(name, tfObjectOffset, encoding, sourceEquivalent, tfEvidence);
    }

    private sealed record CriticalMessageSpec(
        string ClassName,
        string Role,
        string ReadFunction,
        string WriteFunction,
        string Description,
        Tf2Ps3SourceCriticalMessageField[] Fields,
        string[] RequiredTfTokens,
        string[] RequiredSourceTokens);
}

public sealed record Tf2Ps3SourceCriticalMessageIoContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceCriticalMessageIoInputs Inputs,
    Tf2Ps3SourceCriticalMessageIoSummary Summary,
    Tf2Ps3SourceCriticalMessageIoContract[] Contracts,
    string[] Findings);

public sealed record Tf2Ps3SourceCriticalMessageIoInputs(
    string TfElfCExport,
    string SourceEngineRoot,
    string VtableCatalogInput);

public sealed record Tf2Ps3SourceCriticalMessageIoSummary(
    int ContractCount,
    int VtableMatchedCount,
    int TfReadFunctionPresentCount,
    int TfWriteFunctionPresentCount,
    int FieldCount,
    int MissingTfEvidenceTokenCount,
    int MissingSourceEvidenceTokenCount);

public sealed record Tf2Ps3SourceCriticalMessageIoContract(
    string ClassName,
    string Role,
    string ImplementationPriority,
    string ObjectVptr,
    string VtableReadEntry,
    string VtableWriteEntry,
    string ExpectedReadEntry,
    string ExpectedWriteEntry,
    bool VtableMatched,
    bool WriteVtableMatched,
    bool TfReadFunctionPresent,
    bool TfWriteFunctionPresent,
    string Description,
    Tf2Ps3SourceCriticalMessageField[] Fields,
    string[] RequiredTfEvidenceTokens,
    string[] MissingTfEvidenceTokens,
    string[] RequiredSourceEvidenceTokens,
    string[] MissingSourceEvidenceTokens);

public sealed record Tf2Ps3SourceCriticalMessageField(
    string Name,
    string TfObjectOffset,
    string Encoding,
    string SourceEquivalent,
    string TfEvidence);
