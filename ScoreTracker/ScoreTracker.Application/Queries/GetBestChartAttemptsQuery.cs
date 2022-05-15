using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetBestChartAttemptsQuery(Guid UserId) : IRequest<IEnumerable<BestChartAttempt>>
{
}