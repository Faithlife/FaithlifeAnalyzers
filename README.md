# Faithlife.Analyzers

Roslyn-based C# code analyzers used on Faithlife source code.

[![NuGet](https://img.shields.io/nuget/v/Faithlife.Analyzers.svg)](https://www.nuget.org/packages/Faithlife.Analyzers)

[Release Notes](ReleaseNotes.md)

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

## Analyzers

| ID | Description |
|---|---|
| [FL0001](docs/FL0001.md) | `AsyncWorkItem.Current` must only be used in methods that return `IEnumerable<AsyncAction>` |
| [FL0002](docs/FL0002.md) | Optional `StringComparison` arguments must always be specified |
| [FL0003](docs/FL0003.md) | `UntilCanceled()` may only be used in methods that return `IEnumerable<AsyncAction>` |
| [FL0004](docs/FL0004.md) | Use `operator==` or a non-ordinal `StringComparison` |
| [FL0005](docs/FL0005.md) | Avoid `ToReadOnlyCollection` in constructors |
| [FL0006](docs/FL0006.md) | Optional `IComparer<string>` arguments must always be specified |
| [FL0007](docs/FL0007.md) | Avoid `$` in interpolated strings |
| [FL0008](docs/FL0008.md) | `WorkState.None` and `WorkState.ToDo` must not be used when an `IWorkState` is available |
| [FL0009](docs/FL0009.md) | Prefer `""` over `string.Empty` |
| [FL0010](docs/FL0010.md) | Prefer modern language features over `IfNotNull` |
| [FL0011](docs/FL0011.md) | `GetOrAddValue` should not be used with `ConcurrentDictionary` |
| [FL0012](docs/FL0012.md) | `DbConnector.Command` should not be used with an interpolated string |
| [FL0013](docs/FL0013.md) | Do not use `Uri.ToString()` |
| [FL0014](docs/FL0014.md) | Interpolated strings should not be used without interpolation |
| [FL0015](docs/FL0015.md) | Prefer null-conditional operators over ternaries |
| [FL0016](docs/FL0016.md) | Verbatim strings should only be used with certain special characters |
| [FL0017](docs/FL0017.md) | Do not use a switch expression on a constant value |
| [FL0018](docs/FL0018.md) | Prefer string interpolation over `FormatInvariant` |
| [FL0019](docs/FL0019.md) | Local Functions as Event Handlers |
| [FL0020](docs/FL0020.md) | Lambda Expressions as Event Handlers |
| [FL0021](docs/FL0021.md) | Use null propagation |
| [FL0022](docs/FL0022.md) | Use AsyncMethodContext.WorkState |
| [FL0023](docs/FL0023.md) | Replace obsolete Logos.Common.Logging.Extensions extension methods |

## How to Help

* Improve the documentation in the [docs/](docs/) directory, especially by adding rationale for rules or instructions for how to choose between multiple fixes.
* Suggest new analyzers by [opening an issue](https://github.com/Faithlife/FaithlifeAnalyzers/issues/new). Please add the `new analyzer` label.
* Vote for analyzers you would find particularly helpful by adding a 👍 reaction.
* Implement a new analyzer from [this list of the most popular](https://github.com/Faithlife/FaithlifeAnalyzers/issues?q=is%3Aissue+is%3Aopen+sort%3Areactions-%2B1-desc+label%3A%22new+analyzer%22).
