using System.Text;
using Xunit;

namespace TdbIO.Tests;

public class Nc1Tests
{
    private static Nc1Document Read(byte[] data) => Nc1Document.Create(data);

    private static void Throws(byte[] data, long offset, int line)
    {
        var e = Assert.Throws<InvalidDataException>(() => Read(data));
        Assert.Equal(offset, e.Offset);
        Assert.EndsWith($" line {line}", e.Message);
    }

    // Offset of the start of a 1-based line in a file built from these lines.
    private static long LineOffset(IReadOnlyList<string> lines, int line) => lines.Take(line - 1).Sum(l => l.Length + 2);

    [Fact]
    public void MinimalFileIsRead()
    {
        var doc = Read(Synthetic.Nc1File(Synthetic.Nc1Rectangle()));

        Assert.Equal(TdbFileKind.Nc1, doc.Kind);
        Assert.Equal("part.nc1", doc.Comment);
        Assert.Equal(Synthetic.Nc1Fields, doc.Fields);
        Assert.Equal("A1", doc.Mark);
        Assert.Equal("GRADE-X", doc.Material);
        Assert.Equal(3, doc.Quantity);
        Assert.Equal("PL12", doc.Profile);
        Assert.Equal(100m, doc.Length);
        Assert.Equal(["ST", "AK", "EN"], doc.Blocks.Select(b => b.Code));
        Assert.Equal([1, 27, 33], doc.Blocks.Select(b => b.LineNumber));
        Assert.Empty(doc.Warnings);

        var ak = doc.Blocks[1];
        Assert.Equal(5, ak.Lines.Count);
        Assert.Equal('v', ak.Lines[0].Label);
        Assert.All(ak.Lines.Skip(1), l => Assert.Null(l.Label));
        Assert.All(ak.Lines, l => Assert.Equal(7, l.Values.Count));
        Assert.Equal(new Nc1Token("0u", 0m, 'u'), ak.Lines[0].Values[0]);
        Assert.Equal(new Nc1Token("50", 50m, null), ak.Lines[2].Values[1]);
        Assert.Empty(doc.Blocks[0].Lines);
        Assert.Equal(25, doc.Blocks[0].RawLines.Count);
        Assert.Empty(doc.Blocks[2].RawLines);
    }

    [Fact]
    public void BoLinesAreRead()
    {
        var doc = Read(Synthetic.Nc1File(Synthetic.Nc1Rectangle(), ["BO", "  v     20s     25      9", "  v     80s     25      9"]));
        var bo = doc.Blocks[2];
        Assert.Equal("BO", bo.Code);
        Assert.Equal(2, bo.Lines.Count);
        Assert.All(bo.Lines, l => Assert.Equal('v', l.Label));
        Assert.Equal(["80s", "25", "9"], bo.Lines[1].Values.Select(v => v.Text));
        Assert.Equal(new Nc1Token("80s", 80m, 's'), bo.Lines[1].Values[0]);
    }

    [Fact]
    public void MinusZeroAndSuffixesAreKeptAsWritten()
    {
        string[] ak =
        [
            "AK",
            "  o      0s      0       0       0       0       0       0",
            "        100      0   12345      -0       6       0       0",
            "        100      0w      0       0       0       0       0",
            "          0      0       0       0       0       0       0",
        ];
        var doc = Read(Synthetic.Nc1File(ak, ["BO", "  u     10o     20      7", "  u     10u     30      7"]));
        var values = doc.Blocks[1].Lines;
        Assert.Equal(new Nc1Token("-0", 0m, null), values[1].Values[3]);
        Assert.Equal(new Nc1Token("0w", 0m, 'w'), values[2].Values[1]);
        Assert.Equal(new Nc1Token("0s", 0m, 's'), values[0].Values[0]);
        Assert.Equal(['o', 'u'], doc.Blocks[2].Lines.Select(l => l.Values[0].Suffix!.Value));
    }

    [Fact]
    public void DecimalNumbersAreAccepted()
    {
        string[] ak = [.. Synthetic.Nc1Rectangle()];
        ak[2] = "        100.5    0       0       0       0       0       0";
        var doc = Read(Synthetic.Nc1File(ak));
        Assert.Equal(100.5m, doc.Blocks[1].Lines[1].Values[0].Number);
    }

