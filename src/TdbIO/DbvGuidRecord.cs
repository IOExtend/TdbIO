namespace TdbIO;

/// <summary>One record of the GUID list of a DBV@ file.</summary>
/// <param name="Value">The first number of the record, raw; its meaning is not known.</param>
/// <param name="Index">The 1-based number of the record; the reader checks that it matches the record's position.</param>
/// <param name="Id">The GUID, stored with its first three groups little-endian.</param>
public readonly record struct DbvGuidRecord(uint Value, uint Index, Guid Id);
