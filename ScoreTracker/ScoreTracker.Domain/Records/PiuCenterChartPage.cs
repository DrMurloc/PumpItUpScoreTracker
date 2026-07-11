namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The skill-relevant slice of one piucenter per-chart page: their top-skill
    ///     summary plus per-segment badge tallies (SegmentSkillCounts[skill] = number of
    ///     chart segments carrying that badge, out of SegmentCount). The stepchart
    ///     rendering data in the same file is deliberately not surfaced — we link out
    ///     for that (design doc §8a ingestion boundary).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterChartPage(
        string ExternalKey,
        IReadOnlyList<string> SkillSummary,
        int SegmentCount,
        IReadOnlyDictionary<string, int> SegmentSkillCounts,
        IReadOnlyDictionary<string, int> RareSkillCounts,
        IReadOnlyList<string> LastSegmentSkills,
        decimal? Nps,
        string? NotetypeBpmSummary,
        string? SordChartLevel);
}
