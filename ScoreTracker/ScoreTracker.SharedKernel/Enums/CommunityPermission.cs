using System;

namespace ScoreTracker.SharedKernel.Enums
{
    /// <summary>
    ///     The delegable capabilities an admin can hold in a community. The Creator implicitly
    ///     holds <see cref="All" />; these flags are only meaningful for admins. An admin with
    ///     <see cref="PromoteAdmins" /> may grant any subset of the permissions it itself holds
    ///     (the delegation rule).
    /// </summary>
    [Flags]
    public enum CommunityPermission
    {
        None = 0,
        ManageInviteLinks = 1 << 0,
        PromoteAdmins = 1 << 1,
        ManageUsers = 1 << 2,
        ManageChannelSubscriptions = 1 << 3,
        ModerateComments = 1 << 4,

        /// <summary>
        ///     Designate the community's Discord server and map titles to roles in it. Kept
        ///     separate from <see cref="ManageChannelSubscriptions" /> rather than folded into it:
        ///     a score feed and a role table are configured by different people in practice, and
        ///     this one can hand out standing in someone else's server.
        /// </summary>
        ManageDiscord = 1 << 5,
        All = ManageInviteLinks | PromoteAdmins | ManageUsers | ManageChannelSubscriptions |
              ModerateComments | ManageDiscord
    }
}
