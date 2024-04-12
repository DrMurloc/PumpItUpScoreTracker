using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record RemoveDiscordChannelFromCommunityCommand(Name CommunityName, ulong ChannelId) : IRequest
    {
    }
}
