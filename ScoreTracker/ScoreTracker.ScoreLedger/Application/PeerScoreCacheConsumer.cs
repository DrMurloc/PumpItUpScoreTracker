using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.ScoreLedger.Infrastructure;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Drops a player's held scores when their own move, so everyone who has them as a peer sees
///     the import (docs/design/pumbility-overhaul.md §6.14).
///     <para>
///         The same pair of events the Pumbility projection cache rides, for the same reason and in
///         both directions: an import adds a score a peer group should count, and a deletion takes
///         one away. The player's slice is rebuilt from SQL the next time anybody asks for them.
///     </para>
/// </summary>
internal sealed class PeerScoreCacheConsumer :
    IConsumer<PlayerScoresUpdatedEvent>,
    IConsumer<PlayerScoreDataDeletedEvent>
{
    private readonly PeerScoreStore _store;

    public PeerScoreCacheConsumer(PeerScoreStore store)
    {
        _store = store;
    }

    public Task Consume(ConsumeContext<PlayerScoreDataDeletedEvent> context)
    {
        // A null mix is an every-mix wipe, which Evict already reads as "all of them".
        _store.Evict(context.Message.UserId, context.Message.Mix);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        _store.Evict(context.Message.UserId, context.Message.Mix);
        return Task.CompletedTask;
    }
}
