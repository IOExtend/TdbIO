namespace TdbIO.Internal;

/// <summary>Reads the records of a <c>.db2</c> payload: from the end of the header to the end of the data, each record is
/// key, count, row size, the rows and the terminator <c>4F 61 BC 00</c>.</summary>
internal static class Db2Reader
{
    private const int RecordHeaderLength = 12;
    private const uint Terminator = 0x00BC614F;

    public static IReadOnlyList<Db2Record> Read(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;
        var records = new List<Db2Record>();
        int p = XsteelHeader.Length;
        while (p < span.Length)
        {
            if (span.Length - p < RecordHeaderLength)
                throw FormatError.At($"records file has {span.Length - p} bytes left, fewer than a record header", p);
            uint key = Bytes.U32(span, p);
            uint count = Bytes.U32(span, p + 4);
            uint rowSize = Bytes.U32(span, p + 8);
            // Both factors fit in 32 bits, so the product and the total fit in 64 bits without overflow.
            ulong rowsLength = count * (1UL + rowSize);
            if (RecordHeaderLength + rowsLength + 4 > (ulong)(span.Length - p))
                throw FormatError.At($"record of {count} rows of {rowSize} bytes runs past the end", p);
            int rows = p + RecordHeaderLength;
            int end = rows + (int)rowsLength;
            uint terminator = Bytes.U32(span, end);
            if (terminator != Terminator) throw FormatError.At($"record terminator 0x{terminator:X8} is not 0x{Terminator:X8}", end);
            records.Add(new Db2Record(p, key, count, rowSize, payload.Slice(rows, (int)rowsLength)));
            p = end + 4;
        }
        return records;
    }
}
