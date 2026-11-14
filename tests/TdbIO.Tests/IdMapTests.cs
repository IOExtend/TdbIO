using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class IdMapTests
{
    private static readonly Guid A = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid B = Guid.Parse("66666666-7777-4888-9999-aaaaaaaaaaaa");
    private static readonly Guid C = Guid.Parse("bbbbbbbb-cccc-4ddd-aeee-ffffffffffff");

    private static byte[] Db1(byte[] idMap) => Synthetic.Concat(Synthetic.XsteelHead(), idMap);

    [Fact]
    public void CountOneGivesAnEmptyMap()
    {
        var document = Db1Document.Create(Synthetic.Db1Payload([]));
        Assert.Empty(document.IdMap!);
        Assert.Equal(0x0Du, document.IdMapField65);
        Assert.Equal(1u, document.IdMapField73);
        Assert.Equal("9.99", document.Version);
        Assert.Equal(Guid.Parse(Synthetic.ContainerGuidText), document.ModelGuid);
        Assert.Empty(document.Warnings);
        Assert.Equal(TdbFileKind.Db1, document.Kind);
        Assert.Equal(77 + 4, document.Payload.Length);
    }

    [Fact]
    public void FileOrderIsKept()
    {
        var map = IdMapReader.Read(Db1(Synthetic.IdMapBody([(2, A, 7), (3, B, -1), (1, C, 9)])), [])!;
        Assert.Equal([2u, 3u, 1u], map.Entries.Select(e => e.Sequence));
        Assert.Equal(new IdMapEntry(3, B, -1), map.Entries[1]);
        Assert.Equal(77 + 3 * 24, map.End);
    }

    [Theory]
    [InlineData(1u, 1)] // repeated
    [InlineData(0u, 1)] // zero
    [InlineData(4u, 1)] // above N - 1
    public void BadSequenceThrowsAtTheEntry(uint second, int index)
    {
        var bytes = Db1(Synthetic.IdMapBody([(1, A, 1), (second, B, 1), (3, C, 1)]));
        var e = Assert.Throws<InvalidDataException>(() => IdMapReader.Read(bytes, []));
        Assert.Equal(77 + 24 * index, e.Offset);
    }

    [Fact]
    public void ValueAfterTheMapMustBeFive()
    {
        var bytes = Db1(Synthetic.IdMapBody([(1, A, 1)], after: 6));
        Assert.Equal(77 + 24, Assert.Throws<InvalidDataException>(() => IdMapReader.Read(bytes, [])).Offset);
    }

    [Theory]
    [InlineData(3u)]          // one entry more than present
    [InlineData(0xFFFFFFFFu)] // would need more than 64 GiB
    [InlineData(0u)]
    public void CountPastTheEndThrows(uint count)
    {
        var bytes = Db1(Synthetic.IdMapBody([(1, A, 1)], count: count));
        Assert.Equal(69, Assert.Throws<InvalidDataException>(() => IdMapReader.Read(bytes, [])).Offset);
    }

    [Fact]
    public void ShortBodyThrows() =>
        Assert.Throws<InvalidDataException>(() => Db1Document.Create(Synthetic.XsteelHead()));

    [Fact]
    public void WrongMarkerThrows()
    {
        var bytes = Db1(Synthetic.IdMapBody([], marker: 0xDBCEC0BD));
        Assert.Equal(53, Assert.Throws<InvalidDataException>(() => IdMapReader.Read(bytes, [])).Offset);
    }

    [Fact]
    public void UnknownVariantGivesNoMapAndAWarning()
    {
        var document = Db1Document.Create(Db1(Synthetic.IdMapBody([], first: 2)));
        Assert.Null(document.IdMap);
        Assert.Null(document.IdMapField65);
        Assert.Null(document.IdMapField73);
        Assert.Contains(document.Warnings, w => w.Contains("variant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0x10Du, false)]
    [InlineData(0x0Du, false)]
    [InlineData(0x0Eu, true)]
    public void Field65WarnsOnlyOnUnknownValues(uint value, bool warns)
    {
        var document = Db1Document.Create(Db1(Synthetic.IdMapBody([], field65: value)));
        Assert.Equal(warns, document.Warnings.Count == 1);
        Assert.Equal(value, document.IdMapField65);
    }

    [Fact]
    public void GuidIsReadWithLittleEndianFields()
    {
        byte[] stored = [0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
        var bytes = Db1(Synthetic.IdMapBody([(1, new Guid(stored), 1)]));
        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", IdMapReader.Read(bytes, [])!.Entries[0].Id.ToString());
        Assert.True(bytes.AsSpan(77 + 4, 16).SequenceEqual(stored));
    }
}
