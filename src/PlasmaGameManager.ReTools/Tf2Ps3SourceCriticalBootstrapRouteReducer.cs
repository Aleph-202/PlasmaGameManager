using System.Text.Json;
using System.Text.RegularExpressions;

namespace PlasmaGameManager.ReTools;

public static partial class Tf2Ps3SourceCriticalBootstrapRouteReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<Tf2Ps3SourceCriticalBootstrapRouteReport> ReduceAsync(
        string cExportPath,
        string criticalContractPath,
        string outputPath)
    {
        var contracts = await ReadContractsAsync(criticalContractPath);
        var sourceLines = await File.ReadAllLinesAsync(cExportPath);
        var callsites = FindDirectCallsites(sourceLines, contracts);
        var bootstrapSequences = BuildBootstrapSequences(sourceLines, contracts, callsites);

        var contractReports = contracts
            .Select(contract =>
            {
                var directCallsites = callsites
                    .Where(callsite => string.Equals(callsite.TargetWriteFunction, contract.ExpectedWriteEntry, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new Tf2Ps3SourceCriticalBootstrapRouteContract(
                    contract.ClassName,
                    contract.Role,
                    contract.ExpectedReadEntry,
                    contract.ExpectedWriteEntry,
                    directCallsites.Length,
                    directCallsites,
                    directCallsites.Length == 0
                        ? "direct-call-not-found"
                        : "direct-write-callsite-found",
                    DescribeImplementationMeaning(contract.ClassName, directCallsites));
            })
            .ToArray();

        var report = new Tf2Ps3SourceCriticalBootstrapRouteReport(
            "tf2ps3-source-critical-bootstrap-route-map",
            "Maps TF.elf critical CLC/SVC Source message contracts onto direct writer callsites and the native send/queue boundary. This report tracks where raw five-bit Source netmessages become staged payloads; it does not claim the outer peer-channel envelope is complete.",
            cExportPath,
            criticalContractPath,
            new Tf2Ps3SourceCriticalBootstrapRouteSummary(
                contracts.Length,
                contractReports.Count(static contract => contract.DirectCallsiteCount > 0),
                callsites.Length,
                contractReports.Count(static contract => contract.Role.Contains("server", StringComparison.OrdinalIgnoreCase)),
                contractReports.Count(static contract => contract.Role.Contains("client", StringComparison.OrdinalIgnoreCase))),
            contractReports,
            [
                new("raw-source-bitstream", "Ps3SourceNetMessages", "The raw CLC/SVC bitstream field order is now implemented for the critical join/map-load set."),
                new("bootstrap-writer-callsite-a", "TF.elf direct call to 008cec08", "One bootstrap path writes SVC_ServerInfo into a large 96000-bit buffer, then continues through signon/send-table style writers."),
                new("bootstrap-writer-callsite-b", "TF.elf direct call to 008cec08", "A second Source object setup path writes SVC_ServerInfo before class/string-table setup and a later Source object send."),
                new("native-send-wrapper", "008bc978", "Prepared Source payload bytes are staged by the native send wrapper; when immediate send is off, it queues raw payload bytes through 008b9f70."),
                new("optional-bit-sidecar", "008bc978 param_6", "If a sidecar bit-buffer exists, TF.elf prepends a two-byte big-endian bit count and appends sidecar bytes after the main payload before direct/fragment send."),
                new("fragment-or-peer-queue", "008bb058 / 008bc490 / 009252e0", "Immediate sends route into the peer-channel queue or native fragment builder rather than PC-style connectionless Source UDP.")
            ],
            [
                "Direct callsites prove where SVC_ServerInfo and some adjacent bootstrap writers are invoked, but several critical SVC writers remain indirect/vtable-only in the C export.",
                "The next implementation boundary is an outer native peer-channel payload builder that can place the raw SVC_* bitstreams into the same staged send/queue path as TF.elf, without exposing PC srcds packets.",
                "Live insertion is still unproven: the diagnostic bootstrap should remain separated from production response timing until PCAP/live evidence identifies the correct wrapper phase."
            ],
            bootstrapSequences);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        return report;
    }

    private static Tf2Ps3SourceCriticalBootstrapSequence[] BuildBootstrapSequences(
        string[] sourceLines,
        IReadOnlyCollection<Tf2Ps3CriticalContract> contracts,
        IReadOnlyCollection<Tf2Ps3SourceCriticalBootstrapRouteCallsite> callsites)
    {
        var criticalWriters = contracts
            .Where(static contract => contract.Role.Contains("server", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static contract => contract.ExpectedWriteEntry, static contract => contract.ClassName, StringComparer.OrdinalIgnoreCase);
        var bootstrapHelpers = new Dictionary<string, (string Name, string Meaning)>(StringComparer.OrdinalIgnoreCase)
        {
            ["008dadc8"] = ("string-table-create-helper", "helper that wraps one or more SVC_CreateStringTable writes"),
            ["008da9e0"] = ("string-table-update-helper", "helper that wraps SVC_UpdateStringTable writes"),
            ["00a56cb0"] = ("class-info-bootstrap-helper", "helper that wraps SVC_ClassInfo setup"),
            ["00a61150"] = ("snapshot-frame-builder", "native snapshot frame builder used by gameplay entity state"),
            ["00a5fb80"] = ("entity-delta-writer", "native entity-delta group writer"),
            ["00a5e9e0"] = ("queued-bitstream-descriptor-builder", "native queued bitstream descriptor builder"),
            ["008bc978"] = ("native-send-wrapper", "native Source payload staging/send wrapper"),
            ["008b9f70"] = ("peer-channel-queue", "peer-channel queue path reached by native send wrapper"),
            ["008bb058"] = ("fragment-or-queue-send", "fragment/queued send boundary"),
            ["008bc490"] = ("fragment-builder", "native fragment builder boundary"),
            ["009252e0"] = ("native-fragment-send", "low-level native fragment send")
        };

        var functionAddresses = callsites
            .Select(static callsite => callsite.ContainingFunction)
            .Where(static address => address.Length != 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static address => address, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ranges = FindFunctionRanges(sourceLines);
        var sequences = new List<Tf2Ps3SourceCriticalBootstrapSequence>(functionAddresses.Length);
        foreach (var address in functionAddresses)
        {
            if (!ranges.TryGetValue(address, out var range))
            {
                continue;
            }

            var events = new List<Tf2Ps3SourceCriticalBootstrapSequenceEvent>();
            for (var i = range.StartLineIndex; i < range.EndLineIndex; i++)
            {
                var line = sourceLines[i];
                foreach (var (writerAddress, className) in criticalWriters)
                {
                    if (CallsFunction(line, writerAddress))
                    {
                        events.Add(new Tf2Ps3SourceCriticalBootstrapSequenceEvent(
                            i + 1,
                            "source-netmessage-writer",
                            className,
                            writerAddress,
                            line.Trim(),
                            Preview(sourceLines, i)));
                    }
                }

                foreach (var (helperAddress, helper) in bootstrapHelpers)
                {
                    if (CallsFunction(line, helperAddress))
                    {
                        events.Add(new Tf2Ps3SourceCriticalBootstrapSequenceEvent(
                            i + 1,
                            helper.Name,
                            helper.Meaning,
                            helperAddress,
                            line.Trim(),
                            Preview(sourceLines, i)));
                    }
                }
            }

            if (events.Count == 0)
            {
                continue;
            }

            sequences.Add(new Tf2Ps3SourceCriticalBootstrapSequence(
                address,
                range.StartLineIndex + 1,
                range.EndLineIndex,
                events.ToArray(),
                DescribeBootstrapSequence(events)));
        }

        return sequences.ToArray();
    }

    private static string DescribeBootstrapSequence(IReadOnlyList<Tf2Ps3SourceCriticalBootstrapSequenceEvent> events)
    {
        var labels = events.Select(static item => item.Role == "source-netmessage-writer" ? item.Name : item.Role).ToArray();
        if (labels.Contains("SVC_ServerInfo", StringComparer.Ordinal)
            && labels.Contains("string-table-create-helper", StringComparer.Ordinal))
        {
            return "server-info precedes string-table creation helper in this bootstrap owner; class-info and snapshot/entity routes are not inline in the same function.";
        }

        if (labels.Contains("SVC_ClassInfo", StringComparer.Ordinal))
        {
            return "class-info is emitted from a separate bootstrap helper rather than inline with the observed server-info owners.";
        }

        if (labels.Contains("SVC_CreateStringTable", StringComparer.Ordinal)
            || labels.Contains("SVC_UpdateStringTable", StringComparer.Ordinal))
        {
            return "string-table helper owns raw string-table SVC writes; callers establish placement relative to server-info.";
        }

        return "ordered TF.elf call sequence for critical Source bootstrap/helper events.";
    }

    private static Dictionary<string, Tf2Ps3SourceFunctionRange> FindFunctionRanges(string[] sourceLines)
    {
        var starts = new List<Tf2Ps3SourceFunctionStart>();
        for (var i = 0; i < sourceLines.Length; i++)
        {
            var line = sourceLines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            var definition = FunctionNameRegex().Match(line);
            if (definition.Success)
            {
                starts.Add(new Tf2Ps3SourceFunctionStart(i, NormalizeAddress(definition.Groups["address"].Value)));
            }
        }

        var ranges = new Dictionary<string, Tf2Ps3SourceFunctionRange>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1].StartLineIndex : sourceLines.Length;
            ranges[start.Address] = new Tf2Ps3SourceFunctionRange(start.StartLineIndex, end);
        }

        return ranges;
    }

    private static bool CallsFunction(string line, string address)
    {
        return line.Length > 0
            && char.IsWhiteSpace(line[0])
            && (line.Contains("_opd_FUN_" + address, StringComparison.OrdinalIgnoreCase)
                || line.Contains("FUN_" + address, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Tf2Ps3CriticalContract[]> ReadContractsAsync(string path)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        return document.RootElement
            .GetProperty("Contracts")
            .EnumerateArray()
            .Select(static contract => new Tf2Ps3CriticalContract(
                ReadString(contract, "ClassName"),
                ReadString(contract, "Role"),
                NormalizeAddress(ReadString(contract, "ExpectedReadEntry")),
                NormalizeAddress(ReadString(contract, "ExpectedWriteEntry"))))
            .Where(static contract => contract.ExpectedWriteEntry.Length != 0)
            .ToArray();
    }

    private static Tf2Ps3SourceCriticalBootstrapRouteCallsite[] FindDirectCallsites(
        string[] sourceLines,
        IReadOnlyCollection<Tf2Ps3CriticalContract> contracts)
    {
        var targetWrites = contracts
            .Select(static contract => contract.ExpectedWriteEntry)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var callsites = new List<Tf2Ps3SourceCriticalBootstrapRouteCallsite>();
        var currentFunction = "";

        for (var i = 0; i < sourceLines.Length; i++)
        {
            var line = sourceLines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                var definition = FunctionNameRegex().Match(line);
                if (definition.Success)
                {
                    currentFunction = NormalizeAddress(definition.Groups["address"].Value);
                }
            }

            foreach (var targetWrite in targetWrites)
            {
                if (!line.Contains("_opd_FUN_" + targetWrite, StringComparison.Ordinal)
                    && !line.Contains("FUN_" + targetWrite, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(currentFunction, targetWrite, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                callsites.Add(new Tf2Ps3SourceCriticalBootstrapRouteCallsite(
                    targetWrite,
                    i + 1,
                    currentFunction,
                    line.Trim(),
                    Preview(sourceLines, i)));
            }
        }

        return callsites.ToArray();
    }

    private static string DescribeImplementationMeaning(
        string className,
        IReadOnlyCollection<Tf2Ps3SourceCriticalBootstrapRouteCallsite> directCallsites)
    {
        if (directCallsites.Count == 0)
        {
            return className.StartsWith("SVC_", StringComparison.Ordinal)
                ? "Writer exists and field contract is known, but TF.elf reaches it indirectly or through a vtable in the current export. Keep using the contract codec, but do not infer live placement from direct callsites."
                : "Client-side parser/writer contract is known. This class is expected on incoming client payloads rather than server bootstrap generation.";
        }

        return className switch
        {
            "SVC_ServerInfo" => "Direct bootstrap callsites prove SVC_ServerInfo is emitted early into a large Source bit-buffer before downstream send-table/class/string-table/entity setup.",
            "SVC_ClassInfo" => "Direct callsite evidence places class metadata generation in the same early Source object setup region as SVC_ServerInfo.",
            "SVC_CreateStringTable" or "SVC_UpdateStringTable" => "Direct callsites are localized in string-table writer helpers; live ordering must be recovered from the caller that owns the string-table object.",
            _ => "Direct writer callsites are present and should be used to recover live ordering before enabling production emission."
        };
    }

    private static string Preview(string[] sourceLines, int index)
    {
        var start = Math.Max(0, index - 3);
        var end = Math.Min(sourceLines.Length, index + 4);
        return string.Join('\n', sourceLines[start..end].Select(static line => line.TrimEnd()));
    }

    private static string NormalizeAddress(string value)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        return value.ToLowerInvariant().PadLeft(8, '0');
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    [GeneratedRegex(@"(?:_opd_)?FUN_(?<address>[0-9a-fA-F]{8})\(")]
    private static partial Regex FunctionNameRegex();
}

public sealed record Tf2Ps3SourceCriticalBootstrapRouteReport(
    string Status,
    string Note,
    string Input,
    string CriticalContractInput,
    Tf2Ps3SourceCriticalBootstrapRouteSummary Summary,
    Tf2Ps3SourceCriticalBootstrapRouteContract[] Contracts,
    Tf2Ps3SourceCriticalBootstrapRouteBoundary[] Boundaries,
    string[] RemainingWork,
    Tf2Ps3SourceCriticalBootstrapSequence[] BootstrapSequences);

public sealed record Tf2Ps3SourceCriticalBootstrapRouteSummary(
    int ContractCount,
    int ContractsWithDirectWriteCallsites,
    int DirectCallsiteCount,
    int ServerContractCount,
    int ClientContractCount);

public sealed record Tf2Ps3SourceCriticalBootstrapRouteContract(
    string ClassName,
    string Role,
    string ExpectedReadEntry,
    string ExpectedWriteEntry,
    int DirectCallsiteCount,
    Tf2Ps3SourceCriticalBootstrapRouteCallsite[] DirectCallsites,
    string Status,
    string ImplementationMeaning);

public sealed record Tf2Ps3SourceCriticalBootstrapRouteCallsite(
    string TargetWriteFunction,
    int Line,
    string ContainingFunction,
    string Statement,
    string Preview);

public sealed record Tf2Ps3SourceCriticalBootstrapRouteBoundary(
    string Name,
    string Evidence,
    string Meaning);

public sealed record Tf2Ps3SourceCriticalBootstrapSequence(
    string ContainingFunction,
    int StartLine,
    int EndLine,
    Tf2Ps3SourceCriticalBootstrapSequenceEvent[] Events,
    string Inference);

public sealed record Tf2Ps3SourceCriticalBootstrapSequenceEvent(
    int Line,
    string Role,
    string Name,
    string TargetFunction,
    string Statement,
    string Preview);

public sealed record Tf2Ps3CriticalContract(
    string ClassName,
    string Role,
    string ExpectedReadEntry,
    string ExpectedWriteEntry);

internal sealed record Tf2Ps3SourceFunctionStart(int StartLineIndex, string Address);

internal sealed record Tf2Ps3SourceFunctionRange(int StartLineIndex, int EndLineIndex);
