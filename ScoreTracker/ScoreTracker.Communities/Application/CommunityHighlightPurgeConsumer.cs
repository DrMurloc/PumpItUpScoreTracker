using MassTransit;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Messages;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Weekly purge of the community audience index past the 30-day retention window (CH7).
///     Idempotent — deletes by timestamp, safe to re-fire.
///     <para>
///         Same command as PlayerProgress's payload purge, deliberately: two commands could drift
///         and leave the index pointing at wins that no longer exist.
///     </para>
/// </summary>
internal sealed class CommunityHighlightPurgeConsumer : IConsumer<PurgePlayerHighlightsCommand>
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

    public async Task Consume(ConsumeContext<PurgePlayerHighlightsCommand> context)
    {
        await _highlights.PurgeBefore(_dateTime.Now.AddDays(-RetentionDays), context.CancellationToken);
    }
}
