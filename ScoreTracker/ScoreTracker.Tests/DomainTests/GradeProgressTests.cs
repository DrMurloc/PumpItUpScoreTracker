using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class GradeProgressTests
{
    [Theory]
    [InlineData(989050, PhoenixLetterGrade.SSPlus, PhoenixLetterGrade.SSS, 950)]
    [InlineData(984120, PhoenixLetterGrade.SS, PhoenixLetterGrade.SSPlus, 880)]
    [InlineData(968900, PhoenixLetterGrade.AAAPlus, PhoenixLetterGrade.S, 1100)]
    [InlineData(936200, PhoenixLetterGrade.AA, PhoenixLetterGrade.AAPlus, 3800)]
    [InlineData(884000, PhoenixLetterGrade.A, PhoenixLetterGrade.APlus, 16000)]
    public void PointsToNextCountUpToTheNextGradesFloor(int score, PhoenixLetterGrade grade,
        PhoenixLetterGrade next, int points)
    {
        var progress = GradeProgress.Of(PhoenixScore.From(score), MixEnum.Phoenix2);

        Assert.Equal(grade, progress.Grade);
        Assert.Equal(next, progress.NextGrade);
        Assert.Equal(points, progress.PointsToNext);
    }

    [Theory]
    [InlineData(MixEnum.Phoenix, PhoenixLetterGrade.AA, PhoenixLetterGrade.AAPlus, 13000)]
    [InlineData(MixEnum.Phoenix2, PhoenixLetterGrade.APlus, PhoenixLetterGrade.AA, 8000)]
    public void TheSameScoreIsADifferentDistanceOnEachMixBelowAaa(MixEnum mix, PhoenixLetterGrade grade,
        PhoenixLetterGrade next, int points)
    {
        var progress = GradeProgress.Of(PhoenixScore.From(912000), mix);

        Assert.Equal(grade, progress.Grade);
        Assert.Equal(next, progress.NextGrade);
        Assert.Equal(points, progress.PointsToNext);
    }

    [Fact]
    public void SssPlusReachesForThePerfectGame()
    {
        var progress = GradeProgress.Of(PhoenixScore.From(999420), MixEnum.Phoenix2);

        Assert.Equal(PhoenixLetterGrade.SSSPlus, progress.Grade);
        Assert.Null(progress.NextGrade);
        Assert.Equal(580, progress.PointsToNext);
        Assert.Equal(4420, progress.PointsIntoGrade);
        Assert.Equal(5000, progress.GradeWidth);
        Assert.Equal(.884, progress.ShareThrough, 3);
        Assert.False(progress.IsPerfectGame);
    }

    [Fact]
    public void APerfectGameHasNothingLeftToReach()
    {
        var progress = GradeProgress.Of(PhoenixScore.From(1000000), MixEnum.Phoenix2);

        Assert.True(progress.IsPerfectGame);
        Assert.Null(progress.NextGrade);
        Assert.Equal(0, progress.PointsToNext);
        Assert.Equal(1.0, progress.ShareThrough);
    }

    [Fact]
    public void OnePointShortIsOnePointToGo()
    {
        var progress = GradeProgress.Of(PhoenixScore.From(969999), MixEnum.Phoenix2);

        Assert.Equal(PhoenixLetterGrade.AAAPlus, progress.Grade);
        Assert.Equal(1, progress.PointsToNext);
        Assert.Equal(.9999, progress.ShareThrough, 4);
    }

    [Theory]
    [InlineData(974000)]
    [InlineData(958000)]
    [InlineData(936000)]
    [InlineData(880000)]
    public void ShareThroughScalesWithTheGradesWidth(int score)
    {
        // An S is 5,000 wide, an AAA 10,000, a Phoenix 2 AA 20,000 and a Phoenix 2 A 100,000: each of
        // these scores is four fifths of the way through its grade.
        Assert.Equal(.8, GradeProgress.Of(PhoenixScore.From(score), MixEnum.Phoenix2).ShareThrough, 6);
    }

    [Theory]
    [InlineData(MixEnum.Phoenix)]
    [InlineData(MixEnum.Phoenix2)]
    public void EveryFloorStartsItsGradeAndIsTheWholeGradeAwayFromTheNext(MixEnum mix)
    {
        foreach (var grade in System.Enum.GetValues<PhoenixLetterGrade>())
        {
            var floor = grade.GetMinimumScoreFor(mix);
            var progress = GradeProgress.Of(floor, mix);

            Assert.Equal(grade, progress.Grade);
            Assert.Equal(0.0, progress.ShareThrough);
            var line = progress.NextGrade?.GetMinimumScoreFor(mix) ?? PhoenixScore.Max;
            Assert.Equal((int)line - (int)floor, progress.PointsToNext);
        }
    }
}
