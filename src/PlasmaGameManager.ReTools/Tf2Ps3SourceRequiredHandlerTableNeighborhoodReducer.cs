using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRequiredHandlerTableNeighborhoodReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceRequiredHandlerTableNeighborhoodReport> ReduceAsync(
        string elfPath,
        string constructorProbePath,
        string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        using var probeDocument = JsonDocument.Parse(await File.ReadAllTextAsync(constructorProbePath));
        var runs = probeDocument.RootElement.GetProperty("ContiguousRequiredObjectVptrRuns")
            .EnumerateArray()
            .Select(run => BuildRunNeighborhood(image, run))
            .ToArray();

        var report = new Tf2Ps3SourceRequiredHandlerTableNeighborhoodReport(
            "tf2ps3-source-required-handler-table-neighborhood",
            "Data-neighborhood view around contiguous required Source handler object-vptr table runs. This is a static map for locating the setup/consumer path after Ghidra reported no direct xrefs to the table words.",
            new Tf2Ps3SourceRequiredHandlerTableNeighborhoodInputs(elfPath, constructorProbePath),
            new Tf2Ps3SourceRequiredHandlerTableNeighborhoodSummary(
                runs.Length,
                runs.Length == 0 ? 0 : runs.Max(static run => run.HandlerCount),
                runs.Sum(static run => run.Words.Count(static word => word.Annotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal))),
                runs.Sum(static run => run.Words.Count(static word => word.Annotation == "executable-pointer")),
                runs.Sum(static run => run.Words.Count(static word => word.Annotation == "writable-data-pointer")),
                runs.Sum(static run => run.Words.Count(static word => word.Annotation.StartsWith("string-pointer:", StringComparison.Ordinal))),
                runs.Sum(static run => run.Words.Count(static word => word.Annotation == "zero"))),
            runs,
            BuildFindings(runs));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRequiredHandlerTableNeighborhoodRun BuildRunNeighborhood(
        Elf64BigEndianImage image,
        JsonElement run)
    {
        var entries = run.GetProperty("Entries").EnumerateArray()
            .Select(static entry => new RequiredHandlerEntry(
                ReadString(entry, "ReferenceAddress"),
                ReadString(entry, "ClassName"),
                ReadInt(entry, "MessageId"),
                ReadString(entry, "ObjectVptr")))
            .ToArray();
        var byVptr = entries.ToDictionary(static entry => ParseHex(entry.ObjectVptr), static entry => entry);

        var baseAddress = ParseHex(ReadString(run, "BaseAddress"));
        var endAddress = ParseHex(ReadString(run, "EndAddress"));
        var windowStart = baseAddress >= 0x60 ? baseAddress - 0x60u : baseAddress;
        var windowEnd = endAddress + 0x60u;
        var words = new List<Tf2Ps3SourceRequiredHandlerTableNeighborhoodWord>();
        for (var address = windowStart; address <= windowEnd; address += 4)
        {
            if (!image.TryReadU32(address, out var value))
            {
                continue;
            }

            words.Add(new Tf2Ps3SourceRequiredHandlerTableNeighborhoodWord(
                Hex(address),
                Hex(value),
                AnnotateWord(image, value, byVptr),
                DereferenceHint(image, value)));
        }

        return new Tf2Ps3SourceRequiredHandlerTableNeighborhoodRun(
            ReadString(run, "BaseAddress"),
            ReadString(run, "EndAddress"),
            ReadInt(run, "HandlerCount"),
            entries.Select(static entry => new Tf2Ps3SourceRequiredHandlerTableNeighborhoodEntry(
                entry.ReferenceAddress,
                entry.ClassName,
                entry.MessageId,
                entry.ObjectVptr)).ToArray(),
            Hex(windowStart),
            Hex(windowEnd),
            words.ToArray());
    }

    private static string AnnotateWord(
        Elf64BigEndianImage image,
        uint value,
        IReadOnlyDictionary<uint, RequiredHandlerEntry> byVptr)
    {
        if (value == 0)
        {
            return "zero";
        }

        if (byVptr.TryGetValue(value, out var handler))
        {
            return $"required-handler-vptr:{handler.ClassName}/id{handler.MessageId}";
        }

        if (image.TryReadAsciiString(value, 80, out var text))
        {
            return "string-pointer:" + text;
        }

        if (image.IsExecutableAddress(value))
        {
            return "executable-pointer";
        }

        if (image.IsWritableAddress(value))
        {
            return "writable-data-pointer";
        }

        return value < 0x10000 ? "small-int-or-count" : "unmapped-or-immediate";
    }

    private static string DereferenceHint(Elf64BigEndianImage image, uint value)
    {
        if (value == 0 || !image.TryReadU32(value, out var dereferenced))
        {
            return "";
        }

        if (image.IsExecutableAddress(dereferenced))
        {
            return "points-to-executable:" + Hex(dereferenced);
        }

        if (image.IsWritableAddress(dereferenced))
        {
            return "points-to-writable:" + Hex(dereferenced);
        }

        if (image.TryReadAsciiString(dereferenced, 80, out var text))
        {
            return "points-to-string:" + text;
        }

        return "points-to-u32:" + Hex(dereferenced);
    }

    private static string[] BuildFindings(Tf2Ps3SourceRequiredHandlerTableNeighborhoodRun[] runs)
    {
        if (runs.Length == 0)
        {
            return ["No contiguous required-handler vptr table runs were available to inspect."];
        }

        var findings = new List<string>();
        foreach (var run in runs)
        {
            var requiredWords = run.Words
                .Where(static word => word.Annotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal))
                .ToArray();
            findings.Add($"Run {run.BaseAddress}..{run.EndAddress} contains {requiredWords.Length} required-handler vptr words inside neighborhood {run.WindowStartAddress}..{run.WindowEndAddress}.");
            var before = run.Words.Where(word => CompareHex(word.Address, run.BaseAddress) < 0).TakeLast(6)
                .Select(static word => $"{word.Address}={word.Value}/{word.Annotation}");
            var after = run.Words.Where(word => CompareHex(word.Address, run.EndAddress) > 0).Take(6)
                .Select(static word => $"{word.Address}={word.Value}/{word.Annotation}");
            findings.Add("Preceding words: " + string.Join(", ", before));
            findings.Add("Following words: " + string.Join(", ", after));
        }

        findings.Add("No consumer is inferred from this report alone; combine with source-required-handler-vptr-table-ghidra-refs.json and a Ghidra decompile export around any adjacent executable/writable anchors.");
        return findings.ToArray();
    }

    private static int CompareHex(string left, string right)
    {
        return ParseHex(left).CompareTo(ParseHex(right));
    }

    private static uint ParseHex(string value)
    {
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return Convert.ToUInt32(text, 16);
    }

    private static string Hex(uint value) => "0x" + value.ToString("x8");

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

    private sealed record RequiredHandlerEntry(
        string ReferenceAddress,
        string ClassName,
        int MessageId,
        string ObjectVptr);
}

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodReport(
    string Status,
    string Note,
    Tf2Ps3SourceRequiredHandlerTableNeighborhoodInputs Inputs,
    Tf2Ps3SourceRequiredHandlerTableNeighborhoodSummary Summary,
    Tf2Ps3SourceRequiredHandlerTableNeighborhoodRun[] Runs,
    string[] Findings);

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodInputs(
    string Elf,
    string ConstructorProbe);

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodSummary(
    int RunCount,
    int LongestRunHandlerCount,
    int RequiredHandlerVptrWordCount,
    int ExecutablePointerWordCount,
    int WritablePointerWordCount,
    int StringPointerWordCount,
    int ZeroWordCount);

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodRun(
    string BaseAddress,
    string EndAddress,
    int HandlerCount,
    Tf2Ps3SourceRequiredHandlerTableNeighborhoodEntry[] Entries,
    string WindowStartAddress,
    string WindowEndAddress,
    Tf2Ps3SourceRequiredHandlerTableNeighborhoodWord[] Words);

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodEntry(
    string ReferenceAddress,
    string ClassName,
    int MessageId,
    string ObjectVptr);

public sealed record Tf2Ps3SourceRequiredHandlerTableNeighborhoodWord(
    string Address,
    string Value,
    string Annotation,
    string DereferenceHint);
