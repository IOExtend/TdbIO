namespace TdbIO.Internal;

/// <summary>Reads a whole input into memory, never more than a cap.</summary>
internal static class InputReader
{
    /// <summary>Reads the stream from its current position to the end, after <paramref name="head"/>, which was already
    /// read from it. The cap counts both; <paramref name="limit"/> names it in the error. The stream is not disposed.</summary>
    public static byte[] ReadAll(Stream s, long cap, ReadOnlySpan<byte> head, string limit)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!s.CanRead) throw new ArgumentException("The stream is not readable.", nameof(s));
        // A known length rejects a large input before any byte is read and sizes the buffer exactly; counting below covers
        // streams that cannot seek or that grow.
        long known = s.CanSeek ? head.Length + Math.Max(0, s.Length - s.Position) : head.Length;
        if (known > cap) throw TooLarge(cap, limit);

        var output = new MemoryStream((int)known);
        output.Write(head);
        var chunk = new byte[81920];
        int n;
        while ((n = s.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (output.Length + n > cap) throw TooLarge(cap, limit);
            output.Write(chunk, 0, n);
        }
        // Skips the copy when the known length was right, so a seekable input is held in memory once.
        return output.Length == output.Capacity ? output.GetBuffer() : output.ToArray();
    }

    /// <summary>The number of bytes from the current position to the end, without keeping them. A stream that cannot seek
    /// is read through, and more than <paramref name="cap"/> bytes in all (with <paramref name="already"/>) is an
    /// error.</summary>
    public static long CountRest(Stream s, long cap, long already)
    {
        if (s.CanSeek) return Math.Max(0, s.Length - s.Position);
        var chunk = new byte[81920];
        long total = 0;
        int n;
        while ((n = s.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += n;
            if (already + total > cap) throw TooLarge(cap, "MaxUnpackedLength");
        }
        return total;
    }

    private static InvalidDataException TooLarge(long cap, string limit) =>
        FormatError.At($"the input is larger than {limit} ({cap} bytes)", cap);
}
