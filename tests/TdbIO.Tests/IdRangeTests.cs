using Xunit;

namespace TdbIO.Tests;

public class IdRangeTests
{
    private static readonly Guid A = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid B = Guid.Parse("66666666-7777-4888-9999-aaaaaaaaaaaa");

    [Fact]
    public void ContiguousRangesGiveNoWarnings()
    {
        var document = IdRangeDocument.Create(Synthetic.IdRangeFile(130, (100, A, 0, 20), (120, B, 42, 10)));
        Assert.Equal(130u, document.Next);
        Assert.Equal([new IdRange(100, A, 0, 20), new IdRange(120, B, 42, 10)], document.Ranges);
        Assert.Empty(document.Warnings);
        Assert.Equal(TdbFileKind.IdRanges, document.Kind);
    }

    [Fact]
    public void NoRangesIsValid() => Assert.Empty(IdRangeDocument.Create(Synthetic.IdRangeFile(5)).Ranges);

    [Fact]
    public void GapAndNextMismatchWarn()
    {
        var gap = IdRangeDocument.Create(Synthetic.IdRangeFile(131, (100, A, 0, 20), (121, B, 0, 10)));
        Assert.Single(gap.Warnings);
        Assert.Contains("range 1", gap.Warnings[0]);

        var next = IdRangeDocument.Create(Synthetic.IdRangeFile(129, (100, A, 0, 20), (120, B, 0, 10)));
        Assert.Single(next.Warnings);

        // The sum is taken in 64 bits: no wrap-around makes a broken range look contiguous.
        var wrap = IdRangeDocument.Create(Synthetic.IdRangeFile(0, (0xFFFFFFFF, A, 0, 1)));
        Assert.Single(wrap.Warnings);
    }

    [Fact]
    public void MagicAndMarkerMustMatch()
    {
        var magic = Synthetic.IdRangeFile(0);
        magic[0] = (byte)'i';
        Assert.Equal(0, Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create(magic)).Offset);

        var marker = Synthetic.IdRangeFile(0);
        marker[17] ^= 1;
        Assert.Equal(17, Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create(marker)).Offset);
    }

    [Fact]
    public void LengthMustBeExact()
    {
        var file = Synthetic.IdRangeFile(30, (10, A, 0, 20));
        Assert.Equal(21, Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create(file[..^1])).Offset);
        Assert.Equal(21, Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create([.. file, 0])).Offset);

        Synthetic.Le32(0xFFFFFFFFu).CopyTo(file, 21);
        Assert.Equal(21, Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create(file)).Offset);
        Assert.Throws<InvalidDataException>(() => IdRangeDocument.Create(file[..20]));
    }
}
