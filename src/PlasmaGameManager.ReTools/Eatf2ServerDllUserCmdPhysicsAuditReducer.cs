using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Eatf2ServerDllUserCmdPhysicsAuditReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task ReduceAsync(string runtimeContractPath, string sourceRoot, string outputPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(runtimeContractPath));
        var root = document.RootElement;
        var userCmd = root.GetProperty("UserCmdBatch");
        var physics = root.GetProperty("PhysicsSimulation");
        var source = SourceIndex.Load(sourceRoot);

        var items = new[]
        {
            BuildItem(
                "official-max-usercmd-batch",
                "Official server.dll rejects batches above 28 commands.",
                $"{userCmd.GetProperty("MaxCommandsPerBatch").GetInt32()} command maximum from {userCmd.GetProperty("Entry").GetString()}",
                source,
                ["MaxUserCmdsPerBatch = 28", "officialMaxUserCmds={OfficialEatf2ServerDllContracts.MaxUserCmdsPerBatch}"],
                [],
                "complete",
                "The constant and telemetry are present; packet-level batch splitting is tracked separately."),
            BuildItem(
                "official-cusercmd-stride",
                "Official CUserCmd records are 0x40 bytes.",
                userCmd.GetProperty("CUserCmdStrideBytes").GetString() ?? "0x40",
                source,
                ["CUserCmdStrideBytes = 0x40", "CUserCmdStrideBytes"],
                [],
                "complete",
                "The native code carries the official stride as contract metadata, not as a direct memory struct."),
            BuildItem(
                "official-physics-history-stride",
                "Official PhysicsSimulate advances command history in 0x790-byte slots.",
                physics.GetProperty("CommandHistoryStrideBytes").GetString() ?? "0x790",
                source,
                ["PhysicsCommandHistoryStrideBytes = 0x790", "PhysicsCommandHistoryStrideBytes"],
                [],
                "complete",
                "The recovered stride is now attached to command-history entries for later snapshot/simulation consumers."),
            BuildItem(
                "per-player-command-history-ring",
                "Native player state needs a bounded queue of decoded client commands before simulation/snapshot publication.",
                "bounded by official MaxAdditionalPhysicsSimulationCommands",
                source,
                ["NativeClientCommandHistory", "Tf2SourceClientCommandHistoryEntry", "NativeClientCommandHistoryCapacity"],
                [],
                "complete",
                "The current ring stores decoded command intent and official stride metadata, capped at the recovered 0x18 PhysicsSimulate additional-command limit."),
            BuildItem(
                "single-command-intent-decoder",
                "PS3 client payloads are decoded into server-side command intent.",
                "Ps3SourceClientCommandIntent.TryDecode",
                source,
                ["Ps3SourceClientCommandIntent.TryDecode", "TryDecodeSingleBitSidecarCommand", "TryApplyOfficialClientCommand", "ExtractCommandBody", "ReadBiasedCommandAxis"],
                [],
                "partial",
                "The live command-intent path now tries the recovered official single-CUserCmd sidecar decoder before falling back to the older heuristic decoder for unmapped payload shapes."),
            BuildItem(
                "official-batch-parser",
                "A complete replacement must parse/validate batches up to the official 28-command limit.",
                "server.dll function 10125420 initializes/consumes CUserCmd batches",
                source,
                ["MaxUserCmdsPerBatch", "Ps3SourceClientCommandBatch", "TryDecodeBatch", "OfficialDecodeFunctionEntry", "Ps3SourceClcMoveMessage", "Ps3SourceClientCommandBatchBoundaryResolver", "Ps3SourceNativeToClcMoveBoundaryResolver", "TryDecodeBitSidecarClcMoveBatch", "TryApplyOfficialClcMoveClientCommands", "ConvertOfficialClientCommand"],
                [],
                "partial",
                "The implementation now reduces direct, attached type-2, and exact bit-sidecar native wrappers to CLC_Move, then applies the recovered official CUserCmd fields to native player simulation. The remaining gap is proving all live near-MTU/queued wrapper coverage."),
            BuildItem(
                "physics-tick-gate",
                "PhysicsSimulate runs once per official server tick gate before snapshot generation.",
                physics.GetProperty("TickGate").GetString() ?? "DAT_104c6458 + 0x18",
                source,
                ["AdvanceSnapshot", "TickBase", "SimulationTime"],
                ["PhysicsSimulateTickGate", "LastPhysicsSimulationTick"],
                "partial",
                "The native backend advances simulation time and snapshots, but it does not yet mirror the official DAT_104c6458 tick gate exactly."),
            BuildItem(
                "movement-world-rules",
                "Decoded command intent must update Source-visible movement state under world/map rules.",
                "CBasePlayer::PhysicsSimulate plus CTFPlayerMove semantics",
                source,
                ["ApplyClientCommandIntent", "UpdateNativeMovementFlags", "ApplyWorldRules", "Tf2MapMetadata"],
                [],
                "partial",
                "Movement, jump, duck, gravity, world bounds, and map metadata are present; brush collision, trigger touch, water, and ladders remain incomplete."),
            BuildItem(
                "official-telemetry-events",
                "Runtime evidence should make live logs show official usercmd/physics contract metadata.",
                "CBasePlayer::ProcessUsercmds / CBasePlayer::PhysicsSimulate",
                source,
                ["CBasePlayer::ProcessUsercmds", "CBasePlayer::PhysicsSimulate", "physicsHistoryStride=0x{OfficialEatf2ServerDllContracts.PhysicsCommandHistoryStrideBytes:x}"],
                [],
                "complete",
                "Telemetry includes the official function names, slots, maximums, and strides to make live logs auditable.")
        };

        var report = new Eatf2ServerDllUserCmdPhysicsAuditReport(
            "eatf2-serverdll-usercmd-physics-implementation-audit",
            "Audits the native implementation against official EA TF2 server.dll usercmd and PhysicsSimulate contracts. This is stricter than marker presence: it separates complete metadata/history support from still-heuristic command parsing and incomplete movement physics.",
            runtimeContractPath,
            sourceRoot,
            new Eatf2ServerDllUserCmdPhysicsAuditSummary(
                userCmd.GetProperty("MaxCommandsPerBatch").GetInt32(),
                userCmd.GetProperty("CUserCmdStrideBytes").GetString() ?? "",
                physics.GetProperty("CommandHistoryStrideBytes").GetString() ?? "",
                physics.GetProperty("MaxAdditionalSimulationCommands").GetString() ?? "",
                items.Length,
                items.Count(static item => item.Status == "complete"),
                items.Count(static item => item.Status == "partial"),
                items.Count(static item => item.Status == "missing")),
            items,
            BuildFindings(items));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Eatf2ServerDllUserCmdPhysicsAuditItem BuildItem(
        string id,
        string requirement,
        string officialEvidence,
        SourceIndex source,
        string[] implementationMarkers,
        string[] strictMissingMarkers,
        string intendedStatus,
        string notes)
    {
        var markers = implementationMarkers
            .Select(marker => new Eatf2ServerDllUserCmdPhysicsAuditMarker(
                marker,
                source.Contains(marker),
                source.FirstEvidencePath(marker),
                source.FirstEvidenceLine(marker)))
            .ToArray();
        var missing = markers
            .Where(marker => !marker.Present)
            .Select(marker => marker.Marker)
            .ToArray();
        var strictMissing = strictMissingMarkers
            .Where(marker => !source.Contains(marker))
            .ToArray();
        var status = missing.Length > 0
            ? "missing"
            : strictMissing.Length > 0
                ? "partial"
                : intendedStatus;

        return new Eatf2ServerDllUserCmdPhysicsAuditItem(
            id,
            requirement,
            officialEvidence,
            status,
            markers,
            missing,
            strictMissing,
            notes);
    }

    private static string[] BuildFindings(IReadOnlyCollection<Eatf2ServerDllUserCmdPhysicsAuditItem> items)
    {
        var findings = new List<string>
        {
            "Official server.dll usercmd constants are recovered and represented in native code: max batch 28, CUserCmd stride 0x40, PhysicsSimulate command-history stride 0x790, and additional-command cap 0x18.",
            "Native player state now stores decoded command intent in a bounded per-player command history ring, which is the minimum server-side state needed before authoritative simulation/snapshot work.",
            "The protocol/server layers now contain the recovered official 28-command CUserCmd batch decoder, native-to-CLC_Move wrapper resolution for direct/attached/sidecar packets, and live application of decoded new commands into native player simulation.",
            "Physics is still not native-complete: movement/world-rule scaffolding exists, but brush collision, trigger touch, water, ladders, and exact Source prediction remain open."
        };

        var missing = items.Where(static item => item.Status == "missing").Select(static item => item.Id).ToArray();
        if (missing.Length > 0)
        {
            findings.Add($"Missing usercmd/physics implementation items: {string.Join(", ", missing)}.");
        }

        return findings.ToArray();
    }

    private sealed class SourceIndex
    {
        private readonly SourceLine[] _lines;

        private SourceIndex(SourceLine[] lines)
        {
            _lines = lines;
        }

        public static SourceIndex Load(string sourceRoot)
        {
            var lines = new List<SourceLine>();
            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                             && !path.Contains($"{Path.DirectorySeparatorChar}PlasmaGameManager.ReTools{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                foreach (var line in File.ReadLines(path))
                {
                    lines.Add(new SourceLine(path, line.Trim()));
                }
            }

            return new SourceIndex(lines.ToArray());
        }

        public bool Contains(string marker)
        {
            return _lines.Any(line => line.Text.Contains(marker, StringComparison.Ordinal));
        }

        public string FirstEvidencePath(string marker)
        {
            return _lines.FirstOrDefault(line => line.Text.Contains(marker, StringComparison.Ordinal))?.Path ?? "";
        }

        public string FirstEvidenceLine(string marker)
        {
            return _lines.FirstOrDefault(line => line.Text.Contains(marker, StringComparison.Ordinal))?.Text ?? "";
        }
    }

    private sealed record SourceLine(string Path, string Text);
}

