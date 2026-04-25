using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed class GetXXCoOpBestAttemptsQuery : IRequest<IEnumerable<BestXXChartAttempt>>
{
}
