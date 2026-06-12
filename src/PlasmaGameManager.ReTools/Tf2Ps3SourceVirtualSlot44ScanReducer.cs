using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceVirtualSlot44ScanReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex Slot44VirtualCallRegex = new(
        @"\(\*\(code \*\).*?(?:\+ 0x44(?![0-9a-f])|\+0x44(?![0-9a-f]))",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var callsites = functions
            .Select(BuildCallsite)
            .Where(static callsite => callsite.Slot44CallExpressions.Length > 0)
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();

        var candidateCallsites = callsites
            .Where(static callsite => callsite.Role is "source-object-handler-install-candidate" or "source-owner-callback-candidate")
            .ToArray();

        var report = new Tf2Ps3SourceVirtualSlot44ScanReport(
            "tf2ps3-source-virtual-slot44-scan",
            "Whole-export scan for actual indirect virtual calls through vtable slot +0x44. This separates common object-field offset 0x44 noise from callsites that might invoke the Source object registered-handler installer at function-table slot +0x44.",
            cExportPath,
            new Tf2Ps3SourceVirtualSlot44ScanSummary(
                callsites.Length,
                callsites.Count(static callsite => callsite.Role == "source-helper-text-command-negative"),
                callsites.Count(static callsite => callsite.Role == "ui-or-engine-negative"),
                callsites.Count(static callsite => callsite.Role == "source-object-handler-install-candidate"),
                callsites.Count(static callsite => callsite.Role == "source-owner-callback-candidate"),
                callsites.Count(static callsite => callsite.Role == "unclassified-virtual-slot44"),
                callsites.Sum(static callsite => callsite.Slot44CallExpressions.Length),
                candidateCallsites.Length),
            callsites,
            BuildFindings(callsites, candidateCallsites));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceVirtualSlot44Callsite BuildCallsite(ExportedFunction function)
    {
        var slot44Calls = ExtractSlot44Calls(function.Lines);
        var evidence = BuildEvidence(function.Body, slot44Calls);
        return new Tf2Ps3SourceVirtualSlot44Callsite(
            function.Address,
            function.Name,
            Classify(function, evidence),
            slot44Calls,
            evidence,
            Preview(function.Lines));
    }

    private static string Classify(ExportedFunction function, string[] evidence)
    {
        if (function.Address == "00a516a8" && evidence.Contains("text-token-parser", StringComparer.Ordinal))
        {
            return "source-helper-text-command-negative";
        }

        if (HasAny(evidence, "handler-vector-storage", "registered-handler-install-helper", "source-object-create-wrapper", "source-owner-callback-association"))
        {
            return "source-object-handler-install-candidate";
        }

        if (HasAny(evidence, "source-owner-bind-expression", "source-object-owner-callback-field"))
        {
            return "source-owner-callback-candidate";
        }

        if (HasAny(evidence, "hud-ui-token", "resource-ui-token", "client-effect-token", "text-token-parser", "input-token", "sound-token", "entity-datatable-token"))
        {
            return "ui-or-engine-negative";
        }

        return "unclassified-virtual-slot44";
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceVirtualSlot44Callsite> callsites,
        IReadOnlyCollection<Tf2Ps3SourceVirtualSlot44Callsite> candidateCallsites)
    {
        var findings = new List<string>
        {
            "This scan only includes true indirect virtual calls through slot +0x44; plain reads/writes of object field offset 0x44 are intentionally excluded.",
            "00a516a8 remains negative evidence: it is a Source-helper-region +0x44 virtual call, but surrounding token parsing routes it to text/command handling rather than registered payload-handler installation.",
            "A runtime Source/map-load implementation should only be changed if this report contains a source-object-handler-install-candidate whose receiver is proven to be the 0x1e28 Source object created through 008b9c38/0039f330."
        };

        if (candidateCallsites.Count == 0)
        {
            findings.Add("No whole-export +0x44 virtual call currently proves the concrete handler-registration caller for 00a5df70.");
        }
        else
        {
            findings.Add("Candidate +0x44 virtual callsites exist, but each still needs receiver-object proof before it can justify runtime packet behavior.");
        }

        if (callsites.Any(static callsite => callsite.Role == "unclassified-virtual-slot44"))
        {
            findings.Add("Unclassified +0x44 callsites remain. These are not Source-handler proof; they are queued for targeted Ghidra/xref review.");
        }

        return findings.ToArray();
    }

    private static string[] BuildEvidence(string body, string[] slot44Calls)
    {
        var tokens = new List<string> { "virtual-slot-0x44" };
        AddIf(body, tokens, "_opd_FUN_00a625e8", "handler-vector-append");
        AddIf(body, tokens, "_opd_FUN_00a5df70", "registered-handler-install-helper");
        AddIf(body, tokens, "_opd_FUN_00a57f48", "handler-id-lookup");
        AddIf(body, tokens, "0x1e0c", "handler-vector-storage");
        AddIf(body, tokens, "0x1e18", "handler-vector-count-storage");
        AddIf(body, tokens, "FUN_0039f330", "source-object-create-wrapper");
        AddIf(body, tokens, "_opd_FUN_008b9c38", "source-object-create-or-reuse");
        AddIf(body, tokens, "_opd_FUN_00a5d0c0", "source-owner-callback-association");
        AddIf(body, tokens, "param_1[0x782]", "source-object-owner-callback-field");
        if (body.Contains("FUN_0039f330", StringComparison.Ordinal)
            && (body.Contains("ZEXT48(piVar4 + 1)", StringComparison.Ordinal)
                || body.Contains("piVar20 = piVar5 + 1", StringComparison.Ordinal)))
        {
            tokens.Add("source-owner-bind-expression");
        }
        AddIf(body, tokens, "*piVar4 + 0x2c", "source-owner-slot-call");
        AddIf(body, tokens, "*piVar5 + 0x2c", "source-owner-slot-call");
        AddIf(body, tokens, "*puVar8 + 0x20", "source-object-ready-or-cleanup-slot");
        AddIf(body, tokens, "*puVar8 + 0xc", "source-object-ready-or-cleanup-slot");
        AddIf(body, tokens, "FUN_0086e438", "text-token-parser");
        AddIf(body, tokens, "FUN_0086d188", "text-token-parser");
        AddIf(body, tokens, "_opd_FUN_009f8858", "text-token-parser");
        AddIf(body, tokens, "CHud", "hud-ui-token");
        AddIf(body, tokens, "resource/UI", "resource-ui-token");
        AddIf(body, tokens, "ClientEffect", "client-effect-token");
        AddIf(body, tokens, "DT_", "entity-datatable-token");
        AddIf(body, tokens, "m_i", "entity-datatable-token");
        AddIf(body, tokens, "snd_", "sound-token");
        AddIf(body, tokens, "input", "input-token");

        foreach (var call in slot44Calls)
        {
            if (call.Contains("**(int **)(uVar", StringComparison.Ordinal)
                || call.Contains("**(int **)(param_", StringComparison.Ordinal)
                || call.Contains("*local_", StringComparison.Ordinal)
                || call.Contains("*piVar", StringComparison.Ordinal))
            {
                tokens.Add("generic-indirect-receiver");
            }
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasAny(string[] evidence, params string[] tokens)
    {
        return tokens.Any(token => evidence.Contains(token, StringComparer.Ordinal));
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string[] ExtractSlot44Calls(string[] lines)
    {
        var calls = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!Slot44VirtualCallRegex.IsMatch(line))
            {
                continue;
            }

            if (line.EndsWith("))", StringComparison.Ordinal) || line.EndsWith(");", StringComparison.Ordinal))
            {
                calls.Add(line);
                continue;
            }

            var combined = line;
            for (var j = i + 1; j < Math.Min(lines.Length, i + 4); j++)
            {
                combined += " " + lines[j].Trim();
                if (combined.EndsWith(");", StringComparison.Ordinal) || combined.EndsWith("))", StringComparison.Ordinal))
                {
                    break;
                }
            }

            calls.Add(combined);
        }

        return calls
            .Distinct(StringComparer.Ordinal)
            .ToArray();
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

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(80));
        return text.Length <= 2200 ? text : text[..2200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceVirtualSlot44ScanReport(
    string Status,
    string Note,
    string CExportInput,
    Tf2Ps3SourceVirtualSlot44ScanSummary Summary,
    Tf2Ps3SourceVirtualSlot44Callsite[] Callsites,
    string[] Findings);

public sealed record Tf2Ps3SourceVirtualSlot44ScanSummary(
    int VirtualSlot44CallsiteCount,
    int SourceHelperTextCommandNegativeCount,
    int UiOrEngineNegativeCount,
    int SourceObjectHandlerInstallCandidateCount,
    int SourceOwnerCallbackCandidateCount,
    int UnclassifiedVirtualSlot44Count,
    int Slot44CallExpressionCount,
    int CandidateCallsiteCount);

public sealed record Tf2Ps3SourceVirtualSlot44Callsite(
    string Address,
    string Name,
    string Role,
    string[] Slot44CallExpressions,
    string[] EvidenceTokens,
    string Preview);
