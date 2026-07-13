using MassTransit;
using ScoreTracker.Communities.Contracts.Messages;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Weekly purge of community-highlight summaries past the 30-day retention window (CH7).
///     Idempotent — deletes by timestamp, safe to re-fire.
/// </summary>
internal sealed class CommunityHighlightPurgeConsumer : IConsumer<PurgeCommunityHighlightsCommand>
{
    private const int RetentionDays = 30;

    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ICommunityHighlightRepository _highlights;

    public CommunityHighlightPurgeConsumer(ICommunityHighlightRepository highlights,
        IDateTimeOffsetAccessor dateTime)
    {
        _highlights = highlights;
        _dateTime = dateTime;
    }

    public async Task Consume(ConsumeContext<PurgeCommunityHighlightsCommand> context)
    {
        await _highlights.PurgeBefore(_dateTime.Now.AddDays(-RetentionDays), context.CancellationToken);
    }
}
