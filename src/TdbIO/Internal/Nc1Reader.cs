using System.Globalization;
using System.Text;

namespace TdbIO.Internal;

/// <summary>Reads a DSTV NC file: ASCII lines ended by CR LF, an <c>ST</c> header block of fixed layout, then blocks that
/// each start with a line of two upper-case letters, ending with an empty <c>EN</c> block.</summary>
internal static class Nc1Reader
{
    public const int MaxLineLength = 4096;

    // Lines 1 to 26: the code ST, the comment, 20 fields and 4 empty lines. Line 27 is the next block code.
    private const int StLines = 26;
    private const int FieldCount = 20;
    private const int TextFields = 8;

    private readonly record struct Line(string Text, long Offset, int Number);

    public static Nc1Document Read(ReadOnlySpan<byte> data, List<string> warnings)
    {
        if (data.Length > TdbFile.TextMaxLength)
            throw FormatError.At("DSTV file is larger than the 16 MiB limit", TdbFile.TextMaxLength);
        if (!FirstLineIsSt(data)) throw FormatError.At("not a DSTV file: line 1 is not ST", 0, 1);

        var reader = new LineReader(data);
        reader.ReadLine("DSTV file ends inside the ST block", data.Length);

        var (comment, fields, length, stRaw) = ReadStBlock(ref reader, data.Length);
        var blocks = new List<Nc1Block>
        {
            new("ST", 1, stRaw, []),
        };

        Line head = reader.ReadLine("DSTV file ends inside the ST block", data.Length);
        if (!IsCode(head.Text))
            throw Error("the ST block is not followed by a block code", head);

        while (head.Text is not null)
        {
            if (head.Text == "ST")
                throw Error("an ST block may only start the file", head);

            var raw = new List<string>();
            var lines = new List<Nc1Line>();
            Line nextHead = default;

            while (reader.TryReadLine(out Line line))
            {
                if (IsCode(line.Text))
                {
                    nextHead = line;
                    break;
                }

                switch (head.Text)
                {
                    case "EN":
                        throw Error("data after the EN block", line);
                    case "AK":
                        lines.Add(ValidateAndParseAkLine(line, lines.Count));
                        break;
                    case "BO":
                        lines.Add(ValidateAndParseBoLine(line));
                        break;
                }
                raw.Add(line.Text);
            }

            if (head.Text == "EN")
            {
                if (nextHead.Text is not null)
                    throw Error("data after the EN block", nextHead);
                blocks.Add(new("EN", head.Number, raw.ToArray(), []));
            }
            else if (head.Text == "AK")
            {
                if (lines.Count == 0) throw Error("AK block has no lines", head);
                var first = lines[0].Values;
                var last = lines[^1].Values;
                if (first[0].Number != last[0].Number || first[1].Number != last[1].Number)
                    warnings.Add($"AK block at line {head.Number}: the last point differs from the first, the contour is open");
                blocks.Add(new("AK", head.Number, raw.ToArray(), lines.ToArray()));
            }
            else if (head.Text == "BO")
            {
                blocks.Add(new("BO", head.Number, raw.ToArray(), lines.ToArray()));
            }
            else
            {
                blocks.Add(new(head.Text, head.Number, raw.ToArray(), []));
            }

            head = nextHead;
        }

        if (blocks[^1].Code != "EN")
            throw FormatError.At("DSTV file is cut: the last block is not EN", data.Length, reader.LineNumber + 1);

        int? quantity = int.TryParse(fields[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q) ? q : null;
        return new Nc1Document(comment, fields, quantity, length, blocks, warnings);
    }

    private static bool FirstLineIsSt(ReadOnlySpan<byte> data)
    {
        int end = data.IndexOfAny((byte)'\r', (byte)'\n');
        return (end < 0 ? data : data[..end]).SequenceEqual("ST"u8);
    }

    private ref struct LineReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;
        private int _lineNumber;

        public LineReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
            _lineNumber = 0;
        }

        public int LineNumber => _lineNumber;

        public bool TryReadLine(out Line line)
        {
            if (_position >= _data.Length)
            {
                line = default;
                return false;
            }

            int start = _position;
            int number = ++_lineNumber;

            for (int i = start; i < _data.Length; i++)
            {
                byte b = _data[i];
                if (b == '\r')
                {
                    if (i + 1 >= _data.Length || _data[i + 1] != '\n')
                        throw FormatError.At("CR not followed by LF", i, number);
                    line = new Line(Encoding.ASCII.GetString(_data[start..i]), start, number);
                    _position = i + 2;
                    return true;
                }
                if (b == '\n') throw FormatError.At("LF without CR", i, number);
                if (b >= 0x80) throw FormatError.At($"byte 0x{b:X2} is not ASCII", i, number);
                if (b < 0x20 || b == 0x7F) throw FormatError.At($"control byte 0x{b:X2}", i, number);
                if (i - start >= MaxLineLength)
                    throw FormatError.At("line is longer than the 4096 character limit", start, number);
            }

            throw FormatError.At("DSTV file is cut: the last line does not end with CR LF", start, number);
        }

        public Line ReadLine(string eofMessage, long eofOffset)
        {
            if (!TryReadLine(out Line line))
                throw FormatError.At(eofMessage, eofOffset, _lineNumber + 1);
            return line;
        }
    }

    private static (string Comment, string[] Fields, decimal Length, string[] RawLines) ReadStBlock(ref LineReader reader, long length)
    {
        var rawLines = new string[StLines - 1];

        Line second = reader.ReadLine("DSTV file ends inside the ST block", length);
        if (!second.Text.StartsWith("** ", StringComparison.Ordinal)) throw Error("line 2 does not start with '** '", second);
        rawLines[0] = second.Text;

        var fields = new string[FieldCount];
        decimal partLength = 0;
        for (int n = 1; n <= FieldCount; n++)
        {
            Line line = reader.ReadLine("DSTV file ends inside the ST block", length);
            rawLines[n] = line.Text;
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
                decimal parsed = ParseDecimal(value, line);
                if (n == 9) partLength = parsed;
                fields[n - 1] = value;
            }
        }
        for (int i = FieldCount + 1; i < StLines - 1; i++)
        {
            Line line = reader.ReadLine("DSTV file ends inside the ST block", length);
            rawLines[i] = line.Text;
            if (line.Text.Length != 0) throw Error($"line {line.Number} of the ST block is not empty", line);
        }
        return (second.Text[3..], fields, partLength, rawLines);
    }

    private static Nc1Line ValidateAndParseAkLine(Line line, int index)
    {
        string[] tokens = Tokens(line);
        if (index == 0)
        {
            if (tokens.Length != 8 || !IsLabel(tokens[0])) throw Error("the first AK line must hold a label letter and 7 numbers", line);
            return new Nc1Line(tokens[0][0], Numbers(tokens, 1, line));
        }
        else
        {
            if (tokens.Length > 0 && IsLabel(tokens[0])) throw Error("only the first AK line may hold a label letter", line);
            if (tokens.Length != 7) throw Error("an AK line must hold 7 numbers", line);
            return new Nc1Line(null, Numbers(tokens, 0, line));
        }
    }

    private static Nc1Line ValidateAndParseBoLine(Line line)
    {
        string[] tokens = Tokens(line);
        if (tokens.Length != 4 || !IsLabel(tokens[0])) throw Error("a BO line must hold a label letter and 3 numbers", line);
        return new Nc1Line(tokens[0][0], Numbers(tokens, 1, line));
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
