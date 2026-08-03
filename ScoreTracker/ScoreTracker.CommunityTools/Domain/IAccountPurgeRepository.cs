namespace ScoreTracker.CommunityTools.Domain;

internal interface IAccountPurgeRepository
{
    /// <summary>
    ///     Removes every trace of a purged account from this vertical: the tools they owned, the
    ///     shares and blocks they granted, and their sharing preference.
    /// </summary>
    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
