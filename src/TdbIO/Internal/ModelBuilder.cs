namespace TdbIO.Internal;

internal static class ModelBuilder
{
    public static TeklaModel Build(Db1Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return BuildFromSections(doc, doc.ReadTables());
    }

    internal static TeklaModel BuildFromSections(Db1Document doc, IReadOnlyList<TableSection> sections)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(sections);

        // 1. Decoded row collections
        var objRows = FindSection(sections, ObjectRow.TableKey, ObjectRow.ExpectedRowSize)?.AsObjects() ?? Array.Empty<ObjectRow>();
        var assRows = FindSection(sections, AssemblyRow.TableKey, AssemblyRow.ExpectedRowSize)?.AsAssemblies() ?? Array.Empty<AssemblyRow>();
        var partRows = FindSection(sections, PartRow.TableKey, PartRow.ExpectedRowSize)?.AsParts() ?? Array.Empty<PartRow>();
        var boltRows = FindSection(sections, BoltRow.TableKey, BoltRow.ExpectedRowSize)?.AsBolts() ?? Array.Empty<BoltRow>();
        var weldRows = FindSection(sections, WeldingRow.TableKey, WeldingRow.ExpectedRowSize)?.AsWeldings() ?? Array.Empty<WeldingRow>();
        var ptRows = FindSection(sections, PointRow.TableKey, PointRow.ExpectedRowSize)?.AsPoints() ?? Array.Empty<PointRow>();
        var csysRows = FindSection(sections, CoordsysRow.TableKey, CoordsysRow.ExpectedRowSize)?.AsCoordsys() ?? Array.Empty<CoordsysRow>();
        var paRows = FindSection(sections, PartAttrRow.TableKey, PartAttrRow.ExpectedRowSize)?.AsPartAttrs() ?? Array.Empty<PartAttrRow>();
        var strPoolRows = FindSection(sections, StringPoolRow.TableKey, StringPoolRow.ExpectedRowSize)?.AsStringPool() ?? Array.Empty<StringPoolRow>();
        var numAttrRows = FindSection(sections, NumAttrRow.TableKey, NumAttrRow.ExpectedRowSize)?.AsNumAttrs() ?? Array.Empty<NumAttrRow>();
        var strAttrRows = FindSection(sections, StrAttrRow.TableKey, StrAttrRow.ExpectedRowSize)?.AsStrAttrs() ?? Array.Empty<StrAttrRow>();
        var numDefRows = FindSection(sections, NumAttrAttrRow.TableKey, NumAttrAttrRow.ExpectedRowSize)?.AsNumAttrAttrs() ?? Array.Empty<NumAttrAttrRow>();
        var strDefRows = FindSection(sections, StrAttrAttrRow.TableKey, StrAttrAttrRow.ExpectedRowSize)?.AsStrAttrAttrs() ?? Array.Empty<StrAttrAttrRow>();
        var phaseRows = FindSection(sections, ObjectPhaseRow.TableKey, ObjectPhaseRow.ExpectedRowSize)?.AsObjectPhases() ?? Array.Empty<ObjectPhaseRow>();
        var partNumRows = FindSection(sections, PartNumberingRow.TableKey, PartNumberingRow.ExpectedRowSize)?.AsPartNumberings() ?? Array.Empty<PartNumberingRow>();
        var assNumRows = FindSection(sections, AssemblyNumberingRow.TableKey, AssemblyNumberingRow.ExpectedRowSize)?.AsAssemblyNumberings() ?? Array.Empty<AssemblyNumberingRow>();
        var holeRows = FindSection(sections, BoltHoleRow.TableKey, BoltHoleRow.ExpectedRowSize)?.AsBoltHoles() ?? Array.Empty<BoltHoleRow>();
        var boltPtRows = FindSection(sections, BoltHolePointsRow.TableKey, BoltHolePointsRow.ExpectedRowSize)?.AsBoltHolePoints() ?? Array.Empty<BoltHolePointsRow>();

        // 2. Primary lookups
        var objects = new Dictionary<int, ObjectRow>(objRows.Count);
        foreach (var o in objRows) objects[o.Id] = o;

