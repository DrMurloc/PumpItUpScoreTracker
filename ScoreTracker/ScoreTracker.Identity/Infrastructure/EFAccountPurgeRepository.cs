using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure.Entities;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    /// <summary>
    ///     Every table Identity keys to a user. These entities live in ScoreTracker.Data rather
    ///     than this vertical — they predate the split and still hang off the shared context's
    ///     DbSet properties — so AccountPurgeCoverageTests attributes Data's user-keyed types to
    ///     this manifest. UserDataPurge executes it.
    /// </summary>
    internal static readonly Type[] UserOwned =
    {
        typeof(UserApiTokenEntity),
        typeof(UserSettingsEntity),
        typeof(SavedChartEntity),
        typeof(ExternalLoginEntity),
        // The envelope key for a remembered piugame password. Missing it left the one piece
        // of a purged account's data that is actually a credential sitting in the table.
        typeof(UserImportCredentialKeyEntity)
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public Task DeleteIdentityData(Guid userId, CancellationToken cancellationToken = default)
    {
        return UserDataPurge.DeleteAll(_factory, UserOwned, userId, cancellationToken);
    }

    public async Task DeleteUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.User.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
