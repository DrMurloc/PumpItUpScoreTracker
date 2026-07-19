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
        All = ManageInviteLinks | PromoteAdmins | ManageUsers | ManageChannelSubscriptions
    }
}
