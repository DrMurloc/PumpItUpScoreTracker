namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     One directed edge in the similarity graph (docs/design/chart-similarity.md): the
///     persisted output of the nightly calculator. Both sub-scores are always present —
///     an edge exists only when the pair had both signals — and ride the edge rather than
///     being recomputed, because the shelf names what a pair matched on.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityEdge(
    Guid SimilarChartId,
    double Score,
    double SkillScore,
    double IntensityScore);
