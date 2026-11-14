using TdbIO.Internal;
using Xunit;
using static TdbIO.Tests.Synthetic;

namespace TdbIO.Tests;

public class SectionWalkerTests
{
    private static IReadOnlyList<TableSection> WalkDb1(byte[] data, int firstOrdinal = 0) =>
        SectionWalker.Walk(data, 0, firstOrdinal, SectionMode.Db1, null);

    private static InvalidDataException Fails(byte[] data, SectionMode mode = SectionMode.Db1) =>
        Assert.Throws<InvalidDataException>(() => SectionWalker.Walk(data, 0, 0, mode, []));

    [Fact]
    public void PlainSectionsThenKeyedSectionsWalkToTheEnd()
    {
        byte[] plain0 = Db1Section(null, 8, [1, 0]);
        byte[] plain1 = Db1Section(null, 6, [1, 1, 0], (0x04, Counting(6), 10, 10), (0x0C, Counting(6, 6), 11, 20));
        byte[] keyed2 = Db1Section(7, 4, [1], (0x04, Counting(4), -1, 3));
        byte[] keyed3 = Db1Section(9, 12, [1, 0, 0]);
        byte[] data = Concat(plain0, plain1, keyed2, keyed3);

        var sections = WalkDb1(data, firstOrdinal: 5);

        Assert.Equal(4, sections.Count);
        Assert.Equal(new[] { 5, 6, 7, 8 }, sections.Select(s => s.Ordinal));
        Assert.Equal(new int?[] { null, null, 7, 9 }, sections.Select(s => s.Key));
        Assert.Equal(new[] { 8, 6, 4, 12 }, sections.Select(s => s.RowSize));
        Assert.Equal(0, sections[0].Offset);
        Assert.Equal(plain0.Length, sections[0].End);
        Assert.Equal(sections[0].End, sections[1].Offset);
        Assert.Equal(sections[2].End, sections[3].Offset);
        Assert.Equal(data.Length, sections[3].End);
        Assert.Equal(new uint[] { 1, 1, 0 }, sections[1].Descriptors);

        var rows = sections[1].Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal(sections[1].RowsOffset, rows[0].Offset);
        Assert.Equal(sections[1].RowsOffset + 15, rows[1].Offset);
        Assert.Equal((byte)0x04, rows[0].Marker);
        Assert.Equal((byte)0x0C, rows[1].Marker);
        Assert.Equal(Counting(6, 6), rows[1].Body.ToArray());
        Assert.Equal((11L, 20L), (rows[1].A, rows[1].B));
        Assert.Equal(-1L, sections[2].Rows[0].A);
        Assert.Equal(new[] { rows[0], rows[1] }, rows.ToList());
        // A keyed section starts at its key; its rows start after key, separator, header and descriptors.
        Assert.Equal(sections[2].Offset + 4 + 4 + 8 + 4, sections[2].RowsOffset);
    }

    [Fact]
    public void EmptySectionsHaveTheirFixedSizes()
    {
        uint[] descriptors = [1, 0, 0, 0, 0];
        Assert.Equal(4 + 8 + 4 * 5 + 1, Db1Section(null, 40, descriptors).Length);
        Assert.Equal(4 + 4 + 8 + 4 * 5 + 1 + 4, Db1Section(3, 40, descriptors).Length);

        var sections = WalkDb1(Concat(Db1Section(null, 40, descriptors), Db1Section(3, 40, descriptors)));
        Assert.All(sections, s => Assert.Empty(s.Rows));
        Assert.Equal(33, sections[0].End);
        Assert.Equal(33 + 41, sections[1].End);
    }

    [Fact]
    public void RowSizeNeedNotBeAMultipleOfFour()
    {
        byte[] data = Db1Section(1, 5, [1], (0x04, Counting(5), 1, 1), (0x04, Counting(5, 9), 2, 2));
        var section = Assert.Single(WalkDb1(data));
        Assert.Equal(2, section.Rows.Count);
        Assert.Equal(Counting(5, 9), section.Rows[1].Body.ToArray());
        Assert.Equal(data.Length, section.End);
    }

    [Fact]
    public void LargestRowSizeIsAccepted()
    {
        var section = Assert.Single(WalkDb1(Db1Section(null, 65536, [1], (0x04, new byte[65536], 0, 0))));
        Assert.Equal(65536, section.Rows[0].Body.Length);
    }

    [Fact]
    public void KeyedSectionThenPlainSectionThrows()
    {
        byte[] keyed = Db1Section(3, 4, [1]);
        var e = Fails(Concat(Db1Section(null, 4, [1]), keyed, Db1Section(null, 4, [1])));
        Assert.Equal(keyed.Length + 17, e.Offset);
        Assert.Contains("section 2", e.Message);
        Assert.Contains("without a key", e.Message);
    }

