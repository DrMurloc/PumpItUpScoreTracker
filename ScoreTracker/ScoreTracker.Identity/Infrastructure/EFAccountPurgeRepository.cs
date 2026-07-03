using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteIdentityData(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.UserApiToken.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.UserSettings.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.SavedChart.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.ExternalLogin.Where(l => l.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.User.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
