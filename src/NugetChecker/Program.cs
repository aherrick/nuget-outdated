using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Web;
using NuGet.Versioning;

// Parse arguments
string ignoreQuery = "";
bool includePrerelease = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--ignore" && i + 1 < args.Length)
    {
        ignoreQuery = args[i + 1];
        i++;
    }
    else if (args[i] == "--includeprelease" && i + 1 < args.Length)
    {
        bool.TryParse(args[i + 1], out includePrerelease);
        i++;
    }
}

// Parse ignore list
var ignoreList = new List<(string Project, string Package)>();
if (!string.IsNullOrWhiteSpace(ignoreQuery))
{
    var collection = HttpUtility.ParseQueryString(ignoreQuery);
    foreach (string key in collection)
    {
        if (!string.IsNullOrEmpty(key))
        {
            var values = collection.GetValues(key);
            if (values != null)
            {
                foreach (var val in values)
                {
                    ignoreList.Add((key, val));
                }
            }
        }
    }
}

// Find csproj files
var csprojFiles = Directory.GetFiles(
    Directory.GetCurrentDirectory(),
    "*.csproj",
    SearchOption.AllDirectories
);

if (csprojFiles.Length == 0)
{
    Console.WriteLine("No .csproj files found.");
    return 0;
}

using var httpClient = new HttpClient();
var results = new List<PackageResult>();
bool hasFailures = false;

Console.WriteLine($"Found {csprojFiles.Length} projects. Checking packages...");

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

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(versionStr))
                continue;

            // Check ignore list
            if (
                ignoreList.Any(x =>
                    x.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                    && x.Package.Equals(id, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            if (!NuGetVersion.TryParse(versionStr, out var currentVersion))
            {
                // Skip if version is a variable or unparseable
                continue;
            }

            // Get latest version
            var latestVersion = await GetLatestVersionAsync(
                httpClient,
                id,
                includePrerelease
            );

            if (latestVersion == null)
            {
                Console.WriteLine($"Warning: Could not find package '{id}' on NuGet.org.");
                continue;
            }

            bool isUpToDate = currentVersion >= latestVersion;
            if (!isUpToDate)
                hasFailures = true;

            results.Add(
                new PackageResult
                {
                    Project = projectName,
                    Package = id,
                    CurrentVersion = currentVersion.ToString(),
                    LatestVersion = latestVersion.ToString(),
                    IsUpToDate = isUpToDate,
                }
            );
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing {file}: {ex.Message}");
    }
}

// Output Table
Console.WriteLine();
Console.WriteLine("| Project | Package | Current | Latest | Status |");
Console.WriteLine("|---|---|---|---|---|");

foreach (var r in results)
{
    var status = r.IsUpToDate ? "✅" : "❌";
    Console.WriteLine(
        $"| {r.Project} | {r.Package} | {r.CurrentVersion} | {r.LatestVersion} | {status} |"
    );
}

if (hasFailures)
{
    Console.WriteLine();
    Console.WriteLine("Some packages are out of date.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("All packages are up to date.");
return 0;

static async Task<NuGetVersion> GetLatestVersionAsync(
    HttpClient client,
    string packageId,
    bool includePrerelease
)
{
    try
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
        var response = await client.GetStringAsync(url);
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

class PackageResult
{
    public string Project { get; set; }
    public string Package { get; set; }
    public string CurrentVersion { get; set; }
    public string LatestVersion { get; set; }
    public bool IsUpToDate { get; set; }
}