    [Fact]
    public void FourBytesEqualToTheSeparatorStartAPlainSection()
    {
        // After a keyed section, a section whose first word equals the separator is read as one without a key, which is
        // then rejected by the order rule rather than taken as a key.
        var e = Fails(Concat(Db1Section(3, 4, [1]), Db1Section(null, 4, [1])));
        Assert.Contains("without a key", e.Message);
        Assert.Null(Assert.Single(WalkDb1(Db1Section(null, 4, [1]))).Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65537)]
    public void RowSizeOutOfRangeThrows(int rowSize)
    {
        byte[] data = Concat(Db1Section(null, 4, [1]), Db1Section(null, rowSize, [1]));
        var e = Fails(data);
        Assert.Equal(17 + 4, e.Offset);
        Assert.Contains("section 1", e.Message);
        Assert.Contains("row size", e.Message);
    }

    [Fact]
    public void DescriptorCountOutOfRangeThrows()
    {
        Assert.Equal(8, Fails(Db1Section(null, 4, [])).Offset);
        Assert.Equal(8, Fails(Db1Section(null, 2, [1, 0, 0])).Offset);
    }

    [Fact]
    public void DescriptorCountPastTheEndThrows()
    {
        byte[] data = Concat(SectionSeparator, Le32(65536), Le32(65536), Le32(1));
        var e = Fails(data);
        Assert.Equal(12, e.Offset);
        Assert.Contains("descriptors", e.Message);
    }

