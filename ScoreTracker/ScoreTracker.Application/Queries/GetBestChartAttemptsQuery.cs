using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetBestChartAttemptsQuery : IRequest<IEnumerable<BestChartAttempt>>
{
}