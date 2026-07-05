using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetIncludedRandomChartsQuery(RandomSettings Settings, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<Chart>>
    {
    }
}
