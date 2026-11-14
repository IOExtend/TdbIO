namespace TdbIO;

/// <summary>The value of one property of a <see cref="TemplateBlock"/>. Expressions inside strings are not
/// evaluated.</summary>
public sealed class TemplateValue
{
    internal TemplateValue(TemplateValueKind kind, string text, decimal? number, IReadOnlyList<TemplateValue> items)
    {
        Kind = kind;
        Text = text;
        Number = number;
        Items = items;
    }

    /// <summary>The kind of the value.</summary>
    public TemplateValueKind Kind { get; }

    /// <summary>The string without quotes and with escapes resolved, the number or the identifier as written; empty for a
    /// tuple.</summary>
    public string Text { get; }

    /// <summary>The value of a <see cref="TemplateValueKind.Number"/>; null for the other kinds.</summary>
    public decimal? Number { get; }

    /// <summary>The members of a <see cref="TemplateValueKind.Tuple"/> in order; empty for the other kinds.</summary>
    public IReadOnlyList<TemplateValue> Items { get; }
}
