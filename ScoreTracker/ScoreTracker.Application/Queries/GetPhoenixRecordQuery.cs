using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPhoenixRecordQuery(Guid ChartId) : IRequest<RecordedPhoenixScore?>
{
}
