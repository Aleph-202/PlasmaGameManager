using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceTemplatePatchLayoutReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceTemplatePatchLayoutReport> ReduceAsync(
        string responderSourcePath,
        string nativeTemplateDebtPath,
        string outputPath)
    {
        var source = await File.ReadAllTextAsync(responderSourcePath);
        using var debt = JsonDocument.Parse(await File.ReadAllTextAsync(nativeTemplateDebtPath));

        var templates = debt.RootElement.GetProperty("StaticHexTemplates")
            .EnumerateArray()
            .Select(static item => new Tf2Ps3SourceTemplatePatchStaticTemplate(
                item.GetProperty("Name").GetString() ?? "",
                item.GetProperty("Line").GetInt32(),
                item.GetProperty("ByteLength").GetInt32(),
                item.GetProperty("ReplacementTarget").GetString() ?? "unknown"))
            .Where(static item => item.Name.Length > 0)
            .ToArray();

        var templateBytes = ExtractStaticTemplateBytes(source);
        var methods = ExtractMethods(source).ToArray();
        var addPacketSites = ExtractAddPacketSites(source, methods).ToArray();
        var prefixRoutes = ExtractFrozenStatePrefixRoutes(source).ToArray();
        var generatedQueuedPrefixBodies = ExtractGeneratedQueuedPrefixBodies(source, methods).ToArray();

        var layouts = templates
            .Select(template => BuildTemplateLayout(source, template, templateBytes, methods, addPacketSites, prefixRoutes))
            .ToArray();

        var report = new Tf2Ps3SourceTemplatePatchLayoutReport(
            "tf2ps3-source-template-patch-layout",
            "Maps each static PS3 Source responder template to the runtime patch logic that still needs native TF.elf/server.dll field replacement.",
            new Tf2Ps3SourceTemplatePatchLayoutInputs(
                responderSourcePath,
                nativeTemplateDebtPath),
            new Tf2Ps3SourceTemplatePatchLayoutSummary(
                templates.Length,
                layouts.Count(static layout => layout.BuildMethods.Length > 0),
                layouts.Count(static layout => layout.TailPatches.Length > 0),
                layouts.Sum(static layout => layout.TailPatches.Sum(static patch => patch.TailRecordCount)),
                layouts.Sum(static layout => layout.EmbeddedStateLinkRecords.Length),
                generatedQueuedPrefixBodies.Length,
                generatedQueuedPrefixBodies.Sum(static body => body.PrefixByteLength),
                generatedQueuedPrefixBodies.Sum(static body => body.RecordCount),
                prefixRoutes.Length,
                addPacketSites.Length,
                layouts.Count(static layout => layout.DirectSendSites.Length > 0)),
            layouts,
            generatedQueuedPrefixBodies,
            prefixRoutes,
            [
                "PlayerStateLink tail patches are named dynamic fields, not native completion. The static prefix/template bytes ahead of them still need object-stream or queued-peer message builders.",
                "Generated queued PlayerStateLink bodies are progress away from captured templates, but their prefix bytes remain native-debt until produced by the TF.elf queued-peer field writer.",
                "FrozenState prefix routes identify the retail prefix shapes used for post-roster object batches; non-routed shapes still use deterministic filler and remain native-debt.",
                "A template is ready to remove only after its BuildMethods and DirectSendSites are backed by TF.elf/server.dll field constructors instead of Convert.FromHexString bodies."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceTemplatePatchLayout BuildTemplateLayout(
        string source,
        Tf2Ps3SourceTemplatePatchStaticTemplate template,
        IReadOnlyDictionary<string, byte[]> templateBytes,
        Tf2Ps3SourceResponderMethod[] methods,
        Tf2Ps3SourceTemplateDirectSendSite[] addPacketSites,
        Tf2Ps3FrozenStatePrefixRoute[] prefixRoutes)
    {
        var methodsUsingTemplate = methods
            .Where(method => ContainsIdentifier(method.Body, template.Name))
            .ToArray();
        var tailPatches = methodsUsingTemplate
            .SelectMany(method => ExtractTailPatches(source, method))
            .ToArray();
        var directSendSites = addPacketSites
            .Where(site => string.Equals(site.BodyExpression, template.Name, StringComparison.Ordinal))
            .ToArray();
        var routedPrefixes = prefixRoutes
            .Where(route => string.Equals(route.TemplateName, template.Name, StringComparison.Ordinal))
            .ToArray();
        var embeddedStateLinks = templateBytes.TryGetValue(template.Name, out var bytes)
            ? ExtractEmbeddedStateLinkRecords(bytes).ToArray()
            : [];

        return new Tf2Ps3SourceTemplatePatchLayout(
            template.Name,
            template.StaticLine,
            template.ByteLength,
            template.ReplacementTarget,
            methodsUsingTemplate.Select(static method => method.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            BuildKindsForTemplate(template.Name, methodsUsingTemplate, routedPrefixes),
            tailPatches,
            embeddedStateLinks,
            directSendSites,
            routedPrefixes,
            NotesForTemplate(template, methodsUsingTemplate, tailPatches, embeddedStateLinks, directSendSites, routedPrefixes));
    }

    private static string[] BuildKindsForTemplate(
        string templateName,
        Tf2Ps3SourceResponderMethod[] methods,
        Tf2Ps3FrozenStatePrefixRoute[] routedPrefixes)
    {
        var kinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var method in methods)
        {
            if (method.Body.Contains($"(byte[]){templateName}.Clone()", StringComparison.Ordinal))
            {
                kinds.Add("clone-static-template");
            }

            if (Regex.IsMatch(method.Body, $@"new\s+byte\s*\[\s*{Regex.Escape(templateName)}\.Length\s*\+\s*\d+\s*\]"))
            {
                kinds.Add("prefix-plus-dynamic-tail");
            }

            if (method.Body.Contains($"{templateName}.CopyTo(body, 0)", StringComparison.Ordinal))
            {
                kinds.Add("copy-static-prefix");
            }

            if (method.Body.Contains("AddPacket(", StringComparison.Ordinal))
            {
                kinds.Add("direct-send-template-body");
            }
        }

        if (routedPrefixes.Length > 0)
        {
            kinds.Add("frozen-state-prefix-route");
        }

        if (kinds.Count == 0)
        {
            kinds.Add("unpatched-static-body");
        }

        return kinds.ToArray();
    }

    private static string[] NotesForTemplate(
        Tf2Ps3SourceTemplatePatchStaticTemplate template,
        Tf2Ps3SourceResponderMethod[] methods,
        Tf2Ps3SourceTemplateTailPatch[] tailPatches,
        Tf2Ps3EmbeddedStateLinkRecord[] embeddedStateLinks,
        Tf2Ps3SourceTemplateDirectSendSite[] directSendSites,
        Tf2Ps3FrozenStatePrefixRoute[] routedPrefixes)
    {
        var notes = new List<string>();
        if (tailPatches.Length > 0)
        {
            notes.Add($"runtime patches {tailPatches.Sum(static patch => patch.TailRecordCount)} PlayerStateLink record(s) into the static byte body");
        }

        if (embeddedStateLinks.Length > 0)
        {
            notes.Add($"static template already embeds {embeddedStateLinks.Length} PNG state-link record(s) that need a named builder");
        }

        if (routedPrefixes.Length > 0)
        {
            notes.Add("used as a retail FrozenStateObject prefix selected by length/prefix-length tuple");
        }

        if (directSendSites.Length > 0)
        {
            notes.Add("sent directly through AddPacket without named field construction");
        }

        if (methods.Length == 0 && directSendSites.Length == 0 && routedPrefixes.Length == 0)
        {
            notes.Add("template is currently only declared; no runtime use was found by the static reducer");
        }

        notes.Add($"{template.ReplacementTarget} replacement must remove the Convert.FromHexString source body");
        return notes.ToArray();
    }

    private static IReadOnlyDictionary<string, byte[]> ExtractStaticTemplateBytes(string source)
    {
        return Regex.Matches(
                source,
                @"private\s+static\s+readonly\s+byte\[\]\s+(?<name>\w+)\s*=\s*Convert\.FromHexString\(\s*""(?<hex>[0-9a-fA-F]+)""\s*\)",
                RegexOptions.Multiline)
            .Cast<Match>()
            .ToDictionary(
                static match => match.Groups["name"].Value,
                static match => Convert.FromHexString(match.Groups["hex"].Value),
                StringComparer.Ordinal);
    }

    private static IEnumerable<Tf2Ps3EmbeddedStateLinkRecord> ExtractEmbeddedStateLinkRecords(byte[] bytes)
    {
        for (var offset = 0; offset <= bytes.Length - 12; offset++)
        {
            if (bytes[offset] != 0x50
                || bytes[offset + 1] != 0x4e
                || bytes[offset + 2] != 0x47
                || bytes[offset + 3] != 0x01)
            {
                continue;
            }

            yield return new Tf2Ps3EmbeddedStateLinkRecord(
                offset,
                "PNG\\x01",
                ReadUInt32BigEndian(bytes, offset + 4),
                ReadUInt32BigEndian(bytes, offset + 8));
        }
    }

    private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24)
            | ((uint)bytes[offset + 1] << 16)
            | ((uint)bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

    private static IEnumerable<Tf2Ps3SourceTemplateTailPatch> ExtractTailPatches(
        string source,
        Tf2Ps3SourceResponderMethod method)
    {
        var searchOffset = 0;
        while (true)
        {
            var index = method.Body.IndexOf("WritePlayerStateLinkTail(", searchOffset, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var absoluteIndex = method.BodyStart + index;
            var openIndex = source.IndexOf('(', absoluteIndex);
            var closeIndex = FindMatching(source, openIndex, '(', ')');
            if (closeIndex < 0)
            {
                yield break;
            }

            var argumentText = source[(openIndex + 1)..closeIndex];
            var args = SplitTopLevelArguments(argumentText);
            if (args.Length >= 3)
            {
                var objectIds = ParseCollectionExpression(args[1]);
                yield return new Tf2Ps3SourceTemplateTailPatch(
                    method.Name,
                    LineNumber(source, absoluteIndex),
                    objectIds,
                    NormalizeExpression(args[2]),
                    objectIds.Length,
                    checked(objectIds.Length * 12));
            }

            searchOffset = index + "WritePlayerStateLinkTail(".Length;
        }
    }

    private static IEnumerable<Tf2Ps3SourceGeneratedQueuedPrefixBody> ExtractGeneratedQueuedPrefixBodies(
        string source,
        Tf2Ps3SourceResponderMethod[] methods)
    {
        var searchOffset = 0;
        while (true)
        {
            var index = source.IndexOf("BuildQueuedPlayerStateLinkBody(", searchOffset, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var preceding = source[Math.Max(0, index - 64)..index];
            if (preceding.Contains("static byte[]", StringComparison.Ordinal))
            {
                searchOffset = index + "BuildQueuedPlayerStateLinkBody(".Length;
                continue;
            }

            var openIndex = source.IndexOf('(', index);
            var closeIndex = FindMatching(source, openIndex, '(', ')');
            if (closeIndex < 0)
            {
                yield break;
            }

            var args = SplitTopLevelArguments(source[(openIndex + 1)..closeIndex]);
            var method = methods.LastOrDefault(candidate => candidate.Start <= index && candidate.End >= closeIndex);
            var objectIds = ParseCollectionExpression(Argument(args, "objectIds", 5));
            var length = ParseIntExpression(Argument(args, "length", 3));
            var prefixLength = ParseIntExpression(Argument(args, "prefixLength", 4));
            var family = ExtractStringLiteral(Argument(args, "family", 7));
            yield return new Tf2Ps3SourceGeneratedQueuedPrefixBody(
                family.Length == 0 ? method?.Name ?? "unknown" : family,
                method?.Name ?? "unknown",
                LineNumber(source, index),
                length,
                prefixLength,
                objectIds,
                NormalizeExpression(Argument(args, "linkedObjectId", 6)),
                objectIds.Length,
                checked(objectIds.Length * 12),
                "generated-queued-prefix");

            searchOffset = closeIndex + 1;
        }
    }

    private static IEnumerable<Tf2Ps3FrozenStatePrefixRoute> ExtractFrozenStatePrefixRoutes(string source)
    {
        foreach (Match match in Regex.Matches(
                     source,
                     @"\((?<length>\d+),\s*(?<prefix>\d+)\)\s*=>\s*(?<template>\w+)"))
        {
            yield return new Tf2Ps3FrozenStatePrefixRoute(
                int.Parse(match.Groups["length"].Value),
                int.Parse(match.Groups["prefix"].Value),
                match.Groups["template"].Value,
                LineNumber(source, match.Index));
        }
    }

    private static IEnumerable<Tf2Ps3SourceTemplateDirectSendSite> ExtractAddPacketSites(
        string source,
        Tf2Ps3SourceResponderMethod[] methods)
    {
        var searchOffset = 0;
        while (true)
        {
            var index = source.IndexOf("AddPacket(", searchOffset, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var openIndex = source.IndexOf('(', index);
            var closeIndex = FindMatching(source, openIndex, '(', ')');
            if (closeIndex < 0)
            {
                yield break;
            }

            var args = SplitTopLevelArguments(source[(openIndex + 1)..closeIndex]);
            if (args.Length >= 4)
            {
                var method = methods.LastOrDefault(candidate => candidate.Start <= index && candidate.End >= closeIndex);
                yield return new Tf2Ps3SourceTemplateDirectSendSite(
                    method?.Name ?? "unknown",
                    NormalizeExpression(args[2]),
                    ExtractStringLiteral(args[3]),
                    ExtractSequenceAdvance(args),
                    LineNumber(source, index));
            }

            searchOffset = closeIndex + 1;
        }
    }

    private static Tf2Ps3SourceResponderMethod[] ExtractMethods(string source)
    {
        var methods = new List<Tf2Ps3SourceResponderMethod>();
        foreach (Match match in Regex.Matches(
                     source,
                     @"(?m)^\s*(?:private|public|internal)\s+(?!static\s+readonly\b)(?:static\s+)?(?!readonly\b)(?:[\w<>,\[\]\?]+\s+)+(?<name>\w+)\s*\("))
        {
            var openBrace = source.IndexOf('{', match.Index);
            if (openBrace < 0)
            {
                continue;
            }

            var closeBrace = FindMatching(source, openBrace, '{', '}');
            if (closeBrace < 0)
            {
                continue;
            }

            methods.Add(new Tf2Ps3SourceResponderMethod(
                match.Groups["name"].Value,
                match.Index,
                closeBrace,
                openBrace + 1,
                source[(openBrace + 1)..closeBrace]));
        }

        return methods.ToArray();
    }

    private static string[] ParseCollectionExpression(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1];
        }

        if (trimmed.Length == 0)
        {
            return [];
        }

        return SplitTopLevelArguments(trimmed)
            .Select(NormalizeExpression)
            .Where(static item => item.Length > 0)
            .ToArray();
    }

    private static string Argument(string[] args, string name, int positionalIndex)
    {
        var prefix = name + ":";
        foreach (var arg in args)
        {
            if (arg.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
            {
                return arg[(arg.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
            }
        }

        return positionalIndex >= 0 && positionalIndex < args.Length
            ? args[positionalIndex]
            : "";
    }

    private static int ParseIntExpression(string expression)
    {
        var trimmed = NormalizeExpression(expression);
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(trimmed[2..].TrimEnd('u', 'U'), System.Globalization.NumberStyles.HexNumber, null, out var hex))
        {
            return hex;
        }

        return int.TryParse(trimmed, out var value) ? value : 0;
    }

    private static string[] SplitTopLevelArguments(string text)
    {
        var args = new List<string>();
        var start = 0;
        var parens = 0;
        var brackets = 0;
        var braces = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '(':
                    parens++;
                    break;
                case ')':
                    parens--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '{':
                    braces++;
                    break;
                case '}':
                    braces--;
                    break;
                case ',' when parens == 0 && brackets == 0 && braces == 0:
                    args.Add(text[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        args.Add(text[start..].Trim());
        return args.ToArray();
    }

    private static int FindMatching(string source, int openIndex, char open, char close)
    {
        if ((uint)openIndex >= (uint)source.Length || source[openIndex] != open)
        {
            return -1;
        }

        var depth = 0;
        var inString = false;
        for (var i = openIndex; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"' && (i == 0 || source[i - 1] != '\\'))
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string NormalizeExpression(string expression)
    {
        return Regex.Replace(expression.Trim(), @"\s+", " ");
    }

    private static string ExtractStringLiteral(string expression)
    {
        var match = Regex.Match(expression, "^\\s*\"(?<value>(?:\\\\.|[^\"])*)\"\\s*$");
        return match.Success
            ? Regex.Unescape(match.Groups["value"].Value)
            : NormalizeExpression(expression);
    }

    private static int? ExtractSequenceAdvance(string[] args)
    {
        foreach (var arg in args)
        {
            var match = Regex.Match(arg, @"sequenceAdvance\s*:\s*(?<value>\d+)");
            if (match.Success)
            {
                return int.Parse(match.Groups["value"].Value);
            }
        }

        return null;
    }

    private static bool ContainsIdentifier(string text, string identifier)
    {
        return Regex.IsMatch(text, $@"\b{Regex.Escape(identifier)}\b");
    }

    private static int LineNumber(string source, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset && i < source.Length; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}

public sealed record Tf2Ps3SourceTemplatePatchLayoutReport(
    string Status,
    string Note,
    Tf2Ps3SourceTemplatePatchLayoutInputs Inputs,
    Tf2Ps3SourceTemplatePatchLayoutSummary Summary,
    Tf2Ps3SourceTemplatePatchLayout[] TemplateLayouts,
    Tf2Ps3SourceGeneratedQueuedPrefixBody[] GeneratedQueuedPrefixBodies,
    Tf2Ps3FrozenStatePrefixRoute[] FrozenStatePrefixRoutes,
    string[] AcceptanceCriteria);

public sealed record Tf2Ps3SourceTemplatePatchLayoutInputs(
    string ResponderSource,
    string NativeTemplateDebt);

public sealed record Tf2Ps3SourceTemplatePatchLayoutSummary(
    int StaticTemplateCount,
    int TemplatesWithPatchMethodCount,
    int TemplatesWithPlayerStateLinkTailCount,
    int PlayerStateLinkTailRecordCount,
    int EmbeddedStateLinkRecordCount,
    int GeneratedQueuedPrefixBodyCount,
    int GeneratedQueuedPrefixBytes,
    int GeneratedQueuedPrefixRecordCount,
    int FrozenStatePrefixRouteCount,
    int AddPacketSiteCount,
    int TemplatesSentDirectlyCount);

public sealed record Tf2Ps3SourceTemplatePatchLayout(
    string Name,
    int StaticLine,
    int ByteLength,
    string ReplacementTarget,
    string[] BuildMethods,
    string[] BuildKinds,
    Tf2Ps3SourceTemplateTailPatch[] TailPatches,
    Tf2Ps3EmbeddedStateLinkRecord[] EmbeddedStateLinkRecords,
    Tf2Ps3SourceTemplateDirectSendSite[] DirectSendSites,
    Tf2Ps3FrozenStatePrefixRoute[] FrozenStatePrefixRoutes,
    string[] Notes);

public sealed record Tf2Ps3SourceTemplateTailPatch(
    string Method,
    int Line,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression,
    int TailRecordCount,
    int TailByteLength);

public sealed record Tf2Ps3SourceTemplateDirectSendSite(
    string Method,
    string BodyExpression,
    string Explanation,
    int? SequenceAdvance,
    int Line);

public sealed record Tf2Ps3EmbeddedStateLinkRecord(
    int Offset,
    string Marker,
    uint ObjectId,
    uint LinkedObjectId);

public sealed record Tf2Ps3SourceGeneratedQueuedPrefixBody(
    string Family,
    string Method,
    int Line,
    int BodyLength,
    int PrefixByteLength,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression,
    int RecordCount,
    int TailByteLength,
    string PrefixKind);

public sealed record Tf2Ps3FrozenStatePrefixRoute(
    int Length,
    int PrefixLength,
    string TemplateName,
    int Line);

internal sealed record Tf2Ps3SourceTemplatePatchStaticTemplate(
    string Name,
    int StaticLine,
    int ByteLength,
    string ReplacementTarget);

internal sealed record Tf2Ps3SourceResponderMethod(
    string Name,
    int Start,
    int End,
    int BodyStart,
    string Body);
