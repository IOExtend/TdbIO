<p align="center">
  <img src="docs/images/tdbio-banner.png" alt="TdbIO" width="640">
</p>

TdbIO is a .NET library and command line tool that reads Tekla Structures files directly from disk, without Tekla
Structures: model databases (`.db1`, `.db2` and their `.bak` copies), DBV@ files, id range and GUID map files,
`history.db`, DSTV NC files (`.nc1`), templates (`.tpl`, `.rpt`) and reports (`.xsr`).

## What it does

- Tells the kind of a file from its content and opens it as a typed, read-only document.
- Unpacks the gzip wrapper of model files and checks it (size, CRC-32), with a cap on the unpacked size.
- Reads the header of model databases, the id map, the text chains and the date of each save stamp.
- Walks the table sections of model databases and DBV@ files to the last byte. Rows are given as raw bytes, and the
  rows of the most used tables of a model database also as typed values.
- Builds a read-only object model of a model database: parts, assemblies, bolts, welds, their points and attributes.
- Writes the object model as JSON, or as IFC (IFC2x3 and IFC4) with each part approximated by a box.
- Turns the object model into an IOScene scene document in metres (`TdbIO.Scene`), which the command line tool also
  writes as a standalone HTML viewer.
- Reads DSTV NC parts, the block tree of templates and the pages of reports.
- Gives every value whose meaning is not known as stored, and reports values that differ from what is expected as
  warnings instead of failing.

## Install

The packages are published on nuget.org.

