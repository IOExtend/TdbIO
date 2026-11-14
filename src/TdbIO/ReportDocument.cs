using System.Text;
using TdbIO.Internal;

namespace TdbIO;

/// <summary>A finished report (<c>.xsr</c>): plain text in pages. The columns are not parsed.</summary>
public sealed class ReportDocument : TdbDocument
{
    internal ReportDocument(string encodingName, IReadOnlyList<string> lines, IReadOnlyList<int> pageStarts, bool endsWithLineBreak)
        : base(TdbFileKind.Report, [])
    {
        EncodingName = encodingName;
        Lines = lines;
        PageStarts = pageStarts;
        EndsWithLineBreak = endsWithLineBreak;
    }

    /// <summary>The encoding used to read the bytes: <c>ascii</c>, <c>utf-8</c>, <c>cp1251</c>, <c>cp1252</c>, or the
    /// <see cref="Encoding.WebName"/> of <see cref="TdbReadOptions.TextEncoding"/> when it was set. One encoding is used for
    /// the whole file; bytes that do not fit it are not changed.</summary>
    public string EncodingName { get; }

    /// <summary>The lines split on CR LF, without the form feed that starts a page. A lone CR or LF stays in the line.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>The indexes in <see cref="Lines"/> of the lines that began with a form feed (U+000C), that is, the first
    /// lines of every page after the first.</summary>
    public IReadOnlyList<int> PageStarts { get; }

    /// <summary>True when the text ends with CR LF.</summary>
    public bool EndsWithLineBreak { get; }

    internal static ReportDocument Create(byte[] data, Encoding? forced)
    {
        if (data.Length > ReportReader.MaxLength)
            throw FormatError.At("report is larger than the 16 MiB limit", ReportReader.MaxLength);
        string text = TextDecoding.Decode(data, forced, out string encodingName);
        var (lines, pageStarts, endsWithLineBreak) = ReportReader.Read(text);
        return new ReportDocument(encodingName, lines, pageStarts, endsWithLineBreak);
    }
}
