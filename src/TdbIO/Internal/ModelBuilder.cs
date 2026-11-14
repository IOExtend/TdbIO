using System.Text;

namespace TdbIO.Internal;

internal static class ModelBuilder
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static TeklaModel Build(Db1Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        return BuildFromSections(doc, doc.ReadTables());
    }

    internal static TeklaModel BuildFromSections(Db1Document doc, IReadOnlyList<TableSection> sections)
    {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(sections);

        // 1. Decoded active row collections (Marker == 0x0C excluded)
        var objRows = GetActiveRows(FindSection(sections, ObjectRow.TableKey, ObjectRow.ExpectedRowSize), ObjectRow.ExpectedRowSize, r => r.AsObject());
        var assRows = GetActiveRows(FindSection(sections, AssemblyRow.TableKey, AssemblyRow.ExpectedRowSize), AssemblyRow.ExpectedRowSize, r => r.AsAssembly());
        var partRows = GetActiveRows(FindSection(sections, PartRow.TableKey, PartRow.ExpectedRowSize), PartRow.ExpectedRowSize, r => (Row: r.AsPart(), Offset: r.Offset + 1));
        var boltRows = GetActiveRows(FindSection(sections, BoltRow.TableKey, BoltRow.ExpectedRowSize), BoltRow.ExpectedRowSize, r => r.AsBolt());
        var weldRows = GetActiveRows(FindSection(sections, WeldingRow.TableKey, WeldingRow.ExpectedRowSize), WeldingRow.ExpectedRowSize, r => r.AsWelding());
        var ptRows = GetActiveRows(FindSection(sections, PointRow.TableKey, PointRow.ExpectedRowSize), PointRow.ExpectedRowSize, r => (Row: r.AsPoint(), Offset: r.Offset + 1));
        var csysRows = GetActiveRows(FindSection(sections, CoordsysRow.TableKey, CoordsysRow.ExpectedRowSize), CoordsysRow.ExpectedRowSize, r => (Row: r.AsCoordsys(), Offset: r.Offset + 1));
        var paRows = GetActiveRows(FindSection(sections, PartAttrRow.TableKey, PartAttrRow.ExpectedRowSize), PartAttrRow.ExpectedRowSize, r => r.AsPartAttr());
        var strPoolRows = GetActiveRows(FindSection(sections, StringPoolRow.TableKey, StringPoolRow.ExpectedRowSize), StringPoolRow.ExpectedRowSize, r => r.AsStringPool());
        var numAttrRows = GetActiveRows(FindSection(sections, NumAttrRow.TableKey, NumAttrRow.ExpectedRowSize), NumAttrRow.ExpectedRowSize, r => r.AsNumAttr());
        var strAttrRows = GetActiveRows(FindSection(sections, StrAttrRow.TableKey, StrAttrRow.ExpectedRowSize), StrAttrRow.ExpectedRowSize, r => r.AsStrAttr());
        var numDefRows = GetActiveRows(FindSection(sections, NumAttrAttrRow.TableKey, NumAttrAttrRow.ExpectedRowSize), NumAttrAttrRow.ExpectedRowSize, r => (Row: r.AsNumAttrAttr(), Offset: r.Offset + 1));
        var strDefRows = GetActiveRows(FindSection(sections, StrAttrAttrRow.TableKey, StrAttrAttrRow.ExpectedRowSize), StrAttrAttrRow.ExpectedRowSize, r => r.AsStrAttrAttr());
        var phaseRows = GetActiveRows(FindSection(sections, ObjectPhaseRow.TableKey, ObjectPhaseRow.ExpectedRowSize), ObjectPhaseRow.ExpectedRowSize, r => r.AsObjectPhase());
        var partNumRows = GetActiveRows(FindSection(sections, PartNumberingRow.TableKey, PartNumberingRow.ExpectedRowSize), PartNumberingRow.ExpectedRowSize, r => r.AsPartNumbering());
        var assNumRows = GetActiveRows(FindSection(sections, AssemblyNumberingRow.TableKey, AssemblyNumberingRow.ExpectedRowSize), AssemblyNumberingRow.ExpectedRowSize, r => r.AsAssemblyNumbering());
        var objNumRows = GetActiveRows(FindSection(sections, ObjectNumberingRow.TableKey, ObjectNumberingRow.ExpectedRowSize), ObjectNumberingRow.ExpectedRowSize, r => r.AsObjectNumbering());
        var holeRows = GetActiveRows(FindSection(sections, BoltHoleRow.TableKey, BoltHoleRow.ExpectedRowSize), BoltHoleRow.ExpectedRowSize, r => (Row: r.AsBoltHole(), Offset: r.Offset + 1));
        var boltPtRows = GetActiveRows(FindSection(sections, BoltHolePointsRow.TableKey, BoltHolePointsRow.ExpectedRowSize), BoltHolePointsRow.ExpectedRowSize, r => (Row: r.AsBoltHolePoints(), Offset: r.Offset + 1));

        // 2. Primary lookups
        var objects = new Dictionary<int, ObjectRow>(objRows.Count);
        foreach (var o in objRows) objects[o.Id] = o;

        // Follow string-pool NextId chain with cycle and missing-link detection
        var chunksById = new Dictionary<int, StringPoolRow>(strPoolRows.Count);
        var childChunkIds = new HashSet<int>();
        foreach (var sp in strPoolRows)
        {
            chunksById[sp.Id] = sp;
            if (sp.NextId > 0) childChunkIds.Add(sp.NextId);
        }

        var stringPool = new Dictionary<int, string>(strPoolRows.Count);
        var rawBuffer = new List<byte>(128);
        var visitedInCurrentChain = new HashSet<int>();
        var allVisitedInAnyChain = new HashSet<int>();

        void TraverseChain(StringPoolRow head)
        {
            if (allVisitedInAnyChain.Contains(head.Id) && stringPool.ContainsKey(head.Id))
                return;

            rawBuffer.Clear();
            visitedInCurrentChain.Clear();

            int currId = head.Id;
            while (currId > 0)
            {
                if (!visitedInCurrentChain.Add(currId))
                    throw FormatError.At($"Cycle detected in string pool chain at id {currId}", 0);

                allVisitedInAnyChain.Add(currId);

                if (!chunksById.TryGetValue(currId, out var chunk))
                    throw FormatError.At($"Missing string pool chunk link {currId}", 0);

                ReadOnlySpan<byte> chunkBytes = chunk.RawBytes.Span;
                if (chunkBytes.IsEmpty && !string.IsNullOrEmpty(chunk.Text))
                {
                    rawBuffer.AddRange(Encoding.UTF8.GetBytes(chunk.Text));
                }
                else if (!chunkBytes.IsEmpty)
                {
                    int nul = chunkBytes.IndexOf((byte)0);
                    rawBuffer.AddRange(nul >= 0 ? chunkBytes[..nul] : chunkBytes);
                }

                currId = chunk.NextId;
            }

            try
            {
                stringPool[head.Id] = StrictUtf8.GetString(rawBuffer.ToArray());
            }
            catch (DecoderFallbackException e)
            {
                throw FormatError.At("Invalid UTF-8 sequence in string pool chain", 0, e);
            }
        }

        // Pass 1: Traverse all actual chain heads (chunks not referenced as NextId by any chunk)
        foreach (var sp in strPoolRows)
        {
            if (!childChunkIds.Contains(sp.Id))
                TraverseChain(sp);
        }

        // Pass 2: Traverse any remaining unvisited chunks (e.g. to detect orphaned cycles)
        foreach (var sp in strPoolRows)
        {
            if (!allVisitedInAnyChain.Contains(sp.Id))
                TraverseChain(sp);
        }

        var points = new Dictionary<int, ModelPoint>(ptRows.Count);
        foreach (var (pt, offset) in ptRows) points[pt.Id] = new ModelPoint(pt.Id, pt.X, pt.Y, pt.Z) { Offset = offset };

        var coords = new Dictionary<int, ModelCoordsys>(csysRows.Count);
        foreach (var (cs, offset) in csysRows) coords[cs.Id] = new ModelCoordsys(cs.Id, cs.X, cs.Y, cs.Z, cs.Length) { Offset = offset };

        var partAttrs = new Dictionary<int, PartAttrRow>(paRows.Count);
        foreach (var pa in paRows) partAttrs[pa.Id] = pa;

        var phases = new Dictionary<int, int>(phaseRows.Count);
        foreach (var ph in phaseRows) phases[ph.ObjectId] = ph.PhaseNumber;

        var partNumbering = new Dictionary<int, PartNumberingRow>(partNumRows.Count);
        foreach (var pn in partNumRows) partNumbering[pn.Id] = pn;

        var assNumbering = new Dictionary<int, AssemblyNumberingRow>(assNumRows.Count);
        foreach (var an in assNumRows) assNumbering[an.Id] = an;

        var objNumberIds = new Dictionary<int, int>(objNumRows.Count);
        foreach (var on in objNumRows) objNumberIds[on.Id] = on.NumberId;

        // 3. User & System Attributes
        var numDefs = new Dictionary<int, (string Name, double Value, long Offset)>(numDefRows.Count);
        foreach (var (nd, offset) in numDefRows)
            if (!string.IsNullOrEmpty(nd.Name))
                numDefs[nd.Id] = (nd.Name, nd.Value, offset + 8);

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
        var numAttrOffsetsByObj = new Dictionary<int, Dictionary<string, long>>();
        foreach (var na in numAttrRows)
        {
            if (numDefs.TryGetValue(na.AttrId, out var def))
            {
                if (!numAttrsByObj.TryGetValue(na.FatherId, out var dict))
                    numAttrsByObj[na.FatherId] = dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                dict[def.Name] = def.Value;

                if (!numAttrOffsetsByObj.TryGetValue(na.FatherId, out var offDict))
                    numAttrOffsetsByObj[na.FatherId] = offDict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                offDict[def.Name] = def.Offset;
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
            int anId = objNumberIds.TryGetValue(a.Id, out int mappedAnId) ? mappedAnId : a.Id;
            assNumbering.TryGetValue(anId, out var an);
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
                numAttrs)
            {
                NumericAttributeOffsets = numAttrOffsetsByObj.GetValueOrDefault(a.Id)
            };

            assemblies.Add(assembly);
            assembliesById[a.Id] = assembly;
        }

        // 5. Build Parts
        var parts = new List<ModelPart>(partRows.Count);
        var partsById = new Dictionary<int, ModelPart>(partRows.Count);
        foreach (var (p, pBodyOffset) in partRows)
        {
            var obj = objects.GetValueOrDefault(p.Id);
            int phase = phases.GetValueOrDefault(p.Id);
            partAttrs.TryGetValue(p.PartAttrId, out var pa);
            int pnId = objNumberIds.TryGetValue(p.Id, out int mappedPnId) ? mappedPnId : p.Id;
            partNumbering.TryGetValue(pnId, out var pn);

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
                numAttrs,
                lengthOffset: pBodyOffset + 56)
            {
                NumericAttributeOffsets = numAttrOffsetsByObj.GetValueOrDefault(p.Id)
            };

            parts.Add(part);
            partsById[p.Id] = part;
        }

        // 6. Build Bolts and Bolt Holes
        var holesByBolt = new Dictionary<int, List<ModelBoltHole>>();
        foreach (var (h, offset) in holeRows)
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
                h.ShorteningLength)
            {
                Offset = offset
            });
        }

        var patternPtsByBolt = new Dictionary<int, List<ModelPoint>>();
        foreach (var (bp, offset) in boltPtRows)
        {
            if (!patternPtsByBolt.TryGetValue(bp.BoltId, out var list))
                patternPtsByBolt[bp.BoltId] = list = new List<ModelPoint>();
            list.Add(new ModelPoint(0, bp.IntPoint1X, bp.IntPoint1Y, bp.IntPoint1Z) { Offset = offset });
            list.Add(new ModelPoint(0, bp.IntPoint2X, bp.IntPoint2Y, bp.IntPoint2Z) { Offset = offset + 24 });
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
                numAttrs)
            {
                NumericAttributeOffsets = numAttrOffsetsByObj.GetValueOrDefault(b.Id)
            };

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
                numAttrs)
            {
                NumericAttributeOffsets = numAttrOffsetsByObj.GetValueOrDefault(w.Id)
            };

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

        // Generic objects (Table 1 rows that are not assembly, part, bolt, or weld)
        var genericObjectsById = new Dictionary<int, ModelObject>();
        foreach (var o in objRows)
        {
            if (!assembliesById.ContainsKey(o.Id) &&
                !partsById.ContainsKey(o.Id) &&
                !boltsById.ContainsKey(o.Id) &&
                !weldsById.ContainsKey(o.Id))
            {
                int phase = phases.GetValueOrDefault(o.Id);
                var numAttrs = numAttrsByObj.GetValueOrDefault(o.Id);
                var strAttrs = strAttrsByObj.GetValueOrDefault(o.Id);
                var genObj = new ModelObject(
                    o.Id,
                    o.Guid,
                    o.Type,
                    o.Subtype,
                    phase,
                    o.ParentId > 0 ? o.ParentId : null,
                    o.AssemblyId > 0 ? o.AssemblyId : null,
                    strAttrs,
                    numAttrs)
                {
                    NumericAttributeOffsets = numAttrOffsetsByObj.GetValueOrDefault(o.Id)
                };
                genericObjectsById[o.Id] = genObj;
            }
        }

        ModelObject? FindObject(int id)
        {
            if (assembliesById.TryGetValue(id, out var a)) return a;
            if (partsById.TryGetValue(id, out var p)) return p;
            if (boltsById.TryGetValue(id, out var b)) return b;
            if (weldsById.TryGetValue(id, out var w)) return w;
            if (genericObjectsById.TryGetValue(id, out var g)) return g;
            return null;
        }

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

            // Resolve weld parent from complete object index (including generic connections/components)
            if (weld.ParentId.HasValue)
            {
                var parent = FindObject(weld.ParentId.Value);
                if (parent != null)
                {
                    weld.Parent = parent;
                    if (weld.Assembly == null)
                    {
                        if (parent is ModelAssembly parentAss)
                            weld.Assembly = parentAss;
                        else if (parent is ModelPart parentPart)
                            weld.Assembly = parentPart.Assembly;
                        else if (parent.AssemblyId.HasValue && assembliesById.TryGetValue(parent.AssemblyId.Value, out var objAss))
                            weld.Assembly = objAss;
                    }
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
        var allObjects = new List<ModelObject>(assemblies.Count + parts.Count + bolts.Count + welds.Count + genericObjectsById.Count);
        allObjects.AddRange(assemblies);
        allObjects.AddRange(parts);
        allObjects.AddRange(bolts);
        allObjects.AddRange(welds);
        allObjects.AddRange(genericObjectsById.Values);

        return new TeklaModel(doc, doc.ModelGuid, assemblies, parts, bolts, welds, allObjects);
    }

    private static List<T> GetActiveRows<T>(TableSection? section, int expectedSize, Func<TableRow, T> decode)
    {
        if (section == null || section.RowSize != expectedSize)
            return [];
        var list = new List<T>(section.Rows.Count);
        for (int i = 0; i < section.Rows.Count; i++)
        {
            var row = section.Rows[i];
            if (row.Marker == 0x0C) continue;
            list.Add(decode(row));
        }
        return list;
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
