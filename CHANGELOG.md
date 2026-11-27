# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html); versions before 1.0 may change the
API between minor releases.

## [Unreleased]

### Added

- `TableSection.Key` and `TableSection.Descriptors`: the key before a section and its descriptor words, as stored.
  `TableRow.A` and `TableRow.B`: the two numbers after the body of a row.
- Typed decoders for the rows of the most used tables of a model database, next to the raw bytes (`ObjectRow`,
  `AssemblyRow`, `PartRow`, `PointRow` and others; `TableRow.AsPart()`, `TableSection.AsParts()`).
- `Db1Document.ReadModel()`: a read-only object model (`TeklaModel`) of assemblies, parts, bolts, welds, their points and
  their attributes.
- JSON output of the object model: `TeklaModel.ToJson()` and `WriteJson`, and `tdbio export model.db1 model.json`.
- IFC output of the object model: `TeklaModel.ToIfc()` and `WriteIfc` with `TeklaIfcExportOptions` (IFC2x3 and IFC4),
  and `tdbio export model.db1 model.ifc`. Each straight part is approximated by a box.
- The `TdbIO.Scene` package: `TdbSceneReader` turns the object model into an IOScene scene document in metres, and
  `tdbio export model.db1 model.scene.json --scene-json` writes it.
- A standalone HTML viewer of the scene: `tdbio export model.db1 viewer.html`.
- `TableRow.TextEncoding` and `Decode(ReadOnlySpan<byte>, Encoding)` on the decoded row types with text: the text values
  of table rows can be read in a given encoding. `TdbReadOptions.TextEncoding` now applies to them too.
- Tests of the adapter in `TdbIO.Scene.Tests`, and test runs on Linux and macOS in CI.
- `docs/versions.md`: differences between Tekla Structures versions.

### Changed

- The IFC output of the object model is kept apart from the native engine in `src/TdbIO/Ifc`. With
  `BuildNativeCoreOnly=true` the engine is compiled without it, which CI does together with `TdbIO.Scene`; the package
  keeps the same public types.
- `dotnet pack` compares the public API of the library with the last published version.
- `tools/pack-release.ps1` checks that `TdbIO.Scene` depends on exactly the packed `TdbIO` version and on a released
  `IOScene`. `release.yml` publishes the packages to NuGet before it creates the GitHub release, and updates a release
  that an earlier run of the same tag left.
- `tdbio info` prints the values it could read when a text section is broken, and reports that error.
- `--encoding` is checked against the kind of the file before the file is opened. It applies to model databases and
  DBV@ files as well as templates and reports, and `tdbio export` takes it.
- The DSTV NC reader and the template lexer stream their lines and tokens instead of holding them as lists.
- The gzip reader takes the unpacked size in the trailer as the initial size of its buffer.
- Text rows with marker `0x0C` are rejected; only `0x04` was seen in text sections.

### Fixed

- The text values of table rows are no longer required to be UTF-8. They are single-byte in the code page of the system
  that saved the model, so `ReadModel()` and `tdbio export` failed with `invalid UTF-8 string` on every model saved on a
  non-English system. Each value is now read as ASCII or UTF-8 when its bytes are, else in code page 1251 or 1252,
  chosen once for the whole file.
- `tdbio` no longer fails with an unhandled `ArgumentException` on a path of blanks, and `tdbio info` no longer fails as
  a whole when one text section of a model database is broken.
- A template reports the offset of an error as a character offset of its text, and tuple nesting is limited like block
  nesting.

## [0.1.0] - 2026-11-14

First release of TdbIO.

### Added

- `TdbFile.DetectKind` and `TdbFile.Open` for paths and streams: the kind of a file is found from its content (reports by
  their `.xsr` name), and the file is read into a typed, read-only document.
- Gzip unpacking with checks of the header, the size and the CRC-32, capped by `TdbReadOptions.MaxUnpackedLength`
  (1 GiB by default).
- Model databases (`.db1`): header, id map, text sections and chains, save stamp dates (`TextChain.SavedAt`), and the
  table sections with a row view that stores no row objects (`Db1Document.ReadTables`).
- Model records files (`.db2`), DBV@ files, id range files (`.idrm`), `guid.mapper` and the header of `history.db`.
- DSTV NC files (`.nc1`): the `ST` header fields and the blocks, with `AK` and `BO` lines parsed.
- Templates (`.tpl`, `.rpt`) as a tree of blocks and properties, and reports (`.xsr`) as lines and pages, with encoding
  detection (ASCII, UTF-8, code pages 1251 and 1252) or a forced encoding.
- `InvalidDataException` with the offset of the problem in the message and in `Data["Offset"]`, and
  `TdbDocument.Warnings` for values that are kept although they differ from what is expected.
- The `tdbio` command line tool (package `TdbIO.Cli`) with the commands `info`, `tables` and `text`, text or JSON
  output, and exit codes 0, 1 and 2.
- Targets .NET 8 and .NET 10, with no package dependencies. The library never writes to the console.

### Not yet supported

- The content of table rows and `.db2` rows is given as raw bytes; it is not decoded into model objects.
- Drawing files are recognised but not read. Of `history.db` only the header is read.
- Gzip data with more than one member, with bytes after the compressed data or with header flags, and UTF-16 templates
  and reports, are rejected.
