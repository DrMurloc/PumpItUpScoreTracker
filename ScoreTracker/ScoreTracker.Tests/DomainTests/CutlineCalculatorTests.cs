using System;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CutlineCalculatorTests
{
    private static readonly ScoringConfiguration Scoring =
        ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

    [Fact]
    public void TheTierLadderIsEveryHundredThenEveryTenThenEverySeat()
    {
        Assert.Equal(28, CutlineCalculator.TierLadder.Length);
        Assert.Equal(1000, CutlineCalculator.TierLadder.First());
        Assert.Equal(1, CutlineCalculator.TierLadder.Last());
        Assert.Contains(500, CutlineCalculator.TierLadder);
        Assert.Contains(50, CutlineCalculator.TierLadder);
        Assert.Contains(7, CutlineCalculator.TierLadder);
        Assert.DoesNotContain(950, CutlineCalculator.TierLadder);
        Assert.DoesNotContain(45, CutlineCalculator.TierLadder);
        // Strictly descending — the ladder reads top-of-board to the summit.
        Assert.True(CutlineCalculator.TierLadder.Zip(CutlineCalculator.TierLadder.Skip(1))
            .All(pair => pair.First > pair.Second));
    }

    [Fact]
    public void ValueAtRankIsPositionalAndNullBeyondTheBoard()
    {
        var board = new[]
        {
            new PlacementRow(1, 11, 1, 20000m),
            new PlacementRow(1, 12, 1, 20000m),
            new PlacementRow(1, 13, 3, 18000m)
        };

        Assert.Equal(20000m, CutlineCalculator.ValueAtRank(board, 1));
        // Olympic ties don't disturb positional rank semantics: the 2nd best value is row 2.
        Assert.Equal(20000m, CutlineCalculator.ValueAtRank(board, 2));
        Assert.Equal(18000m, CutlineCalculator.ValueAtRank(board, 3));
        Assert.Null(CutlineCalculator.ValueAtRank(board, 4));
    }

    [Fact]
    public void LevelForMatchesTheGoldenFormulaExactly()
    {
        // The inversion must agree with GetScore: the found level clears the per-chart
        // requirement and the level below it does not.
        var tierValue = 12450.30m;
        foreach (var (_, grade) in CutlineCalculator.Grades)
        {
            var level = CutlineCalculator.LevelFor(Scoring, ChartType.Single, grade, tierValue);
            Assert.NotNull(level);
            var perChart = (double)tierValue / CutlineCalculator.ChartsCounted;
            Assert.True(Scoring.GetScore(ChartType.Single, DifficultyLevel.From(level!.Value),
                grade.GetMinimumScoreFor(Scoring.Mix), PhoenixPlate.SuperbGame) >= perChart);
            if (level.Value > (int)DifficultyLevel.All.Min(l => (int)l))
                Assert.True(Scoring.GetScore(ChartType.Single, DifficultyLevel.From(level.Value - 1),
                    grade.GetMinimumScoreFor(Scoring.Mix), PhoenixPlate.SuperbGame) < perChart);
        }
    }

    [Fact]
    public void HigherGradesNeedLowerLevels()
    {
        var tierValue = 14000m;
        var levels = CutlineCalculator.Grades
            .Select(g => CutlineCalculator.LevelFor(Scoring, ChartType.Single, g.Grade, tierValue))
            .ToArray();

        // AAA → S → SS → SSS: each step up in grade can only lower (or hold) the level bar.
        for (var i = 1; i < levels.Length; i++)
            Assert.True(levels[i] <= levels[i - 1],
                $"grade step {i} raised the level bar: {levels[i - 1]} → {levels[i]}");
    }

    [Fact]
    public void AnUnreachableTierReturnsNull()
    {
        // Beyond what 50 maxed charts can produce at this grade — no level clears it.
        var level = CutlineCalculator.LevelFor(Scoring, ChartType.Single, PhoenixLetterGrade.AAA, 99999m);

        Assert.Null(level);
    }

    [Fact]
    public void RisingTiersNeverLowerTheLevelBar()
    {
        var levels = new[] { 10000m, 14000m, 18000m }
            .Select(v => CutlineCalculator.LevelFor(Scoring, ChartType.Single, PhoenixLetterGrade.S, v))
            .ToArray();

        Assert.True(levels[0] <= levels[1] && levels[1] <= levels[2]);
    }
}
