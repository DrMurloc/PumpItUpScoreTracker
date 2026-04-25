using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateInviteLinkCommand(Name CommunityName, DateOnly? ExpirationDate) : IRequest<Guid>
    {
    }
}
