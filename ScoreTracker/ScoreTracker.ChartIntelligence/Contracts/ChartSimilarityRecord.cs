namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     One similar-chart edge, best-first (docs/design/chart-similarity.md). Both
///     sub-scores are always present — an edge exists only when the pair had both
///     signals. Score answers "same kind of problem", nothing more: the shelf orders by
///     difficulty and filters by metadata on top of this, neither of which is baked in
///     here.
///     The list is **not** pre-filtered by quality — it runs down into the tail, because
///     where the bar falls is a render decision. Readers that want only real matches
///     apply their own.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSimilarityRecord(
    Guid ChartId,
    double Score,
    double SkillScore,
    double IntensityScore,
    IReadOnlyList<ChartSharedBadgeRecord> SharedBadges)
{
    /// <summary>
    ///     What counts as a real match. A read-time constant by design: the graph stores
    ///     its top 20 floor-free, so moving this bar is a redeploy rather than a rebuild,
    ///     and the rows below it are near-misses rather than rows that never existed.
    ///     Shared by every consumer that draws the line — the similar-charts shelf and the
    ///     Hot Streak expansion — so "a match" means the same thing everywhere.
    /// </summary>
    public const double MatchFloor = 0.55;
}

/// <summary>
///     A badge both charts carry, at the coverage they share, keyed by piucenter's raw
///     badge name (<c>bracket</c>, <c>twist_90</c>, …) — deliberately not the display
///     skill vocabulary, which is a projection this must not inherit. Naming it for a
///     human is the reader's job.
///     Coverage is the shared fraction, so "bracket 0.50" means *both* charts are at
///     least half brackets.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSharedBadgeRecord(string Badge, double Coverage);
