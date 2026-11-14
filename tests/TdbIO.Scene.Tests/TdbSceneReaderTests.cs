using System.Text;
using System.Text.Json;
using IOScene;
using IOViewer.Exporters;
using TdbIO.CommandLine;
using Xunit;

namespace TdbIO.Scene.Tests;

public sealed class TdbSceneReaderTests
{
    [Fact]
    public void Read_ConvertsTeklaModelToValidSceneDocument()
    {
        var model = CreateSyntheticModel();
        var result = TdbSceneReader.Read(model, new TdbSceneOptions { ModelKey = "test_model.db1" });

        // Contract validation: MUST have 0 errors and 0 warnings
        var diagnostics = result.Document.Validate();
        Assert.Empty(diagnostics);

        // Verify Model metadata
        var sceneModel = Assert.Single(result.Document.Models);
        Assert.Equal("test_model.db1", sceneModel.Key);
        Assert.Equal("test_model.db1", sceneModel.Name);
        Assert.Equal("db1", sceneModel.SourceFormat);
        Assert.Equal("mm", sceneModel.SourceLengthUnit);
        Assert.Equal("Tekla Structures", sceneModel.Application);
        Assert.StartsWith("TdbIO.Scene", sceneModel.Producer);
        Assert.Equal(0.001, result.MetresPerUnit);

        // Verify Element counts
        Assert.Equal(5, result.ElementCount); // 1 assembly + 2 parts + 1 bolt + 1 weld
        Assert.True(result.ElementsWithGeometry >= 2);

        // Verify Root model element
        var rootElem = Assert.Single(result.Document.Elements, e => e.Kind == SceneNodeKind.Model);
        Assert.Equal(1L, rootElem.Id);
        Assert.Null(rootElem.ParentId);

        // Verify Assembly element
        var asmElem = Assert.Single(result.Document.Elements, e => e.Kind == SceneNodeKind.Group);
        Assert.Equal(1L, asmElem.ParentId);
        Assert.Equal("Assembly", asmElem.Category);
        Assert.Contains("ROOF_BEAM_ASM", asmElem.Name);
        Assert.NotEmpty(asmElem.PropertySetIds);

        // Verify Parts elements
        var partElems = result.Document.Elements.Where(e => e.Category == "Part").ToList();
        Assert.Equal(2, partElems.Count);

        var beamElem = Assert.Single(partElems, p => p.TypeName == "HEA300");
        Assert.Equal(asmElem.Id, beamElem.ParentId); // Child of assembly
        Assert.NotEmpty(beamElem.PropertySetIds);

        var colElem = Assert.Single(partElems, p => p.TypeName == "HEB300");
        Assert.Equal(1L, colElem.ParentId); // Standalone part, child of root model

        // Verify PropertySets
        var partPset = Assert.Single(result.Document.PropertySets, p => p.Key == "tekla:part" && p.Properties.Any(prop => prop.Name == "Profile" && prop.Value.Text == "HEA300"));
        var lengthProp = Assert.Single(partPset.Properties, p => p.Name == "Length");
        Assert.Equal(SceneValueKind.Number, lengthProp.Value.Kind);
        Assert.Equal(6.0, lengthProp.Value.Number); // 6000 mm -> 6.0 m
        Assert.Equal("m", lengthProp.Value.Unit);

        // Verify Geometries & Instances
        Assert.NotEmpty(result.Document.Geometries);
        Assert.NotEmpty(result.Document.Instances);

        foreach (var geom in result.Document.Geometries)
        {
            Assert.Equal(SceneTopology.Triangles, geom.Topology);
            Assert.Equal(geom.Vertices.Count, geom.Normals?.Count);
            Assert.True(geom.Indices.Count % 3 == 0);
        }

        // Verify Materials & Colors
        Assert.NotEmpty(result.Document.Materials);
        var instance = Assert.Single(result.Document.Instances, i => i.ElementId == beamElem.Id);
        Assert.NotNull(instance.MaterialId);
        Assert.NotNull(instance.Color);
    }

    [Fact]
    public void SceneJson_Roundtrip_SucceedsAndValidates()
    {
        var model = CreateSyntheticModel();
        var result = TdbSceneReader.Read(model, new TdbSceneOptions { ModelKey = "building.db1" });

        using var ms = new MemoryStream();
        SceneJson.Write(result.Document, ms);
        ms.Position = 0;

        var roundtripDoc = SceneJson.Read(ms);
        Assert.NotNull(roundtripDoc);
        Assert.Equal("building.db1", roundtripDoc.Title);
        Assert.Empty(roundtripDoc.Validate());
    }

    [Fact]
    public void HtmlViewerExporter_ExportsInteractiveViewer()
    {
        var model = CreateSyntheticModel();
        var result = TdbSceneReader.Read(model, new TdbSceneOptions { ModelKey = "building.db1" });

        string html = HtmlViewerExporter.ExportToString(result.Document);
        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("IOViewer", html);
        Assert.Contains("building.db1", html);

        string tempPath = Path.Combine(Path.GetTempPath(), $"viewer_{Guid.NewGuid():N}.html");
        try
        {
            HtmlViewerExporter.Save(result.Document, tempPath);
            Assert.True(File.Exists(tempPath));
            string fileContent = File.ReadAllText(tempPath);
            Assert.Contains("<!DOCTYPE html>", fileContent);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Read_RejectsNonFiniteProfile()
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

        var doc = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: [beam], bolts: [], welds: [], objects: [beam]);

        Assert.Throws<InvalidDataException>(() => TdbSceneReader.Read(model));
    }

    [Fact]
    public void Cli_Export_CommandsWorkCorrectly()
    {
        // Test usage errors and options
        var err = new StringWriter();
        var @out = new StringWriter();

        // Mutually exclusive flags
        int code = Cli.Run(["export", "dummy.db1", "--ifc", "--html"], @out, err);
        Assert.Equal(Cli.ExitUsage, code);
        Assert.Contains("mutually exclusive", err.ToString());
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
            stringAttributes: new Dictionary<string, string> { ["COMMENT"] = "Roof beam" },
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
        assembly.MainPart = beam;
        assembly.Parts = [beam];

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

        var document = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));

        return new TeklaModel(
            document,
            Guid.NewGuid(),
            assemblies: [assembly],
            parts: [beam, column],
            bolts: [bolt],
            welds: [weld],
            objects: [beam, column, assembly, bolt, weld]);
    }
}
