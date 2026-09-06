using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     The published port over the mirror's own reads, so an upstream vertical can ask without
///     referencing this assembly. A dispatch and nothing else — the placement estimate's rules
///     (self-row exclusion, omitting scores past the board's depth) stay in the one handler, and
///     the board-peer rules stay in <see cref="BoardPeerReader" />.
/// </summary>
internal sealed class OfficialPlacementReader(IMediator mediator, BoardPeerReader boardPeers)
    : IOfficialPlacementReader
{
    public Task<BoardPeerGroupReading?> GetBoardPeers(MixEnum mix, ChartType chartType, double minimumPool,
        double maximumPool, CancellationToken cancellationToken)
    {
        return boardPeers.GetBoardPeers(mix, chartType, minimumPool, maximumPool, cancellationToken);
    }

    public Task<IReadOnlyList<BoardScoreReading>> GetBoardScores(MixEnum mix, ChartType chartType,
        IReadOnlyCollection<int> boardPlayerIds, int minimumLevel, int maximumLevel,
        CancellationToken cancellationToken)
    {
        return boardPeers.GetBoardScores(mix, chartType, boardPlayerIds, minimumLevel, maximumLevel,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, OfficialPlacementReading>> EstimatePlacements(MixEnum mix,
        Guid userId, IReadOnlyList<(Guid ChartId, int Score)> scores, CancellationToken cancellationToken)
    {
        var estimates = await mediator.Send(new GetOfficialChartPlacementsQuery(mix, userId,
            scores.Select(s => new GetOfficialChartPlacementsQuery.ChartScore(s.ChartId, s.Score)).ToArray()),
            cancellationToken);
        return estimates.ToDictionary(kv => kv.Key,
            kv => new OfficialPlacementReading(kv.Value.Place, kv.Value.BoardDepth, kv.Value.AsOf));
    }

    public async Task<OfficialBoardReading?> GetPumbilityBoard(MixEnum mix, string boardName,
        CancellationToken cancellationToken)
    {
        var board = await mediator.Send(new GetOfficialPumbilityBoardQuery(mix, boardName), cancellationToken);
        return board == null ? null : new OfficialBoardReading(board.AsOf, board.DescendingValues);
    }
}
