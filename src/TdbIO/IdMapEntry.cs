namespace TdbIO;

/// <summary>One entry of the id map of a model database (<c>.db1</c>).</summary>
/// <param name="Sequence">The sequence number; the numbers of a map are a permutation of 1 to the entry count, but the
/// entries are not always stored in that order.</param>
/// <param name="Id">The GUID of the entry.</param>
/// <param name="Value">A raw signed value whose meaning is not known.</param>
public readonly record struct IdMapEntry(uint Sequence, Guid Id, int Value);
