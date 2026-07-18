using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Infrastructure
{
    internal sealed class EFDiscordFeedSubscriptionRepository : IDiscordFeedSubscriptionRepository, IDiscordFeedReader
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFDiscordFeedSubscriptionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _factory = factory;
        }

        public async Task Register(ulong channelId, DiscordFeedKind kind, MixEnum mix,
            ulong? registeredByDiscordUserId, string? culture, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var kindName = kind.ToString();
            var mixValue = (int)mix;
            var existing = await database.Set<DiscordFeedSubscriptionEntity>().FirstOrDefaultAsync(
                s => s.ChannelId == channelId && s.FeedKind == kindName && s.Mix == mixValue, cancellationToken);
            if (existing != null)
            {
                // Re-registering the same feed updates the language it posts in.
                if (existing.Culture == culture) return;
                existing.Culture = culture;
                await database.SaveChangesAsync(cancellationToken);
                return;
            }

            await database.Set<DiscordFeedSubscriptionEntity>().AddAsync(new DiscordFeedSubscriptionEntity
            {
                Id = Guid.NewGuid(),
                ChannelId = channelId,
                FeedKind = kindName,
                Mix = mixValue,
                RegisteredByDiscordUserId = registeredByDiscordUserId,
                Culture = culture
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
                    Enum.Parse<DiscordFeedKind>(s.FeedKind), (MixEnum)s.Mix, s.Culture))
                .ToArray();
        }

        public async Task<IReadOnlyList<DiscordFeedChannel>> GetSubscribedChannels(DiscordFeedKind kind, MixEnum mix,
            CancellationToken cancellationToken) =>
            await GetSubscribedChannels(kind.ToString(), mix, cancellationToken);

        // IDiscordFeedReader (published): the feed-kind string is the DiscordFeedKind enum name,
        // which is exactly how it is stored, so producing verticals can read subscriptions
        // without referencing Communities.
        public async Task<IReadOnlyList<DiscordFeedChannel>> GetSubscribedChannels(string feedKind, MixEnum mix,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixValue = (int)mix;
            return await database.Set<DiscordFeedSubscriptionEntity>()
                .Where(s => s.FeedKind == feedKind && s.Mix == mixValue)
                .Select(s => new DiscordFeedChannel(s.ChannelId, s.Culture))
                .ToArrayAsync(cancellationToken);
        }
    }
}
