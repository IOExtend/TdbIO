using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TdbIO.Internal;
using Xunit;
using Xunit.Sdk;

namespace TdbIO.Tests;

public partial class VectorTests
{
    [SkippableFact]
    public void ContainerIdMapsMatch() => ContainerVector.ForEach(["count_field_at_69"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry,
            ["u32_at_49", "marker_at_53", "u32_at_57", "u32_at_61", "u32_at_65", "count_field_at_69", "u32_at_73", "records",
             "end_offset", "u32_after_end", "magic", "type_byte", "version", "model_guid", "records_list",
             "seq_is_permutation_1_to_records", "seq_ascending", "first_mismatch_index_1based", "first_record", "last_record",
             "sum_values", "all_guids_v4_rfc_variant_net_order"], []);
        ContainerVector.CheckHeader(x, bytes);
        foreach (int at in new[] { 49, 57, 61 })
            Assert.Equal(x.GetProperty($"u32_at_{at}").GetUInt32(), Bytes.U32(bytes, at));
        Assert.Equal(ContainerVector.Hex(x.GetProperty("marker_at_53")), Bytes.U32(bytes, 53));
        Assert.Equal(x.GetProperty("count_field_at_69").GetUInt32(), Bytes.U32(bytes, 69));

        var document = Db1Document.Create(bytes);
        Assert.Equal(ContainerVector.Hex(x.GetProperty("u32_at_65")), document.IdMapField65);
        Assert.Equal(x.GetProperty("u32_at_73").GetUInt32(), document.IdMapField73);
        var map = document.IdMap!;
        Assert.Equal(x.GetProperty("records").GetInt32(), map.Count);
        var read = IdMapReader.Read(bytes, [])!;
        Assert.Equal(x.GetProperty("end_offset").GetInt64(), read.End);
        Assert.Equal(x.GetProperty("u32_after_end").GetUInt32(), Bytes.U32(bytes, read.End));

        if (x.TryGetProperty("records_list", out var list))
            Assert.Equal(list.EnumerateArray().Select(ContainerVector.Entry), map);
        if (x.TryGetProperty("first_record", out var first)) Assert.Equal(ContainerVector.Entry(first), map[0]);
        if (x.TryGetProperty("last_record", out var last)) Assert.Equal(ContainerVector.Entry(last), map[^1]);
        if (x.TryGetProperty("sum_values", out var sum)) Assert.Equal(sum.GetInt64(), map.Sum(e => (long)e.Value));
        if (x.TryGetProperty("seq_is_permutation_1_to_records", out var permutation))
            Assert.Equal(permutation.GetBoolean(), map.Select(e => e.Sequence).Order().SequenceEqual(Enumerable.Range(1, map.Count).Select(i => (uint)i)));
        if (x.TryGetProperty("seq_ascending", out var ascending))
            Assert.Equal(ascending.GetBoolean(), map.Select((e, i) => e.Sequence == i + 1).All(b => b));
        if (x.TryGetProperty("first_mismatch_index_1based", out var mismatch))
            Assert.Equal(mismatch.GetInt32(), map.Select((e, i) => (e, i)).First(p => p.e.Sequence != p.i + 1).i + 1);
        if (x.TryGetProperty("all_guids_v4_rfc_variant_net_order", out var v4))
            Assert.Equal(v4.GetBoolean(), map.All(e => e.Id.ToString()[14] == '4' && "89ab".Contains(e.Id.ToString()[19])));
    });

    [SkippableFact]
    public void ContainerHeadersMatch() => ContainerVector.ForEach(["type_byte", "model_guid"], (entry, x, bytes) =>
    {
        if (x.TryGetProperty("count_field_at_69", out _) || x.TryGetProperty("gzip_members", out _)) return; // checked elsewhere
        TestVectors.CheckKeys(entry, ["magic", "type_byte", "version", "model_guid"], []);
        ContainerVector.CheckHeader(x, bytes);
    });

    [SkippableFact]
    public void ContainerDrawingHeaderIsNotSupported() => ContainerVector.ForEach(["bytes_7_12"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry, ["magic", "type_byte", "bytes_7_12"], []);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("magic").GetString()!)));
        Assert.Equal(x.GetProperty("type_byte").GetByte(), bytes[6]);
        Assert.Equal(x.GetProperty("bytes_7_12").GetString(), Encoding.ASCII.GetString(bytes, 7, 6));
        Assert.Equal(TdbFileKind.Drawing, TdbFile.DetectKind(new MemoryStream(Synthetic.Gzip(bytes))));
        Assert.Throws<NotSupportedException>(() => Db1Document.Create(bytes));
    });

    [SkippableFact]
    public void ContainerRecordFilesMatch() => ContainerVector.ForEach(["gzip_members"], (entry, x, file) =>
    {
        TestVectors.CheckKeys(entry,
            ["gzip_members", "flg", "mtime", "xfl", "os", "unpacked_length", "unpacked_sha256", "crc32_trailer", "isize_trailer",
             "magic", "type_byte", "version", "model_guid", "entries", "row_markers"], []);
        Assert.Equal(1, x.GetProperty("gzip_members").GetInt32()); // Unpack accepts exactly one member
        Assert.Equal(x.GetProperty("flg").GetByte(), file[3]);
        Assert.Equal(x.GetProperty("mtime").GetUInt32(), Bytes.U32(file, 4));
        Assert.Equal(x.GetProperty("xfl").GetByte(), file[8]);
        Assert.Equal(x.GetProperty("os").GetByte(), file[9]);
        Assert.Equal(ContainerVector.Hex(x.GetProperty("crc32_trailer")), Bytes.U32(file, file.Length - 8));
        Assert.Equal(x.GetProperty("isize_trailer").GetUInt32(), Bytes.U32(file, file.Length - 4));

        byte[] payload = Gzip.Unpack(file, TdbReadOptions.Default.MaxUnpackedLength);
        Assert.Equal(x.GetProperty("unpacked_length").GetInt32(), payload.Length);
        Assert.Equal(x.GetProperty("unpacked_sha256").GetString(), Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        ContainerVector.CheckHeader(x, payload);
        Assert.Equal(TdbFileKind.Db2, TdbFile.DetectKind(new MemoryStream(file)));

        var document = Db2Document.Create(payload);
        if (x.TryGetProperty("entries", out var entries))
        {
            // Each entry is key, count, row size and terminator; the terminator is checked by the reader.
            Assert.Equal(entries.EnumerateArray().Select(e => (ContainerVector.Hex(e[0]), e[1].GetUInt32(), e[2].GetUInt32())),
                document.Records.Select(r => (r.Key, r.Count, r.RowSize)));
            Assert.All(entries.EnumerateArray(), e => Assert.Equal(0x00BC614Fu, ContainerVector.Hex(e[3])));
        }
        else Assert.Empty(document.Records);
        if (x.TryGetProperty("row_markers", out var markers))
        {
            var seen = document.Records.SelectMany(r => Enumerable.Range(0, (int)r.Count).Select(i => r.Rows.Span[i * (1 + (int)r.RowSize)])).Distinct().Order();
            Assert.Equal(markers.EnumerateArray().Select(m => m.GetByte()).Order(), seen);
        }
    });

    [SkippableFact]
    public void ContainerIdRangeFilesMatch() => ContainerVector.ForEach(["next_free"], (entry, x, file) =>
    {
        TestVectors.CheckKeys(entry,
            ["unpacked_length", "magic", "next_free", "marker", "count", "end_offset", "records", "ranges_contiguous",
             "next_free_equals_last_start_plus_length"], []);
        byte[] data = Gzip.Unpack(file, TdbReadOptions.Default.MaxUnpackedLength);
        Assert.Equal(x.GetProperty("unpacked_length").GetInt32(), data.Length);
        Assert.True(data.AsSpan().StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("magic").GetString()!)));
        Assert.Equal(ContainerVector.Hex(x.GetProperty("marker")), Bytes.U32(data, 17));
        Assert.Equal(TdbFileKind.IdRanges, TdbFile.DetectKind(new MemoryStream(file)));

        var document = IdRangeDocument.Create(data);
        Assert.Equal(x.GetProperty("next_free").GetUInt32(), document.Next);
        Assert.Equal(x.GetProperty("count").GetInt32(), document.Ranges.Count);
        Assert.Equal(x.GetProperty("end_offset").GetInt32(), 25 + 28 * document.Ranges.Count);
        Assert.Equal(x.GetProperty("records").EnumerateArray().Select(r => new IdRange(
                r.GetProperty("start").GetUInt32(), Guid.Parse(r.GetProperty("guid").GetString()!),
                r.GetProperty("unknown_u32").GetUInt32(), r.GetProperty("length").GetUInt32())),
            document.Ranges);
        bool clean = x.GetProperty("ranges_contiguous").GetBoolean() && x.GetProperty("next_free_equals_last_start_plus_length").GetBoolean();
        Assert.Equal(clean, document.Warnings.Count == 0);
    });

    [SkippableFact]
    public void ContainerGuidMapperMatches() => ContainerVector.ForEach(["header_prefix"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry,
            ["header_prefix", "header_suffix", "header_length_with_lf", "pairs", "first_pair", "last_pair", "line_length_with_lf",
             "cr_count"], []);
        Assert.Equal(TdbFileKind.GuidMapper, TdbFile.DetectKind(new MemoryStream(Synthetic.Gzip(bytes))));
        var document = GuidMapperDocument.Create(bytes);
        var header = document.HeaderLine.Span;
        Assert.True(header.StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("header_prefix").GetString()!)), "header prefix");
        Assert.True(header.EndsWith(Encoding.ASCII.GetBytes(x.GetProperty("header_suffix").GetString()!)), "header suffix");
        Assert.Equal(x.GetProperty("header_length_with_lf").GetInt32(), header.Length + 1);
        int pairs = x.GetProperty("pairs").GetInt32();
        Assert.Equal(pairs, document.Pairs.Count);
        Assert.Equal(ContainerVector.Pair(x.GetProperty("first_pair")), document.Pairs[0]);
        Assert.Equal(ContainerVector.Pair(x.GetProperty("last_pair")), document.Pairs[^1]);
        Assert.Equal(bytes.Length - header.Length - 1, pairs * x.GetProperty("line_length_with_lf").GetInt32());
        Assert.Equal(x.GetProperty("cr_count").GetInt32(), bytes.Count(b => b == '\r'));
    });

    [SkippableFact]
    public void ContainerTextSectionMatches() => ContainerVector.ForEach(["chain_ids"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry,
            ["rows_offset_in_vector", "row_length", "rows", "row_markers", "ids_in_file_order", "chains", "head_id", "head_tag",
             "chain_ids", "text_length", "text_sha256", "text_prefix", "starts_with_Save", "terminator_offset_in_vector"], []);
        int rowsOffset = x.GetProperty("rows_offset_in_vector").GetInt32();
        var section = TextSectionReader.ReadAt(bytes, rowsOffset - TextSectionReader.SignatureLength);
        int rows = x.GetProperty("rows").GetInt32();
        Assert.Equal(rows, section.RowCount);
        Assert.Equal(TextSectionReader.RowLength, x.GetProperty("row_length").GetInt32());
        var fileRows = Enumerable.Range(0, rows).Select(i => TextSectionReader.ReadRow(bytes, rowsOffset + i * TextSectionReader.RowLength)).ToList();
        Assert.Equal(x.GetProperty("row_markers").EnumerateArray().Select(m => m.GetByte()).Order(), fileRows.Select(r => r.Marker).Distinct().Order());
        Assert.Equal(x.GetProperty("ids_in_file_order").EnumerateArray().Select(e => e.GetUInt32()), fileRows.Select(r => r.Id));

        Assert.Equal(x.GetProperty("chains").GetInt32(), section.Chains.Count);
        var chain = section.Chains[0];
        Assert.Equal(x.GetProperty("head_id").GetUInt32(), chain.HeadId);
        Assert.Equal(ContainerVector.Hex(x.GetProperty("head_tag")), chain.HeadTag);
        Assert.Equal(x.GetProperty("chain_ids").EnumerateArray().Select(e => e.GetUInt32()), chain.RowIds);
        Assert.Equal(x.GetProperty("text_length").GetInt32(), chain.Text.Length);
        Assert.Equal(x.GetProperty("text_sha256").GetString(), Convert.ToHexString(SHA256.HashData(chain.Text.Span)).ToLowerInvariant());
        Assert.True(chain.Text.Span.StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("text_prefix").GetString()!)), "text prefix");
        Assert.Equal(x.GetProperty("starts_with_Save").GetBoolean(), chain.IsSaveStamp);
        int terminator = x.GetProperty("terminator_offset_in_vector").GetInt32();
        Assert.Equal(terminator, rowsOffset + rows * TextSectionReader.RowLength);
        Assert.Equal(0, bytes[terminator]);
    });

    [SkippableFact]
    public void ContainerSaveStampRowMatches() => ContainerVector.ForEach(["decoded_after_prefix"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry,
            ["marker", "id", "link", "tag", "field_at_12", "text", "text_after_prefix", "decoded_after_prefix",
             "equals_tail_of_model_guid"], ["chain_length"]);
        var row = ContainerVector.Row(x, bytes);
        Assert.Equal(x.GetProperty("field_at_12").GetUInt32(), row.Field13);
        var text = TextSectionReader.Part(bytes, row.Link == 0, 0);
        Assert.Equal(x.GetProperty("text").GetString(), Encoding.ASCII.GetString(text));
        Assert.Equal(x.GetProperty("text_after_prefix").GetString(), Encoding.ASCII.GetString(text[5..]));
        string decoded = Encoding.ASCII.GetString(HiddenText.Reveal(text[5..]));
        Assert.Equal(x.GetProperty("decoded_after_prefix").GetString(), decoded);

        // The model GUID of the same sample comes from another vector of the manifests, matched by its sample code.
        string fixture = entry.GetProperty("fixture").GetString()!;
        var model = TestVectors.Entries()
            .Where(e => e.TryGetProperty("fixture", out var f) && f.GetString() == fixture)
            .Select(TestVectors.Expected)
            .Where(e => e is { } ex && ex.TryGetProperty("model_guid", out _))
            .Select(e => e!.Value.GetProperty("model_guid").GetString()!).Distinct().Single();
        Assert.Equal(x.GetProperty("equals_tail_of_model_guid").GetBoolean(), model.EndsWith(decoded, StringComparison.Ordinal));
    });

    [SkippableFact]
    public void ContainerLastRowEndsAtNul() => ContainerVector.ForEach(["text_before_nul"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry, ["marker", "id", "link", "tag", "text_before_nul", "text_length", "nonzero_bytes_after_nul"], []);
        var row = ContainerVector.Row(x, bytes);
        Assert.Equal(0u, row.Link);
        var text = TextSectionReader.Part(bytes, last: true, 0);
        Assert.Equal(x.GetProperty("text_before_nul").GetString(), Encoding.ASCII.GetString(text));
        Assert.Equal(x.GetProperty("text_length").GetInt32(), text.Length);
        // Left-over bytes after the NUL, up to the end of the row, are ignored by the reader.
        int after = bytes.AsSpan(17 + text.Length + 1, 29 - text.Length - 1).ToArray().Count(b => b != 0);
        Assert.Equal(x.GetProperty("nonzero_bytes_after_nul").GetInt32(), after);
    });

    [SkippableFact]
    public void ContainerHistoryHeaderMatches() => ContainerVector.ForEach(["page_size"], (entry, x, bytes) =>
    {
        TestVectors.CheckKeys(entry, ["magic", "page_size", "page_count"], []);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("magic").GetString()!.Replace("\\x00", "\0", StringComparison.Ordinal))));
        Assert.Equal(TdbFileKind.History, TdbFile.DetectKind(new MemoryStream(bytes)));
        var document = HistoryDocument.Create(bytes, bytes.Length);
        Assert.Equal(x.GetProperty("page_size").GetUInt16(), document.PageSize);
        Assert.Equal(x.GetProperty("page_count").GetUInt32(), document.PageCount);
        Assert.Single(document.Warnings); // only the head of the file is in the vector
    });
}

