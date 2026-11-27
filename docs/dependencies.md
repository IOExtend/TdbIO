---
title: Dependencies
---

# Dependencies

All package versions live in `Directory.Packages.props` (central package management); project files carry no versions.
Build settings shared by every project live in `Directory.Build.props`; the SDK is pinned in `global.json`
(`10.0.100`, `rollForward: latestFeature`, no prereleases).

## Runtime packages

None for the core engine (`TdbIO`). The engine library references no external packages and contains no third-party code.
`TdbIO.Scene` references `IOScene` `[0.1.0, 0.2.0-0.0)` as the neutral scene contract and depends on exactly the
version of `TdbIO` it ships with. `TdbIO.Cli` references `TdbIO.Scene` and `IOViewer` `0.3.1`, whose library writes the
standalone HTML viewer of a scene; the tool is the only project that combines the adapter with the viewer.

They use the following .NET runtime libraries:

| Runtime library | Used for |
| --- | --- |
| `System.IO.Compression` (`DeflateStream`) | The deflate data inside the gzip wrapper of model files. The gzip header, trailer and CRC-32 are checked by the library's own code. |
| `System.Text.CodePagesEncodingProvider` | Code pages 1251 and 1252 for templates and reports. The provider is used directly; the library does not register it globally, so the encodings of the calling application are not changed. |
| `System.Text.Json` (`Utf8JsonWriter`) | The `--json` output of the command line tool and the JSON output of the object model (`TeklaModel.WriteJson`). Part of the runtime; no package. |
| `System.Security.Cryptography` (`SHA256`) | The stable IFC GlobalId derived for a model object without a GUID (`TeklaIfcGuid.FromElementId`). Part of the runtime; no package. |

### Decisions

- **No package for CRC-32.** The gzip check needs only CRC-32; a small table-driven implementation avoids a dependency on
  `System.IO.Hashing`.
- **No SQLite package.** Of `history.db` only the header fields are read, which needs no database engine.

## Build-time packages

| Package | Version | Used for |
| --- | --- | --- |
| Microsoft.SourceLink.GitHub | 10.0.403 | Source Link in the symbols. `PrivateAssets` all; not a dependency of the published packages. |

## Test packages

Pinned to exact versions: Microsoft.NET.Test.Sdk 18.12.0, xunit 2.9.3 and xunit.runner.visualstudio 4.0.1. Besides
these, the test projects reference `IOViewer` `0.3.1`: the command line is compiled into the tests and writes the HTML
viewer with it, and the scene tests check that viewer. `TestVectors` reads its manifests with `System.Text.Json`.

## Tools

`.config/dotnet-tools.json` pins docfx 2.81.0 for the documentation site (`dotnet tool restore`, then
`dotnet docfx docfx.json`).

## Auditing

`NuGetAudit` is on with `NuGetAuditMode=all` (direct and transitive) and `NuGetAuditLevel=low`, so every known
advisory is reported as an NU1901-NU1904 warning at restore. Check manually:

    dotnet list TdbIO.slnx package --vulnerable --include-transitive
    dotnet list TdbIO.slnx package --outdated

At version 0.1.0 the audit reports no vulnerable packages in any project. Re-run it when changing dependencies.

## Build settings

`Directory.Build.props` sets `LangVersion` 14.0, `Nullable` enable, `ImplicitUsings` enable, `Deterministic` true,
`EmbedUntrackedSources` true, `PublishRepositoryUrl` true, `VersionPrefix` 0.1.0, symbol packages (`snupkg`), a
`PathMap` that keeps the build folder out of binaries, and `ContinuousIntegrationBuild` true when `CI`,
`GITHUB_ACTIONS` or `TF_BUILD` is set. `src/Directory.Build.props` turns warnings into errors for the library and the
tool; the library generates XML documentation for every public member. `tests/Directory.Build.props` turns warnings into
errors and XML documentation off. The library, the adapter, the tool and the tests target `net8.0` and `net10.0`.

## Package compatibility and the native build

`dotnet pack` of the library compares its public API, for both target frameworks, with the last published version
(`PackageValidationBaselineVersion` in `src/TdbIO/TdbIO.csproj`). An unintended difference fails the pack.

With `BuildNativeCoreOnly=true` the library is compiled without the IFC output (`src/TdbIO/Ifc`) and produces no
package. CI compiles it this way together with `TdbIO.Scene`, which must build on the native engine alone.
