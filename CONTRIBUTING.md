# Contributing to TdbIO

## Reporting bugs

Open a [GitHub issue](https://github.com/IOExtend/TdbIO/issues) with:

- The Tekla Structures version that saved the file and the file extension.
- The tdbio version (`tdbio --version`) or the package version.
- The command line or API call used, with confidential values removed.
- The output of `tdbio info` for the file and the first 64 bytes of the file in hex.
- Expected and actual results, including relevant error messages.

Do not attach Tekla Structures models or files that contain confidential data. The header bytes and a short
description are enough to start; a whole model is never needed.

Report security issues through GitHub private vulnerability reporting in
the Security tab. Do not open public issues for security vulnerabilities.

## Proposing changes

Open an issue first for larger changes so the scope and approach can be
discussed before implementation. Keep changes focused on the agreed problem.

## Clean-room rule

All code is written independently from the analysis of files for
interoperability. Do not add vendor code, SDKs, SDK internals, headers or
binaries. Do not add Tekla Structures models or other Tekla files, customer
data, or names, paths, sizes, counts, GUIDs or other details of private
sample files to the repository, including tests, comments and
documentation. Test inputs are built synthetically in the tests; tests
that need private files read
them from folders named by environment variables and are skipped when
those folders are not set.

Do not put personal data in code, documentation, test data or commit metadata. Use your GitHub noreply address as the commit email.

## Development setup

Install the .NET SDK specified by `global.json`. It currently specifies
10.0.100, allows roll-forward to the latest feature band, and excludes
prerelease SDKs.

From the repository root, run:

```sh
dotnet build TdbIO.slnx -c Release
dotnet test TdbIO.slnx -c Release
```

The library is in `src/TdbIO`, the CLI in `src/TdbIO.Cli`, and tests in
`tests/TdbIO.Tests`.

Tests that need private inputs are skipped with a message when those
inputs are unavailable:

- `TDBIO_TEST_VECTORS` names the folder of test vectors: `manifest-*.json`
  files that list the vector files with their expected values. The tests
  read the manifests at run time; no vector name is written into the code.
- `TDBIO_TEST_FILES` names a folder of Tekla Structures files. The tests
  search it recursively, a few folder levels deep, and pick files by their
  extension only, never by name. They assert only properties that hold
  for every file. A `test_files` folder in an ancestor of the test binary
  is also searched and is ignored by git.

The repository ships no Tekla Structures files.

## Code conventions

- Follow the surrounding code and repository formatting settings.
- Keep nullable reference types enabled.
- Warnings are treated as errors in `src` and `tests`.
- Write short English comments that explain why.
- Document public APIs with XML documentation comments.
- Keep everything that is not part of the supported surface internal in
  `TdbIO.Internal`.
- Do not write console output from the library. Report problems as
  exceptions, and values that are kept despite a mismatch as warnings.
- Treat every file as untrusted input: bound every read and size, and
  report malformed data with `InvalidDataException` and its offset.
- Describe only what the code does. A field whose meaning is not known is
  exposed as a raw value and documented as such.

## Validating output

Command line output must not change unintentionally.

Before changing reading behavior, run `tdbio info`, `tdbio tables` and
`tdbio text` on the same input files with the existing code and keep the
results outside the repository. Run them again after the change and
compare the before and after outputs. Investigate differences and explain
intentional changes in the pull request, without naming private files or
quoting their content.

## Pull requests

Include:

- A description of the problem and the change.
- Tests for behavior changes, including relevant errors and edge cases.
- The build and test commands run, their results, and any skipped checks.
- Before and after output comparison results when reading is affected.

Do not add Tekla Structures models, customer data, vendor code, or
binaries to the repository. Keep private test files outside version
control.

Write commit messages in the imperative mood, for example,
"Reject gzip files with a second member".

## Releases

Push a tag matching the package version, such as `v0.1.0`, to run `release.yml`.
The workflow builds and tests the solution, attaches packages, symbols, source and checksums to a GitHub release published at once (not a draft),
and publishes the NuGet packages through Trusted Publishing. The release notes are the section of `CHANGELOG.md` for the version, so add that section before the tag.
The NuGet policy must name this repository (`IOExtend/TdbIO`), `release.yml`, the configured NuGet profile and the `TdbIO*` package pattern.
The repository variable `NUGET_USER` must hold the name of that NuGet profile; the workflow reads it for the Trusted Publishing login.

## Licensing contributions

Contributions are licensed under the MIT license. By submitting a
contribution, you confirm that you have the right to contribute it under
that license.

## Questions

Ask questions in GitHub Discussions or repository issues.
