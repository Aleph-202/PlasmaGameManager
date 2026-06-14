using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceUsercmdQueueRecordReducer
{
    private const int OfficialRecordStrideBytes = 0x58;
    private const string QueueInitFunction = "00a2a1a8";
    private const string QueueInitAlternateFunction = "00a2a360";
    private const string QueueDrainFunction = "00a2ae20";
    private const string QueueConsumerFunction = "00a2b060";
    private const string QueueInsertFunction = "00a2b470";
    private const string QueueGrowFunction = "00a2b960";
    private const string QueueShiftRightFunction = "00a2bb10";
    private const string QueueCopyInsertFunction = "00a2bb78";
    private const string QueueShiftLeftFunction = "00a2bc18";
    private const string QueueRemoveFunction = "00a2bc88";
    private const string QueueInsertThunkFunction = "0039c890";
    private const string QueueInsertFanoutFunction = "007ec208";
    private const string DeltaWriterFunction = "0080ad88";

    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\*\[\]][\w\s\*\[\]]*?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceUsercmdQueueRecordReport> ReduceAsync(
        string cExportPath,
        string pcapBoundaryProbePath,
        string outputPath,
        string? serverDllUsercmdDecoderPath = null,
        string? pcapRecordCandidateProbePath = null,
        string? tfElfBinaryPath = null)
    {
        var functions = File.Exists(cExportPath)
            ? ExtractFunctions(await File.ReadAllLinesAsync(cExportPath))
                .GroupBy(static function => function.Address, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal)
            : new Dictionary<string, ExportedFunction>(StringComparer.Ordinal);

        functions.TryGetValue(QueueDrainFunction, out var queueDrain);
        functions.TryGetValue(QueueConsumerFunction, out var queueConsumer);
        functions.TryGetValue(QueueInitFunction, out var queueInit);
        functions.TryGetValue(QueueInitAlternateFunction, out var queueInitAlternate);
        functions.TryGetValue(QueueInsertFunction, out var queueInsert);
        functions.TryGetValue(QueueGrowFunction, out var queueGrow);
        functions.TryGetValue(QueueShiftRightFunction, out var queueShiftRight);
        functions.TryGetValue(QueueCopyInsertFunction, out var queueCopyInsert);
        functions.TryGetValue(QueueShiftLeftFunction, out var queueShiftLeft);
        functions.TryGetValue(QueueRemoveFunction, out var queueRemove);
        functions.TryGetValue(QueueInsertThunkFunction, out var queueInsertThunk);
        functions.TryGetValue(QueueInsertFanoutFunction, out var queueInsertFanout);
        functions.TryGetValue(DeltaWriterFunction, out var deltaWriter);

        var queueDrainBody = queueDrain?.Body ?? "";
        var queueConsumerBody = queueConsumer?.Body ?? "";
        var queueInitBody = queueInit?.Body ?? "";
        var queueInitAlternateBody = queueInitAlternate?.Body ?? "";
        var queueInsertBody = queueInsert?.Body ?? "";
        var queueGrowBody = queueGrow?.Body ?? "";
        var queueShiftRightBody = queueShiftRight?.Body ?? "";
        var queueCopyInsertBody = queueCopyInsert?.Body ?? "";
        var queueShiftLeftBody = queueShiftLeft?.Body ?? "";
        var queueRemoveBody = queueRemove?.Body ?? "";
        var queueInsertThunkBody = queueInsertThunk?.Body ?? "";
        var queueInsertFanoutBody = queueInsertFanout?.Body ?? "";
        var deltaWriterBody = deltaWriter?.Body ?? "";

        var queueInitRecovered = FunctionInitializesQueue(queueInitBody)
            && FunctionInitializesQueue(queueInitAlternateBody);
        var queueBaseRecovered = queueDrainBody.Contains("param_1 + 0x82b4", StringComparison.Ordinal);
        var queueCountRecovered = queueDrainBody.Contains("param_1 + 0x82c0", StringComparison.Ordinal);
        var recordStrideRecovered = queueDrainBody.Contains("iVar8 = iVar8 + 0x58", StringComparison.Ordinal);
        var queueInsertRecovered = queueInsertBody.Contains("_opd_FUN_00a2bb78", StringComparison.Ordinal)
            && queueInsertBody.Contains("param_3 == 0", StringComparison.Ordinal)
            && queueInsertBody.Contains("*puVar4 = param_1[0x20ab]", StringComparison.Ordinal);
        var directCopyInsertRecovered = queueInsertRecovered
            && queueCopyInsertBody.Contains("FUN_00871708", StringComparison.Ordinal)
            && queueCopyInsertBody.Contains("0x58", StringComparison.Ordinal);
        var syntheticRecordOutboundPathRecovered = queueInsertBody.Contains("FUN_0086f8f8(puVar4,local_a8,aiStack_c0)", StringComparison.Ordinal)
            && queueInsertBody.Contains("+ 0x5c", StringComparison.Ordinal);
        var vectorGrowthRecovered = queueGrowBody.Contains("_opd_FUN_00a2b7b0", StringComparison.Ordinal)
            && queueGrowBody.Contains("param_1[3]", StringComparison.Ordinal)
            && queueGrowBody.Contains("param_1[4] = *param_1", StringComparison.Ordinal);
        var vectorInsertShiftRecovered = queueShiftRightBody.Contains("(param_2 + param_3) * 0x58", StringComparison.Ordinal)
            && queueShiftRightBody.Contains("param_2 * 0x58", StringComparison.Ordinal);
        var queueRemoveRecovered = queueRemoveBody.Contains("_opd_FUN_00a2bc18", StringComparison.Ordinal)
            && queueRemoveBody.Contains("param_1[3] = param_1[3] - param_3", StringComparison.Ordinal)
            && queueShiftLeftBody.Contains("param_2 * 0x58", StringComparison.Ordinal);
        var insertThunkOnlyRecovered = queueInsertThunkBody.Contains("_opd_FUN_00a2b470", StringComparison.Ordinal);
        var queueInsertFanoutRecovered = queueInsertFanoutBody.Contains("FUN_0039c890", StringComparison.Ordinal)
            && queueInsertFanoutBody.Contains("*param_3 + 0x10", StringComparison.Ordinal)
            && queueInsertFanoutBody.Contains("*param_3 + 0x14", StringComparison.Ordinal)
            && queueInsertFanoutBody.Contains("*param_3 + 8", StringComparison.Ordinal)
            && queueInsertFanoutBody.Contains("*piVar1 + 0x6c", StringComparison.Ordinal);
        var concreteUdpToQueueInsertCallerRecovered = false;
        var previousRecordDeltaBaseRecovered =
            queueDrainBody.Contains("puVar3 = local_a0", StringComparison.Ordinal)
            && queueDrainBody.Contains("puVar3 = (uint *)(iVar8 + *piVar4)", StringComparison.Ordinal);
        var writerCallRecovered = queueDrainBody.Contains("FUN_0086f8f8", StringComparison.Ordinal)
            && deltaWriterBody.Length > 0;
        var localBitreaderRecovered = queueConsumerBody.Contains("FUN_00870968(auStack_80,auStack_177d0,96000,0,-1)", StringComparison.Ordinal)
            || queueConsumerBody.Contains("FUN_00870968(auStack_80,auStack_177d0,96000,0,0xffffffffffffffff)", StringComparison.Ordinal);
        var ownerRouterRecovered = queueConsumerBody.Contains("_opd_FUN_008722a0", StringComparison.Ordinal);
        var category5DispatchRecovered = queueConsumerBody.Contains("_opd_FUN_00872460(iVar9,5)", StringComparison.Ordinal)
            && queueConsumerBody.Contains("+ 0x98", StringComparison.Ordinal);

        var deltaFields = BuildDeltaFields(deltaWriterBody);
        var serverDllFieldMap = BuildServerDllFieldMap(serverDllUsercmdDecoderPath);
        var serverDllMappings = BuildServerDllMappings(deltaFields, serverDllFieldMap);
        var directServerDllMappings = serverDllMappings.Count(static mapping => mapping.MatchStatus == "direct-semantic-match");
        var candidateServerDllMappings = serverDllMappings.Count(static mapping => mapping.MatchStatus == "candidate-bridge-required");
        var pcapProbe = File.Exists(pcapBoundaryProbePath)
            ? BuildPcapProbe(pcapBoundaryProbePath)
            : new Tf2Ps3SourceUsercmdQueueRecordPcapProbe(
                false,
                0,
                0,
                0,
                0,
                [],
                [],
                [],
                "pcap boundary probe missing");
        var recordCandidateProbe = BuildRecordCandidateProbe(pcapRecordCandidateProbePath);
        var descriptorScan = BuildDescriptorScan(tfElfBinaryPath);

        var queueRecordLayerRecovered = queueBaseRecovered
            && queueCountRecovered
            && recordStrideRecovered
            && queueInitRecovered
            && queueInsertRecovered
            && directCopyInsertRecovered
            && syntheticRecordOutboundPathRecovered
            && vectorGrowthRecovered
            && vectorInsertShiftRecovered
            && queueRemoveRecovered
            && previousRecordDeltaBaseRecovered
            && writerCallRecovered
            && deltaFields.Length >= 10;
        var nativeSourceInputReady = false;
        var gates = new[]
        {
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "queue-base-and-count-recovered",
                queueBaseRecovered && queueCountRecovered ? "proven" : "missing",
                "00a2ae20",
                "The client-side official command queue must be located before markerless input can be mapped into native records."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "queue-record-stride-recovered",
                recordStrideRecovered ? "proven" : "missing",
                "0x58-byte records",
                "00a2ae20 advances by 0x58 bytes per queued command record."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "queue-init-insert-remove-mechanics-recovered",
                queueInitRecovered && queueInsertRecovered && directCopyInsertRecovered && vectorGrowthRecovered && vectorInsertShiftRecovered && queueRemoveRecovered
                    ? "proven"
                    : "missing",
                "00a2a1a8/00a2a360, 00a2b470, 00a2b960, 00a2bb10, 00a2bb78, 00a2bc88",
                "TF.elf initializes the queue, grows it by 0x58-byte slots, copies caller records into it, and removes consumed records after 00a2ae20 drains them."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "queue-insert-thunk-and-fanout-callers-recovered",
                insertThunkOnlyRecovered && queueInsertFanoutRecovered && descriptorScan.QueueInsertDescriptorRecovered && descriptorScan.QueueInsertDescriptorReferences.Length == 0 && !concreteUdpToQueueInsertCallerRecovered
                    ? "proven"
                    : "candidate",
                "007ec208 -> FUN_0039c890 -> 00a2b470; OPD descriptor 018fced8",
                "The C export exposes a player fanout caller into the queue-insert thunk, but the TF.elf data scan still finds no receive/parser caller from raw markerless UDP into this queue."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "previous-record-delta-base-recovered",
                previousRecordDeltaBaseRecovered ? "proven" : "missing",
                "puVar3 local zero/current previous record",
                "0080ad88 receives current and previous records, so the wire payload is a delta stream, not a raw CUserCmd array."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "delta-writer-recovered",
                writerCallRecovered && deltaFields.Length >= 10 ? "proven" : "missing",
                "0080ad88",
                "The official TF.elf writer compares record fields and writes changed bits plus fixed-width field values."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "category5-consumer-uses-local-bitreader",
                localBitreaderRecovered && ownerRouterRecovered && category5DispatchRecovered ? "proven" : "missing",
                "00a2b060 -> 00a2ae20 -> 008722a0 -> category 5",
                "Category 5 usercmd dispatch consumes the local queue-delta bitreader before passing the command to the handler."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "pcap-hard-markerless-record-length-candidates-audited",
                pcapProbe.Available && pcapProbe.RawRecordLengthCandidateCount == 0 && pcapProbe.RawRecordLengthMultipleCandidateCount == 0
                    ? "proven"
                    : "candidate",
                pcapBoundaryProbePath,
                "The hard markerless packet bodies are audited against the 0x58 queue stride. Exact multiples are only candidate evidence because TF.elf proves 00a2ae20 writes a delta bitstream, not raw records."),
            new Tf2Ps3SourceUsercmdQueueRecordGate(
                "native-source-input-ready",
                "missing",
                "live/PCAP implementation gate",
                "Native Source input is not ready until hard markerless packets are proven to fill these command records or another TF.elf input queue path.")
        };
        var queueFunctions = BuildQueueFunctions(
            queueInit,
            queueInitAlternate,
            queueInsert,
            queueGrow,
            queueShiftRight,
            queueCopyInsert,
            queueShiftLeft,
            queueRemove,
            queueDrain,
            queueConsumer,
            queueInsertThunk,
            queueInsertFanout,
            queueInitRecovered,
            queueInsertRecovered,
            directCopyInsertRecovered,
            syntheticRecordOutboundPathRecovered,
            vectorGrowthRecovered,
            vectorInsertShiftRecovered,
            queueRemoveRecovered,
            insertThunkOnlyRecovered,
            queueInsertFanoutRecovered);

        var report = new Tf2Ps3SourceUsercmdQueueRecordReport(
            "tf2ps3-source-usercmd-queue-record-map",
            "Recovers the TF.elf client-side official usercmd queue record layer below markerless Source packets and above category-5 CLC_Move processing.",
            new Tf2Ps3SourceUsercmdQueueRecordInputs(
                cExportPath,
                pcapBoundaryProbePath,
                serverDllUsercmdDecoderPath ?? "",
                pcapRecordCandidateProbePath ?? "",
                tfElfBinaryPath ?? ""),
            new Tf2Ps3SourceUsercmdQueueRecordSummary(
                queueRecordLayerRecovered,
                queueBaseRecovered,
                queueCountRecovered,
                recordStrideRecovered,
                queueInitRecovered,
                queueInsertRecovered,
                directCopyInsertRecovered,
                syntheticRecordOutboundPathRecovered,
                vectorGrowthRecovered,
                vectorInsertShiftRecovered,
                queueRemoveRecovered,
                insertThunkOnlyRecovered,
                queueInsertFanoutRecovered,
                descriptorScan.Available,
                descriptorScan.QueueInsertDescriptorRecovered,
                descriptorScan.QueueInsertDescriptorReferences.Length,
                descriptorScan.NeighborDescriptorReferenceCount,
                concreteUdpToQueueInsertCallerRecovered,
                previousRecordDeltaBaseRecovered,
                writerCallRecovered,
                localBitreaderRecovered,
                ownerRouterRecovered,
                category5DispatchRecovered,
                deltaFields.Length,
                pcapProbe.Available,
                pcapProbe.HardMarkerlessPacketCount,
                pcapProbe.RawRecordLengthCandidateCount,
                pcapProbe.RawRecordLengthMultipleCandidateCount,
                serverDllFieldMap.Count > 0,
                directServerDllMappings,
                candidateServerDllMappings,
                recordCandidateProbe.Available,
                recordCandidateProbe.TfElfQueueDeltaDecodedPacketCount,
                recordCandidateProbe.TfElfQueueDeltaExactZeroTrailingPacketCount,
                recordCandidateProbe.TfElfQueueDeltaNonZeroTrailingPacketCount,
                recordCandidateProbe.TfElfSingleRecordSegmentProbeCount,
                recordCandidateProbe.TfElfSingleRecordSegmentExactZeroTrailingCount,
                recordCandidateProbe.TfElfSingleRecordSegmentNonZeroTrailingCount,
                recordCandidateProbe.TfElfSingleRecordSegmentDecodeFailedCount,
                nativeSourceInputReady,
                gates.Count(static gate => gate.Status is "missing" or "candidate")),
            new Tf2Ps3SourceUsercmdQueueRecordLayout(
                "client + 0x82b4",
                "client + 0x82c0",
                OfficialRecordStrideBytes,
                "00a2b470 inserts caller-provided 0x58 records into client + 0x82b4 when param_3 == 0",
                "local_a0 zero/current previous record",
                "FUN_0086f8f8 -> 0080ad88",
                "00a2b060 creates a 96000-bit local reader and routes the serialized queue through 008722a0 category 5."),
            queueFunctions,
            descriptorScan,
            deltaFields,
            serverDllMappings,
            pcapProbe,
            recordCandidateProbe,
            gates,
            [
                "The recovered TF.elf path is queue record -> delta bitstream -> 008722a0 category 5 -> 00a2bd18 -> 00a291c0.",
                "00a2ae20 proves the record base at client + 0x82b4, count at client + 0x82c0, and 0x58-byte stride.",
                "00a2b470 is the recovered queue insertion boundary: param_3 == 0 copies a caller-provided 0x58 record with FUN_00871708, while the nonzero path synthesizes a record, serializes it with FUN_0086f8f8, then sends it through virtual slot +0x5c.",
                queueInsertFanoutRecovered
                    ? "007ec208 is a concrete fanout caller into FUN_0039c890/00a2b470. It iterates a container with +0x10/+0x14/+0x08 virtual calls, filters player/client objects through vtable +0x6c, and queues records per target."
                    : "Recover the concrete caller into FUN_0039c890/00a2b470 so the queue insertion layer is not represented only by its thunk.",
                descriptorScan.Available
                    ? descriptorScan.QueueInsertDescriptorReferences.Length == 0
                        ? "The TF.elf OPD descriptor scan finds the 00a2b470 descriptor at 018fced8, but no direct data reference to that descriptor. Neighbor descriptors are referenced by vtable-like blocks, so 00a2b470 is not the visible receive vtable slot."
                        : "The TF.elf OPD descriptor scan found data references to the 00a2b470 descriptor; inspect DescriptorScan.QueueInsertDescriptorReferences before treating the thunk as the only path."
                    : "Run the queue reducer with TF.elf binary input to audit OPD descriptor references for 00a2b470.",
                "No concrete UDP receive or markerless parser caller into 00a2b470 is recovered yet; 007ec208 is queue fanout from an already-built command/container.",
                "0080ad88 is a delta writer over current versus previous 0x58-byte command records. It is not a raw packet parser.",
                "The EA server.dll CUserCmd decoder is a separate 0x40-byte semantic target. The TF.elf 0x58 queue record has candidate bridges to command/view/movement/weapon intent, but no direct raw-layout equivalence.",
                "No hard markerless PCAP body is exactly one 0x58-byte record, but some bodies are exact 0x58 multiples. Treat those as ingress-transform candidates, not as proof of raw record arrays.",
                recordCandidateProbe.Available
                    ? recordCandidateProbe.TfElfQueueDeltaExactZeroTrailingPacketCount == 0
                        ? "The TF.elf queue-delta probe rejects direct 0x58-multiple bodies as exact queue streams: every decoded prefix leaves non-zero trailing bits."
                        : "Some 0x58-multiple bodies need manual review because the TF.elf queue-delta probe found exact zero-trailing candidates."
                    : "Run analyze-source-usercmd-record-candidates to test 0x58-multiple bodies against the recovered TF.elf queue-delta field order.",
                recordCandidateProbe.Available
                    ? recordCandidateProbe.TfElfSingleRecordSegmentExactZeroTrailingCount == 0
                        ? "The per-0x58 segment probe also rejects the 176-byte bodies as two independent clean queue records: no segment has zero trailing bits."
                        : "Some per-0x58 segments need manual review because the single-record probe found exact zero-trailing candidates."
                    : "Run analyze-source-usercmd-record-candidates to test each 0x58 segment independently.",
                "The remaining native target is the ingress transform or parser that fills the queue records before 00a2ae20 drains them.",
                "Do not mark NativeSourceInputReady until that ingress path is recovered and live/PCAP hard markerless uploads decode into these named records."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceUsercmdQueueRecordDescriptorScan BuildDescriptorScan(string? tfElfBinaryPath)
    {
        if (string.IsNullOrWhiteSpace(tfElfBinaryPath) || !File.Exists(tfElfBinaryPath))
        {
            return new Tf2Ps3SourceUsercmdQueueRecordDescriptorScan(
                false,
                tfElfBinaryPath ?? "",
                0,
                false,
                "",
                "",
                [],
                0,
                [],
                "TF.elf binary not available for OPD descriptor scan");
        }

        var bytes = File.ReadAllBytes(tfElfBinaryPath);
        var queueInsertDescriptors = FindU32BigEndian(bytes, 0x00a2b470u);
        uint descriptorAddress = queueInsertDescriptors.Contains(0x018fced8u)
            ? 0x018fced8u
            : queueInsertDescriptors.FirstOrDefault();
        var descriptorReferences = descriptorAddress == 0
            ? []
            : FindU32BigEndian(bytes, (uint)descriptorAddress);
        var neighborReferenceAddresses = new[]
        {
            0x018fcee0u,
            0x018fcee8u,
            0x018fcef0u,
            0x018fcef8u,
            0x018fcf00u,
            0x018fcf10u,
            0x018fcf18u,
            0x018fcf20u,
            0x018fcf28u,
            0x018fcf30u,
            0x018fcf38u,
            0x018fcf40u,
            0x018fcf48u,
            0x018fcf50u
        };
        var neighborReferences = neighborReferenceAddresses
            .SelectMany(address => FindU32BigEndian(bytes, address).Select(offset => new Tf2Ps3SourceUsercmdQueueRecordDescriptorReference(
                FormatHex(address),
                FormatHex(offset))))
            .Take(64)
            .ToArray();

        var descriptors = ReadDescriptorWindow(bytes, 0x018fcec0, 18);
        return new Tf2Ps3SourceUsercmdQueueRecordDescriptorScan(
            true,
            tfElfBinaryPath,
            bytes.Length,
            descriptorAddress != 0,
            descriptorAddress == 0 ? "" : FormatHex((uint)descriptorAddress),
            descriptorAddress == 0 || descriptorAddress + 8 > bytes.Length
                ? ""
                : Convert.ToHexString(bytes.AsSpan((int)descriptorAddress, 8)).ToLowerInvariant(),
            descriptorReferences.Select(static offset => FormatHex(offset)).ToArray(),
            neighborReferences.Length,
            descriptors,
            descriptorAddress == 0
                ? "queue insert descriptor not found"
                : descriptorReferences.Length == 0
                    ? "queue insert descriptor is present, but direct data references are absent while nearby handler descriptors are referenced elsewhere"
                    : "queue insert descriptor is present and has direct data references");
    }

    private static Tf2Ps3SourceUsercmdQueueRecordDescriptorEntry[] ReadDescriptorWindow(
        byte[] bytes,
        int start,
        int count)
    {
        var entries = new List<Tf2Ps3SourceUsercmdQueueRecordDescriptorEntry>();
        for (var index = 0; index < count; index++)
        {
            var offset = start + index * 8;
            if (offset + 8 > bytes.Length)
            {
                break;
            }

            entries.Add(new Tf2Ps3SourceUsercmdQueueRecordDescriptorEntry(
                index,
                FormatHex((uint)offset),
                FormatHex(ReadU32BigEndian(bytes, offset)),
                FormatHex(ReadU32BigEndian(bytes, offset + 4))));
        }

        return entries.ToArray();
    }

    private static uint[] FindU32BigEndian(byte[] bytes, uint value)
    {
        var offsets = new List<uint>();
        for (var offset = 0; offset + 4 <= bytes.Length; offset++)
        {
            if (ReadU32BigEndian(bytes, offset) == value)
            {
                offsets.Add((uint)offset);
            }
        }

        return offsets.ToArray();
    }

    private static uint ReadU32BigEndian(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24)
        | ((uint)bytes[offset + 1] << 16)
        | ((uint)bytes[offset + 2] << 8)
        | bytes[offset + 3];

    private static string FormatHex(uint value) => value.ToString("x8");

    private static bool FunctionInitializesQueue(string body)
    {
        return body.Contains("+ 0x82b4", StringComparison.Ordinal)
            && body.Contains("_opd_FUN_00a2b9d0", StringComparison.Ordinal)
            && body.Contains("puVar6[4] = *puVar6", StringComparison.Ordinal)
            && body.Contains("puVar6[3] = 0", StringComparison.Ordinal);
    }

    private static Tf2Ps3SourceUsercmdQueueRecordFunction[] BuildQueueFunctions(
        ExportedFunction? queueInit,
        ExportedFunction? queueInitAlternate,
        ExportedFunction? queueInsert,
        ExportedFunction? queueGrow,
        ExportedFunction? queueShiftRight,
        ExportedFunction? queueCopyInsert,
        ExportedFunction? queueShiftLeft,
        ExportedFunction? queueRemove,
        ExportedFunction? queueDrain,
        ExportedFunction? queueConsumer,
        ExportedFunction? queueInsertThunk,
        ExportedFunction? queueInsertFanout,
        bool queueInitRecovered,
        bool queueInsertRecovered,
        bool directCopyInsertRecovered,
        bool syntheticRecordOutboundPathRecovered,
        bool vectorGrowthRecovered,
        bool vectorInsertShiftRecovered,
        bool queueRemoveRecovered,
        bool insertThunkOnlyRecovered,
        bool queueInsertFanoutRecovered)
    {
        return
        [
            BuildQueueFunction(queueInit, "queue initialization", queueInitRecovered, "Initializes client + 0x82b4 as a 0x58-stride vector and resets the count/cursor fields."),
            BuildQueueFunction(queueInitAlternate, "alternate queue initialization", queueInitRecovered, "Duplicates the queue initialization pattern for the sibling constructor path."),
            BuildQueueFunction(queueInsert, "queue insertion boundary", queueInsertRecovered, "Copies caller-provided records when param_3 == 0 and synthesizes/sends a record when param_3 is nonzero."),
            BuildQueueFunction(queueGrow, "queue vector grow", vectorGrowthRecovered, "Grows storage in units of 0x58 bytes."),
            BuildQueueFunction(queueShiftRight, "queue insert shift", vectorInsertShiftRecovered, "Shifts existing 0x58-byte records up before insertion."),
            BuildQueueFunction(queueCopyInsert, "queue copy insert", directCopyInsertRecovered, "Copies exactly 0x58 bytes from the caller source into the selected queue slot."),
            BuildQueueFunction(queueShiftLeft, "queue consume shift", queueRemoveRecovered, "Shifts remaining 0x58-byte records down after consumption."),
            BuildQueueFunction(queueRemove, "queue remove consumed", queueRemoveRecovered, "Drops consumed queue records and decrements the vector count."),
            BuildQueueFunction(queueDrain, "queue delta drain", queueDrain is not null, "Serializes up to the current command window from queue records into a local bitreader."),
            BuildQueueFunction(queueConsumer, "category 5 queue consumer", queueConsumer is not null, "Routes drained records through owner-forward category 5."),
            BuildQueueFunction(queueInsertThunk, "queue insert thunk", insertThunkOnlyRecovered, "Thin wrapper into 00a2b470."),
            BuildQueueFunction(queueInsertFanout, "queue insert fanout caller", queueInsertFanoutRecovered, "Iterates target player/client objects and forwards caller records through 0039c890 into 00a2b470; this is not raw UDP ingress.")
        ];
    }

    private static Tf2Ps3SourceUsercmdQueueRecordFunction BuildQueueFunction(
        ExportedFunction? function,
        string role,
        bool recovered,
        string evidence)
    {
        return new Tf2Ps3SourceUsercmdQueueRecordFunction(
            function?.Address ?? "",
            function?.Name ?? "",
            role,
            recovered ? "proven" : "missing",
            function?.StartLine ?? 0,
            function?.EndLine ?? 0,
            evidence);
    }

    private static Tf2Ps3SourceUsercmdQueueRecordField[] BuildDeltaFields(string deltaWriterBody)
    {
        if (deltaWriterBody.Length == 0)
        {
            return [];
        }

        var fields = new List<Tf2Ps3SourceUsercmdQueueRecordField>();
        AddIfPresent(fields, deltaWriterBody, "param_1[1]", 0x04, 11, "sequence/command-number-low", "changed bit then 11-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[0x12]", 0x48, 13, "tick/count high field", "changed bit then 13-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[0x11]", 0x44, 9, "tick/count low field", "changed bit then 9-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[2]", 0x08, 3, "small mode/channel field", "changed bit then 3-bit write");
        AddIfPresent(fields, deltaWriterBody, "*param_1", 0x00, 31, "command-number delta", "special prev+1 two-bit case or 31-bit absolute write");
        AddIfPresent(fields, deltaWriterBody, "param_1[10]", 0x28, 32, "view angle x/full float-style field", "changed bit then scaled writer FUN_003a13d0");
        AddIfPresent(fields, deltaWriterBody, "param_1[0xb]", 0x2c, 9, "view angle y/short angle field", "changed bit then 9-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[0xd]", 0x34, 8, "view angle z/byte angle field", "changed bit then 8-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[0x13]", 0x4c, 13, "forwardmove or analog axis", "changed bit then clamped 13-bit scaled write");
        AddIfPresent(fields, deltaWriterBody, "param_1[4]", 0x10, 15, "sidemove or movement axis", "changed bit then 15-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[5]", 0x14, 15, "upmove or movement axis", "changed bit then 15-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[6]", 0x18, 15, "button/movement field", "changed bit then 15-bit write");
        AddIfPresent(fields, deltaWriterBody, "param_1[0x15]", 0x54, 12, "weapon/subtype/impulse tail field", "changed bit then 12-bit write");
        return fields.ToArray();
    }

    private static IReadOnlyDictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField> BuildServerDllFieldMap(
        string? serverDllUsercmdDecoderPath)
    {
        if (string.IsNullOrWhiteSpace(serverDllUsercmdDecoderPath) || !File.Exists(serverDllUsercmdDecoderPath))
        {
            return new Dictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField>(StringComparer.Ordinal);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(serverDllUsercmdDecoderPath));
        if (!doc.RootElement.TryGetProperty("Fields", out var fieldsElement)
            || fieldsElement.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField>(StringComparer.Ordinal);
        }

        var fields = new Dictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField>(StringComparer.Ordinal);
        foreach (var field in fieldsElement.EnumerateArray())
        {
            var name = ReadString(field, "Name");
            if (name.Length == 0)
            {
                continue;
            }

            fields[name] = new Tf2Ps3SourceUsercmdQueueRecordServerDllField(
                name,
                ReadString(field, "OffsetHex"),
                ReadString(field, "ValueType"),
                ReadInt(field, "BitWidth"),
                ReadString(field, "DecodeKind"),
                ReadString(field, "AbsentBehavior"));
        }

        return fields;
    }

    private static Tf2Ps3SourceUsercmdQueueRecordServerDllMapping[] BuildServerDllMappings(
        Tf2Ps3SourceUsercmdQueueRecordField[] deltaFields,
        IReadOnlyDictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField> serverDllFields)
    {
        if (deltaFields.Length == 0)
        {
            return [];
        }

        return deltaFields
            .Select(field => BuildServerDllMapping(field, serverDllFields))
            .ToArray();
    }

    private static Tf2Ps3SourceUsercmdQueueRecordServerDllMapping BuildServerDllMapping(
        Tf2Ps3SourceUsercmdQueueRecordField field,
        IReadOnlyDictionary<string, Tf2Ps3SourceUsercmdQueueRecordServerDllField> serverDllFields)
    {
        var targetName = field.TfElfToken switch
        {
            "*param_1" => "command_number",
            "param_1[10]" => "viewangles.x",
            "param_1[0xb]" => "viewangles.y",
            "param_1[0xd]" => "viewangles.z",
            "param_1[0x13]" => "forwardmove",
            "param_1[4]" => "sidemove",
            "param_1[5]" => "upmove",
            "param_1[0x15]" => "weaponselect",
            _ => ""
        };

        var status = targetName.Length == 0
            ? "tfelf-queue-only-or-unresolved"
            : field.TfElfToken == "*param_1"
                ? "direct-semantic-match"
                : "candidate-bridge-required";
        var confidence = field.TfElfToken switch
        {
            "*param_1" => "high",
            "param_1[10]" or "param_1[0xb]" or "param_1[0xd]" => "medium",
            "param_1[0x13]" or "param_1[4]" or "param_1[5]" or "param_1[0x15]" => "medium-low",
            _ => "low"
        };
        var reason = field.TfElfToken switch
        {
            "param_1[1]" => "TF.elf writes this 11-bit preamble field before the command-number delta; no matching 0x40 server.dll CUserCmd field is recovered.",
            "param_1[0x12]" => "TF.elf writes this 13-bit preamble/counter field before the command-number delta; no direct server.dll CUserCmd layout match.",
            "param_1[0x11]" => "TF.elf writes this 9-bit mode/count field and can early-return when it equals 4; this is PS3 queue metadata, not a raw CUserCmd field.",
            "param_1[2]" => "TF.elf writes this 3-bit small mode/channel field before command-number handling; no official server.dll CUserCmd field uses this shape.",
            "*param_1" => "TF.elf command-number delta has the same server.dll semantic target, but uses a PS3 special prev/equal/absolute encoding instead of the server.dll 32-bit presence field.",
            "param_1[10]" => "TF.elf writes a changed bit followed by a scaled float-style angle writer, matching view angle intent but not the raw 32-bit server.dll wire field.",
            "param_1[0xb]" => "TF.elf writes a changed bit plus 9-bit quantized angle data, so it is a candidate bridge to viewangles.y.",
            "param_1[0xd]" => "TF.elf writes a changed bit plus 8-bit quantized angle data, so it is a candidate bridge to viewangles.z.",
            "param_1[0x13]" => "TF.elf writes a changed bit plus a clamped 13-bit scaled float, making this a candidate movement-axis bridge.",
            "param_1[4]" => "TF.elf writes a changed bit plus a 15-bit integer from a float, making this a candidate movement-axis bridge.",
            "param_1[5]" => "TF.elf writes a changed bit plus a 15-bit integer from a float, making this a candidate movement-axis bridge.",
            "param_1[6]" => "TF.elf writes a changed bit plus a 15-bit integer from a float, but the matching server.dll field is not proven; keep as unresolved PS3 command intent.",
            "param_1[0x15]" => "TF.elf writes a changed bit plus 12 bits near the tail, matching weapon-hint intent only as a candidate because server.dll splits weaponselect/weaponsubtype as 11+6 bits.",
            _ => "No mapping rule exists for this TF.elf queue field."
        };

        serverDllFields.TryGetValue(targetName, out var serverField);
        return new Tf2Ps3SourceUsercmdQueueRecordServerDllMapping(
            field.TfElfToken,
            field.ByteOffset,
            field.BitWidth,
            targetName,
            serverField?.OffsetHex ?? "",
            serverField?.ValueType ?? "",
            serverField?.BitWidth ?? 0,
            serverField?.DecodeKind ?? "",
            status,
            confidence,
            reason);
    }

    private static void AddIfPresent(
        List<Tf2Ps3SourceUsercmdQueueRecordField> fields,
        string body,
        string token,
        int byteOffset,
        int bitWidth,
        string name,
        string encoding)
    {
        if (!body.Contains(token, StringComparison.Ordinal))
        {
            return;
        }

        fields.Add(new Tf2Ps3SourceUsercmdQueueRecordField(token, byteOffset, bitWidth, name, encoding));
    }

    private static Tf2Ps3SourceUsercmdQueueRecordPcapProbe BuildPcapProbe(string pcapBoundaryProbePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(pcapBoundaryProbePath));
        var files = doc.RootElement.TryGetProperty("Files", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array
            ? filesElement.EnumerateArray().ToArray()
            : [];
        var hardMarkerless = 0;
        var rawRecord = 0;
        var rawRecordMultiple = 0;
        var lengths = new SortedDictionary<int, int>();
        var multipleLengths = new SortedDictionary<int, int>();
        var multipleSamples = new List<Tf2Ps3SourceUsercmdQueueRecordPcapSample>();
        foreach (var file in files)
        {
            if (!file.TryGetProperty("Samples", out var samples) || samples.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var sample in samples.EnumerateArray())
            {
                hardMarkerless++;
                var bodyLength = sample.TryGetProperty("BodyLength", out var bodyLengthElement)
                    ? bodyLengthElement.GetInt32()
                    : 0;
                if (bodyLength == OfficialRecordStrideBytes)
                {
                    rawRecord++;
                }

                if (bodyLength > 0 && bodyLength % OfficialRecordStrideBytes == 0)
                {
                    rawRecordMultiple++;
                    multipleLengths[bodyLength] = multipleLengths.GetValueOrDefault(bodyLength) + 1;
                    if (multipleSamples.Count < 24)
                    {
                        multipleSamples.Add(ReadPcapProbeSample(sample, bodyLength));
                    }
                }

                lengths[bodyLength] = lengths.GetValueOrDefault(bodyLength) + 1;
            }
        }

        return new Tf2Ps3SourceUsercmdQueueRecordPcapProbe(
            true,
            hardMarkerless,
            rawRecord,
            rawRecordMultiple,
            lengths.Count,
            lengths
                .OrderByDescending(static item => item.Value)
                .ThenBy(static item => item.Key)
                .Take(16)
                .Select(static item => new Tf2Ps3SourceUsercmdQueueRecordLengthCount(item.Key, item.Value))
                .ToArray(),
            multipleLengths
                .OrderByDescending(static item => item.Value)
                .ThenBy(static item => item.Key)
                .Select(static item => new Tf2Ps3SourceUsercmdQueueRecordLengthCount(item.Key, item.Value))
                .ToArray(),
            multipleSamples.ToArray(),
            rawRecord == 0 && rawRecordMultiple == 0
                ? "hard markerless bodies are not raw 0x58 usercmd queue records or exact record arrays"
                : rawRecord == 0
                    ? "no body is exactly one 0x58 record, but some bodies are exact 0x58 multiples; inspect as candidate arrays or wrapped delta streams"
                : "some hard markerless bodies have raw 0x58 record-sized lengths; inspect before assuming transform semantics");
    }

    private static Tf2Ps3SourceUsercmdQueueRecordRecordCandidateProbe BuildRecordCandidateProbe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new Tf2Ps3SourceUsercmdQueueRecordRecordCandidateProbe(
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "record-candidate probe missing");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var summary = doc.RootElement.TryGetProperty("Summary", out var summaryElement)
            ? summaryElement
            : default;
        return new Tf2Ps3SourceUsercmdQueueRecordRecordCandidateProbe(
            true,
            ReadInt(summary, "ExactRecordMultiplePacketCount"),
            ReadInt(summary, "TfElfQueueDeltaDecodedPacketCount"),
            ReadInt(summary, "TfElfQueueDeltaExactZeroTrailingPacketCount"),
            ReadInt(summary, "TfElfQueueDeltaNonZeroTrailingPacketCount"),
            ReadInt(summary, "TfElfSingleRecordSegmentProbeCount"),
            ReadInt(summary, "TfElfSingleRecordSegmentExactZeroTrailingCount"),
            ReadInt(summary, "TfElfSingleRecordSegmentNonZeroTrailingCount"),
            ReadInt(summary, "TfElfSingleRecordSegmentDecodeFailedCount"),
            ReadString(summary, "Conclusion"));
    }

    private static Tf2Ps3SourceUsercmdQueueRecordPcapSample ReadPcapProbeSample(JsonElement sample, int bodyLength)
    {
        var directProbe = sample.TryGetProperty("DirectProbe", out var direct) && direct.ValueKind == JsonValueKind.Object
            ? direct
            : default;
        return new Tf2Ps3SourceUsercmdQueueRecordPcapSample(
            ReadString(sample, "File"),
            ReadInt(sample, "SourceStep"),
            ReadInt64(sample, "PacketIndex"),
            ReadInt(sample, "Sequence"),
            ReadNullableInt(sample, "SequenceDelta"),
            bodyLength,
            bodyLength / OfficialRecordStrideBytes,
            ReadString(sample, "Conclusion"),
            ReadString(sample, "NativeDecodeDetail"),
            ReadString(sample, "BodyPrefixHex"),
            directProbe.ValueKind == JsonValueKind.Object ? ReadNullableInt(directProbe, "FirstNetMessageType") : null,
            directProbe.ValueKind == JsonValueKind.Object ? ReadString(directProbe, "FirstNetMessageName") : "");
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static long ReadInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var value)
            ? value
            : 0;
    }

    private static int? ReadNullableInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var starts = new List<(string Address, string Name, int Line)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[i]);
            }

            if (match.Success)
            {
                starts.Add((match.Groups["address"].Value, match.Groups["name"].Value, i));
            }
        }

        var functions = new List<ExportedFunction>();
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].Line : lines.Length;
            var slice = lines[start.Line..end];
            functions.Add(new ExportedFunction(
                start.Address,
                start.Name,
                start.Line + 1,
                end,
                slice,
                string.Join('\n', slice)));
        }

        return functions;
    }

    private sealed record ExportedFunction(
        string Address,
        string Name,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceUsercmdQueueRecordReport(
    string Status,
    string Note,
    Tf2Ps3SourceUsercmdQueueRecordInputs Inputs,
    Tf2Ps3SourceUsercmdQueueRecordSummary Summary,
    Tf2Ps3SourceUsercmdQueueRecordLayout Layout,
    Tf2Ps3SourceUsercmdQueueRecordFunction[] QueueFunctions,
    Tf2Ps3SourceUsercmdQueueRecordDescriptorScan DescriptorScan,
    Tf2Ps3SourceUsercmdQueueRecordField[] DeltaFields,
    Tf2Ps3SourceUsercmdQueueRecordServerDllMapping[] ServerDllFieldMappings,
    Tf2Ps3SourceUsercmdQueueRecordPcapProbe PcapProbe,
    Tf2Ps3SourceUsercmdQueueRecordRecordCandidateProbe RecordCandidateProbe,
    Tf2Ps3SourceUsercmdQueueRecordGate[] Gates,
    string[] Conclusions);

public sealed record Tf2Ps3SourceUsercmdQueueRecordInputs(
    string TfElfCExport,
    string PcapBoundaryProbe,
    string ServerDllUsercmdDecoder,
    string PcapRecordCandidateProbe,
    string TfElfBinary);

public sealed record Tf2Ps3SourceUsercmdQueueRecordSummary(
    bool QueueRecordLayerRecovered,
    bool QueueBaseRecovered,
    bool QueueCountRecovered,
    bool RecordStrideRecovered,
    bool QueueInitRecovered,
    bool QueueInsertRecovered,
    bool DirectCopyInsertRecovered,
    bool SyntheticRecordOutboundPathRecovered,
    bool VectorGrowthRecovered,
    bool VectorInsertShiftRecovered,
    bool QueueRemoveRecovered,
    bool InsertThunkOnlyRecovered,
    bool QueueInsertFanoutCallerRecovered,
    bool DescriptorScanAvailable,
    bool QueueInsertDescriptorRecovered,
    int QueueInsertDescriptorReferenceCount,
    int NeighborDescriptorReferenceCount,
    bool ConcreteUdpToQueueInsertCallerRecovered,
    bool PreviousRecordDeltaBaseRecovered,
    bool DeltaWriterRecovered,
    bool LocalBitreaderRecovered,
    bool OwnerRouterRecovered,
    bool Category5DispatchRecovered,
    int DeltaFieldCount,
    bool PcapProbeAvailable,
    int PcapHardMarkerlessPacketCount,
    int PcapRawRecordLengthCandidateCount,
    int PcapRawRecordLengthMultipleCandidateCount,
    bool ServerDllDecoderAvailable,
    int DirectServerDllFieldMatchCount,
    int CandidateServerDllFieldMatchCount,
    bool RecordCandidateProbeAvailable,
    int TfElfQueueDeltaDecodedPacketCount,
    int TfElfQueueDeltaExactZeroTrailingPacketCount,
    int TfElfQueueDeltaNonZeroTrailingPacketCount,
    int TfElfSingleRecordSegmentProbeCount,
    int TfElfSingleRecordSegmentExactZeroTrailingCount,
    int TfElfSingleRecordSegmentNonZeroTrailingCount,
    int TfElfSingleRecordSegmentDecodeFailedCount,
    bool NativeSourceInputReady,
    int OpenGateCount);

public sealed record Tf2Ps3SourceUsercmdQueueRecordLayout(
    string QueueBase,
    string QueueCount,
    int RecordStrideBytes,
    string QueueInsertion,
    string DeltaBase,
    string DeltaWriter,
    string Category5Consumer);

public sealed record Tf2Ps3SourceUsercmdQueueRecordDescriptorScan(
    bool Available,
    string TfElfBinary,
    int BinaryLength,
    bool QueueInsertDescriptorRecovered,
    string QueueInsertDescriptorAddress,
    string QueueInsertDescriptorBytes,
    string[] QueueInsertDescriptorReferences,
    int NeighborDescriptorReferenceCount,
    Tf2Ps3SourceUsercmdQueueRecordDescriptorEntry[] DescriptorWindow,
    string Conclusion);

public sealed record Tf2Ps3SourceUsercmdQueueRecordDescriptorEntry(
    int Index,
    string Address,
    string CodeAddress,
    string TocAddress);

public sealed record Tf2Ps3SourceUsercmdQueueRecordDescriptorReference(
    string DescriptorAddress,
    string ReferenceOffset);

public sealed record Tf2Ps3SourceUsercmdQueueRecordFunction(
    string Address,
    string Name,
    string Role,
    string Status,
    int StartLine,
    int EndLine,
    string Evidence);

public sealed record Tf2Ps3SourceUsercmdQueueRecordField(
    string TfElfToken,
    int ByteOffset,
    int BitWidth,
    string ProvisionalName,
    string Encoding);

public sealed record Tf2Ps3SourceUsercmdQueueRecordServerDllField(
    string Name,
    string OffsetHex,
    string ValueType,
    int BitWidth,
    string DecodeKind,
    string AbsentBehavior);

public sealed record Tf2Ps3SourceUsercmdQueueRecordServerDllMapping(
    string TfElfToken,
    int TfElfByteOffset,
    int TfElfBitWidth,
    string ServerDllFieldName,
    string ServerDllOffsetHex,
    string ServerDllValueType,
    int ServerDllBitWidth,
    string ServerDllDecodeKind,
    string MatchStatus,
    string Confidence,
    string Reason);

public sealed record Tf2Ps3SourceUsercmdQueueRecordPcapProbe(
    bool Available,
    int HardMarkerlessPacketCount,
    int RawRecordLengthCandidateCount,
    int RawRecordLengthMultipleCandidateCount,
    int DistinctBodyLengthCount,
    Tf2Ps3SourceUsercmdQueueRecordLengthCount[] TopBodyLengths,
    Tf2Ps3SourceUsercmdQueueRecordLengthCount[] RawRecordLengthMultipleCounts,
    Tf2Ps3SourceUsercmdQueueRecordPcapSample[] RawRecordLengthMultipleSamples,
    string Conclusion);

public sealed record Tf2Ps3SourceUsercmdQueueRecordRecordCandidateProbe(
    bool Available,
    int ExactRecordMultiplePacketCount,
    int TfElfQueueDeltaDecodedPacketCount,
    int TfElfQueueDeltaExactZeroTrailingPacketCount,
    int TfElfQueueDeltaNonZeroTrailingPacketCount,
    int TfElfSingleRecordSegmentProbeCount,
    int TfElfSingleRecordSegmentExactZeroTrailingCount,
    int TfElfSingleRecordSegmentNonZeroTrailingCount,
    int TfElfSingleRecordSegmentDecodeFailedCount,
    string Conclusion);

public sealed record Tf2Ps3SourceUsercmdQueueRecordLengthCount(
    int BodyLength,
    int Count);

public sealed record Tf2Ps3SourceUsercmdQueueRecordPcapSample(
    string File,
    int SourceStep,
    long PacketIndex,
    int Sequence,
    int? SequenceDelta,
    int BodyLength,
    int RecordMultiple,
    string Conclusion,
    string NativeDecodeDetail,
    string BodyPrefixHex,
    int? FirstNetMessageType,
    string FirstNetMessageName);

public sealed record Tf2Ps3SourceUsercmdQueueRecordGate(
    string Id,
    string Status,
    string Evidence,
    string Meaning);
