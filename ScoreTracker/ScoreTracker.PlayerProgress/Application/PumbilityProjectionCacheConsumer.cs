using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.PlayerProgress.Application
{
    /// <summary>
    ///     Drops a player's cached Pumbility projection when their own scores move.
    ///     <para>
    ///         Both directions matter: an import can add a chart worth suggesting, and a
    ///         deletion can take one away — a projection that still recommends a chart the
    ///         player just cleared reads as the page being broken rather than stale.
    ///     </para>
    /// </summary>
    internal sealed class PumbilityProjectionCacheConsumer :
        IConsumer<PlayerScoresUpdatedEvent>,
        IConsumer<PlayerScoreDataDeletedEvent>,
        INotificationHandler<PlayerStatsUpdatedEvent>
    {
        private readonly PumbilityProjectionCache _cache;

        public PumbilityProjectionCacheConsumer(PumbilityProjectionCache cache)
        {
            _cache = cache;
        }

        public Task Consume(ConsumeContext<PlayerScoreDataDeletedEvent> context)
        {
            // A null mix is an every-mix wipe, which Evict already reads as "all of them".
            _cache.Evict(context.Message.UserId, context.Message.Mix);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
        {
            _cache.Evict(context.Message.UserId, context.Message.Mix);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Again once the recalculated stats are saved. The projection reads the player's competitive
        ///     level from those stats, and the score event lands first — an import announces the moment it
        ///     has saved, and a page loaded in the seconds before capture writes the stats would otherwise
        ///     cache a projection priced on the old level (a first import's is empty) for a day.
        /// </summary>
        public Task Handle(PlayerStatsUpdatedEvent notification, CancellationToken cancellationToken)
        {
            _cache.Evict(notification.UserId, notification.Mix);
            return Task.CompletedTask;
        }
    }
}
