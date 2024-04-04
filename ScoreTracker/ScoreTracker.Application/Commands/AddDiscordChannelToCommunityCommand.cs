using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record AddDiscordChannelToCommunityCommand(Name? CommunityName, Guid? InviteCode, ulong ChannelId,
        bool SendScores, bool SendTitles, bool SendNewMembers) : IRequest
    {
    }
}
