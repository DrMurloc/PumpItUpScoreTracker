using System;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Pins the Phoenix 2 PUMBILITY per-chart formula: Base(level) × (grade + plate), additive.
///     The golden rows are real per-chart pumbility values collected from the live Phoenix 2
///     site by the owner (Phx2PumbilityTesting.xlsx, 2026-07) — every row was observed, not
///     derived. Rows that used the community's singles-specific UG/EG/RG plate values are
///     deliberately absent: the shared (doubles-verified) plate table is an owner decision,
///     see the TODO in ScoringConfiguration.Phoenix2PumbilityScoring.
/// </summary>
public sealed class Phoenix2PumbilityScoringTests
{
    private static ScoringConfiguration Scoring()
    {
        return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
    }

    private static double Contribution(ChartType type, int level, PhoenixLetterGrade grade, PhoenixPlate plate)
    {
        return Scoring().GetScore(type, DifficultyLevel.From(level), grade.GetMinimumScore(), plate);
    }

    [Theory]
    // Singles — observed live values (type-agnostic plates)
    [InlineData(ChartType.Single, 16, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 312.06)]
    [InlineData(ChartType.Single, 17, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 319.49)]
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 331.76)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.S, PhoenixPlate.MarvelousGame, 327.60)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.SPlus, PhoenixPlate.MarvelousGame, 329.85)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 339.30)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.AAA, PhoenixPlate.MarvelousGame, 332.76)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.AAAPlus, PhoenixPlate.TalentedGame, 336.99)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 353.91)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 354.38)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SPlus, PhoenixPlate.TalentedGame, 351.36)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 359.04)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 361.44)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.AAA, PhoenixPlate.FairGame, 345.94)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SS, PhoenixPlate.TalentedGame, 361.13)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 361.62)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 364.07)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 366.52)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.AAA, PhoenixPlate.TalentedGame, 353.50)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.AAAPlus, PhoenixPlate.TalentedGame, 358.50)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.SPlus, PhoenixPlate.TalentedGame, 366.00)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 374.00)]
    // Doubles — observed live values (including the doubles-verified UG/EG/RG)
    [InlineData(ChartType.Double, 16, PhoenixLetterGrade.SSSPlus, PhoenixPlate.UltimateGame, 318.36)]
    [InlineData(ChartType.Double, 17, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 323.79)]
    [InlineData(ChartType.Double, 17, PhoenixLetterGrade.SSSPlus, PhoenixPlate.ExtremeGame, 325.08)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SS, PhoenixPlate.TalentedGame, 324.28)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 324.72)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 329.12)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 331.32)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 331.76)]
    [InlineData(ChartType.Double, 18, PhoenixLetterGrade.SSSPlus, PhoenixPlate.UltimateGame, 333.52)]
    [InlineData(ChartType.Double, 22, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 361.44)]
    [InlineData(ChartType.Double, 23, PhoenixLetterGrade.S, PhoenixPlate.TalentedGame, 356.23)]
    [InlineData(ChartType.Double, 23, PhoenixLetterGrade.SPlus, PhoenixPlate.FairGame, 358.19)]
    [InlineData(ChartType.Double, 23, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 366.52)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.AA, PhoenixPlate.RoughGame, 342.50)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.S, PhoenixPlate.RoughGame, 362.50)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SPlus, PhoenixPlate.FairGame, 365.50)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SPlus, PhoenixPlate.TalentedGame, 366.00)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SS, PhoenixPlate.TalentedGame, 368.50)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 369.00)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 374.00)]
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 377.00)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.AAA, PhoenixPlate.RoughGame, 366.60)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.AAA, PhoenixPlate.FairGame, 367.12)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.AAAPlus, PhoenixPlate.FairGame, 372.32)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 383.76)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 386.36)]
    public void MatchesRealPerChartPumbilityObservedOnTheLiveSite(ChartType type, int level,
        PhoenixLetterGrade grade, PhoenixPlate plate, double expected)
    {
        Assert.Equal(expected, Contribution(type, level, grade, plate), 2);
    }

    [Theory]
    [InlineData(16, 210)]
    [InlineData(20, 230)]
    [InlineData(23, 245)]
    [InlineData(24, 250)]
    [InlineData(25, 260)]
    [InlineData(26, 270)]
    [InlineData(28, 290)]
    [InlineData(29, 300)]
    public void BaseValueGrowsFivePerLevelAndDoublesAboveTwentyFour(int level, int expectedBase)
    {
        Assert.Equal(expectedBase, ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(level)));
    }

    [Fact]
    public void PerfectGameKeepsTheGradeMultiplierAndAddsThePlateBonus()
    {
        // 1,000,000 = SSS+ grade (1.50) + PG plate (+0.020) → Base × 1.52, nothing more.
        var result = Scoring().GetScore(ChartType.Single, DifficultyLevel.From(20),
            PhoenixScore.From(1_000_000), PhoenixPlate.PerfectGame);
        Assert.Equal(230 * 1.52, result, 2);
    }

    [Fact]
    public void BrokenPlaysNeverContribute()
    {
        var result = Scoring().GetScore(ChartType.Single, DifficultyLevel.From(24),
            PhoenixScore.From(995_000), PhoenixPlate.RoughGame, isBroken: true);
        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(ChartType.CoOp)]
    [InlineData(ChartType.SinglePerformance)]
    [InlineData(ChartType.DoublePerformance)]
    public void ExcludedChartTypesNeverContribute(ChartType type)
    {
        var result = Scoring().GetScore(type, DifficultyLevel.From(20),
            PhoenixScore.From(995_000), PhoenixPlate.PerfectGame);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CoOpStaysExcludedEvenWhenTheCallerAsksForIt()
    {
        // includeCoOp is Phoenix-era semantics; the official Phoenix 2 formula has no CoOp.
        var result = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, true)
            .GetScore(ChartType.CoOp, DifficultyLevel.From(20), PhoenixScore.From(995_000),
                PhoenixPlate.PerfectGame);
        Assert.Equal(0, result);
    }

    [Fact]
    public void MixesWithoutAPumbilityFormulaThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScoringConfiguration.PumbilityScoring(MixEnum.XX, false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PhoenixArmIsIdenticalToTheHistoricalPhoenixConfiguration(bool includeCoOp)
    {
        var old = ScoringConfiguration.PumbilityScoring(includeCoOp);
        var mixKeyed = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, includeCoOp);

        foreach (var level in new[] { 10, 18, 22, 26 })
        foreach (var score in new[] { 830_000, 926_000, 972_000, 1_000_000 })
        foreach (var type in new[] { ChartType.Single, ChartType.Double, ChartType.CoOp })
            Assert.Equal(
                old.GetScore(type, DifficultyLevel.From(level), PhoenixScore.From(score), PhoenixPlate.RoughGame),
                mixKeyed.GetScore(type, DifficultyLevel.From(level), PhoenixScore.From(score),
                    PhoenixPlate.RoughGame));
    }
}
