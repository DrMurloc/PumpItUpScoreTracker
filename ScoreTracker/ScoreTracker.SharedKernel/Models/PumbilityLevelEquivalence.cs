using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     The exchange rate between scoring and passing: a chart's PUMBILITY value read as
///     <em>which level's 900,000 is worth the same</em> (docs/design/pumbility-calculator.md D4/D5).
///     <para>
///         The baseline is the score <see cref="AnchorScore" />, not a grade name. 900,000 is exactly
///         the AA floor on Phoenix and exactly the A+ floor on Phoenix 2, so it is a real rung on both
///         mixes and the same play on both — a grade name would compare two different scores.
///         <see cref="AnchorGrade" /> resolves it from the floors table rather than stating it.
///     </para>
///     <para>
///         The inversion is piecewise linear over the anchor-grade value at each level the
///         configuration prices, per chart type, so it needs no closed form: Phoenix's quadratic
///         base, Phoenix 2's kink at 24 and its singles priced one level up all fall out of the
///         curve the configuration already answers with. Values past either end extrapolate along
///         the last segment — a fractional level above 29 reads "worth more than any chart in the
///         game", which is what the ruler draws.
///     </para>
/// </summary>
public static class PumbilityLevelEquivalence
{
    /// <summary>The play both mixes agree on: 900,000, the AA floor on Phoenix and the A+ floor on Phoenix 2.</summary>
    public static readonly PhoenixScore AnchorScore = 900_000;

    // Every level a configuration can price. Levels it prices at zero (below 10 on both PUMBILITY
    // formulas) are left off the curve, since a zero has no inverse.
    private static readonly int[] Levels = Enumerable.Range((int)DifficultyLevel.Min, (int)DifficultyLevel.Max)
        .ToArray();

    /// <summary>
    ///     The grade whose floor is exactly <see cref="AnchorScore" /> in this mix. Throws if the
    ///     mix's floors table has no rung there — the calculator has no honest baseline then, and
    ///     inventing one is worse than saying so.
    /// </summary>
    public static PhoenixLetterGrade AnchorGrade(MixEnum mix)
    {
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
            if (grade.GetMinimumScoreFor(mix) == AnchorScore)
                return grade;
        throw new InvalidOperationException($"No grade floor sits at {(int)AnchorScore:N0} on {mix}");
    }

    /// <summary>
    ///     What a chart of this type and level is worth at <paramref name="grade" /> under
    ///     <paramref name="config" />, at the grade's floor score and the lowest plate — the number
    ///     the value table prints and the ruler positions.
    /// </summary>
    public static double ValueAt(ScoringConfiguration config, ChartType type, DifficultyLevel level,
        PhoenixLetterGrade grade)
    {
        return config.GetScore(type, level, grade.GetMinimumScoreFor(config.Mix), PhoenixPlate.RoughGame);
    }

    /// <summary>
    ///     The fractional level whose anchor-grade value equals <paramref name="value" /> for this chart
    ///     type — the ruler's axis. Exactly the level itself for the anchor grade at any priced level.
    /// </summary>
    public static double EquivalentLevel(ScoringConfiguration config, ChartType type, double value)
    {
        var curve = AnchorCurve(config, type);
        if (curve.Length < 2)
            throw new InvalidOperationException($"{config.Mix} prices fewer than two levels for {type}");

        // Find the segment the value falls in; past either end, extend the outermost segment.
        var i = 0;
        while (i < curve.Length - 2 && value > curve[i + 1].Value) i++;
        var (lowLevel, lowValue) = curve[i];
        var (highLevel, highValue) = curve[i + 1];
        return lowLevel + (value - lowValue) / (highValue - lowValue) * (highLevel - lowLevel);
    }

    /// <summary>
    ///     How many levels of pass-pushing a grade on this level is worth: the equivalent level of
    ///     the chart at <paramref name="grade" /> minus the level itself. Zero at the anchor grade,
    ///     negative below it.
    /// </summary>
    public static double LevelsBought(ScoringConfiguration config, ChartType type, DifficultyLevel level,
        PhoenixLetterGrade grade)
    {
        return EquivalentLevel(config, type, ValueAt(config, type, level, grade)) - (int)level;
    }

    /// <summary>
    ///     The lowest grade on <paramref name="level" /> worth at least an anchor-grade play one level
    ///     higher — "passing one level higher takes a 900,000 to what, on the chart you already have".
    ///     Null when no grade reaches it or the level is the top of the ladder.
    /// </summary>
    public static PhoenixLetterGrade? GradeMatchingNextLevel(ScoringConfiguration config, ChartType type,
        DifficultyLevel level)
    {
        if ((int)level >= (int)DifficultyLevel.Max) return null;
        var target = ValueAt(config, type, DifficultyLevel.From((int)level + 1), AnchorGrade(config.Mix));
        if (target <= 0) return null;
        foreach (var grade in Enum.GetValues<PhoenixLetterGrade>())
            if (ValueAt(config, type, level, grade) >= target)
                return grade;
        return null;
    }

    private static (int Level, double Value)[] AnchorCurve(ScoringConfiguration config, ChartType type)
    {
        var anchor = AnchorGrade(config.Mix);
        return Levels
            .Select(l => (Level: l, Value: ValueAt(config, type, DifficultyLevel.From(l), anchor)))
            .Where(p => p.Value > 0)
            .ToArray();
    }
}
