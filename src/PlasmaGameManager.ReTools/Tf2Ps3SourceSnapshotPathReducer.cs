using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceSnapshotPathReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceSnapshotPathReport> ReduceAsync(string cExportPath, string outputPath)
    {
        var text = await File.ReadAllTextAsync(cExportPath);
        var functions = new[]
        {
            BuildFunction(text, "00a61150", "snapshot-frame-builder", "Builds the main TF2 PS3 Source snapshot frame before handing it to 008bc978."),
            BuildFunction(text, "008bc978", "source-send-wrapper", "Stages payload, optional bit sidecar, optional compression/wrap, then sends direct or fragmented."),
            BuildFunction(text, "008bc490", "fragmented-source-send", "Splits large staged Source payloads into 10-byte fragment-header datagrams."),
            BuildFunction(text, "008bb058", "direct-source-send-entry", "Direct send entry that routes into 00925858 instead of raw PC Source UDP."),
            BuildFunction(text, "00925858", "reliable-peer-channel-send-entry", "Resolves local/remote peer channel and calls 009252e0."),
            BuildFunction(text, "009252e0", "reliable-peer-channel-queue", "Copies payload into a native queued packet slot and commits through 009265e0."),
            BuildFunction(text, "00928298", "reliable-peer-slot-allocator", "Allocates or resolves a 0x18-byte reliable peer packet slot."),
            BuildFunction(text, "009265e0", "reliable-peer-slot-commit", "Commits an 8-byte queue pointer pair into the selected slot ring."),
            BuildFunction(text, "0134d0e0", "lzss-wrapper", "Optional Source payload transform that writes an LZSS header and token stream.")
        };

        var report = new Tf2Ps3SourceSnapshotPathReport(
            "tf2ps3-source-snapshot-path-map",
            "Reduces TF.elf Source snapshot/compression/reliable-channel functions into native field contracts for the generated PS3 Source server.",
            cExportPath,
            new Tf2Ps3SourceSnapshotPathSummary(
                functions.Length,
                functions.Count(static function => function.Located),
                SnapshotFrameFields.Length,
                SendWrapperFields.Length,
                FragmentHeaderFields.Length,
                ReliableQueueFields.Length,
                LzssFields.Length),
            functions,
            SnapshotFrameFields,
            SendWrapperFields,
            FragmentHeaderFields,
            ReliableQueueFields,
            LzssFields,
            [
                "Implement snapshot frame generation from named fields: server frame index, acknowledgement/base frame, update flags, pending payload sections, and optional extra bytes.",
                "Implement bit-sidecar framing before compression: two-byte big-endian bit count followed by sidecar bytes.",
                "Implement LZSS wrapper only when native wrap/compression flag path is active and output is smaller/accepted by TF.elf thresholds.",
                "Implement native fragment header and reliable-peer queue semantics before treating long gameplay snapshots as complete."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceSnapshotFunction BuildFunction(string text, string address, string role, string conclusion)
    {
        var body = ExtractFunction(text, address);
        return new Tf2Ps3SourceSnapshotFunction(
            address,
            role,
            body.Length > 0,
            ExtractCalls(body),
            ExtractHexConstants(body),
            ExtractFieldEvidence(address, body),
            Preview(body),
            conclusion);
    }

    private static Tf2Ps3SourceFieldEvidence[] ExtractFieldEvidence(string address, string body)
    {
        return address switch
        {
            "00a61150" =>
            [
                new("snapshot-frame-index", "param_1[2]", "write-s32-biased", "First 32-bit snapshot header field; incremented after send."),
                new("snapshot-base-or-ack-frame", "param_1[3]", "write-s32-biased", "Second 32-bit snapshot header field."),
                new("snapshot-message-id", "0", "write-u8", "Main snapshot message id written as a zero byte."),
                new("snapshot-update-flags", "param_1[6]", "write-u8", "Primary update flags byte."),
                new("snapshot-pending-count", "param_1[7] & 0xff", "write-u8", "Optional count byte, present when param_1[7] > 0 and mirrored into flag bit 0x10."),
                new("snapshot-delta-present-flag", "uVar7 & 0xff", "flag-bit", "00a5fb80 result ORs bit 0x01 into update flags when entity delta content is present."),
                new("snapshot-param2-extra-bytes", "param_2[3]", "raw-byte-copy", "Optional caller-provided raw bytes copied into the bitstream when space remains."),
                new("snapshot-pending-buffer-0x11", "param_1[0x14]", "raw-byte-copy", "Queued pending bytes from param_1+0x11 copied into the frame and then cleared."),
                new("snapshot-pending-buffer-0x1a", "param_1[0x1d]", "raw-byte-copy", "Queued pending bytes from param_1+0x1a copied into the frame and then cleared."),
                new("snapshot-bit-alignment-padding", "5 bits", "bit-mask-clear", "Builder clears/pads up to five bits before sending."),
                new("snapshot-compression-threshold-flag", "global[0x344]/global[0x38c]", "wrap-flag", "iVar14 becomes 1 when configured compression threshold is enabled and payload length crosses it."),
                new("snapshot-send-call", "008bc978(..., payload, byteLen, 0, iVar14)", "native-send-wrapper", "Snapshot uses no bit sidecar but may request compression/wrap.")
            ],
            "008bc978" =>
            [
                new("bit-sidecar-bit-count", "puVar21[3]", "u16be", "Optional sidecar starts with a two-byte big-endian bit count."),
                new("bit-sidecar-byte-length", "(puVar21[3] + 7) >> 3", "derived-length", "Number of sidecar bytes copied after the count."),
                new("wrap-prefix-word", "*(u32*)local_10a8", "u32be", "When wrap is active, first four bytes are copied from a stack word before payload bytes."),
                new("direct-send-threshold", "*puStack_2124[-0x1852]", "threshold", "Payloads larger than this call 008bc490; smaller payloads call 008bb058."),
                new("direct-send-original-length", "iVar5", "length", "Direct path preserves original payload length separately when wrap/sidecar changes staged size.")
            ],
            "008bc490" =>
            [
                new("fragment-sentinel", "0xfffffffe", "u32be", "First four fragment header bytes identify the fragmented send path."),
                new("fragment-packet-counter", "*(iVar9+0xc)", "u32be-with-low-byte-flag", "Per-channel counter increments before fragment emission; low byte carries the compression flag bit."),
                new("fragment-total-count", "lVar19 & 0xff", "u8", "Low byte of the running count value carries total fragment count."),
                new("fragment-index", "(lVar19 >> 8) & 0xff", "u8", "Fragment index is represented by adding 0x100 for each emitted chunk."),
                new("fragment-compressed-flag", "0x80", "flag-bit", "Low counter byte ORs 0x80 when transformed/compressed path was used."),
                new("fragment-header-size", "10", "constant", "Each fragment sends uVar16 + 10 bytes through 008bb058."),
                new("fragment-chunk-size", "*(iVar2 + 4)", "threshold", "Configured chunk size controls fragment payload length.")
            ],
            "009252e0" =>
            [
                new("queue-payload-copy", "FUN_00871708(dst, payload, length)", "memcpy", "Payload bytes are copied into a native queue-owned buffer."),
                new("queue-slot-size", "0x18", "constant", "Packet slot index is multiplied by 0x18."),
                new("queue-peer-address", "param_2 + 4 / +8", "address-port", "Slot key uses peer address and port copied into uStack_68."),
                new("queue-commit", "009265e0(*(slot+0x14), auStack_70, ...)", "slot-commit", "Commits copied payload buffer into selected queue slot.")
            ],
            "00928298" =>
            [
                new("queue-slot-size", "0x18", "constant", "Allocator writes fields at index * 0x18 + base."),
                new("slot-vtable", "PTR_PTR_019743a0", "pointer", "Allocated slot receives native queue vtable pointer."),
                new("slot-address", "param_2 + 4", "u32", "Peer address field copied into slot+0x0c."),
                new("slot-port", "param_2 + 8", "u16", "Peer port field copied into slot+0x10."),
                new("slot-queue-head", "param_2 + 0xc", "pointer", "Queue ring/head copied into slot+0x14.")
            ],
            "009265e0" =>
            [
                new("queue-ring-index", "00926558(param_1,param_1[3],...)", "index", "Commit resolves next queue ring index."),
                new("queue-entry-size", "8", "constant", "Commit writes an 8-byte pair at index*8 + base."),
                new("queue-entry-data", "param_2[0..1]", "u64-pair", "Commits copied payload descriptor pair.")
            ],
            "0134d0e0" =>
            [
                new("lzss-min-input", "0x10", "threshold", "Transform runs only when input length exceeds 16 bytes."),
                new("lzss-magic", "0x4c5a5353", "u32be", "Output starts with ASCII LZSS."),
                new("lzss-uncompressed-length", "param_4[1]", "u32le-on-wire", "Stored length is byte-swapped by the native reader before decode."),
                new("lzss-window-bucket-count", "param_1[2] << 4", "table-size", "Hash/history table size is param_1[2] * 16 bytes."),
                new("lzss-match-minimum", "3", "constant", "Matches shorter than three bytes are emitted as literals."),
                new("lzss-match-maximum", "0x10", "constant", "Search clamps match length to sixteen bytes."),
                new("lzss-backref-distance", "((token[0] << 4) | (token[1] >> 4)) + 1", "12-bit-distance", "Back-reference copies from current output minus encoded distance minus one."),
                new("lzss-control-byte", "bitfield", "bitfield", "Each control byte is shifted; high bit marks a back-reference token."),
                new("lzss-end-marker", "0x0000", "u16", "Successful output appends two zero bytes."),
                new("lzss-native-savings-gate", "pbVar13 < param_4 + (param_3 - 0x10)", "length-gate", "Native transform rejects output once it no longer saves at least sixteen bytes.")
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

    private static readonly Tf2Ps3SourceFieldEvidence[] SnapshotFrameFields =
    [
        new("snapshot-frame-index", "param_1[2]", "write-s32-biased", "Monotonic snapshot sequence; incremented after send."),
        new("snapshot-base-or-ack-frame", "param_1[3]", "write-s32-biased", "Second frame index/base value."),
        new("snapshot-message-id", "0", "write-u8", "Main snapshot payload family id."),
        new("snapshot-update-flags", "param_1[6] plus bits 0x10/0x01", "u8", "Flags controlling optional pending-count and delta content."),
        new("snapshot-pending-count", "param_1[7]", "u8", "Optional count included when > 0."),
        new("snapshot-extra-sections", "param_2, param_1+0x11, param_1+0x1a", "raw-byte-runs", "Pending raw sections appended when buffer space remains.")
    ];

    private static readonly Tf2Ps3SourceFieldEvidence[] SendWrapperFields =
    [
        new("send-mode", "param_1", "int", "Forwarded to fragmented sender; direct path uses 1."),
        new("channel", "param_2", "int", "Source channel/player slot index."),
        new("peer-address", "param_3", "native-address", "Native peer endpoint object; param_3[0] must be 2 or 3."),
        new("payload-pointer", "param_4", "pointer", "Payload bytes after native bitstream construction."),
        new("payload-length", "param_5", "bytes", "Payload byte count before optional sidecar/wrap."),
        new("bit-sidecar", "param_6", "bitstream", "Optional bitstream sidecar with 16-bit bit count prefix."),
        new("wrap-or-compress", "param_7", "bool", "Requests wrapped/compressed staged payload.")
    ];

    private static readonly Tf2Ps3SourceFieldEvidence[] FragmentHeaderFields =
    [
        new("fragment-header-size", "10", "constant", "Header prepended before each fragment payload."),
        new("fragment-sentinel", "0xfffffffe", "u32be", "First four bytes identify fragmented Source send payloads."),
        new("fragment-packet-counter", "*(iVar9+0xc)", "u32be-with-low-byte-flag", "Per-channel packet counter copied into bytes 4-7; byte 7 may carry the compression flag bit."),
        new("fragment-total-count", "lVar19 & 0xff", "u8", "Low byte of the running count value carries total fragment count."),
        new("fragment-index", "(lVar19 >> 8) & 0xff", "u8", "Fragment index increments by adding 0x100 to the running count value."),
        new("fragment-compressed-flag", "0x80", "flag-bit", "Set in the low packet-counter byte when compression/transform path was active."),
        new("fragment-payload-length", "uVar16", "bytes", "Chunk payload length for this datagram.")
    ];

    private static readonly Tf2Ps3SourceFieldEvidence[] ReliableQueueFields =
    [
        new("slot-size", "0x18", "constant", "Reliable peer packet slot stride."),
        new("commit-entry-size", "8", "constant", "Committed queue descriptor pair size."),
        new("peer-address", "slot+0x0c", "u32", "Peer IPv4 address."),
        new("peer-port", "slot+0x10", "u16", "Peer port."),
        new("queue-head", "slot+0x14", "pointer", "Queue ring/head pointer.")
    ];

    private static readonly Tf2Ps3SourceFieldEvidence[] LzssFields =
    [
        new("minimum-input", "0x10", "threshold", "Inputs <= 16 bytes are not transformed."),
        new("magic", "0x4c5a5353", "u32be", "ASCII LZSS header."),
        new("uncompressed-length", "param_4[1] / 0134ce98 byteswap", "u32le-on-wire", "Original byte length immediately follows the LZSS magic."),
        new("minimum-match", "3", "constant", "Smaller matches emit literals."),
        new("maximum-match", "0x10", "constant", "Maximum encoded match length."),
        new("backref-distance", "12-bit offset plus one", "distance", "Encoded as high eight bits then low nibble; decoder copies from current output minus distance minus one."),
        new("native-savings-gate", "input length - 0x10", "threshold", "Compression is usable only if the staged stream stays below the native savings limit."),
        new("terminator", "0x0000", "u16", "Two zero bytes terminate output.")
    ];

    [GeneratedRegex(@"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8})")]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex HexRegex();
}

public sealed record Tf2Ps3SourceSnapshotPathReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourceSnapshotPathSummary Summary,
    Tf2Ps3SourceSnapshotFunction[] Functions,
    Tf2Ps3SourceFieldEvidence[] SnapshotFrameFields,
    Tf2Ps3SourceFieldEvidence[] SendWrapperFields,
    Tf2Ps3SourceFieldEvidence[] FragmentHeaderFields,
    Tf2Ps3SourceFieldEvidence[] ReliableQueueFields,
    Tf2Ps3SourceFieldEvidence[] LzssFields,
    string[] NativeImplementationSteps);

public sealed record Tf2Ps3SourceSnapshotPathSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int SnapshotFieldCount,
    int SendWrapperFieldCount,
    int FragmentHeaderFieldCount,
    int ReliableQueueFieldCount,
    int LzssFieldCount);

public sealed record Tf2Ps3SourceSnapshotFunction(
    string Address,
    string Role,
    bool Located,
    string[] Calls,
    string[] HexConstants,
    Tf2Ps3SourceFieldEvidence[] FieldEvidence,
    string Preview,
    string Conclusion);

public sealed record Tf2Ps3SourceFieldEvidence(
    string Field,
    string Expression,
    string Encoding,
    string Meaning);
