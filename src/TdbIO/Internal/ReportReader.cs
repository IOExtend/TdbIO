namespace TdbIO.Internal;

/// <summary>Splits a report into lines on CR LF and pages on a leading form feed.</summary>
internal static class ReportReader
{
    public static (IReadOnlyList<string> Lines, IReadOnlyList<int> PageStarts, bool EndsWithLineBreak) Read(string text)
    {
        var lines = new List<string>();
        var pageStarts = new List<int>();
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf("\r\n", start, StringComparison.Ordinal);
            string line = end < 0 ? text[start..] : text[start..end];
            if (line.StartsWith('\f'))
            {
                pageStarts.Add(lines.Count);
                line = line[1..];
            }
            lines.Add(line);
            start = end < 0 ? text.Length : end + 2;
        }
        return (lines, pageStarts, text.EndsWith("\r\n", StringComparison.Ordinal));
    }
}
