using TdbIO.Internal;

namespace TdbIO;

/// <summary>A model database (<c>.db1</c>, also <c>xslib.db1</c> and their <c>.bak</c> copies): the header, the id map
/// and the unpacked bytes. Text sections and table sections are read on request.</summary>
public sealed partial class Db1Document : TdbDocument
{
    private readonly byte[] _payload;

    private Db1Document(byte[] payload, string version, Guid modelGuid, IdMap? idMap, IReadOnlyList<string> warnings)
        : base(TdbFileKind.Db1, warnings)
    {
        _payload = payload;
        Version = version;
        ModelGuid = modelGuid;
        IdMapField65 = idMap?.Field65;
        IdMapField73 = idMap?.Field73;
        IdMap = idMap?.Entries;
    }

    /// <summary>The version text of the header (bytes 8 to 11), four characters <c>d.dd</c>.</summary>
    public string Version { get; }

    /// <summary>The model GUID of the header (bytes 13 to 48).</summary>
    public Guid ModelGuid { get; }

    /// <summary>The raw u32 at offset 65, or null when the id map is not read. Values other than 0x0D and 0x10D give a
    /// warning; the meaning of bit 0x100 is not known.</summary>
    public uint? IdMapField65 { get; }

    /// <summary>The raw u32 at offset 73, or null when the id map is not read. Its meaning is not known.</summary>
    public uint? IdMapField73 { get; }

    /// <summary>The id map entries in file order (never sorted), or null with a warning when the header variant before the
    /// map is not known.</summary>
    public IReadOnlyList<IdMapEntry>? IdMap { get; }

    /// <summary>The whole unpacked file, header included. Offsets in this library refer to it.</summary>
    public ReadOnlyMemory<byte> Payload => _payload;

    /// <summary>Reads every section of 54-byte text rows and joins the rows into chains. The sections are read again on
    /// each call.</summary>
    /// <returns>The text sections in file order.</returns>
    /// <exception cref="InvalidDataException">A text section is malformed: a bad marker or closer, a repeated id, a link to a
    /// missing id, a cycle, a row reached from two chains or a last row without NUL.</exception>
    public IReadOnlyList<TextSection> ReadTextSections() => TextSectionReader.ReadAll(_payload);

    internal static Db1Document Create(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var (type, version, modelGuid) = XsteelHeader.Read(payload);
        if (type != XsteelHeader.Db1Type) throw FormatError.At($"Xsteel file type 0x{type:X2} is not a model database", 6);
        var warnings = new List<string>();
        var idMap = IdMapReader.Read(payload, warnings);
        return new Db1Document(payload, version, modelGuid, idMap, warnings);
    }
}
