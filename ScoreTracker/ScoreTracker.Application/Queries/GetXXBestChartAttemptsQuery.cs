using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetXXBestChartAttemptsQuery(Guid UserId) : IRequest<IEnumerable<BestXXChartAttempt>>
{
}
