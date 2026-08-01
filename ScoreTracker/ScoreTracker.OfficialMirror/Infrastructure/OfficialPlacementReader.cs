using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

/// <summary>
///     The published port over the mirror's own placement estimate, so an upstream vertical can
///     ask without referencing this assembly. A dispatch and nothing else — the estimate's rules
///     (self-row exclusion, omitting scores past the board's depth) stay in the one handler.
/// </summary>
internal sealed class OfficialPlacementReader(IMediator mediator) : IOfficialPlacementReader
{
    public async Task<IReadOnlyDictionary<Guid, OfficialPlacementReading>> EstimatePlacements(MixEnum mix,
        Guid userId, IReadOnlyList<(Guid ChartId, int Score)> scores, CancellationToken cancellationToken)
    {
        var estimates = await mediator.Send(new GetOfficialChartPlacementsQuery(mix, userId,
            scores.Select(s => new GetOfficialChartPlacementsQuery.ChartScore(s.ChartId, s.Score)).ToArray()),
            cancellationToken);
        return estimates.ToDictionary(kv => kv.Key,
            kv => new OfficialPlacementReading(kv.Value.Place, kv.Value.BoardDepth, kv.Value.AsOf));
    }
}
