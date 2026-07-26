using System.Linq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The Life Calculator prints these as claims about the game, so they are pinned here.
///     Before this existed the same math lived in the page's code-behind and the page's prose
///     stated a level range that was really the perfect-vs-great spread
///     (docs/design/life-calculator-redesign.md).
/// </summary>
public sealed class LifebarAnalysisTests
{
    private const int HalfBar = LifebarAnalysis.VisibleLife / 2;
    private const int FullBar = LifebarAnalysis.VisibleLife - 1;

    [Theory]
    [InlineData(0, 18)]
    [InlineData(HalfBar, 38)]
    [InlineData(FullBar, 47)]
    public void PerfectFilledMissesBreakEvenAtTheseCombos(int threshold, int expected)
    {
        var combo = LifebarAnalysis.BreakEvenCombo(DifficultyLevel.From(23), Judgment.Perfect, Judgment.Miss,
            threshold);

        Assert.Equal(expected, combo);
    }

    [Theory]
    [InlineData(0, 22)]
    [InlineData(HalfBar, 47)]
    [InlineData(FullBar, 56)]
    public void GreatFilledMissesCostMoreComboThanPerfectFilledOnes(int threshold, int expected)
    {
        var combo = LifebarAnalysis.BreakEvenCombo(DifficultyLevel.From(23), Judgment.Great, Judgment.Miss, threshold);

        Assert.Equal(expected, combo);
    }

    /// <summary>
    ///     The headline correction: the page used to imply these move with level. Only the
    ///     overflow does. Levels 1-9 are excluded from the full-bar row because their overflow
    ///     is thinner than one miss (see the next test).
    /// </summary>
    [Fact]
    public void BreakEvenCombosAreIdenticalAtEveryLevel()
    {
        foreach (var level in DifficultyLevel.All)
        {
            Assert.Equal(18, LifebarAnalysis.BreakEvenCombo(level, Judgment.Perfect, Judgment.Miss, 0));
            Assert.Equal(38, LifebarAnalysis.BreakEvenCombo(level, Judgment.Perfect, Judgment.Miss, HalfBar));
            Assert.Equal(22, LifebarAnalysis.BreakEvenCombo(level, Judgment.Great, Judgment.Miss, 0));

            if (level >= 10)
                Assert.Equal(47, LifebarAnalysis.BreakEvenCombo(level, Judgment.Perfect, Judgment.Miss, FullBar));
        }
    }

    [Fact]
    public void HoldingAFullBarIsImpossibleWhileTheOverflowIsThinnerThanOneMiss()
    {
        // A miss at 1000+ life costs 270, so the bar can only stay full once the overflow
        // can absorb one. Level 9 buys 243; level 10 buys 300.
        Assert.Null(LifebarAnalysis.BreakEvenCombo(DifficultyLevel.From(9), Judgment.Perfect, Judgment.Miss, FullBar));
        Assert.NotNull(LifebarAnalysis.BreakEvenCombo(DifficultyLevel.From(10), Judgment.Perfect, Judgment.Miss,
            FullBar));
    }

    [Fact]
    public void ClearingTheCliffOnBadsRecoversTheWholeBar()
    {
        // A bad is a flat -50, so any combo that rebuilds the multiplier climbs back to max.
        var level = DifficultyLevel.From(23);

        Assert.Equal(18, LifebarAnalysis.BreakEvenCombo(level, Judgment.Perfect, Judgment.Bad, FullBar));
        Assert.True(LifebarAnalysis.SettlePoint(level, Judgment.Perfect, Judgment.Bad, 18)
                    > LifebarAnalysis.VisibleLife);
    }

    /// <summary>
    ///     The cliff is a cliff: one note below break-even the run dies outright, rather than
    ///     settling somewhere slightly lower. Under the line the multiplier never rebuilds.
    /// </summary>
    [Fact]
    public void OneNoteBelowBreakEvenTheRunDies()
    {
        var level = DifficultyLevel.From(23);

        Assert.Equal(0, LifebarAnalysis.SettlePoint(level, Judgment.Perfect, Judgment.Miss, 17));
        Assert.True(LifebarAnalysis.SettlePoint(level, Judgment.Perfect, Judgment.Miss, 18) > 0);
    }

    [Fact]
    public void SevenStraightMissesEndARunFromSongStartAtEveryLevel()
    {
        foreach (var level in DifficultyLevel.All)
        {
            Assert.Equal(7, LifebarAnalysis.ConsecutiveBreaksToFail(level, Judgment.Miss, false));
            Assert.Equal(10, LifebarAnalysis.ConsecutiveBreaksToFail(level, Judgment.Bad, false));
        }
    }

