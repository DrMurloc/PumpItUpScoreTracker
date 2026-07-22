using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class FolderLevelsTests
{
    [Fact]
    public void SinglesStopAt26_NoHarderSingleExistsYet()
    {
        var levels = FolderLevels.LevelsFor(ChartType.Single).ToArray();

        Assert.Equal(26, levels.Max());
        Assert.Contains(26, levels);
        Assert.DoesNotContain(27, levels);
        Assert.Equal(Enumerable.Range(1, 26), levels);
    }

    [Fact]
    public void DoublesRunToTheGameCeiling()
    {
        var levels = FolderLevels.LevelsFor(ChartType.Double).ToArray();

        Assert.Equal((int)DifficultyLevel.Max, levels.Max());
        Assert.Equal(1, levels.Min());
    }

    [Fact]
    public void CoOpLevelsArePlayerCountsTwoThroughFive()
    {
        var levels = FolderLevels.LevelsFor(ChartType.CoOp).ToArray();

        Assert.Equal(new[] { 2, 3, 4, 5 }, levels);
    }

    [Theory]
    [InlineData(ChartType.Single, 1, 26)]
    [InlineData(ChartType.CoOp, 2, 5)]
    public void RangeIsInclusivePerType(ChartType type, int min, int max)
    {
        Assert.Equal((min, max), FolderLevels.Range(type));
    }
}
