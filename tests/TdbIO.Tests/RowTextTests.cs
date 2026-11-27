using System.Buffers.Binary;
using System.Text;
using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

/// <summary>The text values of table rows: zero-terminated, and single-byte in the code page of the system that saved
/// the model. Without a given encoding each value is ASCII, UTF-8, code page 1251 or code page 1252, in that order.
/// All values are made up.</summary>
public class RowTextTests
{
    private static readonly Encoding Cp1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
    private static readonly Encoding Cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    private static string Decode(byte[] body, int at, int length, Encoding? encoding = null) => Bytes.Text(body, at, length, encoding);

    private delegate void FillSpan(Span<byte> span);

    private static TableRow Row(int size, FillSpan fill)
    {
        var bytes = new byte[size];
        fill(bytes);
        return new TableRow(100, 4, bytes, 1, 2);
    }

    [Fact]
    public void OnlyTheBytesOfTheFieldAreRead()
    {
        // Bytes before the field, and bytes after the first NUL of the field, are not part of the value.
        byte[] body = [0xEE, 0xEE, 0, .. Cp1251.GetBytes("Столб"), 0, 0x98, 0x98, 0x98, 0x98];

        Assert.Equal("Столб", Decode(body, 3, 8));
    }

    [Fact]
    public void AValueWithoutNulRunsToTheEndOfTheField() =>
        Assert.Equal("Столб", Decode(Cp1251.GetBytes("Столб"), 0, 5));

    [Fact]
    public void AnEmptyValueIsTheEmptyString() => Assert.Equal("", Decode([0, 0x98, 0, 0], 0, 4));

    [Theory]
    [InlineData(new byte[] { (byte)'A', (byte)'B', 0, 0 }, "AB")]
    [InlineData(new byte[] { 0xD0, 0xA1, 0xD1, 0x82, 0 }, "Ст")]
    [InlineData(new byte[] { 0xD1, 0xF2, 0xEE, 0xEB, 0xE1, 0 }, "Столб")]
    [InlineData(new byte[] { (byte)'1', (byte)'0', 0xB0, (byte)' ', 0xBD, 0 }, "10° ½")]
    public void TheEncodingIsChosenFromTheBytesOfTheValue(byte[] body, string text) =>
        Assert.Equal(text, Decode(body, 0, body.Length));

    [Fact]
    public void LatinLettersWithDiacriticsAreReadByTheRuleAsCodePage1251()
    {
        // The known limit of the rule: code pages 1251 and 1252 share the range 0xC0..0xFF (Cyrillic letters there,
        // Latin letters with diacritics here), and the rule gives it to 1251. Such a file needs its encoding given.
        byte[] body = Cp1252.GetBytes("été");

        Assert.Equal("йtй", Decode(body, 0, body.Length));
        Assert.Equal("été", Decode(body, 0, body.Length, Cp1252));
    }

    [Fact]
    public void TheGivenEncodingIsUsedAsIs()
    {
        // The same bytes of code page 1251, read as code page 1252.
        byte[] body = Cp1251.GetBytes("Столб");

        Assert.Equal("Столб", Decode(body, 0, body.Length, Cp1251));
        Assert.Equal("Ñòîëá", Decode(body, 0, body.Length, Cp1252));
    }

    [Fact]
    public void AByteTheChosenPageDoesNotDefineIsAnErrorAtItsOffsetInTheBody()
    {
        // One high byte of the upper half of two chooses code page 1251, which does not define 0x98.
        byte[] body = [0, 0, 0xC0, 0x98];

        var e = Assert.Throws<InvalidDataException>(() => Decode(body, 2, 2));
        Assert.Equal(3, e.Offset);
        Assert.Contains("cp1251", e.Message);
    }

