using System.Text.RegularExpressions;
using Xunit;

namespace TdbIO.Tests;

/// <summary>Keeps private data out of the repository: no vector names, sample codes or configured folders, and no paths
/// into a user profile, in any text file of the repository.</summary>
public class RepositoryHygieneTests
{
    private static readonly string[] SkippedFolders = ["bin", "obj", "test_files", ".git", ".vs", "_site", "artifacts", "TestResults"];

    private static readonly string[] TextExtensions =
        [".cs", ".csproj", ".props", ".targets", ".slnx", ".json", ".md", ".yml", ".yaml", ".ps1", ".svg", ".txt", ".editorconfig", ".gitattributes", ".gitignore"];

    private static string RepositoryRoot()
    {
        for (var directory = AppContext.BaseDirectory; directory != null; directory = Path.GetDirectoryName(directory))
            if (File.Exists(Path.Combine(directory, "TdbIO.slnx"))) return directory;
        throw new SkipException("The repository root (the folder with TdbIO.slnx) is not an ancestor of the test binary.");
    }

    private static IEnumerable<string> TextFiles(string root) =>
        Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => SkippedFolders.Contains(part, StringComparer.OrdinalIgnoreCase)))
            .Where(file => TextExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                || TextExtensions.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase));

    [SkippableFact]
    public void NoFileHoldsAPathIntoAUserProfile()
    {
        // Built from pieces so that this file does not match itself.
        var profilePath = new Regex("[A-Za-z]:[\\\\/]" + "Users[\\\\/]" + "|/" + "home/", RegexOptions.CultureInvariant);
        string root = RepositoryRoot();
        var hits = TextFiles(root)
            .Where(file => profilePath.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();
        Assert.True(hits.Count == 0, "Paths into a user profile in: " + string.Join(", ", hits));
    }

    [SkippableFact]
    public void CodeFilesLiveUnderSrcOrTests()
    {
        // No project compiles a .cs file outside these folders, so one there is a left-over that would still be committed.
        string root = RepositoryRoot();
        var stray = TextFiles(root)
            .Where(file => Path.GetExtension(file).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file))
            .Where(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0] is not ("src" or "tests"))
            .ToList();
        Assert.True(stray.Count == 0, "Code files outside src and tests: " + string.Join(", ", stray));
    }

    [SkippableFact]
    public void NoFileNamesAVectorASampleOrAConfiguredFolder()
    {
        var entries = TestVectors.Entries();
        var secrets = entries.Select(TestVectors.Name)
            .Concat(entries.Select(e => e.TryGetProperty("fixture", out var f) ? f.GetString() : null).OfType<string>())
            .Concat([Environment.GetEnvironmentVariable(TestVectors.EnvironmentVariable), Environment.GetEnvironmentVariable(TestFiles.EnvironmentVariable)])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.TrimEnd('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string root = RepositoryRoot();
        var hits = new List<string>();
        foreach (string file in TextFiles(root))
        {
            string text = File.ReadAllText(file);
            // Only the position of a hit is reported, so the private value itself never reaches the test log.
            for (int i = 0; i < secrets.Count; i++)
                if (text.Contains(secrets[i], StringComparison.OrdinalIgnoreCase)) hits.Add($"{Path.GetRelativePath(root, file)} (value {i})");
        }
        Assert.True(hits.Count == 0, "Private values found in: " + string.Join(", ", hits));
    }
}
