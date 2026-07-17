using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Infrastructure
{
    internal sealed class EFDiscordFeedSubscriptionRepository : IDiscordFeedSubscriptionRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFDiscordFeedSubscriptionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _factory = factory;
        }

        public async Task Register(ulong channelId, DiscordFeedKind kind, MixEnum mix,
            ulong? registeredByDiscordUserId, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var kindName = kind.ToString();
            var mixValue = (int)mix;
            var exists = await database.Set<DiscordFeedSubscriptionEntity>().AnyAsync(
                s => s.ChannelId == channelId && s.FeedKind == kindName && s.Mix == mixValue, cancellationToken);
            if (exists) return;

            await database.Set<DiscordFeedSubscriptionEntity>().AddAsync(new DiscordFeedSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                FeedKind = kindName,
                Mix = mixValue,
                RegisteredByDiscordUserId = registeredByDiscordUserId
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> Unregister(ulong channelId, DiscordFeedKind kind, MixEnum mix,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var kindName = kind.ToString();
            var mixValue = (int)mix;
            var matches = await database.Set<DiscordFeedSubscriptionEntity>()
                .Where(s => s.ChannelId == channelId && s.FeedKind == kindName && s.Mix == mixValue)
                .ToArrayAsync(cancellationToken);
            if (matches.Length == 0) return false;

            database.Set<DiscordFeedSubscriptionEntity>().RemoveRange(matches);
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<DiscordFeedSubscriptionRecord>> GetForChannel(ulong channelId,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<DiscordFeedSubscriptionEntity>()
                .Where(s => s.ChannelId == channelId)
                .ToArrayAsync(cancellationToken);
            return rows
                .Select(s => new DiscordFeedSubscriptionRecord(s.ChannelId,
                    Enum.Parse<DiscordFeedKind>(s.FeedKind), (MixEnum)s.Mix))
                .ToArray();
        }

        public async Task<IReadOnlyList<ulong>> GetSubscribedChannels(DiscordFeedKind kind, MixEnum mix,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var kindName = kind.ToString();
            var mixValue = (int)mix;
            return await database.Set<DiscordFeedSubscriptionEntity>()
                .Where(s => s.FeedKind == kindName && s.Mix == mixValue)
                .Select(s => s.ChannelId)
                .ToArrayAsync(cancellationToken);
        }
    }
}
