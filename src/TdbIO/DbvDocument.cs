using System.Text;
using TdbIO.Internal;

namespace TdbIO;

/// <summary>A gzip file whose content starts with <c>DBV@</c>: a name, a GUID list, a few lists of numbers and table
/// sections. Raw values whose meaning is not known are not separate properties: the reader checks each of them against
/// the values always seen, reports deviations in <see cref="TdbDocument.Warnings"/>, and <see cref="Payload"/> holds
/// the raw bytes.</summary>
public sealed class DbvDocument : TdbDocument
{
    internal DbvDocument(IReadOnlyList<string> warnings, string name, IReadOnlyList<uint> prologue, IReadOnlyList<uint> listHeader,
        IReadOnlyList<DbvGuidRecord> guidRecords, uint baseOffset, uint currentOffset, uint lastEventOffset,
        IReadOnlyList<DbvC9Record> c9Records, IReadOnlyList<TableSection> sections, ReadOnlyMemory<byte> payload)
        : base(TdbFileKind.Dbv, warnings)
    {
        Name = name;
        Prologue = prologue;
        ListHeader = listHeader;
        GuidRecords = guidRecords;
        BaseOffset = baseOffset;
        CurrentOffset = currentOffset;
        LastEventOffset = lastEventOffset;
        C9Records = c9Records;
        Sections = sections;
        Payload = payload;
    }

    /// <summary>The name stored after the signature, printable ASCII.</summary>
    public string Name { get; }

    /// <summary>The domain identifier of the database (from <see cref="Prologue"/> word 1: 2 for options, 3 for environment, 4 for analysis).</summary>
    public uint DomainId => Prologue[1];

    /// <summary>The generation/version number of the domain file (from <see cref="ListHeader"/> word 3), equals <c>GuidRecords.Count + 1</c>.</summary>
    public uint Generation => ListHeader[3];

    /// <summary>The base byte offset of the domain events: the row enders of the sections lie inside the interval that
    /// starts here and ends at <see cref="CurrentOffset"/>. The raw word is checked to be a multiple of 32.</summary>
    public uint BaseOffset { get; }

    /// <summary>The byte offset after the domain events, which the reader checks to equal
    /// <c>BaseOffset + 32 · K</c> for the length <c>K</c> stored before the lists.</summary>
    public uint CurrentOffset { get; }

    /// <summary>The byte offset of the last domain event, which the reader checks to equal
    /// <c>CurrentOffset − 32</c>.</summary>
    public uint LastEventOffset { get; }

    /// <summary>The six raw numbers after the name.</summary>
    public IReadOnlyList<uint> Prologue { get; }

    /// <summary>The four raw numbers after the list marker. The last one is the number of GUID records plus one.</summary>
    public IReadOnlyList<uint> ListHeader { get; }

    /// <summary>The GUID records in file order.</summary>
    public IReadOnlyList<DbvGuidRecord> GuidRecords { get; }

    /// <summary>The records that start with <c>C9 C0 CE DB</c>.</summary>
    public IReadOnlyList<DbvC9Record> C9Records { get; }

    /// <summary>The table sections, which run to the end of the data. Row bodies are raw.</summary>
    public IReadOnlyList<TableSection> Sections { get; }

    /// <summary>The unpacked bytes of the file.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    internal static DbvDocument Create(byte[] data, Encoding? textEncoding = null) => DbvReader.Read(data, [], textEncoding);
}
