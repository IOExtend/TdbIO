using System.Text;

namespace TdbIO;

/// <summary>One row of a <see cref="TableSection"/>: a marker byte, the raw body and two trailing numbers representing
/// transaction TimeIds.</summary>
/// <param name="Offset">The offset of the marker byte in the data that holds the section.</param>
/// <param name="Marker">The marker byte; never 0. In <c>.db1</c> files it indicates active (<c>0x04</c>) or deleted/superseded
/// (<c>0x0C</c>) status; in DBV@ files it is a non-zero row status/type.</param>
/// <param name="Body">The <see cref="TableSection.RowSize"/> body bytes, raw.</param>
/// <param name="A">The first number after the body: the creation transaction TimeId (signed 32-bit in <c>.db1</c> files,
/// unsigned in DBV@ files). Also accessible via <see cref="CreationTimeId"/>.</param>
/// <param name="B">The second number after the body: the deletion or superseding transaction TimeId, read like <see cref="A"/>.
/// Equal to <see cref="A"/> for active unmodified rows. Also accessible via <see cref="DeletionTimeId"/>.</param>
public readonly record struct TableRow(long Offset, byte Marker, ReadOnlyMemory<byte> Body, long A, long B)
{
    /// <summary>The creation transaction TimeId when this row was inserted (<see cref="A"/>).</summary>
    public long CreationTimeId => A;

    /// <summary>The deletion or superseding transaction TimeId when this row was removed or replaced (<see cref="B"/>).
    /// For active unmodified rows, this equals <see cref="CreationTimeId"/>.</summary>
    public long DeletionTimeId => B;

    /// <summary>True if this row has the active marker (<c>0x04</c>) in <c>.db1</c> files.</summary>
    public bool IsActive => Marker == 0x04;

    /// <summary>True if this row has the deleted or superseded marker (<c>0x0C</c>) in <c>.db1</c> files.</summary>
    public bool IsDeleted => Marker == 0x0C;

    /// <summary>The encoding the typed decoders (<see cref="AsPart"/>, <see cref="AsPartAttr"/> and the others) read the
    /// text values of this row with: the <see cref="TdbReadOptions.TextEncoding"/> the file was opened with, or null to
    /// choose it from the bytes. Each value is then ASCII or UTF-8 when its bytes are, else code page 1251 or 1252, chosen
    /// once for the whole file from all such values of the typed tables. The text of a model is single-byte in the code
    /// page of the system that saved it, so a value with bytes from 0x80 is not an error; a byte the chosen encoding does
    /// not define is.</summary>
    public Encoding? TextEncoding => Text?.Encoding;

    /// <summary>The text rule of the file the row was read from; null for a row built by a caller.</summary>
    internal Internal.RowText? Text { get; init; }

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
