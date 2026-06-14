using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePayloadBuilderReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex PayloadBuilderCallRegex = new(
        @"_opd_FUN_008bc978\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex BufferInitRegex = new(
        @"FUN_0086e948\s*\(\s*(?<buffer>[A-Za-z_][A-Za-z0-9_]*)\s*,(?<args>.*?)\)\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex WriterCallRegex = new(
        @"(?<writer>FUN_00870c28|FUN_0086c9d8|FUN_0086caf8|FUN_0086c7e8|FUN_0086d918)\s*\((?<args>.*?)\)\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex FormatterCallRegex = new(
        @"FUN_008708f8\s*\((?<args>.*?)\)\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex KeyValueAppendCallRegex = new(
        @"_opd_FUN_0090f970\s*\((?<args>.*?)\)\s*;",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, WriterHelperInfo> WriterHelpers =
        new Dictionary<string, WriterHelperInfo>(StringComparer.Ordinal)
        {
            ["FUN_00870c28"] = new("write-u8", 8, "Writes the low 8 bits through _opd_FUN_01338328."),
            ["FUN_0086c9d8"] = new("write-s16-biased", 16, "Writes 15 payload bits plus a sign bit through _opd_FUN_01338ce0(width=0x10). Negative values are biased by +0x80000000 before truncation into the requested bit width."),
            ["FUN_0086caf8"] = new("write-s32-biased", 32, "Writes 31 payload bits plus a sign bit through _opd_FUN_01338ce0(width=0x20). Negative values are biased by +0x80000000 before truncation into the requested bit width."),
            ["FUN_0086c7e8"] = new("write-f32", 32, "Casts the double argument to float and writes the raw 32-bit float lane."),
            ["FUN_0086d918"] = new("write-stringz-u8", null, "Writes each byte with write-u8 until and including the NUL terminator.")
        };

    public static async Task ReduceAsync(string cExportPath, string outputPath, string? elfPath = null)
    {
        var lines = await File.ReadAllLinesAsync(cExportPath);
        var resolver = elfPath is not null && File.Exists(elfPath)
            ? new Tf2Ps3ElfStringResolver(await File.ReadAllBytesAsync(elfPath))
            : null;
        var functions = ExtractFunctions(lines);
        var callsiteFunctions = functions
            .Where(static function => function.Body.Contains("_opd_FUN_008bc978(", StringComparison.Ordinal))
            .Select(function => BuildPayloadBuilderFunction(function, resolver))
            .ToArray();

        var report = new Tf2Ps3SourcePayloadBuilderReport(
            "tf2ps3-source-payload-builder-map",
            "Extracts every TF.elf caller of the native Source/gameplay send builder at 008bc978 and reduces the bitstream writer calls into packet schemas. These are Source-side PS3 payload builders, not Plasma/GameManager text packets.",
            cExportPath,
            elfPath,
            new Tf2Ps3SourcePayloadBuilderSummary(
                callsiteFunctions.Length,
                callsiteFunctions.Count(static function => function.PayloadBuilderCallsites.Count > 0),
                callsiteFunctions.Sum(static function => function.PayloadBuilderCallsites.Count),
                callsiteFunctions.Count(static function => function.SchemaMessageId is not null),
                callsiteFunctions.Count(static function => function.Writes.Count > 0),
                callsiteFunctions.Count(static function => function.FormatterCalls.Count > 0),
                callsiteFunctions.Sum(static function => function.FormatterCalls.Count),
                callsiteFunctions.Count(static function => function.KeyValueAppends.Count > 0),
                callsiteFunctions.Sum(static function => function.KeyValueAppends.Count),
                callsiteFunctions.Count(static function => function.UsesBitPayloadSidecar),
                callsiteFunctions.Count(static function => function.UsesFragmentOrCompressionGate)),
            WriterHelpers.Values.ToArray(),
            [
                new("008bc978", "native-send-wrapper", "Takes a byte pointer/length plus optional bit payload sidecar, then sends direct via 008bb058 or fragmented via 008bc490."),
                new("008b9f70", "payload-queue", "Queues payload bytes when the transport is not in immediate-send mode."),
                new("0134d0e0", "lzss-payload-transform", "The optional transform called by 008bc978 for payloads over 16 bytes. It writes a 0x4c5a5353 'LZSS' header and compresses repeated byte windows before the lower transport layer."),
                new("00925858", "reliable-peer-channel-send-entry", "Direct sender 008bb058 calls this instead of sendto. It resolves the peer endpoint/channel and routes local traffic through 009252e0."),
                new("009252e0", "reliable-peer-channel-queue", "Copies payload bytes into a peer-channel buffer, finds/allocates a 0x18-byte packet slot with 00925cc8/00928298, then queues via 009265e0."),
                new("015?-main-snapshot", "snapshot-frame-builder", "The large callsite near the main snapshot loop sends bitstream snapshots and uses the compression/wrap flag path."),
                new("0090e1xx-009113xx", "source-message-builders", "The callsites with explicit writer helper calls construct typed PS3 Source payloads: 0x44, 0x45, 0x49, and entity/player deltas.")
            ],
            callsiteFunctions);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourcePayloadBuilderFunction BuildPayloadBuilderFunction(
        ExportedFunction function,
        Tf2Ps3ElfStringResolver? resolver)
    {
        var tocBase = resolver?.FindTocBase(function.Address);
        var bufferInitializers = BufferInitRegex.Matches(function.Body)
            .Select(match =>
            {
                var args = SplitTopLevelArguments(match.Groups["args"].Value);
                return new Tf2Ps3SourceBufferInitializer(
                    match.Groups["buffer"].Value,
                    args.ElementAtOrDefault(0) ?? "",
                    args.ElementAtOrDefault(1) ?? "",
                    args.ElementAtOrDefault(2) ?? "",
                    args.ElementAtOrDefault(3) ?? "");
            })
            .ToArray();

        var writes = WriterCallRegex.Matches(function.Body)
            .Select((match, index) => BuildWriterCall(index, match))
            .Where(static write => write is not null)
            .Cast<Tf2Ps3SourcePayloadWrite>()
            .ToArray();

        var callsites = ExtractBuilderCallsites(function.Body)
            .Select((args, index) => BuildCallsite(index, args))
            .ToArray();

        var formatterCalls = FormatterCallRegex.Matches(function.Body)
            .Select((match, index) => BuildFormatterCall(index, match, tocBase, resolver))
            .Where(static call => call is not null)
            .Cast<Tf2Ps3SourceFormatterCall>()
            .ToArray();

        var keyValueAppends = KeyValueAppendCallRegex.Matches(function.Body)
            .Select((match, index) => BuildKeyValueAppend(index, match, tocBase, resolver))
            .Where(static call => call is not null)
            .Cast<Tf2Ps3SourceKeyValueAppendCall>()
            .ToArray();

        var schemaBuffer = callsites
            .Select(static callsite => NormalizeBufferExpression(callsite.PayloadExpression))
            .FirstOrDefault(buffer => buffer.Length > 0 && writes.Any(write => write.Buffer == buffer));

        var schemaWrites = schemaBuffer is null
            ? writes
            : writes.Where(write => write.Buffer == schemaBuffer).ToArray();

        var schemaMessageId = schemaWrites.FirstOrDefault(static write => write.Kind == "write-u8")?.ValueExpression;
        var schemaName = ClassifySchema(function.Address, schemaMessageId, schemaWrites, callsites);

        return new Tf2Ps3SourcePayloadBuilderFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            FormatAddress(tocBase),
            schemaName,
            schemaMessageId,
            schemaBuffer,
            bufferInitializers,
            writes,
            formatterCalls,
            keyValueAppends,
            callsites,
            callsites.Any(static callsite => callsite.BitPayloadExpression is not "0" and not ""),
            callsites.Any(static callsite => callsite.WrapOrCompressionFlagExpression is not "0" and not ""),
            BuildConclusion(schemaName, schemaMessageId, schemaWrites, formatterCalls, keyValueAppends, callsites),
            Preview(function.Lines));
    }

    private static Tf2Ps3SourcePayloadWrite? BuildWriterCall(int index, Match match)
    {
        var writer = match.Groups["writer"].Value;
        if (!WriterHelpers.TryGetValue(writer, out var helper))
        {
            return null;
        }

        var args = SplitTopLevelArguments(match.Groups["args"].Value);
        if (args.Count < 2)
        {
            return null;
        }

        var bufferArgIndex = writer == "FUN_0086c7e8" ? 1 : 0;
        var valueArgIndex = writer == "FUN_0086c7e8" ? 0 : 1;
        var buffer = NormalizeBufferExpression(args.ElementAtOrDefault(bufferArgIndex) ?? "");
        var value = CleanExpression(args.ElementAtOrDefault(valueArgIndex) ?? "");
        if (buffer.Length == 0)
        {
            return null;
        }

        return new Tf2Ps3SourcePayloadWrite(
            index,
            writer,
            helper.Kind,
            helper.FixedBitWidth,
            buffer,
            value,
            ClassifyField(value, helper.Kind));
    }

    private static Tf2Ps3SourcePayloadBuilderCallsite BuildCallsite(int index, IReadOnlyList<string> args)
    {
        var payloadExpression = CleanExpression(args.ElementAtOrDefault(3) ?? "");
        return new Tf2Ps3SourcePayloadBuilderCallsite(
            index,
            CleanExpression(args.ElementAtOrDefault(0) ?? ""),
            CleanExpression(args.ElementAtOrDefault(1) ?? ""),
            CleanExpression(args.ElementAtOrDefault(2) ?? ""),
            payloadExpression,
            CleanExpression(args.ElementAtOrDefault(4) ?? ""),
            CleanExpression(args.ElementAtOrDefault(5) ?? ""),
            CleanExpression(args.ElementAtOrDefault(6) ?? ""),
            NormalizeBufferExpression(payloadExpression),
            ClassifyLengthExpression(CleanExpression(args.ElementAtOrDefault(4) ?? "")));
    }

    private static Tf2Ps3SourceFormatterCall? BuildFormatterCall(
        int index,
        Match match,
        uint? tocBase,
        Tf2Ps3ElfStringResolver? resolver)
    {
        var args = SplitTopLevelArguments(match.Groups["args"].Value);
        if (args.Count < 3)
        {
            return null;
        }

        var destination = CleanExpression(args[0]);
        var capacity = CleanExpression(args.ElementAtOrDefault(1) ?? "");
        var format = CleanExpression(args.ElementAtOrDefault(2) ?? "");
        var values = args.Skip(3).Select(CleanExpression).Where(static value => value.Length > 0).ToArray();
        var resolvedFormat = resolver?.ResolveStringPointer(format, tocBase);

        return new Tf2Ps3SourceFormatterCall(
            index,
            destination,
            capacity,
            format,
            values,
            NormalizeBufferExpression(destination),
            FormatAddress(resolvedFormat?.PointerAddress),
            FormatAddress(resolvedFormat?.StringAddress),
            resolvedFormat?.Value,
            ClassifyFormatterRole(capacity, format, values));
    }

    private static Tf2Ps3SourceKeyValueAppendCall? BuildKeyValueAppend(
        int index,
        Match match,
        uint? tocBase,
        Tf2Ps3ElfStringResolver? resolver)
    {
        var args = SplitTopLevelArguments(match.Groups["args"].Value);
        if (args.Count < 4)
        {
            return null;
        }

        var destination = CleanExpression(args[0]);
        var key = CleanExpression(args.ElementAtOrDefault(1) ?? "");
        var value = CleanExpression(args.ElementAtOrDefault(2) ?? "");
        var capacity = CleanExpression(args.ElementAtOrDefault(3) ?? "");
        var resolvedKey = resolver?.ResolveStringPointer(key, tocBase);
        var resolvedValue = resolver?.ResolveStringPointer(value, tocBase);

        return new Tf2Ps3SourceKeyValueAppendCall(
            index,
            destination,
            key,
            value,
            capacity,
            FormatAddress(resolvedKey?.PointerAddress),
            FormatAddress(resolvedKey?.StringAddress),
            resolvedKey?.Value,
            FormatAddress(resolvedValue?.PointerAddress),
            FormatAddress(resolvedValue?.StringAddress),
            resolvedValue?.Value,
            ClassifyKeyExpression(key),
            ClassifyValueExpression(value));
    }

    private static string ClassifySchema(
        string address,
        string? messageId,
        IReadOnlyList<Tf2Ps3SourcePayloadWrite> writes,
        IReadOnlyList<Tf2Ps3SourcePayloadBuilderCallsite> callsites)
    {
        return messageId switch
        {
            "0x44" => "player-summary-or-scoreboard-update",
            "0x45" => "resource-string-table-or-downloadables-update",
            "0x49" => "server-info-and-map-session-descriptor",
            _ when address == "00910d48" => "entity-health-delta",
            _ when address == "00911140" => "player-rating-delta",
            _ when address == "009113a0" => "formatted-player-connect-or-status-event",
            _ when callsites.Any(static callsite => callsite.WrapOrCompressionFlagExpression != "0") => "main-snapshot-or-compressed-frame",
            _ when writes.Count == 0 => "raw-or-preformatted-source-payload",
            _ => "typed-source-bitstream-payload"
        };
    }

    private static string BuildConclusion(
        string schemaName,
        string? messageId,
        IReadOnlyList<Tf2Ps3SourcePayloadWrite> writes,
        IReadOnlyList<Tf2Ps3SourceFormatterCall> formatterCalls,
        IReadOnlyList<Tf2Ps3SourceKeyValueAppendCall> keyValueAppends,
        IReadOnlyList<Tf2Ps3SourcePayloadBuilderCallsite> callsites)
    {
        if (messageId is "0x44" or "0x45" or "0x49")
        {
            return $"Native schema starts with message id {messageId}. Rebuild this with bitstream writers rather than byte-copying PC srcds output.";
        }

        if (schemaName == "main-snapshot-or-compressed-frame")
        {
            return "This is the Source gameplay snapshot path. It sends the already-built bitstream and may ask 008bc978 to wrap/compress the direct payload.";
        }

        if (formatterCalls.Any(static call => call.Role == "entity-health-delta-formatter"))
        {
            return "Native raw payload is a tiny formatted entity-health delta. It uses the TF.elf formatter before sending through 008bc978.";
        }

        if (keyValueAppends.Count > 0)
        {
            return "Native raw payload is assembled as filtered key/value text and then formatted before 008bc978; generate the semantic keys, not captured bytes.";
        }

        if (writes.Count == 0 && callsites.Count > 0)
        {
            return "The payload buffer is prepared by earlier code or a formatter; inspect the payload expression and caller object state before generating this family.";
        }

        return "Field order is captured from TF.elf writer calls; unknown expressions still need naming from surrounding class/function context.";
    }

    private static string ClassifyFormatterRole(
        string capacity,
        string format,
        IReadOnlyList<string> values)
    {
        if (format.Contains("PTR_s_m_iHealth", StringComparison.Ordinal))
        {
            return "entity-health-delta-formatter";
        }

        if (capacity is "0x805" or "0x800" || values.Any(static value => value.Contains("uVar3 - 0x86a", StringComparison.Ordinal)))
        {
            return "player-status-keyvalue-frame-formatter";
        }

        if (format.Contains("PTR_s_", StringComparison.Ordinal) || format.Contains("DAT_", StringComparison.Ordinal))
        {
            return "native-symbol-format-string";
        }

        return "generic-native-formatter";
    }

    private static string ClassifyKeyExpression(string expression)
    {
        if (expression.Contains("PTR_s_m_iPing", StringComparison.Ordinal))
        {
            return "player-ping-key";
        }

        if (expression.Contains("PTR_s_m_iHealth", StringComparison.Ordinal))
        {
            return "player-health-key";
        }

        if (expression.Contains("PTR_s_m_iPlayerRating", StringComparison.Ordinal))
        {
            return "player-rating-key";
        }

        if (expression.Contains("PTR_s_m_iRatingDelta", StringComparison.Ordinal))
        {
            return "player-rating-delta-key";
        }

        if (expression.Contains("iStack_", StringComparison.Ordinal))
        {
            return "toc-resolved-source-status-key";
        }

        return "unresolved-key-expression";
    }

    private static string ClassifyValueExpression(string expression)
    {
        if (expression.Contains("FUN_0086f2b8", StringComparison.Ordinal))
        {
            return "formatted-numeric-value";
        }

        if (expression.Contains("uVar16", StringComparison.Ordinal)
            || expression.Contains("uVar5", StringComparison.Ordinal)
            || expression.Contains("uVar1", StringComparison.Ordinal))
        {
            return "runtime-player-or-team-value";
        }

        if (expression.Contains("uVar3 - 0x970", StringComparison.Ordinal))
        {
            return "formatted-local-text-value";
        }

        if (expression.Contains("0x", StringComparison.Ordinal))
        {
            return "constant-or-toc-string";
        }

        return "unresolved-value-expression";
    }

    private static string ClassifyField(string expression, string kind)
    {
        if (kind == "write-u8" && expression.StartsWith("0x", StringComparison.Ordinal))
        {
            return "message-or-small-enum";
        }

        if (expression.Contains("param_3[2]", StringComparison.Ordinal)
            || expression.Contains("*(int *)(param_3 + 8)", StringComparison.Ordinal)
            || expression.Contains("piVar9[2]", StringComparison.Ordinal))
        {
            return "destination-slot-or-player-channel";
        }

        if (expression.Contains("0xffffffff", StringComparison.Ordinal))
        {
            return "sentinel-minus-one";
        }

        if (kind.Contains("string", StringComparison.Ordinal))
        {
            return "nul-terminated-string";
        }

        if (kind == "write-f32")
        {
            return "float32";
        }

        return "unresolved-expression";
    }

    private static string ClassifyLengthExpression(string expression)
    {
        if (expression.Contains("+ 7 >> 3", StringComparison.Ordinal) || expression.Contains("+ 7) >> 3", StringComparison.Ordinal))
        {
            return "bitstream-byte-length-from-current-bit-count";
        }

        if (expression == "1")
        {
            return "single-byte-payload";
        }

        if (expression.Contains("iVar8", StringComparison.Ordinal) || expression.Contains("FUN_0086a8b8", StringComparison.Ordinal))
        {
            return "strlen-or-formatted-text-length";
        }

        return "expression";
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractBuilderCallsites(string body)
    {
        var callsites = new List<IReadOnlyList<string>>();
        foreach (Match match in PayloadBuilderCallRegex.Matches(body))
        {
            var start = match.Index + match.Length;
            var end = FindMatchingCallEnd(body, start);
            if (end < 0)
            {
                continue;
            }

            callsites.Add(SplitTopLevelArguments(body[start..end]));
        }

        return callsites;
    }

    private static int FindMatchingCallEnd(string text, int start)
    {
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> SplitTopLevelArguments(string args)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < args.Length; i++)
        {
            var ch = args[i];
            if (ch == '(' || ch == '[')
            {
                depth++;
            }
            else if (ch == ')' || ch == ']')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (ch == ',' && depth == 0)
            {
                result.Add(CleanExpression(args[start..i]));
                start = i + 1;
            }
        }

        if (start <= args.Length)
        {
            result.Add(CleanExpression(args[start..]));
        }

        return result;
    }

    private static string NormalizeBufferExpression(string expression)
    {
        var cleaned = CleanExpression(expression)
            .Replace("(int *)", "", StringComparison.Ordinal)
            .Replace("(uint *)", "", StringComparison.Ordinal)
            .Replace("(ulonglong)", "", StringComparison.Ordinal)
            .Replace("ZEXT48(", "", StringComparison.Ordinal)
            .Replace("&", "", StringComparison.Ordinal)
            .Trim();

        while (cleaned.EndsWith(")", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^1].Trim();
        }

        var bracket = cleaned.IndexOf('[', StringComparison.Ordinal);
        if (bracket > 0)
        {
            cleaned = cleaned[..bracket];
        }

        return Regex.IsMatch(cleaned, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
            ? cleaned
            : "";
    }

    private static string CleanExpression(string expression)
    {
        return Regex.Replace(expression, @"\s+", " ").Trim();
    }

    private static string? FormatAddress(uint? address)
    {
        return address is null ? null : $"0x{address.Value:x8}";
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExportedFunction>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            var braceLine = i;
            while (braceLine < lines.Length && !lines[braceLine].Contains('{', StringComparison.Ordinal))
            {
                braceLine++;
            }

            if (braceLine >= lines.Length)
            {
                continue;
            }

            var depth = 0;
            var end = braceLine;
            var inString = false;
            var inCharacter = false;
            var escaped = false;
            for (; end < lines.Length; end++)
            {
                depth += BraceDelta(lines[end], ref inString, ref inCharacter, ref escaped);

                if (depth == 0 && end > braceLine)
                {
                    break;
                }
            }

            var functionLines = lines[i..Math.Min(end + 1, lines.Length)];
            functions.Add(new ExportedFunction(
                match.Groups["name"].Value,
                match.Groups["address"].Value,
                i + 1,
                end + 1,
                functionLines,
                string.Join('\n', functionLines)));
            i = end;
        }

        return functions;
    }

    private static int BraceDelta(string line, ref bool inString, ref bool inCharacter, ref bool escaped)
    {
        var delta = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if ((inString || inCharacter) && ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (inString)
            {
                if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (inCharacter)
            {
                if (ch == '\'')
                {
                    inCharacter = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '\'')
            {
                inCharacter = true;
                continue;
            }

            if (ch == '{')
            {
                delta++;
            }
            else if (ch == '}')
            {
                delta--;
            }
        }

        return delta;
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        return string.Join('\n', lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(18));
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);

    private sealed class Tf2Ps3ElfStringResolver(byte[] image)
    {
        private static readonly Regex TocRelativePointerRegex = new(
            @"\*\(uint \*\)\([^)]*iStack_[A-Za-z0-9_]+\s*\+\s*-0x(?<offset>[0-9a-fA-F]+)\)",
            RegexOptions.Compiled);

        private static readonly Regex NamedPointerRegex = new(
            @"PTR_[A-Za-z0-9_\[\]]+_(?<address>[0-9a-fA-F]{8})",
            RegexOptions.Compiled);

        private readonly Dictionary<string, uint?> tocBaseByEntry = new(StringComparer.Ordinal);

        public uint? FindTocBase(string entryAddress)
        {
            if (tocBaseByEntry.TryGetValue(entryAddress, out var cached))
            {
                return cached;
            }

            if (!uint.TryParse(entryAddress, System.Globalization.NumberStyles.HexNumber, null, out var entry))
            {
                tocBaseByEntry[entryAddress] = null;
                return null;
            }

            var needle = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(needle, entry);
            for (var i = 0; i <= image.Length - 8; i += 4)
            {
                if (!image.AsSpan(i, 4).SequenceEqual(needle))
                {
                    continue;
                }

                var toc = BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan(i + 4, 4));
                if (VirtualToFileOffset(toc) is not null)
                {
                    tocBaseByEntry[entryAddress] = toc;
                    return toc;
                }
            }

            tocBaseByEntry[entryAddress] = null;
            return null;
        }

        public ResolvedStringPointer? ResolveStringPointer(string expression, uint? tocBase)
        {
            var pointerAddress = ResolvePointerAddress(expression, tocBase);
            if (pointerAddress is null)
            {
                return null;
            }

            var stringAddress = ReadUInt32(pointerAddress.Value);
            var value = stringAddress is null ? null : ReadAsciiString(stringAddress.Value);
            return new ResolvedStringPointer(pointerAddress.Value, stringAddress, value);
        }

        private static uint? ResolvePointerAddress(string expression, uint? tocBase)
        {
            var tocMatch = TocRelativePointerRegex.Match(expression);
            if (tocMatch.Success && tocBase is not null)
            {
                var offset = Convert.ToUInt32(tocMatch.Groups["offset"].Value, 16);
                return tocBase.Value - offset;
            }

            var namedMatch = NamedPointerRegex.Match(expression);
            if (namedMatch.Success)
            {
                return Convert.ToUInt32(namedMatch.Groups["address"].Value, 16);
            }

            return null;
        }

        private uint? ReadUInt32(uint virtualAddress)
        {
            var offset = VirtualToFileOffset(virtualAddress);
            if (offset is null || offset.Value < 0 || offset.Value + 4 > image.Length)
            {
                return null;
            }

            return BinaryPrimitives.ReadUInt32BigEndian(image.AsSpan((int)offset.Value, 4));
        }

        private string? ReadAsciiString(uint virtualAddress)
        {
            var offset = VirtualToFileOffset(virtualAddress);
            if (offset is null || offset.Value < 0 || offset.Value >= image.Length)
            {
                return null;
            }

            var start = (int)offset.Value;
            var end = start;
            var maxEnd = Math.Min(image.Length, start + 128);
            while (end < maxEnd && image[end] != 0)
            {
                if ((image[end] < 0x20 && image[end] is not (byte)'\t' and not (byte)'\n' and not (byte)'\r')
                    || image[end] > 0x7e)
                {
                    return null;
                }

                end++;
            }

            return end == start ? null : System.Text.Encoding.ASCII.GetString(image, start, end - start);
        }

        private static long? VirtualToFileOffset(uint virtualAddress)
        {
            return virtualAddress switch
            {
                >= 0x00010000 and < 0x0176bb30 => virtualAddress - 0x00010000,
                >= 0x01770000 and < 0x019c4318 => virtualAddress - 0x00010000,
                >= 0x10000000 and < 0x100e95c8 => virtualAddress - 0x10000000 + 0x019c0000,
                >= 0x100f0000 and < 0x10175c10 => virtualAddress - 0x100f0000 + 0x01ab0000,
                _ => null
            };
        }
    }

    private sealed record ResolvedStringPointer(uint PointerAddress, uint? StringAddress, string? Value);
}

public sealed record Tf2Ps3SourcePayloadBuilderReport(
    string Status,
    string Note,
    string Input,
    string? ElfInput,
    Tf2Ps3SourcePayloadBuilderSummary Summary,
    IReadOnlyList<WriterHelperInfo> WriterHelpers,
    IReadOnlyList<Tf2Ps3SourcePayloadAnchor> Anchors,
    IReadOnlyList<Tf2Ps3SourcePayloadBuilderFunction> Functions);

public sealed record Tf2Ps3SourcePayloadBuilderSummary(
    int FunctionCount,
    int LocatedPayloadBuilderFunctionCount,
    int PayloadBuilderCallsiteCount,
    int FunctionsWithSchemaMessageId,
    int FunctionsWithWriterCalls,
    int FunctionsWithFormatterCalls,
    int FormatterCallCount,
    int FunctionsWithKeyValueAppends,
    int KeyValueAppendCount,
    int FunctionsUsingBitPayloadSidecar,
    int FunctionsUsingFragmentOrCompressionGate);

public sealed record WriterHelperInfo(string Kind, int? FixedBitWidth, string Semantics);

public sealed record Tf2Ps3SourcePayloadAnchor(string Address, string Role, string Notes);

public sealed record Tf2Ps3SourcePayloadBuilderFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string? TocBase,
    string SchemaName,
    string? SchemaMessageId,
    string? SchemaBuffer,
    IReadOnlyList<Tf2Ps3SourceBufferInitializer> BufferInitializers,
    IReadOnlyList<Tf2Ps3SourcePayloadWrite> Writes,
    IReadOnlyList<Tf2Ps3SourceFormatterCall> FormatterCalls,
    IReadOnlyList<Tf2Ps3SourceKeyValueAppendCall> KeyValueAppends,
    IReadOnlyList<Tf2Ps3SourcePayloadBuilderCallsite> PayloadBuilderCallsites,
    bool UsesBitPayloadSidecar,
    bool UsesFragmentOrCompressionGate,
    string Conclusion,
    string Preview);

