using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record LeaveCommunityCommand(Name CommunityName, Guid? UserId = null) : IRequest
    {
    }
}
