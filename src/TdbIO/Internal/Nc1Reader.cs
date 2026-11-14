using System.Globalization;
using System.Text;

namespace TdbIO.Internal;

/// <summary>Reads a DSTV NC file: ASCII lines ended by CR LF, an <c>ST</c> header block of fixed layout, then blocks that
/// each start with a line of two upper-case letters, ending with an empty <c>EN</c> block.</summary>
internal static class Nc1Reader
{
    // Guards against hostile input, not a property of the format.
    public const int MaxFileLength = 16 * 1024 * 1024;
    public const int MaxLineLength = 4096;

    // Lines 1 to 26: the code ST, the comment, 20 fields and 4 empty lines. Line 27 is the next block code.
    private const int StLines = 26;
    private const int FieldCount = 20;
    private const int TextFields = 8;

    private readonly record struct Line(string Text, long Offset, int Number);

    public static Nc1Document Read(ReadOnlySpan<byte> data, List<string> warnings)
    {
        if (data.Length > MaxFileLength)
            throw FormatError.At("DSTV file is larger than the 16 MiB limit", MaxFileLength);
        if (!FirstLineIsSt(data)) throw FormatError.At("not a DSTV file: line 1 is not ST", 0, 1);

        var lines = SplitLines(data);
        var (comment, fields) = ReadStBlock(lines, data.Length);
        var blocks = new List<Nc1Block>
        {
            new("ST", 1, lines.Skip(1).Take(StLines - 1).Select(l => l.Text).ToArray(), []),
        };

        int at = StLines;
        while (at < lines.Count)
        {
            Line head = lines[at];
            int end = at + 1;
            while (end < lines.Count && !IsCode(lines[end].Text)) end++;
            var body = lines.Skip(at + 1).Take(end - at - 1).ToArray();
            var raw = body.Select(l => l.Text).ToArray();
            switch (head.Text)
            {
                case "ST":
                    throw Error("an ST block may only start the file", head);
                case "EN":
                    if (body.Length > 0) throw Error("data after the EN block", body[0]);
                    if (end < lines.Count) throw Error("data after the EN block", lines[end]);
                    blocks.Add(new("EN", head.Number, raw, []));
                    break;
                case "AK":
                    blocks.Add(new("AK", head.Number, raw, ReadAk(head, body, warnings)));
                    break;
                case "BO":
                    blocks.Add(new("BO", head.Number, raw, ReadBo(body)));
                    break;
                default:
                    // Other codes are not read; their lines are kept as written.
                    blocks.Add(new(head.Text, head.Number, raw, []));
                    break;
            }
            at = end;
        }
        if (blocks[^1].Code != "EN")
            throw FormatError.At("DSTV file is cut: the last block is not EN", data.Length, lines.Count + 1);

        int? quantity = int.TryParse(fields[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q) ? q : null;
        decimal length = ParseDecimal(fields[8], lines[2 + 8]);
        return new Nc1Document(comment, fields, quantity, length, blocks, warnings);
    }

    private static bool FirstLineIsSt(ReadOnlySpan<byte> data)
    {
        int end = data.IndexOfAny((byte)'\r', (byte)'\n');
        return (end < 0 ? data : data[..end]).SequenceEqual("ST"u8);
    }

    /// <summary>Splits on CR LF only. Every byte is checked: ASCII, no control bytes, no lone CR or LF, and the last line
    /// must end with CR LF.</summary>
    private static List<Line> SplitLines(ReadOnlySpan<byte> data)
    {
        var lines = new List<Line>();
        int start = 0;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            int number = lines.Count + 1;
            if (b == '\r')
            {
                if (i + 1 >= data.Length || data[i + 1] != '\n')
                    throw FormatError.At("CR not followed by LF", i, number);
                lines.Add(new(Encoding.ASCII.GetString(data[start..i]), start, number));
                i++;
                start = i + 1;
                continue;
            }
            if (b == '\n') throw FormatError.At("LF without CR", i, number);
            if (b >= 0x80) throw FormatError.At($"byte 0x{b:X2} is not ASCII", i, number);
            if (b < 0x20 || b == 0x7F) throw FormatError.At($"control byte 0x{b:X2}", i, number);
            if (i - start >= MaxLineLength)
                throw FormatError.At("line is longer than the 4096 character limit", start, number);
        }
        if (start != data.Length)
            throw FormatError.At("DSTV file is cut: the last line does not end with CR LF", start, lines.Count + 1);
        return lines;
    }

