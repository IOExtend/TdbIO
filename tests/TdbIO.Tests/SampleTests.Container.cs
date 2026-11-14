using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public partial class SampleTests
{
    [SkippableFact]
    public void EveryModelDatabaseOpens() => ForEachContainer(TdbFileKind.Db1, "model databases", [".db1", ".bak"], file =>
    {
        var document = Db1Document.Create(UnpackContainer(file));
        var sections = document.ReadTextSections();
        return [.. document.Warnings, $"{sections.Count} text sections, {sections.Sum(s => s.Chains.Count)} chains"];
    });

    [SkippableFact]
    public void EveryModelRecordsFileOpens() => ForEachContainer(TdbFileKind.Db2, "model records files", [".db2", ".bak"], file =>
    {
        var document = Db2Document.Create(UnpackContainer(file));
        // The reader only returns when the last record ends exactly at the end of the data.
        Assert.Equal(document.Payload.Length, document.Records.Count == 0 ? XsteelHeader.Length
            : document.Records[^1].Offset + 12 + document.Records[^1].Rows.Length + 4);
        return document.Warnings;
    });

    [SkippableFact]
    public void EveryIdRangeFileOpens() => ForEachContainer(TdbFileKind.IdRanges, "id range files", [".idrm"],
        file => IdRangeDocument.Create(UnpackContainer(file)).Warnings);

    [SkippableFact]
    public void EveryGuidMapperOpens() => ForEachContainer(TdbFileKind.GuidMapper, "GUID map files", [".mapper"],
        file => GuidMapperDocument.Create(UnpackContainer(file)).Warnings);

    [SkippableFact]
    public void EveryHistoryFileOpens() => ForEachContainer(TdbFileKind.History, "history files", [".db"], file =>
    {
        byte[] head = new byte[HistoryReader.HeadLength];
        int length;
        using (var stream = File.OpenRead(file)) length = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        var document = HistoryDocument.Create(head.AsSpan(0, length), new FileInfo(file).Length);
        Assert.True(document.PageSize > 0, "page size is 0");
        return document.Warnings;
    });

    private static byte[] UnpackContainer(string file) => Gzip.Unpack(File.ReadAllBytes(file), TdbReadOptions.Default.MaxUnpackedLength);

    /// <summary>Runs <paramref name="check"/> on every file of <paramref name="kind"/> (by content) among the files with the
    /// extensions; collects failures with relative paths and writes warnings to the test output only.</summary>
    private void ForEachContainer(TdbFileKind kind, string what, string[] extensions, Func<string, IEnumerable<string>> check)
    {
        var files = TestFiles.FindAll(extensions).Where(f => TdbFile.DetectKind(f) == kind).ToList();
        if (files.Count == 0) throw TestFiles.Missing(what);
        var failures = new List<string>();
        foreach (string file in files)
        {
            try
            {
                foreach (string line in check(file)) output.WriteLine($"{TestFiles.Relative(file)}: {line}");
            }
            catch (Exception e) when (e is not SkipException)
            {
                failures.Add($"{TestFiles.Relative(file)}: {e.GetType().Name}: {e.Message}");
            }
        }
        output.WriteLine($"{files.Count} {what} checked");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
