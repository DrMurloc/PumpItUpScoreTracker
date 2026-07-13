using MediatR;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Rebuilds the community feed from recent captured highlights (CH7 backfill). Pulls reconstructed
///     events from PlayerProgress (published query — no SQL onto its tables) and runs each through the
///     shared capturer, exactly as the live consumer would. Idempotent: re-running writes nothing new.
/// </summary>
internal sealed class BackfillCommunityHighlightsHandler : IRequestHandler<BackfillCommunityHighlightsCommand, int>
{
    private readonly ICommunityHighlightCapturer _capturer;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMediator _mediator;

    public BackfillCommunityHighlightsHandler(IMediator mediator, ICommunityHighlightCapturer capturer,
        IDateTimeOffsetAccessor dateTime)
    {
        _mediator = mediator;
        _capturer = capturer;
        _dateTime = dateTime;
    }

    public async Task<int> Handle(BackfillCommunityHighlightsCommand request, CancellationToken cancellationToken)
    {
        var since = _dateTime.Now.AddDays(-Math.Abs(request.Days));
        var events = (await _mediator.Send(new GetRecentHighlightEventsQuery(since), cancellationToken)).ToArray();
        foreach (var reconstructed in events)
            await _capturer.Capture(reconstructed, cancellationToken);
        return events.Length;
    }
}
