using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHandlerRegistrationProofReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string candidateReportPath, string refsReportPath, string outputPath)
    {
        using var candidateDocument = JsonDocument.Parse(await File.ReadAllTextAsync(candidateReportPath));
        using var refsDocument = JsonDocument.Parse(await File.ReadAllTextAsync(refsReportPath));

        var referenceTargets = refsDocument.RootElement
            .GetProperty("Targets")
            .EnumerateArray()
            .Select(BuildReferenceTarget)
            .ToDictionary(static target => target.Address, StringComparer.Ordinal);

        var candidates = candidateDocument.RootElement
            .GetProperty("Candidates")
            .EnumerateArray()
            .Select(candidate => BuildCandidate(candidate, referenceTargets))
            .OrderBy(static candidate => candidate.BaseAddress, StringComparer.Ordinal)
            .ToArray();

        var sourceCandidates = candidates
            .Where(static candidate => candidate.SourcePlayerNeighborhood)
            .ToArray();
        var proven = candidates
            .Where(static candidate => candidate.RegistrationEvidence == "registration-path-proven")
            .ToArray();
        var sourceWithBaseRefs = sourceCandidates
            .Where(static candidate => candidate.TableBaseReferenceHitCount > 0 || candidate.TableBaseScalarHitCount > 0)
            .ToArray();

        var report = new Tf2Ps3SourceHandlerRegistrationProofReport(
            "tf2ps3-source-handler-registration-proof-map",
            "Combines the structural handler-vtable scan with Ghidra reference/scalar evidence. A candidate is not considered a native Source payload handler until an object construction path or table-base reference proves it is appended through 00a5df70.",
            new Tf2Ps3SourceHandlerRegistrationProofInputs(candidateReportPath, refsReportPath),
            new Tf2Ps3SourceHandlerRegistrationProofSummary(
                candidates.Length,
                sourceCandidates.Length,
                sourceCandidates.Select(static candidate => candidate.MessageId).Distinct().Order().ToArray(),
                proven.Length,
                sourceWithBaseRefs.Length,
                sourceCandidates.Count(static candidate => candidate.RegistrationEvidence == "registration-proof-missing")),
            candidates,
            BuildFindings(sourceCandidates, proven));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceHandlerRegistrationProofCandidate BuildCandidate(
        JsonElement candidate,
        IReadOnlyDictionary<string, Tf2Ps3SourceHandlerRegistrationProofReferenceTarget> referenceTargets)
    {
        var baseAddress = NormalizeAddress(ReadString(candidate, "BaseAddress"));
        var baseReference = ReferenceFor(referenceTargets, baseAddress);
        var sourcePlayerNeighborhood = candidate.GetProperty("SourcePlayerNeighborhood").GetBoolean();
        var slots = candidate.GetProperty("Slots")
            .EnumerateArray()
            .Select(slot => BuildSlot(slot, baseAddress, referenceTargets))
            .ToArray();
        var nonTableCodeReferences = slots.Sum(static slot => slot.FunctionCodeReferenceFunctions.Length);
        var externalOpdDataReferences = slots.Sum(static slot => slot.OpdExternalDataReferenceFromAddresses.Length);
        var registrationEvidence = ClassifyRegistrationEvidence(
            sourcePlayerNeighborhood,
            baseReference,
            nonTableCodeReferences,
            externalOpdDataReferences);

        return new Tf2Ps3SourceHandlerRegistrationProofCandidate(
            baseAddress,
            candidate.GetProperty("MessageId").GetInt64(),
            ReadString(candidate, "Classification"),
            sourcePlayerNeighborhood,
            baseReference.ReferenceHitCount,
            baseReference.ScalarHitCount,
            nonTableCodeReferences,
            externalOpdDataReferences,
            registrationEvidence,
            BuildCandidateNotes(candidate, baseReference, nonTableCodeReferences, externalOpdDataReferences),
            slots);
    }

    private static Tf2Ps3SourceHandlerRegistrationProofSlot BuildSlot(
        JsonElement slot,
        string tableBase,
        IReadOnlyDictionary<string, Tf2Ps3SourceHandlerRegistrationProofReferenceTarget> referenceTargets)
    {
        var slotOffset = ReadString(slot, "SlotOffset");
        var opdAddress = NormalizeAddress(ReadString(slot, "OpdAddress"));
        var functionAddress = NormalizeAddress(ReadString(slot, "FunctionAddress"));
        var opdReference = ReferenceFor(referenceTargets, opdAddress);
        var functionReference = ReferenceFor(referenceTargets, functionAddress);
        var tableDataReferences = opdReference.ReferenceHits
            .Where(hit => hit.Type == "DATA" && IsWithinTable(hit.From, tableBase))
            .Select(static hit => hit.From)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var externalDataReferences = opdReference.ReferenceHits
            .Where(hit => hit.Type == "DATA" && !IsWithinTable(hit.From, tableBase))
            .Select(static hit => hit.From)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new Tf2Ps3SourceHandlerRegistrationProofSlot(
            slotOffset,
            ReadString(slot, "Role"),
            ReadString(slot, "Kind"),
            opdAddress,
            functionAddress,
            tableDataReferences,
            externalDataReferences,
            opdReference.CodeReferenceFunctions,
            functionReference.CodeReferenceFunctions,
            SummarizeSlotEvidence(tableDataReferences, externalDataReferences, opdReference, functionReference));
    }

    private static string ClassifyRegistrationEvidence(
        bool sourcePlayerNeighborhood,
        Tf2Ps3SourceHandlerRegistrationProofReferenceTarget baseReference,
        int nonTableCodeReferences,
        int externalOpdDataReferences)
    {
        if (baseReference.CodeReferenceFunctions.Length > 0 || baseReference.ScalarHitCount > 0)
        {
            return "constructor-reference-candidate";
        }

        if (sourcePlayerNeighborhood && baseReference.ReferenceHitCount == 0 && externalOpdDataReferences == 0)
        {
            return "registration-proof-missing";
        }

        if (sourcePlayerNeighborhood)
        {
            return "shared-opd-data-only";
        }

        return nonTableCodeReferences > 0 ? "non-source-helper-references" : "structural-only";
    }

    private static string[] BuildCandidateNotes(
        JsonElement candidate,
        Tf2Ps3SourceHandlerRegistrationProofReferenceTarget baseReference,
        int nonTableCodeReferences,
        int externalOpdDataReferences)
    {
        var notes = candidate.TryGetProperty("Notes", out var notesElement)
            ? notesElement.EnumerateArray()
                .Select(static note => note.GetString() ?? "")
                .Where(static note => note.Length > 0)
                .ToList()
            : [];

        if (baseReference.ReferenceHitCount == 0 && baseReference.ScalarHitCount == 0)
        {
            notes.Add("Ghidra found no direct reference or scalar load of this candidate table base");
        }

        if (externalOpdDataReferences > 0)
        {
            notes.Add("some slot OPD descriptors are shared by adjacent/static tables, which is data evidence rather than registration proof");
        }

        if (nonTableCodeReferences > 0)
        {
            notes.Add("some slot functions are called elsewhere as helpers; this does not prove the table object is registered through 00a5df70");
        }

        return notes
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceHandlerRegistrationProofCandidate> sourceCandidates,
        IReadOnlyCollection<Tf2Ps3SourceHandlerRegistrationProofCandidate> proven)
    {
        var findings = new List<string>
        {
            "No source-neighborhood candidate table base currently has a Ghidra code reference, scalar reference, or direct constructor reference.",
            "Ids 3, 4, and 5 remain structural candidates only; their table bases are unreferenced and their +0x08 slots still look destructor-like in the vtable scan.",
            "Id 16 remains the cleanest Source-neighborhood structural candidate, but Ghidra currently sees only data-table references to its OPD descriptors, not a 00a5df70 registration path.",
            "The next native Source payload step is constructor/object setup recovery: find the code path that creates handler objects and calls the Source-side function-table slot +0x44 -> 00a5df70."
        };

        if (proven.Count == 0)
        {
            findings.Add("No candidate is upgraded to registration-path-proven by this report.");
        }

        if (sourceCandidates.All(static candidate => candidate.RegistrationEvidence == "registration-proof-missing"
                || candidate.RegistrationEvidence == "shared-opd-data-only"))
        {
            findings.Add("Do not implement map-load behavior from these candidates alone; use them as search anchors for the real registered parser/execute handlers.");
        }

        return findings.ToArray();
    }

    private static string SummarizeSlotEvidence(
        string[] tableDataReferences,
        string[] externalDataReferences,
        Tf2Ps3SourceHandlerRegistrationProofReferenceTarget opdReference,
        Tf2Ps3SourceHandlerRegistrationProofReferenceTarget functionReference)
    {
        var parts = new List<string>();
        if (tableDataReferences.Length > 0)
        {
            parts.Add($"table-data-refs:{tableDataReferences.Length}");
        }

        if (externalDataReferences.Length > 0)
        {
            parts.Add($"external-data-refs:{externalDataReferences.Length}");
        }

        if (opdReference.CodeReferenceFunctions.Length > 0)
        {
            parts.Add($"opd-code-refs:{opdReference.CodeReferenceFunctions.Length}");
        }

        if (functionReference.CodeReferenceFunctions.Length > 0)
        {
            parts.Add($"function-code-refs:{functionReference.CodeReferenceFunctions.Length}");
        }

        return parts.Count == 0 ? "no-reference-evidence" : string.Join("; ", parts);
    }

    private static Tf2Ps3SourceHandlerRegistrationProofReferenceTarget BuildReferenceTarget(JsonElement target)
    {
        var hits = target.GetProperty("ReferenceHits")
            .EnumerateArray()
            .Select(static hit => new Tf2Ps3SourceHandlerRegistrationProofReferenceHit(
                NormalizeAddress(ReadString(hit, "From")),
                ReadString(hit, "Type"),
                NormalizeAddress(ReadString(hit, "FunctionEntry")),
                ReadString(hit, "FunctionName")))
            .ToArray();
        var scalarHits = target.GetProperty("ScalarHits")
            .EnumerateArray()
            .Select(static hit => new Tf2Ps3SourceHandlerRegistrationProofScalarHit(
                NormalizeAddress(ReadString(hit, "Instruction")),
                ReadString(hit, "Representation"),
                NormalizeAddress(ReadString(hit, "FunctionEntry")),
                ReadString(hit, "FunctionName")))
            .ToArray();

        return new Tf2Ps3SourceHandlerRegistrationProofReferenceTarget(
            NormalizeAddress(ReadString(target, "Address")),
            target.GetProperty("ReferenceHitCount").GetInt32(),
            target.GetProperty("ScalarHitCount").GetInt32(),
            hits,
            scalarHits,
            hits.Where(static hit => hit.FunctionEntry.Length > 0)
                .Select(static hit => hit.FunctionEntry)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            scalarHits.Where(static hit => hit.FunctionEntry.Length > 0)
                .Select(static hit => hit.FunctionEntry)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static Tf2Ps3SourceHandlerRegistrationProofReferenceTarget ReferenceFor(
        IReadOnlyDictionary<string, Tf2Ps3SourceHandlerRegistrationProofReferenceTarget> referenceTargets,
        string address)
    {
        if (address.Length > 0 && referenceTargets.TryGetValue(address, out var target))
        {
            return target;
        }

        return new Tf2Ps3SourceHandlerRegistrationProofReferenceTarget(address, 0, 0, [], [], [], []);
    }

    private static bool IsWithinTable(string address, string tableBase)
    {
        if (!uint.TryParse(address, System.Globalization.NumberStyles.HexNumber, null, out var value)
            || !uint.TryParse(tableBase, System.Globalization.NumberStyles.HexNumber, null, out var baseValue))
        {
            return false;
        }

        return value >= baseValue && value < baseValue + 0x3c;
    }

    private static string NormalizeAddress(string value)
    {
        value = value.Trim();
        if (value.Length == 0)
        {
            return "";
        }

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return value.ToLowerInvariant().PadLeft(8, '0');
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }
}

public sealed record Tf2Ps3SourceHandlerRegistrationProofReport(
    string Status,
    string Note,
    Tf2Ps3SourceHandlerRegistrationProofInputs Inputs,
    Tf2Ps3SourceHandlerRegistrationProofSummary Summary,
    Tf2Ps3SourceHandlerRegistrationProofCandidate[] Candidates,
    string[] Findings);

public sealed record Tf2Ps3SourceHandlerRegistrationProofInputs(
    string CandidateReport,
    string ReferenceReport);

public sealed record Tf2Ps3SourceHandlerRegistrationProofSummary(
    int CandidateCount,
    int SourceNeighborhoodCandidateCount,
    long[] SourceNeighborhoodMessageIds,
    int ProvenRegistrationCandidateCount,
    int SourceCandidatesWithTableBaseReferences,
    int SourceCandidatesMissingRegistrationProof);

public sealed record Tf2Ps3SourceHandlerRegistrationProofCandidate(
    string BaseAddress,
    long MessageId,
    string CandidateClassification,
    bool SourcePlayerNeighborhood,
    int TableBaseReferenceHitCount,
    int TableBaseScalarHitCount,
    int SlotFunctionCodeReferenceCount,
    int ExternalOpdDataReferenceCount,
    string RegistrationEvidence,
    string[] Notes,
    Tf2Ps3SourceHandlerRegistrationProofSlot[] Slots);

public sealed record Tf2Ps3SourceHandlerRegistrationProofSlot(
    string SlotOffset,
    string Role,
    string Kind,
    string OpdAddress,
    string FunctionAddress,
    string[] OpdTableDataReferenceFromAddresses,
    string[] OpdExternalDataReferenceFromAddresses,
    string[] OpdCodeReferenceFunctions,
    string[] FunctionCodeReferenceFunctions,
    string EvidenceSummary);

public sealed record Tf2Ps3SourceHandlerRegistrationProofReferenceTarget(
    string Address,
    int ReferenceHitCount,
    int ScalarHitCount,
    Tf2Ps3SourceHandlerRegistrationProofReferenceHit[] ReferenceHits,
    Tf2Ps3SourceHandlerRegistrationProofScalarHit[] ScalarHits,
    string[] CodeReferenceFunctions,
    string[] ScalarHitFunctions);

public sealed record Tf2Ps3SourceHandlerRegistrationProofReferenceHit(
    string From,
    string Type,
    string FunctionEntry,
    string FunctionName);

public sealed record Tf2Ps3SourceHandlerRegistrationProofScalarHit(
    string Instruction,
    string Representation,
    string FunctionEntry,
    string FunctionName);