public sealed record Tf2Ps3SourceBufferInitializer(
    string Buffer,
    string BackingPointerExpression,
    string BackingStorageExpression,
    string CapacityBytesExpression,
    string InitialBitLimitExpression);

public sealed record Tf2Ps3SourcePayloadWrite(
    int Index,
    string Helper,
    string Kind,
    int? FixedBitWidth,
    string Buffer,
    string ValueExpression,
    string FieldRole);

public sealed record Tf2Ps3SourceFormatterCall(
    int Index,
    string DestinationExpression,
    string CapacityExpression,
    string FormatExpression,
    IReadOnlyList<string> ValueExpressions,
    string NormalizedDestinationBuffer,
    string? FormatPointerAddress,
    string? FormatStringAddress,
    string? ResolvedFormatString,
    string Role);

public sealed record Tf2Ps3SourceKeyValueAppendCall(
    int Index,
    string DestinationExpression,
    string KeyExpression,
    string ValueExpression,
    string CapacityExpression,
    string? KeyPointerAddress,
    string? KeyStringAddress,
    string? ResolvedKeyString,
    string? ValuePointerAddress,
    string? ValueStringAddress,
    string? ResolvedValueString,
    string KeyRole,
    string ValueRole);

public sealed record Tf2Ps3SourcePayloadBuilderCallsite(
    int Index,
    string UnknownSendModeExpression,
    string SlotOrChannelExpression,
    string PeerAddressExpression,
    string PayloadExpression,
    string LengthExpression,
    string BitPayloadExpression,
    string WrapOrCompressionFlagExpression,
    string NormalizedPayloadBuffer,
    string LengthRole);
