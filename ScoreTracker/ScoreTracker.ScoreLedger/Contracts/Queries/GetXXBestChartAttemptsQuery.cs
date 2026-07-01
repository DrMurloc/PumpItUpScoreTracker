using ScoreTracker.Domain.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetXXBestChartAttemptsQuery(Guid UserId) : IQuery<IEnumerable<BestXXChartAttempt>>
{
}
