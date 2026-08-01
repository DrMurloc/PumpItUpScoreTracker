using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;

namespace ScoreTracker.ScoreLedger.Infrastructure;

/// <summary>
///     The published port over ScoreLedger's own attempt-count query, so an upstream vertical
///     can ask without referencing this assembly. Deliberately a dispatch and nothing else —
///     the counting rule lives in one handler, and a second copy here would be the drift the
///     port is meant to avoid.
/// </summary>
internal sealed class SessionAttemptReader(IMediator mediator) : IScoreAttemptReader
{
    public Task<IReadOnlyDictionary<Guid, int>> GetSessionAttemptCounts(Guid userId, Guid sessionId,
        IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken)
    {
        return mediator.Send(new GetSessionAttemptCountsQuery(userId, sessionId, chartIds), cancellationToken);
    }
}