    [Fact]
    public void AFullBarBuysMoreMissesAndThatDoesScaleWithLevel()
    {
        Assert.Equal(15, LifebarAnalysis.ConsecutiveBreaksToFail(DifficultyLevel.From(23), Judgment.Miss, true));
        Assert.Equal(52, LifebarAnalysis.ConsecutiveBreaksToFail(DifficultyLevel.From(23), Judgment.Bad, true));

        var byLevel = DifficultyLevel.All
            .Select(l => LifebarAnalysis.ConsecutiveBreaksToFail(l, Judgment.Miss, true))
            .ToArray();

        Assert.Equal(byLevel.OrderBy(v => v), byLevel);
        Assert.True(byLevel.Last() > byLevel.First());
    }

    [Fact]
    public void OverflowIsThreeTimesLevelSquared()
    {
        foreach (var level in DifficultyLevel.All)
            Assert.Equal(3 * level * level, LifebarAnalysis.OverflowFor(level));
    }

    [Fact]
    public void NotesToThresholdReportsSurvivalAsNullAboveTheCliff()
    {
        var level = DifficultyLevel.From(23);

        Assert.Equal(14, LifebarAnalysis.NotesToThreshold(level, Judgment.Perfect, Judgment.Miss, 0, 0));
        Assert.Equal(414, LifebarAnalysis.NotesToThreshold(level, Judgment.Perfect, Judgment.Miss, 17, 0));
        Assert.Null(LifebarAnalysis.NotesToThreshold(level, Judgment.Perfect, Judgment.Miss, 18, 0));
    }

    [Fact]
    public void FillingTheBarTakesLongerOnGreatsThanPerfects()
    {
        var level = DifficultyLevel.From(23);

        Assert.Equal(249, LifebarAnalysis.NotesToFillBar(level, Judgment.Perfect));
        Assert.Equal(284, LifebarAnalysis.NotesToFillBar(level, Judgment.Great));
    }

    [Fact]
    public void PreviewDeltaLeavesTheLiveSimulatorAlone()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(23), true);

        var delta = LifebarAnalysis.PreviewDelta(sim, Judgment.Miss, 1);

        Assert.Equal(-270, delta);
        Assert.Equal(sim.MaxLife, sim.CurrentLife);
    }

    /// <summary>
    ///     The insight the step toggle exists to show: the same fifty perfects pay far more
    ///     once the multiplier is capped than they do straight after a miss.
    /// </summary>
    [Fact]
    public void AMissTaxesTheNextFiftyNotes()
    {
        var capped = new LifebarSimulator(DifficultyLevel.From(23));
        for (var i = 0; i < 60; i++) capped.ApplyJudgment(Judgment.Perfect);
        var wiped = capped.Fork();
        wiped.ApplyJudgment(Judgment.Miss);

        var cappedGain = LifebarAnalysis.PreviewDelta(capped, Judgment.Perfect, 50);
        var wipedGain = LifebarAnalysis.PreviewDelta(wiped, Judgment.Perfect, 50);

        Assert.Equal(450, cappedGain);
        Assert.Equal(303, wipedGain);
    }

    /// <summary>
    ///     Carrying a run across a level change has to be exact. Rebuilding by replaying
    ///     judgments back down to the old life drifts — bads move in steps of 50 — which bled
    ///     life on every tick of the Life Calculator's level slider.
    /// </summary>
    [Fact]
    public void AtResumesExactlyWithNoDrift()
    {
        var sim = LifebarSimulator.At(DifficultyLevel.From(23), 1234, .42);

        Assert.Equal(1234, sim.CurrentLife);
        Assert.Equal(.42, sim.LifeMultiplier);

        // Round-tripping through every level must not move a thing.
        var carried = sim;
        foreach (var level in new[] { 24, 25, 26, 25, 24, 23 })
            carried = LifebarSimulator.At(DifficultyLevel.From(level), carried.CurrentLife, carried.LifeMultiplier);

        Assert.Equal(1234, carried.CurrentLife);
        Assert.Equal(.42, carried.LifeMultiplier);
    }

    [Fact]
    public void AtClampsToTheNewLevelsCeiling()
    {
        var dropped = LifebarSimulator.At(DifficultyLevel.From(1), 2587, LifebarSimulator.MaxLifeMultiplier);

        Assert.Equal(1003, dropped.CurrentLife);
        Assert.Equal(LifebarSimulator.MaxLifeMultiplier, dropped.LifeMultiplier);
        Assert.Equal(0, LifebarSimulator.At(DifficultyLevel.From(20), -5, 5).CurrentLife);
        Assert.Equal(LifebarSimulator.MaxLifeMultiplier,
            LifebarSimulator.At(DifficultyLevel.From(20), 100, 5).LifeMultiplier);
    }

    [Fact]
    public void ForkCopiesTheHiddenMultiplierNotJustTheLife()
    {
        var sim = new LifebarSimulator(DifficultyLevel.From(23));
        for (var i = 0; i < 40; i++) sim.ApplyJudgment(Judgment.Perfect);

        var fork = sim.Fork();

        Assert.Equal(sim.CurrentLife, fork.CurrentLife);
        Assert.Equal(sim.LifeMultiplier, fork.LifeMultiplier);
        fork.ApplyJudgment(Judgment.Perfect);
        Assert.NotEqual(sim.CurrentLife, fork.CurrentLife);
    }
}
