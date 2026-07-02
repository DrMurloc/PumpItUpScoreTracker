using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record AddDiscordChannelToCommunityCommand(Name? CommunityName, Guid? InviteCode, ulong ChannelId,
        bool SendScores, bool SendTitles, bool SendNewMembers) : IRequest
    {
    }
}
