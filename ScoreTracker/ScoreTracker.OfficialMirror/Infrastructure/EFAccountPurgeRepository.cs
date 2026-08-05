using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UnlinkUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        // Supplemented placements are this account's own scores, republished on a public
        // board. Deleting the account has to take them: what stays behind is what piugame
        // published, which was never ours to withdraw.
        //
        // ⚠ The four-way account-purge ratchet cannot see this. Placements key on PlayerId,
        // not UserId, so no manifest can name them and nothing fails if it is dropped —
        // AccountPurgeSupplementedTests in Tests.Integration is the only thing standing here.
        var mine = database.Set<OfficialPlayerEntity>()
            .Where(p => p.UserId == userId)
            .Select(p => p.Id);
        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.IsSupplemented && mine.Contains(p.PlayerId))
            .ExecuteDeleteAsync(cancellationToken);

        await database.Set<OfficialPlayerEntity>()
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.UserId, (Guid?)null)
                .SetProperty(p => p.UserIdSource, "None"), cancellationToken);
    }
}
