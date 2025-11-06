# Release Notes

## Pending

Describe changes here when they're committed to the `master` branch. Move them to a version heading when the project version number is updated in preparation for publishing an updated NuGet package.

Prefix the description of the change with `[major]`, `[minor]`, or `[patch]` in accordance with [Semantic Versioning](https://semver.org/).

New analyzers are considered "minor" changes (even though adding a new analyzer is likely to generate warnings
or errors for existing code when the package is upgraded).

## 1.6.0 Beta 2

* Add [FL0023](https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0023): Avoid obsolete `Logos.Common.Logging.Extensions.LoggerExtensions` methods.

## 1.6.0 Beta 1

* Add [FL0021](https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0021): Use null propagation.
* Add [FL0022](https://github.com/Faithlife/FaithlifeAnalyzers/wiki/FL0022): Use `AsyncMethodContext.WorkState`.

## 1.5.1

* Fix FL0014 false positive for interpolated string handlers: [#88](https://github.com/Faithlife/FaithlifeAnalyzers/issues/88).
* Update the version of Microsoft.CodeAnalysis used by Faithlife.Analyzers from 3.5.0 to 4.14.0.

## 1.5.0

* Add FL0019: Local functions used as event handlers (unless they are static, or else they are subscribed and later unsubscribed in the same method).
* Add FL0020: Lambda expressions used as event handlers.

## 1.4.0

* Add FL0017: Do not switch on a constant value.
* Add FL0018: FormatInvariant deprecation.

## 1.3.1

* Fix false positive diagnostic for FL0008.
  * This bug was introduced in 1.3.0.

## 1.3.0

* Improve performance of analyzers.
* Add FL0013: `Uri.ToString` should not be used: [#66](https://github.com/Faithlife/FaithlifeAnalyzers/issues/66).
* Add FL0014: Interpolated strings should not be used without interpolation: [#63](https://github.com/Faithlife/FaithlifeAnalyzers/issues/63).
* Add FL0016: Verbatim strings should only be used when necessary: [#74](https://github.com/Faithlife/FaithlifeAnalyzers/pull/74).

## 1.2.1

* Set flag in all analyzers to stop analyzing generated code.

## 1.2.0

* Add `FL0012`: don't use interpolated string with `DbConnector.Command`: [#17](https://github.com/Faithlife/FaithlifeAnalyzers/issues/69).

## 1.1.0

* Add `FL0005`: detect `.ToReadOnlyCollection()` in constructors: [#17](https://github.com/Faithlife/FaithlifeAnalyzers/issues/17).
* Add `FL0006`: detect `.OrderBy` without a `StringComparer`: [#23](https://github.com/Faithlife/FaithlifeAnalyzers/issues/23).
* Add `FL0007`: detect `$` in interpolated strings: [#50](https://github.com/Faithlife/FaithlifeAnalyzers/issues/50).
* Add `FL0008`: detect usages of `WorkState.None` and `WorkState.ToDo` when alternatives exist: [#4](https://github.com/Faithlife/FaithlifeAnalyzers/issues/4).
* Add `FL0009`: prefer `""` over `string.Empty`: [#7](https://github.com/Faithlife/FaithlifeAnalyzers/issues/7).
* Add `FL0010`: discourage use of `IfNotNull`: [#13](https://github.com/Faithlife/FaithlifeAnalyzers/issues/13).
* Add `FL0011`: detect `ConcurrentDictionary.GetOrAddValue`: [#68](https://github.com/Faithlife/FaithlifeAnalyzers/pull/68).

## 1.0.7

* Ignore `NullReferenceException` that's infrequently thrown by `UntilCanceledAnalyzer`.

## 1.0.6

* Add diagnostic for `NullReferenceException` being thrown by `UntilCanceledAnalyzer`.

## 1.0.5

* Allow `AsyncWorkItem.Current` to be used in lambda passed to `AsyncWorkItem.Start`: [#20](https://github.com/Faithlife/FaithlifeAnalyzers/issues/20).
* Offer `AsyncWorkItem.Current` fix more often: [#19](https://github.com/Faithlife/FaithlifeAnalyzers/issues/19).

## 1.0.4

* Fix bug in `StringComparison` code fix provider that reformatted the entire file.

## 1.0.3

* Fix NuGet package install script.

## 1.0.2

* Downgrade `Microsoft.CodeAnalysis.CSharp.Workspaces` dependency to to 2.7.0.

## 1.0.1

* Fix false positive of `FL0002` for `string.StartsWith(char)` and `string.EndsWith(char)`.

## 1.0.0

* Initial release, supporting [four analyzers](https://github.com/Faithlife/FaithlifeAnalyzers/wiki).
