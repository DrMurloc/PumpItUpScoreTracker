namespace ScoreTracker.SharedKernel.Enums
{
    /// <summary>
    ///     A member's standing in a non-regional community. Creator is a single seat (transfer,
    ///     not co-ownership). Banned is a terminal state whose membership row is retained so a
    ///     lookup blocks rejoin. Regional/World communities are ownerless and carry no roles.
    /// </summary>
    public enum CommunityRole
    {
        Member,
        Admin,
        Creator,
        Banned
    }
}
