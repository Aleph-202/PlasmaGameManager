using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceObjectStreamBootstrapReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Tf2Ps3SourceObjectStreamTarget[] Targets =
    [
        new("00a56fc8", "source-bootstrap-entry", "Connection/map setup entry. It prepares the Source object stream and calls the full bootstrap batch at 00a56cb0."),
        new("00a56cb0", "source-bootstrap-batch", "Full initial bootstrap batch. It calls server-info/string-table setup, emits class-info style records, and sends those records through 00a55e60."),
        new("00a567b0", "server-info-stringtable-batch", "Initial SVC_ServerInfo, SVC_SendTable, SVC_CreateStringTable, and follow-up string/class payload owner. It sends the assembled bit-buffer through 00a55e60 with kind 1."),
        new("00a56a50", "stringtable-update-batch", "Post-bootstrap/update route. It emits SVC_UpdateStringTable and player/resource data, then sends the bit-buffer through 00a55e60 with kind 2."),
        new("00a55e60", "object-stream-envelope-sender", "Recovered object-stream sender. It appends a five-bit terminator/control field, writes object-stream header fields, and appends the raw SVC bit-buffer."),
        new("0086bfa8", "object-stream-kind-and-owner-wrapper", "Thin wrapper into 00734a78."),
        new("00734a78", "object-stream-kind-and-owner-writer", "Writes one byte message kind and one 32-bit owner/callback id into the object stream."),
        new("0086c438", "object-stream-sidecar-wrapper", "Thin wrapper into 00733888."),
        new("00733888", "object-stream-sidecar-writer", "Copies a fixed 0x4c-byte zeroed sidecar into the object stream."),
        new("0086f698", "object-stream-sequence-wrapper", "Thin wrapper into 007347c0."),
        new("007347c0", "object-stream-sequence-writer", "Writes two 32-bit sequence/counter fields into the object stream."),
        new("0086a648", "object-stream-payload-wrapper", "Thin wrapper into 007345e8."),
        new("007345e8", "object-stream-payload-writer", "Writes a 32-bit payload byte length followed by the raw payload bytes."),
        new("0086d4c8", "object-stream-flush-wrapper", "Thin wrapper into 00733690."),
        new("00733690", "object-stream-flush-or-offset", "Flush/current-offset helper used after object-stream payload append."),
        new("00a61150", "snapshot-frame-builder-contrast", "Gameplay snapshot path. It calls 008bc978 directly, unlike the bootstrap object-stream path."),
        new("008bc978", "native-snapshot-send-wrapper-contrast", "Native snapshot/staged send wrapper used by gameplay frames, not directly by the recovered bootstrap batch.")
    ];

    private static readonly Dictionary<string, string> CallRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["00a56cb0"] = "source-bootstrap-batch",
        ["00a567b0"] = "server-info-stringtable-batch",
        ["00a56a50"] = "stringtable-update-batch",
        ["00a55e60"] = "object-stream-envelope-sender",
        ["008cec08"] = "SVC_ServerInfo",
        ["008d3248"] = "SVC_SendTable",
        ["008dadc8"] = "SVC_CreateStringTable-helper",
        ["008d27c0"] = "bootstrap-string/class-followup-writer",
        ["008ce770"] = "SVC_ClassInfo",
        ["008cd018"] = "bootstrap-small-control-writer",
        ["008cd0b8"] = "bootstrap-object-control-writer",
        ["008da9e0"] = "SVC_UpdateStringTable-helper",
        ["0086bfa8"] = "object-stream-kind-and-owner-wrapper",
        ["00734a78"] = "object-stream-kind-and-owner-writer",
        ["0086c438"] = "object-stream-sidecar-wrapper",
        ["00733888"] = "object-stream-sidecar-writer",
        ["0086f698"] = "object-stream-sequence-wrapper",
        ["007347c0"] = "object-stream-sequence-writer",
        ["0086a648"] = "object-stream-payload-wrapper",
        ["007345e8"] = "object-stream-payload-writer",
        ["0086d4c8"] = "object-stream-flush-wrapper",
        ["00733690"] = "object-stream-flush-or-offset",
        ["008bc978"] = "native-snapshot-send-wrapper",
        ["00a5fb80"] = "entity-delta-writer"
    };

    public static async Task<Tf2Ps3SourceObjectStreamBootstrapReport> ReduceAsync(
        string cExportPath,
        string criticalBootstrapRoutePath,
        string outputPath)
    {
        var sourceLines = await File.ReadAllLinesAsync(cExportPath);
        var ranges = FindFunctionRanges(sourceLines);
        var targetRows = Targets
            .Select(target => BuildTarget(sourceLines, ranges, target))
            .ToArray();
        var bootstrapRows = targetRows
            .Where(static target => target.Role is
                "source-bootstrap-entry"
                or "source-bootstrap-batch"
                or "server-info-stringtable-batch"
                or "stringtable-update-batch"
                or "object-stream-envelope-sender")
            .ToArray();
        var bootstrapInlineCallsSnapshotWrapper = bootstrapRows
            .Any(static target => target.Calls.Any(static call => string.Equals(call.TargetFunction, "008bc978", StringComparison.OrdinalIgnoreCase)));
        var objectStreamCalls = bootstrapRows
            .Any(static target => target.Calls.Any(static call => string.Equals(call.TargetFunction, "00a55e60", StringComparison.OrdinalIgnoreCase)))
            && targetRows.Single(static target => target.Address == "00a55e60").Calls.Any(static call => call.TargetFunction is "0086bfa8" or "0086c438" or "0086f698" or "0086a648");

        var report = new Tf2Ps3SourceObjectStreamBootstrapReport(
            "tf2ps3-source-object-stream-bootstrap-map",
            "Places TF.elf critical Source bootstrap netmessages into their native object-stream sender. This report separates initial bootstrap transport from the later gameplay snapshot peer-channel route.",
            cExportPath,
            criticalBootstrapRoutePath,
            new Tf2Ps3SourceObjectStreamBootstrapSummary(
                Targets.Length,
                targetRows.Count(static target => target.Located),
                bootstrapRows.Sum(static target => target.Calls.Length),
                objectStreamCalls,
                bootstrapInlineCallsSnapshotWrapper,
                targetRows.Single(static target => target.Address == "00a61150").Calls.Any(static call => call.TargetFunction == "008bc978"),
                ObjectStreamEnvelopeFields.Length),
            targetRows,
            ObjectStreamEnvelopeFields,
            BuildRoutes(targetRows),
            [
                new(
                    "initial-bootstrap-transport",
                    "00a56fc8 -> 00a56cb0 -> 00a567b0/00a55e60 and 00a56cb0 -> 00a55e60 show initial SVC bootstrap bytes are sent through the Source object-stream sender.",
                    "Initial SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, and string-table bootstrap should be wrapped as object-stream records before native UDP emission."),
                new(
                    "object-stream-envelope",
                    "00a55e60 calls 0086bfa8, 0086c438, 0086f698, and 0086a648; those thin wrappers lead to 00734a78, 00733888, 007347c0, and 007345e8.",
                    "The recovered envelope is kind/u32 owner, 0x4c-byte sidecar, two u32 sequence fields, u32 payload byte length, and payload bytes."),
                new(
                    "bootstrap-vs-snapshot-route",
                    bootstrapInlineCallsSnapshotWrapper
                        ? "A bootstrap target unexpectedly calls 008bc978 directly; recheck this report before changing production sender behavior."
                        : "No recovered bootstrap target calls 008bc978 inline; 00a61150 is the separate gameplay snapshot route that does call 008bc978.",
                    "Do not place initial bootstrap raw SVC frames directly into the gameplay snapshot send wrapper.")
            ],
            [
                "Implement a native object-stream bootstrap encoder before using critical SVC netmessage builders in live production.",
                "Keep gameplay entity snapshots on the 00a61150 -> 008bc978 route; do not merge bootstrap object-stream records and snapshot frames.",
                "The next live-server step is matching object-stream record timing/sequence counters against captured quick-match-to-MOTD flows."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceObjectStreamTargetRow BuildTarget(
        string[] sourceLines,
        IReadOnlyDictionary<string, Tf2Ps3ObjectStreamFunctionRange> ranges,
        Tf2Ps3SourceObjectStreamTarget target)
    {
        if (!ranges.TryGetValue(target.Address, out var range))
        {
            return new Tf2Ps3SourceObjectStreamTargetRow(
                target.Address,
                target.Role,
                target.Meaning,
                false,
                0,
                0,
                [],
                [],
                []);
        }

        var calls = new List<Tf2Ps3SourceObjectStreamCall>();
        var evidence = new List<string>();
        var constants = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = range.StartLineIndex; i < range.EndLineIndex; i++)
        {
            var line = sourceLines[i];
            foreach (Match call in FunctionCallRegex().Matches(line))
            {
                var address = NormalizeAddress(call.Groups["address"].Value);
                if (string.Equals(address, target.Address, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (CallRoles.TryGetValue(address, out var role))
                {
                    calls.Add(new Tf2Ps3SourceObjectStreamCall(
                        i + 1,
                        address,
                        role,
                        line.Trim(),
                        Preview(sourceLines, i)));
                }
            }

            foreach (Match constant in HexConstantRegex().Matches(line))
            {
                constants.Add(constant.Value.ToLowerInvariant());
            }

            if (line.Contains("param_3[3]", StringComparison.Ordinal)
                || line.Contains("0x4c", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x162", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x160", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x534", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x544", StringComparison.OrdinalIgnoreCase)
                || line.Contains("0x568", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add($"{i + 1}: {line.Trim()}");
            }
        }

        return new Tf2Ps3SourceObjectStreamTargetRow(
            target.Address,
            target.Role,
            target.Meaning,
            true,
            range.StartLineIndex + 1,
            range.EndLineIndex,
            calls.ToArray(),
            constants.Take(24).ToArray(),
            evidence.Take(24).ToArray());
    }

    private static Tf2Ps3SourceObjectStreamRoute[] BuildRoutes(
        IReadOnlyCollection<Tf2Ps3SourceObjectStreamTargetRow> targetRows)
    {
        return
        [
            new(
                "initial-bootstrap",
                ["00a56fc8", "00a56cb0", "00a567b0", "00a55e60", "0086bfa8/0086c438/0086f698/0086a648", "00734a78/00733888/007347c0/007345e8"],
                HasCall(targetRows, "00a56fc8", "00a56cb0")
                    && HasCall(targetRows, "00a56cb0", "00a567b0")
                    && HasCall(targetRows, "00a567b0", "00a55e60")
                    && HasObjectStreamEnvelopeCalls(targetRows),
                "Entry/setup reaches the initial SVC bootstrap batch and wraps the resulting raw bit-buffer in object-stream records."),
            new(
                "stringtable-update",
                ["00874458", "00a56a50", "00a55e60", "object-stream-envelope"],
                HasCall(targetRows, "00a56a50", "008da9e0")
                    && HasCall(targetRows, "00a56a50", "00a55e60"),
                "Post-bootstrap string-table/player-resource updates reuse the same object-stream envelope with kind 2."),
            new(
                "gameplay-snapshot",
                ["00a61150", "00a5fb80", "008bc978"],
                HasCall(targetRows, "00a61150", "00a5fb80")
                    && HasCall(targetRows, "00a61150", "008bc978"),
                "Gameplay snapshots use the native snapshot frame and send-wrapper route, not the initial bootstrap object-stream route.")
        ];
    }

    private static bool HasObjectStreamEnvelopeCalls(IReadOnlyCollection<Tf2Ps3SourceObjectStreamTargetRow> targetRows)
    {
        return HasCall(targetRows, "00a55e60", "0086bfa8")
            && HasCall(targetRows, "00a55e60", "0086c438")
            && HasCall(targetRows, "00a55e60", "0086f698")
            && HasCall(targetRows, "00a55e60", "0086a648");
    }

    private static bool HasCall(
        IReadOnlyCollection<Tf2Ps3SourceObjectStreamTargetRow> targetRows,
        string source,
        string target)
    {
        var row = targetRows.SingleOrDefault(row => string.Equals(row.Address, source, StringComparison.OrdinalIgnoreCase));
        return row?.Calls.Any(call => string.Equals(call.TargetFunction, target, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static Dictionary<string, Tf2Ps3ObjectStreamFunctionRange> FindFunctionRanges(string[] sourceLines)
    {
        var starts = new List<Tf2Ps3ObjectStreamFunctionStart>();
        for (var i = 0; i < sourceLines.Length; i++)
        {
            var line = sourceLines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            var definition = FunctionNameRegex().Match(line);
            if (definition.Success)
            {
                starts.Add(new Tf2Ps3ObjectStreamFunctionStart(i, NormalizeAddress(definition.Groups["address"].Value)));
            }
        }

        var ranges = new Dictionary<string, Tf2Ps3ObjectStreamFunctionRange>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].StartLineIndex : sourceLines.Length;
            ranges[start.Address] = new Tf2Ps3ObjectStreamFunctionRange(start.StartLineIndex, end);
        }

        return ranges;
    }

    private static string Preview(string[] sourceLines, int index)
    {
        var start = Math.Max(0, index - 3);
        var end = Math.Min(sourceLines.Length, index + 4);
        return string.Join('\n', sourceLines[start..end].Select(static line => line.TrimEnd()));
    }

    private static string NormalizeAddress(string value)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return value.ToLowerInvariant().PadLeft(8, '0');
    }

    private static readonly Tf2Ps3SourceObjectStreamEnvelopeField[] ObjectStreamEnvelopeFields =
    [
        new("terminator/control", "00a55e60", "param_3[3] plus five-bit mask/advance", "Before envelope send, TF.elf advances the source bit-buffer by five bits when payload bits are present."),
        new("message-kind", "00734a78", "one byte from param_2", "Kind 1 is used by initial bootstrap records; kind 2 is used by the string-table update path."),
        new("owner-or-callback-id", "00734a78", "32-bit param_3", "The sender obtains this from the object vtable callback at *param_1+0x0c."),
        new("sidecar", "00733888", "0x4c-byte fixed copy", "00a55e60 zeroes a 0x4c-byte local sidecar and copies it into the object stream."),
        new("sequence-a", "007347c0", "32-bit param_2", "First copy of param_1[0x162], the object-stream sequence before increment."),
        new("sequence-b", "007347c0", "32-bit param_3", "Second copy of param_1[0x162], matching the first in 00a55e60."),
        new("payload-length", "007345e8", "32-bit byte count", "Byte count is computed as (payloadBitCount + 7) >> 3."),
        new("payload-bytes", "007345e8", "raw byte copy", "The raw SVC bit-buffer bytes are appended after the length field."),
        new("flush/current-offset", "00733690", "current object stream write offset", "Optional flush/current-offset helper after payload append when the debug/time cvar threshold is active.")
    ];

    [GeneratedRegex(@"(?:_opd_)?FUN_(?<address>[0-9a-fA-F]{8})\(")]
    private static partial Regex FunctionNameRegex();

    [GeneratedRegex(@"(?:_opd_)?FUN_(?<address>[0-9a-fA-F]{8})")]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex HexConstantRegex();
}

public sealed record Tf2Ps3SourceObjectStreamBootstrapReport(
    string Status,
    string Note,
    string CExportInput,
    string CriticalBootstrapRouteInput,
    Tf2Ps3SourceObjectStreamBootstrapSummary Summary,
    Tf2Ps3SourceObjectStreamTargetRow[] Targets,
    Tf2Ps3SourceObjectStreamEnvelopeField[] EnvelopeFields,
    Tf2Ps3SourceObjectStreamRoute[] Routes,
    Tf2Ps3SourceObjectStreamFinding[] Findings,
    string[] RemainingWork);

public sealed record Tf2Ps3SourceObjectStreamBootstrapSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int BootstrapRelevantCallCount,
    bool BootstrapUsesObjectStreamSender,
    bool BootstrapCallsSnapshotSendWrapperInline,
    bool GameplaySnapshotCallsSnapshotSendWrapper,
    int ObjectStreamEnvelopeFieldCount);

public sealed record Tf2Ps3SourceObjectStreamTargetRow(
    string Address,
    string Role,
    string Meaning,
    bool Located,
    int StartLine,
    int EndLine,
    Tf2Ps3SourceObjectStreamCall[] Calls,
    string[] HexConstants,
    string[] FieldEvidence);

public sealed record Tf2Ps3SourceObjectStreamCall(
    int Line,
    string TargetFunction,
    string Role,
    string Statement,
    string Preview);

public sealed record Tf2Ps3SourceObjectStreamEnvelopeField(
    string Name,
    string Function,
    string Encoding,
    string Meaning);

public sealed record Tf2Ps3SourceObjectStreamRoute(
    string Name,
    string[] Chain,
    bool Proven,
    string Meaning);

public sealed record Tf2Ps3SourceObjectStreamFinding(
    string Name,
    string Evidence,
    string Meaning);

internal sealed record Tf2Ps3SourceObjectStreamTarget(
    string Address,
    string Role,
    string Meaning);

internal sealed record Tf2Ps3ObjectStreamFunctionStart(int StartLineIndex, string Address);

internal sealed record Tf2Ps3ObjectStreamFunctionRange(int StartLineIndex, int EndLineIndex);
