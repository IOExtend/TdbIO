namespace TdbIO.Internal;

/// <summary>CRC-32 as used by gzip (IEEE polynomial, reflected). Written here to avoid a package dependency.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
