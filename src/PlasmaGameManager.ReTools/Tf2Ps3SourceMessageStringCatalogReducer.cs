using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceMessageStringCatalogReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly MessageAnchorSpec[] Specs =
    [
        new("NET_Tick", "net", 3, 0x1005a5f8, 0x1005a648, true),
        new("NET_StringCmd", "net", 4, 0x1005a608, 0x1005a638, true),
        new("NET_SetConVar", "net", 5, 0x1005a618, 0x1005a628, true),
        new("NET_SignonState", "net", 6, 0x1005a5e0, 0x1005a658, true),

        new("SVC_Print", "server", 7, 0x1005a538, 0x1005a6f0, true),
        new("SVC_ServerInfo", "server", 8, 0x1005a520, 0x1005a700, true),
        new("SVC_SendTable", "server", 9, 0x1005a510, 0x1005a710, true),
        new("SVC_ClassInfo", "server", 10, 0x1005a500, 0x1005a720, true),
        new("SVC_SetPause", "server", 11, 0x1005a4f0, 0x1005a730, true),
        new("SVC_CreateStringTable", "server", 12, 0x1005a4d8, 0x1005a740, true),
        new("SVC_UpdateStringTable", "server", 13, 0x1005a4c0, 0x1005a758, true),
        new("SVC_VoiceInit", "server", 14, 0x1005a4b0, 0x1005a770, true),
        new("SVC_VoiceData", "server", 15, 0x1005a4a0, 0x1005a780, true),
        new("SVC_Sounds", "server", 17, 0x1005a490, 0x1005a790, true),
        new("SVC_SetView", "server", 18, 0x1005a470, 0x1005a7b0, true),
        new("SVC_FixAngle", "server", 19, 0x1005a460, 0x1005a7c0, true),
        new("SVC_CrosshairAngle", "server", 20, 0x1005a448, 0x1005a7d0, true),
        new("SVC_BSPDecal", "server", 21, 0x1005a438, 0x1005a7e8, true),
        new("SVC_UserMessage", "server", 23, 0x1005a410, 0x1005a808, true),
        new("SVC_EntityMessage", "server", 24, 0x1005a3f8, 0x1005a818, true),
        new("SVC_GameEvent", "server", 25, 0x1005a428, 0x1005a7f8, true),
        new("SVC_PacketEntities", "server", 26, 0x1005a3e0, 0x1005a830, true),
        new("SVC_TempEntities", "server", 27, 0x1005a3c8, 0x1005a848, true),
        new("SVC_Prefetch", "server", 28, 0x1005a480, 0x1005a7a0, true),
        new("SVC_Menu", "server", 29, 0x1005a3b8, 0x1005a860, true),
        new("SVC_GameEventList", "server", 30, 0x1005a3a0, 0x1005a870, true),
        new("SVC_GetCvarValue", "server", 31, 0x1005a308, 0x1005a900, true),
        new("SVC_CmdKeyValues", "server", 32, null, null, true),
        new("SVC_SetPauseTimed", "server", 33, null, null, true),

        new("CLC_ClientInfo", "client", 8, 0x1005a5c8, 0x1005a668, true),
        new("CLC_Move", "client", 9, 0x1005a5b8, 0x1005a678, true),
        new("CLC_VoiceData", "client", 10, 0x1005a5a8, 0x1005a688, true),
        new("CLC_BaselineAck", "client", 11, 0x1005a590, 0x1005a698, true),
        new("CLC_ListenEvents", "client", 12, 0x1005a578, 0x1005a6a8, true),
        new("CLC_RespondCvarValue", "client", 13, 0x1005a560, 0x1005a6c0, true),
        new("CLC_FileCRCCheck", "client", 14, 0x1005a548, 0x1005a6d8, true),
        new("CLC_SaveReplay", "client", 15, null, null, true),
        new("CLC_CmdKeyValues", "client", 16, null, null, true),
        new("CLC_FileMD5Check", "client", 17, null, null, true)
    ];

    public static async Task ReduceAsync(string elfPath, string outputPath)
    {
        var image = Elf64BigEndianImage.Load(elfPath);
        var messages = Specs
            .Select(spec => BuildMessage(image, spec))
            .ToArray();
        var report = new Tf2Ps3SourceMessageStringCatalogReport(
            "tf2ps3-source-message-string-catalog",
            "Catalogs TF.elf Source NET/CLC/SVC message typeinfo and lowercase network-name string anchors. This is string/reference evidence only; vtable and constructor proofs remain separate reports.",
            elfPath,
            new Tf2Ps3SourceMessageStringCatalogSummary(
                messages.Length,
                messages.Count(static message => message.TypeInfoPresent),
                messages.Count(static message => message.NetworkNamePresent),
                messages.Count(static message => message.ExpectedInLocalSourceButAbsentInTfElf),
                messages.Count(static message => message.Family == "client" && message.TypeInfoPresent && message.NetworkNamePresent),
                messages.Count(static message => message.Family == "server" && message.TypeInfoPresent && message.NetworkNamePresent),
                messages.Sum(static message => message.TypeInfoReferences.Length + message.NetworkNameReferences.Length)),
            messages,
            BuildFindings(messages));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceMessageStringCatalogEntry BuildMessage(
        Elf64BigEndianImage image,
        MessageAnchorSpec spec)
    {
        var typeInfo = spec.TypeInfoAddress is { } typeInfoAddress
            ? BuildString(image, typeInfoAddress)
            : Tf2Ps3SourceMessageStringEvidence.Absent;
        var networkName = spec.NetworkNameAddress is { } networkNameAddress
            ? BuildString(image, networkNameAddress)
            : Tf2Ps3SourceMessageStringEvidence.Absent;

        return new Tf2Ps3SourceMessageStringCatalogEntry(
            spec.ClassName,
            spec.Family,
            spec.SourceMessageId,
            typeInfo.Address,
            typeInfo.Value,
            typeInfo.Present,
            typeInfo.References,
            networkName.Address,
            networkName.Value,
            networkName.Present,
            networkName.References,
            spec.ExpectedInLocalSource && (!typeInfo.Present || !networkName.Present),
            ClassifyImplementationPriority(spec, typeInfo.Present, networkName.Present));
    }

    private static Tf2Ps3SourceMessageStringEvidence BuildString(Elf64BigEndianImage image, uint address)
    {
        var present = image.TryReadAsciiString(address, 128, out var value);
        var refs = present
            ? image.FindU32References(address)
                .Select(reference => new Tf2Ps3SourceMessageStringReference(
                    Hex(reference),
                    BuildWindow(image, reference)))
                .ToArray()
            : [];
        return new Tf2Ps3SourceMessageStringEvidence(Hex(address), value, present, refs);
    }

    private static Tf2Ps3SourceMessageStringWindowWord[] BuildWindow(Elf64BigEndianImage image, uint reference)
    {
        var start = (reference >= 0x30 ? reference - 0x30 : 0) & 0xffff_fffc;
        var words = new List<Tf2Ps3SourceMessageStringWindowWord>();
        for (var address = start; address < start + 0x70; address += 4)
        {
            if (!image.TryReadU32(address, out var value))
            {
                continue;
            }

            image.TryReadAsciiString(value, 80, out var pointedString);
            words.Add(new Tf2Ps3SourceMessageStringWindowWord(
                Hex(address),
                Hex(value),
                image.AnnotatePointer(value),
                pointedString,
                address == reference));
        }

        return words.ToArray();
    }

    private static string ClassifyImplementationPriority(
        MessageAnchorSpec spec,
        bool typeInfoPresent,
        bool networkNamePresent)
    {
        if (!typeInfoPresent || !networkNamePresent)
        {
            return "absent-from-this-tf-elf";
        }

        if (spec.ClassName is "CLC_ClientInfo" or "CLC_Move" or "CLC_BaselineAck")
        {
            return "critical-client-join";
        }

        if (spec.ClassName is "SVC_ServerInfo" or "SVC_SendTable" or "SVC_ClassInfo" or "SVC_CreateStringTable"
            or "SVC_UpdateStringTable" or "SVC_PacketEntities")
        {
            return "critical-server-map-load";
        }

        return spec.Family switch
        {
            "client" => "client-message",
            "server" => "server-message",
            _ => "net-message"
        };
    }

    private static string[] BuildFindings(IReadOnlyCollection<Tf2Ps3SourceMessageStringCatalogEntry> messages)
    {
        var absent = messages
            .Where(static message => message.ExpectedInLocalSourceButAbsentInTfElf)
            .Select(static message => message.ClassName)
            .ToArray();
        var absentText = absent.Length == 0
            ? "none"
            : string.Join(", ", absent);
        return
        [
            "TF.elf contains concrete string anchors for the old Source NET/CLC/SVC message families, including the client join-critical CLC_ClientInfo, CLC_Move, and CLC_BaselineAck names and the server map-load-critical SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, string-table, and PacketEntities names.",
            $"The Source-era messages absent from the scanned PS3 string table are: {absentText}.",
            "String presence is not enough to implement a handler. The next proof target is resolving each present string's references into exact INetMessage vtables, then mapping each vtable's GetType, ReadFromBuffer/WriteToBuffer, GetName, and ToString slots.",
            "The catalog deliberately preserves absent FileMD5/CmdKeyValues/SaveReplay-style entries so newer PC Source assumptions do not leak into the PS3 native server."
        ];
    }

    private static string Hex(uint value)
    {
        return "0x" + value.ToString("x8");
    }

    private sealed record MessageAnchorSpec(
        string ClassName,
        string Family,
        int SourceMessageId,
        uint? TypeInfoAddress,
        uint? NetworkNameAddress,
        bool ExpectedInLocalSource);

    private sealed record Tf2Ps3SourceMessageStringEvidence(
        string Address,
        string Value,
        bool Present,
        Tf2Ps3SourceMessageStringReference[] References)
    {
        public static readonly Tf2Ps3SourceMessageStringEvidence Absent = new("", "", false, []);
    }
}

