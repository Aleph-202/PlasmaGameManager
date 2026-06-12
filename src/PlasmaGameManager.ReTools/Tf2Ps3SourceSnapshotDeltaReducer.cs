using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceSnapshotDeltaReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceSnapshotDeltaReport> ReduceAsync(string cExportPath, string outputPath)
    {
        var text = await File.ReadAllTextAsync(cExportPath);
        var functions = new[]
        {
            BuildFunction(text, "00a61150", "snapshot-frame-caller", "Calls the entity delta writer, mirrors its result into snapshot flag bit 0x01, and forwards the completed frame to 008bc978."),
            BuildFunction(text, "00a5fb80", "entity-delta-writer", "Writes active entity delta groups into the snapshot bitstream using narrow native bit fields."),
            BuildFunction(text, "00a5b920", "large-active-group-compressor", "Compresses/queues large active group payloads and sets the queued-handle-present descriptor flag consumed by 00a5fb80."),
            BuildFunction(text, "00a586e0", "first-group-bootstrap-sender", "Bootstraps the first delta group when it has no sent bytes yet."),
            BuildFunction(text, "00a57df0", "delta-run-budget-selector", "Budgets object/entity run windows and toggles the next active group bit before 00a5fb80 emits it."),
            BuildFunction(text, "00a5e9e0", "queued-bitstream-cache", "Copies queued entity/object bitstreams into 0x130-byte descriptors consumed by the entity delta writer."),
            BuildFunction(text, "00a5a820", "snapshot-history-slot-update", "Updates the per-channel rolling snapshot history ring after a frame is sent."),
            BuildFunction(text, "00a579d8", "snapshot-history-stats-update", "Maintains rolling per-channel snapshot history/stat windows used by later delta selection.")
        };

        var report = new Tf2Ps3SourceSnapshotDeltaReport(
            "tf2ps3-source-snapshot-delta-map",
            "Reduces TF.elf snapshot entity-delta helper functions into exact field widths and state-machine evidence for the generated PS3 Source server.",
            cExportPath,
            new Tf2Ps3SourceSnapshotDeltaSummary(
                functions.Length,
                functions.Count(static function => function.Located),
                functions.Sum(static function => function.FieldEvidence.Length),
                EntityDeltaFields.Length,
                QueuedBitstreamFields.Length,
                GroupPreparationFields.Length,
                HistoryFields.Length),
            functions,
            EntityDeltaFields,
            QueuedBitstreamFields,
            GroupPreparationFields,
            HistoryFields,
            [
                "Implement native entity delta group encoding before treating snapshot extra bytes as opaque gameplay payloads.",
                "Populate queued bitstream descriptors from semantic entity/player/object state instead of PCAP replay chunks.",
                "Preserve TF.elf history-ring accounting so generated frames can select correct base/ack and delta windows.",
                "Keep live generated responder conservative until object record payload contents are fully mapped."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceSnapshotDeltaFunction BuildFunction(string text, string address, string role, string conclusion)
    {
        var body = ExtractFunction(text, address);
        return new Tf2Ps3SourceSnapshotDeltaFunction(
            address,
            role,
            body.Length > 0,
            ExtractCalls(body),
            ExtractHexConstants(body),
            ExtractFieldEvidence(address),
            Preview(body),
            conclusion);
    }

    private static Tf2Ps3SourceSnapshotDeltaFieldEvidence[] ExtractFieldEvidence(string address)
    {
        return address switch
        {
            "00a61150" =>
            [
                new("delta-present-result", "_opd_FUN_00a5fb80(param_1,param_2)", "flag-source", "The helper return is ORed into snapshot update flag bit 0x01."),
                new("snapshot-send-after-delta", "_opd_FUN_008bc978(...)", "native-send-wrapper", "Entity delta bytes are emitted before the native send wrapper stages/compresses the payload."),
                new("history-update-after-send", "_opd_FUN_00a5a820 / _opd_FUN_00a579d8", "state-update", "History rings are updated only after send succeeds.")
            ],
            "00a5fb80" =>
            [
                new("delta-group-count", "lVar30 = 8", "constant", "The writer scans eight possible entity delta groups."),
                new("delta-group-record-stride", "puVar28 = puVar28 + 7", "0x1c-byte-stride", "Each group record advances seven 32-bit words."),
                new("delta-group-active-state", "puVar28[5] == 1", "state-filter", "Only active state 1 groups are serialized."),
                new("delta-group-index", "uVar16", "write-3-bits", "The active group index is written as three bits."),
                new("delta-partial-run-branch", "full-group bit / partial-run bit", "write-1-bit", "A zero bit selects the full-group path; a one bit selects the partial entity run path."),
                new("delta-full-group-bit-length", "piVar2[0x43]", "write-17-bits", "Full-group inline payload length is written as a 17-bit count."),
                new("delta-partial-run-start-index", "*puVar17", "write-18-bits", "Partial run start index is written immediately after the partial-run branch bit."),
                new("delta-partial-run-entity-count", "puVar17[2]", "write-3-bits", "Partial run entity/object count is encoded in three bits."),
                new("delta-partial-run-object-descriptor-present", "*piVar2 != 0", "write-1-bit", "When present, the writer emits a 32-bit object id and null-terminated object name before the queued payload branch."),
                new("delta-queued-handle-present", "*(char *)(piVar2 + 0x46)", "write-1-bit", "Overflow/queued payload records set a presence bit before the 26-bit handle."),
                new("delta-queued-handle", "piVar2[0x47]", "write-26-bits", "Queued bitstream descriptors can reference a 26-bit handle/id."),
                new("delta-partial-run-bit-length", "piVar2[0x43]", "write-26-bits", "Partial-run queued/raw payload length is written as a 26-bit count."),
                new("delta-start-index", "*puVar17", "write-18-bits", "Object/entity run start index is encoded in 18 bits."),
                new("delta-entity-count", "puVar17[2]", "write-3-bits", "Object/entity run count is encoded in three bits."),
                new("delta-object-id", "piVar2[0x45]", "write-32-bits", "Object descriptor ids are written as full 32-bit values."),
                new("delta-object-name", "(char *)(piVar2 + 1)", "write-stringz-u8", "Object descriptor names are emitted as null-terminated native strings."),
                new("delta-raw-bitstream-copy", "_opd_FUN_00870958", "raw-bit-copy", "Descriptor payload bits are copied directly after their field headers."),
                new("delta-state-mark-written", "puVar28[5] = 2", "state-transition", "Serialized groups move from active state 1 to written state 2.")
            ],
            "00a5e9e0" =>
            [
                new("descriptor-allocation-size", "0x130", "constant", "Queued bitstream records allocate 0x130 bytes."),
                new("descriptor-buffer-pointer", "local_78[0x42]", "pointer", "Pointer to the copied bitstream buffer."),
                new("descriptor-byte-length", "local_78[0x43]", "bytes", "Stored byte length for the copied bitstream."),
                new("descriptor-bit-length", "local_78[0x44]", "bits", "Stored bit length for later 17-bit/26-bit writer branches."),
                new("descriptor-queued-handle-present", "local_78 + 0x46", "byte-flag", "Presence flag consumed by 00a5fb80 before the 26-bit queued handle."),
                new("descriptor-handle", "local_78[0x47]", "26-bit-compatible-id", "Optional descriptor handle used by 00a5fb80."),
                new("descriptor-large-payload-flag", "local_78 + 0x48", "byte-flag", "Flag set when byte length crosses the native inline threshold while overflow tracking is enabled."),
                new("descriptor-chunk-count", "local_78[0x49] = (byteLen + 0xff) >> 8", "u8-page-count", "Stores rounded 256-byte chunk count."),
                new("descriptor-max-bytes", "96000", "threshold", "Native queue rejects/trims payloads above this bitstream storage threshold."),
                new("descriptor-tail-trim", "5 bits", "bit-alignment", "The builder trims/clears up to five tail bits before queueing.")
            ],
            "00a5b920" =>
            [
                new("large-group-enable-flag", "param_1 + 0x42d", "byte-flag", "Compression/large-group queueing runs only when this native flag is enabled."),
                new("group-scan-count", "two groups", "loop", "The helper scans two candidate group lists before entity-delta emission."),
                new("descriptor-byte-length-threshold", "0x1ff", "threshold", "Only descriptors larger than 511 bytes are considered for compression/queued-handle replacement."),
                new("descriptor-queued-handle-present", "piVar2 + 0x46", "byte-flag", "Successful replacement sets the handle-present flag consumed by 00a5fb80."),
                new("descriptor-handle-source", "piVar2[0x43]", "length-before-replacement", "The old byte length is stored into piVar2[0x47] as the queued handle/id."),
                new("descriptor-byte-length-replacement", "piVar2[0x43] = compressedLength", "bytes", "Successful compression replaces the descriptor byte length."),
                new("descriptor-chunk-count", "piVar2[0x49] = (compressedLength + 0xff) >> 8", "u8-page-count", "Chunk count is recomputed after compression.")
            ],
            "00a586e0" =>
            [
                new("first-group-descriptor-count", "*(param_1 + 0xcc)", "count", "Runs only when the first group has at least one descriptor."),
                new("first-group-object-pointer", "**(param_1 + 0xc0)", "pointer", "Reads the first queued object/descriptor pointer."),
                new("bootstrap-unsent-flag", "object + 0x120", "byte-flag", "The bootstrap path runs only for unsent/initial objects."),
                new("bootstrap-sent-byte-count", "object + 300", "bytes", "Skips bootstrap once bytes have already been sent."),
                new("bootstrap-send-call", "00a585b0(param_1, object)", "state-transition", "Sends/bootstrap-caches the first object payload.")
            ],
            "00a57df0" =>
            [
                new("group-window-base", "00a57da8(param_1)", "pointer", "Returns the active group run-window record used by this helper."),
                new("group-budget-source", "param_1[0x94] >> 8", "budget", "Run budget is derived from the high bytes of the native channel budget field."),
                new("run-start-index", "object[0x128] + object[300]", "index", "Run starts at bytes/items already acknowledged plus sent."),
                new("run-remaining-count", "object[0x124] - start", "count", "Remaining run count is clamped to the current budget."),
                new("second-group-throttle", "param_1 + 0x42c", "byte-flag", "When enabled, second group run count is clamped to one."),
                new("active-group-bit-toggle", "param_1[0x14] ^= 1 << groupIndex", "bitset", "The helper toggles the native active group bit for the selected run window."),
                new("group-state-active", "groupRecord[5] = 1", "state-transition", "Selected group is marked active before 00a5fb80 serializes it."),
                new("group-run-cursor-reset", "groupRecord[4] = 0", "state-reset", "Run cursor/offset is reset when the group becomes active.")
            ],
            "00a5a820" =>
            [
                new("history-channel-stride", "param_2 * 0xc2c", "stride", "Per-channel history blocks are 0xc2c bytes apart."),
                new("history-base-offset", "param_1 + 0x550", "base-offset", "Snapshot history ring state starts at offset 0x550 within the channel object."),
                new("history-ring-size", "idx & 0x3f", "64-entry-ring", "History entries use a 64-slot ring."),
                new("history-entry-stride", "(idx & 0x3f) * 0x30", "0x30-byte-stride", "Each ring entry is 0x30 bytes."),
                new("history-entry-active-byte", "entry + 0x14", "byte-flag", "The helper clears/sets an active byte in each ring entry."),
                new("history-last-frame-field", "base + 0x24", "u32", "The sent frame id/index is stored into the history block.")
            ],
            "00a579d8" =>
            [
                new("stats-channel-stride", "param_2 * 0xc2c", "stride", "Uses the same per-channel 0xc2c-byte history layout."),
                new("stats-base-offset", "0x550", "base-offset", "Operates on the same rolling history/stat window as 00a5a820."),
                new("stats-ring-size", "0x40", "64-entry-ring", "Scans 64 history entries."),
                new("stats-window-entry-stride", "0x30", "0x30-byte-stride", "Maintains per-entry byte/bit accounting over 0x30-byte records."),
                new("stats-window-update", "min/max/range branches", "rolling-stats", "Ghidra shows range/min/max style accounting; names remain provisional until more symbols are recovered.")
            ],
            _ => []
        };
    }

    private static string[] ExtractCalls(string body)
    {
        return FunctionCallRegex().Matches(body)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractHexConstants(string body)
    {
        return HexRegex().Matches(body)
            .Select(static match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ExtractFunction(string text, string address)
    {
        foreach (var marker in new[] { $"_opd_FUN_{address}", $"FUN_{address}" })
        {
            var searchStart = 0;
            while (searchStart < text.Length)
            {
                var start = text.IndexOf(marker, searchStart, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                var brace = text.IndexOf('{', start);
                if (brace < 0)
                {
                    break;
                }

                var semicolon = text.IndexOf(';', start, brace - start);
                if (semicolon >= 0)
                {
                    searchStart = start + marker.Length;
                    continue;
                }

                var depth = 0;
                for (var i = brace; i < text.Length; i++)
                {
                    if (text[i] == '{')
                    {
                        depth++;
                    }
                    else if (text[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return text[start..(i + 1)];
                        }
                    }
                }

                break;
            }
        }

        return "";
    }

    private static string Preview(string body)
    {
        return string.Join('\n', body
            .Split('\n')
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(18));
    }

    private static readonly Tf2Ps3SourceSnapshotDeltaFieldEvidence[] EntityDeltaFields =
    [
        new("group-index", "uVar16", "3 bits", "Selects one of eight entity delta groups."),
        new("partial-run-branch", "branch bit", "1 bit", "Zero selects full-group inline payload; one selects partial entity run."),
        new("full-group-bit-length", "piVar2[0x43]", "17 bits", "Full-group payload bit length."),
        new("partial-run-start-index", "*puVar17", "18 bits", "Start index for an entity/object run."),
        new("partial-run-entity-count", "puVar17[2]", "3 bits", "Small run count for serialized object/entity data."),
        new("object-descriptor-present", "*piVar2 != 0", "1 bit", "Controls optional object id/name fields in the partial-run branch."),
        new("queued-handle", "piVar2[0x47]", "26 bits", "References queued descriptor data when not inlined."),
        new("partial-run-bit-length", "piVar2[0x43]", "26 bits", "Partial-run payload bit length."),
        new("object-id", "piVar2[0x45]", "32 bits", "Object descriptor id."),
        new("object-name", "piVar2 + 1", "stringz-u8", "Object descriptor name."),
        new("raw-bitstream", "00870958", "raw bits", "Payload copy from queued bitstream descriptor.")
    ];

    private static readonly Tf2Ps3SourceSnapshotDeltaFieldEvidence[] QueuedBitstreamFields =
    [
        new("allocation-size", "0x130", "bytes", "Queued descriptor object size."),
        new("buffer-pointer", "local_78[0x42]", "pointer", "Copied payload buffer pointer."),
        new("byte-length", "local_78[0x43]", "bytes", "Copied payload byte length."),
        new("bit-length", "local_78[0x44]", "bits", "Copied payload bit length."),
        new("queued-handle-present", "local_78 + 0x46", "byte", "Presence flag for the 26-bit descriptor handle."),
        new("handle", "local_78[0x47]", "id", "Descriptor id consumed by the entity delta writer."),
        new("large-payload-flag", "local_78 + 0x48", "byte", "Threshold flag set from byte length and native overflow tracking."),
        new("chunk-count", "(byteLen + 0xff) >> 8", "u8", "Rounded 256-byte chunk count.")
    ];

    private static readonly Tf2Ps3SourceSnapshotDeltaFieldEvidence[] GroupPreparationFields =
    [
        new("active-state", "groupRecord[5] == 1", "state", "Only active groups are serialized by 00a5fb80."),
        new("written-state", "groupRecord[5] = 2", "state", "Serialized active groups are marked written after payload emission."),
        new("active-group-bitset", "param_1[0x14]", "bitset", "Tracks selected active group window bits."),
        new("run-budget", "param_1[0x94] >> 8", "count", "Limits how many run entries are activated per frame."),
        new("large-group-threshold", "0x1ff", "bytes", "Large descriptors are candidates for compression/queued-handle replacement."),
        new("large-group-enable-flag", "param_1 + 0x42d", "byte", "Enables large active group compression/queueing."),
        new("second-group-throttle", "param_1 + 0x42c", "byte", "Can clamp the second group run count to one.")
    ];

    private static readonly Tf2Ps3SourceSnapshotDeltaFieldEvidence[] HistoryFields =
    [
        new("channel-stride", "0xc2c", "bytes", "Per-channel history block stride."),
        new("base-offset", "0x550", "bytes", "History base offset."),
        new("ring-size", "0x40", "entries", "64-entry rolling history ring."),
        new("entry-stride", "0x30", "bytes", "History entry stride.")
    ];

    [GeneratedRegex(@"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8})")]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex HexRegex();
}

public sealed record Tf2Ps3SourceSnapshotDeltaReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourceSnapshotDeltaSummary Summary,
    Tf2Ps3SourceSnapshotDeltaFunction[] Functions,
    Tf2Ps3SourceSnapshotDeltaFieldEvidence[] EntityDeltaFields,
    Tf2Ps3SourceSnapshotDeltaFieldEvidence[] QueuedBitstreamFields,
    Tf2Ps3SourceSnapshotDeltaFieldEvidence[] GroupPreparationFields,
    Tf2Ps3SourceSnapshotDeltaFieldEvidence[] HistoryFields,
    string[] NativeImplementationSteps);

public sealed record Tf2Ps3SourceSnapshotDeltaSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int FunctionFieldEvidenceCount,
    int EntityDeltaFieldCount,
    int QueuedBitstreamFieldCount,
    int GroupPreparationFieldCount,
    int HistoryFieldCount);

public sealed record Tf2Ps3SourceSnapshotDeltaFunction(
    string Address,
    string Role,
    bool Located,
    string[] Calls,
    string[] HexConstants,
    Tf2Ps3SourceSnapshotDeltaFieldEvidence[] FieldEvidence,
    string Preview,
    string Conclusion);

public sealed record Tf2Ps3SourceSnapshotDeltaFieldEvidence(
    string Field,
    string Expression,
    string Encoding,
    string Meaning);
