using ScoreTracker.Communities.Contracts;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Domain
{
    /// <summary>
    ///     Storage for channel subscriptions to the broadcast feeds (weekly charts, daily
    ///     step, official leaderboards). Registration is idempotent per (channel, feed, mix);
    ///     re-registering updates the stored culture (the language its posts render in).
    /// </summary>
    internal interface IDiscordFeedSubscriptionRepository
    {
        Task Register(ulong channelId, DiscordFeedKind kind, MixEnum mix, ulong? registeredByDiscordUserId,
            string? culture, CancellationToken cancellationToken);

        Task<bool> Unregister(ulong channelId, DiscordFeedKind kind, MixEnum mix, CancellationToken cancellationToken);

        Task<IReadOnlyList<DiscordFeedSubscriptionRecord>> GetForChannel(ulong channelId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<DiscordFeedChannel>> GetSubscribedChannels(DiscordFeedKind kind, MixEnum mix,
            CancellationToken cancellationToken);
    }
}
