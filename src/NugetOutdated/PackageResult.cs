namespace NugetOutdated;

public class PackageResult
{
    public string Project { get; set; }
    public string Package { get; set; }
    public string CurrentVersion { get; set; }
    public string LatestVersion { get; set; }
    public bool IsUpToDate { get; set; }
}
