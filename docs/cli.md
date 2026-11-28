---
title: Command line and exit codes
---

# Command line and exit codes

The `tdbio` tool (package `TdbIO.Cli`) reports the structure of a file. It prints text or JSON to standard output,
writes messages and warnings to standard error and tells success from failure by its exit code. It uses only the public
API of the library.

```text
Usage: tdbio info <file> [--json] [--encoding ascii|utf-8|cp1251|cp1252]
       tdbio tables <file> [--json]
       tdbio text <file> [--json]
       tdbio --help | -h | --version
```

## Commands

| Command | Works on | Output |
| --- | --- | --- |
| `tdbio info <file>` | every supported kind | `key: value` lines: the kind, its main values and the number of warnings. |
| `tdbio tables <file>` | model databases (`.db1`), DBV@ files | A tab-separated header line, then one line per table section. |
| `tdbio text <file>` | model databases (`.db1`) | A tab-separated header line, then one line per text chain, then one line per orphan row. |
| `tdbio export <file> [output]` | model databases (`.db1`) | The object model as JSON (default), or with an option: `--ifc` (IFC), `--scene-json` (an IOScene scene), or no format option and an `.html` output (a standalone viewer). |
| `tdbio --help`, `tdbio -h` | | The usage text, on standard output. |
| `tdbio --version` | | `tdbio <version>`, for example `tdbio 0.1.0`. |

The command comes first; options may come before or after the file. The kind of the file is found from its content, not
from its extension (reports, which have no signature, are recognised by the `.xsr` file name).

## Options

| Option | Commands | Effect |
| --- | --- | --- |
| `--json` | all | Write one indented JSON object (UTF-8) instead of text. |
| `--encoding <name>` | `info`, `export` | Read a template or report, or the text values of the table rows of a model database (`.db1`) or DBV@ file, with this encoding instead of detecting it: `ascii`, `utf-8`, `cp1251` or `cp1252`. Give it for a model saved with a Western European code page, which the detection reads as `cp1251`. The encoding is strict: a byte it does not define is an input error. Given for any other kind of file, it is a usage error. |

An option given twice is a usage error.

## Geometry warnings of an export

The IFC, scene JSON and HTML viewer exports draw every part as a rectangular proxy box between its start and end point,
with the width and height estimated from the numbers in the profile name (150 mm × 150 mm when none are read). The
estimate does not check the profile. The meshes are not decoded Tekla solids and are not made for clash detection or
exact measurement. The warnings go to standard error, so the output on standard output or in the file holds none of
them, and they do not change the exit code. The JSON export of the object model has no geometry and no such warning.

All three exports print one line with the number of proxy boxes. The scene JSON export (`--scene-json`) and the HTML
viewer export build the scene of the `TdbIO.Scene` adapter and then also print, each after a summary line, one line per
part whose box has the default dimensions and one line per part with no usable geometry (no start or end point, or a
degenerate axis). A part is named by its GUID, or `part:{id}` when it has none. Up to 20 parts per kind are listed; the
summary line counts them all, and the full list is in `TdbSceneResult.GeometryDiagnostics`. The IFC export prints only
the line with the number of boxes.

## Exit codes

| Code | Name | Meaning |
| --- | --- | --- |
| 0 | done | The command finished and its output was written. Warnings do not change the code. |
| 1 | usage | The command line is wrong: no command, an unknown command or option, no file, an empty file name, an extra argument, an option repeated, `--encoding` without a value or with an unknown one, or `--encoding` for a file that is not a template, report, model database or DBV@ file. Two lines on standard error: `tdbio: <problem>` and `Run 'tdbio --help' for usage.` |
| 2 | input | The file cannot be used: it does not exist or cannot be read, is not recognised, is cut or malformed, is over a limit, is not supported (a drawing file, UTF-16 text), or is of a kind the command does not handle (for example `tables` on a `.nc1` file). One line on standard error, `tdbio: <problem>`, no stack trace. |

The whole output is built before anything is written, so a failing command writes nothing to standard output. A
message about malformed data ends with the offset of the problem, for example
`tdbio: not a recognised Tekla file at offset 0x0`.

