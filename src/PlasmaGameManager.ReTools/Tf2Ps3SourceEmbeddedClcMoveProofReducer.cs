using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceEmbeddedClcMoveProofReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceEmbeddedClcMoveProofReport> ReduceAsync(
        string cExportPath,
        string embeddedCandidatesPath,
        string ownerCallbackDispatchPath,
        string helperSliceReceiveSiblingsPath,
        string recvBitreaderCensusPath,
        string outputPath)
    {
        using var candidatesDoc = JsonDocument.Parse(await File.ReadAllTextAsync(embeddedCandidatesPath));
        using var ownerDispatchDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerCallbackDispatchPath));
        using var helperSiblingsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceReceiveSiblingsPath));
        using var recvCensusDoc = JsonDocument.Parse(await File.ReadAllTextAsync(recvBitreaderCensusPath));

        var cExportText = File.Exists(cExportPath)
            ? await File.ReadAllTextAsync(cExportPath)
            : "";
        var candidateSummary = candidatesDoc.RootElement.GetProperty("Summary");
        var ownerSummary = ownerDispatchDoc.RootElement.GetProperty("Summary");
        var helperSummary = helperSiblingsDoc.RootElement.GetProperty("Summary");
        var recvSummary = recvCensusDoc.RootElement.GetProperty("Summary");

        var candidates = candidatesDoc.RootElement.GetProperty("Candidates")
            .EnumerateArray()
            .Select(ReadCandidate)
            .ToArray();
        var hardMarkerlessCandidates = candidates
            .Where(static candidate => candidate.IsHardMarkerless)
            .OrderBy(static candidate => candidate.File, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.SourceStep)
            .ToArray();
        var uniqueOffsets = candidates
            .Select(static candidate => candidate.EmbeddedOffset)
            .Distinct()
            .Order()
            .ToArray();
        var hardMarkerlessOffsets = hardMarkerlessCandidates
            .Select(static candidate => candidate.EmbeddedOffset)
            .Distinct()
            .Order()
            .ToArray();
        var offsetReferences = uniqueOffsets
            .Select(offset => BuildOffsetReference(cExportText, offset))
            .ToArray();
        var hardOffsetReferenceCount = offsetReferences
            .Where(reference => hardMarkerlessOffsets.Contains(reference.Offset))
            .Sum(static reference => reference.TotalReferenceCount);
        var hardInteriorSliceCount = hardMarkerlessCandidates
            .Count(static candidate => candidate.IsInteriorEmbeddedSlice);
        var hardPlayerStateServerNeighborCount = hardMarkerlessCandidates
            .Count(static candidate => string.Equals(candidate.PreviousServerSemanticRole, "PlayerStateLinkBatch", StringComparison.Ordinal)
                && string.Equals(candidate.NextServerSemanticRole, "PlayerStateLinkBatch", StringComparison.Ordinal));

        var ownerBuildsParam2 = ReadBool(ownerSummary, "OwnerCallbackPathConstructsParam2");
        var helperBuildsParam2 = ReadBool(helperSummary, "SiblingPathConstructsParam2");
        var recvHasHardCandidate = ReadInt(recvSummary, "HardMarkerlessRecvBitreaderCandidateCount") > 0;
        var exactBoundaryCount = ReadInt(candidateSummary, "ExactBoundaryCandidateCount");
        var nativeBoundaryReady = ReadBool(candidateSummary, "NativeBoundaryReady")
            && exactBoundaryCount > 0
            && (ownerBuildsParam2 || helperBuildsParam2 || recvHasHardCandidate);

        var gates = new[]
        {
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "hard-markerless-embedded-candidates-listed",
                hardMarkerlessCandidates.Length > 0 ? "proven-lead" : "not-present",
                embeddedCandidatesPath,
                "The PCAP scan has hard-markerless packets with embedded CLC_Move-looking slices, but these are weak leads only."),
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "exact-native-boundary-present",
                exactBoundaryCount > 0 ? "proven" : "missing",
                embeddedCandidatesPath,
                "No candidate currently matches a recovered native boundary from Ps3SourceNativeToClcMoveBoundaryResolver."),
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "owner-callback-builds-offset-rule",
                ownerBuildsParam2 ? "proven" : "ruled-out-currently",
                ownerCallbackDispatchPath,
                "Owner callbacks bracket dispatch but do not construct the embedded CLC_Move offset/bitreader rule."),
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "helper-sibling-builds-offset-rule",
                helperBuildsParam2 ? "proven" : "ruled-out-currently",
                helperSliceReceiveSiblingsPath,
                "Helper receive siblings prove slot +0x70 consumes caller-built bitreaders but do not construct the embedded offset rule."),
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "visible-recv-path-builds-offset-rule",
                recvHasHardCandidate ? "proven" : "ruled-out-currently",
                recvBitreaderCensusPath,
                "The visible recv-to-bitreader census does not expose a hard-markerless constructor for these offset hits."),
            new Tf2Ps3SourceEmbeddedClcMoveProofGate(
                "native-embedded-clc-move-boundary-ready",
                nativeBoundaryReady ? "proven" : "missing",
                "server implementation gate",
                "The embedded CLC_Move hits cannot be used by production until a TF.elf/server.dll-backed offset rule is recovered and implemented.")
        };

        var report = new Tf2Ps3SourceEmbeddedClcMoveProofReport(
            "tf2ps3-source-embedded-clc-move-proof-worklist",
            "Turns weak embedded CLC_Move offset-scan hits into a strict reverse-engineering worklist. The report intentionally does not promote these hits to native input without a TF.elf/server.dll offset rule.",
            new Tf2Ps3SourceEmbeddedClcMoveProofInputs(
                cExportPath,
                embeddedCandidatesPath,
                ownerCallbackDispatchPath,
                helperSliceReceiveSiblingsPath,
                recvBitreaderCensusPath),
            new Tf2Ps3SourceEmbeddedClcMoveProofSummary(
                ReadInt(candidateSummary, "EmbeddedCandidateCount"),
                hardMarkerlessCandidates.Length,
                exactBoundaryCount,
                uniqueOffsets,
                hardMarkerlessOffsets,
                hardOffsetReferenceCount,
                hardInteriorSliceCount,
                hardPlayerStateServerNeighborCount,
                ownerBuildsParam2,
                helperBuildsParam2,
                recvHasHardCandidate,
                nativeBoundaryReady,
                gates.Count(static gate => gate.Status is "missing" or "ruled-out-currently")),
            offsetReferences,
            hardMarkerlessCandidates,
            gates,
            [
                "The hard-markerless embedded hits are only offset-scan leads. They do not explain the corpus because the same resolver has zero exact CLC_Move boundary matches.",
                "The hard-markerless hits are interior slices bracketed by PlayerStateLinkBatch server traffic, so they are steady-state input leads rather than proof of a map-load packet boundary.",
                "TF.elf evidence currently says owner callbacks and helper receive siblings observe/consume dispatch, while the visible recv census has no hard-markerless builder.",
                "The next productive RE target is the indirect transform or caller-side object builder before the visible 00a5d9e0 param_2 consumer, not accepting the offset hits as packets."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceEmbeddedClcMoveProofCandidate ReadCandidate(JsonElement element)
    {
        return new Tf2Ps3SourceEmbeddedClcMoveProofCandidate(
            ReadString(element, "File"),
            ReadInt(element, "SourceStep"),
            ReadInt(element, "PacketIndex"),
            ReadInt(element, "Sequence"),
            ReadInt(element, "SequenceDelta"),
            ReadInt(element, "PayloadLength"),
            ReadInt(element, "BodyLength"),
            ReadString(element, "Role"),
            ReadBool(element, "IsHardMarkerless"),
            ReadString(element, "NativeFrameKind"),
            ReadString(element, "ExactBoundaryKind"),
            ReadInt(element, "EmbeddedOffset"),
            ReadBool(element, "IncludesMessageType"),
            ReadInt(element, "NewCommands"),
            ReadInt(element, "BackupCommands"),
            ReadInt(element, "CommandDataBitCount"),
            ReadInt(element, "TotalBitsConsumed"),
            ReadString(element, "BodyPrefixHex"),
            ReadString(element, "BodySuffixHex"),
            ReadString(element, "CandidateWindowHex"),
            ReadNeighborString(element, "PreviousClientPacket", "SemanticRole"),
            ReadNeighborString(element, "NextClientPacket", "SemanticRole"),
            ReadNeighborString(element, "PreviousServerPacket", "SemanticRole"),
            ReadNeighborString(element, "NextServerPacket", "SemanticRole"),
            ReadNeighborString(element, "PreviousClientPacket", "BodyPrefixHex"),
            ReadNeighborString(element, "NextClientPacket", "BodyPrefixHex"),
            ReadNeighborString(element, "PreviousServerPacket", "BodyPrefixHex"),
            ReadNeighborString(element, "NextServerPacket", "BodyPrefixHex"),
            ReadInt(element, "EmbeddedOffset"),
            EmbeddedByteLength(ReadInt(element, "TotalBitsConsumed")),
            Math.Max(0, ReadInt(element, "BodyLength") - ReadInt(element, "EmbeddedOffset") - EmbeddedByteLength(ReadInt(element, "TotalBitsConsumed"))),
            ReadInt(element, "EmbeddedOffset") > 0
                && Math.Max(0, ReadInt(element, "BodyLength") - ReadInt(element, "EmbeddedOffset") - EmbeddedByteLength(ReadInt(element, "TotalBitsConsumed"))) > 0,
            "weak-offset-hit-no-tfelf-rule");
    }

    private static int EmbeddedByteLength(int totalBitsConsumed)
    {
        return checked((totalBitsConsumed + 7) / 8);
    }

    private static Tf2Ps3SourceEmbeddedClcMoveProofOffsetReference BuildOffsetReference(string cExportText, int offset)
    {
        var hex = $"0x{offset:x}";
        var decimalPattern = $@"(?:\+|-)\s*{offset}(?![0-9])";
        var hexPattern = $@"(?:\+|-)\s*{Regex.Escape(hex)}(?![0-9a-fA-F])";
        var decimalCount = Regex.Matches(cExportText, decimalPattern).Count;
        var hexCount = Regex.Matches(cExportText, hexPattern, RegexOptions.IgnoreCase).Count;
        return new Tf2Ps3SourceEmbeddedClcMoveProofOffsetReference(
            offset,
            hex,
            decimalCount,
            hexCount,
            decimalCount + hexCount,
            decimalCount + hexCount > 0
                ? "literal appears in C export but is not proof without a matching dataflow/callsite"
                : "no literal offset reference in C export");
    }

    private static int ReadInt(JsonElement? element, string propertyName)
    {
        if (element is null || !element.Value.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool ReadBool(JsonElement? element, string propertyName)
    {
        return element is not null
            && element.Value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static string ReadNeighborString(JsonElement element, string neighborPropertyName, string propertyName)
    {
        return element.TryGetProperty(neighborPropertyName, out var neighbor)
            && neighbor.ValueKind == JsonValueKind.Object
            && neighbor.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }
}

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofReport(
    string Status,
    string Note,
    Tf2Ps3SourceEmbeddedClcMoveProofInputs Inputs,
    Tf2Ps3SourceEmbeddedClcMoveProofSummary Summary,
    Tf2Ps3SourceEmbeddedClcMoveProofOffsetReference[] OffsetReferences,
    Tf2Ps3SourceEmbeddedClcMoveProofCandidate[] HardMarkerlessCandidates,
    Tf2Ps3SourceEmbeddedClcMoveProofGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofInputs(
    string CExportInput,
    string EmbeddedCandidatesReport,
    string OwnerCallbackDispatchReport,
    string HelperSliceReceiveSiblingsReport,
    string RecvBitreaderCensusReport);

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofSummary(
    int EmbeddedCandidateCount,
    int HardMarkerlessCandidateCount,
    int ExactBoundaryCandidateCount,
    int[] UniqueEmbeddedOffsets,
    int[] HardMarkerlessEmbeddedOffsets,
    int HardMarkerlessTfElfLiteralOffsetReferenceCount,
    int HardMarkerlessInteriorSliceCount,
    int HardMarkerlessPlayerStateServerNeighborCount,
    bool OwnerCallbackPathConstructsParam2,
    bool HelperSiblingPathConstructsParam2,
    bool VisibleRecvPathHasHardMarkerlessCandidate,
    bool NativeBoundaryReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofOffsetReference(
    int Offset,
    string HexOffset,
    int DecimalReferenceCount,
    int HexReferenceCount,
    int TotalReferenceCount,
    string Meaning);

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofCandidate(
    string File,
    int SourceStep,
    int PacketIndex,
    int Sequence,
    int SequenceDelta,
    int PayloadLength,
    int BodyLength,
    string Role,
    bool IsHardMarkerless,
    string NativeFrameKind,
    string ExactBoundaryKind,
    int EmbeddedOffset,
    bool IncludesMessageType,
    int NewCommands,
    int BackupCommands,
    int CommandDataBitCount,
    int TotalBitsConsumed,
    string BodyPrefixHex,
    string BodySuffixHex,
    string CandidateWindowHex,
    string PreviousClientSemanticRole,
    string NextClientSemanticRole,
    string PreviousServerSemanticRole,
    string NextServerSemanticRole,
    string PreviousClientPrefixHex,
    string NextClientPrefixHex,
    string PreviousServerPrefixHex,
    string NextServerPrefixHex,
    int LeadingBytesBeforeEmbeddedSlice,
    int EmbeddedByteLength,
    int TrailingBytesAfterEmbeddedSlice,
    bool IsInteriorEmbeddedSlice,
    string ProofStatus);

public sealed record Tf2Ps3SourceEmbeddedClcMoveProofGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
