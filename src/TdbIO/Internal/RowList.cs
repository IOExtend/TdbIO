using System.Buffers.Binary;
using System.Collections;

namespace TdbIO.Internal;

/// <summary>The rows of a section as a fixed-stride view over the data. A section can hold millions of rows, so nothing
/// is stored per row; the walk has already checked every row.</summary>
internal sealed class RowList(ReadOnlyMemory<byte> data, long rowsOffset, int count, int rowSize, SectionMode mode)
    : IReadOnlyList<TableRow>
{
    public int Count => count;

    public TableRow this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
            long offset = rowsOffset + index * (rowSize + 9L);
            ReadOnlySpan<byte> tail = data.Span.Slice((int)(offset + 1 + rowSize), 8);
            long a, b;
            if (mode == SectionMode.Db1)
            {
                a = BinaryPrimitives.ReadInt32LittleEndian(tail);
                b = BinaryPrimitives.ReadInt32LittleEndian(tail[4..]);
            }
            else
            {
                a = BinaryPrimitives.ReadUInt32LittleEndian(tail);
                b = BinaryPrimitives.ReadUInt32LittleEndian(tail[4..]);
            }
            return new TableRow(offset, data.Span[(int)offset], data.Slice((int)offset + 1, rowSize), a, b);
        }
    }

    public IEnumerator<TableRow> GetEnumerator()
    {
        for (int i = 0; i < count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
