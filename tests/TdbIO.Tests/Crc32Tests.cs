using TdbIO.Internal;
using Xunit;

namespace TdbIO.Tests;

public class Crc32Tests
{
    [Fact]
    public void TheCheckValueOfTheDigitsMatches() => Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    [Fact]
    public void EmptyDataIsZero() => Assert.Equal(0u, Crc32.Compute([]));
}