    [Fact]
    public void DescriptorOtherThanZeroOrOneThrows()
    {
        var e = Fails(Db1Section(null, 4, [1, 2]));
        Assert.Equal(16, e.Offset);
        Assert.Contains("descriptor", e.Message);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x05)]
    [InlineData(0x08)]
    public void RowMarkerOtherThan04Or0CThrows(byte marker)
    {
        var e = Fails(Db1Section(null, 4, [1], (0x04, Counting(4), 0, 0), (marker, Counting(4), 0, 0)));
        Assert.Equal(16 + 13, e.Offset);
        Assert.Contains("marker", e.Message);
    }

    [Fact]
    public void CutRowThrows()
    {
        byte[] data = Db1Section(null, 4, [1], (0x04, Counting(4), 0, 0));
        var e = Fails(data[..^2]);
        Assert.Equal(16, e.Offset);
        Assert.Contains("row runs past the end", e.Message);
    }

    [Fact]
    public void MissingTerminatorThrows()
    {
        byte[] data = Db1Section(null, 4, [1], (0x04, Counting(4), 0, 0));
        var e = Fails(data[..^1]);
        Assert.Equal(data.Length - 1, e.Offset);
        Assert.Contains("without terminator", e.Message);
    }

    [Fact]
    public void MissingOrWrongCloserThrows()
    {
        byte[] data = Db1Section(3, 4, [1]);
        Assert.Contains("closer", Fails(data[..^4]).Message);
        data[^1] = 0xFF;
        var e = Fails(data);
        Assert.Equal(data.Length - 4, e.Offset);
        Assert.Contains("closer", e.Message);
    }

    [Fact]
    public void KeyWithoutSeparatorThrows()
    {
        var e = Fails(Concat(Le32(3), Le32(4), Le32(1)));
        Assert.Equal(4, e.Offset);
        Assert.Contains("separator", e.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TrailingBytesThrow(int extra)
    {
        byte[] section = Db1Section(3, 4, [1]);
        var e = Fails(Concat(section, new byte[extra]));
        Assert.Equal(section.Length, e.Offset);
        Assert.Contains("section 1", e.Message);
    }

    [Fact]
    public void MessageNamesTheOffsetInHex()
    {
        var e = Fails(Concat(Db1Section(null, 4, [1]), Db1Section(null, 0, [1])));
        Assert.EndsWith("at offset 0x15", e.Message);
    }

    [Fact]
    public void RowsAreAViewNotAStoredList()
    {
        const int count = 200_000;
        var body = new byte[] { 0xAB };
        byte[] data = Db1Section(null, 1, [1], Enumerable.Range(0, count).Select(i => ((byte)0x04, body, i, i + 1)).ToArray());
        var section = Assert.Single(WalkDb1(data));
        Assert.IsType<RowList>(section.Rows);
        Assert.Equal(count, section.Rows.Count);
        var last = section.Rows[count - 1];
        Assert.Equal((long)count - 1, last.A);
        Assert.Equal(section.RowsOffset + (count - 1) * 10L, last.Offset);
        Assert.Throws<ArgumentOutOfRangeException>(() => section.Rows[count]);
        Assert.Throws<ArgumentOutOfRangeException>(() => section.Rows[-1]);
    }

    [Fact]
    public void FirstSectionIsFoundAfterTheLead()
    {
        byte[] payload = Db1TablesPayload(Db1Section(null, 4, [1]));
        int first = SectionWalker.FindFirstSection(payload);
        Assert.Equal(payload.Length - 17, first);
        Assert.Equal(payload.Length, SectionWalker.Walk(payload, first, 0, SectionMode.Db1, null)[0].End);
    }

    [Fact]
    public void FirstSectionWithoutLeadThrows()
    {
        byte[] payload = Db1TablesPayload(Db1Section(null, 4, [1]));
        payload[payload.Length - 17 - 1] = 0; // last byte of the lead
        var e = Assert.Throws<InvalidDataException>(() => SectionWalker.FindFirstSection(payload));
        Assert.Equal(payload.Length - 17 - 40, e.Offset);
        Assert.Contains("lead", e.Message);
    }

    [Fact]
    public void FirstSectionTooCloseToTheHeaderThrows()
    {
        byte[] payload = Concat(new byte[49 + 10], Db1Section(null, 4, [1]));
        Assert.Throws<InvalidDataException>(() => SectionWalker.FindFirstSection(payload));
    }

    [Fact]
    public void PayloadWithoutSeparatorThrows()
    {
        Assert.Equal(49, Assert.Throws<InvalidDataException>(() => SectionWalker.FindFirstSection(new byte[200])).Offset);
        Assert.Throws<InvalidDataException>(() => SectionWalker.FindFirstSection(new byte[20]));
    }

    [Fact]
    public void Db1DocumentReadsItsTables()
    {
        byte[] payload = Db1TablesPayload(
            Db1Section(null, 4, [1]),
            Db1Section(10, 3, [1, 0], (0x04, Counting(3), 5, 6)),
            Db1Section(11, 8, [1]));
        var tables = Db1Document.Create(payload).ReadTables();
        Assert.Equal(3, tables.Count);
        Assert.Equal(payload.Length, tables[^1].End);
        Assert.Equal(6, tables[1].Rows[0].B);
    }

    [Fact]
    public void DbvSectionsReadUnsignedNumbersAndAnyType()
    {
        byte[] data = Concat(
            DbvSection(3, [1, 1, 0], (1, Counting(3), 0xFFFFFFFF, 0x80000000), (1, Counting(3, 3), 7, 8)),
            DbvSection(0, []),
            DbvSection(5, [1]));
        var warnings = new List<string>();
        var sections = SectionWalker.Walk(data, 0, 0, SectionMode.Dbv, warnings);
        Assert.Empty(warnings);
        Assert.Equal(3, sections.Count);
        Assert.All(sections, s => Assert.Null(s.Key));
        Assert.Equal(4294967295L, sections[0].Rows[0].A);
        Assert.Equal(2147483648L, sections[0].Rows[0].B);
        Assert.Equal(12 + 12 + 2 * 12 + 1, sections[0].End);
        Assert.Equal(data.Length, sections[2].End);
        Assert.Empty(sections[1].Descriptors);
    }

    [Fact]
    public void DbvGapBetweenSectionsThrows()
    {
        byte[] first = DbvSection(4, [1]);
        var e = Fails(Concat(first, Le32(0x00BC614Fu), DbvSection(4, [1])), SectionMode.Dbv);
        Assert.Equal(first.Length, e.Offset);
        Assert.Contains("section 1", e.Message);
        Assert.Contains("separator", e.Message);
    }

    [Fact]
    public void DbvUnusualTypeAndDescriptorWarn()
    {
        byte[] data = DbvSection(2, [1, 2, 3], (1, Counting(2), 1, 1), (2, Counting(2), 1, 1), (9, Counting(2), 1, 1));
        var warnings = new List<string>();
        var section = Assert.Single(SectionWalker.Walk(data, 0, 0, SectionMode.Dbv, warnings));
        Assert.Equal(3, section.Rows.Count);
        Assert.Equal((byte)2, section.Rows[1].Marker);
        Assert.Equal(new uint[] { 1, 2, 3 }, section.Descriptors);
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("descriptor", StringComparison.Ordinal) && w.Contains("0x10", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("row type", StringComparison.Ordinal) && w.Contains("0x23", StringComparison.Ordinal));
    }

    [Fact]
    public void DbvLimitsThrow()
    {
        Assert.Contains("limit", Fails(Concat(SectionSeparator, Le32(1024u * 1024 + 1), Le32(1u), Le32(1u), [0]), SectionMode.Dbv).Message);
        Assert.Contains("limit", Fails(Concat(SectionSeparator, Le32(4u), Le32(4097u)), SectionMode.Dbv).Message);
        Assert.Contains("descriptors", Fails(Concat(SectionSeparator, Le32(4u), Le32(2u), Le32(1u), [0]), SectionMode.Dbv).Message);
        var largest = SectionWalker.ReadOne(Concat(SectionSeparator, Le32(1024u * 1024), Le32(0u), [0]), 0, 0, SectionMode.Dbv, null);
        Assert.Equal(1024 * 1024, largest.RowSize);
    }

    [Fact]
    public void DbvCutSectionThrows()
    {
        byte[] data = DbvSection(4, [1], (1, Counting(4), 1, 1));
        Assert.Contains("without terminator", Fails(data[..^1], SectionMode.Dbv).Message);
        Assert.Contains("row runs past the end", Fails(data[..^2], SectionMode.Dbv).Message);
        Assert.Contains("section header", Fails(data[..6], SectionMode.Dbv).Message);
    }

    [Fact]
    public void DbvRowMarkerZeroEndsTheRows()
    {
        var section = SectionWalker.ReadOne(Concat(DbvSection(1, [1]), [0xEE]), 0, 4, SectionMode.Dbv, null);
        Assert.Equal(4, section.Ordinal);
        Assert.Equal(17, section.End);
    }
}
