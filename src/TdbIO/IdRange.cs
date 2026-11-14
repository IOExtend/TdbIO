namespace TdbIO;

/// <summary>One range of an id range file (<c>.idrm</c>).</summary>
/// <param name="Start">The first value of the range.</param>
/// <param name="Id">The GUID of the range.</param>
/// <param name="Field20">The raw u32 at offset 20 of the range; its meaning is not known.</param>
/// <param name="Length">The number of values in the range.</param>
public readonly record struct IdRange(uint Start, Guid Id, uint Field20, uint Length);
