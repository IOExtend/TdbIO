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
        int elementCount,
        int elementsWithGeometry)
    {
        Document = document;
        Diagnostics = diagnostics;
        ElementCount = elementCount;
        ElementsWithGeometry = elementsWithGeometry;
    }

    /// <summary>The scene, in metres with Z up.</summary>
    public SceneDocument Document { get; }

    /// <summary>Findings of the conversion.</summary>
    public IReadOnlyList<SceneDiagnostic> Diagnostics { get; }

    /// <summary>The exact factor applied to every coordinate: Tekla millimetres to metres (0.001).</summary>
    public double MetresPerUnit => 0.001;

    /// <summary>Number of converted Tekla model elements, with or without geometry (excluding model root).</summary>
    public int ElementCount { get; }

    /// <summary>Number of converted elements that have geometry.</summary>
    public int ElementsWithGeometry { get; }

    /// <summary>Number of unique geometries in the scene.</summary>
    public int GeometryCount => Document.Geometries.Count;
}
