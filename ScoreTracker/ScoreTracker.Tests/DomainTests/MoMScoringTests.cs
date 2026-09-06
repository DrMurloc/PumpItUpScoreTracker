using System;
using System.Linq;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The PUMBILITY+ builder the season cycle seats on a board (docs/design/march-of-murlocs.md
///     §4, §5). Phoenix is pinned against the table Winter 2025 froze, so extracting the builder
///     from the cycle handler moved nothing; Phoenix 2 is the same structure with D41's four rows
///     on its own cutoffs and nothing else different.
/// </summary>
public sealed class MoMScoringTests
{
    [Fact]
    public void PhoenixIsTheTableWinter2025Froze()
    {
        var built = MoMScoring.ForBoard(MixEnum.Phoenix, ChartType.Double);
        var frozen = MoMRealSessions.Winter2025Season().Scoring;

        Assert.Equal(MixEnum.Phoenix, built.Mix);
        Assert.True(built.AdjustToTime);
        Assert.True(built.ContinuousLetterGradeScale);
        Assert.Equal(frozen.PgLetterGradeModifier, built.PgLetterGradeModifier);
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
            Assert.Equal(frozen.LetterGradeModifiers[grade], built.LetterGradeModifiers[grade]);
        for (var level = 10; level <= 27; level++)
            Assert.Equal(frozen.LevelRatings[DifficultyLevel.From(level)], built.LevelRatings[DifficultyLevel.From(level)]);
        Assert.Equal(3210, built.LevelRatings[DifficultyLevel.From(28)]);
        Assert.Equal(3800, built.LevelRatings[DifficultyLevel.From(29)]);
        for (var level = 1; level <= 9; level++)
            Assert.Equal(level * 10, built.LevelRatings[DifficultyLevel.From(level)]);
        foreach (var type in Enum.GetValues<ChartType>())
            Assert.Equal(frozen.ChartTypeModifiers[type], built.ChartTypeModifiers[type]);
        Assert.False(built.ZeroBelowLevelTen);
        Assert.False(built.SinglesLevelBump);
    }

    [Fact]
    public void Phoenix2IsTheSameStructureWithItsOwnFourRows()
    {
        var phoenix = MoMScoring.For(MixEnum.Phoenix);
        var phoenix2 = MoMScoring.For(MixEnum.Phoenix2);

        Assert.Equal(MixEnum.Phoenix2, phoenix2.Mix);
        Assert.Equal(.70, phoenix2.LetterGradeModifiers[PhoenixLetterGrade.APlus]);
        Assert.Equal(.80, phoenix2.LetterGradeModifiers[PhoenixLetterGrade.AA]);
        Assert.Equal(.90, phoenix2.LetterGradeModifiers[PhoenixLetterGrade.AAPlus]);
        Assert.Equal(1.10, phoenix2.LetterGradeModifiers[PhoenixLetterGrade.AAAPlus]);
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>()
                     .Where(g => g is not (PhoenixLetterGrade.APlus or PhoenixLetterGrade.AA or PhoenixLetterGrade.AAAPlus)))
            Assert.Equal(phoenix.LetterGradeModifiers[grade], phoenix2.LetterGradeModifiers[grade]);
        foreach (var level in DifficultyLevel.All)
            Assert.Equal(phoenix.LevelRatings[level], phoenix2.LevelRatings[level]);
        Assert.Equal(phoenix.PgLetterGradeModifier, phoenix2.PgLetterGradeModifier);
        Assert.Equal(phoenix.StageBreakModifier, phoenix2.StageBreakModifier);
        Assert.True(phoenix2.AdjustToTime);
        Assert.True(phoenix2.ContinuousLetterGradeScale);
        Assert.False(phoenix2.ZeroBelowLevelTen);
        Assert.False(phoenix2.SinglesLevelBump);
    }

    [Fact]
    public void ABoardPricesItsOwnChartTypeOnly()
    {
        var singles = MoMScoring.ForBoard(MixEnum.Phoenix2, ChartType.Single);

        Assert.Equal(1.0, singles.ChartTypeModifiers[ChartType.Single]);
        foreach (var type in Enum.GetValues<ChartType>().Where(t => t != ChartType.Single))
            Assert.Equal(0, singles.ChartTypeModifiers[type]);
        // Each call is a fresh instance: zeroing one board never leaks into the next.
        Assert.Equal(1.0, MoMScoring.For(MixEnum.Phoenix2).ChartTypeModifiers[ChartType.Double]);
    }

    [Theory]
    [InlineData(24, null, 24.5)]
    [InlineData(24, 24.2, 24.5)]
    [InlineData(24, 25.1, 25.1)]
    [InlineData(24, 26.0, 25.5)]
    [InlineData(10, 9.0, 10.5)]
    public void TheBalancedLevelIsTheScoringLevelClampedToOneLevelAboveTheFolder(int folder, double? scoring, double expected)
    {
        Assert.Equal(expected, MoMScoring.BalancedLevel(folder, scoring), 6);
    }

    [Fact]
    public void ABrokenPlayAndAPassedPlayAtTheSameScorePayTheSameOnBothMixes()
    {
        var chart = new ChartBuilder().WithLevel(24).WithType(ChartType.Double).Build();
        foreach (var mix in new[] { MixEnum.Phoenix, MixEnum.Phoenix2 })
        {
            var scoring = MoMScoring.ForBoard(mix, ChartType.Double);
            Assert.Equal(scoring.GetScore(chart, 960000, PhoenixPlate.RoughGame, false),
                scoring.GetScore(chart, 960000, PhoenixPlate.RoughGame, true));
        }
    }
}
