using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The player page's summary: identity, ratings, per-folder completion. Gated inside on the
///     published visibility port — self, public, a shared user-created community, a rival edge —
///     so the answer is null to anyone the player is hidden from, whatever page asked.
/// </summary>
internal sealed class PlayerProfileHandler : IRequestHandler<GetPlayerProfileQuery, PlayerProfileRecord?>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;
    private readonly IPlayerVisibilityReader _visibility;

    public PlayerProfileHandler(IUserReader users, IPlayerVisibilityReader visibility, ICurrentUserAccessor currentUser,
        IPlayerStatsReader playerStats, IChartRepository charts, IScoreReader scores)
    {
        _users = users;
        _visibility = visibility;
        _currentUser = currentUser;
        _playerStats = playerStats;
        _charts = charts;
        _scores = scores;
    }

    public async Task<PlayerProfileRecord?> Handle(GetPlayerProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetUser(request.UserId, cancellationToken);
        if (user == null) return null;

        var viewer = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var visibility = (await _visibility.GetAudience(viewer, cancellationToken)).Describe(user.Id, user.IsPublic);
        if (!visibility.CanView) return null;

        var stats = await _playerStats.GetStats(request.Mix, request.UserId, cancellationToken);
        var charts = await _charts.GetCharts(request.Mix, null, null, null, cancellationToken);

        // Passes per chart, from whichever store this mix records into. Reading the Phoenix one
        // on a legacy mix returns nothing, which drew every folder at zero for a player whose
        // scores were sitting in BestAttempt the whole time. The stats above stay Phoenix-only
        // and come back empty on legacy — the page hides what they feed rather than printing
        // zeros as if they were measurements.
        var passes = request.Mix.UsesLegacyScoring()
            ? (await _scores.GetBestXXAttempts(request.Mix, request.UserId, cancellationToken))
            .Where(b => b.BestAttempt != null)
            .ToDictionary(b => b.Chart.Id, b => LegacyPass(b.BestAttempt!))
            : (await _scores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .ToDictionary(s => s.ChartId,
                s => s.Score != null && !s.IsBroken
                    ? (PhoenixLetterGrade?)s.Score.Value.LetterGradeFor(request.Mix)
                    : null);

        // One record per (type, level) folder — singles and doubles are separate folders with
        // separate standings, so the page draws them as two graphs rather than stacking two
        // types into one column. Co-op "levels" are player counts, not difficulty, so they
        // stay off a difficulty axis.
        var completion = charts
            .Where(c => c.Type is ChartType.Single or ChartType.Double or ChartType.SinglePerformance
                or ChartType.DoublePerformance)
            .GroupBy(c => (
                Type: c.Type is ChartType.Single or ChartType.SinglePerformance
                    ? ChartType.Single
                    : ChartType.Double,
                Level: (int)c.Level))
            .OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Level)
            .Select(g =>
            {
                var folderPasses = g
                    .Select(c => passes.TryGetValue(c.Id, out var grade) ? grade : null)
                    .Where(grade => grade != null)
                    .Select(grade => grade!.Value)
                    .ToArray();
                return new PlayerFolderCompletionRecord(g.Key.Type, g.Key.Level, folderPasses.Length, g.Count(),
                    folderPasses.GroupBy(grade => grade).ToDictionary(x => x.Key, x => x.Count()));
            })
            .ToArray();

        return new PlayerProfileRecord(user.Id, user.Name, user.ProfileImage, user.Country, visibility,
            stats.SkillRating, stats.TotalRating, stats.SinglesRating, stats.DoublesRating,
            stats.SinglesCompetitiveLevel, stats.DoublesCompetitiveLevel,
            stats.HighestLevel, stats.ClearCount, completion);
    }

    /// <summary>
    ///     A legacy pass as a Phoenix letter, so one completion record serves both models. The
    ///     two ladders share every letter XX uses (F through SSS) — the same equivalence
    ///     LetterGradeIcon draws on. A broken run is not a pass in either model.
    /// </summary>
    private static PhoenixLetterGrade? LegacyPass(XXChartAttempt attempt)
    {
        if (attempt.IsBroken) return null;

        return Enum.TryParse<PhoenixLetterGrade>(attempt.LetterGrade.ToString(), out var equivalent)
            ? equivalent
            : null;
    }
}
