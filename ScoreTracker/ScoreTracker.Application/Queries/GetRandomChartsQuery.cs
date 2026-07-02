using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomChartsQuery(RandomSettings Settings) : IQuery<IEnumerable<Chart>>
    {
    }
}
