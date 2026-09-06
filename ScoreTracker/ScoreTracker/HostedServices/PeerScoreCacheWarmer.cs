using MediatR;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.HostedServices
{
    /// <summary>
    ///     Fills the two peer-score stores for Phoenix 2 at startup, so the first viewer after a
    ///     deploy doesn't pay for them (docs/design/pumbility-overhaul.md §6.14).
    ///     <para>
    ///         Phoenix 2 only, and deliberately. It is the mix the peers page runs on, and its whole
    ///         population is forty thousand scores — a second's work. Phoenix 1 is a million, and
    ///         loading all of it at every deploy would spend minutes of a small instance preloading
    ///         players nobody is going to look at; there, the store fills a peer group at a time, on
    ///         the first read that asks for one.
    ///     </para>
    ///     Fire-and-forget and fully swallowed, like the chart-page warmer: a warm-up must never
    ///     delay readiness or fail startup — a cold store just fills on the first request.
    /// </summary>
    public sealed class PeerScoreCacheWarmer : IHostedService
    {
        private const MixEnum Warmed = MixEnum.Phoenix2;

        private readonly ILogger<PeerScoreCacheWarmer> _logger;
        private readonly IServiceProvider _services;

        public PeerScoreCacheWarmer(IServiceProvider services, ILogger<PeerScoreCacheWarmer> logger)
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
                await mediator.Send(new WarmPeerScoresCommand(Warmed), cancellationToken);
                await mediator.Send(new WarmBoardScoresCommand(Warmed), cancellationToken);
                _logger.LogInformation("Peer score caches warmed.");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Peer score warm-up failed; the stores will fill on demand.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
