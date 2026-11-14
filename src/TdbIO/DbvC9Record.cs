namespace TdbIO;

/// <summary>One 20-byte record of a DBV@ file that starts with the marker <c>C9 C0 CE DB</c>. The four numbers after the
/// marker are kept raw; their meaning is not known.</summary>
/// <param name="R0">The first number.</param>
/// <param name="R1">The second number.</param>
/// <param name="R2">The third number.</param>
/// <param name="R3">The fourth number.</param>
public readonly record struct DbvC9Record(int R0, int R1, int R2, int R3);
