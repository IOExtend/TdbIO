namespace TdbIO;

/// <summary>One block of a template: <c>keyword [instance] { properties and blocks };</c>.</summary>
public sealed class TemplateBlock
{
    internal TemplateBlock(string keyword, string? instance, IReadOnlyList<KeyValuePair<string, TemplateValue>> properties,
        IReadOnlyList<TemplateBlock> blocks)
    {
        Keyword = keyword;
        Instance = instance;
        Properties = properties;
        Blocks = blocks;
    }

    /// <summary>The first word of the block, such as <c>template</c>, <c>row</c> or <c>valuefield</c>.</summary>
    public string Keyword { get; }

    /// <summary>The optional second word of the block, or null.</summary>
    public string? Instance { get; }

    /// <summary>The properties (<c>key = value;</c>) in file order. A key that is repeated is kept every time.</summary>
    public IReadOnlyList<KeyValuePair<string, TemplateValue>> Properties { get; }

    /// <summary>The nested blocks in file order.</summary>
    public IReadOnlyList<TemplateBlock> Blocks { get; }

    /// <summary>The value of the first property with this key (ordinal comparison), or null.</summary>
    /// <param name="key">The property key, as written in the file.</param>
    /// <returns>The value, or null when the block has no such property.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public TemplateValue? GetValue(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        foreach (var property in Properties)
            if (string.Equals(property.Key, key, StringComparison.Ordinal)) return property.Value;
        return null;
    }
}
