using MediatR;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The current user's competitive band — the chart board's Competitive Peers scope and the tier
///     list's "similar players" count. The per-score ranking queries that used to live here retired
///     on 2026-09-05: a player's standing among their peers is the peer standing port's answer now
///     (docs/design/peers-abstraction.md §4.3), and the cohort cache below serves the recap.
/// </summary>
internal sealed class ScoreQualitySaga : IRequestHandler<GetCompetitivePlayersQuery, IEnumerable<Guid>>
{
    private readonly CohortScoreProvider _cohorts;
    private readonly IPlayerStatsReader _playerStats;
    private readonly ICurrentUserAccessor _user;

    public ScoreQualitySaga(ICurrentUserAccessor user, IPlayerStatsReader playerStats, CohortScoreProvider cohorts)
    {
        _user = user;
        _playerStats = playerStats;
        _cohorts = cohorts;
    }

    public async Task<IEnumerable<Guid>> Handle(GetCompetitivePlayersQuery request, CancellationToken cancellationToken)
    {
        // Whose band: the subject a host names — another player's sessions page opens THEIR
        // band, not the viewer's (D31) — else the viewer. Logged out with no subject there is no
        // band to show, and no user to dereference.
        var subject = request.Subject ?? (_user.IsLoggedIn ? _user.User.Id : (Guid?)null);
        if (subject == null) return Array.Empty<Guid>();
        var myStats = await _playerStats.GetStats(request.Mix, subject.Value, cancellationToken);
        var competitiveLevel = request.ChartType == ChartType.Single
            ? myStats.SinglesCompetitiveLevel
            : myStats.DoublesCompetitiveLevel;
        // The bucketing/caching itself lives in CohortScoreProvider (shared with the recap saga);
        // this class only resolves "the current web user's bucket".
        return await _cohorts.GetComparablePlayers(request.Mix, request.ChartType,
            CohortScoreProvider.Bucket(competitiveLevel), cancellationToken);
    }
}