    [Fact]
    public void AByteTheGivenEncodingDoesNotDefineIsAnErrorAtItsOffsetInTheBody()
    {
        // A strict encoding, as the command line tool builds it; a lenient one would put a replacement character.
        Encoding ascii = Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        byte[] body = [0, (byte)'a', 0x98];

        var e = Assert.Throws<InvalidDataException>(() => Decode(body, 1, 2, ascii));
        Assert.Equal(2, e.Offset);
        Assert.Contains("us-ascii", e.Message);
    }

    [Fact]
    public void AFieldOutsideTheBodyIsAnErrorAtItsOffset() =>
        Assert.Equal(2, Assert.Throws<InvalidDataException>(() => Decode([1, 2, 3], 2, 4)).Offset);

    [Fact]
    public void RowsOfAFileCarryTheEncodingItWasOpenedWith()
    {
        var body = new byte[AssemblyRow.ExpectedRowSize];
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(0..), 10);
        Cp1251.GetBytes("Столб").CopyTo(body, 20);
        // As in a file: sections without a key come first.
        byte[] payload = Synthetic.Db1TablesPayload(
            Synthetic.Db1Section(null, 4, [1]),
            Synthetic.Db1Section(AssemblyRow.TableKey, AssemblyRow.ExpectedRowSize, [1], (4, body, 1, 1)));
        static TableRow KeyedRow(TdbDocument document) =>
            Assert.Single(Assert.Single(Assert.IsType<Db1Document>(document).ReadTables(), s => s.Key is not null).Rows);

        using var given = new MemoryStream(Synthetic.Gzip(payload));
        TableRow row = KeyedRow(TdbFile.Open(given, null, new TdbReadOptions { TextEncoding = Cp1252 }));
        Assert.Same(Cp1252, row.TextEncoding);
        Assert.Equal("Ñòîëá", row.AsAssembly().Name);

        using var automatic = new MemoryStream(Synthetic.Gzip(payload));
        TableRow plain = KeyedRow(TdbFile.Open(automatic));
        Assert.Null(plain.TextEncoding);
        Assert.Equal("Столб", plain.AsAssembly().Name);
    }

    [Fact]
    public void TheModelReadsTheStringsWithTheEncodingOfTheFile()
    {
        // One chunk in the string pool, referenced by a string attribute of object 100.
        TableRow pool = Row(StringPoolRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], -1);
            Cp1251.GetBytes("Столб").CopyTo(span[16..]);
        });
        TableRow definition = Row(StrAttrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 10);
            Encoding.ASCII.GetBytes("ATTR_NAME").CopyTo(span[8..]);
            BinaryPrimitives.WriteInt32LittleEndian(span[112..], 1);
        });
        TableRow link = Row(StrAttrRow.ExpectedRowSize, span =>
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[0..], 1);
            BinaryPrimitives.WriteInt32LittleEndian(span[4..], 10);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..], 100);
        });
        TableRow obj = Row(ObjectRow.ExpectedRowSize, span => BinaryPrimitives.WriteInt32LittleEndian(span[0..], 100));

        // The sections as the reader builds them for a file opened with the encoding.
        static TableSection Section(int key, int size, Encoding? textEncoding, params TableRow[] rows) =>
            new(0, 0, 100, key, size, [0, 0, 0], 20, rows, textEncoding);
        string Attribute(Encoding? textEncoding) => ModelBuilder.BuildFromSections(
            Db1Document.Create(Synthetic.Db1Payload([]), textEncoding),
            [
                Section(ObjectRow.TableKey, ObjectRow.ExpectedRowSize, textEncoding, obj),
                Section(StringPoolRow.TableKey, StringPoolRow.ExpectedRowSize, textEncoding, pool),
                Section(StrAttrAttrRow.TableKey, StrAttrAttrRow.ExpectedRowSize, textEncoding, definition),
                Section(StrAttrRow.TableKey, StrAttrRow.ExpectedRowSize, textEncoding, link),
            ]).FindObject(100)!.StringAttributes["ATTR_NAME"];

        Assert.Equal("Столб", Attribute(null));
        Assert.Equal("Ñòîëá", Attribute(Cp1252));
    }
}
