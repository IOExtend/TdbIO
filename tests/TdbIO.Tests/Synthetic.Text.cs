namespace TdbIO.Tests;

internal static partial class Synthetic
{
    /// <summary>Made-up header fields 1 to 20 of a DSTV NC file.</summary>
    public static readonly string[] Nc1Fields =
        ["ORDER-1", "A1", "1", "A1", "GRADE-X", "3", "PL12", "B", "100", "50", "12", "12", "12", "0", "4.5", "0.0", "0.0", "0.0", "-1.5", "0.0"];

    /// <summary>The 26 lines of an <c>ST</c> block: code, comment, 20 fields, 4 empty lines.</summary>
    public static List<string> Nc1Header(string[]? fields = null)
    {
        fields ??= Nc1Fields;
        var lines = new List<string> { "ST", "** part.nc1" };
        for (int i = 0; i < 20; i++) lines.Add(i < 8 ? "  " + fields[i] : fields[i].PadLeft(11));
        lines.AddRange(["", "", "", ""]);
        return lines;
    }

    /// <summary>An <c>AK</c> block of a closed 100 x 50 rectangle with the label letter <paramref name="label"/>.</summary>
    public static string[] Nc1Rectangle(char label = 'v') =>
    [
        "AK",
        $"  {label}      0u      0       0       0       0       0       0",
        "        100      0       0       0       0       0       0",
        "        100     50       0       0       0       0       0",
        "          0     50       0       0       0       0       0",
        "          0      0       0       0       0       0       0",
    ];

    /// <summary>Joins lines with CR LF, a CR LF after the last one too, as DSTV files are written.</summary>
    public static byte[] Nc1(IEnumerable<string> lines) => Ascii(string.Concat(lines.Select(l => l + "\r\n")));

    /// <summary>A whole file: header, the given blocks, then <c>EN</c>.</summary>
    public static byte[] Nc1File(params string[][] blocks) => Nc1([.. Nc1Header(), .. blocks.SelectMany(b => b), "EN"]);

    /// <summary>A small template with nesting, an instance name and the value kinds.</summary>
    public const string Template =
        "\r\ntemplate _tmp_0\r\n{\r\n    name = \"Part list\";\r\n    type = TEXTUAL;\r\n    version = 2.5;\r\n" +
        "    margins = (4, 0, -2, 0);\r\n    row _tmp_1\r\n    {\r\n        height = 1;\r\n" +
        "        valuefield _tmp_2\r\n        {\r\n            expr = \"Value(\\\"A\\\")\";\r\n        };\r\n" +
        "        text\r\n        {\r\n            string = \"a\";\r\n        };\r\n    };\r\n};\r\n";
}
