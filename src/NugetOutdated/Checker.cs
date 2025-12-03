using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuGet.Versioning;

namespace NugetOutdated;

public class Checker
{
    private readonly HttpClient _httpClient;

    public Checker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private class CpmSettings
    {
        public Dictionary<string, string> GlobalVersions { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, string>> ProjectVersions { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private CpmSettings LoadCentralPackageManagement(string directory)
    {
        var settings = new CpmSettings();
        var propsFile = Path.Combine(directory, "Directory.Packages.props");

        if (!File.Exists(propsFile))
        {
            return settings;
        }

        try
        {
            var doc = XDocument.Load(propsFile);
            var packageVersions = doc.Descendants()
                .Where(e => e.Name.LocalName == "PackageVersion");

            foreach (var pv in packageVersions)
            {
                var id = pv.Attribute("Include")?.Value ?? pv.Attribute("Update")?.Value;
                var version = pv.Attribute("Version")?.Value;
                var condition = pv.Attribute("Condition")?.Value;

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
                    continue;

                if (string.IsNullOrEmpty(condition))
                {
                    settings.GlobalVersions[id] = version;
                }
                else
                {
                    // Parse condition: Condition="'$(MSBuildProjectName)' == 'MyProject'"
                    var match = Regex.Match(
                        condition,
                        @"'\$\(MSBuildProjectName\)'\s*==\s*'([^']+)'"
                    );
                    if (match.Success)
                    {
                        var projectName = match.Groups[1].Value;
                        if (!settings.ProjectVersions.ContainsKey(projectName))
                        {
                            settings.ProjectVersions[projectName] = new Dictionary<string, string>(
                                StringComparer.OrdinalIgnoreCase
                            );
                        }
                        settings.ProjectVersions[projectName][id] = version;
                    }
                    else
                    {
                        Console.WriteLine(
                            $"⚠️ Skipping complex PackageVersion condition: {condition}"
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Directory.Packages.props: {ex.Message}");
        }

        return settings;
    }

    public async Task<List<PackageResult>> CheckAsync(
        string directory,
        List<(string Project, string Package)> ignoreList,
        bool includePrerelease
    )
    {
        var cpmSettings = LoadCentralPackageManagement(directory);

        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);

        var results = new List<PackageResult>();

        if (csprojFiles.Length == 0)
        {
            return results;
        }

        foreach (var file in csprojFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(file);
            try
            {
                var doc = XDocument.Load(file);

                // Find PackageReferences (ignoring namespace)
                var packages = doc.Descendants().Where(e => e.Name.LocalName == "PackageReference");

                foreach (var pkg in packages)
                {
                    var id = pkg.Attribute("Include")?.Value;
                    var versionStr = pkg.Attribute("Version")?.Value;

                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (string.IsNullOrEmpty(versionStr))
                    {
                        // Try to resolve from CPM
                        if (
                            cpmSettings.ProjectVersions.TryGetValue(
                                projectName,
                                out var projectVersions
                            ) && projectVersions.TryGetValue(id, out var projectVersion)
                        )
                        {
                            versionStr = projectVersion;
                        }
                        else if (cpmSettings.GlobalVersions.TryGetValue(id, out var globalVersion))
                        {
                            versionStr = globalVersion;
                        }
                    }

                    if (string.IsNullOrEmpty(versionStr))
                        continue;

                    bool isIgnored = ignoreList.Any(x =>
                        x.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                        && x.Package.Equals(id, StringComparison.OrdinalIgnoreCase)
                    );

                    if (!NuGetVersion.TryParse(versionStr, out var currentVersion))
                    {
                        if (isIgnored)
                        {
                            results.Add(new PackageResult
                            {
                                Project = projectName,
                                Package = id,
                                CurrentVersion = versionStr,
                                LatestVersion = string.Empty,
                                IsUpToDate = true,
                                IsIgnored = true
                            });
                        }
                        // Skip if version is a variable or unparseable
                        continue;
                    }

                    // Get latest version
                    var latestVersion = await GetLatestVersionAsync(id, includePrerelease);

                    if (latestVersion == null)
                    {
                        if (isIgnored)
                        {
                            results.Add(new PackageResult
                            {
                                Project = projectName,
                                Package = id,
                                CurrentVersion = currentVersion.ToString(),
                                LatestVersion = string.Empty,
                                IsUpToDate = true,
                                IsIgnored = true
                            });
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Could not find package '{id}' on NuGet.org.");
                        }
                        continue;
                    }

                    bool isUpToDate = currentVersion >= latestVersion;

                    results.Add(
                        new PackageResult
                        {
                            Project = projectName,
                            Package = id,
                            CurrentVersion = currentVersion.ToString(),
                            LatestVersion = latestVersion.ToString(),
                            IsUpToDate = isUpToDate,
                            IsIgnored = isIgnored,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {file}: {ex.Message}");
            }
        }

        return results;
    }

    private async Task<NuGetVersion> GetLatestVersionAsync(string packageId, bool includePrerelease)
    {
        try
        {
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var versions = json
                .RootElement.GetProperty("versions")
                .EnumerateArray()
                .Select(v => NuGetVersion.Parse(v.GetString()))
                .Where(v => includePrerelease || !v.IsPrerelease)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            return versions;
        }
        catch
        {
            return null;
        }
    }
}