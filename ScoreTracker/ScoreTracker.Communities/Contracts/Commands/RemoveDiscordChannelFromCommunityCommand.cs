using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record RemoveDiscordChannelFromCommunityCommand(Name CommunityName, ulong ChannelId) : IRequest
    {
    }
}
