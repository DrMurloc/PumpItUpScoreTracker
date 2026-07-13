using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.PlayerProgress.Contracts.Events;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Materializes the community big-wins feed (docs/design/home-page-widgets.md §7). A SECOND
///     consumer of <see cref="ScoreHighlightsCapturedEvent" /> beside <c>CommunitySaga</c>'s Discord
///     card — it delegates the classify-and-persist to the shared <see cref="ICommunityHighlightCapturer" />
///     (which the admin backfill also drives). Failure-isolated: a feed write must never disturb the
///     import pipeline (same contract as the recap saga).
/// </summary>
internal sealed class CommunityHighlightSaga : IConsumer<ScoreHighlightsCapturedEvent>
{
    private readonly ICommunityHighlightCapturer _capturer;
    private readonly ILogger<CommunityHighlightSaga> _logger;

    public CommunityHighlightSaga(ICommunityHighlightCapturer capturer, ILogger<CommunityHighlightSaga> logger)
    {
        _capturer = capturer;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        try
        {
            await _capturer.Capture(context.Message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            // A dropped community-feed row is survivable; a disrupted import is not.
            _logger.LogWarning(ex, "Community highlight capture failed for user {UserId} on {Mix}",
                context.Message.UserId, context.Message.Mix);
        }
    }
}
