using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceHandlerCandidateTocAccessReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceHandlerCandidateTocAccessReport> ReduceAsync(
        string elfPath,
        string handlerCandidatesPath,
        string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        using var candidatesDocument = JsonDocument.Parse(await File.ReadAllTextAsync(handlerCandidatesPath));

        var candidates = candidatesDocument.RootElement.GetProperty("Candidates").EnumerateArray()
            .Select(ReadCandidate)
            .Where(static candidate => candidate.SourcePlayerNeighborhood)
            .ToArray();

        var directTableWords = candidates
            .SelectMany(static candidate => candidate.Slots.Select(slot => new HandlerCandidateTargetWord(
                Address: candidate.BaseAddress + ParseOffset(slot.SlotOffset),
                Value: slot.OpdAddress,
                Kind: "candidate-table-word",
                CandidateBase: candidate.BaseAddress,
                MessageId: candidate.MessageId,
                SlotOffset: slot.SlotOffset,
                SlotRole: slot.Role,
                SlotFunctionAddress: slot.FunctionAddress)))
            .DistinctBy(static word => (word.Address, word.CandidateBase, word.SlotOffset))
            .ToArray();

        var relevantValues = directTableWords
            .Select(static word => word.Value)
            .Where(static value => value != 0)
            .Distinct()
            .ToArray();

        var duplicateValueWords = relevantValues
            .SelectMany(value => image.FindU32ReferencesInLoadedSegments(value, writableOnly: true)
                .Select(address =>
                {
                    var owner = directTableWords.First(word => word.Value == value);
                    return new HandlerCandidateTargetWord(
                        Address: address,
                        Value: value,
                        Kind: address == owner.Address ? "candidate-table-word-duplicate-self" : "duplicate-opd-word",
                        CandidateBase: owner.CandidateBase,
                        MessageId: owner.MessageId,
                        SlotOffset: owner.SlotOffset,
                        SlotRole: owner.SlotRole,
                        SlotFunctionAddress: owner.SlotFunctionAddress);
                }))
            .DistinctBy(static word => (word.Address, word.Value, word.CandidateBase, word.SlotOffset))
            .ToArray();

        var targetWords = directTableWords
            .Concat(duplicateValueWords)
            .DistinctBy(static word => word.Address)
            .OrderBy(static word => word.Address)
            .ToArray();

        var descriptors = image.FindPpc64OpdTocDescriptors();
        var tocBases = descriptors
            .Select(static descriptor => descriptor.TocBase)
            .Distinct()
            .ToArray();

        var accesses = image.FindPpcR2RelativeDataAccessesToAddresses(
                targetWords.Select(static word => word.Address).Distinct().ToArray(),
                tocBases)
            .Select(access => BuildAccess(access, targetWords))
            .Where(static access => access is not null)
            .Select(static access => access!)
            .ToArray();

        var directAccesses = accesses
            .Where(static access => access.TargetKind.StartsWith("candidate-table-word", StringComparison.Ordinal))
            .ToArray();
        var duplicateAccesses = accesses
            .Where(static access => access.TargetKind == "duplicate-opd-word")
            .ToArray();
        var accessFunctionTargets = accesses
            .Select(static access => access.InstructionAddress)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Take(256)
            .ToArray();

        var report = new Tf2Ps3SourceHandlerCandidateTocAccessReport(
            "tf2ps3-source-handler-candidate-toc-access",
            "Scans TF.elf executable r2/TOC-relative memory instructions for accesses to source-neighborhood registered payload handler candidate vtable words. This targets constructor/setup paths that direct Ghidra reference scans miss.",
            new Tf2Ps3SourceHandlerCandidateTocAccessInputs(elfPath, handlerCandidatesPath),
            new Tf2Ps3SourceHandlerCandidateTocAccessSummary(
                candidates.Length,
                candidates.Select(static candidate => candidate.MessageId).Distinct().Order().ToArray(),
                directTableWords.Length,
                relevantValues.Length,
                duplicateValueWords.Length,
                targetWords.Length,
                descriptors.Length,
                tocBases.Length,
                accesses.Length,
                directAccesses.Length,
                duplicateAccesses.Length,
                accesses.Select(static access => access.InstructionAddress).Distinct().Count()),
            candidates.Select(candidate => new Tf2Ps3SourceHandlerCandidateTocAccessCandidate(
                Hex(candidate.BaseAddress),
                candidate.MessageId,
                candidate.Slots.Length,
                directTableWords.Count(word => word.CandidateBase == candidate.BaseAddress),
                duplicateValueWords.Count(word => word.CandidateBase == candidate.BaseAddress),
                accesses.Count(access => access.CandidateBase == Hex(candidate.BaseAddress)))).ToArray(),
            targetWords.Select(static word => new Tf2Ps3SourceHandlerCandidateTocTargetWord(
                Hex(word.Address),
                Hex(word.Value),
                word.Kind,
                Hex(word.CandidateBase),
                word.MessageId,
                word.SlotOffset,
                word.SlotRole,
                word.SlotFunctionAddress)).ToArray(),
            accesses,
            accessFunctionTargets,
            BuildFindings(candidates, accesses, directAccesses, duplicateAccesses));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceHandlerCandidateTocAccessRow? BuildAccess(
        PpcR2RelativeDataAccess access,
        IReadOnlyCollection<HandlerCandidateTargetWord> targetWords)
    {
        var target = targetWords.FirstOrDefault(word => word.Address == access.TargetDataAddress);
        if (target is null)
        {
            return null;
        }

        return new Tf2Ps3SourceHandlerCandidateTocAccessRow(
            Hex(access.InstructionAddress),
            Hex(access.InstructionWord),
            access.MnemonicFamily,
            access.Displacement,
            Hex(access.TocBase),
            Hex(access.TargetDataAddress),
            target.Kind,
            Hex(target.Value),
            Hex(target.CandidateBase),
            target.MessageId,
            target.SlotOffset,
            target.SlotRole,
            target.SlotFunctionAddress);
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<HandlerCandidate> candidates,
        IReadOnlyCollection<Tf2Ps3SourceHandlerCandidateTocAccessRow> accesses,
        IReadOnlyCollection<Tf2Ps3SourceHandlerCandidateTocAccessRow> directAccesses,
        IReadOnlyCollection<Tf2Ps3SourceHandlerCandidateTocAccessRow> duplicateAccesses)
    {
        var findings = new List<string>
        {
            $"Source-neighborhood structural handler candidates scanned: {candidates.Count}.",
            $"Candidate message ids scanned: {string.Join(", ", candidates.Select(static candidate => candidate.MessageId).Distinct().Order())}.",
            $"Direct candidate-table-word r2/TOC accesses found: {directAccesses.Count}.",
            $"Duplicate OPD-word r2/TOC accesses found: {duplicateAccesses.Count}.",
            $"Total r2/TOC accesses found: {accesses.Count}."
        };

        if (directAccesses.Count > 0)
        {
            findings.Add("Direct candidate table consumers exist and should be exported/decompiled next: "
                + string.Join(", ", directAccesses.Take(12).Select(static access =>
                    $"{access.InstructionAddress}->{access.TargetDataAddress}/id{access.MessageId}/{access.SlotOffset}")));
        }
        else
        {
            findings.Add("No executable r2-relative access reaches the exact source-neighborhood candidate table words.");
        }

        if (duplicateAccesses.Count > 0)
        {
            findings.Add("Duplicate OPD-word accesses may point at shared function tables or constructors; decompile their containing functions before promoting any candidate.");
        }
        else
        {
            findings.Add("No duplicate OPD-word r2-relative access was recovered for the source-neighborhood candidate slots.");
        }

        return findings.ToArray();
    }

    private static HandlerCandidate ReadCandidate(JsonElement candidate)
    {
        return new HandlerCandidate(
            ParseHex(ReadString(candidate, "BaseAddress")),
            candidate.GetProperty("MessageId").GetInt32(),
            candidate.GetProperty("SourcePlayerNeighborhood").GetBoolean(),
            candidate.GetProperty("Slots").EnumerateArray().Select(ReadSlot).ToArray());
    }

    private static HandlerSlot ReadSlot(JsonElement slot)
    {
        return new HandlerSlot(
            ReadString(slot, "SlotOffset"),
            ReadString(slot, "Role"),
            ParseHex(ReadString(slot, "OpdAddress")),
            ReadString(slot, "FunctionAddress"));
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static uint ParseHex(string value)
    {
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return text.Length == 0 ? 0 : Convert.ToUInt32(text, 16);
    }

    private static uint ParseOffset(string value)
    {
        return ParseHex(value);
    }

    private static string Hex(uint value) => "0x" + value.ToString("x8");

    private sealed record HandlerCandidate(uint BaseAddress, int MessageId, bool SourcePlayerNeighborhood, HandlerSlot[] Slots);
    private sealed record HandlerSlot(string SlotOffset, string Role, uint OpdAddress, string FunctionAddress);
    private sealed record HandlerCandidateTargetWord(
        uint Address,
        uint Value,
        string Kind,
        uint CandidateBase,
        int MessageId,
        string SlotOffset,
        string SlotRole,
        string SlotFunctionAddress);
}

public sealed record Tf2Ps3SourceHandlerCandidateTocAccessReport(
    string Status,
    string Note,
    Tf2Ps3SourceHandlerCandidateTocAccessInputs Inputs,
    Tf2Ps3SourceHandlerCandidateTocAccessSummary Summary,
    Tf2Ps3SourceHandlerCandidateTocAccessCandidate[] Candidates,
    Tf2Ps3SourceHandlerCandidateTocTargetWord[] TargetWords,
    Tf2Ps3SourceHandlerCandidateTocAccessRow[] Accesses,
    string[] AccessInstructionTargets,
    string[] Findings);

public sealed record Tf2Ps3SourceHandlerCandidateTocAccessInputs(
    string Elf,
    string HandlerCandidates);

public sealed record Tf2Ps3SourceHandlerCandidateTocAccessSummary(
    int SourceNeighborhoodCandidateCount,
    int[] SourceNeighborhoodMessageIds,
    int DirectTableWordCount,
    int RelevantOpdValueCount,
    int DuplicateOpdWordCount,
    int TargetDataWordCount,
    int OpdDescriptorCount,
    int UniqueTocBaseCount,
    int R2RelativeAccessCount,
    int DirectTableWordR2RelativeAccessCount,
    int DuplicateOpdWordR2RelativeAccessCount,
    int UniqueInstructionHitCount);

public sealed record Tf2Ps3SourceHandlerCandidateTocAccessCandidate(
    string BaseAddress,
    int MessageId,
    int SlotCount,
    int DirectTableWordCount,
    int DuplicateOpdWordCount,
    int R2RelativeAccessCount);

public sealed record Tf2Ps3SourceHandlerCandidateTocTargetWord(
    string Address,
    string Value,
    string Kind,
    string CandidateBase,
    int MessageId,
    string SlotOffset,
    string SlotRole,
    string SlotFunctionAddress);

public sealed record Tf2Ps3SourceHandlerCandidateTocAccessRow(
    string InstructionAddress,
    string InstructionWord,
    string MnemonicFamily,
    int Displacement,
    string TocBase,
    string TargetDataAddress,
    string TargetKind,
    string TargetValue,
    string CandidateBase,
    int MessageId,
    string SlotOffset,
    string SlotRole,
    string SlotFunctionAddress);
