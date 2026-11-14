using TdbIO.Internal;

namespace TdbIO;

/// <summary>Decoded row of table 1 (<c>object</c>, size 72): object hierarchy, GUID, type, and status flags.</summary>
/// <param name="Id">The unique object identifier (primary key).</param>
/// <param name="ParentId">The parent object identifier (<c>kuuluu</c>).</param>
/// <param name="AssemblyId">The assembly identifier the object belongs to.</param>
/// <param name="Guid">The 128-bit RFC 4122 GUID of the object.</param>
/// <param name="Type">The object type code (e.g. 1 for part, 2 for assembly).</param>
/// <param name="Subtype">The object subtype code.</param>
/// <param name="ExternalType">The external type GUID.</param>
/// <param name="Mask1">The first mask word.</param>
/// <param name="Mask2">The second mask word.</param>
/// <param name="ObjFlag">The object status flags.</param>
/// <param name="OwnerId">The owner identifier.</param>
/// <param name="PrivilegeId">The privilege or access control identifier.</param>
public readonly record struct ObjectRow(
    int Id,
    int ParentId,
    int AssemblyId,
    Guid Guid,
    int Type,
    int Subtype,
    Guid ExternalType,
    int Mask1,
    int Mask2,
    int ObjFlag,
    int OwnerId,
    int PrivilegeId)
{
    /// <summary>The expected row body size in bytes: 72.</summary>
    public const int ExpectedRowSize = 72;

    /// <summary>The table key in model database sections: 1.</summary>
    public const int TableKey = 1;

    /// <summary>Decodes a row body into an <see cref="ObjectRow"/>.</summary>
    public static ObjectRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.Guid(body, 12),
            Bytes.I32(body, 28),
            Bytes.I32(body, 32),
            Bytes.Guid(body, 36),
            Bytes.I32(body, 52),
            Bytes.I32(body, 56),
            Bytes.I32(body, 60),
            Bytes.I32(body, 64),
            Bytes.I32(body, 68));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="ObjectRow"/>.</summary>
    public static ObjectRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 2 (<c>assembly</c>, size 92): assembly definition and main part reference.</summary>
/// <param name="Id">The assembly identifier (primary key).</param>
/// <param name="Type">The assembly type code.</param>
/// <param name="Class">The assembly class code.</param>
/// <param name="MainPartId">The identifier of the main part (<c>dum</c>).</param>
/// <param name="Lot">The lot/tracking number.</param>
/// <param name="Name">The assembly name (e.g. "BEAM", "COLUMN", "SLAB").</param>
/// <param name="Flags">Assembly flags.</param>
/// <param name="MainPartByUser">Whether the main part was set explicitly by the user.</param>
public readonly record struct AssemblyRow(
    int Id,
    int Type,
    int Class,
    int MainPartId,
    int Lot,
    string Name,
    int Flags,
    int MainPartByUser)
{
    /// <summary>The expected row body size in bytes: 92.</summary>
    public const int ExpectedRowSize = 92;

    /// <summary>The table key in model database sections: 2.</summary>
    public const int TableKey = 2;

    /// <summary>Decodes a row body into an <see cref="AssemblyRow"/>.</summary>
    public static AssemblyRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.AsciiString(body, 20, 64),
            Bytes.I32(body, 84),
            Bytes.I32(body, 88));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="AssemblyRow"/>.</summary>
    public static AssemblyRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 3 (<c>coordsys</c>, size 40): coordinate system origin and length.</summary>
/// <param name="Id">The coordinate system identifier (primary key).</param>
/// <param name="AttrId">The coordinate system attribute identifier.</param>
/// <param name="X">Origin X coordinate.</param>
/// <param name="Y">Origin Y coordinate.</param>
/// <param name="Z">Origin Z coordinate.</param>
/// <param name="Length">Coordinate system length or scale factor.</param>
public readonly record struct CoordsysRow(int Id, int AttrId, double X, double Y, double Z, double Length)
{
    /// <summary>The expected row body size in bytes: 40.</summary>
    public const int ExpectedRowSize = 40;

    /// <summary>The table key in model database sections: 3.</summary>
    public const int TableKey = 3;

    /// <summary>Decodes a row body into a <see cref="CoordsysRow"/>.</summary>
    public static CoordsysRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.F64(body, 8),
            Bytes.F64(body, 16),
            Bytes.F64(body, 24),
            Bytes.F64(body, 32));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="CoordsysRow"/>.</summary>
    public static CoordsysRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 10 (<c>part</c>, size 64): part reference points and local coordinate system.</summary>
