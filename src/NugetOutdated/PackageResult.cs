namespace NugetOutdated;

public class PackageResult
{
    public required string Project { get; set; }
    public required string Package { get; set; }
    public required string CurrentVersion { get; set; }
    public required string LatestVersion { get; set; }
    public bool IsUpToDate { get; set; }
    public bool IsIgnored { get; set; }
}