using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     What each judgement banked, before anything was lost. Misses have no field because a
///     miss earns nothing; <see cref="For" /> reports zero for one.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record EarnedPoints(int Perfects, int Greats, int Goods, int Bads, int Combo)
{
    public int Total => Perfects + Greats + Goods + Bads + Combo;

    public int For(Judgment judgment)
    {
        return judgment switch
        {
            Judgment.Perfect => Perfects,
            Judgment.Great => Greats,
            Judgment.Good => Goods,
            Judgment.Bad => Bads,
            _ => 0
        };
    }
}

/// <summary>
///     A plausible next play that reaches the grade above: how many of each judgement it
///     cleans up. <see cref="Reachable" /> is false when cleaning the whole play still falls
///     short, which combo alone can cause.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ExpectedImprovement(int Greats, int Goods, int Bads, int Misses, int Notes, bool Reachable)
{
    public static readonly ExpectedImprovement OutOfReach = new(0, 0, 0, 0, 0, false);
}

/// <summary>
///     A nearby folder and the grade on it worth closest to what you have.
///     <see cref="AtCeiling" /> marks a folder where even SSS+ falls short and
///     <see cref="AtFloor" /> one where even F overshoots — a closest grade is not always a
///     close one, and presenting a limit as a match would be a lie.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FolderNeighbour(
    ChartType Type,
    DifficultyLevel Level,
    PhoenixLetterGrade Grade,
    int Value,
    int Delta,
    bool AtCeiling,
    bool AtFloor);

/// <summary>
///     Derived answers about one score screen and what a play on it is worth. Pure functions
///     over the scoring model — no ports, no clock, no randomness. The Phoenix Calculator
///     states these as fact, so they live here under test rather than in a page's code-behind
///     (docs/design/phoenix-calculator-redesign.md).
/// </summary>
public static class ScoreAnalysis
{
    /// <summary>The share of a score carried by judgements rather than by max combo.</summary>
    private const double JudgementShare = 0.995;

    /// <summary>The share of a score carried by max combo alone.</summary>
    private const double ComboShare = 0.005;

    private const int PerfectScore = 1_000_000;

    /// <summary>Weights the game pays per judgement — a good banks a fifth, a miss nothing.</summary>
    private static readonly IReadOnlyDictionary<Judgment, double> Weights =
        new Dictionary<Judgment, double>
        {
            [Judgment.Perfect] = 1.0,
            [Judgment.Great] = 0.6,
            [Judgment.Good] = 0.2,
            [Judgment.Bad] = 0.1,
            [Judgment.Miss] = 0.0
        };

    /// <summary>Folders either side that step 3 offers as alternatives.</summary>
    public const int NeighbourReach = 3;

    /// <summary>The lowest folder that scores anything in either Phoenix-family mix.</summary>
    public const int LowestScoringLevel = 10;

    /// <summary>Phoenix prices co-op off a flat base rather than off a level.</summary>
    public const int PhoenixCoOpBaseRating = 2000;