        var stringPool = new Dictionary<int, string>(strPoolRows.Count);
        foreach (var sp in strPoolRows) stringPool[sp.Id] = sp.Text;

        var points = new Dictionary<int, ModelPoint>(ptRows.Count);
        foreach (var pt in ptRows) points[pt.Id] = new ModelPoint(pt.Id, pt.X, pt.Y, pt.Z);

        var coords = new Dictionary<int, ModelCoordsys>(csysRows.Count);
        foreach (var cs in csysRows) coords[cs.Id] = new ModelCoordsys(cs.Id, cs.X, cs.Y, cs.Z, cs.Length);

        var partAttrs = new Dictionary<int, PartAttrRow>(paRows.Count);
        foreach (var pa in paRows) partAttrs[pa.Id] = pa;

        var phases = new Dictionary<int, int>(phaseRows.Count);
        foreach (var ph in phaseRows) phases[ph.ObjectId] = ph.PhaseNumber;

        var partNumbering = new Dictionary<int, PartNumberingRow>(partNumRows.Count);
        foreach (var pn in partNumRows) partNumbering[pn.Id] = pn;

        var assNumbering = new Dictionary<int, AssemblyNumberingRow>(assNumRows.Count);
        foreach (var an in assNumRows) assNumbering[an.Id] = an;

        // 3. User & System Attributes
        var numDefs = new Dictionary<int, (string Name, double Value)>(numDefRows.Count);
        foreach (var nd in numDefRows)
            if (!string.IsNullOrEmpty(nd.Name))
                numDefs[nd.Id] = (nd.Name, nd.Value);

        var strDefs = new Dictionary<int, (string Name, string Value)>(strDefRows.Count);
        foreach (var sd in strDefRows)
        {
            if (string.IsNullOrEmpty(sd.Name)) continue;
            string val = !string.IsNullOrEmpty(sd.Value)
                ? sd.Value
                : (sd.ValueId > 0 && stringPool.TryGetValue(sd.ValueId, out var spVal) ? spVal : string.Empty);
            strDefs[sd.Id] = (sd.Name, val);
        }

        var numAttrsByObj = new Dictionary<int, Dictionary<string, double>>();
        foreach (var na in numAttrRows)
        {
            if (numDefs.TryGetValue(na.AttrId, out var def))
            {
                if (!numAttrsByObj.TryGetValue(na.FatherId, out var dict))
                    numAttrsByObj[na.FatherId] = dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                dict[def.Name] = def.Value;
            }
        }

        var strAttrsByObj = new Dictionary<int, Dictionary<string, string>>();
        foreach (var sa in strAttrRows)
        {
            if (strDefs.TryGetValue(sa.AttrId, out var def))
            {
                if (!strAttrsByObj.TryGetValue(sa.FatherId, out var dict))
                    strAttrsByObj[sa.FatherId] = dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                dict[def.Name] = def.Value;
            }
        }

        // 4. Build Assemblies
        var assemblies = new List<ModelAssembly>(assRows.Count);
        var assembliesById = new Dictionary<int, ModelAssembly>(assRows.Count);
        foreach (var a in assRows)
        {
            var obj = objects.GetValueOrDefault(a.Id);
            int phase = phases.GetValueOrDefault(a.Id);
            assNumbering.TryGetValue(a.Id, out var an);
            var numAttrs = numAttrsByObj.GetValueOrDefault(a.Id);
            var strAttrs = strAttrsByObj.GetValueOrDefault(a.Id);

            var assembly = new ModelAssembly(
                a.Id,
                obj.Guid,
                obj.Type,
                obj.Subtype,
                phase,
                obj.ParentId > 0 ? obj.ParentId : null,
                a.Name,
                an.Prefix ?? string.Empty,
                an.StartNumber,
                an.FPos,
                a.Type,
                a.Class,
                strAttrs,
                numAttrs);

            assemblies.Add(assembly);
            assembliesById[a.Id] = assembly;
        }

