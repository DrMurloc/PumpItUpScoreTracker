namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     One directed edge in the similarity graph (docs/design/chart-similarity.md): the
///     persisted output of the nightly calculator. Both sub-scores are always present —
///     an edge exists only when the pair had both signals — and ride the edge rather than
///     being recomputed, because the shelf names what a pair matched on.
///     Edges are stored floor-free: the score bar is a render-time constant, so the near-
///     misses shelf and a retuned floor both come free from the same stored rows.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record ChartSimilarityEdge(
    Guid SimilarChartId,
    double Score,
    double SkillScore,
    double IntensityScore,
    IReadOnlyList<SharedBadgeCoverage> SharedBadges);

/// <summary>
///     One badge both charts carry, at the coverage they share — <c>min(a, b)</c> on raw
///     (un-gamma'd) coverage, which is the honest reading: "Brackets 50%" means *both*
///     are at least half brackets. This is not a second analysis bolted on for display.
///     Bray-Curtis rearranges to <c>Σ 2·min(a', b') / Σ(a' + b')</c>, so the shared terms
///     ARE the formula — the reasons fall out of the score rather than being reverse-
///     engineered from it. Gamma is monotonic, so ranking by raw min matches ranking by
///     contribution.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed record SharedBadgeCoverage(string Badge, double Coverage);
