using PlasmaGameManager.Protocol;

namespace PlasmaGameManager.Server;

public static class Tf2SourceGameEventResourceCatalog
{
    private static readonly string[] DefaultResourceFiles =
    [
        "serverevents.res",
        "gameevents.res",
        "ModEvents.res"
    ];

    public static IReadOnlyList<Ps3SourceGameEventDescriptor> LoadOrDefault(
        string contentRoot,
        IReadOnlyList<Ps3SourceGameEventDescriptor> fallback)
    {
        if (string.IsNullOrWhiteSpace(contentRoot)
            || !Directory.Exists(contentRoot))
        {
            return fallback;
        }

        var loaded = Load(contentRoot, DefaultResourceFiles);
        return loaded.Count == 0 ? fallback : loaded;
    }

    public static IReadOnlyList<Ps3SourceGameEventDescriptor> Load(
        string contentRoot,
        IReadOnlyList<string>? resourceFiles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        resourceFiles ??= DefaultResourceFiles;

        var events = new List<MutableEventDescriptor>();
        var byName = new Dictionary<string, MutableEventDescriptor>(StringComparer.Ordinal);
        foreach (var resourceFile in resourceFiles)
        {
            var path = ResolveResourcePath(contentRoot, resourceFile);
            if (path is null)
            {
                continue;
            }

            ParseResourceFile(path, events, byName);
        }

        return events
            .Select((descriptor, index) => descriptor.ToDescriptor(index))
            .Where(static descriptor => descriptor is not null)
            .Cast<Ps3SourceGameEventDescriptor>()
            .ToArray();
    }

    private static void ParseResourceFile(
        string path,
        List<MutableEventDescriptor> events,
        Dictionary<string, MutableEventDescriptor> byName)
    {
        var tokens = Tokenize(File.ReadAllText(path));
        var index = 0;
        if (!TryReadToken(tokens, ref index, out _)
            || !TryReadToken(tokens, ref index, out var open)
            || open != "{")
        {
            return;
        }

        while (TryReadToken(tokens, ref index, out var eventName))
        {
            if (eventName == "}")
            {
                return;
            }

            if (!TryReadToken(tokens, ref index, out open)
                || open != "{")
            {
                return;
            }

            var descriptor = GetOrAddEvent(eventName, events, byName);
            descriptor.Fields.Clear();
            descriptor.IsLocal = false;
            while (TryReadToken(tokens, ref index, out var fieldName))
            {
                if (fieldName == "}")
                {
                    break;
                }

                if (!TryReadToken(tokens, ref index, out var typeName))
                {
                    return;
                }

                if (fieldName.Equals("local", StringComparison.Ordinal)
                    && !string.Equals(typeName, "0", StringComparison.Ordinal))
                {
                    descriptor.IsLocal = true;
                    continue;
                }

                if (fieldName is "reliable" or "unreliable" or "suppress")
                {
                    continue;
                }

                if (TryMapFieldType(typeName, out var fieldType)
                    && fieldType != Ps3SourceGameEventFieldType.Local)
                {
                    descriptor.Fields.Add(new Ps3SourceGameEventFieldDescriptor(fieldType, fieldName));
                }
            }
        }
    }

    private static MutableEventDescriptor GetOrAddEvent(
        string eventName,
        List<MutableEventDescriptor> events,
        Dictionary<string, MutableEventDescriptor> byName)
    {
        if (byName.TryGetValue(eventName, out var descriptor))
        {
            return descriptor;
        }

        descriptor = new MutableEventDescriptor(eventName);
        byName.Add(eventName, descriptor);
        events.Add(descriptor);
        return descriptor;
    }

    private static bool TryMapFieldType(string value, out Ps3SourceGameEventFieldType type)
    {
        type = value switch
        {
            "local" or "none" => Ps3SourceGameEventFieldType.Local,
            "string" => Ps3SourceGameEventFieldType.String,
            "float" => Ps3SourceGameEventFieldType.Float,
            "long" => Ps3SourceGameEventFieldType.Long,
            "short" => Ps3SourceGameEventFieldType.Short,
            "byte" => Ps3SourceGameEventFieldType.Byte,
            "bool" => Ps3SourceGameEventFieldType.Bool,
            _ => Ps3SourceGameEventFieldType.Local
        };
        return value is "local" or "none" or "string" or "float" or "long" or "short" or "byte" or "bool";
    }

    private static string? ResolveResourcePath(string contentRoot, string filename)
    {
        var resourceRoot = ResolveChildDirectory(contentRoot, "resource");
        if (resourceRoot is null)
        {
            return null;
        }

        var direct = Path.Combine(resourceRoot, filename);
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.EnumerateFiles(resourceRoot)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), filename, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveChildDirectory(string root, string name)
    {
        var direct = Path.Combine(root, name);
        if (Directory.Exists(direct))
        {
            return direct;
        }

        return Directory.EnumerateDirectories(root)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadToken(IReadOnlyList<string> tokens, ref int index, out string token)
    {
        if (index < 0 || index >= tokens.Count)
        {
            token = string.Empty;
            return false;
        }

        token = tokens[index++];
        return true;
    }

    private static string[] Tokenize(string text)
    {
        var tokens = new List<string>();
        for (var index = 0; index < text.Length;)
        {
            var current = text[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                index += 2;
                while (index < text.Length && text[index] is not '\r' and not '\n')
                {
                    index++;
                }

                continue;
            }

            if (current is '{' or '}')
            {
                tokens.Add(current.ToString());
                index++;
                continue;
            }

            if (current == '"')
            {
                index++;
                var start = index;
                while (index < text.Length && text[index] != '"')
                {
                    index++;
                }

                tokens.Add(text[start..index]);
                if (index < text.Length)
                {
                    index++;
                }

                continue;
            }

            var bareStart = index;
            while (index < text.Length
                && !char.IsWhiteSpace(text[index])
                && text[index] is not '{' and not '}')
            {
                index++;
            }

            tokens.Add(text[bareStart..index]);
        }

        return tokens.ToArray();
    }

    private sealed class MutableEventDescriptor(string name)
    {
        public string Name { get; } = name;

        public bool IsLocal { get; set; }

        public List<Ps3SourceGameEventFieldDescriptor> Fields { get; } = [];

        public Ps3SourceGameEventDescriptor? ToDescriptor(int eventId)
        {
            return IsLocal
                ? null
                : new Ps3SourceGameEventDescriptor(eventId, Name, Fields.ToArray());
        }
    }
}
