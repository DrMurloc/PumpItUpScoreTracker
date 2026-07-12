using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One display-ordered skill on a chart: Highlighted when it came from piucenter's
///     top-3 dominance pick; SegmentFraction is the share of chart sections featuring
///     it (null when only the dominance pick carried it).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSkillChipRecord(Skill Skill, bool Highlighted, decimal? SegmentFraction)
{
    /// <summary>
    ///     The weight a chip carries when SegmentFraction is null (dominance-only picks
    ///     and pre-crawl boolean tags). Every chip consumer weighs the same way — the
    ///     tier-list Skill source and the PUMBILITY projection must not drift.
    /// </summary>
    public const double DefaultSegmentWeight = 0.5;

    public double Weight => SegmentFraction != null ? (double)SegmentFraction.Value : DefaultSegmentWeight;
}
