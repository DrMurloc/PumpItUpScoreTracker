namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One granular piucenter badge as a chart surface displays it: the badge as banked, its
///     English label, whether it is one of the chart's dominant three, and the share of the
///     chart's segments carrying it.
///     This is the honest counterpart to <see cref="ChartSkillChipRecord" />, which projects
///     the same metrics onto the coarse Skill rollup — a 33-into-11 collapse that maps one
///     badge onto several skills, buries five kinds of twist in one bucket and drops two
///     badges entirely (docs/design/nuke-old-skill-categories.md §1). Nothing here maps:
///     the badge is the badge, and <see cref="Coverage" /> is measured rather than defaulted.
///     Whole-chart qualities (bursty, sustained) carry no coverage — a null reads as "this is
///     true of the chart", not "zero percent".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartBadgeChipRecord(string Badge, string DisplayName, BadgeCategory? Category,
    bool Highlighted, decimal? Coverage);
