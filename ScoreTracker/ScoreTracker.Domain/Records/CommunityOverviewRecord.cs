using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    /// <summary>
    ///     One of a player's communities, as the directory and the scope rails read it.
    ///     <para>
    ///         <paramref name="CommunityId" /> is required rather than optional on purpose: chart
    ///         comments store the id of the club they were posted to, and a default-valued Guid
    ///         would silently file a thread under nobody. The rest of the Communities contract
    ///         surface is name-keyed, which is fine for a command a person typed and wrong for a
    ///         foreign key that has to survive a rename.
    ///     </para>
    /// </summary>
    public sealed record CommunityOverviewRecord(Name CommunityName, CommunityPrivacyType PrivacyType, int MemberCount,
        bool IsRegional, Guid CommunityId)
    {
    }
}
