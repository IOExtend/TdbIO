using System.Globalization;

namespace TdbIO.Internal;

/// <summary>Parses template text:
/// <code>
/// file     := block                      (one root block, then only blanks)
/// block    := IDENT [IDENT] "{" { property | block } "}" ";"
/// property := IDENT "=" value ";"
/// value    := STRING | NUMBER | IDENT | "(" value { "," value } ")"
/// </code>
/// </summary>
internal sealed class TemplateParser : IDisposable
{
    // Guards against hostile input, not properties of the format.
    public const int MaxDepth = 64;

    private readonly string _text;
    private readonly IEnumerator<Token> _tokens;
    private Token _current;
    private Token _next;

    private TemplateParser(string text)
    {
        _text = text;
        _tokens = TemplateLexer.Lex(text).GetEnumerator();
        _current = _tokens.MoveNext() ? _tokens.Current : new Token(TokenKind.End, "", text.Length);
        _next = _tokens.MoveNext() ? _tokens.Current : new Token(TokenKind.End, "", text.Length);
    }

    public void Dispose() => _tokens.Dispose();

    public static TemplateBlock Parse(string text)
    {
        using var parser = new TemplateParser(text);
        var root = parser.Block(0);
        if (parser.Peek(0).Kind != TokenKind.End) throw parser.Error("only blanks may follow the root block", parser.Peek(0));
        return root;
    }

    private Token Peek(int ahead) => ahead switch
    {
        0 => _current,
        1 => _next,
        _ => throw new ArgumentOutOfRangeException(nameof(ahead)),
    };

    private Token Next()
    {
        Token token = _current;
        if (token.Kind != TokenKind.End)
        {
            _current = _next;
            _next = _tokens.MoveNext() ? _tokens.Current : new Token(TokenKind.End, "", _text.Length);
        }
        return token;
    }

    private static bool Is(Token token, char punctuation) =>
        token.Kind == TokenKind.Punctuation && token.Text[0] == punctuation;

    private void Expect(char punctuation)
    {
        Token token = Next();
        if (!Is(token, punctuation))
            throw Error(token.Kind == TokenKind.End ? $"the file ends where '{punctuation}' is expected" : $"'{punctuation}' expected", token);
    }

    private TemplateBlock Block(int depth)
    {
        Token keyword = Next();
        if (keyword.Kind != TokenKind.Identifier)
            throw Error(keyword.Kind == TokenKind.End ? "the file ends where a block is expected" : "a block keyword expected", keyword);
        if (depth > MaxDepth) throw Error("blocks are nested deeper than the 64 limit", keyword);
        string? instance = Peek(0).Kind == TokenKind.Identifier ? Next().Text : null;
        Expect('{');

        var properties = new List<KeyValuePair<string, TemplateValue>>();
        var blocks = new List<TemplateBlock>();
        while (!Is(Peek(0), '}'))
        {
            Token token = Peek(0);
            if (token.Kind == TokenKind.End) throw Error("the file ends inside a block", token);
            if (token.Kind != TokenKind.Identifier) throw Error("a key, a block or '}' expected", token);
            if (Is(Peek(1), '='))
            {
                Next();
                Next();
                properties.Add(new(token.Text, Value(0)));
                Expect(';');
            }
            else
            {
                blocks.Add(Block(depth + 1));
            }
        }
        Next();
        Expect(';');
        return new TemplateBlock(keyword.Text, instance, properties, blocks);
    }

    private TemplateValue Value(int depth)
    {
        Token token = Next();
        switch (token.Kind)
        {
            case TokenKind.String:
                return new(TemplateValueKind.String, token.Text, null, []);
            case TokenKind.Identifier:
                return new(TemplateValueKind.Identifier, token.Text, null, []);
            case TokenKind.Number:
                if (!decimal.TryParse(token.Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out decimal number))
                    throw Error("number is out of range", token);
                return new(TemplateValueKind.Number, token.Text, number, []);
            case TokenKind.End:
                throw Error("the file ends where a value is expected", token);
        }
        if (!Is(token, '(')) throw Error("a value expected", token);
        if (depth > MaxDepth) throw Error("tuples are nested deeper than the 64 limit", token);
        var items = new List<TemplateValue>();
        while (true)
        {
            items.Add(Value(depth + 1));
            Token separator = Next();
            if (Is(separator, ')')) return new(TemplateValueKind.Tuple, "", null, items);
            if (!Is(separator, ','))
                throw Error(separator.Kind == TokenKind.End ? "the file ends inside a tuple" : "',' or ')' expected", separator);
        }
    }

    private InvalidDataException Error(string message, Token token) => TemplateLexer.Error(message, _text, token.Offset);
}
