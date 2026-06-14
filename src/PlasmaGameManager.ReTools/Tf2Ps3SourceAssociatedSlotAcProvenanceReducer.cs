using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceAssociatedSlotAcProvenanceReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceAssociatedSlotAcProvenanceReport> ReduceAsync(
        string outputBuilderFunctionsPath,
        string associatedObjectSlot90Path,
        string outputPath)
    {
        using var outputBuilderDoc = JsonDocument.Parse(await File.ReadAllTextAsync(outputBuilderFunctionsPath));
        using var slot90Doc = JsonDocument.Parse(await File.ReadAllTextAsync(associatedObjectSlot90Path));

        var slot90Register = slot90Doc.RootElement.TryGetProperty("RegisterContract", out var register)
            ? register
            : default;
        var slot90DoesNotSetR5R6 = ReadBool(slot90Register, "Slot90DoesNotExplicitlySetR5OrR6");
        var slot90SetterStores = ReadBool(slot90Register, "SlotAcSetterStoresRecovered");

        var callsites = new List<Tf2Ps3SourceAssociatedSlotAcProvenanceCallsite>();
        foreach (var function in outputBuilderDoc.RootElement.GetProperty("functions").EnumerateArray())
        {
            var entry = ReadString(function, "entry");
            var name = ReadString(function, "name");
            var body = ReadString(function, "body");
            var instructions = function.GetProperty("instructions").EnumerateArray()
                .Select(static instruction => new InstructionRow(ReadString(instruction, "address"), ReadString(instruction, "text")))
                .ToArray();

            for (var i = 0; i < instructions.Length; i++)
            {
                if (!instructions[i].Text.Contains("0xac(", StringComparison.Ordinal))
                {
                    continue;
                }

                var bctrlIndex = Array.FindIndex(instructions, i + 1, static instruction => instruction.Text == "bctrl");
                if (bctrlIndex < 0 || bctrlIndex - i > 8)
                {
                    continue;
                }

                var windowStart = Math.Max(0, i - 10);
                var window = instructions[windowStart..(bctrlIndex + 1)];
                var prepWindow = SliceAfterLastOrdinaryCall(window);
                var receiver = LastRegisterWrite(prepWindow, "r3");
                var state = LastRegisterWrite(prepWindow, "r4");
                var arg5 = LastRegisterWrite(prepWindow, "r5");
                var arg6 = LastRegisterWrite(prepWindow, "r6");
                var stateImmediate = TryParseImmediate(state);
                var outputWriterEvidence = HasOutputWriterEvidence(body);
                var receiverIsLocalBuilderObject = receiver.StartsWith("or r3,r", StringComparison.Ordinal);
                var noAdditionalStateWords = string.IsNullOrWhiteSpace(arg5) && string.IsNullOrWhiteSpace(arg6);
                var provenServerOutputState =
                    outputWriterEvidence
                    && receiverIsLocalBuilderObject
                    && stateImmediate == "0xc"
                    && noAdditionalStateWords;
                var clientUploadCandidate =
                    !provenServerOutputState
                    && (!noAdditionalStateWords || stateImmediate.Length == 0);

                callsites.Add(new Tf2Ps3SourceAssociatedSlotAcProvenanceCallsite(
                    entry,
                    name,
                    instructions[i].Address,
                    instructions[bctrlIndex].Address,
                    receiver,
                    state,
                    stateImmediate,
                    arg5,
                    arg6,
                    outputWriterEvidence,
                    receiverIsLocalBuilderObject,
                    noAdditionalStateWords,
                    provenServerOutputState,
                    clientUploadCandidate,
                    provenServerOutputState
                        ? "server-output-state-0x0c"
                        : clientUploadCandidate
                            ? "candidate-client-upload-or-unresolved-state"
                            : "unclassified-slot-ac-state-transition",
                    window.Select(static instruction => $"{instruction.Address}: {instruction.Text}").ToArray()));
            }
        }

        var stateConstants = callsites
            .Select(static callsite => callsite.StateImmediate)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => Convert.ToInt32(value[2..], 16))
            .ToArray();
        var serverOutputCallsites = callsites.Count(static callsite => callsite.ProvenServerOutputState);
        var clientUploadCandidates = callsites.Count(static callsite => callsite.ClientUploadCandidate);
        var allFocusedCallsitesRejectedAsClientInput = callsites.Count > 0 && clientUploadCandidates == 0;

        var report = new Tf2Ps3SourceAssociatedSlotAcProvenanceReport(
            "tf2ps3-source-associated-slotac-provenance",
            "Separates focused TF.elf vtable +0xac state-transition callsites that are proven server-output builders from the still-missing client-upload field grammar.",
            new Tf2Ps3SourceAssociatedSlotAcProvenanceInputs(
                outputBuilderFunctionsPath,
                associatedObjectSlot90Path),
            new Tf2Ps3SourceAssociatedSlotAcProvenanceSummary(
                outputBuilderDoc.RootElement.GetProperty("functions").GetArrayLength(),
                callsites.Count,
                serverOutputCallsites,
                clientUploadCandidates,
                allFocusedCallsitesRejectedAsClientInput,
                slot90DoesNotSetR5R6,
                slot90SetterStores,
                stateConstants,
                false,
                allFocusedCallsitesRejectedAsClientInput ? 1 : 2),
            callsites.ToArray(),
            [
                "The focused +0xac output-builder exports all use local Source output objects as the receiver and write the one-word state constant 0x0c.",
                "These callsites do not set r5 or r6 before the slot +0xac bctrl, so they do not explain the three-word client-upload state grammar by themselves.",
                "The associated slot +0x90 wrapper is still proven to set only r4=0 before +0xac; no focused evidence promotes hard markerless uploads to CLC_Move/usercmd yet.",
                "This narrows the remaining work: recover a different upstream client-upload field source or a downstream consumer that gives the +0x08/+0x0c/+0x10 state triple live meaning."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static bool HasOutputWriterEvidence(string body) =>
        body.Contains("+ 0x70", StringComparison.Ordinal)
        || body.Contains("+ 0x74", StringComparison.Ordinal)
        || body.Contains("+ 0x7c", StringComparison.Ordinal)
        || body.Contains("_opd_FUN_01381a80", StringComparison.Ordinal)
        || body.Contains("_opd_FUN_011390e8", StringComparison.Ordinal);

    private static string LastRegisterWrite(IReadOnlyList<InstructionRow> instructions, string register)
    {
        for (var i = instructions.Count - 1; i >= 0; i--)
        {
            if (IsRegisterWrite(instructions[i].Text, register))
            {
                return instructions[i].Text;
            }
        }

        return "";
    }

    private static InstructionRow[] SliceAfterLastOrdinaryCall(IReadOnlyList<InstructionRow> instructions)
    {
        for (var i = instructions.Count - 1; i >= 0; i--)
        {
            if (instructions[i].Text.StartsWith("bl ", StringComparison.Ordinal))
            {
                return instructions.Skip(i + 1).ToArray();
            }
        }

        return instructions.ToArray();
    }

    private static bool IsRegisterWrite(string text, string register) =>
        text.StartsWith("li " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("or " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("addi " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("lwz " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("ld " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("rldicl " + register + ",", StringComparison.Ordinal);

    private static string TryParseImmediate(string instruction)
    {
        var match = Regex.Match(instruction, @"^li r4,(?<value>0x[0-9a-f]+|-?\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return "";
        }

        var raw = match.Groups["value"].Value;
        var value = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(raw[2..], 16)
            : int.Parse(raw);
        return $"0x{value:x}";
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

    private readonly record struct InstructionRow(string Address, string Text);
}

public sealed record Tf2Ps3SourceAssociatedSlotAcProvenanceReport(
    string Status,
    string Purpose,
    Tf2Ps3SourceAssociatedSlotAcProvenanceInputs Inputs,
    Tf2Ps3SourceAssociatedSlotAcProvenanceSummary Summary,
    Tf2Ps3SourceAssociatedSlotAcProvenanceCallsite[] Callsites,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedSlotAcProvenanceInputs(
    string OutputBuilderFunctionsPath,
    string AssociatedObjectSlot90Path);

public sealed record Tf2Ps3SourceAssociatedSlotAcProvenanceSummary(
    int FocusedFunctionCount,
    int SlotAcCallsiteCount,
    int ProvenServerOutputStateCallsiteCount,
    int ClientUploadDecoderCandidateCount,
    bool AllFocusedSlotAcCallsitesRejectedAsClientInput,
    bool Slot90DoesNotExplicitlySetR5OrR6,
    bool SlotAcSetterStoresRecovered,
    string[] ProvenStateConstants,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceAssociatedSlotAcProvenanceCallsite(
    string Function,
    string Name,
    string SlotLoadAddress,
    string CallAddress,
    string ReceiverRegisterWrite,
    string StateRegisterWrite,
    string StateImmediate,
    string Arg5RegisterWrite,
    string Arg6RegisterWrite,
    bool OutputWriterEvidence,
    bool ReceiverIsLocalBuilderObject,
    bool NoAdditionalStateWords,
    bool ProvenServerOutputState,
    bool ClientUploadCandidate,
    string Classification,
    string[] InstructionWindow);
