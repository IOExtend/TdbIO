using Xunit;

namespace TdbIO.Tests;

public class TdbFileTests
{
    private const int Head = 64 * 1024;

    [Fact]
    public void UnknownDataIsRejectedAfterTheHead()
    {
        var stream = new EndlessStream(Synthetic.Ascii("no signature"));
        var e = Assert.Throws<InvalidDataException>(() => TdbFile.Open(stream));
        Assert.Contains("not a recognised", e.Message);
        Assert.True(stream.BytesRead <= Head, $"{stream.BytesRead} bytes read");
    }

    [Fact]
    public void TextStopsAtItsOwnLimitNotAtMaxUnpackedLength()
    {
        var stream = new EndlessStream(Synthetic.Ascii("ST\r\n"));
        var e = Assert.Throws<InvalidDataException>(() => TdbFile.Open(stream, "a.nc1"));
        Assert.Contains("16 MiB", e.Message);
        Assert.True(stream.BytesRead <= 16 * 1024 * 1024 + 2 * Head, $"{stream.BytesRead} bytes read");
    }

    [Fact]
    public void ASeekableStreamThatIsTooLongIsRejectedBeforeItIsRead()
    {
        var stream = new EndlessStream(Synthetic.Gzip([1, 2, 3]), length: long.MaxValue / 2);
        var e = Assert.Throws<InvalidDataException>(() => TdbFile.Open(stream));
        Assert.Contains("MaxUnpackedLength", e.Message);
        Assert.True(stream.BytesRead <= Head, $"{stream.BytesRead} bytes read");
    }

    [Fact]
    public void ASecondGzipMemberIsRejectedByOpen()
    {
        byte[] member = Synthetic.Gzip(Synthetic.XsteelHead(0x80));
        Assert.IsType<Db2Document>(TdbFile.Open(new MemoryStream(member)));
        Assert.Throws<InvalidDataException>(() => TdbFile.Open(new MemoryStream(Synthetic.Concat(member, member))));
    }

    [Fact]
    public void TheFormatErrorCarriesItsOffset()
    {
        var e = Assert.Throws<InvalidDataException>(() => TdbFile.Open(new MemoryStream(Synthetic.Ascii("no signature"))));
        Assert.Equal(0L, e.Data["Offset"]);
        Assert.EndsWith(" at offset 0x0", e.Message);
    }

    /// <summary>A prefix and then zero bytes without end; it counts what is read and may claim a length.</summary>
    private sealed class EndlessStream(byte[] prefix, long? length = null) : Stream
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            buffer.Clear();
            if (BytesRead < prefix.Length)
            {
                int n = (int)Math.Min(buffer.Length, prefix.Length - BytesRead);
                prefix.AsSpan((int)BytesRead, n).CopyTo(buffer);
            }
            BytesRead += buffer.Length;
            return buffer.Length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => length != null;
        public override bool CanWrite => false;
        public override long Length => length ?? throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
