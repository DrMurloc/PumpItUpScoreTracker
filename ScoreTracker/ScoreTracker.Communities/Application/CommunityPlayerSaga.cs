using MediatR;
using ScoreTracker.Communities.Contracts;
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
///     The community player page's read side: a member's summary (ratings + folder
///     completion) and the you-vs-them folder comparison. Both are guarded the same way as
///     the community's boards — a private community requires the caller to be a member, and
///     the subject must be a member: shared membership is the score-visibility contract the
///     join consent points at.
/// </summary>
internal sealed class CommunityPlayerSaga :
    IRequestHandler<GetCommunityPlayerProfileQuery, CommunityPlayerProfileRecord?>,
    IRequestHandler<GetCommunityFolderComparisonQuery, IEnumerable<CommunityChartComparisonRecord>>,
    IRequestHandler<GetCommunityPlayCountsQuery, IReadOnlyDictionary<Guid, int>>,
    IRequestHandler<GetCommunityCoOpCompletionQuery, IReadOnlyDictionary<Guid, double>>
{
    private readonly IChartRepository _charts;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;

    public CommunityPlayerSaga(ICommunityRepository communities, ICurrentUserAccessor currentUser,
        IScoreReader scores, IPlayerStatsReader playerStats, IChartRepository charts, IUserReader users)
    {
        _communities = communities;
        _currentUser = currentUser;
        _scores = scores;
        _playerStats = playerStats;
        _charts = charts;
        _users = users;
    }

    public async Task<CommunityPlayerProfileRecord?> Handle(GetCommunityPlayerProfileQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GuardVisibility(request.CommunityName, request.UserId, cancellationToken);

        var user = await _users.GetUser(request.UserId, cancellationToken);
        if (user == null) return null;
        GuardPrivateProfile(user, community);

        var stats = await _playerStats.GetStats(request.Mix, request.UserId, cancellationToken);
        var bestScores = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .ToDictionary(s => s.ChartId);
        var charts = await _charts.GetCharts(request.Mix, null, null, null, cancellationToken);

        // Folder completion over singles+doubles level folders; co-op "levels" are player
        // counts, not difficulty, so they stay out of the graph.
        var completion = charts
            .Where(c => c.Type is ChartType.Single or ChartType.Double or ChartType.SinglePerformance
                or ChartType.DoublePerformance)
            .GroupBy(c => (int)c.Level)
            .OrderBy(g => g.Key)
            .Select(g => new CommunityFolderCompletionRecord(g.Key,
                g.Count(c => bestScores.TryGetValue(c.Id, out var s) && s.Score != null && !s.IsBroken),
                g.Count()))
            .ToArray();

        return new CommunityPlayerProfileRecord(user.Id, user.Name, user.ProfileImage, user.Country,
            user.IsPublic, stats.SkillRating, stats.TotalRating, stats.SinglesRating, stats.DoublesRating,
            stats.CompetitiveLevel, stats.SinglesCompetitiveLevel, stats.DoublesCompetitiveLevel,
            stats.HighestLevel, stats.ClearCount, completion);
    }

    public async Task<IEnumerable<CommunityChartComparisonRecord>> Handle(
        GetCommunityFolderComparisonQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();
        var community = await GuardVisibility(request.CommunityName, request.UserId, cancellationToken);
        var target = await _users.GetUser(request.UserId, cancellationToken);
        if (target != null) GuardPrivateProfile(target, community);

        var folder = (await _charts.GetCharts(request.Mix, request.Level, request.ChartType, null,
            cancellationToken)).ToArray();
        var folderIds = folder.Select(c => c.Id).ToHashSet();
        var mine = (await _scores.GetBestScores(request.Mix, _currentUser.User.Id, cancellationToken))
            .Where(s => folderIds.Contains(s.ChartId)).ToDictionary(s => s.ChartId);
        var theirs = (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .Where(s => folderIds.Contains(s.ChartId)).ToDictionary(s => s.ChartId);

        return folder.Select(chart =>
        {
            var my = mine.TryGetValue(chart.Id, out var m) ? m : null;
            var their = theirs.TryGetValue(chart.Id, out var t) ? t : null;
            return new CommunityChartComparisonRecord(chart.Id,
                my?.Score, my?.Plate, my?.IsBroken ?? false, my?.RecordedDate,
                their?.Score, their?.Plate, their?.IsBroken ?? false, their?.RecordedDate);
        }).ToArray();
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

    // The same gate the community's boards use: private communities are members-only, and the
    // subject must be a member for their scores to be community-visible at all.
    private async Task<Community> GuardVisibility(Name communityName, Guid subjectId,
        CancellationToken cancellationToken)
    {
        var community = await GuardCommunity(communityName, cancellationToken);
        if (!community.MemberIds.Contains(subjectId))
            throw new DeniedFromCommunityException("That player is not a member of this community");
        return community;
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

    // Membership is the score-visibility consent: a private-profile member is only a member to
    // people inside the community; an outside viewer must not see them at all.
    private void GuardPrivateProfile(User subject, Community community)
    {
        if (subject.IsPublic) return;
        if (_currentUser.IsLoggedIn && community.MemberIds.Contains(_currentUser.User.Id)) return;
        throw new DeniedFromCommunityException("That player is not visible outside this community");
    }

}
