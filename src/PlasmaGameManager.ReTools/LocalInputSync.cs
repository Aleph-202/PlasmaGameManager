namespace PlasmaGameManager.ReTools;

public static class LocalInputSync
{
    private static readonly string[] RequiredSourceMaps =
    [
        "ctf_2fort",
        "cp_dustbowl"
    ];

    private static readonly string[] RecommendedSourceResources =
    [
        "resource/gameevents.res",
        "resource/serverevents.res",
        "resource/modevents.res",
        "scripts/game_sounds_manifest.txt",
        "scripts/surfaceproperties.txt"
    ];

    private static readonly string[] RequiredPcapRelativePaths =
    [
        "TF2_PS3_network_traffic/packets/server/connections/quick_match_to_motd_2fort_1.pcapng",
        "TF2_PS3_network_traffic/packets/server/connections/quick_match_to_motd_2fort_2.pcapng",
        "TF2_PS3_network_traffic/packets/server/connections/custom_match_joining_cp_db_to_motd_1.pcapng",
        "TF2_PS3_network_traffic/packets/server/creation/creating_and_join_cp_db_unranked_1.pcapng",
        "2Fort.PCAPNG",
        "dustbowl_final.PCAPNG",
        "tf2-ps3-packets/connect.pcapng",
        "tf2-ps3-packets/connecting.pcapng"
    ];

    public static LocalInputReport Sync(string repoRoot)
    {
        return Sync(repoRoot, LocalInputSourcePaths.FromEnvironment());
    }

