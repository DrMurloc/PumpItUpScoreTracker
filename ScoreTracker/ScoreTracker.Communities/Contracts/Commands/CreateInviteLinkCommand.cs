using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateInviteLinkCommand(Name CommunityName, DateOnly? ExpirationDate) : IRequest<Guid>
    {
    }
}
