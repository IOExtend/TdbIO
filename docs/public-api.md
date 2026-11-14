---
title: Public API
---

# Public API

The `TdbIO` package is the supported surface listed here. Every public type is in the namespace `TdbIO`. Everything else
(the readers, the gzip and CRC-32 code, the kind detection) is `internal` in `TdbIO.Internal`; it can change in any
release and is visible only to the test project through `InternalsVisibleTo`.

Constructors of documents and of the types they hold are internal: documents are made only by `TdbFile.Open`. Every
document is read-only, holds its data in memory, keeps no stream open (nothing is `IDisposable`) and is safe to read
from several threads.

## Opening a file

| Type | Use |
| --- | --- |
| `TdbFile` | `DetectKind(path)` and `DetectKind(stream, fileName)` tell the kind of a file from its first bytes (at most 64 KiB) without reading it whole; they never throw for the content and give `Unknown` for data that is short, cut or not recognised. `Open(path, options)` and `Open(stream, fileName, options)` read the file into the document of its kind. The stream need not be seekable and is not disposed; `fileName` is used only to recognise reports. |
| `TdbFileKind` | `Unknown`, `Db1`, `Db2`, `Drawing`, `IdRanges`, `GuidMapper`, `Dbv`, `History`, `Nc1`, `Template`, `Report`. |
| `TdbReadOptions` | `MaxUnpackedLength`: the most bytes read into memory, the unpacked size of gzip data or the size of an uncompressed file (default 1 GiB, valid range 1 to `Array.MaxLength`). `TextEncoding`: the encoding of templates and reports, or null to detect it. `Default` gives a new instance with the defaults. |
| `TdbDocument` | The base class of every document: `Kind` and `Warnings`. |

The kind of each document:

| Kind | Document | Detected by |
| --- | --- | --- |
| `Db1` | `Db1Document` | gzip data that unpacks to `Xsteel` with type byte 0x85 |
| `Db2` | `Db2Document` | gzip data that unpacks to `Xsteel` with type byte 0x80 |
| `Drawing` | none: `Open` throws `NotSupportedException` | gzip data that unpacks to `Xsteel` with type byte 0x20 |
| `IdRanges` | `IdRangeDocument` | gzip data that unpacks to `IdRangeMapper` |
| `GuidMapper` | `GuidMapperDocument` | gzip data that unpacks to `!Guid mapping for ` |
| `Dbv` | `DbvDocument` | gzip data that unpacks to `DBV@` (uncompressed `DBV@` data is `Unknown`) |
| `History` | `HistoryDocument` | an uncompressed SQLite 3 header |
| `Nc1` | `Nc1Document` | uncompressed text whose first line is `ST` |
| `Template` | `TemplateDocument` | uncompressed text whose first word is `template`, after an optional UTF-8 byte order mark and blank lines |
| `Report` | `ReportDocument` | no signature: content that is not recognised, in a file whose name ends with `.xsr` |

An `Xsteel` file with any other type byte is `Unknown` for `DetectKind`; `Open` throws `InvalidDataException` naming the
type byte. Gzip data is never a report, whatever its file name.

## Exceptions

| Exception | When |
| --- | --- |
| `InvalidDataException` | Malformed, cut or inconsistent data; an unknown `Xsteel` type; a limit exceeded; a file that is not recognised; text that a forced `TdbReadOptions.TextEncoding` cannot decode. No partial result is returned. The message ends with ` at offset 0x…` (and ` line N` for line-based text), and `Exception.Data["Offset"]` holds the offset as a `long`: in the unpacked data for the content of gzip files, in the file for errors of the gzip wrapper itself (header, compressed data, size and CRC-32) and for other files; the error for input longer than a read limit gives that limit; for templates the character offset. |
| `NotSupportedException` | A drawing file; a report with a UTF-16 byte order mark. A template in UTF-16 does not start with the word `template` as bytes, so it is not recognised and gives `InvalidDataException`. |
| `ArgumentNullException`, `ArgumentException` | A null or empty path, a null or unreadable stream, a null key for `TemplateBlock.GetValue`. |
| `ArgumentOutOfRangeException` | `TdbReadOptions.MaxUnpackedLength` out of range. |
| `IOException`, `UnauthorizedAccessException` | The file or stream cannot be read. |
| `InvalidOperationException` | `TextChain.RevealSaveText` on a chain that is not a save stamp. |