/// <param name="Id">The part identifier (primary key).</param>
/// <param name="PartAttrId">The attribute identifier in table 44 (<c>part_attr</c>).</param>
/// <param name="PositioningAttrId">The positioning attribute identifier.</param>
/// <param name="StartPointId">The start point identifier in table 13 (<c>point</c>).</param>
/// <param name="EndPointId">The end point identifier in table 13 (<c>point</c>).</param>
/// <param name="GeometryTreeRootId">The geometry tree root node identifier.</param>
/// <param name="CoordsysAttrId">The coordinate system attribute identifier.</param>
/// <param name="Reserved">Reserved integer field.</param>
/// <param name="CoordsysX">Local coordinate system origin X.</param>
/// <param name="CoordsysY">Local coordinate system origin Y.</param>
/// <param name="CoordsysZ">Local coordinate system origin Z.</param>
/// <param name="Length">Length of the part.</param>
public readonly record struct PartRow(
    int Id,
    int PartAttrId,
    int PositioningAttrId,
    int StartPointId,
    int EndPointId,
    int GeometryTreeRootId,
    int CoordsysAttrId,
    int Reserved,
    double CoordsysX,
    double CoordsysY,
    double CoordsysZ,
    double Length)
{
    /// <summary>The expected row body size in bytes: 64.</summary>
    public const int ExpectedRowSize = 64;

    /// <summary>The table key in model database sections: 10.</summary>
    public const int TableKey = 10;

    /// <summary>Decodes a row body into a <see cref="PartRow"/>.</summary>
    public static PartRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20),
            Bytes.I32(body, 24),
            Bytes.I32(body, 28),
            Bytes.F64(body, 32),
            Bytes.F64(body, 40),
            Bytes.F64(body, 48),
            Bytes.F64(body, 56));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="PartRow"/>.</summary>
    public static PartRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 13 (<c>point</c>, size 32): 3D coordinates and visibility.</summary>
/// <param name="Id">The point identifier (primary key).</param>
/// <param name="Visibility">Point visibility flags.</param>
/// <param name="X">3D X coordinate in millimeters.</param>
/// <param name="Y">3D Y coordinate in millimeters.</param>
/// <param name="Z">3D Z coordinate in millimeters.</param>
public readonly record struct PointRow(int Id, int Visibility, double X, double Y, double Z)
{
    /// <summary>The expected row body size in bytes: 32.</summary>
    public const int ExpectedRowSize = 32;

    /// <summary>The table key in model database sections: 13.</summary>
    public const int TableKey = 13;

    /// <summary>Decodes a row body into a <see cref="PointRow"/>.</summary>
    public static PointRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.F64(body, 8),
            Bytes.F64(body, 16),
            Bytes.F64(body, 24));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="PointRow"/>.</summary>
    public static PointRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 16 (<c>relation</c>, size 60): relationship between two objects.</summary>
/// <param name="Id">The relation identifier (primary key).</param>
/// <param name="Type">The relation type code.</param>
/// <param name="Id1">The first related object identifier.</param>
/// <param name="Id2">The second related object identifier.</param>
/// <param name="Guid">The relation GUID string.</param>
/// <param name="Flags">Relation flags.</param>
public readonly record struct RelationRow(int Id, int Type, int Id1, int Id2, string Guid, int Flags)
{
    /// <summary>The expected row body size in bytes: 60.</summary>
    public const int ExpectedRowSize = 60;

    /// <summary>The table key in model database sections: 16.</summary>
    public const int TableKey = 16;

    /// <summary>Decodes a row body into a <see cref="RelationRow"/>.</summary>
    public static RelationRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.AsciiString(body, 16, 40),
            Bytes.I32(body, 56));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="RelationRow"/>.</summary>
    public static RelationRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 20 (<c>welding</c>, size 24): weld connection and seam references.</summary>