    [Fact]
    public void UnknownCodeIsKeptRaw()
    {
        var doc = Read(Synthetic.Nc1File(Synthetic.Nc1Rectangle(), ["XY", "  anything 1 2 3", "", "zz"]));
        var block = doc.Blocks[2];
        Assert.Equal("XY", block.Code);
        Assert.Equal(["  anything 1 2 3", "", "zz"], block.RawLines);
        Assert.Empty(block.Lines);
    }

    [Fact]
    public void RawLinesGiveTheFileBack()
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle(), ["BO", "  v     20s     25      9"]);
        var doc = Read(data);
        string rebuilt = string.Concat(doc.Blocks.SelectMany(b => b.RawLines.Prepend(b.Code)).Select(l => l + "\r\n"));
        Assert.Equal(data, Encoding.ASCII.GetBytes(rebuilt));
    }

    [Fact]
    public void QuantityThatIsNotAnIntegerIsNull()
    {
        string[] fields = [.. Synthetic.Nc1Fields];
        fields[5] = "many";
        var doc = Read(Synthetic.Nc1([.. Synthetic.Nc1Header(fields), .. Synthetic.Nc1Rectangle(), "EN"]));
        Assert.Null(doc.Quantity);
    }

    [Fact]
    public void OpenContourGivesAWarning()
    {
        string[] ak = [.. Synthetic.Nc1Rectangle()];
        ak[^1] = "          0      1       0       0       0       0       0";
        var doc = Read(Synthetic.Nc1File(ak));
        string warning = Assert.Single(doc.Warnings);
        Assert.Contains("line 27", warning);
    }

    [Fact]
    public void SuffixesDoNotOpenAContour()
    {
        string[] ak = [.. Synthetic.Nc1Rectangle()];
        ak[^1] = "          0s     0w      0       0       0       0       0";
        Assert.Empty(Read(Synthetic.Nc1File(ak)).Warnings);
    }

    [Fact]
    public void NonAsciiByteIsRejectedAtItsOffset()
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle());
        data[4] = 0xB2; // inside the comment on line 2
        Throws(data, 4, 2);
    }

    [Theory]
    [InlineData((byte)'\t')]
    [InlineData((byte)0)]
    [InlineData((byte)0x7F)]
    public void ControlByteIsRejected(byte control)
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle());
        data[5] = control;
        Throws(data, 5, 2);
    }

    [Fact]
    public void LoneLineFeedIsRejected()
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle());
        int crOfLine2 = Array.IndexOf(data, (byte)'\r', 4);
        data[crOfLine2] = (byte)' ';
        Throws(data, crOfLine2 + 1, 2);
    }

    [Fact]
    public void LoneCarriageReturnIsRejected()
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle());
        int lfOfLine2 = Array.IndexOf(data, (byte)'\n', 4);
        data[lfOfLine2] = (byte)' ';
        Throws(data, lfOfLine2 - 1, 2);
    }

    [Fact]
    public void MissingFinalLineBreakIsRejected()
    {
        byte[] data = Synthetic.Nc1File(Synthetic.Nc1Rectangle());
        Throws(data[..^2], data.Length - 4, 33);
        Throws(data[..^1], data.Length - 2, 33); // the CR has no LF after it
    }

    [Fact]
    public void LineOneMustBeSt()
    {
        var lines = Synthetic.Nc1Header();
        lines[0] = "SX";
        Throws(Synthetic.Nc1([.. lines, .. Synthetic.Nc1Rectangle(), "EN"]), 0, 1);
        Throws([], 0, 1);
        Throws(Synthetic.Ascii("ST"), 0, 1); // not even one whole line: cut
    }

    [Fact]
    public void DataAfterEnIsRejected()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), .. Synthetic.Nc1Rectangle(), "EN", "  x"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 34), 34);
    }

    [Fact]
    public void EnMustBeTheLastBlock()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), "EN", .. Synthetic.Nc1Rectangle()];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 28), 28);

        List<string> cut = [.. Synthetic.Nc1Header(), .. Synthetic.Nc1Rectangle()];
        byte[] data = Synthetic.Nc1(cut);
        Throws(data, data.Length, 33);
    }

    [Fact]
    public void SecondStBlockIsRejected()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), .. Synthetic.Nc1Rectangle(), "ST", "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 33), 33);
    }

    [Theory]
    [InlineData(1, "*  part.nc1")] // line 2 must start with "** "
    [InlineData(2, "")] // field 1 empty
    [InlineData(4, "x")] // field 3 without the two spaces
    [InlineData(10, "          ")] // field 9 blank
    [InlineData(10, "      12ab")] // field 9 not a number
    [InlineData(21, "      1.5.")] // field 20 not a number
    [InlineData(21, "       -.5")] // field 20 not a number
    [InlineData(22, "  x")] // line 23 must be empty
    [InlineData(25, " ")] // line 26 must be empty
    public void BadStLayoutIsRejected(int index, string replacement)
    {
        var lines = Synthetic.Nc1Header();
        lines[index] = replacement;
        List<string> all = [.. lines, .. Synthetic.Nc1Rectangle(), "EN"];
        Throws(Synthetic.Nc1(all), LineOffset(all, index + 1), index + 1);
    }

    [Fact]
    public void StMustBeFollowedByABlockCode()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), "  x", .. Synthetic.Nc1Rectangle(), "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 27), 27);
    }

    [Fact]
    public void FileThatEndsInsideStIsRejected()
    {
        var lines = Synthetic.Nc1Header().Take(10).ToList();
        byte[] data = Synthetic.Nc1(lines);
        Throws(data, data.Length, 11);
    }

    [Fact]
    public void NumberOutOfRangeIsRejected()
    {
        string[] ak = [.. Synthetic.Nc1Rectangle()];
        ak[2] = "        " + new string('9', 40) + "      0       0       0       0       0       0";
        List<string> lines = [.. Synthetic.Nc1Header(), .. ak, "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 29), 29);
    }

    [Theory]
    [InlineData(1, "  v      0u      0       0       0       0       0")] // 6 numbers
    [InlineData(1, "         0u      0       0       0       0       0       0")] // no label letter
    [InlineData(2, "        100      0       0       0       0       0")] // 6 numbers
    [InlineData(2, "  o    100      0       0       0       0       0       0")] // label letter on a later line
    [InlineData(2, "        100      0       0       0       0       0       x")] // not a number
    [InlineData(2, "        100      0       0       0       0       0      1ab")] // two letters
    [InlineData(2, "        100      -       0       0       0       0       0")] // lone minus
    public void BadAkLineIsRejected(int index, string replacement)
    {
        string[] ak = [.. Synthetic.Nc1Rectangle()];
        ak[index] = replacement;
        List<string> lines = [.. Synthetic.Nc1Header(), .. ak, "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 27 + index), 27 + index);
    }

    [Fact]
    public void EmptyAkBlockIsRejected()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), "AK", "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 27), 27);
    }

    [Theory]
    [InlineData("  v     20s     25       9      1")] // 5 tokens
    [InlineData("  v     20s     25")] // 3 tokens
    [InlineData("        20s     25       9      1")] // no label letter
    [InlineData("  vv    20s     25       9")] // label of two letters
    public void BadBoLineIsRejected(string line)
    {
        List<string> lines = [.. Synthetic.Nc1Header(), .. Synthetic.Nc1Rectangle(), "BO", line, "EN"];
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 34), 34);
    }

    [Fact]
    public void OverlongLineIsRejected()
    {
        List<string> lines = [.. Synthetic.Nc1Header(), .. Synthetic.Nc1Rectangle(), "XY", new string('a', 4096), "EN"];
        Assert.Equal(4096, Read(Synthetic.Nc1(lines)).Blocks[2].RawLines[0].Length);

        lines[^2] = new string('a', 4097);
        Throws(Synthetic.Nc1(lines), LineOffset(lines, 34), 34);
    }

    [Fact]
    public void FileOverTheSizeLimitIsRejected()
    {
        var e = Assert.Throws<InvalidDataException>(() => Read(new byte[16 * 1024 * 1024 + 1]));
        Assert.Contains("16 MiB", e.Message);
    }

    [Fact]
    public void MalformedBoBlockWithManyLinesRejectsEarlyWithoutLargeAllocation()
    {
        var lines = new List<string>(Synthetic.Nc1Header());
        lines.AddRange(Synthetic.Nc1Rectangle());
        lines.Add("BO");
        for (int i = 0; i < 50_000; i++) lines.Add("");
        lines.Add("EN");
        byte[] data = Synthetic.Nc1(lines);

        long before = GC.GetAllocatedBytesForCurrentThread();
        var e = Assert.Throws<InvalidDataException>(() => Read(data));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("a BO line must hold a label letter and 3 numbers", e.Message);
        Assert.True(allocated < 500_000, $"Allocated {allocated} bytes, expected < 500 KB");
    }
}
