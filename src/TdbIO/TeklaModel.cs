namespace TdbIO;

/// <summary>Represents an in-memory, read-only object model decoded from a Tekla Structures model database (.db1).</summary>
public sealed class TeklaModel
{
    private readonly Dictionary<int, ModelObject> _objectsById;
    private readonly Dictionary<Guid, ModelObject> _objectsByGuid;
    private readonly Dictionary<int, ModelAssembly> _assembliesById;
    private readonly Dictionary<int, ModelPart> _partsById;
    private readonly Dictionary<int, ModelBolt> _boltsById;
    private readonly Dictionary<int, ModelWeld> _weldsById;

    internal TeklaModel(
        Db1Document document,
        Guid modelGuid,
        IReadOnlyList<ModelAssembly> assemblies,
        IReadOnlyList<ModelPart> parts,
        IReadOnlyList<ModelBolt> bolts,
        IReadOnlyList<ModelWeld> welds,
        IReadOnlyList<ModelObject> objects)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        ModelGuid = modelGuid;
        Assemblies = assemblies ?? Array.Empty<ModelAssembly>();
        Parts = parts ?? Array.Empty<ModelPart>();
        Bolts = bolts ?? Array.Empty<ModelBolt>();
        Welds = welds ?? Array.Empty<ModelWeld>();
        Objects = objects ?? Array.Empty<ModelObject>();

        _objectsById = new(Objects.Count);
        _objectsByGuid = new(Objects.Count);
        foreach (var obj in Objects)
        {
            _objectsById[obj.Id] = obj;
            if (obj.Guid != Guid.Empty)
                _objectsByGuid[obj.Guid] = obj;
        }

        _assembliesById = new(Assemblies.Count);
        foreach (var a in Assemblies) _assembliesById[a.Id] = a;

        _partsById = new(Parts.Count);
        foreach (var p in Parts) _partsById[p.Id] = p;

        _boltsById = new(Bolts.Count);
        foreach (var b in Bolts) _boltsById[b.Id] = b;

        _weldsById = new(Welds.Count);
        foreach (var w in Welds) _weldsById[w.Id] = w;
    }

    /// <summary>The source model database document.</summary>
    public Db1Document Document { get; }

    /// <summary>The unique model identifier (GUID).</summary>
    public Guid ModelGuid { get; }

    /// <summary>All assemblies in the model.</summary>
    public IReadOnlyList<ModelAssembly> Assemblies { get; }

    /// <summary>All parts (beams, columns, plates, slabs, etc.) in the model.</summary>
    public IReadOnlyList<ModelPart> Parts { get; }

    /// <summary>All bolt groups in the model.</summary>
    public IReadOnlyList<ModelBolt> Bolts { get; }

    /// <summary>All welds in the model.</summary>
    public IReadOnlyList<ModelWeld> Welds { get; }

    /// <summary>All model objects, including parts, assemblies, bolts, welds and connections.</summary>
    public IReadOnlyList<ModelObject> Objects { get; }

    /// <summary>Finds any model object by its integer identifier, or returns <c>null</c> if not found.</summary>
    public ModelObject? FindObject(int id) => _objectsById.GetValueOrDefault(id);

    /// <summary>Finds any model object by its GUID, or returns <c>null</c> if not found.</summary>
    public ModelObject? FindObject(Guid guid) => _objectsByGuid.GetValueOrDefault(guid);

    /// <summary>Finds an assembly by its integer identifier, or returns <c>null</c> if not found.</summary>
    public ModelAssembly? FindAssembly(int id) => _assembliesById.GetValueOrDefault(id);

    /// <summary>Finds a part by its integer identifier, or returns <c>null</c> if not found.</summary>
    public ModelPart? FindPart(int id) => _partsById.GetValueOrDefault(id);

    /// <summary>Finds a bolt group by its integer identifier, or returns <c>null</c> if not found.</summary>
    public ModelBolt? FindBolt(int id) => _boltsById.GetValueOrDefault(id);

    /// <summary>Finds a weld by its integer identifier, or returns <c>null</c> if not found.</summary>
    public ModelWeld? FindWeld(int id) => _weldsById.GetValueOrDefault(id);

    /// <summary>Serializes the decoded model to a JSON string.</summary>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>A JSON string representation of the model.</returns>
    public string ToJson(bool indented = false) => Internal.ModelJsonWriter.ToJson(this, indented);

    /// <summary>Writes the decoded model as JSON to a stream.</summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    public void WriteJson(Stream stream, bool indented = false) => Internal.ModelJsonWriter.Write(this, stream, indented);

    /// <summary>Writes the decoded model as JSON using a <see cref="System.Text.Json.Utf8JsonWriter"/>.</summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    public void WriteJson(System.Text.Json.Utf8JsonWriter writer) => Internal.ModelJsonWriter.Write(this, writer);
}

