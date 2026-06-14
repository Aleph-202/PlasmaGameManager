using System.Text.Json;
using System.Text.RegularExpressions;
using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRawUdpControlProbeReducer
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

    public static async Task<Tf2Ps3SourceRawUdpControlProbeReport> ReduceAsync(
        string cExportPath,
        string pcapInputPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var functionMap = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var probeFunction = functionMap.TryGetValue("0088a208", out var foundProbe) ? foundProbe : null;
        var rawRecvHelper = functionMap.TryGetValue("00813ea8", out var foundRawRecv) ? foundRawRecv : null;
        var rawRecvThunk = functionMap.TryGetValue("008709c8", out var foundRawRecvThunk) ? foundRawRecvThunk : null;

        var functionEvidence = BuildFunctionEvidence(probeFunction, rawRecvHelper, rawRecvThunk);
        var pcapEvidence = AnalyzePcapEvidence(pcapInputPath);
        var positiveFunctionEvidenceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "probe-function-present",
            "uses-raw-recv-thunk",
            "raw-recv-thunk-calls-recvfrom-helper",
            "raw-recvfrom-helper-present",
            "builds-bitreader-over-0x80-stack-buffer",
            "expects-control-byte-0x6e",
            "expects-version-byte-1",
            "stores-control-token-param1-0xe"
        };
        var probeRecovered = functionEvidence
            .Where(evidence => positiveFunctionEvidenceIds.Contains(evidence.Id))
            .All(static evidence => evidence.Found);
        var pcapProbeObserved = pcapEvidence.Summary.TransportBody6e01Count > 0;
        var excludedFromSourceInputBoundary = probeRecovered
            && functionEvidence.Any(static evidence => evidence.Id == "stores-control-token-param1-0xe" && evidence.Found)
            && !functionEvidence.Any(static evidence => evidence.Id == "source-payload-dispatcher-call" && evidence.Found)
            && !functionEvidence.Any(static evidence => evidence.Id == "payload-object-builder-call" && evidence.Found);

        var gates = new[]
        {
            new Tf2Ps3SourceRawUdpControlProbeGate(
                "raw-udp-control-probe-function-recovered",
                probeRecovered ? "proven" : "missing",
                "TF.elf 0088a208",
                "The raw UDP probe loop sends a small bitstream and waits for a 0x6e/0x01/u32 response."),
            new Tf2Ps3SourceRawUdpControlProbeGate(
                "raw-recv-helper-chain-recovered",
                rawRecvHelper is not null
                    && rawRecvThunk is not null
                    && rawRecvThunk.Body.Contains("_opd_FUN_00813ea8", StringComparison.Ordinal)
                    ? "proven" : "missing",
                "008709c8 -> 00813ea8 -> recvfrom",
                "The probe reads through the generic raw recvfrom helper, not through the connected Source frame reader."),
            new Tf2Ps3SourceRawUdpControlProbeGate(
                "pcap-6e01-control-probe-observed",
                pcapProbeObserved ? "proven" : "not-observed",
                pcapInputPath,
                "Historical active Source flows contain short transport bodies beginning with 0x6e01, matching this control probe family."),
            new Tf2Ps3SourceRawUdpControlProbeGate(
                "excluded-from-hard-markerless-source-input-boundary",
                excludedFromSourceInputBoundary ? "proven-negative" : "needs-review",
                "0088a208 body contract",
                "This path stores a control token in param_1[0xe] and does not call 008bdff0/008bdb88/008be1e8, 00a5d9e0, 00a58c10, or 008722a0."),
            new Tf2Ps3SourceRawUdpControlProbeGate(
                "native-source-input-ready",
                "missing",
                "server implementation gate",
                "Naming the raw UDP probe does not recover the high-entropy markerless Source usercmd/map-load upload transform.")
        };

        var report = new Tf2Ps3SourceRawUdpControlProbeReport(
            "tf2ps3-source-raw-udp-control-probe",
            "Names the TF.elf 0088a208 raw UDP mini-control probe so it is not confused with the remaining markerless Source input boundary.",
            new Tf2Ps3SourceRawUdpControlProbeInputs(cExportPath, pcapInputPath),
            new Tf2Ps3SourceRawUdpControlProbeSummary(
                probeFunction is not null,
                rawRecvThunk is not null,
                rawRecvHelper is not null,
                probeRecovered,
                pcapProbeObserved,
                pcapEvidence.Summary.TransportBody6e01Count,
                pcapEvidence.Summary.ClientToServer6e01Count,
                pcapEvidence.Summary.ServerToClient6e01Count,
                excludedFromSourceInputBoundary,
                false,
                gates.Count(static gate => gate.Status is "missing" or "needs-review")),
            functionEvidence,
            pcapEvidence.Files,
            pcapEvidence.Samples,
            gates,
            [
                "TF.elf 0088a208 is a real native raw UDP control probe: it sends a small bitstream, polls the raw recvfrom helper, reads byte 0x6e, byte 0x01, then a 32-bit token into param_1[0xe].",
                pcapProbeObserved
                    ? "The PCAP corpus contains matching 0x6e01 transport bodies, mostly as short control responses."
                    : "The active-flow PCAP corpus does not contain transport bodies beginning with 0x6e01; earlier 6e01 hits were signature/hash text in generated reports, not packet bytes.",
                "This path does not construct or consume the Source payload object / slot +0x70 / 008722a0 usercmd path, so it should be implemented or logged as control traffic only.",
                "The map-load blocker remains the high-entropy markerless Source input transform upstream of payload-object or owner-forwarder dispatch."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRawUdpControlProbeFunctionEvidence[] BuildFunctionEvidence(
        ExportedFunction? probeFunction,
        ExportedFunction? rawRecvHelper,
        ExportedFunction? rawRecvThunk)
    {
        var probeBody = probeFunction?.Body ?? "";
        var rawRecvBody = rawRecvHelper?.Body ?? "";
        var rawRecvThunkBody = rawRecvThunk?.Body ?? "";
        return
        [
            Evidence(
                "probe-function-present",
                "0088a208",
                "TF.elf exports/decompiles the raw UDP control probe candidate.",
                probeFunction is not null,
                Preview(probeFunction)),
            Evidence(
                "uses-raw-recv-thunk",
                "FUN_008709c8(param_1[0xf]",
                "The probe receives replies through the 008709c8 thunk.",
                probeBody.Contains("FUN_008709c8(param_1[0xf]", StringComparison.Ordinal),
                MatchingLines(probeBody, "FUN_008709c8")),
            Evidence(
                "raw-recv-thunk-calls-recvfrom-helper",
                "_opd_FUN_00813ea8",
                "008709c8 forwards to the generic raw recvfrom helper.",
                rawRecvThunkBody.Contains("_opd_FUN_00813ea8", StringComparison.Ordinal),
                MatchingLines(rawRecvThunkBody, "_opd_FUN_00813ea8")),
            Evidence(
                "raw-recvfrom-helper-present",
                "recvfrom(*(int *)(param_1 + 0x14)",
                "00813ea8 wraps recvfrom and writes the peer sockaddr into the caller-provided address buffer.",
                rawRecvBody.Contains("recvfrom(*(int *)(param_1 + 0x14)", StringComparison.Ordinal),
                MatchingLines(rawRecvBody, "recvfrom")),
            Evidence(
                "builds-bitreader-over-0x80-stack-buffer",
                "FUN_0086de68((int)&local_8f8,auStack_8d4,sVar8,0,-1)",
                "The reply bytes are wrapped in the same bitreader primitive used by other Source/engine payload paths.",
                probeBody.Contains("FUN_0086de68((int)&local_8f8,auStack_8d4,sVar8,0,-1)", StringComparison.Ordinal),
                MatchingLines(probeBody, "FUN_0086de68")),
            Evidence(
                "expects-control-byte-0x6e",
                "(uVar7 & 0xff) != 0x6e",
                "The first decoded byte must be 0x6e.",
                probeBody.Contains("(uVar7 & 0xff) != 0x6e", StringComparison.Ordinal)
                    || probeBody.Contains("(uVar7 & 0xff) == 0x6e", StringComparison.Ordinal),
                MatchingLines(probeBody, "0x6e")),
            Evidence(
                "expects-version-byte-1",
                "uVar9 != 1",
                "The second decoded byte must be 1.",
                probeBody.Contains("uVar9 != 1", StringComparison.Ordinal)
                    || probeBody.Contains("uVar9 == 1", StringComparison.Ordinal),
                MatchingLines(probeBody, "uVar9")),
            Evidence(
                "stores-control-token-param1-0xe",
                "param_1[0xe]",
                "The following 32-bit value is stored in param_1[0xe], which is later used to send another control probe.",
                probeBody.Contains("param_1[0xe]", StringComparison.Ordinal)
                    || probeBody.Contains("*puVar2", StringComparison.Ordinal),
                MatchingLines(probeBody, "param_1[0xe]", "*puVar2")),
            Evidence(
                "source-payload-dispatcher-call",
                "_opd_FUN_00a58c10",
                "This would indicate a Source netmessage dispatch path. It must remain absent for this probe.",
                probeBody.Contains("_opd_FUN_00a58c10", StringComparison.Ordinal),
                MatchingLines(probeBody, "_opd_FUN_00a58c10")),
            Evidence(
                "payload-object-builder-call",
                "_opd_FUN_008bdff0",
                "This would indicate the payload-object wrapper path. It must remain absent for this probe.",
                probeBody.Contains("_opd_FUN_008bdff0", StringComparison.Ordinal)
                    || probeBody.Contains("_opd_FUN_008bdb88", StringComparison.Ordinal)
                    || probeBody.Contains("_opd_FUN_008be1e8", StringComparison.Ordinal),
                MatchingLines(probeBody, "_opd_FUN_008bdff0", "_opd_FUN_008bdb88", "_opd_FUN_008be1e8"))
        ];
    }

    private static Tf2Ps3SourceRawUdpControlProbePcapEvidence AnalyzePcapEvidence(string inputPath)
    {
        var extractor = new PcapActiveFlowReplayExtractor();
        var files = EnumeratePcapInputs(inputPath);
        var summaries = new List<Tf2Ps3SourceRawUdpControlProbePcapFile>();
        var samples = new List<Tf2Ps3SourceRawUdpControlProbePcapSample>();
        foreach (var file in files)
        {
            var replay = extractor.Extract(file);
            if (replay is null)
            {
                continue;
            }

            var clientMatches = 0;
            var serverMatches = 0;
            var totalPackets = 0;
            foreach (var packet in replay.SourcePackets)
            {
                if (!Ps3SourceTransportPacket.TryDecode(packet.Payload, out var transport))
                {
                    continue;
                }

                totalPackets++;
                if (transport.Body.Length < 2
                    || transport.Body[0] != 0x6e
                    || transport.Body[1] != 0x01)
                {
                    continue;
                }

                if (packet.Direction == PcapActiveFlowDirection.ClientToServer)
                {
                    clientMatches++;
                }
                else
                {
                    serverMatches++;
                }

                if (samples.Count < 64)
                {
                    samples.Add(new Tf2Ps3SourceRawUdpControlProbePcapSample(
                        RelativePath(inputPath, file),
                        Array.IndexOf(replay.SourcePackets, packet),
                        packet.PacketIndex,
                        packet.Direction.ToString(),
                        transport.CandidateSequence,
                        transport.Body.Length,
                        HexPrefix(transport.Body, 16),
                        transport.Body.Length >= 6
                            ? ((uint)transport.Body[2] << 24)
                              | ((uint)transport.Body[3] << 16)
                              | ((uint)transport.Body[4] << 8)
                              | transport.Body[5]
                            : null));
                }
            }

            if (clientMatches > 0 || serverMatches > 0)
            {
                summaries.Add(new Tf2Ps3SourceRawUdpControlProbePcapFile(
                    RelativePath(inputPath, file),
                    replay.ClientEndpoint,
                    replay.ServerEndpoint,
                    totalPackets,
                    clientMatches,
                    serverMatches));
            }
        }

        var summary = new Tf2Ps3SourceRawUdpControlProbePcapSummary(
            files.Length,
            summaries.Count,
            summaries.Sum(static file => file.ClientToServer6e01Count + file.ServerToClient6e01Count),
            summaries.Sum(static file => file.ClientToServer6e01Count),
            summaries.Sum(static file => file.ServerToClient6e01Count));
        return new Tf2Ps3SourceRawUdpControlProbePcapEvidence(
            summary,
            summaries
                .OrderBy(static file => file.File, StringComparer.Ordinal)
                .ToArray(),
            samples.ToArray());
    }

    private static string[] EnumeratePcapInputs(string inputPath)
    {
        if (File.Exists(inputPath))
        {
            return [inputPath];
        }

        if (!Directory.Exists(inputPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static Tf2Ps3SourceRawUdpControlProbeFunctionEvidence Evidence(
        string id,
        string needle,
        string meaning,
        bool found,
        string[] lines) =>
        new(id, needle, meaning, found, lines);

    private static string[] MatchingLines(string body, params string[] needles) =>
        body.Split('\n')
            .Select(static line => line.Trim())
            .Where(line => needles.Any(needle => line.Contains(needle, StringComparison.Ordinal)))
            .Take(24)
            .ToArray();

    private static string[] Preview(ExportedFunction? function)
    {
        if (function is null)
        {
            return [];
        }

        return function.Lines.Take(120).ToArray();
    }

    private static string HexPrefix(ReadOnlySpan<byte> bytes, int take) =>
        Convert.ToHexString(bytes[..Math.Min(bytes.Length, take)]).ToLowerInvariant();

    private static string RelativePath(string inputRoot, string file)
    {
        if (File.Exists(inputRoot))
        {
            return Path.GetFileName(file);
        }

        return Path.GetRelativePath(inputRoot, file).Replace(Path.DirectorySeparatorChar, '/');
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
        return new ExportedFunction(name, address, functionLines, string.Join('\n', functionLines));
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);

    private sealed record Tf2Ps3SourceRawUdpControlProbePcapEvidence(
        Tf2Ps3SourceRawUdpControlProbePcapSummary Summary,
        Tf2Ps3SourceRawUdpControlProbePcapFile[] Files,
        Tf2Ps3SourceRawUdpControlProbePcapSample[] Samples);
}

public sealed record Tf2Ps3SourceRawUdpControlProbeReport(
    string Status,
    string Note,
    Tf2Ps3SourceRawUdpControlProbeInputs Inputs,
    Tf2Ps3SourceRawUdpControlProbeSummary Summary,
    Tf2Ps3SourceRawUdpControlProbeFunctionEvidence[] FunctionEvidence,
    Tf2Ps3SourceRawUdpControlProbePcapFile[] PcapFiles,
    Tf2Ps3SourceRawUdpControlProbePcapSample[] PcapSamples,
    Tf2Ps3SourceRawUdpControlProbeGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceRawUdpControlProbeInputs(
    string CExportInput,
    string PcapInput);

public sealed record Tf2Ps3SourceRawUdpControlProbeSummary(
    bool ProbeFunctionPresent,
    bool RawRecvThunkPresent,
    bool RawRecvfromHelperPresent,
    bool RawUdpControlProbeRecovered,
    bool Pcap6e01ControlProbeObserved,
    int Pcap6e01ControlProbeCount,
    int PcapClientToServer6e01Count,
    int PcapServerToClient6e01Count,
    bool ExcludedFromHardMarkerlessSourceInputBoundary,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceRawUdpControlProbeFunctionEvidence(
    string Id,
    string Needle,
    string Meaning,
    bool Found,
    string[] Lines);

public sealed record Tf2Ps3SourceRawUdpControlProbePcapSummary(
    int PcapFileCount,
    int FileWith6e01ControlProbeCount,
    int TransportBody6e01Count,
    int ClientToServer6e01Count,
    int ServerToClient6e01Count);

public sealed record Tf2Ps3SourceRawUdpControlProbePcapFile(
    string File,
    string ClientEndpoint,
    string ServerEndpoint,
    int SourcePacketCount,
    int ClientToServer6e01Count,
    int ServerToClient6e01Count);

public sealed record Tf2Ps3SourceRawUdpControlProbePcapSample(
    string File,
    int SourceStep,
    long PacketIndex,
    string Direction,
    ushort Sequence,
    int BodyLength,
    string BodyPrefixHex,
    uint? ControlTokenBigEndian);

public sealed record Tf2Ps3SourceRawUdpControlProbeGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
