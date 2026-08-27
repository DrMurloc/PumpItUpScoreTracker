namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One granular piucenter badge as a chart surface displays it: the badge as banked, its
///     English label, whether it is one of the chart's dominant three, and the share of the
///     chart's segments carrying it.
///     Nothing here maps. The badge is the badge, and <see cref="Coverage" /> is measured
///     rather than defaulted — unlike the deleted rollup, which collapsed 33 badges into 11
///     buckets, buried five kinds of twist in one word and dropped two badges outright
///     (docs/design/nuke-old-skill-categories.md §1).
///     Whole-chart qualities (bursty, sustained) carry no coverage — a null reads as "this is
///     true of the chart", not "zero percent".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartBadgeChipRecord(string Badge, string DisplayName, BadgeCategory? Category,
    bool Highlighted, decimal? Coverage);
