using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3DataNeighborhoodReducer
{
    public static async Task ReduceAsync(string elfPath, string handlerMapPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        using var handlerDoc = JsonDocument.Parse(File.ReadAllText(handlerMapPath));

        var anchors = handlerDoc.RootElement.GetProperty("handlers").EnumerateArray()
            .SelectMany(handler =>
            {
                var role = handler.GetProperty("Role").GetString() ?? "";
                var evidenceKind = handler.GetProperty("EvidenceKind").GetString() ?? "";
                return handler.GetProperty("MatchedStrings").EnumerateArray().Select(s => new StringAnchor(
                    role,
                    evidenceKind,
                    ParseHex(s.GetProperty("Address").GetString() ?? "0"),
                    s.GetProperty("Value").GetString() ?? ""));
            })
            .OrderBy(static anchor => anchor.Address)
            .ToArray();

        var rows = anchors.Select(anchor => BuildAnchor(image, anchor)).ToArray();
        var report = new
        {
            status = "seeded-from-tf2ps3-elf-data-neighborhood",
            note = "Scans TF.elf loaded segments for 32-bit big-endian pointers to GameManager log strings, then emits nearby data words. This narrows pointer-table/jumptable targets without needing a full Ghidra rerun.",
            input = new
            {
                Elf = elfPath,
                HandlerMap = handlerMapPath,
                image.SegmentCount
            },
            summary = new
            {
                StringAnchors = rows.Length,
                AnchorsWithPointerRefs = rows.Count(static row => row.ReferenceCount > 0),
                PointerTableOnlyAnchors = rows.Count(static row => row.EvidenceKind == "pointer-table-evidence"),
                PointerTableOnlyAnchorsWithRefs = rows.Count(static row => row.EvidenceKind == "pointer-table-evidence" && row.ReferenceCount > 0)
            },
            anchors = rows,
            nextTargets = new[]
            {
                "For anchors with table refs, recover the owner object/table around the ref window and identify which adjacent code/data values select the handler.",
                "For anchors with no table refs, use Ghidra dataflow/callsite recovery instead of raw pointer scans; their string pointers may be materialized through TOC-relative code.",
                "Do not treat code-looking values in these windows as functions unless disassembly/decompile confirms a valid instruction stream."
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static AnchorRow BuildAnchor(Elf64BigEndianImage image, StringAnchor anchor)
    {
        var refs = image.FindU32References(anchor.Address)
            .Select(address => new PointerReference(
                Hex(address),
                BuildWindow(image, address)))
            .ToArray();

        return new AnchorRow(
            Role: anchor.Role,
            EvidenceKind: anchor.EvidenceKind,
            StringAddress: Hex(anchor.Address),
            StringValue: anchor.Value,
            ReferenceCount: refs.Length,
            References: refs);
    }

    private static WindowWord[] BuildWindow(Elf64BigEndianImage image, uint referenceAddress)
    {
        var start = referenceAddress >= 0x40 ? referenceAddress - 0x40 : 0;
        start &= 0xffff_fffc;
        var words = new List<WindowWord>();
        for (var address = start; address < start + 0x90; address += 4)
        {
            if (!image.TryReadU32(address, out var value))
            {
                continue;
            }

            words.Add(new WindowWord(
                Address: Hex(address),
                Value: Hex(value),
                Annotation: image.AnnotatePointer(value),
                IsReferenceWord: address == referenceAddress));
        }

        return words.ToArray();
    }

    private static uint ParseHex(string value)
    {
        value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return Convert.ToUInt32(value, 16);
    }

    private static string Hex(uint value) => $"{value:x8}";

    private sealed record StringAnchor(string Role, string EvidenceKind, uint Address, string Value);
    private sealed record AnchorRow(string Role, string EvidenceKind, string StringAddress, string StringValue, int ReferenceCount, PointerReference[] References);
    private sealed record PointerReference(string RefAddress, WindowWord[] Window);
    private sealed record WindowWord(string Address, string Value, string Annotation, bool IsReferenceWord);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}

internal sealed class Elf64BigEndianImage
{
    private readonly byte[] _bytes;
    private readonly Segment[] _segments;

    private Elf64BigEndianImage(byte[] bytes, Segment[] segments)
    {
        _bytes = bytes;
        _segments = segments;
    }

    public int SegmentCount => _segments.Length;

    public static Elf64BigEndianImage Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 0x40 || bytes[0] != 0x7f || bytes[1] != (byte)'E' || bytes[2] != (byte)'L' || bytes[3] != (byte)'F')
        {
            throw new InvalidDataException($"{path} is not an ELF file.");
        }

        if (bytes[4] != 2 || bytes[5] != 2)
        {
            throw new InvalidDataException($"{path} is not a 64-bit big-endian ELF file.");
        }

        var programHeaderOffset = ReadU64(bytes, 0x20);
        var programHeaderEntrySize = ReadU16(bytes, 0x36);
        var programHeaderCount = ReadU16(bytes, 0x38);
        var segments = new List<Segment>();
        for (var i = 0; i < programHeaderCount; i++)
        {
            var offset = checked((int)(programHeaderOffset + (ulong)(i * programHeaderEntrySize)));
            var type = ReadU32(bytes, offset);
            if (type != 1)
            {
                continue;
            }

            var flags = ReadU32(bytes, offset + 4);
            var fileOffset = ReadU64(bytes, offset + 8);
            var virtualAddress = ReadU64(bytes, offset + 0x10);
            var fileSize = ReadU64(bytes, offset + 0x20);
            var memorySize = ReadU64(bytes, offset + 0x28);
            if (fileSize == 0 || memorySize == 0)
            {
                continue;
            }

            segments.Add(new Segment(
                VirtualAddress: checked((uint)virtualAddress),
                FileOffset: checked((uint)fileOffset),
                FileSize: checked((uint)fileSize),
                MemorySize: checked((uint)memorySize),
                Flags: flags));
        }

        return new Elf64BigEndianImage(bytes, segments.ToArray());
    }

    public uint[] FindU32References(uint target)
    {
        var refs = new List<uint>();
        foreach (var segment in _segments.Where(static s => s.IsWritable))
        {
            for (var offset = segment.FileOffset; offset + 4 <= segment.FileOffset + segment.FileSize; offset += 4)
            {
                if (ReadU32(_bytes, checked((int)offset)) == target)
                {
                    refs.Add(segment.VirtualAddress + (offset - segment.FileOffset));
                }
            }
        }

        return refs.Order().ToArray();
    }

    public uint[] FindU32ReferencesInLoadedSegments(uint target, bool executableOnly = false, bool writableOnly = false)
    {
        var refs = new List<uint>();
        foreach (var segment in _segments)
        {
            if (executableOnly && !segment.IsExecutable)
            {
                continue;
            }

            if (writableOnly && !segment.IsWritable)
            {
                continue;
            }

            for (var offset = segment.FileOffset; offset + 4 <= segment.FileOffset + segment.FileSize; offset += 4)
            {
                if (ReadU32(_bytes, checked((int)offset)) == target)
                {
                    refs.Add(segment.VirtualAddress + (offset - segment.FileOffset));
                }
            }
        }

        return refs.Order().ToArray();
    }

    public PpcAddressLoadCandidate[] FindPpcAddressLoadCandidates(uint target, int lookaheadInstructions = 6)
    {
        var candidates = new List<PpcAddressLoadCandidate>();
        var high = (ushort)(target >> 16);
        var highAdjusted = (ushort)((target + 0x8000u) >> 16);
        var low = (ushort)(target & 0xffff);

        foreach (var segment in _segments.Where(static s => s.IsExecutable))
        {
            for (var offset = segment.FileOffset; offset + 4 <= segment.FileOffset + segment.FileSize; offset += 4)
            {
                var word = ReadU32(_bytes, checked((int)offset));
                var opcode = word >> 26;
                if (opcode != 15)
                {
                    continue;
                }

                var ra = (word >> 16) & 0x1f;
                if (ra != 0)
                {
                    continue;
                }

                var imm = (ushort)(word & 0xffff);
                var highKind = imm == high
                    ? "high16"
                    : imm == highAdjusted
                        ? "high16-adjusted"
                        : "";
                if (highKind.Length == 0)
                {
                    continue;
                }

                var register = (int)((word >> 21) & 0x1f);
                var lisAddress = segment.VirtualAddress + (offset - segment.FileOffset);
                var maxLookahead = Math.Min(
                    (uint)lookaheadInstructions * 4u,
                    segment.FileOffset + segment.FileSize - offset - 4);
                for (var delta = 4u; delta <= maxLookahead; delta += 4)
                {
                    var lowWord = ReadU32(_bytes, checked((int)(offset + delta)));
                    var lowOpcode = lowWord >> 26;
                    var lowImm = (ushort)(lowWord & 0xffff);
                    if (lowImm != low)
                    {
                        continue;
                    }

                    if (lowOpcode == 24)
                    {
                        var sourceRegister = (int)((lowWord >> 21) & 0x1f);
                        var targetRegister = (int)((lowWord >> 16) & 0x1f);
                        if (sourceRegister == register && targetRegister == register)
                        {
                            candidates.Add(new PpcAddressLoadCandidate(
                                lisAddress,
                                segment.VirtualAddress + (offset + delta - segment.FileOffset),
                                "lis+ori",
                                highKind,
                                register,
                                targetRegister,
                                delta / 4));
                        }
                    }
                    else if (lowOpcode == 14)
                    {
                        var targetRegister = (int)((lowWord >> 21) & 0x1f);
                        var sourceRegister = (int)((lowWord >> 16) & 0x1f);
                        if (sourceRegister == register)
                        {
                            candidates.Add(new PpcAddressLoadCandidate(
                                lisAddress,
                                segment.VirtualAddress + (offset + delta - segment.FileOffset),
                                "lis+addi",
                                highKind,
                                register,
                                targetRegister,
                                delta / 4));
                        }
                    }
                }
            }
        }

        return candidates
            .OrderBy(static candidate => candidate.LisAddress)
            .ThenBy(static candidate => candidate.LowAddress)
            .ToArray();
    }

    public PpcOpdTocDescriptor[] FindPpc64OpdTocDescriptors()
    {
        var descriptors = new List<PpcOpdTocDescriptor>();
        foreach (var segment in _segments.Where(static s => s.IsWritable))
        {
            for (var offset = segment.FileOffset; offset + 8 <= segment.FileOffset + segment.FileSize; offset += 4)
            {
                var entry = ReadU32(_bytes, checked((int)offset));
                var toc = ReadU32(_bytes, checked((int)(offset + 4)));
                if (!IsExecutableAddress(entry) || !IsWritableAddress(toc))
                {
                    continue;
                }

                descriptors.Add(new PpcOpdTocDescriptor(
                    segment.VirtualAddress + (offset - segment.FileOffset),
                    entry,
                    toc));
            }
        }

        return descriptors
            .OrderBy(static descriptor => descriptor.DescriptorAddress)
            .ToArray();
    }

    public PpcR2RelativeDataAccess[] FindPpcR2RelativeDataAccessesToAddresses(
        IReadOnlyCollection<uint> targetDataAddresses,
        IReadOnlyCollection<uint> knownTocBases)
    {
        var targets = targetDataAddresses.ToHashSet();
        var tocs = knownTocBases.ToHashSet();
        var hits = new List<PpcR2RelativeDataAccess>();

        foreach (var segment in _segments.Where(static s => s.IsExecutable))
        {
            for (var offset = segment.FileOffset; offset + 4 <= segment.FileOffset + segment.FileSize; offset += 4)
            {
                var word = ReadU32(_bytes, checked((int)offset));
                var opcode = word >> 26;
                if (!IsR2RelativeMemoryOpcode(opcode))
                {
                    continue;
                }

                var ra = (word >> 16) & 0x1f;
                if (ra != 2)
                {
                    continue;
                }

                var displacement = DecodeMemoryDisplacement(word, opcode);
                foreach (var target in targets)
                {
                    var tocBase = unchecked(target - (uint)displacement);
                    if (!tocs.Contains(tocBase))
                    {
                        continue;
                    }

                    hits.Add(new PpcR2RelativeDataAccess(
                        InstructionAddress: segment.VirtualAddress + (offset - segment.FileOffset),
                        InstructionWord: word,
                        Opcode: (int)opcode,
                        MnemonicFamily: MemoryOpcodeName(opcode),
                        Displacement: displacement,
                        TocBase: tocBase,
                        TargetDataAddress: target));
                }
            }
        }

        return hits
            .OrderBy(static hit => hit.InstructionAddress)
            .ThenBy(static hit => hit.TargetDataAddress)
            .ToArray();
    }

    public bool TryReadU32(uint virtualAddress, out uint value)
    {
        var segment = FindSegment(virtualAddress);
        if (segment is null || virtualAddress + 4 > segment.Value.VirtualAddress + segment.Value.FileSize)
        {
            value = 0;
            return false;
        }

        var offset = checked((int)(segment.Value.FileOffset + (virtualAddress - segment.Value.VirtualAddress)));
        value = ReadU32(_bytes, offset);
        return true;
    }

    public bool TryReadAsciiString(uint virtualAddress, int maxLength, out string value)
    {
        value = "";
        var segment = FindSegment(virtualAddress);
        if (segment is null || virtualAddress >= segment.Value.VirtualAddress + segment.Value.FileSize)
        {
            return false;
        }

        var offset = checked((int)(segment.Value.FileOffset + (virtualAddress - segment.Value.VirtualAddress)));
        var limit = checked((int)Math.Min(
            _bytes.Length,
            segment.Value.FileOffset + segment.Value.FileSize));
        var chars = new List<char>();
        for (var i = offset; i < limit && chars.Count < maxLength; i++)
        {
            var b = _bytes[i];
            if (b == 0)
            {
                break;
            }

            if (b < 0x20 || b > 0x7e)
            {
                return false;
            }

            chars.Add((char)b);
        }

        if (chars.Count < 3)
        {
            return false;
        }

        value = new string(chars.ToArray());
        return true;
    }

    public bool IsExecutableAddress(uint virtualAddress)
    {
        var segment = FindSegment(virtualAddress);
        return segment is not null && segment.Value.IsExecutable;
    }

    public bool IsWritableAddress(uint virtualAddress)
    {
        var segment = FindSegment(virtualAddress);
        return segment is not null && segment.Value.IsWritable;
    }

    public string AnnotatePointer(uint value)
    {
        if (value == 0)
        {
            return "zero";
        }

        var segment = FindSegment(value);
        if (segment is null)
        {
            return value < 0x10000 ? "small-int-or-count" : "not-loaded";
        }

        if (segment.Value.IsExecutable)
        {
            return "executable-segment-value-not-yet-confirmed-function";
        }

        if (segment.Value.IsWritable)
        {
            return "writable-data-pointer";
        }

        return "read-only-data-pointer";
    }

    private Segment? FindSegment(uint virtualAddress)
    {
        foreach (var segment in _segments)
        {
            if (virtualAddress >= segment.VirtualAddress
                && virtualAddress < segment.VirtualAddress + segment.MemorySize)
            {
                return segment;
            }
        }

        return null;
    }

    private static ushort ReadU16(byte[] bytes, int offset)
    {
        return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
    }

    private static uint ReadU32(byte[] bytes, int offset)
    {
        return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
    }

    private static ulong ReadU64(byte[] bytes, int offset)
    {
        return ((ulong)ReadU32(bytes, offset) << 32) | ReadU32(bytes, offset + 4);
    }

    private static bool IsR2RelativeMemoryOpcode(uint opcode)
    {
        return opcode is 32 or 33 or 34 or 36 or 37 or 38 or 40 or 42 or 46 or 48 or 50 or 52 or 54 or 58 or 62;
    }

    private static int DecodeMemoryDisplacement(uint word, uint opcode)
    {
        var raw = opcode is 58 or 62
            ? (ushort)(word & 0xfffc)
            : (ushort)(word & 0xffff);
        return (short)raw;
    }

    private static string MemoryOpcodeName(uint opcode)
    {
        return opcode switch
        {
            32 => "lwz",
            33 => "lwzu",
            34 => "lbz",
            36 => "stw",
            37 => "stwu",
            38 => "stb",
            40 => "lhz",
            42 => "lha",
            46 => "lmw",
            48 => "lfs",
            50 => "lfd",
            52 => "stfs",
            54 => "stfd",
            58 => "ld/ds-form",
            62 => "std/ds-form",
            _ => "memory"
        };
    }

    private readonly record struct Segment(uint VirtualAddress, uint FileOffset, uint FileSize, uint MemorySize, uint Flags)
    {
        public bool IsExecutable => (Flags & 1) != 0;
        public bool IsWritable => (Flags & 2) != 0;
    }
}

internal sealed record PpcAddressLoadCandidate(
    uint LisAddress,
    uint LowAddress,
    string Kind,
    string HighImmediateKind,
    int SourceRegister,
    int TargetRegister,
    uint InstructionDistance);

internal sealed record PpcOpdTocDescriptor(
    uint DescriptorAddress,
    uint EntryAddress,
    uint TocBase);

internal sealed record PpcR2RelativeDataAccess(
    uint InstructionAddress,
    uint InstructionWord,
    int Opcode,
    string MnemonicFamily,
    int Displacement,
    uint TocBase,
    uint TargetDataAddress);
