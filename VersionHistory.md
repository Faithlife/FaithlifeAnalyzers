# Version History

## Pending

Describe changes here when they're committed to the `master` branch. Move them to **Released** when the project version number is updated in preparation for publishing an updated NuGet package.

Prefix the description of the change with `[major]`, `[minor]`, or `[patch]` in accordance with [Semantic Versioning](https://semver.org/).

New analyzers are considered "minor" changes (even though adding a new analyzer is likely to generate warnings
or errors for existing code when the package is upgraded).

* [minor] Add `FL0005`: detect `.ToReadOnlyCollection()` in constructors: [#17](https://github.com/Faithlife/FaithlifeAnalyzers/issues/17).
* [minor] Add `FL0006`: detect `.OrderBy` without a `StringComparer`: [#23](https://github.com/Faithlife/FaithlifeAnalyzers/issues/23).
* [minor] Add `FL0007`: detect `$` in interpolated strings: [#50](https://github.com/Faithlife/FaithlifeAnalyzers/issues/50).
* [minor] Add `FL0008`: detect usages of `WorkState.None` and `WorkState.ToDo` when alternatives exist: [#4](https://github.com/Faithlife/FaithlifeAnalyzers/issues/4)
* [minor] Add `FL0009`: prefer `""` over `string.Empty`: [#7](https://github.com/Faithlife/FaithlifeAnalyzers/issues/7)
* [minor] Add `FL0010`: discourage use of `IfNotNull`: [#13](https://github.com/Faithlife/FaithlifeAnalyzers/issues/13)

## Released

### 1.0.7

* Ignore `NullReferenceException` that's infrequently thrown by `UntilCanceledAnalyzer`.

### 1.0.6

* Add diagnostic for `NullReferenceException` being thrown by `UntilCanceledAnalyzer`.

### 1.0.5

* Allow `AsyncWorkItem.Current` to be used in lambda passed to `AsyncWorkItem.Start`: [#20](https://github.com/Faithlife/FaithlifeAnalyzers/issues/20).
* Offer `AsyncWorkItem.Current` fix more often: [#19](https://github.com/Faithlife/FaithlifeAnalyzers/issues/19).

### 1.0.4

* Fix bug in `StringComparison` code fix provider that reformatted the entire file.

### 1.0.3

* Fix NuGet package install script.

### 1.0.2

* Downgrade `Microsoft.CodeAnalysis.CSharp.Workspaces` dependency to to 2.7.0.

### 1.0.1

* Fix false positive of `FL0002` for `string.StartsWith(char)` and `string.EndsWith(char)`.

### 1.0.0

* Initial release, supporting [four analyzers](https://github.com/Faithlife/FaithlifeAnalyzers/wiki).
