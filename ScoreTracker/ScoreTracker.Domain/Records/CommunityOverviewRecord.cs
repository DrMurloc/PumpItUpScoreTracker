using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record CommunityOverviewRecord(Name CommunityName, CommunityPrivacyType PrivacyType, int MemberCount,
        bool IsRegional)
    {
    }
}
