using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record AddDiscordChannelToCommunityCommand(Name? CommunityName, Guid? InviteCode, ulong ChannelId,
        bool SendScores, bool SendTitles, bool SendNewMembers) : IRequest
    {
    }
}
