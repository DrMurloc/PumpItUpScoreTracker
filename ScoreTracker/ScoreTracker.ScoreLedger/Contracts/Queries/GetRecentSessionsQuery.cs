using MediatR;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The Sessions page's read: paged session groups with classified journal rows,
///     one continuous timeline across every mix the player recorded on. Returns an
///     empty page for non-public players — the page redirects, this is the defense in
///     depth behind it.
///     <para>
///         <paramref name="Before" /> jumps backwards through a long history without paging
///         to it: only sessions that ended earlier are returned, and the page count reflects
///         the filter rather than the whole timeline.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRecentSessionsQuery(Guid UserId, int Page = 1, int PageSize = 10,
    DateTimeOffset? Before = null) : IQuery<RecentSessionsPage>;
