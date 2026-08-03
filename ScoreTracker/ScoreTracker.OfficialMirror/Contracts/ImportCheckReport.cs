using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     What a completeness check concluded, as the page renders it. Stored, so opening
///     /UploadPhoenixScores shows the standing verdict without touching piugame.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckReport(
    MixEnum Mix,
    DateTimeOffset RanAt,
    ImportCheckVerdict Verdict,
    double OfficialPumbility,
    double LocalPumbility,
    int OfficialPasses,
    int LocalPasses,
    IReadOnlyList<ImportCheckDifference> Differences)
{
    /// <summary>Scores piugame has that we do not, summed across every level.</summary>
    public int MissingCount => Differences.Where(d => d.Kind == ImportCheckDifferenceKind.Missing).Sum(d => d.Count);

    /// <summary>Scores we hold at a value the site has since beaten.</summary>
    public int OutOfDateCount =>
        Differences.Where(d => d.Kind == ImportCheckDifferenceKind.OutOfDate).Sum(d => d.Count);
}

/// <summary>
///     One level's disagreement. <see cref="Level" /> is null for the buckets that are not a
///     single level — CO-OP, 27-and-over, and the sub-10 residual Phoenix will not break down.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ImportCheckDifference(
    string Bucket,
    int? Level,
    ImportCheckDifferenceKind Kind,
    int Count);

public enum ImportCheckDifferenceKind
{
    Missing,
    OutOfDate,

    /// <summary>We hold more than piugame — a CSV import, a manual entry, or a retired chart.
    /// Never an error, and never the headline.</summary>
    Extra
}

public enum ImportCheckVerdict
{
    /// <summary>Every level and band agrees.</summary>
    InSync,
    MissingScores,
    OutOfDateScores,

    /// <summary>Only "we hold more than piugame" differences — nothing to repair.</summary>
    AheadOfSite
}
