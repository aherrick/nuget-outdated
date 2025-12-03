using System.Text.RegularExpressions;
using System.Xml.Linq;
using NugetOutdated.Models;

namespace NugetOutdated.Services;

public partial class CpmParser
{
    [GeneratedRegex(@"'\$\(MSBuildProjectName\)'\s*==\s*'([^']+)'")]
    private static partial Regex ProjectConditionRegex();

    public static CpmSettings Parse(string directory)
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
                    var match = ProjectConditionRegex().Match(condition);
                    if (match.Success)
                    {
                        var projectName = match.Groups[1].Value;
                        if (!settings.ProjectVersions.TryGetValue(projectName, out var projectVersions))
                        {
                            projectVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            settings.ProjectVersions[projectName] = projectVersions;
                        }
                        projectVersions[id] = version;
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
}
