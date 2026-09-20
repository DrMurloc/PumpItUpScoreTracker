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
        // A run that named no card sends blanks. Stored as absent, so the session reads as having
        // no tag rather than an empty one.
        await sessions.Open(id, request.UserId, request.Mix, request.Source, BlankAsAbsent(request.AccountTag),
            BlankAsAbsent(request.CardId), dateTime.Now, cancellationToken);
        return id;
    }

    private static string? BlankAsAbsent(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