    private static (string Comment, string[] Fields) ReadStBlock(List<Line> lines, long length)
    {
        if (lines.Count <= StLines)
            throw FormatError.At("DSTV file ends inside the ST block", length, lines.Count + 1);

        Line second = lines[1];
        if (!second.Text.StartsWith("** ", StringComparison.Ordinal)) throw Error("line 2 does not start with '** '", second);

        var fields = new string[FieldCount];
        for (int n = 1; n <= FieldCount; n++)
        {
            Line line = lines[n + 1];
            if (line.Text.Length == 0) throw Error($"ST field {n} is empty", line);
            if (n <= TextFields)
            {
                if (!line.Text.StartsWith("  ", StringComparison.Ordinal)) throw Error($"ST field {n} does not start with two spaces", line);
                fields[n - 1] = line.Text[2..];
            }
            else
            {
                string value = line.Text.Trim(' ');
                if (!IsDecimal(value)) throw Error($"ST field {n} is not a number", line);
                ParseDecimal(value, line);
                fields[n - 1] = value;
            }
        }
        for (int i = FieldCount + 2; i < StLines; i++)
            if (lines[i].Text.Length != 0) throw Error($"line {i + 1} of the ST block is not empty", lines[i]);
        if (!IsCode(lines[StLines].Text)) throw Error("the ST block is not followed by a block code", lines[StLines]);
        return (second.Text[3..], fields);
    }

    private static Nc1Line[] ReadAk(Line head, Line[] body, List<string> warnings)
    {
        if (body.Length == 0) throw Error("AK block has no lines", head);
        var result = new Nc1Line[body.Length];
        for (int i = 0; i < body.Length; i++)
        {
            string[] tokens = Tokens(body[i]);
            if (i == 0)
            {
                if (tokens.Length != 8 || !IsLabel(tokens[0])) throw Error("the first AK line must hold a label letter and 7 numbers", body[i]);
                result[i] = new Nc1Line(tokens[0][0], Numbers(tokens, 1, body[i]));
            }
            else
            {
                if (tokens.Length > 0 && IsLabel(tokens[0])) throw Error("only the first AK line may hold a label letter", body[i]);
                if (tokens.Length != 7) throw Error("an AK line must hold 7 numbers", body[i]);
                result[i] = new Nc1Line(null, Numbers(tokens, 0, body[i]));
            }
        }
        var first = result[0].Values;
        var last = result[^1].Values;
        if (first[0].Number != last[0].Number || first[1].Number != last[1].Number)
            warnings.Add($"AK block at line {head.Number}: the last point differs from the first, the contour is open");
        return result;
    }

    private static Nc1Line[] ReadBo(Line[] body)
    {
        var result = new Nc1Line[body.Length];
        for (int i = 0; i < body.Length; i++)
        {
            string[] tokens = Tokens(body[i]);
            if (tokens.Length != 4 || !IsLabel(tokens[0])) throw Error("a BO line must hold a label letter and 3 numbers", body[i]);
            result[i] = new Nc1Line(tokens[0][0], Numbers(tokens, 1, body[i]));
        }
        return result;
    }

    private static string[] Tokens(Line line) => line.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static Nc1Token[] Numbers(string[] tokens, int from, Line line)
    {
        var numbers = new Nc1Token[tokens.Length - from];
        for (int i = from; i < tokens.Length; i++)
        {
            string token = tokens[i];
            char? suffix = token.Length > 0 && char.IsAsciiLetterLower(token[^1]) ? token[^1] : null;
            string digits = suffix is null ? token : token[..^1];
            if (!IsDecimal(digits)) throw Error($"token {i + 1} is not a number", line);
            numbers[i - from] = new Nc1Token(token, ParseDecimal(digits, line), suffix);
        }
        return numbers;
    }

    private static bool IsCode(string text) => text.Length == 2 && char.IsAsciiLetterUpper(text[0]) && char.IsAsciiLetterUpper(text[1]);

    private static bool IsLabel(string token) => token.Length == 1 && char.IsAsciiLetterLower(token[0]);

    /// <summary>Matches <c>-?\d+(\.\d+)?</c> with ASCII digits.</summary>
    internal static bool IsDecimal(ReadOnlySpan<char> s)
    {
        int i = 0;
        if (i < s.Length && s[i] == '-') i++;
        int digits = i;
        while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        if (i == digits) return false;
        if (i < s.Length && s[i] == '.')
        {
            int fraction = ++i;
            while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
            if (i == fraction) return false;
        }
        return i == s.Length;
    }

    private static decimal ParseDecimal(string text, Line line) =>
        decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : throw Error("number is out of range", line);

    private static InvalidDataException Error(string message, Line line) => FormatError.At(message, line.Offset, line.Number);
}
