namespace TdbIO.Internal;

/// <summary>Reads the header fields of a <c>history.db</c> file (an SQLite 3 database) that tell its size.</summary>
internal static class HistoryReader
{
    public const int HeadLength = 100;

    public static (ushort PageSize, uint PageCount) Read(ReadOnlySpan<byte> head, long length, List<string> warnings)
    {
        if (!head.StartsWith("SQLite format 3\0"u8)) throw FormatError.At("not an SQLite 3 database", 0);
        Bytes.Need(0, 32, head.Length, "SQLite header");
        ushort pageSize = Bytes.U16BE(head, 16);
        uint pageCount = Bytes.U32BE(head, 28);
        if ((long)pageSize * pageCount != length)
            warnings.Add($"file length {length} is not page size {pageSize} times page count {pageCount} (offsets 0x10 and 0x1C)");
        return (pageSize, pageCount);
    }
}
