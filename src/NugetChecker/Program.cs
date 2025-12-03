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
using NugetChecker;

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

using var httpClient = new HttpClient();
var checker = new Checker(httpClient);

Console.WriteLine($"Checking packages in {Directory.GetCurrentDirectory()}...");

var results = await checker.CheckAsync(Directory.GetCurrentDirectory(), ignoreList, includePrerelease);

// Output Table
Console.WriteLine();
Console.WriteLine("| Project | Package | Current | Latest | Status |");
Console.WriteLine("|---|---|---|---|---|");

bool hasFailures = false;
foreach (var r in results)
{
    var status = r.IsUpToDate ? "✅" : "❌";
    Console.WriteLine(
        $"| {r.Project} | {r.Package} | {r.CurrentVersion} | {r.LatestVersion} | {status} |"
    );
    if (!r.IsUpToDate) hasFailures = true;
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
