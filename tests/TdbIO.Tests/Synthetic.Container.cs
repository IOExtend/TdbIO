namespace TdbIO.Tests;

/// <summary>Builders for model containers, text sections and the files next to the model. All values are made up.</summary>
internal static partial class Synthetic
{
    public const string ContainerGuidText = "0f1e2d3c-4b5a-4968-8778-a1b2c3d4e5f6";

    public static byte[] TextSectionSignature =>
    [
        0x66, 0xC0, 0xCE, 0xDB, 0x2D, 0, 0, 0, 0x06, 0, 0, 0,
        0x01, 0, 0, 0, 0x01, 0, 0, 0, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    public static byte[] TextSectionCloser => [0x4F, 0x61, 0xBC, 0x00];

    /// <summary>The 49-byte header of a model container.</summary>
    public static byte[] XsteelHead(byte type = 0x85, string version = "9.99", string guid = ContainerGuidText) =>
        Concat(Ascii("Xsteel"), [type], Ascii($" {version} {guid}"));

    /// <summary>The id map that follows the header of a <c>.db1</c>, ending with the u32 5.</summary>
    public static byte[] IdMapBody((uint Sequence, Guid Id, int Value)[] entries, uint field65 = 0x0D, uint field73 = 1,
        uint? count = null, uint after = 5, uint first = 1, uint marker = 0xDBCEC0BC) =>
        Concat(
            Le32(first), Le32(marker), Le32(0u), Le32(1u), Le32(field65), Le32(count ?? (uint)entries.Length + 1), Le32(field73),
            Concat(entries.Select(e => Concat(Le32(e.Sequence), e.Id.ToByteArray(), Le32(e.Value))).ToArray()),
            Le32(after));

    /// <summary>A <c>.db1</c> payload: header, id map and whatever follows.</summary>
    public static byte[] Db1Payload((uint Sequence, Guid Id, int Value)[] entries, params byte[][] rest) =>
        Concat(XsteelHead(), IdMapBody(entries), Concat(rest));

    /// <summary>One 54-byte text row; <paramref name="text"/> fills the 29-byte text field from the start.</summary>
    public static byte[] TextRow(uint id, uint link, uint tag, byte[] text, uint field13 = 0, byte marker = 0x04)
    {
        if (text.Length > 29) throw new ArgumentException("the text field holds 29 bytes", nameof(text));
        var field = new byte[29];
        text.CopyTo(field, 0);
        return Concat([marker], Le32(id), Le32(link), Le32(tag), Le32(field13), field, new byte[8]);
    }

    /// <summary>The rows of one chain holding <paramref name="text"/>: 27 bytes per row, the last row up to 27 bytes and a
    /// NUL. Ids run from <paramref name="firstId"/> in chain order.</summary>
    public static List<byte[]> ChainRows(uint firstId, uint headTag, byte[] text)
    {
        var rows = new List<byte[]>();
        int at = 0;
        uint id = firstId;
        do
        {
            int take = Math.Min(27, text.Length - at);
            bool last = at + take == text.Length;
            rows.Add(TextRow(id, last ? 0 : id + 1, rows.Count == 0 ? headTag : 0xFFFFFFFF, text[at..(at + take)]));
            at += take;
            id++;
        } while (at < text.Length);
        return rows;
    }

    /// <summary>A text section: signature, rows, the 00 marker and the closer.</summary>
    public static byte[] TextSectionBytes(IEnumerable<byte[]> rows) => Concat(TextSectionSignature, Concat(rows.ToArray()), [0], TextSectionCloser);

    /// <summary>What a save stamp stores for <paramref name="revealed"/>: <c>Save:</c>, then the text reversed with letters
    /// shifted by 13 places (both steps undo themselves).</summary>
    public static byte[] HiddenSaveText(string revealed)
    {
        var bytes = Ascii(revealed).Select(c => c switch
        {
            >= (byte)'A' and <= (byte)'Z' => (byte)('A' + (c - 'A' + 13) % 26),
            >= (byte)'a' and <= (byte)'z' => (byte)('a' + (c - 'a' + 13) % 26),
            _ => c,
        }).Reverse().ToArray();
        return Concat(Ascii("Save:"), bytes);
    }

    /// <summary>One <c>.db2</c> record.</summary>
    public static byte[] Db2Record(uint key, uint count, uint rowSize, byte[] rows, uint terminator = 0x00BC614F) =>
        Concat(Le32(key), Le32(count), Le32(rowSize), rows, Le32(terminator));

    /// <summary>A <c>guid.mapper</c> text with LF line ends.</summary>
    public static byte[] GuidMapperText(params string[] pairLines) =>
        Ascii("!Guid mapping for sample.db1\n" + string.Concat(pairLines.Select(l => l + "\n")));

    /// <summary>An unpacked <c>.idrm</c> file.</summary>
    public static byte[] IdRangeFile(uint next, params (uint Start, Guid Id, uint Field20, uint Length)[] ranges) =>
        Concat(Ascii("IdRangeMapper"), Le32(next), Le32(0xDBCEC0E1u), Le32((uint)ranges.Length),
            Concat(ranges.Select(r => Concat(Le32(r.Start), r.Id.ToByteArray(), Le32(r.Field20), Le32(r.Length))).ToArray()));

    /// <summary>The first 100 bytes of an SQLite 3 database.</summary>
    public static byte[] SqliteHead(ushort pageSize, uint pageCount)
    {
        var head = new byte[100];
        Ascii("SQLite format 3\0").CopyTo(head, 0);
        Be16(pageSize).CopyTo(head, 16);
        Be32(pageCount).CopyTo(head, 28);
        return head;
    }
}