/// <summary>Base class for all entities in a Tekla Structures model.</summary>
public class ModelObject
{
    /// <summary>Initializes a new instance of <see cref="ModelObject"/>.</summary>
    public ModelObject(
        int id,
        Guid guid,
        int type,
        int subtype,
        int phase,
        int? parentId,
        int? assemblyId,
        IReadOnlyDictionary<string, string>? stringAttributes = null,
        IReadOnlyDictionary<string, double>? numericAttributes = null)
    {
        Id = id;
        Guid = guid;
        Type = type;
        Subtype = subtype;
        Phase = phase;
        ParentId = parentId;
        AssemblyId = assemblyId;
        StringAttributes = stringAttributes ?? new Dictionary<string, string>();
        NumericAttributes = numericAttributes ?? new Dictionary<string, double>();
    }

    internal IReadOnlyDictionary<string, long>? NumericAttributeOffsets { get; init; }

    /// <summary>The unique integer identifier of the object (primary key in the database).</summary>
    public int Id { get; }

    /// <summary>The unique RFC 4122 GUID of the object.</summary>
    public Guid Guid { get; }

    /// <summary>The object type code (e.g. 1 for part, 2 for assembly, 3 for connection, 9 for bolt, 13 for weld).</summary>
    public int Type { get; }

    /// <summary>The object subtype code.</summary>
    public int Subtype { get; }

    /// <summary>The project phase number this object belongs to.</summary>
    public int Phase { get; }

    /// <summary>The parent object identifier, or <c>null</c> if none.</summary>
    public int? ParentId { get; }

    /// <summary>The identifier of the assembly this object belongs to, or <c>null</c> if none.</summary>
    public int? AssemblyId { get; }

    /// <summary>User-defined and system string attributes associated with this object.</summary>
    public IReadOnlyDictionary<string, string> StringAttributes { get; }

    /// <summary>User-defined and system numeric attributes associated with this object.</summary>
    public IReadOnlyDictionary<string, double> NumericAttributes { get; }

    /// <summary>Gets a string attribute value by name, or <c>null</c> if not set.</summary>
    public string? GetStringAttribute(string name) =>
        StringAttributes.TryGetValue(name, out var value) ? value : null;

    /// <summary>Gets a numeric attribute value by name, or <c>null</c> if not set.</summary>
    public double? GetNumericAttribute(string name) =>
        NumericAttributes.TryGetValue(name, out var value) ? value : null;

    /// <inheritdoc/>
    public override string ToString() => $"{GetType().Name} #{Id} ({Guid})";
}

/// <summary>Represents a fabricated assembly or precast unit composed of a main part and optional secondary parts.</summary>
public sealed class ModelAssembly : ModelObject
{
    internal ModelAssembly(
        int id,
        Guid guid,
        int type,
        int subtype,
        int phase,
        int? parentId,
        string name,
        string prefix,
        int startNumber,
        int positionNumber,
        int assemblyType,
        int @class,
        IReadOnlyDictionary<string, string>? stringAttributes = null,
        IReadOnlyDictionary<string, double>? numericAttributes = null)
        : base(id, guid, type, subtype, phase, parentId, id, stringAttributes, numericAttributes)
    {
        Name = name ?? string.Empty;
        Prefix = prefix ?? string.Empty;
        StartNumber = startNumber;
        PositionNumber = positionNumber;
        AssemblyType = assemblyType;
        Class = @class;
        Secondaries = Array.Empty<ModelPart>();
        Parts = Array.Empty<ModelPart>();
        Bolts = Array.Empty<ModelBolt>();
        Welds = Array.Empty<ModelWeld>();
    }

    /// <summary>The assembly name (e.g. "BEAM", "COLUMN", "TRUSS").</summary>
    public string Name { get; }

    /// <summary>The assembly numbering prefix (e.g. "B", "C", "A").</summary>
    public string Prefix { get; }

    /// <summary>The start number for assembly numbering.</summary>
    public int StartNumber { get; }

    /// <summary>The assembly mark position number.</summary>
    public int PositionNumber { get; }

    /// <summary>The assembly type code.</summary>
    public int AssemblyType { get; }

    /// <summary>The assembly class code.</summary>
    public int Class { get; }

    /// <summary>The main part of the assembly, or <c>null</c> if not yet resolved.</summary>
    public ModelPart? MainPart { get; internal set; }

