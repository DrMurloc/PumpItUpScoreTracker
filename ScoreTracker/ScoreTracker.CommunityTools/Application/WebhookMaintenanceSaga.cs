using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.CommunityTools.Contracts.Messages;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     The two housekeeping jobs behind the delivery queue: retry what is owed, and drop what is
///     past its window.
/// </summary>
internal sealed class WebhookMaintenanceSaga :
    IConsumer<RetryDueWebhookDeliveriesCommand>,
    IConsumer<PruneWebhookDeliveriesCommand>
{
    /// <summary>
    ///     Per sweep. At real volume the queue is far smaller than this — the cap exists so one
    ///     tool going dark for a day cannot turn a five-minute sweep into an hour-long one.
    /// </summary>
    private const int BatchSize = 200;

    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IWebhookDeliveryDispatcher _dispatcher;
    private readonly ILogger<WebhookMaintenanceSaga> _logger;

    public WebhookMaintenanceSaga(IWebhookDeliveryRepository deliveries,
        IWebhookDeliveryDispatcher dispatcher, IDateTimeOffsetAccessor dateTime,
        ILogger<WebhookMaintenanceSaga> logger)
    {
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RetryDueWebhookDeliveriesCommand> context)
    {
        // Claimed for long enough to outlast the worst case: BatchSize endpoints each burning the
        // client's ten-second timeout. A sweep that overruns its 5-minute cadence therefore does not
        // find its own in-flight rows waiting for it.
        var now = _dateTime.Now;
        var claimUntil = now + TimeSpan.FromSeconds(BatchSize * 10 + 60);
        var due = await _deliveries.GetDue(now, BatchSize, claimUntil, context.CancellationToken);
        foreach (var delivery in due)
            try
            {
                await _dispatcher.Attempt(delivery.Id, context.CancellationToken);
            }
            catch (Exception e)
            {
                // One endpoint's failure must not abandon the rest of the sweep.
                _logger.LogWarning(e, "Retry failed for delivery {DeliveryId}", delivery.DeliveryId);
            }
    }

    public async Task Consume(ConsumeContext<PruneWebhookDeliveriesCommand> context)
    {
        var now = _dateTime.Now;
        await _deliveries.Prune(now - WebhookRetention.Bodies, now - WebhookRetention.Metadata,
            context.CancellationToken);
    }
}
