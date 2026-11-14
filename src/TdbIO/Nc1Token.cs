namespace TdbIO;

/// <summary>One number of an <c>AK</c> or <c>BO</c> line of a DSTV NC file, such as <c>100</c>, <c>-0</c> or
/// <c>0u</c>.</summary>
/// <param name="Text">The token as written, suffix included, so that <c>-0</c> and <c>0u</c> stay as they are.</param>
/// <param name="Number">The value without the suffix.</param>
/// <param name="Suffix">The lower-case letter written right after the number, or null. Its meaning is not known.</param>
public readonly record struct Nc1Token(string Text, decimal Number, char? Suffix);
