namespace TdbIO;

/// <summary>A file read by <see cref="TdbFile.Open(string, TdbReadOptions?)"/>. Each kind of file has its own subclass;
/// use pattern matching to reach it. Documents are read-only (the library never changes them after reading), hold their
/// data in memory and are safe to read from several threads.</summary>
public abstract class TdbDocument
{
    private protected TdbDocument(TdbFileKind kind, IReadOnlyList<string> warnings)
    {
        Kind = kind;
        Warnings = warnings;
    }

    /// <summary>The kind of the file.</summary>
    public TdbFileKind Kind { get; }

    /// <summary>Values that were kept although they differ from what is expected, one line each with the offset or line
    /// number. The lines never contain text from the file.</summary>
    public IReadOnlyList<string> Warnings { get; }
}
