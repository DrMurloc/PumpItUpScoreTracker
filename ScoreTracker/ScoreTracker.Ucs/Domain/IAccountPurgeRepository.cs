namespace ScoreTracker.Ucs.Domain;

internal interface IAccountPurgeRepository
{
    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
