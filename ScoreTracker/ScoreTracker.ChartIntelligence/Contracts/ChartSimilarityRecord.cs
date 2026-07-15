namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     One similar-chart edge, best-first (docs/design/chart-similarity.md). Both
///     sub-scores are always present — an edge exists only when the pair had both
///     signals. Score answers "same kind of problem", nothing more: the shelf orders by
///     difficulty and filters by metadata on top of this, neither of which is baked in
///     here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSimilarityRecord(
    Guid ChartId,
    double Score,
    double SkillScore,
    double IntensityScore);
