using TdbIO.Internal;

namespace TdbIO;

/// <summary>An id range file (<c>db.idrm</c>, <c>xslib.idrm</c>): the next free value and the ranges handed out.</summary>
public sealed class IdRangeDocument : TdbDocument
{
    private IdRangeDocument(uint next, IReadOnlyList<IdRange> ranges, IReadOnlyList<string> warnings)
        : base(TdbFileKind.IdRanges, warnings)
    {
        Next = next;
        Ranges = ranges;
    }

    /// <summary>The next free value. A warning is given when it is not the end of the last range.</summary>
    public uint Next { get; }

    /// <summary>The ranges in file order. A warning is given when a range does not start where the one before it
    /// ends.</summary>
    public IReadOnlyList<IdRange> Ranges { get; }

    internal static IdRangeDocument Create(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var warnings = new List<string>();
        var (next, ranges) = IdRangeReader.Read(data, warnings);
        return new IdRangeDocument(next, ranges, warnings);
    }
}
