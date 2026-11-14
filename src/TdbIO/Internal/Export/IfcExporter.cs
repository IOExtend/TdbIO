using System.Globalization;
using System.Text;
using TdbIO;

namespace TdbIO.Internal.Export;

/// <summary>
/// Exports <see cref="TeklaModel"/> to Industry Foundation Classes (IFC2x3 / IFC4)
/// in ISO 10303-21 STEP physical file format.
/// </summary>
internal static class IfcExporter
{
    private const string DegreeInRadians = "0.017453292519943278";

    /// <summary>Writes a <see cref="TeklaModel"/> to an IFC file at the specified path.</summary>
    public static void WriteFile(TeklaModel model, string path, TeklaIfcExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(path);

        using var fs = File.Create(path);
        using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 65536);
        Write(model, writer, options, Path.GetFileName(path));
    }

    /// <summary>Writes a <see cref="TeklaModel"/> to a <see cref="Stream"/>.</summary>
    public static void Write(TeklaModel model, Stream stream, TeklaIfcExportOptions? options = null, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 65536, leaveOpen: true);
        Write(model, writer, options, fileName);
    }

    /// <summary>Converts a <see cref="TeklaModel"/> to an IFC STEP string.</summary>
    public static string ToString(TeklaModel model, TeklaIfcExportOptions? options = null)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        Write(model, sw, options, "model.ifc");
        return sw.ToString();
    }

    /// <summary>Writes a <see cref="TeklaModel"/> to a <see cref="TextWriter"/>.</summary>
    public static void Write(
        TeklaModel model,
        TextWriter writer,
        TeklaIfcExportOptions? options = null,
        string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(writer);

        model.ValidateFiniteNumbers();

        options ??= new TeklaIfcExportOptions();
        fileName ??= "model.ifc";

        bool isIfc4 = string.Equals(options.SchemaVersion, "IFC4", StringComparison.OrdinalIgnoreCase);
        string schemaName = isIfc4 ? "IFC4" : "IFC2X3";
        string viewDef = isIfc4 ? "ViewDefinition [DesignTransferView_1.0]" : "ViewDefinition [CoordinationView_V2.0]";

        var w = new StepWriter();

        // 1. Project infrastructure & Owner History
        int personId = w.Add("IFCPERSON", $"$,{StepWriter.Str(options.Author)},$,$,$,$,$,$");
        int orgId = w.Add("IFCORGANIZATION", $"$,{StepWriter.Str(options.Organization)},$,$,$");
        int personOrgId = w.Add("IFCPERSONANDORGANIZATION", $"{StepWriter.Ref(personId)},{StepWriter.Ref(orgId)},$");
        int appId = w.Add("IFCAPPLICATION", $"{StepWriter.Ref(orgId)},'0.1.0',{StepWriter.Str(options.Application)},'TdbIO'");

        int epochSeconds = (int)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        int ownerHistoryId = w.Add("IFCOWNERHISTORY",
            $"{StepWriter.Ref(personOrgId)},{StepWriter.Ref(appId)},.READWRITE.,.NOCHANGE.,$,$,$,{epochSeconds}");

        // 2. Units & Geometric Context
        int lengthUnitId = w.Add("IFCSIUNIT", options.ConvertMmToMetres ? "*,.LENGTHUNIT.,$,.METRE." : "*,.LENGTHUNIT.,.MILLI.,.METRE.");
        int radianId = w.Add("IFCSIUNIT", "*,.PLANEANGLEUNIT.,$,.RADIAN.");
        int dimExpId = w.Add("IFCDIMENSIONALEXPONENTS", "0,0,0,0,0,0,0");
        int angleMeasureId = w.Add("IFCMEASUREWITHUNIT", $"IFCPLANEANGLEMEASURE({DegreeInRadians}),{StepWriter.Ref(radianId)}");
        int degreeUnitId = w.Add("IFCCONVERSIONBASEDUNIT", $"{StepWriter.Ref(dimExpId)},.PLANEANGLEUNIT.,'DEGREE',{StepWriter.Ref(angleMeasureId)}");
        int areaUnitId = w.Add("IFCSIUNIT", "*,.AREAUNIT.,$,.SQUARE_METRE.");
        int volumeUnitId = w.Add("IFCSIUNIT", "*,.VOLUMEUNIT.,$,.CUBIC_METRE.");

        int unitAssignmentId = w.Add("IFCUNITASSIGNMENT",
            StepWriter.List(StepWriter.Ref(lengthUnitId), StepWriter.Ref(degreeUnitId), StepWriter.Ref(areaUnitId), StepWriter.Ref(volumeUnitId)));

        int originPointId = w.Add("IFCCARTESIANPOINT", StepWriter.List(StepWriter.Real(0), StepWriter.Real(0), StepWriter.Real(0)));
        int axisZId = w.Add("IFCDIRECTION", StepWriter.List(StepWriter.Real(0), StepWriter.Real(0), StepWriter.Real(1)));
        int axisXId = w.Add("IFCDIRECTION", StepWriter.List(StepWriter.Real(1), StepWriter.Real(0), StepWriter.Real(0)));
        int worldPlacement3dId = w.Add("IFCAXIS2PLACEMENT3D",
            $"{StepWriter.Ref(originPointId)},{StepWriter.Ref(axisZId)},{StepWriter.Ref(axisXId)}");

        int mainContextId = w.Add("IFCGEOMETRICREPRESENTATIONCONTEXT",
            $"$,'Model',3,1.E-05,{StepWriter.Ref(worldPlacement3dId)},$");
        int bodySubContextId = w.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT",
            $"'Body','Model',*,*,*,*,{StepWriter.Ref(mainContextId)},$,.MODEL_VIEW.,$");

        // 3. Spatial Hierarchy: Project -> Site -> Building -> Storey
        string projectName = options.ProjectName
            ?? (model.ModelGuid != Guid.Empty ? $"Tekla Model {model.ModelGuid:D}" : "Tekla Model");
        string buildingName = options.BuildingName;
        string storeyName = options.StoreyName;

        string projectGuid = model.ModelGuid != Guid.Empty
            ? IfcGuid.ToIfcGuid(model.ModelGuid)
            : IfcGuid.NewGuid();

        int projectId = w.Add("IFCPROJECT",
            $"{StepWriter.Str(projectGuid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(projectName)},$,$,$,$,({StepWriter.Ref(mainContextId)}),{StepWriter.Ref(unitAssignmentId)}");

        int sitePlacementId = w.Add("IFCLOCALPLACEMENT", $"$,{StepWriter.Ref(worldPlacement3dId)}");
        int siteId = w.Add("IFCSITE",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},'Default Site',$,$,{StepWriter.Ref(sitePlacementId)},$,$,.ELEMENT.,$,$,$,$,$");

        int buildingPlacementId = w.Add("IFCLOCALPLACEMENT", $"{StepWriter.Ref(sitePlacementId)},{StepWriter.Ref(worldPlacement3dId)}");
        int buildingId = w.Add("IFCBUILDING",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(buildingName)},$,$,{StepWriter.Ref(buildingPlacementId)},$,$,.ELEMENT.,$,$,$");

        int storeyPlacementId = w.Add("IFCLOCALPLACEMENT", $"{StepWriter.Ref(buildingPlacementId)},{StepWriter.Ref(worldPlacement3dId)}");
        int storeyId = w.Add("IFCBUILDINGSTOREY",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(storeyName)},$,$,{StepWriter.Ref(storeyPlacementId)},$,$,.ELEMENT.,{StepWriter.Real(0)}");

        w.Add("IFCRELAGGREGATES",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,{StepWriter.Ref(projectId)},({StepWriter.Ref(siteId)})");
        w.Add("IFCRELAGGREGATES",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,{StepWriter.Ref(siteId)},({StepWriter.Ref(buildingId)})");
        w.Add("IFCRELAGGREGATES",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,{StepWriter.Ref(buildingId)},({StepWriter.Ref(storeyId)})");

        // 4. Material Cache
        var materialCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // 5. Parts Mapping
        double scale = options.ConvertMmToMetres ? 0.001 : 1.0;
        var partStepIds = new Dictionary<int, int>();
        var standalonePartIds = new List<int>();

        foreach (var part in model.Parts)
        {
            int? shapeRepId = null;
            if (options.ExportGeometry && part.StartPoint.HasValue && part.EndPoint.HasValue)
            {
                var p0 = part.StartPoint.Value;
                var p1 = part.EndPoint.Value;
                int geomId = WritePartFacetedBrep(w, p0, p1, part.Profile, scale);
                shapeRepId = w.Add("IFCSHAPEREPRESENTATION",
                    $"{StepWriter.Ref(bodySubContextId)},'Body','Brep',({StepWriter.Ref(geomId)})");
            }

            int? prodShapeId = shapeRepId.HasValue
                ? w.Add("IFCPRODUCTDEFINITIONSHAPE", $"$,$,({StepWriter.Ref(shapeRepId.Value)})")
                : null;

            int elemPlacementId = w.Add("IFCLOCALPLACEMENT",
                $"{StepWriter.Ref(storeyPlacementId)},{StepWriter.Ref(worldPlacement3dId)}");

            string ifcType = ClassifyPart(part);
            string elemGuid = part.IfcGuid;
            string elemName = string.IsNullOrEmpty(part.Name) ? $"Part_{part.Id}" : part.Name;
            string elemTag = part.Id.ToString(CultureInfo.InvariantCulture);

            int partStepId = AddIfcProduct(w, ifcType, elemGuid, ownerHistoryId, elemName, elemPlacementId, prodShapeId, elemTag, isIfc4);
            partStepIds[part.Id] = partStepId;

            // Material
            if (!string.IsNullOrEmpty(part.Material))
            {
                if (!materialCache.TryGetValue(part.Material, out int matId))
                {
                    matId = w.Add("IFCMATERIAL", isIfc4 ? $"{StepWriter.Str(part.Material)},$,$" : StepWriter.Str(part.Material));
                    materialCache[part.Material] = matId;
                }
                w.Add("IFCRELASSOCIATESMATERIAL",
                    $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,({StepWriter.Ref(partStepId)}),{StepWriter.Ref(matId)}");
            }

            // Properties
            if (options.ExportProperties)
            {
                WritePartProperties(w, partStepId, part, ownerHistoryId, scale);
            }

            if (!options.ExportAssemblies || part.Assembly == null)
            {
                standalonePartIds.Add(partStepId);
            }
        }

        // 6. Assemblies Mapping
        var assemblyStepIds = new List<int>();
        if (options.ExportAssemblies)
        {
            foreach (var asm in model.Assemblies)
            {
                int asmPlacementId = w.Add("IFCLOCALPLACEMENT",
                    $"{StepWriter.Ref(storeyPlacementId)},{StepWriter.Ref(worldPlacement3dId)}");

                string asmGuid = asm.IfcGuid;
                string asmName = string.IsNullOrEmpty(asm.Name) ? $"Assembly_{asm.Id}" : asm.Name;
                string asmTag = asm.Id.ToString(CultureInfo.InvariantCulture);

                int asmStepId = w.Add("IFCELEMENTASSEMBLY",
                    $"{StepWriter.Str(asmGuid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(asmName)},$,$,{StepWriter.Ref(asmPlacementId)},$,{StepWriter.Str(asmTag)},.NOTDEFINED.,.NOTDEFINED.");
                assemblyStepIds.Add(asmStepId);

                // Relate parts to this assembly
                var childPartStepIds = new List<int>();
                foreach (var childPart in asm.Parts)
                {
                    if (partStepIds.TryGetValue(childPart.Id, out int childStepId))
                    {
                        childPartStepIds.Add(childStepId);
                    }
                }

                if (childPartStepIds.Count > 0)
                {
                    w.Add("IFCRELAGGREGATES",
                        $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,{StepWriter.Ref(asmStepId)},{StepWriter.List(childPartStepIds.Select(StepWriter.Ref))}");
                }

                if (options.ExportProperties)
                {
                    WriteAssemblyProperties(w, asmStepId, asm, ownerHistoryId);
                }
            }
        }

        // 7. Bolts Mapping
        var boltStepIds = new List<int>();
        if (options.ExportBolts)
        {
            foreach (var bolt in model.Bolts)
            {
                int boltPlacementId = w.Add("IFCLOCALPLACEMENT",
                    $"{StepWriter.Ref(storeyPlacementId)},{StepWriter.Ref(worldPlacement3dId)}");

                string boltGuid = bolt.IfcGuid;
                string boltTag = bolt.Id.ToString(CultureInfo.InvariantCulture);

                int boltStepId = w.Add("IFCMECHANICALFASTENER",
                    isIfc4
                        ? $"{StepWriter.Str(boltGuid)},{StepWriter.Ref(ownerHistoryId)},'Bolt Group',$,$,{StepWriter.Ref(boltPlacementId)},$,{StepWriter.Str(boltTag)},$,$,.BOLT."
                        : $"{StepWriter.Str(boltGuid)},{StepWriter.Ref(ownerHistoryId)},'Bolt Group',$,$,{StepWriter.Ref(boltPlacementId)},$,{StepWriter.Str(boltTag)},$,$");
                boltStepIds.Add(boltStepId);

                if (options.ExportProperties)
                {
                    WriteBoltProperties(w, boltStepId, bolt, ownerHistoryId);
                }
            }
        }

        // 8. Welds Mapping
        var weldStepIds = new List<int>();
        if (options.ExportWelds)
        {
            foreach (var weld in model.Welds)
            {
                int weldPlacementId = w.Add("IFCLOCALPLACEMENT",
                    $"{StepWriter.Ref(storeyPlacementId)},{StepWriter.Ref(worldPlacement3dId)}");

                string weldGuid = weld.IfcGuid;
                string weldTag = weld.Id.ToString(CultureInfo.InvariantCulture);

                int weldStepId = w.Add("IFCFASTENER",
                    isIfc4
                        ? $"{StepWriter.Str(weldGuid)},{StepWriter.Ref(ownerHistoryId)},'Weld',$,$,{StepWriter.Ref(weldPlacementId)},$,{StepWriter.Str(weldTag)},.WELD."
                        : $"{StepWriter.Str(weldGuid)},{StepWriter.Ref(ownerHistoryId)},'Weld',$,$,{StepWriter.Ref(weldPlacementId)},$,{StepWriter.Str(weldTag)}");
                weldStepIds.Add(weldStepId);

                if (options.ExportProperties)
                {
                    WriteWeldProperties(w, weldStepId, weld, ownerHistoryId);
                }
            }
        }

        // 9. Spatial Containment in Storey
        var containedIds = new List<int>();
        containedIds.AddRange(assemblyStepIds);
        containedIds.AddRange(standalonePartIds);
        containedIds.AddRange(boltStepIds);
        containedIds.AddRange(weldStepIds);

        if (containedIds.Count > 0)
        {
            w.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE",
                $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,{StepWriter.List(containedIds.Select(StepWriter.Ref))},{StepWriter.Ref(storeyId)}");
        }

        // 10. Write complete STEP file
        w.WriteTo(writer, fileName, schemaName, viewDef, options.Application, options.Organization, options.Author);
    }

    private static string ClassifyPart(ModelPart part)
    {
        string name = part.Name.ToUpperInvariant();
        string profile = part.Profile.ToUpperInvariant();

        if (name.Contains("BEAM") || name.Contains("BALKA") || name.Contains("GIRDER") ||
            name.Contains("JOIST") || name.Contains("PURLIN") || name.Contains("RAFTER") || name.Contains("RIGEL"))
        {
            return "IFCBEAM";
        }

        if (name.Contains("COLUMN") || name.Contains("KOLONNA") || name.Contains("PILLAR") ||
            name.Contains("POST") || name.Contains("STANCHION") || name.Contains("STOIKA"))
        {
            return "IFCCOLUMN";
        }

        if (name.Contains("PLATE") || name.Contains("PLASTINA") || name.Contains("LIST") ||
            name.Contains("SLAB") || name.Contains("FLANGE") || name.Contains("WEB") ||
            name.Contains("STIFFENER") || profile.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
        {
            return "IFCPLATE";
        }

        return "IFCMEMBER";
    }

    private static int AddIfcProduct(
        StepWriter w,
        string ifcType,
        string guid,
        int ownerHistoryId,
        string name,
        int placementId,
        int? shapeId,
        string tag,
        bool isIfc4)
    {
        string shapeArg = shapeId.HasValue ? StepWriter.Ref(shapeId.Value) : "$";
        return ifcType switch
        {
            "IFCBEAM" => w.Add("IFCBEAM",
                $"{StepWriter.Str(guid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(name)},$,$,{StepWriter.Ref(placementId)},{shapeArg},{StepWriter.Str(tag)}" + (isIfc4 ? ",.BEAM." : "")),
            "IFCCOLUMN" => w.Add("IFCCOLUMN",
                $"{StepWriter.Str(guid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(name)},$,$,{StepWriter.Ref(placementId)},{shapeArg},{StepWriter.Str(tag)}" + (isIfc4 ? ",.COLUMN." : "")),
            "IFCPLATE" => w.Add("IFCPLATE",
                $"{StepWriter.Str(guid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(name)},$,$,{StepWriter.Ref(placementId)},{shapeArg},{StepWriter.Str(tag)}" + (isIfc4 ? ",.NOTDEFINED." : "")),
            _ => w.Add("IFCMEMBER",
                $"{StepWriter.Str(guid)},{StepWriter.Ref(ownerHistoryId)},{StepWriter.Str(name)},$,$,{StepWriter.Ref(placementId)},{shapeArg},{StepWriter.Str(tag)}" + (isIfc4 ? ",.MEMBER." : "")),
        };
    }

    private static int WritePartFacetedBrep(StepWriter w, ModelPoint p0, ModelPoint p1, string profile, double scale)
    {
        // Vector along the beam axis
        double dx = (p1.X - p0.X) * scale;
        double dy = (p1.Y - p0.Y) * scale;
        double dz = (p1.Z - p0.Z) * scale;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        // Normalize direction
        double dirX = len > 1e-6 ? dx / len : 1.0;
        double dirY = len > 1e-6 ? dy / len : 0.0;
        double dirZ = len > 1e-6 ? dz / len : 0.0;

        // Find perpendicular vectors (u and v)
        double perpX, perpY, perpZ;
        if (Math.Abs(dirZ) < 0.9)
        {
            // Cross product with Z (0, 0, 1)
            perpX = dirY;
            perpY = -dirX;
            perpZ = 0.0;
        }
        else
        {
            // Cross product with Y (0, 1, 0)
            perpX = -dirZ;
            perpY = 0.0;
            perpZ = dirX;
        }
        double perpLen = Math.Sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);
        if (perpLen > 1e-6)
        {
            perpX /= perpLen;
            perpY /= perpLen;
            perpZ /= perpLen;
        }

        // Second perpendicular vector: cross(dir, perp)
        double vX = dirY * perpZ - dirZ * perpY;
        double vY = dirZ * perpX - dirX * perpZ;
        double vZ = dirX * perpY - dirY * perpX;

        // Default half-width and half-height (approximate from profile if possible)
        (double hw, double hh) = EstimateProfileDimensions(profile, scale);

        // 8 box vertices: 4 at p0, 4 at p1
        double x0 = p0.X * scale;
        double y0 = p0.Y * scale;
        double z0 = p0.Z * scale;
        double x1 = p1.X * scale;
        double y1 = p1.Y * scale;
        double z1 = p1.Z * scale;

        int[] pts = new int[8];
        // At start p0
        pts[0] = AddPoint(w, x0 - perpX * hw - vX * hh, y0 - perpY * hw - vY * hh, z0 - perpZ * hw - vZ * hh);
        pts[1] = AddPoint(w, x0 + perpX * hw - vX * hh, y0 + perpY * hw - vY * hh, z0 + perpZ * hw - vZ * hh);
        pts[2] = AddPoint(w, x0 + perpX * hw + vX * hh, y0 + perpY * hw + vY * hh, z0 + perpZ * hw + vZ * hh);
        pts[3] = AddPoint(w, x0 - perpX * hw + vX * hh, y0 - perpY * hw + vY * hh, z0 - perpZ * hw + vZ * hh);
        // At end p1
        pts[4] = AddPoint(w, x1 - perpX * hw - vX * hh, y1 - perpY * hw - vY * hh, z1 - perpZ * hw - vZ * hh);
        pts[5] = AddPoint(w, x1 + perpX * hw - vX * hh, y1 + perpY * hw - vY * hh, z1 + perpZ * hw - vZ * hh);
        pts[6] = AddPoint(w, x1 + perpX * hw + vX * hh, y1 + perpY * hw + vY * hh, z1 + perpZ * hw + vZ * hh);
        pts[7] = AddPoint(w, x1 - perpX * hw + vX * hh, y1 - perpY * hw + vY * hh, z1 - perpZ * hw + vZ * hh);

        // 6 faces (each as outer polyloop)
        var faces = new List<int>(6);
        faces.Add(CreateFace(w, pts[3], pts[2], pts[1], pts[0])); // start cap
        faces.Add(CreateFace(w, pts[4], pts[5], pts[6], pts[7])); // end cap
        faces.Add(CreateFace(w, pts[0], pts[1], pts[5], pts[4])); // bottom
        faces.Add(CreateFace(w, pts[1], pts[2], pts[6], pts[5])); // right
        faces.Add(CreateFace(w, pts[2], pts[3], pts[7], pts[6])); // top
        faces.Add(CreateFace(w, pts[3], pts[0], pts[4], pts[7])); // left

        int shellId = w.Add("IFCCLOSEDSHELL", StepWriter.List(faces.Select(StepWriter.Ref)));
        return w.Add("IFCFACETEDBREP", StepWriter.Ref(shellId));
    }

    private static (double HalfWidth, double HalfHeight) EstimateProfileDimensions(string profile, double scale)
    {
        double widthMm = 150.0;
        double heightMm = 150.0;

        if (!string.IsNullOrEmpty(profile))
        {
            string p = profile.ToUpperInvariant();
            if (p.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
            {
                // PL10*150 or PL10X150
                var parts = p[2..].Split(['*', 'x', 'X']);
                if (parts.Length >= 2)
                {
                    bool parsedT = double.TryParse(parts[0], CultureInfo.InvariantCulture, out double t);
                    bool parsedW = double.TryParse(parts[1], CultureInfo.InvariantCulture, out double w);

                    if ((parsedT && !double.IsFinite(t)) || (parsedW && !double.IsFinite(w)))
                    {
                        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Non-finite dimension in profile '{profile}'."));
                    }

                    if (parsedT && parsedW && t > 0 && w > 0)
                    {
                        heightMm = t;
                        widthMm = w;
                    }
                }
            }
            else
            {
                // HEA300, IPE200, etc. Extract first integer
                int idx = 0;
                while (idx < p.Length && !char.IsDigit(p[idx])) idx++;
                int end = idx;
                while (end < p.Length && char.IsDigit(p[end])) end++;
                if (end > idx && int.TryParse(p[idx..end], CultureInfo.InvariantCulture, out int size) && size > 10)
                {
                    heightMm = size;
                    widthMm = p.StartsWith("IPE") ? size * 0.5 : size;
                }
            }
        }

        if (!double.IsFinite(widthMm) || !double.IsFinite(heightMm))
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Non-finite dimension in profile '{profile}'."));
        }

        return (widthMm * 0.5 * scale, heightMm * 0.5 * scale);
    }

    private static int AddPoint(StepWriter w, double x, double y, double z) =>
        w.Add("IFCCARTESIANPOINT", StepWriter.List(StepWriter.Real(x), StepWriter.Real(y), StepWriter.Real(z)));

    private static int CreateFace(StepWriter w, int p0, int p1, int p2, int p3)
    {
        int loopId = w.Add("IFCPOLYLOOP",
            StepWriter.List(StepWriter.Ref(p0), StepWriter.Ref(p1), StepWriter.Ref(p2), StepWriter.Ref(p3)));
        int boundId = w.Add("IFCFACEOUTERBOUND", $"{StepWriter.Ref(loopId)},.T.");
        return w.Add("IFCFACE", StepWriter.List(StepWriter.Ref(boundId)));
    }

    private static void WritePartProperties(StepWriter w, int partStepId, ModelPart part, int ownerHistoryId, double scale)
    {
        var props = new List<int>();

        void AddLabel(string name, string? val)
        {
            if (!string.IsNullOrEmpty(val))
            {
                props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str(name)},$,IFCLABEL({StepWriter.Str(val)}),$"));
            }
        }

        void AddInteger(string name, int val)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str(name)},$,IFCINTEGER({val}),$"));
        }

        void AddReal(string name, double val)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str(name)},$,IFCREAL({StepWriter.RealExact(val)}),$"));
        }

        AddLabel("Tekla_PartMark", $"{part.Prefix}{part.PositionNumber}");
        AddLabel("Tekla_Profile", part.Profile);
        AddLabel("Tekla_Material", part.Material);
        AddLabel("Tekla_Finish", part.Finish);
        AddInteger("Tekla_Class", part.Class);
        AddInteger("Tekla_Phase", part.Phase);
        if (part.Length > 0)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str("Tekla_Length")},$,IFCLENGTHMEASURE({StepWriter.RealExact(part.Length * scale)}),$"));
        }

        foreach (var (k, v) in part.StringAttributes)
        {
            AddLabel($"Tekla_{k}", v);
        }

        foreach (var (k, v) in part.NumericAttributes)
        {
            AddReal($"Tekla_{k}", v);
        }

        if (props.Count > 0)
        {
            int psetId = w.Add("IFCPROPERTYSET",
                $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},'Tekla_PartAttributes',$,{StepWriter.List(props.Select(StepWriter.Ref))}");
            w.Add("IFCRELDEFINESBYPROPERTIES",
                $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,({StepWriter.Ref(partStepId)}),{StepWriter.Ref(psetId)}");
        }
    }

    private static void WriteAssemblyProperties(StepWriter w, int asmStepId, ModelAssembly asm, int ownerHistoryId)
    {
        var props = new List<int>();

        void AddLabel(string name, string? val)
        {
            if (!string.IsNullOrEmpty(val))
            {
                props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str(name)},$,IFCLABEL({StepWriter.Str(val)}),$"));
            }
        }

        void AddInteger(string name, int val)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str(name)},$,IFCINTEGER({val}),$"));
        }

        AddLabel("Tekla_AssemblyMark", $"{asm.Prefix}{asm.PositionNumber}");
        AddLabel("Tekla_Prefix", asm.Prefix);
        AddInteger("Tekla_StartNumber", asm.StartNumber);
        AddInteger("Tekla_PositionNumber", asm.PositionNumber);
        AddInteger("Tekla_Class", asm.Class);
        AddInteger("Tekla_Phase", asm.Phase);
        AddInteger("Tekla_AssemblyType", asm.AssemblyType);

        foreach (var (k, v) in asm.StringAttributes)
        {
            AddLabel($"Tekla_{k}", v);
        }

        foreach (var (k, v) in asm.NumericAttributes)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str($"Tekla_{k}")},$,IFCREAL({StepWriter.RealExact(v)}),$"));
        }

        if (props.Count > 0)
        {
            int psetId = w.Add("IFCPROPERTYSET",
                $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},'Tekla_AssemblyAttributes',$,{StepWriter.List(props.Select(StepWriter.Ref))}");
            w.Add("IFCRELDEFINESBYPROPERTIES",
                $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,({StepWriter.Ref(asmStepId)}),{StepWriter.Ref(psetId)}");
        }
    }

    private static void WriteBoltProperties(StepWriter w, int boltStepId, ModelBolt bolt, int ownerHistoryId)
    {
        var props = new List<int>
        {
            w.Add("IFCPROPERTYSINGLEVALUE", $"'Tekla_HoleCount',$,IFCINTEGER({bolt.Holes.Count}),$"),
            w.Add("IFCPROPERTYSINGLEVALUE", $"'Tekla_PositionCount',$,IFCINTEGER({bolt.Positions.Count}),$"),
            w.Add("IFCPROPERTYSINGLEVALUE", $"'Tekla_Phase',$,IFCINTEGER({bolt.Phase}),$")
        };

        foreach (var (k, v) in bolt.StringAttributes)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str($"Tekla_{k}")},$,IFCLABEL({StepWriter.Str(v)}),$"));
        }

        int psetId = w.Add("IFCPROPERTYSET",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},'Tekla_BoltAttributes',$,{StepWriter.List(props.Select(StepWriter.Ref))}");
        w.Add("IFCRELDEFINESBYPROPERTIES",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,({StepWriter.Ref(boltStepId)}),{StepWriter.Ref(psetId)}");
    }

    private static void WriteWeldProperties(StepWriter w, int weldStepId, ModelWeld weld, int ownerHistoryId)
    {
        var props = new List<int>
        {
            w.Add("IFCPROPERTYSINGLEVALUE", $"'Tekla_CommonAttrId',$,IFCINTEGER({weld.CommonAttrId}),$"),
            w.Add("IFCPROPERTYSINGLEVALUE", $"'Tekla_Phase',$,IFCINTEGER({weld.Phase}),$")
        };

        foreach (var (k, v) in weld.StringAttributes)
        {
            props.Add(w.Add("IFCPROPERTYSINGLEVALUE", $"{StepWriter.Str($"Tekla_{k}")},$,IFCLABEL({StepWriter.Str(v)}),$"));
        }

        int psetId = w.Add("IFCPROPERTYSET",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},'Tekla_WeldAttributes',$,{StepWriter.List(props.Select(StepWriter.Ref))}");
        w.Add("IFCRELDEFINESBYPROPERTIES",
            $"{StepWriter.Str(IfcGuid.NewGuid())},{StepWriter.Ref(ownerHistoryId)},$,$,({StepWriter.Ref(weldStepId)}),{StepWriter.Ref(psetId)}");
    }
}
