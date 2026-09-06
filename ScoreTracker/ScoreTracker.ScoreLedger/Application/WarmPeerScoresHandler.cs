using MediatR;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Infrastructure;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class WarmPeerScoresHandler : IRequestHandler<WarmPeerScoresCommand>
{
    private readonly PeerScoreStore _store;

    public WarmPeerScoresHandler(PeerScoreStore store)
    {
        _store = store;
    }

    public async Task Handle(WarmPeerScoresCommand request, CancellationToken cancellationToken)
    {
        await _store.Warm(request.Mix, cancellationToken);
    }
}