/// <summary>Helpers of the container vector tests.</summary>
file static class ContainerVector
{
    /// <summary>Runs <paramref name="check"/> on every vector whose expected values have all <paramref name="keys"/>;
    /// failures name the vector.</summary>
    public static void ForEach(string[] keys, Action<JsonElement, JsonElement, byte[]> check)
    {
        var entries = TestVectors.WithKeys(keys);
        Assert.True(entries.Count > 0, $"No vector has the keys {string.Join(", ", keys)}.");
        foreach (var entry in entries)
        {
            try
            {
                check(entry, TestVectors.Expected(entry)!.Value, TestVectors.Read(entry));
            }
            catch (Exception e) when (e is XunitException or InvalidDataException)
            {
                throw new XunitException($"Vector {TestVectors.Name(entry)}: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>A number, or a string "0x…" in hex.</summary>
    public static uint Hex(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? Convert.ToUInt32(e.GetString()![2..], 16) : e.GetUInt32();

    public static IdMapEntry Entry(JsonElement e) => new(e[0].GetUInt32(), Guid.Parse(e[1].GetString()!), e[2].GetInt32());

    public static GuidPair Pair(JsonElement e) => new(Guid.Parse(e[0].GetString()!), Guid.Parse(e[1].GetString()!));

    public static void CheckHeader(JsonElement x, byte[] payload)
    {
        Assert.True(payload.AsSpan().StartsWith(Encoding.ASCII.GetBytes(x.GetProperty("magic").GetString()!)), "magic");
        var (type, version, model) = XsteelHeader.Read(payload);
        Assert.Equal(x.GetProperty("type_byte").GetByte(), type);
        Assert.Equal(x.GetProperty("version").GetString(), version);
        Assert.Equal(Guid.Parse(x.GetProperty("model_guid").GetString()!), model);
    }

    /// <summary>The fixed fields of a vector that is one 54-byte row, checked against marker, id, link and tag.</summary>
    public static TextRow Row(JsonElement x, byte[] bytes)
    {
        Assert.Equal(TextSectionReader.RowLength, bytes.Length);
        var row = TextSectionReader.ReadRow(bytes, 0);
        Assert.Equal(x.GetProperty("marker").GetByte(), row.Marker);
        Assert.Equal(x.GetProperty("id").GetUInt32(), row.Id);
        Assert.Equal(x.GetProperty("link").GetUInt32(), row.Link);
        Assert.Equal(Hex(x.GetProperty("tag")), row.Tag);
        return row;
    }
}
