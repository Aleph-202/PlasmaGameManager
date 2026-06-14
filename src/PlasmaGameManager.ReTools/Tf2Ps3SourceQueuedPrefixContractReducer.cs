using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceQueuedPrefixContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceQueuedPrefixContractReport> ReduceAsync(
        string queuedPeerTargetMapPath,
        string queuedOpaqueReportPath,
        string templatePatchLayoutPath,
        string stateLinkGrammarPath,
        string embeddedObjectGrammarPath,
        string tunnelGhidraMapPath,
        string outputPath)
    {
        using var queuedPeerTarget = JsonDocument.Parse(await File.ReadAllTextAsync(queuedPeerTargetMapPath));
        using var queuedOpaque = JsonDocument.Parse(await File.ReadAllTextAsync(queuedOpaqueReportPath));
        using var templatePatchLayout = JsonDocument.Parse(await File.ReadAllTextAsync(templatePatchLayoutPath));
        using var stateLinkGrammar = JsonDocument.Parse(await File.ReadAllTextAsync(stateLinkGrammarPath));
        using var embeddedObjectGrammar = JsonDocument.Parse(await File.ReadAllTextAsync(embeddedObjectGrammarPath));
        using var tunnelGhidra = JsonDocument.Parse(await File.ReadAllTextAsync(tunnelGhidraMapPath));

        var queuedSummary = queuedOpaque.RootElement.GetProperty("Summary");
        var targetSummary = queuedPeerTarget.RootElement.GetProperty("Summary");
        var tunnelSummary = tunnelGhidra.RootElement.GetProperty("Summary");
        var stateSummary = stateLinkGrammar.RootElement.GetProperty("Summary");
        var embeddedSummary = embeddedObjectGrammar.RootElement.GetProperty("Summary");
        var prefixDebts = ReadStaticPrefixDebts(stateLinkGrammar.RootElement).ToArray();
        var generatedPrefixDebts = ReadGeneratedPrefixDebts(stateLinkGrammar.RootElement).ToArray();
        var nativeBoundaries = ReadNativeBoundaries(queuedPeerTarget.RootElement).ToArray();
        var objectTargets = ReadObjectStreamTargets(queuedPeerTarget.RootElement).ToArray();
        var topFamilies = ReadCounts(queuedSummary, "TopFamilies").Take(24).ToArray();

        var report = new Tf2Ps3SourceQueuedPrefixContractReport(
            "tf2ps3-source-queued-prefix-contract",
            "Native contract for the TF2 PS3 queued Source peer-channel prefix layer. This ties the remaining high-entropy prefix bytes to TF.elf send/queue/object-stream boundaries and keeps decoded COc/DSC/PNG records out of captured-template debt.",
            new Tf2Ps3SourceQueuedPrefixContractInputs(
                queuedPeerTargetMapPath,
                queuedOpaqueReportPath,
                templatePatchLayoutPath,
                stateLinkGrammarPath,
                embeddedObjectGrammarPath,
                tunnelGhidraMapPath),
            new Tf2Ps3SourceQueuedPrefixContractSummary(
                ReadInt(queuedSummary, "FileCount"),
                ReadInt(queuedSummary, "ActiveSourceFlowFileCount"),
                ReadInt(queuedSummary, "QueuedChunkPacketCount"),
                ReadInt(queuedSummary, "ServerQueuedChunkPacketCount"),
                ReadInt(queuedSummary, "ClientQueuedChunkPacketCount"),
                ReadInt(queuedSummary, "QueuedChunkWithEmbeddedRecordsCount"),
                ReadInt(queuedSummary, "QueuedChunkWithoutEmbeddedRecordsCount"),
                ReadInt(queuedSummary, "OpaquePrefixBytes"),
                ReadInt(queuedSummary, "LzssWrappedPrefixCount"),
                ReadInt(queuedSummary, "StrictSnapshotPrefixCount"),
                prefixDebts.Length,
                prefixDebts.Sum(static debt => debt.PrefixByteLength),
                generatedPrefixDebts.Length,
                generatedPrefixDebts.Sum(static debt => debt.PrefixByteLength),
                generatedPrefixDebts.Sum(static debt => debt.RecordCount),
                ReadInt(embeddedSummary, "EmbeddedRecordCount"),
                ReadInt(embeddedSummary, "PngMarkerCount"),
                ReadInt(embeddedSummary, "CocMarkerCount"),
                ReadInt(embeddedSummary, "DscMarkerCount"),
                ReadInt(tunnelSummary, "EaTunnelReferenceCount"),
                ReadInt(tunnelSummary, "NeighborhoodReferenceCount"),
                ReadInt(targetSummary, "PeerChannelLocatedFunctionCount"),
                nativeBoundaries.Length,
                objectTargets.Length),
            nativeBoundaries,
            CloneObjectProperty(queuedPeerTarget.RootElement, "NativeQueueContract"),
            CloneObjectProperty(queuedPeerTarget.RootElement, "NativeSendWrapperContract"),
            CloneObjectProperty(queuedPeerTarget.RootElement, "NativeQueueDrainContract"),
            objectTargets,
            topFamilies.Select(ClassifyFamily).ToArray(),
            prefixDebts,
            generatedPrefixDebts,
            BuildDecodedRecordContracts(stateLinkGrammar.RootElement, embeddedObjectGrammar.RootElement).ToArray(),
            BuildNativeObligations(queuedSummary, targetSummary, tunnelSummary, stateSummary, embeddedSummary, prefixDebts, generatedPrefixDebts),
            BuildConclusions(queuedSummary, tunnelSummary, prefixDebts, generatedPrefixDebts));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPrefixNativeBoundary> ReadNativeBoundaries(JsonElement root)
    {
        foreach (var boundary in root.GetProperty("NativeBoundaries").EnumerateArray())
        {
            yield return new Tf2Ps3SourceQueuedPrefixNativeBoundary(
                ReadString(boundary, "Address"),
                ReadString(boundary, "Role"),
                ReadString(boundary, "Conclusion"),
                StringArray(boundary, "Calls"),
                StringArray(boundary, "EvidenceTokens"));
        }
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPrefixObjectStreamTarget> ReadObjectStreamTargets(JsonElement root)
    {
        foreach (var target in root.GetProperty("ObjectStreamTargets").EnumerateArray())
        {
            yield return new Tf2Ps3SourceQueuedPrefixObjectStreamTarget(
                ReadString(target, "Address"),
                ReadString(target, "Role"),
                ReadString(target, "Meaning"));
        }
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPrefixStaticDebt> ReadStaticPrefixDebts(JsonElement root)
    {
        foreach (var debt in root.GetProperty("PrefixDebt").EnumerateArray())
        {
            yield return new Tf2Ps3SourceQueuedPrefixStaticDebt(
                ReadString(debt, "TemplateName"),
                ReadString(debt, "ReplacementTarget"),
                ReadInt(debt, "PrefixByteLength"),
                ReadInt(debt, "EmbeddedRecordCount"),
                ReadString(debt, "PrefixKind"));
        }
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPrefixGeneratedDebt> ReadGeneratedPrefixDebts(JsonElement root)
    {
        if (!root.TryGetProperty("GeneratedPrefixDebt", out var debts) || debts.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var debt in debts.EnumerateArray())
        {
            yield return new Tf2Ps3SourceQueuedPrefixGeneratedDebt(
                ReadString(debt, "Family"),
                ReadString(debt, "Method"),
                ReadString(debt, "ReplacementTarget"),
                ReadInt(debt, "BodyLength"),
                ReadInt(debt, "PrefixByteLength"),
                ReadInt(debt, "RecordCount"),
                ReadInt(debt, "TailByteLength"),
                ReadString(debt, "PrefixKind"),
                StringArray(debt, "ObjectIdExpressions"),
                ReadString(debt, "LinkedObjectIdExpression"));
        }
    }

    private static Tf2Ps3SourceQueuedPrefixFamily ClassifyFamily(Tf2Ps3SourceQueuedPrefixCount count)
    {
        var value = count.Value;
        var meaning = value switch
        {
            var item when item.StartsWith("server-queued-opaque-prefix-", StringComparison.Ordinal) =>
                "server queued native prefix followed by decoded embedded COc/DSC/PNG records",
            var item when item.StartsWith("server-queued-opaque-only-", StringComparison.Ordinal) =>
                "server queued native prefix without trailing decoded embedded records",
            var item when item.StartsWith("client-queued-", StringComparison.Ordinal) =>
                "unexpected client queued native prefix; requires separate client command handling",
            _ => "unclassified queued native prefix family"
        };

        var replacementTarget = value.Contains("-records-", StringComparison.Ordinal)
            ? "queued-peer-submessage-boundaries"
            : "native-object-stream-bootstrap-or-snapshot-prefix";
        return new Tf2Ps3SourceQueuedPrefixFamily(value, count.Count, meaning, replacementTarget);
    }

    private static IEnumerable<Tf2Ps3SourceQueuedPrefixDecodedRecordContract> BuildDecodedRecordContracts(
        JsonElement stateLinkRoot,
        JsonElement embeddedObjectRoot)
    {
        var stateWire = stateLinkRoot.GetProperty("WireFormat");
        yield return new Tf2Ps3SourceQueuedPrefixDecodedRecordContract(
            ReadString(stateWire, "Marker"),
            "PlayerStateLink",
            ReadInt(stateWire, "Length"),
            ReadInt(stateWire, "Version"),
            "Ps3SourcePlayerStateLinkRecord",
            "object id -> linked/root/player object id");

        foreach (var format in embeddedObjectRoot.GetProperty("WireFormats").EnumerateArray())
        {
            yield return new Tf2Ps3SourceQueuedPrefixDecodedRecordContract(
                ReadString(format, "Marker"),
                ReadString(format, "Role"),
                ReadInt(format, "Length"),
                ReadInt(format, "Version"),
                "Ps3SourceEmbeddedObjectWireRecord",
                ReadString(format, "ClassIdRule"));
        }
    }

    private static string[] BuildNativeObligations(
        JsonElement queuedSummary,
        JsonElement targetSummary,
        JsonElement tunnelSummary,
        JsonElement stateSummary,
        JsonElement embeddedSummary,
        Tf2Ps3SourceQueuedPrefixStaticDebt[] prefixDebts,
        Tf2Ps3SourceQueuedPrefixGeneratedDebt[] generatedPrefixDebts)
    {
        var obligations = new List<string>
        {
            "Generate every queued prefix through TF.elf-equivalent Source bitstream/object-stream builders before appending COc/DSC/PNG records.",
            "Route generated bodies through the native send/queue boundary recovered as 008bc978 -> 008b9f70/008bb058/008bc490, not through PC srcds datagrams.",
            "Keep COc/DSC/PNG generation record-native via Ps3SourceEmbeddedObjectWireRecord and Ps3SourcePlayerStateLinkRecord; those records are not the unresolved prefix bytes.",
            "Do not use EA Tunnel as the client-visible Source packet owner unless new executable xrefs appear; current server.dll/Ghidra evidence has zero EA Tunnel and descriptor-neighborhood references.",
            "Reject replay/captured-template success criteria: opaque prefix byte counts must drop only when replaced by native field writers, not hidden in new hex literals."
        };

        if (ReadInt(queuedSummary, "ClientQueuedChunkPacketCount") == 0)
        {
            obligations.Add("Current PCAP corpus models server-to-client queued prefixes only; client-to-server command handling remains in the clc/usercmd path rather than this queued-prefix report.");
        }

        if (ReadInt(targetSummary, "HasObjectStreamEnvelopeSender") == 0)
        {
            obligations.Add("Object stream envelope sender is missing from the target map; rerun TF.elf object-stream reducers before claiming native bootstrap coverage.");
        }

        if (ReadInt(tunnelSummary, "EaTunnelReferenceCount") != 0 || ReadInt(tunnelSummary, "NeighborhoodReferenceCount") != 0)
        {
            obligations.Add("EA Tunnel evidence changed and must be manually reviewed before keeping it outside the packet-owner model.");
        }

        if (prefixDebts.Length > 0)
        {
            obligations.Add($"Static responder prefix debt still names {prefixDebts.Length} template family/families totaling {prefixDebts.Sum(static debt => debt.PrefixByteLength)} bytes; remove those with native builders.");
        }

        if (generatedPrefixDebts.Length > 0)
        {
            obligations.Add($"Generated queued-prefix debt still names {generatedPrefixDebts.Length} generated state-link body/bodies totaling {generatedPrefixDebts.Sum(static debt => debt.PrefixByteLength)} prefix bytes; replace the generated prefix with the native queued peer-channel writer.");
        }

        obligations.Add($"Decoded record coverage currently includes {ReadInt(embeddedSummary, "EmbeddedRecordCount")} embedded records and {ReadInt(stateSummary, "PcapPlayerStateLinkRecordCount")} PNG state-link records across the corpus.");
        return obligations.ToArray();
    }

    private static string[] BuildConclusions(
        JsonElement queuedSummary,
        JsonElement tunnelSummary,
        Tf2Ps3SourceQueuedPrefixStaticDebt[] prefixDebts,
        Tf2Ps3SourceQueuedPrefixGeneratedDebt[] generatedPrefixDebts)
    {
        var conclusions = new List<string>
        {
            "The remaining queued-prefix layer is native Source peer-channel/object-stream payload, not Plasma/GameManager text and not PC Source query traffic.",
            "Decoded COc/DSC/PNG record tails are now named separately from the opaque prefix debt.",
            "No queued prefix in the current corpus validates as the project LZSS wrapper or as a strict standalone entity snapshot frame.",
            "The replacement path is semantic: rebuild server-info/string tables/class info/snapshot/entity-delta bytes from TF.elf and server.dll semantics, then emit them through the PS3 send wrapper."
        };

        if (ReadInt(tunnelSummary, "EaTunnelReferenceCount") == 0 && ReadInt(tunnelSummary, "NeighborhoodReferenceCount") == 0)
        {
            conclusions.Add("EA Tunnel remains closed as a server.dll metadata/config lead for now; it does not explain the map-load prefix bytes.");
        }

        if (prefixDebts.Length > 0)
        {
            conclusions.Add("Static responder templates still have queued/object-stream prefixes ahead of native PNG records, so the live server is not fully native yet.");
        }

        if (generatedPrefixDebts.Length > 0)
        {
            conclusions.Add("Generated queued PlayerStateLink prefixes remain open native-debt even when no static template bytes remain for those bodies.");
        }
        else
        {
            conclusions.Add("No generated queued PlayerStateLink prefix debt remains in the current responder; remaining corpus prefix bytes describe the native peer-channel format to preserve.");
        }

        if (ReadInt(queuedSummary, "ClientQueuedChunkPacketCount") == 0)
        {
            conclusions.Add("All queued chunks in this corpus are server-to-client, which matches the server bootstrap/snapshot replacement target.");
        }

        return conclusions.ToArray();
    }

    private static JsonElement CloneObjectProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return value.Clone();
        }

        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }

    private static Tf2Ps3SourceQueuedPrefixCount[] ReadCounts(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var counts) || counts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return counts.EnumerateArray()
            .Select(static count => new Tf2Ps3SourceQueuedPrefixCount(
                ReadString(count, "Value"),
                ReadInt(count, "Count")))
            .Where(static count => count.Value.Length > 0)
            .ToArray();
    }

    private static string[] StringArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static item => item.GetString() ?? "").Where(static item => item.Length > 0).ToArray()
            : [];
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.True => 1,
            _ => 0
        };
    }
}