## Warnings

`TdbDocument.Warnings` lists values that were kept although they differ from what is expected, one line each with an
offset or line number. A warning never contains text from the file. Warnings are given for:

- a model database whose header before the id map is not of the known variant (the id map is then not read and
  `IdMap` is null), and an id map field at offset 65 other than 0x0D or 0x10D (the field at offset 73 is kept raw and
  never checked, because no value of it is known to be expected);
- id ranges that do not follow each other, or a next free value that is not the end of the last range;
- a `history.db` whose length is not page size times page count;
- DBV@ numbers that differ from the values always seen, and table sections of DBV@ files with a row type other than 1
  or descriptors other than 0 and 1 (the second prologue number, `Prologue[1]`, is kept raw and never checked, because
  it is known to vary);
- a DSTV NC contour (`AK`) whose last point is not its first point.

## Model databases

| Type | Use |
| --- | --- |
| `Db1Document` | A model database (`.db1`, `xslib.db1` and their `.bak` copies). `Version` (`d.dd`), `ModelGuid`, `IdMap` (entries in file order, or null), `IdMapField65` and `IdMapField73` (raw numbers, null when the id map is not read), `Payload` (the whole unpacked file; offsets refer to it). `ReadTextSections()` and `ReadTables()` read on request and build new lists on each call. `ReadModel()` decodes a connected, read-only object model graph (<see cref="TeklaModel"/>) of parts, assemblies, bolts, welds and attributes. |
| `IdMapEntry` | `Sequence`, `Id` (GUID) and `Value` (raw, meaning not known). |
| `TextSection` | A section of 54-byte text rows: `Offset`, `RowCount`, `Chains` (in the file order of their heads) and `OrphanRowIds` (rows that belong to no chain, reported instead of dropped). The links of all rows are checked, orphans included: a link to a missing id, a cycle or two rows linking to one row is an error. |
| `TextChain` | One text: `HeadId`, `HeadTag` and `HeadField13` (raw), `RowIds` (chain order), `Text` (the bytes as stored, not decoded). `IsSaveStamp` is true when the text starts with `Save:`; `SavedAt` is its date and time, or null. `RevealSaveText()` returns the revealed bytes of a save stamp: fields separated by `;`. Fields after the date and time can hold names of people or machines; do not log them. |
| `Db2Document` | A model records file (`.db2`, `xslib.db2` and their `.bak` copies): `Version`, `ModelGuid`, `Records` (in file order, ending exactly at the end of the file) and `Payload`. |
| `Db2Record` | `Offset`, `Key`, `Count`, `RowSize` and `Rows`: `Count` times one marker byte and `RowSize` bytes, raw. |

## Table sections and DBV@ files

| Type | Use |
| --- | --- |
| `TableSection` | A section of fixed-size rows, from `Db1Document.ReadTables()` or `DbvDocument.Sections`. `Ordinal`, `Offset`, `End`, `Key` (raw, `.db1` only, else null), `RowSize`, `Descriptors` (raw), `RowsOffset` and `Rows`. Sections have no names; tell them apart by `Ordinal` and `RowSize`. `Rows` is a view over the data: a row is built when it is read. Typed bulk decoders (`AsObjects()`, `AsParts()`, `AsPoints()`, etc.) decode all rows into typed record structs. |
| `TableRow` | `Offset` (of the marker byte), `Marker`, `Body` (`RowSize` raw bytes), `A` and `B` (the two numbers after the body: signed 32-bit in `.db1` files, unsigned in DBV@ files). Typed decoders (`AsObject()`, `AsPart()`, `AsPoint()`, etc.) decode the row body into typed record structs. |
| `DbvDocument` | A DBV@ file: `Name`, `Prologue`, `ListHeader`, `GuidRecords`, `B`, `C`, `L1`, `L2`, `T`, `C9Records`, `Sections` and `Payload`. The numbers are raw; their names are positions, not meanings. |
| `DbvGuidRecord` | `Value` (raw), `Index` (1-based, checked against the position) and `Id`. |
| `DbvC9Record` | A record that starts with `C9 C0 CE DB`: four raw numbers `R0` to `R3`. |

