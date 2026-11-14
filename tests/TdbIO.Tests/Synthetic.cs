using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using TdbIO.Internal;

namespace TdbIO.Tests;

/// <summary>Builders of synthetic test input. Every test input is made here or in the other parts of this class; no sample
/// bytes are copied into the tests.</summary>
internal static partial class Synthetic
{
    public static byte[] Le16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, v); return b; }

    public static byte[] Le32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return b; }

    public static byte[] Le32(int v) { var b = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); return b; }

    public static byte[] Le64(ulong v) { var b = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, v); return b; }

    public static byte[] Be16(ushort v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, v); return b; }

    public static byte[] Be32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }

    public static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    public static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    /// <summary>The raw deflate stream of <paramref name="payload"/>.</summary>
    public static byte[] Deflate(byte[] payload)
    {
        var deflated = new MemoryStream();
        using (var deflate = new DeflateStream(deflated, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(payload);
        // Some runtimes write nothing for empty input, but a deflate stream needs a final block: 03 00 is an empty one.
        return deflated.Length == 0 ? [0x03, 0x00] : deflated.ToArray();
    }

    /// <summary>One gzip member as model files store it: header 1F 8B 08 00 with MTIME 0, raw deflate, CRC-32 and size.</summary>
    public static byte[] Gzip(byte[] payload) =>
        Concat([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0x04, 0x0A], Deflate(payload), Le32(Crc32.Compute(payload)), Le32((uint)payload.Length));
}
