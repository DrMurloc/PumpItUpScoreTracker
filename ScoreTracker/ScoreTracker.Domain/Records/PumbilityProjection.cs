using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record PumbilityProjection(
        IReadOnlyDictionary<Guid, PhoenixScore> ExpectedScores,
        IReadOnlyDictionary<Guid, int> ProjectedGains,
        IReadOnlyDictionary<(ChartType ChartType, DifficultyLevel Level), int> InsufficientDataGains,
        IReadOnlyDictionary<Guid, TierListCategory> ChartDifficulty,
        IReadOnlyDictionary<Guid, IReadOnlyList<SkillAdjustmentRecord>> SkillAdjustments);

    /// <summary>
    ///     Why a chart's expected score moved: the damped, chip-weighted share of the
    ///     player's deviation on one skill, in score units. Positive = this skill is a
    ///     strength that raised the projection ("+Twists"), negative = it dragged it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record SkillAdjustmentRecord(Skill Skill, double ScoreDelta);
}
