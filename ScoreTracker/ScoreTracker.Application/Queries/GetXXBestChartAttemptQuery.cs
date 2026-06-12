using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetXXBestChartAttemptQuery
    (Guid ChartId) : IQuery<BestXXChartAttempt>
{
}
