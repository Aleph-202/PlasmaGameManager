using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMarkerlessTransformCandidateReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex CallRegex = new(
        @"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto)\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008b8d50",
        "00a5b4f0",
        "00a5d9e0"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceMarkerlessTransformCandidateReport> ReduceAsync(
        string cExportPath,
        string markerlessClassifierPath,
        string outputPath)
    {
        var lines = await File.ReadAllLinesAsync(cExportPath);
        var functions = ExtractFunctions(lines)
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .OrderBy(static function => Array.IndexOf(TargetAddresses, function.Address))
            .ToArray();

        using var classifier = JsonDocument.Parse(await File.ReadAllTextAsync(markerlessClassifierPath));
        var classifierSummary = classifier.RootElement.GetProperty("Summary");
        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var ingress = byAddress.GetValueOrDefault("008b8d50");
        var queueDrain = byAddress.GetValueOrDefault("00a5b4f0");
        var transform = byAddress.GetValueOrDefault("00a5d9e0");

        var transformLooksStrong = transform is not null
            && transform.Calls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal)
            && transform.EvidenceTokens.Contains("param_2[0xb]", StringComparer.Ordinal)
            && transform.EvidenceTokens.Contains("param_2[0xc]", StringComparer.Ordinal)
            && transform.EvidenceTokens.Contains("param_2[0xd]", StringComparer.Ordinal)
            && transform.EvidenceTokens.Contains("param_2[0xe]", StringComparer.Ordinal)
            && transform.EvidenceTokens.Contains("param_2 + 7", StringComparer.Ordinal);
        var queueDrainClassified = queueDrain is not null
            && queueDrain.Calls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal)
            && queueDrain.EvidenceTokens.Contains("PTR_PTR_01977d5c", StringComparer.Ordinal)
            && queueDrain.EvidenceTokens.Contains("iVar2 + 0x1c", StringComparer.Ordinal);
        var directIngressLinkProven = ingress is not null
            && (ingress.Calls.Contains("_opd_FUN_00a5d9e0", StringComparer.Ordinal)
                || ingress.Calls.Contains("_opd_FUN_00a5b4f0", StringComparer.Ordinal));

        var gates = new[]
        {
            new Tf2Ps3SourceMarkerlessTransformGate(
                "queued-payload-drain-classified",
                queueDrainClassified ? "proven" : "missing",
                "00a5b4f0",
                "The helper pops payload objects from PTR_PTR_01977d5c through virtual slot +0x48 and dispatches queued body iVar2 + 0x1c to 00a58c10.",
                "Treat this as queued/staged payload drain, not proof that direct markerless PCAP bodies start at this boundary."),
            new Tf2Ps3SourceMarkerlessTransformGate(
                "bitreader-transform-candidate-recovered",
                transformLooksStrong ? "candidate-proven" : "missing",
                "00a5d9e0",
                "The helper receives a bitreader-like param_2, reads param_2[0xb]/[0xc]/[0xd]/[0xe], consumes flag bits/subpayloads, then dispatches remaining bits with 00a58c10(param_1, param_2 + 7).",
                "This is the strongest current inner markerless transform candidate, but its caller/ingress from direct UDP still needs proof."),
            new Tf2Ps3SourceMarkerlessTransformGate(
                "direct-udp-ingress-to-transform",
                directIngressLinkProven ? "proven" : "missing",
                "008b8d50 -> 00a5d9e0",
                "The current C export of 008b8d50 only exposes recvfrom draining, not a direct or indirect call edge to the helper-slice transform candidate.",
                "Export instruction/caller context around the post-recvfrom consumer or the helper-slice virtual dispatch site that supplies param_2 to 00a5d9e0."),
            new Tf2Ps3SourceMarkerlessTransformGate(
                "native-markerless-input-ready",
                transformLooksStrong && directIngressLinkProven ? "proven" : "missing",
                "implementation gate",
                "A native server can implement direct markerless input only after both the bitreader transform and direct ingress link are proven.",
                "Keep movement/loading-ack decoding behind evidence logging until this gate is proven.")
        };

        var report = new Tf2Ps3SourceMarkerlessTransformCandidateReport(
            "tf2ps3-source-markerless-transform-candidates",
            "Classifies TF.elf helper-slice receive candidates for the remaining direct markerless client packet family.",
            new Tf2Ps3SourceMarkerlessTransformInputs(
                cExportPath,
                markerlessClassifierPath),
            new Tf2Ps3SourceMarkerlessTransformSummary(
                functions.Length,
                classifierSummary.GetProperty("OpaquePacketCount").GetInt32(),
                classifierSummary.GetProperty("DominantBodyLength").GetInt32(),
                queueDrainClassified,
                transformLooksStrong,
                directIngressLinkProven,
                transformLooksStrong && directIngressLinkProven,
                transform?.Address ?? "",
                gates.Count(static gate => gate.Status != "proven")),
            functions,
            gates,
            [
                "00a5d9e0 is now the best recovered inner transform candidate: it operates on a bitreader-like payload object and can route remaining bits to 00a58c10.",
                "00a5b4f0 is classified as a queued payload drain: it pops staged payloads and passes iVar2 + 0x1c to 00a58c10.",
                "The still-missing proof is the ingress/caller edge that shows direct 008b8d50 markerless UDP bodies are wrapped into the param_2 bitreader consumed by 00a5d9e0.",
                "Next Ghidra pass should export callers or virtual dispatch context for helper-slice slot +0x70 / function 00a5d9e0 and the post-recvfrom buffer consumer from 008b8d50."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceMarkerlessTransformFunction BuildFunction(ExportedFunction function)
    {
        var body = function.Body;
        return new Tf2Ps3SourceMarkerlessTransformFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            function.Address switch
            {
                "008b8d50" => "udp-gameplay-receive-drain",
                "00a5b4f0" => "queued-payload-drain-to-source-dispatcher",
                "00a5d9e0" => "bitreader-transform-candidate-to-source-dispatcher",
                _ => "unknown"
            },
            ExtractCalls(body),
            BuildEvidence(body),
            function.Address switch
            {
                "008b8d50" => "recvfrom buffer auStack_83c, 0x800 max bytes, nonblocking flag 0x80",
                "00a5b4f0" => "queued payload object returned by global queue virtual slot +0x48; payload starts at iVar2 + 0x1c",
                "00a5d9e0" => "bitreader-like param_2 with current word/bit-count/cursor/end fields at [0xb]/[0xc]/[0xd]/[0xe]",
                _ => ""
            },
            function.Address switch
            {
                "008b8d50" => "no transform dispatch exposed by current C export",
                "00a5b4f0" => "00a58c10(param_1, iVar2 + 0x1c)",
                "00a5d9e0" => "optional subpayload handling through 00a594e8/00a5d720, then 00a58c10(param_1, param_2 + 7) when unread bits remain",
                _ => ""
            },
            Preview(function.Lines));
    }

    private static string[] BuildEvidence(string body)
    {
        var evidence = new List<string>();
        foreach (var token in new[]
        {
            "recvfrom(",
            "0x800",
            "0x80",
            "PTR_PTR_01977d5c",
            "+ 0x48",
            "iVar2 + 0x1c",
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a579d8",
            "_opd_FUN_00a594e8",
            "_opd_FUN_00a5aa00",
            "_opd_FUN_00a5d720",
            "FUN_0086d848(param_1 + 0x26)",
            "FUN_0086fb58(param_2,param_1 + 0x26,0)",
            "param_2[0xb]",
            "param_2[0xc]",
            "param_2[0xd]",
            "param_2[0xe]",
            "param_2[9]",
            "param_2[0xf]",
            "param_2[10]",
            "param_2 + 7",
            "*(int *)param_1[0x782] + 0x14",
            "*(int *)param_1[0x782] + 0x18"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                evidence.Add(token);
            }
        }

        return evidence.ToArray();
    }

    private static string[] ExtractCalls(string body)
    {
        return CallRegex.Matches(body)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static List<ExportedFunction> ExtractFunctions(string[] lines)
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
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(90));
        return text.Length <= 2600 ? text : text[..2600];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceMarkerlessTransformCandidateReport(
    string Status,
    string Note,
    Tf2Ps3SourceMarkerlessTransformInputs Inputs,
    Tf2Ps3SourceMarkerlessTransformSummary Summary,
    Tf2Ps3SourceMarkerlessTransformFunction[] Functions,
    Tf2Ps3SourceMarkerlessTransformGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceMarkerlessTransformInputs(
    string CExportPath,
    string MarkerlessClassifierReport);

public sealed record Tf2Ps3SourceMarkerlessTransformSummary(
    int TargetFunctionCount,
    int OpaquePacketCount,
    int DominantOpaqueBodyLength,
    bool QueueDrainClassified,
    bool BitreaderTransformCandidateRecovered,
    bool DirectIngressLinkProven,
    bool NativeMarkerlessInputReady,
    string BestTransformCandidateAddress,
    int OpenGateCount);

public sealed record Tf2Ps3SourceMarkerlessTransformFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string InputShape,
    string DispatchPath,
    string Preview);

public sealed record Tf2Ps3SourceMarkerlessTransformGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Evidence,
    string RemainingWork);
