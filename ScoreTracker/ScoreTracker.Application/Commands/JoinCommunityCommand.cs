using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record JoinCommunityCommand(Name CommunityName, Guid? InviteCode) : IRequest
    {
    }
}
