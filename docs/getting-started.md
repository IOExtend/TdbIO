---
title: Getting started
---

# Getting started

## Command line tool

```sh
dotnet tool install --global TdbIO.Cli
tdbio info model.db1
```

`info` works on every supported file and prints its kind and main values. Two more commands list the inside of a model
database:

```sh
tdbio tables model.db1
tdbio text model.db1
tdbio tables model.db1 --json
```

`tables` also works on DBV@ files. For templates and reports, and for the text values of a model database, `--encoding`
overrides the detected encoding:

```sh
tdbio info list.tpl --encoding cp1252
tdbio export model.db1 model.json --encoding cp1252
```

Run `tdbio --help` for the usage. Exit code 0 means done, 1 a wrong command line, 2 a file that cannot be used. Every
command, key, column and JSON property is described in [Command line and exit codes](cli.md).

## Library

```sh
dotnet add package TdbIO
```

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

`TdbFile.Open` finds the kind from the content, so the file name matters only for reports (`.xsr`), which have no
signature. To check a file without reading it whole, call `TdbFile.DetectKind`. A stream works too:

```csharp
using System.IO;
using TdbIO;

using FileStream stream = File.OpenRead("list.xsr");
var report = (ReportDocument)TdbFile.Open(stream, fileName: "list.xsr");
```

Malformed data throws `InvalidDataException`; its message ends with the offset of the problem, and `Data["Offset"]`
holds it as a `long`. A file that is recognised but not supported (a drawing file, a UTF-16 report) throws
`NotSupportedException`. Values that are kept although they differ from
what is expected are listed in `Warnings`. Large input is capped by `TdbReadOptions.MaxUnpackedLength` (1 GiB by
default):

```csharp
var options = new TdbReadOptions { MaxUnpackedLength = 256L * 1024 * 1024 };
TdbDocument small = TdbFile.Open("model.db1", options);
```

The library is silent: it never writes to the console. The supported surface is listed in [Public API](public-api.md);
every type is in the [API reference](api/TdbIO.yml).

## Status

TdbIO is a preview and the API can change before 1.0. The content of table rows is given as raw bytes and is not yet
decoded into model objects.
