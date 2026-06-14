using System.Text.Json;
using PlasmaGameManager.Server;

namespace PlasmaGameManager.ReTools;

public static class Tf2Ps3SourceContentValidator
{
    private static readonly string[] RequiredMaps =
    [
        "ctf_2fort",
        "cp_dustbowl"
    ];

    private static readonly string[] RequiredGameEventResources =
    [
        "serverevents.res",
        "gameevents.res",
        "ModEvents.res"
    ];

    private static readonly string[] RequiredRuntimeResources =
    [
        "scripts/game_sounds_manifest.txt",
        "scripts/surfaceproperties.txt"
    ];

    private static readonly int[] GeneratedResourceStringBudgets =
    [
        515,
        562,
        581,
        583
    ];

    public static Tf2Ps3SourceContentValidationReport Validate(
        string repoRoot,
        string? contentRoot = null,
        string? mapRoot = null)
    {
        var sources = LocalInputSourcePaths.FromEnvironment();
        var resolvedContentRoot = ResolveMaybeRepoRelativePath(
            repoRoot,
            string.IsNullOrWhiteSpace(contentRoot) ? sources.Tf2Ps3SourceContentRoot : contentRoot);
        var resolvedMapRoot = ResolveMaybeRepoRelativePath(
            repoRoot,
            string.IsNullOrWhiteSpace(mapRoot) ? sources.Tf2Ps3MapRoot : mapRoot);

        var references = new List<Tf2Ps3SourceContentReference>();
        foreach (var mapName in RequiredMaps)
        {
            references.Add(BuildFileReference(
                Name: $"map:{mapName}",
                Category: "map-bsp",
                Classification: "GAME",
                ReferencePath: $"maps/{mapName}.bsp",
                ContentRoots: [resolvedContentRoot, resolvedMapRoot],
                RequiredForNativeMapLoad: true,
                VirtualServerOnly: false,
                Notes: "SVC_ServerInfo and native resource-string tables name the normal Source BSP path; PS3 retail content stores it as MAPS/<MAP>.PS3.BSP.",
                RelativeCandidates: MapBspCandidates(mapName)));
        }

        foreach (var resource in RequiredGameEventResources)
        {
            references.Add(BuildFileReference(
                Name: $"game-event-resource:{resource}",
                Category: "game-event-resource",
                Classification: "GAME",
                ReferencePath: $"resource/{resource}",
                ContentRoots: [resolvedContentRoot],
                RequiredForNativeMapLoad: true,
                VirtualServerOnly: false,
                Notes: "Used by the native SVC_GameEventList builder before falling back to built-in descriptors.",
                RelativeCandidates: ResourceCandidates("resource/" + resource)));
        }

        foreach (var resource in RequiredRuntimeResources)
        {
            references.Add(BuildFileReference(
                Name: $"runtime-resource:{resource}",
                Category: "runtime-resource",
                Classification: "GAME",
                ReferencePath: resource,
                ContentRoots: [resolvedContentRoot],
                RequiredForNativeMapLoad: true,
                VirtualServerOnly: false,
                Notes: "Required by the generated Source init resource set and/or map-load runtime.",
                RelativeCandidates: ResourceCandidates(resource)));
        }

        foreach (var reference in BuildGeneratedResourceStringReferences())
        {
            references.Add(reference with
            {
                ResolvedPath = reference.VirtualServerOnly
                    ? ""
                    : ResolveContentFile([resolvedContentRoot, resolvedMapRoot], GeneratedResourceCandidates(reference.ReferencePath)) ?? "",
            });
        }

        foreach (var reference in BuildPrecacheStringTableReferences())
        {
            references.Add(reference with
            {
                ResolvedPath = ResolveContentFile([resolvedContentRoot, resolvedMapRoot], PrecacheResourceCandidates(reference.ReferencePath)) ?? "",
            });
        }

        references = references
            .Select(FinalizeReference)
            .OrderBy(static reference => reference.Category, StringComparer.Ordinal)
            .ThenBy(static reference => reference.Name, StringComparer.Ordinal)
            .ToList();

        var loadedGameEvents = LoadGameEventDescriptorCount(resolvedContentRoot);
        var blockingMissing = references.Count(static reference =>
            reference.RequiredForNativeMapLoad
            && !reference.Exists
            && !reference.VirtualServerOnly);
        var unresolvedReferenced = references.Count(static reference => reference.ReferencesUnresolvedExtractorName);
        var fallbackGameEvents = loadedGameEvents == 0 ? Tf2Ps3SourceCatalog.BootstrapGameEventDescriptors.Count : 0;
        var summary = new Tf2Ps3SourceContentValidationSummary(
            ContentRoot: resolvedContentRoot,
            MapRoot: resolvedMapRoot,
            ContentRootExists: Directory.Exists(resolvedContentRoot),
            MapRootExists: Directory.Exists(resolvedMapRoot),
            RequiredReferenceCount: references.Count(static reference => reference.RequiredForNativeMapLoad && !reference.VirtualServerOnly),
            RequiredReferencePresentCount: references.Count(static reference => reference.RequiredForNativeMapLoad && !reference.VirtualServerOnly && reference.Exists),
            MissingRequiredReferenceCount: blockingMissing,
            GeneratedResourceReferenceCount: references.Count(static reference => reference.Category == "generated-resource-string"),
            GeneratedResourceReferencePresentCount: references.Count(static reference => reference.Category == "generated-resource-string" && reference.Exists),
            VirtualServerOnlyReferenceCount: references.Count(static reference => reference.VirtualServerOnly),
            Ps3MapAliasReferenceCount: references.Count(static reference => reference.UsesPs3MapAlias),
            UnresolvedReferencedCount: unresolvedReferenced,
            LoadedGameEventDescriptorCount: loadedGameEvents,
            FallbackGameEventDescriptorCount: fallbackGameEvents,
            UsesFallbackGameEventDescriptors: loadedGameEvents == 0,
            NativeSourceContentReady: blockingMissing == 0 && unresolvedReferenced == 0 && Directory.Exists(resolvedContentRoot));

        return new Tf2Ps3SourceContentValidationReport(
            Status: "tf2ps3-source-content-validation",
            OverallStatus: summary.NativeSourceContentReady ? "ready" : "missing-native-source-content",
            Summary: summary,
            References: references);
    }