    /// <summary>The secondary parts attached to the assembly.</summary>
    public IReadOnlyList<ModelPart> Secondaries { get; internal set; }

    /// <summary>All parts belonging to the assembly (main part and secondaries).</summary>
    public IReadOnlyList<ModelPart> Parts { get; internal set; }

    /// <summary>All bolts belonging to or connecting parts of this assembly.</summary>
    public IReadOnlyList<ModelBolt> Bolts { get; internal set; }

    /// <summary>All welds belonging to or connecting parts of this assembly.</summary>
    public IReadOnlyList<ModelWeld> Welds { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => $"Assembly #{Id} '{Name}' Mark={Prefix}{PositionNumber} Parts={Parts.Count}";
}

/// <summary>Represents an individual structural part (steel beam, column, plate, concrete slab, wall, etc.).</summary>
public sealed class ModelPart : ModelObject
{
    internal ModelPart(
        int id,
        Guid guid,
        int type,
        int subtype,
        int phase,
        int? parentId,
        int? assemblyId,
        string name,
        string profile,
        string material,
        string prefix,
        int startNumber,
        int positionNumber,
        int @class,
        string finish,
        ModelPoint? startPoint,
        ModelPoint? endPoint,
        double length,
        ModelCoordsys? coordinateSystem,
        IReadOnlyDictionary<string, string>? stringAttributes = null,
        IReadOnlyDictionary<string, double>? numericAttributes = null,
        long lengthOffset = 0)
        : base(id, guid, type, subtype, phase, parentId, assemblyId, stringAttributes, numericAttributes)
    {
        Name = name ?? string.Empty;
        Profile = profile ?? string.Empty;
        Material = material ?? string.Empty;
        Prefix = prefix ?? string.Empty;
        StartNumber = startNumber;
        PositionNumber = positionNumber;
        Class = @class;
        Finish = finish ?? string.Empty;
        StartPoint = startPoint;
        EndPoint = endPoint;
        Length = length;
        CoordinateSystem = coordinateSystem;
        LengthOffset = lengthOffset;
        Bolts = Array.Empty<ModelBolt>();
        Welds = Array.Empty<ModelWeld>();
    }

    internal long LengthOffset { get; init; }

    /// <summary>The part name (e.g. "BEAM", "COLUMN", "PLATE").</summary>
    public string Name { get; }

    /// <summary>The resolved profile name (e.g. "HEA300", "IPE200", "PL10*150").</summary>
    public string Profile { get; }

    /// <summary>The material grade name (e.g. "S355J2", "S235JR", "C30/37").</summary>
    public string Material { get; }

    /// <summary>The part numbering prefix (e.g. "b", "p").</summary>
    public string Prefix { get; }

    /// <summary>The start number for part numbering.</summary>
    public int StartNumber { get; }

    /// <summary>The part mark position number.</summary>
    public int PositionNumber { get; }

    /// <summary>The part display class / color index.</summary>
    public int Class { get; }

    /// <summary>The surface finish description.</summary>
    public string Finish { get; }

    /// <summary>The parent assembly this part belongs to, or <c>null</c> if standalone.</summary>
    public ModelAssembly? Assembly { get; internal set; }

    /// <summary>Whether this part is the main part of its assembly.</summary>
    public bool IsMainPart => Assembly?.MainPart == this;

    /// <summary>The start point coordinate in model space.</summary>
    public ModelPoint? StartPoint { get; }

    /// <summary>The end point coordinate in model space.</summary>
    public ModelPoint? EndPoint { get; }

    /// <summary>The geometric length of the part.</summary>
    public double Length { get; }

    /// <summary>The local coordinate system origin and scale.</summary>
    public ModelCoordsys? CoordinateSystem { get; }

    /// <summary>All bolts connected to this part.</summary>
    public IReadOnlyList<ModelBolt> Bolts { get; internal set; }

    /// <summary>All welds connected to this part.</summary>
    public IReadOnlyList<ModelWeld> Welds { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => $"Part #{Id} '{Name}' {Profile} ({Material}) Mark={Prefix}{PositionNumber}";
}

/// <summary>Represents a bolt group connecting two or more parts.</summary>
public sealed class ModelBolt : ModelObject
{
    internal ModelBolt(
        int id,
        Guid guid,
        int type,
        int subtype,
        int phase,
        int? parentId,
        int? assemblyId,
        ModelPoint? firstPoint,
        ModelPoint? secondPoint,
        IReadOnlyList<ModelBoltHole> holes,
        IReadOnlyList<ModelPoint> positions,
        IReadOnlyDictionary<string, string>? stringAttributes = null,
        IReadOnlyDictionary<string, double>? numericAttributes = null)
        : base(id, guid, type, subtype, phase, parentId, assemblyId, stringAttributes, numericAttributes)
    {
        FirstPoint = firstPoint;
        SecondPoint = secondPoint;
        Holes = holes ?? Array.Empty<ModelBoltHole>();
        Positions = positions ?? Array.Empty<ModelPoint>();
        ConnectedParts = Array.Empty<ModelPart>();
    }

