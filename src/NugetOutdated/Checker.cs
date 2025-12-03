using System.Collections.Concurrent;
using System.Xml.Linq;
using NuGet.Versioning;
using NugetOutdated.Models;
using NugetOutdated.Services;

namespace NugetOutdated;

public class Checker
{
    private readonly NuGetClient _nuGetClient;

    public Checker(NuGetClient nuGetClient)
    {
        _nuGetClient = nuGetClient;
    }

    public async Task<List<PackageResult>> CheckAsync(
        string directory,
        List<(string Project, string Package)> ignoreList,
        bool includePrerelease
    )
    {
        var cpmSettings = CpmParser.Parse(directory);
        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);
        var results = new ConcurrentBag<PackageResult>();

        await Parallel.ForEachAsync(csprojFiles, async (file, ct) =>
        {
            var projectResults = await ProcessProjectAsync(file, cpmSettings, ignoreList, includePrerelease);
            foreach (var result in projectResults)
            {
                results.Add(result);
            }
        });

        return results.OrderBy(r => r.Project).ThenBy(r => r.Package).ToList();
    }

    private async Task<List<PackageResult>> ProcessProjectAsync(
        string filePath,
        CpmSettings cpmSettings,
        List<(string Project, string Package)> ignoreList,
        bool includePrerelease)
    {
        var results = new List<PackageResult>();
        var projectName = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            var doc = XDocument.Load(filePath);
            var packages = doc.Descendants().Where(e => e.Name.LocalName == "PackageReference");

            foreach (var pkg in packages)
            {
                var id = pkg.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                var versionStr = ResolveVersion(pkg, id, projectName, cpmSettings);
                if (string.IsNullOrEmpty(versionStr)) continue;

                var isIgnored = IsIgnored(id, projectName, ignoreList);

                if (!NuGetVersion.TryParse(versionStr, out var currentVersion))
                {
                    if (isIgnored)
                    {
                        results.Add(CreateResult(projectName, id, versionStr, null, true, true));
                    }
                    continue;
                }

                var latestVersion = await _nuGetClient.GetLatestVersionAsync(id, includePrerelease);

                if (latestVersion == null && !isIgnored)
                {
                    Console.WriteLine($"Warning: Could not find package '{id}' on NuGet.org.");
                    continue;
                }

                bool isUpToDate = latestVersion == null || currentVersion >= latestVersion;
                results.Add(CreateResult(projectName, id, currentVersion.ToString(), latestVersion?.ToString(), isUpToDate, isIgnored));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {filePath}: {ex.Message}");
        }

        return results;
    }

    private string? ResolveVersion(XElement pkg, string id, string projectName, CpmSettings cpmSettings)
    {
        var version = pkg.Attribute("Version")?.Value;
        if (!string.IsNullOrEmpty(version)) return version;

        if (cpmSettings.ProjectVersions.TryGetValue(projectName, out var projectVersions) &&
            projectVersions.TryGetValue(id, out var projectVersion))
        {
            return projectVersion;
        }

        if (cpmSettings.GlobalVersions.TryGetValue(id, out var globalVersion))
        {
            return globalVersion;
        }

        return null;
    }

    private bool IsIgnored(string packageId, string projectName, List<(string Project, string Package)> ignoreList)
    {
        return ignoreList.Any(x =>
            x.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
            && x.Package.Equals(packageId, StringComparison.OrdinalIgnoreCase)
        );
    }

    private PackageResult CreateResult(string project, string package, string current, string? latest, bool isUpToDate, bool isIgnored)
    {
        return new PackageResult
        {
            Project = project,
            Package = package,
            CurrentVersion = current,
            LatestVersion = latest ?? string.Empty,
            IsUpToDate = isUpToDate,
            IsIgnored = isIgnored
        };
    }
}