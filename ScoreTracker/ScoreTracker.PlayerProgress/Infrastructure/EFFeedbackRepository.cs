using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFFeedbackRepository : IFeedbackRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFFeedbackRepository(IDbContextFactory<ChartAttemptDbContext> factory, IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    private static string FeedbackCache(Guid userId)
    {
        return $"{nameof(EFFeedbackRepository)}_Feedback_{userId}";
    }

    public async Task SaveFeedback(Guid userId, SuggestionFeedbackRecord feedback,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<SuggestionFeedbackEntity>().AddAsync(new SuggestionFeedbackEntity
        {
            Id = Guid.NewGuid(),
            ChartId = feedback.ChartId,
            FeedbackCategory = feedback.FeedbackCategory,
            IsPositive = feedback.IsPositive,
            Notes = feedback.Notes,
            ShouldHide = feedback.ShouldHide,
            SuggestionCategory = feedback.SuggestionCategory,
            UserId = userId
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        _cache.Remove(FeedbackCache(userId));
    }

    public async Task<IEnumerable<SuggestionFeedbackRecord>> GetFeedback(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(FeedbackCache(userId), async cache =>
        {
            cache.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return await database.Set<SuggestionFeedbackEntity>().Where(f => f.UserId == userId)
                .Select(e => new SuggestionFeedbackRecord(e.SuggestionCategory, e.FeedbackCategory, e.Notes,
                    e.ShouldHide, e.IsPositive, e.ChartId))
                .ToArrayAsync(cancellationToken);
        });
    }
}