/// <param name="Id">The weld identifier (primary key).</param>
/// <param name="CommonAttrId">Identifier of common weld attributes in table 98 (<c>welding_common_attr</c>).</param>
/// <param name="Seam1Id">Identifier of seam 1 in table 46 (<c>welding_attr</c>).</param>
/// <param name="Seam2Id">Identifier of seam 2 in table 46 (<c>welding_attr</c>).</param>
/// <param name="CompoundSeam1Id">Compound seam 1 identifier.</param>
/// <param name="CompoundSeam2Id">Compound seam 2 identifier.</param>
public readonly record struct WeldingRow(int Id, int CommonAttrId, int Seam1Id, int Seam2Id, int CompoundSeam1Id, int CompoundSeam2Id)
{
    /// <summary>The expected row body size in bytes: 24.</summary>
    public const int ExpectedRowSize = 24;

    /// <summary>The table key in model database sections: 20.</summary>
    public const int TableKey = 20;

    /// <summary>Decodes a row body into a <see cref="WeldingRow"/>.</summary>
    public static WeldingRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="WeldingRow"/>.</summary>
    public static WeldingRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 38 (<c>numattr</c>, size 24): numeric attribute association.</summary>
/// <param name="Id">The numeric attribute row identifier (primary key).</param>
/// <param name="AttrId">Identifier of the attribute value in table 48 (<c>numattr_attr</c>).</param>
/// <param name="FatherId">Identifier of the object owning the attribute.</param>
/// <param name="OwnerId">Attribute owner identifier.</param>
/// <param name="Flags">Attribute flags.</param>
/// <param name="NameId">Identifier of the attribute name.</param>
public readonly record struct NumAttrRow(int Id, int AttrId, int FatherId, int OwnerId, int Flags, int NameId)
{
    /// <summary>The expected row body size in bytes: 24.</summary>
    public const int ExpectedRowSize = 24;

    /// <summary>The table key in model database sections: 38.</summary>
    public const int TableKey = 38;

    /// <summary>Decodes a row body into a <see cref="NumAttrRow"/>.</summary>
    public static NumAttrRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="NumAttrRow"/>.</summary>
    public static NumAttrRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 39 (<c>strattr</c>, size 24): string attribute association.</summary>
/// <param name="Id">The string attribute row identifier (primary key).</param>
/// <param name="AttrId">Identifier of the attribute value in table 49 (<c>strattr_attr</c>).</param>
/// <param name="FatherId">Identifier of the object owning the attribute.</param>
/// <param name="OwnerId">Attribute owner identifier.</param>
/// <param name="Flags">Attribute flags.</param>
/// <param name="NameId">Identifier of the attribute name.</param>
public readonly record struct StrAttrRow(int Id, int AttrId, int FatherId, int OwnerId, int Flags, int NameId)
{
    /// <summary>The expected row body size in bytes: 24.</summary>
    public const int ExpectedRowSize = 24;

    /// <summary>The table key in model database sections: 39.</summary>
    public const int TableKey = 39;

    /// <summary>Decodes a row body into a <see cref="StrAttrRow"/>.</summary>
    public static StrAttrRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="StrAttrRow"/>.</summary>
    public static StrAttrRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 44 (<c>part_attr</c>, size 372): part profile, material, finish, and classification.</summary>
/// <param name="Id">The part attribute identifier (primary key).</param>
/// <param name="ObjType">Object type code.</param>
/// <param name="FormType">Form type code.</param>
/// <param name="ObjClass">Part class number (color/display class).</param>
/// <param name="AssStartNo">Assembly start number.</param>
/// <param name="Flags">Attribute flags.</param>
/// <param name="NPoints">Number of points.</param>
/// <param name="PartFlags">Additional part flags.</param>
/// <param name="Prefix">Part numbering prefix / group.</param>
/// <param name="Name">Part name (e.g. "BEAM", "COLUMN", "PLATE").</param>
/// <param name="Profile">Part profile name (e.g. "IPE300", "HEA200", "PL10*100").</param>
/// <param name="ParameterStringId">Identifier in the string table for parameter strings.</param>
/// <param name="AssemblyPosition">Assembly position prefix.</param>
/// <param name="AssemblyName">Assembly name.</param>
/// <param name="Material">Material grade (e.g. "S235JR", "S355J2").</param>
/// <param name="Finish">Part surface finish.</param>
/// <param name="AssemblyType">Assembly type code.</param>
/// <param name="PourPhase">Pour phase number.</param>
public readonly record struct PartAttrRow(
    int Id,
    int ObjType,
    int FormType,
    int ObjClass,
    int AssStartNo,
    int Flags,
    int NPoints,
    int PartFlags,
    string Prefix,
    string Name,
    string Profile,
    int ParameterStringId,
    string AssemblyPosition,
    string AssemblyName,
    string Material,
    string Finish,
    int AssemblyType,
    int PourPhase)
{
    /// <summary>The expected row body size in bytes: 372.</summary>
    public const int ExpectedRowSize = 372;

    /// <summary>The table key in model database sections: 44.</summary>
    public const int TableKey = 44;

    /// <summary>Decodes a row body into a <see cref="PartAttrRow"/>.</summary>
    public static PartAttrRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20),
            Bytes.I32(body, 24),
            Bytes.I32(body, 28),
            Bytes.AsciiString(body, 32, 22),
            Bytes.AsciiString(body, 54, 62),
            Bytes.AsciiString(body, 116, 62),
            Bytes.I32(body, 180),
            Bytes.AsciiString(body, 184, 22),
            Bytes.AsciiString(body, 206, 62),
            Bytes.AsciiString(body, 268, 32),
            Bytes.AsciiString(body, 300, 62),
            Bytes.I32(body, 364),
            Bytes.I32(body, 368));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="PartAttrRow"/>.</summary>
    public static PartAttrRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 48 (<c>numattr_attr</c>, size 44): numeric attribute definition and value.</summary>
/// <param name="Id">The attribute value identifier (primary key).</param>
/// <param name="Type">Value type code.</param>
/// <param name="Value">The numeric attribute value.</param>
/// <param name="Name">The attribute name.</param>
/// <param name="PrivilegeId">Access privilege identifier.</param>
public readonly record struct NumAttrAttrRow(int Id, int Type, double Value, string Name, int PrivilegeId)
{
    /// <summary>The expected row body size in bytes: 44.</summary>
    public const int ExpectedRowSize = 44;

    /// <summary>The table key in model database sections: 48.</summary>
    public const int TableKey = 48;

    /// <summary>Decodes a row body into a <see cref="NumAttrAttrRow"/>.</summary>
    public static NumAttrAttrRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.F64(body, 8),
            Bytes.AsciiString(body, 16, 21),
            Bytes.I32(body, 40));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="NumAttrAttrRow"/>.</summary>
    public static NumAttrAttrRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 49 (<c>strattr_attr</c>, size 120): string attribute definition and value.</summary>
/// <param name="Id">The attribute value identifier (primary key).</param>
/// <param name="Type">Value type code.</param>
/// <param name="Name">The attribute name.</param>
/// <param name="Value">The string attribute value.</param>
/// <param name="ValueId">Extended value identifier.</param>
/// <param name="PrivilegeId">Access privilege identifier.</param>
public readonly record struct StrAttrAttrRow(int Id, int Type, string Name, string Value, int ValueId, int PrivilegeId)
{
    /// <summary>The expected row body size in bytes: 120.</summary>
    public const int ExpectedRowSize = 120;

    /// <summary>The table key in model database sections: 49.</summary>
    public const int TableKey = 49;

    /// <summary>Decodes a row body into a <see cref="StrAttrAttrRow"/>.</summary>
    public static StrAttrAttrRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.AsciiString(body, 8, 21),
            Bytes.AsciiString(body, 29, 81),
            Bytes.I32(body, 112),
            Bytes.I32(body, 116));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="StrAttrAttrRow"/>.</summary>
    public static StrAttrAttrRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 50 (<c>string</c>, size 45): string pool chunk.</summary>
/// <param name="Id">The string chunk identifier (primary key).</param>
/// <param name="NextId">Identifier of the next chunk in a chained string, or negative if terminating.</param>
/// <param name="HashId">Hash code of the string.</param>
/// <param name="RefCount">Reference count.</param>
/// <param name="Text">The text content of this chunk.</param>
public readonly record struct StringPoolRow(int Id, int NextId, int HashId, int RefCount, string Text)
{
    /// <summary>The expected row body size in bytes: 45.</summary>
    public const int ExpectedRowSize = 45;

    /// <summary>The table key in model database sections: 50.</summary>
    public const int TableKey = 50;

    /// <summary>Decodes a row body into a <see cref="StringPoolRow"/>.</summary>
    public static StringPoolRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.AsciiString(body, 16, 29));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="StringPoolRow"/>.</summary>
    public static StringPoolRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 94 (<c>part_numbering</c>, size 68): part numbering series and mark prefix.</summary>
/// <param name="Id">The numbering identifier (primary key).</param>
/// <param name="SeriesId">Numbering series identifier.</param>
/// <param name="StartNumber">Start number of the numbering series.</param>
/// <param name="FPos">Fabrication position number.</param>
/// <param name="MultiFPos">Multi-position number.</param>
/// <param name="Prefix">Part mark prefix string.</param>
public readonly record struct PartNumberingRow(int Id, int SeriesId, int StartNumber, int FPos, int MultiFPos, string Prefix)
{
    /// <summary>The expected row body size in bytes: 68.</summary>
    public const int ExpectedRowSize = 68;

    /// <summary>The table key in model database sections: 94.</summary>
    public const int TableKey = 94;

    /// <summary>Decodes a row body into a <see cref="PartNumberingRow"/>.</summary>
    public static PartNumberingRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.AsciiString(body, 20, 48));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="PartNumberingRow"/>.</summary>
    public static PartNumberingRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 95 (<c>assembly_numbering</c>, size 76): assembly numbering series and mark prefix.</summary>
/// <param name="Id">The numbering identifier (primary key).</param>
/// <param name="SeriesId">Numbering series identifier.</param>
/// <param name="StartNumber">Start number of the numbering series.</param>
/// <param name="FPos">Assembly position number.</param>
/// <param name="FamilyFPos">Family position number.</param>
/// <param name="QualifierFPos">Qualifier position number.</param>
/// <param name="MultiFPos">Multi-position number.</param>
/// <param name="Prefix">Assembly mark prefix string.</param>
public readonly record struct AssemblyNumberingRow(
    int Id,
    int SeriesId,
    int StartNumber,
    int FPos,
    int FamilyFPos,
    int QualifierFPos,
    int MultiFPos,
    string Prefix)
{
    /// <summary>The expected row body size in bytes: 76.</summary>
    public const int ExpectedRowSize = 76;

    /// <summary>The table key in model database sections: 95.</summary>
    public const int TableKey = 95;

    /// <summary>Decodes a row body into an <see cref="AssemblyNumberingRow"/>.</summary>
    public static AssemblyNumberingRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20),
            Bytes.I32(body, 24),
            Bytes.AsciiString(body, 28, 48));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="AssemblyNumberingRow"/>.</summary>
    public static AssemblyNumberingRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 105 (<c>object_numbering</c>, size 12): object numbering association.</summary>
/// <param name="Id">The object identifier (primary key).</param>
/// <param name="NumberId">Numbering entry identifier.</param>
/// <param name="NumberingGroup">Numbering group code.</param>
public readonly record struct ObjectNumberingRow(int Id, int NumberId, int NumberingGroup)
{
    /// <summary>The expected row body size in bytes: 12.</summary>
    public const int ExpectedRowSize = 12;

    /// <summary>The table key in model database sections: 105.</summary>
    public const int TableKey = 105;

    /// <summary>Decodes a row body into an <see cref="ObjectNumberingRow"/>.</summary>
    public static ObjectNumberingRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="ObjectNumberingRow"/>.</summary>
    public static ObjectNumberingRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 107 (<c>bolt</c>, size 24): bolt group definition and reference points.</summary>
/// <param name="Id">The bolt object identifier (primary key).</param>
/// <param name="BoltAttrId">Identifier in table 108 (<c>bolt_attr</c>).</param>
/// <param name="PositioningAttrId">Positioning attribute identifier.</param>
/// <param name="FirstPointId">First reference point identifier in table 13 (<c>point</c>).</param>
/// <param name="SecondPointId">Second reference point identifier in table 13 (<c>point</c>).</param>
/// <param name="PolygonId">Polygon shape identifier.</param>
public readonly record struct BoltRow(int Id, int BoltAttrId, int PositioningAttrId, int FirstPointId, int SecondPointId, int PolygonId)
{
    /// <summary>The expected row body size in bytes: 24.</summary>
    public const int ExpectedRowSize = 24;

    /// <summary>The table key in model database sections: 107.</summary>
    public const int TableKey = 107;

    /// <summary>Decodes a row body into a <see cref="BoltRow"/>.</summary>
    public static BoltRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16),
            Bytes.I32(body, 20));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="BoltRow"/>.</summary>
    public static BoltRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 132 (<c>object_modification</c>, size 20): object modification timestamp and dependency.</summary>
/// <param name="ModificationId">The modification event identifier (primary key).</param>
/// <param name="Id">The object identifier that was modified.</param>
/// <param name="ModType">The modification type code.</param>
/// <param name="DependencyId">The dependency object identifier.</param>
/// <param name="ModTime">The modification timestamp identifier.</param>
public readonly record struct ObjectModificationRow(int ModificationId, int Id, int ModType, int DependencyId, int ModTime)
{
    /// <summary>The expected row body size in bytes: 20.</summary>
    public const int ExpectedRowSize = 20;

    /// <summary>The table key in model database sections: 132.</summary>
    public const int TableKey = 132;

    /// <summary>Decodes a row body into an <see cref="ObjectModificationRow"/>.</summary>
    public static ObjectModificationRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.I32(body, 12),
            Bytes.I32(body, 16));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="ObjectModificationRow"/>.</summary>
    public static ObjectModificationRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 140 (<c>bolt_hole</c>, size 48): bolt hole through a connected part.</summary>
/// <param name="BoltId">The bolt group identifier.</param>
/// <param name="No">The hole index in the bolt group.</param>
/// <param name="PartId">The part identifier through which this hole passes.</param>
/// <param name="PointZ">The Z offset of the hole along the bolt axis.</param>
/// <param name="Length">The grip length of the hole in this part.</param>
/// <param name="LongHoleDir">Slotted hole direction.</param>
/// <param name="LongHoleType">Slotted hole type.</param>
/// <param name="ShorteningLength">Hole shortening length.</param>
public readonly record struct BoltHoleRow(
    int BoltId,
    int No,
    int PartId,
    double PointZ,
    double Length,
    int LongHoleDir,
    int LongHoleType,
    double ShorteningLength)
{
    /// <summary>The expected row body size in bytes: 48.</summary>
    public const int ExpectedRowSize = 48;

    /// <summary>The table key in model database sections: 140.</summary>
    public const int TableKey = 140;

    /// <summary>Decodes a row body into a <see cref="BoltHoleRow"/>.</summary>
    public static BoltHoleRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4),
            Bytes.I32(body, 8),
            Bytes.F64(body, 16),
            Bytes.F64(body, 24),
            Bytes.I32(body, 32),
            Bytes.I32(body, 36),
            Bytes.F64(body, 40));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="BoltHoleRow"/>.</summary>
    public static BoltHoleRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 141 (<c>object_phase</c>, size 8): project phase assignment.</summary>
