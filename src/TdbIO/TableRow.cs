namespace TdbIO;

/// <summary>One row of a <see cref="TableSection"/>: a marker byte, the raw body and two trailing numbers. The layout of
/// the body and the meaning of the marker and of <see cref="A"/> and <see cref="B"/> are not known, so they are given
/// as stored.</summary>
/// <param name="Offset">The offset of the marker byte in the data that holds the section.</param>
/// <param name="Marker">The marker byte; never 0. In <c>.db1</c> files it is 0x04 or 0x0C, in DBV@ files any non-zero
/// value.</param>
/// <param name="Body">The <see cref="TableSection.RowSize"/> body bytes, raw.</param>
/// <param name="A">The first number after the body: a signed 32-bit value in <c>.db1</c> files, an unsigned one in DBV@
/// files.</param>
/// <param name="B">The second number after the body, read like <see cref="A"/>.</param>
public readonly record struct TableRow(long Offset, byte Marker, ReadOnlyMemory<byte> Body, long A, long B)
{
    /// <summary>Decodes the row body as an <see cref="ObjectRow"/> (table 1, size 72).</summary>
    public ObjectRow AsObject() => ObjectRow.Decode(this);

    /// <summary>Decodes the row body as an <see cref="AssemblyRow"/> (table 2, size 92).</summary>
    public AssemblyRow AsAssembly() => AssemblyRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="CoordsysRow"/> (table 3, size 40).</summary>
    public CoordsysRow AsCoordsys() => CoordsysRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="PartRow"/> (table 10, size 64).</summary>
    public PartRow AsPart() => PartRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="PointRow"/> (table 13, size 32).</summary>
    public PointRow AsPoint() => PointRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="RelationRow"/> (table 16, size 60).</summary>
    public RelationRow AsRelation() => RelationRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="WeldingRow"/> (table 20, size 24).</summary>
    public WeldingRow AsWelding() => WeldingRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="NumAttrRow"/> (table 38, size 24).</summary>
    public NumAttrRow AsNumAttr() => NumAttrRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="StrAttrRow"/> (table 39, size 24).</summary>
    public StrAttrRow AsStrAttr() => StrAttrRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="PartAttrRow"/> (table 44, size 372).</summary>
    public PartAttrRow AsPartAttr() => PartAttrRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="NumAttrAttrRow"/> (table 48, size 44).</summary>
    public NumAttrAttrRow AsNumAttrAttr() => NumAttrAttrRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="StrAttrAttrRow"/> (table 49, size 120).</summary>
    public StrAttrAttrRow AsStrAttrAttr() => StrAttrAttrRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="StringPoolRow"/> (table 50, size 45).</summary>
    public StringPoolRow AsStringPool() => StringPoolRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="PartNumberingRow"/> (table 94, size 68).</summary>
    public PartNumberingRow AsPartNumbering() => PartNumberingRow.Decode(this);

    /// <summary>Decodes the row body as an <see cref="AssemblyNumberingRow"/> (table 95, size 76).</summary>
    public AssemblyNumberingRow AsAssemblyNumbering() => AssemblyNumberingRow.Decode(this);

    /// <summary>Decodes the row body as an <see cref="ObjectNumberingRow"/> (table 105, size 12).</summary>
    public ObjectNumberingRow AsObjectNumbering() => ObjectNumberingRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="BoltRow"/> (table 107, size 24).</summary>
    public BoltRow AsBolt() => BoltRow.Decode(this);

    /// <summary>Decodes the row body as an <see cref="ObjectModificationRow"/> (table 132, size 20).</summary>
    public ObjectModificationRow AsObjectModification() => ObjectModificationRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="BoltHoleRow"/> (table 140, size 48).</summary>
    public BoltHoleRow AsBoltHole() => BoltHoleRow.Decode(this);

    /// <summary>Decodes the row body as an <see cref="ObjectPhaseRow"/> (table 141, size 8).</summary>
    public ObjectPhaseRow AsObjectPhase() => ObjectPhaseRow.Decode(this);

    /// <summary>Decodes the row body as a <see cref="BoltHolePointsRow"/> (table 142, size 60).</summary>
    public BoltHolePointsRow AsBoltHolePoints() => BoltHolePointsRow.Decode(this);
}
