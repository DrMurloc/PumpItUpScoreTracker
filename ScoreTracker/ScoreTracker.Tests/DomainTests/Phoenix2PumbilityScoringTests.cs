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
///     Base(level+1) here. A third era follows: production import telemetry, which priced all
///     sixteen plate × chart-type cells from live pools and showed Singles paying their own
///     Extreme and Ultimate Game bonuses. Singles Rough Game is NOT among them — it pays the
///     same 0.000 a Double does, so the community table's −0.010 stays refuted while its other
///     two singles values are now pinned below.
///     <para>
///         The grade ladder splits by chart type as well, and as of 2026-08-14 EVERY rung of
///         BOTH ladders SSS+ → F is a live read — the grade tables hold no inference at all,
///         the bottom five cells closed by deliberately played small-pool charts. A passing F
///         is the ladder's real bottom rung, not an exclusion — that rule reversed twice in
///         three days, and <see cref="PassingFsPriceAsTheBottomRungNotAnExclusion" /> carries
///         the story. The base curve above level 27 is the formula's one remaining
///         extrapolation (<see cref="TheTopOfTheBaseCurveIsExtrapolatedNotMeasured" />).
///     </para>
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
    // Singles Extreme and Ultimate Game — the two plates a Single prices differently from a
    // Double, observed on live pools 2026-08-10. Base(21) 235 x (1.50 + 0.014) = 355.79 and
    // Base(20) 230 x (1.50 + 0.017) = 348.91; at the doubles bonuses these would read 355.32
    // and 348.22, so these rows are what hold the two tables apart.
    [InlineData(ChartType.Single, 20, PhoenixLetterGrade.SSSPlus, PhoenixPlate.ExtremeGame, 355.79)]
    [InlineData(ChartType.Single, 19, PhoenixLetterGrade.SSSPlus, PhoenixPlate.UltimateGame, 348.91)]
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
    // A Double reads AA at 1.37, so this is 250 x 1.37 — the pre-launch xlsx value, which the
    // live page served again on 2026-08-10 and which the 1.36 re-derivation had written off.
    // The board reconstruction that produced 1.36 was singles-tab only, so it was answering
    // for the other chart type all along.
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
    // The sub-AAA rungs, all live per-chart reads from production pools. A+ on a Double is 1.35
    // against the 1.33 a Single reads.
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.APlus, PhoenixPlate.FairGame, 351.52)]
    // A on a Double is 1.30 against the 1.28 a Single reads — five import-telemetry rows across
    // four levels and three plates, none implying anything else. The grade was interpolated
    // until these arrived and they landed on the guess exactly. The top two also pin the base
    // curve's post-24 kink at 26 and 27: Base(26) = 270 and Base(27) = 280, one +5 step each.
    // Nothing has ever been priced above 27, so Base(28) and Base(29) remain extrapolation.
    [InlineData(ChartType.Double, 24, PhoenixLetterGrade.A, PhoenixPlate.MarvelousGame, 326.50)]
    [InlineData(ChartType.Double, 25, PhoenixLetterGrade.A, PhoenixPlate.RoughGame, 338.00)]
    [InlineData(ChartType.Double, 26, PhoenixLetterGrade.A, PhoenixPlate.FairGame, 351.54)]
    [InlineData(ChartType.Double, 27, PhoenixLetterGrade.A, PhoenixPlate.FairGame, 364.56)]
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.B, PhoenixPlate.FairGame, 270.45)]
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.B, PhoenixPlate.RoughGame, 270.00)]
    [InlineData(ChartType.Single, 15, PhoenixLetterGrade.D, PhoenixPlate.MarvelousGame, 211.26)]
    // C, read three times on Singles at three different levels and two plates, and once on a
    // Double — the one row that showed the grade ladder splits below A+ as well as at it. The
    // Single rows all solve to 1.10 and the Double to 1.20, so this quartet is what holds the
    // two C values apart.
    [InlineData(ChartType.Single, 18, PhoenixLetterGrade.C, PhoenixPlate.FairGame, 247.95)]
    [InlineData(ChartType.Single, 15, PhoenixLetterGrade.C, PhoenixPlate.TalentedGame, 231.84)]
    [InlineData(ChartType.Single, 12, PhoenixLetterGrade.C, PhoenixPlate.TalentedGame, 215.28)]
    [InlineData(ChartType.Double, 12, PhoenixLetterGrade.C, PhoenixPlate.MarvelousGame, 229.14)]
    // The bottom of the Doubles ladder, played deliberately to close it (2026-08-14) and read off
    // the breakdown page. The C and D come as a PAIR on the same level and plate — so they differ
    // by grade alone and the 18.00 between them is 0.10 of Base(10) whatever Marvelous Game is
    // worth. The C confirms 1.20 a second time at a second level; the D measures 1.10 and refutes
    // the 1.15 that had been extrapolated there; the B lands on 1.25 exactly, which is what had
    // been interpolated. Between them they read the ladder's real shape instead of fitting it:
    // every step from A+ down to C is −0.05, and C → D alone is −0.10.
    [InlineData(ChartType.Double, 10, PhoenixLetterGrade.C, PhoenixPlate.MarvelousGame, 217.08)]
    [InlineData(ChartType.Double, 10, PhoenixLetterGrade.D, PhoenixPlate.MarvelousGame, 199.08)]
    [InlineData(ChartType.Double, 10, PhoenixLetterGrade.B, PhoenixPlate.ExtremeGame, 227.16)]
    // The play that overturned the F exclusion (2026-08-14): Monkey Fingers S12, an F walked
    // away from a PASSED stage with a Marvelous Game plate, priced NONZERO on the breakdown
    // page — 176.67 = Base(13) 195 × (0.90 + 0.006), exact to the cent. Beside it, a fourth
    // Singles C at a fourth level (Love is a Danger Zone pt. 2 S11 TG).
    [InlineData(ChartType.Single, 12, PhoenixLetterGrade.F, PhoenixPlate.MarvelousGame, 176.67)]
    [InlineData(ChartType.Single, 11, PhoenixLetterGrade.C, PhoenixPlate.TalentedGame, 209.76)]
    // And the Doubles F that closed the table the same day: Get Your Groove On D10 SG,
    // 181.44 = Base(10) 180 × (1.00 + 0.008) — the last grade cell in either ladder to be
    // measured, landing exactly on the value its neighbours predicted.
    [InlineData(ChartType.Double, 10, PhoenixLetterGrade.F, PhoenixPlate.SuperbGame, 181.44)]
    public void MatchesRealPerChartPumbilityObservedOnTheLiveSite(ChartType type, int level,
        PhoenixLetterGrade grade, PhoenixPlate plate, double expected)
    {
        Assert.Equal(expected, Contribution(type, level, grade, plate), 2);
    }

    [Theory]
    // Every cell where the two chart types disagree. A Single and a Double are compared at the
    // level that gives them the SAME base — a Single prices one level up, so an S(L-1) and a
    // D(L) share a base and any difference left is the table's alone. Note the direction: a
    // Single pays MORE on the two split plates and LESS on all seven split grades, and the
    // grade gap widens going down the ladder until it PLATEAUS at −0.10 from C down — it does
    // not keep widening, which is what the D row read as while it was still extrapolated.
    // Every row here is a difference of two live readings, F included.
    [InlineData(PhoenixLetterGrade.SSSPlus, PhoenixPlate.ExtremeGame, 0.002)]
    [InlineData(PhoenixLetterGrade.SSSPlus, PhoenixPlate.UltimateGame, 0.001)]
    [InlineData(PhoenixLetterGrade.AA, PhoenixPlate.RoughGame, -0.01)]
    [InlineData(PhoenixLetterGrade.APlus, PhoenixPlate.RoughGame, -0.02)]
    [InlineData(PhoenixLetterGrade.A, PhoenixPlate.RoughGame, -0.02)]
    [InlineData(PhoenixLetterGrade.B, PhoenixPlate.RoughGame, -0.05)]
    [InlineData(PhoenixLetterGrade.C, PhoenixPlate.RoughGame, -0.10)]
    [InlineData(PhoenixLetterGrade.D, PhoenixPlate.RoughGame, -0.10)]
    [InlineData(PhoenixLetterGrade.F, PhoenixPlate.RoughGame, -0.10)]
    public void SinglesAndDoublesPriceTheSplitCellsDifferently(PhoenixLetterGrade grade, PhoenixPlate plate,
        double expectedGapPerBasePoint)
    {
        // Guards the split itself, not any one number: collapsing the two tables back into one
        // would zero every gap here while leaving each type's own golden rows intact.
        const int doublesLevel = 20;
        var expectedBase = ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(doublesLevel));

        var singles = Contribution(ChartType.Single, doublesLevel - 1, grade, plate);
        var doubles = Contribution(ChartType.Double, doublesLevel, grade, plate);

        Assert.Equal(expectedGapPerBasePoint * expectedBase, singles - doubles, 2);
    }

    [Theory]
    [InlineData(ChartType.Single)]
    [InlineData(ChartType.Double)]
    public void ThePricedBaseAgreesWithWhatTheFormulaActuallyCharges(ChartType type)
    {
        // The folder projections and the rating table price a whole folder without going through
        // GetScore, so the base they use has to be the base it uses. Asserted across the range
        // rather than at a point because the two ways of saying "one level up" — Base(level + 1)
        // and Base(level) plus the step — agree everywhere except at 29, where the first runs off
        // the end of DifficultyLevel and quietly clamps.
        for (var level = 10; level <= (int)DifficultyLevel.Max; level++)
        {
            var priced = ScoringConfiguration.Phoenix2PricedBase(type, DifficultyLevel.From(level));

            // SSS+ with a Rough Game adds nothing to the grade, so the score IS base x 1.50.
            var viaFormula = Contribution(type, level, PhoenixLetterGrade.SSSPlus, PhoenixPlate.RoughGame) / 1.50;
            Assert.Equal(viaFormula, priced, 6);
        }
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

    [Theory]
    [InlineData(ChartType.Single, 20, PhoenixPlate.RoughGame, 0.90)]
    [InlineData(ChartType.Single, 24, PhoenixPlate.MarvelousGame, 0.906)]
    [InlineData(ChartType.Double, 20, PhoenixPlate.UltimateGame, 1.016)]
    [InlineData(ChartType.Double, 12, PhoenixPlate.FairGame, 1.002)]
    public void PassingFsPriceAsTheBottomRungNotAnExclusion(ChartType type, int level,
        PhoenixPlate plate, double expectedMultiplier)
    {
        // A PASSED stage, not a broken one — 271,620 is a real unbroken F seen in import
        // telemetry, so this is a score a player can actually hold.
        //
        // This test asserted ZERO until 2026-08-14, and the reversal is the story. "A passing F
        // is an exclusion" was the owner's ruling, adopted when the one F ever seen rendering on
        // the breakdown page showed 0.00 — but that chart was BELOW LEVEL 10, so its zero was
        // the sub-10 rule wearing an F grade, and the ruling had no observation behind it at
        // all. The deliberately played Monkey Fingers S12 F MG (see the golden rows) then priced
        // NONZERO at exactly Base(13) × (0.90 + 0.006): a passing F is the ladder's real bottom
        // rung. Both types are measurements now — the Doubles 1.00 closed the same day on Get
        // Your Groove On D10 SG (also in the golden rows).
        var pricedBase = ScoringConfiguration.Phoenix2PricedBase(type, DifficultyLevel.From(level));

        var result = Scoring().GetScore(type, DifficultyLevel.From(level),
            PhoenixScore.From(271_620), plate);

        Assert.Equal(pricedBase * expectedMultiplier, result, 2);
    }

    [Theory]
    [InlineData(28, 290)]
    [InlineData(29, 300)]
    public void TheTopOfTheBaseCurveIsExtrapolatedNotMeasured(int level, double extrapolatedBase)
    {
        // The successor to a ratchet that used to guard the interpolated grade rungs — every
        // one of those is a live reading now, so the mechanism guards the one thing in the
        // formula that still is not: the top of the base curve.
        //
        // Base holds 130 + 5L, plus 5 more per level past 24, everywhere it has been observed:
        // levels 10 through 27. Nothing above 27 has ever been priced, because the only Phoenix 2
        // charts up there are five Doubles — 1949, Dead End, Neo Catharsis and Paradoxx at 28,
        // 1948 at 29 — and none has entered an imported pool. These two rungs are therefore the
        // curve continued rather than the curve read.
        //
        // Worth pinning because the grade ladder just demonstrated both outcomes from exactly
        // this kind of reasoning on a single day: extrapolating its step gave the right A and B
        // and the WRONG D. A future D28 reading should be a deliberate edit here, not a silent
        // agreement.
        Assert.Equal(extrapolatedBase,
            ScoringConfiguration.Phoenix2BaseRating(DifficultyLevel.From(level)), 2);
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
                PhoenixScore.From(score).LetterGradeFor(MixEnum.Phoenix).GetModifier(),
                scoring.GetScore(DifficultyLevel.From(level), PhoenixScore.From(score)), 5);

        Assert.Equal(0, ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, false)
            .GetScore(ChartType.CoOp, DifficultyLevel.From(10), PhoenixScore.From(950_000),
                PhoenixPlate.RoughGame));
        Assert.True(ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, true)
                        .GetScore(ChartType.CoOp, DifficultyLevel.From(10), PhoenixScore.From(950_000),
                            PhoenixPlate.RoughGame) > 0);
    }
}
