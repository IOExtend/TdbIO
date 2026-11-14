using System.Reflection;
using System.Text;

namespace TdbIO.CommandLine;

/// <summary>The command line of the <c>tdbio</c> tool: usage text, argument parsing, the commands and the exit codes. It
/// uses only the public API of the library.</summary>
internal static class Cli
{
    public const int ExitOk = 0;
    public const int ExitUsage = 1;
    public const int ExitInput = 2;

    internal const string UsageText = """
        Usage: tdbio info <file> [--json] [--encoding ascii|utf-8|cp1251|cp1252]
               tdbio tables <file> [--json]
               tdbio text <file> [--json]
               tdbio export <file> [--indent]
               tdbio --help | -h | --version

        Commands:
          info      The kind of the file and its main values.
          tables    The table sections of a model database (.db1) or a DBV@ file, one line each.
          text      The text chains of a model database (.db1), one line each. Save stamps show only their date.
          export    Decodes the object model of a model database (.db1) and writes it as JSON.

        Options:
          --json      Write one JSON object instead of text.
          --indent    Format the JSON output with indentation (export only).
          --encoding  The encoding of a template or report, instead of detecting it (info only).

        Warnings go to stderr and do not change the exit code.
        Exit codes: 0 done, 1 usage error, 2 input error (missing, malformed or unsupported file).
        """;

    private static readonly string[] TableColumns = ["ordinal", "offset", "key", "rowSize", "descriptors", "rows", "end"];
    private static readonly string[] TextColumns = ["section", "headId", "rows", "length", "stamp", "savedAt", "text"];
    private static readonly string[] OrphanColumns = ["section", "id"];

    private sealed record Command(string Name, string Path, bool Json, bool Indent, Encoding? Encoding);

    // A usage error found while parsing or after the kind is known.
    private sealed class UsageException(string message) : Exception(message);

    // The file is fine but of a kind the command does not handle.
    private sealed class WrongKindException(string message) : Exception(message);

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        switch (args)
        {
            case ["--help" or "-h"]:
                stdout.WriteLine(UsageText);
                return ExitOk;
            case ["--version"]:
                string version = typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
                stdout.WriteLine($"tdbio {version.Split('+')[0]}");
                return ExitOk;
        }

