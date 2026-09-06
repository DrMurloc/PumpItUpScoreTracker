using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The read side of March of Murlocs (docs/design/march-of-murlocs.md §12.2): the Season
///     page, one session in full, two sessions compared, the seasons dialog, and the legacy
///     board locator. Reads the MoM tables through the vertical's own port and everything else
///     through published ports — charts from the catalog, players from Identity — never a join.
/// </summary>
internal sealed partial class MoMReadHandler :
    IRequestHandler<GetMoMSeasonPageQuery, MoMSeasonPage?>,
    IRequestHandler<GetMoMSessionQuery, MoMSessionView?>,
    IRequestHandler<CompareMoMSessionsQuery, MoMComparison?>,
    IRequestHandler<GetMoMSeasonsQuery, IReadOnlyList<MoMSeasonListing>>,
    IRequestHandler<GetMoMBoardLocatorQuery, MoMBoardLocator?>
{
    private readonly IMoMReadRepository _mom;
    private readonly IChartRepository _charts;
    private readonly IUserReader _users;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public MoMReadHandler(IMoMReadRepository mom, IChartRepository charts, IUserReader users,
        IDateTimeOffsetAccessor dateTime)
    {
        _mom = mom;
        _charts = charts;
        _users = users;
        _dateTime = dateTime;
    }

    public async Task<MoMSeasonPage?> Handle(GetMoMSeasonPageQuery request, CancellationToken cancellationToken)
    {
        var seasons = await _mom.GetSeasons(cancellationToken);
        var season = request.SeasonId is { } id
            ? seasons.FirstOrDefault(s => s.Id == id)
            : LiveSeason(seasons);
        if (season == null) return null;
        // Neighbours on the season clock, not in the list: the archive's crawlable path.
        var chronological = seasons.OrderBy(s => s.StartsAt).ToList();
        var index = chronological.FindIndex(s => s.Id == season.Id);
        var previous = index > 0 ? chronological[index - 1] : null;
        var next = index < chronological.Count - 1 ? chronological[index + 1] : null;

        var boards = BoardsInOrder(await _mom.GetBoards(new[] { season.Id }, cancellationToken), request.Mix);
        var sessions = await _mom.GetPublishedSessions(boards.Select(b => b.Id), cancellationToken);
        var players = await Players(sessions.Select(s => s.UserId), cancellationToken);
        var views = boards.Select(board =>
        {
            var ranked = MoMBoardRanking.Order(sessions.Where(s => s.BoardId == board.Id), s => s.TotalScore,
                s => s.PublishedAt!.Value);
            var rows = ranked.Select((s, i) => new MoMBoardRow(i + 1, s.Id, s.UserId, players.GetValueOrDefault(s.UserId),
                MoMBoardRanking.SessionNumber(ranked, s, x => x.UserId, x => x.PublishedAt!.Value),
                s.TotalScore, s.ChartsPlayed, s.AverageDifficulty, s.Downtime, s.PublishedAt!.Value, s.VideoUrl)).ToArray();
            return new MoMBoardView(board.Id, board.ChartType, board.Mix, board.Configuration.MaxTime, rows,
                Standing(rows, request.ViewerId));
        }).ToArray();
        return new MoMSeasonPage(Summary(season), views, previous == null ? null : Summary(previous),
            next == null ? null : Summary(next));
    }

    public async Task<MoMBoardLocator?> Handle(GetMoMBoardLocatorQuery request, CancellationToken cancellationToken)
    {
        var board = await _mom.GetBoard(request.BoardId, cancellationToken);
        if (board == null) return null;
        var season = (await _mom.GetSeasons(cancellationToken)).FirstOrDefault(s => s.Id == board.SeasonId);
        return season == null ? null : new MoMBoardLocator(season.Id, board.ChartType, board.Mix, IsLive(season));
    }

    /// <summary>
    ///     The season the clock is inside; failing that (a gap between a season's end and the
    ///     next cycle's tick) the most recently started one, so the landing page always has a
    ///     season to show — they auto-cycle, there is always one.
    /// </summary>
    private MoMSeason? LiveSeason(IReadOnlyList<MoMSeason> seasons)
    {
        return seasons.FirstOrDefault(IsLive) ?? seasons.OrderByDescending(s => s.StartsAt).FirstOrDefault();
    }

    private bool IsLive(MoMSeason season)
    {
        var now = _dateTime.Now;
        return season.StartsAt <= now && season.EndsAt > now;
    }

    private MoMSeasonSummary Summary(MoMSeason season)
    {
        return new MoMSeasonSummary(season.Id, season.Name, season.StartsAt, season.EndsAt, IsLive(season));
    }

    /// <summary>Doubles first, then Singles (§11.2): that is where the event's history lives.</summary>
    private static IReadOnlyList<MoMBoardInfo> BoardsInOrder(IEnumerable<MoMBoardInfo> boards, MixEnum mix)
    {
        return boards.Where(b => b.Mix == mix)
            .OrderBy(b => b.ChartType == ChartType.Double ? 0 : b.ChartType == ChartType.Single ? 1 : 2)
            .ToArray();
    }

    private static MoMStanding? Standing(IReadOnlyList<MoMBoardRow> rows, Guid? viewerId)
    {
        if (viewerId == null) return null;
        var mine = rows.Where(r => r.UserId == viewerId).ToArray();
        if (mine.Length == 0) return null;
        var best = mine.MinBy(r => r.Place)!;
        return new MoMStanding(best.Place, rows.Count, best.SessionId, best.TotalScore, best.ChartsPlayed,
            best.Downtime, mine.Length);
    }

    private async Task<IReadOnlyDictionary<Guid, User>> Players(IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, User>();
        return (await _users.GetUsers(ids, cancellationToken)).ToDictionary(u => u.Id);
    }

    /// <summary>
    ///     Stored chart rows joined onto the catalog and the board's balance: the snapshot's
    ///     override where one exists, the folder level + 0.5 where none does (§9.3). A chart the
    ///     catalog no longer knows drops out of the list; the stored total still stands.
    /// </summary>
    private static IReadOnlyList<MoMSessionChart> Rows(IEnumerable<MoMStoredSessionChart> stored,
        IReadOnlyDictionary<Guid, Chart> charts, MoMBoardInfo board)
    {
        var snapshot = board.Configuration.Scoring.ChartLevelSnapshot;
        return stored.OrderBy(r => r.Ordinal)
            .Where(r => charts.ContainsKey(r.ChartId))
            .Select(r =>
            {
                var chart = charts[r.ChartId];
                var balanced = snapshot != null && snapshot.TryGetValue(chart.Id, out var level)
                    ? level
                    : (int)chart.Level + .5;
                return new MoMSessionChart(chart, r.Score, r.Plate, r.IsBroken, r.SessionScore, r.BonusPoints,
                    balanced, r.PlayedAt);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, Chart>> Charts(MixEnum mix, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        var ids = chartIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, Chart>();
        return (await _charts.GetCharts(mix, chartIds: ids, cancellationToken: cancellationToken))
            .GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
    }
}
