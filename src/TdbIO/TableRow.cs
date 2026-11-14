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
}