Warnings are written after the output, one line each: `tdbio: warning: <warning>`. They name an offset, a line, or, for
the [geometry warnings](#geometry-warnings-of-an-export), a part by its GUID or number, never text from the file.

## info

The first key is `kind`, the name of the `TdbFileKind` value. In text output the last key is `warnings`, the number of
warnings; in JSON output `warnings` is the list of warning lines. A value that is not there is `-` in text and `null` in
JSON.

| Kind | Keys |
| --- | --- |
| `Db1` | `version`, `modelGuid`, `idMapEntries`, `idMapField65` (hex in text), `idMapField73`, `textSections`, `textChains`, `saveStamps`, `lastSavedAt` (the latest save stamp date) |
| `Db2` | `version`, `modelGuid`, `records`, `rows` (the sum of the row counts) |
| `GuidMapper` | `pairs` |
| `IdRanges` | `next`, `ranges` |
| `History` | `pageSize`, `pageCount`, `length` |
| `Dbv` | `name`, `records` (GUID records), `sections`, `rows` (the sum over all sections) |
| `Nc1` | `blocks` (the block codes in order), `mark`, `material`, `quantity`, `profile`, `length` |
| `Template` | `encoding`, `rootKeyword`, `instance`, `type`, `version` (the values of the root properties of these names), `blocks` (the number of blocks below the root), `maxDepth` (the deepest block level; the root is level 0) |
| `Report` | `encoding`, `lines`, `pages` (the number of form feeds), `endsWithLineBreak` |

For a model database `info` also reads the text sections, so a malformed text section makes it fail with exit code 2.

Example, on a synthetic DSTV NC file:

```text
$ tdbio info part.nc1
kind: Nc1
blocks: ST,AK,EN
mark: A1
material: GRADE-X
quantity: 3
profile: PL12
length: 100
warnings: 0
```

```json
$ tdbio info part.nc1 --json
{
  "kind": "Nc1",
  "blocks": [
    "ST",
    "AK",
    "EN"
  ],
  "mark": "A1",
  "material": "GRADE-X",
  "quantity": 3,
  "profile": "PL12",
  "length": 100,
  "warnings": []
}
```

With `--encoding`, `encoding` shows the web name of the encoding used, for example `windows-1252` for `cp1252`.

## tables

Columns, separated by tabs:

| Column | Value |
| --- | --- |
| `ordinal` | The position of the section in the file, from 0. |
| `offset` | Where the section starts (the key when there is one, otherwise the separator), hex. |
| `key` | The raw key stored before the section, or `-`. Always `-` for DBV@ files. |
| `rowSize` | The number of body bytes in every row. |
| `descriptors` | The raw descriptor words, separated by commas. |
| `rows` | The number of rows. |
| `end` | The offset just after the section, hex. |

Offsets refer to the unpacked file. Row bodies are never printed.

```text
$ tdbio tables model.db1
ordinal	offset	key	rowSize	descriptors	rows	end
0	0x7D	-	8	1,0	2	0xB4
1	0xB4	42	4	1	1	0xDA
```

With `--json` the object has `kind`, `sections` (one object per section with the column names as properties; offsets
are decimal numbers, `key` is `null` when absent and `descriptors` is an array) and `warnings`.

## text

Columns, separated by tabs:

| Column | Value |
| --- | --- |
| `section` | The index of the text section, from 0. |
| `headId` | The id of the head row of the chain. |
| `rows` | The number of rows in the chain. |
| `length` | The number of text bytes. |
| `stamp` | `true` for a save stamp, else `false`. |
| `savedAt` | The date and time of a save stamp, `yyyy-MM-ddTHH:mm:ss`, or `-`. |
| `text` | The text bytes, escaped: printable ASCII as is, `\\`, `\t`, `\n`, and `\xNN` for every other byte. `-` for a save stamp. |

The text of a save stamp is never printed: besides the date it can name people or machines. Rows that belong to no
chain follow the chains, one line each: the section index, `-`, `orphan` and the row id.

```text
$ tdbio text model.db1
section	headId	rows	length	stamp	savedAt	text
0	1	1	6	false	-	Grid A
0	10	2	42	true	2026-02-01T10:20:30	-
```

With `--json` the object has `kind`, `chains` (one object per chain with the column names as properties; `savedAt` and
`text` are `null` when absent), `orphans` (objects with `section` and `id`) and `warnings`.

## JSON output

Numbers are decimal, GUIDs use the `D` format (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) and dates the format
`yyyy-MM-ddTHH:mm:ss`. The input path is never written. Warnings are both in the `warnings` array and on standard error.

## Text output

Each value fits on one line: control characters in text taken from a file are written as `\xNN`. Lists are joined with
commas. Numbers use the invariant culture.

All examples on this page come from small synthetic files; the values are made up.
