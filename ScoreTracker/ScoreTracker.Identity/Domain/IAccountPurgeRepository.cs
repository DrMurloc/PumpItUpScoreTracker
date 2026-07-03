namespace ScoreTracker.Identity.Domain;

internal interface IAccountPurgeRepository
{
    /// <summary>Deletes the identity-owned rows for a user: api tokens, settings, saved charts, leftover logins.</summary>
    Task DeleteIdentityData(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the User row itself — last, after every vertical has had its week of purge re-fires.</summary>
    Task DeleteUser(Guid userId, CancellationToken cancellationToken = default);
}
