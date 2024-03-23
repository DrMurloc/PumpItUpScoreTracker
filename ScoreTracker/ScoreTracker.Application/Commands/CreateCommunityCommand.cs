using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Commands
{
    public sealed record CreateCommunityCommand(Name CommunityName, CommunityPrivacyType PrivacyType) : IRequest
    {
    }
}
