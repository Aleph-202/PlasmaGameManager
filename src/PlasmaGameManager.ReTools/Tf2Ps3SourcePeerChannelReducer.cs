using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourcePeerChannelReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] TargetAddresses =
    [
        "008bc978",
        "008bc490",
        "008bb058",
        "00925858",
        "009252e0",
        "00928298",
        "009265e0"
    ];

    public static async Task<Tf2Ps3SourcePeerChannelReport> ReduceAsync(
        string cExportPath,
        string generatedShapeReportPath,
        string outputPath)
    {
        var source = await File.ReadAllTextAsync(cExportPath);
        var functions = TargetAddresses
            .Select(address => BuildFunction(source, address))
            .ToArray();

        var shapeSummary = await TryReadShapeSummaryAsync(generatedShapeReportPath);
        var capturedVisibleFragmentSentinel = shapeSummary?.CapturedNearMtuBodyPrefixSamples.Any(static sample =>
            sample.StartsWith("fffffffe", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("feffffff", StringComparison.OrdinalIgnoreCase)) ?? false;
        var generatedVisibleFragmentSentinel = shapeSummary?.GeneratedNearMtuBodyPrefixSamples.Any(static sample =>
            sample.StartsWith("fffffffe", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("feffffff", StringComparison.OrdinalIgnoreCase)) ?? false;

        var report = new Tf2Ps3SourcePeerChannelReport(
            "tf2ps3-source-peer-channel-map",
            "Maps the lower TF.elf Source peer-channel layer below semantic payload builders. This is the missing native envelope/queue layer between current generated payloads and packets accepted by the PS3 client.",
            cExportPath,
            generatedShapeReportPath,
            new Tf2Ps3SourcePeerChannelSummary(
                TargetAddresses.Length,
                functions.Count(static function => function.Located),
                PeerChannelFields.Length,
                shapeSummary?.CapturedNearMtuPacketCount ?? 0,
                shapeSummary?.GeneratedNearMtuPacketCount ?? 0,
                capturedVisibleFragmentSentinel,
                generatedVisibleFragmentSentinel,
                shapeSummary?.ShapeMatchedTurnCount ?? 0,
                shapeSummary?.TurnCount ?? 0),
            functions,
            PeerChannelFields,
            [
                generatedVisibleFragmentSentinel
                    ? "Generated near-MTU packets still expose the direct 008bc490 fragment sentinel; map loading remains blocked on the peer-channel queue/envelope."
                    : "Generated near-MTU packets now avoid exposing the direct 008bc490 fragment sentinel, matching the markerless captured packet boundary.",
                "TF.elf sends semantic bodies through 008bc978, then through 008bb058/00925858/009252e0. That path copies the payload into queue-owned buffers, associates it with local peer ports 0x697d/0x6987, and commits descriptor pairs into a native reliable queue.",
                "The next runtime step should model this peer-channel envelope instead of sending semantic bitstreams as direct UDP bodies. Treat visible semantic payload generation as necessary but not sufficient for map entry."
            ],
            [
                new("direct-udp-body-is-too-shallow", "Current Ps3SourceTransportPacket.Encode adds only a two-byte sequence before the body. TF.elf has a deeper peer-channel queue/envelope after 008bc978."),
                new("fragment-header-visibility-mismatch", capturedVisibleFragmentSentinel
                    ? "At least one captured near-MTU packet exposes a native fragment sentinel; direct fragment header compatibility should be rechecked per-flow."
                    : generatedVisibleFragmentSentinel
                        ? "Captured near-MTU packets in the selected report do not expose 0xfffffffe/feffffff immediately after the sequence field, while current generated fragments do."
                        : "Captured and generated near-MTU packets are both markerless at the current decode boundary; remaining work is packet cadence and semantic payload fidelity."),
                new("source-map-loading-risk", "Until the peer-channel envelope is implemented, reaching the loading/performance-report screen is expected, but clean map entry is not guaranteed.")
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourcePeerChannelFunction BuildFunction(string text, string address)
    {
        var body = ExtractFunction(text, address);
        return new Tf2Ps3SourcePeerChannelFunction(
            address,
            ClassifyRole(address),
            body.Length > 0,
            ExtractCalls(body),
            ExtractHexConstants(body),
            ExtractEvidenceTokens(body),
            Preview(body),
            DescribeConclusion(address));
    }

    private static async Task<Tf2Ps3SourcePeerChannelShapeSummary?> TryReadShapeSummaryAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        if (!document.RootElement.TryGetProperty("Files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var turnCount = 0;
        var shapeMatchedTurnCount = 0;
        var capturedNearMtuPacketCount = 0;
        var generatedNearMtuPacketCount = 0;
        var capturedSamples = new List<string>();
        var generatedSamples = new List<string>();
        foreach (var file in files.EnumerateArray())
        {
            turnCount += ReadInt(file, "TurnCount");
            shapeMatchedTurnCount += ReadInt(file, "ShapeMatchedTurnCount");
            if (!file.TryGetProperty("SampleTurns", out var turns) || turns.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var turn in turns.EnumerateArray())
            {
                CountNearMtuPackets(turn, "CapturedServerPackets", ref capturedNearMtuPacketCount, capturedSamples);
                CountNearMtuPackets(turn, "GeneratedServerPackets", ref generatedNearMtuPacketCount, generatedSamples);
            }
        }

        return new Tf2Ps3SourcePeerChannelShapeSummary(
            turnCount,
            shapeMatchedTurnCount,
            capturedNearMtuPacketCount,
            generatedNearMtuPacketCount,
            capturedSamples.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToArray(),
            generatedSamples.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToArray());
    }

    private static void CountNearMtuPackets(
        JsonElement element,
        string propertyName,
        ref int count,
        List<string>? capturedSamples)
    {
        if (!element.TryGetProperty(propertyName, out var packets) || packets.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var packet in packets.EnumerateArray())
        {
            var shape = ReadString(packet, "Shape");
            var payloadLength = ReadInt(packet, "PayloadLength");
            var bodyLength = ReadInt(packet, "BodyLength");
            if (shape is not ("NearMtuFragment" or "FragmentedSendCandidate") && payloadLength < 1000 && bodyLength < 998)
            {
                continue;
            }

            count++;
            capturedSamples?.Add(ReadString(packet, "BodyPrefixHex"));
        }
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static string ClassifyRole(string address)
    {
        return address switch
        {
            "008bc978" => "semantic-payload-stager",
            "008bc490" => "native-fragment-body-builder",
            "008bb058" => "direct-peer-channel-entry",
            "00925858" => "local-or-remote-peer-router",
            "009252e0" => "reliable-peer-payload-queue",
            "00928298" => "peer-slot-allocator",
            "009265e0" => "peer-queue-descriptor-commit",
            _ => "peer-channel-helper"
        };
    }

    private static string DescribeConclusion(string address)
    {
        return address switch
        {
            "008bc978" => "Stages the semantic Source payload, optional bit sidecar, and optional transform before choosing direct peer-channel send or fragmentation.",
            "008bc490" => "Builds native fragment bodies with a sentinel/counter/packed total-index header, then submits each fragment through 008bb058.",
            "008bb058" => "Builds local peer endpoint records on ports 0x697d/0x6987 and routes through 00925858 instead of a raw sendto body.",
            "00925858" => "Chooses remote vtable send for non-local addresses or local queue paths at object+0x28/object+0x0c based on local peer port.",
            "009252e0" => "Copies payload bytes into a queue-owned buffer, creates/fetches a peer slot, and commits the descriptor through 009265e0.",
            "00928298" => "Allocates a 0x18-byte peer slot keyed by peer address and port, then stores the queue head pointer.",
            "009265e0" => "Commits an 8-byte descriptor pair into the selected queue ring.",
            _ => ""
        };
    }

    private static string[] ExtractCalls(string body)
    {
        return FunctionCallRegex().Matches(body)
            .Select(static match => match.Value)
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

    private static string[] ExtractEvidenceTokens(string body)
    {
        return EvidenceTokens
            .Where(token => body.Contains(token, StringComparison.Ordinal))
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
            .Take(24));
    }

    private static readonly string[] EvidenceTokens =
    [
        "0xfffffffe",
        "0x697d",
        "0x6987",
        "0x7c",
        "0x18",
        "0x1c",
        "0x28",
        "0x4c",
        "0x4e",
        "0x50",
        "0x80",
        "0x1000",
        "_opd_FUN_008bc490",
        "_opd_FUN_008bb058",
        "_opd_FUN_00925858",
        "_opd_FUN_009252e0",
        "_opd_FUN_00928298",
        "_opd_FUN_009265e0",
        "FUN_0086df98",
        "FUN_00871708",
        "syscall_sys_mutex_lock",
        "syscall_sys_mutex_unlock"
    ];

    private static readonly Tf2Ps3SourcePeerChannelField[] PeerChannelFields =
    [
        new("outer-sequence", "first two UDP bytes", "u16be", "Observed in captured and generated packets. This is not enough to reproduce the native Source body."),
        new("fragment-sentinel", "008bc490 stack header", "u32/native-byte-order", "The native fragment builder writes 0xfffffffe before the per-channel counter/header, but captured near-MTU packets do not expose it at the current decode boundary."),
        new("fragment-channel-counter", "*(channel + 0x0c)", "u32/native-byte-order", "Incremented once per fragmented send before all fragments are emitted."),
        new("fragment-packed-total-index", "lVar19", "u16/native-byte-order", "Starts as total fragment count and adds 0x100 per emitted fragment, giving total in one byte and index in the other."),
        new("fragment-compression-flag", "0x80", "flag-bit", "ORed into the low counter byte when the optional transform path is active."),
        new("local-peer-port-a", "0x697d", "u16", "One local peer endpoint port used by direct-source-send."),
        new("local-peer-port-b", "0x6987", "u16", "Alternate local peer endpoint port used by direct-source-send."),
        new("remote-address-bypass", "param_4+4 != 127.0.0.1 && object+0x48 != param_4+4", "branch", "Non-local peers use a vtable send path rather than the local reliable queue."),
        new("local-primary-queue", "object + 0x28", "queue", "Selected when peer port equals object+0x4c."),
        new("local-secondary-queue", "object + 0x0c", "queue", "Selected when peer port equals object+0x4e."),
        new("queue-payload-copy", "FUN_0086ff98/FUN_00871708", "buffer-copy", "Payload is copied into a queue-owned buffer before slot commit."),
        new("queue-slot-size", "0x18", "constant", "Peer slot stride used by 00928298 and 009252e0."),
        new("queue-slot-address", "slot + 0x0c", "u32", "Peer IPv4 address copied from the endpoint structure."),
        new("queue-slot-port", "slot + 0x10", "u16", "Peer port copied from the endpoint structure."),
        new("queue-head", "slot + 0x14", "pointer", "Queue ring/head pointer used by 009265e0."),
        new("queue-descriptor-size", "8", "constant", "009265e0 writes an 8-byte descriptor pair into the queue ring.")
    ];

    [GeneratedRegex(@"(?:_opd_FUN|FUN)_[0-9a-f]{8}|sendto|recvfrom")]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(@"0x[0-9a-fA-F]+")]
    private static partial Regex HexRegex();
}

public sealed record Tf2Ps3SourcePeerChannelReport(
    string Status,
    string Note,
    string Input,
    string GeneratedShapeReport,
    Tf2Ps3SourcePeerChannelSummary Summary,
    Tf2Ps3SourcePeerChannelFunction[] Functions,
    Tf2Ps3SourcePeerChannelField[] Fields,
    string[] Conclusions,
    Tf2Ps3SourcePeerChannelGap[] RuntimeGaps);

public sealed record Tf2Ps3SourcePeerChannelSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    int FieldCount,
    int CapturedNearMtuPacketCount,
    int GeneratedNearMtuPacketCount,
    bool CapturedNearMtuExposesNativeFragmentSentinel,
    bool GeneratedNearMtuExposesNativeFragmentSentinel,
    int ShapeMatchedTurnCount,
    int TurnCount);

public sealed record Tf2Ps3SourcePeerChannelFunction(
    string Address,
    string Role,
    bool Located,
    string[] Calls,
    string[] HexConstants,
    string[] EvidenceTokens,
    string Preview,
    string Conclusion);

public sealed record Tf2Ps3SourcePeerChannelField(
    string Field,
    string Expression,
    string Encoding,
    string Meaning);

public sealed record Tf2Ps3SourcePeerChannelGap(
    string Gap,
    string Meaning);

public sealed record Tf2Ps3SourcePeerChannelShapeSummary(
    int TurnCount,
    int ShapeMatchedTurnCount,
    int CapturedNearMtuPacketCount,
    int GeneratedNearMtuPacketCount,
    string[] CapturedNearMtuBodyPrefixSamples,
    string[] GeneratedNearMtuBodyPrefixSamples);
