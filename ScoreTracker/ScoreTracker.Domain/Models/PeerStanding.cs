namespace ScoreTracker.Domain.Models;

/// <summary>The four kinds of peer source a player can tick (docs/design/peers-abstraction.md §3).</summary>
public enum PeerSourceKind
{
    Rivals,
    CompetitiveLevel,
    Pumbility,
    Community
}

/// <summary>
///     One ticked source's share of a chart's standing — the line the popover prints for it.
///     Counts are about OTHER people: the subject is never one of their own peers (D10), so
///     <see cref="Members" /> and <see cref="Passed" /> exclude them, and <see cref="Of" /> adds them
///     back because a place is a position inside a population you belong to.
/// </summary>
public sealed record PeerStandingSource(
    PeerSourceKind Kind,
    Guid? CommunityId,
    string? CommunityName,
    bool IsRegional,
    bool IsWorld,
    int Members,
    int Passed,
    int Better,
    int FromOfficialBoard)
{
    public int Place => Better + 1;

    public int Of => Passed + 1;

    /// <summary>Members with no passing score on the chart — never played it, or only broke it.</summary>
    public int NotPassed => Members - Passed;
}

/// <summary>
///     Where one of the subject's scores stands among the peers they chose, on one chart
///     (docs/design/peers-abstraction.md §4.1). Only passes enter the ladder (D9): a peer's broken
///     attempt counts them among <see cref="NotPassed" /> and adds to <see cref="Broke" />, never
///     as a score to rank against.
///     <para>
///         <see cref="Percentile" /> keeps the established <c>Ranking</c> semantic — the share of the
///         cohort at or below you, tie-inclusive, 1.0 = first — over a cohort that includes you, so
///         a chart no peer has passed has no percentile rather than a flattering one.
///     </para>
/// </summary>
public sealed record PeerStanding(
    int PeerCount,
    int Passed,
    int Better,
    int PerfectGames,
    int Broke,
    IReadOnlyList<PeerStandingSource> Sources,
    DateTimeOffset? OfficialAsOf)
{
    /// <summary>At least one peer has passed the chart, so there is something to stand among.</summary>
    public bool HasCohort => Passed > 0;

    /// <summary>The population a place is read inside: the peers who passed it, plus you.</summary>
    public int Cohort => Passed + 1;

    public int Place => Better + 1;

    public double? Percentile => HasCohort ? (Cohort - Better) / (double)Cohort : null;

    /// <summary>Peers with no passing score — never played it, or only broke it.</summary>
    public int NotPassed => PeerCount - Passed;

    public bool IsFirst => HasCohort && Better == 0;

    /// <summary>The standing of a subject whose peers have not passed the chart, or who has no peers at all.</summary>
    public static PeerStanding NoCohort(int peerCount, int broke, IReadOnlyList<PeerStandingSource> sources) =>
        new(peerCount, 0, 0, 0, broke, sources, null);
}
