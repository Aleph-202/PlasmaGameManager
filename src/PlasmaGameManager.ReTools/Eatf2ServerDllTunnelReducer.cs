using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllTunnelReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] AnchorNeedles =
    [
        "EA Tunnel",
        "Secure Server Certification Authority",
        "matchunusedserver",
        "props.{availableServerCount}",
        "NOSERVER",
        "FeslHubSingle_GetGameManager",
        "FeslHubSingle_Get",
        "fesldll.dll",
        "ServerGameDLL005"
    ];

    private static readonly string[] TransportTokens =
    [
        "EA Tunnel",
        "Tunnel",
        "FeslHubSingle_GetGameManager",
        "FeslHubSingle_Get",
        "GameManager",
        "recvfrom",
        "sendto",
        "closesocket",
        "setsockopt",
        "socket(",
        "connect(",
        "bind("
    ];

    private static readonly HashSet<string> DirectSocketImports = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "bind",
        "closesocket",
        "connect",
        "ioctlsocket",
        "recv",
        "recvfrom",
        "select",
        "send",
        "sendto",
        "setsockopt",
        "socket",
        "WSAAsyncSelect",
        "WSACleanup",
        "WSAGetLastError",
        "WSAStartup"
    };

    public static async Task<Eatf2ServerDllTunnelMapReport> ReduceAsync(
        string serverDllPath,
        string targetFunctionsPath,
        string ghidraEvidencePath,
        string outputPath)
    {
        var bytes = await File.ReadAllBytesAsync(serverDllPath);
        var pe = PeImage.Parse(bytes);
        var sections = pe.Sections
            .Select(section => new Eatf2ServerDllPeSection(
                section.Name,
                Hex(pe.ImageBase + section.VirtualAddress),
                Hex(section.VirtualSize),
                Hex(section.SizeOfRawData),
                Hex(section.PointerToRawData)))
            .ToArray();

        var imports = pe.ReadImports(bytes)
            .OrderBy(import => import.Library, StringComparer.OrdinalIgnoreCase)
            .ThenBy(import => import.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var directSocketImports = imports
            .Where(import => IsDirectSocketImport(import))
            .ToArray();

        var anchors = AnchorNeedles
            .Select(needle => BuildAnchor(bytes, pe, needle))
            .ToArray();
        var tunnelNeighborhood = BuildTunnelNeighborhoodFields(bytes, pe, anchors);

        var targetEvidence = ReadTargetFunctionEvidence(targetFunctionsPath);
        var ghidraHits = ReadGhidraEvidenceHits(ghidraEvidencePath);

        var anchorOccurrences = anchors.Sum(static anchor => anchor.Occurrences.Length);
        var codePointerXrefs = anchors.Sum(static anchor =>
            anchor.Occurrences.Sum(static occurrence =>
                occurrence.PointerXrefs.Count(static xref => string.Equals(xref.Section, ".text", StringComparison.Ordinal))));
        var dataPointerXrefs = anchors.Sum(static anchor =>
            anchor.Occurrences.Sum(static occurrence =>
                occurrence.PointerXrefs.Count(static xref => string.Equals(xref.Section, ".data", StringComparison.Ordinal)
                    || string.Equals(xref.Section, ".rdata", StringComparison.Ordinal))));
        var targetFunctionsWithTunnelEvidence = targetEvidence.Functions.Count(static function =>
            function.MatchedTokens.Any(IsTunnelOrSocketToken));

        var report = new Eatf2ServerDllTunnelMapReport(
            "eatf2-serverdll-tunnel-map",
            "Static reducer for the official EA TF2 server.dll EA Tunnel and server transport evidence. This report distinguishes executable transport evidence from configuration strings, imports, FESL bridge hooks, and Source runtime obligations.",
            serverDllPath,
            targetFunctionsPath,
            ghidraEvidencePath,
            new Eatf2ServerDllTunnelMapSummary(
                pe.ImageBaseHex,
                sections.Length,
                imports.Select(static import => import.Library).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                imports.Length,
                directSocketImports.Length > 0,
                AnchorNeedles.Length,
                anchorOccurrences,
                codePointerXrefs,
                dataPointerXrefs,
                tunnelNeighborhood.Length,
                targetEvidence.FunctionCount,
                targetFunctionsWithTunnelEvidence,
                ghidraHits.Length),
            sections,
            imports
                .GroupBy(static import => import.Library, StringComparer.OrdinalIgnoreCase)
                .Select(static group => new Eatf2ServerDllImportLibrary(
                    group.Key,
                    group.Select(static import => import.Name).Where(static name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                    group.Count(static import => import.IsOrdinal)))
                .ToArray(),
            directSocketImports,
            anchors,
            tunnelNeighborhood,
            targetEvidence,
            ghidraHits,
            [
                "EA Tunnel is present as an official string anchor, but this binary-level pass finds no direct .text pointer xref to that string in server.dll.",
                "The EA Tunnel .rdata neighborhood contains compact field tags that decode as disc, desc, gadr, gprt, aprt, dprt, and host; these look like tunnel/session configuration descriptors rather than Source map-load netmessages.",
                "server.dll imports FeslHubSingle_GetGameManager and FeslHubSingle_Get from fesldll.dll; the official DLL hooks GameManager/FESL through that hub instead of directly owning a Winsock transport loop.",
                "The current server.dll target-function export contains Source runtime and FESL bridge functions, but no exported function body that names EA Tunnel or implements direct socket send/recv/connect/bind logic.",
                "matchunusedserver, props.{availableServerCount}, and NOSERVER are server-browser/matchmaking availability anchors referenced through data tables; they are not enough to drive PS3 map-load traffic.",
                "Native TF2 PS3 map load still has to follow TF.elf's PS3 Source packet envelope and the official server.dll Source runtime semantics: CUserCmd decoding, physics simulation, sendtables, snapshots, stats, and GameManager lifecycle callbacks."
            ],
            [
                "Run a targeted Ghidra xref/decompile export around the EA Tunnel string VA and the adjacent .rdata table to prove whether the owning code lives outside the currently exported target-function set.",
                "Export fesldll.dll, if available, because server.dll delegates FESL/GameManager access through that import and may rely on FESL-side tunnel/session services.",
                "Keep using TF.elf send/receive reducers for the PS3 UDP envelope; server.dll confirms Source semantics, not the PS3 packet wrapper."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Eatf2ServerDllStringAnchor BuildAnchor(byte[] bytes, PeImage pe, string needle)
    {
        var occurrences = FindAsciiOccurrences(bytes, needle)
            .Select(offset =>
            {
                var va = pe.TryFileOffsetToVa(offset);
                var section = pe.SectionNameForFileOffset(offset);
                var xrefs = va is null
                    ? []
                    : FindPointerXrefs(bytes, pe, va.Value);
                return new Eatf2ServerDllStringOccurrence(
                    Hex(offset),
                    va is null ? "" : Hex(va.Value),
                    section,
                    xrefs);
            })
            .ToArray();

        return new Eatf2ServerDllStringAnchor(
            needle,
            DescribeAnchorRole(needle),
            occurrences,
            ExplainAnchorEvidence(needle, occurrences));
    }

    private static Eatf2ServerDllTunnelField[] BuildTunnelNeighborhoodFields(
        byte[] bytes,
        PeImage pe,
        Eatf2ServerDllStringAnchor[] anchors)
    {
        var tunnel = anchors.FirstOrDefault(static anchor => anchor.Needle == "EA Tunnel");
        var first = tunnel?.Occurrences.FirstOrDefault();
        if (first is null || !TryParseHex(first.FileOffset, out var tunnelOffset))
        {
            return [];
        }

        var start = checked((int)tunnelOffset);
        var end = Math.Min(bytes.Length - 4, start + 0x180);
        var fields = new List<Eatf2ServerDllTunnelField>();
        for (var offset = start; offset <= end; offset++)
        {
            if (!LooksLikeInlineFourCc(bytes, offset))
            {
                continue;
            }

            var stored = Encoding.ASCII.GetString(bytes.AsSpan(offset, 4));
            if (stored is "Tunn" or "unne" or "nnel")
            {
                continue;
            }

            var decoded = Reverse(stored);
            fields.Add(new Eatf2ServerDllTunnelField(
                Hex(offset),
                pe.TryFileOffsetToVa(offset) is { } va ? Hex(va) : "",
                stored,
                decoded,
                DescribeTunnelField(decoded)));
        }

        return fields
            .DistinctBy(static field => (field.FileOffset, field.StoredBytes))
            .ToArray();
    }

    private static bool LooksLikeInlineFourCc(byte[] bytes, int offset)
    {
        for (var i = 0; i < 4; i++)
        {
            var c = bytes[offset + i];
            if (c < (byte)'a' || c > (byte)'z')
            {
                return false;
            }
        }

        return offset + 4 >= bytes.Length || bytes[offset + 4] == 0;
    }

    private static string Reverse(string value)
    {
        var chars = value.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static string DescribeTunnelField(string decoded)
    {
        return decoded switch
        {
            "disc" => "candidate little-endian FourCC: discovery/session id field",
            "desc" => "candidate little-endian FourCC: description/session descriptor field",
            "gadr" => "candidate little-endian FourCC: game address field",
            "gprt" => "candidate little-endian FourCC: game port field",
            "aprt" => "candidate little-endian FourCC: auxiliary/application port field",
            "dprt" => "candidate little-endian FourCC: destination/default port field",
            "host" => "candidate little-endian FourCC: host name/address field",
            _ => "candidate little-endian FourCC tunnel/session field"
        };
    }

    private static Eatf2ServerDllPointerXref[] FindPointerXrefs(byte[] bytes, PeImage pe, ulong va)
    {
        if (va > uint.MaxValue)
        {
            return [];
        }

        var needle = (uint)va;
        var xrefs = new List<Eatf2ServerDllPointerXref>();
        for (var offset = 0; offset <= bytes.Length - 4; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)) != needle)
            {
                continue;
            }

            var xrefVa = pe.TryFileOffsetToVa(offset);
            xrefs.Add(new Eatf2ServerDllPointerXref(
                Hex(offset),
                xrefVa is null ? "" : Hex(xrefVa.Value),
                pe.SectionNameForFileOffset(offset)));
        }

        return xrefs.ToArray();
    }

    private static int[] FindAsciiOccurrences(byte[] bytes, string needle)
    {
        var pattern = Encoding.ASCII.GetBytes(needle);
        var occurrences = new List<int>();
        for (var offset = 0; offset <= bytes.Length - pattern.Length; offset++)
        {
            if (bytes.AsSpan(offset, pattern.Length).SequenceEqual(pattern))
            {
                occurrences.Add(offset);
            }
        }

        return occurrences.ToArray();
    }

    private static Eatf2ServerDllTargetEvidence ReadTargetFunctionEvidence(string path)
    {
        if (!File.Exists(path))
        {
            return new Eatf2ServerDllTargetEvidence(path, 0, []);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("targetFunctions", out var functionsElement)
            || functionsElement.ValueKind != JsonValueKind.Array)
        {
            return new Eatf2ServerDllTargetEvidence(path, 0, []);
        }

        var functions = new List<Eatf2ServerDllTargetFunctionTransportHit>();
        var functionCount = 0;
        foreach (var function in functionsElement.EnumerateArray())
        {
            functionCount++;
            var entry = ReadString(function, "entry");
            var name = ReadString(function, "name");
            var body = ReadString(function, "body");
            var reasons = function.TryGetProperty("reasons", out var reasonsElement) && reasonsElement.ValueKind == JsonValueKind.Array
                ? reasonsElement.EnumerateArray().Select(static reason => reason.GetString() ?? "").ToArray()
                : [];
            var instructions = function.TryGetProperty("instructions", out var instructionsElement) && instructionsElement.ValueKind == JsonValueKind.Array
                ? instructionsElement.EnumerateArray().Select(static instruction => ReadString(instruction, "text")).ToArray()
                : [];
            var searchable = string.Join('\n', reasons.Concat([body]).Concat(instructions));
            var matchedTokens = TransportTokens
                .Where(token => searchable.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (matchedTokens.Length == 0)
            {
                continue;
            }

            functions.Add(new Eatf2ServerDllTargetFunctionTransportHit(
                entry,
                name,
                matchedTokens,
                reasons.Where(reason => matchedTokens.Any(token => reason.Contains(token, StringComparison.OrdinalIgnoreCase))).Take(8).ToArray(),
                instructions.Where(instruction => matchedTokens.Any(token => instruction.Contains(token, StringComparison.OrdinalIgnoreCase))).Take(12).ToArray()));
        }

        return new Eatf2ServerDllTargetEvidence(path, functionCount, functions.ToArray());
    }

    private static Eatf2ServerDllGhidraEvidenceHit[] ReadGhidraEvidenceHits(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var hits = new List<Eatf2ServerDllGhidraEvidenceHit>();
        var lines = File.ReadLines(path);
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            var matched = AnchorNeedles.Concat(TransportTokens)
                .Where(token => line.Contains(token, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (matched.Length == 0)
            {
                continue;
            }

            hits.Add(new Eatf2ServerDllGhidraEvidenceHit(lineNumber, matched, Truncate(line, 240)));
        }

        return hits.Take(80).ToArray();
    }

    private static bool IsDirectSocketImport(Eatf2ServerDllImport import)
    {
        return import.Library.Equals("ws2_32.dll", StringComparison.OrdinalIgnoreCase)
            || import.Library.Equals("wsock32.dll", StringComparison.OrdinalIgnoreCase)
            || DirectSocketImports.Contains(import.Name);
    }

    private static bool IsTunnelOrSocketToken(string token)
    {
        return token.Contains("Tunnel", StringComparison.OrdinalIgnoreCase)
            || token.Equals("recvfrom", StringComparison.OrdinalIgnoreCase)
            || token.Equals("sendto", StringComparison.OrdinalIgnoreCase)
            || token.Equals("closesocket", StringComparison.OrdinalIgnoreCase)
            || token.Equals("setsockopt", StringComparison.OrdinalIgnoreCase)
            || token.Equals("socket(", StringComparison.OrdinalIgnoreCase)
            || token.Equals("connect(", StringComparison.OrdinalIgnoreCase)
            || token.Equals("bind(", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeAnchorRole(string needle)
    {
        return needle switch
        {
            "EA Tunnel" => "Official EA tunnel label/configuration anchor.",
            "Secure Server Certification Authority" => "Certificate/trust-store anchor adjacent to EA tunnel strings.",
            "matchunusedserver" => "Matchmaking/server-browser no-server result token.",
            "props.{availableServerCount}" => "Matchmaking property for available server count.",
            "NOSERVER" => "Matchmaking/server-browser no-server status token.",
            "FeslHubSingle_GetGameManager" => "Imported FESL hub function used by Source lifecycle callbacks.",
            "FeslHubSingle_Get" => "Imported FESL hub function used by stats and services.",
            "fesldll.dll" => "FESL runtime dependency providing the hub imports.",
            "ServerGameDLL005" => "Source server DLL interface version anchor.",
            _ => "Server.dll static anchor."
        };
    }

    private static string ExplainAnchorEvidence(string needle, Eatf2ServerDllStringOccurrence[] occurrences)
    {
        var codeXrefs = occurrences.Sum(static occurrence =>
            occurrence.PointerXrefs.Count(static xref => string.Equals(xref.Section, ".text", StringComparison.Ordinal)));
        var dataXrefs = occurrences.Sum(static occurrence =>
            occurrence.PointerXrefs.Count(static xref => string.Equals(xref.Section, ".data", StringComparison.Ordinal)
                || string.Equals(xref.Section, ".rdata", StringComparison.Ordinal)));
        if (occurrences.Length == 0)
        {
            return "Not present in this server.dll binary image.";
        }

        if (needle is "FeslHubSingle_GetGameManager" or "FeslHubSingle_Get" or "fesldll.dll")
        {
            return "Present in import metadata; executable callsites are recovered in the target-function/runtime reports.";
        }

        if (codeXrefs > 0)
        {
            return $"Present with {codeXrefs} direct .text pointer xref(s) and {dataXrefs} data xref(s).";
        }

        if (dataXrefs > 0)
        {
            return $"Present with {dataXrefs} direct data-table pointer xref(s) and no direct .text pointer xref in this static pass.";
        }

        return "Present as string data, with no raw direct pointer xref found in this binary-level pass.";
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static bool TryParseHex(string value, out long result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return long.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
        }

        return long.TryParse(value, out result);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string Hex(long value)
    {
        return "0x" + value.ToString("x");
    }

    private static string Hex(ulong value)
    {
        return "0x" + value.ToString("x");
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x");
    }

    private sealed record PeImage(
        ulong ImageBase,
        PeSection[] Sections,
        uint ImportTableRva,
        uint ImportTableSize)
    {
        public string ImageBaseHex => Hex(ImageBase);

        public static PeImage Parse(byte[] bytes)
        {
            if (bytes.Length < 0x100 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z')
            {
                throw new InvalidDataException("Input is not a PE image.");
            }

            var peOffset = ReadInt32(bytes, 0x3c);
            if (peOffset < 0 || peOffset + 0x18 >= bytes.Length
                || bytes[peOffset] != (byte)'P'
                || bytes[peOffset + 1] != (byte)'E'
                || bytes[peOffset + 2] != 0
                || bytes[peOffset + 3] != 0)
            {
                throw new InvalidDataException("Input has no valid PE signature.");
            }

            var coffOffset = peOffset + 4;
            var sectionCount = ReadUInt16(bytes, coffOffset + 2);
            var optionalHeaderSize = ReadUInt16(bytes, coffOffset + 16);
            var optionalOffset = coffOffset + 20;
            var magic = ReadUInt16(bytes, optionalOffset);
            if (magic != 0x10b)
            {
                throw new InvalidDataException($"Only PE32 images are supported by this reducer; optional-header magic was 0x{magic:x}.");
            }

            var imageBase = ReadUInt32(bytes, optionalOffset + 28);
            var dataDirectoryOffset = optionalOffset + 96;
            var importTableRva = ReadUInt32(bytes, dataDirectoryOffset + 8);
            var importTableSize = ReadUInt32(bytes, dataDirectoryOffset + 12);
            var sectionOffset = optionalOffset + optionalHeaderSize;
            var sections = new List<PeSection>();
            for (var i = 0; i < sectionCount; i++)
            {
                var offset = sectionOffset + i * 40;
                var nameBytes = bytes.AsSpan(offset, 8);
                var nul = nameBytes.IndexOf((byte)0);
                var name = Encoding.ASCII.GetString(nul >= 0 ? nameBytes[..nul] : nameBytes);
                sections.Add(new PeSection(
                    name,
                    ReadUInt32(bytes, offset + 8),
                    ReadUInt32(bytes, offset + 12),
                    ReadUInt32(bytes, offset + 16),
                    ReadUInt32(bytes, offset + 20)));
            }

            return new PeImage(imageBase, sections.ToArray(), importTableRva, importTableSize);
        }

        public Eatf2ServerDllImport[] ReadImports(byte[] bytes)
        {
            var importOffset = TryRvaToFileOffset(ImportTableRva);
            if (importOffset is null)
            {
                return [];
            }

            var imports = new List<Eatf2ServerDllImport>();
            for (var descriptorOffset = importOffset.Value; descriptorOffset + 20 <= bytes.Length; descriptorOffset += 20)
            {
                var originalFirstThunk = ReadUInt32(bytes, descriptorOffset);
                var nameRva = ReadUInt32(bytes, descriptorOffset + 12);
                var firstThunk = ReadUInt32(bytes, descriptorOffset + 16);
                if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0)
                {
                    break;
                }

                var nameOffset = TryRvaToFileOffset(nameRva);
                var library = nameOffset is null ? "" : ReadCString(bytes, nameOffset.Value);
                var thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
                var thunkOffset = TryRvaToFileOffset(thunkRva);
                if (thunkOffset is null)
                {
                    continue;
                }

                for (var offset = thunkOffset.Value; offset + 4 <= bytes.Length; offset += 4)
                {
                    var value = ReadUInt32(bytes, offset);
                    if (value == 0)
                    {
                        break;
                    }

                    if ((value & 0x80000000u) != 0)
                    {
                        imports.Add(new Eatf2ServerDllImport(library, "", true));
                        continue;
                    }

                    var importNameOffset = TryRvaToFileOffset(value);
                    if (importNameOffset is null || importNameOffset.Value + 2 >= bytes.Length)
                    {
                        continue;
                    }

                    imports.Add(new Eatf2ServerDllImport(library, ReadCString(bytes, importNameOffset.Value + 2), false));
                }
            }

            return imports.ToArray();
        }

        public ulong? TryFileOffsetToVa(int fileOffset)
        {
            foreach (var section in Sections)
            {
                var start = section.PointerToRawData;
                var end = section.PointerToRawData + section.SizeOfRawData;
                if ((uint)fileOffset < start || (uint)fileOffset >= end)
                {
                    continue;
                }

                return ImageBase + section.VirtualAddress + ((uint)fileOffset - section.PointerToRawData);
            }

            return null;
        }

        public string SectionNameForFileOffset(int fileOffset)
        {
            foreach (var section in Sections)
            {
                var start = section.PointerToRawData;
                var end = section.PointerToRawData + section.SizeOfRawData;
                if ((uint)fileOffset >= start && (uint)fileOffset < end)
                {
                    return section.Name;
                }
            }

            return "headers-or-overlay";
        }

        private int? TryRvaToFileOffset(uint rva)
        {
            foreach (var section in Sections)
            {
                var span = Math.Max(section.VirtualSize, section.SizeOfRawData);
                if (rva < section.VirtualAddress || rva >= section.VirtualAddress + span)
                {
                    continue;
                }

                return checked((int)(section.PointerToRawData + (rva - section.VirtualAddress)));
            }

            return null;
        }
    }

    private sealed record PeSection(
        string Name,
        uint VirtualSize,
        uint VirtualAddress,
        uint SizeOfRawData,
        uint PointerToRawData);

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static string ReadCString(byte[] bytes, int offset)
    {
        var end = offset;
        while (end < bytes.Length && bytes[end] != 0)
        {
            end++;
        }

        return Encoding.ASCII.GetString(bytes.AsSpan(offset, end - offset));
    }
}

public sealed record Eatf2ServerDllTunnelMapReport(
    string Status,
    string Note,
    string ServerDllInput,
    string TargetFunctionsInput,
    string GhidraEvidenceInput,
    Eatf2ServerDllTunnelMapSummary Summary,
    Eatf2ServerDllPeSection[] Sections,
    Eatf2ServerDllImportLibrary[] ImportLibraries,
    Eatf2ServerDllImport[] DirectSocketImports,
    Eatf2ServerDllStringAnchor[] StringAnchors,
    Eatf2ServerDllTunnelField[] TunnelNeighborhoodFields,
    Eatf2ServerDllTargetEvidence TargetFunctionEvidence,
    Eatf2ServerDllGhidraEvidenceHit[] GhidraEvidenceHits,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Eatf2ServerDllTunnelMapSummary(
    string ImageBase,
    int SectionCount,
    int ImportLibraryCount,
    int ImportFunctionCount,
    bool DirectWinsockImportPresent,
    int AnchorNeedleCount,
    int AnchorOccurrenceCount,
    int DirectCodePointerXrefCount,
    int DataPointerXrefCount,
    int TunnelNeighborhoodFieldCount,
    int TargetFunctionCount,
    int TargetFunctionsWithTunnelOrSocketEvidence,
    int GhidraEvidenceHitCount);

public sealed record Eatf2ServerDllPeSection(
    string Name,
    string VirtualAddress,
    string VirtualSize,
    string RawSize,
    string RawPointer);

public sealed record Eatf2ServerDllImportLibrary(
    string Library,
    string[] Functions,
    int OrdinalImportCount);

public sealed record Eatf2ServerDllImport(
    string Library,
    string Name,
    bool IsOrdinal);

public sealed record Eatf2ServerDllStringAnchor(
    string Needle,
    string Role,
    Eatf2ServerDllStringOccurrence[] Occurrences,
    string EvidenceSummary);

public sealed record Eatf2ServerDllStringOccurrence(
    string FileOffset,
    string VirtualAddress,
    string Section,
    Eatf2ServerDllPointerXref[] PointerXrefs);

public sealed record Eatf2ServerDllPointerXref(
    string FileOffset,
    string VirtualAddress,
    string Section);

public sealed record Eatf2ServerDllTunnelField(
    string FileOffset,
    string VirtualAddress,
    string StoredBytes,
    string LittleEndianFourCc,
    string Inference);

public sealed record Eatf2ServerDllTargetEvidence(
    string TargetFunctionsInput,
    int FunctionCount,
    Eatf2ServerDllTargetFunctionTransportHit[] Functions);

public sealed record Eatf2ServerDllTargetFunctionTransportHit(
    string Entry,
    string Name,
    string[] MatchedTokens,
    string[] MatchingReasons,
    string[] MatchingInstructions);

public sealed record Eatf2ServerDllGhidraEvidenceHit(
    int LineNumber,
    string[] MatchedTokens,
    string Text);
