using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class ReportTests
{
    [Fact]
    public void FormFeedStartsAPageAndIsRemoved()
    {
        var (lines, pages, ends) = ReportReader.Read("    head\r\n    row\r\n\f    head\r\n    row");
        Assert.Equal(["    head", "    row", "    head", "    row"], lines);
        Assert.Equal([2], pages);
        Assert.False(ends);
    }

    [Fact]
    public void FinalLineBreakGivesNoEmptyLine()
    {
        var (lines, pages, ends) = ReportReader.Read("a\r\n\r\nb\r\n");
        Assert.Equal(["a", "", "b"], lines);
        Assert.Empty(pages);
        Assert.True(ends);
    }

    [Theory]
    [InlineData("", 0, false)]
    [InlineData("\r\n", 1, true)]
    [InlineData("x", 1, false)]
    public void SmallInputs(string text, int count, bool ends)
    {
        var (lines, _, endsWithLineBreak) = ReportReader.Read(text);
        Assert.Equal(count, lines.Count);
        Assert.Equal(ends, endsWithLineBreak);
    }

    [Fact]
    public void LoneCarriageReturnAndLineFeedStayAsText()
    {
        var (lines, _, _) = ReportReader.Read("a\nb\rc\r\nd");
        Assert.Equal(["a\nb\rc", "d"], lines);
    }

    [Fact]
    public void OnlyOneLeadingFormFeedIsRemoved()
    {
        var (lines, pages, _) = ReportReader.Read("\f\fx\r\na\fb");
        Assert.Equal(["\fx", "a\fb"], lines);
        Assert.Equal([0], pages);
    }

    [Fact]
    public void OneCodePageIsUsedForTheWholeFileAndBytesAreNotChanged()
    {
        // Mostly upper-half letters, so code page 1251; 0xB2 is read as that page says (U+0406), not fixed.
        byte[] data = Synthetic.Concat(Synthetic.Ascii("    "), [0xC0, 0xC1, 0xC2], Synthetic.Ascii(" x"), [0xB2],
            Synthetic.Ascii("\r\n\f    x"));
        var doc = ReportDocument.Create(data, null);
        Assert.Equal(TdbFileKind.Report, doc.Kind);
        Assert.Equal("cp1251", doc.EncodingName);
        Assert.Equal(["    \u0410\u0411\u0412 x\u0406", "    x"], doc.Lines);
        Assert.Equal([1], doc.PageStarts);
        Assert.False(doc.EndsWithLineBreak);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void AsciiReport() => Assert.Equal("ascii", ReportDocument.Create(Synthetic.Ascii("    a\r\n"), null).EncodingName);

    [Fact]
    public void Utf16IsNotSupported() =>
        Assert.Throws<NotSupportedException>(() => ReportDocument.Create([0xFE, 0xFF, 0, (byte)'a'], null));

    [Fact]
    public void ReportOverTheSizeLimitIsRejected()
    {
        var e = Assert.Throws<InvalidDataException>(() => ReportDocument.Create(new byte[16 * 1024 * 1024 + 1], null));
        Assert.Contains("16 MiB", e.Message);
    }
}
