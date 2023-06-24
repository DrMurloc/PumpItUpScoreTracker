using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed class GetXXCoOpBestAttemptsQuery : IRequest<IEnumerable<BestXXChartAttempt>>
{
}