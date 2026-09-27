using MassTransit;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Messages;

namespace ScoreTracker.Web.HostedServices;

/// <summary>
///     Kicks the restart-recovery pass once, a few minutes after the process comes up
///     (docs/design/import-restart-recovery.md §0).
///     <para>
///         ⚠ This is the whole trigger. There is no Hangfire job, no timer and no rescheduling —
///         the failure being recovered from is the process going away, so the process coming back
///         is precisely the moment to look, and a cadence would only add a scheduled job to
///         forget about. Deliberately NOT in RecurringJobRunner for the same reason.
///     </para>
///     <para>
///         The wait is for the process being replaced. A deploy can bring this one up while the old
///         one is still finishing its imports; recovering those before they finish would announce
///         them twice.
///     </para>
/// </summary>
public sealed class StartupRecoveryPublisher : BackgroundService
{
    private static readonly TimeSpan Delay = TimeSpan.FromMinutes(3);

    private readonly IBus _bus;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<StartupRecoveryPublisher> _logger;
    private DateTimeOffset _bootedAt;

    public StartupRecoveryPublisher(IBus bus, IDateTimeOffsetAccessor dateTime,
        ILogger<StartupRecoveryPublisher> logger)
    {
        _bus = bus;
        _dateTime = dateTime;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Stamped HERE, on the way up, not when the pass is sent. This is the line that divides
        // "belonged to the process that just died" from "started under this one", and every import
        // pressed during the wait below belongs to this process.
        _bootedAt = _dateTime.Now;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Delay, stoppingToken);
            await _bus.Publish(new RecoverInterruptedImportsCommand(_bootedAt), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopped before the pass was due; the next start looks instead.
        }
        catch (Exception e)
        {
            // Startup is not the place to be brittle: a recovery that cannot be kicked off is a
            // missed repair, not a reason to refuse to serve the site.
            _logger.LogError(e, "Could not publish the startup import-recovery pass");
        }
    }
}
