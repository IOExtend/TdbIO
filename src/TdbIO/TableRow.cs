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
public readonly record struct TableRow(long Offset, byte Marker, ReadOnlyMemory<byte> Body, long A, long B);
