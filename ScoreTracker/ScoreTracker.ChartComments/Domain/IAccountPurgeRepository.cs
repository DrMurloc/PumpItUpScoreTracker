namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Erases one account's comments. Hand-written rather than a <c>UserOwned</c> manifest, because
///     the manifest issues a blanket delete and deleting a root with replies orphans them — while
///     the row count the coverage test checks would still look exactly right.
/// </summary>
internal interface IAccountPurgeRepository
{
    /// <summary>
    ///     Returns the ids of every comment the purge touched — deleted outright or tombstoned —
    ///     so the consumer can tell the translation pipeline to drop what it holds for them.
    /// </summary>
    Task<IReadOnlyList<Guid>> DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
