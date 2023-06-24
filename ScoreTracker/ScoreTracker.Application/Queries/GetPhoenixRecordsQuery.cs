using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries;

public sealed record GetPhoenixRecordsQuery(Guid UserId) : IRequest<IEnumerable<RecordedPhoenixScore>>
{
}