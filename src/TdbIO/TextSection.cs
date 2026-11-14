namespace TdbIO;

/// <summary>A section of 54-byte text rows in a model database (<c>.db1</c>), with its rows joined into chains.</summary>
public sealed class TextSection
{
    internal TextSection(long offset, int rowCount, IReadOnlyList<TextChain> chains, IReadOnlyList<uint> orphanRowIds)
    {
        Offset = offset;
        RowCount = rowCount;
        Chains = chains;
        OrphanRowIds = orphanRowIds;
    }

    /// <summary>The offset of the 36-byte section signature in <see cref="Db1Document.Payload"/>.</summary>
    public long Offset { get; }

    /// <summary>The number of rows in the section.</summary>
    public int RowCount { get; }

    /// <summary>The chains, in the file order of their head rows.</summary>
    public IReadOnlyList<TextChain> Chains { get; }

    /// <summary>The ids of rows that belong to no chain, in file order. Such rows are reported here instead of being
    /// dropped.</summary>
    public IReadOnlyList<uint> OrphanRowIds { get; }
}
