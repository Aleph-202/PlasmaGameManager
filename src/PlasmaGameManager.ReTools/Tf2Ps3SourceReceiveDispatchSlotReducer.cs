using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceReceiveDispatchSlotReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly string[] ReceiveSlots = ["0x68", "0x6c", "0x70"];

    public static async Task<Tf2Ps3SourceReceiveDispatchSlotReport> ReduceAsync(
        string cExportPath,
        string objectLifecyclePath,
        string clientReceiveContractPath,
        string outputPath)
    {
        using var objectLifecycle = JsonDocument.Parse(await File.ReadAllTextAsync(objectLifecyclePath));
        using var clientReceiveContract = JsonDocument.Parse(await File.ReadAllTextAsync(clientReceiveContractPath));

        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var callsites = functions
            .Select(BuildCallsite)
            .Where(static callsite => callsite.SlotOffsets.Length > 0)
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();

        var sourceRelevant = callsites
            .Where(static callsite => callsite.Role != "non-source-or-unclassified-slot-call")
            .OrderBy(static callsite => callsite.Address, StringComparer.Ordinal)
            .ToArray();

        var slot68Setup = sourceRelevant.Any(static callsite =>
            callsite.Address == "007cc0d0"
            && callsite.SlotOffsets.Contains("0x68", StringComparer.Ordinal)
            && callsite.Role == "source-object-setup-slot-0x68-candidate");
        var slot6cAttach = sourceRelevant.Any(static callsite =>
            callsite.Address == "008b9468"
            && callsite.SlotOffsets.Contains("0x6c", StringComparer.Ordinal)
            && callsite.Role == "proven-accepted-peer-attach-slot-0x6c");
        var slot6cAttachedReader = sourceRelevant.Any(static callsite =>
            callsite.Address == "00a5c2e8"
            && callsite.SlotOffsets.Contains("0x6c", StringComparer.Ordinal)
            && callsite.Role == "proven-attached-stream-reader-slot-0x6c");
        var slot70Setup = sourceRelevant.Any(static callsite =>
            callsite.Address is "0070c300" or "00a5d0c0"
            && callsite.SlotOffsets.Contains("0x70", StringComparer.Ordinal)
            && callsite.Status == "candidate-setup-not-ingress");
        var markerlessIngressSlot70 = sourceRelevant.Any(static callsite =>
            callsite.Role == "direct-markerless-ingress-slot-0x70-proven");

        var clientSummary = clientReceiveContract.RootElement.GetProperty("Summary");
        var lifecycleSummary = objectLifecycle.RootElement.GetProperty("Summary");
        var directMarkerlessIngressPreviouslyProven = ReadBool(clientSummary, "DirectMarkerlessIngressProven");
        var nativeReadyPreviouslyProven = ReadBool(clientSummary, "NativeInputServerReady");
        var nativeInputReady = markerlessIngressSlot70 && nativeReadyPreviouslyProven;

        var gates = new[]
        {
            new Tf2Ps3SourceReceiveDispatchSlotGate(
                "slot-0x6c-attach-callsite-proven",
                slot6cAttach && slot6cAttachedReader ? "proven" : "missing",
                "008b9468 and 00a5c2e8",
                "The accepted-peer attach path invokes the Source object +0x6c reader, and the reader recursively pumps attached frame state through the same slot."),
            new Tf2Ps3SourceReceiveDispatchSlotGate(
                "slot-0x68-source-object-caller",
                slot68Setup ? "candidate-setup-only" : "missing",
                "007cc0d0",
                "The slot +0x68 caller is tied to Source object setup through 0039f330, but this is setup/initialization evidence rather than live markerless UDP ingress."),
            new Tf2Ps3SourceReceiveDispatchSlotGate(
                "slot-0x70-source-object-caller",
                slot70Setup ? "candidate-setup-only" : "missing",
                "0070c300 / 00a5d0c0",
                "The slot +0x70 callers are tied to Source object setup/association and rate-time initialization, not to the direct PCAP markerless receive path."),
            new Tf2Ps3SourceReceiveDispatchSlotGate(
                "direct-markerless-ingress-through-slot-0x70",
                markerlessIngressSlot70 || directMarkerlessIngressPreviouslyProven ? "proven" : "missing",
                "008b8d50 recvfrom buffer -> 00a5d9e0 bitreader",
                "Still no C-visible callsite proves that raw markerless client UDP bodies are wrapped and dispatched through helper slice slot +0x70."),
            new Tf2Ps3SourceReceiveDispatchSlotGate(
                "native-source-input-ready",
                nativeInputReady ? "proven" : "missing",
                "server implementation gate",
                "The native replacement must keep markerless client input incomplete until the direct ingress gate is proven.")
        };

        var report = new Tf2Ps3SourceReceiveDispatchSlotReport(
            "tf2ps3-source-receive-dispatch-slots",
            "Classifies exact TF.elf virtual callsites for Source receive-adjacent slots +0x68, +0x6c, and +0x70. This report intentionally separates proven attached-frame receive calls from setup-only calls that must not be treated as markerless packet ingress.",
            new Tf2Ps3SourceReceiveDispatchSlotInputs(
                cExportPath,
                objectLifecyclePath,
                clientReceiveContractPath),
            new Tf2Ps3SourceReceiveDispatchSlotSummary(
                ReadString(lifecycleSummary, "SourceObjectSize"),
                functions.Count,
                callsites.Length,
                CountSlot(callsites, "0x68"),
                CountSlot(callsites, "0x6c"),
                CountSlot(callsites, "0x70"),
                sourceRelevant.Length,
                slot6cAttach,
                slot6cAttachedReader,
                slot68Setup,
                slot70Setup,
                markerlessIngressSlot70 || directMarkerlessIngressPreviouslyProven,
                nativeInputReady,
                gates.Count(static gate => gate.Status != "proven")),
            sourceRelevant,
            gates,
            BuildFindings(slot6cAttach, slot6cAttachedReader, slot68Setup, slot70Setup, markerlessIngressSlot70 || directMarkerlessIngressPreviouslyProven));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceReceiveDispatchSlotCallsite BuildCallsite(ExportedFunction function)
    {
        var callsBySlot = ReceiveSlots
            .Select(slot => new
            {
                Slot = slot,
                Calls = ExtractSlotCalls(function.Lines, slot)
            })
            .Where(static item => item.Calls.Length > 0)
            .ToArray();

        var slotOffsets = callsBySlot
            .Select(static item => item.Slot)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var callExpressions = callsBySlot
            .SelectMany(static item => item.Calls.Select(call => $"{item.Slot}: {call}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var evidence = BuildEvidence(function, slotOffsets, callExpressions);
        var (role, status, meaning) = Classify(function, slotOffsets, evidence);

        return new Tf2Ps3SourceReceiveDispatchSlotCallsite(
            function.Address,
            function.Name,
            role,
            status,
            slotOffsets,
            callExpressions,
            evidence,
            meaning,
            Preview(function.Lines));
    }

    private static (string Role, string Status, string Meaning) Classify(
        ExportedFunction function,
        string[] slots,
        string[] evidence)
    {
        if (function.Address == "008b9468" && slots.Contains("0x6c", StringComparer.Ordinal))
        {
            return (
                "proven-accepted-peer-attach-slot-0x6c",
                "proven",
                "Accepted peer message type 4 attaches a socket to the Source object and invokes the attached reader slot.");
        }

        if (function.Address == "00a5c2e8" && slots.Contains("0x6c", StringComparer.Ordinal))
        {
            return (
                "proven-attached-stream-reader-slot-0x6c",
                "proven",
                "The attached stream reader recursively advances frame state and dispatches complete type-2 payloads to 00a58c10.");
        }

        if (function.Address == "007cc0d0" && slots.Contains("0x68", StringComparer.Ordinal))
        {
            return (
                "source-object-setup-slot-0x68-candidate",
                "candidate-setup-not-ingress",
                "Creates a Source object through 0039f330 and immediately calls slot +0x68; useful lifecycle evidence, not markerless packet ingress proof.");
        }

        if (function.Address == "0070c300" && slots.Contains("0x70", StringComparer.Ordinal))
        {
            return (
                "source-object-setup-slot-0x70-candidate",
                "candidate-setup-not-ingress",
                "Creates/stores a Source object through 0039f330 and calls slot +0x70 during setup; this must not be mistaken for live markerless receive.");
        }

        if (function.Address == "00a5d0c0" && slots.Contains("0x70", StringComparer.Ordinal))
        {
            return (
                "source-object-association-slot-0x70-candidate",
                "candidate-setup-not-ingress",
                "Associates owner/socket state and calls object slot +0x70 after buffer sizing; this is association/rate initialization, not direct markerless ingress.");
        }

        if (evidence.Contains("source-object-lifecycle-token", StringComparer.Ordinal)
            || evidence.Contains("source-payload-dispatcher-call", StringComparer.Ordinal)
            || evidence.Contains("source-attached-state-token", StringComparer.Ordinal))
        {
            return (
                "source-adjacent-unclassified-slot-call",
                "unclassified",
                "Source-adjacent evidence exists, but the receiver object or ingress role is not proven.");
        }

        return (
            "non-source-or-unclassified-slot-call",
            "not-evidence",
            "Exact vtable slot call exists, but the surrounding function does not prove Source receive semantics.");
    }

    private static string[] BuildFindings(
        bool slot6cAttach,
        bool slot6cAttachedReader,
        bool slot68Setup,
        bool slot70Setup,
        bool markerlessIngressSlot70)
    {
        var findings = new List<string>
        {
            "Exact-slot scanning confirms the proven attached path: 008b9468 invokes Source object slot +0x6c after accepted-peer association, and 00a5c2e8 is the attached-frame reader behind that slot.",
            "The useful +0x68 evidence is currently setup-only: 007cc0d0 creates a Source object and calls slot +0x68, but that does not consume raw PCAP markerless client datagrams.",
            "The useful +0x70 evidence is also setup/association-only: 0070c300 and 00a5d0c0 call slot +0x70 while creating or associating the Source object. This supports the helper-slice candidate map but does not prove direct receive ingress.",
            "No direct slot +0x70 callsite is currently tied to 008b8d50's recvfrom stack buffer or to the 11,874 opaque markerless client packets.",
            "Implementation consequence: keep the replacement server's markerless client-input parser behind an explicit incomplete gate until a Ghidra/callgraph pass proves the 008b8d50 -> bitreader/00a5d9e0 edge."
        };

        if (!slot6cAttach || !slot6cAttachedReader)
        {
            findings.Add("Unexpected regression: the slot +0x6c attach/reader path is not fully proven in this report.");
        }

        if (!slot68Setup)
        {
            findings.Add("No setup-only slot +0x68 Source-object caller was recovered; inspect 007cc0d0 export quality.");
        }

        if (!slot70Setup)
        {
            findings.Add("No setup-only slot +0x70 Source-object caller was recovered; inspect 0070c300/00a5d0c0 export quality.");
        }

        if (markerlessIngressSlot70)
        {
            findings.Add("Direct markerless ingress is now proven by the inputs; promote native input only after matching the packet semantics and server implementation.");
        }

        return findings.ToArray();
    }

    private static string[] BuildEvidence(ExportedFunction function, string[] slotOffsets, string[] callExpressions)
    {
        var tokens = new List<string>();
        foreach (var slot in slotOffsets)
        {
            tokens.Add($"virtual-slot-{slot}");
        }

        AddIf(function.Body, tokens, "FUN_0039f330", "source-object-creator-wrapper-call");
        AddIf(function.Body, tokens, "_opd_FUN_008b9c38", "source-object-create-or-reuse-call");
        AddIf(function.Body, tokens, "_opd_FUN_00a5d0c0", "source-object-associate-owner-call");
        AddIf(function.Body, tokens, "_opd_FUN_00a58c10", "source-payload-dispatcher-call");
        AddIf(function.Body, tokens, "_opd_FUN_008b82c0", "attached-socket-read-call");
        AddIf(function.Body, tokens, "recvfrom", "udp-recvfrom-call");
        AddIf(function.Body, tokens, "param_1[0x24]", "attached-socket-field");
        AddIf(function.Body, tokens, "0x42e", "source-attached-state-token");
        AddIf(function.Body, tokens, "param_1[0x10c]", "attached-frame-kind-field");
        AddIf(function.Body, tokens, "param_1[0x151]", "attached-payload-buffer-field");
        AddIf(function.Body, tokens, "param_1[0x782]", "owner-callback-field");
        AddIf(function.Body, tokens, "0x1e0c", "registered-handler-vector-token");
        AddIf(function.Body, tokens, "0x1e18", "registered-handler-count-token");
        AddIf(function.Body, tokens, "0x1e28", "source-object-size-token");

        if (function.Address is "0070c300" or "007cc0d0" or "008b9468" or "00a5c2e8" or "00a5d0c0")
        {
            tokens.Add("source-object-lifecycle-token");
        }

        if (callExpressions.Any(static call => call.Contains("**(int **)(puVar4 + 0x10)", StringComparison.Ordinal)))
        {
            tokens.Add("stored-source-object-receiver");
        }

        if (callExpressions.Any(static call => call.Contains("*piVar6 + 0x6c", StringComparison.Ordinal)))
        {
            tokens.Add("accepted-peer-source-object-receiver");
        }

        if (callExpressions.Any(static call => call.Contains("*param_1 + 0x6c", StringComparison.Ordinal)))
        {
            tokens.Add("self-source-object-reader-receiver");
        }

        if (callExpressions.Any(static call => call.Contains("*puVar3 + 0x68", StringComparison.Ordinal)))
        {
            tokens.Add("created-source-object-slot-0x68-receiver");
        }

        if (callExpressions.Any(static call => call.Contains("*param_1 + 0x70", StringComparison.Ordinal)))
        {
            tokens.Add("self-source-object-slot-0x70-receiver");
        }

        return tokens
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractSlotCalls(string[] lines, string slot)
    {
        var regex = new Regex(@"\(\*\(code \*\).*?\+\s*" + Regex.Escape(slot) + @"(?![0-9a-f])", RegexOptions.Compiled);
        var calls = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!regex.IsMatch(line))
            {
                continue;
            }

            var combined = line;
            for (var j = i + 1; j < Math.Min(lines.Length, i + 5); j++)
            {
                if (combined.EndsWith(");", StringComparison.Ordinal) || combined.EndsWith("))", StringComparison.Ordinal))
                {
                    break;
                }

                combined += " " + lines[j].Trim();
            }

            calls.Add(combined);
        }

        return calls
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountSlot(IEnumerable<Tf2Ps3SourceReceiveDispatchSlotCallsite> callsites, string slot) =>
        callsites.Count(callsite => callsite.SlotOffsets.Contains(slot, StringComparer.Ordinal));

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static bool ReadBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExportedFunction>();
        var start = -1;
        var name = "";
        var address = "";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]))
            {
                continue;
            }

            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[i]);
            }

            if (!match.Success)
            {
                continue;
            }

            if (start >= 0)
            {
                functions.Add(BuildExportedFunction(lines, start, i - 1, name, address));
            }

            start = i;
            name = match.Groups["name"].Value;
            address = match.Groups["address"].Value;
        }

        if (start >= 0)
        {
            functions.Add(BuildExportedFunction(lines, start, lines.Length - 1, name, address));
        }

        return functions;
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(70));
        return text.Length <= 2200 ? text : text[..2200];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceReceiveDispatchSlotReport(
    string Status,
    string Note,
    Tf2Ps3SourceReceiveDispatchSlotInputs Inputs,
    Tf2Ps3SourceReceiveDispatchSlotSummary Summary,
    Tf2Ps3SourceReceiveDispatchSlotCallsite[] SourceRelevantCallsites,
    Tf2Ps3SourceReceiveDispatchSlotGate[] Gates,
    string[] Findings);

public sealed record Tf2Ps3SourceReceiveDispatchSlotInputs(
    string CExportInput,
    string SourceObjectLifecycleReport,
    string SourceClientReceiveContractReport);

public sealed record Tf2Ps3SourceReceiveDispatchSlotSummary(
    string SourceObjectSize,
    int ScannedFunctionCount,
    int ExactReceiveSlotCallsiteCount,
    int Slot68CallsiteCount,
    int Slot6cCallsiteCount,
    int Slot70CallsiteCount,
    int SourceRelevantCallsiteCount,
    bool Slot6cAcceptedPeerAttachProven,
    bool Slot6cAttachedReaderProven,
    bool Slot68SourceSetupCandidateRecovered,
    bool Slot70SourceSetupCandidateRecovered,
    bool DirectMarkerlessIngressSlot70Proven,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceReceiveDispatchSlotCallsite(
    string Address,
    string Name,
    string Role,
    string Status,
    string[] SlotOffsets,
    string[] CallExpressions,
    string[] EvidenceTokens,
    string Meaning,
    string Preview);

public sealed record Tf2Ps3SourceReceiveDispatchSlotGate(
    string Id,
    string Status,
    string EvidenceSource,
    string Meaning);
