using System.Text;
using Xunit;

namespace TdbIO.Tests;

public class GuidMapperTests
{
    private const string G1 = "11111111-2222-4333-8444-555555555555";
    private const string G2 = "66666666-7777-4888-9999-aaaaaaaaaaaa";
    private const string G3 = "bbbbbbbb-cccc-4ddd-aeee-ffffffffffff";
    private const int HeaderLength = 29; // "!Guid mapping for sample.db1\n"

    [Fact]
    public void ReadsHeaderAndPairs()
    {
        var document = GuidMapperDocument.Create(Synthetic.GuidMapperText($"{G1} {G2}", $"{G2} {G1}", $"{G3} {G3}"));
        Assert.Equal("!Guid mapping for sample.db1", Encoding.ASCII.GetString(document.HeaderLine.Span));
        Assert.Equal(3, document.Pairs.Count);
        Assert.Equal(new GuidPair(Guid.Parse(G2), Guid.Parse(G1)), document.Pairs[1]);
        Assert.Equal(TdbFileKind.GuidMapper, document.Kind);
    }

    [Fact]
    public void HeaderOnlyHasNoPairs() => Assert.Empty(GuidMapperDocument.Create(Synthetic.GuidMapperText()).Pairs);

    [Theory]
    [InlineData(G1 + " " + G2 + "\r", 2)]
    [InlineData(G1 + "  " + G2, 2)]
    [InlineData(G1 + " " + G2 + " ", 2)]
    [InlineData("11111111-2222-4333-8444-55555555555A " + G2, 2)]
    [InlineData("{11111111-2222-4333-8444-5555555555} " + G2, 2)]
    [InlineData("!Guid mapping for other.db1", 2)]
    public void BadSecondLineThrowsWithItsLineNumber(string line, int number)
    {
        var e = Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(Synthetic.GuidMapperText(line)));
        Assert.EndsWith($" line {number}", e.Message);
        Assert.Equal(HeaderLength, e.Offset);
    }

    [Fact]
    public void RepeatedFirstGuidThrows()
    {
        var e = Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(Synthetic.GuidMapperText($"{G1} {G2}", $"{G1} {G3}")));
        Assert.EndsWith(" line 3", e.Message);
        Assert.Equal(HeaderLength + 74, e.Offset);
    }

    [Fact]
    public void MissingFinalLineFeedThrows()
    {
        var bytes = Synthetic.GuidMapperText($"{G1} {G2}", $"{G2} {G3}")[..^1];
        Assert.EndsWith(" line 3", Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(bytes)).Message);
        Assert.EndsWith(" line 1", Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(Synthetic.Ascii("!Guid mapping"))).Message);
    }

    [Fact]
    public void HeaderMustStartWithExclamationAndEndWithLineFeedOnly()
    {
        Assert.EndsWith(" line 1", Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(Synthetic.Ascii("Guid mapping\n"))).Message);
        Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create([]));
        Assert.EndsWith(" line 1", Assert.Throws<InvalidDataException>(() => GuidMapperDocument.Create(Synthetic.Ascii("!Guid mapping\r\n"))).Message);
    }
}
