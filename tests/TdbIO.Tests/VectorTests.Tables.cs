using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

/// <summary>Table sections and DBV@ files against the private vectors. Entries are chosen by the keys of their expected
/// values, never by name.</summary>
public partial class VectorTests
{
    [SkippableFact]
    public void Db1SectionVectorsWalkAsExpected()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("sections").Where(e => TableJson.FirstSectionHas(e, "row_size")), ".db1 sections"))
        {
            string name = TestVectors.Name(entry);
            byte[] data = TestVectors.Read(entry);
            var expected = TableJson.Expected(entry);
            var expectedSections = expected.GetProperty("sections").EnumerateArray().ToList();
            var sections = SectionWalker.Walk(data, 0, expectedSections[0].GetProperty("ordinal").GetInt32(), SectionMode.Db1, null);

            Assert.True(expectedSections.Count == sections.Count, $"{name}: {sections.Count} sections");
            for (int i = 0; i < sections.Count; i++) CheckTableDb1Section(name, expectedSections[i], sections[i]);
            Assert.Equal(data.Length, sections[^1].End);

            var handled = new List<string> { "sections" };
            foreach (var property in expected.EnumerateObject())
                if (CheckTableRowValues(name, property, sections) || CheckDecodedRowText(name, property, sections))
                    handled.Add(property.Name);
            CheckTypedDecoderText(name, expected, sections);
            if (expected.TryGetProperty("ends_at_eof", out var eof))
            {
                Assert.True(eof.GetBoolean() == (sections[^1].End == data.Length), $"{name}: ends_at_eof");
                handled.Add("ends_at_eof");
            }
            if (expected.TryGetProperty("file_max_a_b", out var max))
            {
                Assert.True(max.GetInt64() == Assert.Single(sections).Rows[0].B, $"{name}: file_max_a_b is not the b of row 0");
                handled.Add("file_max_a_b");
            }
            TestVectors.CheckKeys(entry, handled, []);
        }
    }

    private static void CheckTableDb1Section(string name, JsonElement e, TableSection s)
    {
        string at = $"{name}: section {s.Ordinal}";
        Assert.True(e.GetProperty("ordinal").GetInt32() == s.Ordinal, $"{at}: ordinal");
        Assert.True(e.GetProperty("start").GetInt64() == s.Offset, $"{at}: start");
        int? key = e.GetProperty("K").ValueKind == JsonValueKind.Null ? null : e.GetProperty("K").GetInt32();
        Assert.True(key == s.Key, $"{at}: K");
        Assert.True(e.GetProperty("row_size").GetInt32() == s.RowSize, $"{at}: row_size");
        Assert.True(e.GetProperty("desc_count").GetInt32() == s.Descriptors.Count, $"{at}: desc_count");
        var ones = Enumerable.Range(0, s.Descriptors.Count).Where(i => s.Descriptors[i] == 1);
        Assert.True(TableJson.Ints(e.GetProperty("desc_ones")).SequenceEqual(ones), $"{at}: desc_ones");
        Assert.True(e.GetProperty("row_count").GetInt32() == s.Rows.Count, $"{at}: row_count");
        Assert.True(e.GetProperty("end").GetInt64() == s.End, $"{at}: end");
        var rows = e.GetProperty("rows").EnumerateArray().ToList();
        Assert.True(rows.Count == s.Rows.Count, $"{at}: rows");
        for (int i = 0; i < rows.Count; i++)
        {
            var row = s.Rows[i];
            Assert.True(rows[i].GetProperty("off").GetInt64() == row.Offset, $"{at}: row {i} off");
            Assert.True(rows[i].GetProperty("marker").GetInt32() == row.Marker, $"{at}: row {i} marker");
            Assert.True(rows[i].GetProperty("a").GetInt64() == row.A, $"{at}: row {i} a");
            Assert.True(rows[i].GetProperty("b").GetInt64() == row.B, $"{at}: row {i} b");
        }
    }

    // decoded_text_at_N_width_M: the text of the field at N of every row, decoded as the library decodes it: ASCII, UTF-8,
    // else code page 1251 or 1252 chosen once for the file.
    private static bool CheckDecodedRowText(string name, JsonProperty property, IReadOnlyList<TableSection> sections)
    {
        if (TableJson.DecodedTextKey.Match(property.Name) is not { Success: true } m) return false;
        int at = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int width = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        var rows = Assert.Single(sections).Rows;
        var values = property.Value.EnumerateArray().ToList();
        Assert.True(values.Count == rows.Count, $"{name}: {property.Name} count");
        for (int i = 0; i < values.Count; i++)
        {
            string actual = Bytes.Text(rows[i].Body.Span, at, width, rows[i].TextEncoding, rows[i].Text?.CodePage);
            Assert.True(string.Equals(values[i].GetString(), actual, StringComparison.Ordinal), $"{name}: {property.Name} row {i}");
        }
        return true;
    }

    // A section with the key and row size of a typed decoder: the text of the decoded rows equals decoded_text_*.
    private static void CheckTypedDecoderText(string name, JsonElement expected, IReadOnlyList<TableSection> sections)
    {
        if (!expected.TryGetProperty("decoded_text_at_8_width_21", out var names)
            || !expected.TryGetProperty("decoded_text_at_29_width_91", out var values))
            return;
        TableSection section = Assert.Single(sections, s => s.Key == StrAttrAttrRow.TableKey && s.RowSize == StrAttrAttrRow.ExpectedRowSize);
        var rows = section.AsStrAttrAttrs();
        var expectedNames = names.EnumerateArray().ToList();
        var expectedValues = values.EnumerateArray().ToList();
        Assert.True(rows.Count == expectedNames.Count && rows.Count == expectedValues.Count, $"{name}: decoded rows count");
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.True(string.Equals(expectedNames[i].GetString(), rows[i].Name, StringComparison.Ordinal), $"{name}: decoded name row {i}");
            Assert.True(string.Equals(expectedValues[i].GetString(), rows[i].Value, StringComparison.Ordinal), $"{name}: decoded value row {i}");
        }
    }

    // Values read from row bodies at fixed offsets. They only prove that the bodies are sliced at the right place; the
    // library itself never interprets bodies.
    private static bool CheckTableRowValues(string name, JsonProperty property, IReadOnlyList<TableSection> sections)
    {
        if (TableJson.RowValueKey.Match(property.Name) is { Success: true } m)
        {
            var section = m.Groups[2].Success
                ? sections.Single(s => s.Ordinal == int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
                : Assert.Single(sections);
            int at = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            bool isText = m.Groups[3].Value == "text";
            List<JsonElement> values = m.Groups[1].Value == "rows" ? property.Value.EnumerateArray().ToList() : [property.Value];
            Assert.True(values.Count == section.Rows.Count || m.Groups[1].Value != "rows", $"{name}: {property.Name} count");
            for (int i = 0; i < values.Count; i++)
            {
                ReadOnlySpan<byte> body = section.Rows[i].Body.Span[at..];
                if (isText)
                {
                    int nul = body.IndexOf((byte)0);
                    var text = nul >= 0 ? body[..nul] : body;
                    string expected = values[i].ValueKind == JsonValueKind.Object ? values[i].GetProperty("hex").GetString()! : values[i].GetString()!;
                    string actual = values[i].ValueKind == JsonValueKind.Object ? Convert.ToHexString(text) : Encoding.ASCII.GetString(text);
                    Assert.True(string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase), $"{name}: {property.Name} row {i}");
                }
                else
                {
                    Assert.True(values[i].GetInt32() == BinaryPrimitives.ReadInt32LittleEndian(body), $"{name}: {property.Name} row {i}");
                }
            }
            return true;
        }

        int[]? offsets = null;
        if (TableJson.F64ListKey.Match(property.Name) is { Success: true } list)
            offsets = list.Groups[1].Value.Split('_').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        else if (TableJson.F64RangeKey.Match(property.Name) is { Success: true } range)
        {
            int from = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture), to = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
            offsets = Enumerable.Range(0, (to - from) / 8 + 1).Select(k => from + 8 * k).ToArray();
        }
        if (offsets is null) return false;

        var rows = Assert.Single(sections).Rows;
        var expectedRows = property.Value.EnumerateArray().ToList();
        Assert.True(expectedRows.Count == rows.Count, $"{name}: {property.Name} count");
        for (int i = 0; i < rows.Count; i++)
        {
            var expected = expectedRows[i].EnumerateArray().Select(v => v.GetDouble()).ToList();
            Assert.True(expected.Count == offsets.Length, $"{name}: {property.Name} row {i} width");
            for (int k = 0; k < offsets.Length; k++)
            {
                double actual = BinaryPrimitives.ReadDoubleLittleEndian(rows[i].Body.Span[offsets[k]..]);
                // The manifest rounds some values; 1e-9 relative (absolute below 1) covers that and nothing more.
                Assert.True(Math.Abs(actual - expected[k]) <= 1e-9 * Math.Max(1, Math.Abs(expected[k])),
                    $"{name}: {property.Name} row {i} value {k}");
            }
        }
        return true;
    }

    [SkippableFact]
    public void PreAreaVectorPrecedesTheFirstSection()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("record_count", "last40_hex"), "the pre-area"))
        {
            string name = TestVectors.Name(entry);
            byte[] slice = TestVectors.Read(entry);
            var expected = TableJson.Expected(entry);
            // A made-up header before and an empty section after turn the slice into a payload the readers accept.
            byte[] payload = Synthetic.Concat(Synthetic.Ascii("Xsteel"), [0x85], Synthetic.Ascii(" 1.23 01234567-89ab-cdef-0123-456789abcdef"),
                slice, Synthetic.Db1Section(null, 4, [1]));

            int first = SectionWalker.FindFirstSection(payload);
            Assert.True(expected.GetProperty("length_equals_first_section_offset_minus_49").GetBoolean() == (first - 49 == slice.Length),
                $"{name}: first section offset");
            Assert.True(string.Equals(expected.GetProperty("last40_hex").GetString(), Convert.ToHexString(slice.AsSpan(^40)),
                StringComparison.OrdinalIgnoreCase), $"{name}: last40_hex");
            Assert.True(expected.GetProperty("i32_at_0").GetInt32() == BinaryPrimitives.ReadInt32LittleEndian(slice), $"{name}: i32_at_0");
            Assert.True(uint.Parse(expected.GetProperty("marker_u32_at_4").GetString()!.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                == BinaryPrimitives.ReadUInt32LittleEndian(slice.AsSpan(4)), $"{name}: marker_u32_at_4");

            var map = IdMapReader.Read(payload, [])!;
            int count = expected.GetProperty("record_count").GetInt32();
            Assert.True(count == map.Entries.Count, $"{name}: record_count");
            var five = TableJson.Ints(expected.GetProperty("five_i32_at_8")).ToArray();
            Assert.True(five[2] == map.Field65 && five[3] == count + 1 && five[4] == map.Field73, $"{name}: five_i32_at_8");
            Assert.True(TableJson.Ints(expected.GetProperty("record_first_i32")).SequenceEqual(map.Entries.Select(r => (int)r.Sequence)), $"{name}: record_first_i32");
            Assert.True(TableJson.Ints(expected.GetProperty("record_last_i32")).SequenceEqual(map.Entries.Select(r => r.Value)), $"{name}: record_last_i32");
            long recordsEnd = 49 + expected.GetProperty("records_start").GetInt64() + count * expected.GetProperty("record_size").GetInt64();
            Assert.True(recordsEnd == map.End, $"{name}: records_start and record_size");
            Assert.True(expected.GetProperty("list_header_at").GetInt64() == map.End - 49, $"{name}: list_header_at");

            // The lists and the tail after the id map are not read by the library.
            TestVectors.CheckKeys(entry,
                ["length_equals_first_section_offset_minus_49", "last40_hex", "i32_at_0", "marker_u32_at_4", "record_count", "five_i32_at_8",
                 "record_first_i32", "record_last_i32", "records_start", "record_size", "list_header_at"],
                ["list_header", "list1", "list2", "list2_sum", "tail_ints_at", "tail_ints"]);
        }
    }

    [SkippableFact]
    public void DbvGzipVectorUnpacks()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("gzip_header_hex"), "a gzip DBV@ file"))
        {
            string name = TestVectors.Name(entry);
            byte[] file = TestVectors.Read(entry);
            var expected = TableJson.Expected(entry);
            Assert.True(string.Equals(expected.GetProperty("gzip_header_hex").GetString(), Convert.ToHexString(file, 0, 10),
                StringComparison.OrdinalIgnoreCase), $"{name}: gzip header");
            byte[] payload = Gzip.Unpack(file, TableJson.UnpackLimit);
            Assert.True(expected.GetProperty("unpacked_length").GetInt64() == payload.Length, $"{name}: unpacked_length");
            Assert.True(string.Equals(expected.GetProperty("unpacked_sha256").GetString(), Convert.ToHexString(SHA256.HashData(payload)),
                StringComparison.OrdinalIgnoreCase), $"{name}: unpacked_sha256");
            Assert.True(expected.GetProperty("crc32_ok").GetBoolean() == (Crc32.Compute(payload) == BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(^8))),
                $"{name}: crc32_ok");
            Assert.True(KindDetector.FromRaw(file, null) == TdbFileKind.Dbv, $"{name}: kind");
            Assert.Equal(payload.Length, DbvDocument.Create(payload).Payload.Length);
            TestVectors.CheckKeys(entry, ["gzip_header_hex", "unpacked_length", "unpacked_sha256", "crc32_ok"], []);
        }
    }

    [SkippableFact]
    public void DbvFileVectorsParseAsExpected()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("prologue"), "a DBV@ file"))
        {
            string name = TestVectors.Name(entry);
            var expected = TableJson.Expected(entry);
            var doc = DbvDocument.Create(Gzip.Unpack(Synthetic.Gzip(TestVectors.Read(entry)), TableJson.UnpackLimit));
            int n = expected.GetProperty("name_length").GetInt32();
            int marker = 8 + n + 24;

            Assert.True(expected.GetProperty("name").GetString() == doc.Name && n == doc.Name.Length, $"{name}: name");
            Assert.True(TableJson.UInts(expected.GetProperty("prologue")).SequenceEqual(doc.Prologue), $"{name}: prologue");
            Assert.True(expected.GetProperty("marker_bc_offset").GetInt32() == marker, $"{name}: marker_bc_offset");
            Assert.True(BinaryPrimitives.ReadUInt32LittleEndian(doc.Payload.Span[marker..]) == 0xDBCEC0BC, $"{name}: marker");
            Assert.True(TableJson.UInts(expected.GetProperty("A")).SequenceEqual(doc.ListHeader), $"{name}: A");
            Assert.True(expected.GetProperty("guid_record_count").GetInt32() == doc.GuidRecords.Count, $"{name}: guid_record_count");
            Assert.True(expected.GetProperty("guid_records_offset").GetInt32() == marker + 20, $"{name}: guid_records_offset");
            var records = expected.GetProperty("guid_records").EnumerateArray().ToList();
            Assert.True(records.Count == doc.GuidRecords.Count, $"{name}: guid_records");
            for (int i = 0; i < records.Count; i++)
            {
                var r = doc.GuidRecords[i];
                var guid = records[i].GetProperty("guid");
                Assert.True(records[i].GetProperty("v").GetUInt32() == r.Value && records[i].GetProperty("index").GetUInt32() == r.Index,
                    $"{name}: guid record {i}");
                Assert.True(guid.GetProperty("text").GetString() == r.Id.ToString("D"), $"{name}: guid record {i} text");
                Assert.True(string.Equals(guid.GetProperty("bytes").GetString(), Convert.ToHexString(r.Id.ToByteArray()), StringComparison.OrdinalIgnoreCase),
                    $"{name}: guid record {i} bytes");
            }
            // The raw words B, C, K, L1, L2 and T are read back from the payload: the API no longer exposes them, and
            // the reader checks their relations against the values always seen. The marker offset is computed above.
            int bOff = marker + 20 + 24 * doc.GuidRecords.Count;
            uint ReadU32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(doc.Payload.Span.Slice(at, 4));
            Assert.True(expected.GetProperty("B").GetUInt32() == doc.BaseOffset, $"{name}: B");
            Assert.True(expected.GetProperty("B").GetUInt32() == ReadU32(bOff), $"{name}: B in the payload");
            Assert.True(expected.GetProperty("C").GetUInt32() == ReadU32(bOff + 4), $"{name}: C");
            int k = (int)ReadU32(bOff + 8);
            Assert.True(expected.GetProperty("K").GetInt32() == k, $"{name}: K");
            var l1 = Enumerable.Range(0, k).Select(i => ReadU32(bOff + 12 + 4 * i)).ToArray();
            Assert.True(TableJson.UInts(expected.GetProperty("L1")).SequenceEqual(l1), $"{name}: L1");
            var l2 = Enumerable.Range(0, k).Select(i => (int)ReadU32(bOff + 12 + 4 * k + 4 * i)).ToArray();
            Assert.True(TableJson.Ints(expected.GetProperty("L2")).SequenceEqual(l2), $"{name}: L2");
            var t = Enumerable.Range(0, 6).Select(i => ReadU32(bOff + 12 + 8 * k + 4 * i)).ToArray();
            Assert.True(TableJson.UInts(expected.GetProperty("T")).SequenceEqual(t), $"{name}: T");
            var c9 = expected.GetProperty("c9_records").EnumerateArray().Select(r => TableJson.Ints(r).ToArray()).ToList();
            Assert.True(c9.Count == doc.C9Records.Count, $"{name}: c9_records");
            for (int i = 0; i < c9.Count; i++)
                Assert.True(new DbvC9Record(c9[i][0], c9[i][1], c9[i][2], c9[i][3]) == doc.C9Records[i], $"{name}: c9 record {i}");

            var handled = new List<string> { "name", "name_length", "prologue", "marker_bc_offset", "A", "guid_record_count", "guid_records_offset",
                "guid_records", "B", "C", "K", "L1", "L2", "T", "c9_records" };
            if (expected.TryGetProperty("sum_L2", out var sum))
            {
                Assert.True(sum.GetInt64() == l2.Sum(v => (long)v), $"{name}: sum_L2");
                handled.Add("sum_L2");
            }
            if (expected.TryGetProperty("T1_minus_B_div_32", out var blocks))
            {
                Assert.True(blocks.GetInt64() == (t[1] - (long)doc.BaseOffset) / 32, $"{name}: T1_minus_B_div_32");
                handled.Add("T1_minus_B_div_32");
            }
            if (expected.TryGetProperty("sections", out var sections))
            {
                var list = sections.EnumerateArray().ToList();
                Assert.True(list.Count == doc.Sections.Count, $"{name}: sections");
                for (int i = 0; i < list.Count; i++) CheckTableDbvSection($"{name}: section {i}", list[i], doc.Sections[i], null);
                handled.Add("sections");
            }
            else
            {
                Assert.True(doc.Sections.Count == 0, $"{name}: a slice without sections has sections");
            }
            if (expected.TryGetProperty("length", out var length))
            {
                Assert.True(length.GetInt64() == doc.Payload.Length, $"{name}: length");
                handled.Add("length");
            }
            Assert.True(doc.Warnings.Count == 0, $"{name}: warnings: {string.Join("; ", doc.Warnings)}");
            TestVectors.CheckKeys(entry, handled, []);
        }
    }

    [SkippableFact]
    public void DbvSectionVectorsReadAsExpected()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("tuple_size", "rows"), "a DBV@ section"))
        {
            string name = TestVectors.Name(entry);
            byte[] data = TestVectors.Read(entry);
            var expected = TableJson.Expected(entry);
            var warnings = new List<string>();
            var section = SectionWalker.ReadOne(data, 0, 0, SectionMode.Dbv, warnings);
            CheckTableDbvSection(name, expected, section, data.Length);
            uint low = expected.GetProperty("B_of_file").GetUInt32(), high = expected.GetProperty("T1_of_file").GetUInt32();
            Assert.True(section.Rows.All(r => r.A > low && r.B > low && r.A < high && r.B < high), $"{name}: c and m between B and T[1]");
            Assert.True(warnings.Count == 0, $"{name}: warnings: {string.Join("; ", warnings)}");
            TestVectors.CheckKeys(entry, ["offset", "tuple_size", "field_count", "descriptors", "rows", "end", "row_type_bytes", "first_row", "last_row",
                "B_of_file", "T1_of_file"], []);
        }

        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("sections").Where(e => !TableJson.Expected(e).TryGetProperty("prologue", out _)
            && TableJson.FirstSectionHas(e, "tuple_size")), "a run of DBV@ sections"))
        {
            string name = TestVectors.Name(entry);
            byte[] data = TestVectors.Read(entry);
            var list = TableJson.Expected(entry).GetProperty("sections").EnumerateArray().ToList();
            var sections = SectionWalker.Walk(data, 0, 0, SectionMode.Dbv, []);
            Assert.True(list.Count == sections.Count, $"{name}: sections");
            for (int i = 0; i < list.Count; i++) CheckTableDbvSection($"{name}: section {i}", list[i], sections[i], null);
            Assert.Equal(data.Length, sections[^1].End);
            TestVectors.CheckKeys(entry, ["sections"], []);
        }
    }

    private static void CheckTableDbvSection(string at, JsonElement e, TableSection s, long? length)
    {
        Assert.True(e.GetProperty("offset").GetInt64() == s.Offset, $"{at}: offset");
        Assert.True(e.GetProperty("tuple_size").GetInt32() == s.RowSize, $"{at}: tuple_size");
        Assert.True(e.GetProperty("field_count").GetInt32() == s.Descriptors.Count, $"{at}: field_count");
        Assert.True(TableJson.UInts(e.GetProperty("descriptors")).SequenceEqual(s.Descriptors), $"{at}: descriptors");
        Assert.True(e.GetProperty("rows").GetInt32() == s.Rows.Count, $"{at}: rows");
        Assert.True(e.GetProperty("end").GetInt64() == s.End, $"{at}: end");
        if (length is long l) Assert.True(l == s.End, $"{at}: end is not the end of the data");
        var types = s.Rows.Select(r => (int)r.Marker).Distinct().Order();
        Assert.True(TableJson.Ints(e.GetProperty("row_type_bytes")).SequenceEqual(types), $"{at}: row_type_bytes");
        CheckTableRow(at, "first_row", e.GetProperty("first_row"), s.Rows.Count > 0 ? s.Rows[0] : null);
        CheckTableRow(at, "last_row", e.GetProperty("last_row"), s.Rows.Count > 0 ? s.Rows[^1] : null);
    }

    private static void CheckTableRow(string at, string key, JsonElement e, TableRow? row)
    {
        if (e.ValueKind == JsonValueKind.Null)
        {
            Assert.True(row is null, $"{at}: {key} should be absent");
            return;
        }
        Assert.True(row is not null, $"{at}: {key} is missing");
        var pair = TableJson.Ints(e.GetProperty("pair")).ToArray();
        Assert.True(e.GetProperty("offset").GetInt64() == row.Value.Offset && pair[0] == row.Value.A && pair[1] == row.Value.B, $"{at}: {key}");
    }

    [SkippableFact]
    public void Db1GapVectorsShowTheBytesBetweenSections()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("gap_bytes"), "a .db1 gap"))
        {
            string name = TestVectors.Name(entry);
            byte[] data = TestVectors.Read(entry);
            var expected = TableJson.Expected(entry);
            var section = SectionWalker.ReadOne(data, 0, 0, SectionMode.Dbv, []);
            Assert.True(expected.GetProperty("tuple_size").GetInt32() == section.RowSize, $"{name}: tuple_size");
            Assert.True(expected.GetProperty("field_count").GetInt32() == section.Descriptors.Count, $"{name}: field_count");
            Assert.True(expected.GetProperty("terminator_offset").GetInt64() == section.End - 1 && data[section.End - 1] == 0, $"{name}: terminator_offset");

            int next = expected.GetProperty("next_marker_offset").GetInt32();
            int gap = expected.GetProperty("gap_bytes").GetInt32();
            Assert.True(next - section.End == gap, $"{name}: gap_bytes");
            var gapWords = Enumerable.Range(0, gap / 4).Select(i => (long)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)section.End + 4 * i)));
            Assert.True(expected.GetProperty("gap_u32").EnumerateArray().Select(v => v.GetInt64()).SequenceEqual(gapWords), $"{name}: gap_u32");
            Assert.True(data.AsSpan(next, 4).SequenceEqual(Synthetic.SectionSeparator), $"{name}: next marker");
            Assert.True(expected.GetProperty("next_tuple_size").GetUInt32() == BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(next + 4)), $"{name}: next_tuple_size");
            Assert.True(expected.GetProperty("next_field_count").GetUInt32() == BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(next + 8)), $"{name}: next_field_count");

            // A DBV@ reader must not skip the gap.
            var e = Assert.Throws<InvalidDataException>(() => SectionWalker.Walk(data, 0, 0, SectionMode.Dbv, []));
            Assert.True(e.Offset == section.End, $"{name}: the gap is reported at 0x{e.Offset:X}");
            TestVectors.CheckKeys(entry, ["tuple_size", "field_count", "terminator_offset", "gap_bytes", "gap_u32", "next_marker_offset",
                "next_tuple_size", "next_field_count"], []);
        }
    }

    [SkippableFact]
    public void BrokenDbvVectorsThrow()
    {
        foreach (var entry in TableJson.Selected(TestVectors.WithKeys("error"), "a broken DBV@ file"))
        {
            string name = TestVectors.Name(entry);
            byte[] packed = Synthetic.Gzip(TestVectors.Read(entry));
            Assert.True(TableJson.Expected(entry).GetProperty("error").GetBoolean(), $"{name}: error");
            var e = Record.Exception(() => DbvDocument.Create(Gzip.Unpack(packed, TableJson.UnpackLimit)));
            Assert.True(e is InvalidDataException, $"{name}: {e?.GetType().Name ?? "no exception"}");
            TestVectors.CheckKeys(entry, ["error"], ["reason"]);
        }
    }

    /// <summary>Walks every private model database whose unpacked bytes match an entry of the walk summary, matched by
    /// SHA-256, never by name. Needs both the vectors and the sample files.</summary>
    [SkippableFact]
    public void SampleDatabasesWalkAsTheSummarySays()
    {
        var summaries = TestVectors.Entries().Where(e => TestVectors.Name(e).EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(summaries.Count > 0, "No walk summary in the manifests.");
        using var summary = JsonDocument.Parse(TestVectors.Read(Assert.Single(summaries)));
        var root = summary.RootElement;
        var fixtures = root.GetProperty("fixtures").EnumerateObject().Select(p => p.Value).ToList();
        var layout = root.EnumerateObject().Single(p => p.Name.StartsWith("layout_by_ordinal_", StringComparison.Ordinal)).Value
            .EnumerateArray().ToList();

        var files = TestFiles.FindAll(".db1", ".bak");
        if (files.Count == 0) throw TestFiles.Missing(".db1 or .bak files");
        int matched = 0;
        foreach (string file in files)
        {
            if (TdbFile.DetectKind(file) != TdbFileKind.Db1) continue;
            byte[] payload = Gzip.Unpack(File.ReadAllBytes(file), TableJson.UnpackLimit);
            string hash = Convert.ToHexString(SHA256.HashData(payload));
            var fixture = fixtures.FirstOrDefault(f => string.Equals(f.GetProperty("unpacked_sha256").GetString(), hash, StringComparison.OrdinalIgnoreCase));
            if (fixture.ValueKind != JsonValueKind.Object) continue;
            matched++;
            CheckTableWalkSummary(TestFiles.Relative(file), fixture, layout, payload);
        }
        if (matched == 0) throw new SkipException("No sample database matches the walk summary.");
    }

    private static void CheckTableWalkSummary(string at, JsonElement fixture, IReadOnlyList<JsonElement> layout, byte[] payload)
    {
        var tables = Db1Document.Create(payload).ReadTables();
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "unpacked_length", "unpacked_sha256", "version_bytes_8_11", "first_section_offset", "section_count", "plain_count", "keyed_count",
            "row_total", "rows_by_ordinal", "marker_counts",
        };
        HashSet<int> layoutDiffers = [];
        var keyDiffers = new Dictionary<int, int>();
        foreach (var property in fixture.EnumerateObject())
        {
            if (property.Name.StartsWith("layout_differs_from_", StringComparison.Ordinal))
                layoutDiffers = [.. TableJson.Ints(property.Value)];
            else if (property.Name.StartsWith("K_differs_from_", StringComparison.Ordinal))
                keyDiffers = property.Value.EnumerateObject().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetInt32());
            else
                Assert.True(known.Contains(property.Name), $"{at}: summary key {property.Name} is not checked");
        }

        Assert.True(fixture.GetProperty("unpacked_length").GetInt64() == payload.Length, $"{at}: unpacked_length");
        Assert.True(fixture.GetProperty("version_bytes_8_11").GetString() == Encoding.ASCII.GetString(payload, 8, 4), $"{at}: version");
        Assert.True(fixture.GetProperty("first_section_offset").GetInt64() == tables[0].Offset, $"{at}: first_section_offset");
        Assert.True(fixture.GetProperty("section_count").GetInt32() == tables.Count, $"{at}: section_count");
        Assert.True(fixture.GetProperty("plain_count").GetInt32() == tables.Count(t => t.Key is null), $"{at}: plain_count");
        Assert.True(fixture.GetProperty("keyed_count").GetInt32() == tables.Count(t => t.Key is not null), $"{at}: keyed_count");
        Assert.True(fixture.GetProperty("row_total").GetInt64() == tables.Sum(t => (long)t.Rows.Count), $"{at}: row_total");
        Assert.True(TableJson.Ints(fixture.GetProperty("rows_by_ordinal")).SequenceEqual(tables.Select(t => t.Rows.Count)), $"{at}: rows_by_ordinal");
        Assert.Equal(payload.Length, tables[^1].End);

        var markers = new Dictionary<int, long>();
        foreach (var table in tables)
            foreach (var row in table.Rows)
                markers[row.Marker] = markers.GetValueOrDefault(row.Marker) + 1;
        var expectedMarkers = fixture.GetProperty("marker_counts").EnumerateObject()
            .ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetInt64());
        Assert.True(markers.Keys.All(expectedMarkers.ContainsKey) && expectedMarkers.All(m => markers.GetValueOrDefault(m.Key) == m.Value),
            $"{at}: marker_counts");

        // The layout list is [row size, descriptor count, key or null, indexes of descriptor words equal to 1].
        for (int i = 0; i < Math.Min(tables.Count, layout.Count); i++)
        {
            if (layoutDiffers.Contains(i)) continue;
            var l = layout[i].EnumerateArray().ToList();
            var t = tables[i];
            Assert.True(l[0].GetInt32() == t.RowSize && l[1].GetInt32() == t.Descriptors.Count, $"{at}: layout of section {i}");
            Assert.True(TableJson.Ints(l[3]).SequenceEqual(Enumerable.Range(0, t.Descriptors.Count).Where(k => t.Descriptors[k] == 1)), $"{at}: descriptors of section {i}");
            int? key = keyDiffers.TryGetValue(i, out int changed) ? changed : l[2].ValueKind == JsonValueKind.Null ? null : l[2].GetInt32();
            Assert.True(key == t.Key, $"{at}: key of section {i}");
        }
    }
}

