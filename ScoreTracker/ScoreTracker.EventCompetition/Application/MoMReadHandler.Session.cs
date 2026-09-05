using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;

namespace ScoreTracker.EventCompetition.Application;

internal sealed partial class MoMReadHandler
{
    public async Task<MoMSessionView?> Handle(GetMoMSessionQuery request, CancellationToken cancellationToken)
    {
        var stored = await _mom.GetSession(request.SessionId, cancellationToken);
        if (stored == null) return null;
        // A draft is its owner's until it is published (D17); nobody else has a page for it.
        if (stored.PublishedAt == null && stored.UserId != request.ViewerId) return null;
        var board = await _mom.GetBoard(stored.BoardId, cancellationToken);
        if (board == null) return null;
        var seasons = await _mom.GetSeasons(cancellationToken);
        var season = seasons.FirstOrDefault(s => s.Id == board.SeasonId);
        if (season == null) return null;

        // Every published session on the board, plus this one if it is a draft: the marks and
        // the compare picker read the whole board, and a draft still deserves its numbers.
        var published = await _mom.GetPublishedSessions(new[] { board.Id }, cancellationToken);
        var onBoard = published.Any(s => s.Id == stored.Id) ? published : published.Append(stored).ToArray();
        var storedRows = await _mom.GetSessionCharts(onBoard.Select(s => s.Id), cancellationToken);
        var charts = await Charts(board.Mix, storedRows.Select(r => r.ChartId), cancellationToken);
        var rowsBySession = storedRows.GroupBy(r => r.SessionId)
            .ToDictionary(g => g.Key, g => Rows(g, charts, board));
        var window = board.Configuration.MaxTime;
        var levers = onBoard.ToDictionary(s => s.Id,
            s => MoMLeverMath.Levers(rowsBySession.GetValueOrDefault(s.Id) ?? Array.Empty<MoMSessionChart>(),
                window, board.Mix));

        var ranked = MoMBoardRanking.Order(published, s => s.TotalScore, s => s.PublishedAt!.Value);
        var players = await Players(onBoard.Select(s => s.UserId), cancellationToken);
        var boardSessions = ranked.Select((s, i) => new MoMBoardSessionSummary(s.Id, s.UserId,
            players.GetValueOrDefault(s.UserId), i + 1,
            MoMBoardRanking.SessionNumber(ranked, s, x => x.UserId, x => x.PublishedAt!.Value), levers[s.Id])).ToArray();
        var mine = levers[stored.Id];
        var place = ranked.ToList().FindIndex(s => s.Id == stored.Id) + 1; // 0 for a draft
        var boardLevers = ranked.Select(s => levers[s.Id]).ToArray();
        var places = new MoMLeverPlaces(
            MoMBoardRanking.LeverPlace(mine.ChartsPlayed, boardLevers.Select(l => (double)l.ChartsPlayed), true),
            MoMBoardRanking.LeverPlace(mine.AverageBalancedLevel, boardLevers.Select(l => l.AverageBalancedLevel), true),
            MoMBoardRanking.LeverPlace((int)mine.AverageScore, boardLevers.Select(l => (double)(int)l.AverageScore), true),
            MoMBoardRanking.LeverPlace(mine.Downtime.TotalSeconds, boardLevers.Select(l => l.Downtime.TotalSeconds), false),
            ranked.Count);

        var past = await OwnersPastSessions(stored, board, seasons, cancellationToken);
        return new MoMSessionView(stored.Id, Summary(season), board.Id, board.ChartType, board.Mix, window,
            stored.UserId, players.GetValueOrDefault(stored.UserId), stored.PublishedAt, stored.VideoUrl, stored.TotalScore, place,
            ranked.Count, mine, places,
            MoMLeverMath.Timeline(rowsBySession.GetValueOrDefault(stored.Id) ?? Array.Empty<MoMSessionChart>(), window),
            boardSessions, past);
    }

