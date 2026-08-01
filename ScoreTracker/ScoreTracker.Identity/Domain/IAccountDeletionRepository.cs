namespace ScoreTracker.Identity.Domain;

internal interface IAccountDeletionRepository
{
    Task Save(AccountDeletionRequest request, CancellationToken cancellationToken = default);

    /// <summary>The user's pending request, if any. Cancelled and purged rows are not pending.</summary>
    Task<AccountDeletionRequest?> GetPending(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Pending requests whose window has elapsed — the purge saga's second source.</summary>
    Task<IEnumerable<AccountDeletionRequest>> GetPurgeable(DateTimeOffset asOf,
        CancellationToken cancellationToken = default);
}
