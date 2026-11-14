namespace TdbIO.Internal;

/// <summary>Reads an id range file (<c>.idrm</c>): magic <c>IdRangeMapper</c>, the next free value, a marker, a count and
/// 28-byte ranges up to the exact end.</summary>
internal static class IdRangeReader
{
    private const int HeaderLength = 25;
    private const int RangeLength = 28;
    private const uint Marker = 0xDBCEC0E1;

    public static (uint Next, IReadOnlyList<IdRange> Ranges) Read(ReadOnlySpan<byte> data, List<string> warnings)
    {
        if (!data.StartsWith("IdRangeMapper"u8)) throw FormatError.At("not an id range file", 0);
        Bytes.Need(0, HeaderLength, data.Length, "id range header");
        uint next = Bytes.U32(data, 13);
        uint marker = Bytes.U32(data, 17);
        if (marker != Marker) throw FormatError.At($"id range marker 0x{marker:X8} is not 0x{Marker:X8}", 17);
        uint count = Bytes.U32(data, 21);
        long expected = HeaderLength + (long)RangeLength * count;
        if (data.Length != expected)
            throw FormatError.At($"id range file has {data.Length} bytes, not the {expected} that {count} ranges take", 21);

        var ranges = new IdRange[count];
        for (int i = 0; i < count; i++)
        {
            int at = HeaderLength + RangeLength * i;
            ranges[i] = new IdRange(Bytes.U32(data, at), new Guid(data.Slice(at + 4, 16)), Bytes.U32(data, at + 20), Bytes.U32(data, at + 24));
        }
        // Contiguity has been seen on few files only, so a break in it is kept with a warning, not rejected.
        for (int i = 1; i < ranges.Length; i++)
            if ((long)ranges[i - 1].Start + ranges[i - 1].Length != ranges[i].Start)
                warnings.Add($"id range {i} at offset 0x{HeaderLength + RangeLength * i:X} does not start where range {i - 1} ends");
        if (ranges.Length > 0 && (long)ranges[^1].Start + ranges[^1].Length != next)
            warnings.Add("next free value at offset 0xD is not the end of the last range");
        return (next, ranges);
    }
}