public sealed record Tf2Ps3SourceQueuedPrefixContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceQueuedPrefixContractInputs Inputs,
    Tf2Ps3SourceQueuedPrefixContractSummary Summary,
    Tf2Ps3SourceQueuedPrefixNativeBoundary[] NativeBoundaries,
    JsonElement NativeQueueContract,
    JsonElement NativeSendWrapperContract,
    JsonElement NativeQueueDrainContract,
    Tf2Ps3SourceQueuedPrefixObjectStreamTarget[] ObjectStreamTargets,
    Tf2Ps3SourceQueuedPrefixFamily[] PrefixFamilies,
    Tf2Ps3SourceQueuedPrefixStaticDebt[] StaticTemplatePrefixDebt,
    Tf2Ps3SourceQueuedPrefixGeneratedDebt[] GeneratedQueuedPrefixDebt,
    Tf2Ps3SourceQueuedPrefixDecodedRecordContract[] DecodedRecordContracts,
    string[] NativeObligations,
    string[] Conclusions);

public sealed record Tf2Ps3SourceQueuedPrefixContractInputs(
    string QueuedPeerTargetMap,
    string QueuedOpaqueReport,
    string TemplatePatchLayout,
    string StateLinkGrammar,
    string EmbeddedObjectGrammar,
    string TunnelGhidraMap);

