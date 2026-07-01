using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetCommunityQuery(Name CommunityName) : IQuery<Community>

    {
    }
}
