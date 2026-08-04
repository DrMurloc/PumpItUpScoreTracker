using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.Rivals.Domain;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The ghost-becomes-real step (docs/design/rivals.md D5). A board tag somebody stored as a
///     stand-in for a person now HAS that person, so the edge stops standing in and points at the
///     account.
///     <para>
///         Fires on every import, including re-links of a tag that already pointed here, so the
///         promote is a no-op when there is nothing left holding the tag.
///     </para>
/// </summary>
internal sealed class OfficialPlayerLinkSaga : IConsumer<OfficialPlayerLinkedEvent>
{
    private readonly ILogger<OfficialPlayerLinkSaga> _logger;
    private readonly IRivalRepository _rivals;

    public OfficialPlayerLinkSaga(IRivalRepository rivals, ILogger<OfficialPlayerLinkSaga> logger)
    {
        _rivals = rivals;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OfficialPlayerLinkedEvent> context)
    {
        var message = context.Message;
        var promoted = await _rivals.PromoteTagToUser(message.Tag, message.UserId, context.CancellationToken);
        if (promoted > 0)
            _logger.LogInformation("Promoted {Count} rival edges from tag {Tag} to user {UserId}", promoted,
                message.Tag, message.UserId);
    }
}
