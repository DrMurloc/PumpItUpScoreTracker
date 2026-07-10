namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Rival selection: candidates sit within ±0.25 competitive level. Tiers are strict
///     priority, not thresholds — ANY in-range player from your user-created communities
///     outranks everyone outside them; the country community and then the global pool
///     only top up remaining slots (owner call after round one: known faces beat
///     better-matched strangers). Within a tier, rivals rank by how many top-50
///     competitive charts you share, closest competitive level breaking ties.
/// </summary>
internal static class RivalMatcher
{
    public const double CompetitiveRange = .25;

    internal sealed record Candidate(Guid UserId, double CompetitiveLevel, IReadOnlySet<Guid> Top50ChartIds);

    public static IReadOnlyList<(Candidate Candidate, int Overlap)> PickRivals(IReadOnlySet<Guid> myTop50,
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