        // 5. Build Parts
        var parts = new List<ModelPart>(partRows.Count);
        var partsById = new Dictionary<int, ModelPart>(partRows.Count);
        foreach (var p in partRows)
        {
            var obj = objects.GetValueOrDefault(p.Id);
            int phase = phases.GetValueOrDefault(p.Id);
            partAttrs.TryGetValue(p.PartAttrId, out var pa);
            partNumbering.TryGetValue(p.Id, out var pn);

            string name = pa.Name ?? string.Empty;
            string rawProf = pa.Profile ?? string.Empty;
            string paramStr = pa.ParameterStringId > 0 && stringPool.TryGetValue(pa.ParameterStringId, out var sp) ? sp : string.Empty;
            string profile = string.IsNullOrEmpty(rawProf) ? paramStr : rawProf + paramStr;
            string material = pa.Material ?? string.Empty;
            string prefix = !string.IsNullOrEmpty(pn.Prefix) ? pn.Prefix : (pa.Prefix ?? string.Empty);
            int startNo = pn.StartNumber;
            int posNo = pn.FPos;
            int @class = pa.ObjClass;
            string finish = pa.Finish ?? string.Empty;

            var startPt = points.TryGetValue(p.StartPointId, out var pt1) ? pt1 : (ModelPoint?)null;
            var endPt = points.TryGetValue(p.EndPointId, out var pt2) ? pt2 : (ModelPoint?)null;
            var csys = coords.TryGetValue(p.CoordsysAttrId, out var cs) ? cs : (ModelCoordsys?)null;

            var numAttrs = numAttrsByObj.GetValueOrDefault(p.Id);
            var strAttrs = strAttrsByObj.GetValueOrDefault(p.Id);

            int? assId = obj.AssemblyId > 0 ? obj.AssemblyId : null;

            var part = new ModelPart(
                p.Id,
                obj.Guid,
                obj.Type,
                obj.Subtype,
                phase,
                obj.ParentId > 0 ? obj.ParentId : null,
                assId,
                name,
                profile,
                material,
                prefix,
                startNo,
                posNo,
                @class,
                finish,
                startPt,
                endPt,
                p.Length,
                csys,
                strAttrs,
                numAttrs);

            parts.Add(part);
            partsById[p.Id] = part;
        }

        // 6. Build Bolts and Bolt Holes
        var holesByBolt = new Dictionary<int, List<ModelBoltHole>>();
        foreach (var h in holeRows)
        {
            if (!holesByBolt.TryGetValue(h.BoltId, out var list))
                holesByBolt[h.BoltId] = list = new List<ModelBoltHole>();

            var connectedPart = partsById.GetValueOrDefault(h.PartId);
            list.Add(new ModelBoltHole(
                h.No,
                h.PartId,
                connectedPart,
                h.PointZ,
                h.Length,
                h.LongHoleDir,
                h.LongHoleType,
                h.ShorteningLength));
        }

        var patternPtsByBolt = new Dictionary<int, List<ModelPoint>>();
        foreach (var bp in boltPtRows)
        {
            if (!patternPtsByBolt.TryGetValue(bp.BoltId, out var list))
                patternPtsByBolt[bp.BoltId] = list = new List<ModelPoint>();
            list.Add(new ModelPoint(0, bp.IntPoint1X, bp.IntPoint1Y, bp.IntPoint1Z));
            list.Add(new ModelPoint(0, bp.IntPoint2X, bp.IntPoint2Y, bp.IntPoint2Z));
        }

