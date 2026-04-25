using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetIncludedRandomChartsQuery(RandomSettings Settings) : IRequest<IEnumerable<Chart>>
    {
    }
}
