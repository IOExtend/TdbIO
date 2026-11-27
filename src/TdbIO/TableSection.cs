namespace TdbIO;

/// <summary>A section of fixed-size rows, as found in <c>.db1</c> files (<see cref="Db1Document.ReadTables"/>) and in
/// DBV@ files (<see cref="DbvDocument.Sections"/>). Sections represent database tables or domains; in <c>.db1</c> files
/// they are identified by their schema table number in <see cref="Key"/>.</summary>
public sealed class TableSection
{
    internal TableSection(int ordinal, long offset, long end, int? key, int rowSize, IReadOnlyList<uint> descriptors,
        long rowsOffset, IReadOnlyList<TableRow> rows, Internal.RowText? text = null)
    {
        Ordinal = ordinal;
        Offset = offset;
        End = end;
        Key = key;
        RowSize = rowSize;
        Descriptors = descriptors;
        RowsOffset = rowsOffset;
        Rows = rows;
        Text = text;
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

    /// <summary>The text rule of the file, shared with the rows; see <see cref="TableRow.TextEncoding"/>.</summary>
    internal Internal.RowText? Text { get; }

    private IReadOnlyList<T> DecodeRows<T>(int expectedSize, Func<TableRow, T> decode)
    {
        if (RowSize != expectedSize)
            throw new InvalidOperationException($"Section row size {RowSize} does not match expected size {expectedSize} for {typeof(T).Name}.");
        var list = new T[Rows.Count];
        for (int i = 0; i < Rows.Count; i++)
            list[i] = decode(Rows[i]);
        return list;
    }

    /// <summary>Decodes all rows of this section as <see cref="ObjectRow"/> (size 72).</summary>
    public IReadOnlyList<ObjectRow> AsObjects() => DecodeRows(ObjectRow.ExpectedRowSize, r => r.AsObject());

    /// <summary>Decodes all rows of this section as <see cref="AssemblyRow"/> (size 92).</summary>
    public IReadOnlyList<AssemblyRow> AsAssemblies() => DecodeRows(AssemblyRow.ExpectedRowSize, r => r.AsAssembly());

    /// <summary>Decodes all rows of this section as <see cref="CoordsysRow"/> (size 40).</summary>
    public IReadOnlyList<CoordsysRow> AsCoordsys() => DecodeRows(CoordsysRow.ExpectedRowSize, r => r.AsCoordsys());

    /// <summary>Decodes all rows of this section as <see cref="PartRow"/> (size 64).</summary>
    public IReadOnlyList<PartRow> AsParts() => DecodeRows(PartRow.ExpectedRowSize, r => r.AsPart());

    /// <summary>Decodes all rows of this section as <see cref="PointRow"/> (size 32).</summary>
    public IReadOnlyList<PointRow> AsPoints() => DecodeRows(PointRow.ExpectedRowSize, r => r.AsPoint());

    /// <summary>Decodes all rows of this section as <see cref="RelationRow"/> (size 60).</summary>
    public IReadOnlyList<RelationRow> AsRelations() => DecodeRows(RelationRow.ExpectedRowSize, r => r.AsRelation());

    /// <summary>Decodes all rows of this section as <see cref="WeldingRow"/> (size 24).</summary>
    public IReadOnlyList<WeldingRow> AsWeldings() => DecodeRows(WeldingRow.ExpectedRowSize, r => r.AsWelding());

    /// <summary>Decodes all rows of this section as <see cref="NumAttrRow"/> (size 24).</summary>
    public IReadOnlyList<NumAttrRow> AsNumAttrs() => DecodeRows(NumAttrRow.ExpectedRowSize, r => r.AsNumAttr());

    /// <summary>Decodes all rows of this section as <see cref="StrAttrRow"/> (size 24).</summary>
    public IReadOnlyList<StrAttrRow> AsStrAttrs() => DecodeRows(StrAttrRow.ExpectedRowSize, r => r.AsStrAttr());

    /// <summary>Decodes all rows of this section as <see cref="PartAttrRow"/> (size 372).</summary>
    public IReadOnlyList<PartAttrRow> AsPartAttrs() => DecodeRows(PartAttrRow.ExpectedRowSize, r => r.AsPartAttr());

    /// <summary>Decodes all rows of this section as <see cref="NumAttrAttrRow"/> (size 44).</summary>
    public IReadOnlyList<NumAttrAttrRow> AsNumAttrAttrs() => DecodeRows(NumAttrAttrRow.ExpectedRowSize, r => r.AsNumAttrAttr());

    /// <summary>Decodes all rows of this section as <see cref="StrAttrAttrRow"/> (size 120).</summary>
    public IReadOnlyList<StrAttrAttrRow> AsStrAttrAttrs() => DecodeRows(StrAttrAttrRow.ExpectedRowSize, r => r.AsStrAttrAttr());

    /// <summary>Decodes all rows of this section as <see cref="StringPoolRow"/> (size 45).</summary>
    public IReadOnlyList<StringPoolRow> AsStringPool() => DecodeRows(StringPoolRow.ExpectedRowSize, r => r.AsStringPool());

    /// <summary>Decodes all rows of this section as <see cref="PartNumberingRow"/> (size 68).</summary>
    public IReadOnlyList<PartNumberingRow> AsPartNumberings() => DecodeRows(PartNumberingRow.ExpectedRowSize, r => r.AsPartNumbering());

    /// <summary>Decodes all rows of this section as <see cref="AssemblyNumberingRow"/> (size 76).</summary>
    public IReadOnlyList<AssemblyNumberingRow> AsAssemblyNumberings() => DecodeRows(AssemblyNumberingRow.ExpectedRowSize, r => r.AsAssemblyNumbering());

    /// <summary>Decodes all rows of this section as <see cref="ObjectNumberingRow"/> (size 12).</summary>
    public IReadOnlyList<ObjectNumberingRow> AsObjectNumberings() => DecodeRows(ObjectNumberingRow.ExpectedRowSize, r => r.AsObjectNumbering());

    /// <summary>Decodes all rows of this section as <see cref="BoltRow"/> (size 24).</summary>
    public IReadOnlyList<BoltRow> AsBolts() => DecodeRows(BoltRow.ExpectedRowSize, r => r.AsBolt());

    /// <summary>Decodes all rows of this section as <see cref="ObjectModificationRow"/> (size 20).</summary>
    public IReadOnlyList<ObjectModificationRow> AsObjectModifications() => DecodeRows(ObjectModificationRow.ExpectedRowSize, r => r.AsObjectModification());

    /// <summary>Decodes all rows of this section as <see cref="BoltHoleRow"/> (size 48).</summary>
    public IReadOnlyList<BoltHoleRow> AsBoltHoles() => DecodeRows(BoltHoleRow.ExpectedRowSize, r => r.AsBoltHole());

    /// <summary>Decodes all rows of this section as <see cref="ObjectPhaseRow"/> (size 8).</summary>
    public IReadOnlyList<ObjectPhaseRow> AsObjectPhases() => DecodeRows(ObjectPhaseRow.ExpectedRowSize, r => r.AsObjectPhase());

    /// <summary>Decodes all rows of this section as <see cref="BoltHolePointsRow"/> (size 60).</summary>
    public IReadOnlyList<BoltHolePointsRow> AsBoltHolePoints() => DecodeRows(BoltHolePointsRow.ExpectedRowSize, r => r.AsBoltHolePoints());
}
