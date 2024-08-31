using MediatR;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record LeaveCommunityCommand(Name CommunityName, Guid? UserId = null) : IRequest
    {
    }
}
