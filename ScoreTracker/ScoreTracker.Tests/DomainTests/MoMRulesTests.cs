using System;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The published numbers the Rules page renders (docs/design/march-of-murlocs.md §11.11) are
///     read off the same configuration the season cycle seats on a board. These pin the figures
///     the page shows, including its worked example, so a change to either side shows up here.
/// </summary>
public sealed class MoMRulesTests
{
    [Fact]
    public void TheGradeRungsSitOnEachMixOwnCutoffs()
    {
        var phoenix2 = MoMRules.GradeRows(MixEnum.Phoenix2);
        Assert.Equal(
            new[]
            {
                (PhoenixLetterGrade.A, 800000, 0.0), (PhoenixLetterGrade.APlus, 900000, .70),
                (PhoenixLetterGrade.AA, 920000, .80), (PhoenixLetterGrade.AAPlus, 940000, .90),
                (PhoenixLetterGrade.AAA, 950000, 1.0), (PhoenixLetterGrade.AAAPlus, 960000, 1.10),
                (PhoenixLetterGrade.S, 970000, 1.20), (PhoenixLetterGrade.SPlus, 975000, 1.26),
                (PhoenixLetterGrade.SS, 980000, 1.32), (PhoenixLetterGrade.SSPlus, 985000, 1.38),
                (PhoenixLetterGrade.SSS, 990000, 1.44), (PhoenixLetterGrade.SSSPlus, 995000, 1.50)
            },
            phoenix2.Select(r => (r.Grade, r.FromScore, r.Multiplier)).ToArray());

        var phoenix = MoMRules.GradeRows(MixEnum.Phoenix);
        Assert.Equal((PhoenixLetterGrade.A, 750000, 0.0), (phoenix[0].Grade, phoenix[0].FromScore, phoenix[0].Multiplier));
        Assert.Equal((PhoenixLetterGrade.APlus, 825000, .50), (phoenix[1].Grade, phoenix[1].FromScore, phoenix[1].Multiplier));
        Assert.Equal((PhoenixLetterGrade.AA, 900000, .75), (phoenix[2].Grade, phoenix[2].FromScore, phoenix[2].Multiplier));
        Assert.Equal((PhoenixLetterGrade.AAPlus, 925000, .90), (phoenix[3].Grade, phoenix[3].FromScore, phoenix[3].Multiplier));
        Assert.Equal((PhoenixLetterGrade.AAAPlus, 960000, 1.15), (phoenix[5].Grade, phoenix[5].FromScore, phoenix[5].Multiplier));
    }

    [Theory]
    [InlineData(MixEnum.Phoenix2, 799999, 0.0)]
    [InlineData(MixEnum.Phoenix2, 800000, 0.0)]
    [InlineData(MixEnum.Phoenix2, 890000, 0.63)]
    [InlineData(MixEnum.Phoenix2, 935000, 0.875)]
    [InlineData(MixEnum.Phoenix2, 950000, 1.0)]
    [InlineData(MixEnum.Phoenix2, 1000000, 1.6)]
    [InlineData(MixEnum.Phoenix, 812000, 0.4133)]
    [InlineData(MixEnum.Phoenix, 950000, 1.0)]
    public void TheMultiplierClimbsInAStraightLineBetweenRungs(MixEnum mix, int score, double expected)
    {
        Assert.Equal(expected, MoMRules.MultiplierAt(mix, score), 3);
    }

    [Theory]
    [InlineData(9, 90)]
    [InlineData(10, 100)]
    [InlineData(20, 650)]
    [InlineData(22, 930)]
    [InlineData(24, 1450)]
    [InlineData(26, 2210)]
    [InlineData(29, 3800)]
    public void ALevelPaysItsBaseRatingPlusTheStaminaBonusOnBothMixes(int level, int expected)
    {
        Assert.Equal(expected, MoMRules.LevelValue(MixEnum.Phoenix, level));
        Assert.Equal(expected, MoMRules.LevelValue(MixEnum.Phoenix2, level));
    }

    [Fact]
    public void TheConstantsThePageQuotes()
    {
        Assert.Equal(TimeSpan.FromMinutes(105), MoMRules.Window);
        Assert.Equal(TimeSpan.FromMinutes(2), MoMRules.LengthBaseline);
        Assert.Equal(1.6, MoMRules.PerfectGameMultiplier(MixEnum.Phoenix2));
        Assert.Equal(150, MoMRules.LevelBonus(23));
        Assert.Equal(0, MoMRules.LevelBonus(21));
        Assert.Equal(1, MoMRules.Levels.First());
        Assert.Equal(29, MoMRules.Levels.Last());
        Assert.Equal(24.5, MoMRules.BalancedLevel(24, null));
    }

    /// <summary>The page's worked example: a four-minute D23 at 940,000 on Phoenix 2, then at 950,000.</summary>
    [Fact]
    public void TheWorkedExampleOnTheRulesPage()
    {
        var chart = new ChartBuilder().WithLevel(23).WithType(ChartType.Double)
            .WithSong(new Song(Name.From("Four Minutes"), SongType.FullSong, new Uri("https://example.invalid/s.png"),
                TimeSpan.FromMinutes(4), Name.From("artist"), Bpm: null))
            .Build();
        var scoring = MoMScoring.ForBoard(MixEnum.Phoenix2, ChartType.Double);

        Assert.Equal(2088, (int)scoring.GetScore(chart, 940000, PhoenixPlate.RoughGame, false));
        Assert.Equal(2320, (int)scoring.GetScore(chart, 950000, PhoenixPlate.RoughGame, false));
        Assert.Equal(2088, (int)(MoMRules.LevelValue(MixEnum.Phoenix2, 23) * MoMRules.MultiplierAt(MixEnum.Phoenix2, 940000) * 2));
    }
}
