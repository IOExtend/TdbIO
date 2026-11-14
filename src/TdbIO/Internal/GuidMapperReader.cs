namespace TdbIO.Internal;

/// <summary>Reads a <c>guid.mapper</c> file: a header line starting with <c>!</c>, then lines of two lower-case GUIDs
/// separated by one space. Every line ends with LF only.</summary>
internal static class GuidMapperReader
{
    private const int PairLineLength = 73;

    public static (ReadOnlyMemory<byte> Header, IReadOnlyList<GuidPair> Pairs) Read(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        if (span.Length == 0 || span[0] != (byte)'!') throw FormatError.At("GUID map does not start with '!'", 0, 1);
        int headerEnd = span.IndexOf((byte)'\n');
        if (headerEnd < 0) throw FormatError.At("GUID map line does not end with LF", 0, 1);
        // CR LF line ends have not been seen; they are rejected rather than accepted silently.
        if (span[..headerEnd].EndsWith("\r"u8)) throw FormatError.At("GUID map line ends with CR LF", 0, 1);

        var pairs = new List<GuidPair>((span.Length - headerEnd) / (PairLineLength + 1));
        var firsts = new HashSet<Guid>();
        int line = 1;
        for (int p = headerEnd + 1; p < span.Length; p += PairLineLength + 1)
        {
            line++;
            int length = span[p..].IndexOf((byte)'\n');
            if (length < 0) throw FormatError.At("GUID map line does not end with LF", p, line);
            var text = span.Slice(p, length);
            if (text.StartsWith("!"u8)) throw FormatError.At("GUID map has a '!' line after the first line", p, line);
            if (text.Contains((byte)'\r')) throw FormatError.At("GUID map line contains CR", p, line);
            if (length != PairLineLength || text[36] != (byte)' '
                || !XsteelHeader.TryParseLowerGuid(text[..36], out Guid first)
                || !XsteelHeader.TryParseLowerGuid(text[37..], out Guid second))
                throw FormatError.At("GUID map line is not two lower-case GUIDs separated by one space", p, line);
            if (!firsts.Add(first)) throw FormatError.At("GUID map repeats a first GUID", p, line);
            pairs.Add(new GuidPair(first, second));
        }
        return (data[..headerEnd], pairs);
    }
}
