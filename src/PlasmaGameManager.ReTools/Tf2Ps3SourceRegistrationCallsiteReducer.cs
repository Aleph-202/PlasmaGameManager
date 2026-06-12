using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRegistrationCallsiteReducer
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

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var callsites = functions
            .Where(IsRelevant)
            .Select(BuildCallsite)
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();
        var virtualSlot44Callsites = callsites
            .Where(static callsite => callsite.Slot44CallExpressions.Length > 0)
            .ToArray();

        var report = new Tf2Ps3SourceRegistrationCallsiteReport(
            "tf2ps3-source-registration-callsite-map",
            "Scans the TF.elf C export for Source helper-region functions that either own the handler table or make indirect virtual calls through slot +0x44. This is a constructor/setup search aid; it does not treat every +0x44 call as handler registration.",
            cExportPath,
            new Tf2Ps3SourceRegistrationCallsiteSummary(
                callsites.Length,
                callsites.Count(static callsite => callsite.Role == "registration-helper-slot-target"),
                virtualSlot44Callsites.Length,
                callsites.Count(static callsite => callsite.EvidenceTokens.Contains("handler-table-storage", StringComparer.Ordinal)),
                callsites.Count(static callsite => callsite.EvidenceTokens.Contains("payload-dispatcher", StringComparer.Ordinal))),
            callsites,
            BuildFindings(callsites, virtualSlot44Callsites));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static bool IsRelevant(ExportedFunction function)
    {
        if (!IsSourceHelperRegion(function.Address))
        {
            return false;
        }

        return function.Address == "00a5df70"
            || function.Body.Contains("_opd_FUN_00a625e8", StringComparison.Ordinal)
            || function.Body.Contains("0x1e0c", StringComparison.Ordinal)
            || function.Body.Contains("0x1e18", StringComparison.Ordinal)
            || ContainsSlot44VirtualCall(function.Body);
    }

    private static Tf2Ps3SourceRegistrationCallsite BuildCallsite(ExportedFunction function)
    {
        var slot44Calls = ExtractSlot44Calls(function.Lines);
        var evidence = BuildEvidence(function.Body, slot44Calls);
        return new Tf2Ps3SourceRegistrationCallsite(
            function.Address,
            function.Name,
            Classify(function, slot44Calls, evidence),
            slot44Calls,
            evidence,
            Preview(function.Lines));
    }

    private static string Classify(ExportedFunction function, string[] slot44Calls, string[] evidence)
    {
        if (function.Address == "00a5df70")
        {
            return "registration-helper-slot-target";
        }

        if (function.Body.Contains("_opd_FUN_00a625e8", StringComparison.Ordinal))
        {
            return "direct-registration-helper-body";
        }

        if (evidence.Contains("handler-table-storage", StringComparer.Ordinal))
        {
            return "handler-table-owner-helper";
        }

        if (slot44Calls.Length > 0 && evidence.Contains("text-token-parser", StringComparer.Ordinal))
        {
            return "text-command-virtual-slot-0x44-call";
        }

        if (slot44Calls.Length > 0)
        {
            return "source-region-virtual-slot-0x44-call";
        }

        return "source-region-context";
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceRegistrationCallsite> callsites,
        IReadOnlyCollection<Tf2Ps3SourceRegistrationCallsite> virtualSlot44Callsites)
    {
        var findings = new List<string>
        {
            "00a5df70 remains the only direct body that appends to object +0x1e0c/+0x1e18 through 00a625e8.",
            "Indirect +0x44 calls in the 00a5xxxx helper region should be treated as callsite candidates only; the receiver object still has to be proven to use the function-table slice containing 00a5df70.",
            "The current search aid narrows constructor/setup recovery to Source-region callsites instead of all engine-wide +0x44 virtual calls."
        };

        if (virtualSlot44Callsites.Count == 0)
        {
            findings.Add("No indirect +0x44 callsite was found in the 00a5xxxx Source helper region.");
        }
        else if (virtualSlot44Callsites.All(static callsite => callsite.Role == "text-command-virtual-slot-0x44-call"))
        {
            findings.Add("The only true +0x44 virtual call currently found in this region is 00a516a8, and its surrounding token/string parsing path makes it negative evidence for registered Source payload-handler construction.");
        }

        if (callsites.Any(static callsite => callsite.EvidenceTokens.Contains("payload-dispatcher", StringComparer.Ordinal)))
        {
            findings.Add("Some callsites are adjacent to the payload dispatcher/attached stream reader path, so these are the next decompile targets for handler construction evidence.");
        }

        return findings.ToArray();
    }

    private static string[] BuildEvidence(string body, string[] slot44Calls)
    {
        var tokens = new List<string>();
        AddIf(body, tokens, "_opd_FUN_00a625e8", "handler-vector-append");
        AddIf(body, tokens, "_opd_FUN_00a57f48", "handler-id-lookup");
        AddIf(body, tokens, "_opd_FUN_00a58c10", "payload-dispatcher");
        AddIf(body, tokens, "_opd_FUN_00a5c2e8", "attached-stream-reader");
        AddIf(body, tokens, "_opd_FUN_00a584d0", "attached-state-writer");
        AddIf(body, tokens, "_opd_FUN_00a5a550", "token-reset");
        AddIf(body, tokens, "_opd_FUN_009f8858", "text-command-base-check");
        AddIf(body, tokens, "FUN_0086e438", "text-token-parser");
        AddIf(body, tokens, "FUN_0086d188", "text-token-compare");
        AddIf(body, tokens, "_opd_FUN_008bc978", "native-send-builder");
        AddIf(body, tokens, "_opd_FUN_008b82c0", "socket-recv-wrapper");
        AddIf(body, tokens, "0x1e0c", "handler-table-storage");
        AddIf(body, tokens, "0x1e18", "handler-count-storage");
        AddIf(body, tokens, "0x544", "staged-payload-buffer");
        AddIf(body, tokens, "0x42e", "attached-state-byte");
        AddIf(body, tokens, " + 0x44", "offset-0x44-reference");
        AddIf(body, tokens, "+0x44", "offset-0x44-reference");
        if (slot44Calls.Length > 0)
        {
            tokens.Add("virtual-slot-0x44");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
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
        return lines
            .Where(static line => line.Contains("(*(code *)", StringComparison.Ordinal)
                && (line.Contains("+ 0x44", StringComparison.Ordinal) || line.Contains("+0x44", StringComparison.Ordinal)))
            .Select(static line => line.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ContainsSlot44VirtualCall(string body)
    {
        return body.Contains("(*(code *)", StringComparison.Ordinal)
            && (body.Contains("+ 0x44", StringComparison.Ordinal) || body.Contains("+0x44", StringComparison.Ordinal));
    }

    private static bool IsSourceHelperRegion(string address)
    {
        var value = Convert.ToUInt32(address, 16);
        return value is >= 0x00a50000 and < 0x00a65000;
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

public sealed record Tf2Ps3SourceRegistrationCallsiteReport(
    string Status,
    string Note,
    string CExportInput,
    Tf2Ps3SourceRegistrationCallsiteSummary Summary,
    Tf2Ps3SourceRegistrationCallsite[] Callsites,
    string[] Findings);

public sealed record Tf2Ps3SourceRegistrationCallsiteSummary(
    int CallsiteCount,
    int DirectRegistrationHelperCount,
    int SourceRegionVirtualSlot44CallCount,
    int HandlerTableStorageFunctionCount,
    int PayloadDispatcherAdjacentFunctionCount);

public sealed record Tf2Ps3SourceRegistrationCallsite(
    string Address,
    string Name,
    string Role,
    string[] Slot44CallExpressions,
    string[] EvidenceTokens,
    string Preview);
