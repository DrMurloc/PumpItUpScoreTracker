using MassTransit;
using ScoreTracker.Catalog.Contracts.Events;
using ScoreTracker.ChartIntelligence.Contracts.Messages;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Turns a finished piucenter ingestion into a Speed rebuild per mix
///     (docs/design/chart-identity.md §2). Its own consumer rather than a branch inside
///     TierListSaga so each mix's rebuild is a separate message: one mix failing does not
///     strand the rest, and a killed run resumes at the mix it stopped on.
/// </summary>
internal sealed class SpeedTierListTrigger : IConsumer<PiuCenterDataIngestedEvent>
{
    public async Task Consume(ConsumeContext<PiuCenterDataIngestedEvent> context)
    {
        foreach (var mix in context.Message.Mixes)
            await context.Publish(new ProcessSpeedTierListCommand(mix), context.CancellationToken);
    }
}
