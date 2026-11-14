using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public partial class SampleTests
{
    private const long TablesReadLimit = 1L << 30;

    /// <summary>The kind is taken from the content, so a .bak or an unrelated file with a matching extension is sorted
    /// correctly; only files of <paramref name="kind"/> are returned.</summary>
    private static List<string> TableFilesOfKind(TdbFileKind kind, params string[] extensions) =>
        TestFiles.FindAll(extensions).Where(file => TdbFile.DetectKind(file) == kind).ToList();

    [SkippableFact]
    public void EveryModelDatabaseWalksToTheEnd()
    {
        var files = TableFilesOfKind(TdbFileKind.Db1, ".db1", ".bak");
        if (files.Count == 0) throw TestFiles.Missing("model database (.db1) files");
        var failures = new List<string>();
        foreach (string file in files)
        {
            try
            {
                byte[] payload = Gzip.Unpack(File.ReadAllBytes(file), TablesReadLimit);
                var tables = Db1Document.Create(payload).ReadTables();
                // The walk itself rejects gaps, a plain section after a keyed one and bytes after the last section.
                if (tables.Count == 0 || tables[^1].End != payload.Length)
                    failures.Add($"{TestFiles.Relative(file)}: the sections do not end at the end of the payload");
                output.WriteLine($"{TestFiles.Relative(file)}: {tables.Count} sections, {tables.Sum(t => (long)t.Rows.Count)} rows");
            }
            catch (InvalidDataException e)
            {
                failures.Add($"{TestFiles.Relative(file)}: {e.Message}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [SkippableFact]
    public void EveryDbvFileOpens()
    {
        var files = TableFilesOfKind(TdbFileKind.Dbv, ".db", ".db5", ".bak");
        if (files.Count == 0) throw TestFiles.Missing("DBV@ files");
        var failures = new List<string>();
        foreach (string file in files)
        {
            string at = TestFiles.Relative(file);
            try
            {
                var doc = DbvDocument.Create(Gzip.Unpack(File.ReadAllBytes(file), TablesReadLimit));
                long end = doc.Sections.Count > 0 ? doc.Sections[^1].End : -1;
                if (doc.Sections.Count > 0 && end != doc.Payload.Length) failures.Add($"{at}: the sections do not end at the end of the data");
                if (doc.T[0] != doc.ListHeader[3]) failures.Add($"{at}: T[0] differs from A[3]");
                for (int i = 0; i < doc.GuidRecords.Count; i++)
                    if (doc.GuidRecords[i].Index != i + 1) failures.Add($"{at}: GUID record {i + 1} has another index");
                foreach (string warning in doc.Warnings) output.WriteLine($"{at}: warning: {warning}");
            }
            catch (InvalidDataException e)
            {
                failures.Add($"{at}: {e.Message}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
