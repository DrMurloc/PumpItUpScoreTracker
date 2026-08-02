using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.PlayerProgress.Contracts.Events;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Materializes the significant-wins ledger. A SECOND consumer of
///     <see cref="ScoreHighlightsCapturedEvent" /> beside the Discord card — it delegates the
///     classify-and-persist to the shared <see cref="IPlayerHighlightCapturer" /> (which the admin
///     backfill also drives). Failure-isolated: a feed write must never disturb the import
///     pipeline (same contract as the recap saga).
/// </summary>
internal sealed class PlayerHighlightSaga : IConsumer<ScoreHighlightsCapturedEvent>
{
    private readonly IPlayerHighlightCapturer _capturer;
    private readonly ILogger<PlayerHighlightSaga> _logger;

    public PlayerHighlightSaga(IPlayerHighlightCapturer capturer, ILogger<PlayerHighlightSaga> logger)
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
            // A dropped feed row is survivable; a disrupted import is not.
            _logger.LogWarning(ex, "Player highlight capture failed for user {UserId} on {Mix}",
                context.Message.UserId, context.Message.Mix);
        }
    }
}
