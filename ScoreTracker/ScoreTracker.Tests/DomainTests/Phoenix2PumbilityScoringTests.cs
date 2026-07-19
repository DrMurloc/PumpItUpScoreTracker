using System;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Pins the Phoenix 2 PUMBILITY per-chart formula: Base(pricedLevel) × (grade + plate),
///     additive, where singles price one level up the base curve and sub-10 charts price at
///     zero. Two observation eras feed the golden rows: the owner's pre-launch collection
///     (Phx2PumbilityTesting.xlsx, 2026-07 — its DOUBLES rows still hold) and the launch-era
///     per-chart breakdown page my_page/pumbility.php (2026-07-19), which exposed the
///     singles +1-level pricing, the sub-10 zero, and the real A multiplier (1.28) — the
///     xlsx-era singles rows priced at Base(level) are superseded and re-derived at
///     Base(level+1) here. Rows that used the community's singles-specific UG/EG/RG plate
///     values are deliberately absent: the shared (doubles-verified) plate table is an owner
///     decision, see the TODO in ScoringConfiguration.Phoenix2PumbilityScoring.
/// </summary>
public sealed class Phoenix2PumbilityScoringTests
{
    private static ScoringConfiguration Scoring()
    {
        return ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
    }

    private static double Contribution(ChartType type, int level, PhoenixLetterGrade grade, PhoenixPlate plate)
    {
        // Build the score from the Phoenix 2 floor for the grade — a "AA" row must be a real P2 AA
        // (≥920k), not the P1 AA floor of 900k, which P2 now grades A+.
        return Scoring().GetScore(type, DifficultyLevel.From(level),
            grade.GetMinimumScoreFor(MixEnum.Phoenix2), plate);
    }

    [Theory]
    // Singles — OBSERVED on my_page/pumbility.php 2026-07-19 (the launch-era per-chart page;
    // an S(L) prices as Base(L+1))
    [InlineData(ChartType.Single, 14, PhoenixLetterGrade.A, PhoenixPlate.TalentedGame, 263.22)]
    [InlineData(ChartType.Single, 17, PhoenixLetterGrade.AAPlus, PhoenixPlate.FairGame, 306.24)]
    [InlineData(ChartType.Single, 17, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 326.92)]
    [InlineData(ChartType.Single, 17, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 329.12)]
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 336.60)]
    [InlineData(ChartType.Single, 20, PhoenixLetterGrade.S, PhoenixPlate.MarvelousGame, 342.16)]
    [InlineData(ChartType.Single, 20, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 351.56)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 354.24)]
    // Singles — the xlsx-era grade/plate combos re-derived at Base(level+1), keeping coverage
    // across levels 16–24 (the L+1 crossing of the 24-kink is the last three rows)
    [InlineData(ChartType.Single, 16, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 319.49)]
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 339.30)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.S, PhoenixPlate.MarvelousGame, 334.88)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.SPlus, PhoenixPlate.MarvelousGame, 337.18)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 346.84)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.AAA, PhoenixPlate.MarvelousGame, 339.84)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.AAAPlus, PhoenixPlate.TalentedGame, 344.16)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 361.44)]
    [InlineData(ChartType.Single, 21, PhoenixLetterGrade.SSSPlus, PhoenixPlate.SuperbGame, 361.92)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SPlus, PhoenixPlate.TalentedGame, 358.68)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 366.52)]
    [InlineData(ChartType.Single, 22, PhoenixLetterGrade.SSSPlus, PhoenixPlate.MarvelousGame, 368.97)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.AAA, PhoenixPlate.FairGame, 353.00)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SS, PhoenixPlate.TalentedGame, 368.50)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SS, PhoenixPlate.MarvelousGame, 369.00)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SSPlus, PhoenixPlate.MarvelousGame, 371.50)]
    [InlineData(ChartType.Single, 23, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 374.00)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.AAA, PhoenixPlate.TalentedGame, 367.64)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.AAAPlus, PhoenixPlate.TalentedGame, 372.84)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.SPlus, PhoenixPlate.TalentedGame, 380.64)]
    [InlineData(ChartType.Single, 24, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 388.96)]
    // Doubles — observed live values (xlsx 2026-07 + my_page/pumbility.php 2026-07-19; a D(L)
    // prices at Base(L) — no level bump, verified to the cent against the live page)
    [InlineData(ChartType.Double, 12, PhoenixLetterGrade.SSSPlus, PhoenixPlate.PerfectGame, 288.80)]
    [InlineData(ChartType.Double, 17, PhoenixLetterGrade.SSS, PhoenixPlate.MarvelousGame, 321.64)]
    [InlineData(ChartType.Double, 21, PhoenixLetterGrade.SS, PhoenixPlate.RoughGame, 345.45)]
    [InlineData(ChartType.Double, 21, PhoenixLetterGrade.SSPlus, PhoenixPlate.TalentedGame, 348.74)]
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
    // AA re-derived at the launch value 1.36 (SQL board reconstruction 2026-07-19); the xlsx
    // observation 342.50 = 250 x 1.37 was pre-launch tuning.
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.AA, PhoenixPlate.RoughGame, 340.00)]
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
        // 1,000,000 = SSS+ grade (1.50) + PG plate (+0.020) → Base × 1.52, nothing more —
        // priced one level up because it is a single (S20 → Base(21) = 235).
        var result = Scoring().GetScore(ChartType.Single, DifficultyLevel.From(20),
            PhoenixScore.From(1_000_000), PhoenixPlate.PerfectGame);
        Assert.Equal(235 * 1.52, result, 2);
    }

    [Theory]
    [InlineData(ChartType.Single, 9)]
    [InlineData(ChartType.Single, 5)]
    [InlineData(ChartType.Double, 9)]
    public void ChartsBelowLevelTenNeverContribute(ChartType type, int level)
    {
        // Observed live 2026-07-19: an S9 SSS+ UG renders 0.00 on my_page/pumbility.php.
        // Only the singles side has a live sample; the doubles side mirrors it by assumption.
        var result = Scoring().GetScore(type, DifficultyLevel.From(level),
            PhoenixScore.From(998_170), PhoenixPlate.UltimateGame);
        Assert.Equal(0, result);
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

    [Fact]
    public void PhoenixArmKeepsTheHistoricalFormulaByteIdentical()
    {
        // The Phoenix arm must stay the historical configuration: BaseRating(level) x the
        // stock letter-grade modifier, plate-blind, CoOp per the includeCoOp flag.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false);

        foreach (var level in new[] { 10, 18, 22, 26 })
        foreach (var score in new[] { 830_000, 926_000, 972_000 })
            Assert.Equal(
                DifficultyLevel.From(level).BaseRating *
                PhoenixScore.From(score).LetterGrade.GetModifier(),
                scoring.GetScore(DifficultyLevel.From(level), PhoenixScore.From(score)), 5);

        Assert.Equal(0, ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false)
            .GetScore(ChartType.CoOp, DifficultyLevel.From(10), PhoenixScore.From(950_000),
                PhoenixPlate.RoughGame));
        Assert.True(ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, true)
                        .GetScore(ChartType.CoOp, DifficultyLevel.From(10), PhoenixScore.From(950_000),
                            PhoenixPlate.RoughGame) > 0);
    }
}
