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
    ///     The AFK guard's wall, measured against production (D36): Premium ends a stage on the
    ///     51st consecutive miss, and the journal shows the wall exactly — one bar-side break
    ///     each at 49 and 50 misses, then 19 at 51 and 26 at 52, a valley of 8 rows across
    ///     40–49 between 1,310 genuine deaths below and 382 walk-offs above (22% of Phoenix 2's
    ///     bar-side breaks; Phoenix 1 carries the same second hump at 23%). Corroborated by
    ///     shape: rows past the wall average FEWER bads and goods than deaths below it despite
    ///     tenfold the misses — nobody grazes notes from off the pad.
    /// </summary>
    public const int WalkOffMissFloor = 51;

    /// <summary>
    ///     A null note count, a null level or an unjudged play all mean the same thing: not enough
    ///     to tell, so no claim. Never guess — an absent badge is silent, a wrong one is not.
    /// </summary>
    public static StageBreakCause Solve(int perfects, int greats, int goods, int bads, int misses,
        int? noteCount, DifficultyLevel? level, MixEnum mix)
    {
        // The walk-off check needs only the miss count, so it answers even where the level or
        // note count cannot — and it goes first: a run wearing the guard's tail has nothing
        // left for the bar or grade arithmetic to say about the player.
        if (misses >= WalkOffMissFloor) return StageBreakCause.WalkedOff;

        if (level == null) return StageBreakCause.Unattributed;

        var margin = new LifebarSimulator(level.Value).MaxLife * SurvivingFraction;
        // Two screens, cheapest first. The heal-first walk is the FRIENDLIEST ordering — if
        // even that one dies, the row is refuted without touching the search. What survives it
        // still faces the adversarial minimum, because an ordering that spaces the damage
        // through the heal stream keeps the multiplier crushed and suppresses nearly all the
        // healing — which is not a curiosity, it is what a struggling run actually looks like.
        if (LifeRemaining(perfects, greats, bads, misses, level.Value) <= margin)
            return StageBreakCause.Unattributed;
        if (MinimalEndingLife(perfects + greats, bads, misses, level.Value) <= margin)
            return StageBreakCause.Unattributed;

        return new StageBreakCause(true,
            BrokenPlate(greats, goods, bads, misses),
            UnreachableGrade(perfects, greats, goods, bads, misses, noteCount, mix));
    }

    /// <summary>
    ///     The friendliest ordering: every perfect and great heals first, then the damage lands
    ///     in whichever miss/bad interleaving hurts most. An upper bound on how well the run
    ///     could have ended — used only as the cheap first screen, because the ordering that
    ///     matters for the PROOF is the cruellest one, and that is
    ///     <see cref="MinimalEndingLife" />'s job.
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
    ///     The least life ANY ordering of these judgements can end on. This is the half of the
    ///     gate that makes the flag a proof: <see cref="LifeRemaining" /> asks how well the run
    ///     could have gone, this asks how badly — and only a run whose WORST ordering still
    ///     ends above the margin provably did not die.
    ///     <para>
    ///         A Pareto search over (life, multiplier) after (heals, misses, bads) events, both
    ///         axes minimised — lower life and a lower multiplier are each worse for survival,
    ///         so a state dominated on both can never produce the minimum and is dropped.
    ///         Layers advance one heal at a time; within a layer, damage expands from the
    ///         neighbouring cells. Two deliberate conservatisms keep the answer a lower bound:
    ///         every heal is applied as a GREAT (the weaker heal on the slower ramp — swapping
    ///         a great for a perfect lowers life and multiplier at that step, and every
    ///         transition is monotone in both, so the substituted trajectory sits under the
    ///         real one for any ordering), and orderings that empty the bar mid-run clamp at
    ///         zero and continue (invalid as evidence — the run would have ended there — so
    ///         counting them only errs toward refusing to claim). A flag that survives THIS is
    ///         the certainty the journal column promises (D29).
    ///     </para>
    /// </summary>
    private static int MinimalEndingLife(int heals, int bads, int misses, DifficultyLevel level)
    {
        var cap = new LifebarSimulator(level).MaxLife;
        var layer = NextLayer(null, misses, bads, cap);
        for (var h = 0; h < heals; h++)
        {
            var next = NextLayer(layer, misses, bads, cap);
            // Every state saturated: the remaining heals are no-ops and the answer is settled.
            if (LayersEqual(next, layer)) break;

            layer = next;
        }

        return layer[misses, bads].Min(s => s.Life);
    }

    /// <summary>
    ///     One heal layer: each cell heals the previous layer's same cell (or seeds at the
    ///     start), then damage expands from the cells one miss or one bad behind it. Cells are
    ///     filled in ascending damage order, so the neighbours a cell reads are already built.
    /// </summary>
    private static List<(int Life, double Mult)>[,] NextLayer(List<(int Life, double Mult)>[,]? previous,
        int misses, int bads, int cap)
    {
        var layer = new List<(int Life, double Mult)>[misses + 1, bads + 1];
        for (var m = 0; m <= misses; m++)
        for (var b = 0; b <= bads; b++)
        {
            var states = new List<(int Life, double Mult)>();
            if (previous == null)
            {
                if (m == 0 && b == 0) states.Add((500, 0.1));
            }
            else
            {
                foreach (var (life, mult) in previous[m, b])
                    states.Add((Math.Min(life + (int)(10 * mult), cap),
                        Math.Min(mult + 0.016, LifebarSimulator.MaxLifeMultiplier)));
            }

            if (m > 0)
                foreach (var (life, mult) in layer[m - 1, b])
                    states.Add((
                        Math.Max(life + (int)(-500 * (life > 1000 ? 1000 : life) / 2000.0 - 20.0), 0),
                        Math.Max(mult - 0.7, 0.0)));
            if (b > 0)
                foreach (var (life, mult) in layer[m, b - 1])
                    states.Add((Math.Max(life - 50, 0), Math.Max(mult - 0.35, 0.0)));

            layer[m, b] = Pareto(states);
        }

        return layer;
    }

    /// <summary>Keeps only states no other state beats on BOTH axes.</summary>
    private static List<(int Life, double Mult)> Pareto(List<(int Life, double Mult)> states)
    {
        states.Sort((a, b) => a.Life != b.Life ? a.Life.CompareTo(b.Life) : a.Mult.CompareTo(b.Mult));
        var frontier = new List<(int Life, double Mult)>();
        var bestMult = double.MaxValue;
        foreach (var state in states)
            if (state.Mult < bestMult - 1e-12)
            {
                frontier.Add(state);
                bestMult = state.Mult;
            }

        return frontier;
    }

    private static bool LayersEqual(List<(int Life, double Mult)>[,] a, List<(int Life, double Mult)>[,] b)
    {
        for (var m = 0; m < a.GetLength(0); m++)
        for (var i = 0; i < a.GetLength(1); i++)
        {
            if (a[m, i].Count != b[m, i].Count) return false;
            for (var s = 0; s < a[m, i].Count; s++)
                if (a[m, i][s] != b[m, i][s])
                    return false;
        }

        return true;
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
    ///     stage break, so it is bounded rather than estimated: a point estimate cannot work here —
    ///     the combo component is 0.5% of the score, which is exactly one top-end grade band
    ///     (D33, §5). Only bads and misses break a combo; a good HOLDS it without advancing it.
    ///     So a run whose only blemishes are goods can still combo every other note, and a run
    ///     carrying a bad or miss is bounded by its longest breaker-free stretch — everything
    ///     before the break (goods transparent) or every note after it.
    /// </summary>
    private static double ReachableCeiling(int perfects, int greats, int goods, int bads, int misses,
        int judged, int noteCount)
    {
        var remaining = noteCount - judged;
        var combo = bads + misses == 0
            ? perfects + greats + remaining
            : Math.Max(perfects + greats, remaining);

        return 1_000_000.0 *
               (0.995 * (perfects + 0.6 * greats + 0.2 * goods + 0.1 * bads + remaining)
                + 0.005 * combo) / noteCount;
    }
}
