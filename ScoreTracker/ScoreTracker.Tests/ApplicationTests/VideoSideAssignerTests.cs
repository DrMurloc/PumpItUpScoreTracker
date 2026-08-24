using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class VideoSideAssignerTests
{
    private const string Url = "https://www.youtube.com/embed/abc123";
    private const string OtherUrl = "https://www.youtube.com/embed/xyz789";

    private static VideoChart Chart(ChartType type, int level, string url = Url)
    {
        return new VideoChart(Guid.NewGuid(), type, level, url);
    }

    private static Func<string, int> AllRowsAreInTheSong(IEnumerable<VideoChart> charts)
    {
        var counts = charts.GroupBy(c => c.VideoUrl).ToDictionary(g => g.Key, g => g.Count());
        return url => counts[url];
    }

    [Fact]
    public void TwoSinglesWithDistinctLevelsGetLowerLeftHigherRight()
    {
        var lower = Chart(ChartType.Single, 17);
        var higher = Chart(ChartType.Single, 22);
        var charts = new[] { higher, lower };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Equal(VideoSide.Left, sides[lower.ChartId]);
        Assert.Equal(VideoSide.Right, sides[higher.ChartId]);
    }

    [Fact]
    public void TwoPerformanceChartsWithDistinctLevelsOrderByLevelLikeAnySameTypePair()
    {
        var lower = Chart(ChartType.SinglePerformance, 2);
        var higher = Chart(ChartType.SinglePerformance, 4);
        var charts = new[] { lower, higher };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Equal(VideoSide.Left, sides[lower.ChartId]);
        Assert.Equal(VideoSide.Right, sides[higher.ChartId]);
    }

    [Fact]
    public void TiedLevelsAreLeftUntouchedRatherThanGuessedOrCleared()
    {
        var charts = new[] { Chart(ChartType.Single, 8), Chart(ChartType.Single, 8) };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Empty(sides);
    }

    [Fact]
    public void SinglePlusPerformancePairIsLeftUntouchedBecauseLevelsCannotOrderIt()
    {
        // Hand-researched sides live in the column for these; a recompute must not wipe them.
        var charts = new[] { Chart(ChartType.Single, 4), Chart(ChartType.SinglePerformance, 3) };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Empty(sides);
    }

    [Fact]
    public void SoloVideoIsCleared()
    {
        var solo = Chart(ChartType.Single, 20);

        var sides = VideoSideAssigner.ComputeSides(new[] { solo }, AllRowsAreInTheSong(new[] { solo }));

        Assert.Null(sides[solo.ChartId]);
    }

    [Fact]
    public void PairSharingItsUrlWithAThirdChartElsewhereIsClearedAsAMislink()
    {
        var charts = new[] { Chart(ChartType.Single, 4), Chart(ChartType.Single, 6) };

        var sides = VideoSideAssigner.ComputeSides(charts, _ => 3);

        Assert.Null(sides[charts[0].ChartId]);
        Assert.Null(sides[charts[1].ChartId]);
    }

    [Fact]
    public void ThreeSinglesOfOneSongOnOneUrlAreCleared()
    {
        var charts = new[]
            { Chart(ChartType.Single, 2), Chart(ChartType.Single, 6), Chart(ChartType.Single, 9) };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.All(charts, c => Assert.Null(sides[c.ChartId]));
    }

    [Theory]
    [InlineData(ChartType.Double)]
    [InlineData(ChartType.DoublePerformance)]
    [InlineData(ChartType.CoOp)]
    [InlineData(ChartType.HalfDouble)]
    public void PairContainingANonSinglesChartIsCleared(ChartType other)
    {
        var charts = new[] { Chart(ChartType.Single, 10), Chart(other, 12) };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Null(sides[charts[0].ChartId]);
        Assert.Null(sides[charts[1].ChartId]);
    }

    [Fact]
    public void UrlGroupsWithinOneSongAreDecidedIndependently()
    {
        var lower = Chart(ChartType.Single, 17);
        var higher = Chart(ChartType.Single, 22);
        var soloDouble = Chart(ChartType.Double, 23, OtherUrl);
        var charts = new[] { lower, higher, soloDouble };

        var sides = VideoSideAssigner.ComputeSides(charts, AllRowsAreInTheSong(charts));

        Assert.Equal(VideoSide.Left, sides[lower.ChartId]);
        Assert.Equal(VideoSide.Right, sides[higher.ChartId]);
        Assert.Null(sides[soloDouble.ChartId]);
    }
}
