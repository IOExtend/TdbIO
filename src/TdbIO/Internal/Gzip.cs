using System.IO.Compression;

namespace TdbIO.Internal;

/// <summary>The gzip wrapper of model files: one member, header flags 0, CRC-32 and size checked. Anything else is
/// rejected, never repaired.</summary>
internal static class Gzip
{
    private const int HeaderLength = 10;
    private const int TrailerLength = 8;

    public static bool IsGzip(ReadOnlySpan<byte> d) => d.Length >= 2 && d[0] == 0x1F && d[1] == 0x8B;

    /// <summary>Unpacks a whole gzip file. Offsets in errors are file offsets.</summary>
    public static byte[] Unpack(byte[] file, long maxUnpacked)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!IsGzip(file)) throw FormatError.At("not gzip data", 0);
        if (file.Length < HeaderLength + TrailerLength) throw FormatError.At("gzip data is too short", file.Length);
        if (file[2] != 8) throw FormatError.At($"gzip compression method {file[2]} is not deflate", 2);
        // Only FLG = 0 has been seen; names, comments, extra fields and header CRCs are rejected rather than guessed at.
        if (file[3] != 0) throw FormatError.At($"gzip FLG 0x{file[3]:X2} is not supported", 3);

        int trailer = file.Length - TrailerLength;
        uint size = Bytes.U32(file, trailer + 4);
        if (size > maxUnpacked)
            throw FormatError.At($"gzip unpacked size {size} is larger than MaxUnpackedLength ({maxUnpacked} bytes)", trailer + 4);

        byte[] output = Inflate(file, trailer, size);
        if (Crc32.Compute(output) != Bytes.U32(file, trailer))
            throw FormatError.At("gzip CRC-32 does not match the unpacked data", trailer);
        return output;
    }

    /// <summary>Inflates up to <c>into.Length</c> bytes of the gzip data in <paramref name="head"/>, which may be cut, without
    /// any check of the trailer. Returns the number of bytes inflated, or 0 on any failure.</summary>
    public static int Peek(ReadOnlySpan<byte> head, Span<byte> into)
    {
        if (head.Length <= HeaderLength || !IsGzip(head) || head[2] != 8 || head[3] != 0) return 0;
        using var deflate = new DeflateStream(new MemoryStream(head[HeaderLength..].ToArray()), CompressionMode.Decompress);
        try
        {
            int total = 0, n;
            while (total < into.Length && (n = deflate.Read(into[total..])) > 0) total += n;
            return total;
        }
        catch (InvalidDataException)
        {
            return 0; // corrupt deflate data: the kind is unknown, Open reports the error
        }
    }

    // Reads into a buffer that grows up to the declared size, so a forged size cannot allocate memory the data does not fill.
    private static byte[] Inflate(byte[] file, int trailer, uint size)
    {
        var input = new LastByteAloneStream(file, HeaderLength, trailer);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        var buffer = new byte[(int)Math.Min(size, Math.Max(65536L, file.Length * 4L))];
        int total = 0;
        try
        {
            while (true)
            {
                if (total == buffer.Length)
                {
                    if (total == size) break;
                    Array.Resize(ref buffer, (int)Math.Min(size, buffer.Length * 2L));
                }
                int n = deflate.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            if (deflate.ReadByte() >= 0) throw FormatError.At($"deflate data unpacks to more than {size} bytes", trailer);
        }
        catch (InvalidDataException e) when (!e.Data.Contains(FormatError.OffsetKey))
        {
            throw FormatError.At("deflate data is corrupt", HeaderLength, e);
        }
        // The inflater asked for input after the last byte: the final deflate block never ended.
        if (input.ReadPastEnd) throw FormatError.At("deflate data is cut before its final block ends", trailer);
        // The deflate stream ended before the trailer: a second member or other bytes follow it.
        if (input.Position < trailer) throw FormatError.At("bytes follow the deflate data before the gzip trailer", trailer);
        if (total < size) throw FormatError.At($"deflate data ends after {total} of {size} bytes", trailer);
        return buffer;
    }

    /// <summary>Serves the deflate bytes, but never the last byte together with others. The inflater asks for input only after
    /// it used all it was given, so it gets the last byte only if the deflate stream has not ended before it, and it always
    /// uses that byte. When the deflate stream ends, <see cref="Position"/> is therefore the end of the stream exactly when
    /// no bytes follow it.</summary>
    private sealed class LastByteAloneStream(byte[] data, int start, int end) : Stream
    {
        private int _position = start;

        public bool ReadPastEnd { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty) return 0;
            if (_position == end)
            {
                ReadPastEnd = true;
                return 0;
            }
            int n = _position < end - 1 ? Math.Min(buffer.Length, end - 1 - _position) : 1;
            data.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false; // keeps the inflater from moving the position back
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
