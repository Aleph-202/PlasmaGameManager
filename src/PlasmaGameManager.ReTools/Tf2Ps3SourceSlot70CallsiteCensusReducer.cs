using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceSlot70CallsiteCensusReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex Slot70CallRegex = new(
        @"\(\*\(code \*\).*?\+\s*0x70(?![0-9a-f])",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceSlot70CallsiteCensusReport> ReduceAsync(
        string cExportPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var callsites = functions
            .Select(BuildCallsite)
            .Where(static callsite => callsite.Slot70CallExpressions.Length > 0)
            .OrderByDescending(static callsite => callsite.Score)
            .ThenBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();

        var classCounts = callsites
            .GroupBy(static callsite => callsite.Classification, StringComparer.Ordinal)
            .Select(static group => new Tf2Ps3SourceSlot70ClassCount(group.Key, group.Count()))
            .OrderBy(static count => count.Classification, StringComparer.Ordinal)
            .ToArray();
        var directMarkerlessCandidates = callsites
            .Where(static callsite => callsite.Classification == "direct-markerless-ingress-candidate")
            .ToArray();
        var networkOrSocket = callsites
            .Where(static callsite => HasAny(callsite.EvidenceTokens, "recvfrom-call", "connected-socket-read-call", "bitbuffer-init-helper"))
            .ToArray();
        var payloadDispatcher = callsites
            .Where(static callsite => HasAny(callsite.EvidenceTokens, "native-source-payload-dispatcher-call", "slot70-wrapper-direct-token"))
            .ToArray();
        var helperSliceToken = callsites
            .Where(static callsite => HasAny(callsite.EvidenceTokens, "helper-slice-opd-token", "helper-slice-table-token", "queued-payload-global-token", "attached-reader-buffer-token"))
            .ToArray();
        var sourceSetup = callsites
            .Where(static callsite => callsite.Classification == "source-setup-or-association")
            .ToArray();
        var sourceTokenNonIngress = callsites
            .Where(static callsite => callsite.Classification == "source-token-non-ingress")
            .ToArray();
        var param2FieldShape = callsites
            .Where(static callsite => HasAny(callsite.EvidenceTokens, "param2-field-shape-token"))
            .ToArray();
        var ranked = callsites
            .Where(static callsite => callsite.Score > 0
                || callsite.Classification != "generic-slot70-interface")
            .Take(60)
            .ToArray();

        var gates = new[]
        {
            new Tf2Ps3SourceSlot70CallsiteCensusGate(
                "all-exact-slot70-calls-censused",
                callsites.Length > 0 ? "proven" : "missing",
                "TF.elf C export exact virtual slot scan",
                "All exact C-export virtual calls through slot +0x70 are counted before claiming a caller-side param_2 builder is absent."),
            new Tf2Ps3SourceSlot70CallsiteCensusGate(
                "no-network-or-bitbuffer-slot70-calls",
                networkOrSocket.Length == 0 ? "proven-negative" : "candidate",
                "slot +0x70 callsites intersected with recvfrom/008b82c0/FUN_0086de68",
                "No exact slot +0x70 callsite in the current C export also contains socket read or bitbuffer construction evidence."),
            new Tf2Ps3SourceSlot70CallsiteCensusGate(
                "no-payload-dispatcher-slot70-calls",
                payloadDispatcher.Length == 0 ? "proven-negative" : "candidate",
                "slot +0x70 callsites intersected with 00a58c10/00a5d9e0 tokens",
                "No exact slot +0x70 callsite directly contains native payload-dispatcher or slot +0x70 wrapper call tokens."),
            new Tf2Ps3SourceSlot70CallsiteCensusGate(
                "direct-markerless-ingress-candidate",
                directMarkerlessCandidates.Length == 0 ? "missing" : "candidate",
                "ranked slot +0x70 callsite census",
                "The caller-side transform for hard markerless PCAP bodies still is not visible as an exact slot +0x70 callsite in the C export."),
            new Tf2Ps3SourceSlot70CallsiteCensusGate(
                "native-source-input-ready",
                directMarkerlessCandidates.Length > 0 ? "candidate" : "missing",
                "server implementation gate",
                "Native client-upload handling remains gated until a candidate is proven against TF.elf dataflow and PCAP bodies.")
        };

        var report = new Tf2Ps3SourceSlot70CallsiteCensusReport(
            "tf2ps3-source-slot70-callsite-census",
            "Censuses every exact TF.elf C-export virtual call through slot +0x70 to narrow the missing caller-side bitreader builder for 00a5d9e0.",
            new Tf2Ps3SourceSlot70CallsiteCensusInputs(cExportPath),
            new Tf2Ps3SourceSlot70CallsiteCensusSummary(
                functions.Count,
                callsites.Length,
                networkOrSocket.Length,
                payloadDispatcher.Length,
                helperSliceToken.Length,
                sourceSetup.Length,
                sourceTokenNonIngress.Length,
                param2FieldShape.Length,
                directMarkerlessCandidates.Length,
                false,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            classCounts,
            ranked,
            gates,
            [
                "The C export has 306 exact slot +0x70 virtual-call functions, but zero combine the slot call with recvfrom, 008b82c0 connected-socket reads, or FUN_0086de68 bitbuffer init.",
                "The two Source helper-slice-looking slot +0x70 calls remain 0070c300 and 00a5d0c0, both setup/association paths already known not to consume markerless client payloads.",
                "The next useful target is not another exact slot-call text search. It is Ghidra/dataflow around helper-slice dispatch, owner callbacks, or inline assembly paths that materialize the bitreader object before 00a5d9e0."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceSlot70CensusCallsite BuildCallsite(ExportedFunction function)
    {
        var calls = ExtractSlot70Calls(function.Lines);
        if (calls.Length == 0)
        {
            return new Tf2Ps3SourceSlot70CensusCallsite(function.Address, function.Name, "no-slot70", 0, [], [], "", "");
        }

        var evidence = BuildEvidence(function, calls);
        var classification = Classify(function, evidence);
        var score = Score(classification, evidence, function.Body);

        return new Tf2Ps3SourceSlot70CensusCallsite(
            function.Address,
            function.Name,
            classification,
            score,
            calls,
            evidence,
            MeaningForClassification(classification),
            Preview(function));
    }

    private static string Classify(ExportedFunction function, string[] evidence)
    {
        if (function.Address is "0070c300" or "00a5d0c0")
        {
            return "source-setup-or-association";
        }

        if (HasAny(evidence, "recvfrom-call", "connected-socket-read-call", "bitbuffer-init-helper")
            && HasAny(evidence, "source-object-field-token", "native-source-payload-dispatcher-call", "helper-slice-opd-token", "attached-reader-buffer-token"))
        {
            return "direct-markerless-ingress-candidate";
        }

        if (HasAny(evidence, "native-source-payload-dispatcher-call", "slot70-wrapper-direct-token", "helper-slice-opd-token", "helper-slice-table-token"))
        {
            return "source-dispatch-adjacent-candidate";
        }

        if (HasAny(evidence, "source-object-field-token", "attached-reader-field-token", "source-object-creator-call", "param2-field-shape-token"))
        {
            return "source-token-non-ingress";
        }

        if (HasAny(evidence, "hud-or-vgui-token", "particle-or-material-token", "datadesc-or-varmapping-token"))
        {
            return "known-non-source-interface";
        }

        return "generic-slot70-interface";
    }

    private static int Score(string classification, string[] evidence, string body)
    {
        var score = classification switch
        {
            "direct-markerless-ingress-candidate" => 500,
            "source-dispatch-adjacent-candidate" => 250,
            "source-setup-or-association" => 180,
            "source-token-non-ingress" => 80,
            "known-non-source-interface" => 15,
            _ => 0
        };

        score += evidence.Length * 5;
        if (body.Contains("local_", StringComparison.Ordinal) || body.Contains("auStack", StringComparison.Ordinal))
        {
            score += 5;
        }

        return score;
    }

    private static string[] BuildEvidence(ExportedFunction function, string[] calls)
    {
        var tokens = new List<string>();
        AddIf(function.Body, tokens, "recvfrom", "recvfrom-call");
        AddIf(function.Body, tokens, "_opd_FUN_008b82c0", "connected-socket-read-call");
        AddIf(function.Body, tokens, "FUN_0086de68", "bitbuffer-init-helper");
        AddIf(function.Body, tokens, "_opd_FUN_00a58c10", "native-source-payload-dispatcher-call");
        AddIf(function.Body, tokens, "_opd_FUN_00a5d9e0", "slot70-wrapper-direct-token");
        AddIf(function.Body, tokens, "0190e530", "helper-slice-opd-token");
        AddIf(function.Body, tokens, "0180ca30", "helper-slice-table-token");
        AddIf(function.Body, tokens, "PTR_PTR_01977d5c", "queued-payload-global-token");
        AddIf(function.Body, tokens, "PTR_DAT_01977dcc", "attached-reader-buffer-token");
        AddIf(function.Body, tokens, "FUN_0039f330", "source-object-creator-call");
        AddIf(function.Body, tokens, "_opd_FUN_00a5d0c0", "source-object-association-call");
        AddIf(function.Body, tokens, "_opd_FUN_00a5c2e8", "attached-reader-direct-token");
        AddIf(function.Body, tokens, "_opd_FUN_008b9468", "accepted-peer-attach-token");

        if (AnyContains(function.Body, "0x1de0", "0x1e0c", "0x1e18", "0x1e28", "0x782", "0x788", "0x42e", "0x430", "0x544"))
        {
            tokens.Add("source-object-field-token");
        }

        if (AnyContains(function.Body, "param_1[0x24]", "param_1[0x10c]", "param_1[0x10e]", "param_1[0x151]"))
        {
            tokens.Add("attached-reader-field-token");
        }

        if (AnyContains(function.Body, "param_2[0xb]", "param_2[0xc]", "param_2[0xd]", "param_2[0xe]", "param_2 + 7", "((int)param_2 + 7)"))
        {
            tokens.Add("param2-field-shape-token");
        }

        if (AnyContains(function.Body, "Hud", "CHud", "vgui", "CBaseViewport", "CBaseHudChat"))
        {
            tokens.Add("hud-or-vgui-token");
        }

        if (AnyContains(function.Body, "Dme", "Particle", "particle", "Material", "material", "FX_", "Debris", "fleck"))
        {
            tokens.Add("particle-or-material-token");
        }

        if (calls.Any(static call => call.Contains("0x30", StringComparison.Ordinal)
                || call.Contains("0x31", StringComparison.Ordinal)
                || call.Contains("0x32", StringComparison.Ordinal)
                || call.Contains("0x33", StringComparison.Ordinal)
                || call.Contains("local_", StringComparison.Ordinal)))
        {
            tokens.Add("datadesc-or-varmapping-token");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string MeaningForClassification(string classification) => classification switch
    {
        "direct-markerless-ingress-candidate" =>
            "This exact slot +0x70 callsite combines network/bitbuffer evidence with Source ownership evidence and must be checked in Ghidra.",
        "source-dispatch-adjacent-candidate" =>
            "Source dispatch/helper-slice tokens appear near the slot call, but markerless packet ingress is not proven.",
        "source-setup-or-association" =>
            "Known Source object setup/association callsite. Useful lifecycle evidence, not client payload ingress.",
        "source-token-non-ingress" =>
            "Source-like or bitfield tokens appear, but the function lacks network/bitbuffer evidence and is not a proven ingress path.",
        "known-non-source-interface" =>
            "The slot is used by unrelated UI, particle, material, datadesc, or other engine interfaces.",
        _ =>
            "Generic virtual slot +0x70 call with no useful Source ingress evidence."
    };

    private static string[] ExtractSlot70Calls(string[] lines)
    {
        var calls = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!Slot70CallRegex.IsMatch(line))
            {
                continue;
            }

            var combined = line;
            for (var j = i + 1; j < Math.Min(lines.Length, i + 6); j++)
            {
                if (combined.EndsWith(");", StringComparison.Ordinal)
                    || combined.EndsWith("))", StringComparison.Ordinal))
                {
                    break;
                }

                combined += " " + lines[j].Trim();
            }

            calls.Add(combined);
        }

        return calls
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasAny(string[] values, params string[] needles) =>
        needles.Any(needle => values.Contains(needle, StringComparer.Ordinal));

    private static bool AnyContains(string body, params string[] needles) =>
        needles.Any(needle => body.Contains(needle, StringComparison.Ordinal));

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
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

    private static string Preview(ExportedFunction function)
    {
        var text = string.Join('\n', function.Lines.Take(80));
        return text.Length <= 2600 ? text : text[..2600];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceSlot70CallsiteCensusReport(
    string Status,
    string Note,
    Tf2Ps3SourceSlot70CallsiteCensusInputs Inputs,
    Tf2Ps3SourceSlot70CallsiteCensusSummary Summary,
    Tf2Ps3SourceSlot70ClassCount[] ClassificationCounts,
    Tf2Ps3SourceSlot70CensusCallsite[] RankedCallsites,
    Tf2Ps3SourceSlot70CallsiteCensusGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceSlot70CallsiteCensusInputs(
    string CExportInput);

public sealed record Tf2Ps3SourceSlot70CallsiteCensusSummary(
    int ScannedFunctionCount,
    int ExactSlot70CallsiteFunctionCount,
    int NetworkOrSocketSlot70CallsiteCount,
    int PayloadDispatcherSlot70CallsiteCount,
    int HelperSliceTokenSlot70CallsiteCount,
    int SourceSetupOrAssociationCallsiteCount,
    int SourceTokenNonIngressCallsiteCount,
    int Param2FieldShapeSlot70CallsiteCount,
    int DirectMarkerlessIngressCandidateCount,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceSlot70ClassCount(
    string Classification,
    int Count);

public sealed record Tf2Ps3SourceSlot70CensusCallsite(
    string Address,
    string Name,
    string Classification,
    int Score,
    string[] Slot70CallExpressions,
    string[] EvidenceTokens,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceSlot70CallsiteCensusGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
