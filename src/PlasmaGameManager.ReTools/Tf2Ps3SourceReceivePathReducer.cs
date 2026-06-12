using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceReceivePathReducer
{
    private static readonly Regex FunctionDefinitionRegex = new(
        @"^(?<return>[\w\s\*\[\]]+?)\s+(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex SplitFunctionDefinitionRegex = new(
        @"^(?<name>(?:_opd_FUN|FUN)_(?<address>[0-9a-f]{8}))\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] TargetAddresses =
    [
        "008b82c0",
        "008b83a8",
        "008b8d50",
        "008b9f70",
        "008ba3d8",
        "008ba628",
        "008b9468",
        "008bfa08",
        "008bfa88",
        "00a57f48",
        "00a584d0",
        "00a58868",
        "00a58c10",
        "00a5a550",
        "00a5c2e8",
        "00a5df70",
        "008bc978",
        "008bc490",
        "008bb058",
        "009252e0",
        "00925858"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string cExportPath, string outputPath)
    {
        var lines = await File.ReadAllLinesAsync(cExportPath);
        var functions = ExtractFunctions(lines)
            .Where(static function => TargetAddresses.Contains(function.Address, StringComparer.Ordinal))
            .Select(BuildFunction)
            .ToArray();

        var receiveDrain = functions.SingleOrDefault(static function => function.Address == "008b8d50");
        var queue = functions.SingleOrDefault(static function => function.Address == "008b9f70");
        var queueDrainers = functions
            .Where(static function => function.Address is "008ba3d8" or "008ba628")
            .ToArray();
        var peerControlReaders = functions
            .Where(static function => function.Address is "008b9468")
            .ToArray();
        var acceptedPeerListHelpers = functions
            .Where(static function => function.Address is "008bfa08" or "008bfa88")
            .ToArray();
        var attachedPlayerStreamReaders = functions
            .Where(static function => function.Address is "00a5c2e8")
            .ToArray();
        var sendBuilders = functions
            .Where(static function => function.Address is "008bc978" or "008bc490" or "008bb058" or "009252e0" or "00925858")
            .ToArray();
        var packetHandlers = BuildPacketHandlers(functions);

        var unresolved = new List<Tf2Ps3SourceReceiveUnknown>();
        if (!peerControlReaders.Any(static function =>
                function.Calls.Contains("_opd_FUN_008b82c0", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("uVar12 == 4", StringComparer.Ordinal)))
        {
            unresolved.Add(new Tf2Ps3SourceReceiveUnknown(
                "accepted-peer-control-reader-missing",
                "008b9468",
                "Expected 008b9468 to read 5-byte accepted-peer control records and branch on message type 4. Re-run targeted Ghidra decompile around 008b9468 if this disappears."));
        }

        if (!acceptedPeerListHelpers.Any(static function =>
                function.Address == "008bfa88"
                && function.Calls.Contains("_opd_FUN_008bfa08", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("param_1[3] = param_1[3] - 1", StringComparer.Ordinal)))
        {
            unresolved.Add(new Tf2Ps3SourceReceiveUnknown(
                "accepted-peer-list-remove-helper-missing",
                "008bfa88",
                "Expected 008bfa88 to remove one accepted-peer table element and decrement the table count at +0x0c."));
        }

        if (!attachedPlayerStreamReaders.Any(static function =>
                function.Calls.Contains("_opd_FUN_008b82c0", StringComparer.Ordinal)
                && function.Calls.Contains("_opd_FUN_00a58c10", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("param_1[0x10c]", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("param_1[0x151]", StringComparer.Ordinal)))
        {
            unresolved.Add(new Tf2Ps3SourceReceiveUnknown(
                "attached-player-source-stream-reader-missing",
                "00a5c2e8",
                "Expected 00a5c2e8 to read frame-kind bytes from the attached player socket, stage variable payloads, and dispatch them through 00a58c10."));
        }

        if (!functions.Any(static function =>
                function.Address == "00a58c10"
                && function.Calls.Contains("_opd_FUN_00a58868", StringComparer.Ordinal)
                && function.Calls.Contains("_opd_FUN_00a57f48", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("2 < uVar15", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("*piVar11 + 0x14", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("*piVar11 + 0x10", StringComparer.Ordinal)))
        {
            unresolved.Add(new Tf2Ps3SourceReceiveUnknown(
                "attached-payload-dispatcher-missing",
                "00a58c10",
                "Expected 00a58c10 to read 5-bit native Source payload message ids, route ids 0/1/2 to 00a58868, and dispatch ids >=3 through the registered handler table."));
        }

        if (!functions.Any(static function =>
                function.Address == "00a57f48"
                && function.EvidenceTokens.Contains("param_1 + 0x1e18", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("param_1 + 0x1e0c", StringComparer.Ordinal)
                && function.EvidenceTokens.Contains("*piVar2 + 0x20", StringComparer.Ordinal)))
        {
            unresolved.Add(new Tf2Ps3SourceReceiveUnknown(
                "attached-payload-handler-lookup-missing",
                "00a57f48",
                "Expected 00a57f48 to scan the native Source handler table and compare handler vtable +0x20 ids with the decoded 5-bit message id."));
        }

        var report = new Tf2Ps3SourceReceivePathReport(
            "tf2ps3-native-source-receive-path",
            "Maps the TF.elf native Source/gameplay receive and queue path. This is receive-side evidence for future usercmd/input decoding, not a replay recipe. The queue selector is a Source socket/player slot channel, not a classic client/server direction.",
            cExportPath,
            new Tf2Ps3SourceReceivePathSummary(
                functions.Length,
                receiveDrain is not null,
                receiveDrain?.ContainsRecvfrom ?? false,
                receiveDrain?.RecvBufferLengthHex ?? "",
                queue?.QueueDirections.Length ?? 0,
                queueDrainers.Length,
                sendBuilders.Length,
                packetHandlers.Length,
                unresolved.Count),
            functions,
            packetHandlers,
            [
                new("socket-recv-wrapper", "008b82c0", "Connected-socket recv wrapper. It maps transient PS3 socket errors 0x23/0x39 to a zero-byte/no-data result instead of fatal close."),
                new("socket-close-slot-clear", "008b83a8", "Closes a connected socket and clears the slot +0x0c connected-socket field when a slot index is supplied."),
                new("slot-table-base", "PTR_DAT_0197336c + 0xe8", "Per-slot 0x10 records used by the UDP gameplay sockets."),
                new("slot-count", "PTR_DAT_0197336c + 0xf4", "Number of source/gameplay socket slots iterated by 008b8d50."),
                new("recv-socket-field", "slot + 0x08", "Socket used by 008b8d50 recvfrom."),
                new("connected-socket-field", "slot + 0x0c", "Socket connected/opened by 008b8e70 and closed by 008b83a8."),
                new("source-send-slot-index", "008bc978 param_2", "Selects the 0x10-byte Source UDP slot/channel record used for direct sends or deferred queueing."),
                new("accepted-peer-table", "PTR_DAT_0197336c + 0x1b0 / +0x1bc", "Connected/accepted peer socket table scanned by 008b9468."),
                new("accepted-peer-record-stride", "0x18", "Each pending accepted-peer record advances by 0x18 bytes."),
                new("accepted-peer-record-socket", "record + 0x00", "Socket read by 008b9468 and attached to the matched Source player object."),
                new("accepted-peer-record-peer-id", "record + 0x04", "Compared against Source player object vtable callback +0xc0."),
                new("accepted-peer-record-last-activity", "record + 0x08", "Float timestamp used for accepted-peer timeout checks."),
                new("accepted-peer-record-address", "record + 0x0c", "Address blob copied into Source player object offset +0x98 on successful association."),
                new("accepted-peer-control-header", "5 bytes", "008b9468 reads a 5-byte control record from each accepted peer before dispatching association state."),
                new("accepted-peer-message-type", "first 8 bits == 4", "Message type 4 is the association/attach path in 008b9468."),
                new("accepted-peer-association-token", "next 32 bits", "Compared against registered connection/player objects via vtable callbacks at 0xc0 and 0xc4."),
                new("source-player-object-table", "PTR_DAT_0197336c + 0x148 / +0x154", "Array/count of Source player connection objects scanned for association."),
                new("source-player-peer-id-callback", "object vtable + 0xc0", "Must equal accepted-peer record +0x04."),
                new("source-player-token-callback", "object vtable + 0xc4", "Must equal decoded 32-bit association token."),
                new("source-player-attached-socket", "object + 0x90", "Must be zero before attach; receives accepted-peer socket on success."),
                new("source-player-address-copy", "object + 0x98", "Destination for accepted-peer address blob copied from record +0x0c."),
                new("source-player-associated-flag", "object + 0x42e", "Set to 1 after successful accepted-peer association."),
                new("source-player-post-attach-callback", "object vtable + 0x6c", "Invoked after socket and associated flag are stored."),
                new("accepted-peer-list-remove", "008bfa88", "Removes the consumed/failed accepted-peer record and decrements table count."),
                new("attached-player-stream-reader", "00a5c2e8", "Post-association client stream reader for the attached Source player object."),
                new("attached-player-frame-kind", "object + 0x430 / param_1[0x10c]", "One-byte client frame kind read from attached socket object +0x90."),
                new("attached-player-frame-token", "object + 0x434 / param_1[0x10d]", "32-bit token/id read from type 2 or type 4 client headers."),
                new("attached-player-frame-length", "object + 0x438 / param_1[0x10e]", "Type 2 payload byte length, capped at 96000."),
                new("attached-player-frame-read-offset", "object + 0x43c / param_1[0x10f]", "Accumulated payload bytes read into the frame buffer."),
                new("attached-player-frame-buffer", "object + 0x544 / param_1[0x151]", "Staging buffer passed to 00a58c10 after a complete type 2 payload arrives."),
                new("attached-player-type-1", "frame kind 1", "Marks object +0x42e associated, calls vtable +0x6c, then advances connection state through 00a584d0."),
                new("attached-player-type-2", "frame kind 2", "Reads a 6-byte header: native 16-bit length followed by native 32-bit token/id, then reads the declared payload."),
                new("attached-player-type-3", "frame kind 3", "Guarded continuation/armed payload path. If object +0x440 is false, the reader returns failure before reading the staged payload."),
                new("attached-player-type-4", "frame kind 4", "Reads a native 32-bit token/id and compares it with the expected object/session token at object +0xc0 -> +0x114."),
                new("attached-player-payload-dispatch", "00a58c10", "Dispatches a complete type 2 staged payload through the TF2 PS3 native Source bitstream reader."),
                new("source-payload-message-id", "00a58c10 first field", "Every staged type 2 payload is a bitstream of native Source messages. The dispatcher reads a 5-bit message id before each message body."),
                new("source-payload-builtin-control-ids", "message ids 0, 1, 2", "Message id 0 is a no-op/continue. Id 1 reads a string into the object +0x1e08 callback at vtable +0x0c. Id 2 reads a 32-bit key, string, and 1-bit branch to vtable +0x1c/+0x24."),
                new("source-payload-handler-table", "object +0x1e0c / +0x1e18", "Registered native Source payload handlers scanned by 00a57f48 for message ids >=3."),
                new("source-payload-handler-id-callback", "handler vtable +0x20", "Returns the message id handled by a registered payload handler."),
                new("source-payload-handler-pre-callback", "handler vtable +0x28", "Called before parsing/executing a registered payload handler and also used for filter/name comparisons."),
                new("source-payload-handler-parse-callback", "handler vtable +0x14", "Parses handler-specific fields from the bit reader. Failure clears the player address and aborts the payload."),
                new("source-payload-handler-result-callback", "handler vtable +0x24", "Result/id passed to the player/session vtable +0xd0 accounting callback with the consumed bit count."),
                new("source-payload-handler-execute-callback", "handler vtable +0x10", "Executes the parsed native Source payload handler."),
                new("source-payload-handler-cleanup-callback", "handler vtable +0x30", "Invoked when the message is filtered/skipped or when a name-filter path breaks out of execution."),
                new("source-payload-handler-registration", "00a5df70", "Rejects duplicate handler ids through 00a57f48, appends handlers at object +0x1e0c/+0x1e18, then binds the handler back to the player/session."),
                new("attached-player-state-advance", "00a584d0", "Advances the attached player/source connection state after type 1 setup or a completed type 2 payload."),
                new("attached-player-token-reset", "00a5a550", "Called by type 4 token validation to reset/accept the token-bound attached state."),
                new("queue-pool", "PTR_DAT_0197336c + 0x228", "Packet allocation pool used by 008b9f70."),
                new("queue-inline-threshold", "0x801", "Payloads below this use the inline 0x808 allocation path."),
                new("queue-ceiling", "0x17701", "Payload allocation ceiling recovered from 008b9f70."),
                new("recv-buffer", "0x800", "Maximum UDP recvfrom payload size in 008b8d50.")
            ],
            unresolved.ToArray());

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Tf2Ps3SourceReceiveFunction BuildFunction(ExportedFunction function)
    {
        var calls = ExtractCalls(function.Body);
        return new Tf2Ps3SourceReceiveFunction(
            function.Name,
            function.Address,
            function.StartLine,
            function.EndLine,
            ClassifyRole(function.Address),
            calls,
            function.Body.Contains("recvfrom(", StringComparison.Ordinal),
            function.Body.Contains("sendto(", StringComparison.Ordinal),
            function.Body.Contains("0x800", StringComparison.Ordinal) ? "0x800" : "",
            function.Body.Contains("0x80", StringComparison.Ordinal),
            ExtractQueueDirections(function.Body),
            BuildEvidence(function.Body),
            Preview(function.Lines));
    }

    private static string ClassifyRole(string address)
    {
        return address switch
        {
            "008b8d50" => "udp-gameplay-receive-drain",
            "008b82c0" => "connected-socket-recv-wrapper",
            "008b83a8" => "socket-close-and-slot-clear",
            "008b9f70" => "source-payload-queue",
            "008ba3d8" => "source-queue-drain-a",
            "008ba628" => "source-queue-drain-b",
            "008b9468" => "accepted-peer-short-control-reader",
            "008bfa08" => "accepted-peer-list-remove-range",
            "008bfa88" => "accepted-peer-list-remove-one",
            "00a57f48" => "attached-payload-handler-table-lookup",
            "00a584d0" => "attached-player-state-ack-sender",
            "00a58868" => "attached-payload-builtin-control-handler",
            "00a58c10" => "attached-payload-message-dispatcher",
            "00a5a550" => "attached-player-token-slot-reset",
            "00a5c2e8" => "attached-player-source-stream-reader",
            "00a5df70" => "attached-payload-handler-registration",
            "008bc978" => "source-send-wrapper",
            "008bc490" => "fragmented-source-send",
            "008bb058" => "direct-source-send",
            "009252e0" => "reliable-peer-channel-queue",
            "00925858" => "reliable-peer-channel-send",
            _ => "source-network-helper"
        };
    }

    private static Tf2Ps3SourceQueueDirection[] ExtractQueueDirections(string body)
    {
        var directions = new List<Tf2Ps3SourceQueueDirection>();
        if (body.Contains("param_1 == 1", StringComparison.Ordinal)
            && body.Contains("puVar3 + 600", StringComparison.Ordinal)
            && body.Contains("puVar3 + 0x260", StringComparison.Ordinal)
            && body.Contains("puVar3 + 0x270", StringComparison.Ordinal))
        {
            directions.Add(new Tf2Ps3SourceQueueDirection(
                1,
                "slot-channel-one",
                "0x258/0x260/0x268/0x270",
                "Uses PTR_DAT_0197336c + 600, +0x260, +0x268, +0x270. 008bc978 passes its slot index here when immediate sending is disabled."));
        }

        if (body.Contains("param_1 == 0", StringComparison.Ordinal)
            && body.Contains("puVar3 + 0x278", StringComparison.Ordinal)
            && body.Contains("puVar3 + 0x280", StringComparison.Ordinal)
            && body.Contains("puVar3 + 0x290", StringComparison.Ordinal))
        {
            directions.Add(new Tf2Ps3SourceQueueDirection(
                0,
                "slot-channel-zero",
                "0x278/0x280/0x288/0x290",
                "Uses PTR_DAT_0197336c + 0x278, +0x280, +0x288, +0x290. This is the alternate slot queue selected by 008bc978 param_2."));
        }

        return directions.ToArray();
    }

    private static string[] BuildEvidence(string body)
    {
        var evidence = new List<string>();
        foreach (var token in new[]
        {
            "recvfrom(",
            "recv(",
            "socketclose()",
            "sendto(",
            "0x800",
            "0x80",
            "0x23",
            "0x39",
            "0x801",
            "0x808",
            "0x17701",
            "0xfffffffe",
            "_opd_FUN_008bc490",
            "_opd_FUN_008bb058",
            "_opd_FUN_008b9f70",
            "FUN_00871708",
            "FUN_00871958",
            "_opd_FUN_008b82c0",
            "FUN_0086de68",
            "uVar12 == 4",
            "0x1b0",
            "0x1bc",
            "0x42e",
            "0x26",
            "0x148",
            "0x154",
            "0x160",
            "0x6c",
            "0x90",
            "piVar6[0x24]",
            "piVar6 + 0x26",
            "piVar20[1] == iVar11",
            "uVar13 == uVar12",
            "param_1[0x24]",
            "param_1[0x10c]",
            "param_1[0x10d]",
            "param_1[0x10e]",
            "param_1[0x10f]",
            "param_1[0x151]",
            "iVar2 == 1",
            "iVar2 == 2",
            "iVar2 == 3",
            "iVar2 == 4",
            "param_1[0x10e] = uVar3",
            "param_1[0x10d] = uVar3",
            "param_1[0x10f] = iVar2",
            "_opd_FUN_008b82c0(param_1[0x24],auStack_234,6,0)",
            "_opd_FUN_008b82c0(param_1[0x24],auStack_234,4,0)",
            "_opd_FUN_008b82c0(param_1[0x24],(void *)(iVar1 + param_1[0x151]),iVar2 - iVar1,0)",
            "96000",
            "_opd_FUN_00a58c10",
            "_opd_FUN_00a584d0",
            "_opd_FUN_00a5a550",
            "param_1[3] = param_1[3] - 1",
            "_opd_FUN_008bfa08",
            "_opd_FUN_008bfa88",
            "0xc0",
            "0xc4",
            "param_1 + 0x1e18",
            "param_1 + 0x1e0c",
            "*piVar2 + 0x20",
            "_opd_FUN_00a58868",
            "_opd_FUN_00a57f48",
            "2 < uVar15",
            "uVar15 < 3",
            "*piVar11 + 0x28",
            "*piVar11 + 0x14",
            "*piVar11 + 0x24",
            "*piVar11 + 0x10",
            "*piVar11 + 0x30",
            "*param_1 + 0xd0",
            "*param_1 + 0xd8",
            "param_1[0x788]",
            "FUN_0086d188",
            "FUN_00870138(param_1 + 0x26,0)",
            "param_2 == 1",
            "param_2 == 2",
            "FUN_0086e338",
            "0x400",
            "*(int **)(param_1 + 0x1e08)",
            "FUN_00870c28((int *)local_60,4)",
            "FUN_0086caf8((int *)local_60,param_2)",
            "_opd_FUN_008b8328",
            "_opd_FUN_00a621c8",
            "_opd_FUN_00a625e8",
            "param_1 + 0x1e0c",
            "param_1 + 0x1e18",
            "*puStack00000038 + 8"
        })
        {
            if (body.Contains(token, StringComparison.Ordinal))
            {
                evidence.Add(token);
            }
        }

        return evidence.ToArray();
    }

    private static Tf2Ps3SourcePacketHandlerContract[] BuildPacketHandlers(
        IReadOnlyCollection<Tf2Ps3SourceReceiveFunction> functions)
    {
        var contracts = new List<Tf2Ps3SourcePacketHandlerContract>();
        if (functions.Any(static function => function.Address == "008b82c0"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "connected-socket-recv-wrapper",
                "008b82c0",
                "socket-helper",
                "recv(socket, buffer, requestedLength, flags)",
                [
                    "returns recv byte count when positive",
                    "returns 0 for transient no-data socket errors 0x23 and 0x39 after 008b8288 updates last socket error",
                    "returns -1 for hard socket errors"
                ],
                [
                    "This wrapper is why live handling must treat zero-byte reads as nonfatal wait states."
                ],
                "Use nonblocking/soft-read semantics for RPCS3/native Source receive loops instead of closing on no-data.",
                ["recv(", "0x23", "0x39"]));
        }

        if (functions.Any(static function => function.Address == "008b9468"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "accepted-peer-association-control",
                "008b9468",
                "accepted-peer frame type 4",
                "5 bytes: 8-bit message type, then 32-bit association token through FUN_0086de68 bit reader",
                [
                    "accepted-peer record age must be within timeout window",
                    "message type must equal 4",
                    "accepted-peer record +0x04 must equal Source player vtable +0xc0 peer id",
                    "decoded token must equal Source player vtable +0xc4 token",
                    "Source player object +0x90 / piVar6[0x24] must not already hold an attached socket"
                ],
                [
                    "copies accepted-peer address record +0x0c into Source player object +0x98",
                    "sets Source player associated flag at byte +0x42e",
                    "stores accepted socket into Source player object +0x90 / piVar6[0x24]",
                    "calls Source player vtable +0x6c after attaching",
                    "optionally clears the accepted-peer address blob with FUN_00870138",
                    "removes the consumed accepted-peer record through 008bfa88",
                    "on mismatch, clears/ closes the accepted socket and removes the peer record"
                ],
                "Before payload frame kind 1/2/4 can work, the server-visible peer must have passed this type-4 association control path.",
                [
                    "_opd_FUN_008b82c0",
                    "FUN_0086de68",
                    "uVar12 == 4",
                    "piVar20[1] == iVar11",
                    "uVar13 == uVar12",
                    "piVar6[0x24]",
                    "0x42e",
                    "_opd_FUN_008bfa88"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a5c2e8"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-frame-kind-1",
                "00a5c2e8",
                "attached-player frame type 1",
                "1-byte frame kind only",
                [
                    "attached socket at object +0x90 / param_1[0x24] must be nonzero",
                    "current frame kind cache at object +0x430 / param_1[0x10c] must be 1"
                ],
                [
                    "sets byte object +0x42e associated flag",
                    "calls Source player vtable +0x6c",
                    "advances state through 00a584d0 using object +0x1e00 / param_1[0x780]"
                ],
                "Treat this as the lightweight attached-player/session-ready control frame.",
                ["iVar2 == 1", "0x42e", "_opd_FUN_00a584d0"]));

            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-frame-kind-2",
                "00a5c2e8",
                "attached-player frame type 2",
                "6-byte header: 16-bit payload length, then 32-bit token/id; followed by payload bytes into object +0x544",
                [
                    "attached socket at object +0x90 / param_1[0x24] must be nonzero",
                    "payload length cache object +0x438 / param_1[0x10e] is filled only once per frame",
                    "declared payload length must be <= 96000",
                    "payload bytes are read incrementally until object +0x43c / param_1[0x10f] equals object +0x438"
                ],
                [
                    "stores payload length at object +0x438 / param_1[0x10e]",
                    "stores token/id at object +0x434 / param_1[0x10d]",
                    "accumulates bytes into object +0x544 / param_1[0x151]",
                    "on complete payload, initializes a bit reader over the staged bytes",
                    "dispatches the staged native Source payload through 00a58c10",
                    "calls Source player vtable +0x6c",
                    "advances state through 00a584d0 using the saved token/id"
                ],
                "This is the main native Source client payload handler. Live 71/235 byte client uploads should eventually be decoded here semantically, not translated as PC srcds packets.",
                [
                    "iVar2 == 2",
                    "_opd_FUN_008b82c0(param_1[0x24],auStack_234,6,0)",
                    "param_1[0x10e] = uVar3",
                    "param_1[0x10d] = uVar3",
                    "96000",
                    "param_1[0x10f] = iVar2",
                    "_opd_FUN_00a58c10",
                    "_opd_FUN_00a584d0"
                ]));

            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-frame-kind-3",
                "00a5c2e8",
                "attached-player frame type 3",
                "uses previously staged length/payload state; guarded by byte object +0x440",
                [
                    "if byte object +0x440 is zero, the function returns failure before payload processing"
                ],
                [
                    "when armed, falls through into the common staged-payload read/dispatch path"
                ],
                "This appears to be a gated continuation or mode-specific payload frame. Keep it separate from type 2 until more PCAP/live evidence hits it.",
                ["iVar2 == 3", "param_1 + 0x110"]));

            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-frame-kind-4",
                "00a5c2e8",
                "attached-player frame type 4",
                "4-byte token/id through FUN_0086de68 bit reader",
                [
                    "attached socket at object +0x90 / param_1[0x24] must be nonzero",
                    "token cache object +0x434 / param_1[0x10d] is read only when currently zero",
                    "decoded token must equal expected token at *(object +0xc0)->0x114"
                ],
                [
                    "on token match, optionally clears object +0x98 address copy",
                    "calls 00a5a550(object, 0) to accept/reset token-bound attached state",
                    "on token mismatch, clears object +0x98 address copy",
                    "calls Source player vtable +0x6c and returns success"
                ],
                "This is a token validation/reset control frame on the attached player stream, distinct from payload-bearing type 2.",
                [
                    "iVar2 == 4",
                    "_opd_FUN_008b82c0(param_1[0x24],auStack_234,4,0)",
                    "param_1[0x10d] = uVar3",
                    "_opd_FUN_00a5a550"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a58c10"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-payload-dispatcher",
                "00a58c10",
                "attached-player type 2 staged payload",
                "bitstream loop: 5-bit native Source message id followed by either a built-in control body or a registered handler-specific body",
                [
                    "called only after frame kind 2 has fully staged the declared payload bytes into object +0x544",
                    "bit reader must have at least 5 readable bits for another message id",
                    "message ids 0, 1, and 2 are reserved built-in controls",
                    "message ids >=3 must resolve through 00a57f48 in the registered handler table"
                ],
                [
                    "routes ids 0/1/2 to 00a58868",
                    "routes ids >=3 to a handler returned by 00a57f48",
                    "calls handler vtable +0x28 before parse and for filter/name comparisons",
                    "calls handler vtable +0x14 to parse handler-specific bits",
                    "calls player/session vtable +0xd0 with handler vtable +0x24 result and consumed bit count",
                    "sets a transient executing flag at object byte +0x04 while calling handler vtable +0x10",
                    "if parse or lookup fails, clears object +0x98 through FUN_00870138(param_1 + 0x26, 0) and aborts",
                    "if object byte +0x05 is set during execution, calls player/session vtable +0x04 and aborts",
                    "if player/session vtable +0xd8 reports a post-handler stop condition, aborts"
                ],
                "Implement this as the native Source payload dispatcher. The server should decode a sequence of 5-bit message ids and then dispatch to explicit semantic handlers instead of forwarding PC srcds packets.",
                [
                    "_opd_FUN_00a58868",
                    "_opd_FUN_00a57f48",
                    "2 < uVar15",
                    "*piVar11 + 0x28",
                    "*piVar11 + 0x14",
                    "*piVar11 + 0x24",
                    "*piVar11 + 0x10",
                    "*piVar11 + 0x30",
                    "*param_1 + 0xd0",
                    "*param_1 + 0xd8",
                    "FUN_00870138(param_1 + 0x26,0)"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a58868"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-built-in-control-ids",
                "00a58868",
                "built-in native Source payload ids 0/1/2",
                "id 0 has no body; id 1 reads one <=0x400 string; id 2 reads a 32-bit key, one <=0x400 string, and one branch bit",
                [
                    "called only for inner message ids lower than 3",
                    "id 1 and id 2 string reads are capped at 0x400 bytes",
                    "any unsupported built-in id clears object +0x98 and fails"
                ],
                [
                    "id 0 returns success without consuming a body",
                    "id 1 calls the object at +0x1e08 through vtable +0x0c with the decoded string",
                    "id 2 decodes a 32-bit key before the string",
                    "id 2 uses the trailing one-bit flag to choose the +0x1c or +0x24 callback on object +0x1e08",
                    "unsupported built-in ids clear object +0x98 through FUN_00870138 and return failure"
                ],
                "These controls are part of the same native Source payload stream. They should be decoded before registered handler ids because they do not use the handler table.",
                [
                    "param_2 == 1",
                    "param_2 == 2",
                    "FUN_0086e338",
                    "0x400",
                    "*(int **)(param_1 + 0x1e08)"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a57f48"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-handler-table-lookup",
                "00a57f48",
                "registered native Source payload handler lookup",
                "no wire bytes; scans object handler table for a decoded 5-bit message id",
                [
                    "handler count at object +0x1e18 must be greater than zero",
                    "handler pointer array at object +0x1e0c must contain registered handler objects",
                    "handler vtable +0x20 must return the decoded message id"
                ],
                [
                    "returns the handler object whose vtable +0x20 id equals the message id",
                    "returns zero when no handler is registered for the id"
                ],
                "This is the native dispatch table behind message ids >=3. Unknown ids are protocol errors for TF2 PS3 and should be logged as missing semantic handlers.",
                [
                    "param_1 + 0x1e18",
                    "param_1 + 0x1e0c",
                    "*piVar2 + 0x20"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a584d0"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-state-ack-frame",
                "00a584d0",
                "server attached-player state/token ack",
                "writer emits control value 4 followed by a 32-bit token/id, then sends on the attached socket",
                [
                    "attached socket at object +0x90 must be valid",
                    "token/id argument is normally object +0x1e00 or the saved type-2 frame token"
                ],
                [
                    "builds a small bitstream with FUN_00870c28(..., 4)",
                    "writes the 32-bit token/id with FUN_0086caf8",
                    "optionally clears object +0x98 when global send state asks for it",
                    "sends the resulting bytes through 008b8328 on object +0x90"
                ],
                "Use this as the model for native acknowledgement after type 1 setup and completed type 2 payload processing.",
                [
                    "FUN_00870c28((int *)local_60,4)",
                    "FUN_0086caf8((int *)local_60,param_2)",
                    "_opd_FUN_008b8328"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a5a550"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-token-slot-reset",
                "00a5a550",
                "attached-player token slot reset",
                "no wire bytes; resets one of the token-bound player payload slots selected by the frame kind 4 token path",
                [
                    "slot index comes from the token path; TF2 PS3 frame kind 4 currently calls this with index 0",
                    "slot base is object +0xc0 plus index * 0x14"
                ],
                [
                    "frees any allocated slot buffer at slot object +0x108 / local_20[0][0x42]",
                    "closes/unregisters an active slot object before clearing it",
                    "removes the slot entry with 00a621c8",
                    "frees the removed slot object with FUN_00871968"
                ],
                "This is cleanup/acceptance state for token-bound native payload fragments, not a gameplay packet by itself.",
                [
                    "_opd_FUN_00a621c8",
                    "FUN_00871968"
                ]));
        }

        if (functions.Any(static function => function.Address == "00a5df70"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "attached-player-handler-registration",
                "00a5df70",
                "native Source payload handler registration",
                "no wire bytes; installs one registered handler object into object +0x1e0c/+0x1e18",
                [
                    "handler vtable +0x20 id must not already be present in 00a57f48",
                    "handler table array/count at object +0x1e0c/+0x1e18 must be appendable"
                ],
                [
                    "rejects duplicate handler ids",
                    "appends handler object through 00a625e8",
                    "calls handler vtable +0x08 to bind the handler back to the player/session"
                ],
                "Use this to reconstruct the registered native message id set. Handler-complete reverse engineering means every registered id has a named parse and execute contract.",
                [
                    "_opd_FUN_00a57f48",
                    "_opd_FUN_00a625e8",
                    "*puStack00000038 + 8"
                ]));
        }

        if (functions.Any(static function => function.Address == "00925858"))
        {
            contracts.Add(new Tf2Ps3SourcePacketHandlerContract(
                "reliable-peer-channel-send-routing",
                "00925858",
                "same-visible-endpoint send route",
                "payload pointer/length plus sockaddr-like peer endpoint",
                [
                    "if remote IP is not 127.0.0.1 and does not match object +0x48, the function uses the external peer send path",
                    "if remote port matches object +0x4c, queue through object +0x28",
                    "if remote port matches object +0x4e, queue through object +0x0c",
                    "if neither port matches, set object +0x50 to 0x80010016 and fail"
                ],
                [
                    "same-host/same-visible-flow traffic goes through 009252e0 reliable queue instead of raw PC Source UDP",
                    "returns payload length on successful queue"
                ],
                "This confirms the PS3 Source endpoint can remain the same visible IP/port while internally routing to two reliable peer queues.",
                ["0x7f000001", "0x4c", "0x4e", "0x80010016", "_opd_FUN_009252e0"]));
        }

        return contracts.ToArray();
    }

    private static string[] ExtractCalls(string body)
    {
        return Regex.Matches(body, @"\b(?:_opd_FUN|FUN)_[0-9a-f]{8}|recvfrom|recv|sendto|connect|socket")
            .Select(static match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ExportedFunction> ExtractFunctions(string[] lines)
    {
        var functions = new List<ExportedFunction>();
        var start = -1;
        var name = "";
        var address = "";
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && char.IsWhiteSpace(lines[i][0]))
            {
                continue;
            }

            var match = FunctionDefinitionRegex.Match(lines[i]);
            if (!match.Success)
            {
                match = SplitFunctionDefinitionRegex.Match(lines[i]);
            }

            if (!match.Success)
            {
                continue;
            }

            if (start >= 0)
            {
                functions.Add(BuildExportedFunction(lines, start, i - 1, name, address));
            }

            start = i;
            name = match.Groups["name"].Value;
            address = match.Groups["address"].Value;
        }

        if (start >= 0)
        {
            functions.Add(BuildExportedFunction(lines, start, lines.Length - 1, name, address));
        }

        return functions;
    }

    private static ExportedFunction BuildExportedFunction(string[] lines, int start, int end, string name, string address)
    {
        var functionLines = lines[start..(end + 1)];
        return new ExportedFunction(name, address, start + 1, end + 1, functionLines, string.Join('\n', functionLines));
    }

    private static string Preview(IReadOnlyList<string> lines)
    {
        var text = string.Join('\n', lines.Take(80));
        return text.Length <= 2400 ? text : text[..2400];
    }

    private sealed record ExportedFunction(
        string Name,
        string Address,
        int StartLine,
        int EndLine,
        string[] Lines,
        string Body);
}

public sealed record Tf2Ps3SourceReceivePathReport(
    string Status,
    string Note,
    string Input,
    Tf2Ps3SourceReceivePathSummary Summary,
    Tf2Ps3SourceReceiveFunction[] Functions,
    Tf2Ps3SourcePacketHandlerContract[] PacketHandlers,
    Tf2Ps3SourceReceiveAnchor[] Anchors,
    Tf2Ps3SourceReceiveUnknown[] RemainingReceiveUnknowns);

public sealed record Tf2Ps3SourceReceivePathSummary(
    int TargetFunctionCount,
    bool HasReceiveDrain,
    bool ReceiveDrainUsesRecvfrom,
    string ReceiveBufferLengthHex,
    int QueueDirectionCount,
    int QueueDrainerCount,
    int SendBuilderCount,
    int PacketHandlerContractCount,
    int RemainingReceiveUnknownCount);

public sealed record Tf2Ps3SourceReceiveFunction(
    string Name,
    string Address,
    int StartLine,
    int EndLine,
    string Role,
    string[] Calls,
    bool ContainsRecvfrom,
    bool ContainsSendto,
    string RecvBufferLengthHex,
    bool UsesNonBlockingRecvFlag,
    Tf2Ps3SourceQueueDirection[] QueueDirections,
    string[] EvidenceTokens,
    string Preview);

public sealed record Tf2Ps3SourceQueueDirection(
    int ParamValue,
    string Direction,
    string RingOffsets,
    string Evidence);

public sealed record Tf2Ps3SourcePacketHandlerContract(
    string Name,
    string Address,
    string PacketKind,
    string WireShape,
    string[] Preconditions,
    string[] StateMutations,
    string ServerImplementationGuidance,
    string[] EvidenceTokens);

public sealed record Tf2Ps3SourceReceiveAnchor(
    string Name,
    string Expression,
    string Meaning);

public sealed record Tf2Ps3SourceReceiveUnknown(
    string Id,
    string AddressOrKey,
    string NextReverseEngineeringTarget);
