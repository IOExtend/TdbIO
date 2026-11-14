using Xunit;

namespace TdbIO.Tests;

public class Db2Tests
{
    private static byte[] Db2(params byte[][] records) => Synthetic.Concat(Synthetic.XsteelHead(0x80), Synthetic.Concat(records));

    [Fact]
    public void HeaderOnlyHasNoRecords()
    {
        var document = Db2Document.Create(Synthetic.XsteelHead(0x80, "2.50"));
        Assert.Empty(document.Records);
        Assert.Equal("2.50", document.Version);
        Assert.Equal(TdbFileKind.Db2, document.Kind);
        Assert.Equal(49, document.Payload.Length);
    }

    [Fact]
    public void RecordsAndRowsRoundTrip()
    {
        byte[] rows = [4, 1, 2, 3, 0x0C, 4, 5, 6];
        var document = Db2Document.Create(Db2(Synthetic.Db2Record(7, 0, 12, []), Synthetic.Db2Record(9, 2, 3, rows)));
        Assert.Equal(2, document.Records.Count);
        var (offset, key, count, rowSize, data) = document.Records[1];
        Assert.Equal(49 + 16, offset);
        Assert.Equal((9u, 2u, 3u), (key, count, rowSize));
        Assert.Equal(rows, data.ToArray());
        Assert.Equal(0, document.Records[0].Rows.Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void ShortRemainderThrows(int left)
    {
        var bytes = Db2(Synthetic.Db2Record(1, 0, 4, []), new byte[left]);
        Assert.Equal(49 + 16, Assert.Throws<InvalidDataException>(() => Db2Document.Create(bytes)).Offset);
    }

    [Theory]
    [InlineData(0xFFFFFFFFu, 0xFFFFFFFFu)]
    [InlineData(1u, 0xFFFFFFFFu)]
    [InlineData(3u, 1u)]
    public void RowsPastTheEndThrow(uint count, uint rowSize)
    {
        var bytes = Db2(Synthetic.Db2Record(1, count, rowSize, [4, 0, 4, 0]));
        Assert.Equal(49, Assert.Throws<InvalidDataException>(() => Db2Document.Create(bytes)).Offset);
    }

    [Fact]
    public void BadTerminatorThrows()
    {
        var bytes = Db2(Synthetic.Db2Record(1, 1, 2, [4, 9, 9], terminator: 0x00BC6150));
        Assert.Equal(49 + 12 + 3, Assert.Throws<InvalidDataException>(() => Db2Document.Create(bytes)).Offset);
    }
}
