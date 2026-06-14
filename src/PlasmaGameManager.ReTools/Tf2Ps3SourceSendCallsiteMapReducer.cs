using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceSendCallsiteMapReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceSendCallsiteMapReport> ReduceAsync(
        string cExportPath,
        string outputPath)
    {
        var text = await File.ReadAllTextAsync(cExportPath);
        var functions = ExtractFunctions(text)
            .Where(static function => ContainsSourceSendCall(function.Body))
            .Select(BuildFunction)
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        var callsites = functions.SelectMany(static function => function.Callsites).ToArray();
        var snapshotCallsites = functions.Where(static function => function.Role == "native-snapshot-frame-builder").ToArray();
        var directMessages = functions.Where(static function => function.Role.StartsWith("direct-source-message-", StringComparison.Ordinal)).ToArray();
        var formattedStatus = functions.Where(static function => function.Role.Contains("status", StringComparison.Ordinal)
            || function.Role.Contains("delta", StringComparison.Ordinal)
            || function.Role.Contains("event", StringComparison.Ordinal)
            || function.Role.Contains("formatted", StringComparison.Ordinal)).ToArray();
        var wrapperForwarders = functions.Where(static function => function.Role == "thin-native-send-forwarder").ToArray();

        var report = new Tf2Ps3SourceSendCallsiteMapReport(
            "tf2ps3-source-send-callsite-map",
            "Classifies every TF.elf callsite that passes bytes to 008bc978 and separates payload writers from the transport queue/send wrapper. The long Source prefix/control bytes are owned by upstream snapshot/direct-message writers, not by the queue wrapper itself.",
            cExportPath,
            new Tf2Ps3SourceSendCallsiteMapSummary(
                LocatedSourceSendFunctions: functions.Length,
                SourceSendCallsiteCount: callsites.Length,
                DirectTypedMessageFunctionCount: directMessages.Length,
                FormattedStatusOrEventFunctionCount: formattedStatus.Length,
                SnapshotFrameFunctionCount: snapshotCallsites.Length,
                ThinWrapperForwarderCount: wrapperForwarders.Length,
                CallsitesUsingBitSidecar: callsites.Count(static callsite => callsite.BitPayloadExpression is not "0" and not ""),
                CallsitesRequestingWrapOrCompression: callsites.Count(static callsite => callsite.WrapOrCompressionFlagExpression is not "0" and not ""),
                UpstreamWriterOwnedCallsiteCount: callsites.Count(static callsite => callsite.Ownership == "upstream-payload-writer-owned"),
                TransportOnlyCallsiteCount: callsites.Count(static callsite => callsite.Ownership == "transport-wrapper-owned"),
                SnapshotEntityDeltaWriterAddress: "00a5fb80",
                SnapshotFrameBuilderAddress: "00a61150",
                NativeSendWrapperAddress: "008bc978",
                NativeQueueAddress: "008b9f70"),
            new Tf2Ps3SourceSendBoundary(
                "008bc978",
                "native-source-send-wrapper",
                "Non-immediate path calls 008b9f70(param_2, payloadLength, payloadPointer) and returns the original length. Immediate path may append an optional bit sidecar, optionally transform/compress the staged main payload, and choose direct 008bb058 or fragmented 008bc490.",
                "008bc978 does not construct the long Source prefix/control bitstream. Treat it as staging/transport. If a generated frame has wrong long prefix bytes, the bug is upstream in the snapshot/direct-message payload writer."),
            new Tf2Ps3SourceSendBoundary(
                "008b9f70",
                "native-source-payload-queue",
                "Copies already-built payload bytes into a queue cell. Payloads under 0x801 bytes are copied inline after an 8-byte cell header; larger payloads get a heap buffer pointer plus length.",
                "The queue preserves payload bytes. It is not a semantic writer and cannot fix missing snapshot fields."),
            new Tf2Ps3SourceSendBoundary(
                "00a61150 -> 00a5fb80 -> 008bc978",
                "native-snapshot-frame-writer",
                "00a61150 writes the snapshot frame index/base, message id 0, update flags, optional pending count and raw pending sections. It calls 00a5fb80 for entity delta groups, pads/clears tail bits, then forwards the complete bitstream to 008bc978.",
                "This is the owner of the long map-load/gameplay Source frame prefix around native state-link/entity records. NativeMapLoadReady depends on generating this route semantically, not replaying PCAP chunks."),
            functions,
            [
                "The recovered callsite map has no evidence that GameManager/Plasma owns these bytes; the failure remains in native Source snapshot/signon payload generation.",
                "Direct message families 0x44, 0x45, and 0x49 are separate typed Source messages and should remain generated by their own bitstream builders.",
                "The long high-entropy-looking prefix/control bytes seen before embedded COc/PNG/DSC records match the snapshot/entity-delta route, especially 00a61150 plus 00a5fb80 field writes and optional compression gating.",
                "Next implementation target: replace generated PlayerStateLinkBatch/mixed-binary steady-state bodies with semantic 00a61150 snapshot frames whose 00a5fb80 entity groups are populated from TF2 server state."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceSendCallsiteFunction BuildFunction(ExportedSendFunction function)
    {
        var role = ClassifyRole(function.Address, function.Body);
        var calls = FunctionCallRegex().Matches(function.Body)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var writerSignals = BuildWriterSignals(function.Address, function.Body);
        var callsites = ExtractBuilderCallsites(function.Body)
            .Select((args, index) => BuildCallsite(index, role, args))
            .ToArray();

        return new Tf2Ps3SourceSendCallsiteFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            role,
            ClassifyUpstreamOwner(role),
            calls,
            writerSignals,
            callsites,
            Preview(function.Body),
            BuildConclusion(role));
    }

    private static Tf2Ps3SourceSendCallsite BuildCallsite(
        int index,
        string role,
        IReadOnlyList<string> args)
    {
        var sendMode = CleanExpression(args.ElementAtOrDefault(0) ?? "");
        var slot = CleanExpression(args.ElementAtOrDefault(1) ?? "");
        var peer = CleanExpression(args.ElementAtOrDefault(2) ?? "");
        var payload = CleanExpression(args.ElementAtOrDefault(3) ?? "");
        var length = CleanExpression(args.ElementAtOrDefault(4) ?? "");
        var sidecar = CleanExpression(args.ElementAtOrDefault(5) ?? "");
        var wrap = CleanExpression(args.ElementAtOrDefault(6) ?? "");
        var owner = ClassifyOwnership(role);

        return new Tf2Ps3SourceSendCallsite(
            index,
            sendMode,
            slot,
            peer,
            payload,
            length,
            sidecar,
            wrap,
            owner,
            ClassifyPayloadOrigin(role, payload),
            ClassifyLengthRole(length),
            ClassifyTransportMutation(sidecar, wrap),
            owner == "upstream-payload-writer-owned"
                ? "Payload bytes are complete before 008bc978 is entered."
                : "This callsite merely forwards caller-owned bytes into 008bc978.");
    }

    private static string ClassifyRole(string address, string body)
    {
        return address switch
        {
            "0039f860" => "thin-native-send-forwarder",
            "007ccf20" => "direct-source-message-0x6b-gameplay-stat-timesused",
            "00810178" => "direct-source-message-0x41-hud-player-object-update",
            "00811750" => "direct-source-message-0x41-hud-player-object-minimal-update",
            "008bd158" => "formatted-vararg-control-frame",
            "0090e550" => "direct-source-message-0x44-player-summary-scoreboard",
            "0090e8b0" => "direct-source-message-0x45-resource-stringtable",
            "0090ebf8" => "direct-source-message-0x49-server-map-session",
            "00910d48" => "formatted-entity-health-delta",
            "00911140" => "single-byte-player-rating-delta",
            "009113a0" => "formatted-player-status-keyvalue-event",
            "00a61150" => "native-snapshot-frame-builder",
            _ when body.Contains("_opd_FUN_00a5fb80", StringComparison.Ordinal) => "native-snapshot-frame-builder",
            _ when body.Contains("FUN_00870c28", StringComparison.Ordinal) => "typed-source-bitstream-writer",
            _ when body.Contains("FUN_008708f8", StringComparison.Ordinal) => "formatted-source-text-writer",
            _ => "unclassified-source-send-caller"
        };
    }

    private static string ClassifyUpstreamOwner(string role)
    {
        return role switch
        {
            "thin-native-send-forwarder" => "caller of FUN_0039f860 / 008bc978",
            "native-snapshot-frame-builder" => "00a61150 snapshot frame plus 00a5fb80 entity delta writer",
            "formatted-vararg-control-frame" => "008bd158 vararg formatter",
            _ when role.StartsWith("direct-source-message-", StringComparison.Ordinal) => "current direct Source message writer",
            _ when role.Contains("status", StringComparison.Ordinal)
                || role.Contains("delta", StringComparison.Ordinal)
                || role.Contains("event", StringComparison.Ordinal)
                || role.Contains("formatted", StringComparison.Ordinal) => "current formatted Source status/event writer",
            _ => "current function or caller-owned prebuilt payload"
        };
    }

    private static string ClassifyOwnership(string role)
    {
        return role == "thin-native-send-forwarder"
            ? "transport-wrapper-owned"
            : "upstream-payload-writer-owned";
    }

    private static string ClassifyPayloadOrigin(string role, string payload)
    {
        if (role == "native-snapshot-frame-builder")
        {
            return "00a61150 local snapshot bitstream after 00a5fb80 entity delta serialization";
        }

        if (role.StartsWith("direct-source-message-", StringComparison.Ordinal))
        {
            return "local direct-message bitstream buffer";
        }

        if (role.Contains("formatted", StringComparison.Ordinal) || role.Contains("delta", StringComparison.Ordinal))
        {
            return "formatted status/event payload buffer";
        }

        if (payload.Contains("param_", StringComparison.Ordinal))
        {
            return "caller-provided payload pointer";
        }

        return "local or prebuilt payload pointer";
    }

    private static string ClassifyLengthRole(string expression)
    {
        if (expression.Contains("+ 7 >> 3", StringComparison.Ordinal) || expression.Contains("+ 7) >> 3", StringComparison.Ordinal))
        {
            return "bit-count-rounded-to-byte-length";
        }

        if (expression == "1")
        {
            return "single-byte-payload";
        }

        if (expression.Contains("iVar1 + 5", StringComparison.Ordinal))
        {
            return "formatter-length-plus-five-byte-prefix";
        }

        if (expression.Contains("iVar8", StringComparison.Ordinal) || expression.Contains("iVar4 + 1", StringComparison.Ordinal))
        {
            return "formatted-string-length";
        }

        return "caller-provided-byte-length-expression";
    }

    private static string ClassifyTransportMutation(string sidecar, string wrap)
    {
        if (sidecar is not "0" and not "" && wrap is not "0" and not "")
        {
            return "008bc978 may append a bit-sidecar and request payload transform/compression.";
        }

        if (sidecar is not "0" and not "")
        {
            return "008bc978 appends a two-byte big-endian bit count followed by sidecar bits.";
        }

        if (wrap is not "0" and not "")
        {
            return "008bc978 may wrap/compress the already-built payload before direct/fragment send.";
        }

        return "008bc978 stages/queues the already-built payload without semantic mutation.";
    }

    private static string[] BuildWriterSignals(string address, string body)
    {
        var signals = new List<string>();
        AddIf(body, signals, "FUN_0086e948", "local-bit-buffer-init");
        AddIf(body, signals, "FUN_0039d3f0", "local-bit-buffer-init-wrapper");
        AddIf(body, signals, "FUN_0086caf8", "write-s32-biased");
        AddIf(body, signals, "FUN_0039cd00", "write-s32-biased-wrapper");
        AddIf(body, signals, "FUN_0086c9d8", "write-s16-biased");
        AddIf(body, signals, "FUN_00870c28", "write-u8");
        AddIf(body, signals, "FUN_003a13d0", "write-u8-wrapper");
        AddIf(body, signals, "FUN_0086c7e8", "write-f32");
        AddIf(body, signals, "FUN_0086d918", "write-stringz-u8");
        AddIf(body, signals, "FUN_0039c290", "write-stringz-u8-wrapper");
        AddIf(body, signals, "FUN_008708f8", "format-string");
        AddIf(body, signals, "FUN_0086e5c8", "raw-byte-copy-into-bitstream");
        AddIf(body, signals, "FUN_00870958", "raw-bit-copy");
        AddIf(body, signals, "_opd_FUN_00a5fb80", "entity-delta-writer");
        AddIf(body, signals, "_opd_FUN_00a5e9e0", "queued-bitstream-descriptor-cache");
        AddIf(body, signals, "_opd_FUN_00a5a820", "snapshot-history-slot-update");
        AddIf(body, signals, "_opd_FUN_00a579d8", "snapshot-history-stats-update");

        if (address == "00a61150")
        {
            signals.Add("snapshot-tail-padding-clear-five-bits");
            signals.Add("snapshot-update-flag-bit-0x01-from-00a5fb80");
            signals.Add("snapshot-update-flag-bit-0x10-from-pending-count");
        }

        return signals.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string BuildConclusion(string role)
    {
        return role switch
        {
            "native-snapshot-frame-builder" =>
                "Primary map-load/gameplay target. Generate the complete 00a61150 snapshot frame and 00a5fb80 entity groups semantically; 008bc978 only transports it.",
            "thin-native-send-forwarder" =>
                "Do not model this as a writer. It is an import/thunk-style forwarder to 008bc978.",
            _ when role.StartsWith("direct-source-message-", StringComparison.Ordinal) =>
                "Keep this as a separate native direct-message builder with the recovered message id and field order.",
            _ when role.Contains("formatted", StringComparison.Ordinal)
                || role.Contains("delta", StringComparison.Ordinal)
                || role.Contains("event", StringComparison.Ordinal) =>
                "Generate from named server/player state; the bytes are not GameManager/Plasma and are complete before 008bc978.",
            _ =>
                "Payload ownership is not fully named yet; inspect this caller before changing generated runtime behavior."
        };
    }

    private static void AddIf(string body, List<string> signals, string needle, string signal)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            signals.Add(signal);
        }
    }

    private static ExportedSendFunction[] ExtractFunctions(string text)
    {
        var lines = text.Split('\n');
        var functions = new List<ExportedSendFunction>();
        var seenBraceLines = new HashSet<int>();
        for (var callLine = 0; callLine < lines.Length; callLine++)
        {
            if (!ContainsSourceSendCall(lines[callLine]))
            {
                continue;
            }

            var braceLine = FindEnclosingFunctionBraceLine(lines, callLine);
            if (braceLine < 0 || !seenBraceLines.Add(braceLine))
            {
                continue;
            }

            var headerStart = FindFunctionHeaderStart(lines, braceLine);
            if (headerStart < 0)
            {
                continue;
            }

            var signature = string.Join(' ', lines[headerStart..braceLine]).Trim();
            var match = FunctionNameRegex().Match(signature);
            if (!match.Success)
            {
                continue;
            }

            var end = FindFunctionEndLine(lines, braceLine);
            if (end < 0)
            {
                continue;
            }

            functions.Add(new ExportedSendFunction(
                match.Groups["name"].Value,
                match.Groups["address"].Value,
                headerStart + 1,
                end + 1,
                string.Join('\n', lines[(braceLine + 1)..end])));
        }

        return functions
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();
    }

    private static int FindEnclosingFunctionBraceLine(string[] lines, int callLine)
    {
        for (var i = callLine; i >= 0; i--)
        {
            if (lines[i].Trim() == "{" && FindFunctionHeaderStart(lines, i) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFunctionHeaderStart(string[] lines, int braceLine)
    {
        for (var i = braceLine - 1; i >= 0 && i >= braceLine - 12; i--)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (FunctionNameRegex().IsMatch(line))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindFunctionEndLine(string[] lines, int braceLine)
    {
        var depth = 0;
        for (var i = braceLine; i < lines.Length; i++)
        {
            depth += CountBraceDeltaOutsideLiterals(lines[i]);
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static int CountBraceDeltaOutsideLiterals(string line)
    {
        var depth = 0;
        var inSingle = false;
        var inDouble = false;
        var escape = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if ((inSingle || inDouble) && ch == '\\')
            {
                escape = true;
                continue;
            }

            if (!inDouble && ch == '\'')
            {
                inSingle = !inSingle;
                continue;
            }

            if (!inSingle && ch == '"')
            {
                inDouble = !inDouble;
                continue;
            }

            if (inSingle || inDouble)
            {
                continue;
            }

            if (ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                break;
            }

            if (ch == '{')
            {
                depth++;
            }
            else if (ch == '}')
            {
                depth--;
            }
        }

        return depth;
    }

    private static bool ContainsSourceSendCall(string body)
    {
        return body.Contains("_opd_FUN_008bc978(", StringComparison.Ordinal)
            || body.Contains("FUN_0039f860(", StringComparison.Ordinal);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ExtractBuilderCallsites(string body)
    {
        var callsites = new List<IReadOnlyList<string>>();
        foreach (Match match in SourceSendCallRegex().Matches(body))
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
                depth--;
            }
            else if (ch == ',' && depth == 0)
            {
                result.Add(CleanExpression(args[start..i]));
                start = i + 1;
            }
        }

        result.Add(CleanExpression(args[start..]));
        return result;
    }

    private static string CleanExpression(string expression)
    {
        return Regex.Replace(expression, @"\s+", " ").Trim();
    }

    private static string Preview(string body)
    {
        return string.Join('\n', body
            .Split('\n')
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(24));
    }

    [GeneratedRegex(@"\b(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(")]
    private static partial Regex FunctionNameRegex();

    [GeneratedRegex(@"(?:_opd_FUN_008bc978|FUN_0039f860)\s*\(")]
    private static partial Regex SourceSendCallRegex();

    [GeneratedRegex(@"\b(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8})\s*\(")]
    private static partial Regex FunctionCallRegex();

    private sealed record ExportedSendFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string Body);
}

public sealed record Tf2Ps3SourceSendCallsiteMapReport(
    string Status,
    string Note,
    string CExportInput,
    Tf2Ps3SourceSendCallsiteMapSummary Summary,
    Tf2Ps3SourceSendBoundary NativeSendWrapperBoundary,
    Tf2Ps3SourceSendBoundary NativeQueueBoundary,
    Tf2Ps3SourceSendBoundary SnapshotWriterBoundary,
    Tf2Ps3SourceSendCallsiteFunction[] Functions,
    string[] Conclusions);

public sealed record Tf2Ps3SourceSendCallsiteMapSummary(
    int LocatedSourceSendFunctions,
    int SourceSendCallsiteCount,
    int DirectTypedMessageFunctionCount,
    int FormattedStatusOrEventFunctionCount,
    int SnapshotFrameFunctionCount,
    int ThinWrapperForwarderCount,
    int CallsitesUsingBitSidecar,
    int CallsitesRequestingWrapOrCompression,
    int UpstreamWriterOwnedCallsiteCount,
    int TransportOnlyCallsiteCount,
    string SnapshotEntityDeltaWriterAddress,
    string SnapshotFrameBuilderAddress,
    string NativeSendWrapperAddress,
    string NativeQueueAddress);

public sealed record Tf2Ps3SourceSendBoundary(
    string AddressOrRoute,
    string Role,
    string Evidence,
    string Meaning);

public sealed record Tf2Ps3SourceSendCallsiteFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string UpstreamOwner,
    string[] Calls,
    string[] WriterSignals,
    Tf2Ps3SourceSendCallsite[] Callsites,
    string Preview,
    string Conclusion);

public sealed record Tf2Ps3SourceSendCallsite(
    int Index,
    string UnknownSendModeExpression,
    string SlotOrChannelExpression,
    string PeerAddressExpression,
    string PayloadExpression,
    string LengthExpression,
    string BitPayloadExpression,
    string WrapOrCompressionFlagExpression,
    string Ownership,
    string PayloadOrigin,
    string LengthRole,
    string TransportMutation,
    string OwnershipReason);
