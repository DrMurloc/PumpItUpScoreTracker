using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Contracts
{
    /// <summary>
    ///     A tier list read result: the entries plus whether they are the Phoenix list standing
    ///     in for an empty Phoenix2 one — the UI renders that as a "provisional" badge
    ///     (locked decision, plan doc).
    /// </summary>
    [ExcludeFromCodeCoverage]
    /// <param name="PeerCount">
    ///     How many players' scores are behind a personalized Score list, excluding the reader.
    ///     Zero on every other lens and on the community lists, which have no cohort. The page
    ///     gates the Personalized view on it: a folder only a couple of people at your level have
    ///     touched cannot rank anything, and saying so beats a column of Not Rated.
    /// </param>
    public sealed record TierListResult(IReadOnlyList<SongTierListEntry> Entries, bool IsProvisionalFallback,
        int PeerCount = 0)
    {
    }
}
