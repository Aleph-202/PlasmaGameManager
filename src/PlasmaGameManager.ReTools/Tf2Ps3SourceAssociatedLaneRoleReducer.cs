using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceAssociatedLaneRoleReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008be1e8",
        "008b9ad8",
        "008bdff0",
        "00a58418"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceAssociatedLaneRoleReport> ReduceAsync(
        string cExportPath,
        string payloadObjectDispatchPath,
        string associatedObjectSlot90Path,
        string associatedSlotAcProvenancePath,
        string ownerForwardContextPath,
        string category5UsercmdHandlerPath,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        using var payloadDispatchDoc = JsonDocument.Parse(await File.ReadAllTextAsync(payloadObjectDispatchPath));
        using var slot90Doc = JsonDocument.Parse(await File.ReadAllTextAsync(associatedObjectSlot90Path));
        using var slotAcProvenanceDoc = JsonDocument.Parse(await File.ReadAllTextAsync(associatedSlotAcProvenancePath));
        using var ownerForwardDoc = JsonDocument.Parse(await File.ReadAllTextAsync(ownerForwardContextPath));
        using var category5Doc = JsonDocument.Parse(await File.ReadAllTextAsync(category5UsercmdHandlerPath));

        var payloadSummary = payloadDispatchDoc.RootElement.GetProperty("Summary");
        var slot90Summary = slot90Doc.RootElement.GetProperty("Summary");
        var slot90Register = slot90Doc.RootElement.GetProperty("RegisterContract");
        var slotAcSummary = slotAcProvenanceDoc.RootElement.GetProperty("Summary");
        var ownerForwardSummary = ownerForwardDoc.RootElement.GetProperty("Summary");
        var category5Summary = category5Doc.RootElement.GetProperty("Summary");

        var drain = functions.SingleOrDefault(static function => function.Address == "008be1e8");
        var lookup = functions.SingleOrDefault(static function => function.Address == "008b9ad8");
        var builder = functions.SingleOrDefault(static function => function.Address == "008bdff0");
        var slot90 = functions.SingleOrDefault(static function => function.Address == "00a58418");

        var drainRecovered =
            drain is not null
            && drain.EvidenceTokens.Contains("non-minus-one-associated-lookup", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("associated-slot90-dispatch", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("minus-one-owner-bitreader-branch", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("owner-slot8-dispatch", StringComparer.Ordinal);
        var associatedDispatchRecovered =
            ReadBool(payloadSummary, "AssociatedSlot90DispatchRecovered")
            && ReadBool(slot90Summary, "Slot90EntryRecovered")
            && ReadBool(slot90Summary, "SlotAcStateTripleRecovered");
        var associatedResetAdapterRecovered =
            associatedDispatchRecovered
            && ReadBool(slot90Register, "Slot90DoesNotExplicitlySetR5OrR6")
            && ReadBool(slot90Register, "SlotAcSetterStoresRecovered")
            && ReadBool(slotAcSummary, "AllFocusedSlotAcCallsitesRejectedAsClientInput")
            && slot90?.EvidenceTokens.Contains("slot90-zero-state-adapter", StringComparer.Ordinal) == true;
        var associatedLaneNotClcMoveBoundary =
            drainRecovered
            && associatedResetAdapterRecovered
            && ReadInt(slotAcSummary, "ClientUploadDecoderCandidateCount") == 0;

        var ownerBitreaderRecovered =
            ReadBool(payloadSummary, "OwnerBitreaderDispatchRecovered")
            && drain?.EvidenceTokens.Contains("minus-one-owner-bitreader-branch", StringComparer.Ordinal) == true
            && drain.EvidenceTokens.Contains("owner-bitreader-word-refill", StringComparer.Ordinal)
            && drain.EvidenceTokens.Contains("owner-slot8-dispatch", StringComparer.Ordinal);
        var category5UsercmdRecovered =
            ReadBool(ownerForwardSummary, "Category5UsercmdRouteCandidateRecovered")
            && ReadBool(category5Summary, "Category5RouteRecovered")
            && ReadBool(category5Summary, "HandlerCallConventionMatchesDispatchObject")
            && ReadBool(category5Summary, "ClcMoveReadContractRecovered")
            && ReadBool(category5Summary, "OfficialServerDllUsercmdDecoderRecovered");
        var ownerLaneIsUsercmdRoute =
            ownerBitreaderRecovered
            && category5UsercmdRecovered;
        var nativeSourceInputReady =
            false;

        var gates = new[]
        {
            new Tf2Ps3SourceAssociatedLaneRoleGate(
                "payload-drain-splits-associated-and-owner-lanes",
                drainRecovered ? "proven" : "missing",
                "008be1e8",
                "TF.elf drain must show non--1 payload-object dispatch separately from -1 owner bitreader dispatch."),
            new Tf2Ps3SourceAssociatedLaneRoleGate(
                "associated-lane-is-state-reset-adapter",
                associatedResetAdapterRecovered ? "proven" : "missing",
                "00a58418 -> +0xac, source-associated-slotac-provenance.json",
                "The non--1 associated slot +0x90 lane must be proven as state/reset/association, not CLC_Move decoding."),
            new Tf2Ps3SourceAssociatedLaneRoleGate(
                "owner-lane-is-category5-usercmd-route",
                ownerLaneIsUsercmdRoute ? "proven" : "missing",
                "008be1e8 -1 branch -> owner +0x08 -> 008722a0 category 5 -> 00a291c0",
                "The usercmd path must remain anchored to the owner -1 bitreader route and official server.dll CUserCmd decoder."),
            new Tf2Ps3SourceAssociatedLaneRoleGate(
                "native-source-input-ready",
                "missing",
                "requires PCAP/live markerless packet proof plus steady gameplay",
                "This report refines the lane roles; it does not live-prove map-load input completion.")
        };

        var report = new Tf2Ps3SourceAssociatedLaneRoleReport(
            "tf2ps3-source-associated-lane-role",
            "Separates TF.elf markerless Source input lanes: non--1 payload objects are associated-object state/association dispatch, while CLC_Move/usercmd evidence belongs to the -1 owner bitreader/category-5 route.",
            new Tf2Ps3SourceAssociatedLaneRoleInputs(
                cExportPath,
                payloadObjectDispatchPath,
                associatedObjectSlot90Path,
                associatedSlotAcProvenancePath,
                ownerForwardContextPath,
                category5UsercmdHandlerPath),
            new Tf2Ps3SourceAssociatedLaneRoleSummary(
                TargetAddresses.Length,
                functions.Length,
                drainRecovered,
                associatedDispatchRecovered,
                associatedResetAdapterRecovered,
                associatedLaneNotClcMoveBoundary,
                ownerBitreaderRecovered,
                category5UsercmdRecovered,
                ownerLaneIsUsercmdRoute,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status == "missing")),
            functions,
            new Tf2Ps3SourceAssociatedLaneRoleModel(
                "non--1 first word",
                "008be1e8 -> 008b9ad8 -> associated object vtable +0x90 -> 00a58418 -> vtable +0xac/00a578c8",
                associatedLaneNotClcMoveBoundary
                    ? "association/state reset lane; do not decode as CLC_Move without a separate owner/category route"
                    : "association lane not fully classified",
                "-1 first word",
                "008be1e8 local bitreader -> owner vtable +0x08 -> 008722a0 context route -> category 5 -> 00a2bd18 -> 00a291c0",
                ownerLaneIsUsercmdRoute
                    ? "native CLC_Move/usercmd route candidate with official server.dll CUserCmd decoder evidence"
                    : "owner/usercmd route still incomplete"),
            gates,
            [
                "The C export and existing reducers agree that 008be1e8 splits markerless payloads by first word: non--1 payload objects go to associated-object lookup and slot +0x90; -1 payloads are rebuilt as a bitreader and passed to owner slot +0x08.",
                "The associated slot +0x90 target 00a58418 is a zero-state/reset adapter. It delegates to +0xac/00a578c8 and does not explicitly prepare r5/r6 for a three-word client upload grammar.",
                "Focused +0xac callsites recovered so far are server-output state transitions with state constant 0x0c, not client-upload CLC_Move decoders.",
                "The recovered CLC_Move/usercmd route remains the owner -1/category-5 path ending at 00a291c0 and the EA server.dll CUserCmd field contract.",
                "NativeMapLoadReady remains false until live/PCAP hard markerless packets are proven against the owner/category route and the generated map-load output reaches steady gameplay."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceAssociatedLaneRoleFunction BuildFunction(ExtractedFunction function)
    {
        var body = string.Join('\n', function.Lines);
        var tokens = new List<string>();

        AddIf(body, tokens, "_opd_FUN_008b9ad8(param_1,piVar7)", "non-minus-one-associated-lookup");
        AddIf(body, tokens, "*piVar7 + 0x90", "associated-slot90-dispatch");
        AddIf(body, tokens, "(int)param_7 != -1", "non-minus-one-discriminator");
        AddIf(body, tokens, "uVar5 + 0x1c", "minus-one-owner-bitreader-branch");
        AddIf(body, tokens, "*param_2 + 8", "owner-slot8-dispatch");
        AddIf(body, tokens, "owner-bitreader-word-refill", "owner-bitreader-word-refill");
        AddIf(body, tokens, "_opd_FUN_008bdff0", "payload-object-builder");
        AddIf(body, tokens, "FUN_0086de68((int)(puVar8 + 7)", "payload-object-bitreader-init");
        AddIf(body, tokens, "FUN_0086fb58(param_2,piVar5,0)", "association-descriptor-compare");
        AddIf(body, tokens, "FUN_0086ff38((int)(param_1 + 0x11))", "slot90-reset-buffer");
        AddIf(body, tokens, "*param_1 + 0xac", "slot90-delegates-to-slot-ac");
        AddIf(body, tokens, "(param_1,0)", "slot90-zero-state-adapter");

        if (function.Address == "008be1e8" && body.Contains("*(uint *)(iVar8 + 0x10)", StringComparison.Ordinal))
        {
            tokens.Add("owner-bitreader-word-refill");
        }

        return new Tf2Ps3SourceAssociatedLaneRoleFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            RoleFor(function.Address),
            tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            string.Join('\n', function.Lines.Take(80)));
    }

    private static string RoleFor(string address) => address switch
    {
        "008be1e8" => "payload-object-drain-lane-split",
        "008b9ad8" => "associated-object-lookup",
        "008bdff0" => "payload-object-builder",
        "00a58418" => "associated-slot90-state-reset-adapter",
        _ => "target-function"
    };

    private static List<ExtractedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExtractedFunction>();
        ExtractedFunction? current = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = FunctionDefinitionRegex.Match(line);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(line);
            }

            if (match.Success)
            {
                if (current is not null)
                {
                    current.EndLine = i;
                    functions.Add(current);
                }

                current = new ExtractedFunction(
                    match.Groups["name"].Value,
                    match.Groups["address"].Value,
                    i + 1);
            }

            current?.Lines.Add(line);
        }

        if (current is not null)
        {
            current.EndLine = lines.Length;
            functions.Add(current);
        }

        return functions;
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;

    private sealed class ExtractedFunction(string name, string address, int startLine)
    {
        public string Name { get; } = name;
        public string Address { get; } = address;
        public int StartLine { get; } = startLine;
        public int EndLine { get; set; } = startLine;
        public List<string> Lines { get; } = [];
    }
}

public sealed record Tf2Ps3SourceAssociatedLaneRoleReport(
    string Status,
    string Purpose,
    Tf2Ps3SourceAssociatedLaneRoleInputs Inputs,
    Tf2Ps3SourceAssociatedLaneRoleSummary Summary,
    Tf2Ps3SourceAssociatedLaneRoleFunction[] Functions,
    Tf2Ps3SourceAssociatedLaneRoleModel LaneModel,
    Tf2Ps3SourceAssociatedLaneRoleGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedLaneRoleInputs(
    string CExportPath,
    string PayloadObjectDispatchPath,
    string AssociatedObjectSlot90Path,
    string AssociatedSlotAcProvenancePath,
    string OwnerForwardContextPath,
    string Category5UsercmdHandlerPath);

public sealed record Tf2Ps3SourceAssociatedLaneRoleSummary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool PayloadDrainLaneSplitRecovered,
    bool AssociatedDispatchRecovered,
    bool AssociatedResetAdapterRecovered,
    bool AssociatedLaneRejectedAsClcMoveBoundary,
    bool OwnerBitreaderRecovered,
    bool Category5UsercmdRecovered,
    bool OwnerLaneIsUsercmdRoute,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceAssociatedLaneRoleFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceAssociatedLaneRoleModel(
    string AssociatedLaneSelector,
    string AssociatedLanePath,
    string AssociatedLaneRole,
    string OwnerLaneSelector,
    string OwnerLanePath,
    string OwnerLaneRole);

public sealed record Tf2Ps3SourceAssociatedLaneRoleGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
