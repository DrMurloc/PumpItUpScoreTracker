using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Deletes the ledger's rows for a purged account: the existing Delete-All-Scores flow
///     covers best attempts, stats, titles, and history; the journal and per-score stats are
///     purge-only deletes. Idempotent — the purge event re-fires daily for a week.
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IMediator _mediator;
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IMediator mediator, IAccountPurgeRepository purge)
    {
        _mediator = mediator;
        _purge = purge;
    }

    public async Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        await _mediator.Send(new WipeUserScoresCommand(context.Message.RetiredUserId, true),
            context.CancellationToken);
        await _purge.DeleteAllForUser(context.Message.RetiredUserId, context.CancellationToken);
    }
}