        try
        {
            Command command = Parse(args);
            TdbDocument doc = TdbFile.Open(command.Path, new TdbReadOptions { TextEncoding = command.Encoding });
            if (command.Encoding != null && doc is not (TemplateDocument or ReportDocument))
                throw new UsageException("Option '--encoding' applies to templates and reports only.");

            // Everything is read before anything is written, so an error never leaves half an output.
            var output = new StringWriter();
            switch (command.Name)
            {
                case "info": Info(output, doc, command.Json); break;
                case "tables": Tables(output, doc, command.Json); break;
                case "export": Export(output, doc, command.Indent); break;
                default: Text(output, doc, command.Json); break;
            }
            stdout.Write(output.ToString());
            foreach (string warning in doc.Warnings) stderr.WriteLine($"tdbio: warning: {warning}");
            return ExitOk;
        }
        catch (UsageException e)
        {
            stderr.WriteLine($"tdbio: {e.Message}");
            stderr.WriteLine("Run 'tdbio --help' for usage.");
            return ExitUsage;
        }
        catch (Exception e) when (e is WrongKindException or InvalidDataException or NotSupportedException or IOException
                                      or UnauthorizedAccessException or DecoderFallbackException)
        {
            stderr.WriteLine($"tdbio: {e.Message}");
            return ExitInput;
        }
    }

    private static Command Parse(string[] args)
    {
        if (args.Length == 0) throw new UsageException("No command given.");
        string name = args[0];
        if (name is not ("info" or "tables" or "text" or "export")) throw new UsageException($"Unknown command '{name}'.");

        string? path = null;
        string? encodingName = null;
        bool json = false;
        bool indent = false;
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--json")
            {
                if (json) throw new UsageException("Option '--json' is repeated.");
                json = true;
            }
            else if (arg == "--indent" && name == "export")
            {
                if (indent) throw new UsageException("Option '--indent' is repeated.");
                indent = true;
            }
            else if (arg == "--encoding" && name == "info")
            {
                if (encodingName != null) throw new UsageException("Option '--encoding' is repeated.");
                if (i + 1 == args.Length) throw new UsageException("Option '--encoding' needs a value.");
                encodingName = args[++i];
            }
            else if (arg.Length == 0) throw new UsageException("The file name is empty.");
            else if (arg[0] == '-') throw new UsageException($"Unknown option '{arg}' for '{name}'.");
            else if (path != null) throw new UsageException($"Unexpected argument '{arg}'.");
            else path = arg;
        }
        if (path == null) throw new UsageException("No file given.");
        return new Command(name, path, json, indent, encodingName == null ? null : EncodingOf(encodingName));
    }

    // Strict encodings: a byte the encoding does not define is an input error, never replaced.
    private static Encoding EncodingOf(string name) => name switch
    {
        "ascii" => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
        // With the identifier the encoding has a preamble, so the library drops a byte order mark at the start.
        "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
        "cp1251" or "cp1252" => CodePagesEncodingProvider.Instance.GetEncoding(name == "cp1251" ? 1251 : 1252,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)!,
        _ => throw new UsageException($"Unknown encoding '{name}'."),
    };

    private static void Info(TextWriter w, TdbDocument doc, bool json)
    {
        var fields = new List<KeyValuePair<string, object?>>();
        void Add(string key, object? value) => fields.Add(new(key, value));

        Add("kind", doc.Kind.ToString());
        switch (doc)
        {
            case Db1Document db1:
                var sections = db1.ReadTextSections();
                var chains = sections.SelectMany(s => s.Chains).ToList();
                Add("version", db1.Version);
                Add("modelGuid", db1.ModelGuid);
                Add("idMapEntries", db1.IdMap?.Count);
                Add("idMapField65", db1.IdMapField65 is uint field65 ? new Hex(field65) : null);
                Add("idMapField73", db1.IdMapField73);
                Add("textSections", sections.Count);
                Add("textChains", chains.Count);
                Add("saveStamps", chains.Count(c => c.IsSaveStamp));
                Add("lastSavedAt", chains.Max(c => c.SavedAt));
                break;
            case Db2Document db2:
                Add("version", db2.Version);
                Add("modelGuid", db2.ModelGuid);
                Add("records", db2.Records.Count);
                Add("rows", db2.Records.Sum(r => (long)r.Count));
                break;
            case GuidMapperDocument mapper:
                Add("pairs", mapper.Pairs.Count);
                break;
            case IdRangeDocument ranges:
                Add("next", ranges.Next);
                Add("ranges", ranges.Ranges.Count);
                break;
            case HistoryDocument history:
                Add("pageSize", history.PageSize);
                Add("pageCount", history.PageCount);
                Add("length", history.Length);
                break;
            case DbvDocument dbv:
                Add("name", dbv.Name);
                Add("records", dbv.GuidRecords.Count);
                Add("sections", dbv.Sections.Count);
                Add("rows", dbv.Sections.Sum(s => (long)s.Rows.Count));
                break;
            case Nc1Document nc1:
                Add("blocks", nc1.Blocks.Select(b => b.Code).ToList());
                Add("mark", nc1.Mark);
                Add("material", nc1.Material);
                Add("quantity", nc1.Quantity);
                Add("profile", nc1.Profile);
                Add("length", nc1.Length);
                break;
            case TemplateDocument template:
                var (blocks, depth) = Measure(template.Root);
                Add("encoding", template.EncodingName);
                Add("rootKeyword", template.Root.Keyword);
                Add("instance", template.Root.Instance);
                Add("type", template.Root.GetValue("type")?.Text);
                Add("version", template.Root.GetValue("version")?.Text);
                Add("blocks", blocks);
                Add("maxDepth", depth);
                break;
            case ReportDocument report:
                Add("encoding", report.EncodingName);
                Add("lines", report.Lines.Count);
                Add("pages", report.PageStarts.Count);
                Add("endsWithLineBreak", report.EndsWithLineBreak);
                break;
        }

        if (json)
        {
            Output.WriteJson(w, j =>
            {
                Output.WriteProperties(j, fields);
                Output.WriteProperties(j, [new("warnings", doc.Warnings)]);
            });
        }
        else
        {
            Add("warnings", doc.Warnings.Count);
            Output.WriteFields(w, fields);
        }
    }

    private static void Tables(TextWriter w, TdbDocument doc, bool json)
    {
        IReadOnlyList<TableSection> sections = doc switch
        {
            Db1Document db1 => db1.ReadTables(),
            DbvDocument dbv => dbv.Sections,
            _ => throw new WrongKindException($"'tables' needs a model database (.db1) or a DBV@ file, not {doc.Kind}."),
        };
        var rows = sections.Select(s => new object?[]
            { s.Ordinal, new Hex(s.Offset), s.Key, s.RowSize, s.Descriptors, s.Rows.Count, new Hex(s.End) }).ToList();

        if (json)
        {
            Output.WriteJson(w, j =>
            {
                Output.WriteProperties(j, [new("kind", doc.Kind.ToString())]);
                Output.WriteObjects(j, "sections", TableColumns, rows);
                Output.WriteProperties(j, [new("warnings", doc.Warnings)]);
            });
        }
        else Output.WriteTsv(w, TableColumns, rows);
    }

    private static void Text(TextWriter w, TdbDocument doc, bool json)
    {
        if (doc is not Db1Document db1) throw new WrongKindException($"'text' needs a model database (.db1), not {doc.Kind}.");
        var sections = db1.ReadTextSections();

        // A save stamp may name people or machines, so only its date is shown, never its text.
        var chains = sections.SelectMany((s, i) => s.Chains.Select(c => new object?[]
        {
            i, c.HeadId, c.RowIds.Count, c.Text.Length, c.IsSaveStamp, c.SavedAt,
            c.IsSaveStamp ? null : Output.EscapeBytes(c.Text.Span),
        })).ToList();
        var orphans = sections.SelectMany((s, i) => s.OrphanRowIds.Select(id => new object?[] { i, id })).ToList();

        if (json)
        {
            Output.WriteJson(w, j =>
            {
                Output.WriteProperties(j, [new("kind", doc.Kind.ToString())]);
                Output.WriteObjects(j, "chains", TextColumns, chains);
                Output.WriteObjects(j, "orphans", OrphanColumns, orphans);
                Output.WriteProperties(j, [new("warnings", doc.Warnings)]);
            });
        }
        else
        {
            Output.WriteTsv(w, TextColumns, chains);
            foreach (var orphan in orphans) w.WriteLine($"{orphan[0]}\t-\torphan\t{orphan[1]}");
        }
    }

    private static void Export(TextWriter w, TdbDocument doc, bool indent)
    {
        if (doc is not Db1Document db1) throw new WrongKindException($"'export' needs a model database (.db1), not {doc.Kind}.");
        var model = db1.ReadModel();
        w.WriteLine(model.ToJson(indent));
    }

    // The number of blocks below the root and the deepest level (the root is level 0).
    private static (int Blocks, int Depth) Measure(TemplateBlock block)
    {
        int blocks = 0, depth = 0;
        foreach (var child in block.Blocks)
        {
            var (n, d) = Measure(child);
            blocks += 1 + n;
            depth = Math.Max(depth, d + 1);
        }
        return (blocks, depth);
    }
}