    /// <summary>The first reference point / origin of the bolt group.</summary>
    public ModelPoint? FirstPoint { get; }

    /// <summary>The second reference point / direction of the bolt group.</summary>
    public ModelPoint? SecondPoint { get; }

    /// <summary>The parts connected through the bolt holes.</summary>
    public IReadOnlyList<ModelPart> ConnectedParts { get; internal set; }

    /// <summary>The individual holes penetrating the connected parts.</summary>
    public IReadOnlyList<ModelBoltHole> Holes { get; }

    /// <summary>The 3D coordinates of the bolt locations in the pattern.</summary>
    public IReadOnlyList<ModelPoint> Positions { get; }

    /// <summary>The assembly this bolt belongs to, or <c>null</c> if not set.</summary>
    public ModelAssembly? Assembly { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => $"Bolt #{Id} ({Guid}) Holes={Holes.Count} ConnectedParts={ConnectedParts.Count}";
}

/// <summary>Represents a structural weld connecting two or more parts.</summary>
public sealed class ModelWeld : ModelObject
{
    internal ModelWeld(
        int id,
        Guid guid,
        int type,
        int subtype,
        int phase,
        int? parentId,
        int? assemblyId,
        int commonAttrId,
        IReadOnlyDictionary<string, string>? stringAttributes = null,
        IReadOnlyDictionary<string, double>? numericAttributes = null)
        : base(id, guid, type, subtype, phase, parentId, assemblyId, stringAttributes, numericAttributes)
    {
        CommonAttrId = commonAttrId;
        ConnectedParts = Array.Empty<ModelPart>();
    }

    /// <summary>The identifier of the common weld attributes row.</summary>
    public int CommonAttrId { get; }

    /// <summary>The parent connection or component creating this weld, or <c>null</c> if none.</summary>
    public ModelObject? Parent { get; internal set; }

    /// <summary>The assembly this weld belongs to, or <c>null</c> if not set.</summary>
    public ModelAssembly? Assembly { get; internal set; }

    /// <summary>The parts joined by this weld.</summary>
    public IReadOnlyList<ModelPart> ConnectedParts { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => $"Weld #{Id} ({Guid}) AttrId={CommonAttrId}";
}

/// <summary>A 3D coordinate point in model space.</summary>
/// <param name="Id">The point identifier.</param>
/// <param name="X">The X coordinate in millimeters.</param>
/// <param name="Y">The Y coordinate in millimeters.</param>
/// <param name="Z">The Z coordinate in millimeters.</param>
public readonly record struct ModelPoint(int Id, double X, double Y, double Z)
{
    internal long Offset { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}

/// <summary>A coordinate system origin and scale.</summary>
/// <param name="Id">The coordinate system identifier.</param>
/// <param name="X">Origin X coordinate.</param>
/// <param name="Y">Origin Y coordinate.</param>
/// <param name="Z">Origin Z coordinate.</param>
/// <param name="Length">Length or scale factor.</param>
public readonly record struct ModelCoordsys(int Id, double X, double Y, double Z, double Length)
{
    internal long Offset { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"Origin=({X:F2}, {Y:F2}, {Z:F2}), Length={Length:F2}";
}

/// <summary>A single hole in a connected part penetrated by a bolt group.</summary>
/// <param name="No">The hole index in the group.</param>
/// <param name="PartId">The identifier of the penetrated part.</param>
/// <param name="Part">The resolved penetrated part, or <c>null</c> if not found.</param>
/// <param name="PointZ">The Z offset along the bolt axis.</param>
/// <param name="Length">The grip length in this part.</param>
/// <param name="LongHoleDir">Slotted hole direction.</param>
/// <param name="LongHoleType">Slotted hole type.</param>
/// <param name="ShorteningLength">Hole shortening length.</param>
public readonly record struct ModelBoltHole(
    int No,
    int PartId,
    ModelPart? Part,
    double PointZ,
    double Length,
    int LongHoleDir,
    int LongHoleType,
    double ShorteningLength)
{
    internal long Offset { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"Hole #{No} in Part #{PartId} Grip={Length:F1}";
}
