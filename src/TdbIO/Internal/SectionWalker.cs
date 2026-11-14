using System.Globalization;

namespace TdbIO.Internal;

/// <summary>Which file the sections come from. The section grammar is the same; the files differ in what lies between
/// sections and in which values are accepted.</summary>
internal enum SectionMode
{
    /// <summary><c>.db1</c>: an optional key before and a closer after keyed sections; strict markers and descriptors.</summary>
    Db1,

    /// <summary>DBV@: sections follow each other with no gap; unexpected row types and descriptors give warnings.</summary>
    Dbv,
}

/// <summary>Reads table sections: <c>[key] separator size count descriptors rows 00 [closer]</c>. The number of rows is not
/// stored, so every section is walked row by row. A broken section is an error; the walker never searches for the next
/// separator, because row bodies are arbitrary bytes.</summary>
internal static class SectionWalker
{
    /// <summary>The first byte after the 49-byte container header.</summary>
    private const int BodyStart = 49;

    private const int Db1MaxRowSize = 65536;
    private const int DbvMaxRowSize = 1024 * 1024;
    private const int DbvMaxDescriptors = 4096;

    private static ReadOnlySpan<byte> Separator => [0x66, 0xC0, 0xCE, 0xDB];
    private static ReadOnlySpan<byte> Closer => [0x4F, 0x61, 0xBC, 0x00];

    // The 40 bytes right before the first section of a .db1: twice (C9 C0 CE DB, i32 0, i32 1, i32 0, i32 -1).
    private static ReadOnlySpan<byte> FirstSectionLead =>
    [
        0xC9, 0xC0, 0xCE, 0xDB, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF,
        0xC9, 0xC0, 0xCE, 0xDB, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF,
    ];

    /// <summary>The offset of the first section of a <c>.db1</c> payload: the first separator at or after 49, which must be
    /// preceded by the fixed 40-byte lead.</summary>
    public static int FindFirstSection(ReadOnlySpan<byte> payload)
    {
        Bytes.Need(BodyStart, 0, payload.Length, "model header");
        int found = payload[BodyStart..].IndexOf(Separator);
        if (found < 0) throw FormatError.At("no table section separator after the header", BodyStart);
        int first = BodyStart + found;
        int lead = first - FirstSectionLead.Length;
        if (lead < BodyStart || !payload[lead..first].SequenceEqual(FirstSectionLead))
            throw FormatError.At("section 0: the 40 bytes before the first section do not match the expected lead",
                Math.Max(lead, BodyStart));
        return first;
    }

    /// <summary>Reads sections from <paramref name="start"/> to the end of <paramref name="data"/>, which they must fill
    /// exactly. In <see cref="SectionMode.Db1"/> a section without a key after one with a key is an error.</summary>
    public static IReadOnlyList<TableSection> Walk(ReadOnlyMemory<byte> data, int start, int firstOrdinal, SectionMode mode,
        List<string>? warnings)
    {
        var sections = new List<TableSection>();
        long p = start;
        int ordinal = firstOrdinal;
        bool keyedSeen = false;
        while (p < data.Length)
        {
            var section = ReadOne(data, checked((int)p), ordinal, mode, warnings);
            if (section.Key is null && keyedSeen)
                throw Error(ordinal, "a section without a key follows a section with a key", section.Offset);
            keyedSeen |= section.Key is not null;
            sections.Add(section);
            p = section.End;
            ordinal++;
        }
        // Every read is bounds-checked, so the loop can only stop at the end; checked anyway because the rule is strict.
        if (p != data.Length) throw Error(ordinal, "the sections do not end at the end of the data", p);
        return sections.AsReadOnly();
    }