    public static LocalInputReport Sync(string repoRoot, LocalInputSourcePaths sources)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, ".local/ghidra"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".local/ooanalyzer"));
        CopyDirectory(sources.Bfbc2R34Directory, Path.Combine(repoRoot, ".local/input/BFBC2_R34"));
        CopyIfExists(
            Path.Combine(sources.Tf2Ps3Usrdir, "BIN/TF.elf"),
            Path.Combine(repoRoot, ".local/input/TF2PS3/TF.elf"));
        CopyIfExists(
            Path.Combine(sources.Tf2Ps3Usrdir, "EBOOT.elf"),
            Path.Combine(repoRoot, ".local/input/TF2PS3/EBOOT.elf"));
        CopyDirectory(
            sources.PcapCorpusDirectory,
            Path.Combine(repoRoot, ".local/input/pcaps"));

        var report = ValidateSynced(repoRoot, sources);
        WriteReport(repoRoot, report);
        return report;
    }

    public static LocalInputReport ValidateSynced(string repoRoot)
    {
        var report = ValidateSynced(repoRoot, LocalInputSourcePaths.FromEnvironment());
        WriteReport(repoRoot, report);
        return report;
    }

    public static LocalInputReport ValidateSynced(string repoRoot, LocalInputSourcePaths sources)
    {
        var root = Path.Combine(repoRoot, ".local/input");
        var pcaps = Path.Combine(root, "pcaps");
        var contentRoot = ResolveMaybeRepoRelativePath(repoRoot, sources.Tf2Ps3SourceContentRoot);
        var mapRoot = ResolveMaybeRepoRelativePath(repoRoot, sources.Tf2Ps3MapRoot);
        var inputs = new List<LocalInputStatus>
        {
            FileStatus("bfbc2-main-exe", sources.Bfbc2R34Directory, Path.Combine(root, "BFBC2_R34/Frost.Game.Main_Win32_Final.exe"), required: true),
            FileStatus("tf2ps3-tf-elf", Path.Combine(sources.Tf2Ps3Usrdir, "BIN/TF.elf"), Path.Combine(root, "TF2PS3/TF.elf"), required: true),
            FileStatus("tf2ps3-eboot-elf", Path.Combine(sources.Tf2Ps3Usrdir, "EBOOT.elf"), Path.Combine(root, "TF2PS3/EBOOT.elf"), required: false),
            DirectoryStatus("pcap-corpus", sources.PcapCorpusDirectory, pcaps, required: true),
            DirectoryStatus("tf2ps3-source-content-root", sources.Tf2Ps3SourceContentRoot, contentRoot, required: false),
            DirectoryStatus("tf2ps3-map-root", sources.Tf2Ps3MapRoot, mapRoot, required: false),
            DirectoryStatus("ghidra-scratch", ".local/ghidra", Path.Combine(repoRoot, ".local/ghidra"), required: true),
            DirectoryStatus("ooanalyzer-scratch", ".local/ooanalyzer", Path.Combine(repoRoot, ".local/ooanalyzer"), required: true)
        };

        foreach (var relativePath in RequiredPcapRelativePaths)
        {
            inputs.Add(FileStatus($"pcap:{relativePath}", Path.Combine(sources.PcapCorpusDirectory, relativePath), Path.Combine(pcaps, relativePath), required: true));
        }

        foreach (var mapName in RequiredSourceMaps)
        {
            inputs.Add(ContentFileStatus(
                $"content-map:{mapName}",
                [contentRoot, mapRoot],
                required: false,
                $"{mapName}.bsp",
                $"maps/{mapName}.bsp",
                $"MAPS/{mapName}.bsp",
                $"GAME/TF/MAPS/{mapName}.bsp",
                $"tf/maps/{mapName}.bsp"));
        }

        foreach (var relativePath in RecommendedSourceResources)
        {
            inputs.Add(ContentFileStatus(
                $"content-resource:{relativePath}",
                contentRoot,
                required: false,
                relativePath,
                relativePath.ToUpperInvariant(),
                $"GAME/TF/{relativePath}",
                $"GAME/TF/{relativePath.ToUpperInvariant()}",
                $"tf/{relativePath}"));
        }

        var required = inputs.Where(static input => input.Required).ToArray();
        var requiredMaps = inputs.Where(static input => input.Name.StartsWith("content-map:", StringComparison.Ordinal)).ToArray();
        var recommendedContentResources = inputs.Where(static input => input.Name.StartsWith("content-resource:", StringComparison.Ordinal)).ToArray();
        var contentRootExists = Directory.Exists(contentRoot);
        var requiredMapPresentCount = requiredMaps.Count(static input => input.Exists);
        var recommendedResourcePresentCount = recommendedContentResources.Count(static input => input.Exists);
        return new LocalInputReport(
            "local-input-sync-report",
            required.All(static input => input.Exists) ? "ready" : "missing-required-inputs",
            new LocalInputSummary(
                inputs.Count,
                required.Length,
                required.Count(static input => input.Exists),
                required.Count(static input => !input.Exists),
                Directory.Exists(pcaps) ? CountPcapFiles(pcaps) : 0,
                contentRoot,
                mapRoot,
                contentRootExists,
                RequiredSourceMaps.Length,
                requiredMapPresentCount,
                RecommendedSourceResources.Length,
                recommendedResourcePresentCount,
                contentRootExists && requiredMapPresentCount == RequiredSourceMaps.Length),
            inputs);
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            Console.WriteLine($"missing: {source}");
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            Console.WriteLine($"missing: {source}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static LocalInputStatus FileStatus(string name, string source, string destination, bool required)
    {
        var info = new FileInfo(destination);
        return new LocalInputStatus(name, "file", source, destination, required, info.Exists, info.Exists ? info.Length : 0, 0);
    }

    private static LocalInputStatus DirectoryStatus(string name, string source, string destination, bool required)
    {
        var exists = Directory.Exists(destination);
        return new LocalInputStatus(name, "directory", source, destination, required, exists, 0, exists ? Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Count() : 0);
    }

    private static LocalInputStatus ContentFileStatus(string name, string contentRoot, bool required, params string[] relativeCandidates)
    {
        return ContentFileStatus(name, [contentRoot], required, relativeCandidates);
    }

    private static LocalInputStatus ContentFileStatus(string name, IReadOnlyList<string> contentRoots, bool required, params string[] relativeCandidates)
    {
        var resolved = contentRoots.Select(root => ResolveContentFile(root, relativeCandidates))
            .FirstOrDefault(static path => path is not null);
        var destination = resolved ?? Path.Combine(contentRoots[0], relativeCandidates[0]);
        var info = new FileInfo(destination);
        return new LocalInputStatus(name, "content-file", string.Join(Path.PathSeparator, contentRoots), destination, required, info.Exists, info.Exists ? info.Length : 0, 0);
    }

    private static string ResolveMaybeRepoRelativePath(string repoRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.Combine(repoRoot, ".local/input/TF2PS3/GAME/TF");
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string? ResolveContentFile(string contentRoot, IReadOnlyList<string> relativeCandidates)
    {
        if (!Directory.Exists(contentRoot))
        {
            return null;
        }

        foreach (var candidate in relativeCandidates)
        {
            var direct = Path.Combine(contentRoot, candidate);
            if (File.Exists(direct))
            {
                return direct;
            }

            var caseInsensitive = ResolveCaseInsensitivePath(contentRoot, candidate);
            if (caseInsensitive is not null)
            {
                return caseInsensitive;
            }
        }

        foreach (var candidate in relativeCandidates)
        {
            var fileName = Path.GetFileName(candidate);
            var match = Directory.EnumerateFiles(contentRoot, "*", SearchOption.AllDirectories)
                .FirstOrDefault(file => Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string? ResolveCaseInsensitivePath(string root, string relativePath)
    {
        var current = root;
        foreach (var segment in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(current))
            {
                return null;
            }

            var nextDirectory = Directory.EnumerateDirectories(current)
                .FirstOrDefault(directory => Path.GetFileName(directory).Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (nextDirectory is not null)
            {
                current = nextDirectory;
                continue;
            }

            var nextFile = Directory.EnumerateFiles(current)
                .FirstOrDefault(file => Path.GetFileName(file).Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (nextFile is not null)
            {
                current = nextFile;
                continue;
            }

            return null;
        }

        return File.Exists(current) ? current : null;
    }

    private static int CountPcapFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Count(static file => file.EndsWith(".pcap", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".pcapng", StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteReport(string repoRoot, LocalInputReport report)
    {
        var output = Path.Combine(repoRoot, "artifacts/local-input-status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed record LocalInputSourcePaths(
    string Bfbc2R34Directory,
    string Tf2Ps3Usrdir,
    string PcapCorpusDirectory,
    string Tf2Ps3SourceContentRoot,
    string Tf2Ps3MapRoot)
{
    public static LocalInputSourcePaths FromEnvironment()
    {
        var contentRoot = Environment.GetEnvironmentVariable("TF2PS3_SOURCE_CONTENT_ROOT")
            ?? Environment.GetEnvironmentVariable("PLASMA_TF2_SOURCE_CONTENT_ROOT")
            ?? ".local/input/TF2PS3/GAME/TF";

        return new LocalInputSourcePaths(
            Environment.GetEnvironmentVariable("PLASMA_BFBC2_R34_SOURCE")
                ?? ".local/source/BFBC2_R34",
            Environment.GetEnvironmentVariable("PLASMA_TF2PS3_USRDIR_SOURCE")
                ?? ".local/source/TF2PS3/USRDIR",
            Environment.GetEnvironmentVariable("PLASMA_PCAP_CORPUS_SOURCE")
                ?? ".local/source/pcaps",
            contentRoot,
            Environment.GetEnvironmentVariable("TF2PS3_MAP_ROOT")
                ?? Environment.GetEnvironmentVariable("PLASMA_TF2_MAP_ROOT")
                ?? contentRoot);
    }
}

public sealed record LocalInputReport(
    string Status,
    string OverallStatus,
    LocalInputSummary Summary,
    IReadOnlyList<LocalInputStatus> Inputs);

public sealed record LocalInputSummary(
    int InputCount,
    int RequiredInputCount,
    int RequiredPresentCount,
    int RequiredMissingCount,
    int PcapFileCount,
    string Tf2Ps3SourceContentRoot,
    string Tf2Ps3MapRoot,
    bool ContentRootExists,
    int RequiredMapCount,
    int RequiredMapPresentCount,
    int RecommendedResourceCount,
    int RecommendedResourcePresentCount,
    bool ContentReady);

public sealed record LocalInputStatus(
    string Name,
    string Kind,
    string Source,
    string Destination,
    bool Required,
    bool Exists,
    long Bytes,
    int FileCount);
