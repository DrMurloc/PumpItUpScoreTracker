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
        var levels = FolderLevels.LevelsFor(ChartTypeCategory.Single).ToArray();

        Assert.Equal(26, levels.Max());
        Assert.Contains(26, levels);
        Assert.DoesNotContain(27, levels);
        Assert.Equal(Enumerable.Range(1, 26), levels);
    }

    [Fact]
    public void DoublesRunToTheGameCeiling()
    {
        var levels = FolderLevels.LevelsFor(ChartTypeCategory.Double).ToArray();

        Assert.Equal((int)DifficultyLevel.Max, levels.Max());
        Assert.Equal(1, levels.Min());
    }

    [Fact]
    public void CoOpLevelsArePlayerCountsTwoThroughFive()
    {
        var levels = FolderLevels.LevelsFor(ChartTypeCategory.CoOp).ToArray();

        Assert.Equal(new[] { 2, 3, 4, 5 }, levels);
    }

    [Theory]
    [InlineData(ChartTypeCategory.Single, 1, 26)]
    [InlineData(ChartTypeCategory.CoOp, 2, 5)]
    public void RangeIsInclusivePerFolder(ChartTypeCategory category, int min, int max)
    {
        Assert.Equal((min, max), FolderLevels.Range(category));
    }

    // A half-double folder is a doubles folder, so it offers the doubles range rather than
    // falling through to something of its own.
    [Fact]
    public void HalfDoublesShareTheDoublesRange()
    {
        Assert.Equal(FolderLevels.Range(ChartTypeCategory.Double),
            FolderLevels.Range(ChartType.HalfDouble.Category()));
    }
}
