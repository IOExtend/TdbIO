using TdbIO.Internal;

namespace TdbIO;

/// <summary>A <c>history.db</c> file. It is an SQLite 3 database; only its header is read, the schema is not
/// known.</summary>
public sealed class HistoryDocument : TdbDocument
{
    private HistoryDocument(ushort pageSize, uint pageCount, long length, IReadOnlyList<string> warnings)
        : base(TdbFileKind.History, warnings)
    {
        PageSize = pageSize;
        PageCount = pageCount;
        Length = length;
    }

    /// <summary>The raw page size field (big-endian u16 at offset 16); no special values are decoded.</summary>
    public ushort PageSize { get; }

    /// <summary>The page count (big-endian u32 at offset 28).</summary>
    public uint PageCount { get; }

    /// <summary>The length of the file in bytes. A warning is given when it is not page size times page count.</summary>
    public long Length { get; }

    /// <summary>Reads the document from the first bytes of the file (at least 32) and the file length.</summary>
    internal static HistoryDocument Create(ReadOnlySpan<byte> head, long length)
    {
        var warnings = new List<string>();
        var (pageSize, pageCount) = HistoryReader.Read(head, length, warnings);
        return new HistoryDocument(pageSize, pageCount, length, warnings);
    }
}
