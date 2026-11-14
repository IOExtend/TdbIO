namespace TdbIO;

/// <summary>One record of a model records file (<c>.db2</c>).</summary>
/// <param name="Offset">The offset of the record's key in the unpacked file.</param>
/// <param name="Key">The raw key.</param>
/// <param name="Count">The number of rows.</param>
/// <param name="RowSize">The size of a row after its marker byte.</param>
/// <param name="Rows">The raw rows: <paramref name="Count"/> times one marker byte and <paramref name="RowSize"/> bytes. The
/// content is not known and is not interpreted.</param>
public readonly record struct Db2Record(long Offset, uint Key, uint Count, uint RowSize, ReadOnlyMemory<byte> Rows);
