using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetAllRandomSettingsQuery : IRequest<IEnumerable<(Name name, RandomSettings settings)>>
    {
    }
}
