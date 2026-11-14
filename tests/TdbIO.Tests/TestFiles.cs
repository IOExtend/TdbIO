namespace TdbIO.Tests;

/// <summary>Where the tests find private sample files that are not part of the repository. The root is named by the
/// environment variable TDBIO_TEST_FILES; every folder test_files in an ancestor of the test binary is searched too.
/// Files are found by extension only, a few folder levels deep, never by name. A test that finds nothing is reported as
/// skipped with the reason, never passed.</summary>
internal static class TestFiles
{
    public const string EnvironmentVariable = "TDBIO_TEST_FILES";

    // Model folders lie one or two levels below the root; their subfolders are one or two levels further down.
    private static readonly EnumerationOptions Search = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 4,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <summary>Folders searched for files, the one named by the environment variable first.</summary>
    public static IEnumerable<string> Roots()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured && Directory.Exists(configured))
            yield return Path.GetFullPath(configured);
        for (var directory = AppContext.BaseDirectory; directory != null; directory = Path.GetDirectoryName(directory))
        {
            string candidate = Path.Combine(directory, "test_files");
            if (Directory.Exists(candidate)) yield return candidate;
        }
    }

    /// <summary>Every file with one of the extensions (ordinal, ignoring case) in the roots, each full path once, ordered by
    /// path.</summary>
    public static IReadOnlyList<string> FindAll(params string[] extensions) =>
        Roots()
            .SelectMany(root => Directory.EnumerateFiles(root, "*", Search))
            .Where(file => extensions.Any(e => Path.GetExtension(file).Equals(e, StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The path relative to the root it was found in, for messages; the root itself is never printed.</summary>
    public static string Relative(string path)
    {
        foreach (string root in Roots())
        {
            string relative = Path.GetRelativePath(root, path);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative)) return relative.Replace('\\', '/');
        }
        return Path.GetFileName(path);
    }

    /// <summary>The skip for a test that found nothing to check. It names the environment variable only, never a
    /// path.</summary>
    public static SkipException Missing(string what)
    {
        string hint = Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } configured && !Directory.Exists(configured)
            ? $"{EnvironmentVariable} is set but does not name an existing folder."
            : $"Set {EnvironmentVariable} to the folder that holds the private test files.";
        return new($"No {what} found under the test root. {hint}");
    }
}
