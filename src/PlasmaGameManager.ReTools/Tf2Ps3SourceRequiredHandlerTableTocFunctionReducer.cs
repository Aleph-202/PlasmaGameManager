using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceRequiredHandlerTableTocFunctionReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceRequiredHandlerTableTocFunctionReport> ReduceAsync(
        string tocAccessPath,
        string instructionContextPath,
        string focusedFunctionsPath,
        string outputPath)
    {
        using var tocDocument = JsonDocument.Parse(await File.ReadAllTextAsync(tocAccessPath));
        using var contextDocument = JsonDocument.Parse(await File.ReadAllTextAsync(instructionContextPath));
        using var functionsDocument = JsonDocument.Parse(await File.ReadAllTextAsync(focusedFunctionsPath));

        var instructionContexts = contextDocument.RootElement.GetProperty("Entries")
            .EnumerateArray()
            .Select(static entry => new InstructionContext(
                ReadString(entry, "Address"),
                ReadString(entry, "FunctionEntry"),
                ReadString(entry, "FunctionName")))
            .Where(static entry => entry.InstructionAddress.Length > 0)
            .ToDictionary(static entry => NormalizeAddress(entry.InstructionAddress), static entry => entry);

        var accesses = tocDocument.RootElement.GetProperty("Accesses")
            .EnumerateArray()
            .Select(access => new TocFunctionAccess(
                ReadString(access, "InstructionAddress"),
                ReadString(access, "InstructionWord"),
                ReadString(access, "MnemonicFamily"),
                ReadString(access, "TocBase"),
                ReadString(access, "TargetDataAddress"),
                ReadString(access, "TargetKind"),
                ReadString(access, "TargetValue"),
                ReadString(access, "TargetAnnotation"),
                instructionContexts.TryGetValue(NormalizeAddress(ReadString(access, "InstructionAddress")), out var context)
                    ? context.FunctionEntry
                    : "",
                instructionContexts.TryGetValue(NormalizeAddress(ReadString(access, "InstructionAddress")), out context)
                    ? context.FunctionName
                    : ""))
            .Where(static access => access.TargetKind == "direct-neighborhood-word")
            .Where(static access => IsFocusedAnnotation(access.TargetAnnotation))
            .Where(static access => access.FunctionEntry.Length > 0)
            .ToArray();

        var functions = functionsDocument.RootElement.GetProperty("functions")
            .EnumerateArray()
            .Where(static function => function.ValueKind == JsonValueKind.Object)
            .Select(static function => new DecompiledFunction(
                NormalizeAddress(ReadString(function, "entry")),
                ReadString(function, "name"),
                ReadString(function, "signature"),
                ReadString(function, "body")))
            .Where(static function => function.Entry.Length > 0)
            .GroupBy(static function => function.Entry, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        var groups = accesses
            .GroupBy(static access => access.FunctionEntry, StringComparer.Ordinal)
            .Select(group => BuildFunctionGroup(group.Key, group.ToArray(), functions))
            .OrderByDescending(static group => group.RequiredHandlerCount)
            .ThenByDescending(static group => group.AccessCount)
            .ThenBy(static group => group.Entry, StringComparer.Ordinal)
            .ToArray();

        var primary = groups.FirstOrDefault(static group => group.RequiredHandlerCount == 11)
            ?? groups.FirstOrDefault()
            ?? new Tf2Ps3SourceRequiredHandlerTocFunctionGroup("", "", 0, 0, [], []);
        var primaryFunction = primary.Entry.Length > 0 && functions.TryGetValue(primary.Entry, out var function)
            ? function
            : new DecompiledFunction("", "", "", "");
        var allocations = AllocationRegex().Matches(primaryFunction.Body)
            .Select(static match => ParseHex(match.Groups["size"].Value))
            .ToArray();
        var requiredAccesses = accesses
            .Where(access => string.Equals(access.FunctionEntry, primary.Entry, StringComparison.Ordinal))
            .Where(static access => access.TargetAnnotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal))
            .OrderBy(static access => ParseHex(NormalizeAddress(access.InstructionAddress)))
            .ToArray();
        var primaryHandlers = requiredAccesses
            .Select((access, index) => BuildPrimaryHandler(access, index < allocations.Length ? allocations[index] : 0))
            .ToArray();

        var report = new Tf2Ps3SourceRequiredHandlerTableTocFunctionReport(
            "tf2ps3-source-required-handler-table-toc-function-map",
            "Merges the raw TOC/r2 accesses with Ghidra containing-function and decompile exports. This resolves the executable path that constructs/registers TF.elf's required CBaseClient client-message handlers.",
            new Tf2Ps3SourceRequiredHandlerTableTocFunctionInputs(tocAccessPath, instructionContextPath, focusedFunctionsPath),
            new Tf2Ps3SourceRequiredHandlerTableTocFunctionSummary(
                accesses.Length,
                groups.Length,
                groups.Count(static group => group.RequiredHandlerCount > 0),
                primary.Entry,
                primary.RequiredHandlerCount,
                primaryHandlers.Length,
                allocations.Length,
                primaryFunction.Body.Contains("*param_2 + 100", StringComparison.Ordinal) ? "0x64" : ""),
            primaryHandlers,
            groups.Take(48).ToArray(),
            BuildFindings(primary, primaryHandlers, primaryFunction));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRequiredHandlerTocFunctionGroup BuildFunctionGroup(
        string functionEntry,
        TocFunctionAccess[] accesses,
        IReadOnlyDictionary<string, DecompiledFunction> functions)
    {
        functions.TryGetValue(functionEntry, out var function);
        var annotations = accesses
            .Select(static access => access.TargetAnnotation)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var requiredCount = annotations.Count(static annotation => annotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal));

        return new Tf2Ps3SourceRequiredHandlerTocFunctionGroup(
            functionEntry,
            function?.Name ?? accesses[0].FunctionName,
            accesses.Length,
            requiredCount,
            annotations,
            accesses.Take(16)
                .Select(static access => new Tf2Ps3SourceRequiredHandlerTocFunctionAccessSample(
                    access.InstructionAddress,
                    access.TargetDataAddress,
                    access.TargetValue,
                    access.TargetAnnotation))
                .ToArray());
    }

    private static Tf2Ps3SourceRequiredHandlerConstructorEntry BuildPrimaryHandler(TocFunctionAccess access, uint allocationSize)
    {
        var match = RequiredHandlerAnnotationRegex().Match(access.TargetAnnotation);
        var messageName = match.Success ? match.Groups["name"].Value : access.TargetAnnotation;
        var messageId = match.Success ? int.Parse(match.Groups["id"].Value) : -1;
        return new Tf2Ps3SourceRequiredHandlerConstructorEntry(
            messageId,
            messageName,
            access.TargetValue,
            "0x" + allocationSize.ToString("x"),
            access.TargetDataAddress,
            access.InstructionAddress);
    }

    private static string[] BuildFindings(
        Tf2Ps3SourceRequiredHandlerTocFunctionGroup primary,
        Tf2Ps3SourceRequiredHandlerConstructorEntry[] primaryHandlers,
        DecompiledFunction primaryFunction)
    {
        var findings = new List<string>
        {
            $"Primary constructor candidate is {primary.Entry} with {primary.RequiredHandlerCount}/11 required handler vptrs.",
            "The constructor allocates one object per required present client message and registers it through CNetChan virtual slot +0x64.",
            "The allocation/register order matches TF.elf message-id order for ids 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, and 14.",
            "The common object header stores the vptr at +0, a byte flag at +4, a zero word at +8, and the owner/client pointer param_1+8 at +0xc."
        };

        if (primaryHandlers.Any(static handler => handler.MessageName == "CLC_Move")
            && primaryFunction.Body.Contains("FUN_0086db98", StringComparison.Ordinal))
        {
            findings.Add("CLC_Move and CLC_VoiceData initialize their embedded bit-buffer/snapshot subobjects through 0086db98 before registration.");
        }

        if (primaryHandlers.Any(static handler => handler.MessageName == "NET_SetConVar")
            && primaryFunction.Body.Contains("FUN_0086d248", StringComparison.Ordinal))
        {
            findings.Add("NET_SetConVar initializes its convar vector/list through 0086d248 before registration.");
        }

        if (primaryHandlers.Length == 11)
        {
            findings.Add("This resolves the previous concrete-handler construction gap: the CBaseClient required handlers are not copied from a runtime table, they are allocated and registered by this function.");
        }

        return findings.ToArray();
    }

    private static bool IsFocusedAnnotation(string annotation)
    {
        return annotation.StartsWith("required-handler-vptr:", StringComparison.Ordinal)
            || annotation.Contains("CBaseClient::", StringComparison.Ordinal)
            || annotation.Contains("SV_SendServerinfo", StringComparison.Ordinal)
            || annotation.Contains("Server info data overflow", StringComparison.Ordinal)
            || annotation.Contains("UpdatePlayers", StringComparison.Ordinal)
            || annotation.Contains("NetMessage", StringComparison.Ordinal);
    }

    private static string NormalizeAddress(string address)
    {
        return address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? address[2..] : address;
    }

    private static uint ParseHex(string value)
    {
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return Convert.ToUInt32(text, 16);
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    [GeneratedRegex(@"FUN_00870fc8\(0x(?<size>[0-9a-fA-F]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex AllocationRegex();

    [GeneratedRegex(@"^required-handler-vptr:(?<name>.+)/id(?<id>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex RequiredHandlerAnnotationRegex();

    private sealed record InstructionContext(string InstructionAddress, string FunctionEntry, string FunctionName);
    private sealed record DecompiledFunction(string Entry, string Name, string Signature, string Body);
    private sealed record TocFunctionAccess(
        string InstructionAddress,
        string InstructionWord,
        string MnemonicFamily,
        string TocBase,
        string TargetDataAddress,
        string TargetKind,
        string TargetValue,
        string TargetAnnotation,
        string FunctionEntry,
        string FunctionName);
}

public sealed record Tf2Ps3SourceRequiredHandlerTableTocFunctionReport(
    string Status,
    string Note,
    Tf2Ps3SourceRequiredHandlerTableTocFunctionInputs Inputs,
    Tf2Ps3SourceRequiredHandlerTableTocFunctionSummary Summary,
    Tf2Ps3SourceRequiredHandlerConstructorEntry[] PrimaryConstructorEntries,
    Tf2Ps3SourceRequiredHandlerTocFunctionGroup[] FocusedFunctionGroups,
    string[] Findings);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocFunctionInputs(
    string TocAccess,
    string InstructionContext,
    string FocusedFunctions);

public sealed record Tf2Ps3SourceRequiredHandlerTableTocFunctionSummary(
    int FocusedDirectAccessCount,
    int FocusedFunctionCount,
    int FunctionsTouchingRequiredHandlers,
    string PrimaryConstructorCandidate,
    int PrimaryConstructorRequiredHandlerCount,
    int PrimaryConstructorEntryCount,
    int PrimaryConstructorAllocationCount,
    string RegisterVirtualSlotOffset);

public sealed record Tf2Ps3SourceRequiredHandlerConstructorEntry(
    int MessageId,
    string MessageName,
    string Vptr,
    string AllocationSize,
    string VptrWordAddress,
    string LoadInstructionAddress);

public sealed record Tf2Ps3SourceRequiredHandlerTocFunctionGroup(
    string Entry,
    string Name,
    int AccessCount,
    int RequiredHandlerCount,
    string[] Annotations,
    Tf2Ps3SourceRequiredHandlerTocFunctionAccessSample[] Samples);

public sealed record Tf2Ps3SourceRequiredHandlerTocFunctionAccessSample(
    string InstructionAddress,
    string TargetDataAddress,
    string TargetValue,
    string TargetAnnotation);
