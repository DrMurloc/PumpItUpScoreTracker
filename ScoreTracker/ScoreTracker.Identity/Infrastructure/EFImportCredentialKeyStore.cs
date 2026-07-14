using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure.Entities;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class EFImportCredentialKeyStore : IImportCredentialKeyStore
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFImportCredentialKeyStore(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(Guid keyId, Guid userId, byte[] wrappedDataKey, DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var set = database.Set<UserImportCredentialKeyEntity>();
        var entity = await set.SingleOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);
        if (entity == null)
        {
            entity = new UserImportCredentialKeyEntity { KeyId = keyId };
            await set.AddAsync(entity, cancellationToken);
        }

        entity.UserId = userId;
        entity.WrappedDataKey = wrappedDataKey;
        entity.CreatedAt = createdAt;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]?> GetWrappedKey(Guid keyId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<UserImportCredentialKeyEntity>()
            .SingleOrDefaultAsync(k => k.KeyId == keyId && k.UserId == userId, cancellationToken);
        return entity?.WrappedDataKey;
    }

    public async Task Delete(Guid keyId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<UserImportCredentialKeyEntity>()
            .Where(k => k.KeyId == keyId && k.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<UserImportCredentialKeyEntity>()
            .Where(k => k.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<UserImportCredentialKeyEntity>().ExecuteDeleteAsync(cancellationToken);
    }
}
