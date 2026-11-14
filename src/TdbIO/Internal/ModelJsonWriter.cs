using System.Text;
using System.Text.Json;

namespace TdbIO.Internal;

internal static class ModelJsonWriter
{
    public static string ToJson(TeklaModel model, bool indented)
    {
        using var stream = new MemoryStream();
        Write(model, stream, indented);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }

    public static void Write(TeklaModel model, Stream stream, bool indented)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented });
        Write(model, writer);
    }

    public static void Write(TeklaModel model, Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();

        writer.WriteString("modelGuid", model.ModelGuid.ToString("D"));
        writer.WriteString("version", model.Document.Version);

        writer.WriteStartObject("counts");
        writer.WriteNumber("assemblies", model.Assemblies.Count);
        writer.WriteNumber("parts", model.Parts.Count);
        writer.WriteNumber("bolts", model.Bolts.Count);
        writer.WriteNumber("welds", model.Welds.Count);
        writer.WriteNumber("objects", model.Objects.Count);
        writer.WriteEndObject();

        // Assemblies
        writer.WriteStartArray("assemblies");
        foreach (var a in model.Assemblies)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", a.Id);
            writer.WriteString("guid", a.Guid.ToString("D"));
            writer.WriteString("name", a.Name);
            writer.WriteString("mark", $"{a.Prefix}{a.PositionNumber}");
            writer.WriteString("prefix", a.Prefix);
            writer.WriteNumber("startNumber", a.StartNumber);
            writer.WriteNumber("positionNumber", a.PositionNumber);
            writer.WriteNumber("class", a.Class);
            writer.WriteNumber("assemblyType", a.AssemblyType);
            writer.WriteNumber("phase", a.Phase);

            if (a.MainPart != null) writer.WriteNumber("mainPartId", a.MainPart.Id);
            else writer.WriteNull("mainPartId");

            writer.WriteStartArray("partIds");
            foreach (var p in a.Parts) writer.WriteNumberValue(p.Id);
            writer.WriteEndArray();

            writer.WriteStartArray("secondaryPartIds");
            foreach (var p in a.Secondaries) writer.WriteNumberValue(p.Id);
            writer.WriteEndArray();

            writer.WriteStartArray("boltIds");
            foreach (var b in a.Bolts) writer.WriteNumberValue(b.Id);
            writer.WriteEndArray();

            writer.WriteStartArray("weldIds");
            foreach (var w in a.Welds) writer.WriteNumberValue(w.Id);
            writer.WriteEndArray();

            WriteAttributes(writer, a);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Parts
        writer.WriteStartArray("parts");
        foreach (var p in model.Parts)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", p.Id);
            writer.WriteString("guid", p.Guid.ToString("D"));
            writer.WriteString("name", p.Name);
            writer.WriteString("profile", p.Profile);
            writer.WriteString("material", p.Material);
            writer.WriteString("mark", $"{p.Prefix}{p.PositionNumber}");
            writer.WriteString("prefix", p.Prefix);
            writer.WriteNumber("startNumber", p.StartNumber);
            writer.WriteNumber("positionNumber", p.PositionNumber);
            writer.WriteNumber("class", p.Class);
            writer.WriteString("finish", p.Finish);
            writer.WriteNumber("length", p.Length);
            writer.WriteNumber("phase", p.Phase);

            if (p.AssemblyId.HasValue) writer.WriteNumber("assemblyId", p.AssemblyId.Value);
            else writer.WriteNull("assemblyId");

            writer.WriteBoolean("isMainPart", p.IsMainPart);

            if (p.StartPoint.HasValue) WritePoint(writer, "startPoint", p.StartPoint.Value);
            else writer.WriteNull("startPoint");

            if (p.EndPoint.HasValue) WritePoint(writer, "endPoint", p.EndPoint.Value);
            else writer.WriteNull("endPoint");

            if (p.CoordinateSystem.HasValue)
            {
                var cs = p.CoordinateSystem.Value;
                writer.WriteStartObject("coordinateSystem");
                writer.WriteNumber("id", cs.Id);
                writer.WriteNumber("x", cs.X);
                writer.WriteNumber("y", cs.Y);
                writer.WriteNumber("z", cs.Z);
                writer.WriteNumber("length", cs.Length);
                writer.WriteEndObject();
            }
            else writer.WriteNull("coordinateSystem");

            writer.WriteStartArray("boltIds");
            foreach (var b in p.Bolts) writer.WriteNumberValue(b.Id);
            writer.WriteEndArray();

            writer.WriteStartArray("weldIds");
            foreach (var w in p.Welds) writer.WriteNumberValue(w.Id);
            writer.WriteEndArray();

            WriteAttributes(writer, p);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Bolts
        writer.WriteStartArray("bolts");
        foreach (var b in model.Bolts)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", b.Id);
            writer.WriteString("guid", b.Guid.ToString("D"));
            writer.WriteNumber("phase", b.Phase);

            if (b.AssemblyId.HasValue) writer.WriteNumber("assemblyId", b.AssemblyId.Value);
            else writer.WriteNull("assemblyId");

            if (b.FirstPoint.HasValue) WritePoint(writer, "firstPoint", b.FirstPoint.Value);
            else writer.WriteNull("firstPoint");

            if (b.SecondPoint.HasValue) WritePoint(writer, "secondPoint", b.SecondPoint.Value);
            else writer.WriteNull("secondPoint");

            writer.WriteStartArray("connectedPartIds");
            foreach (var cp in b.ConnectedParts) writer.WriteNumberValue(cp.Id);
            writer.WriteEndArray();

            writer.WriteStartArray("holes");
            foreach (var h in b.Holes)
            {
                writer.WriteStartObject();
                writer.WriteNumber("no", h.No);
                writer.WriteNumber("partId", h.PartId);
                writer.WriteNumber("pointZ", h.PointZ);
                writer.WriteNumber("length", h.Length);
                writer.WriteNumber("longHoleDir", h.LongHoleDir);
                writer.WriteNumber("longHoleType", h.LongHoleType);
                writer.WriteNumber("shorteningLength", h.ShorteningLength);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("positions");
            foreach (var pos in b.Positions)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", pos.X);
                writer.WriteNumber("y", pos.Y);
                writer.WriteNumber("z", pos.Z);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            WriteAttributes(writer, b);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Welds
        writer.WriteStartArray("welds");
        foreach (var w in model.Welds)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", w.Id);
            writer.WriteString("guid", w.Guid.ToString("D"));
            writer.WriteNumber("phase", w.Phase);
            writer.WriteNumber("commonAttrId", w.CommonAttrId);

            if (w.AssemblyId.HasValue) writer.WriteNumber("assemblyId", w.AssemblyId.Value);
            else writer.WriteNull("assemblyId");

            if (w.ParentId.HasValue) writer.WriteNumber("parentId", w.ParentId.Value);
            else writer.WriteNull("parentId");

            writer.WriteStartArray("connectedPartIds");
            foreach (var cp in w.ConnectedParts) writer.WriteNumberValue(cp.Id);
            writer.WriteEndArray();

            WriteAttributes(writer, w);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter writer, string propertyName, ModelPoint pt)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteNumber("id", pt.Id);
        writer.WriteNumber("x", pt.X);
        writer.WriteNumber("y", pt.Y);
        writer.WriteNumber("z", pt.Z);
        writer.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter writer, ModelObject obj)
    {
        if (obj.StringAttributes.Count > 0)
        {
            writer.WriteStartObject("stringAttributes");
            foreach (var (k, v) in obj.StringAttributes)
                writer.WriteString(k, v);
            writer.WriteEndObject();
        }

        if (obj.NumericAttributes.Count > 0)
        {
            writer.WriteStartObject("numericAttributes");
            foreach (var (k, v) in obj.NumericAttributes)
                writer.WriteNumber(k, v);
            writer.WriteEndObject();
        }
    }
}
