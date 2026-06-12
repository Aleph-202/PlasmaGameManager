using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMarkerlessDataflowTargetReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceMarkerlessDataflowTargetReport> ReduceAsync(
        string helperSliceContractPath,
        string ownerCallbackMapPath,
        string ownerVtableMapPath,
        string registrationCallsiteMapPath,
        string registrationBinaryReferenceMapPath,
        string requiredHandlerTocFunctionMapPath,
        string slot70CallsiteCensusPath,
        string slot70Param2BuilderPath,
        string slot70Param2FieldContractPath,
        string bitstreamHelperContractPath,
        string outputPath)
    {
        using var helperSlice = JsonDocument.Parse(await File.ReadAllTextAsync(helperSliceContractPath));
        using var ownerCallbacks = JsonDocument.Parse(await File.ReadAllTextAsync(ownerCallbackMapPath));
        using var ownerVtables = JsonDocument.Parse(await File.ReadAllTextAsync(ownerVtableMapPath));
        using var registrationCallsites = JsonDocument.Parse(await File.ReadAllTextAsync(registrationCallsiteMapPath));
        using var registrationBinaryRefs = JsonDocument.Parse(await File.ReadAllTextAsync(registrationBinaryReferenceMapPath));
        using var requiredHandlers = JsonDocument.Parse(await File.ReadAllTextAsync(requiredHandlerTocFunctionMapPath));
        using var slot70Census = JsonDocument.Parse(await File.ReadAllTextAsync(slot70CallsiteCensusPath));
        using var slot70Builder = JsonDocument.Parse(await File.ReadAllTextAsync(slot70Param2BuilderPath));
        using var fieldContract = JsonDocument.Parse(await File.ReadAllTextAsync(slot70Param2FieldContractPath));
        using var bitstreamContract = JsonDocument.Parse(await File.ReadAllTextAsync(bitstreamHelperContractPath));

        var helperSummary = helperSlice.RootElement.GetProperty("Summary");
        var ownerSummary = ownerCallbacks.RootElement.GetProperty("Summary");
        var ownerVtableSummary = ownerVtables.RootElement.GetProperty("Summary");
        var registrationCallsiteSummary = registrationCallsites.RootElement.GetProperty("Summary");
        var registrationBinarySummary = registrationBinaryRefs.RootElement.GetProperty("Summary");
        var requiredHandlerSummary = requiredHandlers.RootElement.GetProperty("Summary");
        var slot70Summary = slot70Census.RootElement.GetProperty("Summary");
        var slot70BuilderSummary = slot70Builder.RootElement.GetProperty("Summary");
        var fieldSummary = fieldContract.RootElement.GetProperty("Summary");
        var bitstreamSummary = bitstreamContract.RootElement.GetProperty("Summary");

        var exactVisibleSlot70IngressRuledOut =
            ReadInt(slot70Summary, "DirectMarkerlessIngressCandidateCount") == 0
            && ReadInt(slot70Summary, "NetworkOrSocketSlot70CallsiteCount") == 0
            && ReadInt(slot70Summary, "PayloadDispatcherSlot70CallsiteCount") == 0;
        var helperSliceRegistrationKnown =
            ReadString(helperSummary, "RegistrationFunction") == "00a5df70"
            && ReadString(helperSummary, "RegistrationSlotAddress") == "0x0180ca04"
            && ReadInt(helperSummary, "HandlerRegistrationSlotCount") == 1;
        var ownerCallbackInterfaceKnown =
            ReadString(ownerSummary, "OwnerCallbackOffset") == "0x1e08"
            && ReadInt(ownerSummary, "CallbackUsingFunctionCount") >= 5
            && ReadInt(ownerSummary, "CallbackSlotCount") >= 5;
        var slot70CalleeContractKnown =
            ReadString(slot70BuilderSummary, "Slot70FunctionAddress") == "00a5d9e0"
            && ReadString(slot70BuilderSummary, "Slot70TableAddress") == "0x0180ca30"
            && ReadInt(fieldSummary, "RequiredEvidenceMissingCount") == 0
            && ReadBool(fieldSummary, "CallerSideParam2BuilderRecovered") == false
            && ReadInt(bitstreamSummary, "RecoveredPrimitiveCount") == ReadInt(bitstreamSummary, "PrimitiveCount");
        var requiredHandlerConstructorKnown =
            ReadString(requiredHandlerSummary, "PrimaryConstructorCandidate") == "009fa398"
            && ReadInt(requiredHandlerSummary, "PrimaryConstructorRequiredHandlerCount") == 11
            && ReadString(requiredHandlerSummary, "RegisterVirtualSlotOffset") == "0x64";
        var registrationEvidenceHasBinaryLead =
            ReadInt(registrationBinarySummary, "HandlerRegistrationOpdReferenceCount") >= 1
            && ReadInt(registrationCallsiteSummary, "DirectRegistrationHelperCount") == 1;

        var concreteMarkerlessDataflowEdgeRecovered =
            ReadBool(slot70BuilderSummary, "Param2ConstructionSiteRecovered")
            || ReadInt(slot70Summary, "DirectMarkerlessIngressCandidateCount") > 0;
        var nativeSourceInputReady =
            concreteMarkerlessDataflowEdgeRecovered
            && ReadBool(fieldSummary, "NativeSourceInputReady")
            && ReadBool(bitstreamSummary, "NativeSourceInputReady");

        var targets = BuildTargets(
            helperSummary,
            ownerSummary,
            ownerVtableSummary,
            registrationCallsiteSummary,
            registrationBinarySummary,
            requiredHandlerSummary,
            slot70Summary,
            slot70BuilderSummary,
            fieldSummary,
            bitstreamSummary);

        var gates = new[]
        {
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "slot70-exact-visible-ingress-ruled-out",
                exactVisibleSlot70IngressRuledOut ? "proven-negative" : "needs-review",
                slot70CallsiteCensusPath,
                "The hard markerless path is not a simple visible C-export virtual call through slot +0x70."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "helper-slice-registration-known",
                helperSliceRegistrationKnown ? "proven" : "missing",
                helperSliceContractPath,
                "The helper-slice registration slot and 00a5df70 registration function are pinned."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "owner-callback-interface-known",
                ownerCallbackInterfaceKnown ? "proven" : "missing",
                ownerCallbackMapPath,
                "The Source object owner callback interface at object +0x1e08 is named enough to trace callbacks next."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "slot70-callee-and-bitstream-contract-known",
                slot70CalleeContractKnown ? "proven" : "missing",
                $"{slot70Param2FieldContractPath}; {bitstreamHelperContractPath}",
                "The callee-side 00a5d9e0 field layout and primitive bitstream helper contract are recovered."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "required-client-handler-constructor-known",
                requiredHandlerConstructorKnown ? "proven" : "missing",
                requiredHandlerTocFunctionMapPath,
                "CBaseClient::ConnectionStart handler construction is no longer the likely native input blocker."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "registration-binary-lead-present",
                registrationEvidenceHasBinaryLead ? "candidate" : "missing",
                $"{registrationCallsiteMapPath}; {registrationBinaryReferenceMapPath}",
                "Registration references still give useful Ghidra/dataflow leads, but not the markerless packet edge itself."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "concrete-markerless-dataflow-edge",
                concreteMarkerlessDataflowEdgeRecovered ? "proven" : "missing",
                $"{slot70CallsiteCensusPath}; {slot70Param2BuilderPath}",
                "This is the remaining proof needed before claiming native client-upload handling is complete."),
            new Tf2Ps3SourceMarkerlessDataflowGate(
                "native-source-input-ready",
                nativeSourceInputReady ? "proven" : "missing",
                "server implementation gate",
                "The native replacement must not claim complete Source input support until the concrete markerless edge is recovered.")
        };

        var report = new Tf2Ps3SourceMarkerlessDataflowTargetReport(
            "tf2ps3-source-markerless-dataflow-targets",
            "Consolidates TF.elf markerless Source-input evidence and ranks the next concrete dataflow targets after ruling out exact visible slot +0x70 ingress.",
            new Tf2Ps3SourceMarkerlessDataflowInputs(
                helperSliceContractPath,
                ownerCallbackMapPath,
                ownerVtableMapPath,
                registrationCallsiteMapPath,
                registrationBinaryReferenceMapPath,
                requiredHandlerTocFunctionMapPath,
                slot70CallsiteCensusPath,
                slot70Param2BuilderPath,
                slot70Param2FieldContractPath,
                bitstreamHelperContractPath),
            new Tf2Ps3SourceMarkerlessDataflowSummary(
                ReadInt(slot70Summary, "ExactSlot70CallsiteFunctionCount"),
                ReadInt(slot70Summary, "DirectMarkerlessIngressCandidateCount"),
                ReadInt(ownerSummary, "CallbackSlotCount"),
                ReadInt(ownerSummary, "CallbackUsingFunctionCount"),
                ReadInt(helperSummary, "SlotCount"),
                ReadString(helperSummary, "RegistrationFunction"),
                ReadString(helperSummary, "RegistrationSlotAddress"),
                ReadString(slot70BuilderSummary, "Slot70FunctionAddress"),
                ReadString(slot70BuilderSummary, "Slot70OpdAddress"),
                ReadString(slot70BuilderSummary, "Slot70TableAddress"),
                ReadString(requiredHandlerSummary, "PrimaryConstructorCandidate"),
                ReadInt(requiredHandlerSummary, "PrimaryConstructorRequiredHandlerCount"),
                ReadInt(ownerVtableSummary, "TablesWithHandlerInstallEvidence"),
                ReadInt(registrationCallsiteSummary, "CallsiteCount"),
                ReadInt(registrationBinarySummary, "HandlerRegistrationOpdReferenceCount"),
                ReadInt(fieldSummary, "RecoveredFieldCount"),
                ReadInt(bitstreamSummary, "RecoveredPrimitiveCount"),
                exactVisibleSlot70IngressRuledOut,
                helperSliceRegistrationKnown,
                ownerCallbackInterfaceKnown,
                slot70CalleeContractKnown,
                requiredHandlerConstructorKnown,
                concreteMarkerlessDataflowEdgeRecovered,
                nativeSourceInputReady,
                targets.Length,
                gates.Count(static gate => gate.Status is "missing" or "candidate" or "needs-review")),
            targets,
            gates,
            [
                "The exact slot +0x70 C-export census is now a proven-negative search result for direct markerless ingress, not an open broad search.",
                "The 00a5d9e0 callee-side object, field layout, and primitive bitstream helpers are known; the missing proof is the caller-side markerless-packet-to-param_2 construction edge.",
                "The strongest next target is Ghidra/dataflow through owner callback/interface use around object +0x1e08 and helper-slice slot 0x0180ca30, with registration/constructor paths used as control evidence.",
                "Do not replace native client upload handling with PC srcds forwarding or packet replay until the concrete markerless dataflow edge is recovered."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceMarkerlessDataflowTarget[] BuildTargets(
        JsonElement helperSummary,
        JsonElement ownerSummary,
        JsonElement ownerVtableSummary,
        JsonElement registrationCallsiteSummary,
        JsonElement registrationBinarySummary,
        JsonElement requiredHandlerSummary,
        JsonElement slot70Summary,
        JsonElement slot70BuilderSummary,
        JsonElement fieldSummary,
        JsonElement bitstreamSummary)
    {
        return
        [
            new(
                "owner-callback-dispatch-path",
                1,
                "open-primary-target",
                [
                    $"owner-callback-offset={ReadString(ownerSummary, "OwnerCallbackOffset")}",
                    $"callback-slots={ReadInt(ownerSummary, "CallbackSlotCount")}",
                    $"callback-using-functions={ReadInt(ownerSummary, "CallbackUsingFunctionCount")}",
                    $"owner-vtables-with-handler-install-evidence={ReadInt(ownerVtableSummary, "TablesWithHandlerInstallEvidence")}"
                ],
                "The slot +0x70 callee calls back through the Source object owner/interface. The remaining markerless transform may be hidden behind this owner callback path rather than a visible direct slot call.",
                "Use Ghidra dataflow from 00a5d9e0 owner callback loads at object +0x1e08/+0x34 and inspect callback implementers for packet-body or bitreader construction."),
            new(
                "helper-slice-virtual-dispatch-path",
                2,
                "open-primary-target",
                [
                    $"helper-slice-base={ReadString(helperSummary, "FullSliceBase")}",
                    $"slot70-table-address={ReadString(slot70BuilderSummary, "Slot70TableAddress")}",
                    $"slot70-opd={ReadString(slot70BuilderSummary, "Slot70OpdAddress")}",
                    $"slot70-function={ReadString(slot70BuilderSummary, "Slot70FunctionAddress")}",
                    $"exact-slot70-callsite-functions={ReadInt(slot70Summary, "ExactSlot70CallsiteFunctionCount")}",
                    $"direct-markerless-candidates={ReadInt(slot70Summary, "DirectMarkerlessIngressCandidateCount")}"
                ],
                "The helper-slice slot is registered as data and normal C-export callsites do not expose the live markerless edge. The next edge may be an indirect dispatch or inline call pattern Ghidra did not render as the exact text pattern.",
                "Trace xrefs and references to 0x0180ca30/0x0190e530 and inspect indirect-call setup in disassembly, not only decompiled C output."),
            new(
                "registration-helper-binary-reference-path",
                3,
                "open-secondary-target",
                [
                    $"registration-function={ReadString(helperSummary, "RegistrationFunction")}",
                    $"registration-slot={ReadString(helperSummary, "RegistrationSlotAddress")}",
                    $"direct-registration-helper-count={ReadInt(registrationCallsiteSummary, "DirectRegistrationHelperCount")}",
                    $"handler-registration-opd-reference-count={ReadInt(registrationBinarySummary, "HandlerRegistrationOpdReferenceCount")}"
                ],
                "The registration helper is pinned and still gives a binary-reference lead into handler/vector setup, but current reports do not connect it to markerless packet bytes.",
                "Follow the raw handler-registration OPD reference in Ghidra and compare the constructed objects against 00a5d9e0 param_2 field use."),
            new(
                "required-handler-constructor-control-path",
                4,
                "resolved-control-evidence",
                [
                    $"constructor={ReadString(requiredHandlerSummary, "PrimaryConstructorCandidate")}",
                    $"required-handler-count={ReadInt(requiredHandlerSummary, "PrimaryConstructorRequiredHandlerCount")}",
                    $"register-slot={ReadString(requiredHandlerSummary, "RegisterVirtualSlotOffset")}"
                ],
                "The required client message handler constructor is recovered. This is important control evidence, but it does not explain hard markerless packet wrapping.",
                "Use 009fa398 as a sanity check for registered client-message semantics, not as the current primary markerless wrapper target."),
            new(
                "bitstream-callee-contract-control-path",
                5,
                "resolved-control-evidence",
                [
                    $"field-count={ReadInt(fieldSummary, "RecoveredFieldCount")}/{ReadInt(fieldSummary, "FieldCount")}",
                    $"primitive-count={ReadInt(bitstreamSummary, "RecoveredPrimitiveCount")}/{ReadInt(bitstreamSummary, "PrimitiveCount")}",
                    $"caller-builder-recovered={ReadBool(fieldSummary, "CallerSideParam2BuilderRecovered")}"
                ],
                "The 00a5d9e0 callee contract is sufficiently known for implementation once the caller-side wrapper is found.",
                "Keep the callee contract stable and focus new RE on the edge that populates param_2 from markerless UDP bodies.")
        ];
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return "";
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => property.GetRawText()
        };
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.True;
}

public sealed record Tf2Ps3SourceMarkerlessDataflowTargetReport(
    string Status,
    string Note,
    Tf2Ps3SourceMarkerlessDataflowInputs Inputs,
    Tf2Ps3SourceMarkerlessDataflowSummary Summary,
    Tf2Ps3SourceMarkerlessDataflowTarget[] RankedTargets,
    Tf2Ps3SourceMarkerlessDataflowGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceMarkerlessDataflowInputs(
    string HelperSliceContractReport,
    string OwnerCallbackMapReport,
    string OwnerVtableMapReport,
    string RegistrationCallsiteMapReport,
    string RegistrationBinaryReferenceMapReport,
    string RequiredHandlerTocFunctionMapReport,
    string Slot70CallsiteCensusReport,
    string Slot70Param2BuilderReport,
    string Slot70Param2FieldContractReport,
    string BitstreamHelperContractReport);

public sealed record Tf2Ps3SourceMarkerlessDataflowSummary(
    int ExactSlot70CallsiteFunctionCount,
    int DirectMarkerlessIngressCandidateCount,
    int OwnerCallbackSlotCount,
    int CallbackUsingFunctionCount,
    int HelperSliceSlotCount,
    string HandlerRegistrationFunction,
    string HandlerRegistrationSlotAddress,
    string Slot70FunctionAddress,
    string Slot70OpdAddress,
    string Slot70TableAddress,
    string PrimaryRequiredHandlerConstructor,
    int PrimaryRequiredHandlerCount,
    int OwnerVtablesWithHandlerInstallEvidence,
    int RegistrationCallsiteCount,
    int HandlerRegistrationOpdReferenceCount,
    int RecoveredSlot70FieldCount,
    int RecoveredBitstreamPrimitiveCount,
    bool ExactVisibleSlot70IngressRuledOut,
    bool HelperSliceRegistrationKnown,
    bool OwnerCallbackInterfaceKnown,
    bool Slot70CalleeContractKnown,
    bool RequiredHandlerConstructorKnown,
    bool ConcreteMarkerlessDataflowEdgeRecovered,
    bool NativeSourceInputReady,
    int RankedTargetCount,
    int OpenGateCount);

public sealed record Tf2Ps3SourceMarkerlessDataflowTarget(
    string Id,
    int Priority,
    string Status,
    string[] Evidence,
    string Reason,
    string NextStep);

public sealed record Tf2Ps3SourceMarkerlessDataflowGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
