---
title: Private test fixtures
---

# Private test fixtures

The tests come in three tiers. Synthetic tests build their input in code and run everywhere. Vector tests and sample
tests check the reader against real Tekla Structures files and against small pieces cut from them. Those inputs cannot
be published, so they are not in the repository. The tests that need them find them through two helpers, `TestVectors`
and `TestFiles`, and are reported as **skipped with a reason** when the input is missing. They never pass silently and
never fail for lack of a file.

| Tier | Input | Runs |
| --- | --- | --- |
| 1. Synthetic | Bytes built in code (`Synthetic*.cs` in the test project); all values are made up | always |
| 2. Vectors | The folder named by `TDBIO_TEST_VECTORS` | when the variable names an existing folder |
| 3. Sample files | The folder named by `TDBIO_TEST_FILES`, and `test_files` folders | when files of the kind are found |

## Tier 1: synthetic tests

Every reader, the gzip and CRC-32 code, the kind detection, the encoding rule, the command line and the repository
checks are tested on synthetic bytes. Each test builds the smallest input that shows one rule, valid or broken, and
checks the result or the exception with its offset. No sample bytes are copied into the tests.

## Tier 2: vectors (`TDBIO_TEST_VECTORS`)

`TestVectors` reads the folder named by the environment variable `TDBIO_TEST_VECTORS`; there is no default location.
The folder holds small inputs cut from sample files and one or more manifests named `manifest-*.json`. Each manifest
entry names a vector file, its SHA-256 (and optionally its length) and an `expected` object with the values the reader
must give.

- A vector whose bytes do not match its hash or length fails the test.
- Tests select entries by the keys of their `expected` object, never by name. Vector names are read from the manifests
  at run time and are not written into the code; a failure message names the vector.
- A test fails when an entry it selects has an `expected` key that the test neither checks nor knowingly ignores, so a
  changed manifest is noticed instead of passing unchecked.

`VectorTests` (22 facts) uses them:

| Area | Facts | Checks |
| --- | --- | --- |
| Manifests | `EveryManifestFileMatchesItsSha256` | every listed file against its hash and length |
| Model containers | `ContainerIdMapsMatch`, `ContainerHeadersMatch`, `ContainerDrawingHeaderIsNotSupported`, `ContainerRecordFilesMatch`, `ContainerIdRangeFilesMatch`, `ContainerGuidMapperMatches`, `ContainerTextSectionMatches`, `ContainerSaveStampRowMatches`, `ContainerLastRowEndsAtNul`, `ContainerHistoryHeaderMatches` | headers, id maps, gzip framing, `.db2` records, id ranges, GUID pairs, text sections and chains, save stamps, the `history.db` header |
| Table sections | `Db1SectionVectorsWalkAsExpected`, `PreAreaVectorPrecedesTheFirstSection`, `Db1GapVectorsShowTheBytesBetweenSections`, `DbvGzipVectorUnpacks`, `DbvFileVectorsParseAsExpected`, `DbvSectionVectorsReadAsExpected`, `BrokenDbvVectorsThrow` | section starts, keys, row sizes, descriptors, row counts, ends and rows of `.db1` and DBV@ data; the DBV@ lists; DBV@ data that must be rejected |
| Text formats | `DstvVectorsMatch`, `TemplateVectorsMatch`, `ReportVectorsMatch` | DSTV NC header fields and blocks, template trees and encodings, report lines and page starts |
| Both tiers | `SampleDatabasesWalkAsTheSummarySays` | the table walk of every sample model database whose unpacked bytes match a summary entry by SHA-256; needs `TDBIO_TEST_FILES` as well; with only `TDBIO_TEST_VECTORS` set, the summary entry gets only its SHA-256 check |

Each fact loops over the manifests, so it is skipped as a whole when the folder is missing.

## Tier 3: sample files (`TDBIO_TEST_FILES`)

`TestFiles` looks for sample files in:

1. The folder named by the environment variable `TDBIO_TEST_FILES`.
2. Then every folder named `test_files` in an ancestor folder of the test binary (the repository root among them),
   nearest first. `test_files/` is listed in `.gitignore`.