The walk of a model database starts at its first table section and must end exactly at the end of `Payload`; a section
without a key after one with a key, a gap or bytes after the last section is an error. The bytes before the first
section, other than the header and the id map, are not interpreted; read them from `Payload`.

## Decoded table rows

Typed record structs decoded from the row bodies of the most common model database table sections:

| Type | Table Key | Size | Contents |
| --- | --- | --- | --- |
| `ObjectRow` | 1 | 72 | `Id`, `ParentId` (`kuuluu`), `AssemblyId`, `Guid` (128-bit RFC 4122), `Type`, `Subtype`, `ExternalType`, `Mask1`, `Mask2`, `ObjFlag`, `OwnerId`, `PrivilegeId`. |
| `AssemblyRow` | 2 | 92 | `Id`, `Type`, `Class`, `MainPartId` (`dum`), `Lot`, `Name`, `Flags`, `MainPartByUser`. |
| `CoordsysRow` | 3 | 40 | `Id`, `AttrId`, origin coordinates `X`, `Y`, `Z`, `Length`. |
| `PartRow` | 10 | 64 | `Id`, `PartAttrId`, `PositioningAttrId`, `StartPointId` (`p1`), `EndPointId` (`p2`), `GeometryTreeRootId`, `CoordsysAttrId`, `Reserved`, local coordinate system `CoordsysX`, `CoordsysY`, `CoordsysZ`, `Length`. |
| `PointRow` | 13 | 32 | `Id`, `Visibility`, 3D coordinates `X`, `Y`, `Z` in millimeters. |
| `RelationRow` | 16 | 60 | `Id`, `Type`, `Id1`, `Id2`, `Guid` (string), `Flags`. |
| `WeldingRow` | 20 | 24 | `Id`, `CommonAttrId`, `Seam1Id`, `Seam2Id`, `CompoundSeam1Id`, `CompoundSeam2Id`. |
| `NumAttrRow` | 38 | 24 | `Id`, `AttrId`, `FatherId`, `OwnerId`, `Flags`, `NameId`. |
| `StrAttrRow` | 39 | 24 | `Id`, `AttrId`, `FatherId`, `OwnerId`, `Flags`, `NameId`. |
| `PartAttrRow` | 44 | 372 | `Id`, `ObjType`, `FormType`, `ObjClass`, `AssStartNo`, `Flags`, `NPoints`, `PartFlags`, `Prefix`, `Name`, `Profile`, `ParameterStringId`, `AssemblyPosition`, `AssemblyName`, `Material`, `Finish`, `AssemblyType`, `PourPhase`. |
| `NumAttrAttrRow` | 48 | 44 | `Id`, `Type`, `Value` (double), `Name`, `PrivilegeId`. |
| `StrAttrAttrRow` | 49 | 120 | `Id`, `Type`, `Name`, `Value` (string), `ValueId`, `PrivilegeId`. |
| `StringPoolRow` | 50 | 45 | `Id`, `NextId`, `HashId`, `RefCount`, `Text`. |
| `PartNumberingRow` | 94 | 68 | `Id`, `SeriesId`, `StartNumber`, `FPos`, `MultiFPos`, `Prefix`. |
| `AssemblyNumberingRow` | 95 | 76 | `Id`, `SeriesId`, `StartNumber`, `FPos`, `FamilyFPos`, `QualifierFPos`, `MultiFPos`, `Prefix`. |
| `ObjectNumberingRow` | 105 | 12 | `Id`, `NumberId`, `NumberingGroup`. |
| `BoltRow` | 107 | 24 | `Id`, `BoltAttrId`, `PositioningAttrId`, `FirstPointId` (`p1`), `SecondPointId` (`p2`), `PolygonId`. |
| `ObjectModificationRow` | 132 | 20 | `ModificationId`, `Id`, `ModType`, `DependencyId`, `ModTime`. |
| `BoltHoleRow` | 140 | 48 | `BoltId`, `No`, `PartId`, `PointZ`, `Length`, `LongHoleDir`, `LongHoleType`, `ShorteningLength`. |
| `ObjectPhaseRow` | 141 | 8 | `ObjectId`, `PhaseNumber`. |
| `BoltHolePointsRow` | 142 | 60 | `BoltId`, intermediate coordinates `IntPoint1X`, `IntPoint1Y`, `IntPoint1Z`, `IntPoint2X`, `IntPoint2Y`, `IntPoint2Z`, `NumHoles`. |