    public static void WriteReport(string outputPath, Tf2Ps3SourceContentValidationReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<Tf2Ps3SourceContentReference> BuildGeneratedResourceStringReferences()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapName in RequiredMaps)
        {
            foreach (var budget in GeneratedResourceStringBudgets)
            {
                foreach (var entry in Tf2Ps3SourceCatalog.BuildBootstrapResourceStringEntries(mapName, budget))
                {
                    var key = $"{mapName}\0{entry.Classification}\0{entry.ResourceName}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var virtualServerOnly = IsVirtualServerOnly(entry.ResourceName, entry.Classification);
                    yield return new Tf2Ps3SourceContentReference(
                        Name: $"generated:{mapName}:{entry.Classification}:{entry.ResourceName}",
                        Category: "generated-resource-string",
                        Classification: entry.Classification,
                        ReferencePath: entry.ResourceName,
                        RequiredForNativeMapLoad: !virtualServerOnly,
                        VirtualServerOnly: virtualServerOnly,
                        Exists: false,
                        ResolvedPath: "",
                        Bytes: 0,
                        UsesPs3MapAlias: false,
                        ReferencesUnresolvedExtractorName: ContainsUnresolvedName(entry.ResourceName),
                        Status: "",
                        Notes: virtualServerOnly
                            ? "Generated by the native Source resource-string table but satisfied by server state rather than a PS3 content file."
                            : "Generated by the native Source resource-string table and must resolve against extracted PS3 content.");
                }
            }
        }
    }

    private static IEnumerable<Tf2Ps3SourceContentReference> BuildPrecacheStringTableReferences()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mapName in RequiredMaps)
        {
            foreach (var table in Tf2Ps3SourceCatalog.BuildBootstrapPrecacheStringTables(mapName))
            {
                foreach (var entry in table.Entries)
                {
                    if (string.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    var referencePath = PrecacheReferencePath(table.TableName, entry);
                    var key = $"{table.TableName}\0{referencePath}";
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    yield return new Tf2Ps3SourceContentReference(
                        Name: $"precache:{table.TableName}:{entry}",
                        Category: "source-precache-string-table",
                        Classification: table.TableName.ToUpperInvariant(),
                        ReferencePath: referencePath,
                        RequiredForNativeMapLoad: true,
                        VirtualServerOnly: false,
                        Exists: false,
                        ResolvedPath: "",
                        Bytes: 0,
                        UsesPs3MapAlias: false,
                        ReferencesUnresolvedExtractorName: ContainsUnresolvedName(referencePath),
                        Status: "",
                        Notes: "Generated by the native SVC_CreateStringTable bootstrap; logical Source names must resolve against extracted PS3 content or a known PS3 alias.");
                }
            }
        }
    }

    private static Tf2Ps3SourceContentReference BuildFileReference(
        string Name,
        string Category,
        string Classification,
        string ReferencePath,
        IReadOnlyList<string> ContentRoots,
        bool RequiredForNativeMapLoad,
        bool VirtualServerOnly,
        string Notes,
        IReadOnlyList<string> RelativeCandidates)
    {
        return FinalizeReference(new Tf2Ps3SourceContentReference(
            Name,
            Category,
            Classification,
            ReferencePath,
            RequiredForNativeMapLoad,
            VirtualServerOnly,
            Exists: false,
            ResolvedPath: ResolveContentFile(ContentRoots, RelativeCandidates) ?? "",
            Bytes: 0,
            UsesPs3MapAlias: false,
            ReferencesUnresolvedExtractorName: ContainsUnresolvedName(ReferencePath),
            Status: "",
            Notes));
    }

    private static Tf2Ps3SourceContentReference FinalizeReference(Tf2Ps3SourceContentReference reference)
    {
        if (reference.VirtualServerOnly)
        {
            return reference with
            {
                Exists = true,
                Bytes = 0,
                Status = "virtual-server-only",
                UsesPs3MapAlias = false,
                ReferencesUnresolvedExtractorName = reference.ReferencesUnresolvedExtractorName
            };
        }

        var exists = !string.IsNullOrWhiteSpace(reference.ResolvedPath) && File.Exists(reference.ResolvedPath);
        var usesPs3MapAlias = exists
            && reference.ReferencePath.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(reference.ResolvedPath).Contains(".PS3.", StringComparison.OrdinalIgnoreCase);
        var referencesUnresolved = reference.ReferencesUnresolvedExtractorName
            || (exists && ContainsUnresolvedName(reference.ResolvedPath));
        return reference with
        {
            Exists = exists,
            Bytes = exists ? new FileInfo(reference.ResolvedPath).Length : 0,
            UsesPs3MapAlias = usesPs3MapAlias,
            ReferencesUnresolvedExtractorName = referencesUnresolved,
            Status = exists
                ? usesPs3MapAlias ? "resolved-ps3-map-alias" : "resolved"
                : reference.RequiredForNativeMapLoad ? "missing-required" : "missing-optional"
        };
    }

    private static int LoadGameEventDescriptorCount(string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
        {
            return 0;
        }

        try
        {
            return Tf2SourceGameEventResourceCatalog.Load(contentRoot).Count;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsVirtualServerOnly(string resourceName, string classification)
    {
        if (classification.Equals("SENDTABLE", StringComparison.Ordinal))
        {
            return true;
        }

        return resourceName.Equals("motd.txt", StringComparison.OrdinalIgnoreCase)
            || resourceName.EndsWith(".nav", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] MapBspCandidates(string mapName)
    {
        var upper = mapName.ToUpperInvariant();
        return
        [
            $"maps/{mapName}.bsp",
            $"MAPS/{upper}.BSP",
            $"MAPS/{upper}.PS3.BSP",
            $"GAME/TF/MAPS/{mapName}.bsp",
            $"GAME/TF/MAPS/{upper}.PS3.BSP",
            $"_virtual/GAME/TF/maps/{mapName}.bsp",
            $"_virtual/GAME/TF/MAPS/{upper}.PS3.BSP",
            $"{upper}.PS3.BSP"
        ];
    }

    private static string[] ResourceCandidates(string relativePath)
    {
        var upper = relativePath.ToUpperInvariant();
        return
        [
            relativePath,
            upper,
            $"GAME/TF/{relativePath}",
            $"GAME/TF/{upper}",
            $"tf/{relativePath}",
            $"TF/{upper}",
            $"_virtual/GAME/TF/{relativePath}",
            $"_virtual/GAME/TF/{upper}"
        ];
    }

    private static string[] GeneratedResourceCandidates(string referencePath)
    {
        if (referencePath.StartsWith("maps/", StringComparison.OrdinalIgnoreCase)
            && referencePath.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase))
        {
            var mapName = Path.GetFileNameWithoutExtension(referencePath);
            return MapBspCandidates(mapName);
        }

        return ResourceCandidates(referencePath);
    }

    private static string[] PrecacheResourceCandidates(string referencePath)
    {
        var candidates = new List<string>(ResourceCandidates(referencePath));
        if (referencePath.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase))
        {
            var ps3ModelPath = referencePath[..^".mdl".Length] + ".ps3.mdl";
            candidates.AddRange(ResourceCandidates(ps3ModelPath));
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string PrecacheReferencePath(string tableName, string entry)
    {
        return tableName switch
        {
            "soundprecache" => "sound/" + entry,
            "decalprecache" => "materials/" + entry + ".vmt",
            _ => entry
        };
    }

    private static string? ResolveContentFile(IReadOnlyList<string> contentRoots, IReadOnlyList<string> relativeCandidates)
    {
        foreach (var root in contentRoots.Where(static root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.Ordinal))
        {
            var resolved = ResolveContentFile(root, relativeCandidates);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
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
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

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

    private static string ResolveMaybeRepoRelativePath(string repoRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.Combine(repoRoot, ".local/input/TF2PS3/GAME/TF");
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static bool ContainsUnresolvedName(string value)
    {
        return value.Contains("unresolved", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unknown", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record Tf2Ps3SourceContentValidationReport(
    string Status,
    string OverallStatus,
    Tf2Ps3SourceContentValidationSummary Summary,
    IReadOnlyList<Tf2Ps3SourceContentReference> References);

public sealed record Tf2Ps3SourceContentValidationSummary(
    string ContentRoot,
    string MapRoot,
    bool ContentRootExists,
    bool MapRootExists,
    int RequiredReferenceCount,
    int RequiredReferencePresentCount,
    int MissingRequiredReferenceCount,
    int GeneratedResourceReferenceCount,
    int GeneratedResourceReferencePresentCount,
    int VirtualServerOnlyReferenceCount,
    int Ps3MapAliasReferenceCount,
    int UnresolvedReferencedCount,
    int LoadedGameEventDescriptorCount,
    int FallbackGameEventDescriptorCount,
    bool UsesFallbackGameEventDescriptors,
    bool NativeSourceContentReady);

public sealed record Tf2Ps3SourceContentReference(
    string Name,
    string Category,
    string Classification,
    string ReferencePath,
    bool RequiredForNativeMapLoad,
    bool VirtualServerOnly,
    bool Exists,
    string ResolvedPath,
    long Bytes,
    bool UsesPs3MapAlias,
    bool ReferencesUnresolvedExtractorName,
    string Status,
    string Notes);
