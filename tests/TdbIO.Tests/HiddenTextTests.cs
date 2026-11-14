using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class HiddenTextTests
{
    [Fact]
    public void ReversesTheWholeTextAndShiftsLettersOnly()
    {
        byte[] stored = [.. "Nop-12;"u8, 0x80, 0xFF, .. "zA"u8];
        byte[] revealed = HiddenText.Reveal(stored);
        Assert.Equal([.. "Nm"u8, 0xFF, 0x80, .. ";21-cbA"u8], revealed);
    }

    [Fact]
    public void WholeReversalDiffersFromPerRowReversal()
    {
        // A stamp longer than one row: reversing each 27-byte part on its own would put the date elsewhere.
        string revealed = "14.11.2026;09:08:07;" + new string('q', 20);
        var rows = Synthetic.ChainRows(1, 1, Synthetic.HiddenSaveText(revealed));
        var chain = TextSectionReader.ReadAt(Synthetic.TextSectionBytes(rows), 0).Chains[0];
        Assert.Equal(revealed, Encoding.ASCII.GetString(chain.RevealSaveText()));

        var perRow = chain.Text.ToArray().Chunk(27).SelectMany(part => HiddenText.Reveal(part)).ToArray();
        Assert.NotEqual(revealed, Encoding.ASCII.GetString(perRow.AsSpan(5)));
    }

    [Fact]
    public void PrefixIsKeptInTheText()
    {
        var chain = TextSectionReader.ReadAt(Synthetic.TextSectionBytes(Synthetic.ChainRows(1, 1, Synthetic.HiddenSaveText("01.02.2003;04:05:06"))), 0).Chains[0];
        Assert.True(chain.Text.Span.StartsWith("Save:"u8));
        Assert.Equal(new DateTime(2003, 2, 1, 4, 5, 6), chain.SavedAt);
    }

    [Theory]
    [InlineData("14.11.2026;09:08:07;x;y", true)]
    [InlineData("14.11.2026;09:08:07", true)]
    [InlineData("32.13.2026;09:08:07;x", false)]
    [InlineData("14.11.2026 09:08:07;x", false)]
    [InlineData("14-11-2026;09:08:07;x", false)]
    [InlineData("", false)]
    public void SavedAtParsesFieldsOneAndTwo(string revealed, bool parses) =>
        Assert.Equal(parses, HiddenText.ParseSavedAt(Encoding.ASCII.GetBytes(revealed)).HasValue);

    [Fact]
    public void BadDateGivesNullNotAnError()
    {
        var chain = TextSectionReader.ReadAt(Synthetic.TextSectionBytes(Synthetic.ChainRows(1, 1, Synthetic.HiddenSaveText("no date here"))), 0).Chains[0];
        Assert.True(chain.IsSaveStamp);
        Assert.Null(chain.SavedAt);
    }

    [Fact]
    public void RevealOnOpenTextThrows()
    {
        var chain = TextSectionReader.ReadAt(Synthetic.TextSectionBytes(Synthetic.ChainRows(1, 1, Synthetic.Ascii("Saved later"))), 0).Chains[0];
        Assert.False(chain.IsSaveStamp);
        Assert.Throws<InvalidOperationException>(() => chain.RevealSaveText());
    }
}
