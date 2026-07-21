using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class OfficialPlayerTagTests
{
    [Theory]
    [InlineData("DRMURLOC #7251", "DRMURLOC#7251")]
    [InlineData("DRMURLOC#7251", "DRMURLOC#7251")]
    [InlineData("  TAG #001  ", "TAG#001")]
    [InlineData("TAG #001", "TAG#001")]
    [InlineData("A B C#42", "ABC#42")]
    public void CollapsesEveryWhitespaceShapeToTheBoardForm(string raw, string expected)
    {
        Assert.Equal(expected, OfficialPlayerTag.Normalize(raw));
    }
}
