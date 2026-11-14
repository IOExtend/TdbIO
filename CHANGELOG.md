# Changelog

All notable changes are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html); versions before 1.0 may change the
API between minor releases.

## [0.1.0] - Unreleased

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
