namespace TdbIO.Internal;

/// <summary>The id map of a model database, read right after the header.</summary>
/// <param name="Field65">Raw u32 at offset 65.</param>
/// <param name="Field73">Raw u32 at offset 73.</param>
/// <param name="Entries">The entries in file order.</param>
/// <param name="End">The offset just after the last entry, where the u32 5 lies.</param>
internal sealed record IdMap(uint Field65, uint Field73, IReadOnlyList<IdMapEntry> Entries, long End);

/// <summary>Reads the id map of a <c>.db1</c> payload: a fixed preamble at 49, the count N at 69 and N − 1 entries of 24
/// bytes from 77, followed by the u32 5.</summary>
internal static class IdMapReader
{
    private const int First = 77;
    private const int EntryLength = 24;
    private const uint Marker = 0xDBCEC0BC;

    public static IdMap? Read(ReadOnlySpan<byte> payload, List<string> warnings)
    {
        Bytes.Need(XsteelHeader.Length, First - XsteelHeader.Length, payload.Length, "id map header");
        // Only one preamble has been seen; another one is reported and the map left alone rather than guessed at.
        if (Bytes.U32(payload, 49) != 1 || Bytes.U32(payload, 57) != 0 || Bytes.U32(payload, 61) != 1)
        {
            warnings.Add("unknown id map header variant at offset 0x31; the id map is not read");
            return null;
        }
        uint marker = Bytes.U32(payload, 53);
        if (marker != Marker) throw FormatError.At($"id map marker 0x{marker:X8} is not 0x{Marker:X8}", 53);

        uint field65 = Bytes.U32(payload, 65);
        if (field65 is not (0x0D or 0x10D)) warnings.Add($"id map field at offset 0x41 has the unexpected value 0x{field65:X}");
        uint count = Bytes.U32(payload, 69);
        uint field73 = Bytes.U32(payload, 73);
        if (count == 0) throw FormatError.At("id map count is 0", 69);

        long entries = count - 1L;
        long end = First + EntryLength * entries;
        if (end + 4 > payload.Length) throw FormatError.At($"id map of {entries} entries runs past the end", 69);
        uint after = Bytes.U32(payload, end);
        if (after != 5) throw FormatError.At($"u32 after the id map is {after}, not 5", end);

        // Sequence numbers are a permutation of 1..N-1; the file order is kept because it is not always ascending.
        var seen = new bool[entries + 1];
        var list = new IdMapEntry[entries];
        for (long i = 0; i < entries; i++)
        {
            long at = First + EntryLength * i;
            uint sequence = Bytes.U32(payload, at);
            if (sequence == 0 || sequence > entries)
                throw FormatError.At($"id map sequence {sequence} is outside 1..{entries}", at);
            if (seen[sequence]) throw FormatError.At($"id map sequence {sequence} is repeated", at);
            seen[sequence] = true;
            list[i] = new IdMapEntry(sequence, new Guid(payload.Slice((int)at + 4, 16)), Bytes.I32(payload, at + 20));
        }
        return new IdMap(field65, field73, list, end);
    }
}
