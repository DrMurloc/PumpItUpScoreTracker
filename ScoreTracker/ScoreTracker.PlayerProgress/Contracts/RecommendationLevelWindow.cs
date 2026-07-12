namespace ScoreTracker.PlayerProgress.Contracts;

public enum RecommendationLevelMode
{
    /// <summary>Window follows the player's competitive level: [CL − Spread, CL + Spread].</summary>
    Dynamic,

    /// <summary>Window is pinned to [MinLevel, MaxLevel] and never drifts.</summary>
    Static
}

public enum RecommendationLevelBasis
{
    /// <summary>The level printed on the chart.</summary>
    ChartLevel,

    /// <summary>
    ///     Community-calibrated scoring difficulty — a chart that scores like a 21 counts
    ///     as a 21. Charts without a calibration fall back to their printed level.
    /// </summary>
    ScoringLevel
}

/// <summary>
///     An explicit level window for the recommendation engine (the widget's Dynamic /
///     Static level config). When present it REPLACES the legacy per-category bands
///     (fills CL−3..CL−1, old scores CL−2..CL) and filters the score-based categories
///     (Push PGs, Improve Top 50) that were previously unbounded. Null = legacy behavior.
///     Title-driven categories ignore it — the pushing title pins its own level.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecommendationLevelWindow(
    RecommendationLevelMode Mode,
    int Spread,
    int MinLevel,
    int MaxLevel,
    RecommendationLevelBasis Basis)
{
    public static RecommendationLevelWindow Dynamic(int spread,
        RecommendationLevelBasis basis = RecommendationLevelBasis.ChartLevel)
    {
        return new RecommendationLevelWindow(RecommendationLevelMode.Dynamic, spread, 0, 0, basis);
    }

    public static RecommendationLevelWindow Static(int minLevel, int maxLevel,
        RecommendationLevelBasis basis = RecommendationLevelBasis.ChartLevel)
    {
        return new RecommendationLevelWindow(RecommendationLevelMode.Static, 0, minLevel, maxLevel, basis);
    }
}
