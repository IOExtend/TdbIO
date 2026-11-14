using System.Globalization;
using System.Reflection;
using IOScene;
using TdbIO;
using Vector3d = IOScene.Vector3d;
using RgbaColor = IOScene.RgbaColor;

namespace TdbIO.Scene;

/// <summary>
/// Converts Tekla Structures models (<see cref="TeklaModel"/>) or model files into an <see cref="SceneDocument"/>.
/// </summary>
public static class TdbSceneReader
{
    private const int ModelId = 1;

    private static readonly string Producer = "TdbIO.Scene "
        + (typeof(TdbSceneReader).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.1.0");

    /// <summary>Reads a Tekla model database file (.db1) and converts it into a scene.</summary>
    /// <param name="path">Path to the model database file (.db1).</param>
    /// <param name="options">Conversion options, or <c>null</c> for defaults.</param>
    /// <returns>The scene and conversion report.</returns>
    public static TdbSceneResult Read(string path, TdbSceneOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string key = options?.ModelKey ?? Path.GetFileName(path);
        if (key.Length == 0)
        {
            throw new ArgumentException("TdbSceneOptions.ModelKey must not be empty; leave it null to use the file name.", nameof(options));
        }

        var doc = TdbFile.Open(path);
        if (doc is not Db1Document db1)
        {
            throw new NotSupportedException($"TdbIO.Scene requires a Tekla model database (.db1), but found '{doc.Kind}'.");
        }

        var model = db1.ReadModel();
        return Convert(model, key, path, options);
    }

    /// <summary>Converts an in-memory <see cref="TeklaModel"/> into a scene.</summary>
    /// <param name="model">The decoded Tekla model.</param>
    /// <param name="options">Conversion options.</param>
    /// <returns>The scene and conversion report.</returns>
    public static TdbSceneResult Read(TeklaModel model, TdbSceneOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (options?.ModelKey != null && options.ModelKey.Length == 0)
        {
            throw new ArgumentException("TdbSceneOptions.ModelKey must not be empty.", nameof(options));
        }

        string key = options?.ModelKey
            ?? (model.ModelGuid != Guid.Empty ? $"model_{model.ModelGuid:N}.db1" : "model.db1");

        return Convert(model, key, null, options);
    }

    private static TdbSceneResult Convert(TeklaModel model, string key, string? path, TdbSceneOptions? options)
    {
        model.ValidateFiniteNumbers();
        options ??= new TdbSceneOptions();
        var scene = new SceneDocument { Title = key };

        scene.Models.Add(new SceneModel
        {
            Id = ModelId,
            Key = key,
            Name = key,
            Path = path,
            SourceFormat = "db1",
            Producer = Producer,
            SourceLengthUnit = "mm",
            Application = "Tekla Structures",
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        });

        // Root element of the model
        const long rootElemId = 1L;
        scene.Elements.Add(new SceneElement
        {
            Id = rootElemId,
            ModelId = ModelId,
            Kind = SceneNodeKind.Model,
            Name = key,
        });

        long nextElemId = 2L;
        int nextPsetId = 1;
        int nextGeomId = 1;
        int nextMatId = 1;

        var materialCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var asmElementMap = new Dictionary<int, long>(model.Assemblies.Count);

        int convertedElements = 0;
        int elementsWithGeometry = 0;

        // 1. Assemblies
        foreach (var asm in model.Assemblies)
        {
            long asmId = nextElemId++;
            asmElementMap[asm.Id] = asmId;
            convertedElements++;

            string asmMark = $"{asm.Prefix}{asm.PositionNumber}".Trim();
            string asmName = string.IsNullOrEmpty(asm.Name) ? "Assembly" : asm.Name;
            if (!string.IsNullOrEmpty(asmMark))
            {
                asmName = $"{asmMark} {asmName}";
            }

            var asmElement = new SceneElement
            {
                Id = asmId,
                ModelId = ModelId,
                Kind = SceneNodeKind.Group,
                ParentId = rootElemId,
                Name = asmName,
                Category = "Assembly",
                TypeName = string.IsNullOrEmpty(asm.Prefix) ? "Assembly" : asm.Prefix,
                SourceId = asm.Guid != Guid.Empty ? asm.Guid.ToString("D") : $"asm:{asm.Id}",
                IfcGuid = asm.IfcGuid,
            };

            // Assembly PropertySet
            var asmProps = new List<SceneProperty>();
            void AddProp(string name, string? keyNamespace, SceneValue val) =>
                asmProps.Add(new SceneProperty { Name = name, Key = keyNamespace, Value = val });

            AddProp("Assembly Mark", "tekla:assembly:mark", SceneValue.FromText($"{asm.Prefix}{asm.PositionNumber}"));
            AddProp("Prefix", "tekla:assembly:prefix", SceneValue.FromText(asm.Prefix));
            AddProp("Start Number", "tekla:assembly:start_number", SceneValue.FromInteger(asm.StartNumber, asm.StartNumber.ToString(CultureInfo.InvariantCulture)));
            AddProp("Position Number", "tekla:assembly:position_number", SceneValue.FromInteger(asm.PositionNumber, asm.PositionNumber.ToString(CultureInfo.InvariantCulture)));
            AddProp("Class", "tekla:assembly:class", SceneValue.FromInteger(asm.Class, asm.Class.ToString(CultureInfo.InvariantCulture)));
            AddProp("Phase", "tekla:assembly:phase", SceneValue.FromInteger(asm.Phase, asm.Phase.ToString(CultureInfo.InvariantCulture)));
            AddProp("Assembly Type", "tekla:assembly:type", SceneValue.FromInteger(asm.AssemblyType, asm.AssemblyType.ToString(CultureInfo.InvariantCulture)));

            foreach (var (k, v) in asm.StringAttributes)
            {
                AddProp(k, $"tekla:attr:{k}", SceneValue.FromText(v));
            }
            foreach (var (k, v) in asm.NumericAttributes)
            {
                AddProp(k, $"tekla:attr:{k}", SceneValue.FromNumber(v, null, v.ToString("G", CultureInfo.InvariantCulture)));
            }

            var asmPset = new ScenePropertySet
            {
                Id = nextPsetId++,
                Name = "Assembly",
                Key = "tekla:assembly",
                Properties = asmProps,
            };
            scene.PropertySets.Add(asmPset);
            asmElement.PropertySetIds.Add(asmPset.Id);

            scene.Elements.Add(asmElement);
        }

        // 2. Parts
        foreach (var part in model.Parts)
        {
            long partId = nextElemId++;
            convertedElements++;

            long parentId = (part.Assembly != null && asmElementMap.TryGetValue(part.Assembly.Id, out var asmElemId))
                ? asmElemId
                : rootElemId;

            string partName = string.IsNullOrEmpty(part.Name) ? "Part" : part.Name;
            string partMark = $"{part.Prefix}{part.PositionNumber}".Trim();
            if (!string.IsNullOrEmpty(partMark))
            {
                partName = $"{partMark} {partName}";
            }

            var partElement = new SceneElement
            {
                Id = partId,
                ModelId = ModelId,
                Kind = SceneNodeKind.Element,
                ParentId = parentId,
                Name = partName,
                Category = "Part",
                TypeName = string.IsNullOrEmpty(part.Profile) ? "Part" : part.Profile,
                SourceId = part.Guid != Guid.Empty ? part.Guid.ToString("D") : $"part:{part.Id}",
                IfcGuid = part.IfcGuid,
            };

            // Part PropertySet
            var partProps = new List<SceneProperty>();
            void AddPartProp(string name, string? keyNamespace, SceneValue val) =>
                partProps.Add(new SceneProperty { Name = name, Key = keyNamespace, Value = val });

            AddPartProp("Part Mark", "tekla:part:mark", SceneValue.FromText($"{part.Prefix}{part.PositionNumber}"));
            AddPartProp("Profile", "tekla:part:profile", SceneValue.FromText(part.Profile));
            AddPartProp("Material", "tekla:part:material", SceneValue.FromText(part.Material));
            AddPartProp("Class", "tekla:part:class", SceneValue.FromInteger(part.Class, part.Class.ToString(CultureInfo.InvariantCulture)));
            AddPartProp("Phase", "tekla:part:phase", SceneValue.FromInteger(part.Phase, part.Phase.ToString(CultureInfo.InvariantCulture)));
            if (!string.IsNullOrEmpty(part.Finish))
            {
                AddPartProp("Finish", "tekla:part:finish", SceneValue.FromText(part.Finish));
            }
            if (part.Length > 0)
            {
                AddPartProp("Length", "tekla:part:length", SceneValue.FromNumber(part.Length * 0.001, "m", $"{part.Length:F1} mm"));
            }

            foreach (var (k, v) in part.StringAttributes)
            {
                AddPartProp(k, $"tekla:attr:{k}", SceneValue.FromText(v));
            }
            foreach (var (k, v) in part.NumericAttributes)
            {
                AddPartProp(k, $"tekla:attr:{k}", SceneValue.FromNumber(v, null, v.ToString("G", CultureInfo.InvariantCulture)));
            }

            var partPset = new ScenePropertySet
            {
                Id = nextPsetId++,
                Name = "Part",
                Key = "tekla:part",
                Properties = partProps,
            };
            scene.PropertySets.Add(partPset);
            partElement.PropertySetIds.Add(partPset.Id);

            scene.Elements.Add(partElement);

            // Material
            string matKey = $"{part.Class}_{part.Material}";
            if (!materialCache.TryGetValue(matKey, out int matId))
            {
                matId = nextMatId++;
                var color = ColorForClass(part.Class);
                scene.Materials.Add(new SceneMaterial
                {
                    Id = matId,
                    Name = string.IsNullOrEmpty(part.Material) ? $"Class {part.Class}" : part.Material,
                    Color = color,
                    Roughness = 0.4,
                    Metalness = 0.6,
                });
                materialCache[matKey] = matId;
            }

            // Geometry (in metres, scale factor 0.001)
            if (options.GenerateGeometry && part.StartPoint.HasValue && part.EndPoint.HasValue)
            {
                var p0 = part.StartPoint.Value;
                var p1 = part.EndPoint.Value;

                var (geomVertices, geomNormals, geomIndices) = BuildBoxMesh(p0, p1, part.Profile, 0.001);
                if (geomVertices.Count > 0)
                {
                    int geomId = nextGeomId++;
                    scene.Geometries.Add(new SceneGeometry
                    {
                        Id = geomId,
                        Topology = SceneTopology.Triangles,
                        Vertices = geomVertices,
                        Normals = geomNormals,
                        Indices = geomIndices,
                    });

                    scene.Instances.Add(new SceneInstance
                    {
                        ElementId = partId,
                        GeometryId = geomId,
                        MaterialId = matId,
                        Color = ColorForClass(part.Class),
                    });

                    elementsWithGeometry++;
                }
            }
        }

        // 3. Bolts
        if (options.IncludeBolts)
        {
            foreach (var bolt in model.Bolts)
            {
                long boltId = nextElemId++;
                convertedElements++;

                long parentId = (bolt.Assembly != null && asmElementMap.TryGetValue(bolt.Assembly.Id, out var asmElemId))
                    ? asmElemId
                    : rootElemId;

                var boltElement = new SceneElement
                {
                    Id = boltId,
                    ModelId = ModelId,
                    Kind = SceneNodeKind.Element,
                    ParentId = parentId,
                    Name = $"Bolt Group ({bolt.Holes.Count} holes)",
                    Category = "Bolt",
                    TypeName = "Bolt Group",
                    SourceId = bolt.Guid != Guid.Empty ? bolt.Guid.ToString("D") : $"bolt:{bolt.Id}",
                    IfcGuid = bolt.IfcGuid,
                };

                var boltProps = new List<SceneProperty>
                {
                    new() { Name = "Hole Count", Key = "tekla:bolt:hole_count", Value = SceneValue.FromInteger(bolt.Holes.Count, bolt.Holes.Count.ToString(CultureInfo.InvariantCulture)) },
                    new() { Name = "Position Count", Key = "tekla:bolt:position_count", Value = SceneValue.FromInteger(bolt.Positions.Count, bolt.Positions.Count.ToString(CultureInfo.InvariantCulture)) },
                    new() { Name = "Phase", Key = "tekla:bolt:phase", Value = SceneValue.FromInteger(bolt.Phase, bolt.Phase.ToString(CultureInfo.InvariantCulture)) }
                };

                foreach (var (k, v) in bolt.StringAttributes)
                {
                    boltProps.Add(new SceneProperty { Name = k, Key = $"tekla:attr:{k}", Value = SceneValue.FromText(v) });
                }

                var boltPset = new ScenePropertySet
                {
                    Id = nextPsetId++,
                    Name = "Bolt Group",
                    Key = "tekla:bolt",
                    Properties = boltProps,
                };
                scene.PropertySets.Add(boltPset);
                boltElement.PropertySetIds.Add(boltPset.Id);

                scene.Elements.Add(boltElement);
            }
        }

        // 4. Welds
        if (options.IncludeWelds)
        {
            foreach (var weld in model.Welds)
            {
                long weldId = nextElemId++;
                convertedElements++;

                long parentId = (weld.Assembly != null && asmElementMap.TryGetValue(weld.Assembly.Id, out var asmElemId))
                    ? asmElemId
                    : rootElemId;

                var weldElement = new SceneElement
                {
                    Id = weldId,
                    ModelId = ModelId,
                    Kind = SceneNodeKind.Element,
                    ParentId = parentId,
                    Name = "Weld",
                    Category = "Weld",
                    TypeName = "Weld",
                    SourceId = weld.Guid != Guid.Empty ? weld.Guid.ToString("D") : $"weld:{weld.Id}",
                    IfcGuid = weld.IfcGuid,
                };

                var weldProps = new List<SceneProperty>
                {
                    new() { Name = "Common Attr Id", Key = "tekla:weld:common_attr_id", Value = SceneValue.FromInteger(weld.CommonAttrId, weld.CommonAttrId.ToString(CultureInfo.InvariantCulture)) },
                    new() { Name = "Phase", Key = "tekla:weld:phase", Value = SceneValue.FromInteger(weld.Phase, weld.Phase.ToString(CultureInfo.InvariantCulture)) }
                };

                foreach (var (k, v) in weld.StringAttributes)
                {
                    weldProps.Add(new SceneProperty { Name = k, Key = $"tekla:attr:{k}", Value = SceneValue.FromText(v) });
                }

                var weldPset = new ScenePropertySet
                {
                    Id = nextPsetId++,
                    Name = "Weld",
                    Key = "tekla:weld",
                    Properties = weldProps,
                };
                scene.PropertySets.Add(weldPset);
                weldElement.PropertySetIds.Add(weldPset.Id);

                scene.Elements.Add(weldElement);
            }
        }

        var diagnostics = scene.Validate();
        return new TdbSceneResult(scene, diagnostics, convertedElements, elementsWithGeometry);
    }

    private static RgbaColor ColorForClass(int teklaClass) => teklaClass switch
    {
        1 => new RgbaColor(178, 178, 178), // Grey
        2 => new RgbaColor(217, 56, 58),   // Red
        3 => new RgbaColor(92, 184, 92),   // Green
        4 => new RgbaColor(51, 122, 183),  // Blue
        5 => new RgbaColor(91, 192, 222),  // Cyan
        6 => new RgbaColor(217, 83, 79),   // Magenta
        7 => new RgbaColor(240, 173, 78),  // Orange / Yellow
        8 => new RgbaColor(139, 69, 19),   // Brown
        9 => new RgbaColor(46, 125, 50),   // Dark Green
        10 => new RgbaColor(21, 101, 192), // Dark Blue
        11 => new RgbaColor(123, 31, 162), // Purple
        12 => new RgbaColor(0, 137, 123),  // Teal
        13 => new RgbaColor(233, 30, 99),  // Pink
        14 => new RgbaColor(84, 110, 122), // Dark Grey
        _ => new RgbaColor(144, 202, 249), // Default Light Blue
    };

    private static (List<Vector3d> Vertices, List<Vector3d> Normals, List<int> Indices) BuildBoxMesh(
        ModelPoint p0, ModelPoint p1, string profile, double scale)
    {
        double dx = (p1.X - p0.X) * scale;
        double dy = (p1.Y - p0.Y) * scale;
        double dz = (p1.Z - p0.Z) * scale;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        if (len < 1e-6)
        {
            return ([], [], []);
        }

        double dirX = dx / len;
        double dirY = dy / len;
        double dirZ = dz / len;

        double perpX, perpY, perpZ;
        if (Math.Abs(dirZ) < 0.9)
        {
            perpX = dirY;
            perpY = -dirX;
            perpZ = 0.0;
        }
        else
        {
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

        double vX = dirY * perpZ - dirZ * perpY;
        double vY = dirZ * perpX - dirX * perpZ;
        double vZ = dirX * perpY - dirY * perpX;

        (double hw, double hh) = EstimateProfileDimensions(profile, scale);

        double x0 = p0.X * scale;
        double y0 = p0.Y * scale;
        double z0 = p0.Z * scale;
        double x1 = p1.X * scale;
        double y1 = p1.Y * scale;
        double z1 = p1.Z * scale;

        // 8 corner positions
        var c0 = new Vector3d(x0 - perpX * hw - vX * hh, y0 - perpY * hw - vY * hh, z0 - perpZ * hw - vZ * hh);
        var c1 = new Vector3d(x0 + perpX * hw - vX * hh, y0 + perpY * hw - vY * hh, z0 + perpZ * hw - vZ * hh);
        var c2 = new Vector3d(x0 + perpX * hw + vX * hh, y0 + perpY * hw + vY * hh, z0 + perpZ * hw + vZ * hh);
        var c3 = new Vector3d(x0 - perpX * hw + vX * hh, y0 - perpY * hw + vY * hh, z0 - perpZ * hw + vZ * hh);
        var c4 = new Vector3d(x1 - perpX * hw - vX * hh, y1 - perpY * hw - vY * hh, z1 - perpZ * hw - vZ * hh);
        var c5 = new Vector3d(x1 + perpX * hw - vX * hh, y1 + perpY * hw - vY * hh, z1 + perpZ * hw - vZ * hh);
        var c6 = new Vector3d(x1 + perpX * hw + vX * hh, y1 + perpY * hw + vY * hh, z1 + perpZ * hw + vZ * hh);
        var c7 = new Vector3d(x1 - perpX * hw + vX * hh, y1 - perpY * hw + vY * hh, z1 - perpZ * hw + vZ * hh);

        var vertices = new List<Vector3d>(24);
        var normals = new List<Vector3d>(24);
        var indices = new List<int>(36);

        void AddFace(Vector3d a, Vector3d b, Vector3d c, Vector3d d, Vector3d normal)
        {
            int baseIdx = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }

        var nStart = new Vector3d(-dirX, -dirY, -dirZ);
        var nEnd = new Vector3d(dirX, dirY, dirZ);
        var nBottom = new Vector3d(-vX, -vY, -vZ);
        var nRight = new Vector3d(perpX, perpY, perpZ);
        var nTop = new Vector3d(vX, vY, vZ);
        var nLeft = new Vector3d(-perpX, -perpY, -perpZ);

        AddFace(c3, c2, c1, c0, nStart);   // Start cap
        AddFace(c4, c5, c6, c7, nEnd);     // End cap
        AddFace(c0, c1, c5, c4, nBottom);  // Bottom
        AddFace(c1, c2, c6, c5, nRight);   // Right
        AddFace(c2, c3, c7, c6, nTop);     // Top
        AddFace(c3, c0, c4, c7, nLeft);    // Left

        return (vertices, normals, indices);
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
                var parts = p[2..].Split(['*', 'x', 'X']);
                if (parts.Length >= 2)
                {
                    if (double.TryParse(parts[0], CultureInfo.InvariantCulture, out double t) &&
                        double.TryParse(parts[1], CultureInfo.InvariantCulture, out double w))
                    {
                        if (!double.IsFinite(t) || !double.IsFinite(w))
                        {
                            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Non-finite dimension in profile '{profile}'."));
                        }
                        if (t > 0 && w > 0)
                        {
                            heightMm = t;
                            widthMm = w;
                        }
                    }
                    else if (parts[0].Contains("NAN", StringComparison.OrdinalIgnoreCase) ||
                             parts[1].Contains("NAN", StringComparison.OrdinalIgnoreCase) ||
                             parts[0].Contains("INFINITY", StringComparison.OrdinalIgnoreCase) ||
                             parts[1].Contains("INFINITY", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"Non-finite dimension in profile '{profile}'."));
                    }
                }
            }
            else
            {
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
}
