using System;
using System.Collections.Generic;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The one-shot pairing decision (docs/design/video-sides.md): sides are durable data, so
///     the assigner only ever answers for the URL group a registration event just formed —
///     and answers nothing at all for groups it can't derive, which is what keeps hand-set
///     sides untouched.
/// </summary>
public sealed class VideoSideAssignerTests
{
    private static VideoChart Chart(ChartType type, int level)
    {
        return new VideoChart(Guid.NewGuid(), type, level);
    }

    private static IReadOnlyDictionary<Guid, VideoSide> Decide(params VideoChart[] charts)
    {
        return VideoSideAssigner.DecideSides(charts, charts.Length);
    }

    [Fact]
    public void TwoSinglesWithDistinctLevelsGetLowerLeftHigherRight()
    {
        var lower = Chart(ChartType.Single, 17);
        var higher = Chart(ChartType.Single, 22);

        var sides = Decide(higher, lower);

        Assert.Equal(VideoSide.Left, sides[lower.ChartId]);
        Assert.Equal(VideoSide.Right, sides[higher.ChartId]);
    }

    [Fact]
    public void TwoPerformanceChartsWithDistinctLevelsOrderByLevelLikeAnySameTypePair()
    {
        var lower = Chart(ChartType.SinglePerformance, 2);
        var higher = Chart(ChartType.SinglePerformance, 4);

        var sides = Decide(lower, higher);

        Assert.Equal(VideoSide.Left, sides[lower.ChartId]);
        Assert.Equal(VideoSide.Right, sides[higher.ChartId]);
    }

    [Fact]
    public void TiedLevelsDecideNothing()
    {
        Assert.Empty(Decide(Chart(ChartType.Single, 8), Chart(ChartType.Single, 8)));
    }

    [Fact]
    public void SinglePlusPerformanceDecidesNothingBecauseLevelsCannotOrderIt()
    {
        // These sides come from watching the video; the caller writes nothing here, which is
        // exactly what leaves a hand-researched pair intact.
        Assert.Empty(Decide(Chart(ChartType.Single, 4), Chart(ChartType.SinglePerformance, 3)));
    }

    [Fact]
    public void ASoloChartDecidesNothing()
    {
        Assert.Empty(Decide(Chart(ChartType.Single, 20)));
    }

    [Fact]
    public void APairSharingItsUrlWithAThirdChartElsewhereDecidesNothing()
    {
        // The cross-song mislink shape: the song sees two of its own charts, the URL holds a
        // stranger too — no derivation until the data is cleaned.
        var charts = new[] { Chart(ChartType.Single, 4), Chart(ChartType.Single, 6) };

        Assert.Empty(VideoSideAssigner.DecideSides(charts, 3));
    }

    [Fact]
    public void ThreeChartsOfOneSongOnOneUrlDecideNothing()
    {
        Assert.Empty(Decide(Chart(ChartType.Single, 2), Chart(ChartType.Single, 6),
            Chart(ChartType.Single, 9)));
    }

    [Theory]
    [InlineData(ChartType.Double)]
    [InlineData(ChartType.DoublePerformance)]
    [InlineData(ChartType.CoOp)]
    [InlineData(ChartType.HalfDouble)]
    public void APairContainingANonSinglesChartDecidesNothing(ChartType other)
    {
        Assert.Empty(Decide(Chart(ChartType.Single, 10), Chart(other, 12)));
    }
}
