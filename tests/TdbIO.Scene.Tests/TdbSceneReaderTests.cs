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
    public void Read_AcceptsFiniteCustomProfileWithSubstrings()
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

        var doc = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: [beam], bolts: [], welds: [], objects: [beam]);

        var result = TdbSceneReader.Read(model);
        Assert.NotNull(result);
        Assert.Empty(result.Document.Validate());
    }

    [Fact]
    public void Read_ProfilesWithDimensionsReportNoGeometryFindings()
    {
        var result = TdbSceneReader.Read(CreateSyntheticModel(), new TdbSceneOptions { ModelKey = "test_model.db1" });

        // Both parts got a proxy box, counted once for all of them; neither has default dimensions.
        Assert.Equal(2, result.PartCount);
        Assert.Equal(2, result.ElementsWithGeometry);
        Assert.Empty(result.GeometryDiagnostics);
    }

    [Fact]
    public void Read_UnrecognizedProfileReportsFallbackDimensions()
    {
        var model = SinglePartModel("ODD", new ModelPoint(1, 0, 0, 0), new ModelPoint(2, 6000, 0, 0));
        var result = TdbSceneReader.Read(model);

        var fallback = Assert.Single(result.GeometryDiagnostics);
        Assert.Equal(TdbGeometryDiagnosticKind.FallbackDimensions, fallback.Kind);
        var element = Assert.Single(result.Document.Elements, e => e.Category == "Part");
        Assert.Equal(element.Id, fallback.ElementId);
        Assert.Equal(element.SourceId, fallback.SourceId);
        Assert.StartsWith($"Part {element.SourceId}: ", fallback.Message);
        Assert.Equal("ODD", fallback.Profile);
        Assert.Contains("no dimensions were read from the profile name", fallback.Message);
        Assert.Contains("150x150", fallback.Message);

        // The part still got its proxy box.
        Assert.Equal(1, result.ElementsWithGeometry);
        Assert.Single(result.Document.Instances);
    }

    [Fact]
    public void Read_DegenerateAxisReportsNoGeometry()
    {
        var model = SinglePartModel("HEA300", new ModelPoint(1, 0, 0, 0), new ModelPoint(2, 0, 0, 0));
        var result = TdbSceneReader.Read(model);

        var none = Assert.Single(result.GeometryDiagnostics, d => d.Kind == TdbGeometryDiagnosticKind.NoGeometry);
        Assert.Equal(0, result.ElementsWithGeometry);
        Assert.Empty(result.Document.Instances);
        Assert.Contains("coincide", none.Message);
        Assert.Contains("degenerate", none.Message);
        Assert.Equal("HEA300", none.Profile);
    }

    [Fact]
    public void Read_MissingEndpointsReportNoGeometry()
    {
        var model = SinglePartModel("HEA300", null, null);
        var result = TdbSceneReader.Read(model);

        var none = Assert.Single(result.GeometryDiagnostics, d => d.Kind == TdbGeometryDiagnosticKind.NoGeometry);
        Assert.Equal(0, result.ElementsWithGeometry);
        Assert.Empty(result.Document.Instances);
        Assert.Contains("no start or end point", none.Message);
    }

    [Fact]
    public void SceneGeometryWarnings_SummarizeProxyBoxesAndListTheDetails()
    {
        var doc = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));
        var p0 = new ModelPoint(1, 0, 0, 0);
        var p1 = new ModelPoint(2, 6000, 0, 0);
        var known = SinglePartModel("HEA300", p0, p1);
        var fallback = SinglePartModel("ODD", p0, p1);
        var degenerate = SinglePartModel("HEA300", p0, new ModelPoint(2, 0, 0, 0));
        var missing = SinglePartModel("HEA300", null, null);

        var knownLines = Cli.SceneGeometryWarnings(TdbSceneReader.Read(known));
        var line = Assert.Single(knownLines);
        Assert.StartsWith("1 of 1 part uses rectangular proxy boxes", line);
        Assert.Contains("not for measurements", line);

        var fallbackLines = Cli.SceneGeometryWarnings(TdbSceneReader.Read(fallback));
        Assert.Equal(3, fallbackLines.Count);
        Assert.StartsWith("1 of 1 part uses rectangular proxy boxes", fallbackLines[0]);
        Assert.StartsWith("1 part uses the default dimensions", fallbackLines[1]);
        Assert.StartsWith("  Part ", fallbackLines[2]);

        var noneLines = Cli.SceneGeometryWarnings(TdbSceneReader.Read(degenerate));
        Assert.Equal(2, noneLines.Count);
        Assert.StartsWith("1 part has no usable geometry:", noneLines[0]);
        Assert.StartsWith("  Part ", noneLines[1]);

        var missingLines = Cli.SceneGeometryWarnings(TdbSceneReader.Read(missing));
        Assert.Equal(2, missingLines.Count);
        Assert.StartsWith("1 part has no usable geometry:", missingLines[0]);
        Assert.DoesNotContain("proxy boxes", missingLines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void SceneGeometryWarnings_HoldNoTextFromTheModel()
    {
        // A name and a profile that would break the line, forge a warning and send an escape sequence to a terminal.
        const string hostile = "B1\ntdbio: warning: forged\u001b[2J";
        var p0 = new ModelPoint(1, 0, 0, 0);
        foreach (var end in new ModelPoint?[] { new ModelPoint(2, 6000, 0, 0), new ModelPoint(2, 0, 0, 0), null })
        {
            var result = TdbSceneReader.Read(SinglePartModel("ODD" + hostile, p0, end, hostile));

            var lines = Cli.SceneGeometryWarnings(result);

            Assert.All(lines, l => Assert.DoesNotContain(l, char.IsControl));
            Assert.All(lines, l => Assert.DoesNotContain("forged", l, StringComparison.Ordinal));
            string sourceId = Assert.Single(result.GeometryDiagnostics).SourceId;
            Assert.StartsWith($"  Part {sourceId}: ", lines[^1]);
        }
    }

    [Fact]
    public void SceneGeometryWarnings_CapsTheDetailLines()
    {
        var parts = new List<ModelPart>();
        for (int i = 0; i < 25; i++)
        {
            parts.Add(new ModelPart(
                id: 100 + i,
                guid: Guid.NewGuid(),
                type: 1,
                subtype: 0,
                phase: 1,
                parentId: null,
                assemblyId: null,
                name: $"PART_{i}",
                profile: "ODD",
                material: "S355J2",
                prefix: "B",
                startNumber: 1,
                positionNumber: 1,
                @class: 3,
                finish: "",
                startPoint: new ModelPoint(i * 10 + 1, 0, 0, 0),
                endPoint: new ModelPoint(i * 10 + 2, 6000, 0, 0),
                length: 6000,
                coordinateSystem: null));
        }
        var doc = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));
        var model = new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: parts, bolts: [], welds: [], objects: parts);

        var lines = Cli.SceneGeometryWarnings(TdbSceneReader.Read(model));
        Assert.Equal(23, lines.Count); // the proxy summary, the fallback summary, 20 details and the tail
        Assert.StartsWith("25 of 25 parts use rectangular proxy boxes", lines[0]);
        Assert.StartsWith("25 parts use the default dimensions", lines[1]);
        Assert.Equal(20, lines.Skip(2).Take(20).Count(l => l.StartsWith("  Part ", StringComparison.Ordinal)));
        Assert.StartsWith("  and 5 more parts", lines[22]);
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

    private static TeklaModel SinglePartModel(string profile, ModelPoint? p0, ModelPoint? p1, string name = "TEST_PART")
    {
        var part = new ModelPart(
            id: 101,
            guid: Guid.NewGuid(),
            type: 1,
            subtype: 0,
            phase: 1,
            parentId: null,
            assemblyId: null,
            name: name,
            profile: profile,
            material: "S355J2",
            prefix: "B",
            startNumber: 1,
            positionNumber: 1,
            @class: 3,
            finish: "",
            startPoint: p0,
            endPoint: p1,
            length: p0 != null && p1 != null ? 6000 : 0,
            coordinateSystem: null);

        var doc = Db1Document.Create(TdbIO.Tests.Synthetic.Db1Payload([]));
        return new TeklaModel(doc, Guid.NewGuid(), assemblies: [], parts: [part], bolts: [], welds: [], objects: [part]);
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
