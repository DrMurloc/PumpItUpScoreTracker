using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The top of a ladder reaches for the perfect game: SSS+ on a Phoenix mix, SSS on Rise, whose
///     ladder has no plus tiers. Asking the enum's top value instead threw for every Rise score at
///     SSS wherever a score rendered (the bug check's finding).
/// </summary>
public sealed class GradeProgressRiseTests
{
    [Theory]
    [InlineData(990000)]
    [InlineData(995000)]
    [InlineData(999999)]
    public void AnSssOnRiseIsTheTopOfItsLadderAndReachesForThePerfectGame(int score)
    {
        var progress = GradeProgress.Of(PhoenixScore.From(score), MixEnum.Rise);

        Assert.Equal(PhoenixLetterGrade.SSS, progress.Grade);
        Assert.Null(progress.NextGrade);
        Assert.Equal(1_000_000 - score, progress.PointsToNext);
        Assert.False(progress.IsPerfectGame);
    }

    [Fact]
    public void AMillionOnRiseIsThePerfectGame()
    {
        var progress = GradeProgress.Of(PhoenixScore.Max, MixEnum.Rise);

        Assert.Equal(PhoenixLetterGrade.SSS, progress.Grade);
        Assert.True(progress.IsPerfectGame);
    }

    [Fact]
    public void ARiseGradeBelowTheTopStillNamesTheNextRung()
    {
        var progress = GradeProgress.Of(PhoenixScore.From(975000), MixEnum.Rise);

        Assert.Equal(PhoenixLetterGrade.SS, progress.Grade);
        Assert.Equal(PhoenixLetterGrade.SSS, progress.NextGrade);
        Assert.Equal(15000, progress.PointsToNext);
    }

    [Fact]
    public void ThePhoenixTopIsStillSssPlus()
    {
        var progress = GradeProgress.Of(PhoenixScore.From(997000), MixEnum.Phoenix2);

        Assert.Equal(PhoenixLetterGrade.SSSPlus, progress.Grade);
        Assert.Null(progress.NextGrade);
        Assert.Equal(3000, progress.PointsToNext);
    }
}
