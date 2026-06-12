using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

[ExcludeFromCodeCoverage]
public sealed record GetPhoenixRecordsQuery(Guid UserId) : IQuery<IEnumerable<RecordedPhoenixScore>>
{
}
