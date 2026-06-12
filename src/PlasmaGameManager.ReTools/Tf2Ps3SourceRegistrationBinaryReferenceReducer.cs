using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRegistrationBinaryReferenceReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Tf2Ps3SourceRegistrationBinaryTarget[] FixedTargets =
    [
        new("0x0180c9a0", "source-helper-slice-table-base", null, "Start of the helper/function-table slice that contains the registration slot."),
        new("0x0180c9c0", "source-helper-slice-visible-base", null, "Earlier reducer's helper slice base; useful for catching constructor/table installs that use the visible table address."),
        new("0x0180ca04", "source-helper-registration-slot-address", null, "Table address whose word points at the OPD descriptor for 00a5df70."),
        new("0x0190e558", "source-handler-registration-opd", null, "OPD descriptor for 00a5df70.")
    ];

    public static async Task ReduceAsync(
        string elfPath,
        string candidateReportPath,
        string ghidraRawReferenceRefsPath,
        string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var rawReferenceGhidraHits = File.Exists(ghidraRawReferenceRefsPath)
            ? LoadGhidraHitCounts(ghidraRawReferenceRefsPath)
            : new Dictionary<string, Tf2Ps3SourceRegistrationGhidraHitCounts>(StringComparer.OrdinalIgnoreCase);
        var candidateTargets = LoadSourceNeighborhoodCandidateTargets(candidateReportPath);
        var targets = FixedTargets.Concat(candidateTargets)
            .GroupBy(static target => target.Address, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static target => target.Address, StringComparer.Ordinal)
            .ToArray();

        var rows = targets
            .Select(target => BuildRow(image, target, rawReferenceGhidraHits))
            .ToArray();

        var candidateRows = rows
            .Where(static row => row.Role == "source-neighborhood-handler-candidate-table")
            .ToArray();
        var report = new Tf2Ps3SourceRegistrationBinaryReferenceReport(
            "tf2ps3-source-registration-binary-reference-map",
            "Independent raw big-endian u32 scan over TF.elf writable segments for the helper slice, registration OPD, and current Source-neighborhood handler candidate table bases. This checks whether Ghidra missed simple table/base pointer references that could prove handler object setup.",
            elfPath,
            candidateReportPath,
            ghidraRawReferenceRefsPath,
            new Tf2Ps3SourceRegistrationBinaryReferenceSummary(
                rows.Length,
                rows.Count(static row => row.RawReferenceCount > 0),
                candidateRows.Length,
                candidateRows.Count(static row => row.RawReferenceCount > 0),
                rows.Sum(static row => row.ExternalReferenceCount),
                rows.SelectMany(static row => row.References).Count(static reference => reference.Classification == "raw-unreferenced-data-table-word"),
                rows.Single(static row => row.Role == "source-helper-slice-table-base").RawReferenceCount,
                rows.Single(static row => row.Role == "source-helper-slice-visible-base").RawReferenceCount,
                rows.Single(static row => row.Role == "source-helper-registration-slot-address").RawReferenceCount,
                rows.Single(static row => row.Role == "source-handler-registration-opd").RawReferenceCount),
            rows,
            BuildFindings(rows));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceRegistrationBinaryReferenceRow BuildRow(
        Elf64BigEndianImage image,
        Tf2Ps3SourceRegistrationBinaryTarget target,
        IReadOnlyDictionary<string, Tf2Ps3SourceRegistrationGhidraHitCounts> rawReferenceGhidraHits)
    {
        var value = ParseHex(target.Address);
        var refs = image.FindU32References(value)
            .Select(reference => new Tf2Ps3SourceRegistrationRawReference(
                Hex(reference),
                ClassifyReference(target, reference, rawReferenceGhidraHits),
                rawReferenceGhidraHits.TryGetValue(Hex(reference), out var counts) ? counts.ReferenceHitCount : 0,
                rawReferenceGhidraHits.TryGetValue(Hex(reference), out counts) ? counts.ScalarHitCount : 0))
            .ToArray();

        return new Tf2Ps3SourceRegistrationBinaryReferenceRow(
            target.Address,
            target.Role,
            target.MessageId,
            target.Purpose,
            refs.Length,
            refs.Count(static reference => reference.Classification == "external-or-constructor-candidate"),
            refs,
            BuildEvidence(target, refs));
    }

    private static string ClassifyReference(
        Tf2Ps3SourceRegistrationBinaryTarget target,
        uint reference,
        IReadOnlyDictionary<string, Tf2Ps3SourceRegistrationGhidraHitCounts> rawReferenceGhidraHits)
    {
        if (target.Role == "source-handler-registration-opd" && reference == 0x0180ca04)
        {
            return "expected-helper-slice-slot";
        }

        if (rawReferenceGhidraHits.TryGetValue(Hex(reference), out var counts)
            && counts.ReferenceHitCount == 0
            && counts.ScalarHitCount == 0)
        {
            return "raw-unreferenced-data-table-word";
        }

        return reference >= 0x0180c9a0 && reference <= 0x0180ca98
            ? "helper-slice-local-table-word"
            : "external-or-constructor-candidate";
    }

    private static string[] BuildEvidence(
        Tf2Ps3SourceRegistrationBinaryTarget target,
        IReadOnlyCollection<Tf2Ps3SourceRegistrationRawReference> refs)
    {
        var evidence = new List<string>();
        if (refs.Count == 0)
        {
            evidence.Add("no-raw-writable-u32-references");
        }

        if (refs.Any(static reference => reference.Classification == "external-or-constructor-candidate"))
        {
            evidence.Add("has-external-raw-reference");
        }

        if (refs.Any(static reference => reference.Classification == "expected-helper-slice-slot"))
        {
            evidence.Add("only-known-registration-opd-reference-is-helper-slice-slot");
        }

        if (refs.Any(static reference => reference.Classification == "raw-unreferenced-data-table-word"))
        {
            evidence.Add("raw-reference-has-no-ghidra-xrefs");
        }

        if (target.Role == "source-neighborhood-handler-candidate-table")
        {
            evidence.Add("source-neighborhood-candidate");
        }

        return evidence.ToArray();
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceRegistrationBinaryReferenceRow> rows)
    {
        var candidateRows = rows
            .Where(static row => row.Role == "source-neighborhood-handler-candidate-table")
            .ToArray();
        var findings = new List<string>
        {
            "The raw ELF scan is independent of Ghidra xrefs and looks for simple writable-segment big-endian u32 references.",
            $"Current Source-neighborhood handler candidates scanned: {string.Join(", ", candidateRows.Select(static row => $"{row.Address}/id{row.MessageId}"))}.",
            "The OPD descriptor for 00a5df70 is still only referenced by helper-slice table word 0180ca04 in this raw scan."
        };

        if (candidateRows.Any(static row => row.RawReferenceCount > 0))
        {
            findings.Add("At least one structural handler candidate table has a raw reference; inspect ExternalReferenceCount and follow that constructor/setup path next.");
        }
        else
        {
            findings.Add("No structural Source-neighborhood handler candidate table has a raw writable-segment base reference, so simple constructor/table installs are not present in the current ELF image.");
        }

        if (rows.Any(static row => row.ExternalReferenceCount > 0))
        {
            findings.Add("One or more raw references are outside the helper slice and should be promoted to Ghidra/decompile targets.");
        }
        else
        {
            findings.Add("No raw external reference with Ghidra support was found for the helper slice base, visible base, registration slot address, registration OPD, or current Source-neighborhood handler candidate table bases.");
        }

        return findings.ToArray();
    }

    private static Dictionary<string, Tf2Ps3SourceRegistrationGhidraHitCounts> LoadGhidraHitCounts(string ghidraRefsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(ghidraRefsPath));
        var counts = new Dictionary<string, Tf2Ps3SourceRegistrationGhidraHitCounts>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in document.RootElement.GetProperty("Targets").EnumerateArray())
        {
            var address = NormalizeHex(target.GetProperty("Address").GetString() ?? "");
            counts[address] = new Tf2Ps3SourceRegistrationGhidraHitCounts(
                target.GetProperty("ReferenceHitCount").GetInt32(),
                target.GetProperty("ScalarHitCount").GetInt32());
        }

        return counts;
    }

    private static Tf2Ps3SourceRegistrationBinaryTarget[] LoadSourceNeighborhoodCandidateTargets(string candidateReportPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(candidateReportPath));
        var targets = new List<Tf2Ps3SourceRegistrationBinaryTarget>();
        foreach (var candidate in document.RootElement.GetProperty("Candidates").EnumerateArray())
        {
            var notes = candidate.GetProperty("Notes").EnumerateArray()
                .Select(static note => note.GetString() ?? "")
                .ToArray();
            if (!notes.Any(static note => note.Contains("candidate table is adjacent to the recovered Source-side function-table slice", StringComparison.Ordinal)))
            {
                continue;
            }

            var messageId = candidate.TryGetProperty("MessageId", out var messageIdElement)
                ? messageIdElement.GetInt32()
                : (int?)null;
            targets.Add(new Tf2Ps3SourceRegistrationBinaryTarget(
                NormalizeHex(candidate.GetProperty("BaseAddress").GetString() ?? ""),
                "source-neighborhood-handler-candidate-table",
                messageId,
                "Current structural candidate table base from source-handler-vtable-candidates.json."));
        }

        return targets.ToArray();
    }

    private static string NormalizeHex(string value)
    {
        var number = ParseHex(value);
        return Hex(number);
    }

    private static uint ParseHex(string value)
    {
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
        return Convert.ToUInt32(text, 16);
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }
}

