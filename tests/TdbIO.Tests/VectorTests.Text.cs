using System.Text.Json;
using Xunit;

namespace TdbIO.Tests;

public partial class VectorTests
{
    [SkippableFact]
    public void DstvVectorsMatch()
    {
        string[] keys = ["line2_comment", "st_fields_1_to_20", "block_sequence", "blocks", "line_ending"];
        TextCheck.CheckEach(TestVectors.WithKeys("st_fields_1_to_20"), "DSTV", entry =>
        {
            TestVectors.CheckKeys(entry, keys, ["line2_note"]);
            var expected = TestVectors.Expected(entry)!.Value;
            byte[] data = TestVectors.Read(entry);
            Assert.Equal(TdbFileKind.Nc1, TdbFile.DetectKind(new MemoryStream(data), "v.nc1"));
            var doc = Nc1Document.Create(data);

            Assert.Equal(expected.GetProperty("line2_comment").GetString(), doc.Comment);
            Assert.Equal(TextCheck.Strings(expected.GetProperty("st_fields_1_to_20")), doc.Fields);
            Assert.Equal(TextCheck.Strings(expected.GetProperty("block_sequence")), doc.Blocks.Select(b => b.Code));
            Assert.Empty(doc.Warnings);

            // The expected blocks list the AK and BO blocks in file order. The key names are those of the manifest.
            var blocks = doc.Blocks.Where(b => b.Code is not ("ST" or "EN")).ToList();
            var expectedBlocks = expected.GetProperty("blocks").EnumerateArray().ToList();
            Assert.Equal(expectedBlocks.Count, blocks.Count);
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var want = expectedBlocks[i];
                Assert.Equal(want.GetProperty("code").GetString(), block.Code);
                if (block.Code == "AK")
                {
                    Assert.Equal(want.GetProperty("face").GetString(), block.Lines[0].Label.ToString());
                    Assert.Equal(want.GetProperty("points").EnumerateArray().Select(TextCheck.Strings),
                        block.Lines.Select(l => l.Values.Select(v => v.Text).ToList()));
                }
                else
                {
                    Assert.Equal("BO", block.Code);
                    Assert.Equal(want.GetProperty("holes").EnumerateArray().Select(TextCheck.Strings),
                        block.Lines.Select(l => l.Values.Select(v => v.Text).Prepend(l.Label.ToString()!).ToList()));
                }
            }

            Assert.StartsWith("CRLF", expected.GetProperty("line_ending").GetString());
            Assert.Equal("\r\n"u8.ToArray(), data[^2..]);
        });
    }

    [SkippableFact]
    public void TemplateVectorsMatch()
    {
        string[] keys =
        [
            "encoding_rule_result", "parse_ok", "root_keyword", "root_instance", "block_counts", "max_depth_root_is_0", "root_type",
            "root_version", "root_width", "root_maxheight", "root_margins", "first_bytes_hex", "line_ending", "first_valuefield_props",
            "strings_with_superscript_two", "text_string_values_equal_U+00A0",
        ];
        TextCheck.CheckEach(TestVectors.WithKeys("root_keyword"), "template", entry =>
        {
            TestVectors.CheckKeys(entry, keys, []);
            var expected = TestVectors.Expected(entry)!.Value;
            byte[] data = TestVectors.Read(entry);
            Assert.Equal(TdbFileKind.Template, TdbFile.DetectKind(new MemoryStream(data), "v.tpl"));
            Assert.True(expected.GetProperty("parse_ok").GetBoolean());
            var doc = TemplateDocument.Create(data, null);
            var root = doc.Root;

            Assert.Equal(expected.GetProperty("encoding_rule_result").GetString(), doc.EncodingName);
            Assert.Equal(expected.GetProperty("root_keyword").GetString(), root.Keyword);
            Assert.Equal(expected.GetProperty("root_instance").GetString(), root.Instance);
            Assert.Equal(expected.GetProperty("max_depth_root_is_0").GetInt32(), TextCheck.MaxDepth(root));

            var counts = TextCheck.Walk(root).GroupBy(b => b.Keyword).ToDictionary(g => g.Key, g => g.Count());
            var wantCounts = expected.GetProperty("block_counts").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
            Assert.Equal(wantCounts.OrderBy(p => p.Key, StringComparer.Ordinal), counts.OrderBy(p => p.Key, StringComparer.Ordinal));

            Assert.Equal(TemplateValueKind.Identifier, root.GetValue("type")?.Kind);
            Assert.Equal(expected.GetProperty("root_type").GetString(), root.GetValue("type")!.Text);
            TextCheck.AssertValue(expected.GetProperty("root_version"), root.GetValue("version"));
            TextCheck.AssertValue(expected.GetProperty("root_width"), root.GetValue("width"));
            TextCheck.AssertValue(expected.GetProperty("root_maxheight"), root.GetValue("maxheight"));
            TextCheck.AssertValue(expected.GetProperty("root_margins"), root.GetValue("margins"));

            Assert.Equal(expected.GetProperty("first_bytes_hex").GetString(), Convert.ToHexString(data, 0, 4).ToLowerInvariant());
            string ending = expected.GetProperty("line_ending").GetString()!;
            bool hasCr = data.Contains((byte)'\r');
            Assert.Equal(ending, hasCr ? "CRLF" : "LF");
            if (hasCr) Assert.Equal(TextCheck.Count(data, "\r\n"u8), data.Count(b => b == '\n'));

            if (expected.TryGetProperty("first_valuefield_props", out var props))
            {
                var field = TextCheck.Walk(root).First(b => b.Keyword == "valuefield");
                foreach (var prop in props.EnumerateObject()) TextCheck.AssertValue(prop.Value, field.GetValue(prop.Name));
            }
            var strings = TextCheck.Walk(root).SelectMany(b => b.Properties).SelectMany(p => TextCheck.Values(p.Value))
                .Where(v => v.Kind == TemplateValueKind.String).Select(v => v.Text).ToList();
            if (expected.TryGetProperty("strings_with_superscript_two", out var squared))
                Assert.Equal(TextCheck.Strings(squared), strings.Where(s => s.Contains('\u00B2')));
            if (expected.TryGetProperty("text_string_values_equal_U+00A0", out var nbsp))
                Assert.Equal(nbsp.GetInt32(), strings.Count(s => s == "\u00A0"));
        });
    }

    [SkippableFact]
    public void ReportVectorsMatch()
    {
        string[] keys = ["encoding_rule_result", "line_count_after_split_on_CRLF", "lines", "ends_with_crlf", "ff_positions"];
        TextCheck.CheckEach(TestVectors.WithKeys("ff_positions"), "report", entry =>
        {
            TestVectors.CheckKeys(entry, keys, []);
            var expected = TestVectors.Expected(entry)!.Value;
            var doc = ReportDocument.Create(TestVectors.Read(entry), null);

            Assert.Equal(expected.GetProperty("encoding_rule_result").GetString(), doc.EncodingName);
            Assert.Equal(expected.GetProperty("ends_with_crlf").GetBoolean(), doc.EndsWithLineBreak);

            // The expected lines are a plain split on CR LF: form feeds kept, and an empty last item after a final CR LF.
            var rebuilt = doc.Lines.Select((l, i) => doc.PageStarts.Contains(i) ? "\f" + l : l).ToList();
            if (doc.EndsWithLineBreak) rebuilt.Add("");
            Assert.Equal(TextCheck.Strings(expected.GetProperty("lines")), rebuilt);
            Assert.Equal(expected.GetProperty("line_count_after_split_on_CRLF").GetInt32(), rebuilt.Count);

            string text = string.Join("\r\n", rebuilt);
            var formFeeds = text.Select((c, i) => (c, i)).Where(p => p.c == '\f').Select(p => p.i);
            Assert.Equal(expected.GetProperty("ff_positions").EnumerateArray().Select(e => e.GetInt32()), formFeeds);
        });
    }

    // Helpers of the text vector tests, nested so that they cannot clash with other parts of this class.
    private static class TextCheck
    {
        public static void CheckEach(IReadOnlyList<JsonElement> entries, string what, Action<JsonElement> check)
        {
            if (entries.Count == 0) throw new SkipException($"The vector folder has no {what} vectors.");
            var failures = new List<string>();
            foreach (var entry in entries)
            {
                try
                {
                    check(entry);
                }
                catch (Exception e) when (e is not SkipException)
                {
                    failures.Add($"{TestVectors.Name(entry)}: {e.Message}");
                }
            }
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        public static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

        public static IEnumerable<TemplateBlock> Walk(TemplateBlock block) => block.Blocks.SelectMany(Walk).Prepend(block);

        public static int MaxDepth(TemplateBlock block) => block.Blocks.Count == 0 ? 0 : 1 + block.Blocks.Max(MaxDepth);

        public static IEnumerable<TemplateValue> Values(TemplateValue value) => value.Items.SelectMany(Values).Prepend(value);

        public static int Count(ReadOnlySpan<byte> data, ReadOnlySpan<byte> part)
        {
            int count = 0;
            for (int at; (at = data.IndexOf(part)) >= 0; data = data[(at + part.Length)..]) count++;
            return count;
        }

        // JSON strings are template strings, numbers are numbers, arrays are tuples and {"enum": X} is the identifier X.
        public static void AssertValue(JsonElement expected, TemplateValue? actual)
        {
            Assert.NotNull(actual);
            switch (expected.ValueKind)
            {
                case JsonValueKind.String:
                    Assert.Equal(TemplateValueKind.String, actual.Kind);
                    Assert.Equal(expected.GetString(), actual.Text);
                    break;
                case JsonValueKind.Number:
                    Assert.Equal(TemplateValueKind.Number, actual.Kind);
                    Assert.Equal(expected.GetDecimal(), actual.Number);
                    break;
                case JsonValueKind.Array:
                    Assert.Equal(TemplateValueKind.Tuple, actual.Kind);
                    var items = expected.EnumerateArray().ToList();
                    Assert.Equal(items.Count, actual.Items.Count);
                    for (int i = 0; i < items.Count; i++) AssertValue(items[i], actual.Items[i]);
                    break;
                case JsonValueKind.Object:
                    Assert.Equal(TemplateValueKind.Identifier, actual.Kind);
                    Assert.Equal(expected.GetProperty("enum").GetString(), actual.Text);
                    break;
                default:
                    Assert.Fail($"Unexpected JSON value kind {expected.ValueKind}.");
                    break;
            }
        }
    }
}
