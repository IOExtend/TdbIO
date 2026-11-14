using System.Globalization;
using System.Text;

namespace TdbIO.Internal;

/// <summary>Reads the unpacked bytes of a DBV@ file:
/// <c>"DBV@" n name P[6] marker A[4] records B C K L1[K] L2[K] T[6] C9[T5] sections</c>. Nothing is aligned. Values that
/// differ from the ones always seen are kept with a warning; broken structure is an error.</summary>
internal static class DbvReader
{
    private const uint ListMarker = 0xDBCEC0BC;
    private const uint C9Marker = 0xDBCEC0C9;
    private const int RecordLength = 24;
    private const int C9Length = 20;

    // Signature, name length, a one-byte name, the prologue, the list marker and the list header.
    private const int MinLength = 8 + 1 + 24 + 4 + 16;

    public static DbvDocument Read(byte[] payload, List<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ReadOnlySpan<byte> d = payload;
        long length = d.Length;
        if (!d.StartsWith("DBV@"u8)) throw FormatError.At("not a DBV@ file", 0);
        if (length < MinLength) throw FormatError.At(Invariant($"DBV@ data of {length} bytes is shorter than {MinLength}"), 0);

        uint n = Bytes.U32(d, 4);
        if (n < 1 || 8L + n + 24 + 4 > length) throw FormatError.At(Invariant($"DBV@ name length {n} is out of range"), 4);
        ReadOnlySpan<byte> nameBytes = d.Slice(8, (int)n);
        int bad = nameBytes.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7E);
        if (bad >= 0) throw FormatError.At("DBV@ name has a byte that is not printable ASCII", 8 + bad);
        string name = Encoding.ASCII.GetString(nameBytes);

        long p = 8 + n;
        var prologue = ReadU32s(d, p, 6);
        Expect(warnings, prologue[0] == 1, "prologue word P[0]", p);
        for (int i = 2; i <= 4; i++) Expect(warnings, prologue[i] == 0, Invariant($"prologue word P[{i}]"), p + 4 * i);
        Expect(warnings, prologue[5] == 1, "prologue word P[5]", p + 20);
        p += 24;

        if (Bytes.U32(d, p) != ListMarker) throw FormatError.At("unsupported DBV@ layout: the list marker is missing", p);
        p += 4;
        Bytes.Need(p, 16, length, "DBV@ list header");
        var listHeader = ReadU32s(d, p, 4);
        Expect(warnings, listHeader[0] == 0, "list header word A[0]", p);
        Expect(warnings, listHeader[1] == 1, "list header word A[1]", p + 4);
        Expect(warnings, listHeader[2] == 13, "list header word A[2]", p + 8);
        if (listHeader[3] == 0) throw FormatError.At("DBV@ record count word A[3] is 0", p + 12);
        long recordCount = listHeader[3] - 1L;
        p += 16;

        Bytes.Need(p, RecordLength * recordCount, length, "DBV@ GUID records");
        var records = new DbvGuidRecord[recordCount];
        for (int k = 0; k < records.Length; k++, p += RecordLength)
        {
            uint index = Bytes.U32(d, p + 4);
            if (index != k + 1L)
                throw FormatError.At(Invariant($"DBV@ GUID record {k + 1} has the index {index}"), p + 4);
            // .NET reads the first three groups little-endian, which is how the file stores them.
            records[k] = new DbvGuidRecord(Bytes.U32(d, p), index, new Guid(d.Slice((int)p + 8, 16)));
        }

        Bytes.Need(p, 12, length, "DBV@ list lengths");
        uint b = Bytes.U32(d, p), c = Bytes.U32(d, p + 4), k32 = Bytes.U32(d, p + 8);
        Expect(warnings, c == 5, "word C", p + 4);
        long listLength = k32;
        Bytes.Need(p, 12 + 8 * listLength + 24, length, "DBV@ lists");
        p += 12;
        var l1 = ReadU32s(d, p, (int)listLength);
        p += 4 * listLength;
        var l2 = new int[listLength];
        for (int i = 0; i < l2.Length; i++, p += 4) l2[i] = Bytes.I32(d, p);

        var t = ReadU32s(d, p, 6);
        if (t[0] != listHeader[3])
            throw FormatError.At(Invariant($"DBV@ word T[0] = {t[0]} differs from the record count word A[3] = {listHeader[3]}"), p);
        Expect(warnings, t[1] == b + 32L * listLength, "word T[1]", p + 4);
        Expect(warnings, t[4] == t[1] - 32L, "word T[4]", p + 16);
        p += 24;

        if (t[5] > (length - p) / C9Length)
            throw FormatError.At(Invariant($"DBV@ C9 record count T[5] = {t[5]} is more than the data can hold"), p - 4);
        var c9 = new DbvC9Record[t[5]];
        for (int i = 0; i < c9.Length; i++, p += C9Length)
        {
            if (Bytes.U32(d, p) != C9Marker) throw FormatError.At(Invariant($"DBV@ C9 record {i} has no C9 marker"), p);
            c9[i] = new DbvC9Record(Bytes.I32(d, p + 4), Bytes.I32(d, p + 8), Bytes.I32(d, p + 12), Bytes.I32(d, p + 16));
            Expect(warnings, c9[i] == new DbvC9Record(0, 1, 0, -1), Invariant($"C9 record {i}"), p + 4);
        }

        var sections = SectionWalker.Walk(payload, (int)p, 0, SectionMode.Dbv, warnings);
        return new DbvDocument(warnings.AsReadOnly(), name, Array.AsReadOnly(prologue), Array.AsReadOnly(listHeader),
            Array.AsReadOnly(records), b, c, Array.AsReadOnly(l1), Array.AsReadOnly(l2), Array.AsReadOnly(t), Array.AsReadOnly(c9),
            sections, payload);
    }

    private static uint[] ReadU32s(ReadOnlySpan<byte> d, long at, int count)
    {
        var values = new uint[count];
        for (int i = 0; i < count; i++) values[i] = Bytes.U32(d, at + 4L * i);
        return values;
    }

    // Values other than the ones always seen are kept; the warning names the field and offset, never file text.
    private static void Expect(List<string> warnings, bool usual, string what, long at)
    {
        if (!usual) warnings.Add(Invariant($"DBV@ {what} at offset 0x{at:X} has an unusual value"));
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
