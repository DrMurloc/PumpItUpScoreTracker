using MassTransit;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Weekly purge of significant-win summaries past the 30-day retention window (CH7).
///     Idempotent — deletes by timestamp, safe to re-fire. Communities purges its audience index
///     off the same command, so the pair cannot fall out of step.
/// </summary>
internal sealed class PlayerHighlightPurgeConsumer : IConsumer<PurgePlayerHighlightsCommand>
{
    private const int RetentionDays = 30;

    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IPlayerHighlightRepository _highlights;

    public PlayerHighlightPurgeConsumer(IPlayerHighlightRepository highlights,
        IDateTimeOffsetAccessor dateTime)
    {
        _highlights = highlights;
        _dateTime = dateTime;
    }

    public async Task Consume(ConsumeContext<PurgePlayerHighlightsCommand> context)
    {
        await _highlights.PurgeBefore(_dateTime.Now.AddDays(-RetentionDays), context.CancellationToken);
    }
}
