namespace TdbIO;

/// <summary>One line of a <c>guid.mapper</c> file. The meaning of the two columns is not known.</summary>
/// <param name="First">The first GUID of the line; no two lines share it.</param>
/// <param name="Second">The second GUID of the line.</param>
public readonly record struct GuidPair(Guid First, Guid Second);
