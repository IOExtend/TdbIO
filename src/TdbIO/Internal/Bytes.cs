using System.Buffers.Binary;

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
}
