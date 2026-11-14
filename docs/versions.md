---
title: Tekla Structures versions and format variations
---

# Tekla Structures versions and format variations

This document records the database and file format characteristics across multiple Tekla Structures versions, from Tekla Structures 2021 through Tekla Structures 2026.0, based on inspection of model databases (`.db1`, `.db2`), table sections, text sections, and container headers.

## Tested versions

| Tekla Structures release | Version tag in header | Stream and section characteristics |
| ------------------------ | --------------------- | ---------------------------------- |
| 2021                     | `9.08`                | Multi-member gzip; multi-baseline pre-area |
| 2022                     | `9.21`                | Multi-member gzip; dual-C9 pre-area |
| 2024                     | `9.52`                | Multi-member gzip; dual-C9 pre-area |
| 2025 / 2026 early        | `9.60`                | Multi-member or single-member gzip; dual-C9 pre-area |
| 2026.0                   | `9.65`                | Multi-member or single-member gzip; dual-C9 pre-area |
| 2026.0 current           | `9.66`                | Multi-member or single-member gzip; dual-C9 pre-area |

## Container format

### Container header (49 bytes)

Across all versions from `9.08` to `9.66`, the container header follows an identical 49-byte (0x31) fixed ASCII layout:

- `0x00..0x05` (6 bytes): Magic string `Xsteel`.
- `0x06` (1 byte): File type byte:
  - `0x85` for model databases (`.db1`, `xslib.db1`).
  - `0x80` for model records (`.db2`, `xslib.db2`).
  - `0x20` for drawing files (`.cgr`).
- `0x07` (1 byte): Separator space (`0x20`).
- `0x08..0x0B` (4 bytes): Version formatted as `%5.2f` (e.g. `9.08`, `9.21`, `9.52`, `9.60`, `9.65`, `9.66`).
- `0x0C` (1 byte): Separator space (`0x20`).
- `0x0D..0x30` (36 bytes): Model GUID formatted as lowercase hyphenated UUID (`8-4-4-4-12`).

### Gzip compression: Single-member vs Multi-member streams

A key variation across releases lies in how gzip compression is written to disk:

1. **Multi-member Gzip Streams:**
   - In Tekla Structures 2021 (`9.08`), 2022 (`9.21`), and 2024 (`9.52`), `.db1` files are commonly written as **concatenated multi-member gzip streams** (per RFC 1952).
   - The file contains multiple sequential gzip blocks, each starting with `1F 8B 08 00` and having its own 8-byte trailer (`CRC-32` and `ISIZE`).
   - A trailing 20-byte empty gzip member (`1F 8B 08 00 00 00 00 00 04 0A 03 00 00 00 00 00 00 00 00 00`, ISIZE=0, CRC=0) is frequently appended as a stream terminator/flush block.
2. **Single-member Gzip Streams:**
   - Some models saved in newer versions (such as 2026.0 single-member saves) are packed as a single contiguous gzip member with the declared uncompressed size at the end of the file.

A compliant reader must decompress all members of a multi-member gzip stream sequentially until the end of the file.

## ID Map section

The ID map is located immediately after the 49-byte container header (at byte offset 49).

### Preamble structure

The 28-byte preamble before the entries is structurally uniform across all versions:

- `Offset 49` (u32): Fixed value `1`.
- `Offset 53` (u32): Marker `0xDBCEC0BC`.
- `Offset 57` (u32): Fixed value `0`.
- `Offset 61` (u32): Fixed value `1`.
- `Offset 65` (u32): Flag field `Field65`:
  - `0x10D` (269) with bit `0x100` set, seen in `9.08` (2021), `9.52` (2024), `9.60`, `9.65`, and `9.66`.
  - `0x0D` (13), seen in `9.21` (2022) and some clean exports.
- `Offset 69` (u32): Entry count $N$ (number of entries plus 1).
- `Offset 73` (u32): Domain/schema field `Field73`:
  - In standard metric projects (2021, 2022, 2024, 2026), `Field73` is consistently `431`.
  - In models configured with custom or regional domain numbering environments, `Field73` can contain a larger domain ID (e.g. values exceeding `5000000`).

### Entries and terminator

