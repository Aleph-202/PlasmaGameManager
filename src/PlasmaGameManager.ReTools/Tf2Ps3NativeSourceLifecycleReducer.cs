using System.Text;
using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3NativeSourceLifecycleReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Tf2Ps3LifecycleAnchor[] TfElfStringAnchors =
    [
        new("SV_CreateBaseline", "0x01a11fb0", "server baseline construction before signon payloads"),
        new("SVC_ServerInfo", "0x01a1a520", "server-info netmessage class"),
        new("SVC_SendTable", "0x01a1a510", "send-table netmessage class"),
        new("SVC_ClassInfo", "0x01a1a500", "class-info netmessage class"),
        new("SVC_CreateStringTable", "0x01a1a4d8", "create-string-table netmessage class"),
        new("SVC_UpdateStringTable", "0x01a1a4c0", "update-string-table netmessage class"),
        new("SVC_PacketEntities", "0x01a1a3e0", "standalone packet-entities netmessage class"),
        new("CBaseClient::SendSignonData", "0x01a23038", "engine signon-data entry"),
        new("CBaseClient::SendSnapshot", "0x01a23080", "engine snapshot send entry"),
        new("Finished [delta %s]", "0x01a230d0", "snapshot success log"),
        new("ERROR! Couldn't send snapshot.", "0x01a23110", "snapshot failure log"),
        new("CBaseClient::SendServerInfo", "0x01a23130", "server-info send entry"),
        new("CBaseClient::SendServerInfo(finished)", "0x01a231c8", "server-info send completion log"),
        new("CGameClient::ActivatePlayer -start", "0x01a24288", "game client activation start"),
        new("CGameClient::ActivatePlayer -end", "0x01a24380", "game client activation end")
    ];

    private static readonly Tf2Ps3LifecycleAnchor[] OfficialServerDllStringAnchors =
    [
        new("CBasePlayer::ProcessUsercmds", "", "official game DLL usercmd batch consumer"),
        new("CBasePlayer::PhysicsSimulate", "", "official game DLL per-tick physics simulation entry"),
        new("CTFGameRules: ClientDisconnected", "", "official TF game-rules disconnect lifecycle"),
        new("CMultiplayRules: ClientConnected", "", "official multiplayer game-rules connect lifecycle"),
        new("FeslHubSingle_GetGameManager", "", "official game DLL EA GameManager bridge"),
        new("Error Sending Stats update Transaction", "", "official game DLL stats upload boundary")
    ];

    public static async Task<Tf2Ps3NativeSourceLifecycleReport> ReduceAsync(
        string tfElfPath,
        string officialServerDllPath,
        string nativeMessageContractPath,
        string packetEntitiesPlacementPath,
        string loadingReplacementPlanPath,
        string outputPath)
    {
        using var nativeMessageContract = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));
        using var packetEntitiesPlacement = JsonDocument.Parse(await File.ReadAllTextAsync(packetEntitiesPlacementPath));
        using var loadingReplacementPlan = JsonDocument.Parse(await File.ReadAllTextAsync(loadingReplacementPlanPath));

        var tfElfAnchors = ProbeAnchors(tfElfPath, TfElfStringAnchors);
        var officialDllAnchors = ProbeAnchors(officialServerDllPath, OfficialServerDllStringAnchors);
        var nativeSummary = nativeMessageContract.RootElement.GetProperty("Summary");
        var placementSummary = packetEntitiesPlacement.RootElement.GetProperty("Summary");
        var loadingSummary = loadingReplacementPlan.RootElement.GetProperty("Summary");
        var productionFakeFrames = ReadInt(loadingSummary, "ProductionFakeFrameCount");
        var nativeRecordGeneratedPrefixFrames = ReadInt(loadingSummary, "NativeRecordWithGeneratedPrefixFrameCount");
        var generatedQueuedPrefixDebtBytes = ReadInt(loadingSummary, "GeneratedQueuedPrefixDebtBytes");
        var steadyPaddingRisk = ReadInt(loadingSummary, "SteadyNativeSnapshotPaddingRiskCount");
        var staticTemplates = ReadInt(loadingSummary, "StaticHexTemplateCount");
        var loadingAcceptanceBlocking = ReadInt(loadingSummary, "AcceptanceBlockingCount");
        var nativeMapLoadReady = productionFakeFrames == 0
            && nativeRecordGeneratedPrefixFrames == 0
            && generatedQueuedPrefixDebtBytes == 0
            && steadyPaddingRisk == 0
            && staticTemplates == 0
            && loadingAcceptanceBlocking == 0;

        var report = new Tf2Ps3NativeSourceLifecycleReport(
            "tf2ps3-native-source-lifecycle-contract",
            "Combines TF.elf engine-side Source signon/snapshot evidence with the official EA TF2 server.dll game-side obligations. This report is an implementation contract for the native Source replacement, not a packet replay recipe.",
            new Tf2Ps3NativeSourceLifecycleSummary(
                TfElfAnchorCount: tfElfAnchors.Length,
                TfElfAnchorsPresent: tfElfAnchors.Count(static anchor => anchor.Present),
                OfficialServerDllAnchorCount: officialDllAnchors.Length,
                OfficialServerDllAnchorsPresent: officialDllAnchors.Count(static anchor => anchor.Present),
                NativeMessageCount: ReadInt(nativeSummary, "MessageCount"),
                FieldReducedMessageCount: ReadInt(nativeSummary, "FieldReducedMessageCount"),
                CriticalMessageCount: ReadInt(nativeSummary, "CriticalMessageCount"),
                NativeGameplayRouteIdentified: ReadBool(placementSummary, "NativeGameplayRouteIdentified"),
                ProductionFakeLoadingFrameCount: productionFakeFrames,
                NativeRecordWithGeneratedPrefixFrameCount: nativeRecordGeneratedPrefixFrames,
                GeneratedQueuedPrefixDebtBytes: generatedQueuedPrefixDebtBytes,
                SteadySnapshotPaddingRiskCount: steadyPaddingRisk,
                StaticHexTemplateCount: staticTemplates,
                LoadingAcceptanceBlockingCount: loadingAcceptanceBlocking,
                NativeMapLoadReady: nativeMapLoadReady),
            tfElfAnchors,
            officialDllAnchors,
            [
                new(
                    "game-dll-connect",
                    "CMultiplayRules/CTFGameRules plus FeslHubSingle_GetGameManager in server.dll",
                    "Register the player in TF rules and EA GameManager state before sending Source signon bytes."),
                new(
                    "engine-signon-bootstrap",
                    "CBaseClient::SendSignonData, CBaseClient::SendServerInfo, SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, SVC_CreateStringTable, and SVC_UpdateStringTable in TF.elf",
                    "Map-load bootstrap must be field-built as object-stream signon payloads through 00a55e60, not as arbitrary high-entropy loading frames."),
                new(
                    "client-activate",
                    "CGameClient::ActivatePlayer -start/-end in TF.elf",
                    "The native replacement needs an explicit transition after signon/class/string-table state where the player becomes active in TF rules."),
                new(
                    "snapshot-loop",
                    "CBaseClient::SendSnapshot, Finished [delta %s], and ERROR! Couldn't send snapshot. in TF.elf; 00a61150 -> 00a5fb80 -> 008bc978 in the placement report",
                    "Steady gameplay output should use native snapshot frames and semantic entity-delta groups."),
                new(
                    "game-dll-input",
                    "CBasePlayer::ProcessUsercmds and CBasePlayer::PhysicsSimulate in server.dll",
                    "Decoded PS3 CLC_Move/usercmd batches feed player simulation, then snapshots publish the resulting world state.")
            ],
            [
                new(
                    "do-not-use-steady-snapshot-as-early-bootstrap",
                    "The responder test suite regresses when early loading filler is replaced with steady snapshot frames; signon/bootstrap and steady snapshots are distinct lifecycle phases.",
                    "Replace early high-entropy frames by implementing the object-stream signon route, not by reusing command snapshot generation."),
                new(
                    "packetentities-placement",
                    "The standalone SVC_PacketEntities writer exists, but the placement report identifies the live route as 00a61150 -> 00a5fb80 -> 008bc978.",
                    "Keep standalone packet-entities for codec compatibility; use snapshot frames for gameplay output."),
                new(
                    "current-map-load-blocker",
                    $"native-template-debt reports {productionFakeFrames} fake loading frames, {nativeRecordGeneratedPrefixFrames} generated native-record-prefix frames, {generatedQueuedPrefixDebtBytes} generated queued-prefix debt bytes, {steadyPaddingRisk} steady padding-risk frames, {staticTemplates} static templates, and {loadingAcceptanceBlocking} blocking loading/map-load items.",
                    nativeMapLoadReady
                        ? "The loading/map-load payload gate is clear; remaining live issues should be chased as field completeness, gameplay, or client-input obligations."
                        : "The native replacement is not map-load complete until every blocking loading/map-load item is replaced with field-built signon/object-stream/snapshot payloads.")
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3LifecycleAnchorStatus[] ProbeAnchors(string path, IReadOnlyList<Tf2Ps3LifecycleAnchor> anchors)
    {
        if (!File.Exists(path))
        {
            return anchors
                .Select(anchor => new Tf2Ps3LifecycleAnchorStatus(anchor.Name, anchor.ExpectedAddress, anchor.Meaning, Present: false))
                .ToArray();
        }

        var bytes = File.ReadAllBytes(path);
        return anchors
            .Select(anchor => new Tf2Ps3LifecycleAnchorStatus(
                anchor.Name,
                anchor.ExpectedAddress,
                anchor.Meaning,
                IndexOf(bytes, Encoding.ASCII.GetBytes(anchor.Name)) >= 0))
            .ToArray();
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }

        for (var index = 0; index <= haystack.Length - needle.Length; index++)
        {
            if (haystack.Slice(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }

        return -1;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : false;
    }
}

public sealed record Tf2Ps3NativeSourceLifecycleReport(
    string Status,
    string Note,
    Tf2Ps3NativeSourceLifecycleSummary Summary,
    Tf2Ps3LifecycleAnchorStatus[] TfElfStringAnchors,
    Tf2Ps3LifecycleAnchorStatus[] OfficialServerDllStringAnchors,
    Tf2Ps3NativeSourceLifecycleStage[] LifecycleStages,
    Tf2Ps3NativeSourceLifecycleFinding[] Findings);

public sealed record Tf2Ps3NativeSourceLifecycleSummary(
    int TfElfAnchorCount,
    int TfElfAnchorsPresent,
    int OfficialServerDllAnchorCount,
    int OfficialServerDllAnchorsPresent,
    int NativeMessageCount,
    int FieldReducedMessageCount,
    int CriticalMessageCount,
    bool NativeGameplayRouteIdentified,
    int ProductionFakeLoadingFrameCount,
    int NativeRecordWithGeneratedPrefixFrameCount,
    int GeneratedQueuedPrefixDebtBytes,
    int SteadySnapshotPaddingRiskCount,
    int StaticHexTemplateCount,
    int LoadingAcceptanceBlockingCount,
    bool NativeMapLoadReady);

public sealed record Tf2Ps3LifecycleAnchorStatus(
    string Name,
    string ExpectedAddress,
    string Meaning,
    bool Present);

public sealed record Tf2Ps3NativeSourceLifecycleStage(
    string Name,
    string Evidence,
    string ImplementationMeaning);

public sealed record Tf2Ps3NativeSourceLifecycleFinding(
    string Name,
    string Evidence,
    string Meaning);

public sealed record Tf2Ps3LifecycleAnchor(
    string Name,
    string ExpectedAddress,
    string Meaning);
