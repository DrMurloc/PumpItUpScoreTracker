using MassTransit;
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
        IConsumer<PlayerScoreDataDeletedEvent>
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
    }
}