        var bolts = new List<ModelBolt>(boltRows.Count);
        var boltsById = new Dictionary<int, ModelBolt>(boltRows.Count);
        foreach (var b in boltRows)
        {
            var obj = objects.GetValueOrDefault(b.Id);
            int phase = phases.GetValueOrDefault(b.Id);
            var pt1 = points.TryGetValue(b.FirstPointId, out var p1) ? p1 : (ModelPoint?)null;
            var pt2 = points.TryGetValue(b.SecondPointId, out var p2) ? p2 : (ModelPoint?)null;
            var holes = holesByBolt.GetValueOrDefault(b.Id) ?? (IReadOnlyList<ModelBoltHole>)Array.Empty<ModelBoltHole>();
            var positions = patternPtsByBolt.GetValueOrDefault(b.Id) ?? (IReadOnlyList<ModelPoint>)Array.Empty<ModelPoint>();
            var numAttrs = numAttrsByObj.GetValueOrDefault(b.Id);
            var strAttrs = strAttrsByObj.GetValueOrDefault(b.Id);

            var bolt = new ModelBolt(
                b.Id,
                obj.Guid,
                obj.Type,
                obj.Subtype,
                phase,
                obj.ParentId > 0 ? obj.ParentId : null,
                obj.AssemblyId > 0 ? obj.AssemblyId : null,
                pt1,
                pt2,
                holes,
                positions,
                strAttrs,
                numAttrs);

            var connectedParts = new List<ModelPart>();
            foreach (var h in holes)
            {
                if (h.Part != null && !connectedParts.Contains(h.Part))
                    connectedParts.Add(h.Part);
            }
            bolt.ConnectedParts = connectedParts;

            bolts.Add(bolt);
            boltsById[b.Id] = bolt;
        }

        // 7. Build Welds
        var welds = new List<ModelWeld>(weldRows.Count);
        var weldsById = new Dictionary<int, ModelWeld>(weldRows.Count);
        foreach (var w in weldRows)
        {
            var obj = objects.GetValueOrDefault(w.Id);
            int phase = phases.GetValueOrDefault(w.Id);
            var numAttrs = numAttrsByObj.GetValueOrDefault(w.Id);
            var strAttrs = strAttrsByObj.GetValueOrDefault(w.Id);

            var weld = new ModelWeld(
                w.Id,
                obj.Guid,
                obj.Type,
                obj.Subtype,
                phase,
                obj.ParentId > 0 ? obj.ParentId : null,
                obj.AssemblyId > 0 ? obj.AssemblyId : null,
                w.CommonAttrId,
                strAttrs,
                numAttrs);

            welds.Add(weld);
            weldsById[w.Id] = weld;
        }

        // 8. Link Assemblies, Parts, Bolts and Welds
        var partsByAssembly = new Dictionary<int, List<ModelPart>>();
        foreach (var part in parts)
        {
            if (part.AssemblyId.HasValue && assembliesById.TryGetValue(part.AssemblyId.Value, out var ass))
            {
                part.Assembly = ass;
                if (!partsByAssembly.TryGetValue(ass.Id, out var assParts))
                    partsByAssembly[ass.Id] = assParts = new List<ModelPart>();
                assParts.Add(part);
            }
        }

        foreach (var assRow in assRows)
        {
            if (assembliesById.TryGetValue(assRow.Id, out var ass))
            {
                var assParts = partsByAssembly.GetValueOrDefault(ass.Id) ?? new List<ModelPart>();
                ass.Parts = assParts;

                ModelPart? main = null;
                if (assRow.MainPartId > 0)
                    main = partsById.GetValueOrDefault(assRow.MainPartId);
                if (main == null && assParts.Count > 0)
                    main = assParts[0];
                ass.MainPart = main;

                var secondaries = new List<ModelPart>(assParts.Count);
                foreach (var p in assParts)
                {
                    if (p != main) secondaries.Add(p);
                }
                ass.Secondaries = secondaries;
            }
        }

        var boltsByPart = new Dictionary<int, List<ModelBolt>>();
        var boltsByAssembly = new Dictionary<int, List<ModelBolt>>();
        foreach (var bolt in bolts)
        {
            if (bolt.AssemblyId.HasValue && assembliesById.TryGetValue(bolt.AssemblyId.Value, out var ass))
            {
                bolt.Assembly = ass;
                if (!boltsByAssembly.TryGetValue(ass.Id, out var assBolts))
                    boltsByAssembly[ass.Id] = assBolts = new List<ModelBolt>();
                assBolts.Add(bolt);
            }

            foreach (var cp in bolt.ConnectedParts)
            {
                if (!boltsByPart.TryGetValue(cp.Id, out var partBolts))
                    boltsByPart[cp.Id] = partBolts = new List<ModelBolt>();
                if (!partBolts.Contains(bolt))
                    partBolts.Add(bolt);

                if (cp.Assembly != null)
                {
                    if (!boltsByAssembly.TryGetValue(cp.Assembly.Id, out var assBolts))
                        boltsByAssembly[cp.Assembly.Id] = assBolts = new List<ModelBolt>();
                    if (!assBolts.Contains(bolt))
                        assBolts.Add(bolt);
                }
            }
        }

