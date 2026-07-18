using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.HostedServices
{
    /// <summary>
    ///     Warms the shared, chart-agnostic caches a chart page needs, once at startup, so the
    ///     first visitor doesn't pay them. The expensive one is the verdict's History facet:
    ///     ChartVerdictHandler.MixLevelMap sweeps EVERY mix's full catalog to build the
    ///     cross-mix level map — a multi-second load, cached 24h under one global key and read
    ///     by every chart page. Step analysis loads the whole PIU Center metric table the same
    ///     way. Both are keyed off no particular chart, so warming them through any one chart
    ///     makes the first real chart page fast for everyone.
    ///     Fire-and-forget and fully swallowed: a warm-up must never delay readiness or fail
    ///     startup — a cold cache just fills on the first request, exactly as it does today.
    /// </summary>
    public sealed class ChartPageCacheWarmer : IHostedService
    {
        private readonly ILogger<ChartPageCacheWarmer> _logger;
        private readonly IServiceProvider _services;

        public ChartPageCacheWarmer(IServiceProvider services, ILogger<ChartPageCacheWarmer> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Background, not awaited: the app serves immediately while this fills in behind it.
            _ = Task.Run(() => WarmAsync(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        private async Task WarmAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _services.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var chart = (await mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken))
                    .FirstOrDefault();
                if (chart == null) return;

                // Computing one chart's verdict runs the MixLevelMap sweep and caches it;
                // one step-analysis read fills the whole-table metric cache.
                await mediator.Send(new GetChartVerdictQuery(chart.Id, MixEnum.Phoenix), cancellationToken);
                await mediator.Send(new GetChartStepAnalysisQuery(chart.Id), cancellationToken);
                _logger.LogInformation("Chart page caches warmed.");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Chart page cache warm-up failed; the first chart page will warm on demand.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
