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

    private const double ScoreScale = 1_000_000.0;
    private const double NoteShare = 0.995;
    private const double ComboShare = 0.005;

    /// <summary>Score arithmetic in doubles lands a hair either side of an integer floor.</summary>
    private const double FloorTolerance = 1e-6;

    /// <summary>The judgements that can move the best reachable score; a perfect never does.</summary>
    private static readonly Judgment[] ScoreMovingJudgments =
        { Judgment.Great, Judgment.Good, Judgment.Bad, Judgment.Miss };

    /// <summary>
    ///     A null level or an unjudged play leaves the life bar unsized, so no claim. A run the bar
    ///     provably did not end names the plate it broke by exactly one judgement and, where the note
    ///     count allows, the grade its last judgement could have crossed.
    /// </summary>
    public static StageBreakCause Solve(int perfects, int greats, int goods, int bads, int misses,
        int? noteCount, DifficultyLevel? level, MixEnum mix)
    {
        // The walk-off check needs only the miss count, so it answers even where the level or
        // note count cannot — and it goes first: a run wearing the guard's tail has nothing
        // left for the bar or grade arithmetic to say about the player.
        if (misses >= WalkOffMissFloor) return StageBreakCause.WalkedOff;

        if (level == null) return StageBreakCause.Unattributed;

        // Two screens, cheapest first. The heal-first walk is the FRIENDLIEST ordering — if
        // even that one empties the bar, the row is refuted without touching the search. What
        // survives it still faces the adversarial minimum, because an ordering that spaces the
        // damage through the heal stream keeps the multiplier crushed and suppresses nearly all
        // the healing — which is not a curiosity, it is what a struggling run actually looks like.
        // A run whose cruellest ordering still ends with any life left did not empty the bar.
        if (LifeRemaining(perfects, greats, bads, misses, level.Value) <= 0)
            return StageBreakCause.Unattributed;
        if (MinimalEndingLife(perfects + greats, bads, misses, level.Value) <= 0)
            return StageBreakCause.Unattributed;

        return new StageBreakCause(true,
            BrokenPlate(greats, goods, bads, misses),
            NamedGrade(perfects, greats, goods, bads, misses, noteCount, mix));
    }

    /// <summary>
    ///     Solves one player's stage breaks on one chart in one session together, in input order.
    ///     Each run is solved alone first. A player replaying a chart keeps the command they set, so
    ///     the targets that fit every run the bar did not end name all of them: the grade every such
    ///     run could have crossed — the one most of their evenly spread guesses pick when more than one
    ///     fits, the higher on a tie — and the highest plate every such run broke by exactly one
    ///     judgement. A target one run fits and another rules out names none of them. When nothing
    ///     fits them all, the command changed and each run keeps its own answer. A run that fits no
    ///     target on its own sits out and keeps its empty answer: the player's command could not have
    ///     ended it, so it says nothing about which command was set.
    /// </summary>
    public static IReadOnlyList<StageBreakCause> SolveStreak(IReadOnlyList<JudgementCounts> runs, int? noteCount,
        DifficultyLevel? level, MixEnum mix)
    {
        var causes = runs
            .Select(run => Solve(run.Perfects, run.Greats, run.Goods, run.Bads, run.Misses, noteCount, level, mix))
            .ToArray();
        var candidates = Enumerable.Range(0, runs.Count)
            .Where(i => causes[i].IsNonLifebarBreak && causes[i].IsNamed)
            .ToArray();
        if (candidates.Length < 2) return causes;

        IEnumerable<PhoenixPlate> sharedPlates = Plates(runs[candidates[0]]);
        foreach (var i in candidates.Skip(1)) sharedPlates = sharedPlates.Intersect(Plates(runs[i]));
        var plates = sharedPlates.ToArray();

        var notes = noteCount.GetValueOrDefault();
        var grades = Array.Empty<PhoenixLetterGrade>();
        if (notes > 0)
        {
            IEnumerable<PhoenixLetterGrade> sharedGrades = Crossable(runs[candidates[0]]);
            foreach (var i in candidates.Skip(1)) sharedGrades = sharedGrades.Intersect(Crossable(runs[i]));
            grades = sharedGrades.OrderBy(grade => grade).ToArray();
        }

        if (grades.Length == 0 && plates.Length == 0) return causes;

        PhoenixLetterGrade? named = grades.Length switch
        {
            0 => null,
            1 => grades[0],
            _ => candidates
                .Select(i => EvenlySpreadGuess(runs[i].Perfects, runs[i].Greats, runs[i].Goods, runs[i].Bads,
                    runs[i].Misses, notes, mix, grades))
                .GroupBy(guess => guess)
                .OrderByDescending(votes => votes.Count())
                .ThenByDescending(votes => votes.Key)
                .First().Key
        };
        PhoenixPlate? plate = plates.Length == 0 ? null : plates[0];

        foreach (var i in candidates) causes[i] = causes[i] with { PassPlate = plate, PassGrade = named };
        return causes;

        IReadOnlyList<PhoenixLetterGrade> Crossable(JudgementCounts run)
        {
            return CrossableGrades(run.Perfects, run.Greats, run.Goods, run.Bads, run.Misses, notes, mix);
        }

        IReadOnlyList<PhoenixPlate> Plates(JudgementCounts run)
        {
            return BrokenPlates(run.Greats, run.Goods, run.Bads, run.Misses);
        }
    }

    /// <summary>
    ///     Every grade this run's last judgement could have put out of reach, ascending. A grade
    ///     qualifies when some placement of the run's bads and misses among its judged notes leaves
    ///     the best reachable score at or above the grade's floor just before the last judgement and
    ///     below it just after. The best reachable score is every remaining note perfect with the
    ///     longest combo still possible; only bads and misses break a combo, and a good holds it
    ///     without advancing it.
    /// </summary>
    public static IReadOnlyList<PhoenixLetterGrade> CrossableGrades(int perfects, int greats, int goods, int bads,
        int misses, int noteCount, MixEnum mix)
    {
        if (noteCount <= 0 || perfects + greats + goods + bads + misses > noteCount)
            return Array.Empty<PhoenixLetterGrade>();

        return Enum.GetValues<PhoenixLetterGrade>()
            .Where(grade => grade >= LowestPassGrade)
            .Where(grade => CouldHaveJustCrossed(perfects, greats, goods, bads, misses, noteCount,
                grade.GetMinimumScoreFor(mix)))
            .ToArray();
    }

    /// <summary>
    ///     Every plate this run broke by exactly one judgement, strictest first — the plates a Pass
    ///     command would have fired on as the run ended.
    /// </summary>
    public static IReadOnlyList<PhoenixPlate> BrokenPlates(int greats, int goods, int bads, int misses)
    {
        return PhoenixPlateHelperMethods.Tolerances
            .Where(tolerance => tolerance.CountIn(greats, goods, bads, misses) == tolerance.MaxAllowed + 1)
            .Select(tolerance => tolerance.Plate)
            .ToArray();
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
    ///     ends with life left provably did not die.
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
    ///     would have fired on. <see cref="BrokenPlates" /> lists strictest first, so the first
    ///     entry is the highest: a first miss with no bads before it names Extreme Game rather
    ///     than Superb Game, since both would have fired and Extreme is the higher target (D32).
    /// </summary>
    private static PhoenixPlate? BrokenPlate(int greats, int goods, int bads, int misses)
    {
        var plates = BrokenPlates(greats, goods, bads, misses);
        return plates.Count == 0 ? null : plates[0];
    }

    /// <summary>
    ///     The grade this run's last judgement put out of reach: the one grade it could have crossed,
    ///     or the evenly spread guess when it could have crossed more than one.
    /// </summary>
    private static PhoenixLetterGrade? NamedGrade(int perfects, int greats, int goods, int bads, int misses,
        int? noteCount, MixEnum mix)
    {
        if (noteCount is not > 0) return null;

        var crossable = CrossableGrades(perfects, greats, goods, bads, misses, noteCount.Value, mix);
        return crossable.Count switch
        {
            0 => null,
            1 => crossable[0],
            _ => EvenlySpreadGuess(perfects, greats, goods, bads, misses, noteCount.Value, mix, crossable)
        };
    }

    /// <summary>
    ///     The candidate the run most likely just fell under. The estimate puts the killing judgement
    ///     last and spreads the other bads and misses evenly through the run, so the longest combo still
    ///     possible is the judged combo notes shared across the stretches between breaks, or every note
    ///     left when that is longer. The guess is the nearest candidate floor above that best reachable
    ///     score, or the highest candidate when the score clears them all. Candidates are ascending.
    /// </summary>
    private static PhoenixLetterGrade EvenlySpreadGuess(int perfects, int greats, int goods, int bads, int misses,
        int noteCount, MixEnum mix, IReadOnlyList<PhoenixLetterGrade> candidates)
    {
        var remaining = noteCount - (perfects + greats + goods + bads + misses);
        var breaks = bads + misses;
        var combo = breaks == 0
            ? perfects + greats + remaining
            : Math.Max((perfects + greats + breaks - 1) / breaks, remaining);
        var estimate = ScoreScale * (NoteShare * (perfects + 0.6 * greats + 0.2 * goods + 0.1 * bads + remaining)
                                     + ComboShare * combo) / noteCount;

        foreach (var candidate in candidates)
        {
            double floor = candidate.GetMinimumScoreFor(mix);
            if (floor > estimate) return candidate;
        }

        return candidates[^1];
    }

    /// <summary>
    ///     Whether some placement of the run's bads and misses puts the best reachable score at or
    ///     above <paramref name="floor" /> before the last judgement and below it after. The judged
    ///     notes that advance a combo split into finished stretches and the stretch still running
    ///     when the run ended. A longer finished stretch only raises the score on both sides of the
    ///     last judgement, so for each length of the running stretch the shortest finished stretch
    ///     that still reaches the floor beforehand is the one to test afterwards.
    /// </summary>
    private static bool CouldHaveJustCrossed(int perfects, int greats, int goods, int bads, int misses,
        int noteCount, double floor)
    {
        var remainingAfter = noteCount - (perfects + greats + goods + bads + misses);
        var remainingBefore = remainingAfter + 1;
        var noteCredit = perfects + 0.6 * greats + 0.2 * goods + 0.1 * bads;
        var perComboNote = ScoreScale * ComboShare / noteCount;
        var notesAfter = ScoreScale * NoteShare * (noteCredit + remainingAfter) / noteCount;
        var threshold = floor - FloorTolerance;

        foreach (var last in ScoreMovingJudgments)
        {
            var (count, credit) = last switch
            {
                Judgment.Great => (greats, 0.6),
                Judgment.Good => (goods, 0.2),
                Judgment.Bad => (bads, 0.1),
                _ => (misses, 0.0)
            };
            if (count == 0) continue;

            var notesBefore = ScoreScale * NoteShare * (noteCredit - credit + remainingBefore) / noteCount;
            var comboNotes = perfects + greats - (last == Judgment.Great ? 1 : 0);
            var breaks = bads + misses - (last is Judgment.Bad or Judgment.Miss ? 1 : 0);

            // Before the last judgement no combo is longer than one unbroken run through every note
            // left; after it, the combo still to come is at least the notes left.
            if (notesBefore + perComboNote * (comboNotes + remainingBefore) < threshold) continue;
            if (notesAfter + perComboNote * remainingAfter >= threshold) continue;

            for (var running = 0; running <= comboNotes; running++)
            {
                var finishedNotes = comboNotes - running;
                if (breaks == 0 && finishedNotes > 0) continue;

                var finished = breaks == 0 ? 0 : (finishedNotes + breaks - 1) / breaks;
                if (notesBefore + perComboNote * Math.Max(finished, running + remainingBefore) < threshold)
                {
                    var needed = (int)Math.Ceiling((threshold - notesBefore) / perComboNote);
                    if (needed > finishedNotes) continue;

                    finished = Math.Max(finished, needed);
                    if (notesBefore + perComboNote * Math.Max(finished, running + remainingBefore) < threshold)
                        continue;
                }

                var comboAfter = last switch
                {
                    Judgment.Great => Math.Max(finished, running + remainingBefore),
                    Judgment.Good => Math.Max(finished, running + remainingAfter),
                    _ => Math.Max(Math.Max(finished, running), remainingAfter)
                };
                if (notesAfter + perComboNote * comboAfter < threshold) return true;
            }
        }

        return false;
    }
}
