using System.Text.Json;
using NuGet.Versioning;

namespace NugetOutdated.Services;

public class NuGetClient(HttpClient httpClient)
{
    public async Task<NuGetVersion> GetLatestVersionAsync(string packageId, bool includePrerelease)
    {
        try
        {
            var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLower()}/index.json";
            var response = await httpClient.GetStringAsync(url);
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