namespace ScoreTracker.Identity.Domain;

internal interface IMergeRequestRepository
{
    Task Save(MergeRequest merge, CancellationToken cancellationToken = default);
    Task<MergeRequest?> Get(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<MergeRequest>> GetActiveInvolving(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<MergeRequest>> GetPurgeable(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}
