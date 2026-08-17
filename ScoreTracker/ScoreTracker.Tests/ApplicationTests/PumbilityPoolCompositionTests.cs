using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The population section's two pure pieces (docs/design/pumbility-calculator.md D9/D10): which
///     band a merged pool total lands in, and how full pools accumulate into per-band sums that agree
///     with the PUMBILITY page's own split.
/// </summary>
public sealed class PumbilityPoolCompositionTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 16, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void PhoenixTwoBandsAreTheGemsOfTheTitleLadder()
    {
        var bands = PumbilityPoolBands.For(MixEnum.Phoenix2);
        Assert.Equal(new[] { PumbilityPoolBands.Phoenix2Unranked, "[P.B] BRONZE", "[P.B] SILVER", "[P.B] GOLD",
            "[P.B] PLATINUM", "[P.B] DIAMOND", "[P.B] RED BERYL", "[P.B] ALEXANDRITE", "ABYSS ABSOLUTE" },
            bands.Select(b => b.Key));
        Assert.Equal(10_000, bands.Single(b => b.Key == "[P.B] BRONZE").Floor);
        Assert.Equal(20_000, bands.Single(b => b.Key == "ABYSS ABSOLUTE").Floor);
        Assert.Null(bands.Last().Ceiling);
        // Every ceiling is the next floor: the bands tile the line with no gap and no overlap.
        for (var i = 0; i + 1 < bands.Count; i++) Assert.Equal(bands[i + 1].Floor, bands[i].Ceiling);
        Assert.All(bands.Skip(1), b => Assert.Equal(b.Key, b.Title));
        Assert.Null(bands[0].Title);
    }

    [Fact]
    public void PhoenixBandsAreEightUnevenTotals()
    {
        var bands = PumbilityPoolBands.For(MixEnum.Phoenix);
        Assert.Equal(new[] { "lt20k", "20k", "30k", "40k", "50k", "60k", "70k", "80k+" }, bands.Select(b => b.Key));
        Assert.Equal(0, bands[0].Floor);
        Assert.Equal(20_000, bands[0].Ceiling);
        Assert.Equal(80_000, bands.Last().Floor);
        Assert.Null(bands.Last().Ceiling);
        Assert.All(bands, b => Assert.Null(b.Title));
    }

    [Theory]
    [InlineData(MixEnum.Phoenix2, 9_999.99, PumbilityPoolBands.Phoenix2Unranked)]
    [InlineData(MixEnum.Phoenix2, 10_000, "[P.B] BRONZE")]
    [InlineData(MixEnum.Phoenix2, 17_999.99, "[P.B] DIAMOND")]
    [InlineData(MixEnum.Phoenix2, 18_000, "[P.B] RED BERYL")]
    [InlineData(MixEnum.Phoenix2, 25_000, "ABYSS ABSOLUTE")]
    [InlineData(MixEnum.Phoenix, 19_999.99, "lt20k")]
    [InlineData(MixEnum.Phoenix, 20_000, "20k")]
    [InlineData(MixEnum.Phoenix, 64_466.9, "60k")]
    [InlineData(MixEnum.Phoenix, 96_000, "80k+")]
    public void ATotalLandsInExactlyOneBand(MixEnum mix, double total, string expected)
    {
        // Floors are inclusive, ceilings exclusive — the raw double is compared, never a rounded
        // one, so 17,999.99 does not get promoted to the rung it has not reached.
        Assert.Equal(expected, PumbilityPoolBands.BandFor(mix, total)!.Key);
    }

    [Fact]
    public void AMixWithoutAPumbilityFormulaHasNoBands()
    {
        Assert.Empty(PumbilityPoolBands.For(MixEnum.XX));
        Assert.Null(PumbilityPoolBands.BandFor(MixEnum.XX, 15_000));
    }

    [Fact]
    public void ThePartsSumToThePoolAndTheBandIsChosenByThatTotal()
    {
        // Two Phoenix 2 pools built from real Decompose parts: the sums the builder writes must be
        // exactly what the PUMBILITY page would show for the same pool, and the total that picks the
        // band must be the sum of the parts — not a re-priced or rounded stand-in.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var builder = new PumbilityPoolCompositionBuilder(MixEnum.Phoenix2);
        var pool = Pool(scoring, ChartType.Double, level: 24, score: 972_000, PhoenixPlate.MarvelousGame);
        builder.Add(pool);
        builder.Add(pool);

        var record = builder.Build(At);
        var expectedTotal = pool.Sum(c => c.Parts.Base + c.Parts.FromGrade + c.Parts.FromPlate);
        var band = record.Bands.Single(b => b.Players > 0);
        Assert.Equal(PumbilityPoolBands.BandFor(MixEnum.Phoenix2, expectedTotal)!.Key, band.Key);
        Assert.Equal(2, band.Players);
        Assert.Equal(100, band.ChartsPooled);
        Assert.Equal(2 * expectedTotal, band.Total, 6);
        Assert.Equal(2 * pool.Sum(c => c.Parts.Base), band.LevelPart, 6);
        Assert.Equal(2 * pool.Sum(c => c.Parts.FromGrade), band.ScorePart, 6);
        Assert.Equal(2 * pool.Sum(c => c.Parts.FromPlate), band.PlatePart, 6);
        Assert.True(band.PlatePart > 0, "a Marvelous Game plate on Phoenix 2 is a real, small, positive part");
        Assert.Equal(24, band.AverageLevel);
        Assert.Equal(100, band.GradeCounts[PhoenixLetterGrade.S]);
        Assert.Equal(2, record.PoolsCounted);
        Assert.Equal(At, record.ComputedAt);
    }

    [Fact]
    public void EveryBandIsPresentEvenWhenNobodyIsInIt()
    {
        var builder = new PumbilityPoolCompositionBuilder(MixEnum.Phoenix);
        var record = builder.Build(At);
        Assert.Equal(PumbilityPoolBands.For(MixEnum.Phoenix).Count, record.Bands.Count);
        Assert.All(record.Bands, b => Assert.Equal(0, b.Players));
        Assert.All(record.Bands, b => Assert.Equal(0, b.AverageLevel));
        Assert.Equal(0, record.PoolsCounted);
    }

    [Fact]
    public void OnPhoenixThePlatePartIsExactlyZero()
    {
        // Not a rounding argument: every Phoenix plate carries ×1.0, so the part is 0 and the page
        // says the formula has no plate term rather than drawing a hairline.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var builder = new PumbilityPoolCompositionBuilder(MixEnum.Phoenix);
        builder.Add(Pool(scoring, ChartType.Single, level: 20, score: 985_000, PhoenixPlate.PerfectGame));
        var band = builder.Build(At).Bands.Single(b => b.Players > 0);
        Assert.Equal(0, band.PlatePart);
        Assert.Equal(50, band.GradeCounts[PhoenixLetterGrade.SSPlus]);
    }

    private static IReadOnlyCollection<PooledChart> Pool(ScoringConfiguration scoring, ChartType type, int level,
        int score, PhoenixPlate plate)
    {
        var chart = new ChartBuilder().WithLevel(level).WithType(type).Build();
        var parts = scoring.Decompose(chart, score, plate, false);
        var grade = ((PhoenixScore)score).LetterGradeFor(scoring.Mix);
        return Enumerable.Repeat(new PooledChart(level, grade, parts), 50).ToArray();
    }
}
