namespace TdbIO;

/// <summary>One parsed body line of an <c>AK</c> or <c>BO</c> block of a DSTV NC file.</summary>
public sealed class Nc1Line
{
    internal Nc1Line(char? label, IReadOnlyList<Nc1Token> values)
    {
        Label = label;
        Values = values;
    }

    /// <summary>The single lower-case letter that starts the line; its meaning is not known. It is set on the first line
    /// of an <c>AK</c> block and on every <c>BO</c> line, and is null on the other <c>AK</c> lines.</summary>
    public char? Label { get; }

    /// <summary>The numbers of the line in order: 7 for <c>AK</c>, 3 for <c>BO</c>.</summary>
    public IReadOnlyList<Nc1Token> Values { get; }
}
