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
    public void DifficultyGreaterThan29ThrowsException()
    {
        Assert.Throws<InvalidDifficultyLevelException>(() => DifficultyLevel.From(30));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(14)]
    public void DifficultyBetween1And29IsFine(int level)
    {
        var difficultyLevel = DifficultyLevel.From(level);
        Assert.Equal(level, (int)difficultyLevel);
    }
}