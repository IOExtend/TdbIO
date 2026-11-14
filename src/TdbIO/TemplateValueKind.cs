namespace TdbIO;

/// <summary>The kind of a <see cref="TemplateValue"/>.</summary>
public enum TemplateValueKind
{
    /// <summary>A quoted string; <see cref="TemplateValue.Text"/> holds it without quotes and escapes.</summary>
    String,

    /// <summary>A number such as <c>4</c>, <c>-2</c> or <c>3.21</c>; <see cref="TemplateValue.Number"/> holds its value.</summary>
    Number,

    /// <summary>A bare word such as <c>TRUE</c> or <c>LEFT</c>.</summary>
    Identifier,

    /// <summary>A list of values in parentheses, such as <c>(4, 0, 0, 0)</c>; <see cref="TemplateValue.Items"/> holds them.</summary>
    Tuple,
}
