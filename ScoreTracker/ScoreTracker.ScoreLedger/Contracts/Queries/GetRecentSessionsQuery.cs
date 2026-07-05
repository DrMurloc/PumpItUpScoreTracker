using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The Sessions page's read: paged session groups with classified journal rows.
///     Returns an empty page for non-public players — the page redirects, this is the
///     defense in depth behind it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRecentSessionsQuery(Guid UserId, MixEnum Mix, int Page = 1, int PageSize = 10)
    : IQuery<RecentSessionsPage>;
