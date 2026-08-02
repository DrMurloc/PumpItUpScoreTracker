namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Recap peer selection: candidates sit within ±0.25 competitive level. Tiers are strict
///     priority, not thresholds — ANY in-range player from your user-created communities
///     outranks everyone outside them; the country community and then the global pool
///     only top up remaining slots (owner call after round one: known faces beat
///     better-matched strangers). Within a tier, peers rank by how many top-50
///     competitive charts you share, closest competitive level breaking ties.
///     <para>
///         These are picked FOR the player, which is what separates them from a Rival — a
///         player the user chose themselves (docs/design/rivals.md D48). The recap's stored
///         payload still calls them rivals: <c>PlayerRecap</c> is serialized whole behind a
///         schema-version equality gate, so renaming the contract would blank every stored
///         recap until an admin rebuild.
///     </para>
/// </summary>
internal static class RecapPeerMatcher
{
    public const double CompetitiveRange = .25;

    /// <summary>
    ///     Community mates qualify at double the range — a peer you know at 0.4 away
    ///     beats a perfectly matched stranger (and at the top of the ladder ±0.25 is a
    ///     handful of players worldwide).
    /// </summary>
    public const double CommunityCompetitiveRange = .5;

    internal sealed record Candidate(Guid UserId, double CompetitiveLevel, IReadOnlySet<Guid> Top50ChartIds);

    public static IReadOnlyList<(Candidate Candidate, int Overlap)> PickPeers(IReadOnlySet<Guid> myTop50,
        double myCompetitiveLevel, IEnumerable<Candidate> pool, int count = 3)
    {
        return pool
            .Select(c => (Candidate: c, Overlap: c.Top50ChartIds.Count(myTop50.Contains)))
            .OrderByDescending(x => x.Overlap)
            .ThenBy(x => Math.Abs(x.Candidate.CompetitiveLevel - myCompetitiveLevel))
            .ThenBy(x => x.Candidate.UserId)
            .Take(count)
            .ToArray();
    }
}
