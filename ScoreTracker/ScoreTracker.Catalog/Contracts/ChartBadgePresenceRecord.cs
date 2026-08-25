namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One badge a chart really carries. <see cref="Weight" /> is measured coverage, or 1.0 for
///     a whole-chart quality — it is true of the whole chart, so it weighs as much as a badge
///     covering all of it rather than as the nothing its absent coverage would make it.
///     The family travels with the badge for the same reason it does on a chip: a reader
///     tinting by family must never have to map the vocabulary itself.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartBadgePresenceRecord(string Badge, string DisplayName, BadgeCategory? Family,
    decimal Weight);
