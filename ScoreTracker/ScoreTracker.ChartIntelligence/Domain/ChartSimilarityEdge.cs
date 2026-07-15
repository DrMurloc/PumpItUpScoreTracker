namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     One directed edge in the similarity graph (docs/design/chart-similarity.md): the
///     persisted output of the nightly calculator. A null sub-score means that signal was
///     unavailable for the pair (weight renormalization already accounted for it) — the
///     why-chips read the breakdown, so it rides the edge rather than being recomputed.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityEdge(
    Guid SimilarChartId,
    double Score,
    double? SkillScore,
    double? DifficultyScore,
    double? PlayersScore,
    double? IntensityScore,
    double? MetaScore,
    int SharedScorers);
