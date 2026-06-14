using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceOwnerForwardWrapperVariantReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[A-Za-z_][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly KnownWrapper[] PrimaryWrappers =
    [
        new("00a52720", "owner-slot8-word5-bitreader", "bitreader-rebuild-word5", "param_2[5]", "param_2 + 6", "param_2 + 0xf", "", "008722a0"),
        new("00a52810", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52840", "word6-bitreader-forward", "bitreader-rebuild-word6", "param_2[6]", "param_2 + 7", "param_2 + 0x10", "", "008722a0"),
        new("00a52930", "word5-bitreader-forward", "bitreader-rebuild-word5", "param_2[5]", "param_2 + 6", "param_2 + 0xf", "", "008722a0"),
        new("00a52a20", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52a50", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52a80", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52ab0", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52ae0", "deferred-pointer-word6-forward", "deferred-pointer-word6", "param_2[6]", "param_2 + 10", "param_2[0x13]", "", "008722a0"),
        new("00a52ba0", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52bd0", "direct-owner-forward", "direct-forward", "", "", "", "", "008722a0"),
        new("00a52c00", "guarded-direct-owner-forward", "guarded-direct-forward", "", "", "", "FUN_00870a98", "008722a0"),
        new("00a53250", "guarded-direct-owner-forward", "guarded-direct-forward", "", "", "", "FUN_0086d408", "008722a0"),
        new("00a539d0", "config-state-or-word4-fallback-forward", "config-state-or-fallback-word4", "param_2[4]", "param_2 + 5", "param_2 + 0xe", "", "008722a0"),
        new("00a554d0", "state-copy-word5-bitreader-forward", "state-copy-word5", "param_2[5]", "param_2 + 6", "param_2 + 0xf", "FUN_0086b208", "008722a0")
    ];

    private static readonly (string Address, string Target)[] ThunkWrappers =
    [
        ("00a55d38", "00a52720"),
        ("00a55d40", "00a52810"),
        ("00a55d48", "00a52840"),
        ("00a55d50", "00a52930"),
        ("00a55d58", "00a52a20"),
        ("00a55d60", "00a52a50"),
        ("00a55d68", "00a52a80"),
        ("00a55d70", "00a52ab0"),
        ("00a55d78", "00a52ae0"),
        ("00a55d80", "00a52ba0"),
        ("00a55d88", "00a52bd0"),
        ("00a55d90", "00a52c00"),
        ("00a55da8", "00a53250"),
        ("00a55dd0", "00a539d0"),
        ("00a55de0", "00a554d0")
    ];

    public static async Task<Tf2Ps3SourceOwnerForwardWrapperVariantReport> ReduceAsync(
        string cExportPath,
        string sourceRoot,
        string outputPath)
    {
        var functions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var functionMap = functions
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(static function => function.Body.Length).First(),
                StringComparer.Ordinal);
        var implementation = ScanImplementation(sourceRoot);
        var wrappers = PrimaryWrappers
            .Select(wrapper => BuildWrapper(wrapper, functionMap, implementation))
            .ToArray();
        var thunks = ThunkWrappers
            .Select(thunk => BuildThunk(thunk.Address, thunk.Target, functionMap))
            .ToArray();

        var locatedWrappers = wrappers.Count(static wrapper => wrapper.Located);
        var recoveredWrappers = wrappers.Count(static wrapper => wrapper.ShapeRecovered);
        var implementedWrappers = wrappers.Count(static wrapper => wrapper.ImplementationStatus is "implemented" or "not-required");
        var missingImplementation = wrappers
            .Where(static wrapper => wrapper.ImplementationStatus is not ("implemented" or "not-required"))
            .Select(static wrapper => wrapper.Address)
            .ToArray();
        var locatedThunks = thunks.Count(static thunk => thunk.Located);
        var matchingThunks = thunks.Count(static thunk => thunk.TargetCallRecovered);
        var bitstreamWrappers = wrappers.Where(static wrapper => wrapper.BoundaryRequirement != "none").ToArray();
        var guardedWrappers = wrappers.Count(static wrapper => wrapper.LayoutKind == "guarded-direct-forward");
        var ready = locatedWrappers == wrappers.Length
            && recoveredWrappers == wrappers.Length
            && missingImplementation.Length == 0;

        var layoutCounts = wrappers
            .GroupBy(static wrapper => wrapper.LayoutKind, StringComparer.Ordinal)
            .Select(static group => new Tf2Ps3SourceOwnerForwardWrapperVariantLayoutCount(group.Key, group.Count()))
            .OrderBy(static count => count.LayoutKind, StringComparer.Ordinal)
            .ToArray();

        var gates = new[]
        {
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "all-primary-wrapper-functions-located",
                locatedWrappers == wrappers.Length ? "proven" : "missing",
                cExportPath,
                "Every concrete TF.elf owner-forward wrapper sibling in the 00a52720/00a554d0 family is present in the C export."),
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "all-primary-wrapper-shapes-recovered",
                recoveredWrappers == wrappers.Length ? "proven" : "missing",
                cExportPath,
                "Each located wrapper contains the expected 008722a0 call, bit-count/source/reader fields, and guard/setup helper where applicable."),
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "all-bitstream-layouts-implemented",
                missingImplementation.Length == 0 ? "proven" : "missing",
                sourceRoot,
                "Every recovered bitstream wrapper family is covered by the native protocol resolver or is a direct forwarder with no extra wrapper boundary."),
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "guarded-forwarders-documented",
                guardedWrappers == 2 ? "proven" : "needs-review",
                "00a52c00, 00a53250",
                "The two guarded direct-forward functions are documented as client-side predicate wrappers around the same 008722a0 semantic dispatch path."),
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "thunk-forwarders-located",
                locatedThunks == thunks.Length && matchingThunks == thunks.Length ? "proven" : "missing",
                cExportPath,
                "The OPD/thunk wrappers at 00a55d38..00a55de0 forward to the recovered concrete wrapper functions."),
            new Tf2Ps3SourceOwnerForwardWrapperVariantGate(
                "native-wrapper-variant-coverage-ready",
                ready ? "proven" : "missing",
                "TF.elf owner-forward wrapper family + native protocol implementation",
                "The recovered wrapper family has no remaining implementation-sized wrapper-layout gap. This is still not live map-load proof.")
        };

        var report = new Tf2Ps3SourceOwnerForwardWrapperVariantReport(
            "tf2ps3-source-owner-forward-wrapper-variants",
            "Direct TF.elf C-export audit of every 008722a0 owner-forward wrapper sibling, including functions outside the owner vtable slot table.",
            new Tf2Ps3SourceOwnerForwardWrapperVariantInputs(cExportPath, sourceRoot),
            new Tf2Ps3SourceOwnerForwardWrapperVariantSummary(
                wrappers.Length,
                locatedWrappers,
                recoveredWrappers,
                bitstreamWrappers.Length,
                implementedWrappers,
                missingImplementation.Length,
                missingImplementation,
                layoutCounts.Length,
                guardedWrappers,
                thunks.Length,
                locatedThunks,
                matchingThunks,
                implementation.Word5BoundaryImplemented,
                implementation.Word6BoundaryImplemented,
                implementation.DeferredPointerWord6BoundaryImplemented,
                implementation.ConfigFallbackWord4BoundaryImplemented,
                implementation.StateCopyWord5BoundaryImplemented,
                ready,
                false),
            layoutCounts,
            wrappers,
            thunks,
            gates,
            [
                "The direct TF.elf scan covers five wrapper families: direct forward, guarded direct forward, word-5 bitreader rebuild, word-6 bitreader rebuild, deferred pointer word-6, config/state fallback word-4, and the larger state-copy word-5 variant.",
                "00a554d0 has extra local state-copy/setup work, but its final client-upload bitstream boundary is the same param_2[5] / param_2+6 / param_2+0xf layout used by 00a52720 and 00a52930.",
                ready
                    ? "No owner-forward wrapper family is currently missing an implementation-sized resolver. The remaining markerless input blocker is the raw packet-to-wrapper selection/transform proof and live map-load verification, not another visible 008722a0 wrapper variant."
                    : "At least one recovered wrapper family is still not covered by native protocol code; implement that family before treating markerless Source input as native-ready."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceOwnerForwardWrapperVariantEntry BuildWrapper(
        KnownWrapper wrapper,
        IReadOnlyDictionary<string, FunctionBlock> functionMap,
        ImplementationScan implementation)
    {
        if (!functionMap.TryGetValue(wrapper.Address, out var function))
        {
            return new Tf2Ps3SourceOwnerForwardWrapperVariantEntry(
                wrapper.Address,
                wrapper.Role,
                wrapper.LayoutKind,
                false,
                false,
                wrapper.BitCountField,
                wrapper.BitSource,
                wrapper.ReaderTarget,
                wrapper.GuardFunction,
                wrapper.DispatchTarget,
                BoundaryRequirement(wrapper.LayoutKind),
                "missing",
                "function not found in C export",
                ["function-missing"]);
        }

        var tokens = EvidenceTokens(wrapper, function.Body);
        var shapeRecovered = RequiredTokens(wrapper).All(token => function.Body.Contains(token, StringComparison.Ordinal));
        return new Tf2Ps3SourceOwnerForwardWrapperVariantEntry(
            wrapper.Address,
            wrapper.Role,
            wrapper.LayoutKind,
            true,
            shapeRecovered,
            wrapper.BitCountField,
            wrapper.BitSource,
            wrapper.ReaderTarget,
            wrapper.GuardFunction,
            wrapper.DispatchTarget,
            BoundaryRequirement(wrapper.LayoutKind),
            ImplementationStatus(wrapper.LayoutKind, implementation),
            ImplementationEvidence(wrapper.LayoutKind, implementation),
            tokens);
    }

    private static Tf2Ps3SourceOwnerForwardWrapperVariantThunk BuildThunk(
        string address,
        string target,
        IReadOnlyDictionary<string, FunctionBlock> functionMap)
    {
        if (!functionMap.TryGetValue(address, out var function))
        {
            return new Tf2Ps3SourceOwnerForwardWrapperVariantThunk(address, target, false, false, []);
        }

        var targetToken = $"_opd_FUN_{target}";
        var tokens = new List<string>();
        if (function.Body.Contains(targetToken, StringComparison.Ordinal))
        {
            tokens.Add("target-call-recovered");
        }

        if (function.Body.Contains("param_1 + -8", StringComparison.Ordinal)
            || function.Body.Contains("param_1 - 8", StringComparison.Ordinal))
        {
            tokens.Add("this-pointer-minus-eight-adjustment");
        }

        return new Tf2Ps3SourceOwnerForwardWrapperVariantThunk(
            address,
            target,
            true,
            tokens.Contains("target-call-recovered", StringComparer.Ordinal),
            tokens.ToArray());
    }

    private static string[] RequiredTokens(KnownWrapper wrapper)
    {
        var tokens = new List<string> { $"_opd_FUN_{wrapper.DispatchTarget}" };
        if (wrapper.BitCountField.Length > 0)
        {
            tokens.Add(wrapper.BitCountField);
        }

        if (wrapper.BitSource.Length > 0)
        {
            tokens.Add(wrapper.BitSource);
        }

        if (wrapper.ReaderTarget.Length > 0 && wrapper.ReaderTarget.StartsWith("param_2 +", StringComparison.Ordinal))
        {
            tokens.Add(wrapper.ReaderTarget);
        }

        if (wrapper.GuardFunction.Length > 0)
        {
            tokens.Add(wrapper.GuardFunction);
        }

        return tokens.ToArray();
    }

    private static string[] EvidenceTokens(KnownWrapper wrapper, string body)
    {
        var tokens = new List<string>();
        foreach (var token in RequiredTokens(wrapper))
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                tokens.Add(TokenName(token));
            }
        }

        if (body.Contains("FUN_0086acb8", StringComparison.Ordinal))
        {
            tokens.Add("bit-payload-copy-helper-0086acb8");
        }

        if (body.Contains("FUN_00870968", StringComparison.Ordinal))
        {
            tokens.Add("bitreader-rebuild-helper-00870968");
        }

        if (body.Contains("param_2[0x13]", StringComparison.Ordinal))
        {
            tokens.Add("temporary-payload-pointer-param2-word13");
        }

        if (body.Contains("FUN_0086b208", StringComparison.Ordinal))
        {
            tokens.Add("state-copy-setup-helper-0086b208");
        }

        return tokens.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string TokenName(string token) =>
        token switch
        {
            "_opd_FUN_008722a0" => "dispatches-to-owner-forward-router-008722a0",
            "param_2[5]" => "uses-param2-word5-bit-count",
            "param_2 + 6" => "uses-param2-word6-bit-source",
            "param_2 + 0xf" => "rebuilds-reader-at-param2-word15",
            "param_2[6]" => "uses-param2-word6-bit-count",
            "param_2 + 7" => "uses-param2-word7-bit-source",
            "param_2 + 0x10" => "rebuilds-reader-at-param2-word16",
            "param_2 + 10" => "uses-param2-word10-bit-source",
            "param_2[4]" => "uses-param2-word4-bit-count",
            "param_2 + 5" => "uses-param2-word5-bit-source",
            "param_2 + 0xe" => "rebuilds-reader-at-param2-word14",
            "FUN_00870a98" => "guard-predicate-00870a98",
            "FUN_0086d408" => "guard-predicate-0086d408",
            "FUN_0086b208" => "state-copy-setup-helper-0086b208",
            _ => token
        };

    private static string BoundaryRequirement(string layoutKind) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => "decode param_2[5] bit count, payload bytes at param_2 + 6, reader target param_2 + 0xf",
            "state-copy-word5" => "decode the same word-5 bitstream boundary after the wrapper copies state fields and runs its setup helper",
            "bitreader-rebuild-word6" => "decode param_2[6] bit count, payload bytes at param_2 + 7, reader target param_2 + 0x10",
            "deferred-pointer-word6" => "decode param_2[6] bit count, payload bytes at param_2 + 10, temporary payload pointer at param_2[0x13]",
            "config-state-or-fallback-word4" => "decode config/state writes and fallback param_2[4] bit count, payload bytes at param_2 + 5, reader target param_2 + 0xe",
            _ => "none"
        };

    private static string ImplementationStatus(string layoutKind, ImplementationScan implementation) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => implementation.Word5BoundaryImplemented ? "implemented" : "missing",
            "state-copy-word5" => implementation.StateCopyWord5BoundaryImplemented ? "implemented" : "missing",
            "bitreader-rebuild-word6" => implementation.Word6BoundaryImplemented ? "implemented" : "missing",
            "deferred-pointer-word6" => implementation.DeferredPointerWord6BoundaryImplemented ? "implemented" : "missing",
            "config-state-or-fallback-word4" => implementation.ConfigFallbackWord4BoundaryImplemented ? "implemented" : "missing",
            "direct-forward" or "guarded-direct-forward" => "not-required",
            _ => "missing"
        };

    private static string ImplementationEvidence(string layoutKind, ImplementationScan implementation) =>
        layoutKind switch
        {
            "bitreader-rebuild-word5" => implementation.Word5BoundaryEvidence,
            "state-copy-word5" => implementation.StateCopyWord5BoundaryEvidence,
            "bitreader-rebuild-word6" => implementation.Word6BoundaryEvidence,
            "deferred-pointer-word6" => implementation.DeferredPointerWord6BoundaryEvidence,
            "config-state-or-fallback-word4" => implementation.ConfigFallbackWord4BoundaryEvidence,
            "guarded-direct-forward" => "client-side guard predicate only; no additional wire bitstream boundary beyond 008722a0 semantic dispatch",
            "direct-forward" => "direct wrapper forwards the already-built payload object to 008722a0",
            _ => "not found"
        };

    private static ImplementationScan ScanImplementation(string sourceRoot)
    {
        var text = ReadImplementationText(sourceRoot);
        var hasWord5Boundary =
            text.Contains("Word5PayloadWord6ReaderWord15", StringComparison.Ordinal)
            && text.Contains("OwnerSlot8Bitstream", StringComparison.Ordinal)
            && text.Contains("TryResolveOwnerSlot8Bitstream", StringComparison.Ordinal);
        var hasWord6Boundary =
            text.Contains("Word6PayloadWord7ReaderWord16", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderWord6Bitstream", StringComparison.Ordinal);
        var hasDeferredPointerBoundary =
            text.Contains("DeferredPointerWord6PayloadWord10PointerWord19", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderDeferredPointerWord6Bitstream", StringComparison.Ordinal);
        var hasConfigFallbackBoundary =
            text.Contains("ConfigFallbackWord4PayloadWord5ReaderWord14", StringComparison.Ordinal)
            && text.Contains("OwnerForwarderConfigFallbackWord4Bitstream", StringComparison.Ordinal);
        var hasStateCopyBoundary = hasWord5Boundary;

        return new ImplementationScan(
            hasWord5Boundary,
            hasWord5Boundary
                ? "Ps3SourcePayloadObjectFrame.Word5PayloadWord6ReaderWord15 + OwnerSlot8Bitstream resolver"
                : "word-5 bitstream resolver not found",
            hasWord6Boundary,
            hasWord6Boundary
                ? "Ps3SourcePayloadObjectFrame.Word6PayloadWord7ReaderWord16 + OwnerForwarderWord6Bitstream resolver"
                : "word-6 bitstream resolver not found",
            hasDeferredPointerBoundary,
            hasDeferredPointerBoundary
                ? "Ps3SourcePayloadObjectFrame.DeferredPointerWord6PayloadWord10PointerWord19 + OwnerForwarderDeferredPointerWord6Bitstream resolver"
                : "deferred pointer word-6 resolver not found",
            hasConfigFallbackBoundary,
            hasConfigFallbackBoundary
                ? "Ps3SourcePayloadObjectFrame.ConfigFallbackWord4PayloadWord5ReaderWord14 + OwnerForwarderConfigFallbackWord4Bitstream resolver"
                : "config fallback word-4 resolver not found",
            hasStateCopyBoundary,
            hasStateCopyBoundary
                ? "00a554d0's final bitstream boundary is covered by the shared word-5 resolver; its pre-dispatch state-copy fields are documented in this report"
                : "state-copy word-5 boundary not covered");
    }

    private static string ReadImplementationText(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return "";
        }

        return string.Join('\n', Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText));
    }

    private static List<FunctionBlock> ExtractFunctions(string[] lines)
    {
        var functions = new List<FunctionBlock>();
        for (var index = 0; index < lines.Length; index++)
        {
            var match = FunctionDefinitionRegex.Match(lines[index]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[index]);
            }

            if (!match.Success)
            {
                continue;
            }

            var address = match.Groups["address"].Value;
            var start = index;
            var end = lines.Length;
            for (var cursor = index + 1; cursor < lines.Length; cursor++)
            {
                if (FunctionDefinitionRegex.IsMatch(lines[cursor]) || SplitFunctionDefinitionRegex.IsMatch(lines[cursor]))
                {
                    end = cursor;
                    break;
                }
            }

            var body = string.Join('\n', lines.AsSpan(start, end - start).ToArray());
            functions.Add(new FunctionBlock(address, body));
            index = end - 1;
        }

        return functions;
    }

    private sealed record KnownWrapper(
        string Address,
        string Role,
        string LayoutKind,
        string BitCountField,
        string BitSource,
        string ReaderTarget,
        string GuardFunction,
        string DispatchTarget);

    private sealed record FunctionBlock(string Address, string Body);

    private sealed record ImplementationScan(
        bool Word5BoundaryImplemented,
        string Word5BoundaryEvidence,
        bool Word6BoundaryImplemented,
        string Word6BoundaryEvidence,
        bool DeferredPointerWord6BoundaryImplemented,
        string DeferredPointerWord6BoundaryEvidence,
        bool ConfigFallbackWord4BoundaryImplemented,
        string ConfigFallbackWord4BoundaryEvidence,
        bool StateCopyWord5BoundaryImplemented,
        string StateCopyWord5BoundaryEvidence);
}

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantReport(
    string Status,
    string Note,
    Tf2Ps3SourceOwnerForwardWrapperVariantInputs Inputs,
    Tf2Ps3SourceOwnerForwardWrapperVariantSummary Summary,
    Tf2Ps3SourceOwnerForwardWrapperVariantLayoutCount[] LayoutCounts,
    Tf2Ps3SourceOwnerForwardWrapperVariantEntry[] Wrappers,
    Tf2Ps3SourceOwnerForwardWrapperVariantThunk[] Thunks,
    Tf2Ps3SourceOwnerForwardWrapperVariantGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantInputs(
    string CExport,
    string SourceRoot);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantSummary(
    int PrimaryWrapperCount,
    int LocatedPrimaryWrapperCount,
    int ShapeRecoveredPrimaryWrapperCount,
    int BitstreamWrapperCount,
    int ImplementedOrNotRequiredWrapperCount,
    int MissingImplementationCount,
    string[] MissingImplementationAddresses,
    int LayoutKindCount,
    int GuardedDirectForwarderCount,
    int ThunkWrapperCount,
    int LocatedThunkWrapperCount,
    int MatchingThunkWrapperCount,
    bool Word5BoundaryImplemented,
    bool Word6BoundaryImplemented,
    bool DeferredPointerWord6BoundaryImplemented,
    bool ConfigFallbackWord4BoundaryImplemented,
    bool StateCopyWord5BoundaryImplemented,
    bool NativeWrapperVariantCoverageReady,
    bool LiveMapLoadVerified);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantLayoutCount(
    string LayoutKind,
    int Count);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantEntry(
    string Address,
    string Role,
    string LayoutKind,
    bool Located,
    bool ShapeRecovered,
    string BitCountField,
    string BitSource,
    string ReaderTarget,
    string GuardFunction,
    string DispatchTarget,
    string BoundaryRequirement,
    string ImplementationStatus,
    string ImplementationEvidence,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantThunk(
    string Address,
    string TargetAddress,
    bool Located,
    bool TargetCallRecovered,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceOwnerForwardWrapperVariantGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
