using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The player's skill-deviation profile around an anchor folder: how their scores
///     on charts highlighting each skill deviate from their own folder baselines,
///     within the ±3-folder window the tier-list Skill source uses. Anchor at the
///     folder whose charts you are reasoning about (Pumbility projections anchor at
///     the player's per-type competitive level).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerSkillDeviationsQuery(Guid UserId, ChartType ChartType,
    DifficultyLevel AnchorLevel, MixEnum Mix = MixEnum.Phoenix) : IQuery<PlayerSkillDeviations>;
