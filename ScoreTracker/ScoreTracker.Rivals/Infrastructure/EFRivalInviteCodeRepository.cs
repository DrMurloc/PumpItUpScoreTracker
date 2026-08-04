using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.Rivals.Infrastructure.Entities;

namespace ScoreTracker.Rivals.Infrastructure;

internal sealed class EFRivalInviteCodeRepository : IRivalInviteCodeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFRivalInviteCodeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<string?> GetCodeFor(Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalInviteCodeEntity>()
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserForCode(string code, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var owner = await database.Set<RivalInviteCodeEntity>()
            .AsNoTracking()
            .Where(c => c.Code == code)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        return owner;
    }

    public async Task<bool> TrySetCode(Guid userId, string code, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        // Codes are drawn at random, so a collision is rare rather than impossible. Reporting it
        // lets the caller draw again; silently reusing somebody else's code would hand a stranger
        // the wrong person's invite.
        var taken = await database.Set<RivalInviteCodeEntity>()
            .AnyAsync(c => c.Code == code && c.UserId != userId, cancellationToken);
        if (taken) return false;

        var existing = await database.Set<RivalInviteCodeEntity>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (existing == null)
        {
            await database.Set<RivalInviteCodeEntity>().AddAsync(new RivalInviteCodeEntity
            {
                UserId = userId,
                Code = code,
                CreatedAt = at
            }, cancellationToken);
        }
        else
        {
            existing.Code = code;
            existing.CreatedAt = at;
        }

        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
