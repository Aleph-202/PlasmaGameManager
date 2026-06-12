using System.Text.Json;

namespace PlasmaGameManager.ReTools;

public static class Tf2SourceLaunchProfileExtractor
{
    public static async Task<PcapEaTextTf2SourceLaunchProfile> ExtractAsync(
        string reportPath,
        string scenarioFile,
        string outputPath)
    {
        var report = JsonSerializer.Deserialize<PcapEaTextCorpusReport>(await File.ReadAllTextAsync(reportPath))
            ?? throw new InvalidOperationException($"Could not deserialize EA text report: {reportPath}");
        var profile = Extract(report, scenarioFile);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(profile, JsonOptions));
        return profile;
    }

    public static PcapEaTextTf2SourceLaunchProfile Extract(PcapEaTextCorpusReport report, string scenarioFile)
    {
        var normalized = NormalizePath(scenarioFile);
        var file = report.Files.FirstOrDefault(candidate => PathMatches(NormalizePath(candidate.File), normalized))
            ?? throw new InvalidOperationException($"No EA text report file matched scenario '{scenarioFile}'.");
        var scenario = file.Tf2CreateScenarios.FirstOrDefault()
            ?? throw new InvalidOperationException($"EA text report file '{scenarioFile}' does not contain a TF2 create scenario.");
        return scenario.SourceLaunchProfile;
    }

    private static bool PathMatches(string candidate, string requested)
    {
        return candidate.Equals(requested, StringComparison.Ordinal)
            || candidate.EndsWith($"/{requested}", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
