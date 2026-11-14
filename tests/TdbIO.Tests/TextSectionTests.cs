using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class TextSectionTests
{
    private static TextSection Read(IEnumerable<byte[]> rows) => Assert.Single(TextSectionReader.ReadAll(Synthetic.TextSectionBytes(rows)));

    private static InvalidDataException Fails(IEnumerable<byte[]> rows) =>
        Assert.Throws<InvalidDataException>(() => TextSectionReader.ReadAll(Synthetic.TextSectionBytes(rows)));

    private static string Text(TextChain chain) => System.Text.Encoding.ASCII.GetString(chain.Text.Span);

    [Fact]
    public void RowsInReverseFileOrderAssemble()
    {
        string text = new string('a', 27) + new string('b', 27) + "tail";
        var rows = Synthetic.ChainRows(10, 0x1234, Synthetic.Ascii(text));
        rows.Reverse();
        var section = Read(rows);
        Assert.Equal(3, section.RowCount);
        var chain = Assert.Single(section.Chains);
        Assert.Equal(text, Text(chain));
        Assert.Equal([10u, 11u, 12u], chain.RowIds);
        Assert.Equal(10u, chain.HeadId);
        Assert.Equal(0x1234u, chain.HeadTag);
        Assert.False(chain.IsSaveStamp);
        Assert.Null(chain.SavedAt);
        Assert.Empty(section.OrphanRowIds);
    }

    [Fact]
    public void FullLastRowAndStaleBytesAfterNul()
    {
        // A last row with 27 bytes, and a last row whose bytes after the NUL are left-overs.
        var exact = Read(Synthetic.ChainRows(1, 0, Synthetic.Ascii(new string('x', 54))));
        Assert.Equal(new string('x', 54), Text(exact.Chains[0]));

        var stale = Synthetic.TextRow(5, 0, 7, Synthetic.Concat(Synthetic.Ascii("abc"), [0], Synthetic.Ascii("old text")));
        Assert.Equal("abc", Text(Read([stale]).Chains[0]));
    }

    [Fact]
    public void OneRowEmptyChainAndTagZeroIsAHead()
    {
        var chain = Assert.Single(Read([Synthetic.TextRow(3, 0, 0, [])]).Chains);
        Assert.Equal(0, chain.Text.Length);
        Assert.Equal(0u, chain.HeadTag);
    }

    [Fact]
    public void ChainsKeepHeadFileOrderAndHeadFields()
    {
        var section = Read([Synthetic.TextRow(9, 0, 1, Synthetic.Ascii("second"), field13: 77), Synthetic.TextRow(2, 0, 1, Synthetic.Ascii("first"))]);
        Assert.Equal([9u, 2u], section.Chains.Select(c => c.HeadId));
        Assert.Equal(77u, section.Chains[0].HeadField13);
    }

    [Fact]
    public void MissingNulInLastRowThrows()
    {
        var row = Synthetic.TextRow(1, 0, 1, Synthetic.Ascii(new string('z', 29)));
        Assert.Equal(36 + 17, Fails([row]).Offset);
    }

    [Fact]
    public void DanglingLinkThrows() =>
        Assert.Equal(36 + 5, Fails([Synthetic.TextRow(1, 99, 1, Synthetic.Ascii("x"))]).Offset);

    [Fact]
    public void CycleThrows()
    {
        var e = Fails([Synthetic.TextRow(1, 2, 1, []), Synthetic.TextRow(2, 1, 0xFFFFFFFF, [])]);
        Assert.Contains("cycle", e.Message);
        Assert.Equal(36, e.Offset);
    }

    [Fact]
    public void RepeatedIdThrows() =>
        Assert.Equal(36 + 54 + 1, Fails([Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(1, 0, 1, [])]).Offset);

    [Fact]
    public void RowInTwoChainsThrows()
    {
        var e = Fails([Synthetic.TextRow(1, 3, 1, []), Synthetic.TextRow(2, 3, 1, []), Synthetic.TextRow(3, 0, 0xFFFFFFFF, [])]);
        Assert.Contains("two rows", e.Message);
        Assert.Equal(36 + 54 + 5, e.Offset);
    }

    [Fact]
    public void LinkToAHeadThrows()
    {
        // The head is linked from a row read before it.
        var e = Fails([Synthetic.TextRow(1, 2, 1, []), Synthetic.TextRow(2, 0, 1, [])]);
        Assert.Equal(36 + 54, e.Offset);
    }

    [Fact]
    public void OrphansAreReported()
    {
        var section = Read([Synthetic.TextRow(4, 0, 0xFFFFFFFF, []), Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(8, 0, 0xFFFFFFFF, [])]);
        Assert.Equal([4u, 8u], section.OrphanRowIds);
        Assert.Single(section.Chains);
    }

    [Fact]
    public void OrphanRunIsReportedInFileOrder()
    {
        var section = Read([Synthetic.TextRow(4, 8, 0xFFFFFFFF, []), Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(8, 0, 0xFFFFFFFF, [])]);
        Assert.Equal([4u, 8u], section.OrphanRowIds);
    }

    [Fact]
    public void OrphanLinkToAMissingIdThrows() =>
        Assert.Equal(36 + 54 + 5, Fails([Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(4, 99, 0xFFFFFFFF, [])]).Offset);

    [Fact]
    public void OrphanCycleThrows()
    {
        var e = Fails([Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(4, 8, 0xFFFFFFFF, []), Synthetic.TextRow(8, 4, 0xFFFFFFFF, [])]);
        Assert.Contains("cycle", e.Message);
        Assert.Equal(36 + 54, e.Offset);
    }

    [Fact]
    public void OrphanLinkIntoAChainThrows()
    {
        var e = Fails([Synthetic.TextRow(1, 2, 1, []), Synthetic.TextRow(2, 0, 0xFFFFFFFF, []), Synthetic.TextRow(5, 2, 0xFFFFFFFF, [])]);
        Assert.Contains("two rows", e.Message);
        Assert.Equal(36 + 2 * 54 + 5, e.Offset);
    }

    [Fact]
    public void OrphanLinkToAHeadThrows() =>
        Assert.Equal(36, Fails([Synthetic.TextRow(1, 0, 1, []), Synthetic.TextRow(5, 1, 0xFFFFFFFF, [])]).Offset);

    [Fact]
    public void MissingCloserOrBadMarkerThrows()
    {
        var bytes = Synthetic.TextSectionBytes([Synthetic.TextRow(1, 0, 1, [])]);
        bytes[^1] = 0x01;
        Assert.Equal(36 + 54 + 1, Assert.Throws<InvalidDataException>(() => TextSectionReader.ReadAll(bytes)).Offset);

        var cut = Synthetic.TextSectionBytes([Synthetic.TextRow(1, 0, 1, [])])[..^4];
        Assert.Throws<InvalidDataException>(() => TextSectionReader.ReadAll(cut));

        var marker = Fails([Synthetic.TextRow(1, 0, 1, [], marker: 0x05)]);
        Assert.Equal(36, marker.Offset);
        Assert.Contains("0x05", marker.Message);

        Assert.Single(Read([Synthetic.TextRow(1, 0, 1, [], marker: 0x0C)]).Chains);
    }

    [Fact]
    public void RowsWithoutEndThrow() =>
        Assert.Throws<InvalidDataException>(() => TextSectionReader.ReadAll(Synthetic.Concat(Synthetic.TextSectionSignature, Synthetic.TextRow(1, 0, 1, []))));

    [Fact]
    public void NoSignatureFindsNoSections() =>
        Assert.Empty(TextSectionReader.ReadAll(new byte[64]));

    [Fact]
    public void DocumentFindsEverySectionAndSaveStamps()
    {
        var first = Synthetic.TextSectionBytes(Synthetic.ChainRows(1, 5, Synthetic.Ascii("open text")));
        var stamp = Synthetic.TextSectionBytes(Synthetic.ChainRows(20, 5, Synthetic.HiddenSaveText("14.11.2026;09:08:07;one;two;" + new string('n', 30))));
        var payload = Synthetic.Db1Payload([], first, new byte[3], stamp);
        var document = Db1Document.Create(payload);

        var sections = document.ReadTextSections();
        Assert.Equal(2, sections.Count);
        Assert.Equal(81, sections[0].Offset);
        Assert.Equal(81 + first.Length + 3, sections[1].Offset);
        var chain = sections[1].Chains[0];
        Assert.True(chain.IsSaveStamp);
        Assert.Equal(new DateTime(2026, 11, 14, 9, 8, 7), chain.SavedAt);
        Assert.Equal("14.11.2026;09:08:07;one;two;" + new string('n', 30), System.Text.Encoding.ASCII.GetString(chain.RevealSaveText()));
        Assert.Equal(3, chain.RowIds.Count);
    }

    [Fact]
    public void DocumentWithoutTextSectionsGivesNone() =>
        Assert.Empty(Db1Document.Create(Synthetic.Db1Payload([])).ReadTextSections());

    [Fact]
    public void BrokenSectionDoesNotHideTheHeader()
    {
        var broken = Synthetic.Concat(Synthetic.TextSectionSignature, Synthetic.TextRow(1, 9, 1, []), [0], Synthetic.TextSectionCloser);
        var document = Db1Document.Create(Synthetic.Db1Payload([], broken));
        Assert.NotNull(document.IdMap);
        Assert.Equal(81 + 36 + 5, Assert.Throws<InvalidDataException>(() => document.ReadTextSections()).Offset);
    }
}
