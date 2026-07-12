using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetXXBestChartAttemptQuery
    (Guid ChartId, MixEnum Mix = MixEnum.XX) : IQuery<BestXXChartAttempt>
{
}
