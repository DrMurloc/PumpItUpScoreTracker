using MassTransit;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Translations.Contracts.Messages;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Erases a purged account's comments, notes and votes. Idempotent — the purge event re-fires
///     daily for a week, and a second pass finds nothing left to key on.
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IAccountPurgeRepository purge)
    {
        _purge = purge;
    }

    public async Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        var touched = await _purge.DeleteAllForUser(context.Message.RetiredUserId, context.CancellationToken);

        // The pipeline holds this account's words too — queued texts and stored pivots. Words a
        // purge removes must not survive in a translation queue.
        if (touched.Count > 0)
            await context.Publish(new DiscardTranslationRequestsCommand(
                touched.Select(CommentSourceKeys.For).ToArray()), context.CancellationToken);
    }
}
