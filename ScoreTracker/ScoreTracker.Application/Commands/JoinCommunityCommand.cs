using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record JoinCommunityCommand(Name CommunityName, Guid? InviteCode, Guid? UserId = null) : IRequest
    {
    }
}
