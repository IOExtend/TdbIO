using System.Text;

namespace TdbIO.Internal;

/// <summary>The 49-byte header of model containers (<c>.db1</c>, <c>.db2</c>): <c>Xsteel</c>, a type byte, a space, the
/// version <c>d.dd</c>, a space and the model GUID as 36 lower-case characters.</summary>
internal static class XsteelHeader
{
    public const int Length = 49;
    public const byte Db1Type = 0x85;
    public const byte Db2Type = 0x80;
    private const byte DrawingType = 0x20;

    public static (byte Type, string Version, Guid ModelGuid) Read(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 7 || !payload.StartsWith("Xsteel"u8)) throw FormatError.At("not a model container", 0);
        byte type = payload[6];
        // Drawings share the magic but carry no model GUID; they are recognised, not read.
        if (type == DrawingType) throw new NotSupportedException("drawing files are recognised but not supported");
        if (type != Db1Type && type != Db2Type) throw FormatError.At($"Xsteel file type 0x{type:X2} is not known", 6);
        Bytes.Need(0, Length, payload.Length, "Xsteel header");
        if (payload[7] != (byte)' ') throw FormatError.At("not a model container: no space after the type", 7);
        if (!IsDigit(payload[8]) || payload[9] != (byte)'.' || !IsDigit(payload[10]) || !IsDigit(payload[11]))
            throw FormatError.At("not a model container: the version is not d.dd", 8);
        if (payload[12] != (byte)' ') throw FormatError.At("not a model container: no space after the version", 12);
        if (!TryParseLowerGuid(payload.Slice(13, 36), out Guid model))
            throw FormatError.At("not a model container: the model GUID is not 36 lower-case characters", 13);
        return (type, Encoding.ASCII.GetString(payload.Slice(8, 4)), model);
    }

    /// <summary>Parses a GUID written as 8-4-4-4-12 lower-case hex digits; anything else (upper case, braces, other
    /// lengths) fails.</summary>
    public static bool TryParseLowerGuid(ReadOnlySpan<byte> text, out Guid guid)
    {
        guid = default;
        if (text.Length != 36) return false;
        Span<char> chars = stackalloc char[36];
        for (int i = 0; i < 36; i++)
        {
            byte c = text[i];
            bool dash = i is 8 or 13 or 18 or 23;
            if (dash ? c != (byte)'-' : !(IsDigit(c) || c is >= (byte)'a' and <= (byte)'f')) return false;
            chars[i] = (char)c;
        }
        return Guid.TryParseExact(chars, "D", out guid);
    }

    private static bool IsDigit(byte c) => c is >= (byte)'0' and <= (byte)'9';
}
