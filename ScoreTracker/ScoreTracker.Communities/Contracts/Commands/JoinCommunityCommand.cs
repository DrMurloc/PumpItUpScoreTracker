using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record JoinCommunityCommand(Name CommunityName, Guid? InviteCode, Guid? UserId = null) : IRequest
    {
    }
}
