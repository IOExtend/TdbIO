namespace TdbIO.Internal;

/// <summary>Tells the kind of a file from its first bytes.</summary>
internal static class KindDetector
{
    /// <summary>Bytes inflated from a gzip head to classify it; enough for the longest signature.</summary>
    public const int PeekLength = 32;

    /// <summary>The kind of unpacked gzip data, from its first bytes.</summary>
    public static TdbFileKind FromUnpacked(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith("Xsteel"u8))
        {
            if (head.Length < 7) return TdbFileKind.Unknown;
            return head[6] switch
            {
                0x85 => TdbFileKind.Db1,
                0x80 => TdbFileKind.Db2,
                0x20 => TdbFileKind.Drawing,
                _ => TdbFileKind.Unknown,
            };
        }
        if (head.StartsWith("IdRangeMapper"u8)) return TdbFileKind.IdRanges;
        if (head.StartsWith("!Guid mapping for "u8)) return TdbFileKind.GuidMapper;
        if (head.StartsWith("DBV@"u8)) return TdbFileKind.Dbv;
        return TdbFileKind.Unknown;
    }

    /// <summary>The kind of a file from its first bytes as stored. Gzip data is classified by its unpacked head. A report has
    /// no signature and is recognised by <paramref name="fileName"/> only when the content is not.</summary>
    public static TdbFileKind FromRaw(ReadOnlySpan<byte> head, string? fileName)
    {
        TdbFileKind kind;
        if (Gzip.IsGzip(head))
        {
            Span<byte> unpacked = stackalloc byte[PeekLength];
            int n = Gzip.Peek(head, unpacked);
            kind = FromUnpacked(unpacked[..n]);
        }
        else if (head.StartsWith("SQLite format 3\0"u8)) kind = TdbFileKind.History;
        else if (head.StartsWith("ST\r\n"u8)) kind = TdbFileKind.Nc1;
        else if (IsTemplate(head)) kind = TdbFileKind.Template;
        else kind = TdbFileKind.Unknown;

        // Gzip data is never a report: Open unpacks it and rejects what it does not recognise.
        if (kind == TdbFileKind.Unknown && !Gzip.IsGzip(head) && fileName != null
            && fileName.EndsWith(".xsr", StringComparison.OrdinalIgnoreCase))
            return TdbFileKind.Report;
        return kind;
    }

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    // Optional UTF-8 BOM, blanks (space, CR, LF), then the word "template" as a whole identifier.
    private static bool IsTemplate(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(Utf8Bom)) head = head[3..];
        head = head.TrimStart(" \r\n"u8);
        if (!head.StartsWith("template"u8)) return false;
        return head.Length == 8 || !IsIdentifierByte(head[8]);
    }

    private static bool IsIdentifierByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or (byte)'_';
}