Files are found by extension, never by name, with these extensions: `.db1`, `.db2`, `.db`, `.db5`, `.idrm`, `.mapper`,
`.bak`, `.nc1`, `.tpl`, `.rpt`, `.xsr` (ignoring case). The search goes down to four folder levels below each root, so
the root can be a folder of models: model folders one or two levels below it and their subfolders are reached. Reparse
points (links) are not followed and folders that cannot be read are passed over. The kind of each file is taken from
its content, so a `.bak` file or an unrelated file with a matching extension is sorted correctly; files of the kinds
`Unknown` and `Drawing` are left out.

No test asserts a count or value tied to a particular file. Each fact checks a property that every file of its kind
must have, collects the failures and reports them with the path relative to the root; the root itself is never
printed. Warnings go to the test output only.

`SampleTests` (11 facts) uses them:

| Fact | Files | Property checked for every file |
| --- | --- | --- |
| `DetectionNeverThrowsForContent` | all of the extensions | `TdbFile.DetectKind` does not throw |
| `EveryModelDatabaseOpens` | `.db1`, `.bak` of kind `Db1` | the file opens and its text sections are read |
| `EveryModelDatabaseWalksToTheEnd` | `.db1`, `.bak` of kind `Db1` | the table walk ends exactly at the end of the unpacked data |
| `EveryModelRecordsFileOpens` | `.db2`, `.bak` of kind `Db2` | the records end exactly at the end of the unpacked data |
| `EveryIdRangeFileOpens` | `.idrm` | the file opens |
| `EveryGuidMapperOpens` | `.mapper` | the file opens |
| `EveryHistoryFileOpens` | `.db` of kind `History` | the header is read and the page size is not 0 |
| `EveryDbvFileOpens` | `.db`, `.db5`, `.bak` of kind `Dbv` | the file opens, its sections end at the end of the data, and the GUID record indexes and list counts agree |
| `EveryNc1Opens` | `.nc1` | the first block is `ST`, the last `EN`, and the raw lines give the file back byte for byte |
| `EveryTemplateOpens` | `.tpl`, `.rpt` of kind `Template` | the file parses and the root keyword is `template` |
| `EveryReportOpens` | `.xsr` | the lines, joined again and encoded with the detected encoding, give the file back byte for byte |

Each fact skips on its own: a folder with only DSTV NC files runs the facts for `.nc1` and the detection fact and skips
the others. A model database that fails the table walk is a finding about the format, not something to patch in the
test.

## How a test is skipped

xunit 2.9.3 has no runtime skip of its own. The attributes `[SkippableFact]` and `[SkippableTheory]` (`SkippableFact.cs`)
report a test that throws `SkipException` as skipped, with the message as the reason. `TestVectors` and `TestFiles`
throw it when their input is missing, so a test needs no code of its own to skip. The reason names the environment
variable to set, never a path, and also says so when the variable names a folder that does not exist.

## Keeping private data out

`RepositoryHygieneTests` has two facts:

- `NoFileHoldsAPathIntoAUserProfile` (always runs): no text file of the repository holds a path into a user profile.
- `NoFileNamesAVectorASampleOrAConfiguredFolder` (needs `TDBIO_TEST_VECTORS`): no text file of the repository holds a
  vector name, a sample code from the manifests, or the value of either environment variable.

Expected values from sample files live only in the private manifests and are read at run time. Test names, messages and
synthetic data use no sample names, sizes, counts or GUIDs.

## What runs without the private files

With neither variable set and no `test_files` folder, 34 facts are skipped on each target framework (`net8.0` and
`net10.0`): the 22 facts of `VectorTests`, the 11 facts of `SampleTests` and
`RepositoryHygieneTests.NoFileNamesAVectorASampleOrAConfiguredFolder`. Every other test runs and must pass.

Run with the private files:

    TDBIO_TEST_FILES=<folder> TDBIO_TEST_VECTORS=<folder> dotnet test TdbIO.slnx -c Release

PowerShell:

    $env:TDBIO_TEST_FILES = '<folder>'
    $env:TDBIO_TEST_VECTORS = '<folder>'
    dotnet test TdbIO.slnx -c Release

Never write the folders into the repository: the hygiene test fails when their paths appear in it.
