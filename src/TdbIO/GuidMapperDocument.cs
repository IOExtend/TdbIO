using TdbIO.Internal;

namespace TdbIO;

/// <summary>A <c>guid.mapper</c> file: a header line and pairs of GUIDs.</summary>
public sealed class GuidMapperDocument : TdbDocument
{
    private GuidMapperDocument(ReadOnlyMemory<byte> headerLine, IReadOnlyList<GuidPair> pairs)
        : base(TdbFileKind.GuidMapper, [])
    {
        HeaderLine = headerLine;
        Pairs = pairs;
    }

    /// <summary>The first line without its LF, as raw bytes: it names a model file, and the encoding of names is not
    /// known.</summary>
    public ReadOnlyMemory<byte> HeaderLine { get; }

    /// <summary>The pairs in file order.</summary>
    public IReadOnlyList<GuidPair> Pairs { get; }

    internal static GuidMapperDocument Create(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var (header, pairs) = GuidMapperReader.Read(data);
        return new GuidMapperDocument(header, pairs);
    }
}
