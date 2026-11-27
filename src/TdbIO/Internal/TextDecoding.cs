using System.Buffers;
using System.Text;

namespace TdbIO.Internal;

/// <summary>Chooses the encoding of a text and decodes it. For a template or report: UTF-8 after a byte order mark, ASCII,
/// UTF-8, then code page 1251 when most high bytes are letters of that page, else code page 1252. The text values of
/// table rows follow the same order without the byte order mark steps (<see cref="DecodeUnmarked"/>). Bytes are never
/// changed to fit a guess.</summary>
internal static class TextDecoding
{
    public static string Decode(ReadOnlySpan<byte> bytes, Encoding? forced, out string encodingName)
    {
        if (forced != null)
        {
            // A byte order mark of the forced encoding is not text, as with the detected UTF-8 one.
            int start = forced.Preamble.Length > 0 && bytes.StartsWith(forced.Preamble) ? forced.Preamble.Length : 0;
            return DecodeForced(bytes[start..], forced, out encodingName, start);
        }
        if (bytes.StartsWith(Utf8Bom))
        {
            int bad = FirstInvalidUtf8(bytes[3..]);
            if (bad >= 0) throw FormatError.At("text after a UTF-8 byte order mark is not valid UTF-8", bad + 3);
            encodingName = "utf-8";
            return Encoding.UTF8.GetString(bytes[3..]);
        }
        if (bytes.StartsWith(Utf16Le) || bytes.StartsWith(Utf16Be))
            throw new NotSupportedException("UTF-16 text is not supported.");
        return DecodeUnmarked(bytes, null, out encodingName);
    }

    /// <summary>Decodes bytes that carry no byte order mark: as <paramref name="forced"/> when it is given, else ASCII,
    /// UTF-8, then <paramref name="codePage"/> when it is given, else code page 1251 when at least half of the high bytes
    /// are 0xC0 or above, else code page 1252. This is the order of the rule for templates and reports without its mark
    /// steps, and the rule for one text value of a table row, whose file gives the code page (<see cref="RowText"/>).</summary>
    public static string DecodeUnmarked(ReadOnlySpan<byte> bytes, Encoding? forced, out string encodingName, int? codePage = null)
    {
        if (forced != null) return DecodeForced(bytes, forced, out encodingName, 0);

        int high = 0, upper = 0;
        foreach (byte b in bytes)
        {
            if (b < 0x80) continue;
            high++;
            if (b >= 0xC0) upper++;
        }
        if (high == 0)
        {
            encodingName = "ascii";
            return Encoding.ASCII.GetString(bytes);
        }
        if (FirstInvalidUtf8(bytes) < 0)
        {
            encodingName = "utf-8";
            return Encoding.UTF8.GetString(bytes);
        }

        int chosen = codePage ?? CodePageOf(high, upper);
        encodingName = chosen == 1251 ? "cp1251" : "cp1252";
        Encoding encoding = CodePagesEncodingProvider.Instance.GetEncoding(chosen, EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback) ?? throw new InvalidOperationException($"Code page {chosen} is not available.");
        int undefined = FirstUndefined(bytes, encoding);
        if (undefined >= 0)
            throw FormatError.At($"byte 0x{bytes[undefined]:X2} is not defined in {encodingName}", undefined);
        return encoding.GetString(bytes);
    }

    /// <summary>The code page of text that is neither ASCII nor UTF-8 from its bytes from 0x80: 1251 when at least half
    /// of them are 0xC0 or above (the letters of that page), else 1252.</summary>
    public static int CodePageOf(long high, long upper) => upper * 2 >= high ? 1251 : 1252;

    /// <summary>Adds the bytes from 0x80 of <paramref name="value"/> to the counts of <see cref="CodePageOf"/> when the
    /// value is neither ASCII nor UTF-8, the only values a code page is chosen for.</summary>
    public static void CountCodePageBytes(ReadOnlySpan<byte> value, ref long high, ref long upper)
    {
        int valueHigh = 0, valueUpper = 0;
        foreach (byte b in value)
        {
            if (b < 0x80) continue;
            valueHigh++;
            if (b >= 0xC0) valueUpper++;
        }
        if (valueHigh == 0 || FirstInvalidUtf8(value) < 0) return;
        high += valueHigh;
        upper += valueUpper;
    }

    private static string DecodeForced(ReadOnlySpan<byte> bytes, Encoding forced, out string encodingName, long start)
    {
        encodingName = forced.WebName;
        try
        {
            return forced.GetString(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw FormatError.At($"the text is not valid {encodingName}", start + Math.Max(0, e.Index), e);
        }
    }

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16Le => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16Be => [0xFE, 0xFF];

    /// <summary>The offset of the first byte that is not part of well-formed UTF-8, or -1.</summary>
    private static int FirstInvalidUtf8(ReadOnlySpan<byte> bytes)
    {
        int at = 0;
        while (at < bytes.Length)
        {
            if (Rune.DecodeFromUtf8(bytes[at..], out _, out int consumed) != OperationStatus.Done) return at;
            at += consumed;
        }
        return -1;
    }

    // A single-byte code page decodes byte by byte, so each high byte value is checked once on its own.
    private static int FirstUndefined(ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        Span<sbyte> defined = stackalloc sbyte[128]; // 0 unknown, 1 defined, -1 undefined
        defined.Clear();
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b < 0x80) continue;
            ref sbyte state = ref defined[b - 0x80];
            if (state == 0) state = IsDefined(b, encoding) ? (sbyte)1 : (sbyte)-1;
            if (state < 0) return i;
        }
        return -1;
    }

    // .NET maps the gaps of code pages 1251 and 1252 to C1 control characters instead of failing, so those count as undefined.
    private static bool IsDefined(byte b, Encoding encoding)
    {
        try
        {
            string decoded = encoding.GetString([b]);
            return decoded.Length == 1 && decoded[0] is not (>= '\u0080' and <= '\u009F');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
