using System.Text;
using Xunit;

namespace TdbIO.Tests;

public partial class SampleTests
{
    [SkippableFact]
    public void EveryNc1Opens() => CheckEveryTextFile("DSTV files", TdbFileKind.Nc1, [".nc1"], data =>
    {
        var doc = Nc1Document.Create(data);
        Assert.Equal("ST", doc.Blocks[0].Code);
        Assert.Equal("EN", doc.Blocks[^1].Code);
        string rebuilt = string.Concat(doc.Blocks.SelectMany(b => b.RawLines.Prepend(b.Code)).Select(l => l + "\r\n"));
        Assert.True(Encoding.ASCII.GetBytes(rebuilt).AsSpan().SequenceEqual(data), "raw lines do not give the file back");
        return doc.Warnings;
    });

    [SkippableFact]
    public void EveryTemplateOpens() => CheckEveryTextFile("templates", TdbFileKind.Template, [".tpl", ".rpt"], data =>
    {
        var doc = TemplateDocument.Create(data, null);
        Assert.Equal("template", doc.Root.Keyword);
        return doc.Warnings;
    });

    [SkippableFact]
    public void EveryReportOpens() => CheckEveryTextFile("reports", TdbFileKind.Report, [".xsr"], data =>
    {
        var doc = ReportDocument.Create(data, null);
        var lines = doc.Lines.Select((l, i) => doc.PageStarts.Contains(i) ? "\f" + l : l);
        string text = string.Join("\r\n", lines) + (doc.EndsWithLineBreak ? "\r\n" : "");
        byte[] bom = doc.EncodingName == "utf-8" && data.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? [0xEF, 0xBB, 0xBF] : [];
        byte[] encoded = [.. bom, .. TextEncodingByName(doc.EncodingName).GetBytes(text)];
        Assert.True(encoded.AsSpan().SequenceEqual(data), "lines encoded again do not give the file back");
        return doc.Warnings;
    });

    private static Encoding TextEncodingByName(string name) => name switch
    {
        "ascii" => Encoding.ASCII,
        "utf-8" => new UTF8Encoding(false),
        "cp1251" => CodePagesEncodingProvider.Instance.GetEncoding(1251)!,
        "cp1252" => CodePagesEncodingProvider.Instance.GetEncoding(1252)!,
        _ => throw new InvalidOperationException($"Unknown encoding name {name}."),
    };

    // Files are found by extension and kept only when their content is of the kind; each failure names the relative path.
    private void CheckEveryTextFile(string what, TdbFileKind kind, string[] extensions, Func<byte[], IReadOnlyList<string>> check)
    {
        var files = TestFiles.FindAll(extensions).Where(f => TdbFile.DetectKind(f) == kind).ToList();
        if (files.Count == 0) throw TestFiles.Missing(what);
        var failures = new List<string>();
        int warnings = 0;
        foreach (string file in files)
        {
            try
            {
                var fileWarnings = check(File.ReadAllBytes(file));
                foreach (string warning in fileWarnings) output.WriteLine($"{TestFiles.Relative(file)}: {warning}");
                warnings += fileWarnings.Count;
            }
            catch (Exception e) when (e is not IOException and not UnauthorizedAccessException)
            {
                failures.Add($"{TestFiles.Relative(file)}: {e.GetType().Name}: {e.Message}");
            }
        }
        output.WriteLine($"{files.Count} {what} checked, {warnings} warnings");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
