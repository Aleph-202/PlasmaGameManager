using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRequiredHandlerTableTocAccessReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceRequiredHandlerTableTocAccessReport> ReduceAsync(
        string elfPath,
        string tableNeighborhoodPath,
        string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        using var neighborhoodDocument = JsonDocument.Parse(await File.ReadAllTextAsync(tableNeighborhoodPath));

        var words = neighborhoodDocument.RootElement.GetProperty("Runs")
            .EnumerateArray()
            .SelectMany(static run => run.GetProperty("Words").EnumerateArray())
            .Select(static word => new NeighborhoodWord(
                ParseHex(ReadString(word, "Address")),
                ParseHex(ReadString(word, "Value")),
                ReadString(word, "Annotation")))
            .ToArray();

        var relevantValues = words
            .Where(static word => word.Annotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal)
                || word.Annotation.StartsWith("string-pointer:", StringComparison.Ordinal)
                || word.Annotation == "writable-data-pointer")
            .Select(static word => word.Value)
            .Where(static value => value != 0)
            .Distinct()
            .ToArray();
        var directNeighborhoodAddresses = words.Select(static word => word.Address).Distinct().ToArray();
        var valueReferenceAddresses = relevantValues
            .SelectMany(value => image.FindU32ReferencesInLoadedSegments(value, writableOnly: true)
                .Select(address => new ValueReference(address, value, AnnotationForValue(words, value))))
            .GroupBy(static reference => reference.Address)
            .Select(static group => group.First())
            .OrderBy(static reference => reference.Address)
            .ToArray();

        var targetDataAddresses = directNeighborhoodAddresses
            .Concat(valueReferenceAddresses.Select(static reference => reference.Address))
            .Distinct()
            .Order()
            .ToArray();

        var opdDescriptors = image.FindPpc64OpdTocDescriptors();
        var tocRows = opdDescriptors
            .GroupBy(static descriptor => descriptor.TocBase)
            .Select(static group => new Tf2Ps3SourceRequiredHandlerTableTocBase(
                Hex(group.Key),
                group.Count(),
                group.Take(12).Select(static descriptor => new Tf2Ps3SourceRequiredHandlerTableTocDescriptorSample(
                    Hex(descriptor.DescriptorAddress),
                    Hex(descriptor.EntryAddress))).ToArray()))
            .OrderByDescending(static row => row.DescriptorCount)
            .ThenBy(static row => row.TocBase, StringComparer.Ordinal)
            .ToArray();
        var knownTocs = tocRows.Select(static row => ParseHex(row.TocBase)).ToArray();

        var accesses = image.FindPpcR2RelativeDataAccessesToAddresses(targetDataAddresses, knownTocs)
            .Select(access => BuildAccessRow(access, words, valueReferenceAddresses))
            .ToArray();

        var directWindowAccesses = accesses.Where(static access => access.TargetKind == "direct-neighborhood-word").ToArray();
        var requiredVptrValueAccesses = accesses.Where(static access => access.TargetAnnotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal)).ToArray();
        var report = new Tf2Ps3SourceRequiredHandlerTableTocAccessReport(
            "tf2ps3-source-required-handler-table-toc-access",
            "Scans executable PowerPC instructions for r2/TOC-relative accesses to the required-handler table neighborhood or duplicate data words that contain the same vptr/string values. This catches PS3 code that never materializes absolute table addresses.",
            new Tf2Ps3SourceRequiredHandlerTableTocAccessInputs(elfPath, tableNeighborhoodPath),
            new Tf2Ps3SourceRequiredHandlerTableTocAccessSummary(
                opdDescriptors.Length,
                tocRows.Length,
                tocRows.Length == 0 ? "" : tocRows[0].TocBase,
                tocRows.Length == 0 ? 0 : tocRows[0].DescriptorCount,
                directNeighborhoodAddresses.Length,
                relevantValues.Length,
                valueReferenceAddresses.Length,
                targetDataAddresses.Length,
                accesses.Length,
                directWindowAccesses.Length,
                requiredVptrValueAccesses.Length,
                accesses.Select(static access => access.InstructionAddress).Distinct().Count()),
            tocRows.Take(24).ToArray(),
            valueReferenceAddresses.Select(static reference => new Tf2Ps3SourceRequiredHandlerTableValueReference(
                Hex(reference.Address),
                Hex(reference.Value),
                reference.Annotation)).ToArray(),
            accesses,
            BuildFindings(tocRows, accesses, directWindowAccesses, requiredVptrValueAccesses));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRequiredHandlerTableTocAccessRow BuildAccessRow(
        PpcR2RelativeDataAccess access,
        IReadOnlyCollection<NeighborhoodWord> words,
        IReadOnlyCollection<ValueReference> valueReferences)
    {
        var direct = words.FirstOrDefault(word => word.Address == access.TargetDataAddress);
        if (direct is not null)
        {
            return new Tf2Ps3SourceRequiredHandlerTableTocAccessRow(
                Hex(access.InstructionAddress),
                Hex(access.InstructionWord),
                access.MnemonicFamily,
                access.Displacement,
                Hex(access.TocBase),
                Hex(access.TargetDataAddress),
                "direct-neighborhood-word",
                Hex(direct.Value),
                direct.Annotation);
        }

        var valueReference = valueReferences.FirstOrDefault(reference => reference.Address == access.TargetDataAddress);
        return new Tf2Ps3SourceRequiredHandlerTableTocAccessRow(
            Hex(access.InstructionAddress),
            Hex(access.InstructionWord),
            access.MnemonicFamily,
            access.Displacement,
            Hex(access.TocBase),
            Hex(access.TargetDataAddress),
            "duplicate-value-word",
            valueReference is null ? "0x00000000" : Hex(valueReference.Value),
            valueReference?.Annotation ?? "");
    }

    private static string[] BuildFindings(
        Tf2Ps3SourceRequiredHandlerTableTocBase[] tocRows,
        Tf2Ps3SourceRequiredHandlerTableTocAccessRow[] accesses,
        Tf2Ps3SourceRequiredHandlerTableTocAccessRow[] directWindowAccesses,
        Tf2Ps3SourceRequiredHandlerTableTocAccessRow[] requiredVptrValueAccesses)
    {
        var findings = new List<string>
        {
            $"Recovered {tocRows.Length} unique OPD TOC bases from writable descriptors.",
            tocRows.Length == 0
                ? "No OPD TOC base was recovered; r2-relative scanning could not proceed."
                : $"Most common TOC base is {tocRows[0].TocBase} with {tocRows[0].DescriptorCount} descriptor(s).",
            $"r2-relative accesses to direct 0x019965xx neighborhood words: {directWindowAccesses.Length}.",
            $"r2-relative accesses to duplicate required-handler vptr value words: {requiredVptrValueAccesses.Length}.",
            $"Total relevant r2-relative accesses found: {accesses.Length}."
        };

        if (directWindowAccesses.Length == 0)
        {
            findings.Add("No executable r2-relative instruction reaches the exact required-handler neighborhood window, so the 0x0199659c run still has no direct code consumer.");
        }

        if (requiredVptrValueAccesses.Length > 0)
        {
            var sample = string.Join(", ", requiredVptrValueAccesses.Take(8)
                .Select(static access => $"{access.InstructionAddress}->{access.TargetDataAddress}/{access.TargetAnnotation}"));
            findings.Add("Duplicate required-handler vptr values are TOC-accessed elsewhere: " + sample + ".");
        }
        else
        {
            findings.Add("No duplicate required-handler vptr value word is reached by an r2-relative instruction in the scanned text.");
        }

        return findings.ToArray();
    }

    private static string AnnotationForValue(IReadOnlyCollection<NeighborhoodWord> words, uint value)
    {
        return words.FirstOrDefault(word => word.Value == value)?.Annotation ?? "";
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

    private sealed record NeighborhoodWord(uint Address, uint Value, string Annotation);
    private sealed record ValueReference(uint Address, uint Value, string Annotation);
}

