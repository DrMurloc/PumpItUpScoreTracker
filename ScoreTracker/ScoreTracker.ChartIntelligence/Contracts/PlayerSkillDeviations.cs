using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     Per-skill deviations from the player's own folder baselines, pooled over the
///     ±3-folder window around the query's anchor — the same computation the tier-list
///     Skill source runs (one implementation, no drift; see TierListBlendBuilder).
///     ScoreDeviation is in score units measured on the floored 900k–1M proficiency
///     band: +5,800 means charts highlighting this skill run ~5,800 above the player's
///     folder norm. Consumers must skip skill-based adjustments unless
///     <see cref="Usable" /> — fewer than three sufficiently-evidenced skills means
///     the picture is noise, not signal.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerSkillDeviations(
    IReadOnlyDictionary<Skill, SkillDeviationRecord> Skills,
    bool Usable,
    int ScoredChartCount)
{
    /// <summary>
    ///     Deviations are measured on this floored band. They only say something about
    ///     scores INSIDE it — don't apply them to expectations below the floor.
    /// </summary>
    public const int BandFloor = 900_000;

    public const int BandCeiling = 1_000_000;
}

[ExcludeFromCodeCoverage]
public sealed record SkillDeviationRecord(double ScoreDeviation, double Evidence, bool Usable);
