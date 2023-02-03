using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetBestChartAttemptQuery
    (Guid ChartId) : IRequest<BestChartAttempt>
{
}