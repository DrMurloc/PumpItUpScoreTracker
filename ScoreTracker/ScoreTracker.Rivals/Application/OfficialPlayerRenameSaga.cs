using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.Rivals.Domain;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Follows an accepted rename (docs/design/rivals.md D5). The merge deleted the old dimension
///     row, so an edge still holding the old tag would resolve to nobody.
///     <para>
///         Usually the sweep's own decision rather than an admin's — a rename the evidence settles
///         merges unattended (docs/design/rename-matching.md). An UNDETECTED rename still leaves
///         the edge pointing at a tag the boards no longer carry, which the roster renders as
///         exactly that rather than hiding it (D6); there are simply far fewer of them now.
///     </para>
/// </summary>
internal sealed class OfficialPlayerRenameSaga : IConsumer<OfficialPlayerRenamedEvent>
{
    private readonly ILogger<OfficialPlayerRenameSaga> _logger;
    private readonly IRivalRepository _rivals;

    public OfficialPlayerRenameSaga(IRivalRepository rivals, ILogger<OfficialPlayerRenameSaga> logger)
    {
        _rivals = rivals;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OfficialPlayerRenamedEvent> context)
    {
        var message = context.Message;
        var renamed = await _rivals.RenameTag(message.OldTag, message.NewTag, context.CancellationToken);
        if (renamed > 0)
            _logger.LogInformation("Followed rename of {Old} to {New} across {Count} rival edges",
                message.OldTag, message.NewTag, renamed);
    }
}