## Object model: parts, assemblies, bolts and welds

High-level connected object model produced by `Db1Document.ReadModel()`:

| Type | Use |
| --- | --- |
| `TeklaModel` | Decoded object graph: `Document`, `ModelGuid`, `Assemblies`, `Parts`, `Bolts`, `Welds`, `Objects`. Fast lookup methods: `FindObject(id/guid)`, `FindPart(id)`, `FindAssembly(id)`, `FindBolt(id)`, `FindWeld(id)`. JSON serialization: `ToJson(indented)` and `WriteJson(stream/writer, indented)`. |
| `ModelObject` | Base entity: `Id`, `Guid` (RFC 4122), `Type`, `Subtype`, `Phase`, `ParentId`, `AssemblyId`, `StringAttributes`, `NumericAttributes`, `GetStringAttribute(name)`, `GetNumericAttribute(name)`. |
| `ModelAssembly` | Assembly / cast unit: `Name`, `Prefix`, `StartNumber`, `PositionNumber`, `AssemblyType`, `Class`, `MainPart`, `Secondaries`, `Parts`, `Bolts`, `Welds`. |
| `ModelPart` | Structural part: `Name`, `Profile` (resolved with parameter strings), `Material`, `Prefix`, `StartNumber`, `PositionNumber`, `Class`, `Finish`, `Assembly`, `IsMainPart`, `StartPoint`, `EndPoint`, `Length`, `CoordinateSystem`, `Bolts`, `Welds`. |
| `ModelBolt` | Bolt group: `FirstPoint`, `SecondPoint`, `ConnectedParts`, `Holes`, `Positions`, `Assembly`. |
| `ModelWeld` | Structural weld: `CommonAttrId`, `Parent`, `Assembly`, `ConnectedParts`. |
| `ModelPoint` | 3D coordinate point in millimeters: `Id`, `X`, `Y`, `Z`. |
| `ModelCoordsys` | Local coordinate system origin and scale: `Id`, `X`, `Y`, `Z`, `Length`. |
| `ModelBoltHole` | Bolt hole penetration: `No`, `PartId`, `Part`, `PointZ`, `Length`, `LongHoleDir`, `LongHoleType`, `ShorteningLength`. |

## Files next to the model

| Type | Use |
| --- | --- |
| `GuidMapperDocument` | A `guid.mapper` file: `HeaderLine` (the first line as raw bytes) and `Pairs`. |
| `GuidPair` | `First` and `Second`; no two lines share `First`. The meaning of the columns is not known. |
| `IdRangeDocument` | An id range file (`.idrm`): `Next` and `Ranges`. |
| `IdRange` | `Start`, `Id`, `Field20` (raw) and `Length`. |
| `HistoryDocument` | A `history.db` file, an SQLite 3 database of which only the header is read: `PageSize` (raw), `PageCount` and `Length`. The rest of the file is not loaded. |

