namespace ScoreTracker.ScoreLedger.Domain;

internal interface IAccountPurgeRepository
{
    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);
}
