using ScoreTracker.Communities.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Domain
{
    /// <summary>
    ///     Storage for channel subscriptions to the broadcast feeds (weekly charts, daily
    ///     step, official leaderboards). Registration is idempotent per (channel, feed, mix).
    /// </summary>
    internal interface IDiscordFeedSubscriptionRepository
    {
        Task Register(ulong channelId, DiscordFeedKind kind, MixEnum mix, ulong? registeredByDiscordUserId,
            CancellationToken cancellationToken);

        Task<bool> Unregister(ulong channelId, DiscordFeedKind kind, MixEnum mix, CancellationToken cancellationToken);

        Task<IReadOnlyList<DiscordFeedSubscriptionRecord>> GetForChannel(ulong channelId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<ulong>> GetSubscribedChannels(DiscordFeedKind kind, MixEnum mix,
            CancellationToken cancellationToken);
    }
}
