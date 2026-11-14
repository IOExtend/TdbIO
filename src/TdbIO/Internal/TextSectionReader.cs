using System.Buffers;

namespace TdbIO.Internal;

/// <summary>The fixed fields of one 54-byte text row.</summary>
internal readonly record struct TextRow(int Offset, byte Marker, uint Id, uint Link, uint Tag, uint Field13);

/// <summary>Reads the sections of 54-byte text rows in a <c>.db1</c> payload and joins their rows into chains. Each section
/// starts with a fixed 36-byte signature and ends with a 00 marker byte followed by <c>4F 61 BC 00</c>.</summary>
internal static class TextSectionReader
{
    public const int SignatureLength = 36;
    public const int RowLength = 54;
    private const int TextField = 17;
    private const int TextFieldLength = 29;
    private const int PartLength = 27;
    private const uint ContinuationTag = 0xFFFFFFFF;

    private static ReadOnlySpan<byte> Signature =>
    [
        0x66, 0xC0, 0xCE, 0xDB, 0x2D, 0, 0, 0, 0x06, 0, 0, 0,
        0x01, 0, 0, 0, 0x01, 0, 0, 0, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static ReadOnlySpan<byte> Closer => [0x4F, 0x61, 0xBC, 0x00];

    /// <summary>Every text section in file order.</summary>
    public static IReadOnlyList<TextSection> ReadAll(ReadOnlyMemory<byte> payload)
    {
        var sections = new List<TextSection>();
        int from = 0;
        while (from < payload.Length)
        {
            int found = payload.Span[from..].IndexOf(Signature);
            if (found < 0) break;
            var section = ReadAt(payload, from + found, out int end);
            sections.Add(section);
            from = end;
        }
        return sections;
    }

    private static TextSection ReadAt(ReadOnlyMemory<byte> data, int signatureOffset, out int end)
    {
        var span = data.Span;
        Bytes.Need(signatureOffset, SignatureLength, span.Length, "text section signature");
        if (!span.Slice(signatureOffset, SignatureLength).SequenceEqual(Signature))
            throw FormatError.At("no text section signature", signatureOffset);

        var rows = new List<TextRow>();
        int p = signatureOffset + SignatureLength;
        while (true)
        {
            Bytes.Need(p, 1, span.Length, "text section");
            byte marker = span[p];
            if (marker == 0) break;
            if (marker != 0x04) throw FormatError.At($"text row marker 0x{marker:X2} is not 0x04", p);
            Bytes.Need(p, RowLength, span.Length, "text row");
            rows.Add(ReadRow(span, p));
            p += RowLength;
        }
        Bytes.Need(p + 1, Closer.Length, span.Length, "text section closer");
        if (!span.Slice(p + 1, Closer.Length).SequenceEqual(Closer)) throw FormatError.At("text section closer is missing", p + 1);
        end = p + 1 + Closer.Length;

        var index = new Dictionary<uint, int>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
            if (!index.TryAdd(rows[i].Id, i)) throw FormatError.At($"text row id {rows[i].Id} is repeated", rows[i].Offset + 1);

        // Every link is checked, also the links of rows that no head reaches: it must name a row of this section, and no
        // row may be linked from two rows.
        var linkedFrom = new int[rows.Count];
        Array.Fill(linkedFrom, -1);
        for (int i = 0; i < rows.Count; i++)
        {
            TextRow row = rows[i];
            if (row.Link == 0) continue;
            if (!index.TryGetValue(row.Link, out int target))
                throw FormatError.At($"text row {row.Id} links to the missing id {row.Link}", row.Offset + 5);
            if (linkedFrom[target] >= 0) throw FormatError.At($"text row {row.Link} is linked from two rows", row.Offset + 5);
            linkedFrom[target] = i;
        }

        // owner[i] is the chain that holds row i; the walk visits each row at most once, which bounds every chain by the
        // number of rows in the section.
        var owner = new int[rows.Count];
        Array.Fill(owner, -1);
        var chains = new List<TextChain>();
        for (int i = 0; i < rows.Count; i++)
        {
            TextRow head = rows[i];
            if (head.Tag == ContinuationTag) continue;
            if (owner[i] >= 0) throw FormatError.At($"text row {head.Id} is a chain head but is linked from another row", head.Offset);
            int chain = chains.Count;
            var ids = new List<uint>();
            var text = new ArrayBufferWriter<byte>();
            for (int current = i; ;)
            {
                TextRow row = rows[current];
                owner[current] = chain;
                ids.Add(row.Id);
                bool last = row.Link == 0;
                text.Write(Part(span.Slice(row.Offset, RowLength), last, row.Offset));
                if (last) break;
                if (!index.TryGetValue(row.Link, out int next))
                    throw FormatError.At($"text row {row.Id} links to the missing id {row.Link}", row.Offset + 5);
                if (owner[next] == chain) throw FormatError.At($"text chain of head {head.Id} has a cycle", rows[next].Offset);
                if (owner[next] >= 0) throw FormatError.At($"text row {rows[next].Id} is reached from two chains", rows[next].Offset);
                current = next;
            }
            chains.Add(new TextChain(head.Id, head.Tag, head.Field13, ids, text.WrittenSpan.ToArray()));
        }

        // Rows that no head reaches are orphans. Each run of them starts at a row that nothing links to; as no row has two
        // incoming links, the rows still unmarked after these runs can only lie on a cycle.
        const int Orphan = -2;
        for (int i = 0; i < rows.Count; i++)
        {
            if (owner[i] != -1 || linkedFrom[i] >= 0) continue;
            for (int current = i; ;)
            {
                owner[current] = Orphan;
                if (rows[current].Link == 0) break;
                int next = index[rows[current].Link];
                if (owner[next] >= 0)
                    throw FormatError.At($"text row {rows[next].Id} is a chain head but is linked from another row", rows[next].Offset);
                current = next;
            }
        }
        var orphans = new List<uint>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (owner[i] == -1) throw FormatError.At($"text rows from id {rows[i].Id} form a cycle", rows[i].Offset);
            if (owner[i] == Orphan) orphans.Add(rows[i].Id);
        }
        return new TextSection(signatureOffset, rows.Count, chains, orphans);
    }

    /// <summary>The fixed fields of the row at <paramref name="at"/>; the caller has checked that 54 bytes are there.</summary>
    public static TextRow ReadRow(ReadOnlySpan<byte> data, int at) =>
        new(at, data[at], Bytes.U32(data, at + 1), Bytes.U32(data, at + 5), Bytes.U32(data, at + 9), Bytes.U32(data, at + 13));

    /// <summary>The part of the chain text held by one 54-byte row: 27 bytes for a row that links on, the bytes up to the
    /// first NUL for the last row (bytes after it are left-overs and ignored).</summary>
    public static ReadOnlySpan<byte> Part(ReadOnlySpan<byte> row, bool last, long rowOffset)
    {
        var field = row.Slice(TextField, TextFieldLength);
        if (!last) return field[..PartLength];
        int nul = field.IndexOf((byte)0);
        if (nul < 0) throw FormatError.At("last text row has no NUL in its text", rowOffset + TextField);
        return field[..nul];
    }
}
