namespace TdbIO;

/// <summary>A section of fixed-size rows, as found in <c>.db1</c> files (<see cref="Db1Document.ReadTables"/>) and in
/// DBV@ files (<see cref="DbvDocument.Sections"/>). Sections have no names; tell them apart by
/// <see cref="Ordinal"/> and <see cref="RowSize"/>.</summary>
public sealed class TableSection
{
    internal TableSection(int ordinal, long offset, long end, int? key, int rowSize, IReadOnlyList<uint> descriptors,
        long rowsOffset, IReadOnlyList<TableRow> rows)
    {
        Ordinal = ordinal;
        Offset = offset;
        End = end;
        Key = key;
        RowSize = rowSize;
        Descriptors = descriptors;
        RowsOffset = rowsOffset;
        Rows = rows;
    }

    /// <summary>The position of the section in its file, from 0.</summary>
    public int Ordinal { get; }

    /// <summary>The offset where the section starts: the key when there is one, otherwise the separator.</summary>
    public long Offset { get; }

    /// <summary>The offset just after the section: after the closer when the section has a key, otherwise after the 0
    /// byte that ends the rows.</summary>
    public long End { get; }

    /// <summary>The raw 32-bit key stored before the section in <c>.db1</c> files, or null when the section has none.
    /// Always null in DBV@ files. Its meaning is not known, and the same key may belong to another section in files of a
    /// different version.</summary>
    public int? Key { get; }

    /// <summary>The number of body bytes in every row.</summary>
    public int RowSize { get; }

    /// <summary>The descriptor words of the section header, raw. Their meaning is not known.</summary>
    public IReadOnlyList<uint> Descriptors { get; }

    /// <summary>The offset of the first row (or of the 0 byte that ends the rows when there are none).</summary>
    public long RowsOffset { get; }

    /// <summary>The rows in file order. The list is a view over the data: each row is built when it is read.</summary>
    public IReadOnlyList<TableRow> Rows { get; }
}