- Starting at offset 77, $N - 1$ entries of 24 bytes follow:
  - `Sequence` (u32): 1-based permutation index.
  - `Id` (16 bytes): GUID.
  - `Value` (i32): Schema or domain transaction integer.
- Immediately following entry $N - 1$, a trailing u32 `5` terminates the ID map across all versions.

## Text sections

Text sections are 54-byte rows located between the ID map and the table sections:

- Row size: 54 bytes (1 byte marker + 53 bytes body and chain links).
- Logical text reconstruction via row chains (`HeadId`, `HeadTag`, `HeadField13`, `RowIds`) is identical across all versions.
- Save stamps starting with `Save:` (with reversed and ROT13 shifted text containing ISO date/time, user, and workstation metadata) exist in every version tested.

## Pre-Area and First Section Lead

Between the text sections and the first table section, C9 domain baseline records (`0xC9, 0xC0, 0xCE, 0xDB`, each 20 bytes: marker, $R_0$, $R_1$, $R_2$, $R_3$) reside:

1. **Versions 9.21 through 9.66 (2022–2026):**
   - The lead immediately preceding the first section separator (`66 C0 CE DB`) consists of exactly two C9 records of `(0, 1, 0, -1)` (40 bytes total).
2. **Version 9.08 (2021):**
   - The pre-area contains a multi-baseline structure: a C9 record with $R_1 = 2$, followed by an 8-byte pointer/descriptor (`40 7D 3E 02 FF FF FF FF`), followed by a C9 record with $R_1 = 1$ immediately preceding the table section separator.

## Table sections

The internal database engine (LumoDB / DBvirtual) layout is fully consistent from 2021 to 2026:

- **Section structure:**
  - Unkeyed internal sections (indices 0..27).
  - Keyed schema tables (from index 28 onwards), preceded by a 4-byte table key (`Key`).
  - Section separator: `66 C0 CE DB`.
  - Section header: `RowSize` (u32), `DescriptorCount` (u32), and descriptor words.
  - Keyed section closer: `4F 61 BC 00`.
- **Row format:**
  - 1-byte marker: `0x04` for active rows, `0x0C` for deleted or superseded rows.
  - Body: `RowSize` bytes.
  - Trailing integers: Two signed 32-bit little-endian integers (`i32`, 4 bytes each) recording the creation transaction and deletion/superseding transaction IDs. On disk, each row occupies exactly `1 + RowSize + 4 + 4 = RowSize + 9` bytes. Note: While stored as 32-bit integers on disk, the public API exposes these values as 64-bit `long` properties (`CreationTimeId`, `DeletionTimeId`) for range safety and API consistency.
  - Row terminator: `0x00` byte after the last row of the section.
- **Key schema table IDs:**
  - Table 1: `object` (`ObjectRow`, size 72)
  - Table 2: `assembly` (`AssemblyRow`, size 92)
  - Table 3: `coordsys` (`CoordsysRow`, size 40)
  - Table 10: `part` (`PartRow`, size 64)
  - Table 13: `point` (`PointRow`, size 32)
  - Table 16: `relation` (`RelationRow`, size 60)
  - Table 20: `weld` (`WeldingRow`, size 24)
  - Table 38: `num_attr` (`NumAttrRow`, size 24)
  - Table 39: `str_attr` (`StrAttrRow`, size 24)
  - Table 44: `part_attr` (`PartAttrRow`, size 372)
  - Table 48: `num_attr_attr` (`NumAttrAttrRow`, size 44)
  - Table 49: `str_attr_attr` (`StrAttrAttrRow`, size 120)
  - Table 50: `string_pool` (`StringPoolRow`, size 45)
  - Table 94: `part_numbering` (`PartNumberingRow`, size 68)
  - Table 95: `assembly_numbering` (`AssemblyNumberingRow`, size 76)
  - Table 105: `object_numbering` (`ObjectNumberingRow`, size 12)
  - Table 107: `bolt` (`BoltRow`, size 24)
  - Table 132: `object_modification` (`ObjectModificationRow`, size 20)
  - Table 140: `bolt_hole` (`BoltHoleRow`, size 48)
  - Table 141: `object_phase` (`ObjectPhaseRow`, size 8)
  - Table 142: `bolt_hole_points` (`BoltHolePointsRow`, size 60)

All table sections across all versions walk continuously from the first section to the final byte of the uncompressed file.
