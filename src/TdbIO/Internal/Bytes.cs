using System.Buffers.Binary;
using System.Text;

namespace TdbIO.Internal;

/// <summary>Bounds-checked reads. Offsets and sizes are <see cref="long"/> so that sums taken from the file cannot
/// overflow.</summary>
internal static class Bytes
{
    /// <summary>Throws unless <paramref name="count"/> bytes from <paramref name="at"/> lie inside
    /// <paramref name="length"/> bytes.</summary>
    public static void Need(long at, long count, long length, string what)
    {
        if (at < 0 || count < 0 || at > length || count > length - at)
            throw FormatError.At($"{what} runs past the end", at);
    }

    public static uint U32(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 4, d.Length, "u32");
        return BinaryPrimitives.ReadUInt32LittleEndian(d[(int)at..]);
    }

    public static int I32(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 4, d.Length, "i32");
        return BinaryPrimitives.ReadInt32LittleEndian(d[(int)at..]);
    }

    public static ushort U16BE(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 2, d.Length, "u16");
        return BinaryPrimitives.ReadUInt16BigEndian(d[(int)at..]);
    }

    public static uint U32BE(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 4, d.Length, "u32");
        return BinaryPrimitives.ReadUInt32BigEndian(d[(int)at..]);
    }

    public static double F64(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 8, d.Length, "f64");
        return BinaryPrimitives.ReadDoubleLittleEndian(d[(int)at..]);
    }

    public static float F32(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 4, d.Length, "f32");
        return BinaryPrimitives.ReadSingleLittleEndian(d[(int)at..]);
    }

    public static Guid Guid(ReadOnlySpan<byte> d, long at)
    {
        Need(at, 16, d.Length, "guid");
        return new Guid(d.Slice((int)at, 16), bigEndian: true);
    }

    /// <summary>Decodes the zero-terminated text value of a row body: the bytes from <paramref name="at"/> up to the first
    /// NUL, or to the end of the field of <paramref name="length"/> bytes. With <paramref name="encoding"/> the value is
    /// read in that encoding; otherwise each value is ASCII, UTF-8, then <paramref name="codePage"/>, the code page chosen
    /// for the file (<see cref="RowText"/>), or code page 1251 or 1252 chosen from the value when it is null
    /// (<see cref="TextDecoding.DecodeUnmarked"/>). A text of a model is single-byte in the code page of the system that
    /// saved it, so a value with bytes from 0x80 is not an error. A byte that the chosen encoding does not define is.</summary>
    public static string Text(ReadOnlySpan<byte> d, long at, int length, Encoding? encoding, int? codePage = null)
    {
        Need(at, length, d.Length, "string");
        var field = d.Slice((int)at, length);
        int nul = field.IndexOf((byte)0);
        if (nul >= 0) field = field[..nul];
        if (field.IsEmpty) return string.Empty;
        try
        {
            return TextDecoding.DecodeUnmarked(field, encoding, out _, codePage);
        }
        // The offsets of TextDecoding count from the value; the offsets of a row count from the body.
        catch (InvalidDataException e) when (e.Data[FormatError.OffsetKey] is long inner)
        {
            throw FormatError.At(FormatError.WithoutOffset(e.Message), at + inner, e.InnerException ?? e);
        }
    }
}
