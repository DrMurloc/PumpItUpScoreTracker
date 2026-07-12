using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     Circuit-scoped memo of the chart catalog per mix (§2.5): N widgets on a board
///     share one dictionary instead of each loading ~thousands of charts. Scoped
///     lifetime = one instance per Blazor circuit, dying with it — no invalidation
///     story needed (the catalog changes on admin imports, not mid-session).
/// </summary>
public sealed class ChartCatalogCache(IMediator mediator)
{
    private readonly Dictionary<MixEnum, IReadOnlyDictionary<Guid, Chart>> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<IReadOnlyDictionary<Guid, Chart>> GetCharts(MixEnum mix,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(mix, out var cached)) return cached;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(mix, out cached)) return cached;
            var charts = (await mediator.Send(new GetChartsQuery(mix), cancellationToken))
                .ToDictionary(c => c.Id);
            _cache[mix] = charts;
            return charts;
        }
        finally
        {
            _lock.Release();
        }
    }
}
