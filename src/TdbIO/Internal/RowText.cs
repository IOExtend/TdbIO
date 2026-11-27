using System.Text;

namespace TdbIO.Internal;

/// <summary>How the typed decoders read the text values of the rows of one file; one instance is shared by all the rows
/// of the file.</summary>
/// <param name="encoding">The <see cref="TdbReadOptions.TextEncoding"/> the file was opened with, or null.</param>
internal sealed class RowText(Encoding? encoding)
{
    // The text fields (offset, width) that the typed decoders in ModelRows.cs read, by table key and row size.
    // Keep in step with the Bytes.Text calls there.
    private static readonly (int Key, int RowSize, (int At, int Width)[] Fields)[] TextFields =
    [
        (AssemblyRow.TableKey, AssemblyRow.ExpectedRowSize, [(20, 64)]),
        (RelationRow.TableKey, RelationRow.ExpectedRowSize, [(16, 40)]),
        (PartAttrRow.TableKey, PartAttrRow.ExpectedRowSize,
            [(32, 22), (54, 62), (116, 62), (184, 22), (206, 62), (268, 32), (300, 62)]),
        (NumAttrAttrRow.TableKey, NumAttrAttrRow.ExpectedRowSize, [(16, 21)]),
        (StrAttrAttrRow.TableKey, StrAttrAttrRow.ExpectedRowSize, [(8, 21), (29, 81)]),
        (StringPoolRow.TableKey, StringPoolRow.ExpectedRowSize, [(16, 29)]),
        (PartNumberingRow.TableKey, PartNumberingRow.ExpectedRowSize, [(20, 48)]),
        (AssemblyNumberingRow.TableKey, AssemblyNumberingRow.ExpectedRowSize, [(28, 48)]),
    ];

    /// <summary>The encoding the values are read in, used as given; null to choose it from the bytes.</summary>
    public Encoding? Encoding => encoding;

    /// <summary>Without <see cref="Encoding"/>: the code page, 1251 or 1252, of the values that are neither ASCII nor
    /// UTF-8, chosen once from those values in the whole file (<see cref="ChooseCodePage"/>); null to choose it for each
    /// value. Set at the end of the walk, before any row is read.</summary>
    public int? CodePage { get; private set; }

    /// <summary>Sets <see cref="CodePage"/> from the text fields of the typed tables in <paramref name="sections"/>.
    /// A short value such as <c>№5</c> holds too few bytes to tell the two code pages apart, and the values of one
    /// file share the code page of the system that saved it, so the choice counts the bytes of all of them.</summary>
    public void ChooseCodePage(IReadOnlyList<TableSection> sections)
    {
        if (encoding != null) return;
        long high = 0, upper = 0;
        foreach (var section in sections)
        {
            foreach (var (key, rowSize, fields) in TextFields)
            {
                if (section.Key != key || section.RowSize != rowSize) continue;
                foreach (var row in section.Rows)
                {
                    ReadOnlySpan<byte> body = row.Body.Span;
                    foreach (var (at, width) in fields)
                    {
                        var field = body.Slice(at, width);
                        int nul = field.IndexOf((byte)0);
                        TextDecoding.CountCodePageBytes(nul >= 0 ? field[..nul] : field, ref high, ref upper);
                    }
                }
            }
        }
        CodePage = high == 0 ? null : TextDecoding.CodePageOf(high, upper);
    }
}
