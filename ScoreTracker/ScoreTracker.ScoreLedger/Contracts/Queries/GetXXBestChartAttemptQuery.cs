using ScoreTracker.Domain.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetXXBestChartAttemptQuery
    (Guid ChartId) : IQuery<BestXXChartAttempt>
{
}
