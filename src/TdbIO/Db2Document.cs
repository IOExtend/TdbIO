using TdbIO.Internal;

namespace TdbIO;

/// <summary>A model records file (<c>.db2</c>, also <c>xslib.db2</c> and their <c>.bak</c> copies): the header and the
/// records up to the end of the file.</summary>
public sealed class Db2Document : TdbDocument
{
    private readonly byte[] _payload;

    private Db2Document(byte[] payload, string version, Guid modelGuid, IReadOnlyList<Db2Record> records)
        : base(TdbFileKind.Db2, [])
    {
        _payload = payload;
        Version = version;
        ModelGuid = modelGuid;
        Records = records;
    }

    /// <summary>The version text of the header (bytes 8 to 11), four characters <c>d.dd</c>.</summary>
    public string Version { get; }

    /// <summary>The model GUID of the header (bytes 13 to 48).</summary>
    public Guid ModelGuid { get; }

    /// <summary>The records in file order; they end exactly at the end of the file.</summary>
    public IReadOnlyList<Db2Record> Records { get; }

    /// <summary>The whole unpacked file, header included.</summary>
    public ReadOnlyMemory<byte> Payload => _payload;

    internal static Db2Document Create(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var (type, version, modelGuid) = XsteelHeader.Read(payload);
        if (type != XsteelHeader.Db2Type) throw FormatError.At($"Xsteel file type 0x{type:X2} is not a records file", 6);
        return new Db2Document(payload, version, modelGuid, Db2Reader.Read(payload));
    }
}
