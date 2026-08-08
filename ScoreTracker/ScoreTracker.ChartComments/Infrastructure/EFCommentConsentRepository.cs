using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentConsentRepository : ICommentConsentRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentConsentRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<CommentConsent?> GetFor(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentConsentEntity>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        return entity == null
            ? null
            : new CommentConsent(entity.AgreedToTermsAt, entity.TermsVersion,
                entity.ConsentedToPublicIdentityAt);
    }

    public async Task Record(Guid userId, int termsVersion, bool consentedToPublicIdentity,
        DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentConsentEntity>()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (entity == null)
        {
            entity = new CommentConsentEntity { Id = Guid.NewGuid(), UserId = userId, AgreedToTermsAt = at };
            await database.Set<CommentConsentEntity>().AddAsync(entity, cancellationToken);
        }

        // The terms stamp moves forward when the version does, which is what makes editing the
        // rules re-prompt instead of silently reinterpreting an old yes.
        if (entity.TermsVersion != termsVersion)
        {
            entity.TermsVersion = termsVersion;
            entity.AgreedToTermsAt = at;
        }

        // Never unset. The public-identity consent is a thing that happened, not a current setting.
        if (consentedToPublicIdentity) entity.ConsentedToPublicIdentityAt ??= at;

        await database.SaveChangesAsync(cancellationToken);
    }
}
