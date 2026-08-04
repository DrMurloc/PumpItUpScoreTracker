using System.Linq;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RivalInviteCodeTests
{
    [Theory]
    [InlineData("K7QF-2M9X-BR4T")]
    [InlineData("k7qf-2m9x-br4t")]
    [InlineData("K7QF2M9XBR4T")]
    [InlineData("  K7QF 2M9X BR4T  ")]
    public void ParsingIsForgivingButTheStoredFormIsCanonical(string typed)
    {
        Assert.Equal("K7QF-2M9X-BR4T", RivalInviteCode.From(typed).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("K7QF-2M9X")]
    [InlineData("K7QF-2M9X-BR4TX")]
    public void AWrongLengthIsRejected(string? typed)
    {
        Assert.Throws<InvalidRivalInviteCodeException>(() => RivalInviteCode.From(typed));
    }

    /// <summary>
    ///     I, O, 0 and 1 are the four characters a person transcribes wrong, so no code contains
    ///     them — and a code that appears to is a misread, not a code.
    /// </summary>
    [Theory]
    [InlineData("I7QF-2M9X-BR4T")]
    [InlineData("O7QF-2M9X-BR4T")]
    [InlineData("07QF-2M9X-BR4T")]
    [InlineData("17QF-2M9X-BR4T")]
    public void TheConfusableCharactersAreRejected(string typed)
    {
        Assert.Throws<InvalidRivalInviteCodeException>(() => RivalInviteCode.From(typed));
    }

    [Fact]
    public void TryParseReportsRatherThanThrows()
    {
        Assert.False(RivalInviteCode.TryParse("nope", out _));
        Assert.True(RivalInviteCode.TryParse("K7QF-2M9X-BR4T", out var parsed));
        Assert.Equal("K7QF-2M9X-BR4T", parsed.ToString());
    }

    [Fact]
    public void AGeneratedCodeParsesBackAsItself()
    {
        // A fixed draw: every position takes the same alphabet index, so the shape is what's
        // under test rather than the randomness.
        var random = new Mock<IRandomNumberGenerator>();
        random.Setup(r => r.Next(It.IsAny<int>())).Returns(5);

        var code = RivalInviteCode.Generate(random.Object);

        Assert.Equal(code.ToString(), RivalInviteCode.From(code.ToString()).ToString());
        Assert.Equal(14, code.ToString().Length);
        Assert.Equal(2, code.ToString().Count(c => c == '-'));
    }
}
