using Xunit;

namespace TdbIO.Tests;

public class HistoryTests
{
    [Fact]
    public void ReadsBigEndianPageFields()
    {
        var document = HistoryDocument.Create(Synthetic.SqliteHead(0x1000, 3), 3 * 0x1000);
        Assert.Equal(0x1000, document.PageSize);
        Assert.Equal(3u, document.PageCount);
        Assert.Equal(3 * 0x1000, document.Length);
        Assert.Empty(document.Warnings);
        Assert.Equal(TdbFileKind.History, document.Kind);
    }

    [Fact]
    public void LengthMismatchWarns()
    {
        var document = HistoryDocument.Create(Synthetic.SqliteHead(0x200, 0x01000002), 100);
        Assert.Equal(0x200, document.PageSize);
        Assert.Equal(0x01000002u, document.PageCount);
        Assert.Single(document.Warnings);
    }

    [Fact]
    public void ShortOrWrongHeadThrows()
    {
        Assert.Throws<InvalidDataException>(() => HistoryDocument.Create(Synthetic.SqliteHead(1, 1).AsSpan(0, 31), 31));
        var wrong = Synthetic.SqliteHead(1, 1);
        wrong[0] = (byte)'s';
        Assert.Equal(0, Assert.Throws<InvalidDataException>(() => HistoryDocument.Create(wrong, 100)).Offset);
    }
}
