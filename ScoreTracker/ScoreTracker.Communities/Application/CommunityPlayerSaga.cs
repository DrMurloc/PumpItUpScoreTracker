using MediatR;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     The community leaderboard's member-wide aggregates: play counts and pooled co-op completion
///     for the member set. Both are guarded the same way as the community's boards — a private
///     community requires the caller to be a member. A single member's profile and the you-vs-them
///     compare are not community reads any more: they live on the player page, gated by player
///     visibility (docs/design/player-page-and-site-search.md §2).
/// </summary>
internal sealed class CommunityPlayerSaga :
    IRequestHandler<GetCommunityPlayCountsQuery, IReadOnlyDictionary<Guid, int>>,
    IRequestHandler<GetCommunityCoOpCompletionQuery, IReadOnlyDictionary<Guid, double>>
{
    private readonly IChartRepository _charts;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreReader _scores;

    public CommunityPlayerSaga(ICommunityRepository communities, ICurrentUserAccessor currentUser,
        IScoreReader scores, IChartRepository charts)
    {
        _communities = communities;
        _currentUser = currentUser;
        _scores = scores;
        _charts = charts;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> Handle(GetCommunityPlayCountsQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GuardCommunity(request.CommunityName, cancellationToken);
        return await _scores.GetJournaledChartCounts(request.Mix, community.MemberIds, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, double>> Handle(GetCommunityCoOpCompletionQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GuardCommunity(request.CommunityName, cancellationToken);
        var coOpCharts = (await _charts.GetCharts(request.Mix, null, ChartType.CoOp, null, cancellationToken))
            .Count();
        if (coOpCharts == 0) return new Dictionary<Guid, double>();

        var members = community.MemberIds.ToArray();
        var passed = new Dictionary<Guid, int>();
        // Co-op "levels" are player counts ×2–×5 — pool every folder into one completion figure.
        for (var players = 2; players <= 5; players++)
        foreach (var (userId, record) in await _scores.GetPlayerScores(request.Mix, members, ChartType.CoOp,
                     players, cancellationToken))
            if (record.Score != null && !record.IsBroken)
                passed[userId] = passed.GetValueOrDefault(userId) + 1;

        return passed.ToDictionary(kv => kv.Key, kv => (double)kv.Value / coOpCharts);
    }

    private async Task<Community> GuardCommunity(Name communityName, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(communityName, cancellationToken)
                        ?? throw new CommunityNotFoundException();
        if (community.PrivacyType == CommunityPrivacyType.Private &&
            !(_currentUser.IsLoggedIn && community.MemberIds.Contains(_currentUser.User.Id)))
            throw new DeniedFromCommunityException("This community is private and you must be a member to view it");
        return community;
    }
}
