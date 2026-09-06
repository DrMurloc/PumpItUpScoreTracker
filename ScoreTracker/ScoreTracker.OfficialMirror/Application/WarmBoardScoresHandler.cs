using MediatR;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Infrastructure;

namespace ScoreTracker.OfficialMirror.Application;

internal sealed class WarmBoardScoresHandler : IRequestHandler<WarmBoardScoresCommand>
{
    private readonly BoardScoreStore _store;

    public WarmBoardScoresHandler(BoardScoreStore store)
    {
        _store = store;
    }

    public async Task Handle(WarmBoardScoresCommand request, CancellationToken cancellationToken)
    {
        await _store.Warm(request.Mix, cancellationToken);
    }
}
