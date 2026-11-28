namespace TdbIO.Scene;

/// <summary>What the conversion did with the geometry of one part.</summary>
public enum TdbGeometryDiagnosticKind
{
    /// <summary>No dimensions were read from the profile name and the proxy box of the part uses the default ones. A part
    /// without this finding has dimensions estimated from the name, which are not a check of the profile.</summary>
    FallbackDimensions,

    /// <summary>The part has no usable geometry (no endpoints or a degenerate axis) and got no mesh.</summary>
    NoGeometry,
}

/// <summary>One finding about the scene geometry of a part. Every mesh is a rectangular proxy box around the part axis,
/// not the decoded Tekla solid (<see cref="TdbSceneResult.ElementsWithGeometry"/> counts them, so they have no findings
/// of their own); some boxes have default dimensions and some parts get no mesh. The scene of <see cref="TdbSceneReader"/> is for viewing; it is not made for clash detection or exact measurement.</summary>
/// <param name="Kind">The kind of the finding.</param>
/// <param name="ElementId">The id of the scene element of the part.</param>
/// <param name="SourceId">The source identifier of the part: its GUID, or <c>part:{id}</c> when the GUID is empty.</param>
/// <param name="Profile">The profile name of the part, or an empty string.</param>
/// <param name="Message">The human-readable finding, ready to be written as a warning line. It names the part by
/// <paramref name="SourceId"/> and holds no text from the file.</param>
public sealed record TdbGeometryDiagnostic(
    TdbGeometryDiagnosticKind Kind,
    long ElementId,
    string SourceId,
    string Profile,
    string Message);
