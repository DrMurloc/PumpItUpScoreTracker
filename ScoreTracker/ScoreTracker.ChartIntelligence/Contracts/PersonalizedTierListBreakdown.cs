using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts
{
    /// <summary>
    ///     The blend's internals for one folder + lens + player, exposed for the
    ///     Personalized Breakdown page: per-chart source categories (community vs the
    ///     personal sources vs the final), the pooled per-skill deviations the skill
    ///     source actually used, and each source's status so silent degradation is
    ///     visible instead of quietly matching the community list.
    /// </summary>
    /// <param name="ProjectedChartCount">
    ///     How many of the folder's charts the projection could answer for, out of
    ///     <paramref name="FolderChartCount" />. Zero means nobody near this player's competitive
    ///     level has scored anything here — the source is silent, and the page has to say so
    ///     rather than let it read as agreement with the community.
    /// </param>
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
        double ProjectionWeight,
        int ProjectedChartCount,
        int FolderChartCount,
        bool IsProvisionalFallback);

    /// <summary>
    ///     One chart's tier under each vote: the stored community sources alone, the
    ///     personal sources, and the personalized final. Unrecorded = that source had
    ///     nothing to say about this chart.
    /// </summary>
    /// <param name="ProjectedScore">
    ///     What players near this one's competitive level score here — the number the Score list's
    ///     tier is cut from. Null where no peer has played the chart, which is a different thing
    ///     from a low projection and has to render as such.
    /// </param>
    [ExcludeFromCodeCoverage]
    public sealed record BreakdownChartRecord(
        Guid ChartId,
        TierListCategory CommunityCategory,
        TierListCategory PersonalizedCategory,
        TierListCategory SkillCategory,
        TierListCategory SimilarPlayersCategory,
        TierListCategory ProjectionCategory,
        PhoenixScore? ProjectedScore);

    /// <summary>
    ///     One skill's pooled estimate: deviation from the player's own baseline on
    ///     the 900k–1M proficiency scale (±0.034 = ±3.4%), the effective observation
    ///     count behind it, and whether it cleared the evidence gate.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record BreakdownSkillRecord(Skill Skill, double Deviation, double Evidence, bool Usable);
}
