using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllTargetFunctionReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Eatf2ServerDllTargetFunctionMapReport> ReduceAsync(string targetFunctionsPath, string outputPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(targetFunctionsPath));
        var functions = document.RootElement.GetProperty("targetFunctions")
            .EnumerateArray()
            .Select(ReadFunction)
            .ToDictionary(static function => function.Entry, StringComparer.Ordinal);

        var gameManagerCallsites = new[]
        {
            BuildKnownFunction(
                functions,
                "1027c790",
                "serverdll-gamemanager-player-added",
                "Official server.dll notifies EA GameManager when a Source player joins/enters the game.",
                [
                    "Calls FeslHubSingle_GetGameManager.",
                    "Resolves Source player index through the engine interface and subtracts one before dispatch.",
                    "Calls the returned GameManager object's vtable +0x10 with player index and translated identity/context."
                ]),
            BuildKnownFunction(
                functions,
                "1027c800",
                "serverdll-gamemanager-player-removed",
                "Official server.dll notifies EA GameManager when a Source player leaves/disconnects.",
                [
                    "Calls FeslHubSingle_GetGameManager.",
                    "Resolves Source player index through the engine interface and subtracts one before dispatch.",
                    "Calls the returned GameManager object's vtable +0x18 with player index."
                ])
        };

        var statsFunctions = new[]
        {
            BuildKnownFunction(
                functions,
                "1028e740",
                "ranked-stats-update-transaction",
                "Official ranked stats are batched into FESL transaction rows and sent through the FESL hub, not mutated directly by menu-side Arcadia data.",
                [
                    "Builds three stat fields per update row.",
                    "Obtains FeslHubSingle_Get and walks vtable +0x24 then +0x04 to create/use the stats transaction service.",
                    "Sends the transaction through vtable +0x0c and logs transaction-buffer/error details."
                ]),
            BuildKnownFunction(
                functions,
                "102917e0",
                "player-stats-registration",
                "Official server.dll registers a joined CTFPlayer with TFStatsReporter/FeslStatsRetriever.",
                [
                    "Uses player name at CTFPlayer +0xd7c for logging and lookup.",
                    "Creates a FeslFunctor2Params<TFStatsReporter,FeslPlayerInfoRef,unsigned_short> callback.",
                    "Calls an engine/stats service vtable +0x14 to request stats registration/lookup."
                ]),
            BuildKnownFunction(
                functions,
                "102909f0",
                "player-stats-lookup-callback",
                "Official server.dll receives FESL stats lookup callbacks and installs returned player ranking/stat handles.",
                [
                    "Logs 'Lookup callback received'.",
                    "Matches returned player refs against local reporter slots.",
                    "Stores returned bucket/ref pointers at per-player slot offsets +0x20/+0x24 and queues a follow-up retriever functor."
                ])
        };

        var sourceRuntimeFunctions = new[]
        {
            BuildKnownFunction(
                functions,
                "10125420",
                "source-usercmd-batch-consumer",
                "Official server.dll consumes CUserCmd batches before physics; the native backend must decode real PS3 client command payloads into this semantic model.",
                [
                    "Rejects more than 0x1c / 28 commands with the 'too many cmds' warning.",
                    "Initializes CUserCmd records with a 0x40-byte stride.",
                    "Calls the player vtable +0x5b4 with command array, backup/sequence data, command count, and timing context."
                ]),
            BuildKnownFunction(
                functions,
                "1021c080",
                "source-usercmd-delta-decoder",
                "Official server.dll decodes one delta-compressed CUserCmd from a bitstream into the 0x40-byte Source command record.",
                [
                    "Copies the previous/default CUserCmd into the destination before applying field deltas.",
                    "Each decoded field is guarded by a presence bit; absent command/tick fields increment from the previous command while other absent fields inherit.",
                    "Reads 32-bit command/view/button fields, signed 16-bit movement/mouse fields, 8-bit impulse, 11-bit weaponselect, 6-bit weaponsubtype, and derives random_seed from command_number."
                ]),
            BuildKnownFunction(
                functions,
                "101a4750",
                "source-player-physics-simulate",
                "Official server.dll runs queued command simulation through CBasePlayer::PhysicsSimulate; generated snapshots need to be backed by this style of player state progression.",
                [
                    "Enters the 'CBasePlayer::PhysicsSimulate' vprof scope.",
                    "Processes queued usercmd history and calls player vtable +0x5b8 for individual command simulation.",
                    "Copies the last processed CUserCmd into player state before advancing movement/physics bookkeeping."
                ])
        };

        var gameStatsFunctions = new[]
        {
            BuildKnownFunction(functions, "1012f1d0", "level-init", "Official server.dll records level init and map lifecycle stats.", ["Logs CBaseGameStats::Event_LevelInit."]),
            BuildKnownFunction(functions, "1012e980", "level-shutdown", "Official server.dll records level shutdown and elapsed time.", ["Logs CBaseGameStats::Event_LevelShutdown."]),
            BuildKnownFunction(functions, "1012eb50", "player-connected", "Official server.dll records player connected events.", ["Logs CBaseGameStats::Event_PlayerConnected."]),
            BuildKnownFunction(functions, "1012eb70", "player-disconnected", "Official server.dll records player disconnected events.", ["Logs CBaseGameStats::Event_PlayerDisconnected."]),
            BuildKnownFunction(functions, "1012ebc0", "weapon-fired", "Official server.dll records weapon fired events.", ["Logs CBaseGameStats::Event_WeaponFired."]),
            BuildKnownFunction(functions, "1012ec00", "weapon-hit", "Official server.dll records weapon hit and damage events.", ["Logs CBaseGameStats::Event_WeaponHit."])
        };

        var allKnown = gameManagerCallsites
            .Concat(statsFunctions)
            .Concat(sourceRuntimeFunctions)
            .Concat(gameStatsFunctions)
            .ToArray();
        var missing = allKnown
            .Where(static function => !function.Present)
            .Select(static function => function.ExpectedEntry)
            .ToArray();
        var report = new Eatf2ServerDllTargetFunctionMapReport(
            "eatf2-serverdll-target-function-map",
            "Targeted Ghidra decompile reduction for official EA TF2 server.dll functions that matter to GameManager, ranked stats, usercmd, physics, and level/player lifecycle.",
            targetFunctionsPath,
            new Eatf2ServerDllTargetFunctionSummary(
                functions.Count,
                allKnown.Length,
                allKnown.Count(static function => function.Present),
                missing.Length),
            gameManagerCallsites,
            statsFunctions,
            sourceRuntimeFunctions,
            gameStatsFunctions,
            missing,
            [
                "EA GameManager integration in server.dll is lifecycle/stats notification, not a replacement for Source map-load transport.",
                "The live map-load failure remains inside the native Source command/snapshot path: usercmd decoding, player state, game rules, sendtables, and PS3 transport framing.",
                "Ranked stats should be updated only from ranked server-side gameplay events after this native runtime path is authoritative."
            ]);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Eatf2ServerDllKnownFunction BuildKnownFunction(
        IReadOnlyDictionary<string, TargetFunction> functions,
        string entry,
        string role,
        string semantics,
        string[] evidence)
    {
        if (!functions.TryGetValue(entry, out var function))
        {
            return new Eatf2ServerDllKnownFunction(entry, role, semantics, false, "", [], [], evidence);
        }

        return new Eatf2ServerDllKnownFunction(
            entry,
            role,
            semantics,
            true,
            function.Name,
            function.Reasons,
            ExtractBodyTokens(function.Body),
            evidence);
    }

    private static string[] ExtractBodyTokens(string body)
    {
        return new[]
            {
                "FeslHubSingle_GetGameManager",
                "FeslHubSingle_Get",
                "CUserCmd::vftable",
                "CBasePlayer::ProcessUsercmds",
                "CBasePlayer::PhysicsSimulate",
                "TFStatsReporter",
                "FeslStatsRetriever",
                "CBaseGameStats::Event_LevelInit",
                "CBaseGameStats::Event_LevelShutdown",
                "CBaseGameStats::Event_PlayerConnected",
                "CBaseGameStats::Event_PlayerDisconnected",
                "CBaseGameStats::Event_WeaponFired",
                "CBaseGameStats::Event_WeaponHit"
            }
            .Where(token => body.Contains(token, StringComparison.Ordinal))
            .ToArray();
    }

    private static TargetFunction ReadFunction(JsonElement function)
    {
        return new TargetFunction(
            function.GetProperty("entry").GetString() ?? "",
            function.GetProperty("name").GetString() ?? "",
            function.GetProperty("reasons").EnumerateArray()
                .Select(static reason => reason.GetString() ?? "")
                .Where(static reason => reason.Length > 0)
                .ToArray(),
            function.GetProperty("body").GetString() ?? "");
    }

    private sealed record TargetFunction(
        string Entry,
        string Name,
        string[] Reasons,
        string Body);
}

public sealed record Eatf2ServerDllTargetFunctionMapReport(
    string Status,
    string Note,
    string TargetFunctionsInput,
    Eatf2ServerDllTargetFunctionSummary Summary,
    Eatf2ServerDllKnownFunction[] GameManagerCallsites,
    Eatf2ServerDllKnownFunction[] RankedStatsFunctions,
    Eatf2ServerDllKnownFunction[] SourceRuntimeFunctions,
    Eatf2ServerDllKnownFunction[] GameStatsFunctions,
    string[] MissingExpectedEntries,
    string[] Findings);

public sealed record Eatf2ServerDllTargetFunctionSummary(
    int ExportedTargetFunctionCount,
    int ExpectedFunctionCount,
    int ExpectedFunctionsPresent,
    int MissingExpectedFunctionCount);

public sealed record Eatf2ServerDllKnownFunction(
    string ExpectedEntry,
    string Role,
    string Semantics,
    bool Present,
    string DecompiledName,
    string[] Reasons,
    string[] BodyTokens,
    string[] Evidence);
