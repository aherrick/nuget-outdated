using System.Net;
using Moq;
using Moq.Protected;
using NugetOutdated.Services;
using Xunit;

namespace NugetOutdated.Tests;

public class CheckerTests : IDisposable
{
    private readonly string _testDir;

    public CheckerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private Checker CreateChecker(string jsonResponse)
    {
        var mockHandler = new Mock<HttpMessageHandler>();

        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object);
        var nuGetClient = new NuGetClient(httpClient);

        return new Checker(nuGetClient);
    }

    [Fact]
    public async Task CheckAsync_ReturnsEmpty_WhenNoCsprojFiles()
    {
        // Arrange
        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task CheckAsync_IdentifiesOutdatedPackage()
    {
        // Arrange
        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("TestProject", result.Project);
        Assert.Equal("Newtonsoft.Json", result.Package);
        Assert.Equal("12.0.1", result.CurrentVersion);
        Assert.Equal("13.0.1", result.LatestVersion);
        Assert.False(result.IsUpToDate);
    }

    [Fact]
    public async Task CheckAsync_IdentifiesUpToDatePackage()
    {
        // Arrange
        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsUpToDate);
    }

    [Fact]
    public async Task CheckAsync_SupportsCentralPackageManagement_Global()
    {
        // Arrange
        var cpmContent = """
<Project>
  <ItemGroup>
    <PackageVersion Include="Newtonsoft.Json" Version="12.0.1" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "Directory.Packages.props"), cpmContent);

        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Newtonsoft.Json", result.Package);
        Assert.Equal("12.0.1", result.CurrentVersion);
    }

    [Fact]
    public async Task CheckAsync_SupportsCentralPackageManagement_ProjectSpecific()
    {
        // Arrange
        var cpmContent = """
<Project>
  <ItemGroup>
    <PackageVersion Update="Newtonsoft.Json" Version="12.0.1" Condition="'$(MSBuildProjectName)' == 'TestProject'" />
    <PackageVersion Update="Newtonsoft.Json" Version="13.0.1" Condition="'$(MSBuildProjectName)' == 'OtherProject'" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "Directory.Packages.props"), cpmContent);

        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Newtonsoft.Json", result.Package);
        Assert.Equal("12.0.1", result.CurrentVersion);
    }

    [Fact]
    public async Task CheckAsync_IgnoresSpecifiedPackages()
    {
        // Arrange
        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
    <PackageReference Include="Serilog" Version="2.10.0" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        // Only need response for Newtonsoft.Json as Serilog is ignored
        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1"]}""");
        var ignoreList = new List<(string, string)> { ("TestProject", "Serilog") };

        // Act
        var results = await checker.CheckAsync(_testDir, ignoreList, false);

        // Assert
        Assert.Equal(2, results.Count);

        var ignored = results.Single(r => r.Package == "Serilog");
        Assert.True(ignored.IsIgnored);
        Assert.True(ignored.IsUpToDate);
        Assert.Empty(ignored.LatestVersion);

        var checkedPkg = results.Single(r => r.Package == "Newtonsoft.Json");
        Assert.False(checkedPkg.IsIgnored);
        Assert.Equal("13.0.1", checkedPkg.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_SupportsPreReleaseVersions()
    {
        // Arrange
        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="12.0.1" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "TestProject.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["12.0.1", "13.0.1-beta1"]}""");

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), true);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("13.0.1-beta1", result.LatestVersion);
        Assert.False(result.IsUpToDate);
    }

    [Fact]
    public async Task CheckAsync_IgnoredPackage_ShowsLatestVersion()
    {
        // Arrange
        var csprojContent = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Velopack" Version="0.0.1298" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(Path.Combine(_testDir, "AndyTV.csproj"), csprojContent);

        var checker = CreateChecker("""{"versions": ["0.0.1298", "0.0.1300"]}""");
        var ignoreList = new List<(string, string)> { ("AndyTV", "Velopack") };

        // Act
        var results = await checker.CheckAsync(_testDir, ignoreList, false);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("AndyTV", result.Project);
        Assert.Equal("Velopack", result.Package);
        Assert.Equal("0.0.1298", result.CurrentVersion);
        Assert.Equal("0.0.1300", result.LatestVersion);
        Assert.True(result.IsIgnored);
        Assert.False(result.IsUpToDate); // Package is outdated, but ignored
    }
}