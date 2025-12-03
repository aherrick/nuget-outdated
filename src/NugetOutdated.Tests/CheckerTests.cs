using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using NugetOutdated;
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

    [Fact]
    public async Task CheckAsync_ReturnsEmpty_WhenNoCsprojFiles()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var checker = new Checker(httpClient);

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
                    Content = new StringContent("""{"versions": ["12.0.1", "13.0.1"]}"""),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object);
        var checker = new Checker(httpClient);

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
                    Content = new StringContent("""{"versions": ["12.0.1", "13.0.1"]}"""),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object);
        var checker = new Checker(httpClient);

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
                    Content = new StringContent("""{"versions": ["12.0.1", "13.0.1"]}"""),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object);
        var checker = new Checker(httpClient);

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
                    Content = new StringContent("""{"versions": ["12.0.1", "13.0.1"]}"""),
                }
            );

        var httpClient = new HttpClient(mockHandler.Object);
        var checker = new Checker(httpClient);

        // Act
        var results = await checker.CheckAsync(_testDir, new List<(string, string)>(), false);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Newtonsoft.Json", result.Package);
        Assert.Equal("12.0.1", result.CurrentVersion);
    }
}