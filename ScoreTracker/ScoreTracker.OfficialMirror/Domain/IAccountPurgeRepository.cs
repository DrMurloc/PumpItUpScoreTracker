namespace ScoreTracker.OfficialMirror.Domain;

internal interface IAccountPurgeRepository
{
    /// <summary>
    ///     Unlinks a purged account from the mirrored official players — deliberately not a
    ///     delete. An OfficialPlayer row mirrors a public piugame leaderboard entry that exists
    ///     whether we do or not; removing it would corrupt the mirror. Only the link goes.
    /// </summary>
    Task UnlinkUser(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the rows this vertical genuinely owns about a person, as opposed to the
    ///     mirrored public data above. Today that is their import history — a record of when
    ///     they pressed a button on our site, which is ours and goes with them.
    /// </summary>
    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
