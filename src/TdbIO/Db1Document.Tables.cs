using TdbIO.Internal;

namespace TdbIO;

public sealed partial class Db1Document
{
    /// <summary>Reads the table sections of the model database, from the first section to the end of
    /// <see cref="Payload"/>, which they must fill exactly. The rows are a view over the payload, so large tables cost no
    /// memory per row. Builds a new list on each call.</summary>
    /// <returns>The sections in file order: first the sections without a key, then the ones with a key.</returns>
    /// <exception cref="InvalidDataException">A section is malformed or cut, a section without a key follows one with a key,
    /// or bytes are left after the last section. The message names the section and the offset.</exception>
    public IReadOnlyList<TableSection> ReadTables() =>
        SectionWalker.Walk(Payload, SectionWalker.FindFirstSection(Payload.Span), 0, SectionMode.Db1, null);
}
