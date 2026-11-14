using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TdbIO.CommandLine;

/// <summary>A number that text output shows in hexadecimal; JSON output keeps it a plain number.</summary>
internal readonly record struct Hex(long Value);

/// <summary>Text and JSON writers of the <c>tdbio</c> commands. Values are strings, numbers, booleans, <see cref="Hex"/>,
/// <see cref="Guid"/>, <see cref="DateTime"/>, lists of those, or null (shown as <c>-</c> in text).</summary>
internal static class Output
{
    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss";

    /// <summary>Writes <c>key: value</c> lines.</summary>
    public static void WriteFields(TextWriter w, IEnumerable<KeyValuePair<string, object?>> fields)
    {
        foreach (var (key, value) in fields) w.WriteLine($"{key}: {Text(value)}");
    }

    /// <summary>Writes a tab-separated header line, then one line per row.</summary>
    public static void WriteTsv(TextWriter w, IReadOnlyList<string> columns, IEnumerable<object?[]> rows)
    {
        w.WriteLine(string.Join('\t', columns));
        foreach (var row in rows) w.WriteLine(string.Join('\t', row.Select(Text)));
    }

    /// <summary>Writes one indented JSON object built by <paramref name="body"/>.</summary>
    public static void WriteJson(TextWriter w, Action<Utf8JsonWriter> body)
    {
        var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            body(json);
            json.WriteEndObject();
        }
        w.WriteLine(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
    }

    /// <summary>Writes each field as a JSON property.</summary>
    public static void WriteProperties(Utf8JsonWriter json, IEnumerable<KeyValuePair<string, object?>> fields)
    {
        foreach (var (key, value) in fields)
        {
            json.WritePropertyName(key);
            WriteValue(json, value);
        }
    }

    /// <summary>Writes an array of objects, one per row, with <paramref name="columns"/> as property names.</summary>
    public static void WriteObjects(Utf8JsonWriter json, string name, IReadOnlyList<string> columns, IEnumerable<object?[]> rows)
    {
        json.WriteStartArray(name);
        foreach (var row in rows)
        {
            json.WriteStartObject();
            WriteProperties(json, columns.Select((c, i) => new KeyValuePair<string, object?>(c, row[i])));
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    /// <summary>Text of open bytes: printable ASCII as is, <c>\\</c>, <c>\t</c>, <c>\n</c> and <c>\xNN</c> for every other
    /// byte. Nothing is decoded, because the encoding of bytes from 0x80 is not known.</summary>
    public static string EscapeBytes(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(bytes.Length);
        foreach (byte b in bytes)
        {
            switch (b)
            {
                case (byte)'\\': text.Append(@"\\"); break;
                case (byte)'\t': text.Append(@"\t"); break;
                case (byte)'\n': text.Append(@"\n"); break;
                case >= 0x20 and < 0x7F: text.Append((char)b); break;
                default: text.Append(CultureInfo.InvariantCulture, $"\\x{b:X2}"); break;
            }
        }
        return text.ToString();
    }

    private static string Text(object? value) => value switch
    {
        null => "-",
        string s => EscapeControls(s),
        bool b => b ? "true" : "false",
        Hex h => string.Create(CultureInfo.InvariantCulture, $"0x{h.Value:X}"),
        DateTime d => d.ToString(DateFormat, CultureInfo.InvariantCulture),
        Guid g => g.ToString("D"),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable list => string.Join(',', list.Cast<object?>().Select(Text)),
        _ => throw new InvalidOperationException($"no text form for {value.GetType().Name}"),
    };

    // Text output is one value per field or cell, so control characters in file text must not break the line.
    private static string EscapeControls(string s)
    {
        if (!s.Any(char.IsControl)) return s;
        var text = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsControl(c)) text.Append(CultureInfo.InvariantCulture, $"\\x{(int)c:X2}");
            else text.Append(c);
        }
        return text.ToString();
    }

    private static void WriteValue(Utf8JsonWriter json, object? value)
    {
        switch (value)
        {
            case null: json.WriteNullValue(); break;
            case string s: json.WriteStringValue(s); break;
            case bool b: json.WriteBooleanValue(b); break;
            case Hex h: json.WriteNumberValue(h.Value); break;
            case int i: json.WriteNumberValue(i); break;
            case long l: json.WriteNumberValue(l); break;
            case uint u: json.WriteNumberValue(u); break;
            case ushort u: json.WriteNumberValue(u); break;
            case decimal m: json.WriteNumberValue(m); break;
            case DateTime d: json.WriteStringValue(d.ToString(DateFormat, CultureInfo.InvariantCulture)); break;
            case Guid g: json.WriteStringValue(g.ToString("D")); break;
            case IEnumerable list:
                json.WriteStartArray();
                foreach (object? item in list) WriteValue(json, item);
                json.WriteEndArray();
                break;
            default: throw new InvalidOperationException($"no JSON form for {value.GetType().Name}");
        }
    }
}
