using System.Buffers.Binary;
using System.Text.Json;
using TdbIO.CommandLine;
using Xunit;
using static TdbIO.Tests.Synthetic;

namespace TdbIO.Tests;

/// <summary>The <c>tdbio</c> command line on synthetic files written to a temporary folder.</summary>
public sealed class CliTests : IDisposable
{
    private const string Pair = "11111111-2222-4333-8444-555555555555 66666666-7777-4888-9999-aaaaaaaaaaaa";
    private static readonly Guid A = new("0a0b0c0d-1111-4222-8333-444455556666");

    // Revealed save text with a made-up person name: it must never reach the output, in any form.
    private const string StampText = "01.02.2003;04:05:06;Someone;host";

    private readonly string _folder = Directory.CreateTempSubdirectory("tdbio-cli-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string File(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, data);
        return path;
    }

    private static (int Exit, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int exit = Cli.Run(args, stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private static string[] Lines(string text) => text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    private string Db1WithText(uint field65 = 0x0D) => File("m.db1", Gzip(Concat(
        XsteelHead(),
        IdMapBody([(1, A, 7)], field65),
        TextSectionBytes([.. ChainRows(1, 3, Ascii("Plain\ttext\\")), .. ChainRows(10, 5, HiddenSaveText(StampText))]))));

    private string Db1WithTables() => File("t.db1", Gzip(Db1TablesPayload(
        Db1Section(null, 4, [1], (0x04, Counting(4), 1, 2)),
        Db1Section(7, 8, [1, 0], (0x0C, Counting(8), -1, 3), (0x04, Counting(8, 8), 4, 5)))));

    private string Dbv() => File("d.db5", Gzip(new DbvParts { Sections = [DbvSection(4, [1], (1, Counting(4), 0x401, 0x402))] }.Build()));

    // Usage errors

    [Theory]
    [InlineData]
    [InlineData("dump", "x")]
    [InlineData("info")]
    [InlineData("info", "")]
    [InlineData("info", "a", "b")]
    [InlineData("info", "a", "--json", "--json")]
    [InlineData("info", "a", "--xml")]
    [InlineData("info", "a", "--encoding")]
    [InlineData("info", "a", "--encoding", "utf-16")]
    [InlineData("info", "a", "--encoding", "ascii", "--encoding", "ascii")]
    [InlineData("tables", "a", "--encoding", "ascii")]
    [InlineData("export")]
    [InlineData("export", "a", "--indent", "--indent")]
    [InlineData("export", "a", "--encoding", "ascii")]
    [InlineData("text", "-h")]
    [InlineData("--help", "x")]
    public void UsageErrorsExitOneWithTwoLines(params string[] args)
    {
        var (exit, stdout, stderr) = Run(args);
        Assert.Equal(Cli.ExitUsage, exit);
        Assert.Equal("", stdout);
        string[] lines = Lines(stderr);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("tdbio: ", lines[0]);
        Assert.Equal("Run 'tdbio --help' for usage.", lines[1]);
    }

    [Fact]
    public void EncodingOnAModelFileIsAUsageError()
    {
        var (exit, stdout, _) = Run("info", Db1WithText(), "--encoding", "cp1251");
        Assert.Equal(Cli.ExitUsage, exit);
        Assert.Equal("", stdout);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpShowsUsage(string flag)
    {
        var (exit, stdout, stderr) = Run(flag);
        Assert.Equal(Cli.ExitOk, exit);
        Assert.StartsWith("Usage: tdbio info <file>", stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void VersionIsTheAssemblyVersion()
    {
        var (exit, stdout, _) = Run("--version");
        Assert.Equal(Cli.ExitOk, exit);
        Assert.Matches(@"^tdbio \d+\.\d+\.\d+", stdout);
    }

    // info

    private static Dictionary<string, string> Fields(string stdout) =>
        Lines(stdout).Select(l => l.Split(": ", 2)).ToDictionary(p => p[0], p => p[1]);

    [Fact]
    public void InfoOnModelDatabase()
    {
        var (exit, stdout, stderr) = Run("info", Db1WithText());
        Assert.Equal(Cli.ExitOk, exit);
        Assert.Equal("", stderr);
        var f = Fields(stdout);
        Assert.Equal("Db1", f["kind"]);
        Assert.Equal("9.99", f["version"]);
        Assert.Equal(ContainerGuidText, f["modelGuid"]);
        Assert.Equal("1", f["idMapEntries"]);
        Assert.Equal("0xD", f["idMapField65"]);
        Assert.Equal("1", f["textSections"]);
        Assert.Equal("2", f["textChains"]);
        Assert.Equal("1", f["saveStamps"]);
        Assert.Equal("2003-02-01T04:05:06", f["lastSavedAt"]);
        Assert.Equal("0", f["warnings"]);
    }

    [Fact]
    public void InfoOnOtherKinds()
    {
        string db2 = File("m.db2", Gzip(Concat(XsteelHead(0x80), Db2Record(1, 2, 3, [4, 1, 2, 3, 4, 4, 5, 6]))));
        Assert.Equal(("Db2", "2"), (Fields(Run("info", db2).Out)["kind"], Fields(Run("info", db2).Out)["rows"]));

        Assert.Equal("1", Fields(Run("info", File("guid.mapper", Gzip(GuidMapperText(Pair)))).Out)["pairs"]);

        var ranges = Fields(Run("info", File("m.idrm", Gzip(IdRangeFile(30, (10, A, 0, 20))))).Out);
        Assert.Equal(("IdRanges", "30", "1"), (ranges["kind"], ranges["next"], ranges["ranges"]));

        var dbv = Fields(Run("info", Dbv()).Out);
        Assert.Equal(("Dbv", "Abc", "2", "1", "1"), (dbv["kind"], dbv["name"], dbv["records"], dbv["sections"], dbv["rows"]));

        var nc1 = Fields(Run("info", File("p.nc1", Nc1File(Nc1Rectangle()))).Out);
        Assert.Equal(("Nc1", "ST,AK,EN", "A1", "GRADE-X", "3", "PL12", "100"),
            (nc1["kind"], nc1["blocks"], nc1["mark"], nc1["material"], nc1["quantity"], nc1["profile"], nc1["length"]));

        var tpl = Fields(Run("info", File("a.tpl", Ascii(Template))).Out);
        Assert.Equal(("Template", "ascii", "template", "_tmp_0", "TEXTUAL", "2.5", "3", "2"),
            (tpl["kind"], tpl["encoding"], tpl["rootKeyword"], tpl["instance"], tpl["type"], tpl["version"], tpl["blocks"], tpl["maxDepth"]));

        var xsr = Fields(Run("info", File("r.xsr", Ascii("  a\r\n\f  b\r\n"))).Out);
        Assert.Equal(("Report", "ascii", "2", "1", "true"), (xsr["kind"], xsr["encoding"], xsr["lines"], xsr["pages"], xsr["endsWithLineBreak"]));
    }

    [Fact]
    public void InfoOnHistoryWarnsAboutTheLength()
    {
        var (exit, stdout, stderr) = Run("info", File("history.db", SqliteHead(512, 3)));
        Assert.Equal(Cli.ExitOk, exit);
        var f = Fields(stdout);
        Assert.Equal(("History", "512", "3", "100", "1"), (f["kind"], f["pageSize"], f["pageCount"], f["length"], f["warnings"]));
        Assert.StartsWith("tdbio: warning: ", Assert.Single(Lines(stderr)));
    }

    [Fact]
    public void ForcedEncodingIsUsed()
    {
        var f = Fields(Run("info", File("r.xsr", Ascii("  a\r\n")), "--encoding", "cp1252").Out);
        Assert.Equal("windows-1252", f["encoding"]);
    }

    [Fact]
    public void ForcedEncodingThatDoesNotFitIsAnInputError()
    {
        var (exit, stdout, stderr) = Run("info", File("r.xsr", [0x20, 0xC3, 0x0D, 0x0A]), "--encoding", "ascii");
        Assert.Equal(Cli.ExitInput, exit);
        Assert.Equal("", stdout);
        Assert.Contains("at offset 0x1", Assert.Single(Lines(stderr)));
    }

    [Fact]
    public void ForcedUtf8DropsTheByteOrderMark()
    {
        var (exit, stdout, _) = Run("info", File("t.tpl", Synthetic.Concat([0xEF, 0xBB, 0xBF], Ascii("template {\r\n};\r\n"))),
            "--encoding", "utf-8");
        Assert.Equal(0, exit);
        Assert.Equal("utf-8", Fields(stdout)["encoding"]);
    }

    [Fact]
    public void WarningsGoToStderrAndKeepExitZero()
    {
        var (exit, stdout, stderr) = Run("info", Db1WithText(field65: 0x99));
        Assert.Equal(Cli.ExitOk, exit);
        Assert.Equal("1", Fields(stdout)["warnings"]);
        Assert.StartsWith("tdbio: warning: ", Assert.Single(Lines(stderr)));
    }

    [Fact]
    public void InfoJsonParsesAndHoldsWarnings()
    {
        string path = Db1WithText(field65: 0x99);
        var (exit, stdout, _) = Run("info", path, "--json");
        Assert.Equal(Cli.ExitOk, exit);
        using var json = JsonDocument.Parse(stdout);
        var root = json.RootElement;
        Assert.Equal("Db1", root.GetProperty("kind").GetString());
        Assert.Equal(0x99, root.GetProperty("idMapField65").GetInt32());
        Assert.Equal(1, root.GetProperty("idMapEntries").GetInt32());
        Assert.Equal("2003-02-01T04:05:06", root.GetProperty("lastSavedAt").GetString());
        Assert.Equal(1, root.GetProperty("warnings").GetArrayLength());
        Assert.DoesNotContain(_folder.Replace(@"\", @"\\"), stdout);
        Assert.DoesNotContain("m.db1", stdout);
    }

    // tables

    [Fact]
    public void TablesOnModelDatabase()
    {
        var (exit, stdout, _) = Run("tables", Db1WithTables());
        Assert.Equal(Cli.ExitOk, exit);
        string[] lines = Lines(stdout);
        Assert.Equal("ordinal\toffset\tkey\trowSize\tdescriptors\trows\tend", lines[0]);
        Assert.Equal(3, lines.Length);
        string[] plain = lines[1].Split('\t');
        Assert.Equal(("0", "-", "4", "1", "1"), (plain[0], plain[2], plain[3], plain[4], plain[5]));
        Assert.StartsWith("0x", plain[1]);
        string[] keyed = lines[2].Split('\t');
        Assert.Equal(("1", "7", "8", "1,0", "2"), (keyed[0], keyed[2], keyed[3], keyed[4], keyed[5]));
    }

    [Fact]
    public void TablesJsonOnDbv()
    {
        var (exit, stdout, _) = Run("tables", Dbv(), "--json");
        Assert.Equal(Cli.ExitOk, exit);
        using var json = JsonDocument.Parse(stdout);
        var section = Assert.Single(json.RootElement.GetProperty("sections").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, section.GetProperty("key").ValueKind);
        Assert.Equal(4, section.GetProperty("rowSize").GetInt32());
        Assert.Equal(1, section.GetProperty("rows").GetInt32());
        Assert.True(section.GetProperty("end").GetInt64() > section.GetProperty("offset").GetInt64());
        Assert.Equal(0, json.RootElement.GetProperty("warnings").GetArrayLength());
    }

    // text

    [Fact]
    public void TextShowsOpenTextEscapedAndOnlyTheDateOfSaveStamps()
    {
        var (exit, stdout, _) = Run("text", Db1WithText());
        Assert.Equal(Cli.ExitOk, exit);
        string[] lines = Lines(stdout);
        Assert.Equal("section\theadId\trows\tlength\tstamp\tsavedAt\ttext", lines[0]);
        Assert.Equal("0\t1\t1\t11\tfalse\t-\tPlain\\ttext\\\\", lines[1]);
        string[] stamp = lines[2].Split('\t');
        Assert.Equal(("0", "10", "true", "2003-02-01T04:05:06", "-"), (stamp[0], stamp[1], stamp[4], stamp[5], stamp[6]));
        Assert.DoesNotContain("Someone", stdout);
        Assert.DoesNotContain("rabrzbF", stdout);
        Assert.DoesNotContain("Save:", stdout);
    }

    [Fact]
    public void TextJsonHidesSaveStampText()
    {
        var (exit, stdout, _) = Run("text", Db1WithText(), "--json");
        Assert.Equal(Cli.ExitOk, exit);
        using var json = JsonDocument.Parse(stdout);
        var chains = json.RootElement.GetProperty("chains").EnumerateArray().ToList();
        Assert.Equal(2, chains.Count);
        Assert.Equal(@"Plain\ttext\\", chains[0].GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, chains[1].GetProperty("text").ValueKind);
        Assert.Empty(json.RootElement.GetProperty("orphans").EnumerateArray());
        Assert.DoesNotContain("Someone", stdout);
    }

    [Fact]
    public void OrphanRowsArePrinted()
    {
        byte[] section = TextSectionBytes([.. ChainRows(1, 3, Ascii("x")), TextRow(9, 0, 0xFFFFFFFF, Ascii("y"))]);
        var (exit, stdout, _) = Run("text", File("o.db1", Gzip(Concat(XsteelHead(), IdMapBody([]), section))));
        Assert.Equal(Cli.ExitOk, exit);
        Assert.Equal("0\t-\torphan\t9", Lines(stdout)[^1]);
    }

    [Fact]
    public void BytesAreEscaped() =>
        Assert.Equal(@"a\\\t\n\x00\x7F\xC3\x0D~", Output.EscapeBytes([(byte)'a', (byte)'\\', 9, 10, 0, 0x7F, 0xC3, 13, (byte)'~']));

    // Input errors

    [Fact]
    public void MissingFileIsAnInputError() => AssertInputError("info", Path.Combine(_folder, "none.db1"));

    [Fact]
    public void DrawingIsAnInputError() => AssertInputError("info", File("x.dg", Gzip(XsteelHead(0x20))));

    [Fact]
    public void TablesOnDstvIsAnInputError() => AssertInputError("tables", File("p.nc1", Nc1File(Nc1Rectangle())));

    [Fact]
    public void TextOnDbvIsAnInputError() => AssertInputError("text", Dbv());

    [Fact]
    public void UnknownFileIsAnInputError() => AssertInputError("info", File("x.bin", Ascii("hello")));

    [Fact]
    public void CutGzipIsAnInputError()
    {
        byte[] gz = Gzip(Concat(XsteelHead(), IdMapBody([])));
        AssertInputError("info", File("cut.db1", gz[..^3]));
    }

    [Fact]
    public void MalformedDstvIsAnInputError() => AssertInputError("info", File("bad.nc1", Ascii("ST\r\nAK\r\n")));

    [Fact]
    public void BrokenTablesAreAnInputErrorWithNoOutput() =>
        AssertInputError("tables", File("m.db1", Gzip(Concat(XsteelHead(), IdMapBody([])))));

    [Fact]
    public void ExportOnDbvIsAnInputError() => AssertInputError("export", Dbv());

    [Fact]
    public void ExportOutputsJsonForDb1()
    {
        var (exit, stdout, stderr) = Run("export", Db1WithTables(), "--indent");
        Assert.Equal(Cli.ExitOk, exit);
        Assert.Equal("", stderr);
        using var json = JsonDocument.Parse(stdout);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("modelGuid", out _));
        Assert.True(root.TryGetProperty("assemblies", out _));
        Assert.True(root.TryGetProperty("parts", out _));
        Assert.True(root.TryGetProperty("counts", out _));
    }

    [Fact]
    public void ExportOnModelWithNonFiniteNumberReturnsExitInput()
    {
        var partBytes = new byte[PartRow.ExpectedRowSize];
        BinaryPrimitives.WriteInt32LittleEndian(partBytes.AsSpan(0), 101);
        BinaryPrimitives.WriteDoubleLittleEndian(partBytes.AsSpan(56), double.NaN);

        var objBytes = new byte[ObjectRow.ExpectedRowSize];
        BinaryPrimitives.WriteInt32LittleEndian(objBytes.AsSpan(0), 101);
        BinaryPrimitives.WriteInt32LittleEndian(objBytes.AsSpan(28), 1); // Type = 1 (Part)

        byte[] unkeyedSec = Db1Section(null, 4, [1], (0x04, Counting(4), 1, 2));
        byte[] objSec = Db1Section(ObjectRow.TableKey, ObjectRow.ExpectedRowSize, [1], (0x04, objBytes, 1, 2));
        byte[] partSec = Db1Section(PartRow.TableKey, PartRow.ExpectedRowSize, [1], (0x04, partBytes, 1, 2));

        string nanFile = File("nan.db1", Gzip(Db1TablesPayload(unkeyedSec, objSec, partSec)));

        var (exit, stdout, stderr) = Run("export", nanFile);
        Assert.Equal(Cli.ExitInput, exit);
        Assert.Contains("Non-finite number NaN in 'length'", stderr);
        Assert.Contains("0x152", stderr);

        // Finite counterpart succeeds
        var finitePartBytes = (byte[])partBytes.Clone();
        BinaryPrimitives.WriteDoubleLittleEndian(finitePartBytes.AsSpan(56), 1000.0);
        byte[] finitePartSec = Db1Section(PartRow.TableKey, PartRow.ExpectedRowSize, [1], (0x04, finitePartBytes, 1, 2));
        string okFile = File("ok.db1", Gzip(Db1TablesPayload(unkeyedSec, objSec, finitePartSec)));

        var (okExit, okStdout, okStderr) = Run("export", okFile);
        Assert.Equal(Cli.ExitOk, okExit);
        Assert.Empty(okStderr);
        using var json = JsonDocument.Parse(okStdout);
        Assert.Equal(1000.0, json.RootElement.GetProperty("parts")[0].GetProperty("length").GetDouble());
    }

    private static void AssertInputError(params string[] args)
    {
        var (exit, stdout, stderr) = Run(args);
        Assert.Equal(Cli.ExitInput, exit);
        Assert.Equal("", stdout);
        string line = Assert.Single(Lines(stderr));
        Assert.StartsWith("tdbio: ", line);
        Assert.DoesNotContain("   at ", stderr);
    }
}
