using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceNetchanStaticAnchorReducer
{
    private const uint AnchorStart = 0x01997b00;
    private const uint AnchorEndExclusive = 0x01997c00;
    private const uint AdjacentTableStart = 0x0180cbc0;
    private const uint AdjacentTableEndExclusive = 0x0180ce50;
    private const uint HelperSliceBase = 0x0180c9a0;
    private const uint AdjacentTableBase = 0x0180cbc0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string elfPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var anchorWords = BuildWords(image, AnchorStart, AnchorEndExclusive);
        var adjacentWords = BuildWords(image, AdjacentTableStart, AdjacentTableEndExclusive);
        var helperReferences = anchorWords
            .Where(static word => word.Value == Hex(HelperSliceBase))
            .ToArray();
        var adjacentReferences = anchorWords
            .Where(static word => word.Value == Hex(AdjacentTableBase))
            .ToArray();
        var report = new Tf2Ps3SourceNetchanStaticAnchorReport(
            "tf2ps3-source-netchan-static-anchor",
            "Classifies the raw helper-slice base reference at 01997b5c. The surrounding block is Source CNetChan/netchannel static data and cvar/string descriptors, with an adjacent RTTI/vtable-style table nearby; this makes the helper-slice reference useful CNetChan evidence but not proof of concrete registered message-handler constructors.",
            elfPath,
            new Tf2Ps3SourceNetchanStaticAnchorSummary(
                Hex(AnchorStart),
                Hex(AnchorEndExclusive),
                anchorWords.Length,
                anchorWords.Count(static word => word.Annotation is "read-only-data-pointer" or "writable-data-pointer" or "executable-segment-value-not-yet-confirmed-function"),
                anchorWords.Count(static word => word.StringValue.Length > 0),
                anchorWords.Count(static word => word.Role == "cnetchan-string-pointer"),
                helperReferences.Length,
                helperReferences.FirstOrDefault()?.Address ?? "",
                adjacentReferences.Length,
                adjacentReferences.FirstOrDefault()?.Address ?? "",
                Hex(AdjacentTableStart),
                Hex(AdjacentTableEndExclusive),
                adjacentWords.Count(static word => word.StringValue.Length > 0),
                adjacentWords.Count(static word => word.Role == "adjacent-rtti-string-pointer"),
                0),
            anchorWords,
            adjacentWords,
            BuildFindings(anchorWords, adjacentWords, helperReferences, adjacentReferences));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceNetchanStaticAnchorWord[] BuildWords(
        Elf64BigEndianImage image,
        uint start,
        uint endExclusive)
    {
        var words = new List<Tf2Ps3SourceNetchanStaticAnchorWord>();
        for (var address = start; address < endExclusive; address += 4)
        {
            if (!image.TryReadU32(address, out var value))
            {
                continue;
            }

            image.TryReadAsciiString(value, 96, out var stringValue);
            words.Add(new Tf2Ps3SourceNetchanStaticAnchorWord(
                Hex(address),
                Hex(address - start),
                Hex(value),
                image.AnnotatePointer(value),
                stringValue,
                ClassifyWord(address, value, stringValue),
                EvidenceTokens(address, value, stringValue)));
        }

        return words.ToArray();
    }

    private static string ClassifyWord(uint address, uint value, string stringValue)
    {
        if (value == HelperSliceBase)
        {
            return "cnetchan-helper-slice-base-reference";
        }

        if (value == AdjacentTableBase)
        {
            return "adjacent-rtti-vtable-table-reference";
        }

        if (IsCNetChanString(stringValue))
        {
            return "cnetchan-string-pointer";
        }

        if (IsAdjacentRttiString(stringValue))
        {
            return "adjacent-rtti-string-pointer";
        }

        if (LooksLikeFloat(value))
        {
            return "float-literal-or-cvar-default";
        }

        if (stringValue.Length > 0)
        {
            return "string-pointer";
        }

        if (value == 0)
        {
            return "zero";
        }

        return "unclassified-word";
    }

    private static string[] EvidenceTokens(uint address, uint value, string stringValue)
    {
        var tokens = new List<string>();
        if (value == HelperSliceBase)
        {
            tokens.Add("points-to-source-helper-slice-base");
            tokens.Add("same-block-as-cnetchan-strings");
        }

        if (value == AdjacentTableBase)
        {
            tokens.Add("points-to-adjacent-vtable-or-rtti-table");
        }

        if (IsCNetChanString(stringValue))
        {
            tokens.Add("cnetchan-or-netchannel-string");
        }

        if (IsAdjacentRttiString(stringValue))
        {
            tokens.Add("adjacent-rtti-or-interface-string");
        }

        if (LooksLikeFloat(value))
        {
            tokens.Add("float-looking-cvar-default");
        }

        if (address == 0x01997b5c)
        {
            tokens.Add("known-raw-helper-base-reference-address");
        }

        return tokens.ToArray();
    }

    private static bool IsCNetChanString(string value)
    {
        return value.Contains("CNetChan", StringComparison.Ordinal)
            || value.Contains("netchan", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("net_", StringComparison.Ordinal)
            || value is "outAcknHeader" or "outDataHeader" or "inDataHeader"
            || value.Contains("net message", StringComparison.Ordinal)
            || value.Contains("NetChannel", StringComparison.Ordinal);
    }

    private static bool IsAdjacentRttiString(string value)
    {
        return value.Contains("IFileReadBinary", StringComparison.Ordinal)
            || value.Contains("COM_IOReadBinary", StringComparison.Ordinal)
            || value.Contains("AudioDevice", StringComparison.Ordinal);
    }

    private static bool LooksLikeFloat(uint value)
    {
        var exponent = value & 0x7f80_0000;
        return exponent is >= 0x3e00_0000 and <= 0x4500_0000
            && (value & 0x7f80_0000) != 0x7f80_0000
            && value >= 0x3d00_0000;
    }

    private static string[] BuildFindings(
        IReadOnlyCollection<Tf2Ps3SourceNetchanStaticAnchorWord> anchorWords,
        IReadOnlyCollection<Tf2Ps3SourceNetchanStaticAnchorWord> adjacentWords,
        IReadOnlyCollection<Tf2Ps3SourceNetchanStaticAnchorWord> helperReferences,
        IReadOnlyCollection<Tf2Ps3SourceNetchanStaticAnchorWord> adjacentReferences)
    {
        var findings = new List<string>
        {
            "The raw helper-slice base reference is word 01997b5c -> 0180c9a0 inside the 01997b00 static block.",
            "The same block contains direct CNetChan/netchannel evidence including outAcknHeader, outDataHeader, net_showudp, net_showmsg, net_showfragments, net_blockmsg, net_showdrop, and NetChannel removed.",
            "The nearby 0180cbc0 table has RTTI/interface string evidence such as COM_IOReadBinary, IFileReadBinary, and CAudioDevice*, so the 01997b00 neighborhood is a mixed Source static-data area rather than a clean registered-handler constructor path.",
            "This promotes 0180c9a0 as a native CNetChan/helper slice anchor, but it still does not prove concrete Source payload handler ids or map-load parser contracts."
        };

        if (helperReferences.Count != 1)
        {
            findings.Add("Unexpected helper-slice reference count; inspect the anchor words before using this report as a negative constructor proof.");
        }

        if (adjacentReferences.Count == 0)
        {
            findings.Add("No adjacent 0180cbc0 reference was found in the anchor block; the static-neighborhood classification may need updating.");
        }

        if (anchorWords.Count(static word => word.Role == "cnetchan-string-pointer") < 10)
        {
            findings.Add("CNetChan/netchannel string evidence is weaker than expected; rerun with the exact BLES00153 v01.10 TF.elf.");
        }

        if (adjacentWords.Count(static word => word.Role == "adjacent-rtti-string-pointer") < 3)
        {
            findings.Add("Adjacent RTTI/interface string evidence is weaker than expected; inspect 0180cbc0 manually.");
        }

        return findings.ToArray();
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }
}

public sealed record Tf2Ps3SourceNetchanStaticAnchorReport(
    string Status,
    string Note,
    string ElfInput,
    Tf2Ps3SourceNetchanStaticAnchorSummary Summary,
    Tf2Ps3SourceNetchanStaticAnchorWord[] AnchorWords,
    Tf2Ps3SourceNetchanStaticAnchorWord[] AdjacentTableWords,
    string[] Findings);

public sealed record Tf2Ps3SourceNetchanStaticAnchorSummary(
    string AnchorStart,
    string AnchorEndExclusive,
    int AnchorWordCount,
    int LoadedPointerWordCount,
    int StringPointerWordCount,
    int CNetChanStringPointerCount,
    int HelperSliceBaseReferenceCount,
    string HelperSliceBaseReferenceAddress,
    int AdjacentTableReferenceCount,
    string AdjacentTableReferenceAddress,
    string AdjacentTableStart,
    string AdjacentTableEndExclusive,
    int AdjacentTableStringPointerCount,
    int AdjacentRttiStringPointerCount,
    int ConstructorPathProofCount);

public sealed record Tf2Ps3SourceNetchanStaticAnchorWord(
    string Address,
    string Offset,
    string Value,
    string Annotation,
    string StringValue,
    string Role,
    string[] EvidenceTokens);
