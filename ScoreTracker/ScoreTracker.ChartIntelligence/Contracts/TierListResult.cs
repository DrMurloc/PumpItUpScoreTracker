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
    /// <param name="Appearances">
    ///     Per chart, how many of the peer group's PUMBILITY pools hold it — what the cards print as
    ///     "175 peers". Null on every lens but PUMBILITY, which is the only one counting people
    ///     rather than combining stored opinions.
    /// </param>
    public sealed record TierListResult(IReadOnlyList<SongTierListEntry> Entries, bool IsProvisionalFallback,
        int PeerCount = 0, IReadOnlyDictionary<Guid, int>? Appearances = null)
    {
    }
}
