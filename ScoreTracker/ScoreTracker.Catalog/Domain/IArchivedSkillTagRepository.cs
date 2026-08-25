namespace ScoreTracker.Catalog.Domain;

/// <summary>
///     Read-only access to the archived hand tags. There is deliberately no write path: the
///     archive was written once, when the crawler took ownership of the live tags, and the
///     Chabala lens reads it as the historical record it is.
/// </summary>
internal interface IArchivedSkillTagRepository
{
    /// <summary>
    ///     Archived tags per chart, highlighted ones first then alphabetical. Charts the archive
    ///     has nothing for — everything added after the flip — are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetArchivedTags(IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken = default);
}