    private static readonly IReadOnlyDictionary<MixEnum, ScoringConfiguration> PumbilityConfigs =
        new Dictionary<MixEnum, ScoringConfiguration>
        {
            [MixEnum.Phoenix] = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix, true),
            [MixEnum.Phoenix2] = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)
        };

    /// <summary>
    ///     What each judgement banked — the complement of the per-judgement losses the score
    ///     screen already exposes.
    ///     <para>
    ///         Rounds rather than truncating, unlike the loss side, which casts to match the
    ///         shipped screen. Truncating five contributions loses up to five points and the
    ///         column stops adding up to the score it decomposes; rounding lands on it. The
    ///         two sides can still disagree by a point or two, which is what the page's
    ///         existing rounding footnote covers.
    ///     </para>
    /// </summary>
    public static EarnedPoints Earned(JudgementCounts counts, int maxCombo)
    {
        var notes = counts.NoteCount;
        if (notes <= 0) return new EarnedPoints(0, 0, 0, 0, 0);

        int Banked(Judgment judgment, int count)
        {
            return (int)Math.Round(JudgementShare * Weights[judgment] * count / notes * PerfectScore);
        }

        return new EarnedPoints(
            Banked(Judgment.Perfect, counts.Perfects),
            Banked(Judgment.Great, counts.Greats),
            Banked(Judgment.Good, counts.Goods),
            Banked(Judgment.Bad, counts.Bads),
            (int)Math.Round(ComboShare * maxCombo / notes * PerfectScore));
    }

    /// <summary>
    ///     The axis floor for the earned bar: the LOWER of a grade-ladder floor and what
    ///     perfects alone banked, floored to 25,000.
    ///     <para>
    ///         The ladder tightens as the grades do — six of the sixteen live inside the last
    ///         3% — but a ladder floor alone can sit above where perfects reach, and then the
    ///         biggest contribution to the score is clipped clean off the bar. Taking the
    ///         lower guarantees the cut lands inside the perfects segment, so every play shows
    ///         a perfect window (docs/design/phoenix-calculator-redesign.md §3.2).
    ///     </para>
    /// </summary>
    public static int EarnedBaseline(PhoenixScore score, int perfectsBanked)
    {
        var raw = (int)score;
        var ladder = raw switch
        {
            > 990_000 => 975_000,
            > 975_000 => 950_000,
            > 950_000 => 925_000,
            > 925_000 => 900_000,
            > 900_000 => 850_000,
            _ => raw / 100_000 * 100_000
        };

        var perfectFloor = perfectsBanked / 25_000 * 25_000;
        // A perfects total landing exactly on a 25k line would leave zero visible width.
        if (perfectFloor >= perfectsBanked) perfectFloor -= 25_000;

        return Math.Max(0, Math.Min(ladder, perfectFloor));
    }

    /// <summary>
    ///     Points from <paramref name="score" /> to the floor of the next grade in
    ///     <paramref name="mix" />, or null at the top of the ladder.
    /// </summary>
    public static int? PointsToNextGrade(PhoenixScore score, MixEnum mix)
    {
        var grade = score.LetterGradeFor(mix);
        if (grade == PhoenixLetterGrade.SSSPlus) return null;
        return (int)(grade + 1).GetMinimumScoreFor(mix) - (int)score;
    }

    /// <summary>
    ///     What a plausible next play cleans up to gain <paramref name="need" /> points.
    ///     <para>
    ///         The shipped score screen answers this by sampling a walk that upgrades a note
    ///         drawn in proportion to what you still get wrong, which is the right model — the
    ///         diff should look like a realistic improvement, not like fixing one judgement
    ///         type. Only the sampling is a problem: a process-wide seeded Random makes the
    ///         answer depend on call order and differ between two refreshes of the same play.
    ///         Allocating fractionally instead of drawing gives that same walk's expected
    ///         diff, deterministically (docs/design/phoenix-calculator-redesign.md §3.4).
    ///     </para>
    /// </summary>
    public static ExpectedImprovement ExpectedDiff(JudgementCounts counts, int maxCombo, int need)
    {
        var notes = counts.NoteCount;
        if (notes <= 0 || need <= 0) return ExpectedImprovement.OutOfReach;

        // Upgrading a note to a perfect banks the weight it was missing; anything that also
        // broke the chain hands back a point of combo on top.
        double GainPerNote(Judgment judgment)
        {
            var judgementGain = JudgementShare * (1.0 - Weights[judgment]) / notes * PerfectScore;
            var comboGain = judgment == Judgment.Great ? 0 : ComboShare / notes * PerfectScore;
            return judgementGain + comboGain;
        }

        double greats = counts.Greats, goods = counts.Goods, bads = counts.Bads, misses = counts.Misses;
        var ceiling = counts.Greats + counts.Goods + counts.Bads + counts.Misses;
        double gained = 0;
        var steps = 0;

        while (gained < need && steps < ceiling)
        {
            var pool = greats + goods + bads + misses;
            if (pool <= double.Epsilon) break;

            var greatShare = greats / pool;
            var goodShare = goods / pool;
            var badShare = bads / pool;
            var missShare = misses / pool;

            gained += GainPerNote(Judgment.Great) * greatShare
                      + GainPerNote(Judgment.Good) * goodShare
                      + GainPerNote(Judgment.Bad) * badShare
                      + GainPerNote(Judgment.Miss) * missShare;

            greats -= greatShare;
            goods -= goodShare;
            bads -= badShare;
            misses -= missShare;
            steps++;
        }

        if (gained < need) return ExpectedImprovement.OutOfReach;

        return new ExpectedImprovement(
            (int)Math.Round(counts.Greats - greats),
            (int)Math.Round(counts.Goods - goods),
            (int)Math.Round(counts.Bads - bads),
            (int)Math.Round(counts.Misses - misses),
            steps,
            true);
    }

    /// <summary>
    ///     What one chart is worth in PUMBILITY. Phoenix prices on level and grade alone;
    ///     Phoenix 2 prices singles one level up its base curve, adds the plate bonus, and
    ///     pays nothing below level 10 or for co-op.
    /// </summary>
    public static int PumbilityValue(MixEnum mix, ChartType type, DifficultyLevel level,
        PhoenixLetterGrade grade, PhoenixPlate plate)
    {
        if (!PumbilityConfigs.TryGetValue(mix, out var config)) return 0;

        if (mix != MixEnum.Phoenix2)
        {
            var phoenixBase = type == ChartType.CoOp ? PhoenixCoOpBaseRating : level.BaseRating;
            return (int)(phoenixBase * config.LetterGradeModifiers[grade]);
        }

        if (type == ChartType.CoOp || level < LowestScoringLevel) return 0;
        var effective = type == ChartType.Single
            ? DifficultyLevel.From(Math.Min(level + 1, DifficultyLevel.Max))
            : level;
        var baseRating = ScoringConfiguration.Phoenix2BaseRating(effective);
        return (int)(baseRating * (config.LetterGradeModifiers[grade] + config.PlateModifiers[plate]));
    }

    /// <summary>
    ///     How close two folders must be to read as worth the same. Half of one folder step,
    ///     measured off the mix's own base curve.
    ///     <para>
    ///         A constant cannot serve both mixes: Phoenix values span 75x (base 100 to 2000)
    ///         while Phoenix 2 spans barely 2x, so a flat 10% highlights 7% of one grid and
    ///         44% of the other. Deriving it lands on about 5% in both and re-tunes itself if
    ///         a base curve is ever re-derived (docs/design/phoenix-calculator-redesign.md §3.5).
    ///     </para>
    /// </summary>
    public static double EquivalenceBand(MixEnum mix, ChartType type)
    {
        var ratios = new List<double>();
        var cap = (int)MaxLevelFor(mix, type);
        for (var level = LowestScoringLevel; level < cap; level++)
        {
            double below = BaseRatingFor(mix, type, level);
            double above = BaseRatingFor(mix, type, level + 1);
            if (below > 0 && above > 0) ratios.Add(above / below - 1);
        }

        if (ratios.Count == 0) return 0.05;
        ratios.Sort();
        return ratios[ratios.Count / 2] / 2;
    }

    /// <summary>
    ///     The folders within <see cref="NeighbourReach" /> of <paramref name="level" /> and
    ///     the grade on each worth closest to the current value, in ladder order.
    ///     <para>
    ///         Bounded by folders rather than by a value tolerance on purpose. Phoenix's 75x
    ///         spread means a tolerance hunt over the whole grid returns pairs that are
    ///         arithmetically equal and useless as advice — a D on 26 "equals" an S on 21.
    ///         Three folders is the range a player would actually consider, so those are
    ///         unreachable by construction rather than filtered out afterwards
    ///         (docs/design/phoenix-calculator-redesign.md §3.3).
    ///     </para>
    /// </summary>
    public static IReadOnlyList<FolderNeighbour> Neighbours(MixEnum mix, ChartType type,
        DifficultyLevel level, PhoenixLetterGrade grade, PhoenixPlate plate)
    {
        var searchType = type == ChartType.CoOp ? ChartType.Single : type;
        var target = PumbilityValue(mix, type, level, grade, plate);
        if (target <= 0) return Array.Empty<FolderNeighbour>();

        // Co-op has no level of its own, so it anchors on the top folder rather than on a
        // slider position that means nothing to it.
        var anchor = type == ChartType.CoOp ? (int)DifficultyLevel.Max : (int)level;
        var cap = (int)MaxLevelFor(mix, searchType);
        var results = new List<FolderNeighbour>();

        for (var candidate = anchor - NeighbourReach; candidate <= anchor + NeighbourReach; candidate++)
        {
            if (candidate < LowestScoringLevel || candidate > cap) continue;
            if (candidate == anchor && type != ChartType.CoOp) continue;

            FolderNeighbour? best = null;
            foreach (var option in Enum.GetValues<PhoenixLetterGrade>())
            {
                var value = PumbilityValue(mix, searchType, candidate, option, plate);
                if (value <= 0) continue;
                var delta = value - target;
                if (best != null && Math.Abs(delta) >= Math.Abs(best.Delta)) continue;
                best = new FolderNeighbour(searchType, candidate, option, value, delta,
                    option == PhoenixLetterGrade.SSSPlus && delta < 0,
                    option == PhoenixLetterGrade.F && delta > 0);
            }

            if (best != null) results.Add(best);
        }

        return results;
    }

    /// <summary>The highest folder a mix offers for the type — singles stop short of the ceiling.</summary>
    public static DifficultyLevel MaxLevelFor(MixEnum mix, ChartType type)
    {
        return mix == MixEnum.Phoenix2 && type == ChartType.Single
            ? DifficultyLevel.From(MaxPhoenix2SingleLevel)
            : DifficultyLevel.Max;
    }

    /// <summary>No single chart harder than this exists, so no picker offers an empty folder.</summary>
    public const int MaxPhoenix2SingleLevel = 26;

    private static int BaseRatingFor(MixEnum mix, ChartType type, DifficultyLevel level)
    {
        if (mix != MixEnum.Phoenix2) return level.BaseRating;
        if (level < LowestScoringLevel) return 0;
        var effective = type == ChartType.Single
            ? DifficultyLevel.From(Math.Min(level + 1, DifficultyLevel.Max))
            : level;
        return ScoringConfiguration.Phoenix2BaseRating(effective);
    }
}
