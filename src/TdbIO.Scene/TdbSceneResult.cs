using IOScene;

namespace TdbIO.Scene;

/// <summary>
/// The result of <see cref="TdbSceneReader"/>: the scene and what happened to the model's data.
/// </summary>
public sealed class TdbSceneResult
{
    internal TdbSceneResult(
        SceneDocument document,
        IReadOnlyList<SceneDiagnostic> diagnostics,
        IReadOnlyList<TdbGeometryDiagnostic> geometryDiagnostics,
        int elementCount,
        int elementsWithGeometry,
        int partCount)
    {
        Document = document;
        Diagnostics = diagnostics;
        GeometryDiagnostics = geometryDiagnostics;
        ElementCount = elementCount;
        ElementsWithGeometry = elementsWithGeometry;
        PartCount = partCount;
    }

    /// <summary>The scene, in metres with Z up.</summary>
    public SceneDocument Document { get; }

    /// <summary>Findings of the conversion.</summary>
    public IReadOnlyList<SceneDiagnostic> Diagnostics { get; }

    /// <summary>Findings about the geometry of the parts: the proxy boxes, the fallback dimensions and the parts that
    /// got no mesh. Every mesh is a rectangular proxy box around the part axis, not the decoded Tekla solid.</summary>
    public IReadOnlyList<TdbGeometryDiagnostic> GeometryDiagnostics { get; }

    /// <summary>The exact factor applied to every coordinate: Tekla millimetres to metres (0.001).</summary>
    public double MetresPerUnit => 0.001;

    /// <summary>Number of converted Tekla model elements, with or without geometry (excluding model root).</summary>
    public int ElementCount { get; }

    /// <summary>Number of converted elements that have geometry.</summary>
    public int ElementsWithGeometry { get; }

    /// <summary>Number of converted parts, with or without geometry.</summary>
    public int PartCount { get; }

    /// <summary>Number of unique geometries in the scene.</summary>
    public int GeometryCount => Document.Geometries.Count;
}
