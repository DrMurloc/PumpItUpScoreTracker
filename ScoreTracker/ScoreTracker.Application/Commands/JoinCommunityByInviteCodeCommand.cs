using MediatR;

namespace ScoreTracker.Application.Commands;

public sealed record JoinCommunityByInviteCodeCommand(Guid InviteCode) : IRequest
{
}