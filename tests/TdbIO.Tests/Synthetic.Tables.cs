using System.Text;

namespace TdbIO.Tests;

/// <summary>Builders for table sections and DBV@ files. All values are made up.</summary>
internal static partial class Synthetic
{
    public static readonly byte[] SectionSeparator = [0x66, 0xC0, 0xCE, 0xDB];
    public static readonly byte[] SectionCloser = [0x4F, 0x61, 0xBC, 0x00];

    /// <summary>The 40 bytes that stand right before the first section of a model database.</summary>
    public static byte[] FirstSectionLead()
    {
        byte[] one = Concat(Le32(0xDBCEC0C9u), Le32(0), Le32(1), Le32(0), Le32(-1));
        return Concat(one, one);
    }

    /// <summary>A <c>.db1</c> section; a key adds the key before and the closer after it.</summary>
    public static byte[] Db1Section(int? key, int rowSize, uint[] descriptors, params (byte Marker, byte[] Body, int A, int B)[] rows)
    {
        var parts = new List<byte[]>();
        if (key is int k) parts.Add(Le32(k));
        parts.Add(SectionSeparator);
        parts.Add(Le32(rowSize));
        parts.Add(Le32(descriptors.Length));
        parts.AddRange(descriptors.Select(Le32));
        foreach (var row in rows) parts.Add(Concat([row.Marker], row.Body, Le32(row.A), Le32(row.B)));
        parts.Add([0]);
        if (key is not null) parts.Add(SectionCloser);
        return Concat([.. parts]);
    }

    /// <summary>A DBV@ section.</summary>
    public static byte[] DbvSection(uint rowSize, uint[] descriptors, params (byte Type, byte[] Body, uint C, uint M)[] rows)
    {
        var parts = new List<byte[]> { SectionSeparator, Le32(rowSize), Le32((uint)descriptors.Length) };
        parts.AddRange(descriptors.Select(Le32));
        foreach (var row in rows) parts.Add(Concat([row.Type], row.Body, Le32(row.C), Le32(row.M)));
        parts.Add([0]);
        return Concat([.. parts]);
    }

    /// <summary>Bytes 0, 1, 2… of the given length, so that each row body differs.</summary>
    public static byte[] Counting(int length, byte start = 0) => Enumerable.Range(0, length).Select(i => (byte)(start + i)).ToArray();

    /// <summary>A <c>.db1</c> payload: header, an id map with no entries, a made-up pre-area, the lead and the sections.</summary>
    public static byte[] Db1TablesPayload(params byte[][] sections) =>
        Concat(Ascii("Xsteel"), [0x85], Ascii(" 1.23 01234567-89ab-cdef-0123-456789abcdef"),
            Le32(1), Le32(0xDBCEC0BCu), Le32(0), Le32(1), Le32(0x0D), Le32(1), Le32(1), Le32(5), Le32(0),
            FirstSectionLead(), Concat(sections));

    /// <summary>The parts of a DBV@ file. Defaults give a valid file with no warnings; tests change one part.</summary>
    public sealed class DbvParts
    {
        public string Name { get; set; } = "Abc";
        public uint[] Prologue { get; set; } = [1, 7, 0, 0, 0, 1];
        public uint Marker { get; set; } = 0xDBCEC0BC;
        public uint[]? ListHeader { get; set; }
        public List<(uint Value, uint Index, byte[] Guid)> Records { get; set; } =
            [(100, 1, Counting(16)), (1, 2, Counting(16, 0x40))];
        public uint B { get; set; } = 0x400;
        public uint C { get; set; } = 5;
        public uint[] L1 { get; set; } = [1, 2];
        public int[] L2 { get; set; } = [32, -32];
        public uint[]? T { get; set; }
        public List<(uint Marker, int[] R)> C9 { get; set; } = [(0xDBCEC0C9, [0, 1, 0, -1]), (0xDBCEC0C9, [0, 1, 0, -1])];
        public List<byte[]> Sections { get; set; } = [];

        public uint[] DefaultListHeader => [0, 1, 13, (uint)Records.Count + 1];

        public uint[] DefaultT
        {
            get
            {
                uint t1 = B + 32 * (uint)L1.Length;
                return [(uint)Records.Count + 1, t1, 0, 0, t1 - 32, (uint)C9.Count];
            }
        }

        /// <summary>The offset of the list marker.</summary>
        public int MarkerOffset => 8 + Encoding.ASCII.GetByteCount(Name) + 24;

        /// <summary>The offset of B, right after the GUID records.</summary>
        public int BOffset => MarkerOffset + 20 + 24 * Records.Count;

        /// <summary>The offset of T[0].</summary>
        public int TOffset => BOffset + 12 + 8 * L1.Length;

        /// <summary>The offset of the first section.</summary>
        public int SectionsOffset => TOffset + 24 + 20 * C9.Count;

        public byte[] Build()
        {
            byte[] name = Encoding.Latin1.GetBytes(Name);
            var parts = new List<byte[]> { Ascii("DBV@"), Le32((uint)name.Length), name };
            parts.AddRange(Prologue.Select(Le32));
            parts.Add(Le32(Marker));
            parts.AddRange((ListHeader ?? DefaultListHeader).Select(Le32));
            foreach (var r in Records) parts.Add(Concat(Le32(r.Value), Le32(r.Index), r.Guid));
            parts.Add(Le32(B));
            parts.Add(Le32(C));
            parts.Add(Le32((uint)L1.Length));
            parts.AddRange(L1.Select(Le32));
            parts.AddRange(L2.Select(Le32));
            parts.AddRange((T ?? DefaultT).Select(Le32));
            foreach (var c9 in C9) parts.Add(Concat([Le32(c9.Marker), .. c9.R.Select(Le32)]));
            parts.AddRange(Sections);
            return Concat([.. parts]);
        }
    }
}
