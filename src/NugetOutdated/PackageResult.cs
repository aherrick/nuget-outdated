namespace NugetOutdated;

public record PackageResult(
    string Project,
    string Package,
    string CurrentVersion,
    string LatestVersion,
    bool IsUpToDate,
    bool IsIgnored)
{
    public string Status => IsIgnored ? "[grey]🔒[/]" : (IsUpToDate ? "[green]✅[/]" : "[red]❌[/]");
}