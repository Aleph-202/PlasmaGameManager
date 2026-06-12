using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllTunnelGhidraReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> KnownTunnelFields = new(StringComparer.Ordinal)
    {
        "disc",
        "desc",
        "gadr",
        "gprt",
        "aprt",
        "dprt",
        "host",
        "extp",
        "sdsc"
    };

    public static async Task<Eatf2ServerDllTunnelGhidraMapReport> ReduceAsync(
        string evidencePath,
        string outputPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
        var root = document.RootElement;

        var anchors = root.GetProperty("anchors").EnumerateArray()
            .Select(ReadAnchor)
            .ToArray();
        var neighborhood = root.GetProperty("tunnelNeighborhood").EnumerateArray()
            .Select(ReadNeighborhoodWord)
            .ToArray();
        var targetFunctions = root.GetProperty("targetFunctions").EnumerateArray()
            .Select(ReadTargetFunction)
            .ToArray();

        var eaTunnel = anchors.FirstOrDefault(static anchor => anchor.Text == "EA Tunnel");
        var knownFields = neighborhood
            .Where(static word => KnownTunnelFields.Contains(word.ReversedAscii))
            .ToArray();
        var uniqueFields = knownFields
            .Select(static word => word.ReversedAscii)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var anchorTextReferences = anchors
            .SelectMany(static anchor => anchor.References)
            .Count(static reference => reference.Block == ".text");
        var textReferencesWithoutFunction = anchors
            .SelectMany(static anchor => anchor.References)
            .Concat(neighborhood.SelectMany(static word => word.References))
            .Count(static reference => reference.Block == ".text" && reference.FunctionEntry.Length == 0);
        var neighborhoodReferences = neighborhood.Sum(static word => word.ReferenceCount);
        var fieldsWithReferences = knownFields.Count(static word => word.ReferenceCount > 0);

        var report = new Eatf2ServerDllTunnelGhidraMapReport(
            "eatf2-serverdll-tunnel-ghidra-map",
            "Targeted Ghidra reduction for the official EA TF2 server.dll EA Tunnel lead. This distinguishes real executable owners from unreferenced tunnel descriptor strings/field tags.",
            evidencePath,
            new Eatf2ServerDllTunnelGhidraSummary(
                ReadString(root, "program"),
                ReadString(root, "imageBase"),
                ReadInt(root, "anchorCount"),
                eaTunnel is null ? 0 : eaTunnel.References.Length,
                anchorTextReferences,
                textReferencesWithoutFunction,
                ReadInt(root, "neighborhoodWordCount"),
                knownFields.Length,
                uniqueFields,
                neighborhoodReferences,
                fieldsWithReferences,
                ReadInt(root, "targetFunctionCount"),
                ReadInt(root, "emittedFunctionCount")),
            anchors,
            knownFields,
            targetFunctions,
            BuildFindings(anchors, eaTunnel, uniqueFields, neighborhoodReferences, targetFunctions),
            [
                "Keep TF.elf as authoritative for the client-visible PS3 Source UDP packet envelope and native receive handlers.",
                "Use server.dll for Source gameplay/server-side obligations: usercmd decode, physics, sendtables, snapshots, lifecycle, and ranked stats.",
                "If available, export fesldll.dll next; server.dll imports FESL/GameManager hub calls, while EA Tunnel itself is only an unreferenced descriptor table in this DLL."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static string[] BuildFindings(
        Eatf2ServerDllTunnelGhidraAnchor[] anchors,
        Eatf2ServerDllTunnelGhidraAnchor? eaTunnel,
        string[] uniqueFields,
        int neighborhoodReferences,
        Eatf2ServerDllTunnelGhidraFunction[] targetFunctions)
    {
        var findings = new List<string>();
        if (eaTunnel is null)
        {
            findings.Add("Ghidra did not recover the EA Tunnel anchor in this export.");
        }
        else if (eaTunnel.References.Length == 0)
        {
            findings.Add("Ghidra confirms EA Tunnel at 0x10402368 has zero direct references in the recovered server.dll program.");
        }
        else
        {
            findings.Add($"Ghidra found {eaTunnel.References.Length} direct reference(s) to EA Tunnel; inspect function/data owners before treating it as transport code.");
        }

        findings.Add($"The adjacent tunnel descriptor table recovers these little-endian field tags: {string.Join(", ", uniqueFields)}.");
        var textReferencedAnchors = anchors
            .Where(static anchor => anchor.References.Any(static reference => reference.Block == ".text"))
            .Select(static anchor => $"{anchor.Text}@{anchor.Address}")
            .ToArray();
        if (textReferencedAnchors.Length > 0)
        {
            findings.Add("The only anchor text reference(s) are outside EA Tunnel: "
                + string.Join(", ", textReferencedAnchors)
                + ". In the current export this is Source interface registration evidence, not tunnel transport evidence.");
        }

        if (neighborhoodReferences == 0)
        {
            findings.Add("No recovered Ghidra references point at the EA Tunnel descriptor neighborhood, so the table is not currently tied to executable transport logic.");
        }
        else
        {
            findings.Add($"The EA Tunnel descriptor neighborhood has {neighborhoodReferences} recovered reference(s); inspect those references before changing transport behavior.");
        }

        if (targetFunctions.Length == 0)
        {
            findings.Add("No decompiled function target was reached from EA Tunnel or its descriptor table; this closes the current server.dll tunnel lead as metadata/configuration evidence, not packet-owner proof.");
        }
        else
        {
            findings.Add($"Ghidra reached {targetFunctions.Length} function target(s) from tunnel evidence; these need manual review.");
        }

        return findings.ToArray();
    }

    private static Eatf2ServerDllTunnelGhidraAnchor ReadAnchor(JsonElement element)
    {
        var references = element.GetProperty("references").EnumerateArray()
            .Select(ReadReference)
            .ToArray();
        return new Eatf2ServerDllTunnelGhidraAnchor(
            ReadString(element, "address"),
            ReadString(element, "block"),
            ReadString(element, "text"),
            references);
    }

    private static Eatf2ServerDllTunnelGhidraWord ReadNeighborhoodWord(JsonElement element)
    {
        var references = element.GetProperty("references").EnumerateArray()
            .Select(ReadReference)
            .ToArray();
        return new Eatf2ServerDllTunnelGhidraWord(
            ReadString(element, "address"),
            ReadString(element, "block"),
            ReadString(element, "dword"),
            ReadString(element, "ascii"),
            ReadString(element, "reversedAscii"),
            ReadBool(element, "looksLikeFieldTag"),
            ReadBool(element, "pointsIntoProgram"),
            ReadString(element, "pointedBlock"),
            ReadInt(element, "referenceCount"),
            references);
    }

    private static Eatf2ServerDllTunnelGhidraReference ReadReference(JsonElement element)
    {
        return new Eatf2ServerDllTunnelGhidraReference(
            ReadString(element, "from"),
            ReadString(element, "block"),
            ReadString(element, "type"),
            ReadString(element, "functionEntry"),
            ReadString(element, "functionName"));
    }

    private static Eatf2ServerDllTunnelGhidraFunction ReadTargetFunction(JsonElement element)
    {
        var reasons = element.TryGetProperty("reasons", out var reasonsElement) && reasonsElement.ValueKind == JsonValueKind.Array
            ? reasonsElement.EnumerateArray().Select(static reason => reason.GetString() ?? "").ToArray()
            : [];
        return new Eatf2ServerDllTunnelGhidraFunction(
            ReadString(element, "entry"),
            ReadString(element, "name"),
            reasons);
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static bool ReadBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }
}

public sealed record Eatf2ServerDllTunnelGhidraMapReport(
    string Status,
    string Note,
    string GhidraEvidenceInput,
    Eatf2ServerDllTunnelGhidraSummary Summary,
    Eatf2ServerDllTunnelGhidraAnchor[] Anchors,
    Eatf2ServerDllTunnelGhidraWord[] KnownTunnelFields,
    Eatf2ServerDllTunnelGhidraFunction[] TargetFunctions,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Eatf2ServerDllTunnelGhidraSummary(
    string Program,
    string ImageBase,
    int AnchorCount,
    int EaTunnelReferenceCount,
    int AnchorTextReferenceCount,
    int TextReferencesWithoutContainingFunction,
    int NeighborhoodWordCount,
    int KnownTunnelFieldCount,
    string[] UniqueKnownTunnelFields,
    int NeighborhoodReferenceCount,
    int KnownTunnelFieldsWithReferences,
    int TargetFunctionCount,
    int EmittedFunctionCount);

public sealed record Eatf2ServerDllTunnelGhidraAnchor(
    string Address,
    string Block,
    string Text,
    Eatf2ServerDllTunnelGhidraReference[] References);

public sealed record Eatf2ServerDllTunnelGhidraWord(
    string Address,
    string Block,
    string Dword,
    string Ascii,
    string ReversedAscii,
    bool LooksLikeFieldTag,
    bool PointsIntoProgram,
    string PointedBlock,
    int ReferenceCount,
    Eatf2ServerDllTunnelGhidraReference[] References);

public sealed record Eatf2ServerDllTunnelGhidraReference(
    string From,
    string Block,
    string Type,
    string FunctionEntry,
    string FunctionName);

public sealed record Eatf2ServerDllTunnelGhidraFunction(
    string Entry,
    string Name,
    string[] Reasons);
