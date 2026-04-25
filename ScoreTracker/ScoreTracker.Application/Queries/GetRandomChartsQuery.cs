using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomChartsQuery(RandomSettings Settings) : IRequest<IEnumerable<Chart>>
    {
    }
}
