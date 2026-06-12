using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourcePacketEntitiesPlacementReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourcePacketEntitiesPlacementReport> ReduceAsync(
        string criticalBootstrapRoutePath,
        string snapshotPathMapPath,
        string snapshotDeltaMapPath,
        string outputPath)
    {
        using var criticalRoute = JsonDocument.Parse(await File.ReadAllTextAsync(criticalBootstrapRoutePath));
        using var snapshotPath = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPathMapPath));
        using var snapshotDelta = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotDeltaMapPath));

        var packetEntities = criticalRoute.RootElement
            .GetProperty("Contracts")
            .EnumerateArray()
            .Single(static contract => ReadString(contract, "ClassName") == "SVC_PacketEntities");
        var snapshotFrame = FindFunction(snapshotPath.RootElement, "00a61150");
        var sendWrapper = FindFunction(snapshotPath.RootElement, "008bc978");
        var entityDelta = FindFunction(snapshotDelta.RootElement, "00a5fb80");
        var queuedBitstream = FindFunction(snapshotDelta.RootElement, "00a5e9e0");

        var report = new Tf2Ps3SourcePacketEntitiesPlacementReport(
            "tf2ps3-source-packet-entities-placement-map",
            "Places TF.elf's standalone SVC_PacketEntities message contract against the actual native PS3 Source snapshot/entity-delta route used for live gameplay payloads.",
            criticalBootstrapRoutePath,
            snapshotPathMapPath,
            snapshotDeltaMapPath,
            new Tf2Ps3SourcePacketEntitiesPlacementSummary(
                StandaloneMessageId: 26,
                StandaloneWriterEntry: "008ce4c0",
                StandaloneDirectCallsiteCount: ReadInt(packetEntities, "DirectCallsiteCount"),
                SnapshotFrameBuilderEntry: "00a61150",
                EntityDeltaWriterEntry: "00a5fb80",
                NativeSendWrapperEntry: "008bc978",
                SnapshotFrameFieldCount: ReadInt(snapshotPath.RootElement.GetProperty("Summary"), "SnapshotFieldCount"),
                EntityDeltaFieldCount: ReadInt(snapshotDelta.RootElement.GetProperty("Summary"), "EntityDeltaFieldCount"),
                QueuedBitstreamFieldCount: ReadInt(snapshotDelta.RootElement.GetProperty("Summary"), "QueuedBitstreamFieldCount"),
                NativeGameplayRouteIdentified: Calls(snapshotFrame).Contains("_opd_FUN_00a5fb80", StringComparer.Ordinal)
                    && Calls(snapshotFrame).Contains("_opd_FUN_008bc978", StringComparer.Ordinal)),
            [
                new(
                    "standalone-svc-packetentities-contract",
                    "SVC_PacketEntities has a resolved INetMessage vtable and WriteToBuffer at 008ce4c0, but the direct bootstrap reducer finds no C-visible caller.",
                    "Keep the raw five-bit SVC_PacketEntities codec for signon/bootstrap compatibility, but do not assume this writer is the live gameplay snapshot path."),
                new(
                    "snapshot-frame-builder",
                    "00a61150 writes the native snapshot frame header, calls 00a5fb80, ORs the result into update flag bit 0x01, then forwards the complete frame to 008bc978.",
                    "Live entity/gameplay state should be generated as native snapshot frames, not as PC-style standalone SVC_PacketEntities datagrams."),
                new(
                    "entity-delta-writer",
                    "00a5fb80 scans eight entity delta groups, writes a 3-bit group index, branch bits, object/run fields, optional queued handles, and raw descriptor payload bits.",
                    "The native server must populate semantic entity groups and object descriptors before snapshot send."),
                new(
                    "queued-bitstream-descriptor",
                    "00a5e9e0 builds 0x130-byte queued bitstream descriptors with buffer pointer, byte length, bit length, optional 26-bit handle, large-payload flag, and chunk count.",
                    "Large or reusable entity payloads need descriptor semantics before they are consumed by 00a5fb80."),
                new(
                    "native-send-wrapper",
                    "008bc978 stages snapshot bytes, optional bit sidecars, compression/wrap, and native queue/fragment sends.",
                    "The outer UDP bytes must remain PS3-native peer-channel traffic rather than vanilla srcds connectionless packets.")
            ],
            [
                new(
                    "implemented-codec-boundary",
                    "Ps3SourceNetMessages.BuildPacketEntities plus Ps3SourceSnapshotFrameBuilder and Ps3SourceEntityDeltaFrameBuilder cover the recovered writer widths.",
                    "The server has the primitive encoders needed for the identified native route."),
                new(
                    "production-placement-boundary",
                    "Diagnostic standalone SVC_* bootstrap payloads are safe as analysis artifacts, but production map/gameplay load should flow through snapshot frames once live timing is proven.",
                    "Avoid replay-only insertion of isolated netmessages into the native peer channel."),
                new(
                    "remaining-native-work",
                    "The field route is identified; the missing work is semantic population of TF2 class baselines, entity groups, and per-frame history selection from server state.",
                    "Next implementation work should move object/resource/game-rule state into entity delta descriptors instead of opaque PCAP chunks.")
            ],
            [
                $"snapshot-frame-calls: {string.Join(", ", Calls(snapshotFrame))}",
                $"entity-delta-calls: {string.Join(", ", Calls(entityDelta))}",
                $"send-wrapper-calls: {string.Join(", ", Calls(sendWrapper))}",
                $"queued-bitstream-calls: {string.Join(", ", Calls(queuedBitstream))}"
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static JsonElement FindFunction(JsonElement root, string address)
    {
        return root
            .GetProperty("Functions")
            .EnumerateArray()
            .Single(function => string.Equals(ReadString(function, "Address"), address, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] Calls(JsonElement function)
    {
        return function.GetProperty("Calls")
            .EnumerateArray()
            .Select(static call => call.GetString() ?? "")
            .Where(static call => call.Length > 0)
            .ToArray();
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
}

public sealed record Tf2Ps3SourcePacketEntitiesPlacementReport(
    string Status,
    string Note,
    string CriticalBootstrapRouteInput,
    string SnapshotPathInput,
    string SnapshotDeltaInput,
    Tf2Ps3SourcePacketEntitiesPlacementSummary Summary,
    Tf2Ps3SourcePacketEntitiesPlacementFinding[] Findings,
    Tf2Ps3SourcePacketEntitiesPlacementBoundary[] ImplementationBoundaries,
    string[] NativeCallEvidence);

public sealed record Tf2Ps3SourcePacketEntitiesPlacementSummary(
    int StandaloneMessageId,
    string StandaloneWriterEntry,
    int StandaloneDirectCallsiteCount,
    string SnapshotFrameBuilderEntry,
    string EntityDeltaWriterEntry,
    string NativeSendWrapperEntry,
    int SnapshotFrameFieldCount,
    int EntityDeltaFieldCount,
    int QueuedBitstreamFieldCount,
    bool NativeGameplayRouteIdentified);

public sealed record Tf2Ps3SourcePacketEntitiesPlacementFinding(
    string Name,
    string Evidence,
    string Meaning);

public sealed record Tf2Ps3SourcePacketEntitiesPlacementBoundary(
    string Name,
    string Evidence,
    string Meaning);
