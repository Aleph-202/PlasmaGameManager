using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceFieldContractReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceFieldContractReport> ReduceAsync(
        string builderMapPath,
        string embeddedObjectPath,
        string gameplayPhasesPath,
        string outputPath)
    {
        using var builderDoc = JsonDocument.Parse(File.ReadAllText(builderMapPath));
        using var embeddedDoc = JsonDocument.Parse(File.ReadAllText(embeddedObjectPath));
        using var phasesDoc = JsonDocument.Parse(File.ReadAllText(gameplayPhasesPath));

        var nativeMessages = BuildNativeMessageContracts(builderDoc.RootElement);
        var deltaMessages = BuildDeltaMessageContracts(builderDoc.RootElement);
        var sourceEntityFields = BuildSourceEntityFieldContracts();
        var embeddedObjects = BuildEmbeddedObjectContracts(embeddedDoc.RootElement);
        var phaseContract = BuildPhaseContract(phasesDoc.RootElement);
        var unknowns = BuildRemainingUnknowns(builderDoc.RootElement, embeddedDoc.RootElement, phasesDoc.RootElement);

        var report = new Tf2Ps3SourceFieldContractReport(
            "tf2ps3-native-source-field-contract",
            "Combines TF.elf Source send-builder field recovery with PCAP corpus object/phase observations. This is a native-generation contract, not a packet replay recipe.",
            new Tf2Ps3SourceFieldContractInputs(builderMapPath, embeddedObjectPath, gameplayPhasesPath),
            new Tf2Ps3SourceFieldContractSummary(
                nativeMessages.Length,
                nativeMessages.Count(static message => message.KnownFieldCount > 0),
                nativeMessages.Sum(static message => message.Fields.Count(static field => field.Status == "recovered")),
                nativeMessages.Sum(static message => message.Fields.Count(static field => field.Status == "unresolved-expression")),
                deltaMessages.Length,
                sourceEntityFields.Length,
                embeddedObjects.Length,
                embeddedObjects.Sum(static item => item.ObservedCount),
                phaseContract.LongGameplaySessionCount,
                unknowns.Length),
            nativeMessages,
            deltaMessages,
            sourceEntityFields,
            embeddedObjects,
            phaseContract,
            unknowns);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3NativeSourceMessageContract[] BuildNativeMessageContracts(JsonElement root)
    {
        return root.GetProperty("Functions").EnumerateArray()
            .Where(static function => function.TryGetProperty("SchemaMessageId", out var id)
                && id.ValueKind == JsonValueKind.String
                && id.GetString() is "0x44" or "0x45" or "0x49")
            .Select(BuildNativeMessageContract)
            .OrderBy(static message => message.MessageId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Tf2Ps3NativeSourceMessageContract BuildNativeMessageContract(JsonElement function)
    {
        var address = ReadString(function, "Address");
        var messageId = ReadString(function, "SchemaMessageId");
        var writes = function.GetProperty("Writes").EnumerateArray()
            .Select((write, index) =>
            {
                var field = NativeFieldSemantics(messageId, index, ReadString(write, "ValueExpression"));
                return new Tf2Ps3NativeSourceField(
                    index,
                    ReadString(write, "Kind"),
                    ReadNullableInt(write, "FixedBitWidth"),
                    ReadString(write, "ValueExpression"),
                    ReadString(write, "FieldRole"),
                    field.Name,
                    field.Meaning,
                    FieldStatus(write, field.Name));
            })
            .ToArray();
        return new Tf2Ps3NativeSourceMessageContract(
            address,
            messageId,
            MessageName(messageId),
            ReadString(function, "SchemaName"),
            ReadString(function, "SchemaBuffer"),
            ReadBool(function, "UsesBitPayloadSidecar"),
            ReadBool(function, "UsesFragmentOrCompressionGate"),
            writes.Length,
            writes,
            NativeGenerationGuidance(messageId));
    }

    private static Tf2Ps3SourceDeltaContract[] BuildDeltaMessageContracts(JsonElement root)
    {
        return root.GetProperty("Functions").EnumerateArray()
            .Where(static function => ReadString(function, "Address") is "00910d48" or "00911140" or "009113a0")
            .Select(static function =>
            {
                var address = ReadString(function, "Address");
                var schema = ReadString(function, "SchemaName");
                var callsite = function.GetProperty("PayloadBuilderCallsites").EnumerateArray().FirstOrDefault();
                var length = callsite.ValueKind == JsonValueKind.Object ? ReadString(callsite, "LengthExpression") : "";
                var peer = callsite.ValueKind == JsonValueKind.Object ? ReadString(callsite, "PeerAddressExpression") : "";
                return address switch
                {
                    "00910d48" => new Tf2Ps3SourceDeltaContract(
                        address,
                        schema,
                        "player-health-delta",
                        "m_iHealth[slot]",
                        "Formats and sends a changed CPlayerResource m_iHealth value to each interested peer.",
                        length,
                        peer,
                        "Generate from current player health, connected/alive state, and peer interest filtering."),
                    "00911140" => new Tf2Ps3SourceDeltaContract(
                        address,
                        schema,
                        "player-rating-delta",
                        "m_iPlayerRating[slot] / m_iRatingDelta[slot]",
                        "Checks cached per-peer rating state and emits a single-byte delta notification when thresholds are crossed.",
                        length,
                        peer,
                        "Generate only from real rating/rating-delta state; do not fill this path with deterministic random bytes."),
                    "009113a0" => new Tf2Ps3SourceDeltaContract(
                        address,
                        schema,
                        "formatted-player-status-event",
                        "player connect/status text payload",
                        "Formats a variable-length status/connect event with player index, user id, name, connection flags, and textual reason before sending it to peers that pass interest checks.",
                        length,
                        peer,
                        "Generate as a preformatted native text event from the active player/session event state; keep the exact optional connection-flag branches isolated until all reason strings are named."),
                    _ => throw new InvalidOperationException($"Unexpected Source delta builder {address}")
                };
            })
            .OrderBy(static item => item.Address, StringComparer.Ordinal)
            .ToArray();
    }

    private static Tf2Ps3EmbeddedObjectContract[] BuildEmbeddedObjectContracts(JsonElement root)
    {
        var summary = root.GetProperty("Summary");
        var roleCounts = summary.GetProperty("RecordRoleCounts").EnumerateObject()
            .ToDictionary(static item => item.Name, static item => item.Value.GetInt32(), StringComparer.Ordinal);
        var files = root.GetProperty("Files").EnumerateArray().ToArray();

        return
        [
            BuildEmbeddedObjectContract(
                "COc",
                "FrozenStateObject",
                "0x25-byte record: marker, version, object id, linked frozen-state object id, class id 0x00004408, display name.",
                roleCounts,
                files),
            BuildEmbeddedObjectContract(
                "COc",
                "PlayerObject",
                "0x25-byte record: marker, version, object id, parent/association id, player class id, display name.",
                roleCounts,
                files),
            BuildEmbeddedObjectContract(
                "PNG",
                "PlayerStateLink",
                "0x0c-byte record: marker, version, source object id, linked object id.",
                roleCounts,
                files),
            BuildEmbeddedObjectContract(
                "DSC",
                "PlayerDescriptor",
                "0x10-byte record: marker, version 0, descriptor object id, descriptor link, descriptor class id.",
                roleCounts,
                files)
        ];
    }

    private static Tf2Ps3EmbeddedObjectContract BuildEmbeddedObjectContract(
        string marker,
        string role,
        string fieldLayout,
        IReadOnlyDictionary<string, int> roleCounts,
        JsonElement[] files)
    {
        var participantIds = files
            .SelectMany(ParticipantSummaries)
            .Where(summary => HasRoleEvidence(summary, role))
            .Select(summary => ReadString(summary, "ObjectIdHex"))
            .Where(static value => value.Length > 0)
            .GroupBy(static value => value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => new Tf2Ps3SourceObservedValue(group.Key, group.Count()))
            .ToArray();
        var names = files
            .SelectMany(file => CountValues(file, "PrintableCandidateCounts"))
            .Where(static value => !value.Value.Equals("FrozenState_", StringComparison.Ordinal))
            .GroupBy(static value => value.Value, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Sum(static item => item.Count))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(16)
            .Select(static group => new Tf2Ps3SourceObservedValue(group.Key, group.Sum(static item => item.Count)))
            .ToArray();

        return new Tf2Ps3EmbeddedObjectContract(
            marker,
            role,
            fieldLayout,
            roleCounts.GetValueOrDefault(role),
            participantIds,
            names,
            role switch
            {
                "FrozenStateObject" => "Generate from current session/player mesh state. FieldC 0x00004408 is fixed for TF2 PS3 frozen-state association records.",
                "PlayerStateLink" => "Generate whenever the mesh association graph changes and during steady state. Most gameplay packets are PNG link batches.",
                "PlayerObject" => "Generate from native player roster state once non-frozen roster object records are required.",
                "PlayerDescriptor" => "Generate from player descriptor state for long gameplay sessions; this remains lower priority for first MOTD/loading transition.",
                _ => "Generate from native state."
            });
    }

    private static Tf2Ps3SourceEntityFieldContract[] BuildSourceEntityFieldContracts()
    {
        return
        [
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_vecOrigin",
                "0x2b4",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00065300 registers base entity origin through the vector send-prop writer.",
                "Generate from server-side player position in CTFPlayer entity deltas."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_angRotation",
                "0x2c0",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00065300 registers base entity rotation through the vector send-prop writer.",
                "Generate from server-side player rotation or zero during pre-spawn/loading handoff."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_iTeamNum",
                "0x80",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00065300 registers the base entity team number field.",
                "Generate from player team assignment once Source-side team state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_flSimulationTime",
                "0x58",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00065300 registers base entity simulation time.",
                "Generate from Source tick/timebase for every networked entity."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_nModelIndex",
                "0x74",
                "0x02",
                "uint16",
                "TF.elf _opd_FUN_00065300 registers model index as a two-byte send field.",
                "Generate from precached model table index once model tables are native."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_fEffects",
                "0x60",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00065300 registers base entity effects flags.",
                "Generate from EF_* state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_nRenderMode",
                "0x64",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00065300 registers render mode.",
                "Generate from render state; zero is normal opaque rendering."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_nRenderFX",
                "0x44",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00065300 registers render FX.",
                "Generate from render state; zero is valid for no special effect."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_clrRender",
                "0x48",
                "0x04",
                "rgba32",
                "TF.elf _opd_FUN_00065300 registers render color.",
                "Generate from entity render color, default opaque white."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_CollisionGroup",
                "0x2d0",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00065300 registers collision group.",
                "Generate from collision rules/group state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_flElasticity",
                "0x1cc",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00065300 registers collision elasticity.",
                "Generate from physics/collision state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_flShadowCastDistance",
                "0x1d0",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00065300 registers shadow cast distance.",
                "Generate from render/shadow state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_hOwnerEntity",
                "0x450",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_00065300 registers owner entity handle.",
                "Generate from ownership relationships such as weapons, projectiles, and buildables."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_hEffectEntity",
                "0x454",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_00065300 registers effect entity handle.",
                "Generate from effect attachment state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "moveparent",
                "0x144",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_00065300 registers moveparent.",
                "Generate from entity parent hierarchy."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_iParentAttachment",
                "0x12e",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00065300 registers parent attachment index.",
                "Generate from attachment hierarchy."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "movetype",
                "0x0000",
                "send-proxy",
                "int32",
                "TF.elf _opd_FUN_00065300 registers movetype through a send proxy.",
                "Generate from movement type semantics once proxy encoding is fully recovered."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "movecollide",
                "0x0000",
                "send-proxy",
                "int32",
                "TF.elf _opd_FUN_00065300 registers movecollide through a send proxy.",
                "Generate from movement collision semantics once proxy encoding is fully recovered."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_Collision",
                "0x150",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00065300 registers the collision nested sendtable.",
                "Generate as a table anchor before individual collision fields are fully recovered."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_vecMins",
                "0x08",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0011c468 registers collision mins through the vector send-prop writer.",
                "Generate from entity collision bounds."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_vecMaxs",
                "0x14",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0011c468 registers collision maxs through the vector send-prop writer.",
                "Generate from entity collision bounds."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_nSolidType",
                "0x29",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_0011c468 registers solid type.",
                "Generate from entity collision representation."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_usSolidFlags",
                "0x24",
                "0x02",
                "uint16",
                "TF.elf _opd_FUN_0011c468 registers solid flags.",
                "Generate from entity solid/collision flags."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_nSurroundType",
                "0x28",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_0011c468 registers surrounding-bounds type.",
                "Generate from collision/render bounds mode."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_triggerBloat",
                "0x2a",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_0011c468 registers trigger bloat.",
                "Generate from trigger/collision bounds inflation."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_vecSpecifiedSurroundingMins",
                "0x2c",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0011c468 registers explicitly specified surrounding mins.",
                "Generate from entity surrounding bounds when manually specified."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_CollisionProperty",
                "m_vecSpecifiedSurroundingMaxs",
                "0x38",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0011c468 registers explicitly specified surrounding maxs.",
                "Generate from entity surrounding bounds when manually specified."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_iTextureFrameIndex",
                "0x444",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00065300 registers texture frame index.",
                "Generate from animated texture/render state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "predictable_id",
                "0x0000",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00065300 registers the predictable id nested sendtable.",
                "Generate as a table anchor for predicted entities."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PredictableId",
                "m_PredictableID",
                "0x84",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00065780 registers the predictable id value.",
                "Generate from entity prediction identity state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PredictableId",
                "m_bIsPlayerSimulated",
                "0x440",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00065780 registers the player-simulated prediction flag.",
                "Generate true for player-simulated predicted entities when applicable."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_bSimulatedEveryTick",
                "0x441",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00065300 registers the simulated-every-tick flag.",
                "Generate true for player-critical entities during loading handoff."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_bAnimatedEveryTick",
                "0x442",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00065300 registers the animated-every-tick flag.",
                "Generate true for animated player/world entities."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseEntity",
                "m_bAlternateSorting",
                "0x443",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00065300 registers alternate sorting.",
                "Generate from render sorting state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nSequence",
                "0x6f8",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers animation sequence.",
                "Generate from model animation state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nForceBone",
                "0x4bc",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers force-bone index.",
                "Generate from animation/physics force state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_vecForce",
                "0x4b0",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0003c9b8 registers animation force vector.",
                "Generate from physics impulse state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nSkin",
                "0x47c",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers model skin.",
                "Generate from model skin/bodygroup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nBody",
                "0x480",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers model body.",
                "Generate from model bodygroup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nHitboxSet",
                "0x484",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers hitbox set.",
                "Generate from model/class hitbox state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_flModelWidthScale",
                "0x560",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c9b8 registers model width scale.",
                "Generate from model scale state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_flPoseParameter",
                "0x564",
                "0x04[0x18]",
                "float-array",
                "TF.elf _opd_FUN_0003c9b8 registers m_flPoseParameter[0] and wraps it as a 0x18-entry array.",
                "Generate from animation pose parameters."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_flPlaybackRate",
                "0x4a4",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c9b8 registers animation playback rate.",
                "Generate from animation playback state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_flEncodedController",
                "0x67c",
                "0x04[0x04]",
                "float-array",
                "TF.elf _opd_FUN_0003c9b8 registers m_flEncodedController[0] and wraps it as a four-entry array.",
                "Generate from animation controller state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_bClientSideAnimation",
                "0x6c8",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_0003c9b8 registers client-side animation flag.",
                "Generate from entity animation ownership."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_bClientSideFrameReset",
                "0x504",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_0003c9b8 registers client-side frame reset.",
                "Generate from animation reset events."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nNewSequenceParity",
                "0x6cc",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers sequence parity.",
                "Generate from animation sequence changes."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nResetEventsParity",
                "0x6d0",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0003c9b8 registers reset-events parity.",
                "Generate from animation event reset changes."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_nMuzzleFlashParity",
                "0x778",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_0003c9b8 registers muzzle flash parity.",
                "Generate from weapon muzzle flash events."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_hLightingOrigin",
                "0x770",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_0003c9b8 registers lighting origin handle.",
                "Generate from lighting origin state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_hLightingOriginRelative",
                "0x774",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_0003c9b8 registers relative lighting origin handle.",
                "Generate from lighting origin state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "serveranimdata",
                "0x0000",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_0003c9b8 registers DT_ServerAnimationData as serveranimdata.",
                "Generate as a table anchor for server animation cycle."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ServerAnimationData",
                "m_flCycle",
                "0x700",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c900 registers server animation cycle.",
                "Generate from animation cycle state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_fadeMinDist",
                "0x508",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c9b8 registers fade minimum distance.",
                "Generate from render fade settings."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_fadeMaxDist",
                "0x50c",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c9b8 registers fade maximum distance.",
                "Generate from render fade settings."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseAnimating",
                "m_flFadeScale",
                "0x510",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0003c9b8 registers fade scale.",
                "Generate from render fade settings."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_iHealth",
                "0x78",
                "0x04",
                "int32",
                "TF.elf BasePlayer send-table setup registers the entity health field.",
                "Generate alongside CPlayerResource health so entity and resource state agree."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_lifeState",
                "0x77",
                "0x01",
                "uint8",
                "TF.elf BasePlayer send-table setup registers the life-state byte.",
                "Generate 0 during alive/pre-spawn handoff, then track Source life state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_fFlags",
                "0x2cc",
                "0x04",
                "int32",
                "TF.elf BasePlayer send-table setup registers movement/state flags.",
                "Generate from Source movement flags once gameplay simulation is native."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_angEyeAngles[0]",
                "0x1460",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00338928 registers TF player eye pitch.",
                "Generate from view angle state; zero is acceptable during initial loading."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_angEyeAngles[1]",
                "0x1464",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00338928 registers TF player eye yaw.",
                "Generate from view angle state; zero/heading is acceptable during initial loading."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_PlayerClass",
                "0x1318",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00338928 registers the nested TF player class table.",
                "Generate as the native class-state parent when class choice is known."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_iClass",
                "0xb58",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338d00 registers class id on the ragdoll table; this name also anchors TF class id encoding.",
                "Use as class-id evidence while exact nested m_PlayerClass members are still being mapped."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_iAmmo",
                "0xb38",
                "0x04[0x20]",
                "int32-array",
                "TF.elf local-player send-table setup registers m_iAmmo[0] then wraps it as a 0x20-entry array.",
                "Generate from local player ammo buckets; zero-filled during first loading handoff is valid but not gameplay-complete."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_iFOV",
                "0xeb8",
                "0x04",
                "int32",
                "TF.elf BasePlayer local data setup registers current FOV.",
                "Generate from player camera state; default 75 is suitable before class/camera changes."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_iFOVStart",
                "0xebc",
                "0x04",
                "int32",
                "TF.elf BasePlayer local data setup registers starting FOV.",
                "Generate from FOV interpolation state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flFOVTime",
                "0xec0",
                "0x04",
                "float",
                "TF.elf BasePlayer local data setup registers FOV interpolation time.",
                "Generate from FOV transition timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_iDefaultFOV",
                "0xec4",
                "0x04",
                "int32",
                "TF.elf BasePlayer local data setup registers default FOV.",
                "Generate from player class/camera settings."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_hZoomOwner",
                "0xec8",
                "0x04",
                "ehandle",
                "TF.elf BasePlayer send-table setup registers zoom owner immediately after FOV fields.",
                "Generate from scoped/zoom state; zero is valid before scoped weapons exist."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_hVehicle",
                "0xf4c",
                "0x04",
                "ehandle",
                "TF.elf BasePlayer send-table setup registers vehicle handle.",
                "Generate zero for TF2 PS3 because normal TF gameplay does not use vehicles."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_hUseEntity",
                "0xf54",
                "0x04",
                "ehandle",
                "TF.elf BasePlayer send-table setup registers the active use entity handle.",
                "Generate from interaction target once gameplay simulation exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_flMaxspeed",
                "0xf58",
                "0x04",
                "float",
                "TF.elf BasePlayer send-table setup registers max movement speed.",
                "Generate from class speed once player class is selected."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_iObserverMode",
                "0xf20",
                "0x04",
                "int32",
                "TF.elf BasePlayer send-table setup registers observer mode.",
                "Generate from spectator/death camera state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_hObserverTarget",
                "0xf24",
                "0x04",
                "ehandle",
                "TF.elf BasePlayer send-table setup registers observer target handle.",
                "Generate from spectator/death camera state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_hViewModel",
                "0xff0",
                "0x04[0x02]",
                "ehandle-array",
                "TF.elf BasePlayer setup registers m_hViewModel[0] and wraps it as a two-entry handle array.",
                "Generate from first-person weapon/viewmodel objects once weapon entities are native."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_szLastPlaceName",
                "0x1134",
                "0x12",
                "string",
                "TF.elf BasePlayer setup registers the last nav/place name string.",
                "Generate an empty string until map place-name state is derived."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayer",
                "m_ubEFNoInterpParity",
                "0x115c",
                "0x01",
                "uint8",
                "TF.elf BasePlayer setup registers no-interp effect parity.",
                "Generate from entity interpolation parity; zero is stable during initial load."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_vecViewOffset",
                "0xcc",
                "0x04[0x03]",
                "vector3",
                "TF.elf local-player setup registers m_vecViewOffset[0..2].",
                "Generate from player stance/class camera height."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flFriction",
                "0x1e0",
                "0x04",
                "float",
                "TF.elf local-player setup registers friction.",
                "Generate from movement state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_fOnTarget",
                "0xecc",
                "0x01",
                "uint8",
                "TF.elf local-player setup registers on-target assist state.",
                "Generate from aim/target state; zero is valid before gameplay."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_nTickBase",
                "0xfc8",
                "0x04",
                "int32",
                "TF.elf local-player setup registers tick base.",
                "Generate from server simulation tick/frame index."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_nNextThinkTick",
                "0x6c",
                "0x04",
                "int32",
                "TF.elf local-player setup registers next think tick.",
                "Generate from entity think schedule."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_hGroundEntity",
                "0x1d8",
                "0x04",
                "ehandle",
                "TF.elf local-player setup registers ground entity handle.",
                "Generate from movement collision state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_hLastWeapon",
                "0xfd8",
                "0x04",
                "ehandle",
                "TF.elf local-player send-table setup registers the last weapon entity handle.",
                "Generate from active weapon history once native weapon state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_vecVelocity",
                "0xd8",
                "0x04[0x03]",
                "vector3",
                "TF.elf local-player setup registers m_vecVelocity[0..2].",
                "Generate from player movement; zero vector is valid before spawn movement."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_vecBaseVelocity",
                "0x10c",
                "0x0c",
                "vector3",
                "TF.elf local-player setup registers base velocity.",
                "Generate from conveyor/knockback base movement state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_hConstraintEntity",
                "0xf04",
                "0x04",
                "ehandle",
                "TF.elf local-player setup registers movement constraint entity handle.",
                "Generate from constraint state; zero means unconstrained."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_vecConstraintCenter",
                "0xf08",
                "0x0c",
                "vector3",
                "TF.elf local-player setup registers movement constraint center.",
                "Generate from constraint state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flConstraintRadius",
                "0xf14",
                "0x04",
                "float",
                "TF.elf local-player setup registers movement constraint radius.",
                "Generate from constraint state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flConstraintWidth",
                "0xf18",
                "0x04",
                "float",
                "TF.elf local-player setup registers movement constraint width.",
                "Generate from constraint state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flConstraintSpeedFactor",
                "0xf1c",
                "0x04",
                "float",
                "TF.elf local-player setup registers movement constraint speed factor.",
                "Generate from constraint state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flDeathTime",
                "0xf44",
                "0x04",
                "float",
                "TF.elf local-player setup registers death time.",
                "Generate from death/spectator state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_nWaterLevel",
                "0x130",
                "0x01",
                "uint8",
                "TF.elf local-player setup registers water level.",
                "Generate from map collision/water state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BasePlayerLocalData",
                "m_flLaggedMovementValue",
                "0x1114",
                "0x04",
                "float",
                "TF.elf local-player setup registers lagged movement scale.",
                "Generate 1.0 unless gameplay effects alter movement scale."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bDucked",
                "0x44",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers ducked state.",
                "Generate from crouch state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bDucking",
                "0x45",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers active duck transition.",
                "Generate from crouch transition state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bInDuckJump",
                "0x46",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers duck-jump state.",
                "Generate from movement state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_flDucktime",
                "0x48",
                "0x04",
                "float",
                "TF.elf DT_Local setup registers duck transition time.",
                "Generate from crouch timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_flDuckJumpTime",
                "0x4c",
                "0x04",
                "float",
                "TF.elf DT_Local setup registers duck-jump timing.",
                "Generate from movement timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_flJumpTime",
                "0x50",
                "0x04",
                "float",
                "TF.elf DT_Local setup registers jump timing.",
                "Generate from movement timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_flFallVelocity",
                "0x58",
                "0x04",
                "float",
                "TF.elf DT_Local setup registers fall velocity.",
                "Generate from movement velocity."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_vecPunchAngle",
                "0x6c",
                "0x0c",
                "vector3",
                "TF.elf DT_Local setup registers view punch angle.",
                "Generate from recoil/view kick state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_vecPunchAngleVel",
                "0xa4",
                "0x0c",
                "vector3",
                "TF.elf DT_Local setup registers view punch angular velocity.",
                "Generate from recoil/view kick state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bDrawViewmodel",
                "0xdc",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers viewmodel visibility.",
                "Generate true unless hidden by observer/class state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bWearingSuit",
                "0xdd",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers suit flag inherited from Source base local data.",
                "Generate false for TF2 PS3."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bPoisoned",
                "0xde",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers poisoned state.",
                "Generate false for TF2 PS3 unless future gameplay effect requires it."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_flStepSize",
                "0xe0",
                "0x04",
                "float",
                "TF.elf DT_Local setup registers step size.",
                "Generate Source default 18.0 unless movement simulation overrides it."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Local",
                "m_bAllowAutoMovement",
                "0xe4",
                "0x01",
                "uint8",
                "TF.elf DT_Local setup registers auto-movement allowance.",
                "Generate true for normal player control."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_bSaveMeParity",
                "0x14ac",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00338928 registers medic call/save-me parity.",
                "Generate from call-for-medic state; zero is valid before player input."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_nWaterLevel",
                "0x130",
                "0x01",
                "uint8",
                "TF.elf _opd_FUN_00338928 registers TF player water level inherited into the TF table.",
                "Generate with local water level."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_hRagdoll",
                "0x12b0",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_00338928 registers player ragdoll handle.",
                "Generate zero while alive/pre-spawn."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_Shared",
                "0x1350",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00338928 registers the nested TF shared-state table.",
                "Generate as an explicit native table anchor before individual shared fields are recovered."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nPlayerCond",
                "0x08",
                "0x04",
                "int32",
                "Updated BLES TF.elf string xref table at 0x10104aa0 registers m_nPlayerCond.",
                "Generate from TF condition bitmask state; zero is valid before class/spawn state is initialized."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_bJumping",
                "0xa8",
                "0x01",
                "bool",
                "Updated BLES TF.elf string xref table at 0x10104b10 registers m_bJumping.",
                "Generate from local movement state; false is valid during loading/pre-spawn."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nPlayerState",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_nPlayerState in the same shared-state block.",
                "Generate from TF player lifecycle state once spawn/team selection state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_iDesiredPlayerClass",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_iDesiredPlayerClass.",
                "Generate from class-selection request state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nDisguiseTeam",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_nDisguiseTeam.",
                "Generate from spy disguise state; zero before disguise state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nDisguiseClass",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_nDisguiseClass.",
                "Generate from spy disguise state; zero before disguise state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_iDisguiseTargetIndex",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf string xref table at 0x1981758 registers m_iDisguiseTargetIndex.",
                "Generate from spy disguise target state; zero before disguise state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_iDisguiseHealth",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf string xref table at 0x198175c registers m_iDisguiseHealth.",
                "Generate from spy disguise target health state; zero before disguise state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nDesiredDisguiseTeam",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_nDesiredDisguiseTeam.",
                "Generate from requested spy disguise state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_nDesiredDisguiseClass",
                "0x00",
                "0x04",
                "int32",
                "Updated BLES TF.elf DT_TFPlayerShared string table registers m_nDesiredDisguiseClass.",
                "Generate from requested spy disguise state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerShared",
                "m_flCloakMeter",
                "0xa4",
                "0x04",
                "float",
                "Updated BLES TF.elf string xref table at 0x10104ad8 registers m_flCloakMeter.",
                "Generate from spy cloak state; full meter is valid before gameplay starts."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_hItem",
                "0x1498",
                "0x04",
                "ehandle",
                "TF.elf _opd_FUN_00338928 registers held item handle.",
                "Generate from carried objective/item state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "tflocaldata",
                "0x0000",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00338928 registers the TF local-only data table.",
                "Generate as a native table anchor for local-player-only TF fields."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFLocalPlayerExclusive",
                "m_vecOrigin",
                "0x2b4",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00338eb0 registers local-player-exclusive origin.",
                "Generate from local player origin in addition to the base entity origin."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFLocalPlayerExclusive",
                "player_object_array_element",
                "0x0000",
                "send-proxy",
                "int32",
                "TF.elf _opd_FUN_00338eb0 registers player_object_array_element through a send proxy.",
                "Generate from local TF player object array state once that array is fully modeled."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFLocalPlayerExclusive",
                "_player_object_array_",
                "0x0000",
                "array-proxy",
                "array",
                "TF.elf _opd_FUN_00338eb0 registers the _player_object_array_ send array with four elements.",
                "Generate from local TF player object array state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "tfnonlocaldata",
                "0x0000",
                "nested-table",
                "datatable",
                "TF.elf _opd_FUN_00338928 registers the TF non-local data table.",
                "Generate as a native table anchor for remote-player TF fields."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFNonLocalPlayerExclusive",
                "m_vecOrigin",
                "0x2b4",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00338c48 registers non-local-player-exclusive origin.",
                "Generate from remote player origin in addition to the base entity origin."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayer",
                "m_iSpawnCounter",
                "0x14a8",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338928 registers spawn counter.",
                "Generate from respawn state; zero is valid before a spawn is committed."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatCharacter",
                "m_hActiveWeapon",
                "0xc78",
                "0x04",
                "ehandle",
                "TF.elf base-combat-character setup registers the active weapon entity handle.",
                "Generate from current weapon entity id/handle; zero is only a pre-spawn placeholder."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatCharacter",
                "m_hMyWeapons",
                "0xbb8",
                "0x04[0x30]",
                "ehandle-array",
                "TF.elf base-combat-character setup registers m_hMyWeapons[0] at 3000 and wraps it as a 0x30-entry handle array.",
                "Generate from player inventory handles; empty handles are only valid before inventory state is known."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BCCLocalPlayerExclusive",
                "m_flNextAttack",
                "0xb30",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0005d0d8 registers the local combat-character next-attack timer.",
                "Generate from weapon fire timing; zero is valid before a weapon is active."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatWeapon",
                "m_hOwner",
                "0x784",
                "0x04",
                "ehandle",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x101066f8 registers m_hOwner.",
                "Generate from owning player handle."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatWeapon",
                "m_iState",
                "0x7c0",
                "0x04",
                "int32",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x10106730 registers m_iState.",
                "Generate from weapon state; zero is valid before active equip state exists."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatWeapon",
                "m_iViewModelIndex",
                "0x79c",
                "0x04",
                "int32",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x10106768 registers m_iViewModelIndex.",
                "Generate from weapon model precache state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatWeapon",
                "m_iWorldModelIndex",
                "0x7a0",
                "0x04",
                "int32",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x101067a0 registers m_iWorldModelIndex.",
                "Generate from weapon model precache state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseCombatWeapon",
                "m_nViewModelIndex",
                "0x788",
                "0x04",
                "int32",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x10106958 registers m_nViewModelIndex.",
                "Generate from weapon model precache state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_flNextPrimaryAttack",
                "0x434",
                "0x04",
                "float",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x101223a8 registers m_flNextPrimaryAttack.",
                "Generate from weapon fire timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_flNextSecondaryAttack",
                "0x438",
                "0x04",
                "float",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x101223e0 registers m_flNextSecondaryAttack.",
                "Generate from weapon fire timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_flTimeWeaponIdle",
                "0x43c",
                "0x04",
                "float",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x10122418 registers m_flTimeWeaponIdle.",
                "Generate from weapon idle timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_iPrimaryAmmoType",
                "0x470",
                "0x04",
                "int32",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x10122568 registers m_iPrimaryAmmoType.",
                "Generate from weapon definition and ammo table."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_iSecondaryAmmoType",
                "0x474",
                "0x04",
                "int32",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x101225a0 registers m_iSecondaryAmmoType.",
                "Generate from weapon definition and ammo table."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_iClip1",
                "0x478",
                "0x04",
                "int32",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x101225d8 registers m_iClip1.",
                "Generate from active weapon clip state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalWeaponData",
                "m_iClip2",
                "0x47c",
                "0x04",
                "int32",
                "Updated BLES TF.elf local weapon-data descriptor table at 0x10122610 registers m_iClip2.",
                "Generate from active weapon secondary clip state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalActiveWeaponData",
                "m_bInReload",
                "0x440",
                "0x01",
                "bool",
                "Updated BLES TF.elf local active-weapon descriptor table at 0x10122450 registers m_bInReload.",
                "Generate from active weapon reload state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalActiveWeaponData",
                "m_bFireOnEmpty",
                "0x441",
                "0x01",
                "bool",
                "Updated BLES TF.elf local active-weapon descriptor table at 0x10122488 registers m_bFireOnEmpty.",
                "Generate from active weapon empty-fire state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_LocalActiveWeaponData",
                "m_flNextEmptySoundTime",
                "0x7a4",
                "0x04",
                "float",
                "Updated BLES TF.elf base-combat-weapon descriptor table at 0x10106a38 registers m_flNextEmptySoundTime.",
                "Generate from active weapon sound timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBase",
                "m_bLowered",
                "0x83b",
                "0x01",
                "bool",
                "Updated BLES TF.elf TF weapon-base descriptor table at 0x10106158 registers m_bLowered.",
                "Generate from TF weapon lowered/ready state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBase",
                "m_iReloadMode",
                "0x830",
                "0x04",
                "int32",
                "Updated BLES TF.elf TF weapon-base descriptor table at 0x10106190 registers m_iReloadMode.",
                "Generate from TF reload-mode state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBase",
                "m_bResetParity",
                "0x0000",
                "0x01",
                "bool",
                "Updated BLES TF.elf DT_TFWeaponBase string table registers m_bResetParity; concrete offset still needs a deeper table pass.",
                "Generate from TF weapon reset-event state once exact parity semantics are mapped."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBase",
                "m_bReloadedThroughAnimEvent",
                "0x8d6",
                "0x01",
                "bool",
                "Updated BLES TF.elf TF weapon-base descriptor table at 0x101061c8 registers m_bReloadedThroughAnimEvent.",
                "Generate from TF reload animation event state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBuilder",
                "m_iBuildState",
                "0x0000",
                "0x04",
                "int32",
                "Updated BLES TF.elf weapon-builder xref table at 0x1981c90 registers m_iBuildState.",
                "Generate from engineer builder weapon build-state; zero is valid before object placement starts."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBuilder",
                "BuilderLocalData",
                "0x0000",
                "nested-table",
                "datatable",
                "Updated BLES TF.elf weapon-builder xref table at 0x1981c94 registers BuilderLocalData / DT_BuilderLocalData.",
                "Generate as a table anchor before local builder fields."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BuilderLocalData",
                "m_iObjectType",
                "0x0000",
                "0x04",
                "int32",
                "Updated BLES TF.elf weapon-builder xref table at 0x1981c9c registers m_iObjectType.",
                "Generate from engineer selected object type."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BuilderLocalData",
                "m_hObjectBeingBuilt",
                "0x0000",
                "0x04",
                "ehandle",
                "Updated BLES TF.elf weapon-builder xref table at 0x1981ca4 registers m_hObjectBeingBuilt.",
                "Generate from active object placement/build entity handle."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponFlameThrower",
                "m_iWeaponState",
                "0x8d8",
                "0x04",
                "int32",
                "Updated BLES TF.elf flamethrower descriptor table at 0x1010512c registers m_iWeaponState.",
                "Generate from flamethrower spin/fire state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponFlameThrower",
                "m_bCritFire",
                "0x8dc",
                "0x01",
                "bool",
                "Updated BLES TF.elf flamethrower descriptor table at 0x10105164 registers m_bCritFire.",
                "Generate from flamethrower critical-fire state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_bHealing",
                "0x8dc",
                "0x01",
                "bool",
                "Updated BLES TF.elf medigun descriptor table at 0x10105248 registers m_bHealing.",
                "Generate from medigun heal-beam state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_bAttacking",
                "0x8dd",
                "0x01",
                "bool",
                "Updated BLES TF.elf medigun descriptor table at 0x10105280 registers m_bAttacking.",
                "Generate from medigun attack button/beam state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_bHolstered",
                "0x8f0",
                "0x01",
                "bool",
                "Updated BLES TF.elf medigun descriptor table at 0x101052b8 registers m_bHolstered.",
                "Generate from medigun holster state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_hHealingTarget",
                "0x8d8",
                "0x04",
                "ehandle",
                "Updated BLES TF.elf medigun descriptor table at 0x101052f0 registers m_hHealingTarget.",
                "Generate from current medigun heal target handle."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_flHealEffectLifetime",
                "0x8e8",
                "0x04",
                "float",
                "Updated BLES TF.elf medigun descriptor table at 0x10105328 registers m_flHealEffectLifetime.",
                "Generate from active medigun heal effect timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_flChargeLevel",
                "0x8f4",
                "0x04",
                "float",
                "Updated BLES TF.elf medigun descriptor table at 0x10105360 registers m_flChargeLevel.",
                "Generate from medigun ubercharge fraction/state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMedigun",
                "m_bChargeRelease",
                "0x8f1",
                "0x01",
                "bool",
                "Updated BLES TF.elf medigun descriptor table at 0x10105398 registers m_bChargeRelease.",
                "Generate from medigun charge-release state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFWeaponBottle",
                "m_bBroken",
                "0x0000",
                "0x01",
                "bool",
                "Updated BLES TF.elf weapon-bottle xref table at 0x1981da0 registers m_bBroken.",
                "Generate from bottle broken-state; false is valid for non-bottle or unbroken bottle state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponPipebombLauncher",
                "PipebombLauncherLocalData",
                "0x0000",
                "nested-table",
                "datatable",
                "Updated BLES TF.elf pipebomb-launcher table at 0x1982524 registers PipebombLauncherLocalData / DT_PipebombLauncherLocalData.",
                "Generate as a table anchor before pipebomb launcher local fields."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponPipebombLauncher",
                "m_iPipebombCount",
                "0x0000",
                "0x04",
                "int32",
                "Updated BLES TF.elf pipebomb-launcher xref table at 0x198252c registers m_iPipebombCount.",
                "Generate from active sticky/pipebomb count."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PipebombLauncherLocalData",
                "m_flChargeBeginTime",
                "0x8f4",
                "0x04",
                "float",
                "Updated BLES TF.elf pipebomb-launcher descriptor table at 0x10105520 registers m_flChargeBeginTime.",
                "Generate from pipebomb launcher charge timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponPistol",
                "PistolLocalData",
                "0x0000",
                "nested-table",
                "datatable",
                "Updated BLES TF.elf weapon-pistol table at 0x1982590 registers PistolLocalData / DT_PistolLocalData.",
                "Generate as a table anchor before pistol local fields."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PistolLocalData",
                "m_flSoonestPrimaryAttack",
                "0x8d8",
                "0x04",
                "float",
                "Updated BLES TF.elf pistol descriptor table at 0x101055c8 registers m_flSoonestPrimaryAttack.",
                "Generate from pistol fire-rate timing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMinigun",
                "m_iWeaponState",
                "0x8d8",
                "0x04",
                "int32",
                "Updated BLES TF.elf minigun descriptor table at 0x10105424 reuses the TF weapon-state sendprop.",
                "Generate from minigun spin/fire state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_WeaponMinigun",
                "m_bCritShot",
                "0x0000",
                "0x01",
                "bool",
                "Updated BLES TF.elf minigun xref table at 0x19822b4 registers m_bCritShot.",
                "Generate from minigun critical-shot state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iPing",
                "0x500",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iPing[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from connection latency; zero/low local latency is valid for local testing."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iScore",
                "0x584",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iScore[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from scoreboard state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iDeaths",
                "0x608",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iDeaths[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from scoreboard state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_bConnected",
                "0x68c",
                "0x01[0x21]",
                "bool-array",
                "TF.elf PlayerResource setup registers m_bConnected[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from GameManager/Source connection state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iTeam",
                "0x6b0",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iTeam[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from team assignment."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_bAlive",
                "0x734",
                "0x01[0x21]",
                "bool-array",
                "TF.elf PlayerResource setup registers m_bAlive[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from spawn/life state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iHealth",
                "0x758",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iHealth[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from player health."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iPlayerRating",
                "0x7dc",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iPlayerRating[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from ranking/rating when available."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_PlayerResource",
                "m_iRatingDelta",
                "0x860",
                "0x04[0x21]",
                "int32-array",
                "TF.elf PlayerResource setup registers m_iRatingDelta[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from ranking/rating delta when available."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iTotalScore",
                "0x964",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iTotalScore[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from scoreboard state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iCaptures",
                "0x9e8",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iCaptures[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from objective stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iDefenses",
                "0xa6c",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iDefenses[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from objective stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iDominations",
                "0xaf0",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iDominations[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from combat stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iRevenge",
                "0xb74",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iRevenge[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from combat stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iBuildingsDestroyed",
                "0xbf8",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iBuildingsDestroyed[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from engineer-object combat stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iHeadshots",
                "0xc7c",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iHeadshots[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from sniper stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iBackstabs",
                "0xd00",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iBackstabs[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from spy stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iHealPoints",
                "0xd84",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iHealPoints[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from medic stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iInvulns",
                "0xe08",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iInvulns[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from medic invulnerability stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iTeleports",
                "0xe8c",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iTeleports[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from teleporter stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iResupplyPoints",
                "0xf10",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iResupplyPoints[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from resupply stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iKillAssists",
                "0xf94",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iKillAssists[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from assist stats."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iMaxHealth",
                "0x1018",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iMaxHealth[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from class/state health limits."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFPlayerResource",
                "m_iPlayerClass",
                "0x109c",
                "0x04[0x21]",
                "int32-array",
                "TF.elf TFPlayerResource setup registers m_iPlayerClass[0] and wraps it as a 0x21-entry array.",
                "Generate per player slot from class selection state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Team",
                "m_iTeamNum",
                "0x4c0",
                "0x04",
                "int32",
                "TF.elf Team setup registers the team number field.",
                "Generate from team entity id."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Team",
                "m_iScore",
                "0x4ac",
                "0x04",
                "int32",
                "TF.elf Team setup registers team score.",
                "Generate from team scoreboard state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Team",
                "m_iRoundsWon",
                "0x4b0",
                "0x04",
                "int32",
                "TF.elf Team setup registers rounds-won score.",
                "Generate from team round history."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_Team",
                "m_szTeamname",
                "0x48c",
                "0x20",
                "string",
                "TF.elf Team setup registers the team name string.",
                "Generate RED/BLU/unassigned/spectator names from team number."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFTeam",
                "m_nFlagCaptures",
                "0x4c4",
                "0x04",
                "int32",
                "TF.elf TFTeam setup registers flag capture count.",
                "Generate from CTF objective state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFTeam",
                "m_iRole",
                "0x4c8",
                "0x04",
                "int32",
                "TF.elf TFTeam setup registers team role.",
                "Generate from TF team metadata; zero is valid until exact role semantics are mapped."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTimerToShowInHUD",
                "0x478",
                "0x04",
                "int32",
                "TF.elf objective-resource table registers m_iTimerToShowInHUD before control-point state arrays.",
                "Generate from the active objective/timer shown in the HUD."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iNumControlPoints",
                "0x47c",
                "0x04",
                "int32",
                "TF.elf objective-resource table registers m_iNumControlPoints.",
                "Generate from map objective layout, e.g. Dustbowl control-point count."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bPlayingMiniRounds",
                "0x484",
                "0x01",
                "bool",
                "TF.elf objective-resource table registers mini-round active state.",
                "Generate from multi-stage control-point map state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bControlPointsReset",
                "0x485",
                "0x01",
                "bool",
                "TF.elf objective-resource table registers control-point reset state.",
                "Generate from round reset state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iUpdateCapHudParity",
                "0x488",
                "0x04",
                "int32",
                "TF.elf objective-resource table registers capture-HUD parity.",
                "Generate from objective state changes so the client refreshes capture HUD data."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_vCPPositions",
                "0x490",
                "0x0c[0x08]",
                "vector3-array",
                "TF.elf objective-resource table registers m_vCPPositions[0] and wraps it as an 8-entry vector array.",
                "Generate from map control-point origins."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bCPIsVisible",
                "0x4f0",
                "0x01[0x08]",
                "bool-array",
                "TF.elf objective-resource table registers visible control-point flags.",
                "Generate from active stage/round objective visibility."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_flLazyCapPerc",
                "0x4f8",
                "0x04[0x08]",
                "float-array",
                "TF.elf objective-resource table registers lazy capture percentages.",
                "Generate from live capture progress."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTeamIcons",
                "0x538",
                "0x04[0x40]",
                "int32-array",
                "TF.elf objective-resource table registers team icon array.",
                "Generate from objective/team icon state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTeamOverlays",
                "0x638",
                "0x04[0x40]",
                "int32-array",
                "TF.elf objective-resource table registers team overlay array.",
                "Generate from objective/team overlay state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTeamReqCappers",
                "0x738",
                "0x04[0x40]",
                "int32-array",
                "TF.elf objective-resource table registers required cappers per team/objective.",
                "Generate from map objective rules."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_flTeamCapTime",
                "0x838",
                "0x04[0x40]",
                "float-array",
                "TF.elf objective-resource table registers team capture times.",
                "Generate from capture progress rules and active players in zone."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iPreviousPoints",
                "0x938",
                "0x04[0xc0]",
                "int32-array",
                "TF.elf objective-resource table registers previous control-point graph entries.",
                "Generate from the map objective graph."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bTeamCanCap",
                "0xc38",
                "0x01[0x40]",
                "bool-array",
                "TF.elf objective-resource table registers team can-cap flags.",
                "Generate from objective ownership and unlock state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTeamBaseIcons",
                "0xc78",
                "0x04[0x20]",
                "int32-array",
                "TF.elf objective-resource table registers team base icon array.",
                "Generate from map/team objective metadata."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iBaseControlPoints",
                "0xcf8",
                "0x04[0x20]",
                "int32-array",
                "TF.elf objective-resource table registers base control-point array.",
                "Generate from map/team objective metadata."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bInMiniRound",
                "0xd78",
                "0x01[0x08]",
                "bool-array",
                "TF.elf objective-resource table registers mini-round membership flags.",
                "Generate from active stage state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bWarnOnCap",
                "0xd80",
                "0x01[0x08]",
                "bool-array",
                "TF.elf objective-resource table registers warn-on-capture flags.",
                "Generate from objective alert state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iNumTeamMembers",
                "0x1580",
                "0x04[0x40]",
                "int32-array",
                "TF.elf objective-resource table registers team member counts.",
                "Generate from live team membership."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iCappingTeam",
                "0x1680",
                "0x04[0x08]",
                "int32-array",
                "TF.elf objective-resource table registers active capping team per point.",
                "Generate from live capture-zone state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iTeamInZone",
                "0x16a0",
                "0x04[0x08]",
                "int32-array",
                "TF.elf objective-resource table registers team-in-zone per point.",
                "Generate from live player positions in capture zones."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_bBlocked",
                "0x16c0",
                "0x01[0x08]",
                "bool-array",
                "TF.elf objective-resource table registers blocked capture flags.",
                "Generate from contested capture-zone state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_iOwner",
                "0x16c8",
                "0x04[0x08]",
                "int32-array",
                "TF.elf objective-resource table registers control-point owner team array.",
                "Generate from authoritative objective ownership."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_BaseTeamObjectiveResource",
                "m_pszCapLayoutInHUD",
                "0x1750",
                "0x20",
                "string",
                "TF.elf objective-resource table registers capture layout string for the HUD.",
                "Generate from active stage/control-point layout."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_bTimerPaused",
                "0x476",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_002595a0 registers the team round timer paused flag.",
                "Generate from round timer state; false is valid for active loading/setup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_flTimeRemaining",
                "0x478",
                "0x04",
                "float",
                "TF.elf _opd_FUN_002595a0 registers the remaining round/setup time.",
                "Generate from server round clock once gameplay simulation is active."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_flTimerEndTime",
                "0x47c",
                "0x04",
                "float",
                "TF.elf _opd_FUN_002595a0 registers the absolute timer end time.",
                "Generate alongside m_flTimeRemaining from the Source tick/timebase."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_nTimerMaxLength",
                "0x48c",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_002595a0 registers the maximum timer length.",
                "Generate from map mode/round rules."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_bIsDisabled",
                "0x480",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_002595a0 registers the timer disabled flag.",
                "Generate false when a visible gameplay timer is active."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_bShowInHUD",
                "0x481",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_002595a0 registers the HUD visibility flag.",
                "Generate true for normal TF round timers."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_nTimerLength",
                "0x484",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_002595a0 registers the current timer length.",
                "Generate from active round/setup timer configuration."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_nTimerInitialLength",
                "0x488",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_002595a0 registers initial timer length.",
                "Generate from map mode/round rules."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_bAutoCountdown",
                "0x490",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_002595a0 registers auto-countdown behavior.",
                "Generate from round timer behavior."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_nSetupTimeLength",
                "0x494",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_002595a0 registers setup time length.",
                "Generate from map mode and round setup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_nState",
                "0x498",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_002595a0 registers timer state.",
                "Generate from the native timer state machine."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamRoundTimer",
                "m_bStartPaused",
                "0x49c",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_002595a0 registers whether the timer starts paused.",
                "Generate from round timer initialization state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_vecOrigin",
                "0x14",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00335ea8 registers fire-bullets temp entity origin.",
                "Generate from weapon muzzle/source position when firing events are simulated."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_vecAngles[0]",
                "0x20",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00335ea8 registers fire-bullets pitch angle.",
                "Generate from weapon/view pitch."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_vecAngles[1]",
                "0x24",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00335ea8 registers fire-bullets yaw angle.",
                "Generate from weapon/view yaw."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_iWeaponID",
                "0x2c",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00335ea8 registers fire-bullets weapon id.",
                "Generate from active TF weapon id."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_iMode",
                "0x30",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00335ea8 registers fire-bullets mode.",
                "Generate from primary/secondary fire mode."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_iSeed",
                "0x34",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00335ea8 registers spread/random seed.",
                "Generate deterministically from command number/tick for replicated spread."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_iPlayer",
                "0x10",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00335ea8 registers firing player index.",
                "Generate from source player slot."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEFireBullets",
                "m_flSpread",
                "0x38",
                "0x04",
                "float",
                "TF.elf _opd_FUN_00335ea8 registers weapon spread.",
                "Generate from active weapon accuracy/spread state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEPlayerAnimEvent",
                "m_iPlayerIndex",
                "0x10",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338b50 registers player animation event owner index.",
                "Generate from player animation event state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEPlayerAnimEvent",
                "m_iEvent",
                "0x14",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338b50 registers player animation event id.",
                "Generate from Source animation event id."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TEPlayerAnimEvent",
                "m_nData",
                "0x18",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338b50 registers player animation event data.",
                "Generate from Source animation event payload data."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_vecRagdollOrigin",
                "0xb3c",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00338d00 registers TF ragdoll origin.",
                "Generate from player death position when ragdoll entities exist."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_iPlayerIndex",
                "0xb48",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338d00 registers the ragdoll owner player index.",
                "Generate from source player slot."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_vecForce",
                "0x4b0",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00338d00 registers ragdoll impulse force.",
                "Generate from damage impulse."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_vecRagdollVelocity",
                "0xb30",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_00338d00 registers ragdoll velocity.",
                "Generate from player velocity at death."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_nForceBone",
                "0x4bc",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338d00 registers ragdoll force bone.",
                "Generate from hitgroup/bone when known."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_bGib",
                "0xb51",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00338d00 registers a bool at 0xb51; the ELF string table resolves it as m_bGib.",
                "Generate from explosive/gib death state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_bBurning",
                "0xb52",
                "0x01",
                "bool",
                "TF.elf _opd_FUN_00338d00 registers burning ragdoll state.",
                "Generate from burning death state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFRagdoll",
                "m_iTeam",
                "0xb54",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_00338d00 registers ragdoll team.",
                "Generate from player team at death."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "m_vecOrigin[0]",
                "0x10",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0034c228 registers TF explosion origin x.",
                "Generate from explosion origin."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "m_vecOrigin[1]",
                "0x14",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0034c228 registers TF explosion origin y.",
                "Generate from explosion origin."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "m_vecOrigin[2]",
                "0x18",
                "0x04",
                "float",
                "TF.elf _opd_FUN_0034c228 registers TF explosion origin z.",
                "Generate from explosion origin."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "m_vecNormal",
                "0x1c",
                "0x0c",
                "vector3",
                "TF.elf _opd_FUN_0034c228 registers explosion normal vector.",
                "Generate from surface normal or default up vector."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "m_iWeaponID",
                "0x28",
                "0x04",
                "int32",
                "TF.elf _opd_FUN_0034c228 registers explosion weapon id.",
                "Generate from causing weapon id."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TETFExplosion",
                "entindex",
                "0x0000",
                "send-proxy",
                "int32",
                "TF.elf _opd_FUN_0034c228 registers entindex through the temp-entity send proxy.",
                "Generate from causing entity index."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectSentrygun",
                "m_iUpgradeLevel",
                "0xe18",
                "0x04",
                "int32",
                "TF.elf ObjectSentrygun setup registers the sentry upgrade level.",
                "Generate from engineer buildable state when sentry objects exist."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectSentrygun",
                "m_iAmmoShells",
                "0xe20",
                "0x04",
                "int32",
                "TF.elf ObjectSentrygun setup registers shell ammo.",
                "Generate from sentry ammo state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectSentrygun",
                "m_iAmmoRockets",
                "0xe28",
                "0x04",
                "int32",
                "TF.elf ObjectSentrygun setup registers rocket ammo.",
                "Generate from sentry ammo state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectSentrygun",
                "m_iState",
                "0xe14",
                "0x04",
                "int32",
                "TF.elf ObjectSentrygun setup registers object state.",
                "Generate from buildable active/building/carried state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectSentrygun",
                "m_iUpgradeMetal",
                "0xe2c",
                "0x04",
                "int32",
                "TF.elf ObjectSentrygun setup registers upgrade metal.",
                "Generate from sentry upgrade-progress state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectTeleporter",
                "m_iState",
                "0xe14",
                "0x04",
                "int32",
                "TF.elf ObjectTeleporter setup registers object state.",
                "Generate from teleporter active/building/recharging state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectTeleporter",
                "m_flRechargeTime",
                "0xe1c",
                "0x04",
                "float",
                "TF.elf ObjectTeleporter setup registers recharge time.",
                "Generate from teleporter cooldown state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectTeleporter",
                "m_iTimesUsed",
                "0xe20",
                "0x04",
                "int32",
                "TF.elf ObjectTeleporter setup registers use count.",
                "Generate from teleporter usage state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_ObjectTeleporter",
                "m_flYawToExit",
                "0xe24",
                "0x04",
                "float",
                "TF.elf ObjectTeleporter setup registers exit yaw.",
                "Generate from teleporter orientation state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_iRoundState",
                "0x28",
                "0x04",
                "int32",
                "TF.elf TeamplayRoundBasedRules setup registers the round state field.",
                "Generate from match state so the client can transition loading/setup/active round correctly."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_bInWaitingForPlayers",
                "0x38",
                "0x01",
                "bool",
                "TF.elf TeamplayRoundBasedRules setup registers the waiting-for-players flag.",
                "Generate from pre-match population/readiness state so the client can show correct join/setup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_iWinningTeam",
                "0x30",
                "0x04",
                "int32",
                "TF.elf TeamplayRoundBasedRules setup registers the winning-team field.",
                "Generate from current round winner; zero while no winner is decided."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_bInOvertime",
                "0x2c",
                "0x01",
                "bool",
                "TF.elf TeamplayRoundBasedRules setup registers the overtime flag.",
                "Generate from timer/objective state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_bInSetup",
                "0x2d",
                "0x01",
                "bool",
                "TF.elf TeamplayRoundBasedRules setup registers the setup flag.",
                "Generate from map/mode setup state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_bSwitchedTeamsThisRound",
                "0x2e",
                "0x01",
                "bool",
                "TF.elf TeamplayRoundBasedRules setup registers the switched-teams-this-round flag.",
                "Generate from scramble/swap state; false for normal joins."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_bAwaitingReadyRestart",
                "0x39",
                "0x01",
                "bool",
                "TF.elf TeamplayRoundBasedRules setup registers the ready-restart wait flag.",
                "Generate from tournament/ready restart state."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_flRestartRoundTime",
                "0x3c",
                "0x04",
                "float",
                "TF.elf TeamplayRoundBasedRules setup registers round restart time.",
                "Generate from round timer/restart scheduling."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_flMapResetTime",
                "0x40",
                "0x04",
                "float",
                "TF.elf TeamplayRoundBasedRules setup registers map reset time.",
                "Generate from map reset scheduling."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_flNextRespawnWave",
                "0x44",
                "0x04[0x20]",
                "float-array",
                "TF.elf TeamplayRoundBasedRules setup registers m_flNextRespawnWave[0] and wraps it as a 0x20-entry array.",
                "Generate per team/player bucket from respawn wave scheduling."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TeamplayRoundBasedRules",
                "m_TeamRespawnWaveTimes",
                "0xc4",
                "0x04[0x20]",
                "float-array",
                "TF.elf TeamplayRoundBasedRules setup registers team respawn wave time array.",
                "Generate per team from game mode respawn settings."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFGameRules",
                "m_nGameType",
                "0x15c",
                "0x04",
                "int32",
                "TF.elf TFGameRules setup registers the game-type field before red/blue goal strings.",
                "Generate from map/mode profile, e.g. ctf_ maps as capture-the-flag."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFGameRules",
                "m_pszTeamGoalStringRed",
                "0x160",
                "0x100",
                "string",
                "TF.elf TFGameRules setup registers the red team goal string.",
                "Generate from current map/objective text when available."),
            new Tf2Ps3SourceEntityFieldContract(
                "DT_TFGameRules",
                "m_pszTeamGoalStringBlue",
                "0x260",
                "0x100",
                "string",
                "TF.elf TFGameRules setup registers the blue team goal string.",
                "Generate from current map/objective text when available.")
        ];
    }

    private static Tf2Ps3SourcePhaseContract BuildPhaseContract(JsonElement root)
    {
        var summary = root.GetProperty("Summary");
        return new Tf2Ps3SourcePhaseContract(
            summary.GetProperty("ActiveSourceFlowCount").GetInt32(),
            summary.GetProperty("LongGameplaySessionCount").GetInt32(),
            summary.GetProperty("SourcePacketCount").GetInt32(),
            summary.GetProperty("ClientToServerPacketCount").GetInt32(),
            summary.GetProperty("ServerToClientPacketCount").GetInt32(),
            summary.GetProperty("FilesWithSteadyGameplayWindowCount").GetInt32(),
            "Native server must produce setup, loading/MOTD, and steady gameplay traffic without replaying captured packets. Current generated server covers setup/object graph scaffolding, native snapshot frame headers, LZSS/fragment wrapping, core TF entity deltas, and session/profile/player-state driven snapshot values; the remaining gap is authoritative Source world simulation for movement, projectiles, buildings, scoring, and map logic.");
    }

    private static Tf2Ps3SourceRemainingUnknown[] BuildRemainingUnknowns(
        JsonElement builderRoot,
        JsonElement embeddedRoot,
        JsonElement phasesRoot)
    {
        var unknowns = new List<Tf2Ps3SourceRemainingUnknown>();
        var recoveredDeltaAddresses = new HashSet<string>(StringComparer.Ordinal)
        {
            "00910d48",
            "00911140",
            "009113a0"
        };
        var recoveredTransportAddresses = new HashSet<string>(StringComparer.Ordinal)
        {
            "0039f860",
            "008bc978",
            "008bd158",
            "00a61150"
        };
        foreach (var function in builderRoot.GetProperty("Functions").EnumerateArray())
        {
            var address = ReadString(function, "Address");
            if (recoveredDeltaAddresses.Contains(address) || recoveredTransportAddresses.Contains(address))
            {
                continue;
            }

            var schemaId = ReadString(function, "SchemaMessageId");
            if (schemaId.Length == 0)
            {
                unknowns.Add(new Tf2Ps3SourceRemainingUnknown(
                    "tf-elf-source-builder",
                    address,
                    ReadString(function, "SchemaName"),
                    ReadString(function, "Conclusion")));
            }
            else if (ReadBool(function, "UsesFragmentOrCompressionGate"))
            {
                unknowns.Add(new Tf2Ps3SourceRemainingUnknown(
                    "tf-elf-source-builder",
                    ReadString(function, "Address"),
                    $"{ReadString(function, "SchemaName")} compression/wrap path",
                    "Exact LZSS/wrap trigger and lower reliable-peer channel fields must be fully mapped before gameplay snapshots are native-complete."));
            }
        }

        var phaseSummary = phasesRoot.GetProperty("Summary");
        if (phaseSummary.GetProperty("FilesWithSteadyGameplayWindowCount").GetInt32() > 0)
        {
            unknowns.Add(new Tf2Ps3SourceRemainingUnknown(
                "tf-elf-source-gameplay",
                "live-source-simulation-state",
                "steady gameplay snapshot/entity semantics",
                "Transport, snapshot header, compression/wrap, object records, and core TF entity fields are mapped; generated snapshots now consume session/profile/player state, but a playable server still needs authoritative Source world simulation feeding movement, projectile, building, scoring, and map objective state into those fields."));
        }

        return unknowns
            .OrderBy(static item => item.Category, StringComparer.Ordinal)
            .ThenBy(static item => item.AddressOrKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Tf2Ps3SourceObservedValue> CountValues(JsonElement file, string property)
    {
        if (!file.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var value in values.EnumerateArray())
        {
            yield return new Tf2Ps3SourceObservedValue(ReadString(value, "Value"), ReadInt(value, "Count"));
        }
    }

    private static IEnumerable<JsonElement> ParticipantSummaries(JsonElement file)
    {
        if (!file.TryGetProperty("ParticipantSummaries", out var summaries) || summaries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var summary in summaries.EnumerateArray())
        {
            yield return summary;
        }
    }

    private static bool HasRoleEvidence(JsonElement participant, string role)
    {
        return role switch
        {
            "FrozenStateObject" => ReadInt(participant, "FrozenStateObjectRecordCount") > 0,
            "PlayerObject" => ReadInt(participant, "PlayerObjectRecordCount") > 0,
            "PlayerDescriptor" => ReadInt(participant, "PlayerDescriptorRecordCount") > 0,
            "PlayerStateLink" => ReadInt(participant, "OutboundLinkCount") > 0 || ReadInt(participant, "InboundLinkCount") > 0,
            _ => false
        };
    }

    private static Tf2Ps3NativeFieldSemantics NativeFieldSemantics(string messageId, int index, string expression)
    {
        if (messageId == "0x44")
        {
            return index switch
            {
                0 => new("sentinel", "Native Source messages begin with -1 before the message id."),
                1 => new("message-id", "0x44 player summary / scoreboard update."),
                2 => new("summary-header-value", "Value returned by the player-summary object vtable slot 0x0c before per-player records."),
                3 => new("player-slot-index", "Loop index for the player entry currently being serialized."),
                4 => new("player-display-name", "Display name from the active player object vtable slot 0x18."),
                5 => new("player-score-or-stat", "32-bit player resource stat read from the resolved player resource object at offset 0x2c."),
                6 => new("player-float-branch-live-value", "Branch option: writes the live float value from the player object's nested object when vtable slot 0x70 is false."),
                7 => new("player-float-branch-default-value", "Branch option: writes the fallback global float when vtable slot 0x70 is true. Runtime emits exactly one float after each player-score-or-stat."),
                _ => Tf2Ps3NativeFieldSemantics.Unknown
            };
        }

        if (messageId == "0x45")
        {
            return index switch
            {
                0 => new("sentinel", "Native Source messages begin with -1 before the message id."),
                1 => new("message-id", "0x45 resource string table / downloadable resource update."),
                2 => new("resource-count", "Number of resource entries accepted by the native filter, clamped/tested against 0x100."),
                3 => new("resource-name", "Resource name/path from the resource iterator vtable slot 0x14."),
                4 => new("resource-entry-value-branch-path", "Branch option: writes the resource mount/base path or fallback path when the entry is not flagged with 0x20."),
                5 => new("resource-entry-value-branch-download-classification", "Branch option: writes the download classification fallback when the selected path is non-empty and does not match the expected prefix."),
                6 => new("resource-entry-value-branch-default-classification", "Branch option: writes the default classification for remaining flagged resources. Runtime emits exactly one value string after each resource-name."),
                _ => Tf2Ps3NativeFieldSemantics.Unknown
            };
        }

        if (messageId == "0x49")
        {
            return index switch
            {
                0 => new("sentinel", "Native Source messages begin with -1 before the message id."),
                1 => new("message-id", "0x49 server-info / map session descriptor."),
                2 => new("server-info-version", "Constant 8 in TF.elf."),
                3 => new("server-name", "String returned by session object vtable slot 0x34."),
                4 => new("map-name", "String returned by session object vtable slot 0x38."),
                5 => new("game-directory", "Game/mod directory copied into a 0x104-byte stack buffer."),
                6 => new("server-provider-or-description", "String returned by the global server object vtable slot 0x2c."),
                7 => new("listen-port-or-network-short", "16-bit global server/network value written before player counts."),
                8 => new("current-player-count", "Session vtable slot 0x0c."),
                9 => new("max-player-count", "Session vtable slot 0x18, capped by configured max when not dedicated/listen."),
                10 => new("bot-or-reserved-count", "Session vtable slot 0x14."),
                11 => new("server-variant-branch-plain", "Branch option: writes 0x6c when session vtable slots 0x6c and 0x5c are false."),
                12 => new("server-variant-branch-listen", "Branch option: writes 100 when vtable slot 0x6c is false and slot 0x5c is true."),
                13 => new("server-variant-branch-secure", "Branch option: writes 0x70 when session vtable slot 0x6c is true."),
                14 => new("platform-code", "0x6c constant written after exactly one server-variant branch byte."),
                15 => new("password-or-private-flag", "Boolean derived from session vtable slot 0x70."),
                16 => new("not-dedicated-or-client-visible-flag", "Inverse of session vtable slot 0x6c."),
                17 => new("connection-address", "Formatted 0x28-byte address string built from globals and session state."),
                _ => Tf2Ps3NativeFieldSemantics.Unknown
            };
        }

        return expression switch
        {
            "0xffffffff" => new("sentinel", "Native Source messages begin with -1 before the message id."),
            _ => Tf2Ps3NativeFieldSemantics.Unknown
        };
    }

    private static string FieldStatus(JsonElement write, string nativeFieldName)
    {
        var role = ReadString(write, "FieldRole");
        var value = ReadString(write, "ValueExpression");
        if (nativeFieldName.Length > 0)
        {
            return "recovered";
        }

        if (role is "unresolved-expression" || value.Length == 0)
        {
            return "unresolved-expression";
        }

        return value.StartsWith("param_", StringComparison.Ordinal)
            || value.Contains("*(int *)", StringComparison.Ordinal)
            || value.Contains("piVar", StringComparison.Ordinal)
                ? "dynamic-expression"
                : "recovered";
    }

    private static string MessageName(string messageId)
    {
        return messageId switch
        {
            "0x44" => "native-player-summary",
            "0x45" => "native-resource-string-table",
            "0x49" => "native-server-info",
            _ => "native-source-message"
        };
    }

    private static string NativeGenerationGuidance(string messageId)
    {
        return messageId switch
        {
            "0x44" => "Generate with TF.elf bit writers for player summary/scoreboard state; do not forward PC Source A2S bytes.",
            "0x45" => "Generate map/resource string table from mounted PS3 TF filesystem and current map.",
            "0x49" => "Generate server-info/map session descriptor after the second client Source packet, before object-state batches.",
            _ => "Generate from native field contract."
        };
    }

    private static string ReadString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
    }

    private static int? ReadNullableInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static bool ReadBool(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }
}

public sealed record Tf2Ps3SourceFieldContractReport(
    string Status,
    string Note,
    Tf2Ps3SourceFieldContractInputs Inputs,
    Tf2Ps3SourceFieldContractSummary Summary,
    Tf2Ps3NativeSourceMessageContract[] NativeMessages,
    Tf2Ps3SourceDeltaContract[] SourceDeltaContracts,
    Tf2Ps3SourceEntityFieldContract[] SourceEntityFieldContracts,
    Tf2Ps3EmbeddedObjectContract[] EmbeddedObjectRecords,
    Tf2Ps3SourcePhaseContract PhaseContract,
    Tf2Ps3SourceRemainingUnknown[] RemainingUnknowns);

public sealed record Tf2Ps3SourceFieldContractInputs(
    string BuilderMap,
    string EmbeddedObjects,
    string GameplayPhases);

public sealed record Tf2Ps3SourceFieldContractSummary(
    int NativeMessageContractCount,
    int NativeMessagesWithKnownFields,
    int RecoveredNativeFieldCount,
    int UnresolvedNativeFieldCount,
    int DeltaContractCount,
    int SourceEntityFieldContractCount,
    int EmbeddedObjectContractCount,
    int ObservedEmbeddedRecordCount,
    int LongGameplaySessionCount,
    int RemainingUnknownCount);

public sealed record Tf2Ps3NativeSourceMessageContract(
    string Address,
    string MessageId,
    string MessageName,
    string SchemaName,
    string SchemaBuffer,
    bool UsesBitPayloadSidecar,
    bool UsesFragmentOrCompressionGate,
    int KnownFieldCount,
    Tf2Ps3NativeSourceField[] Fields,
    string NativeGenerationGuidance);

public sealed record Tf2Ps3NativeSourceField(
    int Index,
    string Kind,
    int? FixedBitWidth,
    string ValueExpression,
    string FieldRole,
    string NativeFieldName,
    string NativeFieldMeaning,
    string Status);

public sealed record Tf2Ps3SourceDeltaContract(
    string Address,
    string SchemaName,
    string NativeName,
    string SourceProperty,
    string Semantics,
    string LengthExpression,
    string PeerAddressExpression,
    string NativeGenerationGuidance);

public sealed record Tf2Ps3SourceEntityFieldContract(
    string SendTable,
    string SourceProperty,
    string Offset,
    string Width,
    string ValueKind,
    string TfElfEvidence,
    string NativeGenerationGuidance);

public readonly record struct Tf2Ps3NativeFieldSemantics(string Name, string Meaning)
{
    public static Tf2Ps3NativeFieldSemantics Unknown { get; } = new("", "");
}

public sealed record Tf2Ps3EmbeddedObjectContract(
    string Marker,
    string Role,
    string FieldLayout,
    int ObservedCount,
    Tf2Ps3SourceObservedValue[] TopObjectIds,
    Tf2Ps3SourceObservedValue[] TopDisplayNames,
    string NativeGenerationGuidance);

public sealed record Tf2Ps3SourceObservedValue(string Value, int Count);

public sealed record Tf2Ps3SourcePhaseContract(
    int ActiveSourceFlowCount,
    int LongGameplaySessionCount,
    int SourcePacketCount,
    int ClientToServerPacketCount,
    int ServerToClientPacketCount,
    int FilesWithSteadyGameplayWindowCount,
    string NativeGenerationGuidance);

public sealed record Tf2Ps3SourceRemainingUnknown(
    string Category,
    string AddressOrKey,
    string Role,
    string NextReverseEngineeringTarget);
