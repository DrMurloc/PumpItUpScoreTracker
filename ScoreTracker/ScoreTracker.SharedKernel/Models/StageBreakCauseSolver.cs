using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Works out what ended a stage break: the life bar, or one of Phoenix 2's Stage Pass
///     commands. The official site prints no command on any card, so every answer here is
///     inference from the judgement counts, the chart's note count and level, and the mix's
///     grade floors (docs/design/pass-command-detection.md).
/// </summary>
public static class StageBreakCauseSolver
{
    /// <summary>
    ///     How much of the bar must survive before we call a break non-lifebar.
    ///     <see cref="LifeRemaining" /> heals first and takes every point of damage second, which
    ///     is the most survival-friendly ordering there is — so a run "surviving" on a sliver of
    ///     bar is a life bar death the arithmetic flattered. Measured: without this, 105 rows
    ///     survive on 1-5% of the bar, one of them on 15 life (D30).
    /// </summary>
    private const double SurvivingFraction = 0.05;

    /// <summary>The lowest grade the Stage Pass command list offers. Below this there is no target.</summary>
    private const PhoenixLetterGrade LowestPassGrade = PhoenixLetterGrade.A;

    /// <summary>
    ///     A null note count, a null level or an unjudged play all mean the same thing: not enough
    ///     to tell, so no claim. Never guess — an absent badge is silent, a wrong one is not.
    /// </summary>
    public static StageBreakCause Solve(int perfects, int greats, int goods, int bads, int misses,
        int? noteCount, DifficultyLevel? level, MixEnum mix)
    {
        if (level == null) return StageBreakCause.Unattributed;

        var maxLife = new LifebarSimulator(level.Value).MaxLife;
        if (LifeRemaining(perfects, greats, bads, misses, level.Value) <= maxLife * SurvivingFraction)
            return StageBreakCause.Unattributed;

        return new StageBreakCause(true,
            BrokenPlate(greats, goods, bads, misses),
            UnreachableGrade(perfects, greats, goods, bads, misses, noteCount, mix));
    }

    /// <summary>
    ///     The least life this run could have ended on. Every perfect and great heals first —
    ///     the bar caps, so a run with hundreds of them reaches full whatever the order — and
    ///     then the damage lands in whichever miss/bad interleaving hurts most. Misses scale with
    ///     the bar and bads are flat, so that ordering matters and is searched rather than assumed.
    /// </summary>
    private static int LifeRemaining(int perfects, int greats, int bads, int misses, DifficultyLevel level)
    {
        var healed = new LifebarSimulator(level);
        for (var i = 0; i < perfects; i++) healed.ApplyJudgment(Judgment.Perfect);
        for (var i = 0; i < greats; i++) healed.ApplyJudgment(Judgment.Great);

        var lowest = healed.CurrentLife;
        for (var missesFirst = 0; missesFirst <= misses; missesFirst++)
        {
            var run = healed.Fork();
            for (var i = 0; i < missesFirst; i++) run.ApplyJudgment(Judgment.Miss);
            for (var i = 0; i < bads; i++) run.ApplyJudgment(Judgment.Bad);
            for (var i = missesFirst; i < misses; i++) run.ApplyJudgment(Judgment.Miss);
            if (run.CurrentLife < lowest) lowest = run.CurrentLife;
        }

        return lowest;
    }

    /// <summary>
    ///     The highest plate this run broke by exactly one judgement — the one a Pass command
    ///     would have fired on. The tolerance table is ordered strictest first, so the first
    ///     match is the highest: a first miss with no bads before it names Extreme Game rather
    ///     than Superb Game, since both would have fired and Extreme is the higher target (D32).
    /// </summary>
    private static PhoenixPlate? BrokenPlate(int greats, int goods, int bads, int misses)
    {
        foreach (var tolerance in PhoenixPlateHelperMethods.Tolerances)
            if (tolerance.CountIn(greats, goods, bads, misses) == tolerance.MaxAllowed + 1)
                return tolerance.Plate;

        return null;
    }

    /// <summary>
    ///     The grade that stopped being reachable on this run's last judgement, if any. Compares
    ///     the best score still attainable — every remaining note perfect, best possible combo —
    ///     against each floor, and names the one it fell under by less than a single note's worth
    ///     of score (D33).
    /// </summary>
    private static PhoenixLetterGrade? UnreachableGrade(int perfects, int greats, int goods, int bads,
        int misses, int? noteCount, MixEnum mix)
    {
        if (noteCount is not > 0) return null;

        var judged = perfects + greats + goods + bads + misses;
        if (judged > noteCount.Value) return null;

        var ceiling = ReachableCeiling(perfects, greats, goods, bads, misses, judged, noteCount.Value);
        var oneNote = 995_000.0 / noteCount.Value;

        // Declaration order is ascending, so the first floor above the ceiling is the nearest one.
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
        {
            if (grade < LowestPassGrade) continue;

            double floor = grade.GetMinimumScoreFor(mix);
            if (floor <= ceiling) continue;

            return floor - ceiling < oneNote ? grade : null;
        }

        return null;
    }

    /// <summary>
    ///     The best score this run could still have finished on. Max combo is never observed on a
    ///     stage break, so it is bounded rather than estimated: the longest run available is
    ///     either everything before the break or every note after it, and a run with no combo
    ///     breaker at all can still full-combo. A point estimate cannot work here — the combo
    ///     component is 0.5% of the score, which is exactly one top-end grade band (D33, §5).
    /// </summary>
    private static double ReachableCeiling(int perfects, int greats, int goods, int bads, int misses,
        int judged, int noteCount)
    {
        var remaining = noteCount - judged;
        var comboBreakers = goods + bads + misses;
        var combo = comboBreakers == 0 ? noteCount : Math.Max(perfects + greats, remaining);

        return 1_000_000.0 *
               (0.995 * (perfects + 0.6 * greats + 0.2 * goods + 0.1 * bads + remaining)
                + 0.005 * combo) / noteCount;
    }
}