    /// <summary>
    ///     The owner's published sessions on the same lineage — same mix, same chart type — in
    ///     other seasons (§11.3). A different type or mix is a different sport and is never
    ///     offered (D15); the same board's other sessions belong to the same-board mode.
    /// </summary>
    private async Task<IReadOnlyList<MoMPastSession>> OwnersPastSessions(MoMStoredSession stored, MoMBoardInfo board,
        IReadOnlyList<MoMSeason> seasons, CancellationToken cancellationToken)
    {
        var lineage = (await _mom.GetBoards(seasons.Select(s => s.Id), cancellationToken))
            .Where(b => b.Id != board.Id && b.Mix == board.Mix && b.ChartType == board.ChartType)
            .ToDictionary(b => b.Id);
        if (lineage.Count == 0) return Array.Empty<MoMPastSession>();
        var seasonsById = seasons.ToDictionary(s => s.Id);
        return (await _mom.GetPublishedSessions(lineage.Keys, cancellationToken))
            .Where(s => s.UserId == stored.UserId)
            .Select(s => new MoMPastSession(s.Id, Summary(seasonsById[lineage[s.BoardId].SeasonId]), s.TotalScore,
                s.PublishedAt!.Value))
            .OrderByDescending(p => p.Season.StartsAt).ThenByDescending(p => p.TotalScore)
            .ToArray();
    }

    public async Task<MoMComparison?> Handle(CompareMoMSessionsQuery request, CancellationToken cancellationToken)
    {
        var mine = await _mom.GetSession(request.SessionId, cancellationToken);
        var theirs = await _mom.GetSession(request.OtherSessionId, cancellationToken);
        if (mine == null || theirs == null) return null;
        var myBoard = await _mom.GetBoard(mine.BoardId, cancellationToken);
        var theirBoard = mine.BoardId == theirs.BoardId ? myBoard : await _mom.GetBoard(theirs.BoardId, cancellationToken);
        if (myBoard == null || theirBoard == null) return null;
        // Never across chart types or mixes (D15): the four numbers mean nothing between them.
        if (myBoard.Mix != theirBoard.Mix || myBoard.ChartType != theirBoard.ChartType) return null;
        var seasons = await _mom.GetSeasons(cancellationToken);
        var mySeason = seasons.FirstOrDefault(s => s.Id == myBoard.SeasonId);
        var theirSeason = seasons.FirstOrDefault(s => s.Id == theirBoard.SeasonId);
        if (mySeason == null || theirSeason == null) return null;

        var storedRows = await _mom.GetSessionCharts(new[] { mine.Id, theirs.Id }, cancellationToken);
        var charts = await Charts(myBoard.Mix, storedRows.Select(r => r.ChartId), cancellationToken);
        var myRows = Rows(storedRows.Where(r => r.SessionId == mine.Id), charts, myBoard);
        var theirRows = Rows(storedRows.Where(r => r.SessionId == theirs.Id), charts, theirBoard);
        var sameBoard = myBoard.Id == theirBoard.Id;
        MoMRepricingSplit? repricing = null;
        var olderIsMine = false;
        if (!sameBoard)
        {
            // The older session is re-priced under the newer season (D20), whichever side it is.
            olderIsMine = mySeason.StartsAt < theirSeason.StartsAt;
            repricing = olderIsMine
                ? MoMRepricing.Split(myRows, mine.TotalScore, myBoard.Configuration, theirBoard.Configuration)
                : MoMRepricing.Split(theirRows, theirs.TotalScore, theirBoard.Configuration, myBoard.Configuration);
        }

        var players = await Players(new[] { theirs.UserId }, cancellationToken);
        return new MoMComparison(mine.Id, theirs.Id,
            MoMLeverMath.Levers(myRows, myBoard.Configuration.MaxTime, myBoard.Mix),
            MoMLeverMath.Levers(theirRows, theirBoard.Configuration.MaxTime, theirBoard.Mix),
            players.GetValueOrDefault(theirs.UserId), Summary(theirSeason), sameBoard,
            MoMCompare.Shared(myRows, theirRows, worstFirst: sameBoard), repricing, olderIsMine);
    }
}
