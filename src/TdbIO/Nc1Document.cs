using TdbIO.Internal;

namespace TdbIO;

/// <summary>A DSTV NC file (<c>.nc1</c>): one part, its <c>ST</c> header block, its <c>AK</c> blocks and its
/// <c>BO</c> blocks. Only ASCII text with CR LF line breaks is read.</summary>
public sealed class Nc1Document : TdbDocument
{
    internal Nc1Document(string comment, IReadOnlyList<string> fields, int? quantity, decimal length,
        IReadOnlyList<Nc1Block> blocks, IReadOnlyList<string> warnings)
        : base(TdbFileKind.Nc1, warnings)
    {
        Comment = comment;
        Fields = fields;
        Quantity = quantity;
        Length = length;
        Blocks = blocks;
    }

    /// <summary>Line 2 of the file after <c>** </c>; usually the file name.</summary>
    public string Comment { get; }

    /// <summary>The 20 header fields of the <c>ST</c> block, lines 3 to 22: fields 1 to 8 without their two leading
    /// spaces, fields 9 to 20 (numbers) without padding.</summary>
    public IReadOnlyList<string> Fields { get; }

    /// <summary>Field 4: the part mark.</summary>
    public string Mark => Fields[3];

    /// <summary>Field 5: the material grade.</summary>
    public string Material => Fields[4];

    /// <summary>Field 6: the quantity, or null when the field is not an integer.</summary>
    public int? Quantity { get; }

    /// <summary>Field 7: the profile, as written. When field 8 is <c>B</c>, it may hold a short form of the
    /// profile name.</summary>
    public string Profile => Fields[6];

    /// <summary>Field 9 as the file gives it: usually the length in millimetres.</summary>
    public decimal Length { get; }

    /// <summary>All blocks in file order, from <c>ST</c> to <c>EN</c>.</summary>
    public IReadOnlyList<Nc1Block> Blocks { get; }

    internal static Nc1Document Create(byte[] data) => Nc1Reader.Read(data, []);
}
