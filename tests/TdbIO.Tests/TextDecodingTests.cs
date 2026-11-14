using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class TextDecodingTests
{
    private static (string Text, string Name) Decode(byte[] bytes, Encoding? forced = null)
    {
        string text = TextDecoding.Decode(bytes, forced, out string name);
        return (text, name);
    }

    [Fact]
    public void AByteOrderMarkGivesUtf8WithoutTheMark() =>
        Assert.Equal(("a b", "utf-8"), Decode([0xEF, 0xBB, 0xBF, (byte)'a', 0xC2, 0xA0, (byte)'b']));

    [Fact]
    public void InvalidUtf8AfterAByteOrderMarkIsRejectedAtItsOffset()
    {
        var e = Assert.Throws<InvalidDataException>(() => Decode([0xEF, 0xBB, 0xBF, (byte)'a', (byte)'b', 0xB2]));
        Assert.Equal(5, e.Offset);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, (byte)'a', 0 })]
    [InlineData(new byte[] { 0xFE, 0xFF, 0, (byte)'a' })]
    public void Utf16IsNotSupported(byte[] bytes) => Assert.Throws<NotSupportedException>(() => Decode(bytes));

    [Fact]
    public void SevenBitTextIsAscii() => Assert.Equal(("template\r\n", "ascii"), Decode(Synthetic.Ascii("template\r\n")));

    [Fact]
    public void EmptyTextIsAscii() => Assert.Equal(("", "ascii"), Decode([]));

    [Fact]
    public void ValidUtf8IsUtf8() => Assert.Equal(("x y", "utf-8"), Decode([(byte)'x', 0xC2, 0xA0, (byte)'y']));

    [Fact]
    public void MostlyUpperHighBytesAreCodePage1251()
    {
        // Two letters of the upper half and one 0xB2: the 0xB2 is decoded as the code page says, not changed to fit.
        var (text, name) = Decode([0xC0, 0xC1, (byte)'x', 0xB2]);
        Assert.Equal("cp1251", name);
        Assert.Equal("АБxІ", text);
    }

    [Fact]
    public void MostlyLowerHighBytesAreCodePage1252()
    {
        var (text, name) = Decode([(byte)'m', 0xB2, (byte)' ', (byte)'m', 0xB3]);
        Assert.Equal("cp1252", name);
        Assert.Equal("m² m³", text);
    }

    [Fact]
    public void HalfUpperHighBytesAreCodePage1251() => Assert.Equal("cp1251", Decode([0xC0, 0xB2]).Name);

    [Fact]
    public void AForcedEncodingWins()
    {
        var (text, name) = Decode([(byte)'m', 0xB2], Encoding.Latin1);
        Assert.Equal("m²", text);
        Assert.Equal(Encoding.Latin1.WebName, name);
        Assert.Equal("utf-8", Decode(Synthetic.Ascii("abc"), Encoding.UTF8).Name);
    }

    [Fact]
    public void AForcedEncodingDropsItsOwnByteOrderMark() =>
        Assert.Equal(("ab", "utf-8"), Decode([0xEF, 0xBB, 0xBF, (byte)'a', (byte)'b'], Encoding.UTF8));

    [Fact]
    public void BytesAStrictForcedEncodingRejectsGiveAFormatError()
    {
        var ascii = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var e = Assert.Throws<InvalidDataException>(() => Decode([(byte)'a', (byte)'b', 0xC0], ascii));
        Assert.Equal(2, e.Offset);
    }

    [Fact]
    public void AByteTheCodePageDoesNotDefineIsRejectedAtItsOffset()
    {
        // 0x98 is not defined in code page 1251; 0xC0 makes that page the choice.
        var e = Assert.Throws<InvalidDataException>(() => Decode([(byte)'a', 0xC0, (byte)'b', 0x98]));
        Assert.Equal(3, e.Offset);
        Assert.Contains("cp1251", e.Message);
    }

    [Theory]
    [InlineData(0x81)]
    [InlineData(0x8D)]
    [InlineData(0x8F)]
    [InlineData(0x90)]
    [InlineData(0x9D)]
    public void TheGapsOfCodePage1252AreRejected(byte gap)
    {
        var e = Assert.Throws<InvalidDataException>(() => Decode([(byte)'m', 0xB2, gap]));
        Assert.Equal(2, e.Offset);
        Assert.Contains("cp1252", e.Message);
    }
}
