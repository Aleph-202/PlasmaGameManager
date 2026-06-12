using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMessageVtableCatalogReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly (uint Offset, string Role)[] INetMessageSlots =
    [
        (0x00, "destructor0"),
        (0x04, "destructor1"),
        (0x08, "SetNetChannel"),
        (0x0c, "SetReliable"),
        (0x10, "Process"),
        (0x14, "ReadFromBuffer"),
        (0x18, "WriteToBuffer"),
        (0x1c, "IsReliable"),
        (0x20, "GetType"),
        (0x24, "GetGroup"),
        (0x28, "GetName"),
        (0x2c, "GetNetChannel"),
        (0x30, "ToString")
    ];

    public static async Task ReduceAsync(string elfPath, string messageCatalogPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        using var catalogDocument = JsonDocument.Parse(await File.ReadAllTextAsync(messageCatalogPath));
        var messages = catalogDocument.RootElement.GetProperty("Messages").EnumerateArray()
            .Select(message => BuildMessage(image, message))
            .ToArray();

        var report = new Tf2Ps3SourceMessageVtableCatalogReport(
            "tf2ps3-source-message-vtable-catalog",
            "Resolves TF.elf Source message RTTI/type-name anchors into candidate INetMessage vptrs by scanning references to the RTTI object word immediately before the type-name pointer. Slot functions are OPD-decoded when possible; this is structural vtable evidence, not yet full Read/Write field semantics.",
            new Tf2Ps3SourceMessageVtableCatalogInputs(elfPath, messageCatalogPath),
            new Tf2Ps3SourceMessageVtableCatalogSummary(
                messages.Length,
                messages.Count(static message => message.PresentInTfElf),
                messages.Count(static message => message.CandidateVtables.Length == 1),
                messages.Count(static message => message.CandidateVtables.Length == 0 && message.PresentInTfElf),
                messages.Count(static message => message.CandidateVtables.Length > 1),
                messages.Sum(static message => message.CandidateVtables.Length),
                messages.Sum(static message => message.CandidateVtables.Sum(static table => table.Slots.Count(static slot => slot.OpdResolved))),
                messages.Count(static message => message.ImplementationPriority is "critical-client-join" or "critical-server-map-load"
                    && message.CandidateVtables.Length == 1)),
            messages,
            BuildFindings(messages));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceMessageVtableCatalogMessage BuildMessage(Elf64BigEndianImage image, JsonElement message)
    {
        var className = ReadString(message, "ClassName");
        var family = ReadString(message, "Family");
        var messageId = message.GetProperty("SourceMessageId").GetInt32();
        var priority = ReadString(message, "ImplementationPriority");
        var typeInfoPresent = message.GetProperty("TypeInfoPresent").GetBoolean();
        var networkNamePresent = message.GetProperty("NetworkNamePresent").GetBoolean();
        var typeInfoReferenceAddress = FirstReferenceAddress(message, "TypeInfoReferences");
        var rttiObjectAddress = typeInfoPresent && typeInfoReferenceAddress >= 4
            ? typeInfoReferenceAddress - 4
            : 0;
        var candidateVtables = rttiObjectAddress == 0
            ? []
            : image.FindU32References(rttiObjectAddress)
                .Select(reference => BuildVtable(image, reference, className))
                .ToArray();

        return new Tf2Ps3SourceMessageVtableCatalogMessage(
            className,
            family,
            messageId,
            priority,
            typeInfoPresent && networkNamePresent,
            Hex(typeInfoReferenceAddress),
            rttiObjectAddress == 0 ? "" : Hex(rttiObjectAddress),
            ReadString(message, "TypeInfoString"),
            ReadString(message, "NetworkNameString"),
            candidateVtables,
            ClassifyConfidence(typeInfoPresent, networkNamePresent, candidateVtables));
    }

    private static Tf2Ps3SourceMessageVtableCandidate BuildVtable(Elf64BigEndianImage image, uint rttiReferenceAddress, string className)
    {
        var vptr = rttiReferenceAddress + 4;
        var slots = INetMessageSlots
            .Select(slot => BuildSlot(image, vptr + slot.Offset, slot.Offset, slot.Role))
            .ToArray();
        var evidence = new List<string>
        {
            $"vptr[-1] at {Hex(rttiReferenceAddress)} references RTTI object for {className}",
            $"candidate object vptr starts at {Hex(vptr)}"
        };

        if (className == "CLC_Move" && vptr == 0x017fdc30)
        {
            evidence.Add("matches exact CLC_Move vptr recovered in source-clc-move-contract.json");
        }

        var unresolvedSlots = slots.Count(static slot => !slot.OpdResolved);
        if (unresolvedSlots == 0)
        {
            evidence.Add("all expected INetMessage slots resolve through PS3 OPD descriptors");
        }
        else
        {
            evidence.Add($"{unresolvedSlots} expected INetMessage slots did not resolve through an executable OPD entry");
        }

        return new Tf2Ps3SourceMessageVtableCandidate(
            Hex(rttiReferenceAddress),
            Hex(vptr),
            slots,
            evidence.ToArray());
    }

    private static Tf2Ps3SourceMessageVtableSlot BuildSlot(
        Elf64BigEndianImage image,
        uint slotAddress,
        uint slotOffset,
        string role)
    {
        if (!image.TryReadU32(slotAddress, out var opdAddress))
        {
            return new Tf2Ps3SourceMessageVtableSlot(
                Hex(slotAddress),
                Hex(slotOffset),
                role,
                "",
                "",
                "unreadable",
                false);
        }

        var opdResolved = TryResolveOpdEntry(image, opdAddress, out var entryAddress);
        return new Tf2Ps3SourceMessageVtableSlot(
            Hex(slotAddress),
            Hex(slotOffset),
            role,
            Hex(opdAddress),
            opdResolved ? Hex(entryAddress) : "",
            image.AnnotatePointer(opdAddress),
            opdResolved);
    }

    private static bool TryResolveOpdEntry(Elf64BigEndianImage image, uint opdAddress, out uint entryAddress)
    {
        if (image.TryReadU32(opdAddress, out entryAddress) && image.IsExecutableAddress(entryAddress))
        {
            return true;
        }

        entryAddress = 0;
        return false;
    }

    private static string ClassifyConfidence(
        bool typeInfoPresent,
        bool networkNamePresent,
        Tf2Ps3SourceMessageVtableCandidate[] candidates)
    {
        if (!typeInfoPresent || !networkNamePresent)
        {
            return "absent-message";
        }

        return candidates.Length switch
        {
            0 => "missing-vtable-reference",
            1 => candidates[0].Slots.All(static slot => slot.OpdResolved) ? "single-vtable-structural" : "single-vtable-partial-slots",
            _ => "multiple-vtable-candidates"
        };
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceMessageVtableCatalogMessage> messages)
    {
        var critical = messages
            .Where(static message => message.ImplementationPriority is "critical-client-join" or "critical-server-map-load")
            .ToArray();
        var missingCritical = critical
            .Where(static message => message.CandidateVtables.Length != 1)
            .Select(static message => message.ClassName)
            .ToArray();
        var missingText = missingCritical.Length == 0
            ? "none"
            : string.Join(", ", missingCritical);
        return
        [
            "The TF.elf Source message RTTI pattern is consistent: message type-name pointers live in 12-byte RTTI records, and candidate INetMessage vptrs are found by references to the word immediately before each type-name pointer.",
            $"Critical join/map-load messages without exactly one structural vtable candidate: {missingText}.",
            "This report identifies the exact virtual slots to decompile next. GetType/ReadFromBuffer/WriteToBuffer slots are now addressable for the native server contract instead of being inferred from PC Source alone.",
            "Do not treat this report as field-layout proof. Field semantics still require per-message decompile reduction, as already done for CLC_Move."
        ];
    }

    private static uint FirstReferenceAddress(JsonElement message, string propertyName)
    {
        var refs = message.GetProperty(propertyName).EnumerateArray().ToArray();
        return refs.Length == 0 ? 0 : ParseHex(ReadString(refs[0], "Address"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? ""
                : "";
    }

    private static uint ParseHex(string value)
    {
        value = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return value.Length == 0 ? 0 : Convert.ToUInt32(value, 16);
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }
}

public sealed record Tf2Ps3SourceMessageVtableCatalogReport(
    string Status,
    string Note,
    Tf2Ps3SourceMessageVtableCatalogInputs Inputs,
    Tf2Ps3SourceMessageVtableCatalogSummary Summary,
    Tf2Ps3SourceMessageVtableCatalogMessage[] Messages,
    string[] Findings);

public sealed record Tf2Ps3SourceMessageVtableCatalogInputs(
    string ElfInput,
    string MessageCatalogInput);

public sealed record Tf2Ps3SourceMessageVtableCatalogSummary(
    int MessageCount,
    int PresentMessageCount,
    int SingleVtableCandidateMessageCount,
    int MissingVtableCandidateMessageCount,
    int MultipleVtableCandidateMessageCount,
    int CandidateVtableCount,
    int ResolvedOpdSlotCount,
    int CriticalMessageSingleVtableCount);

public sealed record Tf2Ps3SourceMessageVtableCatalogMessage(
    string ClassName,
    string Family,
    int SourceMessageId,
    string ImplementationPriority,
    bool PresentInTfElf,
    string TypeInfoReferenceAddress,
    string RttiObjectAddress,
    string TypeInfoString,
    string NetworkNameString,
    Tf2Ps3SourceMessageVtableCandidate[] CandidateVtables,
    string Confidence);

public sealed record Tf2Ps3SourceMessageVtableCandidate(
    string RttiReferenceAddress,
    string ObjectVptr,
    Tf2Ps3SourceMessageVtableSlot[] Slots,
    string[] Evidence);

public sealed record Tf2Ps3SourceMessageVtableSlot(
    string SlotAddress,
    string SlotOffset,
    string Role,
    string OpdAddress,
    string EntryAddress,
    string OpdAnnotation,
    bool OpdResolved);
