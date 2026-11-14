using System.Buffers.Binary;
using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class TableRowDecoderTests
{
    private delegate void FillSpan(Span<byte> span);

    private static TableRow MakeRow(int size, FillSpan fill)
    {
        var bytes = new byte[size];
        fill(bytes);
        return new TableRow(100, 4, bytes, 1, 2);
    }

    private static TableSection MakeSection(int rowSize, int? key, params TableRow[] rows) =>
        new(0, 0, 100, key, rowSize, [0, 0, 0], 20, rows);

    [Fact]
    public void ObjectRowDecodesCorrectly()
    {
        var guid = Guid.NewGuid();
        var extType = Guid.NewGuid();
        var row = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 12345);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 67890);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 11111);
            guid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[32..], 2);
            extType.TryWriteBytes(span[36..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[52..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[56..], 200);
            BinaryPrimitives.WriteInt32LittleEndian(span[60..], 300);
            BinaryPrimitives.WriteInt32LittleEndian(span[64..], 400);
            BinaryPrimitives.WriteInt32LittleEndian(span[68..], 500);
        });

        var decoded = row.AsObject();
        Assert.Equal(12345, decoded.Id);
        Assert.Equal(67890, decoded.ParentId);
        Assert.Equal(11111, decoded.AssemblyId);
        Assert.Equal(guid, decoded.Guid);
        Assert.Equal(1, decoded.Type);
        Assert.Equal(2, decoded.Subtype);
        Assert.Equal(extType, decoded.ExternalType);
        Assert.Equal(100, decoded.Mask1);
        Assert.Equal(200, decoded.Mask2);
        Assert.Equal(300, decoded.ObjFlag);
        Assert.Equal(400, decoded.OwnerId);
        Assert.Equal(500, decoded.PrivilegeId);
    }

    [Fact]
    public void AssemblyRowDecodesCorrectly()
    {
        var row = MakeRow(AssemblyRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 20);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 30);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 40);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 50);
            Encoding.UTF8.GetBytes("COLUMN\0").CopyTo(span[20..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[84..], 60);
            BinaryPrimitives.WriteInt32LittleEndian(span[88..], 1);
        });

        var decoded = row.AsAssembly();
        Assert.Equal(10, decoded.Id);
        Assert.Equal(20, decoded.Type);
        Assert.Equal(30, decoded.Class);
        Assert.Equal(40, decoded.MainPartId);
        Assert.Equal(50, decoded.Lot);
        Assert.Equal("COLUMN", decoded.Name);
        Assert.Equal(60, decoded.Flags);
        Assert.Equal(1, decoded.MainPartByUser);
    }

    [Fact]
    public void AssemblyRowWithInvalidUtf8SequenceThrowsInvalidDataException()
    {
        var row = MakeRow(AssemblyRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            span[20] = 0xFF;
        });

        var e = Assert.Throws<InvalidDataException>(() => row.AsAssembly());
        Assert.Equal(20, e.Offset);
    }

    [Fact]
    public void CoordsysRowDecodesCorrectly()
    {
        var row = MakeRow(CoordsysRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 100.5);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 200.75);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 300.125);
            BinaryPrimitives.WriteDoubleLittleEndian(span[32..], 1000.0);
        });

        var decoded = row.AsCoordsys();
        Assert.Equal(1, decoded.Id);
        Assert.Equal(2, decoded.AttrId);
        Assert.Equal(100.5, decoded.X);
        Assert.Equal(200.75, decoded.Y);
        Assert.Equal(300.125, decoded.Z);
        Assert.Equal(1000.0, decoded.Length);
    }

    [Fact]
    public void PartRowDecodesCorrectly()
    {
        var row = MakeRow(PartRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 101);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 102);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 103);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 104);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 105);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 106);
            BinaryPrimitives.WriteInt32LittleEndian(span[24..], 107);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[32..], 10.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[40..], 20.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[48..], 30.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[56..], 500.0);
        });

        var decoded = row.AsPart();
        Assert.Equal(101, decoded.Id);
        Assert.Equal(102, decoded.PartAttrId);
        Assert.Equal(103, decoded.PositioningAttrId);
        Assert.Equal(104, decoded.StartPointId);
        Assert.Equal(105, decoded.EndPointId);
        Assert.Equal(106, decoded.GeometryTreeRootId);
        Assert.Equal(107, decoded.CoordsysAttrId);
        Assert.Equal(0, decoded.Reserved);
        Assert.Equal(10.0, decoded.CoordsysX);
        Assert.Equal(20.0, decoded.CoordsysY);
        Assert.Equal(30.0, decoded.CoordsysZ);
        Assert.Equal(500.0, decoded.Length);
    }

    [Fact]
    public void PointRowDecodesCorrectly()
    {
        var row = MakeRow(PointRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 777);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 4);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 1234.5);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], -6789.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 0.0);
        });

        var decoded = row.AsPoint();
        Assert.Equal(777, decoded.Id);
        Assert.Equal(4, decoded.Visibility);
        Assert.Equal(1234.5, decoded.X);
        Assert.Equal(-6789.0, decoded.Y);
        Assert.Equal(0.0, decoded.Z);
    }

    [Fact]
    public void RelationRowDecodesCorrectly()
    {
        var row = MakeRow(RelationRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 55);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 61000);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 11);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 22);
            Encoding.UTF8.GetBytes("7b4b9794-f4b8-4040-b87b-3da518911e24\0").CopyTo(span[16..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[56..], 99);
        });

        var decoded = row.AsRelation();
        Assert.Equal(55, decoded.Id);
        Assert.Equal(61000, decoded.Type);
        Assert.Equal(11, decoded.Id1);
        Assert.Equal(22, decoded.Id2);
        Assert.Equal("7b4b9794-f4b8-4040-b87b-3da518911e24", decoded.Guid);
        Assert.Equal(99, decoded.Flags);
    }

    [Fact]
    public void WeldingRowDecodesCorrectly()
    {
        var row = MakeRow(WeldingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 3);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 4);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 5);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 6);
        });

        var decoded = row.AsWelding();
        Assert.Equal(1, decoded.Id);
        Assert.Equal(2, decoded.CommonAttrId);
        Assert.Equal(3, decoded.Seam1Id);
        Assert.Equal(4, decoded.Seam2Id);
        Assert.Equal(5, decoded.CompoundSeam1Id);
        Assert.Equal(6, decoded.CompoundSeam2Id);
    }

    [Fact]
    public void BoltRowDecodesCorrectly()
    {
        var row = MakeRow(BoltRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 20);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 30);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 40);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 50);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 60);
        });

        var decoded = row.AsBolt();
        Assert.Equal(10, decoded.Id);
        Assert.Equal(20, decoded.BoltAttrId);
        Assert.Equal(30, decoded.PositioningAttrId);
        Assert.Equal(40, decoded.FirstPointId);
        Assert.Equal(50, decoded.SecondPointId);
        Assert.Equal(60, decoded.PolygonId);
    }

    [Fact]
    public void ObjectPhaseRowDecodesCorrectly()
    {
        var row = MakeRow(ObjectPhaseRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 123);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 3);
        });

        var decoded = row.AsObjectPhase();
        Assert.Equal(123, decoded.ObjectId);
        Assert.Equal(3, decoded.PhaseNumber);
    }

    [Fact]
    public void ObjectNumberingRowDecodesCorrectly()
    {
        var row = MakeRow(ObjectNumberingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 500);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 600);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 700);
        });

        var decoded = row.AsObjectNumbering();
        Assert.Equal(500, decoded.Id);
        Assert.Equal(600, decoded.NumberId);
        Assert.Equal(700, decoded.NumberingGroup);
    }

    [Fact]
    public void ObjectModificationRowDecodesCorrectly()
    {
        var row = MakeRow(ObjectModificationRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 3);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 4);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 5);
        });

        var decoded = row.AsObjectModification();
        Assert.Equal(1, decoded.ModificationId);
        Assert.Equal(2, decoded.Id);
        Assert.Equal(3, decoded.ModType);
        Assert.Equal(4, decoded.DependencyId);
        Assert.Equal(5, decoded.ModTime);
    }

    [Fact]
    public void NumAttrAndStrAttrRowsDecodeCorrectly()
    {
        var numRow = MakeRow(NumAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 3);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 4);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 5);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 6);
        });
        var num = numRow.AsNumAttr();
        Assert.Equal(1, num.Id);
        Assert.Equal(2, num.AttrId);
        Assert.Equal(3, num.FatherId);
        Assert.Equal(4, num.OwnerId);
        Assert.Equal(5, num.Flags);
        Assert.Equal(6, num.NameId);

        var strRow = MakeRow(StrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 20);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 30);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 40);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 50);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 60);
        });
        var str = strRow.AsStrAttr();
        Assert.Equal(10, str.Id);
        Assert.Equal(20, str.AttrId);
        Assert.Equal(30, str.FatherId);
        Assert.Equal(40, str.OwnerId);
        Assert.Equal(50, str.Flags);
        Assert.Equal(60, str.NameId);
    }

    [Fact]
    public void PartAttrRowDecodesCorrectly()
    {
        var row = MakeRow(PartAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 4);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[24..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 0);
            Encoding.UTF8.GetBytes("B\0").CopyTo(span[32..]);
            Encoding.UTF8.GetBytes("BEAM\0").CopyTo(span[54..]);
            Encoding.UTF8.GetBytes("HEA300\0").CopyTo(span[116..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[180..], 999);
            Encoding.UTF8.GetBytes("A\0").CopyTo(span[184..]);
            Encoding.UTF8.GetBytes("MAIN_BEAM\0").CopyTo(span[206..]);
            Encoding.UTF8.GetBytes("S355J2\0").CopyTo(span[268..]);
            Encoding.UTF8.GetBytes("PAINTED\0").CopyTo(span[300..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[364..], 7);
            BinaryPrimitives.WriteInt32LittleEndian(span[368..], 2);
        });

        var decoded = row.AsPartAttr();
        Assert.Equal(1, decoded.Id);
        Assert.Equal(2, decoded.ObjType);
        Assert.Equal(0, decoded.FormType);
        Assert.Equal(4, decoded.ObjClass);
        Assert.Equal(1, decoded.AssStartNo);
        Assert.Equal(0, decoded.Flags);
        Assert.Equal(2, decoded.NPoints);
        Assert.Equal(0, decoded.PartFlags);
        Assert.Equal("B", decoded.Prefix);
        Assert.Equal("BEAM", decoded.Name);
        Assert.Equal("HEA300", decoded.Profile);
        Assert.Equal(999, decoded.ParameterStringId);
        Assert.Equal("A", decoded.AssemblyPosition);
        Assert.Equal("MAIN_BEAM", decoded.AssemblyName);
        Assert.Equal("S355J2", decoded.Material);
        Assert.Equal("PAINTED", decoded.Finish);
        Assert.Equal(7, decoded.AssemblyType);
        Assert.Equal(2, decoded.PourPhase);
    }

    [Fact]
    public void AttrDefinitionRowsDecodeCorrectly()
    {
        var numAttrRow = MakeRow(NumAttrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 42.5);
            Encoding.UTF8.GetBytes("DIAMETER\0").CopyTo(span[16..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[40..], 99);
        });
        var num = numAttrRow.AsNumAttrAttr();
        Assert.Equal(1, num.Id);
        Assert.Equal(2, num.Type);
        Assert.Equal(42.5, num.Value);
        Assert.Equal("DIAMETER", num.Name);
        Assert.Equal(99, num.PrivilegeId);

        var strAttrRow = MakeRow(StrAttrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 3);
            Encoding.UTF8.GetBytes("COMMENT\0").CopyTo(span[8..]);
            Encoding.UTF8.GetBytes("Important part\0").CopyTo(span[29..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[112..], 500);
            BinaryPrimitives.WriteInt32LittleEndian(span[116..], 0);
        });
        var str = strAttrRow.AsStrAttrAttr();
        Assert.Equal(10, str.Id);
        Assert.Equal(3, str.Type);
        Assert.Equal("COMMENT", str.Name);
        Assert.Equal("Important part", str.Value);
        Assert.Equal(500, str.ValueId);
        Assert.Equal(0, str.PrivilegeId);
    }

    [Fact]
    public void StringPoolRowDecodesCorrectly()
    {
        var row = MakeRow(StringPoolRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], -1);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 12345);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 1);
            Encoding.UTF8.GetBytes("Hello Tekla\0").CopyTo(span[16..]);
        });

        var decoded = row.AsStringPool();
        Assert.Equal(100, decoded.Id);
        Assert.Equal(-1, decoded.NextId);
        Assert.Equal(12345, decoded.HashId);
        Assert.Equal(1, decoded.RefCount);
        Assert.Equal("Hello Tekla", decoded.Text);
    }

    [Fact]
    public void NumberingRowsDecodeCorrectly()
    {
        var partNumRow = MakeRow(PartNumberingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 5);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 0);
            Encoding.UTF8.GetBytes("P-\0").CopyTo(span[20..]);
        });
        var pnum = partNumRow.AsPartNumbering();
        Assert.Equal(1, pnum.Id);
        Assert.Equal(0, pnum.SeriesId);
        Assert.Equal(100, pnum.StartNumber);
        Assert.Equal(5, pnum.FPos);
        Assert.Equal(0, pnum.MultiFPos);
        Assert.Equal("P-", pnum.Prefix);

        var assNumRow = MakeRow(AssemblyNumberingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 2);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 200);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..], 0);
            BinaryPrimitives.WriteInt32LittleEndian(span[24..], 0);
            Encoding.UTF8.GetBytes("B-\0").CopyTo(span[28..]);
        });
        var anum = assNumRow.AsAssemblyNumbering();
        Assert.Equal(2, anum.Id);
        Assert.Equal(0, anum.SeriesId);
        Assert.Equal(200, anum.StartNumber);
        Assert.Equal(10, anum.FPos);
        Assert.Equal("B-", anum.Prefix);
    }

    [Fact]
    public void BoltHolesRowsDecodeCorrectly()
    {
        var holeRow = MakeRow(BoltHoleRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 200);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 15.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 30.0);
            BinaryPrimitives.WriteInt32LittleEndian(span[32..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[36..], 2);
            BinaryPrimitives.WriteDoubleLittleEndian(span[40..], -5.0);
        });
        var hole = holeRow.AsBoltHole();
        Assert.Equal(100, hole.BoltId);
        Assert.Equal(1, hole.No);
        Assert.Equal(200, hole.PartId);
        Assert.Equal(15.0, hole.PointZ);
        Assert.Equal(30.0, hole.Length);
        Assert.Equal(1, hole.LongHoleDir);
        Assert.Equal(2, hole.LongHoleType);
        Assert.Equal(-5.0, hole.ShorteningLength);

        var ptsRow = MakeRow(BoltHolePointsRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 1.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 2.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 3.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[32..], 4.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[40..], 5.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[48..], 6.0);
            BinaryPrimitives.WriteInt32LittleEndian(span[56..], 8);
        });
        var pts = ptsRow.AsBoltHolePoints();
        Assert.Equal(100, pts.BoltId);
        Assert.Equal(1.0, pts.IntPoint1X);
        Assert.Equal(2.0, pts.IntPoint1Y);
        Assert.Equal(3.0, pts.IntPoint1Z);
        Assert.Equal(4.0, pts.IntPoint2X);
        Assert.Equal(5.0, pts.IntPoint2Y);
        Assert.Equal(6.0, pts.IntPoint2Z);
        Assert.Equal(8, pts.NumHoles);
    }

    [Fact]
    public void BodyTooShortThrowsArgumentException()
    {
        var shortRow = MakeRow(10, _ => { });
        Assert.Throws<ArgumentException>(() => shortRow.AsObject());
        Assert.Throws<ArgumentException>(() => shortRow.AsPoint());
        Assert.Throws<ArgumentException>(() => shortRow.AsPart());
    }

    [Fact]
    public void SectionRowSizeMismatchThrowsInvalidOperationException()
    {
        var row = MakeRow(32, _ => { });
        var section = MakeSection(32, 13, row);

        // Section has RowSize 32, but AsObjects expects 72
        Assert.Throws<InvalidOperationException>(() => section.AsObjects());
        Assert.Throws<InvalidOperationException>(() => section.AsParts());

        // AsPoints matches 32
        var points = section.AsPoints();
        Assert.Single(points);
    }

    [SkippableFact]
    public void RealModelsDecodeKnownTableSections()
    {
        var files = TestFiles.FindAll(".db1", ".bak").Where(f => TdbFile.DetectKind(f) == TdbFileKind.Db1).ToList();
        if (files.Count == 0) throw TestFiles.Missing("model database files");

        foreach (string file in files)
        {
            var doc = (Db1Document)TdbFile.Open(file);
            var sections = doc.ReadTables();

            foreach (var section in sections)
            {
                if (section.Rows.Count == 0) continue;

                switch (section.Key)
                {
                    case ObjectRow.TableKey when section.RowSize == ObjectRow.ExpectedRowSize:
                        var objects = section.AsObjects();
                        Assert.Equal(section.Rows.Count, objects.Count);
                        Assert.All(objects, o => Assert.NotEqual(Guid.Empty, o.Guid));
                        break;

                    case AssemblyRow.TableKey when section.RowSize == AssemblyRow.ExpectedRowSize:
                        var assemblies = section.AsAssemblies();
                        Assert.Equal(section.Rows.Count, assemblies.Count);
                        Assert.All(assemblies, a => Assert.True(a.Id > 0 && a.Name != null));
                        break;

                    case PointRow.TableKey when section.RowSize == PointRow.ExpectedRowSize:
                        var points = section.AsPoints();
                        Assert.Equal(section.Rows.Count, points.Count);
                        Assert.All(points, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z)));
                        break;

                    case PartRow.TableKey when section.RowSize == PartRow.ExpectedRowSize:
                        var parts = section.AsParts();
                        Assert.Equal(section.Rows.Count, parts.Count);
                        Assert.All(parts, p => Assert.True(p.Id > 0 && p.StartPointId > 0 && p.EndPointId > 0));
                        break;

                    case CoordsysRow.TableKey when section.RowSize == CoordsysRow.ExpectedRowSize:
                        var csys = section.AsCoordsys();
                        Assert.Equal(section.Rows.Count, csys.Count);
                        Assert.All(csys, c => Assert.True(double.IsFinite(c.X) && double.IsFinite(c.Y) && double.IsFinite(c.Z)));
                        break;

                    case BoltRow.TableKey when section.RowSize == BoltRow.ExpectedRowSize:
                        var bolts = section.AsBolts();
                        Assert.Equal(section.Rows.Count, bolts.Count);
                        Assert.All(bolts, b => Assert.True(b.Id > 0 && b.FirstPointId > 0 && b.SecondPointId > 0));
                        break;

                    case WeldingRow.TableKey when section.RowSize == WeldingRow.ExpectedRowSize:
                        var welds = section.AsWeldings();
                        Assert.Equal(section.Rows.Count, welds.Count);
                        Assert.All(welds, w => Assert.True(w.Id > 0));
                        break;

                    case ObjectPhaseRow.TableKey when section.RowSize == ObjectPhaseRow.ExpectedRowSize:
                        var phases = section.AsObjectPhases();
                        Assert.Equal(section.Rows.Count, phases.Count);
                        Assert.All(phases, ph => Assert.True(ph.ObjectId > 0 && ph.PhaseNumber >= 0));
                        break;

                    case RelationRow.TableKey when section.RowSize == RelationRow.ExpectedRowSize:
                        var rels = section.AsRelations();
                        Assert.Equal(section.Rows.Count, rels.Count);
                        Assert.All(rels, r => Assert.True(r.Id > 0 && r.Guid != null));
                        break;

                    case NumAttrRow.TableKey when section.RowSize == NumAttrRow.ExpectedRowSize:
                        var numAttrs = section.AsNumAttrs();
                        Assert.Equal(section.Rows.Count, numAttrs.Count);
                        Assert.All(numAttrs, n => Assert.True(n.Id > 0 && n.AttrId > 0));
                        break;

                    case StrAttrRow.TableKey when section.RowSize == StrAttrRow.ExpectedRowSize:
                        var strAttrs = section.AsStrAttrs();
                        Assert.Equal(section.Rows.Count, strAttrs.Count);
                        Assert.All(strAttrs, s => Assert.True(s.Id > 0 && s.AttrId > 0));
                        break;

                    case PartAttrRow.TableKey when section.RowSize == PartAttrRow.ExpectedRowSize:
                        var partAttrs = section.AsPartAttrs();
                        Assert.Equal(section.Rows.Count, partAttrs.Count);
                        Assert.All(partAttrs, pa => Assert.True(pa.Id > 0 && pa.Profile != null));
                        break;

                    case PartNumberingRow.TableKey when section.RowSize == PartNumberingRow.ExpectedRowSize:
                        var partNums = section.AsPartNumberings();
                        Assert.Equal(section.Rows.Count, partNums.Count);
                        Assert.All(partNums, pn => Assert.True(pn.Id > 0 && pn.Prefix != null));
                        break;

                    case AssemblyNumberingRow.TableKey when section.RowSize == AssemblyNumberingRow.ExpectedRowSize:
                        var assNums = section.AsAssemblyNumberings();
                        Assert.Equal(section.Rows.Count, assNums.Count);
                        Assert.All(assNums, an => Assert.True(an.Id > 0 && an.Prefix != null));
                        break;

                    case ObjectNumberingRow.TableKey when section.RowSize == ObjectNumberingRow.ExpectedRowSize:
                        var objNums = section.AsObjectNumberings();
                        Assert.Equal(section.Rows.Count, objNums.Count);
                        Assert.All(objNums, on => Assert.True(on.Id > 0));
                        break;

                    case ObjectModificationRow.TableKey when section.RowSize == ObjectModificationRow.ExpectedRowSize:
                        var mods = section.AsObjectModifications();
                        Assert.Equal(section.Rows.Count, mods.Count);
                        Assert.All(mods, m => Assert.True(m.ModificationId > 0 && m.Id > 0));
                        break;

                    case BoltHoleRow.TableKey when section.RowSize == BoltHoleRow.ExpectedRowSize:
                        var holes = section.AsBoltHoles();
                        Assert.Equal(section.Rows.Count, holes.Count);
                        Assert.All(holes, h => Assert.True(h.BoltId > 0 && h.PartId > 0));
                        break;

                    case BoltHolePointsRow.TableKey when section.RowSize == BoltHolePointsRow.ExpectedRowSize:
                        var holePts = section.AsBoltHolePoints();
                        Assert.Equal(section.Rows.Count, holePts.Count);
                        Assert.All(holePts, hp => Assert.True(hp.BoltId > 0 && hp.NumHoles > 0));
                        break;

                    case StrAttrAttrRow.TableKey when section.RowSize == StrAttrAttrRow.ExpectedRowSize:
                        var strDefs = section.AsStrAttrAttrs();
                        Assert.Equal(section.Rows.Count, strDefs.Count);
                        Assert.All(strDefs, sd => Assert.True(sd.Id > 0 && sd.Name != null));
                        break;

                    case NumAttrAttrRow.TableKey when section.RowSize == NumAttrAttrRow.ExpectedRowSize:
                        var numDefs = section.AsNumAttrAttrs();
                        Assert.Equal(section.Rows.Count, numDefs.Count);
                        Assert.All(numDefs, nd => Assert.True(nd.Id > 0 && nd.Name != null));
                        break;

                    case StringPoolRow.TableKey when section.RowSize == StringPoolRow.ExpectedRowSize:
                        var strPool = section.AsStringPool();
                        Assert.Equal(section.Rows.Count, strPool.Count);
                        Assert.All(strPool, sp => Assert.True(sp.Id > 0 && sp.Text != null));
                        break;
                }
            }
        }
    }
}
