using MediatR;

namespace ScoreTracker.Application.Commands;

[ExcludeFromCodeCoverage]
public sealed record JoinCommunityByInviteCodeCommand(Guid InviteCode) : IRequest
{
}
