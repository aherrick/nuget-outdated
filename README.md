# Nuget Outdated

[![Build](https://github.com/aherrick/nuget-outdated/actions/workflows/build.yml/badge.svg)](https://github.com/aherrick/nuget-outdated/actions/workflows/build.yml)

A simple GitHub Action to check for outdated NuGet packages in your .NET projects.

## Features

*   **Standard Package References**: Checks `PackageReference` items in `.csproj` files.
*   **Central Package Management (CPM)**: Supports `Directory.Packages.props`.
    *   Global versions: `<PackageVersion Include="PackageId" Version="1.2.3" />`
    *   Project-specific versions: `<PackageVersion Update="PackageId" Version="1.2.3" Condition="'$(MSBuildProjectName)' == 'MyProject'" />`
*   **Ignore List**: Ability to ignore specific packages for specific projects.
*   **Pre-release Support**: Option to check against pre-release versions.

## Usage

To use this action in your workflow, add the following step:

```yaml
steps:
  - uses: actions/checkout@v4
  
  - name: Check for outdated NuGet packages
    uses: aherrick/nuget-outdated@main
    with:
      # Optional: Ignore specific packages (Format: ProjectName=PackageId&Project2=Package2)
      ignore: 'MyProject=Newtonsoft.Json&OtherProject=Serilog'
      
      # Optional: Check for pre-release versions (default: false)
      includeprelease: 'true'
```

## Inputs

| Input | Description | Required | Default |
|---|---|---|---|
| `ignore` | Query string for ignored packages (e.g. `project1=PackageA&project2=PackageB`) | No | `''` |
| `includeprelease` | Check for latest prerelease versions | No | `'false'` |
