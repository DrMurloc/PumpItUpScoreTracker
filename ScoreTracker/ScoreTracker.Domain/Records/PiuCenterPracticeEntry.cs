namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     One position in piucenter's per-skill practice lists: chart
    ///     <paramref name="ExternalKey" /> is the <paramref name="Rank" />-th best
    ///     practice chart for <paramref name="Skill" /> at <paramref name="SordLevel" />
    ///     (e.g. "S17"). Rank is 1-based.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterPracticeEntry(
        string Skill,
        string SordLevel,
        int Rank,
        string ExternalKey);
}
