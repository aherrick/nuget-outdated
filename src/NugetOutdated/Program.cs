using System.Text;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using NugetOutdated;
using NugetOutdated.Services;
using Spectre.Console;

// CI environments often report 80 columns, causing wrapping. Force a wider output.
AnsiConsole.Profile.Width = 240;
AnsiConsole.Profile.Encoding = Encoding.UTF8;

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
        if (bool.TryParse(args[i + 1], out var result))
        {
            includePrerelease = result;
        }
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
                    var projectName = key;
                    if (projectName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        projectName = Path.GetFileNameWithoutExtension(projectName);
                    }
                    ignoreList.Add((projectName, val));
                }
            }
        }
    }
}

var services = new ServiceCollection();
services.AddHttpClient<NuGetClient>().AddStandardResilienceHandler();
services.AddTransient<Checker>();
var serviceProvider = services.BuildServiceProvider();
var checker = serviceProvider.GetRequiredService<Checker>();

Console.WriteLine($"Checking packages in {Directory.GetCurrentDirectory()}...");

var results = await checker.CheckAsync(
    Directory.GetCurrentDirectory(),
    ignoreList,
    includePrerelease
);

// Output Table
var table = new Table();
table.Border(TableBorder.Square);
table.AddColumn(new TableColumn(nameof(PackageResult.Project)).NoWrap());
table.AddColumn(new TableColumn(nameof(PackageResult.Package)).NoWrap());
table.AddColumn(new TableColumn("Current").NoWrap());
table.AddColumn(new TableColumn("Latest").NoWrap());
table.AddColumn(new TableColumn("Status").NoWrap());

bool hasFailures = false;
foreach (var r in results)
{
    table.AddRow(r.Project, r.Package, r.CurrentVersion, r.LatestVersion, r.Status);
    if (!r.IsUpToDate && !r.IsIgnored)
        hasFailures = true;
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