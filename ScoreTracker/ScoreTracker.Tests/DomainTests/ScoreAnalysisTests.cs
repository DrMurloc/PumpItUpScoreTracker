using System;
using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ScoreAnalysisTests
{
    // The play the design doc works through: 933 notes scoring 917,168, which grades AA in
    // Phoenix and A+ in Phoenix 2 — the same number either side of that mix's re-cut.
    private static readonly JudgementCounts Sample = new(780, 120, 20, 5, 8);
    private const int SampleCombo = 700;
    private const int SampleScore = 917_168;

    [Fact]
    public void EarnedPointsSumToTheScore()
    {
        var earned = ScoreAnalysis.Earned(Sample, SampleCombo);

        Assert.Equal(831_833, earned.Perfects);
        Assert.Equal(76_785, earned.Greats);
        Assert.Equal(4_266, earned.Goods);
        Assert.Equal(533, earned.Bads);
        Assert.Equal(3_751, earned.Combo);
        // Rounding each contribution can leave the column a point or two off the ceilinged
        // score; it must never be further out than that, or the decomposition is wrong.
        Assert.InRange(earned.Total, SampleScore - 2, SampleScore + 2);
    }

    [Fact]
    public void AMissBanksNothing()
    {
        Assert.Equal(0, ScoreAnalysis.Earned(Sample, SampleCombo).For(Judgment.Miss));
    }

    [Fact]
    public void EarnedIsZeroForAnEmptyPlay()
    {
        Assert.Equal(0, ScoreAnalysis.Earned(new JudgementCounts(0, 0, 0, 0, 0), 0).Total);
    }

    // The whole point of the two-candidate baseline: a plain ladder floor can sit above where
    // perfects reach, and then the biggest contribution to the score is clipped off the bar.
    [Theory]
    [InlineData(917_168, 831_833, 825_000)] // ladder says 850k, perfects pull it to 825k
    [InlineData(1_000_000, 995_000, 975_000)] // a perfect play still leaves a window
    [InlineData(0, 0, 0)] // nothing played, nothing to make room for
    public void EarnedBaselineTakesTheLowerOfTheLadderAndThePerfectsFloor(int score, int perfects, int expected)
    {
        Assert.Equal(expected, ScoreAnalysis.EarnedBaseline(score, perfects));
    }

    [Fact]
    public void EarnedBaselineAlwaysLeavesAPerfectWindowOpen()
    {
        // Sweep real plays across the range rather than a handful of pinned cases: whatever
        // the score, the cut has to land inside the perfects segment or the bar opens on
        // greats and hides what actually carried the score.
        for (var perfects = 1; perfects <= 933; perfects++)
        {
            var counts = new JudgementCounts(perfects, 933 - perfects, 0, 0, 0);
            var earned = ScoreAnalysis.Earned(counts, perfects);
            var score = earned.Total;

            var baseline = ScoreAnalysis.EarnedBaseline(score, earned.Perfects);

            Assert.True(baseline < earned.Perfects,
                $"{perfects} perfects scored {score}: baseline {baseline} hides all {earned.Perfects} perfect points");
            Assert.True(baseline < 1_000_000, "the window must never collapse to zero width");
        }
    }

    [Fact]
    public void PointsToNextGradeReadsTheMixItIsGiven()
    {
        // 917,168 is AA in Phoenix (AA+ opens at 925k) but only A+ in Phoenix 2, whose AA
        // opens at 920k — the same score, two different climbs.
        Assert.Equal(7_832, ScoreAnalysis.PointsToNextGrade(SampleScore, MixEnum.Phoenix));
        Assert.Equal(2_832, ScoreAnalysis.PointsToNextGrade(SampleScore, MixEnum.Phoenix2));
    }

    [Fact]
    public void PointsToNextGradeIsNullAtTheTopOfTheLadder()
    {
        Assert.Null(ScoreAnalysis.PointsToNextGrade(1_000_000, MixEnum.Phoenix));
        Assert.Null(ScoreAnalysis.PointsToNextGrade(995_000, MixEnum.Phoenix2));
    }

    [Fact]
    public void ExpectedDiffTracksWhatYouActuallyGetWrong()
    {
        // Same score, two different plays. The diff has to look like each one's own next
        // attempt, which is what the sampled walk was for.
        var greatHeavy = ScoreAnalysis.ExpectedDiff(Sample, SampleCombo, 7_832);
        Assert.True(greatHeavy.Reachable);
        Assert.True(greatHeavy.Greats > greatHeavy.Misses,
            $"a play with 120 greats and 8 misses should lean on greats, got {greatHeavy}");

        var missHeavy = new JudgementCounts(700, 40, 10, 10, 60);
        var need = ScoreAnalysis.PointsToNextGrade(
            ScoreAnalysis.Earned(missHeavy, 560).Total, MixEnum.Phoenix);
        var missResult = ScoreAnalysis.ExpectedDiff(missHeavy, 560, need!.Value);

        Assert.True(missResult.Reachable);
        Assert.True(missResult.Misses > missResult.Greats,
            $"a play with 60 misses and 40 greats should lean on misses, got {missResult}");
    }

    [Fact]
    public void ExpectedDiffIsStableAcrossRepeatedCalls()
    {
        // The shipped walk samples a process-wide seeded Random, so the same play can answer
        // differently depending on what called it first. This one may not.
        var first = ScoreAnalysis.ExpectedDiff(Sample, SampleCombo, 7_832);

        foreach (var _ in Enumerable.Range(0, 25))
            Assert.Equal(first, ScoreAnalysis.ExpectedDiff(Sample, SampleCombo, 7_832));
    }

    [Fact]
    public void ExpectedDiffReportsWhenCleaningTheWholePlayStillFallsShort()
    {
        var counts = new JudgementCounts(10, 1, 0, 0, 0);

        Assert.False(ScoreAnalysis.ExpectedDiff(counts, 11, 900_000).Reachable);
    }

    [Fact]
    public void PumbilityPricesPhoenixOnLevelAndGradeAlone()
    {
        // Level 21 bases at 760; AA's modifier is 1.0, so the plate cannot move it.
        foreach (var plate in Enum.GetValues<PhoenixPlate>())
            Assert.Equal(760, ScoreAnalysis.PumbilityValue(
                MixEnum.Phoenix, ChartType.Single, 21, PhoenixLetterGrade.AA, plate));
    }

    [Fact]
    public void PumbilityPricesPhoenix2SinglesOneLevelUpTheCurve()
    {
        // An S21 prices off base(22) = 240, a D21 off base(21) = 235.
        var single = ScoreAnalysis.PumbilityValue(
            MixEnum.Phoenix2, ChartType.Single, 21, PhoenixLetterGrade.APlus, PhoenixPlate.TalentedGame);
        var doubles = ScoreAnalysis.PumbilityValue(
            MixEnum.Phoenix2, ChartType.Double, 21, PhoenixLetterGrade.APlus, PhoenixPlate.TalentedGame);

        Assert.Equal(320, single);
        Assert.Equal(313, doubles);
    }

    [Fact]
    public void PumbilityPaysNothingForPhoenix2CoOpOrBelowLevelTen()
    {
        Assert.Equal(0, ScoreAnalysis.PumbilityValue(
            MixEnum.Phoenix2, ChartType.CoOp, 21, PhoenixLetterGrade.SSSPlus, PhoenixPlate.PerfectGame));
        Assert.Equal(0, ScoreAnalysis.PumbilityValue(
            MixEnum.Phoenix2, ChartType.Single, 9, PhoenixLetterGrade.SSSPlus, PhoenixPlate.PerfectGame));
    }

    [Fact]
    public void PhoenixCoOpPricesOffItsFlatBase()
    {
        Assert.Equal(2_000, ScoreAnalysis.PumbilityValue(
            MixEnum.Phoenix, ChartType.CoOp, 1, PhoenixLetterGrade.AA, PhoenixPlate.FairGame));
    }

    // Pinned against the table in the design doc §3.5. A constant cannot serve both mixes:
    // Phoenix values span 75x and Phoenix 2 barely 2x, so the flat 10% this replaced
    // highlighted 7% of one grid and 44% of the other.
    [Fact]
    public void EquivalenceBandIsFarTighterInPhoenix2()
    {
        var phoenix = ScoreAnalysis.EquivalenceBand(MixEnum.Phoenix, ChartType.Single);
        var p2Single = ScoreAnalysis.EquivalenceBand(MixEnum.Phoenix2, ChartType.Single);
        var p2Double = ScoreAnalysis.EquivalenceBand(MixEnum.Phoenix2, ChartType.Double);

        Assert.InRange(phoenix, 0.080, 0.090);
        Assert.InRange(p2Single, 0.011, 0.013);
        Assert.InRange(p2Double, 0.011, 0.013);
        Assert.True(phoenix > p2Single * 5,
            "Phoenix's spread is an order of magnitude wider; a shared constant cannot fit both");
    }

    [Fact]
    public void NeighboursStayWithinThreeFoldersAndInsideTheLevelRange()
    {
        var middle = ScoreAnalysis.Neighbours(MixEnum.Phoenix, ChartType.Single, 21,
            PhoenixLetterGrade.AA, PhoenixPlate.TalentedGame);

        Assert.Equal(new[] { 18, 19, 20, 22, 23, 24 }, middle.Select(n => (int)n.Level));
        Assert.DoesNotContain(middle, n => (int)n.Level == 21);
    }

    [Theory]
    [InlineData(10, new[] { 11, 12, 13 })]
    [InlineData(29, new[] { 26, 27, 28 })]
    public void NeighboursClampAtBothEndsOfTheLadder(int level, int[] expected)
    {
        var neighbours = ScoreAnalysis.Neighbours(MixEnum.Phoenix, ChartType.Single, level,
            PhoenixLetterGrade.AA, PhoenixPlate.TalentedGame);

        Assert.Equal(expected, neighbours.Select(n => (int)n.Level));
    }

    [Fact]
    public void NeighboursStopSinglesAtTheHighestSingleThatExists()
    {
        var neighbours = ScoreAnalysis.Neighbours(MixEnum.Phoenix2, ChartType.Single, 25,
            PhoenixLetterGrade.AA, PhoenixPlate.TalentedGame);

        Assert.All(neighbours, n => Assert.True((int)n.Level <= ScoreAnalysis.MaxPhoenix2SingleLevel));
    }

    [Fact]
    public void NeighboursFlagAFolderWhereEvenTheTopGradeFallsShort()
    {
        // From AA on 21 (760) nothing on an 18 reaches it — SSS+ there tops out at 690.
        var neighbours = ScoreAnalysis.Neighbours(MixEnum.Phoenix, ChartType.Single, 21,
            PhoenixLetterGrade.AA, PhoenixPlate.TalentedGame);
        var eighteen = neighbours.Single(n => (int)n.Level == 18);

        Assert.Equal(PhoenixLetterGrade.SSSPlus, eighteen.Grade);
        Assert.True(eighteen.AtCeiling);
        Assert.Equal(690, eighteen.Value);
        Assert.Equal(-70, eighteen.Delta);
    }

    [Fact]
    public void CoOpAnchorsOnTheFolderWorthTheSameAsItsFlatBase()
    {
        var neighbours = ScoreAnalysis.Neighbours(MixEnum.Phoenix, ChartType.CoOp, 21,
            PhoenixLetterGrade.AA, PhoenixPlate.TalentedGame);

        // Co-op AA is 2,000, which level 29 AA matches exactly.
        var top = neighbours.Single(n => (int)n.Level == 29);
        Assert.Equal(0, top.Delta);
        Assert.Equal(PhoenixLetterGrade.AA, top.Grade);
    }
}
