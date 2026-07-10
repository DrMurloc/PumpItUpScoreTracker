namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Rival selection: candidates sit within ±0.25 competitive level; the pool ladder is
///     your user-created communities → your country community → everyone (a pool must
///     offer at least 3 candidates to win); rivals rank by how many top-50 competitive
///     charts you share, closest competitive level breaking ties.
/// </summary>
internal static class RivalMatcher
{
    public const double CompetitiveRange = .25;
    public const int MinimumPoolSize = 3;

    internal sealed record Candidate(Guid UserId, double CompetitiveLevel, IReadOnlySet<Guid> Top50ChartIds);

    public static IReadOnlyList<Guid> SelectPool(IReadOnlyList<Guid> communityCandidates,
        IReadOnlyList<Guid> countryCandidates, IReadOnlyList<Guid> globalCandidates)
    {
        if (communityCandidates.Count >= MinimumPoolSize) return communityCandidates;
        if (countryCandidates.Count >= MinimumPoolSize) return countryCandidates;
        return globalCandidates;
    }

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
