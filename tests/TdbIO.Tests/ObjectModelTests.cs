using System.Buffers.Binary;
using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class ObjectModelTests
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
    public void ReadModelThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => ModelBuilder.Build(null!));
    }

    [Fact]
    public void ReadModelDecodesSyntheticModelCorrectly()
    {
        var partGuid = Guid.NewGuid();
        var assGuid = Guid.NewGuid();
        var boltGuid = Guid.NewGuid();
        var weldGuid = Guid.NewGuid();
        var secPartGuid = Guid.NewGuid();

        // 1. Table 1 (object)
        var objRow1 = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100); // Id = 100 (Assembly)
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 0);   // ParentId
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100); // AssemblyId
            assGuid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 2);  // Type = 2 (Assembly)
        });
        var objRow2 = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 101); // Id = 101 (Main Part)
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 0);   // ParentId
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100); // AssemblyId = 100
            partGuid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 1);  // Type = 1 (Part)
        });
        var objRow3 = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 102); // Id = 102 (Secondary Part)
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 0);   // ParentId
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100); // AssemblyId = 100
            secPartGuid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 1);  // Type = 1 (Part)
        });
        var objRow4 = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 103); // Id = 103 (Bolt)
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 100); // ParentId
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100); // AssemblyId = 100
            boltGuid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 9);  // Type = 9 (Bolt)
        });
        var objRow5 = MakeRow(ObjectRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 104); // Id = 104 (Weld)
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 100); // ParentId
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100); // AssemblyId = 100
            weldGuid.TryWriteBytes(span[12..], bigEndian: true, out _);
            BinaryPrimitives.WriteInt32LittleEndian(span[28..], 13); // Type = 13 (Weld)
        });
        var objSec = MakeSection(ObjectRow.ExpectedRowSize, ObjectRow.TableKey, objRow1, objRow2, objRow3, objRow4, objRow5);

        // 2. Table 2 (assembly)
        var assRow = MakeRow(AssemblyRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 1);   // Type
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 5);   // Class
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 101); // MainPartId = 101
            Encoding.UTF8.GetBytes("COLUMN\0").CopyTo(span[20..]);
        });
        var assSec = MakeSection(AssemblyRow.ExpectedRowSize, AssemblyRow.TableKey, assRow);

        // 3. Table 13 (point)
        var ptRow1 = MakeRow(PointRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 0.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 0.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 0.0);
        });
        var ptRow2 = MakeRow(PointRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 2);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 0.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 0.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 3000.0);
        });
        var ptRow3 = MakeRow(PointRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 3);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 100.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 50.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 500.0);
        });
        var ptSec = MakeSection(PointRow.ExpectedRowSize, PointRow.TableKey, ptRow1, ptRow2, ptRow3);

        // 4. Table 3 (coordsys)
        var csRow = MakeRow(CoordsysRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 10.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[16..], 20.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 30.0);
            BinaryPrimitives.WriteDoubleLittleEndian(span[32..], 1.0);
        });
        var csSec = MakeSection(CoordsysRow.ExpectedRowSize, CoordsysRow.TableKey, csRow);

        // 5. Table 50 (stringpool)
        var spRow1 = MakeRow(StringPoolRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            Encoding.UTF8.GetBytes("300\0").CopyTo(span[16..]);
        });
        var spRow2 = MakeRow(StringPoolRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 2);
            Encoding.UTF8.GetBytes("Special Beam\0").CopyTo(span[16..]);
        });
        var spSec = MakeSection(StringPoolRow.ExpectedRowSize, StringPoolRow.TableKey, spRow1, spRow2);

        // 6. Table 44 (partattr)
        var paRow1 = MakeRow(PartAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 201);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 3); // Class
            Encoding.UTF8.GetBytes("C\0").CopyTo(span[32..]);
            Encoding.UTF8.GetBytes("COLUMN\0").CopyTo(span[54..]);
            Encoding.UTF8.GetBytes("HEA\0").CopyTo(span[116..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[180..], 1); // ParameterStringId = 1 ("300") -> "HEA300"
            Encoding.UTF8.GetBytes("S355JR\0").CopyTo(span[268..]);
            Encoding.UTF8.GetBytes("GALVANIZED\0").CopyTo(span[300..]);
        });
        var paRow2 = MakeRow(PartAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 202);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 4); // Class
            Encoding.UTF8.GetBytes("P\0").CopyTo(span[32..]);
            Encoding.UTF8.GetBytes("PLATE\0").CopyTo(span[54..]);
            Encoding.UTF8.GetBytes("PL10*200\0").CopyTo(span[116..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[180..], 0);
            Encoding.UTF8.GetBytes("S235JR\0").CopyTo(span[268..]);
        });
        var paSec = MakeSection(PartAttrRow.ExpectedRowSize, PartAttrRow.TableKey, paRow1, paRow2);

        // 7. Table 10 (part)
        var partRow1 = MakeRow(PartRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 101); // Id = 101
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 201); // PartAttrId = 201
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 1);  // StartPointId = 1
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 2);  // EndPointId = 2
            BinaryPrimitives.WriteInt32LittleEndian(span[24..], 10); // CoordsysAttrId = 10
            BinaryPrimitives.WriteDoubleLittleEndian(span[56..], 3000.0); // Length
        });
        var partRow2 = MakeRow(PartRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 102); // Id = 102
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 202); // PartAttrId = 202
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 3);
            BinaryPrimitives.WriteDoubleLittleEndian(span[56..], 200.0);
        });
        var partSec = MakeSection(PartRow.ExpectedRowSize, PartRow.TableKey, partRow1, partRow2);

        // 8. Numbering
        var partNumRow = MakeRow(PartNumberingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 101);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 1);   // StartNumber
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 5);  // FPos = 5
            Encoding.UTF8.GetBytes("C/\0").CopyTo(span[20..]);
        });
        var partNumSec = MakeSection(PartNumberingRow.ExpectedRowSize, PartNumberingRow.TableKey, partNumRow);

        var assNumRow = MakeRow(AssemblyNumberingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 1);   // StartNumber
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 10); // FPos = 10
            Encoding.UTF8.GetBytes("A/\0").CopyTo(span[28..]);
        });
        var assNumSec = MakeSection(AssemblyNumberingRow.ExpectedRowSize, AssemblyNumberingRow.TableKey, assNumRow);

        // 9. Table 141 (phase)
        var phaseRow1 = MakeRow(ObjectPhaseRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2); // Phase = 2
        });
        var phaseRow2 = MakeRow(ObjectPhaseRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 101);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);
        });
        var phaseSec = MakeSection(ObjectPhaseRow.ExpectedRowSize, ObjectPhaseRow.TableKey, phaseRow1, phaseRow2);

        // 10. Table 107 (bolt) & Table 140 (hole)
        var boltRow = MakeRow(BoltRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 103);
            BinaryPrimitives.WriteInt32LittleEndian(span[12..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[16..], 3);
        });
        var boltSec = MakeSection(BoltRow.ExpectedRowSize, BoltRow.TableKey, boltRow);

        var holeRow1 = MakeRow(BoltHoleRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 103); // BoltId = 103
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 1);   // No = 1
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 101); // PartId = 101
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 15.0); // Length = 15
        });
        var holeRow2 = MakeRow(BoltHoleRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 103); // BoltId = 103
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 2);   // No = 2
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 102); // PartId = 102
            BinaryPrimitives.WriteDoubleLittleEndian(span[24..], 10.0);
        });
        var holeSec = MakeSection(BoltHoleRow.ExpectedRowSize, BoltHoleRow.TableKey, holeRow1, holeRow2);

        // 11. Table 20 (weld)
        var weldRow = MakeRow(WeldingRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 104);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 999); // CommonAttrId
        });
        var weldSec = MakeSection(WeldingRow.ExpectedRowSize, WeldingRow.TableKey, weldRow);

        // 12. Attributes (Tables 38, 39, 48, 49)
        var numDefRow = MakeRow(NumAttrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 501);
            BinaryPrimitives.WriteDoubleLittleEndian(span[8..], 42.5);
            Encoding.UTF8.GetBytes("MY_WEIGHT\0").CopyTo(span[16..]);
        });
        var numDefSec = MakeSection(NumAttrAttrRow.ExpectedRowSize, NumAttrAttrRow.TableKey, numDefRow);

        var numAttrRow = MakeRow(NumAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 501); // AttrId = 501
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 101); // FatherId = 101
        });
        var numAttrSec = MakeSection(NumAttrRow.ExpectedRowSize, NumAttrRow.TableKey, numAttrRow);

        var strDefRow = MakeRow(StrAttrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 601);
            Encoding.UTF8.GetBytes("USER_NOTE\0").CopyTo(span[8..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[112..], 2); // ValueId = 2 -> "Special Beam"
        });
        var strDefSec = MakeSection(StrAttrAttrRow.ExpectedRowSize, StrAttrAttrRow.TableKey, strDefRow);

        var strAttrRow = MakeRow(StrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 601); // AttrId = 601
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 101); // FatherId = 101
        });
        var strAttrSec = MakeSection(StrAttrRow.ExpectedRowSize, StrAttrRow.TableKey, strAttrRow);

        // Build a mock Db1Document
        var doc = Db1Document.Create(Synthetic.Db1Payload([]));

        // Feed sections directly through internal builder
        var model = ModelBuilder.BuildFromSections(
            doc,
            [objSec, assSec, ptSec, csSec, spSec, paSec, partSec, partNumSec, assNumSec, phaseSec, boltSec, holeSec, weldSec, numDefSec, numAttrSec, strDefSec, strAttrSec]);

        Assert.NotNull(model);
        Assert.Equal(doc.ModelGuid, model.ModelGuid);
        Assert.Same(doc, model.Document);

        // Verify Assembly
        Assert.Single(model.Assemblies);
        var ass = model.Assemblies[0];
        Assert.Equal(100, ass.Id);
        Assert.Equal(assGuid, ass.Guid);
        Assert.Equal("COLUMN", ass.Name);
        Assert.Equal("A/", ass.Prefix);
        Assert.Equal(1, ass.StartNumber);
        Assert.Equal(10, ass.PositionNumber);
        Assert.Equal(2, ass.Phase);
        Assert.Equal("Assembly #100 'COLUMN' Mark=A/10 Parts=2", ass.ToString());

        // Verify Parts & Hierarchy
        Assert.Equal(2, model.Parts.Count);
        var mainPart = model.Parts[0];
        Assert.Equal(101, mainPart.Id);
        Assert.Equal(partGuid, mainPart.Guid);
        Assert.Equal("COLUMN", mainPart.Name);
        Assert.Equal("HEA300", mainPart.Profile);
        Assert.Equal("S355JR", mainPart.Material);
        Assert.Equal("C/", mainPart.Prefix);
        Assert.Equal(1, mainPart.StartNumber);
        Assert.Equal(5, mainPart.PositionNumber);
        Assert.Equal(3, mainPart.Class);
        Assert.Equal("GALVANIZED", mainPart.Finish);
        Assert.Equal(3000.0, mainPart.Length);
        Assert.Equal(2, mainPart.Phase);
        Assert.True(mainPart.IsMainPart);
        Assert.Same(ass, mainPart.Assembly);
        Assert.Same(mainPart, ass.MainPart);

        var secPart = model.Parts[1];
        Assert.Equal(102, secPart.Id);
        Assert.Equal("PL10*200", secPart.Profile);
        Assert.False(secPart.IsMainPart);
        Assert.Same(ass, secPart.Assembly);

        Assert.Equal(2, ass.Parts.Count);
        Assert.Single(ass.Secondaries);
        Assert.Same(secPart, ass.Secondaries[0]);

        // Verify Geometry
        Assert.NotNull(mainPart.StartPoint);
        Assert.Equal(0.0, mainPart.StartPoint.Value.X);
        Assert.Equal(0.0, mainPart.StartPoint.Value.Y);
        Assert.Equal(0.0, mainPart.StartPoint.Value.Z);

        Assert.NotNull(mainPart.EndPoint);
        Assert.Equal(0.0, mainPart.EndPoint.Value.X);
        Assert.Equal(0.0, mainPart.EndPoint.Value.Y);
        Assert.Equal(3000.0, mainPart.EndPoint.Value.Z);

        Assert.NotNull(mainPart.CoordinateSystem);
        Assert.Equal(10.0, mainPart.CoordinateSystem.Value.X);
        Assert.Equal(20.0, mainPart.CoordinateSystem.Value.Y);
        Assert.Equal(30.0, mainPart.CoordinateSystem.Value.Z);

        // Verify Attributes
        Assert.Equal(42.5, mainPart.GetNumericAttribute("MY_WEIGHT"));
        Assert.Equal("Special Beam", mainPart.GetStringAttribute("USER_NOTE"));
        Assert.Null(mainPart.GetStringAttribute("NON_EXISTING"));

        // Verify Bolts
        Assert.Single(model.Bolts);
        var bolt = model.Bolts[0];
        Assert.Equal(103, bolt.Id);
        Assert.Equal(boltGuid, bolt.Guid);
        Assert.Equal(2, bolt.Holes.Count);
        Assert.Equal(2, bolt.ConnectedParts.Count);
        Assert.Contains(mainPart, bolt.ConnectedParts);
        Assert.Contains(secPart, bolt.ConnectedParts);
        Assert.Single(mainPart.Bolts);
        Assert.Same(bolt, mainPart.Bolts[0]);
        Assert.Single(secPart.Bolts);
        Assert.Same(bolt, secPart.Bolts[0]);
        Assert.Single(ass.Bolts);
        Assert.Same(bolt, ass.Bolts[0]);

        // Verify Welds
        Assert.Single(model.Welds);
        var weld = model.Welds[0];
        Assert.Equal(104, weld.Id);
        Assert.Equal(weldGuid, weld.Guid);
        Assert.Equal(999, weld.CommonAttrId);
        Assert.Same(ass, weld.Parent);
        Assert.Same(ass, weld.Assembly);
        Assert.Single(ass.Welds);
        Assert.Same(weld, ass.Welds[0]);

        // Verify Lookups
        Assert.Same(mainPart, model.FindObject(101));
        Assert.Same(mainPart, model.FindObject(partGuid));
        Assert.Same(mainPart, model.FindPart(101));
        Assert.Null(model.FindPart(9999));
        Assert.Same(ass, model.FindAssembly(100));
        Assert.Same(bolt, model.FindBolt(103));
        Assert.Same(weld, model.FindWeld(104));
    }

    [SkippableFact]
    public void RealModelsDecodeObjectModelCorrectly()
    {
        var files = TestFiles.FindAll(".db1", ".bak").Where(f => TdbFile.DetectKind(f) == TdbFileKind.Db1).ToList();
        if (files.Count == 0) throw TestFiles.Missing("model database files");

        foreach (string file in files)
        {
            var doc = (Db1Document)TdbFile.Open(file);
            var model = doc.ReadModel();

            Assert.NotNull(model);
            Assert.Equal(doc.ModelGuid, model.ModelGuid);
            Assert.Same(doc, model.Document);

            // Objects
            Assert.Equal(model.Objects.Count, model.Objects.Select(o => o.Id).Distinct().Count());
            foreach (var obj in model.Objects)
            {
                Assert.True(obj.Id > 0);
                Assert.Same(obj, model.FindObject(obj.Id));
                if (obj.Guid != Guid.Empty)
                    Assert.Same(obj, model.FindObject(obj.Guid));
            }

            // Assemblies
            foreach (var a in model.Assemblies)
            {
                Assert.True(a.Id > 0);
                Assert.NotNull(a.Name);
                Assert.Same(a, model.FindAssembly(a.Id));
                Assert.Same(a, model.FindObject(a.Id));

                if (a.MainPart != null)
                {
                    Assert.Same(a, a.MainPart.Assembly);
                    Assert.True(a.MainPart.IsMainPart);
                    Assert.Contains(a.MainPart, a.Parts);
                }

                foreach (var sec in a.Secondaries)
                {
                    Assert.Same(a, sec.Assembly);
                    Assert.False(sec.IsMainPart);
                    Assert.Contains(sec, a.Parts);
                }

                foreach (var b in a.Bolts)
                    Assert.True(b.ConnectedParts.Any(p => p.Assembly == a) || b.Assembly == a);

                foreach (var w in a.Welds)
                    Assert.True(w.Assembly == a || w.Parent == a);
            }

            // Parts
            foreach (var p in model.Parts)
            {
                Assert.True(p.Id > 0);
                Assert.NotNull(p.Name);
                Assert.NotNull(p.Profile);
                Assert.NotNull(p.Material);
                Assert.Same(p, model.FindPart(p.Id));
                Assert.Same(p, model.FindObject(p.Id));

                if (p.Assembly != null)
                    Assert.Contains(p, p.Assembly.Parts);

                if (p.StartPoint.HasValue)
                {
                    var pt = p.StartPoint.Value;
                    Assert.True(double.IsFinite(pt.X) && double.IsFinite(pt.Y) && double.IsFinite(pt.Z));
                }
                if (p.EndPoint.HasValue)
                {
                    var pt = p.EndPoint.Value;
                    Assert.True(double.IsFinite(pt.X) && double.IsFinite(pt.Y) && double.IsFinite(pt.Z));
                }

                foreach (var b in p.Bolts)
                    Assert.Contains(p, b.ConnectedParts);
            }

            // Bolts
            foreach (var b in model.Bolts)
            {
                Assert.True(b.Id > 0);
                Assert.Same(b, model.FindBolt(b.Id));
                Assert.Same(b, model.FindObject(b.Id));

                foreach (var h in b.Holes)
                {
                    if (h.Part != null)
                        Assert.Contains(h.Part, b.ConnectedParts);
                }
            }

            // Welds
            foreach (var w in model.Welds)
            {
                Assert.True(w.Id > 0);
                Assert.Same(w, model.FindWeld(w.Id));
                Assert.Same(w, model.FindObject(w.Id));
            }
        }
    }
}
