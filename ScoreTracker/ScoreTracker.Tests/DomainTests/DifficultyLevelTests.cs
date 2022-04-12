using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class DifficultyLevelTests
{
    [Fact]
    public void DifficultyLowerThan1ThrowsException()
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.From(0));
    }

    [Fact]
    public void DifficultyGreaterThan28ThrowsException()
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.From(29));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(28)]
    [InlineData(14)]
    public void DifficultyBetween1And28IsFine(int level)
    {
        var difficultyLevel = DifficultyLevel.From(level);
        Assert.Equal(level, (int)difficultyLevel);
    }
}