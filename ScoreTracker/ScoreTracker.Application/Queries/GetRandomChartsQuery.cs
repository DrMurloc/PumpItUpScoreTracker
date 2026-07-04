using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomChartsQuery(RandomSettings Settings, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<Chart>>
    {
    }
}
