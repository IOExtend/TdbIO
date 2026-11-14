using System.Text;

namespace TdbIO;

/// <summary>Options for <see cref="TdbFile.Open(string, TdbReadOptions?)"/>.</summary>
public sealed class TdbReadOptions
{
    private const long DefaultMaxUnpackedLength = 1L << 30;

    private readonly long _maxUnpackedLength = DefaultMaxUnpackedLength;

    /// <summary>A new instance with the default values.</summary>
    public static TdbReadOptions Default => new();

    /// <summary>The largest number of bytes read into memory: the unpacked size of gzip data, or the size of an
    /// uncompressed file. Larger input is rejected with <see cref="InvalidDataException"/> instead of being cut. The default
    /// is 1 GiB; the valid range is 1 to <see cref="Array.MaxLength"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is out of range.</exception>
    public long MaxUnpackedLength
    {
        get => _maxUnpackedLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1L);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (long)Array.MaxLength);
            _maxUnpackedLength = value;
        }
    }

    /// <summary>The encoding of templates and reports (<c>.tpl</c>, <c>.rpt</c>, <c>.xsr</c>), used as given. When null
    /// (the default) the encoding is chosen from the bytes: UTF-8 after a byte order mark, ASCII, UTF-8, then code page
    /// 1251 or 1252.</summary>
    public Encoding? TextEncoding { get; init; }
}
