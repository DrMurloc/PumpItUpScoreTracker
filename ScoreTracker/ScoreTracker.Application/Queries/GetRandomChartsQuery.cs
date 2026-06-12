using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomChartsQuery(RandomSettings Settings) : IQuery<IEnumerable<Chart>>
    {
    }
}
