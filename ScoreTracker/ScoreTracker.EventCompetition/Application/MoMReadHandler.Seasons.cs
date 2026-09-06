using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;

namespace ScoreTracker.EventCompetition.Application;

internal sealed partial class MoMReadHandler
{
    public async Task<IReadOnlyList<MoMSeasonListing>> Handle(GetMoMSeasonsQuery request,
        CancellationToken cancellationToken)
    {
        var seasons = await _mom.GetSeasons(cancellationToken);
        var boards = (await _mom.GetBoards(seasons.Select(s => s.Id), cancellationToken))
            .Where(b => b.Mix == request.Mix)
            .ToArray();
        var sessions = await _mom.GetPublishedSessions(boards.Select(b => b.Id), cancellationToken);
        var byBoard = sessions.GroupBy(s => s.BoardId).ToDictionary(g => g.Key,
            g => MoMBoardRanking.Order(g, s => s.TotalScore, s => s.PublishedAt!.Value));
        // Only the winners need a name and a face here; the viewer's own result is a place.
        var winners = await Players(byBoard.Values.Where(r => r.Count > 0).Select(r => r[0].UserId), cancellationToken);
        // A season with no board of this mix is not part of the mix's history (D38): the
        // dialog would otherwise offer Phoenix 2 a row of seasons with nothing on them.
        return seasons.Where(season => boards.Any(b => b.SeasonId == season.Id))
            .OrderByDescending(s => s.StartsAt)
            .Select(season => new MoMSeasonListing(Summary(season),
                BoardsInOrder(boards.Where(b => b.SeasonId == season.Id), request.Mix).Select(board =>
                {
                    var ranked = byBoard.GetValueOrDefault(board.Id) ?? Array.Empty<MoMStoredSession>();
                    var winner = ranked.Count > 0 ? ranked[0] : null;
                    // The viewer's best session on the board is the first of theirs in rank order.
                    var myIndex = request.ViewerId == null
                        ? -1
                        : ranked.ToList().FindIndex(s => s.UserId == request.ViewerId);
                    return new MoMSeasonBoardListing(board.Id, board.ChartType, ranked.Count,
                        winner == null ? null : winners.GetValueOrDefault(winner.UserId), winner?.TotalScore,
                        myIndex < 0 ? null : myIndex + 1,
                        myIndex < 0 ? null : ranked[myIndex].TotalScore);
                }).ToArray()))
            .ToArray();
    }
}
