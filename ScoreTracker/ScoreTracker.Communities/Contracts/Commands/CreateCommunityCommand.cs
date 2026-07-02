using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record CreateCommunityCommand(Name CommunityName, CommunityPrivacyType PrivacyType) : IRequest
    {
    }
}
