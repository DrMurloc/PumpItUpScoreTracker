using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class BeginScoreSessionHandler(IScoreSessionRepository sessions, IDateTimeOffsetAccessor dateTime)
    : IRequestHandler<BeginScoreSessionCommand, Guid>
{
    public async Task<Guid> Handle(BeginScoreSessionCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await sessions.Open(id, request.UserId, request.Mix, request.Source, request.AccountTag, request.CardId,
            dateTime.Now, cancellationToken);
        return id;
    }
}
