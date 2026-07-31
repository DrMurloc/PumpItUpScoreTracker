using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Deletes the Ledger's rows for a purged account: best attempts across every mix, the XX
///     legacy table, per-score stats, and the journal. Idempotent — the purge event re-fires
///     daily for a week.
///     It deliberately does not route through WipeUserScoresCommand any more. That command is
///     the player-facing wipe: it recomputes derived state and announces itself on the bus,
///     both pointless for an account being deleted, and it reached across into tables that
///     PlayerProgress owns — covering two mixes and silently leaving the rest. Each vertical
///     deletes its own now.
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IAccountPurgeRepository purge)
    {
        _purge = purge;
    }

    public Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        return _purge.DeleteAllForUser(context.Message.RetiredUserId, context.CancellationToken);
    }
}
