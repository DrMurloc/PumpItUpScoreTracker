using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Every judgement breakdown here is a real play from the 2026-08-27 production import,
///     named where the owner confirmed which command was set. See
///     docs/design/pass-command-detection.md §4.
/// </summary>
public sealed class StageBreakCauseSolverTests
{
    [Fact]
    public void IoliteSkyRunEndingJustUnderTheSSSPlusFloorNamesThatGradeAndNoPlate()
    {
        // Owner ran Pass SSS+ on this chart five times over seven minutes; his best on it is
        // 995,140, an SSS+ by 140 points. Every attempt ended with the ceiling a fraction of a
        // note under 995,000.
        var cause = StageBreakCauseSolver.Solve(806, 1, 0, 0, 4, 1000, 21, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixLetterGrade.SSSPlus, cause.PassGrade);
        Assert.Null(cause.PassPlate);
    }

    [Fact]
    public void TheSameChartWithFewerNotesJudgedStillNamesSSSPlus()
    {
        var cause = StageBreakCauseSolver.Solve(456, 0, 0, 0, 3, 1000, 21, MixEnum.Phoenix2);

        Assert.Equal(PhoenixLetterGrade.SSSPlus, cause.PassGrade);
    }

    [Fact]
    public void ARunEndingOnItsFirstGreatNamesPerfectGame()
    {
        // The End of the World ft. Skizzo S20, 77% of the way in on a single great. Nothing but
        // a Pass PG ends a run there.
        var cause = StageBreakCauseSolver.Solve(859, 1, 0, 0, 0, 1119, 20, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixPlate.PerfectGame, cause.PassPlate);
        Assert.Null(cause.PassGrade);
    }

    [Fact]
    public void ARunCanNameBothAPlateAndAGrade()
    {
        // Antique Serenade D23: the first good/bad/miss took Ultimate Game, and the same
        // judgement dropped the ceiling under SSS+. Both are true, so both are recorded.
        var cause = StageBreakCauseSolver.Solve(877, 9, 0, 0, 1, 1000, 23, MixEnum.Phoenix2);

        Assert.Equal(PhoenixPlate.UltimateGame, cause.PassPlate);
        Assert.Equal(PhoenixLetterGrade.SSSPlus, cause.PassGrade);
    }

    [Fact]
    public void AFirstMissWithNoBadsBeforeItNamesExtremeGameRatherThanSuperbGame()
    {
        // Both would have fired. Extreme is the higher target, and the highest match wins.
        var cause = StageBreakCauseSolver.Solve(900, 5, 2, 0, 1, 1000, 21, MixEnum.Phoenix2);

        Assert.Equal(PhoenixPlate.ExtremeGame, cause.PassPlate);
    }

    [Fact]
    public void ARunThatBledOutOverSixtyMissesIsLeftAlone()
    {
        // B3 D21, 61 misses and a bad. The bar emptied; nothing downstream should run.
        var cause = StageBreakCauseSolver.Solve(451, 5, 2, 1, 61, 1100, 21, MixEnum.Phoenix2);

        Assert.False(cause.IsNonLifebarBreak);
        Assert.Null(cause.PassPlate);
        Assert.Null(cause.PassGrade);
    }

    [Fact]
    public void AMessyRunWithBadsAndMissesIsLeftAlone()
    {
        var cause = StageBreakCauseSolver.Solve(666, 62, 39, 27, 61, 1000, 21, MixEnum.Phoenix2);

        Assert.False(cause.IsNonLifebarBreak);
    }

