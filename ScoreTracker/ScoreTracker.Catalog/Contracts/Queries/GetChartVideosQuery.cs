using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Contracts.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetChartVideosQuery
    (IEnumerable<Guid>? ChartIds = null) : IQuery<IEnumerable<ChartVideoInformation>>
{
}
