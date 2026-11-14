using System.Text;

namespace TdbIO.Internal;

internal enum TokenKind
{
    Identifier,
    Number,
    String,
    Punctuation,
    End,
}

/// <summary>One token; <see cref="Text"/> is the unescaped content for strings. <see cref="Offset"/> is the character
/// offset of its first character.</summary>
internal readonly record struct Token(TokenKind Kind, string Text, int Offset);

/// <summary>Splits template text into identifiers, numbers, strings and punctuation. Blanks are space, CR and LF only;
/// any other character outside a string is an error.</summary>
internal static class TemplateLexer
{
    public static IEnumerable<Token> Lex(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            int start = i;
            if (c is ' ' or '\r' or '\n')
            {
                i++;
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                while (++i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_')) { }
                yield return new(TokenKind.Identifier, text[start..i], start);
            }
            else if (c == '-' || char.IsAsciiDigit(c))
            {
                i = Number(text, i);
                yield return new(TokenKind.Number, text[start..i], start);
            }
            else if (c == '"')
            {
                (string value, i) = QuotedString(text, i);
                yield return new(TokenKind.String, value, start);
            }
            else if (c is '{' or '}' or '(' or ')' or ';' or '=' or ',')
            {
                yield return new(TokenKind.Punctuation, c.ToString(), start);
                i++;
            }
            else
            {
                throw Error($"character U+{(int)c:X4} is not allowed outside a string", text, i);
            }
        }
        yield return new(TokenKind.End, "", text.Length);
    }

    // -?\d+(\.\d+)? ; returns the offset after the number.
    private static int Number(string text, int i)
    {
        int start = i;
        if (text[i] == '-') i++;
        int digits = i;
        while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
        if (i == digits) throw Error("a minus sign must be followed by a digit", text, start);
        if (i < text.Length && text[i] == '.')
        {
            int fraction = ++i;
            while (i < text.Length && char.IsAsciiDigit(text[i])) i++;
            if (i == fraction) throw Error("a decimal point must be followed by a digit", text, fraction - 1);
        }
        return i;
    }

    // Only \" \n \r \t are escapes; there is no \\. A line break or control character inside a string is an error.
    private static (string Value, int End) QuotedString(string text, int start)
    {
        var value = new StringBuilder();
        int i = start + 1;
        while (true)
        {
            if (i >= text.Length) throw Error("string is not closed", text, start);
            char c = text[i];
            if (c == '"') return (value.ToString(), i + 1);
            if (c == '\\')
            {
                if (i + 1 >= text.Length) throw Error("string is not closed", text, start);
                value.Append(text[i + 1] switch
                {
                    '"' => '"',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => throw Error("unknown escape in a string", text, i),
                });
                i += 2;
                continue;
            }
            if (c is '\r' or '\n') throw Error("line break inside a string", text, i);
            if (char.IsControl(c)) throw Error($"control character U+{(int)c:X4} inside a string", text, i);
            value.Append(c);
            i++;
        }
    }

    public static InvalidDataException Error(string message, string text, int offset) =>
        FormatError.At(message, offset, LineAt(text, offset));

    /// <summary>The 1-based line of a character offset; lines end with LF.</summary>
    public static int LineAt(string text, int offset) => text.AsSpan(0, Math.Min(offset, text.Length)).Count('\n') + 1;
}
