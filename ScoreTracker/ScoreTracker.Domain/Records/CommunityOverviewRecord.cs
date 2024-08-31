using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CommunityOverviewRecord(Name CommunityName, CommunityPrivacyType PrivacyType, int MemberCount,
        bool IsRegional)
    {
    }
}
