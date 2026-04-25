using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartVideosQuery
    (IEnumerable<Guid>? ChartIds = null) : IRequest<IEnumerable<ChartVideoInformation>>
{
}
