using System.Globalization;
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
    public void StepWriter_RejectsNonFiniteReals()
    {
        Assert.Throws<InvalidDataException>(() => StepWriter.Real(double.NaN));
        Assert.Throws<InvalidDataException>(() => StepWriter.Real(double.PositiveInfinity));
        Assert.Throws<InvalidDataException>(() => StepWriter.RealExact(double.NaN));
        Assert.Throws<InvalidDataException>(() => StepWriter.RealExact(double.NegativeInfinity));
    }

    [Fact]
    public void TeklaModel_ToIfc_RejectsNonFiniteProfile()
    {
        var p0 = new ModelPoint(1, 0, 0, 0);
        var p1 = new ModelPoint(2, 6000, 0, 0);
        var beam = new ModelPart(
            id: 101,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: null,
            assemblyId: null,
            name: "BEAM",
            profile: "PLNaN*200",
            material: "S355J2",
            prefix: "B",
            startNumber: 1,
            positionNumber: 1,
            @class: 3,
            finish: "",
            startPoint: p0,
            endPoint: p1,
            length: 6000,
            coordinateSystem: null);

        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: [beam], bolts: [], welds: [], objects: [beam]);

        Assert.Throws<InvalidDataException>(() => model.ToIfc());
    }

    [Fact]
    public void TeklaModel_ToIfc_AcceptsFiniteCustomProfileWithSubstrings()
    {
        var p0 = new ModelPoint(1, 0, 0, 0);
        var p1 = new ModelPoint(2, 6000, 0, 0);
        var beam = new ModelPart(
            id: 101,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: null,
            assemblyId: null,
            name: "BEAM",
            profile: "CUSTOM_NANO200",
            material: "S355J2",
            prefix: "B",
            startNumber: 1,
            positionNumber: 1,
            @class: 3,
            finish: "",
            startPoint: p0,
            endPoint: p1,
            length: 6000,
            coordinateSystem: null);

        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: [beam], bolts: [], welds: [], objects: [beam]);

        string ifc = model.ToIfc();
        Assert.Contains("CUSTOM_NANO200", ifc);
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

    [Fact]
    public void StepWriter_RealExact_PreservesSmallPrecision_AndValidStepSyntax()
    {
        double small = 1e-8;
        string formatted = StepWriter.RealExact(small);
        Assert.Equal("1.E-08", formatted);

        // Valid ISO 10303-21 STEP real syntax: must contain a decimal point in the mantissa
        int expIndex = formatted.IndexOfAny(['E', 'e']);
        Assert.True(expIndex > 0);
        Assert.Contains('.', formatted[..expIndex]);

        // Round-trip parse back to double
        double parsed = double.Parse(formatted, CultureInfo.InvariantCulture);
        Assert.Equal(small, parsed);
    }

    [Fact]
    public void Ifc_SchemaConformance_Ifc2x3()
    {
        var model = CreateSyntheticModel();
        string ifc = model.ToIfc(new TeklaIfcExportOptions { SchemaVersion = "IFC2X3" });
        var entities = ParseIfcEntities(ifc);

        var buildings = entities.Values.Where(e => e.Type == "IFCBUILDING").ToList();
        Assert.NotEmpty(buildings);
        Assert.All(buildings, b => Assert.Equal(12, b.Args.Count));

        var assemblies = entities.Values.Where(e => e.Type == "IFCELEMENTASSEMBLY").ToList();
        Assert.NotEmpty(assemblies);
        Assert.All(assemblies, a => Assert.Equal(10, a.Args.Count));

        var materials = entities.Values.Where(e => e.Type == "IFCMATERIAL").ToList();
        Assert.NotEmpty(materials);
        Assert.All(materials, m => Assert.Single(m.Args));

        var bolts = entities.Values.Where(e => e.Type == "IFCMECHANICALFASTENER").ToList();
        Assert.NotEmpty(bolts);
        Assert.All(bolts, b => Assert.Equal(10, b.Args.Count));

        var welds = entities.Values.Where(e => e.Type == "IFCFASTENER").ToList();
        Assert.NotEmpty(welds);
        Assert.All(welds, w => Assert.Equal(8, w.Args.Count));
    }

    [Fact]
    public void Ifc_SchemaConformance_Ifc4()
    {
        var model = CreateSyntheticModel();
        string ifc = model.ToIfc(new TeklaIfcExportOptions { SchemaVersion = "IFC4" });
        var entities = ParseIfcEntities(ifc);

        var buildings = entities.Values.Where(e => e.Type == "IFCBUILDING").ToList();
        Assert.NotEmpty(buildings);
        Assert.All(buildings, b => Assert.Equal(12, b.Args.Count));

        var assemblies = entities.Values.Where(e => e.Type == "IFCELEMENTASSEMBLY").ToList();
        Assert.NotEmpty(assemblies);
        Assert.All(assemblies, a => Assert.Equal(10, a.Args.Count));

        var materials = entities.Values.Where(e => e.Type == "IFCMATERIAL").ToList();
        Assert.NotEmpty(materials);
        Assert.All(materials, m => Assert.Equal(3, m.Args.Count));

        var bolts = entities.Values.Where(e => e.Type == "IFCMECHANICALFASTENER").ToList();
        Assert.NotEmpty(bolts);
        Assert.All(bolts, b =>
        {
            Assert.Equal(11, b.Args.Count);
            Assert.Equal(".BOLT.", b.Args[10]);
        });

        var welds = entities.Values.Where(e => e.Type == "IFCFASTENER").ToList();
        Assert.NotEmpty(welds);
        Assert.All(welds, w =>
        {
            Assert.Equal(9, w.Args.Count);
            Assert.Equal(".WELD.", w.Args[8]);
        });

        var plates = entities.Values.Where(e => e.Type == "IFCPLATE").ToList();
        Assert.NotEmpty(plates);
        Assert.All(plates, p =>
        {
            Assert.Equal(9, p.Args.Count);
            Assert.NotEqual(".PLATE.", p.Args[8]);
            Assert.Equal(".NOTDEFINED.", p.Args[8]);
        });
    }

    [Fact]
    public void Ifc_ExportGeometryFalse_OmitsProductDefinitionShape()
    {
        var model = CreateSyntheticModel();
        string ifc = model.ToIfc(new TeklaIfcExportOptions { ExportGeometry = false });
        var entities = ParseIfcEntities(ifc);

        Assert.DoesNotContain(entities.Values, e => e.Type == "IFCPRODUCTDEFINITIONSHAPE");

        var beams = entities.Values.Where(e => e.Type == "IFCBEAM").ToList();
        Assert.NotEmpty(beams);
        Assert.All(beams, b => Assert.Equal("$", b.Args[6]));

        var columns = entities.Values.Where(e => e.Type == "IFCCOLUMN").ToList();
        Assert.NotEmpty(columns);
        Assert.All(columns, c => Assert.Equal("$", c.Args[6]));
    }

    [Fact]
    public void Ifc_Units_ConvertMmToMetres_ScalesLengthAndDeclaresUnit()
    {
        var model = CreateSyntheticModel();

        // 1. ConvertMmToMetres = true (default)
        string ifcM = model.ToIfc(new TeklaIfcExportOptions { ConvertMmToMetres = true });
        Assert.Contains("IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);", ifcM);
        Assert.Contains("IFCLENGTHMEASURE(6.)", ifcM);
        Assert.DoesNotContain("IFCLENGTHMEASURE(6000.)", ifcM);

        // 2. ConvertMmToMetres = false
        string ifcMm = model.ToIfc(new TeklaIfcExportOptions { ConvertMmToMetres = false });
        Assert.Contains("IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);", ifcMm);
        Assert.Contains("IFCLENGTHMEASURE(6000.)", ifcMm);
    }

    [Fact]
    public void Ifc_GuidEmpty_Matches_IOScene_IfcGuid()
    {
        var p0 = new ModelPoint(1, 0, 0, 0);
        var p1 = new ModelPoint(2, 1000, 0, 0);
        var part = new ModelPart(1, Guid.Empty, 1, 0, 1, null, null, "PART", "PL10", "S235", "", 1, 1, 1, "", p0, p1, 1000, null);
        var asm = new ModelAssembly(2, Guid.Empty, 2, 0, 1, null, "ASM", "A", 1, 1, 0, 1);
        var bolt = new ModelBolt(3, Guid.Empty, 9, 0, 1, null, null, p0, p1, [], []);
        var weld = new ModelWeld(4, Guid.Empty, 13, 0, 1, null, null, 1);

        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.Empty, [asm], [part], [bolt], [weld], [part, asm, bolt, weld]);

        string ifc = model.ToIfc();
        var entities = ParseIfcEntities(ifc);

        var ifcPart = Assert.Single(entities.Values, e => e.Type == "IFCPLATE" || e.Type == "IFCMEMBER");
        Assert.Equal($"'{part.IfcGuid}'", ifcPart.Args[0]);

        var ifcAsm = Assert.Single(entities.Values, e => e.Type == "IFCELEMENTASSEMBLY");
        Assert.Equal($"'{asm.IfcGuid}'", ifcAsm.Args[0]);

        var ifcBolt = Assert.Single(entities.Values, e => e.Type == "IFCMECHANICALFASTENER");
        Assert.Equal($"'{bolt.IfcGuid}'", ifcBolt.Args[0]);

        var ifcWeld = Assert.Single(entities.Values, e => e.Type == "IFCFASTENER");
        Assert.Equal($"'{weld.IfcGuid}'", ifcWeld.Args[0]);
    }

    private static List<string> ParseStepArgs(string argsText)
    {
        var list = new List<string>();
        int depth = 0;
        bool inQuote = false;
        int start = 0;
        for (int i = 0; i < argsText.Length; i++)
        {
            char c = argsText[i];
            if (c == '\'')
            {
                if (inQuote && i + 1 < argsText.Length && argsText[i + 1] == '\'')
                {
                    i++; // skip escaped quote ''
                }
                else
                {
                    inQuote = !inQuote;
                }
            }
            else if (!inQuote)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    list.Add(argsText[start..i].Trim());
                    start = i + 1;
                }
            }
        }
        list.Add(argsText[start..].Trim());
        return list;
    }

    private static Dictionary<int, (string Type, List<string> Args)> ParseIfcEntities(string ifc)
    {
        var dict = new Dictionary<int, (string Type, List<string> Args)>();
        foreach (var line in ifc.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            int id = int.Parse(line[1..eq].Trim(), CultureInfo.InvariantCulture);
            int openParen = line.IndexOf('(', eq);
            int closeParen = line.LastIndexOf(')');
            if (openParen < 0 || closeParen <= openParen) continue;
            string type = line[(eq + 1)..openParen].Trim();
            string argsText = line[(openParen + 1)..closeParen];
            var args = ParseStepArgs(argsText);
            dict[id] = (type, args);
        }
        return dict;
    }
}