public sealed record Tf2Ps3SourceMessageStringCatalogReport(
    string Status,
    string Note,
    string ElfInput,
    Tf2Ps3SourceMessageStringCatalogSummary Summary,
    Tf2Ps3SourceMessageStringCatalogEntry[] Messages,
    string[] Findings);

public sealed record Tf2Ps3SourceMessageStringCatalogSummary(
    int MessageCount,
    int TypeInfoPresentCount,
    int NetworkNamePresentCount,
    int ExpectedInLocalSourceButAbsentInTfElfCount,
    int PresentClientMessageCount,
    int PresentServerMessageCount,
    int RawReferenceCount);

public sealed record Tf2Ps3SourceMessageStringCatalogEntry(
    string ClassName,
    string Family,
    int SourceMessageId,
    string TypeInfoAddress,
    string TypeInfoString,
    bool TypeInfoPresent,
    Tf2Ps3SourceMessageStringReference[] TypeInfoReferences,
    string NetworkNameAddress,
    string NetworkNameString,
    bool NetworkNamePresent,
    Tf2Ps3SourceMessageStringReference[] NetworkNameReferences,
    bool ExpectedInLocalSourceButAbsentInTfElf,
    string ImplementationPriority);

public sealed record Tf2Ps3SourceMessageStringReference(
    string Address,
    Tf2Ps3SourceMessageStringWindowWord[] Window);

public sealed record Tf2Ps3SourceMessageStringWindowWord(
    string Address,
    string Value,
    string Annotation,
    string PointedString,
    bool IsReferenceWord);