        foreach (var part in parts)
            part.Bolts = boltsByPart.GetValueOrDefault(part.Id) ?? (IReadOnlyList<ModelBolt>)Array.Empty<ModelBolt>();

        foreach (var ass in assemblies)
            ass.Bolts = boltsByAssembly.GetValueOrDefault(ass.Id) ?? (IReadOnlyList<ModelBolt>)Array.Empty<ModelBolt>();

        // Link welds
        var weldsByPart = new Dictionary<int, List<ModelWeld>>();
        var weldsByAssembly = new Dictionary<int, List<ModelWeld>>();
        foreach (var weld in welds)
        {
            if (weld.AssemblyId.HasValue && assembliesById.TryGetValue(weld.AssemblyId.Value, out var ass))
            {
                weld.Assembly = ass;
                if (!weldsByAssembly.TryGetValue(ass.Id, out var assWelds))
                    weldsByAssembly[ass.Id] = assWelds = new List<ModelWeld>();
                assWelds.Add(weld);
            }

            // Welds often attach via parent component
            if (weld.ParentId.HasValue)
            {
                if (assembliesById.TryGetValue(weld.ParentId.Value, out var parentAss))
                {
                    weld.Parent = parentAss;
                    weld.Assembly ??= parentAss;
                }
                else if (partsById.TryGetValue(weld.ParentId.Value, out var parentPart))
                {
                    weld.Parent = parentPart;
                    weld.Assembly ??= parentPart.Assembly;
                }
            }

            if (weld.Assembly != null)
            {
                if (!weldsByAssembly.TryGetValue(weld.Assembly.Id, out var assWelds))
                    weldsByAssembly[weld.Assembly.Id] = assWelds = new List<ModelWeld>();
                if (!assWelds.Contains(weld))
                    assWelds.Add(weld);
            }
        }

        foreach (var part in parts)
            part.Welds = weldsByPart.GetValueOrDefault(part.Id) ?? (IReadOnlyList<ModelWeld>)Array.Empty<ModelWeld>();

        foreach (var ass in assemblies)
            ass.Welds = weldsByAssembly.GetValueOrDefault(ass.Id) ?? (IReadOnlyList<ModelWeld>)Array.Empty<ModelWeld>();

        // 9. All objects list
        var allObjects = new List<ModelObject>(objects.Count > 0 ? objects.Count : assemblies.Count + parts.Count + bolts.Count + welds.Count);
        var seenIds = new HashSet<int>();

        foreach (var a in assemblies)
        {
            allObjects.Add(a);
            seenIds.Add(a.Id);
        }
        foreach (var p in parts)
        {
            if (seenIds.Add(p.Id)) allObjects.Add(p);
        }
        foreach (var b in bolts)
        {
            if (seenIds.Add(b.Id)) allObjects.Add(b);
        }
        foreach (var w in welds)
        {
            if (seenIds.Add(w.Id)) allObjects.Add(w);
        }

        foreach (var o in objRows)
        {
            if (seenIds.Add(o.Id))
            {
                int phase = phases.GetValueOrDefault(o.Id);
                var numAttrs = numAttrsByObj.GetValueOrDefault(o.Id);
                var strAttrs = strAttrsByObj.GetValueOrDefault(o.Id);
                allObjects.Add(new ModelObject(
                    o.Id,
                    o.Guid,
                    o.Type,
                    o.Subtype,
                    phase,
                    o.ParentId > 0 ? o.ParentId : null,
                    o.AssemblyId > 0 ? o.AssemblyId : null,
                    strAttrs,
                    numAttrs));
            }
        }

        return new TeklaModel(doc, doc.ModelGuid, assemblies, parts, bolts, welds, allObjects);
    }

    private static TableSection? FindSection(IReadOnlyList<TableSection> sections, int key, int rowSize)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            if (s.Key == key && s.RowSize == rowSize)
                return s;
        }
        return null;
    }
}
