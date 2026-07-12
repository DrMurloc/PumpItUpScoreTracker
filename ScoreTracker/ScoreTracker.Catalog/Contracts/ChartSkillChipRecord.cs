using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One display-ordered skill on a chart: Highlighted when it came from piucenter's
///     top-3 dominance pick; SegmentFraction is the share of chart sections featuring
///     it (null when only the dominance pick carried it).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSkillChipRecord(Skill Skill, bool Highlighted, decimal? SegmentFraction);