public sealed record Tf2Ps3SourceRequiredHandlerTableTocAccessReport(
    string Status,
    string Note,
    Tf2Ps3SourceRequiredHandlerTableTocAccessInputs Inputs,
    Tf2Ps3SourceRequiredHandlerTableTocAccessSummary Summary,
    Tf2Ps3SourceRequiredHandlerTableTocBase[] TocBases,
    Tf2Ps3SourceRequiredHandlerTableValueReference[] ValueReferenceWords,
    Tf2Ps3SourceRequiredHandlerTableTocAccessRow[] Accesses,
    string[] Findings);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocAccessInputs(
    string Elf,
    string TableNeighborhood);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocAccessSummary(
    int OpdDescriptorCount,
    int UniqueTocBaseCount,
    string MostCommonTocBase,
    int MostCommonTocDescriptorCount,
    int DirectNeighborhoodAddressCount,
    int RelevantValueCount,
    int DuplicateValueReferenceAddressCount,
    int TargetDataAddressCount,
    int R2RelativeAccessCount,
    int DirectNeighborhoodR2RelativeAccessCount,
    int RequiredHandlerVptrValueR2RelativeAccessCount,
    int UniqueInstructionHitCount);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocBase(
    string TocBase,
    int DescriptorCount,
    Tf2Ps3SourceRequiredHandlerTableTocDescriptorSample[] DescriptorSamples);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocDescriptorSample(
    string DescriptorAddress,
    string EntryAddress);

public sealed record Tf2Ps3SourceRequiredHandlerTableValueReference(
    string Address,
    string Value,
    string Annotation);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocAccessRow(
    string InstructionAddress,
    string InstructionWord,
    string MnemonicFamily,
    int Displacement,
    string TocBase,
    string TargetDataAddress,
    string TargetKind,
    string TargetValue,
    string TargetAnnotation);
