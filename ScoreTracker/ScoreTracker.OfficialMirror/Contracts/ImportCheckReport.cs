using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     What a completeness check concluded. Missing and out-of-date scores share ONE list: an
///     account can be short a chart and behind on another at the same time, and splitting them
///     into two views would make the player fix the same account twice.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckReport(
    MixEnum Mix,
    ImportCheckVerdict Verdict,
    double OfficialPumbility,
    double LocalPumbility,
    int OfficialPasses,
    int LocalPasses,
    IReadOnlyList<ImportCheckDifference> Differences)
{
    /// <summary>Everything a repair could act on — "we hold more than PIUGAME" is not one.</summary>
    public IReadOnlyList<ImportCheckDifference> Repairable =>
        Differences.Where(d => d.Kind != ImportCheckDifferenceKind.Extra).ToArray();

    public int RepairableCount => Repairable.Sum(d => d.Count);

    /// <summary>
    ///     The PUMBILITY the findings are worth. Below ten it is display rounding rather than a
    ///     real gap — the official Phoenix board truncates to whole numbers — so the panel says
    ///     nothing at all.
    /// </summary>
    public double? PumbilityGap =>
        Math.Abs(OfficialPumbility - LocalPumbility) >= 10
            ? Math.Abs(OfficialPumbility - LocalPumbility)
            : null;
}

/// <summary>
///     One level's disagreement. <see cref="Level" /> is null for the buckets that are not a single
///     level — CO-OP, 27-and-over, and the sub-10 residual Phoenix will not break down.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckDifference(
    string Bucket,
    int? Level,
    ImportCheckDifferenceKind Kind,
    int Count,
    IReadOnlyList<ImportCheckChart> Charts);

/// <summary>
///     One chart a check found. <c>CurrentScore</c> is what we already hold — present means the
///     player has beaten it since their last import, absent means we never imported the chart at
///     all. The panel needs no other flag to tell the two apart.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckChart(Guid ChartId, int Score, int? CurrentScore);

public enum ImportCheckDifferenceKind
{
    Missing,
    OutOfDate,

    /// <summary>We hold more than PIUGAME — a CSV import, a manual entry, or a retired chart.
    /// Never an error, and never repaired.</summary>
    Extra
}

public enum ImportCheckVerdict
{
    InSync,

    /// <summary>Something is missing, out of date, or both — the panel shows one list either way.</summary>
    NeedsAttention
}
