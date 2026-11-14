using System.Text.Json;
using Xunit;

namespace TdbIO.Tests;

public class JsonExportTests
{
    [Fact]
    public void WriteJsonThrowsOnNull()
    {
        var doc = Db1Document.Create(Synthetic.Db1TablesPayload(
            Synthetic.Db1Section(null, 4, [1], (0x04, Synthetic.Counting(4), 1, 2))));
        var model = doc.ReadModel();
        Assert.Throws<ArgumentNullException>(() => model.WriteJson((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => model.WriteJson((Utf8JsonWriter)null!));
    }

    [SkippableFact]
    public void RealModelsExportToValidJson()
    {
        var files = TestFiles.FindAll(".db1", ".bak").Where(f => TdbFile.DetectKind(f) == TdbFileKind.Db1).ToList();
        if (files.Count == 0) throw TestFiles.Missing("model database files");

        foreach (string file in files)
        {
            var doc = (Db1Document)TdbFile.Open(file);
            var model = doc.ReadModel();

            string compactJson = model.ToJson(indented: false);
            Assert.False(string.IsNullOrEmpty(compactJson));
            Assert.DoesNotContain("\n", compactJson);

            string indentedJson = model.ToJson(indented: true);
            Assert.Contains("\n", indentedJson);

            // Stream write matches string export
            using var ms = new MemoryStream();
            model.WriteJson(ms, indented: true);
            string streamJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            Assert.Equal(indentedJson, streamJson);

            // Parse and validate structure
            using var json = JsonDocument.Parse(compactJson);
            var root = json.RootElement;

            Assert.Equal(model.ModelGuid.ToString("D"), root.GetProperty("modelGuid").GetString());
            Assert.Equal(doc.Version, root.GetProperty("version").GetString());

            var counts = root.GetProperty("counts");
            Assert.Equal(model.Assemblies.Count, counts.GetProperty("assemblies").GetInt32());
            Assert.Equal(model.Parts.Count, counts.GetProperty("parts").GetInt32());
            Assert.Equal(model.Bolts.Count, counts.GetProperty("bolts").GetInt32());
            Assert.Equal(model.Welds.Count, counts.GetProperty("welds").GetInt32());
            Assert.Equal(model.Objects.Count, counts.GetProperty("objects").GetInt32());

            var assemblies = root.GetProperty("assemblies");
            Assert.Equal(model.Assemblies.Count, assemblies.GetArrayLength());

            var parts = root.GetProperty("parts");
            Assert.Equal(model.Parts.Count, parts.GetArrayLength());

            var bolts = root.GetProperty("bolts");
            Assert.Equal(model.Bolts.Count, bolts.GetArrayLength());

            var welds = root.GetProperty("welds");
            Assert.Equal(model.Welds.Count, welds.GetArrayLength());
        }
    }

    [Fact]
    public void NonFiniteLengthThrowsInvalidDataException()
    {
        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var part = new ModelPart(1, Guid.NewGuid(), 1, 0, 1, null, null, "BEAM", "HEA300", "S355", "B", 1, 1, 1, "", null, null, double.NaN, null, lengthOffset: 0x12A);
        var model = new TeklaModel(doc, doc.ModelGuid, [], [part], [], [], [part]);

        var ex = Assert.Throws<InvalidDataException>(() => model.ToJson());
        Assert.Equal(0x12A, (long)ex.Data["Offset"]!);
        Assert.Contains("0x12A", ex.Message);
    }

    [Fact]
    public void NonFiniteCoordinatesThrowInvalidDataException()
    {
        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var pt = new ModelPoint(1, double.PositiveInfinity, 0, 0) { Offset = 0x100 };
        var part = new ModelPart(1, Guid.NewGuid(), 1, 0, 1, null, null, "BEAM", "HEA300", "S355", "B", 1, 1, 1, "", pt, null, 1000.0, null);
        var model = new TeklaModel(doc, doc.ModelGuid, [], [part], [], [], [part]);

        var ex = Assert.Throws<InvalidDataException>(() => model.ToJson());
        Assert.Equal(0x108, (long)ex.Data["Offset"]!);
        Assert.Contains("0x108", ex.Message);
    }

    [Fact]
    public void NonFiniteNumericAttributeThrowsInvalidDataException()
    {
        var doc = Db1Document.Create(Synthetic.Db1Payload([]));
        var attrs = new Dictionary<string, double> { ["WEIGHT"] = double.NaN };
        var offsets = new Dictionary<string, long> { ["WEIGHT"] = 0x200 };
        var part = new ModelPart(1, Guid.NewGuid(), 1, 0, 1, null, null, "BEAM", "HEA300", "S355", "B", 1, 1, 1, "", null, null, 1000.0, null, null, attrs)
        {
            NumericAttributeOffsets = offsets
        };
        var model = new TeklaModel(doc, doc.ModelGuid, [], [part], [], [], [part]);

        var ex = Assert.Throws<InvalidDataException>(() => model.ToJson());
        Assert.Equal(0x200, (long)ex.Data["Offset"]!);
        Assert.Contains("0x200", ex.Message);
    }
}
