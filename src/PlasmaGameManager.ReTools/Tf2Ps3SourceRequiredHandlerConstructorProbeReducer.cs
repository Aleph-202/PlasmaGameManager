using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceRequiredHandlerConstructorProbeReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceRequiredHandlerConstructorProbeReport> ReduceAsync(
        string elfPath,
        string cExportPath,
        string messageVtableCatalogPath,
        string netchanRegistrationSetupPath,
        string requiredClientReadContractPath,
        string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var cExport = await File.ReadAllTextAsync(cExportPath);
        using var catalogDocument = JsonDocument.Parse(await File.ReadAllTextAsync(messageVtableCatalogPath));
        using var setupDocument = JsonDocument.Parse(await File.ReadAllTextAsync(netchanRegistrationSetupPath));
        using var readContractDocument = JsonDocument.Parse(await File.ReadAllTextAsync(requiredClientReadContractPath));

        var catalog = catalogDocument.RootElement.GetProperty("Messages")
            .EnumerateArray()
            .ToDictionary(static message => ReadString(message, "ClassName"), StringComparer.Ordinal);
        var readContracts = readContractDocument.RootElement.GetProperty("Contracts")
            .EnumerateArray()
            .ToDictionary(static contract => ReadString(contract, "ClassName"), StringComparer.Ordinal);
        var requiredHandlers = setupDocument.RootElement.GetProperty("RequiredHandlers")
            .EnumerateArray()
            .Where(static handler => ReadBool(handler, "PresentInTfElf"))
            .Select(handler => BuildHandlerProbe(image, cExport, handler, catalog, readContracts))
            .OrderBy(static handler => handler.MessageId)
            .ThenBy(static handler => handler.ClassName, StringComparer.Ordinal)
            .ToArray();
        var vptrRuns = BuildRequiredObjectVptrRuns(requiredHandlers);
        if (vptrRuns.Length > 0)
        {
            var classesInRuns = vptrRuns
                .SelectMany(static run => run.Entries.Select(static entry => entry.ClassName))
                .ToHashSet(StringComparer.Ordinal);
            requiredHandlers = requiredHandlers
                .Select(handler => classesInRuns.Contains(handler.ClassName)
                    && handler.ConstructorLeadStatus == "writable-vptr-reference-candidate"
                        ? handler with { ConstructorLeadStatus = "contiguous-required-vptr-table" }
                        : handler)
                .ToArray();
        }

        var handlersWithVptrCodeLoads = requiredHandlers.Count(static handler =>
            handler.Targets.Any(static target => target.Role == "object-vptr" && target.ExecutablePpcAddressLoadCandidateCount > 0));
        var handlersWithWritableVptrRefs = requiredHandlers.Count(static handler =>
            handler.Targets.Any(static target => target.Role == "object-vptr" && target.WritableU32ReferenceCount > 0));
        var handlersWithAnyConstructorLead = requiredHandlers.Count(static handler => handler.ConstructorLeadStatus != "no-constructor-lead-found");
        var handlersWithReadContracts = requiredHandlers.Count(static handler => handler.ReadContractStatus == "read-field-reduced");

        var report = new Tf2Ps3SourceRequiredHandlerConstructorProbeReport(
            "tf2ps3-source-required-handler-constructor-probe",
            "Scans TF.elf for direct writable references, executable PowerPC address-load candidates, and C-export mentions of required Source client handler vtables. This targets the remaining native gap between proven CNetChan registration mechanics and concrete handler object construction.",
            new Tf2Ps3SourceRequiredHandlerConstructorProbeInputs(
                elfPath,
                cExportPath,
                messageVtableCatalogPath,
                netchanRegistrationSetupPath,
                requiredClientReadContractPath),
            new Tf2Ps3SourceRequiredHandlerConstructorProbeSummary(
                requiredHandlers.Length,
                handlersWithReadContracts,
                handlersWithWritableVptrRefs,
                handlersWithVptrCodeLoads,
                handlersWithAnyConstructorLead,
                requiredHandlers.Length - handlersWithAnyConstructorLead,
                vptrRuns.Length,
                vptrRuns.Length == 0 ? 0 : vptrRuns.Max(static run => run.HandlerCount),
                requiredHandlers.Sum(static handler => handler.Targets.Sum(static target => target.WritableU32ReferenceCount)),
                requiredHandlers.Sum(static handler => handler.Targets.Sum(static target => target.AllLoadedU32ReferenceCount)),
                requiredHandlers.Sum(static handler => handler.Targets.Sum(static target => target.ExecutablePpcAddressLoadCandidateCount))),
            vptrRuns,
            requiredHandlers,
            BuildFindings(requiredHandlers, vptrRuns),
            BuildNextTargets(requiredHandlers));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceRequiredHandlerVptrReferenceRun[] BuildRequiredObjectVptrRuns(
        Tf2Ps3SourceRequiredHandlerConstructorProbeRow[] handlers)
    {
        if (handlers.Length == 0)
        {
            return [];
        }

        var rows = handlers.OrderBy(static handler => handler.MessageId).ToArray();
        var firstRefs = ObjectVptrWritableRefs(rows[0]).ToArray();
        var runs = new List<Tf2Ps3SourceRequiredHandlerVptrReferenceRun>();
        foreach (var baseAddress in firstRefs)
        {
            var entries = new List<Tf2Ps3SourceRequiredHandlerVptrReferenceRunEntry>();
            var ok = true;
            for (var i = 0; i < rows.Length; i++)
            {
                var expectedAddress = baseAddress + (uint)(i * 4);
                var refs = ObjectVptrWritableRefs(rows[i]).ToHashSet();
                if (!refs.Contains(expectedAddress))
                {
                    ok = false;
                    break;
                }

                entries.Add(new Tf2Ps3SourceRequiredHandlerVptrReferenceRunEntry(
                    Hex(expectedAddress),
                    rows[i].ClassName,
                    rows[i].MessageId,
                    rows[i].ObjectVptr));
            }

            if (!ok)
            {
                continue;
            }

            runs.Add(new Tf2Ps3SourceRequiredHandlerVptrReferenceRun(
                Hex(baseAddress),
                Hex(baseAddress + (uint)((rows.Length - 1) * 4)),
                rows.Length,
                rows.Select(static row => row.MessageId).ToArray(),
                rows.Select(static row => row.ClassName).ToArray(),
                entries.ToArray(),
                [
                    "one writable 4-byte word per required present handler",
                    "table order matches required handler order sorted by message id",
                    "table values are concrete INetMessage object vptrs"
                ]));
        }

        return runs
            .OrderBy(static run => run.BaseAddress, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<uint> ObjectVptrWritableRefs(Tf2Ps3SourceRequiredHandlerConstructorProbeRow row)
    {
        return row.Targets
            .Where(static target => target.Role == "object-vptr")
            .SelectMany(static target => target.WritableU32ReferenceSample)
            .Select(static address => TryParseHex(address, out var parsed) ? parsed : 0)
            .Where(static address => address != 0);
    }

    private static Tf2Ps3SourceRequiredHandlerConstructorProbeRow BuildHandlerProbe(
        Elf64BigEndianImage image,
        string cExport,
        JsonElement requiredHandler,
        IReadOnlyDictionary<string, JsonElement> catalog,
        IReadOnlyDictionary<string, JsonElement> readContracts)
    {
        var className = ReadString(requiredHandler, "ClassName");
        catalog.TryGetValue(className, out var message);
        readContracts.TryGetValue(className, out var readContract);

        var vtable = message.ValueKind == JsonValueKind.Object
            ? message.GetProperty("CandidateVtables").EnumerateArray().FirstOrDefault()
            : default;
        var slots = vtable.ValueKind == JsonValueKind.Object
            ? vtable.GetProperty("Slots").EnumerateArray().ToArray()
            : [];

        var targets = new List<Tf2Ps3SourceRequiredHandlerProbeTarget>();
        AddTarget(targets, image, cExport, "object-vptr", ReadString(vtable, "ObjectVptr"), "Concrete INetMessage object vptr; constructors normally install this value into handler objects.");
        AddTarget(targets, image, cExport, "rtti-reference-word", ReadString(vtable, "RttiReferenceAddress"), "The vptr[-1] word address immediately before the object vtable.");
        AddTarget(targets, image, cExport, "rtti-object", ReadString(message, "RttiObjectAddress"), "RTTI object referenced by the vptr[-1] word.");
        AddTarget(targets, image, cExport, "typeinfo-reference", ReadString(message, "TypeInfoReferenceAddress"), "RTTI record/string reference address used to recover the class.");

        foreach (var role in new[] { "Process", "ReadFromBuffer", "WriteToBuffer", "GetType", "GetName", "ToString" })
        {
            var slot = slots.FirstOrDefault(slot => ReadString(slot, "Role") == role);
            AddTarget(targets, image, cExport, $"slot-{role.ToLowerInvariant()}-opd", ReadString(slot, "OpdAddress"), $"{role} OPD pointer from the handler vtable.");
            AddTarget(targets, image, cExport, $"slot-{role.ToLowerInvariant()}-entry", ReadString(slot, "EntryAddress"), $"{role} executable entry from the handler vtable.");
        }

        var objectVptr = targets.FirstOrDefault(static target => target.Role == "object-vptr");
        var leadStatus = objectVptr is not null && objectVptr.ExecutablePpcAddressLoadCandidateCount > 0
            ? "executable-vptr-load-candidate"
            : objectVptr is not null && objectVptr.WritableU32ReferenceCount > 0
                ? "writable-vptr-reference-candidate"
                : targets.Any(static target => target.ExecutablePpcAddressLoadCandidateCount > 0)
                    ? "non-vptr-executable-address-load-candidate"
                    : "no-constructor-lead-found";

        return new Tf2Ps3SourceRequiredHandlerConstructorProbeRow(
            className,
            ReadString(requiredHandler, "Family"),
            ReadInt(requiredHandler, "MessageId"),
            ReadString(requiredHandler, "NetworkName"),
            ReadString(readContract, "ContractStatus").Length > 0 ? ReadString(readContract, "ContractStatus") : ReadString(requiredHandler, "ContractStatus"),
            readContract.ValueKind == JsonValueKind.Object ? ReadInt(readContract, "FieldCount") : ReadInt(requiredHandler, "FieldCount"),
            ReadString(vtable, "ObjectVptr"),
            ReadString(vtable, "RttiReferenceAddress"),
            ReadString(message, "RttiObjectAddress"),
            leadStatus,
            targets.ToArray());
    }

    private static void AddTarget(
        List<Tf2Ps3SourceRequiredHandlerProbeTarget> targets,
        Elf64BigEndianImage image,
        string cExport,
        string role,
        string addressText,
        string purpose)
    {
        if (!TryParseHex(addressText, out var address))
        {
            return;
        }

        var writableRefs = image.FindU32References(address);
        var allRefs = image.FindU32ReferencesInLoadedSegments(address);
        var executableLoads = image.FindPpcAddressLoadCandidates(address)
            .Take(24)
            .Select(static candidate => new Tf2Ps3SourceRequiredHandlerPpcLoadCandidate(
                Hex(candidate.LisAddress),
                Hex(candidate.LowAddress),
                candidate.Kind,
                candidate.HighImmediateKind,
                candidate.SourceRegister,
                candidate.TargetRegister,
                (int)candidate.InstructionDistance))
            .ToArray();
        var cMentions = CountAddressMentions(cExport, address);

        targets.Add(new Tf2Ps3SourceRequiredHandlerProbeTarget(
            role,
            Hex(address),
            purpose,
            writableRefs.Length,
            allRefs.Length,
            executableLoads.Length,
            cMentions,
            writableRefs.Take(24).Select(Hex).ToArray(),
            allRefs.Except(writableRefs).Take(24).Select(Hex).ToArray(),
            executableLoads,
            BuildTargetEvidence(role, writableRefs.Length, allRefs.Length, executableLoads.Length, cMentions)));
    }

    private static string[] BuildTargetEvidence(
        string role,
        int writableRefCount,
        int allLoadedRefCount,
        int ppcLoadCount,
        int cMentionCount)
    {
        var evidence = new List<string>();
        if (writableRefCount > 0)
        {
            evidence.Add("writable-u32-reference");
        }

        if (allLoadedRefCount > writableRefCount)
        {
            evidence.Add("non-writable-loaded-u32-reference");
        }

        if (ppcLoadCount > 0)
        {
            evidence.Add("executable-ppc-address-load-candidate");
        }

        if (cMentionCount > 0)
        {
            evidence.Add("ghidra-c-export-address-mention");
        }

        if (evidence.Count == 0)
        {
            evidence.Add(role == "object-vptr"
                ? "no-simple-vptr-constructor-reference"
                : "no-simple-reference");
        }

        return evidence.ToArray();
    }

    private static string[] BuildFindings(
        Tf2Ps3SourceRequiredHandlerConstructorProbeRow[] handlers,
        Tf2Ps3SourceRequiredHandlerVptrReferenceRun[] vptrRuns)
    {
        var findings = new List<string>
        {
            $"Required present handler probes: {handlers.Length}.",
            $"Handlers with implementation-ready read contracts in the companion report: {handlers.Count(static handler => handler.ReadContractStatus == "read-field-reduced")}.",
            $"Handlers with direct object-vptr PPC address-load candidates: {handlers.Count(static handler => handler.Targets.Any(static target => target.Role == "object-vptr" && target.ExecutablePpcAddressLoadCandidateCount > 0))}.",
            $"Handlers with direct writable object-vptr references: {handlers.Count(static handler => handler.Targets.Any(static target => target.Role == "object-vptr" && target.WritableU32ReferenceCount > 0))}.",
            vptrRuns.Length > 0
                ? $"Recovered {vptrRuns.Length} contiguous required-handler object-vptr table run(s); longest run has {vptrRuns.Max(static run => run.HandlerCount)} handlers."
                : "No contiguous required-handler object-vptr table run was recovered."
        };

        var objectVptrLeads = handlers
            .Where(static handler => handler.Targets.Any(static target => target.Role == "object-vptr" && target.ExecutablePpcAddressLoadCandidateCount > 0))
            .Select(static handler => $"{handler.ClassName}/id{handler.MessageId}")
            .ToArray();
        findings.Add(objectVptrLeads.Length > 0
            ? $"Direct vptr constructor/code-load leads exist for: {string.Join(", ", objectVptrLeads)}."
            : "No direct object-vptr PPC address-load candidates were found for the required handlers; this argues against simple in-place constructors in the scanned executable text.");

        var fallbackLeads = handlers
            .Where(static handler => handler.ConstructorLeadStatus == "non-vptr-executable-address-load-candidate")
            .Select(static handler => $"{handler.ClassName}/id{handler.MessageId}")
            .ToArray();
        if (fallbackLeads.Length > 0)
        {
            findings.Add($"Non-vptr slot/typeinfo address-load leads remain for: {string.Join(", ", fallbackLeads)}. These need decompile inspection before treating them as constructor evidence.");
        }

        if (vptrRuns.Length > 0)
        {
            var run = vptrRuns[0];
            findings.Add($"The strongest constructor/registration lead is the contiguous required vptr table at {run.BaseAddress}..{run.EndAddress}: {string.Join(", ", run.ClassNames)}.");
        }

        if (handlers.All(static handler => handler.ConstructorLeadStatus == "no-constructor-lead-found"))
        {
            findings.Add("The handler constructor gap is not explained by direct data xrefs or simple PowerPC address materialization; the next target should be table-driven registration through helper slot +0x44 or allocator callbacks already recovered around the Source object lifecycle.");
        }

        return findings.ToArray();
    }

    private static string[] BuildNextTargets(Tf2Ps3SourceRequiredHandlerConstructorProbeRow[] handlers)
    {
        var targets = new List<string>();
        foreach (var handler in handlers.Where(static handler => handler.ConstructorLeadStatus is not "no-constructor-lead-found" and not "contiguous-required-vptr-table"))
        {
            var firstLead = handler.Targets
                .SelectMany(static target => target.ExecutablePpcAddressLoadCandidates.Select(candidate => (target.Role, candidate.LisAddress)))
                .FirstOrDefault();
            if (firstLead.LisAddress is not null)
            {
                targets.Add($"Decompile the function containing {firstLead.LisAddress} for {handler.ClassName} ({firstLead.Role}) and verify whether it installs/registers the handler object.");
            }
        }

        targets.Add("If the executable leads are weak/noisy, continue from the proven Source object lifecycle path (008b9c38 -> 00a5e058 -> 00a5d0c0) and helper slot +0x44/00a5df70 to locate indirect handler allocation tables.");
        targets.Add("Decompile references around the contiguous required-handler object-vptr table to determine whether it is a static prototype array, a registration template list, or an allocator seed table.");
        targets.Add("Promote any confirmed handler constructor to runtime parser registration only after its object vptr, message id, and ReadFromBuffer route agree with source-required-client-read-contract.json.");
        return targets.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountAddressMentions(string cExport, uint address)
    {
        var lower = cExport.ToLowerInvariant();
        var hex = address.ToString("x8");
        return CountOccurrences(lower, hex)
            + CountOccurrences(lower, "0x" + hex)
            + CountOccurrences(lower, "dat_" + hex)
            + CountOccurrences(lower, "ptr_" + hex);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static bool TryParseHex(string value, out uint result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out result);
    }

    private static string Hex(uint value) => "0x" + value.ToString("x8");

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

public sealed record Tf2Ps3SourceRequiredHandlerConstructorProbeReport(
    string Status,
    string Note,
    Tf2Ps3SourceRequiredHandlerConstructorProbeInputs Inputs,
    Tf2Ps3SourceRequiredHandlerConstructorProbeSummary Summary,
    Tf2Ps3SourceRequiredHandlerVptrReferenceRun[] ContiguousRequiredObjectVptrRuns,
    Tf2Ps3SourceRequiredHandlerConstructorProbeRow[] RequiredHandlers,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Tf2Ps3SourceRequiredHandlerConstructorProbeInputs(
    string Elf,
    string CExport,
    string SourceMessageVtableCatalog,
    string NetchanRegistrationSetup,
    string RequiredClientReadContract);

public sealed record Tf2Ps3SourceRequiredHandlerConstructorProbeSummary(
    int RequiredPresentHandlerCount,
    int RequiredHandlersWithReadContracts,
    int HandlersWithWritableObjectVptrReferences,
    int HandlersWithExecutableObjectVptrLoadCandidates,
    int HandlersWithAnyConstructorLead,
    int HandlersWithNoConstructorLead,
    int ContiguousRequiredObjectVptrRunCount,
    int LongestContiguousRequiredObjectVptrRunLength,
    int TotalWritableU32ReferenceCount,
    int TotalLoadedU32ReferenceCount,
    int TotalExecutablePpcAddressLoadCandidateCount);

public sealed record Tf2Ps3SourceRequiredHandlerConstructorProbeRow(
    string ClassName,
    string Family,
    int MessageId,
    string NetworkName,
    string ReadContractStatus,
    int ReadContractFieldCount,
    string ObjectVptr,
    string RttiReferenceAddress,
    string RttiObjectAddress,
    string ConstructorLeadStatus,
    Tf2Ps3SourceRequiredHandlerProbeTarget[] Targets);

public sealed record Tf2Ps3SourceRequiredHandlerVptrReferenceRun(
    string BaseAddress,
    string EndAddress,
    int HandlerCount,
    int[] MessageIds,
    string[] ClassNames,
    Tf2Ps3SourceRequiredHandlerVptrReferenceRunEntry[] Entries,
    string[] Evidence);

public sealed record Tf2Ps3SourceRequiredHandlerVptrReferenceRunEntry(
    string ReferenceAddress,
    string ClassName,
    int MessageId,
    string ObjectVptr);

public sealed record Tf2Ps3SourceRequiredHandlerProbeTarget(
    string Role,
    string Address,
    string Purpose,
    int WritableU32ReferenceCount,
    int AllLoadedU32ReferenceCount,
    int ExecutablePpcAddressLoadCandidateCount,
    int CExportMentionCount,
    string[] WritableU32ReferenceSample,
    string[] NonWritableLoadedU32ReferenceSample,
    Tf2Ps3SourceRequiredHandlerPpcLoadCandidate[] ExecutablePpcAddressLoadCandidates,
    string[] Evidence);

public sealed record Tf2Ps3SourceRequiredHandlerPpcLoadCandidate(
    string LisAddress,
    string LowAddress,
    string Kind,
    string HighImmediateKind,
    int SourceRegister,
    int TargetRegister,
    int InstructionDistance);
