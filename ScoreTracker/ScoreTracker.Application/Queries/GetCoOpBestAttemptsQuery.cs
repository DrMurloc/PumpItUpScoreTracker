using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed class GetCoOpBestAttemptsQuery : IRequest<IEnumerable<BestChartAttempt>>
{
}