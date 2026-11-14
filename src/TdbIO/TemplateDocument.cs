using System.Text;
using TdbIO.Internal;

namespace TdbIO;

/// <summary>A template (<c>.tpl</c>) or report template (<c>.rpt</c>): one root block of nested blocks and properties.
/// The grammar is read in general; no keyword or key is known to the reader.</summary>
public sealed class TemplateDocument : TdbDocument
{
    internal TemplateDocument(string encodingName, TemplateBlock root)
        : base(TdbFileKind.Template, [])
    {
        EncodingName = encodingName;
        Root = root;
    }

    /// <summary>The encoding used to read the bytes: <c>ascii</c>, <c>utf-8</c>, <c>cp1251</c>, <c>cp1252</c>, or the
    /// <see cref="Encoding.WebName"/> of <see cref="TdbReadOptions.TextEncoding"/> when it was set.</summary>
    public string EncodingName { get; }

    /// <summary>The root block, usually with the keyword <c>template</c>.</summary>
    public TemplateBlock Root { get; }

    internal static TemplateDocument Create(byte[] data, Encoding? forced)
    {
        if (data.Length > TemplateParser.MaxLength)
            throw FormatError.At("template is larger than the 16 MiB limit", TemplateParser.MaxLength);
        string text = TextDecoding.Decode(data, forced, out string encodingName);
        return new TemplateDocument(encodingName, TemplateParser.Parse(text));
    }
}
