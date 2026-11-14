using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace TdbIO.Tests;

/// <summary>The private vector folder (inputs cut from sample files and their expected values), named only by the
/// environment variable TDBIO_TEST_VECTORS. Every manifest-*.json in it is read at run time; vector names, sample codes
/// and paths are never written into the code. Tests select entries by the keys of their "expected" object, never by
/// name.</summary>
internal static class TestVectors
{
    public const string EnvironmentVariable = "TDBIO_TEST_VECTORS";

    public static string Folder() =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } folder && Directory.Exists(folder)
            ? folder
            : throw new SkipException($"{EnvironmentVariable} is not set or does not name a folder; vector tests need the private vector folder.");

    /// <summary>The entries of the "vectors" arrays of every manifest-*.json, manifests in name order.</summary>
    public static IReadOnlyList<JsonElement> Entries()
    {
        var entries = new List<JsonElement>();
        foreach (string manifest in Directory.EnumerateFiles(Folder(), "manifest-*.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(manifest));
            entries.AddRange(document.RootElement.GetProperty("vectors").EnumerateArray().Select(e => e.Clone()));
        }
        Assert.True(entries.Count > 0, $"{EnvironmentVariable} names a folder without manifest-*.json entries.");
        return entries;
    }

    public static string Name(JsonElement entry) => entry.GetProperty("name").GetString()!;

    /// <summary>The "expected" object of the entry, or null when it has none or it is not an object.</summary>
    public static JsonElement? Expected(JsonElement entry) =>
        entry.TryGetProperty("expected", out var expected) && expected.ValueKind == JsonValueKind.Object ? expected : null;

    /// <summary>Entries whose "expected" object has all of the keys.</summary>
    public static IReadOnlyList<JsonElement> WithKeys(params string[] keys) =>
        Entries().Where(e => Expected(e) is { } expected && keys.All(k => expected.TryGetProperty(k, out _))).ToList();

    /// <summary>The bytes of a vector, checked against the length (when given) and the SHA-256 of the manifest.</summary>
    public static byte[] Read(JsonElement entry)
    {
        string name = Name(entry);
        Assert.True(Path.GetFileName(name) == name, $"Vector name '{name}' is not a plain file name.");
        byte[] bytes = File.ReadAllBytes(Path.Combine(Folder(), name));
        if (entry.TryGetProperty("length", out var length))
            Assert.True(length.GetInt64() == bytes.Length, $"Vector {name}: length {bytes.Length} differs from the manifest.");
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.True(string.Equals(actual, entry.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase),
            $"Vector {name}: SHA-256 does not match the manifest.");
        return bytes;
    }

    /// <summary>Fails when the "expected" object has a key that the test neither checks nor knowingly ignores, so that a
    /// changed manifest is noticed instead of passing unchecked.</summary>
    public static void CheckKeys(JsonElement entry, IEnumerable<string> checkedKeys, IEnumerable<string> ignoredKeys)
    {
        if (Expected(entry) is not { } expected) return;
        var known = new HashSet<string>(checkedKeys.Concat(ignoredKeys), StringComparer.Ordinal);
        var unknown = expected.EnumerateObject().Select(p => p.Name).Where(k => !known.Contains(k)).ToList();
        Assert.True(unknown.Count == 0, $"Vector {Name(entry)}: expected keys not handled by the test: {string.Join(", ", unknown)}.");
    }
}
