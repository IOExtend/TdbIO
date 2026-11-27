using System.Globalization;

namespace TdbIO.Internal;

/// <summary>Builds the <see cref="InvalidDataException"/> thrown for malformed data. The message ends with
/// <c> at offset 0x…</c> (and <c> line N</c> for text), and <c>Data["Offset"]</c> holds the offset as a <see cref="long"/>.</summary>
internal static class FormatError
{
    public const string OffsetKey = "Offset";

    public static InvalidDataException At(string message, long offset) => WithData(new(WithOffset(message, offset)), offset);

    public static InvalidDataException At(string message, long offset, Exception innerException) =>
        WithData(new(WithOffset(message, offset), innerException), offset);

    // Text readers also name the 1-based line, after the offset.
    public static InvalidDataException At(string message, long offset, int line) =>
        WithData(new(string.Create(CultureInfo.InvariantCulture, $"{WithOffset(message, offset)} line {line}")), offset);

    private static InvalidDataException WithData(InvalidDataException e, long offset)
    {
        e.Data[OffsetKey] = offset;
        return e;
    }

    /// <summary>The message of an error from <see cref="At(string, long)"/> without its <c> at offset 0x…</c> end, for an
    /// error rethrown with an offset that counts from somewhere else.</summary>
    public static string WithoutOffset(string message)
    {
        int cut = message.LastIndexOf(" at offset 0x", StringComparison.Ordinal);
        return cut >= 0 ? message[..cut] : message;
    }

    private static string WithOffset(string message, long offset) =>
        string.Create(CultureInfo.InvariantCulture, $"{message} at offset 0x{offset:X}");
}
