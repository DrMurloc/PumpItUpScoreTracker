using ScoreTracker.Communities.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Domain;

/// <summary>
///     The community big-wins ledger (docs/design/home-page-widgets.md §7). Vertical-internal
///     like <see cref="ICommunityRepository" /> — only the highlight saga and its query handlers
///     touch it. One summary row per (event × community the winner belongs to); rows carry a JSON
///     list of <see cref="SignificantWin" /> and are purged after 30 days.
/// </summary>
internal interface ICommunityHighlightRepository
{
    /// <summary>
    ///     Persist one summary per community the winner belongs to. No-op when the user is in no
    ///     communities. <paramref name="eventId" /> dedupes the same win across shared communities on read.
    /// </summary>
    Task AddForUserCommunities(Guid eventId, Guid userId, MixEnum mix, DateTimeOffset occurredAt,
        Guid? sessionId, IReadOnlyList<SignificantWin> wins, CancellationToken cancellationToken);

    /// <summary>
    ///     Recent wins across the requested communities, newest first, deduped per event. Gated on the
    ///     requester's own membership (consent boundary, CH2) — a community the requester isn't in yields
    ///     nothing even if named.
    /// </summary>
    Task<IReadOnlyList<CommunityHighlightEntry>> GetForUser(Guid requestingUserId,
        IReadOnlyCollection<Name> communityNames, MixEnum mix, int take, CancellationToken cancellationToken);

    /// <summary>Drop summaries older than the cutoff. Returns rows removed.</summary>
    Task<int> PurgeBefore(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>A read row from the ledger — the winner's id (name/avatar resolved at read) plus the win list.</summary>
internal sealed record CommunityHighlightEntry(
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    IReadOnlyList<SignificantWin> Wins);
