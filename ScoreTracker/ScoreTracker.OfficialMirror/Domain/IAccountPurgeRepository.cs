namespace ScoreTracker.OfficialMirror.Domain;

internal interface IAccountPurgeRepository
{
    /// <summary>
    ///     Unlinks a purged account from the mirrored official players — deliberately not a
    ///     delete. An OfficialPlayer row mirrors a public piugame leaderboard entry that exists
    ///     whether we do or not; removing it would corrupt the mirror. Only the link goes.
    /// </summary>
    Task UnlinkUser(Guid userId, CancellationToken cancellationToken = default);
}
