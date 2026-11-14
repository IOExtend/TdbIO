using System.Buffers.Binary;
using TdbIO.Internal;
using Xunit;
using static TdbIO.Tests.Synthetic;

namespace TdbIO.Tests;

public class DbvTests
{
    private static InvalidDataException Fails(byte[] data) => Assert.Throws<InvalidDataException>(() => DbvDocument.Create(data));

    private static byte[] Patch(byte[] data, int at, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), value);
        return data;
    }

    [Fact]
    public void FileWithoutSectionsIsRead()
    {
        var parts = new DbvParts();
        byte[] data = parts.Build();
        var doc = DbvDocument.Create(data);

        Assert.Equal(TdbFileKind.Dbv, doc.Kind);
        Assert.Empty(doc.Warnings);
        Assert.Equal("Abc", doc.Name);
        Assert.Equal(new uint[] { 1, 7, 0, 0, 0, 1 }, doc.Prologue);
        Assert.Equal(new uint[] { 0, 1, 13, 3 }, doc.ListHeader);
        Assert.Equal(2, doc.GuidRecords.Count);
        Assert.Equal(new DbvGuidRecord(100, 1, new Guid("03020100-0504-0706-0809-0a0b0c0d0e0f")), doc.GuidRecords[0]);
        Assert.Equal(new DbvGuidRecord(1, 2, new Guid("43424140-4544-4746-4849-4a4b4c4d4e4f")), doc.GuidRecords[1]);
        Assert.Equal(0x400u, doc.B);
        Assert.Equal(5u, doc.C);
        Assert.Equal(new uint[] { 1, 2 }, doc.L1);
        Assert.Equal(new[] { 32, -32 }, doc.L2);
        Assert.Equal(new uint[] { 3, 0x440, 0, 0, 0x420, 2 }, doc.T);
        Assert.Equal(new[] { new DbvC9Record(0, 1, 0, -1), new DbvC9Record(0, 1, 0, -1) }, doc.C9Records);
        Assert.Empty(doc.Sections);
        Assert.Equal(data, doc.Payload.ToArray());
    }

    [Fact]
    public void FileWithSectionsIsRead()
    {
        var parts = new DbvParts
        {
            Sections = [DbvSection(4, [1, 0], (1, Counting(4), 0x401, 0x402), (1, Counting(4, 4), 0x403, 0x403)), DbvSection(2, [1])],
        };
        byte[] data = parts.Build();
        var doc = DbvDocument.Create(data);

        Assert.Empty(doc.Warnings);
        Assert.Equal(2, doc.Sections.Count);
        Assert.Equal(parts.SectionsOffset, doc.Sections[0].Offset);
        Assert.Equal(new[] { 0, 1 }, doc.Sections.Select(s => s.Ordinal));
        Assert.Equal(data.Length, doc.Sections[1].End);
        var row = doc.Sections[0].Rows[1];
        Assert.Equal((byte)1, row.Marker);
        Assert.Equal(Counting(4, 4), row.Body.ToArray());
        Assert.Equal((0x403L, 0x403L), (row.A, row.B));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Ab")]
    [InlineData("Abcde")]
    [InlineData("Ab cd~")]
    public void NamesOfAnyLengthLeaveTheRestUnaligned(string name)
    {
        var parts = new DbvParts { Name = name, Sections = [DbvSection(1, [1], (1, [9], 0x401, 0x401))] };
        var doc = DbvDocument.Create(parts.Build());
        Assert.Equal(name, doc.Name);
        Assert.Equal(parts.SectionsOffset, doc.Sections[0].Offset);
        Assert.Empty(doc.Warnings);
    }

    [Fact]
    public void FileWithoutItsLastByteThrows()
    {
        byte[] data = new DbvParts { Sections = [DbvSection(4, [1]), DbvSection(8, [1, 0])] }.Build();
        var e = Fails(data[..^1]);
        Assert.Equal(data.Length - 1, e.Offset);
        Assert.Contains("section 1", e.Message);
    }

    [Fact]
    public void WrongSignatureOrShortDataThrows()
    {
        byte[] data = new DbvParts().Build();
        data[3] = (byte)'A';
        Assert.Contains("not a DBV@", Fails(data).Message);
        Assert.Equal(0, Fails(Concat(Ascii("DBV@"), Le32(1u), new byte[44])).Offset);
        Assert.Contains("not a DBV@", Fails([]).Message);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1000u)]
    [InlineData(uint.MaxValue)]
    public void NameLengthOutOfRangeThrows(uint length)
    {
        var e = Fails(Patch(new DbvParts().Build(), 4, length));
        Assert.Equal(4, e.Offset);
        Assert.Contains("name length", e.Message);
    }

    [Theory]
    [InlineData("A\u001FB")]
    [InlineData("A\u007FB")]
    [InlineData("AéB")]
    [InlineData("A\0B")]
    public void NonPrintableNameThrows(string name)
    {
        var e = Fails(new DbvParts { Name = name }.Build());
        Assert.Equal(9, e.Offset);
        Assert.Contains("printable", e.Message);
    }

    [Fact]
    public void MissingListMarkerThrows()
    {
        var parts = new DbvParts { Marker = 0xDBCEC0BD };
        var e = Fails(parts.Build());
        Assert.Equal(parts.MarkerOffset, e.Offset);
        Assert.Contains("unsupported DBV@ layout", e.Message);
    }

    [Fact]
    public void RecordCountWordZeroThrows()
    {
        var parts = new DbvParts { Records = [], ListHeader = [0, 1, 13, 0], T = [0, 0x400, 0, 0, 0x3E0, 2], L1 = [], L2 = [] };
        Assert.Equal(parts.MarkerOffset + 16, Fails(parts.Build()).Offset);
    }

    [Fact]
    public void RecordCountWordOneMeansNoRecords()
    {
        var doc = DbvDocument.Create(new DbvParts { Records = [] }.Build());
        Assert.Empty(doc.GuidRecords);
        Assert.Equal(1u, doc.ListHeader[3]);
        Assert.Equal(1u, doc.T[0]);
    }

    [Fact]
    public void RecordsPastTheEndThrow()
    {
        var parts = new DbvParts { ListHeader = [0, 1, 13, uint.MaxValue] };
        var e = Fails(parts.Build());
        Assert.Equal(parts.MarkerOffset + 20, e.Offset);
        Assert.Contains("GUID records", e.Message);
    }

    [Fact]
    public void RecordIndexOutOfOrderThrows()
    {
        var parts = new DbvParts();
        parts.Records[1] = (1, 3, Counting(16));
        var e = Fails(parts.Build());
        Assert.Equal(parts.MarkerOffset + 20 + 24 + 4, e.Offset);
        Assert.Contains("index", e.Message);
    }

    [Fact]
    public void ListsPastTheEndThrow()
    {
        var parts = new DbvParts();
        var e = Fails(Patch(parts.Build(), parts.BOffset + 8, 0x7FFFFFFF));
        Assert.Equal(parts.BOffset, e.Offset);
        Assert.Contains("lists", e.Message);
    }

    [Fact]
    public void FirstTWordDifferentFromRecordCountWordThrows()
    {
        var parts = new DbvParts();
        parts.T = parts.DefaultT;
        parts.T[0] = 9;
        var e = Fails(parts.Build());
        Assert.Equal(parts.TOffset, e.Offset);
        Assert.Contains("T[0]", e.Message);
    }

    [Fact]
    public void C9RecordWithoutMarkerThrows()
    {
        var parts = new DbvParts();
        parts.C9[1] = (0xDBCEC0C8, [0, 1, 0, -1]);
        var e = Fails(parts.Build());
        Assert.Equal(parts.TOffset + 24 + 20, e.Offset);
        Assert.Contains("C9", e.Message);
    }

    [Fact]
    public void C9CountLargerThanTheDataThrows()
    {
        var parts = new DbvParts();
        parts.T = parts.DefaultT;
        parts.T[5] = 3;
        var e = Fails(parts.Build());
        Assert.Equal(parts.TOffset + 20, e.Offset);
        Assert.Contains("T[5]", e.Message);
    }

    [Fact]
    public void BytesAfterTheC9RecordsMustBeSections()
    {
        var parts = new DbvParts { Sections = [[0, 0, 0, 0]] };
        var e = Fails(parts.Build());
        Assert.Equal(parts.SectionsOffset, e.Offset);
        Assert.Contains("section 0", e.Message);
    }

    [Theory]
    [InlineData("P0")]
    [InlineData("P2")]
    [InlineData("P5")]
    [InlineData("A0")]
    [InlineData("A1")]
    [InlineData("A2")]
    [InlineData("C")]
    [InlineData("T1")]
    [InlineData("T4")]
    [InlineData("R")]
    [InlineData("RowType")]
    public void UnusualValuesAreKeptWithAWarning(string field)
    {
        var parts = new DbvParts { Name = "Secret" };
        switch (field)
        {
            case "P0": parts.Prologue[0] = 2; break;
            case "P2": parts.Prologue[2] = 1; break;
            case "P5": parts.Prologue[5] = 0; break;
            case "A0": parts.ListHeader = [1, 1, 13, 3]; break;
            case "A1": parts.ListHeader = [0, 2, 13, 3]; break;
            case "A2": parts.ListHeader = [0, 1, 14, 3]; break;
            case "C": parts.C = 6; break;
            case "T1": parts.T = [3, 0x500, 0, 0, 0x4E0, 2]; break;
            case "T4": parts.T = [3, 0x440, 0, 0, 0x400, 2]; break;
            case "R": parts.C9[0] = (0xDBCEC0C9, [0, 1, 0, -2]); break;
            case "RowType": parts.Sections = [DbvSection(1, [1], (3, [0], 0x401, 0x401))]; break;
        }
        var doc = DbvDocument.Create(parts.Build());
        string warning = Assert.Single(doc.Warnings);
        Assert.Contains("offset 0x", warning);
        Assert.DoesNotContain("Secret", warning);
        if (field == "C") Assert.Equal(6u, doc.C);
        if (field == "R") Assert.Equal(-2, doc.C9Records[0].R3);
    }

    [Fact]
    public void GzipWrappedFileIsDetectedAndUnpacked()
    {
        byte[] data = new DbvParts { Sections = [DbvSection(4, [1])] }.Build();
        byte[] file = Synthetic.Gzip(data);
        Assert.Equal(TdbFileKind.Dbv, KindDetector.FromRaw(file, null));
        Assert.Equal(TdbFileKind.Unknown, KindDetector.FromRaw(data, null));
        var doc = DbvDocument.Create(TdbIO.Internal.Gzip.Unpack(file, 1 << 20));
        Assert.Single(doc.Sections);
    }
}
