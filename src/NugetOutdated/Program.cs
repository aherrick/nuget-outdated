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
using NugetOutdated;
using Spectre.Console;

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
var table = new Table();
table.AddColumn(nameof(PackageResult.Project));
table.AddColumn(nameof(PackageResult.Package));
table.AddColumn("Current");
table.AddColumn("Latest");
table.AddColumn("Status");

bool hasFailures = false;
foreach (var r in results)
{
    string status;
    if (r.IsIgnored)
    {
        status = "[grey]🔒[/]";
    }
    else
    {
        status = r.IsUpToDate ? "[green]✅[/]" : "[red]❌[/]";
    }

    table.AddRow(
        r.Project,
        r.Package,
        r.CurrentVersion,
        r.LatestVersion,
        status
    );
    if (!r.IsUpToDate && !r.IsIgnored) hasFailures = true;
}

AnsiConsole.Write(table);

if (hasFailures)
{
    Console.WriteLine();
    Console.WriteLine("Some packages are out of date.");
    return 1;
}

Console.WriteLine();
Console.WriteLine("All packages are up to date.");
return 0;