/// <param name="ObjectId">The object identifier.</param>
/// <param name="PhaseNumber">The phase number.</param>
public readonly record struct ObjectPhaseRow(int ObjectId, int PhaseNumber)
{
    /// <summary>The expected row body size in bytes: 8.</summary>
    public const int ExpectedRowSize = 8;

    /// <summary>The table key in model database sections: 141.</summary>
    public const int TableKey = 141;

    /// <summary>Decodes a row body into an <see cref="ObjectPhaseRow"/>.</summary>
    public static ObjectPhaseRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.I32(body, 4));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into an <see cref="ObjectPhaseRow"/>.</summary>
    public static ObjectPhaseRow Decode(TableRow row) => Decode(row.Body.Span);
}

/// <summary>Decoded row of table 142 (<c>bolt_hole_points</c>, size 60): intermediate bolt pattern coordinates.</summary>
/// <param name="BoltId">The bolt group identifier.</param>
/// <param name="IntPoint1X">First intermediate coordinate X.</param>
/// <param name="IntPoint1Y">First intermediate coordinate Y.</param>
/// <param name="IntPoint1Z">First intermediate coordinate Z.</param>
/// <param name="IntPoint2X">Second intermediate coordinate X.</param>
/// <param name="IntPoint2Y">Second intermediate coordinate Y.</param>
/// <param name="IntPoint2Z">Second intermediate coordinate Z.</param>
/// <param name="NumHoles">Number of holes in the pattern.</param>
public readonly record struct BoltHolePointsRow(
    int BoltId,
    double IntPoint1X,
    double IntPoint1Y,
    double IntPoint1Z,
    double IntPoint2X,
    double IntPoint2Y,
    double IntPoint2Z,
    int NumHoles)
{
    /// <summary>The expected row body size in bytes: 60.</summary>
    public const int ExpectedRowSize = 60;

    /// <summary>The table key in model database sections: 142.</summary>
    public const int TableKey = 142;

    /// <summary>Decodes a row body into a <see cref="BoltHolePointsRow"/>.</summary>
    public static BoltHolePointsRow Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < ExpectedRowSize)
            throw new ArgumentException($"Row body length {body.Length} is smaller than expected size {ExpectedRowSize}.", nameof(body));
        return new(
            Bytes.I32(body, 0),
            Bytes.F64(body, 8),
            Bytes.F64(body, 16),
            Bytes.F64(body, 24),
            Bytes.F64(body, 32),
            Bytes.F64(body, 40),
            Bytes.F64(body, 48),
            Bytes.I32(body, 56));
    }

    /// <summary>Decodes the body of <paramref name="row"/> into a <see cref="BoltHolePointsRow"/>.</summary>
    public static BoltHolePointsRow Decode(TableRow row) => Decode(row.Body.Span);
}
