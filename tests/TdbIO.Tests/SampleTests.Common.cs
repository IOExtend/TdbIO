using Xunit;
using Xunit.Abstractions;

namespace TdbIO.Tests;

/// <summary>Checks on private sample files (TDBIO_TEST_FILES or test_files), found by extension only. Each fact asserts a
/// property that holds for every file and is skipped with the reason when no file is found.</summary>
public partial class SampleTests(ITestOutputHelper output)
{
    /// <summary>Extensions of the files the tests look at; the kind is always taken from the content.</summary>
    internal static readonly string[] Extensions = [".db1", ".db2", ".db", ".db5", ".idrm", ".mapper", ".bak", ".nc1", ".tpl", ".rpt", ".xsr"];

    [SkippableFact]
    public void DetectionNeverThrowsForContent()
    {
        var files = TestFiles.FindAll(Extensions);
        if (files.Count == 0) throw TestFiles.Missing("sample files");
        var failures = new List<string>();
        var kinds = new SortedDictionary<TdbFileKind, int>();
        foreach (string file in files)
        {
            try
            {
                var kind = TdbFile.DetectKind(file);
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            }
            catch (Exception e) when (e is not IOException and not UnauthorizedAccessException)
            {
                failures.Add($"{TestFiles.Relative(file)}: {e.GetType().Name}: {e.Message}");
            }
        }
        output.WriteLine(string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}")));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