## DSTV NC files

| Type | Use |
| --- | --- |
| `Nc1Document` | One part: `Comment` (line 2 after `** `), `Fields` (the 20 header fields of the `ST` block), `Mark` (field 4), `Material` (field 5), `Quantity` (field 6, null when not an integer), `Profile` (field 7), `Length` (field 9, usually mm) and `Blocks`. When field 8 is `B`, `Profile` may hold a short form of the profile name. |
| `Nc1Block` | `Code` (two upper-case letters), `LineNumber`, `RawLines` (the body as written; joined with the codes and CR LF they give the file back) and `Lines` (parsed for `AK` and `BO`, empty otherwise). |
| `Nc1Line` | `Label` (a lower-case letter of unknown meaning, set on the first `AK` line and on every `BO` line) and `Values`. |
| `Nc1Token` | `Text` (as written, so `-0` and `0u` stay as they are), `Number` and `Suffix` (a lower-case letter or null; its meaning is not known). |

## Templates and reports

| Type | Use |
| --- | --- |
| `TemplateDocument` | A template (`.tpl`) or report template (`.rpt`): `EncodingName` and `Root`. |
| `TemplateBlock` | `Keyword`, `Instance` (or null), `Properties` (in file order, repeated keys kept), `Blocks` and `GetValue(key)` (the first property with that key, ordinal, or null). |
| `TemplateValue` | `Kind`, `Text` (the unescaped string, the number or identifier as written; empty for a tuple), `Number` (for numbers) and `Items` (for tuples). Expressions inside strings are not evaluated. |
| `TemplateValueKind` | `String`, `Number`, `Identifier`, `Tuple`. |
| `ReportDocument` | A finished report (`.xsr`): `EncodingName`, `Lines` (split on CR LF, the leading form feed removed), `PageStarts` (indexes of the lines that began with a form feed) and `EndsWithLineBreak`. Columns are not parsed. |

Without `TdbReadOptions.TextEncoding`, the encoding of a template or report is chosen from its bytes: UTF-8 after a UTF-8
byte order mark; `ascii` when every byte is below 0x80; `utf-8` when the bytes are valid UTF-8; otherwise `cp1251` when at
least half of the bytes from 0x80 are 0xC0 or above, else `cp1252`. One encoding is used for the whole file. A byte the
chosen code page does not define is an `InvalidDataException` at its offset: bytes are never replaced to make the text
decode. A forced encoding is used as given, without its own byte order mark.

## Limits

| Limit | Value |
| --- | --- |
| Bytes read into memory, unpacked | `TdbReadOptions.MaxUnpackedLength`, 1 GiB by default; checked against the gzip size field before unpacking |
| Gzip | one member, header flags 0, size and CRC-32 checked |
| `.nc1`, `.tpl`, `.rpt`, `.xsr` size | 16 MiB; reading stops there (or at `MaxUnpackedLength` when it is smaller) |
| `.nc1` line length | 4096 characters |
| Template block depth | 64 (the root is level 0) |
| Template tuple nesting | 64 levels |
| Row size of a `.db1` table section | 1 to 65536 bytes, at most as many descriptors as row bytes |
| Row size and descriptor count of a DBV@ table section | at most 1 MiB and 4096 |

The compressed data must end exactly where the 8-byte gzip trailer starts, so a second member or any other bytes after
the compressed data are an error. A plain file of an unknown kind is rejected after its first 64 KiB are read. Gzip data is read and unpacked
(up to `MaxUnpackedLength`) before its kind is known, so an unknown kind or a drawing file is rejected only after that.

## Diagnostics

The library never writes to the console and has no logging. Problems are reported only as exceptions and as
`Warnings`. The `tdbio` command line tool writes the warnings to standard error.

Every type is also in the [API reference](api/TdbIO.yml), generated from the XML documentation.
