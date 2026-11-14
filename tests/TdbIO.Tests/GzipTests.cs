using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class GzipTests
{
    private const long Limit = 1L << 30;

    private static readonly byte[] Payload = Synthetic.Ascii(string.Concat(Enumerable.Repeat("synthetic payload ", 200)));

    private static InvalidDataException Rejects(byte[] file, long limit = Limit) =>
        Assert.Throws<InvalidDataException>(() => Gzip.Unpack(file, limit));

    [Fact]
    public void ARoundTripGivesThePayload() => Assert.Equal(Payload, Gzip.Unpack(Synthetic.Gzip(Payload), Limit));

    [Fact]
    public void AnEmptyPayloadRoundTrips() => Assert.Empty(Gzip.Unpack(Synthetic.Gzip([]), Limit));

    [Fact]
    public void APayloadLargerThanTheFirstBufferRoundTrips()
    {
        var random = new Random(1);
        var payload = new byte[300_000];
        random.NextBytes(payload.AsSpan(0, 1000));
        Assert.Equal(payload, Gzip.Unpack(Synthetic.Gzip(payload), Limit));
    }

    [Fact]
    public void ANonZeroFlagByteIsRejectedByName()
    {
        byte[] file = Synthetic.Gzip(Payload);
        file[3] = 0x08;
        var e = Rejects(file);
        Assert.Equal(3, e.Offset);
        Assert.Contains("FLG", e.Message);
    }

    [Theory]
    [InlineData(0, 0x1E)]
    [InlineData(1, 0x8C)]
    [InlineData(2, 0x07)]
    public void ABadMagicOrMethodIsRejected(int at, byte value)
    {
        byte[] file = Synthetic.Gzip(Payload);
        file[at] = value;
        Rejects(file);
    }

    [Fact]
    public void ACrcMismatchIsRejected()
    {
        byte[] file = Synthetic.Gzip(Payload);
        file[^8] ^= 1;
        Assert.Equal(file.Length - 8, Rejects(file).Offset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void ASizeMismatchIsRejected(int delta)
    {
        byte[] file = Synthetic.Gzip(Payload);
        Synthetic.Le32((uint)(Payload.Length + delta)).CopyTo(file, file.Length - 4);
        Rejects(file);
    }

    [Fact]
    public void CutDeflateDataIsRejected()
    {
        byte[] full = Synthetic.Gzip(Payload);
        byte[] deflate = Synthetic.Deflate(Payload);
        byte[] cut = Synthetic.Concat(full[..10], deflate[..(deflate.Length / 2)], full[^8..]);
        Rejects(cut);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void ACutTrailerIsRejected(int cut) => Rejects(Synthetic.Gzip(Payload)[..^cut]);

    [Fact]
    public void ASecondMemberIsRejected() =>
        Rejects(Synthetic.Concat(Synthetic.Gzip(Payload), Synthetic.Gzip(Synthetic.Ascii("second member"))));

    // The trailer check alone cannot catch the next three: the last 8 bytes are a valid trailer of the first member.
    [Fact]
    public void ASecondIdenticalMemberIsRejected()
    {
        byte[] member = Synthetic.Gzip(Payload);
        var e = Rejects(Synthetic.Concat(member, member));
        Assert.Contains("follow the deflate data", e.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(70_000)]
    public void BytesBetweenTheDeflateDataAndAValidTrailerAreRejected(int count)
    {
        byte[] member = Synthetic.Gzip(Payload);
        Rejects(Synthetic.Concat(member[..^8], new byte[count], member[^8..]));
    }

    [Fact]
    public void AMemberFollowedByACopyOfItsTrailerIsRejected()
    {
        byte[] member = Synthetic.Gzip(Payload);
        Rejects(Synthetic.Concat(member, member[^8..]));
    }

    [Fact]
    public void NoDeflateDataIsRejected() =>
        Rejects(Synthetic.Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0], Synthetic.Le32(0u), Synthetic.Le32(0u)));

    [Fact]
    public void AnEmptyPayloadFollowedByJunkIsRejected()
    {
        byte[] member = Synthetic.Gzip([]);
        Rejects(Synthetic.Concat(member[..^8], [0], member[^8..]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void TrailingBytesAreRejected(int count) => Rejects(Synthetic.Concat(Synthetic.Gzip(Payload), new byte[count]));

    [Fact]
    public void DataShorterThanHeaderAndTrailerIsRejected() => Rejects([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0]);

    [Fact]
    public void ASizeOverTheLimitIsRejectedBeforeInflating()
    {
        // The deflate data is garbage: inflating first would report it as corrupt instead of naming the limit.
        byte[] file = Synthetic.Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0], [0xFF, 0xFF, 0xFF], Synthetic.Le32(0u), Synthetic.Le32(5000u));
        var e = Rejects(file, limit: 4999);
        Assert.Contains("MaxUnpackedLength", e.Message);
        Assert.Equal(file.Length - 4, e.Offset);
    }

    [Fact]
    public void CorruptDeflateDataIsRejected()
    {
        byte[] file = Synthetic.Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0], [0xFF, 0xFF, 0xFF], Synthetic.Le32(0u), Synthetic.Le32(10u));
        Rejects(file);
    }

    [Fact]
    public void LargeForgedIsizeWithCorruptDataDoesNotAllocateLargeBuffer()
    {
        byte[] file = Synthetic.Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0], [0xFF, 0xFF, 0xFF], Synthetic.Le32(0u), Synthetic.Le32(64u * 1024 * 1024));
        long before = GC.GetAllocatedBytesForCurrentThread();
        Rejects(file);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 4 * 1024 * 1024, $"Allocated {allocated} bytes, expected < 4 MiB");
    }

    [Fact]
    public void PeekInflatesTheHeadOfCutData()
    {
        // Deterministic uncompressed deflate block (BFINAL=0, BTYPE=00) of 32 bytes,
        // ensuring the cut prefix inflates across all platforms without depending on platform-specific compression trees.
        byte[] uncompressedBlock = [0x00, 0x20, 0x00, 0xDF, 0xFF];
        byte[] file = Synthetic.Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0], uncompressedBlock, Payload[..32], [0x00]);
        var into = new byte[32];
        Assert.Equal(32, Gzip.Peek(file, into));
        Assert.Equal(Payload[..32], into);
    }

    [Fact]
    public void PeekReturnsZeroOnFailure()
    {
        var into = new byte[32];
        Assert.Equal(0, Gzip.Peek([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF], into));
        Assert.Equal(0, Gzip.Peek([0x1F, 0x8B], into));
        Assert.Equal(0, Gzip.Peek(Synthetic.Ascii("not gzip data at all"), into));
    }
}
