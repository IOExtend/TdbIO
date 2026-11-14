using Xunit;

namespace TdbIO.Tests;

/// <summary>Checks against the private vectors (TDBIO_TEST_VECTORS). Each test loops over the manifests, so it is skipped as
/// a whole when the folder is not available; failure messages name the vector read from the manifest.</summary>
public partial class VectorTests
{
    [SkippableFact]
    public void EveryManifestFileMatchesItsSha256()
    {
        foreach (var entry in TestVectors.Entries()) TestVectors.Read(entry);
    }
}
