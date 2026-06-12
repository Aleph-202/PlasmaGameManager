using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceNetchanRegistrationSetupReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceNetchanRegistrationSetupReport> ReduceAsync(
        string netchanCrossmapPath,
        string helperSlicePath,
        string registrationCallsitePath,
        string objectLifecyclePath,
        string objectVtableLifecyclePath,
        string ownerVtablePath,
        string handlerRegistrationPath,
        string handlerRegistrationProofPath,
        string binaryReferencePath,
        string nativeMessageContractPath,
        string outputPath)
    {
        using var crossmapDocument = JsonDocument.Parse(await File.ReadAllTextAsync(netchanCrossmapPath));
        using var helperSliceDocument = JsonDocument.Parse(await File.ReadAllTextAsync(helperSlicePath));
        using var registrationCallsiteDocument = JsonDocument.Parse(await File.ReadAllTextAsync(registrationCallsitePath));
        using var objectLifecycleDocument = JsonDocument.Parse(await File.ReadAllTextAsync(objectLifecyclePath));
        using var objectVtableLifecycleDocument = JsonDocument.Parse(await File.ReadAllTextAsync(objectVtableLifecyclePath));
        using var ownerVtableDocument = JsonDocument.Parse(await File.ReadAllTextAsync(ownerVtablePath));
        using var handlerRegistrationDocument = JsonDocument.Parse(await File.ReadAllTextAsync(handlerRegistrationPath));
        using var handlerRegistrationProofDocument = JsonDocument.Parse(await File.ReadAllTextAsync(handlerRegistrationProofPath));
        using var binaryReferenceDocument = JsonDocument.Parse(await File.ReadAllTextAsync(binaryReferencePath));
        using var nativeMessageDocument = JsonDocument.Parse(await File.ReadAllTextAsync(nativeMessageContractPath));

        var crossmapSummary = crossmapDocument.RootElement.GetProperty("Summary");
        var helperSummary = helperSliceDocument.RootElement.GetProperty("Summary");
        var callsiteSummary = registrationCallsiteDocument.RootElement.GetProperty("Summary");
        var lifecycleSummary = objectLifecycleDocument.RootElement.GetProperty("Summary");
        var vtableSummary = objectVtableLifecycleDocument.RootElement.GetProperty("Summary");
        var ownerSummary = ownerVtableDocument.RootElement.GetProperty("Summary");
        var handlerSummary = handlerRegistrationDocument.RootElement.GetProperty("Summary");
        var proofSummary = handlerRegistrationProofDocument.RootElement.GetProperty("Summary");
        var binarySummary = binaryReferenceDocument.RootElement.GetProperty("Summary");
        var nativeSummary = nativeMessageDocument.RootElement.GetProperty("Summary");

        var nativeMessages = nativeMessageDocument.RootElement.GetProperty("Messages")
            .EnumerateArray()
            .ToDictionary(static item => ReadString(item, "ClassName"), StringComparer.Ordinal);

        var requiredHandlers = crossmapDocument.RootElement.GetProperty("ExpectedConnectionStartMessages")
            .EnumerateArray()
            .Where(static item => ReadBool(item, "RequiredByBaseClientConnectionStart"))
            .Select(item => BuildRequiredHandler(item, nativeMessages))
            .ToArray();

        var absentRequired = requiredHandlers.Count(static handler => !handler.PresentInTfElf);
        var fieldReducedRequired = requiredHandlers.Count(static handler => handler.FieldReduced);
        var vtableOnlyRequired = requiredHandlers.Count(static handler => handler.ContractStatus == "vtable-resolved-only");
        var setupProofStatus = ReadInt(proofSummary, "ProvenRegistrationCandidateCount") > 0
            ? "handler-constructor-proven"
            : "helper-slice-proven-handler-constructor-unresolved";

        var report = new Tf2Ps3SourceNetchanRegistrationSetupReport(
            "tf2ps3-source-netchan-registration-setup-map",
            "Consolidates the native TF.elf CNetChan-style setup evidence. This report distinguishes proven channel mechanics from still-unresolved concrete handler construction so the native server does not rely on guessed packet handlers.",
            new Tf2Ps3SourceNetchanRegistrationSetupInputs(
                netchanCrossmapPath,
                helperSlicePath,
                registrationCallsitePath,
                objectLifecyclePath,
                objectVtableLifecyclePath,
                ownerVtablePath,
                handlerRegistrationPath,
                handlerRegistrationProofPath,
                binaryReferencePath,
                nativeMessageContractPath),
            new Tf2Ps3SourceNetchanRegistrationSetupSummary(
                ReadInt(crossmapSummary, "MappedFunctionCount"),
                ReadInt(crossmapSummary, "StrongMappedFunctionCount"),
                ReadInt(crossmapSummary, "TfElfNetMessageTypeBits"),
                ReadInt(crossmapSummary, "LocalSourceNetMessageTypeBits"),
                ReadString(helperSummary, "RegistrationFunction"),
                ReadString(helperSummary, "RegistrationSlotAddress"),
                ReadString(helperSummary, "RegistrationVisibleSliceOffset"),
                ReadString(lifecycleSummary, "SourceObjectSize"),
                ReadString(lifecycleSummary, "RegisteredHandlerVectorOffset"),
                ReadString(lifecycleSummary, "RegisteredHandlerCountOffset"),
                ReadInt(callsiteSummary, "DirectRegistrationHelperCount"),
                ReadInt(proofSummary, "ProvenRegistrationCandidateCount"),
                ReadInt(proofSummary, "SourceCandidatesWithTableBaseReferences"),
                ReadInt(binarySummary, "SourceNeighborhoodCandidatesWithRawReferences"),
                requiredHandlers.Length,
                absentRequired,
                fieldReducedRequired,
                vtableOnlyRequired,
                ReadInt(nativeSummary, "PresentInTfElfCount"),
                ReadInt(nativeSummary, "FieldReducedMessageCount"),
                setupProofStatus),
            BuildCoreMappings(crossmapDocument.RootElement.GetProperty("FunctionCrossmap")),
            BuildHelperSlice(helperSummary),
            BuildSourceObject(lifecycleSummary),
            new Tf2Ps3SourceNetchanRegistrationProof(
                ReadInt(callsiteSummary, "DirectRegistrationHelperCount"),
                ReadInt(callsiteSummary, "SourceRegionVirtualSlot44CallCount"),
                ReadInt(handlerSummary, "DirectRegistrationCallsiteCount"),
                ReadInt(handlerSummary, "RemainingUnknownCount"),
                ReadInt(proofSummary, "CandidateCount"),
                ReadInt(proofSummary, "SourceNeighborhoodCandidateCount"),
                ReadInt(proofSummary, "ProvenRegistrationCandidateCount"),
                ReadInt(proofSummary, "SourceCandidatesWithTableBaseReferences"),
                ReadInt(proofSummary, "SourceCandidatesMissingRegistrationProof"),
                ReadInt(binarySummary, "HelperSliceTableBaseReferenceCount"),
                ReadInt(binarySummary, "HelperRegistrationSlotAddressReferenceCount"),
                ReadInt(binarySummary, "HandlerRegistrationOpdReferenceCount"),
                ReadInt(vtableSummary, "HandlerRegistrationEvidenceCount"),
                ReadInt(ownerSummary, "HandlerInstallEvidenceCount")),
            requiredHandlers,
            [
                "TF.elf CNetChan mechanics are proven: registered-message lookup, registration helper, payload processing, and datagram send all cross-map strongly to Source CNetChan functions.",
                "TF.elf consumes five-bit netmessage ids in this path; do not use the local PC Source six-bit NETMSG_TYPE_BITS contract for PS3 wire generation.",
                "The source object allocation, registered-handler vector, handler count field, and helper-slice registration slot are proven, but the concrete handler constructor path remains unresolved.",
                "The required connection-start handler set is now tied to the native TF.elf message contract; field-reduced handlers can be implemented semantically, vtable-only handlers still need field reduction.",
                "CLC_FileMD5Check and CLC_CmdKeyValues are absent in this TF.elf contract, so a native TF2 PS3 server must not require them unless later binary evidence proves a separate registration path."
            ],
            [
                "Recover the concrete handler object constructors that call helper slice slot +0x44 / 00a5df70, then bind each constructor to a native message class and id.",
                "Reduce ReadFromBuffer fields for remaining required vtable-only client messages before accepting them in the native server.",
                "Wire the field-reduced client join messages into the native Source responder only after the outer object-stream/native wrapper delivers raw CLC message bits."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceNetchanCoreMapping[] BuildCoreMappings(JsonElement mappings)
    {
        return mappings.EnumerateArray()
            .Select(static mapping => new Tf2Ps3SourceNetchanCoreMapping(
                ReadString(mapping, "TfElfFunctionAddress"),
                ReadString(mapping, "SourceSemanticName"),
                ReadString(mapping, "Role"),
                ReadString(mapping, "Confidence"),
                ReadString(mapping, "Conclusion")))
            .ToArray();
    }

    private static Tf2Ps3SourceNetchanHelperSlice BuildHelperSlice(JsonElement summary)
    {
        return new Tf2Ps3SourceNetchanHelperSlice(
            ReadString(summary, "FullSliceBase"),
            ReadString(summary, "VisibleSliceBase"),
            ReadString(summary, "EndExclusive"),
            ReadInt(summary, "SlotCount"),
            ReadString(summary, "RegistrationSlotAddress"),
            ReadString(summary, "RegistrationFullSliceOffset"),
            ReadString(summary, "RegistrationVisibleSliceOffset"),
            ReadString(summary, "RegistrationOpdAddress"),
            ReadString(summary, "RegistrationFunction"),
            ReadInt(summary, "PayloadDispatcherCallerSlotCount"),
            ReadInt(summary, "NativeSendBuilderCallerSlotCount"),
            ReadInt(summary, "SocketRecvWrapperCallerSlotCount"),
            ReadInt(summary, "HandlerRegistrationSlotCount"));
    }

    private static Tf2Ps3SourceNetchanSourceObject BuildSourceObject(JsonElement summary)
    {
        return new Tf2Ps3SourceNetchanSourceObject(
            ReadString(summary, "SourceObjectSize"),
            ReadString(summary, "OwnerCallbackOffset"),
            ReadString(summary, "RegisteredHandlerVectorOffset"),
            ReadString(summary, "RegisteredHandlerCountOffset"),
            ReadString(summary, "AttachedSocketOffset"),
            ReadString(summary, "AttachedStateByteOffset"),
            ReadString(summary, "StagedPayloadBufferOffset"));
    }

    private static Tf2Ps3SourceNetchanRequiredHandler BuildRequiredHandler(
        JsonElement expected,
        IReadOnlyDictionary<string, JsonElement> nativeMessages)
    {
        var className = ReadString(expected, "ClassName");
        nativeMessages.TryGetValue(className, out var native);
        var present = native.ValueKind == JsonValueKind.Object && ReadBool(native, "PresentInTfElf");
        var status = native.ValueKind == JsonValueKind.Object ? ReadString(native, "ContractStatus") : "missing-from-native-message-contract";
        var fieldCount = native.ValueKind == JsonValueKind.Object ? ReadInt(native, "FieldCount") : 0;
        var fieldReduced = status is "critical-field-reduced" or "clc-move-field-reduced" or "bootstrap-control-field-reduced";

        return new Tf2Ps3SourceNetchanRequiredHandler(
            className,
            ReadString(expected, "Family"),
            ReadString(expected, "MacroName"),
            present,
            native.ValueKind == JsonValueKind.Object ? ReadInt(native, "MessageId") : -1,
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "NetworkName") : "",
            status,
            fieldReduced,
            fieldCount,
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "Route") : "",
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "ReadFromBufferEntry") : "",
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "WriteToBufferEntry") : "",
            native.ValueKind == JsonValueKind.Object ? ReadString(native, "NativeImplementationGuidance") : "No TF.elf native message contract entry exists for this required Source template message.");
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static bool ReadBool(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.True;
    }
}