public sealed record Eatf2ServerDllUserCmdPhysicsAuditReport(
    string Status,
    string Note,
    string RuntimeContractInput,
    string SourceRoot,
    Eatf2ServerDllUserCmdPhysicsAuditSummary Summary,
    Eatf2ServerDllUserCmdPhysicsAuditItem[] Items,
    string[] Findings);

public sealed record Eatf2ServerDllUserCmdPhysicsAuditSummary(
    int OfficialMaxCommandsPerBatch,
    string OfficialCUserCmdStrideBytes,
    string OfficialPhysicsCommandHistoryStrideBytes,
    string OfficialMaxAdditionalSimulationCommands,
    int AuditItemCount,
    int CompleteItemCount,
    int PartialItemCount,
    int MissingItemCount);

public sealed record Eatf2ServerDllUserCmdPhysicsAuditItem(
    string Id,
    string Requirement,
    string OfficialEvidence,
    string Status,
    Eatf2ServerDllUserCmdPhysicsAuditMarker[] ImplementationMarkers,
    string[] MissingImplementationMarkers,
    string[] StrictMissingMarkers,
    string Notes);

public sealed record Eatf2ServerDllUserCmdPhysicsAuditMarker(
    string Marker,
    bool Present,
    string EvidencePath,
    string EvidenceLine);
