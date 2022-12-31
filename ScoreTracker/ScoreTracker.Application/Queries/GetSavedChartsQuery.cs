using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries;

public sealed record GetSavedChartsQuery : IRequest<IEnumerable<SavedChartRecord>>
{
}