    /// <summary>Reads the one section that starts at <paramref name="offset"/>.</summary>
    public static TableSection ReadOne(ReadOnlyMemory<byte> data, int offset, int ordinal, SectionMode mode, List<string>? warnings)
    {
        ReadOnlySpan<byte> d = data.Span;
        long length = d.Length;
        long p = offset;

        Need(ordinal, p, 4, length, "section start");
        int? key = null;
        if (!d.Slice((int)p, 4).SequenceEqual(Separator))
        {
            // Only .db1 has keys; no key seen equals the separator, so 4 bytes equal to it start a section without one.
            if (mode == SectionMode.Dbv) throw Error(ordinal, "section separator expected", p);
            key = Bytes.I32(d, p);
            p += 4;
            Need(ordinal, p, 4, length, "section separator");
            if (!d.Slice((int)p, 4).SequenceEqual(Separator)) throw Error(ordinal, "section separator expected after the key", p);
        }
        p += 4;

        Need(ordinal, p, 8, length, "section header");
        int rowSize;
        int count;
        if (mode == SectionMode.Db1)
        {
            int rs = Bytes.I32(d, p), nd = Bytes.I32(d, p + 4);
            if (rs <= 0 || rs > Db1MaxRowSize)
                throw Error(ordinal, string.Create(CultureInfo.InvariantCulture, $"row size {rs} is outside 1..{Db1MaxRowSize}"), p);
            if (nd <= 0 || nd > rs)
                throw Error(ordinal, string.Create(CultureInfo.InvariantCulture, $"descriptor count {nd} is outside 1..{rs}"), p + 4);
            (rowSize, count) = (rs, nd);
        }
        else
        {
            uint s = Bytes.U32(d, p), f = Bytes.U32(d, p + 4);
            if (s > DbvMaxRowSize)
                throw Error(ordinal, string.Create(CultureInfo.InvariantCulture, $"row size {s} is larger than the limit of {DbvMaxRowSize}"), p);
            if (f > DbvMaxDescriptors)
                throw Error(ordinal, string.Create(CultureInfo.InvariantCulture, $"descriptor count {f} is larger than the limit of {DbvMaxDescriptors}"), p + 4);
            (rowSize, count) = ((int)s, (int)f);
        }
        p += 8;
        Need(ordinal, p, 4L * count, length, "section descriptors");

        var descriptors = new uint[count];
        long firstOddDescriptor = -1;
        for (int i = 0; i < count; i++, p += 4)
        {
            descriptors[i] = Bytes.U32(d, p);
            if (descriptors[i] > 1)
            {
                if (mode == SectionMode.Db1) throw Error(ordinal, "descriptor word is not 0 or 1", p);
                if (firstOddDescriptor < 0) firstOddDescriptor = p;
            }
        }
        if (firstOddDescriptor >= 0)
            warnings?.Add(Warning(ordinal, offset, "has a descriptor word other than 0 or 1", firstOddDescriptor));

        long rowsOffset = p;
        int rows = 0;
        long firstOddType = -1;
        while (true)
        {
            if (p >= length) throw Error(ordinal, "section without terminator: the data ends before the 0 byte", p);
            byte marker = d[(int)p];
            if (marker == 0)
            {
                p++;
                break;
            }
            if (mode == SectionMode.Db1 && marker is not (0x04 or 0x0C))
                throw Error(ordinal, string.Create(CultureInfo.InvariantCulture, $"row marker 0x{marker:X2} is not 0x04 or 0x0C"), p);
            if (mode == SectionMode.Dbv && marker != 1 && firstOddType < 0) firstOddType = p;
            if (rowSize + 9L > length - p) throw Error(ordinal, "row runs past the end", p);
            p += rowSize + 9L;
            rows++;
        }
        if (firstOddType >= 0) warnings?.Add(Warning(ordinal, offset, "has a row type other than 1", firstOddType));

        if (key is not null)
        {
            Need(ordinal, p, 4, length, "section closer");
            if (!d.Slice((int)p, 4).SequenceEqual(Closer)) throw Error(ordinal, "section closer expected after the rows", p);
            p += 4;
        }

        return new TableSection(ordinal, offset, p, key, rowSize, Array.AsReadOnly(descriptors), rowsOffset,
            new RowList(data, rowsOffset, rows, rowSize, mode));
    }

    private static void Need(int ordinal, long at, long count, long length, string what)
    {
        if (count > length - at) throw Error(ordinal, $"{what} runs past the end", at);
    }

    private static InvalidDataException Error(int ordinal, string message, long offset) =>
        FormatError.At(string.Create(CultureInfo.InvariantCulture, $"section {ordinal}: {message}"), offset);

    private static string Warning(int ordinal, long sectionOffset, string what, long at) =>
        string.Create(CultureInfo.InvariantCulture, $"section {ordinal} at offset 0x{sectionOffset:X} {what}, first at offset 0x{at:X}");
}
