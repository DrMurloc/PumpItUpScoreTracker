using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts
{
    /// <summary>
    ///     The blend's internals for one folder + lens + player, exposed for the
    ///     Personalized Breakdown page: per-chart source categories (community vs the
    ///     personal sources vs the final), the pooled per-skill deviations the skill
    ///     source actually used, and each source's status so silent degradation is
    ///     visible instead of quietly matching the community list.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PersonalizedTierListBreakdown(
        IReadOnlyList<BreakdownChartRecord> Charts,
        IReadOnlyList<BreakdownSkillRecord> Skills,
        bool SkillSourceActive,
        int UsableSkillCount,
        int ScoredChartCount,
        int OutdatedScoreCount,
        int SimilarPlayerCount,
        double CommunityWeight,
        double SkillWeight,
        double SimilarPlayersWeight,
        bool IsProvisionalFallback);

    /// <summary>
    ///     One chart's tier under each vote: the stored community sources alone, the
    ///     personal sources, and the personalized final. Unrecorded = that source had
    ///     nothing to say about this chart.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record BreakdownChartRecord(
        Guid ChartId,
        TierListCategory CommunityCategory,
        TierListCategory PersonalizedCategory,
        TierListCategory SkillCategory,
        TierListCategory SimilarPlayersCategory);

    /// <summary>
    ///     One skill's pooled estimate: deviation from the player's own baseline on
    ///     the 900k–1M proficiency scale (±0.034 = ±3.4%), the effective observation
    ///     count behind it, and whether it cleared the evidence gate.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record BreakdownSkillRecord(Skill Skill, double Deviation, double Evidence, bool Usable);
}
