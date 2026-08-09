using MassTransit;
using ScoreTracker.OfficialMirror.Contracts.Messages;

namespace ScoreTracker.Web.HostedServices;

/// <summary>
///     Kicks the restart-recovery pass once, on the way up
///     (docs/design/import-restart-recovery.md §4).
///     <para>
///         ⚠ This is the whole trigger. There is no Hangfire job, no timer and no rescheduling —
///         the failure being recovered from is the process going away, so the process coming back
///         is precisely the moment to look, and a cadence would only add a scheduled job to
///         forget about. Deliberately NOT in RecurringJobRunner for the same reason.
///     </para>
/// </summary>
public sealed class StartupRecoveryPublisher : IHostedService
{
    private readonly IBus _bus;
    private readonly ILogger<StartupRecoveryPublisher> _logger;

    public StartupRecoveryPublisher(IBus bus, ILogger<StartupRecoveryPublisher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _bus.Publish(new RecoverInterruptedImportsCommand(), cancellationToken);
        }
        catch (Exception e)
        {
            // Startup is not the place to be brittle: a recovery that cannot be kicked off is a
            // missed repair, not a reason to refuse to serve the site.
            _logger.LogError(e, "Could not publish the startup import-recovery pass");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
