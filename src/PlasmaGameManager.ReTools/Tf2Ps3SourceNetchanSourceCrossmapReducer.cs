using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceNetchanSourceCrossmapReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Tf2Ps3SourceNetchanExpectedMessage[] ExpectedConnectionStartMessages =
    [
        new("NET_Tick", "net", "Tick", true),
        new("NET_StringCmd", "net", "StringCmd", true),
        new("NET_SetConVar", "net", "SetConVar", true),
        new("NET_SignonState", "net", "SignonState", true),
        new("CLC_ClientInfo", "client", "ClientInfo", true),
        new("CLC_Move", "client", "Move", true),
        new("CLC_VoiceData", "client", "VoiceData", true),
        new("CLC_BaselineAck", "client", "BaselineAck", true),
        new("CLC_ListenEvents", "client", "ListenEvents", true),
        new("CLC_RespondCvarValue", "client", "RespondCvarValue", true),
        new("CLC_FileCRCCheck", "client", "FileCRCCheck", true),
        new("CLC_FileMD5Check", "client", "FileMD5Check", true),
        new("CLC_CmdKeyValues", "client", "CmdKeyValues", true),
        new("CLC_SaveReplay", "client", "SaveReplay", false)
    ];

    public static async Task ReduceAsync(
        string cExportPath,
        string sourceEngineRoot,
        string outputPath)
    {
        var cExport = await File.ReadAllLinesAsync(cExportPath);
        var functions = ExtractFunctions(cExport).ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var sourceFiles = LoadSourceFiles(sourceEngineRoot);
        var maps = BuildMaps(functions, sourceFiles);
        var report = new Tf2Ps3SourceNetchanSourceCrossmapReport(
            "tf2ps3-source-netchan-source-crossmap",
            "Cross-maps the proven TF.elf helper slice to Source CNetChan semantics using local Source engine code. This names the native functions and records the important PS3 delta: TF.elf reads 5-bit net message ids, while the local Source tree uses NETMSG_TYPE_BITS=6.",
            cExportPath,
            sourceEngineRoot,
            new Tf2Ps3SourceNetchanSourceCrossmapSummary(
                maps.Length,
                maps.Count(static map => map.Confidence == "strong"),
                5,
                ExtractNetmsgTypeBits(sourceFiles.NetH),
                ExpectedConnectionStartMessages.Count(static message => message.RequiredByBaseClientConnectionStart),
                0),
            maps,
            ExpectedConnectionStartMessages,
            BuildFindings(maps, sourceFiles));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceNetchanCrossmapEntry[] BuildMaps(
        IReadOnlyDictionary<string, ExportedFunction> functions,
        SourceFiles sourceFiles)
    {
        return
        [
            BuildMap(functions, sourceFiles, "00a57f48", "CNetChan::FindMessage", "registered-message-lookup",
                ["INetMessage *CNetChan::FindMessage(int type)", "int numtypes = m_NetMessages.Count();", "m_NetMessages[i]->GetType() == type"],
                ["param_1 + 0x1e0c", "+ 0x20", "return 0"],
                "strong",
                "TF scans the object registered-handler vector and calls handler vtable +0x20 for the type, matching Source CNetChan::FindMessage."),
            BuildMap(functions, sourceFiles, "00a5df70", "CNetChan::RegisterMessage", "registered-message-install",
                ["bool CNetChan::RegisterMessage(INetMessage *msg)", "m_NetMessages.AddToTail( msg );", "msg->SetNetChannel( this );"],
                ["_opd_FUN_00a57f48", "_opd_FUN_00a625e8", "+ 8"],
                "strong",
                "TF rejects duplicate ids through FindMessage, appends to the handler vector, then calls handler vtable +0x08, matching msg->SetNetChannel(this)."),
            BuildMap(functions, sourceFiles, "00a58c10", "CNetChan::ProcessMessages", "payload-dispatcher",
                ["bool CNetChan::ProcessMessages", "buf.ReadUBitLong( NETMSG_TYPE_BITS )", "INetMessage\t* netmsg = FindMessage( cmd );", "netmsg->ReadFromBuffer( buf )"],
                ["_opd_FUN_00a58868", "_opd_FUN_00a57f48", "+ 0x14", "+ 0x10"],
                "strong",
                "TF reads 5-bit message ids, handles built-in controls, then dispatches registered messages through parse/execute vtable slots."),
            BuildMap(functions, sourceFiles, "00a58868", "CNetChan::ProcessControlMessage", "builtin-control-message-handler",
                ["if ( cmd == net_NOP )", "if ( cmd == net_Disconnect )", "if ( cmd == net_File )", "m_MessageHandler->FileRequested", "m_MessageHandler->FileDenied"],
                ["param_2 == 2", "+ 0x1c", "+ 0x24"],
                "strong",
                "TF's built-in ids 0/1/2 line up with Source control messages NOP, Disconnect, and File."),
            BuildMap(functions, sourceFiles, "00a61150", "CNetChan::SendDatagram", "native-datagram-builder",
                ["int CNetChan::SendDatagram", "CNetChan_TransmitBits->send", "send.WriteLong ( m_nOutSequenceNr );", "send.WriteLong ( m_nInSequenceNr );"],
                ["_opd_FUN_008bc978", "param_1[2]", "param_1[3]", "0x17710"],
                "strong",
                "TF builds native CNetChan datagrams with the same sequence/ack header shape and send-buffer string evidence."),
            BuildMap(functions, sourceFiles, "00a5e058", "CNetChan::CNetChan/Clear-style initializer", "netchannel-object-initializer",
                ["CNetChan::CNetChan()", "m_StreamUnreliable.SetDebugName", "m_nOutSequenceNr = 1"],
                ["PTR_PTR_01977dbc", "param_1 + 0x1e0c", "96000"],
                "strong",
                "TF installs the concrete object table, initializes bit buffers, initializes the registered-handler vector, and clears attached socket/state."),
            BuildMap(functions, sourceFiles, "00a5e2c8", "CNetChan::SetMaxBufferSize", "buffer-resize-helper",
                ["void CNetChan::SetMaxBufferSize", "stream = &m_StreamReliable;", "stream = &m_StreamVoice;", "stream = &m_StreamUnreliable;"],
                ["4000", "96000", "param_2 == 0", "param_4 == 0"],
                "strong",
                "TF chooses among the same reliable/unreliable/voice-style buffers and clamps resize requests to Source-native payload limits.")
        ];
    }

    private static Tf2Ps3SourceNetchanCrossmapEntry BuildMap(
        IReadOnlyDictionary<string, ExportedFunction> functions,
        SourceFiles sourceFiles,
        string tfAddress,
        string sourceName,
        string role,
        string[] sourceNeedles,
        string[] tfNeedles,
        string confidence,
        string conclusion)
    {
        functions.TryGetValue(tfAddress, out var function);
        var tfBody = function?.Body ?? "";
        var tfEvidence = tfNeedles
            .Where(needle => tfBody.Contains(needle, StringComparison.Ordinal))
            .ToArray();
        var sourceEvidence = sourceNeedles
            .Select(needle => FindSourceEvidence(sourceFiles, needle))
            .Where(static evidence => evidence.LineNumber > 0)
            .ToArray();

        return new Tf2Ps3SourceNetchanCrossmapEntry(
            tfAddress,
            sourceName,
            role,
            confidence,
            tfEvidence,
            sourceEvidence,
            conclusion);
    }

    private static Tf2Ps3SourceEvidenceLine FindSourceEvidence(SourceFiles sourceFiles, string needle)
    {
        foreach (var (path, lines) in sourceFiles.All)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(needle, StringComparison.Ordinal))
                {
                    return new Tf2Ps3SourceEvidenceLine(path, i + 1, lines[i].Trim());
                }
            }
        }

        return new Tf2Ps3SourceEvidenceLine("", 0, "");
    }

    private static int ExtractNetmsgTypeBits(string[] netH)
    {
        foreach (var line in netH)
        {
            var match = Regex.Match(line, @"#define\s+NETMSG_TYPE_BITS\s+(?<bits>\d+)");
            if (match.Success)
            {
                return int.Parse(match.Groups["bits"].Value);
            }
        }

        return 0;
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceNetchanCrossmapEntry> maps,
        SourceFiles sourceFiles)
    {
        return
        [
            "The TF.elf helper slice is Source CNetChan: 00a57f48 maps to FindMessage, 00a5df70 maps to RegisterMessage, 00a58c10 maps to ProcessMessages, and 00a61150 maps to SendDatagram.",
            $"The local Source tree defines NETMSG_TYPE_BITS={ExtractNetmsgTypeBits(sourceFiles.NetH)}, while TF.elf's dispatcher consumes 5-bit ids. The native PS3 server must keep the TF.elf 5-bit contract rather than copying newer PC framing blindly.",
            "CBaseClient::ConnectionStart is the best semantic template for server-side registered client message objects: NET_Tick, NET_StringCmd, NET_SetConVar, NET_SignonState, CLC_ClientInfo, CLC_Move, CLC_VoiceData, CLC_BaselineAck, CLC_ListenEvents, CLC_RespondCvarValue, CLC_FileCRCCheck, CLC_FileMD5Check, and CLC_CmdKeyValues.",
            "This cross-map proves CNetChan mechanics and expected message families, but concrete TF.elf registered handler object constructors still need to be recovered before implementing map-load payload behavior as native rather than replay-like."
        ];
    }

    private static SourceFiles LoadSourceFiles(string sourceEngineRoot)
    {
        var netChanCppPath = Path.Combine(sourceEngineRoot, "engine/net_chan.cpp");
        var netHPath = Path.Combine(sourceEngineRoot, "engine/net.h");
        var inetMsgHandlerPath = Path.Combine(sourceEngineRoot, "public/inetmsghandler.h");
        var baseClientPath = Path.Combine(sourceEngineRoot, "engine/baseclient.cpp");
        return new SourceFiles(
            File.ReadAllLines(netChanCppPath),
            File.ReadAllLines(netHPath),
            File.ReadAllLines(inetMsgHandlerPath),
            File.ReadAllLines(baseClientPath),
            [
                (netChanCppPath, File.ReadAllLines(netChanCppPath)),
                (netHPath, File.ReadAllLines(netHPath)),
                (inetMsgHandlerPath, File.ReadAllLines(inetMsgHandlerPath)),
                (baseClientPath, File.ReadAllLines(baseClientPath))
            ]);
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
        return new ExportedFunction(name, address, string.Join('\n', functionLines));
    }

    private sealed record SourceFiles(
        string[] NetChanCpp,
        string[] NetH,
        string[] InetMsgHandlerH,
        string[] BaseClientCpp,
        (string Path, string[] Lines)[] All);

    private sealed record ExportedFunction(string Name, string Address, string Body);
}

