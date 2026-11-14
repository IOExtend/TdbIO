namespace TdbIO;

/// <summary>A section of fixed-size rows, as found in <c>.db1</c> files (<see cref="Db1Document.ReadTables"/>) and in
/// DBV@ files (<see cref="DbvDocument.Sections"/>). Sections represent database tables or domains; in <c>.db1</c> files
/// they are identified by their schema table number in <see cref="Key"/>.</summary>
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

    /// <summary>The schema table number (table ID) stored before the section in <c>.db1</c> files (for example, 1 for
    /// object, 3 for coordsys, 13 for point, 16 for part, 105 for object_numbering, 141 for object_phase), or null when
    /// the section has no table key (such as internal table domains, or any section in DBV@ files).</summary>
    public int? Key { get; }

    /// <summary>The number of body bytes in every row.</summary>
    public int RowSize { get; }

    /// <summary>The index descriptors of the section header. Descriptor 0 defines the primary index/cluster;
    /// descriptors 1..N indicate whether secondary indexes defined in the table schema are active (1) or inactive (0)
    /// for this table domain.</summary>
    public IReadOnlyList<uint> Descriptors { get; }

    /// <summary>The offset of the first row (or of the 0 byte that ends the rows when there are none).</summary>
    public long RowsOffset { get; }

    /// <summary>The rows in file order. The list is a view over the data: each row is built when it is read.</summary>
    public IReadOnlyList<TableRow> Rows { get; }
}
