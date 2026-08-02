using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Communities.Domain;
using ScoreTracker.PlayerProgress.Contracts.Events;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Indexes a stored significant-win event against the winner's communities
///     (docs/design/rivals.md §4.2). Writes no wins: the payload lives once in PlayerProgress, and
///     this only records who may see it.
///     <para>
///         An event rather than a direct call because the capture moved out of this vertical —
///         PlayerProgress cannot write Communities' table, and Communities needs nothing from the
///         payload to index it.
///     </para>
///     Failure-isolated for the same reason the capture is: a missing index row costs one feed
///     entry, and nothing upstream should care.
/// </summary>
internal sealed class CommunityHighlightIndexSaga : IConsumer<PlayerHighlightsStoredEvent>
{
    private readonly ICommunityHighlightRepository _highlights;
    private readonly ILogger<CommunityHighlightIndexSaga> _logger;

    public CommunityHighlightIndexSaga(ICommunityHighlightRepository highlights,
        ILogger<CommunityHighlightIndexSaga> logger)
    {
        _highlights = highlights;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PlayerHighlightsStoredEvent> context)
    {
        var message = context.Message;
        try
        {
            await _highlights.AddForUserCommunities(message.EventId, message.UserId, message.Mix,
                message.OccurredAt, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Community highlight indexing failed for event {EventId}", message.EventId);
        }
    }
}
