using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Derived answers about the lifebar, all built by running <see cref="LifebarSimulator" />.
///     Pure functions over the simulator: no ports, no clock, no randomness.
///     The Life Calculator page states these as fact, so they live here under test rather
///     than in a page's code-behind (docs/design/life-calculator-redesign.md).
/// </summary>
public static class LifebarAnalysis
{
    /// <summary>The life a full bar tops out at, and the boundary of the visible rainbow.</summary>
    public const int VisibleLife = 1000;

    /// <summary>Every song starts here, whatever the level.</summary>
    public const int StartingLife = 500;

    // A settle point is a fixed point of "combo filler notes, then one break". Runs converge
    // geometrically, so a stable streak is the exit; the cycle cap is a backstop for the
    // handful of combos that oscillate by a point or two forever.
    private const int SettleCycleCap = 900;
    private const int SettleStableStreak = 25;

    /// <summary>
    ///     Where life ends up if you take one <paramref name="breakJudgment" /> every
    ///     <paramref name="combo" /> notes forever, starting from a full bar. Zero means the
    ///     run dies.
    /// </summary>
    public static int SettlePoint(DifficultyLevel level, Judgment filler, Judgment breakJudgment, int combo)
    {
        var sim = new LifebarSimulator(level, true);
        var last = -1;
        var stable = 0;
        for (var cycle = 0; cycle < SettleCycleCap; cycle++)
        {
            for (var i = 0; i < combo; i++) sim.ApplyJudgment(filler);
            sim.ApplyJudgment(breakJudgment);
            if (sim.CurrentLife <= 0) return 0;

            if (sim.CurrentLife == last)
            {
                if (++stable == SettleStableStreak) return sim.CurrentLife;
            }
            else
            {
                last = sim.CurrentLife;
                stable = 0;
            }
        }

        return sim.CurrentLife;
    }

    /// <summary>
    ///     The smallest combo between breaks whose settle point stays strictly above
    ///     <paramref name="threshold" />. Null when no combo reaches it — below level 10 the
    ///     overflow is thinner than one miss, so a full visible bar can't be held at all.
    /// </summary>
    public static int? BreakEvenCombo(DifficultyLevel level, Judgment filler, Judgment breakJudgment, int threshold)
    {
        for (var combo = 0; combo <= 120; combo++)
            if (SettlePoint(level, filler, breakJudgment, combo) > threshold)
                return combo;

        return null;
    }

    /// <summary>
    ///     Notes taken before life falls to <paramref name="threshold" />, starting from a full
    ///     bar and breaking every <paramref name="combo" /> notes. Null means the run never
    ///     ends — the bar recovers faster than the breaks drain it.
    /// </summary>
    public static int? NotesToThreshold(DifficultyLevel level, Judgment filler, Judgment breakJudgment, int combo,
        int threshold)
    {
        var sim = new LifebarSimulator(level, true);
        sim.ApplyJudgment(breakJudgment);
        var last = sim.CurrentLife;
        var repeats = 0;
        var notes = 0;
        while (true)
        {
            for (var i = 0; i < combo; i++)
            {
                notes++;
                sim.ApplyJudgment(filler);
                // Back to a full bar with a break still to come = a cycle that never drains.
                if (sim.CurrentLife == sim.MaxLife) return null;
            }

            sim.ApplyJudgment(breakJudgment);
            if (sim.CurrentLife == last)
            {
                if (++repeats == 10) return null;
            }
            else
            {
                last = sim.CurrentLife;
                repeats = 0;
            }

            notes++;
            if (sim.CurrentLife <= threshold) return notes;
        }
    }

    /// <summary>
    ///     How many of one judgment in a row it takes to fail, from a full bar or from the
    ///     500 life every song opens on.
    /// </summary>
    public static int ConsecutiveBreaksToFail(DifficultyLevel level, Judgment breakJudgment, bool fromFullLife)
    {
        var sim = new LifebarSimulator(level, fromFullLife);
        var count = 0;
        while (sim.CurrentLife > 0)
        {
            sim.ApplyJudgment(breakJudgment);
            count++;
        }

        return count;
    }

    /// <summary>Notes of one judgment to climb from the opening 500 life to a full bar.</summary>
    public static int NotesToFillBar(DifficultyLevel level, Judgment filler)
    {
        var sim = new LifebarSimulator(level);
        var notes = 0;
        while (sim.CurrentLife < sim.MaxLife)
        {
            sim.ApplyJudgment(filler);
            notes++;
        }

        return notes;
    }

    /// <summary>
    ///     Life this judgment would cost or pay from the given state, without committing to
    ///     it — what the page prints on each judgment key.
    /// </summary>
    public static int PreviewDelta(LifebarSimulator sim, Judgment judgment, int times)
    {
        var probe = sim.Fork();
        var before = probe.CurrentLife;
        for (var i = 0; i < times; i++) probe.ApplyJudgment(judgment);
        return probe.CurrentLife - before;
    }

    /// <summary>The overflow a level buys you: everything above the visible bar.</summary>
    public static int OverflowFor(DifficultyLevel level) => new LifebarSimulator(level).MaxLife - VisibleLife;
}
