using TdbIO.Internal;

namespace TdbIO;

/// <summary>Entry point: tells the kind of a file and opens it as a <see cref="TdbDocument"/>.</summary>
public static class TdbFile
{
    // Detection never needs more: gzip signatures lie in the first inflated bytes, text signatures after a few blank lines.
    private const int HeadLength = 64 * 1024;

    // The size limit of .nc1, .tpl, .rpt and .xsr files.
    internal const int TextMaxLength = 16 * 1024 * 1024;

    /// <summary>Tells the kind of a file from its first bytes (at most 64 KiB) and, for reports, its file name. Never throws
    /// for the content: data that is short, cut or not recognised gives <see cref="TdbFileKind.Unknown"/>.</summary>
    /// <param name="path">The file to examine.</param>
    /// <returns>The kind of the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the file is denied.</exception>
    public static TdbFileKind DetectKind(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return DetectKind(file, Path.GetFileName(path));
    }

    /// <summary>Tells the kind of the data from its first bytes (at most 64 KiB, read from the current position) and, for
    /// reports, the file name. A seekable stream is moved back to where it was; the stream is not disposed. Never throws for
    /// the content: data that is short, cut or not recognised gives <see cref="TdbFileKind.Unknown"/>.</summary>
    /// <param name="stream">A readable stream.</param>
    /// <param name="fileName">The file name, used only to recognise reports (<c>.xsr</c>), which have no signature.</param>
    /// <returns>The kind of the data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
    /// <exception cref="IOException">The stream cannot be read.</exception>
    public static TdbFileKind DetectKind(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream is not readable.", nameof(stream));
        long? start = stream.CanSeek ? stream.Position : null;
        var head = new byte[HeadLength];
        int length = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        if (start is long position) stream.Position = position;
        return KindDetector.FromRaw(head.AsSpan(0, length), fileName);
    }

    /// <summary>Opens a file and reads it into the document of its kind.</summary>
    /// <param name="path">The file to open.</param>
    /// <param name="options">Read options; null for the defaults.</param>
    /// <returns>The document; its type depends on <see cref="TdbDocument.Kind"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
    /// <exception cref="InvalidDataException">The file is malformed, over a limit or not recognised.</exception>
    /// <exception cref="NotSupportedException">The file is recognised but not supported.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the file is denied.</exception>
    public static TdbDocument Open(string path, TdbReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(file, Path.GetFileName(path), options ?? TdbReadOptions.Default);
    }

    /// <summary>Reads the data from the current position to the end into the document of its kind. The stream need not be
    /// seekable and is not disposed.</summary>
    /// <param name="stream">A readable stream.</param>
    /// <param name="fileName">The file name, used only to recognise reports (<c>.xsr</c>), which have no signature.</param>
    /// <param name="options">Read options; null for the defaults.</param>
    /// <returns>The document; its type depends on <see cref="TdbDocument.Kind"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
    /// <exception cref="InvalidDataException">The data is malformed, over a limit or not recognised.</exception>
    /// <exception cref="NotSupportedException">The data is recognised but not supported.</exception>
    /// <exception cref="IOException">The stream cannot be read.</exception>
    public static TdbDocument Open(Stream stream, string? fileName = null, TdbReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("The stream is not readable.", nameof(stream));
        return Read(stream, fileName, options ?? TdbReadOptions.Default);
    }

    private static TdbDocument Read(Stream stream, string? fileName, TdbReadOptions options)
    {
        long cap = options.MaxUnpackedLength;
        // The kind is known from the head, so the rest is read only when it is needed and only up to the cap of that kind.
        var buffer = new byte[HeadLength];
        int n = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        ReadOnlySpan<byte> head = buffer.AsSpan(0, n);
        if (Gzip.IsGzip(head))
        {
            byte[] payload = Gzip.Unpack(InputReader.ReadAll(stream, cap, head, "MaxUnpackedLength"), cap);
            return KindDetector.FromUnpacked(payload) switch
            {
                TdbFileKind.Db1 => Db1Document.Create(payload),
                TdbFileKind.Db2 => Db2Document.Create(payload),
                TdbFileKind.Drawing => throw new NotSupportedException("drawing files are recognised but not supported"),
                TdbFileKind.IdRanges => IdRangeDocument.Create(payload),
                TdbFileKind.GuidMapper => GuidMapperDocument.Create(payload),
                TdbFileKind.Dbv => DbvDocument.Create(payload),
                _ => throw NotRecognised(),
            };
        }
        return KindDetector.FromRaw(head, fileName) switch
        {
            // Only the header of history.db is read, so the database itself is never loaded.
            TdbFileKind.History => HistoryDocument.Create(head, n + InputReader.CountRest(stream, cap, n)),
            TdbFileKind.Nc1 => Nc1Document.Create(ReadText(stream, head, cap)),
            TdbFileKind.Template => TemplateDocument.Create(ReadText(stream, head, cap), options.TextEncoding),
            TdbFileKind.Report => ReportDocument.Create(ReadText(stream, head, cap), options.TextEncoding),
            _ => throw NotRecognised(),
        };
    }

    // Text formats have their own smaller limit; reading stops there instead of after MaxUnpackedLength bytes.
    private static byte[] ReadText(Stream stream, ReadOnlySpan<byte> head, long cap) =>
        cap > TextMaxLength
            ? InputReader.ReadAll(stream, TextMaxLength, head, "the 16 MiB limit of text files")
            : InputReader.ReadAll(stream, cap, head, "MaxUnpackedLength");

    private static InvalidDataException NotRecognised() => FormatError.At("not a recognised Tekla file", 0);
}
