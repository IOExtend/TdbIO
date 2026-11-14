namespace TdbIO;

/// <summary>One block of a DSTV NC file: a line with a two-letter code and the lines up to the next code.</summary>
public sealed class Nc1Block
{
    internal Nc1Block(string code, int lineNumber, IReadOnlyList<string> rawLines, IReadOnlyList<Nc1Line> lines)
    {
        Code = code;
        LineNumber = lineNumber;
        RawLines = rawLines;
        Lines = lines;
    }

    /// <summary>The block code, two upper-case letters such as <c>ST</c>, <c>AK</c>, <c>BO</c> or <c>EN</c>.</summary>
    public string Code { get; }

    /// <summary>The 1-based line number of the code line.</summary>
    public int LineNumber { get; }

    /// <summary>The body lines as written, without line breaks. Joining the code and these lines of every block with
    /// CR LF, plus a final CR LF, gives the file back.</summary>
    public IReadOnlyList<string> RawLines { get; }

    /// <summary>The parsed body lines of <c>AK</c> and <c>BO</c> blocks; empty for <c>ST</c>, <c>EN</c> and unknown
    /// codes, whose lines are kept only in <see cref="RawLines"/>.</summary>
    public IReadOnlyList<Nc1Line> Lines { get; }
}
