using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Domain;

/// <summary>
///     The communities the site creates and maintains itself: World, and the ninety-odd per-country
///     ones. Nobody joins them on purpose — an account is put in and taken out as its profile
///     changes — so nobody gets to delete them either.
///     <para>
///         ⚠ World is flagged <c>IsRegional = 0</c> in the database, so the regional flag alone does
///         not identify it; the codebase has always matched it by name, and so does this. Other
///         copies of that name live in <c>CommunitySaga</c> and <c>EFCommunitiesRepository</c>.
///     </para>
/// </summary>
internal static class SystemCommunities
{
    public const string World = "World";

    /// <summary>
    ///     Whether the site owns this community rather than a player. Ownerless until somebody is
    ///     named on the row — which is a legitimate thing to do, so that a person can configure
    ///     Discord roles for the official server, and is exactly why the deletion guard cannot
    ///     lean on "has no owner" the way it used to.
    /// </summary>
    public static bool IsSystem(Name communityName, bool isRegional) =>
        isRegional || string.Equals(communityName.ToString(), World, StringComparison.OrdinalIgnoreCase);
}
