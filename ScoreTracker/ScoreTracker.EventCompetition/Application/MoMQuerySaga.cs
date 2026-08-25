using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The MoM read surface: seasons, boards, sessions and the Past Seasons dialog's
///     listing. Boards rank sessions, not players (D16) — score descending, earliest
///     publication breaking a tie (§1) — and a draft is served only to its owner (D17).
/// </summary>
internal sealed class MoMQuerySaga :
    IRequestHandler<GetMoMSeasonQuery, MoMSeasonView?>,
    IRequestHandler<GetMoMBoardQuery, MoMBoardView?>,
    IRequestHandler<GetMoMSessionQuery, MoMSessionView?>,
    IRequestHandler<GetMoMDraftQuery, MoMSessionView?>,
    IRequestHandler<GetMoMSeasonsQuery, IReadOnlyList<MoMSeasonListing>>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMoMRepository _mom;
    private readonly IUserReader _users;

    public MoMQuerySaga(IMoMRepository mom, IUserReader users, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime, IChartRepository charts)
    {
        _mom = mom;
        _users = users;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _charts = charts;
    }

    public async Task<MoMSeasonView?> Handle(GetMoMSeasonQuery request,
        CancellationToken cancellationToken)
    {
        var ordered = (await _mom.GetSeasons(cancellationToken))
            .OrderBy(s => s.StartsAt).ToArray();
        var season = Resolve(ordered, request);
        if (season == null) return null;

        var boards = (await _mom.GetBoards(cancellationToken))
            .Where(b => b.SeasonId == season.Id).ToArray();
        var counts = (await _mom.GetPublishedSessions(boards.Select(b => b.Id).ToArray(),
                cancellationToken))
            .GroupBy(s => s.BoardId)
            .ToDictionary(g => g.Key, g => g.Count());

        var index = Array.IndexOf(ordered, season);
        return new MoMSeasonView(season.Id, season.Name, season.Year, season.Quarter,
            season.StartsAt, season.EndsAt, IsLive(season),
            boards.Select(b => new MoMBoardSummary(b.Id, b.Mix, b.ChartType,
                counts.TryGetValue(b.Id, out var count) ? count : 0)).ToArray(),
            index > 0 ? Ref(ordered[index - 1]) : null,
            index < ordered.Length - 1 ? Ref(ordered[index + 1]) : null);
    }

    public async Task<MoMBoardView?> Handle(GetMoMBoardQuery request,
        CancellationToken cancellationToken)
    {
        var board = (await _mom.GetBoards(cancellationToken))
            .FirstOrDefault(b => b.Id == request.BoardId);
        if (board == null) return null;

        var season = (await _mom.GetSeasons(cancellationToken))
            .First(s => s.Id == board.SeasonId);
        var ranked = Rank(await _mom.GetPublishedSessions(new[] { board.Id }, cancellationToken));
        var users = (await _users.GetUsers(ranked.Select(s => s.UserId).Distinct().ToArray(),
                cancellationToken))
            .ToDictionary(u => u.Id);

        return new MoMBoardView(board.Id, Ref(season), board.Mix, board.ChartType,
            ranked.Select((s, i) =>
            {
                users.TryGetValue(s.UserId, out var user);
                return new MoMBoardRow(i + 1, s.Id, s.UserId,
                    user?.Name.ToString() ?? "?", user?.ProfileImage,
                    user?.Country?.ToString(), s.TotalScore, s.ChartsPlayed,
                    s.AverageDifficulty, s.AverageGrade, s.LowestLevel, s.HighestLevel,
                    TimeSpan.FromTicks(s.RestTimeTicks), s.PublishedAt!.Value,
                    ParseUri(s.VideoUrl));
            }).ToArray());
    }

    public async Task<MoMSessionView?> Handle(GetMoMSessionQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _mom.GetSession(request.SessionId, cancellationToken);
        if (session == null) return null;
        // A draft is visible only to its owner (D17); to anyone else it does not exist.
        if (session.PublishedAt == null && !IsOwnerOrAdmin(session.UserId)) return null;

        return await Compose(session, cancellationToken);
    }

    public async Task<MoMSessionView?> Handle(GetMoMDraftQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;
        var draft = await _mom.GetDraft(request.BoardId, _currentUser.User.Id, cancellationToken);
        return draft == null ? null : await Compose(draft, cancellationToken);
    }

    public async Task<IReadOnlyList<MoMSeasonListing>> Handle(GetMoMSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        var seasons = (await _mom.GetSeasons(cancellationToken))
            .OrderByDescending(s => s.StartsAt).ToArray();
        var boards = (await _mom.GetBoards(cancellationToken))
            .GroupBy(b => b.SeasonId).ToDictionary(g => g.Key, g => g.ToArray());
        var sessions = (await _mom.GetPublishedSessions(
                boards.Values.SelectMany(b => b.Select(x => x.Id)).ToArray(), cancellationToken))
            .GroupBy(s => s.BoardId).ToDictionary(g => g.Key, g => Rank(g.ToArray()));
        var viewerId = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;

        var winnerIds = sessions.Values
            .Where(ranked => ranked.Count > 0)
            .Select(ranked => ranked[0].UserId)
            .Distinct().ToArray();
        var winners = (await _users.GetUsers(winnerIds, cancellationToken))
            .ToDictionary(u => u.Id);

        return seasons.Select(season => new MoMSeasonListing(Ref(season), season.StartsAt,
                season.EndsAt, IsLive(season),
                (boards.TryGetValue(season.Id, out var seasonBoards)
                    ? seasonBoards
                    : Array.Empty<MoMBoardRecord>())
                .Select(b =>
                {
                    var ranked = sessions.TryGetValue(b.Id, out var r)
                        ? r
                        : Array.Empty<MoMSessionRecord>();
                    var winner = ranked.Count > 0 ? ranked[0] : null;
                    var mine = viewerId == null
                        ? Array.Empty<(MoMSessionRecord Session, int Place)>()
                        : ranked.Select((s, i) => (Session: s, Place: i + 1))
                            .Where(x => x.Session.UserId == viewerId)
                            .ToArray();
                    var yourBest = mine.Length > 0
                        ? mine.MinBy(x => x.Place)
                        : ((MoMSessionRecord Session, int Place)?)null;
                    return new MoMBoardStanding(b.Id, b.Mix, b.ChartType, ranked.Count,
                        winner == null
                            ? null
                            : winners.TryGetValue(winner.UserId, out var user)
                                ? user.Name.ToString()
                                : "?",
                        winner?.TotalScore,
                        yourBest?.Place, yourBest?.Session.TotalScore, yourBest?.Session.Id);
                }).ToArray()))
            .ToArray();
    }

    /// <summary>
    ///     Score descending, earliest publication winning a tie (§1). Sessions arrive
    ///     published-only from the repository, so PublishedAt is always set here.
    /// </summary>
    private static IReadOnlyList<MoMSessionRecord> Rank(
        IEnumerable<MoMSessionRecord> sessions)
    {
        return sessions.OrderByDescending(s => s.TotalScore).ThenBy(s => s.PublishedAt).ToArray();
    }

    private async Task<MoMSessionView> Compose(MoMSessionRecord session,
        CancellationToken cancellationToken)
    {
        var board = (await _mom.GetBoards(cancellationToken)).First(b => b.Id == session.BoardId);
        var season = (await _mom.GetSeasons(cancellationToken)).First(s => s.Id == board.SeasonId);
        var configuration = await _mom.GetBoardConfiguration(board.Id, true, cancellationToken)
                            ?? throw new InvalidOperationException(
                                $"MoM board {board.Id} has no configuration");
        var chartRows = await _mom.GetSessionCharts(session.Id, cancellationToken);
        var snapshot = await _mom.GetSeasonSnapshot(board.Id, cancellationToken);
        var levels = chartRows.Count == 0
            ? new Dictionary<Guid, Chart>()
            : (await _charts.GetCharts(board.Mix,
                chartIds: chartRows.Select(c => c.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var user = await _users.GetUser(session.UserId, cancellationToken);

        int? place = null;
        if (session.PublishedAt != null)
        {
            var ranked = Rank(await _mom.GetPublishedSessions(new[] { board.Id },
                cancellationToken));
            var index = ranked.ToList().FindIndex(s => s.Id == session.Id);
            if (index >= 0) place = index + 1;
        }

        return new MoMSessionView(session.Id, board.Id, Ref(season), board.Mix, board.ChartType,
            session.UserId, user?.Name.ToString() ?? "?", session.PublishedAt,
            session.TotalScore, session.ChartsPlayed, TimeSpan.FromTicks(session.RestTimeTicks),
            session.AverageDifficulty, session.AverageGrade, session.LowestLevel,
            session.HighestLevel, ParseUri(session.VideoUrl), place,
            configuration.MaxTime, configuration.AllowRepeats,
            chartRows.Select(c => new MoMSessionChartRow(c.Ordinal, c.ChartId, c.Score,
                Enum.Parse<PhoenixPlate>(c.Plate), c.IsBroken, c.SessionScore,
                c.BonusPoints, c.PlayedAt,
                snapshot.TryGetValue(c.ChartId, out var balanced)
                    ? balanced
                    : levels.TryGetValue(c.ChartId, out var chart)
                        ? (int)chart.Level + 0.5
                        : 0)).ToArray());
    }

    private bool IsOwnerOrAdmin(Guid userId)
    {
        return _currentUser.IsLoggedIn &&
               (_currentUser.User.Id == userId || _currentUser.User.IsAdmin);
    }

    private bool IsLive(MoMSeason season)
    {
        var now = _dateTime.Now;
        return season.StartsAt <= now && season.EndsAt > now;
    }

    private MoMSeason? Resolve(IReadOnlyList<MoMSeason> ordered, GetMoMSeasonQuery request)
    {
        if (request.Year != null && request.Quarter != null)
            return ordered.FirstOrDefault(s =>
                s.Year == request.Year && s.Quarter == request.Quarter);
        if (!string.IsNullOrWhiteSpace(request.LegacyName))
        {
            // Hyphens equal spaces so a URL segment resolves against the stored name directly.
            var wanted = request.LegacyName.Replace('-', ' ');
            return ordered.FirstOrDefault(s => s.Year == null &&
                                               string.Equals(s.Name, wanted,
                                                   StringComparison.OrdinalIgnoreCase));
        }

        // No selector: the live season, else the most recent one already started — the gap
        // between a season's end and the next cycle tick still renders a page.
        var now = _dateTime.Now;
        return ordered.LastOrDefault(s => s.StartsAt <= now && s.EndsAt > now)
               ?? ordered.LastOrDefault(s => s.StartsAt <= now)
               ?? ordered.FirstOrDefault();
    }

    private static MoMSeasonRef Ref(MoMSeason season)
    {
        return new MoMSeasonRef(season.Id, season.Name, season.Year, season.Quarter);
    }

    private static Uri? ParseUri(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed : null;
    }
}
