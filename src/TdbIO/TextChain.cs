using TdbIO.Internal;

namespace TdbIO;

/// <summary>One logical text: a head row and the rows it links to, in chain order.</summary>
public sealed class TextChain
{
    private static ReadOnlySpan<byte> SavePrefix => "Save:"u8;

    private readonly byte[] _text;

    internal TextChain(uint headId, uint headTag, uint headField13, IReadOnlyList<uint> rowIds, byte[] text)
    {
        HeadId = headId;
        HeadTag = headTag;
        HeadField13 = headField13;
        RowIds = rowIds;
        _text = text;
        IsSaveStamp = text.AsSpan().StartsWith(SavePrefix);
        if (IsSaveStamp) SavedAt = HiddenText.ParseSavedAt(RevealSaveText());
    }

    /// <summary>The id of the head row.</summary>
    public uint HeadId { get; }

    /// <summary>The raw u32 at offset 9 of the head row. Any value other than 0xFFFFFFFF marks a head; the meaning of the
    /// value is not known.</summary>
    public uint HeadTag { get; }

    /// <summary>The raw u32 at offset 13 of the head row; its meaning is not known.</summary>
    public uint HeadField13 { get; }

    /// <summary>The row ids in chain order, head first.</summary>
    public IReadOnlyList<uint> RowIds { get; }

    /// <summary>The text bytes as stored, not decoded: the encoding of bytes 0x80 and above is not known, so decoding is left
    /// to the caller. For a save stamp these are the stored (hidden) bytes.</summary>
    public ReadOnlyMemory<byte> Text => _text;

    /// <summary>True when the text starts with the ASCII bytes <c>Save:</c>: the rest of such a text is stored hidden and
    /// is read with <see cref="RevealSaveText"/>.</summary>
    public bool IsSaveStamp { get; }

    /// <summary>The date and time in fields 1 and 2 of the revealed save stamp, or null when this is not a save stamp or
    /// the fields do not parse.</summary>
    public DateTime? SavedAt { get; }

    /// <summary>The revealed text of a save stamp: the bytes after <c>Save:</c>, reversed as a whole, with letters shifted
    /// by 13 places. The result is fields separated by <c>;</c>. Fields after the date and time may hold names of people
    /// or machines; do not log them.</summary>
    /// <returns>A new array with the revealed bytes.</returns>
    /// <exception cref="InvalidOperationException">The chain is not a save stamp.</exception>
    public byte[] RevealSaveText() => IsSaveStamp
        ? HiddenText.Reveal(_text.AsSpan(SavePrefix.Length))
        : throw new InvalidOperationException("The text is not a save stamp.");
}
