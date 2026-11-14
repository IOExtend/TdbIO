using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class KindDetectionTests
{
    private static byte[] Xsteel(byte type) => Synthetic.Concat(Synthetic.Ascii("Xsteel"), [type], Synthetic.Ascii(" 1.23 "), new byte[42]);

    private static TdbFileKind Detect(byte[] data, string? fileName = null) => TdbFile.DetectKind(new MemoryStream(data), fileName);

    [Theory]
    [InlineData(0x85, TdbFileKind.Db1)]
    [InlineData(0x80, TdbFileKind.Db2)]
    [InlineData(0x20, TdbFileKind.Drawing)]
    [InlineData(0x81, TdbFileKind.Unknown)]
    [InlineData(0x00, TdbFileKind.Unknown)]
    public void TheXsteelTypeByteGivesTheKind(byte type, TdbFileKind expected) => Assert.Equal(expected, Detect(Synthetic.Gzip(Xsteel(type))));

    [Theory]
    [InlineData("IdRangeMapper", TdbFileKind.IdRanges)]
    [InlineData("!Guid mapping for ", TdbFileKind.GuidMapper)]
    [InlineData("DBV@", TdbFileKind.Dbv)]
    [InlineData("Xsteel", TdbFileKind.Unknown)]
    [InlineData("IdRangeMappe", TdbFileKind.Unknown)]
    [InlineData("!Guid mapping", TdbFileKind.Unknown)]
    public void UnpackedSignaturesGiveTheKind(string head, TdbFileKind expected) =>
        Assert.Equal(expected, Detect(Synthetic.Gzip(Synthetic.Concat(Synthetic.Ascii(head), new byte[8]))));

    [Fact]
    public void ASignatureNeedsNoMoreThanItsOwnBytes() =>
        Assert.Equal(TdbFileKind.IdRanges, Detect(Synthetic.Gzip(Synthetic.Ascii("IdRangeMapper"))));

    [Fact]
    public void UncompressedSignaturesOfGzipKindsAreUnknown()
    {
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Ascii("DBV@ and more bytes")));
        Assert.Equal(TdbFileKind.Unknown, Detect(Xsteel(0x85)));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Ascii("IdRangeMapper")));
    }

    [Fact]
    public void AnSqliteHeaderIsHistory() =>
        Assert.Equal(TdbFileKind.History, Detect(Synthetic.Concat(Synthetic.Ascii("SQLite format 3\0"), new byte[84])));

    [Fact]
    public void AnStLineIsDstv()
    {
        Assert.Equal(TdbFileKind.Nc1, Detect(Synthetic.Ascii("ST\r\n** comment\r\n")));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Ascii("ST\n** comment\n")));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Ascii("STX\r\n")));
    }

    [Theory]
    [InlineData("template\r\n{\r\n", true)]
    [InlineData("\r\n\r\ntemplate {", true)]
    [InlineData("  \n template", true)]
    [InlineData("template", true)]
    [InlineData("template_x", false)]
    [InlineData("templatex {", false)]
    [InlineData("template9", false)]
    [InlineData("\ttemplate", false)]
    [InlineData("Template", false)]
    [InlineData("x template", false)]
    public void TheFirstWordTemplateIsATemplate(string text, bool expected) =>
        Assert.Equal(expected ? TdbFileKind.Template : TdbFileKind.Unknown, Detect(Synthetic.Ascii(text)));

    [Fact]
    public void ATemplateMayStartWithAUtf8ByteOrderMark() =>
        Assert.Equal(TdbFileKind.Template, Detect(Synthetic.Concat([0xEF, 0xBB, 0xBF], Synthetic.Ascii("\r\ntemplate"))));

    [Fact]
    public void AReportIsRecognisedOnlyByItsNameAndOnlyWhenTheContentIsUnknown()
    {
        byte[] text = Synthetic.Ascii("    report line\r\n");
        Assert.Equal(TdbFileKind.Report, Detect(text, "list.xsr"));
        Assert.Equal(TdbFileKind.Report, Detect(text, "LIST.XSR"));
        Assert.Equal(TdbFileKind.Unknown, Detect(text, "list.xsr.txt"));
        Assert.Equal(TdbFileKind.Unknown, Detect(text));
        Assert.Equal(TdbFileKind.Nc1, Detect(Synthetic.Ascii("ST\r\n"), "list.xsr"));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Gzip(text), "list.xsr"));
    }

    [Fact]
    public void ShortEmptyAndCorruptDataIsUnknown()
    {
        Assert.Equal(TdbFileKind.Unknown, Detect([]));
        Assert.Equal(TdbFileKind.Unknown, Detect([0x1F]));
        Assert.Equal(TdbFileKind.Unknown, Detect([0x1F, 0x8B]));
        Assert.Equal(TdbFileKind.Unknown, Detect([0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF]));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Gzip(Synthetic.Ascii("Xstee"))));
        Assert.Equal(TdbFileKind.Unknown, Detect(Synthetic.Gzip(Synthetic.Ascii("Xsteel"))));
    }

    [Fact]
    public void AGzipFileWithFlagsIsUnknownForDetection()
    {
        byte[] file = Synthetic.Gzip(Xsteel(0x85));
        file[3] = 0x08;
        Assert.Equal(TdbFileKind.Unknown, Detect(file));
    }

    [Fact]
    public void ACutGzipFileIsStillClassified()
    {
        var random = new Random(2);
        var payload = Synthetic.Concat(Xsteel(0x85), new byte[200_000]);
        random.NextBytes(payload.AsSpan(49));
        byte[] file = Synthetic.Gzip(payload);
        Assert.True(file.Length > 64 * 1024);
        Assert.Equal(TdbFileKind.Db1, Detect(file[..(64 * 1024)]));
        Assert.Equal(TdbFileKind.Db1, Detect(file));
    }

    [Fact]
    public void ASeekableStreamIsMovedBackAndNotDisposed()
    {
        var stream = new MemoryStream(Synthetic.Concat(new byte[3], Synthetic.Ascii("ST\r\n")));
        stream.Position = 3;
        Assert.Equal(TdbFileKind.Nc1, TdbFile.DetectKind(stream));
        Assert.Equal(3, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void DetectKindOfAPathPassesTheFileName()
    {
        string folder = Path.Combine(Path.GetTempPath(), "tdbio-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "a.xsr");
            File.WriteAllBytes(path, Synthetic.Ascii("    report line\r\n"));
            Assert.Equal(TdbFileKind.Report, TdbFile.DetectKind(path));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ArgumentsAreChecked()
    {
        Assert.Throws<ArgumentNullException>(() => TdbFile.DetectKind((string)null!));
        Assert.Throws<ArgumentException>(() => TdbFile.DetectKind(""));
        Assert.Throws<ArgumentNullException>(() => TdbFile.DetectKind((Stream)null!));
        var closed = new MemoryStream();
        closed.Dispose();
        Assert.Throws<ArgumentException>(() => TdbFile.DetectKind(closed));
    }

    [Fact]
    public void FromUnpackedNeedsTheTypeByte() => Assert.Equal(TdbFileKind.Unknown, KindDetector.FromUnpacked(Synthetic.Ascii("Xsteel")));
}
