using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;
using Appearance = ScoreTracker.Catalog.Domain.ChartSpanCalculator.Appearance;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ChartSpanCalculatorTests
{
    [Fact]
    public void LatestPicksTheNewestMixByDisplayOrder()
    {
        var latest = ChartSpanCalculator.Latest(new[] { MixEnum.Phoenix, MixEnum.Prex3, MixEnum.XX });

        Assert.Equal(MixEnum.Phoenix, latest);
    }

    [Fact]
    public void LinkedMixPrefersTheVisitorsMixWhenTheChartAppearsThere()
    {
        var linked = ChartSpanCalculator.LinkedMix(MixEnum.Phoenix,
            new[] { MixEnum.XX, MixEnum.Phoenix, MixEnum.Phoenix2 });

        Assert.Equal(MixEnum.Phoenix, linked);
    }

    [Fact]
    public void LinkedMixFallsBackToTheLatestAppearanceWhenAbsentFromTheVisitorsMix()
    {
        var linked = ChartSpanCalculator.LinkedMix(MixEnum.Phoenix2,
            new[] { MixEnum.Exceed, MixEnum.XX, MixEnum.Phoenix });

        Assert.Equal(MixEnum.Phoenix, linked);
    }

    [Fact]
    public void LevelChangeIsTheSignedDeltaBetweenEarliestAndLatestModernAppearances()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.Phoenix2, 19, false),
            new Appearance(MixEnum.Exceed2, 18, false),
            new Appearance(MixEnum.Phoenix, 19, false)
        }, false);

        Assert.Equal(1, change);
    }

    [Fact]
    public void LevelChangeReportsDownRerates()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.Exceed2, 20, false),
            new Appearance(MixEnum.Phoenix2, 19, false)
        }, false);

        Assert.Equal(-1, change);
    }

    [Fact]
    public void LevelChangeIsZeroWhenTheLevelNeverMoved()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.XX, 21, false),
            new Appearance(MixEnum.Phoenix, 21, false)
        }, false);

        Assert.Equal(0, change);
    }

    [Fact]
    public void LevelChangeIgnoresSlotScaleAndPreExceedAppearances()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.Prex3, 6, true),
            new Appearance(MixEnum.Premiere3, 8, false),
            new Appearance(MixEnum.Exceed, 16, false),
            new Appearance(MixEnum.Phoenix2, 18, false)
        }, false);

        Assert.Equal(2, change);
    }

    [Fact]
    public void LevelChangeIsNullWithFewerThanTwoComparableAppearances()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.Prex3, 6, true),
            new Appearance(MixEnum.Phoenix2, 18, false)
        }, false);

        Assert.Null(change);
    }

    [Fact]
    public void LevelChangeIsNullForCoOps()
    {
        var change = ChartSpanCalculator.LevelChange(new[]
        {
            new Appearance(MixEnum.Phoenix, 2, false),
            new Appearance(MixEnum.Phoenix2, 4, false)
        }, true);

        Assert.Null(change);
    }
}
