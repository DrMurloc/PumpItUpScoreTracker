using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The level/score/plate split behind "Where your PUMBILITY comes from". It is a
///     decomposition of the formula rather than a model of it, so the binding property is that
///     the parts sum to the score they split — under every formula, at every grade, on both
///     mixes (docs/design/pumbility-overhaul.md §3.6).
/// </summary>
public sealed class ScoreContributionTests
{
    public static TheoryData<MixEnum, int, ChartType, int, PhoenixPlate> Cases()
    {
        var data = new TheoryData<MixEnum, int, ChartType, int, PhoenixPlate>();
        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        foreach (var level in new[] { 10, 15, 21, 24, 25, 27 })
        foreach (var type in new[] { ChartType.Single, ChartType.Double })
        foreach (var score in new[] { 500_000, 800_000, 905_000, 950_000, 985_000, 1_000_000 })
        foreach (var plate in new[] { PhoenixPlate.RoughGame, PhoenixPlate.TalentedGame, PhoenixPlate.PerfectGame })
            data.Add(mix, level, type, score, plate);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheThreePartsSumToTheScoreTheySplit(MixEnum mix, int level, ChartType type, int score,
        PhoenixPlate plate)
    {
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        var chart = new ChartBuilder().WithType(type).WithLevel(level).Build();

        var split = config.Decompose(chart, score, plate, false);

        Assert.Equal(config.GetScore(chart, score, plate, false), split.Total, 6);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ABrokenRunDecomposesToNothingAtAll(MixEnum mix, int level, ChartType type, int score,
        PhoenixPlate plate)
    {
        // Both PUMBILITY configs zero a stage break outright, so there is nothing to attribute
        // to the chart, the score or the plate — not a small number, none of it.
        var config = ScoringConfiguration.PumbilityScoring(mix, false);
        var chart = new ChartBuilder().WithType(type).WithLevel(level).Build();

        var split = config.Decompose(chart, score, plate, true);

        Assert.Equal(0, split.Base);
        Assert.Equal(0, split.FromGrade);
        Assert.Equal(0, split.FromPlate);
    }

    [Theory]
    [InlineData(PhoenixPlate.RoughGame)]
    [InlineData(PhoenixPlate.TalentedGame)]
    [InlineData(PhoenixPlate.PerfectGame)]
    public void OnPhoenixThePlateContributesExactlyNothing(PhoenixPlate plate)
    {
        // The point the band exists to make: Phoenix 1's plate modifiers are all 1.0, so the
        // plate you walked away with never entered the number.
        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(24).Build();

        var split = config.Decompose(chart, 925_308, plate, false);

        Assert.Equal(0, split.FromPlate);
        Assert.Equal(0, config.PlateHeadroom(chart, 925_308, plate));
    }

    [Fact]
    public void OnPhoenix2ThePlateIsWorthAboutOnePercentOfAChart()
    {
        // Additive bonuses of 0.000 to 0.020 against grade multipliers of 1.08 to 1.50, so the
        // whole plate ladder end to end cannot reach a fiftieth of a chart.
        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();

        var total = config.GetScore(chart, 985_000, PhoenixPlate.RoughGame, false);
        var headroom = config.PlateHeadroom(chart, 985_000, PhoenixPlate.RoughGame);

        Assert.True(headroom > 0);
        Assert.InRange(headroom / total, 0.005, 0.02);
    }

    [Fact]
    public void PlateHeadroomIsWhatIsLeftBetweenTheHeldPlateAndTheBest()
    {
        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(23).Build();

        var held = config.GetScore(chart, 991_500, PhoenixPlate.FairGame, false);
        var best = config.GetScore(chart, 991_500, PhoenixPlate.PerfectGame, false);

        Assert.Equal(best - held, config.PlateHeadroom(chart, 991_500, PhoenixPlate.FairGame), 6);
        Assert.Equal(0, config.PlateHeadroom(chart, 991_500, PhoenixPlate.PerfectGame));
    }

    [Fact]
    public void AChartBelowLevelTenDecomposesToNothingOnBothMixes()
    {
        // The bug the section's bar was found on: below level 10 the base rating is zero, so a
        // perfect run on one is worth nothing to split.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(9).Build();

        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        {
            var split = ScoringConfiguration.PumbilityScoring(mix, false)
                .Decompose(chart, 1_000_000, PhoenixPlate.PerfectGame, false);
            Assert.Equal(0, split.Total);
        }
    }

    [Fact]
    public void APassingFDecomposesToNothingOnPhoenix2()
    {
        // The split has to honour the same exclusion the total does, or a chart worth zero would
        // still report a level part and a plate part on the breakdown. Phoenix 1 has no such
        // rule and keeps paying for an F, so only the Phoenix 2 arm is asserted at zero.
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(20).Build();

        var split = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
            .Decompose(chart, 271_620, PhoenixPlate.MarvelousGame, false);

        Assert.Equal(0, split.Total);
        Assert.Equal(0, split.Base);
        Assert.Equal(0, split.FromPlate);
    }

    [Fact]
    public void TheLevelPartIsTheLargestShareAndThePlatePartIsTheSmallest()
    {
        // What the band claims in words, asserted as an ordering rather than as fixed numbers,
        // so a re-verified Phoenix 2 grade table cannot silently invert the argument.
        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(21).Build();

        var split = config.Decompose(chart, 985_000, PhoenixPlate.SuperbGame, false);

        Assert.True(split.Base > split.FromGrade);
        Assert.True(split.FromGrade > split.FromPlate);
        Assert.True(split.FromPlate / split.Total < 0.02);
    }

    [Fact]
    public void AFormulaWithNoSuchSplitSaysSoRatherThanInventingOne()
    {
        // Avalanche folds the stage break into the grade term, so there is no plate part to
        // report. Answering anyway would be making one up.
        var config = new ScoringConfiguration
        {
            Formula = ScoringConfiguration.CalculationType.Avalanche
        };
        var chart = new ChartBuilder().WithLevel(20).Build();

        Assert.Throws<NotSupportedException>(() =>
            config.Decompose(chart, 950_000, PhoenixPlate.SuperbGame, false));
    }

    [Fact]
    public void SummingContributionsIsTheSameAsSummingScores()
    {
        // The pool band adds fifty of these up, so the operator has to be the arithmetic it
        // looks like.
        var config = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var charts = new[] { 21, 22, 23, 24, 25 }
            .Select(l => new ChartBuilder().WithType(ChartType.Single).WithLevel(l).Build())
            .ToArray();

        var summed = charts.Select(c => config.Decompose(c, 985_000, PhoenixPlate.SuperbGame, false))
            .Aggregate(default(ScoreContribution), (a, b) => a + b);

        Assert.Equal(charts.Sum(c => config.GetScore(c, 985_000, PhoenixPlate.SuperbGame, false)),
            summed.Total, 6);
    }
}
