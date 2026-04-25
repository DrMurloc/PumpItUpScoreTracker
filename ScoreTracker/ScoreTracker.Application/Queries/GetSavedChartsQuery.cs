using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetSavedChartsQuery : IRequest<IEnumerable<SavedChartRecord>>
{
}
