using System.Text;
using TdbIO.Internal;

namespace TdbIO;

/// <summary>A gzip file whose content starts with <c>DBV@</c>: a name, a GUID list, a few lists of numbers and table
/// sections. Most numbers have no known meaning and are given raw; values that differ from the ones always seen are kept
/// and reported in <see cref="TdbDocument.Warnings"/>.</summary>
public sealed class DbvDocument : TdbDocument
{
    internal DbvDocument(IReadOnlyList<string> warnings, string name, IReadOnlyList<uint> prologue, IReadOnlyList<uint> listHeader,
        IReadOnlyList<DbvGuidRecord> guidRecords, uint b, uint c, IReadOnlyList<uint> l1, IReadOnlyList<int> l2, IReadOnlyList<uint> t,
        IReadOnlyList<DbvC9Record> c9Records, IReadOnlyList<TableSection> sections, ReadOnlyMemory<byte> payload)
        : base(TdbFileKind.Dbv, warnings)
    {
        Name = name;
        Prologue = prologue;
        ListHeader = listHeader;
        GuidRecords = guidRecords;
        B = b;
        C = c;
        L1 = l1;
        L2 = l2;
        T = t;
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

    /// <summary>The base byte offset of domain events (alias for <see cref="B"/>).</summary>
    public uint BaseOffset => B;

    /// <summary>The domain event level (alias for <see cref="C"/>; expected to be 5).</summary>
    public uint EventLevel => C;

    /// <summary>The current byte offset after domain events (from <see cref="T"/> word 1; equals <c>BaseOffset + 32 * L1.Count</c>).</summary>
    public uint CurrentOffset => T[1];

    /// <summary>The byte offset of the last domain event (from <see cref="T"/> word 4; equals <c>CurrentOffset - 32</c>).</summary>
    public uint LastEventOffset => T[4];

    /// <summary>The six raw numbers after the name.</summary>
    public IReadOnlyList<uint> Prologue { get; }

    /// <summary>The four raw numbers after the list marker. The last one is the number of GUID records plus one.</summary>
    public IReadOnlyList<uint> ListHeader { get; }

    /// <summary>The GUID records in file order.</summary>
    public IReadOnlyList<DbvGuidRecord> GuidRecords { get; }

    /// <summary>The raw number after the GUID records.</summary>
    public uint B { get; }

    /// <summary>The raw number after <see cref="B"/>.</summary>
    public uint C { get; }

    /// <summary>The first list of raw numbers; its length is stored before it.</summary>
    public IReadOnlyList<uint> L1 { get; }

    /// <summary>The second list of raw numbers, signed, as long as <see cref="L1"/>.</summary>
    public IReadOnlyList<int> L2 { get; }

    /// <summary>The six raw numbers after the lists. The first one equals the last number of <see cref="ListHeader"/>;
    /// the last one is the number of <see cref="C9Records"/>.</summary>
    public IReadOnlyList<uint> T { get; }

    /// <summary>The records that start with <c>C9 C0 CE DB</c>.</summary>
    public IReadOnlyList<DbvC9Record> C9Records { get; }

    /// <summary>The table sections, which run to the end of the data. Row bodies are raw.</summary>
    public IReadOnlyList<TableSection> Sections { get; }

    /// <summary>The unpacked bytes of the file.</summary>
    public ReadOnlyMemory<byte> Payload { get; }

    internal static DbvDocument Create(byte[] data, Encoding? textEncoding = null) => DbvReader.Read(data, [], textEncoding);
}
