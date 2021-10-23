# Faithlife.Analyzers

Roslyn-based C# code analyzers used on Faithlife source code.

[![Build](https://github.com/Faithlife/FaithlifeAnalyzers/workflows/Build/badge.svg)](https://github.com/Faithlife/FaithlifeAnalyzers/actions?query=workflow%3ABuild) [![NuGet](https://img.shields.io/nuget/v/Faithlife.Analyzers.svg)](https://www.nuget.org/packages/Faithlife.Analyzers)

[Analyzer Documentation](https://github.com/Faithlife/FaithlifeAnalyzers/wiki) | [Release Notes](ReleaseNotes.md)

## How to Use

Use `PackageReference` in your `.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Faithlife.Analyzers" Version="1.2.0" PrivateAssets="All" IncludeAssets="runtime; build; native; contentfiles; analyzers" />
  </ItemGroup>
```

To disable a particular analzyer, add a line to your `.editorconfig` under `[*.cs]`. For example:

```text
[*.cs]
dotnet_diagnostic.FL0009.severity = none
```

## How to Help

* Improve the documentation on [the wiki](https://github.com/Faithlife/FaithlifeAnalyzers/wiki), especially by adding rationale for rules or instructions for how to choose between multiple fixes.
* Suggest new analyzers by [opening an issue](https://github.com/Faithlife/FaithlifeAnalyzers/issues/new). Please add the `new analyzer` label.
* Vote for analyzers you would find particularly helpful by adding a 👍 reaction.
* Implement a new analyzer from [this list of the most popular](https://github.com/Faithlife/FaithlifeAnalyzers/issues?q=is%3Aissue+is%3Aopen+sort%3Areactions-%2B1-desc+label%3A%22new+analyzer%22).
