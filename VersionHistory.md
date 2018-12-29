# Version History

## Pending

Add changes here when they're committed to the `master` branch. Move them to "Released" once the version number
is updated in preparation for publishing an updated NuGet package.

Prefix the description of the change with `[major]`, `[minor]` or `[patch]` in accordance with [SemVer](http://semver.org).

## Released

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