public sealed record Tf2Ps3SourceNetchanRegistrationSetupReport(
    string Status,
    string Note,
    Tf2Ps3SourceNetchanRegistrationSetupInputs Inputs,
    Tf2Ps3SourceNetchanRegistrationSetupSummary Summary,
    Tf2Ps3SourceNetchanCoreMapping[] CoreMappings,
    Tf2Ps3SourceNetchanHelperSlice HelperSlice,
    Tf2Ps3SourceNetchanSourceObject SourceObject,
    Tf2Ps3SourceNetchanRegistrationProof RegistrationProof,
    Tf2Ps3SourceNetchanRequiredHandler[] RequiredHandlers,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Tf2Ps3SourceNetchanRegistrationSetupInputs(
    string NetchanCrossmap,
    string HelperSlice,
    string RegistrationCallsite,
    string ObjectLifecycle,
    string ObjectVtableLifecycle,
    string OwnerVtable,
    string HandlerRegistration,
    string HandlerRegistrationProof,
    string BinaryReference,
    string NativeMessageContract);

public sealed record Tf2Ps3SourceNetchanRegistrationSetupSummary(
    int CNetChanFunctionCount,
    int StrongCNetChanFunctionCount,
    int TfElfNetMessageTypeBits,
    int LocalSourceNetMessageTypeBits,
    string RegistrationFunction,
    string RegistrationSlotAddress,
    string RegistrationVisibleSliceOffset,
    string SourceObjectSize,
    string RegisteredHandlerVectorOffset,
    string RegisteredHandlerCountOffset,
    int DirectRegistrationHelperCount,
    int ProvenRegistrationCandidateCount,
    int SourceCandidatesWithTableBaseReferences,
    int SourceNeighborhoodCandidatesWithRawReferences,
    int RequiredConnectionStartMessageCount,
    int AbsentRequiredHandlerCount,
    int FieldReducedRequiredHandlerCount,
    int VtableOnlyRequiredHandlerCount,
    int NativeMessageContractPresentCount,
    int FieldReducedMessageCount,
    string SetupProofStatus);

public sealed record Tf2Ps3SourceNetchanCoreMapping(
    string TfElfFunctionAddress,
    string SourceSemanticName,
    string Role,
    string Confidence,
    string Conclusion);

public sealed record Tf2Ps3SourceNetchanHelperSlice(
    string FullSliceBase,
    string VisibleSliceBase,
    string EndExclusive,
    int SlotCount,
    string RegistrationSlotAddress,
    string RegistrationFullSliceOffset,
    string RegistrationVisibleSliceOffset,
    string RegistrationOpdAddress,
    string RegistrationFunction,
    int PayloadDispatcherCallerSlotCount,
    int NativeSendBuilderCallerSlotCount,
    int SocketRecvWrapperCallerSlotCount,
    int HandlerRegistrationSlotCount);

public sealed record Tf2Ps3SourceNetchanSourceObject(
    string SourceObjectSize,
    string OwnerCallbackOffset,
    string RegisteredHandlerVectorOffset,
    string RegisteredHandlerCountOffset,
    string AttachedSocketOffset,
    string AttachedStateByteOffset,
    string StagedPayloadBufferOffset);

public sealed record Tf2Ps3SourceNetchanRegistrationProof(
    int DirectRegistrationHelperCount,
    int SourceRegionVirtualSlot44CallCount,
    int DirectRegistrationCallsiteCount,
    int HandlerRegistrationRemainingUnknownCount,
    int HandlerCandidateCount,
    int SourceNeighborhoodCandidateCount,
    int ProvenRegistrationCandidateCount,
    int SourceCandidatesWithTableBaseReferences,
    int SourceCandidatesMissingRegistrationProof,
    int HelperSliceTableBaseReferenceCount,
    int HelperRegistrationSlotAddressReferenceCount,
    int HandlerRegistrationOpdReferenceCount,
    int ObjectVtableHandlerRegistrationEvidenceCount,
    int OwnerVtableHandlerInstallEvidenceCount);

public sealed record Tf2Ps3SourceNetchanRequiredHandler(
    string ClassName,
    string Family,
    string MacroName,
    bool PresentInTfElf,
    int MessageId,
    string NetworkName,
    string ContractStatus,
    bool FieldReduced,
    int FieldCount,
    string Route,
    string ReadFromBufferEntry,
    string WriteToBufferEntry,
    string NativeImplementationGuidance);
