namespace TdbIO.Tests;

internal static class InvalidDataExceptionExtensions
{
    extension(InvalidDataException e)
    {
        /// <summary>The offset the library stores in <c>Data["Offset"]</c>; a missing or mistyped value fails the test.</summary>
        public long Offset => e.Data["Offset"] is long offset ? offset : throw new Xunit.Sdk.XunitException($"no offset in: {e.Message}");
    }
}
