using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class TemplateTests
{
    private static TemplateBlock Parse(string text) => TemplateParser.Parse(text);

    private static void Throws(string text, int offset, int line)
    {
        var e = Assert.Throws<InvalidDataException>(() => Parse(text));
        Assert.Equal(offset, e.Offset);
        Assert.EndsWith($" line {line}", e.Message);
    }

    [Fact]
    public void BlocksPropertiesAndInstancesAreRead()
    {
        var root = Parse(Synthetic.Template);

        Assert.Equal("template", root.Keyword);
        Assert.Equal("_tmp_0", root.Instance);
        Assert.Equal(["name", "type", "version", "margins"], root.Properties.Select(p => p.Key));
        Assert.Equal("Part list", root.GetValue("name")!.Text);
        Assert.Equal(TemplateValueKind.String, root.GetValue("name")!.Kind);
        Assert.Equal(TemplateValueKind.Identifier, root.GetValue("type")!.Kind);
        Assert.Equal("TEXTUAL", root.GetValue("type")!.Text);
        Assert.Equal(2.5m, root.GetValue("version")!.Number);
        Assert.Equal("2.5", root.GetValue("version")!.Text);

        var row = Assert.Single(root.Blocks);
        Assert.Equal(("row", "_tmp_1"), (row.Keyword, row.Instance));
        Assert.Equal(["valuefield", "text"], row.Blocks.Select(b => b.Keyword));
        Assert.Null(row.Blocks[1].Instance);
        Assert.Equal("Value(\"A\")", row.Blocks[0].GetValue("expr")!.Text);
        Assert.Empty(row.Blocks[0].Blocks);
    }

    [Fact]
    public void TuplesHoldTheirItems()
    {
        var root = Parse("template { a = (4, 0, -2, 0); b = (1, 0.5); c = ((1, 2), X, \"s\"); d = -1.25; };");

        var a = root.GetValue("a")!;
        Assert.Equal(TemplateValueKind.Tuple, a.Kind);
        Assert.Equal("", a.Text);
        Assert.Null(a.Number);
        Assert.Equal([4m, 0m, -2m, 0m], a.Items.Select(i => i.Number!.Value));
        Assert.Equal([1m, 0.5m], root.GetValue("b")!.Items.Select(i => i.Number!.Value));

        var c = root.GetValue("c")!;
        Assert.Equal([TemplateValueKind.Tuple, TemplateValueKind.Identifier, TemplateValueKind.String], c.Items.Select(i => i.Kind));
        Assert.Equal(2, c.Items[0].Items.Count);
        Assert.Equal(-1.25m, root.GetValue("d")!.Number);
        Assert.Empty(root.GetValue("d")!.Items);
    }

    [Fact]
    public void TheFourEscapesAreResolvedAndNothingElseChanges()
    {
        var root = Parse("""template { a = "q\"x\" \n\r\t"; b = "x\r\r\n"; """ + "c = \"\u00B2 \u00A0\"; };");
        Assert.Equal("q\"x\" \n\r\t", root.GetValue("a")!.Text);
        Assert.Equal("x\r\r\n", root.GetValue("b")!.Text);
        Assert.Equal("\u00B2 \u00A0", root.GetValue("c")!.Text);
    }

    [Fact]
    public void NestedQuotesAreUnescapedOneLevel()
    {
        var root = Parse("""template { f = "GetValue(\"Text(\"x\")\")"; };""");
        Assert.Equal("GetValue(\"Text(\"x\")\")", root.GetValue("f")!.Text);
    }

    [Fact]
    public void RepeatedKeysAreKeptInOrder()
    {
        var root = Parse("template { a = 1; b = 2; a = 3; };");
        Assert.Equal(["a", "b", "a"], root.Properties.Select(p => p.Key));
        Assert.Equal(1m, root.GetValue("a")!.Number);
        Assert.Equal(3m, root.Properties[2].Value.Number);
    }

    [Fact]
    public void GetValueIsOrdinal()
    {
        var root = Parse("template { Name = 1; };");
        Assert.Null(root.GetValue("name"));
        Assert.NotNull(root.GetValue("Name"));
        Assert.Throws<ArgumentNullException>(() => root.GetValue(null!));
    }

    [Fact]
    public void LayoutDoesNotMatter()
    {
        var root = Parse("template{a=1;b c{d=X;};e{};};\r\n \n");
        Assert.Equal(["b", "e"], root.Blocks.Select(b => b.Keyword));
        Assert.Equal("c", root.Blocks[0].Instance);
    }

    [Fact]
    public void UnknownKeywordsAndKeysAreReadByTheSameRules()
    {
        var root = Parse("anything x1 { _k9 = TRUE; inner { }; };");
        Assert.Equal("anything", root.Keyword);
        Assert.Equal("TRUE", root.GetValue("_k9")!.Text);
    }

    [Fact]
    public void DepthOf64IsAccepted()
    {
        Assert.NotNull(Parse(Nested(64)));
    }

    [Fact]
    public void DepthOf65IsRejected()
    {
        string text = Nested(65);
        Throws(text, 65 * 4, 1); // "b { " is 4 characters; the 65th nested keyword is too deep
    }

    // A root plus `depth` nested blocks: "b { b { ... }; };".
    private static string Nested(int depth) =>
        string.Concat(Enumerable.Repeat("b { ", depth + 1)) + string.Concat(Enumerable.Repeat("}; ", depth + 1));

    [Theory]
    [InlineData("template { a = \"x\\q\"; };", 17, 1)] // unknown escape
    [InlineData("template { a = \"x\\\\\"; };", 17, 1)] // \\ is not an escape
    [InlineData("template {\n a = \"x\ny\"; };", 18, 2)] // line break in a string
    [InlineData("template { a = \"x\ry\"; };", 17, 1)] // CR in a string
    [InlineData("template { a = \"x\u0001\"; };", 17, 1)] // control character in a string
    [InlineData("template { a = \"x", 15, 1)] // string not closed
    [InlineData("template { a = \"x\\", 15, 1)] // string not closed after a backslash
    [InlineData("template { a = 1 };", 17, 1)] // no ';' after a value
    [InlineData("template { a = 1; }", 19, 1)] // no ';' after '}'
    [InlineData("template { a = 1;", 17, 1)] // end of file inside a block
    [InlineData("template {\r\n b {\r\n", 18, 3)] // end of file inside a nested block
    [InlineData("template { }; x", 14, 1)] // a token after the root
    [InlineData("template { }; template { };", 14, 1)] // two roots
    [InlineData("template\t{ };", 8, 1)] // a tab
    [InlineData("template { a = é; };", 15, 1)] // a letter outside ASCII
    [InlineData("template { a = -x; };", 15, 1)] // minus without a digit
    [InlineData("template { a = 1.; };", 16, 1)] // decimal point without a digit
    [InlineData("template { a = ; };", 15, 1)] // no value
    [InlineData("template { a = (1 2); };", 18, 1)] // no comma
    [InlineData("template { a = (); };", 16, 1)] // empty tuple
    [InlineData("template { a = (1,", 18, 1)] // end of file inside a tuple
    [InlineData("template { 1 = 2; };", 11, 1)] // a key that is not an identifier
    [InlineData("template x y { };", 11, 1)] // three words before '{'
    [InlineData("template = 1;", 9, 1)] // the root is not a block
    [InlineData("", 0, 1)] // empty
    [InlineData(" \r\n ", 4, 2)] // only blanks
    [InlineData("{ };", 0, 1)] // no keyword
    public void MalformedTemplateIsRejectedWithOffsetAndLine(string text, int offset, int line) => Throws(text, offset, line);

    [Fact]
    public void NumberOutOfRangeIsRejected() => Throws("template { a = " + new string('9', 40) + "; };", 15, 1);

    [Fact]
    public void DeeplyNestedTuplesAreRejected()
    {
        string text = "template { a = " + new string('(', 66) + "1" + new string(')', 66) + "; };";
        Throws(text, 15 + 65, 1);
        Assert.NotNull(Parse("template { a = " + new string('(', 65) + "1" + new string(')', 65) + "; };"));
    }

    [Fact]
    public void DocumentReportsTheEncoding()
    {
        var ascii = TemplateDocument.Create(Synthetic.Ascii(Synthetic.Template), null);
        Assert.Equal(TdbFileKind.Template, ascii.Kind);
        Assert.Equal("ascii", ascii.EncodingName);
        Assert.Equal("template", ascii.Root.Keyword);
        Assert.Empty(ascii.Warnings);

        byte[] utf8 = Synthetic.Concat(Synthetic.Ascii("template { a = \""), [0xC2, 0xA0], Synthetic.Ascii("\"; };"));
        var u = TemplateDocument.Create(utf8, null);
        Assert.Equal("utf-8", u.EncodingName);
        Assert.Equal("\u00A0", u.Root.GetValue("a")!.Text);

        byte[] single = Synthetic.Concat(Synthetic.Ascii("template { a = \"[m"), [0xB2], Synthetic.Ascii("]\"; };"));
        var s = TemplateDocument.Create(single, null);
        Assert.Equal("cp1252", s.EncodingName);
        Assert.Equal("[m\u00B2]", s.Root.GetValue("a")!.Text);

        var forced = TemplateDocument.Create(single, Encoding.Latin1);
        Assert.Equal(Encoding.Latin1.WebName, forced.EncodingName);
    }

    [Fact]
    public void ByteOrderMarkIsDroppedBeforeParsing()
    {
        var doc = TemplateDocument.Create(Synthetic.Concat([0xEF, 0xBB, 0xBF], Synthetic.Ascii("template { };")), null);
        Assert.Equal("utf-8", doc.EncodingName);
    }

    [Fact]
    public void Utf16IsNotSupported() =>
        Assert.Throws<NotSupportedException>(() => TemplateDocument.Create([0xFF, 0xFE, (byte)'t', 0], null));

    [Fact]
    public void TemplateOverTheSizeLimitIsRejected()
    {
        var e = Assert.Throws<InvalidDataException>(() => TemplateDocument.Create(new byte[16 * 1024 * 1024 + 1], null));
        Assert.Contains("16 MiB", e.Message);
    }
}
