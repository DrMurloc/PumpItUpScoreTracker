using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RecapPlayerTypeCalculatorTests
{
    [Fact]
    public void FewerThanTenScoresProducesNoType()
    {
        var scores = Enumerable.Repeat((PhoenixScore)999_000, 9).ToArray();

        Assert.Null(RecapPlayerTypeCalculator.Calculate(scores));
    }

    [Fact]
    public void TenScoresIsEnoughForAType()
    {
        var scores = Enumerable.Repeat((PhoenixScore)999_000, 10).ToArray();

        Assert.Equal(RecapPlayerType.Perfectionist, RecapPlayerTypeCalculator.Calculate(scores));
    }

    [Theory]
    [InlineData(949_999, RecapPlayerType.PassPusher)]
    [InlineData(950_000, RecapPlayerType.PassRefiner)]
    [InlineData(969_999, RecapPlayerType.PassRefiner)]
    [InlineData(970_000, RecapPlayerType.BalancedPlayer)]
    [InlineData(979_999, RecapPlayerType.BalancedPlayer)]
    [InlineData(980_000, RecapPlayerType.Competitive)]
    [InlineData(994_999, RecapPlayerType.Competitive)]
    [InlineData(995_000, RecapPlayerType.Perfectionist)]
    public void AverageScoreBandsMapToLetterGradeTypes(int score, RecapPlayerType expected)
    {
        var scores = Enumerable.Repeat((PhoenixScore)score, 10).ToArray();

        Assert.Equal(expected, RecapPlayerTypeCalculator.Calculate(scores));
    }

    [Fact]
    public void TypeUsesTheAverageAcrossScores()
    {
        var scores = new PhoenixScore[] { 940_000, 960_000, 940_000, 960_000, 940_000, 960_000, 940_000, 960_000, 940_000, 960_000 };

        Assert.Equal(RecapPlayerType.PassRefiner, RecapPlayerTypeCalculator.Calculate(scores));
    }
}