    [Fact]
    public void ARunThatEndedSevenPerfectNotesInIsFlaggedButNotNamed()
    {
        // RexBmxTwo's Pavane: his partner was off-pad, the command fired on the other side and
        // ended this run. The bar plainly did not do it, and there is nothing here to name.
        var cause = StageBreakCauseSolver.Solve(7, 0, 0, 0, 0, 1092, 17, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.False(cause.IsNamed);
    }

    [Fact]
    public void AChartWithNoKnownLevelMakesNoClaimAtAll()
    {
        var cause = StageBreakCauseSolver.Solve(806, 1, 0, 0, 4, 1000, null, MixEnum.Phoenix2);

        Assert.False(cause.IsNonLifebarBreak);
        Assert.False(cause.IsNamed);
    }

    [Fact]
    public void AChartWithNoNoteCountStillNamesAPlateButNeverAGrade()
    {
        // The plate rule reads judgements alone; the grade rule cannot run without the count.
        // 95 breaks across 69 charts sit here today.
        var cause = StageBreakCauseSolver.Solve(859, 1, 0, 0, 0, null, 20, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixPlate.PerfectGame, cause.PassPlate);
        Assert.Null(cause.PassGrade);
    }

    [Fact]
    public void ACeilingThatIsNowhereNearAFloorNamesNoGrade()
    {
        // Eight misses on a 1,000-note level 26 leaves the bar at 868 of 3,028 and the ceiling at
        // 988,968 — a thousand points under SSS, which is more than one note away.
        var cause = StageBreakCauseSolver.Solve(700, 4, 0, 0, 8, 1000, 26, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Null(cause.PassGrade);
    }

    [Fact]
    public void AShortRunWhoseDamageCanHideAmongTheHealsIsLeftAlone()
    {
        // 60 perfects, 4 bads, 3 misses at level 15. Heal-first leaves 131 — comfortably alive —
        // but an ordering that spaces the damage through the heals keeps the multiplier crushed,
        // the perfects heal one point each, and the bar reaches zero on the 63rd of 67 notes. A
        // run with exactly these judgements can absolutely be a life bar death, so no claim.
        var cause = StageBreakCauseSolver.Solve(60, 0, 0, 4, 3, 67, 15, MixEnum.Phoenix2);

        Assert.False(cause.IsNonLifebarBreak);
        Assert.False(cause.IsNamed);
    }

    [Fact]
    public void SixMissesThatLeaveLifeUnderTheCruellestOrderingAreNotALifebarDeath()
    {
        // 115 perfects and 6 misses at level 24. The cruellest ordering of those judgements still
        // ends on 60 life of a 2,728 bar, so no ordering emptied it — and six misses is exactly
        // Marvelous Game's threshold.
        var cause = StageBreakCauseSolver.Solve(115, 0, 0, 0, 6, 1000, 24, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixPlate.MarvelousGame, cause.PassPlate);
    }

    [Fact]
    public void AnySliverOfLifeLeftIsStillProofTheBarDidNotEmpty()
    {
        // Sixteen misses at level 26: the cruellest ordering ends on 16 life of 3,028. Every heal
        // counts as a great and every mid-run death clamps and continues, so the search can only
        // land at or below the real run — 16 left means the bar held.
        var cause = StageBreakCauseSolver.Solve(700, 4, 0, 0, 16, 1000, 26, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
    }

    [Fact]
    public void AnEarlySSSPlusBreakIsFlaggedWithFewHealsBehindIt()
    {
        // Iolite Sky D21, 96 notes in: five misses and nothing but perfects. The multiplier had
        // barely started to heal, so the cruellest ordering ends on 89 life — low, but not empty.
        var cause = StageBreakCauseSolver.Solve(91, 0, 0, 0, 5, 1000, 21, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixLetterGrade.SSSPlus, cause.PassGrade);
    }

    [Fact]
    public void AnEarlySSSBreakIsFlaggedWithFewHealsBehindIt()
    {
        // Caprice of DJ Otada S21, 118 notes in; the cruellest ordering ends on 109 life.
        var cause = StageBreakCauseSolver.Solve(104, 7, 2, 0, 5, 904, 21, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Equal(PhoenixLetterGrade.SSS, cause.PassGrade);
    }

    [Fact]
    public void TheMissCountPlatesAreNamedAtTheirOwnThresholds()
    {
        var marvelous = StageBreakCauseSolver.Solve(900, 0, 0, 0, 6, 1000, 26, MixEnum.Phoenix2);
        var talented = StageBreakCauseSolver.Solve(900, 0, 0, 0, 11, 1000, 26, MixEnum.Phoenix2);

        Assert.Equal(PhoenixPlate.MarvelousGame, marvelous.PassPlate);
        Assert.Equal(PhoenixPlate.TalentedGame, talented.PassPlate);
    }

    [Fact]
    public void EachMixReadsItsOwnGradeFloors()
    {
        // A ceiling of 919,900: goods drive it down because they cost score and no life at all,
        // and with no bad or miss the combo runs straight through them. Phoenix 2 puts AA at
        // 920,000, so it just went out of reach; Phoenix puts AA at 900,000, already cleared,
        // and its next floor up is 5,100 away.
        var onPhoenix = StageBreakCauseSolver.Solve(500, 0, 100, 0, 0, 1000, 21, MixEnum.Phoenix);
        var onPhoenix2 = StageBreakCauseSolver.Solve(500, 0, 100, 0, 0, 1000, 21, MixEnum.Phoenix2);

        Assert.Equal(PhoenixLetterGrade.AA, onPhoenix2.PassGrade);
        Assert.Null(onPhoenix.PassGrade);
    }

    [Fact]
    public void AGoodHoldsTheComboSoAGoodsOnlyBreakNamesNoGradeItCouldStillReach()
    {
        // 500 perfects / 98 goods on 1,000 notes: treating the goods as combo breakers put the
        // ceiling at 919,492 and named Pass AA — but the combo runs through goods, the true
        // ceiling is 921,502, and AA was still reachable. A real Pass AA would not have ended
        // this run, so nothing may be named.
        var cause = StageBreakCauseSolver.Solve(500, 0, 98, 0, 0, 1000, 21, MixEnum.Phoenix2);

        Assert.True(cause.IsNonLifebarBreak);
        Assert.Null(cause.PassGrade);
    }

    /// <summary>
    ///     The AFK guard's wall (D36): at 51 misses the run wears the guard's tail and the
    ///     answer is "walked off" before any bar or grade arithmetic runs — even where the
    ///     level or note count is unknown, because the rule needs only the miss count.
    /// </summary>
    [Fact]
    public void FiftyOneMissesIsAWalkOffEvenWithNothingElseKnown()
    {
        var cause = StageBreakCauseSolver.Solve(500, 10, 5, 3, 51, null, null, MixEnum.Phoenix2);

        Assert.True(cause.IsWalkOff);
        Assert.False(cause.IsNonLifebarBreak);
        Assert.Null(cause.PassPlate);
        Assert.Null(cause.PassGrade);
    }

    [Fact]
    public void FiftyMissesStaysBelowTheWall()
    {
        var cause = StageBreakCauseSolver.Solve(500, 10, 5, 3, 50, null, null, MixEnum.Phoenix2);

        Assert.False(cause.IsWalkOff);
        Assert.Equal(StageBreakCause.Unattributed, cause);
    }

    [Fact]
    public void AMissThatCutsTheComboCanTakeARunMoreThanOneNotePastALine()
    {
        // Caprice of DJ Otada S21, three misses: the best reachable score now sits 1,108 points
        // under SSS on a chart where a note is worth 1,101. A miss costs its note and the combo it
        // cuts, so SSS is still the line the last judgement could have crossed.
        var crossable = StageBreakCauseSolver.CrossableGrades(593, 14, 0, 0, 3, 904, MixEnum.Phoenix2);

        Assert.Equal(new[] { PhoenixLetterGrade.SSS }, crossable);
    }

    [Fact]
    public void ARunWithNoBadOrMissFarFromEveryFloorCrossedNothing()
    {
        // With no break the combo is known exactly: the last good left the best reachable score at
        // 921,502, still over AA's 920,000 and nowhere near AA+.
        var crossable = StageBreakCauseSolver.CrossableGrades(500, 0, 98, 0, 0, 1000, MixEnum.Phoenix2);

        Assert.Empty(crossable);
    }

    [Fact]
    public void BreaksThatCouldHaveFallenAnywhereCanLeaveTwoLinesCrossable()
    {
        // Six misses 90% of the way in: bunched, they leave SSS+ just gone; spread out, SSS.
        var crossable = StageBreakCauseSolver.CrossableGrades(900, 0, 0, 0, 6, 1000, MixEnum.Phoenix2);

        Assert.Equal(new[] { PhoenixLetterGrade.SSS, PhoenixLetterGrade.SSSPlus }, crossable);
    }

    [Fact]
    public void CrossableGradesReadEachMixsOwnFloors()
    {
        // The last of a hundred goods takes the best reachable score to 919,900: under Phoenix 2's
        // AA floor at 920,000, while Phoenix's AA sits at 900,000 and its next floor 5,100 above.
        Assert.Equal(new[] { PhoenixLetterGrade.AA },
            StageBreakCauseSolver.CrossableGrades(500, 0, 100, 0, 0, 1000, MixEnum.Phoenix2));
        Assert.Empty(StageBreakCauseSolver.CrossableGrades(500, 0, 100, 0, 0, 1000, MixEnum.Phoenix));
    }

    [Fact]
    public void JudgementsThatOutnumberTheChartCrossNothing()
    {
        Assert.Empty(StageBreakCauseSolver.CrossableGrades(900, 0, 0, 0, 6, 800, MixEnum.Phoenix2));
    }

    [Fact]
    public void EveryPlateOneJudgementBrokeIsListedStrictestFirst()
    {
        // A lone bad is at once the first non-perfect, the first good/bad/miss and the first
        // bad/miss, so Perfect, Ultimate and Extreme Game all fell on it.
        Assert.Equal(new[] { PhoenixPlate.PerfectGame, PhoenixPlate.UltimateGame, PhoenixPlate.ExtremeGame },
            StageBreakCauseSolver.BrokenPlates(0, 0, 1, 0));
    }

    [Fact]
    public void APlateIsBrokenOnlyByTheJudgementJustPastItsTolerance()
    {
        // Guitar Man S20: the sixth miss is one past Marvelous Game's five; every stricter plate was
        // lost judgements earlier.
        Assert.Equal(new[] { PhoenixPlate.MarvelousGame }, StageBreakCauseSolver.BrokenPlates(8, 0, 1, 6));
    }
}