public sealed record Tf2Ps3SourceQueuedPrefixContractSummary(
    int PcapFileCount,
    int ActiveSourceFlowFileCount,
    int QueuedChunkPacketCount,
    int ServerQueuedChunkPacketCount,
    int ClientQueuedChunkPacketCount,
    int QueuedChunkWithEmbeddedRecordsCount,
    int QueuedChunkWithoutEmbeddedRecordsCount,
    int OpaquePrefixBytes,
    int LzssWrappedPrefixCount,
    int StrictSnapshotPrefixCount,
    int StaticTemplatePrefixDebtCount,
    int StaticTemplatePrefixDebtBytes,
    int GeneratedQueuedPrefixDebtCount,
    int GeneratedQueuedPrefixDebtBytes,
    int GeneratedQueuedPrefixRecordCount,
    int DecodedEmbeddedRecordCount,
    int DecodedPngMarkerCount,
    int DecodedCocMarkerCount,
    int DecodedDscMarkerCount,
    int EaTunnelReferenceCount,
    int EaTunnelNeighborhoodReferenceCount,
    int PeerChannelLocatedFunctionCount,
    int NativeBoundaryCount,
    int ObjectStreamTargetCount);

public sealed record Tf2Ps3SourceQueuedPrefixNativeBoundary(
    string Address,
    string Role,
    string Conclusion,
    string[] Calls,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceQueuedPrefixObjectStreamTarget(
    string Address,
    string Role,
    string Meaning);

public sealed record Tf2Ps3SourceQueuedPrefixFamily(
    string Family,
    int Count,
    string Meaning,
    string ReplacementTarget);

public sealed record Tf2Ps3SourceQueuedPrefixStaticDebt(
    string TemplateName,
    string ReplacementTarget,
    int PrefixByteLength,
    int EmbeddedRecordCount,
    string PrefixKind);

public sealed record Tf2Ps3SourceQueuedPrefixGeneratedDebt(
    string Family,
    string Method,
    string ReplacementTarget,
    int BodyLength,
    int PrefixByteLength,
    int RecordCount,
    int TailByteLength,
    string PrefixKind,
    string[] ObjectIdExpressions,
    string LinkedObjectIdExpression);

public sealed record Tf2Ps3SourceQueuedPrefixDecodedRecordContract(
    string Marker,
    string Role,
    int Length,
    int Version,
    string Builder,
    string FieldRule);

public sealed record Tf2Ps3SourceQueuedPrefixCount(string Value, int Count);