public sealed record Tf2Ps3SourceNetchanSourceCrossmapReport(
    string Status,
    string Note,
    string CExportInput,
    string SourceEngineRoot,
    Tf2Ps3SourceNetchanSourceCrossmapSummary Summary,
    Tf2Ps3SourceNetchanCrossmapEntry[] FunctionCrossmap,
    Tf2Ps3SourceNetchanExpectedMessage[] ExpectedConnectionStartMessages,
    string[] Findings);

public sealed record Tf2Ps3SourceNetchanSourceCrossmapSummary(
    int MappedFunctionCount,
    int StrongMappedFunctionCount,
    int TfElfNetMessageTypeBits,
    int LocalSourceNetMessageTypeBits,
    int RequiredConnectionStartMessageCount,
    int ConcreteTfElfHandlerConstructorProofCount);

public sealed record Tf2Ps3SourceNetchanCrossmapEntry(
    string TfElfFunctionAddress,
    string SourceSemanticName,
    string Role,
    string Confidence,
    string[] TfElfEvidenceTokens,
    Tf2Ps3SourceEvidenceLine[] SourceEvidence,
    string Conclusion);

public sealed record Tf2Ps3SourceEvidenceLine(string Path, int LineNumber, string Text);

public sealed record Tf2Ps3SourceNetchanExpectedMessage(
    string ClassName,
    string Family,
    string MacroName,
    bool RequiredByBaseClientConnectionStart);
