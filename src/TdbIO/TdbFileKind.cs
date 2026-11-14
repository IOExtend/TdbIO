namespace TdbIO;

/// <summary>The kind of a file, found from its content (and, for reports only, from its file name).</summary>
public enum TdbFileKind
{
    /// <summary>Not a recognised file.</summary>
    Unknown,

    /// <summary>A model database: gzip data that unpacks to <c>Xsteel</c> with type byte 0x85 (<c>.db1</c> files and their
    /// <c>.bak</c> copies).</summary>
    Db1,

    /// <summary>A model records file: gzip data that unpacks to <c>Xsteel</c> with type byte 0x80 (<c>.db2</c> files and their
    /// <c>.bak</c> copies).</summary>
    Db2,

    /// <summary>A drawing file: gzip data that unpacks to <c>Xsteel</c> with type byte 0x20. Recognised but not
    /// supported.</summary>
    Drawing,

    /// <summary>An id range file: gzip data that unpacks to <c>IdRangeMapper</c> (<c>.idrm</c> files).</summary>
    IdRanges,

    /// <summary>A GUID map file: gzip data that unpacks to <c>!Guid mapping for </c> (<c>guid.mapper</c>).</summary>
    GuidMapper,

    /// <summary>A DBV@ file: gzip data that unpacks to <c>DBV@</c>. Uncompressed <c>DBV@</c> data is <see cref="Unknown"/>.</summary>
    Dbv,

    /// <summary>A history file: an uncompressed SQLite 3 database (<c>history.db</c>).</summary>
    History,

    /// <summary>A DSTV NC file (<c>.nc1</c>): uncompressed text that starts with the line <c>ST</c>.</summary>
    Nc1,

    /// <summary>A template (<c>.tpl</c>, <c>.rpt</c>): uncompressed text whose first word is <c>template</c>, after an
    /// optional UTF-8 byte order mark and blank lines.</summary>
    Template,

    /// <summary>A report (<c>.xsr</c>). Reports have no signature: a file is a report only when its content is not
    /// recognised and its name ends with <c>.xsr</c>.</summary>
    Report,
}
