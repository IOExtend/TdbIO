namespace TdbIO;

/// <summary>
/// Options controlling IFC STEP physical file export from <see cref="TeklaModel"/>.
/// </summary>
public sealed class TeklaIfcExportOptions
{
    /// <summary>The IFC schema version to export (e.g. "IFC2X3" or "IFC4"). Default is "IFC2X3".</summary>
    public string SchemaVersion { get; set; } = "IFC2X3";

    /// <summary>The IFC project name, or null to derive from model or default.</summary>
    public string? ProjectName { get; set; }

    /// <summary>The IFC building name.</summary>
    public string BuildingName { get; set; } = "Tekla Building";

    /// <summary>The IFC building storey name.</summary>
    public string StoreyName { get; set; } = "Level 1";

    /// <summary>The name of the producing application. Default is "TdbIO".</summary>
    public string Application { get; set; } = "TdbIO";

    /// <summary>The organization name. Default is "IOExtend".</summary>
    public string Organization { get; set; } = "IOExtend";

    /// <summary>The author name.</summary>
    public string Author { get; set; } = "";

    /// <summary>Whether to export Tekla user-defined and numeric properties as IFC property sets. Default is true.</summary>
    public bool ExportProperties { get; set; } = true;

    /// <summary>Whether to group parts under their assemblies (IFCELEMENTASSEMBLY). Default is true.</summary>
    public bool ExportAssemblies { get; set; } = true;

    /// <summary>Whether to export bolt groups as IFCMECHANICALFASTENER. Default is true.</summary>
    public bool ExportBolts { get; set; } = true;

    /// <summary>Whether to export welds as IFCFASTENER. Default is true.</summary>
    public bool ExportWelds { get; set; } = true;

    /// <summary>Whether to export part geometry as IFC faceted BREPs. Default is true.</summary>
    public bool ExportGeometry { get; set; } = true;

    /// <summary>Whether to convert Tekla model millimeters to IFC standard metres (0.001 scale factor). Default is true.</summary>
    public bool ConvertMmToMetres { get; set; } = true;
}
