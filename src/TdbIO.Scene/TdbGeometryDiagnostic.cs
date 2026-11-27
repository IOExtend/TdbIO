namespace TdbIO.Scene;

/// <summary>What the conversion did with the geometry of one part.</summary>
public enum TdbGeometryDiagnosticKind
{
    /// <summary>The part got a rectangular proxy box around its axis instead of the decoded Tekla solid.</summary>
    ProxyBox,

    /// <summary>The profile of the part was not recognized and the proxy box uses the default dimensions.</summary>
    FallbackDimensions,

    /// <summary>The part has no usable geometry (no endpoints or a degenerate axis) and got no mesh.</summary>
    NoGeometry,
}

/// <summary>One finding about the scene geometry of a part: every mesh is an approximation, and some parts get none.
/// The scene of <see cref="TdbSceneReader"/> is for viewing; it is not made for clash detection or exact measurement.</summary>
/// <param name="Kind">The kind of the finding.</param>
/// <param name="ElementId">The id of the scene element of the part.</param>
/// <param name="SourceId">The source identifier of the part: its GUID, or <c>part:{id}</c> when the GUID is empty.</param>
/// <param name="Profile">The profile name of the part, or an empty string.</param>
/// <param name="Message">The human-readable finding, ready to be written as a warning line.</param>
public sealed record TdbGeometryDiagnostic(
    TdbGeometryDiagnosticKind Kind,
    long ElementId,
    string SourceId,
    string Profile,
    string Message);
