namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     One similar-chart edge, best-first (docs/design/chart-similarity.md). Null
///     sub-scores mean that signal was unavailable for the pair — the similar-shelf
///     why-chips derive from the non-null breakdown (players ≥ 0.45, style ≥ 0.75,
///     behavior ≥ 0.8 …); SharedScorers backs the players chip's confidence floor.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSimilarityRecord(
    Guid ChartId,
    double Score,
    double? StyleScore,
    double? BehaviorScore,
    double? PlayersScore,
    double? IntensityScore,
    double? MetaScore,
    int SharedScorers);
