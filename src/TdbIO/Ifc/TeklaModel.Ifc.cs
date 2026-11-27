namespace TdbIO;

/// <summary>
/// The IFC output of <see cref="TeklaModel"/>. Target-format output is kept apart from the native engine: this folder
/// is excluded from the native engine build (<c>BuildNativeCoreOnly</c>), which the scene adapter is compiled against.
/// The regular package keeps these members.
/// </summary>
public sealed partial class TeklaModel
{
    /// <summary>Serializes the decoded model to an IFC STEP physical file string.</summary>
    /// <param name="options">Options controlling IFC export, or <c>null</c> for defaults.</param>
    /// <returns>An IFC STEP string representation of the model.</returns>
    public string ToIfc(TeklaIfcExportOptions? options = null) => Internal.Export.IfcExporter.ToString(this, options);

    /// <summary>Writes the decoded model as an IFC STEP physical file to a stream.</summary>
    /// <param name="stream">The output stream.</param>
    /// <param name="options">Options controlling IFC export, or <c>null</c> for defaults.</param>
    public void WriteIfc(Stream stream, TeklaIfcExportOptions? options = null) => Internal.Export.IfcExporter.Write(this, stream, options);

    /// <summary>Writes the decoded model as an IFC STEP physical file to a file path.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="options">Options controlling IFC export, or <c>null</c> for defaults.</param>
    public void WriteIfc(string path, TeklaIfcExportOptions? options = null) => Internal.Export.IfcExporter.WriteFile(this, path, options);
}