public sealed record Tf2Ps3SourceRegistrationBinaryReferenceReport(
    string Status,
    string Note,
    string ElfInput,
    string CandidateReportInput,
    string GhidraRawReferenceRefsInput,
    Tf2Ps3SourceRegistrationBinaryReferenceSummary Summary,
    Tf2Ps3SourceRegistrationBinaryReferenceRow[] Targets,
    string[] Findings);

public sealed record Tf2Ps3SourceRegistrationBinaryReferenceSummary(
    int TargetCount,
    int TargetsWithRawReferences,
    int SourceNeighborhoodCandidateTargetCount,
    int SourceNeighborhoodCandidatesWithRawReferences,
    int ExternalReferenceCount,
    int RawUnreferencedDataReferenceCount,
    int HelperSliceTableBaseReferenceCount,
    int HelperSliceVisibleBaseReferenceCount,
    int HelperRegistrationSlotAddressReferenceCount,
    int HandlerRegistrationOpdReferenceCount);

public sealed record Tf2Ps3SourceRegistrationBinaryReferenceRow(
    string Address,
    string Role,
    int? MessageId,
    string Purpose,
    int RawReferenceCount,
    int ExternalReferenceCount,
    Tf2Ps3SourceRegistrationRawReference[] References,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceRegistrationRawReference(
    string Address,
    string Classification,
    int GhidraReferenceHitCount,
    int GhidraScalarHitCount);

public sealed record Tf2Ps3SourceRegistrationBinaryTarget(
    string Address,
    string Role,
    int? MessageId,
    string Purpose);

public sealed record Tf2Ps3SourceRegistrationGhidraHitCounts(
    int ReferenceHitCount,
    int ScalarHitCount);
