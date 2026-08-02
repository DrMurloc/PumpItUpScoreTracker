using MediatR;
using ScoreTracker.PlayerProgress.Contracts.Commands;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

internal sealed class StorePlayerHighlightHandler : IRequestHandler<StorePlayerHighlightCommand, bool>
{
    private readonly IPlayerHighlightRepository _highlights;

    public StorePlayerHighlightHandler(IPlayerHighlightRepository highlights)
    {
        _highlights = highlights;
    }

    public Task<bool> Handle(StorePlayerHighlightCommand request, CancellationToken cancellationToken)
    {
        return _highlights.Add(request.EventId, request.UserId, request.Mix, request.OccurredAt,
            request.SessionId, request.Wins, cancellationToken);
    }
}
