using System.Globalization;

namespace TdbIO.Internal;

/// <summary>The text of save stamps: the bytes after <c>Save:</c> are stored reversed (as a whole) and with letters
/// shifted by 13 places.</summary>
internal static class HiddenText
{
    public static byte[] Reveal(ReadOnlySpan<byte> payload)
    {
        var decoded = payload.ToArray();
        Array.Reverse(decoded);
        for (int i = 0; i < decoded.Length; i++) decoded[i] = Rot13(decoded[i]);
        return decoded;
    }

    /// <summary>Fields 1 and 2 of the revealed text (<c>dd.MM.yyyy;HH:mm:ss;…</c>) as a date, or null when they do not
    /// parse.</summary>
    public static DateTime? ParseSavedAt(ReadOnlySpan<byte> decoded)
    {
        int first = decoded.IndexOf((byte)';');
        if (first < 0) return null;
        int second = decoded[(first + 1)..].IndexOf((byte)';');
        ReadOnlySpan<byte> time = second < 0 ? decoded[(first + 1)..] : decoded.Slice(first + 1, second);
        ReadOnlySpan<byte> date = decoded[..first];
        if (date.Length != 10 || time.Length != 8) return null;
        Span<char> text = stackalloc char[19];
        for (int i = 0; i < 10; i++) text[i] = (char)date[i];
        text[10] = ' ';
        for (int i = 0; i < 8; i++) text[11 + i] = (char)time[i];
        return DateTime.TryParseExact(text, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;
    }

    private static byte Rot13(byte c) => c switch
    {
        >= (byte)'A' and <= (byte)'Z' => (byte)('A' + (c - 'A' + 13) % 26),
        >= (byte)'a' and <= (byte)'z' => (byte)('a' + (c - 'a' + 13) % 26),
        _ => c,
    };
}
