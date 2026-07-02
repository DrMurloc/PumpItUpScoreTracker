using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record RemoveDiscordChannelFromCommunityCommand(Name CommunityName, ulong ChannelId) : IRequest
    {
    }
}
