using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Catalog.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetIncludedRandomChartsQuery(RandomSettings Settings) : IQuery<IEnumerable<Chart>>
    {
    }
}