[![TdbIO on NuGet](https://img.shields.io/nuget/v/TdbIO?label=TdbIO)](https://www.nuget.org/packages/TdbIO) [![TdbIO.Cli on NuGet](https://img.shields.io/nuget/v/TdbIO.Cli?label=TdbIO.Cli)](https://www.nuget.org/packages/TdbIO.Cli)

Install the command line tool:

```sh
dotnet tool install --global TdbIO.Cli
```

Add the library to a .NET project:

```sh
dotnet add package TdbIO
```

The library targets .NET 8 and .NET 10. It uses managed code without package dependencies or native dependencies and
is tested on Windows, Linux and macOS. So that an export never overwrites its input, the command line tool asks the
operating system whether two paths name the same file (symbolic links, junctions, hard links).

`TdbIO.Scene`, the adapter from the object model to an IOScene scene document, is in this repository
(`src/TdbIO.Scene`). It is not on nuget.org yet.

## Command line

Show the kind of a file and its main values:

```sh
tdbio info model.db1
```

```text
kind: Db1
version: 9.99
modelGuid: 0f1e2d3c-4b5a-4968-8778-a1b2c3d4e5f6
idMapEntries: 1
idMapField65: 0xD
idMapField73: 1
textSections: 1
textChains: 2
saveStamps: 1
lastSavedAt: 2026-02-01T10:20:30
warnings: 0
```

List the table sections of a model database or a DBV@ file, one tab-separated line each:

```sh
tdbio tables model.db1
```

```text
ordinal	offset	key	rowSize	descriptors	rows	end
0	0x7D	-	8	1,0	2	0xB4
1	0xB4	42	4	1	1	0xDA
```

List the text chains of a model database. A save stamp shows only its date, because the rest of it can name people or
machines:

```sh
tdbio text model.db1
```

```text
section	headId	rows	length	stamp	savedAt	text
0	1	1	6	false	-	Grid A
0	10	2	42	true	2026-02-01T10:20:30	-
```

Read a DSTV NC file, a template or a report:

```sh
tdbio info part.nc1
tdbio info list.tpl --encoding cp1252
tdbio info list.xsr --json
```

Export decoded Tekla model objects to JSON, Industry Foundation Classes (IFC) or a standalone 3D HTML viewer:

```sh
# Export to Tekla object model JSON
tdbio export model.db1 model.json

# Export to neutral IOScene JSON
tdbio export model.db1 model.scene.json --scene-json

# Export to IFC (IFC2x3 / IFC4)
tdbio export model.db1 model.ifc

# Generate an interactive 3D WebGL HTML viewer that works offline
tdbio export model.db1 viewer.html
```

Every command takes `--json` to write one JSON object instead of text. Warnings go to standard error and do not change
the exit code. The scene and HTML exports warn about the approximate geometry on standard error: how many parts are
proxy boxes, which profiles fell back to the default dimensions and which parts have no usable geometry. The outputs
above come from small synthetic files; the values are made up. Run `tdbio --help` for the usage, and see
[the command line](docs/cli.md) for every key, column and JSON property.

| Code | Meaning |
| --- | --- |
| 0 | The command finished and its output was written. |
| 1 | The command line is wrong. Two lines on standard error: the problem and `Run 'tdbio --help' for usage.` |
| 2 | The file cannot be used: it is missing or unreadable, not recognised, malformed, over a limit, not supported, or of a kind the command does not handle. One line on standard error, no stack trace. |

## Library

Open a file, then use pattern matching to reach the document of its kind:

```csharp
using System;
using TdbIO;

TdbDocument document = TdbFile.Open("model.db1");
foreach (string warning in document.Warnings)
{
    Console.Error.WriteLine(warning);
}

switch (document)
{
    case Db1Document model:
        Console.WriteLine($"{model.Version} {model.ModelGuid}");
        foreach (TableSection section in model.ReadTables())
        {
            Console.WriteLine($"section {section.Ordinal}: {section.Rows.Count} rows of {section.RowSize} bytes");
        }
        foreach (TextSection texts in model.ReadTextSections())
        {
            foreach (TextChain chain in texts.Chains)
            {
                if (chain.SavedAt is DateTime savedAt)
                {
                    Console.WriteLine($"saved {savedAt:yyyy-MM-dd HH:mm:ss}");
                }
            }
        }
        break;
    case Nc1Document part:
        Console.WriteLine($"{part.Mark} {part.Profile} {part.Length} mm, {part.Blocks.Count} blocks");
        break;
    case TemplateDocument template:
        Console.WriteLine($"{template.Root.Keyword} {template.Root.GetValue("type")?.Text}");
        break;
}
```

A malformed file throws `InvalidDataException` with the byte offset of the problem in its message (for templates,
a character offset in the decoded text); a file that is recognised but not supported throws `NotSupportedException`. The library never writes to the console. Documents are
read-only and hold the unpacked file in memory; table rows are a view over it, so large tables cost no memory per row.

See the [public API](docs/public-api.md) for every type and [dependencies](docs/dependencies.md) for package details.

## Documentation

The documentation site with the API reference is at <https://ioextend.github.io/TdbIO/>. [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/IOExtend/TdbIO) offers an automatically generated overview of the repository.

## Supported files

The kind is always found from the content. Only reports, which have no signature, are also recognised by the `.xsr`
file name.

| Files | Kind | Read | Given raw, not decoded |
| --- | --- | --- | --- |
| `.db1`, `xslib.db1` and their `.bak` copies | `Db1` | Header (version, model GUID), id map, text sections and chains, save stamp dates, table sections and rows, typed rows of the most used tables, the object model (`ReadModel`) | Rows of the other tables, the meaning of section keys, descriptor words and of the two numbers after each row, the id map values, the bytes before the first table section, text bytes from 0x80 |
| `.db2`, `xslib.db2` and their `.bak` copies | `Db2` | Header, records with key, row count and row size | Row content |
| DBV@ files (gzip data that unpacks to `DBV@`) | `Dbv` | Name, GUID records, number lists, `C9` records, table sections and rows | Most numbers, row bodies |
| `.idrm` | `IdRanges` | Next free value, ranges | One field of each range |
| `guid.mapper` | `GuidMapper` | GUID pairs | The header line (raw bytes) |
| `history.db` | `History` | Page size, page count, length | The database content |
| `.nc1` (DSTV NC) | `Nc1` | Header fields, blocks, `AK` and `BO` lines | Lines of other blocks, the meaning of number suffixes |
| `.tpl`, `.rpt` | `Template` | Block tree, properties, strings, numbers, identifiers, tuples | Expressions inside strings are not evaluated |
| `.xsr` | `Report` | Lines and page starts | Columns are not parsed |
| Drawing files | `Drawing` | Recognised only | Opening one throws `NotSupportedException` |

Values that differ from the ones always seen are kept and reported in `Warnings`.

## Limitations

- Version 0.1.0 is a preview. The API can change before 1.0.
- Reading only. There is no support for writing or changing files.
- Rows are decoded into typed values only for the most used tables of a model database. `.db2` rows and the rows of the
  other tables are given as raw bytes with their offsets. The meaning of section keys, of descriptor words and of the
  two numbers after each row is not established from the files.
- Part geometry is not decoded from the file. The IFC output and the scene adapter draw each straight part as a box
  between its start and end point, with the width and height estimated from the profile name (150 mm × 150 mm when the
  name gives none). These meshes are not Tekla solids and are not made for clash detection or exact measurement.
- The text chains of model databases are given as bytes. The text values of decoded rows are single-byte in the code
  page of the system that saved the model. A value is read as ASCII or UTF-8 when its bytes are; the other values are
  read in code page 1251 or 1252, chosen once for the whole file. `TdbReadOptions.TextEncoding` (`--encoding` of the
  command line tool) sets the encoding instead. The choice between the two pages cannot tell Cyrillic from Latin letters
  with diacritics, so give the encoding for a model saved with a Western European page.
- Drawing files are recognised but not read. Of `history.db` only the header is read.
- Only gzip data with one member, header flags 0 and nothing after the trailer is accepted.
- Templates and reports in UTF-16 are not supported. DSTV NC files must be ASCII with CR LF line breaks.
- A file is read into memory whole. The unpacked size is capped at 1 GiB by default (`TdbReadOptions.MaxUnpackedLength`);
  DSTV NC files, templates and reports are capped at 16 MiB.

## Build from source and tests

Use the .NET SDK selected by `global.json`, which specifies 10.0.100 with `latestFeature` roll-forward.

From the repository root:

```sh
dotnet build TdbIO.slnx -c Release
dotnet test TdbIO.slnx -c Release
```

The tests come in three tiers:

1. Synthetic tests build their input in code and always run.
2. Vector tests read small inputs and expected values from a private folder named by `TDBIO_TEST_VECTORS`.
3. Sample tests read every Tekla Structures file found by extension under the folder named by `TDBIO_TEST_FILES` (or a
   `test_files` folder in an ancestor of the test binary) and check properties that hold for every file.

Tests of tiers 2 and 3 are reported as skipped, with the reason, when their input is missing. The repository includes no
Tekla Structures files. See [test fixtures](docs/test-fixtures.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). Release changes are recorded in
[CHANGELOG.md](CHANGELOG.md).

## Security

Report vulnerabilities through GitHub private vulnerability reporting in the repository's Security tab. Do not open
public issues for security vulnerabilities. See [SECURITY.md](SECURITY.md).

## License and trademarks

TdbIO is licensed under the [MIT License](LICENSE). Copyright holder: mahach666.

TdbIO is not affiliated with, endorsed by or sponsored by Trimble. Tekla Structures is a trademark of Trimble Inc. (or
its affiliates).

The file formats are read by analysing files for interoperability. No Tekla code or binaries are included.
