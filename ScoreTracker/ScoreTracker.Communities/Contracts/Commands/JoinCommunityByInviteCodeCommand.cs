using MediatR;

namespace ScoreTracker.Communities.Contracts.Commands;

[ExcludeFromCodeCoverage]
public sealed record JoinCommunityByInviteCodeCommand(Guid InviteCode) : IRequest
{
}
