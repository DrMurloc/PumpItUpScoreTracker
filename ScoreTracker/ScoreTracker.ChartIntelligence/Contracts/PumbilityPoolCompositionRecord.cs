using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     Where PUMBILITY comes from across every full pool on a mix, per band of total PUMBILITY
///     (docs/design/pumbility-calculator.md D9). Sums, not averages: the reader divides by
///     <see cref="Players" /> or by the pooled-chart count as its own presentation needs.
/// </summary>
/// <param name="Key">Stable band identity — a Phoenix 2 gem title's name, or a Phoenix range key like "20k".</param>
/// <param name="Title">The band's own name where it has one (a Phoenix 2 gem); null for a plain total range.</param>
/// <param name="Floor">The lowest merged-pool total in the band, inclusive.</param>
/// <param name="Ceiling">Where the next band starts, exclusive; null for the top band.</param>
/// <param name="Players">Full pools whose total fell in the band.</param>
/// <param name="ChartsPooled">Charts across all those pools — fifty per player.</param>
/// <param name="LevelSum">Sum of the printed level over every pooled chart, for an average level.</param>
/// <param name="LevelPart">The D16 decomposition's level part, summed over every pooled chart.</param>
/// <param name="ScorePart">The D16 decomposition's grade part, summed — negative rungs included as they are.</param>
/// <param name="PlatePart">The D16 decomposition's plate part, summed; zero on a mix whose plates carry ×1.0.</param>
/// <param name="GradeCounts">How many pooled charts sat at each grade, in the mix's own grade cutoffs.</param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPoolBandRecord(string Key, string? Title, double Floor, double? Ceiling, int Players,
    int ChartsPooled, double LevelSum, double LevelPart, double ScorePart, double PlatePart,
    IReadOnlyDictionary<PhoenixLetterGrade, int> GradeCounts)
{
    public double Total => LevelPart + ScorePart + PlatePart;
    public double AverageLevel => ChartsPooled == 0 ? 0 : LevelSum / ChartsPooled;
}

/// <summary>
///     The whole picture for one mix, oldest band first. <see cref="PoolsCounted" /> is every full pool
///     that went into it — including the ones in bands too thin to draw.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPoolCompositionRecord(MixEnum Mix, DateTimeOffset ComputedAt, int PoolsCounted,
    IReadOnlyList<PumbilityPoolBandRecord> Bands)
{
    /// <summary>
    ///     Fewer players than this and a band is not drawn — a bar built on one or two people is a
    ///     picture of them, not of the rung. On the contract so the page and the sweep read one number.
    /// </summary>
    public const int MinimumPlayersToDraw = 5;

    /// <summary>The bands with enough players to draw, lowest first.</summary>
    public IEnumerable<PumbilityPoolBandRecord> Drawable => Bands.Where(b => b.Players >= MinimumPlayersToDraw);
}
