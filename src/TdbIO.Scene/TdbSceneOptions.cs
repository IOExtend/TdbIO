namespace TdbIO.Scene;

/// <summary>
/// Options for <see cref="TdbSceneReader"/>.
/// </summary>
public sealed class TdbSceneOptions
{
    /// <summary>
    /// The key of the scene model (<c>SceneModel.Key</c>), which with each element's <c>SourceId</c> forms the identity
    /// that may be stored outside the scene. Null uses the file name without directory, for example <c>model.db1</c>.
    /// Required when the model is converted from memory, where there is no file name. An empty key is invalid, and
    /// <see cref="TdbSceneReader"/> throws <see cref="ArgumentException"/> for it.
    /// </summary>
    public string? ModelKey { get; set; }

    /// <summary>
    /// Whether to include bolt groups in the scene hierarchy. Default is true.
    /// </summary>
    public bool IncludeBolts { get; set; } = true;

    /// <summary>
    /// Whether to include welds in the scene hierarchy. Default is true.
    /// </summary>
    public bool IncludeWelds { get; set; } = true;

    /// <summary>
    /// Whether to generate 3D mesh geometry for parts with start and end points. Default is true.
    /// </summary>
    public bool GenerateGeometry { get; set; } = true;
}
