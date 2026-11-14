using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class XsteelHeaderTests
{
    [Fact]
    public void ReadsTypeVersionAndGuid()
    {
        var (type, version, guid) = XsteelHeader.Read(Synthetic.XsteelHead(0x80, "1.23"));
        Assert.Equal(0x80, type);
        Assert.Equal("1.23", version);
        Assert.Equal(Guid.Parse(Synthetic.ContainerGuidText), guid);
    }

    [Theory]
    [InlineData(0, (byte)'x', 0)]  // magic
    [InlineData(7, (byte)'_', 7)]  // space after the type
    [InlineData(9, (byte)',', 8)]  // version pattern
    [InlineData(10, (byte)'a', 8)] // version digit
    [InlineData(12, (byte)'_', 12)] // space after the version
    [InlineData(13, (byte)'A', 13)] // upper-case GUID
    [InlineData(21, (byte)'0', 13)] // GUID dash
    public void BadByteThrowsAtItsField(int index, byte value, long offset)
    {
        var head = Synthetic.XsteelHead();
        head[index] = value;
        var e = Assert.Throws<InvalidDataException>(() => XsteelHeader.Read(head));
        Assert.Equal(offset, e.Offset);
    }

    [Fact]
    public void ShortGuidThrows()
    {
        var head = Synthetic.XsteelHead(guid: Synthetic.ContainerGuidText[..35] + " ");
        Assert.Equal(13, Assert.Throws<InvalidDataException>(() => XsteelHeader.Read(head)).Offset);
    }

    [Fact]
    public void CutHeaderThrows()
    {
        var e = Assert.Throws<InvalidDataException>(() => XsteelHeader.Read(Synthetic.XsteelHead()[..48]));
        Assert.Contains("runs past the end", e.Message);
        Assert.Equal(0, Assert.Throws<InvalidDataException>(() => XsteelHeader.Read("Xste"u8)).Offset);
    }

    [Fact]
    public void DrawingIsNotSupportedEvenWhenShort()
    {
        Assert.Throws<NotSupportedException>(() => XsteelHeader.Read(Synthetic.Concat(Synthetic.Ascii("Xsteel"), [0x20], Synthetic.Ascii(" 1.00/"))));
        Assert.Throws<NotSupportedException>(() => Db1Document.Create(Synthetic.XsteelHead(0x20)));
    }

    [Fact]
    public void UnknownTypeNamesItsValue()
    {
        var e = Assert.Throws<InvalidDataException>(() => XsteelHeader.Read(Synthetic.XsteelHead(0x99)));
        Assert.Equal(6, e.Offset);
        Assert.Contains("0x99", e.Message);
    }

    [Fact]
    public void DocumentsCheckTheType()
    {
        Assert.Equal(6, Assert.Throws<InvalidDataException>(() => Db1Document.Create(Synthetic.XsteelHead(0x80))).Offset);
        Assert.Equal(6, Assert.Throws<InvalidDataException>(() => Db2Document.Create(Synthetic.Db1Payload([]))).Offset);
    }
}
