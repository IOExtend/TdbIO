---
title: TdbIO
---

# TdbIO

TdbIO is a .NET library and command line tool that reads Tekla Structures files directly from disk, without Tekla
Structures: model databases (`.db1`, `.db2` and their `.bak` copies), DBV@ files, id range and GUID map files,
`history.db`, DSTV NC files (`.nc1`), templates (`.tpl`, `.rpt`) and reports (`.xsr`).

- Tells the kind of a file from its content and opens it as a typed, read-only document.
- Reads the header of model databases, the id map, the text chains and the date of each save stamp.
- Walks the table sections of model databases and DBV@ files to the last byte. Rows are given as raw bytes.
- Reads DSTV NC parts, the block tree of templates and the pages of reports.
- Gives every value whose meaning is not known as stored, and reports unexpected values as warnings.

The library targets .NET 8 and .NET 10, has no package dependencies and never writes to the console. Version 0.1.0 is a
preview: the content of table rows is not yet decoded into model objects.

## Where to go next

- [Getting started](getting-started.md): install the tool or the package and read a first file.
- [Command line and exit codes](cli.md): the `tdbio` commands and their output.
- [Public API](public-api.md): every public type, the exceptions, the warnings and the limits.
- [Dependencies](dependencies.md): the runtime libraries and packages used.
- [Private test fixtures](test-fixtures.md): the three test tiers and how to run them.
- [API reference](api/TdbIO.yml): every public type, generated from the XML documentation.
- [Source on GitHub](https://github.com/IOExtend/TdbIO).

TdbIO is not affiliated with, endorsed by or sponsored by Trimble. Tekla Structures is a trademark of Trimble Inc. (or
its affiliates).

The file formats are read by analysing files for interoperability. No Tekla code or binaries are included.
