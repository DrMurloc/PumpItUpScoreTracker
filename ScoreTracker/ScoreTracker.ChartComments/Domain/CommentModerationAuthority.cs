using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     The moderation hierarchy as a pure function (owner, 2026-08-13): the creator moderates
///     admins and members; an admin holding <see cref="CommunityPermission.ModerateComments" />
///     moderates members only; admins never act on each other; nobody touches the creator.
///     <para>
///         The site admin sits outside the hierarchy and carries site tools only — removal reaches
///         everything everywhere, but a community mute is the club's own instrument, which is why
///         <see cref="MayMute" /> takes no site-admin flag at all.
///     </para>
/// </summary>
internal static class CommentModerationAuthority
{
    /// <summary>
    ///     Whether an actor may remove another player's comment. A null <paramref name="actorRole" />
    ///     or <paramref name="authorRole" /> means no membership row — for the author that is
    ///     someone who left the club, who is moderated like a member; for the actor it is a
    ///     non-member, who moderates nothing.
    /// </summary>
    public static bool MayRemove(bool actorIsSiteAdmin, CommunityRole? actorRole,
        CommunityPermission actorPermissions, CommunityRole? authorRole)
    {
        if (actorIsSiteAdmin) return true;

        return actorRole switch
        {
            CommunityRole.Creator => authorRole != CommunityRole.Creator,
            CommunityRole.Admin => actorPermissions.HasFlag(CommunityPermission.ModerateComments) &&
                                   IsMemberTier(authorRole),
            _ => false
        };
    }

    /// <summary>
    ///     Whether an actor may mute (or lift the mute of) a target in their community. No
    ///     site-admin parameter on purpose — the site admin's tools are removal and the account
    ///     lock, never a community mute. A null <paramref name="targetRole" /> is someone with no
    ///     membership row, and you cannot take the mic from someone who is not in the room.
    /// </summary>
    public static bool MayMute(CommunityRole? actorRole, CommunityPermission actorPermissions,
        CommunityRole? targetRole)
    {
        if (targetRole == null) return false;

        return actorRole switch
        {
            CommunityRole.Creator => targetRole != CommunityRole.Creator,
            CommunityRole.Admin => actorPermissions.HasFlag(CommunityPermission.ModerateComments) &&
                                   IsMemberTier(targetRole),
            _ => false
        };
    }

    /// <summary>Member, retained ban, or no row at all — everything below the admin tier.</summary>
    private static bool IsMemberTier(CommunityRole? role) =>
        role is null or CommunityRole.Member or CommunityRole.Banned;
}
