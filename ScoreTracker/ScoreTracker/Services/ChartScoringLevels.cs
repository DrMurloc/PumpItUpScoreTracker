using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Application.Queries;

namespace ScoreTracker.Web.Services;

// Circuit-scoped lazy cache of the Chart Intelligence scoring-level projection so a
// per-chart lookup (the similar-charts shelf's difficulty sort and chart facts) doesn't
// issue a query each.
public sealed class ChartScoringLevels
{
    private readonly IMediator _mediator;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IDictionary<Guid, double>? _levels;

    public ChartScoringLevels(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<double?> GetScoringLevel(Guid chartId, CancellationToken cancellationToken = default)
    {
        if (_levels == null)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _levels ??= await _mediator.Send(new GetChartScoringLevelsQuery(), cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
        }

        return _levels.TryGetValue(chartId, out var level) ? level : null;
    }
}
