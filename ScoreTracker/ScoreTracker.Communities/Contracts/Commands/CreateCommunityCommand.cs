using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateCommunityCommand(Name CommunityName, CommunityPrivacyType PrivacyType) : IRequest
    {
    }
}
