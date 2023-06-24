using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetXXBestChartAttemptsQuery(Guid UserId) : IRequest<IEnumerable<BestXXChartAttempt>>
{
}