using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record LeaveCommunityCommand(Name CommunityName, Guid? UserId = null) : IRequest
    {
    }
}
