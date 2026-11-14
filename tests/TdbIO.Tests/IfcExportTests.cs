using System.Text;
using TdbIO.Internal.Export;
using Xunit;

namespace TdbIO.Tests;

public sealed class IfcExportTests
{
    [Fact]
    public void StepWriter_EscapesUnicodeAndSpecialChars()
    {
        Assert.Equal(@"\X2\04220435043A043B0430\X0\", StepWriter.Escape("Текла"));
        Assert.Equal("It''s a test", StepWriter.Escape("It's a test"));
        Assert.Equal(@"C:\\Path\\File", StepWriter.Escape(@"C:\Path\File"));
    }

    [Fact]
    public void StepWriter_FormatsRealsCorrectly()
    {
        Assert.Equal("0.", StepWriter.Real(0));
        Assert.Equal("12.5", StepWriter.Real(12.5));
        Assert.Equal("-100.", StepWriter.Real(-100));
    }

    [Fact]
    public void IfcGuid_ProducesStandard22CharGlobalId()
    {
        var guid = Guid.NewGuid();
        string ifcGuid = IfcGuid.ToIfcGuid(guid);
        Assert.Equal(22, ifcGuid.Length);

        // BuildingSMART IFC character set
        foreach (char c in ifcGuid)
        {
            Assert.True(
                char.IsAsciiLetterOrDigit(c) || c == '_' || c == '$',
                $"Character '{c}' is not valid in IFC GUID");
        }
    }

    [Fact]
    public void IfcGuid_DeterministicFromElementId()
    {
        string guid1 = IfcGuid.FromElementId(42, "Part");
        string guid2 = IfcGuid.FromElementId(42, "Part");
        string guidOther = IfcGuid.FromElementId(43, "Part");

        Assert.Equal(22, guid1.Length);
        Assert.Equal(guid1, guid2);
        Assert.NotEqual(guid1, guidOther);
    }

    [Fact]
    public void TeklaModel_ExportsValidIfc2x3()
    {
        var model = CreateSyntheticModel();
        string ifc = model.ToIfc();

        Assert.StartsWith("ISO-10303-21;", ifc);
        Assert.Contains("FILE_SCHEMA(('IFC2X3'));", ifc);
        Assert.Contains("IFCPROJECT", ifc);
        Assert.Contains("IFCSITE", ifc);
        Assert.Contains("IFCBUILDING", ifc);
        Assert.Contains("IFCBUILDINGSTOREY", ifc);
        Assert.Contains("IFCELEMENTASSEMBLY", ifc);
        Assert.Contains("IFCBEAM", ifc);
        Assert.Contains("IFCCOLUMN", ifc);
        Assert.Contains("IFCPLATE", ifc);
        Assert.Contains("IFCMECHANICALFASTENER", ifc);
        Assert.Contains("IFCFASTENER", ifc);
        Assert.Contains("IFCFACETEDBREP", ifc);
        Assert.Contains("IFCPROPERTYSET", ifc);
        Assert.Contains("IFCMATERIAL", ifc);
        Assert.Contains("IFCRELAGGREGATES", ifc);
        Assert.Contains("IFCRELCONTAINEDINSPATIALSTRUCTURE", ifc);
        Assert.Contains("END-ISO-10303-21;", ifc);
    }

    [Fact]
    public void TeklaModel_ExportsValidIfc4()
    {
        var model = CreateSyntheticModel();
        string ifc = model.ToIfc(new TeklaIfcExportOptions { SchemaVersion = "IFC4" });

        Assert.Contains("FILE_SCHEMA(('IFC4'));", ifc);
        Assert.Contains("IFCBEAM", ifc);
        Assert.Contains(".BEAM.", ifc);
        Assert.Contains("END-ISO-10303-21;", ifc);
    }

    [Fact]
    public void TeklaModel_WriteIfc_ToFileAndStream()
    {
        var model = CreateSyntheticModel();

        using var ms = new MemoryStream();
        model.WriteIfc(ms);
        string fromStream = Encoding.UTF8.GetString(ms.ToArray());
        Assert.StartsWith("ISO-10303-21;", fromStream);

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ifc");
        try
        {
            model.WriteIfc(tempPath);
            Assert.True(File.Exists(tempPath));
            string fromFile = File.ReadAllText(tempPath);
            Assert.StartsWith("ISO-10303-21;", fromFile);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static TeklaModel CreateSyntheticModel()
    {
        var p0 = new ModelPoint(1, 0, 0, 0);
        var p1 = new ModelPoint(2, 6000, 0, 0);
        var p2 = new ModelPoint(3, 0, 0, 3000);

        var beam = new ModelPart(
            id: 101,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: 201,
            assemblyId: 201,
            name: "STEEL_BEAM",
            profile: "HEA300",
            material: "S355J2",
            prefix: "B",
            startNumber: 1,
            positionNumber: 1,
            @class: 3,
            finish: "PAINT",
            startPoint: p0,
            endPoint: p1,
            length: 6000,
            coordinateSystem: null,
            stringAttributes: new Dictionary<string, string> { ["COMMENT"] = "Main roof girder" },
            numericAttributes: new Dictionary<string, double> { ["WEIGHT"] = 530.4 });

        var column = new ModelPart(
            id: 102,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: null,
            assemblyId: null,
            name: "COLUMN",
            profile: "HEB300",
            material: "S355J2",
            prefix: "C",
            startNumber: 1,
            positionNumber: 1,
            @class: 4,
            finish: "",
            startPoint: p0,
            endPoint: p2,
            length: 3000,
            coordinateSystem: null);

        var plate = new ModelPart(
            id: 103,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: 201,
            assemblyId: 201,
            name: "END_PLATE",
            profile: "PL10*200",
            material: "S235JR",
            prefix: "p",
            startNumber: 1,
            positionNumber: 5,
            @class: 2,
            finish: "",
            startPoint: p0,
            endPoint: new ModelPoint(4, 200, 0, 0),
            length: 200,
            coordinateSystem: null);

        var assembly = new ModelAssembly(
            id: 201,
            guid: Guid.NewGuid(),
            type: 2,
            subtype: 0,
            phase: 1,
            parentId: null,
            name: "ROOF_BEAM_ASM",
            prefix: "A",
            startNumber: 1,
            positionNumber: 1,
            assemblyType: 0,
            @class: 3);

        beam.Assembly = assembly;
        plate.Assembly = assembly;
        assembly.MainPart = beam;
        assembly.Parts = [beam, plate];
        assembly.Secondaries = [plate];

        var bolt = new ModelBolt(
            id: 301,
            guid: Guid.NewGuid(),
            type: 9,
            subtype: 0,
            phase: 1,
            parentId: 201,
            assemblyId: 201,
            firstPoint: p0,
            secondPoint: p1,
            holes: [new ModelBoltHole(1, 101, beam, 0, 20, 0, 0, 0)],
            positions: [new ModelPoint(5, 100, 50, 0)]);
        bolt.Assembly = assembly;

        var weld = new ModelWeld(
            id: 401,
            guid: Guid.NewGuid(),
            type: 13,
            subtype: 0,
            phase: 1,
            parentId: 201,
            assemblyId: 201,
            commonAttrId: 501);
        weld.Assembly = assembly;

        assembly.Bolts = [bolt];
        assembly.Welds = [weld];

        var document = Db1Document.Create(Synthetic.Db1Payload([]));

        return new TeklaModel(
            document,
            Guid.NewGuid(),
            assemblies: [assembly],
            parts: [beam, column, plate],
            bolts: [bolt],
            welds: [weld],
            objects: [beam, column, plate, assembly, bolt, weld]);
    }
}
