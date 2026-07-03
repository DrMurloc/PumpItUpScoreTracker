using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     The purge half of the account-merge feature. Fired daily by Hangfire; for every merge
///     past its grace window it re-publishes AccountPurgeStartedEvent (each vertical deletes
///     its own rows, idempotently) and deletes the identity-owned data. The User row falls
///     last, a week after the purge began — that week of daily re-fires is what makes an
///     in-memory-bus crash mid-purge self-healing.
/// </summary>
internal sealed class AccountPurgeSaga : IConsumer<ProcessAccountPurgesCommand>
{
    private static readonly TimeSpan RefireWindow = TimeSpan.FromDays(7);

    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<AccountPurgeSaga> _logger;
    private readonly IMergeRequestRepository _merges;
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeSaga(IMergeRequestRepository merges, IAccountPurgeRepository purge,
        IDateTimeOffsetAccessor dateTime, ILogger<AccountPurgeSaga> logger)
    {
        _merges = merges;
        _purge = purge;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessAccountPurgesCommand> context)
    {
        var now = _dateTime.Now;
        foreach (var merge in await _merges.GetPurgeable(now, context.CancellationToken))
        {
            _logger.LogInformation("Purging retired account {RetiredUserId} (merge {MergeId})",
                merge.RetiredUserId, merge.Id);
            await context.Publish(new AccountPurgeStartedEvent(merge.RetiredUserId), context.CancellationToken);
            await _purge.DeleteIdentityData(merge.RetiredUserId, context.CancellationToken);

            if (now >= merge.PurgeAfter + RefireWindow)
            {
                await _purge.DeleteUser(merge.RetiredUserId, context.CancellationToken);
                await _merges.Save(merge with { State = MergeState.Purged, PurgedAt = now },
                    context.CancellationToken);
            }
            else if (merge.State == MergeState.Active)
            {
                await _merges.Save(merge with { State = MergeState.Purging, PurgedAt = merge.PurgedAt ?? now },
                    context.CancellationToken);
            }
        }
    }
}
