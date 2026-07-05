using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts
{
    /// <summary>
    ///     A tier list read result: the entries plus whether they are the Phoenix list standing
    ///     in for an empty Phoenix2 one — the UI renders that as a "provisional" badge
    ///     (locked decision, plan doc).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record TierListResult(IReadOnlyList<SongTierListEntry> Entries, bool IsProvisionalFallback)
    {
    }
}
