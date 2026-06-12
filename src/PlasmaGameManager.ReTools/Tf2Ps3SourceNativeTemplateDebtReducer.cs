using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceNativeTemplateDebtReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceNativeTemplateDebtReport> ReduceAsync(
        string responderSourcePath,
        string queuedPeerTargetMapPath,
        string outputPath)
    {
        var source = await File.ReadAllTextAsync(responderSourcePath);
        using var targetMap = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPeerTargetMapPath));

        var hexTemplates = ExtractHexTemplates(source).ToArray();
        var loadingFrames = ExtractLoadingFrames(source).ToArray();
        var highEntropyCalls = ExtractCallSites(source, "BuildHighEntropyBinaryBody")
            .Concat(ExtractCallSites(source, "FillHighEntropyDeterministic"))
            .Concat(ExtractHighEntropyPaddingCallSites(source))
            .OrderBy(static item => item.Line)
            .ThenBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();
        var targetNames = targetMap.RootElement.GetProperty("NextImplementationTargets").EnumerateArray()
            .Select(static target => target.GetProperty("Name").GetString() ?? "")
            .Where(static name => name.Length > 0)
            .ToArray();

        var debtItems = hexTemplates
            .Select(template => new Tf2Ps3SourceNativeTemplateDebtItem(
                template.Name,
                "static-hex-template",
                template.Line,
                template.ByteLength,
                CountIdentifierUses(source, template.Name),
                ReplacementTargetForTemplate(template.Name),
                ExplanationForTemplate(template.Name)))
            .Concat(loadingFrames
                .Where(static frame => frame.Kind is "HighEntropy" or "MixedBinary")
                .Select(frame => new Tf2Ps3SourceNativeTemplateDebtItem(
                    $"loading-frame:{frame.Kind}:{frame.Length}:line-{frame.Line}",
                    "deterministic-loading-recipe",
                    frame.Line,
                    frame.Length,
                    1,
                    frame.Kind == "HighEntropy"
                        ? "native-snapshot-and-entity-delta-route"
                        : "queued-peer-submessage-boundaries",
                    "Generated deterministic filler still stands in for a recovered native queued peer-channel bitstream or compact submessage.")))
            .Concat(highEntropyCalls.Select(call => new Tf2Ps3SourceNativeTemplateDebtItem(
                call.Name,
                "high-entropy-generator-call",
                call.Line,
                null,
                1,
                "native-snapshot-and-entity-delta-route",
                "High-entropy deterministic padding/filler is acceptable only as a temporary shape shim; final native output must be built from named Source message/object-stream fields.")))
            .ToArray();

        var report = new Tf2Ps3SourceNativeTemplateDebtReport(
            "tf2ps3-source-native-template-debt",
            "Static audit of the current PS3 Source responder. The report names captured/template/filler families that must disappear before the server can be called fully native.",
            new Tf2Ps3SourceNativeTemplateDebtInputs(
                responderSourcePath,
                queuedPeerTargetMapPath),
            new Tf2Ps3SourceNativeTemplateDebtSummary(
                hexTemplates.Length,
                hexTemplates.Sum(static template => template.ByteLength),
                loadingFrames.Length,
                loadingFrames.Count(static frame => frame.Kind == "HighEntropy"),
                loadingFrames.Count(static frame => frame.Kind == "MixedBinary"),
                loadingFrames.Count(static frame => frame.Kind == "PlayerStateLink"),
                highEntropyCalls.Length,
                debtItems.Length,
                debtItems.Count(static item => item.ReplacementTarget == "native-object-stream-bootstrap"),
                debtItems.Count(static item => item.ReplacementTarget == "queued-peer-submessage-boundaries"),
                debtItems.Count(static item => item.ReplacementTarget == "native-snapshot-and-entity-delta-route")),
            targetNames,
            hexTemplates,
            TopLoadingFrameLengths(loadingFrames),
            debtItems,
            [
                "Do not treat a PC srcds packet or captured PCAP byte body as native success. The replacement path is TF.elf object-stream/bootstrap plus queued peer-channel composition.",
                "Final native completion requires StaticHexTemplateCount == 0 and HighEntropyLoadingFrameCount == 0 for production responder paths.",
                "EA Tunnel is not a replacement target here; current Ghidra evidence leaves TF.elf's Source send/queue path as authoritative for the client-visible UDP envelope."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static IEnumerable<Tf2Ps3StaticHexTemplate> ExtractHexTemplates(string source)
    {
        foreach (Match match in HexTemplateRegex().Matches(source))
        {
            var name = match.Groups["name"].Value;
            var hex = match.Groups["hex"].Value;
            yield return new Tf2Ps3StaticHexTemplate(
                name,
                LineNumber(source, match.Index),
                hex.Length / 2,
                hex[..Math.Min(16, hex.Length)].ToLowerInvariant(),
                hex[Math.Max(0, hex.Length - 16)..].ToLowerInvariant(),
                ReplacementTargetForTemplate(name));
        }
    }

    private static IEnumerable<Tf2Ps3LoadingFrameRecipe> ExtractLoadingFrames(string source)
    {
        foreach (Match match in ExplicitLoadingFrameRegex().Matches(source))
        {
            yield return new Tf2Ps3LoadingFrameRecipe(
                match.Groups["kind"].Value,
                int.Parse(match.Groups["length"].Value),
                LineNumber(source, match.Index));
        }

        foreach (Match match in EncodedLoadingFrameStringRegex().Matches(source))
        {
            var line = LineNumber(source, match.Index);
            foreach (Match token in EncodedLoadingFrameTokenRegex().Matches(match.Groups["line"].Value))
            {
                yield return new Tf2Ps3LoadingFrameRecipe(
                    token.Groups["kind"].Value switch
                    {
                        "L" => "PlayerStateLink",
                        "M" => "MixedBinary",
                        _ => "HighEntropy"
                    },
                    int.Parse(token.Groups["length"].Value),
                    line);
            }
        }
    }

    private static IEnumerable<Tf2Ps3HighEntropyCallSite> ExtractCallSites(string source, string methodName)
    {
        foreach (Match match in Regex.Matches(source, $@"\b{Regex.Escape(methodName)}\s*\("))
        {
            yield return new Tf2Ps3HighEntropyCallSite(methodName, LineNumber(source, match.Index));
        }
    }

    private static IEnumerable<Tf2Ps3HighEntropyCallSite> ExtractHighEntropyPaddingCallSites(string source)
    {
        foreach (Match match in Regex.Matches(source, @"highEntropyPadding\s*:\s*true"))
        {
            yield return new Tf2Ps3HighEntropyCallSite("PadNativeBitstream(highEntropyPadding:true)", LineNumber(source, match.Index));
        }
    }

    private static Tf2Ps3SourceNativeTemplateDebtCount[] TopLoadingFrameLengths(Tf2Ps3LoadingFrameRecipe[] frames)
    {
        return frames
            .GroupBy(static frame => $"{frame.Kind}:{frame.Length}", StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(32)
            .Select(static group => new Tf2Ps3SourceNativeTemplateDebtCount(group.Key, group.Count()))
            .ToArray();
    }

    private static string ReplacementTargetForTemplate(string name)
    {
        if (name.Contains("MapLoad", StringComparison.Ordinal)
            || name.Contains("ServerInfo", StringComparison.Ordinal)
            || name.Contains("Continuation", StringComparison.Ordinal))
        {
            return "native-object-stream-bootstrap";
        }

        if (name.Contains("FrozenState", StringComparison.Ordinal)
            || name.Contains("Player", StringComparison.Ordinal)
            || name.Contains("Prompt", StringComparison.Ordinal)
            || name.Contains("LateLargeCommand", StringComparison.Ordinal))
        {
            return "queued-peer-submessage-boundaries";
        }

        return "native-snapshot-and-entity-delta-route";
    }

    private static string ExplanationForTemplate(string name)
    {
        if (name.Contains("Prefix", StringComparison.Ordinal))
        {
            return "Static prefix material must be replaced by named queued-prefix fields or object-stream header fields.";
        }

        if (name.Contains("Template", StringComparison.Ordinal))
        {
            return "Static template body still contains fixed captured-like structure with dynamic fields patched around it.";
        }

        return "Static response body must be replaced by native Source message/object-stream/snapshot generation.";
    }

    private static int CountIdentifierUses(string source, string identifier)
    {
        return Regex.Matches(source, $@"\b{Regex.Escape(identifier)}\b").Count;
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

    [GeneratedRegex(@"private\s+static\s+readonly\s+byte\[\]\s+(?<name>\w+)\s*=\s*Convert\.FromHexString\(\s*""(?<hex>[0-9a-fA-F]+)""\s*\)", RegexOptions.Multiline)]
    private static partial Regex HexTemplateRegex();

    [GeneratedRegex(@"new\(\s*LoadingContinuationFrameKind\.(?<kind>\w+)\s*,\s*(?<length>\d+)")]
    private static partial Regex ExplicitLoadingFrameRegex();

    [GeneratedRegex(@"^\s*(?<line>(?:\d+[HLM](?:;|$))+)\s*$", RegexOptions.Multiline)]
    private static partial Regex EncodedLoadingFrameStringRegex();

    [GeneratedRegex(@"(?<length>\d+)(?<kind>[HLM])")]
    private static partial Regex EncodedLoadingFrameTokenRegex();
}

public sealed record Tf2Ps3SourceNativeTemplateDebtReport(
    string Status,
    string Note,
    Tf2Ps3SourceNativeTemplateDebtInputs Inputs,
    Tf2Ps3SourceNativeTemplateDebtSummary Summary,
    string[] NativeReplacementTargets,
    Tf2Ps3StaticHexTemplate[] StaticHexTemplates,
    Tf2Ps3SourceNativeTemplateDebtCount[] TopLoadingFrameLengths,
    Tf2Ps3SourceNativeTemplateDebtItem[] DebtItems,
    string[] AcceptanceCriteria);

public sealed record Tf2Ps3SourceNativeTemplateDebtInputs(
    string ResponderSource,
    string QueuedPeerTargetMap);

public sealed record Tf2Ps3SourceNativeTemplateDebtSummary(
    int StaticHexTemplateCount,
    int StaticHexTemplateBytes,
    int LoadingFrameRecipeCount,
    int HighEntropyLoadingFrameCount,
    int MixedBinaryLoadingFrameCount,
    int PlayerStateLinkLoadingFrameCount,
    int HighEntropyGeneratorCallCount,
    int DebtItemCount,
    int NativeObjectStreamBootstrapDebtCount,
    int QueuedPeerSubmessageDebtCount,
    int NativeSnapshotEntityDeltaDebtCount);

public sealed record Tf2Ps3StaticHexTemplate(
    string Name,
    int Line,
    int ByteLength,
    string First8Hex,
    string Last8Hex,
    string ReplacementTarget);

public sealed record Tf2Ps3LoadingFrameRecipe(
    string Kind,
    int Length,
    int Line);

public sealed record Tf2Ps3HighEntropyCallSite(
    string Name,
    int Line);

public sealed record Tf2Ps3SourceNativeTemplateDebtItem(
    string Name,
    string Kind,
    int Line,
    int? ByteLength,
    int UseCount,
    string ReplacementTarget,
    string Explanation);

public sealed record Tf2Ps3SourceNativeTemplateDebtCount(
    string Value,
    int Count);
