using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceNativeMessageContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, Tf2Ps3SourceNativeMessageField[]> DirectTfElfFieldReductions =
        new Dictionary<string, Tf2Ps3SourceNativeMessageField[]>(StringComparer.Ordinal)
        {
            ["NET_Tick"] =
            [
                new("m_nTick", "+0x10", "32-bit unsigned tick", "m_nTick", "TF.elf direct writer emits message type 3 followed by one 32-bit tick field; no PC-era host-frame-time fields are written.")
            ],
            ["NET_StringCmd"] =
            [
                new("m_szCommand", "+0x10", "NUL-terminated ASCII string", "m_szCommand", "TF.elf direct writer emits message type 4 and one command string, with the recovered null fallback string when empty.")
            ],
            ["NET_SetConVar"] =
            [
                new("m_nCount", "derived", "8-bit unsigned pair count", "m_ConVars.Count()", "TF.elf direct writer emits message type 5, then an 8-bit name/value pair count."),
                new("m_ConVars", "array", "NUL-terminated ASCII name string followed by value string per entry", "name/value pairs", "TF.elf direct writer serializes each cvar name and value string in sequence after the 8-bit count.")
            ],
            ["CLC_VoiceData"] =
            [
                new("m_nLength", "+0x10", "16-bit unsigned payload length in bits", "m_nLength", "TF.elf 008c87e0 reads 16 bits to object +0x10; 008c38b8 writes this length with the 16-bit word writer."),
                new("m_DataInOrOut", "+0x14/+0x38", "raw voice payload bitstream of m_nLength bits", "m_DataIn/m_DataOut", "TF.elf 008c87e0 copies the bf_read state into object +0x14 and seeks by m_nLength; 008c38b8 writes payload bits from the voice buffer.")
            ],
            ["CLC_ListenEvents"] =
            [
                new("m_EventArray", "+0x10", "sixteen 32-bit listener mask words", "m_EventArray", "TF.elf 008c3db0 reads 16 consecutive 32-bit words to object +0x10; 008d02b0 writes the same 16 words after message type 12.")
            ],
            ["CLC_RespondCvarValue"] =
            [
                new("m_iCookie", "+0x10", "32-bit unsigned cookie", "m_iCookie", "TF.elf 008c54e8 reads 32 bits to object +0x10; 008d0040 writes param_1[4] with 32 bits."),
                new("m_eStatusCode", "+0x1c", "4-bit signed status code", "m_eStatusCode", "TF.elf 008c54e8 sign-extends a 4-bit field to object +0x1c; 008d0040 writes param_1[7] with 4 bits."),
                new("m_szCvarName", "+0x20", "NUL-terminated ASCII string, max 0x100 bytes", "m_szCvarName", "TF.elf 008c54e8 reads the cvar name string into object +0x20; 008d0040 writes param_1[5] as a string."),
                new("m_szCvarValue", "string pointer/value buffer", "NUL-terminated ASCII string, max 0x100 bytes", "m_szCvarValue", "TF.elf 008c54e8 reads the cvar value string after the name; 008d0040 writes param_1[6] as a string.")
            ],
            ["CLC_FileCRCCheck"] =
            [
                new("m_bReserved", "prefix", "one bit reserved flag", "reserved", "TF.elf 008c3f90 consumes one leading bit before path id; 008cf9c0 writes a zero bit before path compression fields."),
                new("m_PathID", "+0x10", "2-bit common path id code or fallback string", "m_PathID", "TF.elf 008c3f90 reads a 2-bit selector and either copies a common path string or reads a fallback string; 008cf9c0 mirrors the compressed selector/string write."),
                new("m_Filename", "+0x45", "3-bit common filename prefix code or fallback string", "m_Filename", "TF.elf 008c3f90 reads a 3-bit selector and optional filename tail; 008cf9c0 writes the compressed filename prefix selector and string tail."),
                new("m_CRC", "+0x218", "32-bit unsigned CRC", "m_CRC", "TF.elf 008cf9c0 writes object +0x218 with 32 bits after the path and filename fields; 008c3f90 reads the trailing CRC.")
            ],
            ["SVC_Print"] =
            [
                new("m_szText", "+0x10", "NUL-terminated ASCII string, null fallback", "m_szText", "TF.elf direct writer emits message type 7 and one print string, with the recovered svc_print null fallback when empty.")
            ],
            ["SVC_SetPause"] =
            [
                new("m_bPaused", "+0x10", "one bit", "m_bPaused", "TF.elf direct writer emits message type 11 followed by one pause flag bit.")
            ],
            ["SVC_VoiceInit"] =
            [
                new("m_szCodec", "+0x10", "NUL-terminated ASCII string", "m_szCodec", "TF.elf direct writer emits message type 14, codec string, then legacy quality byte."),
                new("m_nQuality", "after codec string", "8-bit unsigned legacy quality", "m_nQuality", "TF.elf direct writer writes the codec quality byte after the codec string.")
            ],
            ["SVC_VoiceData"] =
            [
                new("m_nFromClient", "+0x10", "8-bit unsigned", "m_nFromClient", "TF.elf direct writer emits message type 15 and writes from-client as one byte."),
                new("m_bProximity", "+0x14", "8-bit boolean byte", "m_bProximity", "TF.elf direct writer writes the proximity flag as a byte rather than a one-bit flag."),
                new("m_nLength", "+0x18", "16-bit unsigned payload length in bits", "m_nLength", "TF.elf direct writer writes voice payload bit length as a 16-bit word."),
                new("m_DataOut", "+0x40", "raw bit payload of m_nLength bits", "m_DataOut", "TF.elf direct writer writes exactly m_nLength bits from the voice payload buffer.")
            ],
            ["SVC_FixAngle"] =
            [
                new("m_bRelative", "+0x10", "one bit", "m_bRelative", "TF.elf direct writer emits message type 19 followed by the relative flag."),
                new("m_Angle.x", "+0x14", "16-bit bit-angle", "m_Angle.x", "TF.elf direct writer encodes x as a 16-bit Source bit-angle."),
                new("m_Angle.y", "+0x18", "16-bit bit-angle", "m_Angle.y", "TF.elf direct writer encodes y as a 16-bit Source bit-angle."),
                new("m_Angle.z", "+0x1c", "16-bit bit-angle", "m_Angle.z", "TF.elf direct writer encodes z as a 16-bit Source bit-angle.")
            ],
            ["SVC_CrosshairAngle"] =
            [
                new("m_Angle.x", "+0x10", "16-bit bit-angle", "m_Angle.x", "TF.elf direct writer emits message type 20 and encodes x as a 16-bit Source bit-angle."),
                new("m_Angle.y", "+0x14", "16-bit bit-angle", "m_Angle.y", "TF.elf direct writer encodes y as a 16-bit Source bit-angle."),
                new("m_Angle.z", "+0x18", "16-bit bit-angle", "m_Angle.z", "TF.elf direct writer encodes z as a 16-bit Source bit-angle.")
            ],
            ["SVC_UserMessage"] =
            [
                new("m_nMsgType", "+0x10", "8-bit unsigned usermessage id", "m_nMsgType", "TF.elf direct writer emits message type 23 followed by an 8-bit usermessage id."),
                new("m_nLength", "+0x14", "11-bit unsigned payload length in bits", "m_nLength", "TF.elf direct writer writes the payload length with NET_MESSAGE_BITS, 11 bits."),
                new("m_DataOut", "+0x3c", "raw bit payload of m_nLength bits", "m_DataOut", "TF.elf direct writer writes exactly m_nLength bits from the usermessage payload buffer.")
            ],
            ["SVC_EntityMessage"] =
            [
                new("m_nEntityIndex", "+0x10", "11-bit unsigned MAX_EDICT_BITS", "m_nEntityIndex", "TF.elf direct writer emits message type 24 and writes entity index with 11 bits."),
                new("m_nClassID", "+0x14", "9-bit unsigned server class id", "m_nClassID", "TF.elf direct writer writes class id with the recovered 9-bit server-class width."),
                new("m_nLength", "+0x18", "11-bit unsigned payload length in bits", "m_nLength", "TF.elf direct writer writes entity-message payload length with 11 bits."),
                new("m_DataOut", "+0x40", "raw bit payload of m_nLength bits", "m_DataOut", "TF.elf direct writer writes exactly m_nLength bits from the entity-message payload buffer.")
            ],
            ["SVC_GameEvent"] =
            [
                new("m_nLength", "+0x14", "11-bit unsigned payload length in bits", "m_nLength", "TF.elf direct writer emits message type 25 and writes game-event payload length with 11 bits."),
                new("m_DataOut", "+0x3c", "raw serialized game-event payload", "m_DataOut", "TF.elf direct writer writes exactly m_nLength bits from the serialized game-event payload buffer.")
            ],
            ["SVC_TempEntities"] =
            [
                new("m_nNumEntries", "+0x10", "8-bit unsigned temp entity count", "m_nNumEntries", "TF.elf direct writer emits message type 27 and writes temp-entity count with 8 bits."),
                new("m_nLength", "+0x14", "17-bit unsigned payload length in bits", "m_nLength", "TF.elf direct writer writes temp-entity payload length with 17 bits."),
                new("m_DataOut", "+0x3c", "raw temp-entity payload bits", "m_DataOut", "TF.elf direct writer writes exactly m_nLength bits from the temp-entity payload buffer.")
            ],
            ["SVC_Prefetch"] =
            [
                new("m_nSoundIndex", "+0x10", "13-bit unsigned sound index", "m_nSoundIndex", "TF.elf direct writer emits message type 28 and writes the PS3 build's 13-bit sound index field.")
            ],
            ["SVC_GetCvarValue"] =
            [
                new("m_iCookie", "+0x10", "32-bit signed cookie", "m_iCookie", "TF.elf direct writer emits message type 31 and writes the cvar query cookie as a signed 32-bit field."),
                new("m_szCvarName", "+0x14", "NUL-terminated ASCII string", "m_szCvarName", "TF.elf direct writer writes one cvar name string after the cookie.")
            ],
            ["SVC_Sounds"] =
            [
                new("m_bReliableSound", "+0x10", "one bit; reliable branch selects one 8-bit length", "m_bReliableSound", "TF.elf 008d1080 reads *(char *)(this+0x10), writes one bit, then writes length with 8 bits when set."),
                new("m_nNumSounds", "+0x14", "8-bit unsigned when unreliable", "m_nNumSounds", "TF.elf 008d1080 writes this+0x14 with 8 bits only on the unreliable branch."),
                new("m_nLength", "+0x18", "8-bit reliable length or 16-bit unreliable length, in bits", "m_nLength", "TF.elf 008d1080 copies this+0x4c to this+0x18, then writes 8/16 bits according to the reliable flag."),
                new("m_DataOut", "+0x40", "SoundInfo_t::WriteDelta entry payload of m_nLength bits", "m_DataOut", "TF.elf 008d1080 passes *(this+0x40) and m_nLength to the bit payload writer after the length header; Source consumes the payload as delta-compressed SoundInfo_t records.")
            ],
            ["SVC_BSPDecal"] =
            [
                new("m_Pos", "+0x10", "WriteBitVec3Coord / ReadBitVec3Coord", "m_Pos", "TF.elf 008ce980 calls 0086b128 on this+0x10; 008c4648 calls 008707c8 on this+0x10."),
                new("m_nDecalTextureIndex", "+0x1c", "9-bit unsigned", "m_nDecalTextureIndex", "TF.elf 008ce980 writes this+0x1c with 9 bits; 008c4648 reads 9 bits."),
                new("m_nEntityIndex", "+0x20", "presence bit, then 11-bit unsigned when non-zero", "m_nEntityIndex", "TF.elf 008ce980 branches on this+0x20, writes a presence bit, then writes 11 bits."),
                new("m_nModelIndex", "+0x24", "11-bit unsigned when entity index is present", "m_nModelIndex", "TF.elf 008ce980 writes this+0x24 with 11 bits after m_nEntityIndex; this is PS3-specific versus the public PC 13-bit SP_MODEL_INDEX_BITS."),
                new("m_bLowPriority", "+0x28", "one bit", "m_bLowPriority", "TF.elf 008ce980 writes *(char *)(this+0x28) as the final bit.")
            ],
            ["SVC_Menu"] =
            [
                new("m_MenuKeyValues", "+0x10", "KeyValues WriteAsBinary output, max 4096 bytes", "m_MenuKeyValues", "TF.elf 008cd780 requires this+0x10, calls the KeyValues binary writer into a CUtlBuffer, rejects length >= 0x1001, then writes raw bytes."),
                new("m_Type", "+0x14", "16-bit signed dialog type", "m_Type", "TF.elf 008cd780 writes this+0x14 through the 16-bit signed writer before the binary payload length."),
                new("m_iLength", "derived", "16-bit unsigned byte length", "m_iLength", "TF.elf 008cd780 writes local binary KeyValues length with a 16-bit word; 008cd9a8 reads it before byte payload."),
                new("m_MenuData", "derived", "m_iLength raw bytes", "binary KeyValues payload", "TF.elf 008cd780 writes local CUtlBuffer bytes with the byte writer; 008cd9a8 reads bytes and calls KeyValues::ReadAsBinary.")
            ],
            ["SVC_GameEventList"] =
            [
                new("m_nNumEvents", "+0x10", "9-bit unsigned MAX_EVENT_BITS", "m_nNumEvents", "TF.elf 008cf4f8 writes this+0x10 with 9 bits; 008c50e0 reads 9 bits."),
                new("m_nLength", "+0x14", "20-bit unsigned payload length in bits", "m_nLength", "TF.elf 008cf4f8 writes this+0x14 with 20 bits; 008c50e0 reads 20 bits."),
                new("m_DataOut", "+0x3c", "raw event descriptor bit payload of m_nLength bits", "m_DataOut", "TF.elf 008cf4f8 writes *(this+0x3c) through the bit payload writer after count and length.")
            ]
        };

    public static async Task<Tf2Ps3SourceNativeMessageContractReport> ReduceAsync(
        string vtableCatalogPath,
        string criticalIoContractPath,
        string clcMoveContractPath,
        string bootstrapControlPath,
        string packetEntitiesPlacementPath,
        string outputPath)
    {
        using var vtableDocument = JsonDocument.Parse(await File.ReadAllTextAsync(vtableCatalogPath));
        using var criticalDocument = JsonDocument.Parse(await File.ReadAllTextAsync(criticalIoContractPath));
        using var clcMoveDocument = JsonDocument.Parse(await File.ReadAllTextAsync(clcMoveContractPath));
        using var bootstrapDocument = JsonDocument.Parse(await File.ReadAllTextAsync(bootstrapControlPath));
        using var packetEntitiesDocument = JsonDocument.Parse(await File.ReadAllTextAsync(packetEntitiesPlacementPath));

        var criticalContracts = criticalDocument.RootElement.GetProperty("Contracts")
            .EnumerateArray()
            .ToDictionary(static item => ReadString(item, "ClassName"), StringComparer.Ordinal);
        var bootstrapControls = bootstrapDocument.RootElement.GetProperty("Messages")
            .EnumerateArray()
            .ToDictionary(static item => ReadString(item, "ClassName"), StringComparer.Ordinal);
        var clcMoveFields = clcMoveDocument.RootElement.GetProperty("FieldLayout")
            .EnumerateArray()
            .Select(static field => new Tf2Ps3SourceNativeMessageField(
                ReadString(field, "Name"),
                ReadString(field, "Offset"),
                ReadString(field, "Type"),
                "",
                ReadString(field, "Evidence")))
            .ToArray();
        var packetEntitiesSummary = packetEntitiesDocument.RootElement.GetProperty("Summary");

        var messages = vtableDocument.RootElement.GetProperty("Messages")
            .EnumerateArray()
            .Select(message => BuildMessage(message, criticalContracts, clcMoveFields, bootstrapControls, packetEntitiesSummary))
            .OrderBy(static message => message.FamilySortKey)
            .ThenBy(static message => message.MessageId)
            .ThenBy(static message => message.ClassName, StringComparer.Ordinal)
            .ToArray();

        var present = messages.Where(static message => message.PresentInTfElf).ToArray();
        var fieldReduced = messages.Where(static message => message.ContractStatus is "critical-field-reduced" or "clc-move-field-reduced" or "bootstrap-control-field-reduced" or "tfelf-direct-field-reduced").ToArray();
        var criticalPresent = messages.Where(static message => message.ImplementationPriority.Contains("critical", StringComparison.Ordinal)).ToArray();

        var report = new Tf2Ps3SourceNativeMessageContractReport(
            "tf2ps3-source-native-message-contract",
            "Implementation-facing native Source message contract reduced from TF.elf vtables and field-level I/O reports. This is a semantic contract for a native server, not packet replay data.",
            new Tf2Ps3SourceNativeMessageContractInputs(
                vtableCatalogPath,
                criticalIoContractPath,
                clcMoveContractPath,
                bootstrapControlPath,
                packetEntitiesPlacementPath),
            new Tf2Ps3SourceNativeMessageContractSummary(
                messages.Length,
                present.Length,
                messages.Length - present.Length,
                present.Count(static message => message.Family == "server"),
                present.Count(static message => message.Family == "client"),
                present.Count(static message => message.Family == "net"),
                criticalPresent.Length,
                fieldReduced.Length,
                messages.Count(static message => message.ContractStatus == "vtable-resolved-only"),
                messages.Count(static message => message.Route == "gameplay-snapshot-native-route")),
            messages,
            [
                "Every present TF.elf NET/CLC/SVC message has a single resolved INetMessage-style vtable with Process, ReadFromBuffer, WriteToBuffer, GetType, GetName, and ToString slots.",
                "Critical client join parsing is field-reduced for CLC_ClientInfo, CLC_Move, and CLC_BaselineAck; CLC_Move comes from the dedicated TF.elf/Source CLC_Move reducer.",
                "Critical server map-load generation is field-reduced for SVC_ServerInfo, SVC_SendTable, SVC_ClassInfo, SVC_CreateStringTable, SVC_UpdateStringTable, and the standalone SVC_PacketEntities codec.",
                "NET_SignonState and SVC_SetView are separately field-reduced as bootstrap control messages inside the object-stream route.",
                "Direct TF.elf source-message reductions now cover all remaining shared/client/server message wrappers, including CLC_VoiceData, CLC_ListenEvents, CLC_RespondCvarValue, CLC_FileCRCCheck, SVC_Sounds, SVC_BSPDecal, SVC_Menu, and SVC_GameEventList.",
                "No present TF.elf NET/CLC/SVC message remains vtable-resolved-only in the formal contract; deeper payload semantics still remain for choosing TF2 event/menu/sound content, CRC path policy, and gameplay snapshot payloads.",
                "Live gameplay entity updates should use the native snapshot route through 00a61150 -> 00a5fb80 -> 008bc978, while the standalone SVC_PacketEntities writer remains a codec compatibility contract."
            ],
            [
                "Implement vtable-resolved-only server messages only after their WriteToBuffer fields are reduced; do not guess field layouts from PC Source when TF.elf differs.",
                "Tie this message contract into the native Source responder so generated map-load batches are assembled from semantic state and encoded with the TF.elf field widths.",
                "Continue registration/setup recovery for the CNetChan-like handler table: 00a57f48 FindMessage, 00a5df70 RegisterMessage, and 00a58c10 ProcessMessages."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceNativeMessageContract BuildMessage(
        JsonElement message,
        IReadOnlyDictionary<string, JsonElement> criticalContracts,
        Tf2Ps3SourceNativeMessageField[] clcMoveFields,
        IReadOnlyDictionary<string, JsonElement> bootstrapControls,
        JsonElement packetEntitiesSummary)
    {
        var className = ReadString(message, "ClassName");
        var family = ReadString(message, "Family");
        var priority = ReadString(message, "ImplementationPriority");
        var present = ReadBool(message, "PresentInTfElf");
        var vtable = present ? message.GetProperty("CandidateVtables").EnumerateArray().FirstOrDefault() : default;
        var slots = present && vtable.ValueKind == JsonValueKind.Object
            ? vtable.GetProperty("Slots").EnumerateArray().ToArray()
            : [];

        criticalContracts.TryGetValue(className, out var critical);
        bootstrapControls.TryGetValue(className, out var bootstrap);
        DirectTfElfFieldReductions.TryGetValue(className, out var directTfElfFields);
        var fieldSource = critical.ValueKind == JsonValueKind.Object
            ? "source-critical-message-io-contract"
            : className == "CLC_Move"
                ? "source-clc-move-contract"
            : bootstrap.ValueKind == JsonValueKind.Object
                ? "source-bootstrap-control-message-map"
            : directTfElfFields is not null
                ? "tfelf-direct-write-read-reduction"
                : "";
        var fields = critical.ValueKind == JsonValueKind.Object
            ? ReadFields(critical.GetProperty("Fields"))
            : className == "CLC_Move"
                ? clcMoveFields
            : bootstrap.ValueKind == JsonValueKind.Object
                ? ReadBootstrapFields(bootstrap.GetProperty("Fields"))
            : directTfElfFields is not null
                ? directTfElfFields
                : [];

        var status = !present
            ? "absent-in-tfelf"
            : critical.ValueKind == JsonValueKind.Object
                ? "critical-field-reduced"
                : className == "CLC_Move"
                    ? "clc-move-field-reduced"
            : bootstrap.ValueKind == JsonValueKind.Object
                ? "bootstrap-control-field-reduced"
            : directTfElfFields is not null
                ? "tfelf-direct-field-reduced"
                : "vtable-resolved-only";

        return new Tf2Ps3SourceNativeMessageContract(
            className,
            family,
            FamilySortKey(family),
            ReadInt(message, "SourceMessageId"),
            priority,
            present,
            ReadString(message, "NetworkNameString"),
            present && vtable.ValueKind == JsonValueKind.Object ? ReadString(vtable, "ObjectVptr") : "",
            Slot(slots, "Process"),
            Slot(slots, "ReadFromBuffer"),
            Slot(slots, "WriteToBuffer"),
            Slot(slots, "GetType"),
            Slot(slots, "GetName"),
            Slot(slots, "ToString"),
            status,
            fieldSource,
            fields.Length,
            fields,
            RouteFor(className, family, priority, status, packetEntitiesSummary),
            GuidanceFor(className, family, priority, status));
    }

    private static Tf2Ps3SourceNativeMessageField[] ReadFields(JsonElement fields)
    {
        return fields.EnumerateArray()
            .Select(static field => new Tf2Ps3SourceNativeMessageField(
                ReadString(field, "Name"),
                ReadString(field, "TfObjectOffset"),
                ReadString(field, "Encoding"),
                ReadString(field, "SourceEquivalent"),
                ReadString(field, "TfEvidence")))
            .ToArray();
    }

    private static Tf2Ps3SourceNativeMessageField[] ReadBootstrapFields(JsonElement fields)
    {
        return fields.EnumerateArray()
            .Select(static field => new Tf2Ps3SourceNativeMessageField(
                ReadString(field, "Name"),
                ReadString(field, "Offset"),
                ReadString(field, "Encoding"),
                "",
                ReadString(field, "EvidenceToken")))
            .ToArray();
    }

    private static string Slot(JsonElement[] slots, string role)
    {
        var slot = slots.FirstOrDefault(item => ReadString(item, "Role") == role);
        return slot.ValueKind == JsonValueKind.Object ? ReadString(slot, "EntryAddress") : "";
    }

    private static string RouteFor(
        string className,
        string family,
        string priority,
        string status,
        JsonElement packetEntitiesSummary)
    {
        if (className == "SVC_PacketEntities")
        {
            return ReadBool(packetEntitiesSummary, "NativeGameplayRouteIdentified")
                ? "gameplay-snapshot-native-route"
                : "standalone-packetentities-codec";
        }

        if (status == "bootstrap-control-field-reduced")
        {
            return "bootstrap-control-object-stream";
        }

        if (priority == "critical-server-map-load")
        {
            return "bootstrap-map-load-object-stream";
        }

        if (priority == "critical-client-join")
        {
            return "client-join-parse";
        }

        return family switch
        {
            "server" => "server-to-client-source-message",
            "client" => "client-to-server-source-message",
            "net" => "shared-net-message",
            _ => "source-message"
        };
    }

    private static string GuidanceFor(string className, string family, string priority, string status)
    {
        if (className == "SVC_PacketEntities")
        {
            return "Keep the standalone writer for codec compatibility, but feed live gameplay through the native snapshot/entity-delta route.";
        }

        if (status is "critical-field-reduced" or "clc-move-field-reduced" or "tfelf-direct-field-reduced")
        {
            return priority == "critical-client-join"
                ? "Parse this from the client before treating map-load/sign-on as accepted."
                : "Generate this from semantic server/session/map state with the recovered TF.elf field widths.";
        }

        if (status == "bootstrap-control-field-reduced")
        {
            return "Generate this inside the object-stream bootstrap sequence using the recovered TF.elf field widths.";
        }

        if (status == "absent-in-tfelf")
        {
            return "Do not emit or expect this PC Source-era message for TF2 PS3 unless later binary evidence proves it exists.";
        }

        return family switch
        {
            "server" => "Vtable is resolved; reduce WriteToBuffer before using it in the native server.",
            "client" => "Vtable is resolved; reduce ReadFromBuffer before accepting this client message.",
            "net" => "Vtable is resolved; reduce direction-specific I/O before changing runtime behavior.",
            _ => "Vtable is resolved; field-level semantics still need reduction."
        };
    }

    private static int FamilySortKey(string family)
    {
        return family switch
        {
            "net" => 0,
            "client" => 1,
            "server" => 2,
            _ => 3
        };
    }

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

public sealed record Tf2Ps3SourceNativeMessageContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceNativeMessageContractInputs Inputs,
    Tf2Ps3SourceNativeMessageContractSummary Summary,
    Tf2Ps3SourceNativeMessageContract[] Messages,
    string[] Findings,
    string[] NextReverseEngineeringTargets);

public sealed record Tf2Ps3SourceNativeMessageContractInputs(
    string VtableCatalog,
    string CriticalIoContract,
    string ClcMoveContract,
    string BootstrapControl,
    string PacketEntitiesPlacement);

public sealed record Tf2Ps3SourceNativeMessageContractSummary(
    int MessageCount,
    int PresentInTfElfCount,
    int AbsentInTfElfCount,
    int ServerMessageCount,
    int ClientMessageCount,
    int NetMessageCount,
    int CriticalMessageCount,
    int FieldReducedMessageCount,
    int VtableResolvedOnlyCount,
    int GameplaySnapshotNativeRouteCount);

public sealed record Tf2Ps3SourceNativeMessageContract(
    string ClassName,
    string Family,
    int FamilySortKey,
    int MessageId,
    string ImplementationPriority,
    bool PresentInTfElf,
    string NetworkName,
    string ObjectVptr,
    string ProcessEntry,
    string ReadFromBufferEntry,
    string WriteToBufferEntry,
    string GetTypeEntry,
    string GetNameEntry,
    string ToStringEntry,
    string ContractStatus,
    string FieldEvidenceSource,
    int FieldCount,
    Tf2Ps3SourceNativeMessageField[] Fields,
    string Route,
    string NativeImplementationGuidance);

public sealed record Tf2Ps3SourceNativeMessageField(
    string Name,
    string ObjectOffset,
    string Encoding,
    string SourceEquivalent,
    string TfEvidence);