/// <summary>JSON helpers of the table vector tests, in their own class so that they cannot clash with the helpers of other
/// parts of <see cref="VectorTests"/>.</summary>
internal static class TableJson
{
    public const long UnpackLimit = 1L << 30;

    public static readonly Regex RowValueKey = new(@"^(rows|row0|ord(\d+)_row0)_(text|i32)_at_(\d+)$", RegexOptions.CultureInvariant);
    public static readonly Regex DecodedTextKey = new(@"^decoded_text_at_(\d+)_width_(\d+)$", RegexOptions.CultureInvariant);
    public static readonly Regex F64ListKey = new(@"^rows_f64_at_(\d+(?:_\d+)+)$", RegexOptions.CultureInvariant);
    public static readonly Regex F64RangeKey = new(@"^rows_f64_at_(\d+)_to_(\d+)$", RegexOptions.CultureInvariant);

    public static JsonElement Expected(JsonElement entry) => TestVectors.Expected(entry)!.Value;

    public static bool FirstSectionHas(JsonElement entry, string key) =>
        Expected(entry).GetProperty("sections").EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } first
        && first.TryGetProperty(key, out _);

    public static IReadOnlyList<JsonElement> Selected(IEnumerable<JsonElement> entries, string what)
    {
        var list = entries.ToList();
        Assert.True(list.Count > 0, $"No vector in the manifests has the expected keys of {what}.");
        return list;
    }

    public static IEnumerable<int> Ints(JsonElement array) => array.EnumerateArray().Select(v => v.GetInt32());

    public static IEnumerable<uint> UInts(JsonElement array) => array.EnumerateArray().Select(v => v.GetUInt32());
}
