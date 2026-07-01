using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateInviteLinkCommand(Name CommunityName, DateOnly? ExpirationDate) : IRequest<Guid>
    {
    }
}
