namespace NugetOutdated.Models;

public class CpmSettings
{
    public Dictionary<string, string> GlobalVersions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string>> ProjectVersions { get; } = new(StringComparer.OrdinalIgnoreCase);
}
