using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceAssociatedObjectSlot90Reducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "00a578a8",
        "00a578c8",
        "00a57930",
        "00a58380",
        "00a583a0",
        "00a58418",
        "00a5be68",
        "00a5cb60"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceAssociatedObjectSlot90Report> ReduceAsync(
        string cExportPath,
        string playerVtablePath,
        string associatedObjectTokenContractPath,
        string outputPath,
        string registerFunctionsPath = "",
        string callsiteContextPath = "",
        string outputBuilderFunctionsPath = "")
    {
        var allFunctions = ExtractFunctions(await File.ReadAllLinesAsync(cExportPath));
        var functions = allFunctions
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .GroupBy(static function => function.Address, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(function => function.EndLine - function.StartLine).First())
            .OrderBy(static function => function.Address, StringComparer.Ordinal)
            .ToArray();

        using var playerVtableDoc = JsonDocument.Parse(await File.ReadAllTextAsync(playerVtablePath));
        using var tokenContractDoc = JsonDocument.Parse(await File.ReadAllTextAsync(associatedObjectTokenContractPath));

        var slot90 = FindSlot(playerVtableDoc.RootElement, "0x00000090");
        var slotA8 = FindSlot(playerVtableDoc.RootElement, "0x000000a8");
        var slotAc = FindSlot(playerVtableDoc.RootElement, "0x000000ac");
        var slotB4 = FindSlot(playerVtableDoc.RootElement, "0x000000b4");
        var slotC0 = FindSlot(playerVtableDoc.RootElement, "0x000000c0");
        var slotC4 = FindSlot(playerVtableDoc.RootElement, "0x000000c4");
        var tokenSummary = tokenContractDoc.RootElement.GetProperty("Summary");

        var byAddress = functions.ToDictionary(static function => function.Address, StringComparer.Ordinal);
        var slot90Function = byAddress.GetValueOrDefault("00a58418");
        var stateGetter = byAddress.GetValueOrDefault("00a578a8");
        var stateSetter = byAddress.GetValueOrDefault("00a578c8");
        var tokenSetter = byAddress.GetValueOrDefault("00a57930");
        var pendingCounterDec = byAddress.GetValueOrDefault("00a58380");
        var pendingCounterRead = byAddress.GetValueOrDefault("00a583a0");
        var descriptorPredicate = byAddress.GetValueOrDefault("00a5be68");
        var activeStatePredicate = byAddress.GetValueOrDefault("00a5cb60");

        var upstreamRecovered =
            ReadBool(tokenSummary, "AssociatedObjectTokenContractRecovered")
            && ReadBool(tokenSummary, "AssociatedSlot90DispatchRecovered")
            && ReadBool(tokenSummary, "AssociatedLookupRecovered");
        var slot90Recovered =
            slot90.FunctionAddress == "00a58418"
            && slot90Function?.EvidenceTokens.Contains("slot90-reset-buffer-0x44", StringComparer.Ordinal) == true
            && slot90Function.EvidenceTokens.Contains("slot90-delegates-to-slot-ac", StringComparer.Ordinal);
        var stateTripleRecovered =
            slotA8.FunctionAddress == "00a578a8"
            && slotAc.FunctionAddress == "00a578c8"
            && stateGetter?.EvidenceTokens.Contains("state-triple-get-field-0x08", StringComparer.Ordinal) == true
            && stateSetter?.EvidenceTokens.Contains("state-triple-set-field-0x10", StringComparer.Ordinal) == true;
        var descriptorPredicateRecovered =
            slotB4.FunctionAddress == "00a5be68"
            && descriptorPredicate?.EvidenceTokens.Contains("connection-state-helper-0x98", StringComparer.Ordinal) == true
            && descriptorPredicate.EvidenceTokens.Contains("pending-counter-helper-00a583a0", StringComparer.Ordinal)
            && descriptorPredicate.EvidenceTokens.Contains("deadline-field-0xb8", StringComparer.Ordinal);
        var pendingCounterRecovered =
            pendingCounterDec?.EvidenceTokens.Contains("pending-counter-field-0x1e24", StringComparer.Ordinal) == true
            && pendingCounterRead?.EvidenceTokens.Contains("pending-counter-field-0x1e24", StringComparer.Ordinal) == true;
        var connectionPredicatesRecovered =
            slotC0.FunctionAddress == "00a5cb60"
            && activeStatePredicate?.EvidenceTokens.Contains("active-state-count-field-0x2c", StringComparer.Ordinal) == true
            && slotC4.FunctionAddress == "00a57930"
            && tokenSetter?.EvidenceTokens.Contains("association-token-byte-field-0x42c", StringComparer.Ordinal) == true;
        var contractRecovered =
            upstreamRecovered
            && slot90Recovered
            && stateTripleRecovered
            && descriptorPredicateRecovered
            && pendingCounterRecovered
            && connectionPredicatesRecovered;
        var stateContract = BuildStateContract(
            slot90,
            slotA8,
            slotAc,
            slotB4,
            slotC0,
            slotC4,
            contractRecovered);
        var registerContract = await BuildRegisterContractAsync(registerFunctionsPath, callsiteContextPath);
        var directSlotAcCallerCensus = BuildDirectSlotAcCallerCensus(allFunctions);
        var outputBuilderRegisterCensus = await BuildOutputBuilderRegisterCensusAsync(outputBuilderFunctionsPath);

        var gates = new[]
        {
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "upstream-associated-object-token-contract",
                upstreamRecovered ? "proven" : "missing",
                associatedObjectTokenContractPath,
                "008be1e8/008b9ad8 must already prove hard markerless payloads reach associated-object slot +0x90."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "slot90-entry-delegates-to-slot-ac",
                slot90Recovered ? "proven" : "missing",
                "0x0180ca50 -> 00a58418 -> vtable +0xac",
                "The associated +0x90 entry is a small state/reset wrapper, not the complete CLC_Move/usercmd parser."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "slot-ac-state-triple-recovered",
                stateTripleRecovered ? "proven" : "missing",
                "0x0180ca68/+0xa8 getter and 0x0180ca6c/+0xac setter",
                "The delegated slot reads/writes the state triple at object +0x08/+0x0c/+0x10."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "slot-b4-descriptor-predicate-recovered",
                descriptorPredicateRecovered && pendingCounterRecovered ? "proven" : "missing",
                "0x0180ca74 -> 00a5be68 -> 00a583a0 / +0x98 / +0xb8",
                "The lookup descriptor path is a readiness/state predicate over connection state, pending counter, and deadline fields."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "slot-c0-c4-connection-token-predicates-recovered",
                connectionPredicatesRecovered ? "proven" : "missing",
                "0x0180ca80/+0xc0 and 0x0180ca84/+0xc4",
                "The association lookup also depends on active connection state and the byte token field at +0x42c."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "associated-slot90-field-grammar",
                "missing",
                "slot +0xac downstream users and server.dll packet obligations",
                "The state triple is recovered, but the exact payload-object field grammar and downstream usercmd/map-load effect are still incomplete."),
            new Tf2Ps3SourceAssociatedObjectSlot90Gate(
                "native-source-input-ready",
                "missing",
                "requires field grammar plus live/PCAP decoded packets",
                "This report narrows the input boundary; it does not make NativeSourceInputReady true.")
        };

        var report = new Tf2Ps3SourceAssociatedObjectSlot90Report(
            "tf2ps3-source-associated-object-slot90-map",
            "Pins the TF.elf associated-object slot +0x90 path used by hard markerless Source client uploads, and separates proven state predicates from the still-missing field grammar.",
            new Tf2Ps3SourceAssociatedObjectSlot90Inputs(cExportPath, playerVtablePath, associatedObjectTokenContractPath),
            new Tf2Ps3SourceAssociatedObjectSlot90Summary(
                TargetAddresses.Length,
                functions.Length,
                upstreamRecovered,
                slot90Recovered,
                stateTripleRecovered,
                descriptorPredicateRecovered,
                pendingCounterRecovered,
                connectionPredicatesRecovered,
                contractRecovered,
                false,
                gates.Count(static gate => gate.Status is "missing")),
            new Tf2Ps3SourceAssociatedObjectSlot90SlotMap(
                slot90,
                slotA8,
                slotAc,
                slotB4,
                slotC0,
                slotC4),
            stateContract,
            registerContract,
            directSlotAcCallerCensus,
            outputBuilderRegisterCensus,
            functions,
            gates,
            [
                "Hard markerless client uploads are no longer an unexplained encrypted blob at the transport boundary: upstream reports show they enter the associated-object lookup and slot +0x90 dispatch path.",
                "Slot +0x90 resolves to 00a58418, which optionally clears the object +0x44 buffer and delegates to vtable +0xac. The concrete +0xac slot is 00a578c8, a state-triple setter for object +0x08/+0x0c/+0x10.",
                "Slot +0xb4 resolves to 00a5be68, which is a readiness/descriptor predicate over connection state at +0x98, pending counter +0x1e24, and deadline +0xb8. It is not itself CLC_Move decoding.",
                "The object field storage contract is now explicit: +0x08/+0x0c/+0x10 are the delegated slot +0xac state words, +0x44 is the slot +0x90 reset buffer, +0x98/+0xb8/+0x1e24 gate readiness, and +0x42c stores the association token byte.",
                "Instruction-level Ghidra evidence corrects the C-level assumption: 008be1e8 passes the payload object to slot +0x90 in r4 and sets r5=1, but 00a58418 only explicitly sets r4=0 before calling slot +0xac. It does not prepare r5/r6, and its reset helper call can clobber volatile registers.",
                "The direct +0xac caller census separates the associated slot90 zero-state wrapper from source-output message-builder candidates that call +0xac as a one-word state transition. Those candidates need instruction-level object provenance before they can explain the markerless upload grammar.",
                "Focused Ghidra output-builder exports prove source-output builders do call +0xac with an explicit one-word r4 state constant, currently 0xc; this is output/map-load state evidence, not yet client-upload CLC_Move decoding.",
                "The next strict-native target is not locating these fields again; it is proving the payload-object value grammar that feeds them and mapping those values to server.dll usercmd/map-load obligations."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceAssociatedObjectSlot90StateContract BuildStateContract(
        Tf2Ps3SourceAssociatedObjectSlot90Slot slot90,
        Tf2Ps3SourceAssociatedObjectSlot90Slot slotA8,
        Tf2Ps3SourceAssociatedObjectSlot90Slot slotAc,
        Tf2Ps3SourceAssociatedObjectSlot90Slot slotB4,
        Tf2Ps3SourceAssociatedObjectSlot90Slot slotC0,
        Tf2Ps3SourceAssociatedObjectSlot90Slot slotC4,
        bool contractRecovered) =>
        new(
            contractRecovered,
            "00a58418",
            "+0x44",
            slot90.SlotOffset,
            slotA8.SlotOffset,
            slotAc.SlotOffset,
            slotB4.SlotOffset,
            slotC0.SlotOffset,
            slotC4.SlotOffset,
            [
                new Tf2Ps3SourceAssociatedObjectSlot90StateField(
                    "association-state-word-0",
                    "+0x08",
                    "00a578a8",
                    "00a578c8",
                    "00a578a8 reads *(param_1 + 0x08); 00a578c8 writes *(param_1 + 0x08) from slot +0xac argument 1.",
                    "First word of the associated-object state triple written after slot +0x90 delegates to slot +0xac.",
                    "Exact payload-object value source and whether this word selects usercmd, map-load ack, or another Source control role is not proven."),
                new Tf2Ps3SourceAssociatedObjectSlot90StateField(
                    "association-state-word-1",
                    "+0x0c",
                    "00a578a8",
                    "00a578c8",
                    "00a578a8 reads *(param_1 + 0x0c); 00a578c8 writes *(param_1 + 0x0c) from slot +0xac argument 2.",
                    "Second word of the associated-object state triple written after slot +0x90 delegates to slot +0xac.",
                    "Exact payload-object value source and downstream server.dll meaning are not proven."),
                new Tf2Ps3SourceAssociatedObjectSlot90StateField(
                    "association-state-word-2",
                    "+0x10",
                    "00a578a8",
                    "00a578c8",
                    "00a578a8 reads *(param_1 + 0x10); 00a578c8 writes *(param_1 + 0x10) from slot +0xac argument 3.",
                    "Third word of the associated-object state triple written after slot +0x90 delegates to slot +0xac.",
                    "Exact payload-object value source and downstream server.dll meaning are not proven.")
            ],
            [
                "Upstream associated-object token dispatch reaches vtable slot +0x90 for the hard markerless corpus.",
                "Slot +0x90 optionally resets the object buffer at +0x44 and delegates to vtable slot +0xac with r4 explicitly forced to zero.",
                "Slot +0xac writes the three state words at +0x08/+0x0c/+0x10; slot +0xa8 reads the same triple.",
                "Slot +0xb4 is a descriptor/readiness predicate over +0x98, +0xb8, and +0x1e24.",
                "Slot +0xc4 writes the association token byte at +0x42c."
            ],
            [
                "Recover which callers legitimately set the +0xac r5/r6 values and whether the slot +0x90 wrapper is only a zero-state/ack transition.",
                "Promote or reject each direct +0xac source-like caller candidate with Ghidra instruction-level object provenance.",
                "Correlate the +0x08/+0x0c/+0x10 words with hard markerless PCAP/live client uploads.",
                "Map each proven state word combination to server.dll CLC_Move, loading ack, Source control, or ignored keepalive semantics.",
                "Only then promote the markerless client-upload path to NativeSourceInputReady."
            ]);

    private static async Task<Tf2Ps3SourceAssociatedObjectSlot90RegisterContract> BuildRegisterContractAsync(
        string registerFunctionsPath,
        string callsiteContextPath)
    {
        var registerFunctionsAvailable = File.Exists(registerFunctionsPath);
        var callsiteContextAvailable = File.Exists(callsiteContextPath);
        var registerFunctions = registerFunctionsAvailable
            ? JsonDocument.Parse(await File.ReadAllTextAsync(registerFunctionsPath))
            : null;
        var callsiteContext = callsiteContextAvailable
            ? JsonDocument.Parse(await File.ReadAllTextAsync(callsiteContextPath))
            : null;

        try
        {
            var slot90Instructions = registerFunctions?.RootElement.GetProperty("functions").EnumerateArray()
                .FirstOrDefault(static function => ReadString(function, "entry") == "00a58418")
                .GetProperty("instructions").EnumerateArray()
                .Select(static instruction => new InstructionRow(ReadString(instruction, "address"), ReadString(instruction, "text")))
                .ToArray() ?? Array.Empty<InstructionRow>();
            var slotAcInstructions = registerFunctions?.RootElement.GetProperty("functions").EnumerateArray()
                .FirstOrDefault(static function => ReadString(function, "entry") == "00a578c8")
                .GetProperty("instructions").EnumerateArray()
                .Select(static instruction => new InstructionRow(ReadString(instruction, "address"), ReadString(instruction, "text")))
                .ToArray() ?? Array.Empty<InstructionRow>();
            var callsiteInstructions = callsiteContext?.RootElement.GetProperty("addresses").EnumerateArray()
                .Select(static address => new InstructionRow(
                    ReadString(address, "address"),
                    address.TryGetProperty("instruction", out var instruction) && instruction.ValueKind == JsonValueKind.Object
                        ? ReadString(instruction, "text")
                        : ""))
                .Where(static instruction => instruction.Text.Length > 0)
                .ToArray() ?? Array.Empty<InstructionRow>();

            var dispatchRecovered =
                ContainsInstruction(callsiteInstructions, "008be600", "bl 0x008b9ad8")
                && ContainsInstruction(callsiteInstructions, "008be610", "or r4,r30,r30")
                && ContainsInstruction(callsiteInstructions, "008be614", "rldicl r3,r3,0x0,0x20")
                && ContainsInstruction(callsiteInstructions, "008be61c", "li r5,0x1")
                && ContainsInstruction(callsiteInstructions, "008be638", "bctrl");
            var slot90DelegationRecovered =
                ContainsInstruction(slot90Instructions, "00a5844c", "li r4,0x0")
                && ContainsInstruction(slot90Instructions, "00a58450", "lwz r9,0xac(r11)")
                && ContainsInstruction(slot90Instructions, "00a58464", "bctrl");
            var slot90DoesNotSetR5OrR6 =
                !slot90Instructions.Any(static instruction =>
                    IsRegisterWrite(instruction.Text, "r5") || IsRegisterWrite(instruction.Text, "r6"));
            var resetCanClobberVolatileRegisters =
                ContainsInstruction(slot90Instructions, "00a5843c", "bl 0x0086ff38");
            var setterStoresRecovered =
                ContainsInstruction(slotAcInstructions, "00a578cc", "stw r4,0x8(r3)")
                && ContainsInstruction(slotAcInstructions, "00a578d0", "stw r5,0xc(r3)")
                && ContainsInstruction(slotAcInstructions, "00a578c8", "stw r6,0x10(r3)");

            return new Tf2Ps3SourceAssociatedObjectSlot90RegisterContract(
                registerFunctionsPath,
                callsiteContextPath,
                registerFunctionsAvailable,
                callsiteContextAvailable,
                dispatchRecovered,
                slot90DelegationRecovered,
                slot90DoesNotSetR5OrR6,
                resetCanClobberVolatileRegisters,
                setterStoresRecovered,
                [
                    new("008be1e8", "008be610/008be614/008be61c/008be638", "r3=associated object, r4=payload object, r5=1 before slot +0x90 bctrl."),
                    new("00a58418", "00a5844c/00a58450/00a58464", "slot +0x90 wrapper forces r4=0 before slot +0xac bctrl."),
                    new("00a58418", "00a5843c", "optional reset helper call occurs before slot +0xac and may clobber volatile r5/r6."),
                    new("00a578c8", "00a578c8/00a578cc/00a578d0", "slot +0xac setter stores r4 -> +0x08, r5 -> +0x0c, r6 -> +0x10.")
                ],
                [
                    "The associated-object dispatch register contract is instruction-proven for the non--1 payload-object path.",
                    "The slot +0xac setter storage is instruction-proven.",
                    "The C-level idea that slot +0x90 passes three meaningful state words is not proven. In the slot +0x90 wrapper, only r4 is explicitly prepared for slot +0xac.",
                    "The remaining native input work is to find the direct +0xac callers or downstream consumers that give r5/r6 stable semantics, then correlate those states with hard markerless client uploads."
                ]);
        }
        finally
        {
            registerFunctions?.Dispose();
            callsiteContext?.Dispose();
        }
    }

    private static Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCensus BuildDirectSlotAcCallerCensus(
        IReadOnlyList<ExportedFunction> allFunctions)
    {
        var candidates = allFunctions
            .Where(static function => ContainsVirtualSlotAcCall(function.Lines))
            .Select(BuildDirectSlotAcCallerCandidate)
            .Where(static candidate => candidate.EvidenceTokens.Length > 0 && candidate.CallLines.Length > 0)
            .OrderBy(static candidate => candidate.Role, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Address, StringComparer.Ordinal)
            .Take(48)
            .ToArray();
        var sourceLikeCount = candidates.Count(static candidate =>
            candidate.Role.Contains("source", StringComparison.Ordinal)
            || candidate.Role.Contains("associated", StringComparison.Ordinal));

        return new Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCensus(
            allFunctions.Count(static function => ContainsVirtualSlotAcCall(function.Lines)),
            candidates.Length,
            sourceLikeCount,
            candidates,
            [
                "The census is intentionally conservative: it is based on C-export call shapes and source-specific evidence tokens, not enough to prove object provenance by itself.",
                "00a58418 is the associated slot +0x90 wrapper and calls +0xac with an explicit zero state.",
                "The strongest non-wrapper leads are source-output message-builder functions that combine +0xac with +0x70/+0x74/+0x7c writes; those need focused Ghidra callsite exports and instruction-level object provenance before implementation.",
                "Unrelated engine/material/audio virtual +0xac calls remain excluded unless they carry Source object/vector/handler evidence."
            ]);
    }

    private static Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCandidate BuildDirectSlotAcCallerCandidate(
        ExportedFunction function)
    {
        var tokens = new List<string>();
        AddIf(function.Body, tokens, "+ 0x70", "slot-70-message-field-writer");
        AddIf(function.Body, tokens, "+ 0x74", "slot-74-message-field-writer");
        AddIf(function.Body, tokens, "+ 0x7c", "slot-7c-message-finalizer");
        AddIf(function.Body, tokens, "+ 0x98", "slot-98-state-update");
        AddIf(function.Body, tokens, "PTR_PTR_01977dbc", "source-object-vtable-pointer");
        AddIf(function.Body, tokens, "+ 0x1e0c", "source-handler-vector-field");
        AddIf(function.Body, tokens, "+ 0x1e18", "source-handler-vector-count-field");
        AddIf(function.Body, tokens, "+ 0x1e08", "source-owner-callback-field");
        AddIf(function.Body, tokens, "_opd_FUN_00a58418", "associated-slot90-wrapper-reference");
        if (function.Address == "00a58418")
        {
            tokens.Add("associated-slot90-zero-state-wrapper");
        }

        var callLines = function.Lines
            .Where(IsVirtualSlotAcCallLine)
            .Select(static line => line.Trim())
            .Take(8)
            .ToArray();
        if (callLines.Any(static line =>
                line.Contains(",0)", StringComparison.Ordinal)
                || line.Contains(",0xc)", StringComparison.Ordinal)
                || line.Contains(",3)", StringComparison.Ordinal)))
        {
            tokens.Add("one-word-state-transition-call");
        }

        return new Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCandidate(
            function.Address,
            function.Name,
            ClassifyDirectSlotAcCallerRole(function.Address, tokens),
            tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            callLines,
            Preview(function.Lines));
    }

    private static bool ContainsVirtualSlotAcCall(IEnumerable<string> lines) =>
        lines.Any(IsVirtualSlotAcCallLine);

    private static bool IsVirtualSlotAcCallLine(string line) =>
        Regex.IsMatch(line, @"\*\*\(undefined4 \*\*\)\(.*\+ 0xac\)\)");

    private static string ClassifyDirectSlotAcCallerRole(string address, IReadOnlyCollection<string> tokens)
    {
        if (address == "00a58418")
        {
            return "associated-slot90-zero-state-wrapper";
        }

        if (tokens.Contains("source-object-vtable-pointer", StringComparer.Ordinal)
            || tokens.Contains("source-handler-vector-field", StringComparer.Ordinal)
            || tokens.Contains("source-owner-callback-field", StringComparer.Ordinal))
        {
            return "source-object-lifecycle-or-handler-candidate";
        }

        if (tokens.Contains("slot-70-message-field-writer", StringComparer.Ordinal)
            && tokens.Contains("slot-74-message-field-writer", StringComparer.Ordinal)
            && tokens.Contains("slot-7c-message-finalizer", StringComparer.Ordinal))
        {
            return "source-output-message-builder-candidate";
        }

        if (tokens.Contains("one-word-state-transition-call", StringComparer.Ordinal))
        {
            return "one-word-state-transition-candidate";
        }

        return "unclassified-slot-ac-candidate";
    }

    private static async Task<Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderRegisterCensus>
        BuildOutputBuilderRegisterCensusAsync(string outputBuilderFunctionsPath)
    {
        if (!File.Exists(outputBuilderFunctionsPath))
        {
            return new Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderRegisterCensus(
                outputBuilderFunctionsPath,
                false,
                0,
                0,
                0,
                Array.Empty<string>(),
                Array.Empty<Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderCallsite>(),
                ["Run the focused Ghidra export for source-associated-object-slotac-output-builder-functions.json."]);
        }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(outputBuilderFunctionsPath));
        var callsites = new List<Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderCallsite>();
        foreach (var function in doc.RootElement.GetProperty("functions").EnumerateArray())
        {
            var entry = ReadString(function, "entry");
            var name = ReadString(function, "name");
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

                var windowStart = Math.Max(0, i - 8);
                var window = instructions[windowStart..(bctrlIndex + 1)];
                var state = LastRegisterWrite(window, "r4");
                var receiver = LastRegisterWrite(window, "r3");
                var arg5 = LastRegisterWrite(window, "r5");
                callsites.Add(new Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderCallsite(
                    entry,
                    name,
                    instructions[i].Address,
                    instructions[bctrlIndex].Address,
                    receiver,
                    state,
                    TryParseImmediate(state),
                    arg5,
                    window.Select(static instruction => $"{instruction.Address}: {instruction.Text}").ToArray()));
            }
        }

        var constants = callsites
            .Select(static callsite => callsite.StateImmediate)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => Convert.ToInt32(value[2..], 16))
            .ToArray();

        return new Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderRegisterCensus(
            outputBuilderFunctionsPath,
            true,
            doc.RootElement.GetProperty("functions").GetArrayLength(),
            callsites.Count,
            callsites.Count(static callsite => callsite.StateImmediate.Length > 0),
            constants,
            callsites.Take(64).ToArray(),
            [
                "Focused output-builder functions contain concrete +0xac state transitions with explicit r4 constants.",
                "The repeated r4=0xc pattern is paired with slot +0x74/+0x7c message finalization in multiple Source output builders.",
                "These callsites are likely server-output/map-load state emitters. They should be cross-mapped to server.dll obligations before using them for generated map-load traffic.",
                "They do not close NativeSourceInputReady because they do not decode hard markerless client uploads."
            ]);
    }

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

    private static bool ContainsInstruction(IEnumerable<InstructionRow> instructions, string address, string text) =>
        instructions.Any(instruction =>
            instruction.Address.Equals(address, StringComparison.OrdinalIgnoreCase)
            && instruction.Text.Equals(text, StringComparison.Ordinal));

    private static bool IsRegisterWrite(string text, string register) =>
        text.StartsWith("li " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("or " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("addi " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("lwz " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("ld " + register + ",", StringComparison.Ordinal)
        || text.StartsWith("rldicl " + register + ",", StringComparison.Ordinal);

    private static Tf2Ps3SourceAssociatedObjectSlot90Function BuildFunction(ExportedFunction function) =>
        new(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyRole(function.Address),
            ExtractCalls(function.Body),
            BuildEvidenceTokens(function),
            Preview(function.Lines));

    private static string ClassifyRole(string address) =>
        address switch
        {
            "00a578a8" => "associated-slot-a8-state-getter",
            "00a578c8" => "associated-slot-ac-state-setter",
            "00a57930" => "associated-slot-c4-token-byte-setter",
            "00a58380" => "associated-pending-counter-decrement",
            "00a583a0" => "associated-pending-counter-predicate",
            "00a58418" => "associated-slot90-entry-wrapper",
            "00a5be68" => "associated-slot-b4-descriptor-predicate",
            "00a5cb60" => "associated-slot-c0-active-state-predicate",
            _ => "associated-slot90-helper"
        };

    private static string[] BuildEvidenceTokens(ExportedFunction function)
    {
        var body = function.Body;
        var tokens = new List<string>();

        AddIf(body, tokens, "FUN_0086ff38((int)(param_1 + 0x11))", "slot90-reset-buffer-0x44");
        AddIf(body, tokens, "*param_1 + 0xac", "slot90-delegates-to-slot-ac");
        AddIf(body, tokens, "*param_2 = *(undefined4 *)(param_1 + 8)", "state-triple-get-field-0x08");
        AddIf(body, tokens, "*param_3 = *(undefined4 *)(param_1 + 0xc)", "state-triple-get-field-0x0c");
        AddIf(body, tokens, "*param_4 = *(undefined4 *)(param_1 + 0x10)", "state-triple-get-field-0x10");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 8) = param_2", "state-triple-set-field-0x08");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 0xc) = param_3", "state-triple-set-field-0x0c");
        AddIf(body, tokens, "*(undefined4 *)(param_1 + 0x10) = param_4", "state-triple-set-field-0x10");
        AddIf(body, tokens, "param_1 + 0x1e24", "pending-counter-field-0x1e24");
        AddIf(body, tokens, "_opd_FUN_00a583a0(param_1)", "pending-counter-helper-00a583a0");
        AddIf(body, tokens, "param_1 + 0x98", "connection-state-helper-0x98");
        AddIf(body, tokens, "FUN_0086d0a8((int *)(param_1 + 0x98))", "connection-state-ready-read");
        AddIf(body, tokens, "*(double *)(param_1 + 0xb8)", "deadline-field-0xb8");
        AddIf(body, tokens, "param_1 + 0x2c", "active-state-count-field-0x2c");
        AddIf(body, tokens, "param_1 + 0xcc", "active-state-count-field-0xcc");
        AddIf(body, tokens, "param_1 + 0xe0", "active-state-count-field-0xe0");
        AddIf(body, tokens, "param_1 + 0x42c", "association-token-byte-field-0x42c");

        foreach (var token in new[]
        {
            "FUN_0086d0a8",
            "FUN_0086ff38",
            "_opd_FUN_00a583a0",
            "_opd_FUN_00a5be68"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                tokens.Add(token);
            }
        }

        return tokens.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static Tf2Ps3SourceAssociatedObjectSlot90Slot FindSlot(JsonElement root, string slotOffset)
    {
        foreach (var slot in root.GetProperty("Slots").EnumerateArray())
        {
            if (ReadString(slot, "SlotOffset").Equals(slotOffset, StringComparison.OrdinalIgnoreCase))
            {
                return new Tf2Ps3SourceAssociatedObjectSlot90Slot(
                    ReadInt(slot, "SlotIndex"),
                    ReadString(slot, "SlotOffset"),
                    ReadString(slot, "TableAddress"),
                    ReadString(slot, "OpdAddress"),
                    ReadString(slot, "FunctionAddress"),
                    ReadString(slot, "Role"));
            }
        }

        throw new InvalidOperationException($"Slot {slotOffset} not found.");
    }

    private static void AddIf(string body, List<string> tokens, string needle, string token)
    {
        if (body.Contains(needle, StringComparison.Ordinal))
        {
            tokens.Add(token);
        }
    }

    private static string[] ExtractCalls(string body)
    {
        var matches = Regex.Matches(body, @"(?<name>(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto)\s*\(");
        return matches
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static int ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static ExportedFunction[] ExtractFunctions(string[] lines)
    {
        var starts = new List<(int Index, string Name, string Address)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
                continue;
            }

            match = SplitFunctionDefinitionRegex.Match(lines[i]);
            if (match.Success)
            {
                starts.Add((i, match.Groups["name"].Value, match.Groups["address"].Value));
            }
        }

        var functions = new List<ExportedFunction>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].Index - 1 : lines.Length - 1;
            functions.Add(BuildExportedFunction(lines, start.Index, end, start.Name, start.Address));
        }

        return functions.ToArray();
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(90));
        return text.Length <= 3000 ? text : text[..3000];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);

    private sealed record InstructionRow(string Address, string Text);
}

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Report(
    string Status,
    string Note,
    Tf2Ps3SourceAssociatedObjectSlot90Inputs Inputs,
    Tf2Ps3SourceAssociatedObjectSlot90Summary Summary,
    Tf2Ps3SourceAssociatedObjectSlot90SlotMap SlotMap,
    Tf2Ps3SourceAssociatedObjectSlot90StateContract StateContract,
    Tf2Ps3SourceAssociatedObjectSlot90RegisterContract RegisterContract,
    Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCensus DirectSlotAcCallerCensus,
    Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderRegisterCensus OutputBuilderRegisterCensus,
    Tf2Ps3SourceAssociatedObjectSlot90Function[] Functions,
    Tf2Ps3SourceAssociatedObjectSlot90Gate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Inputs(
    string CExport,
    string PlayerVtableMap,
    string AssociatedObjectTokenContract);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Summary(
    int TargetFunctionCount,
    int LocatedFunctionCount,
    bool UpstreamAssociatedObjectTokenContractRecovered,
    bool Slot90EntryRecovered,
    bool SlotAcStateTripleRecovered,
    bool SlotB4DescriptorPredicateRecovered,
    bool PendingCounterRecovered,
    bool ConnectionTokenPredicatesRecovered,
    bool AssociatedObjectSlot90ContractRecovered,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90SlotMap(
    Tf2Ps3SourceAssociatedObjectSlot90Slot Slot90,
    Tf2Ps3SourceAssociatedObjectSlot90Slot SlotA8,
    Tf2Ps3SourceAssociatedObjectSlot90Slot SlotAc,
    Tf2Ps3SourceAssociatedObjectSlot90Slot SlotB4,
    Tf2Ps3SourceAssociatedObjectSlot90Slot SlotC0,
    Tf2Ps3SourceAssociatedObjectSlot90Slot SlotC4);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Slot(
    int SlotIndex,
    string SlotOffset,
    string TableAddress,
    string OpdAddress,
    string FunctionAddress,
    string Role);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90StateContract(
    bool StorageContractRecovered,
    string Slot90EntryFunction,
    string Slot90ResetBufferOffset,
    string Slot90Offset,
    string GetterSlotOffset,
    string SetterSlotOffset,
    string DescriptorPredicateSlotOffset,
    string ActiveStatePredicateSlotOffset,
    string TokenSetterSlotOffset,
    Tf2Ps3SourceAssociatedObjectSlot90StateField[] Fields,
    string[] ProvenSemantics,
    string[] MissingSemantics);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90StateField(
    string Name,
    string ObjectOffset,
    string GetterFunction,
    string SetterFunction,
    string Evidence,
    string NativeMeaning,
    string RemainingQuestion);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90RegisterContract(
    string RegisterFunctionsPath,
    string CallsiteContextPath,
    bool RegisterFunctionsAvailable,
    bool CallsiteContextAvailable,
    bool AssociatedSlot90DispatchRegistersRecovered,
    bool Slot90DelegatesToSlotAcRegistersRecovered,
    bool Slot90DoesNotExplicitlySetR5OrR6,
    bool Slot90ResetCanClobberVolatileRegisters,
    bool SlotAcSetterStoresRecovered,
    Tf2Ps3SourceAssociatedObjectSlot90RegisterEvidence[] Evidence,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90RegisterEvidence(
    string Function,
    string InstructionAddresses,
    string Meaning);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCensus(
    int VirtualSlotAcFunctionCount,
    int CandidateCount,
    int SourceLikeCandidateCount,
    Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCandidate[] Candidates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90DirectSlotAcCallerCandidate(
    string Address,
    string Name,
    string Role,
    string[] EvidenceTokens,
    string[] CallLines,
    string Preview);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderRegisterCensus(
    string OutputBuilderFunctionsPath,
    bool OutputBuilderFunctionsAvailable,
    int FunctionCount,
    int SlotAcCallsiteCount,
    int ConstantStateCallsiteCount,
    string[] StateConstants,
    Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderCallsite[] Callsites,
    string[] Conclusions);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90OutputBuilderCallsite(
    string Function,
    string Name,
    string SlotLoadAddress,
    string CallAddress,
    string ReceiverRegisterWrite,
    string StateRegisterWrite,
    string StateImmediate,
    string Arg5RegisterWrite,
    string[] InstructionWindow);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Function(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceAssociatedObjectSlot90Gate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
