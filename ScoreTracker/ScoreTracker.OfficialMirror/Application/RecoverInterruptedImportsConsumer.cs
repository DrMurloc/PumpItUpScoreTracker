using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Import recovery, read from the import runs themselves (docs/design/import-restart-recovery.md §0).
///     An import announces the moment its last score saves, so a run that reported an ending has, in
///     every way but one, already announced: the startup pass catches the runs a restart cut short,
///     and the five-minute tick catches the one way a live process can lose an announcement — a run
///     that failed after saving, and then failed to announce what it saved.
///     <para>
///         Both replay through <see cref="ReplaySessionCommand" />, which does nothing for a session
///         already announced or already processed, so a run can be offered to it any number of times
///         and post at most one card.
///     </para>
/// </summary>
internal sealed class RecoverInterruptedImportsConsumer : IConsumer<RecoverInterruptedImportsCommand>,
    IConsumer<OverdueScoreBatchesFlushedEvent>
{
    /// <summary>
    ///     How far back either pass looks. A session older than this is not worth a card: a run that
    ///     never finished and started earlier is closed without one, and without a notice.
    /// </summary>
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(1);

    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILogger<RecoverInterruptedImportsConsumer> _logger;
    private readonly IMediator _mediator;
    private readonly IImportResultRepository _results;

    public RecoverInterruptedImportsConsumer(IMediator mediator, IImportResultRepository results,
        IDateTimeOffsetAccessor dateTime, ILogger<RecoverInterruptedImportsConsumer> logger)
    {
        _mediator = mediator;
        _results = results;
        _dateTime = dateTime;
        _logger = logger;
    }

    /// <summary>
    ///     The startup pass. Every run that began in the day before this process did is replayed —
    ///     newest first, so the player who just pressed the button hears first — and one that never
    ///     reported an ending is closed Interrupted after its replay, so the notice telling the player
    ///     their scores are already in is true by the time they can see it.
    ///     <para>
    ///         "Began before this process" and not "is old": at startup the accumulator is empty and
    ///         nothing from the previous process can still announce, while the run this boot actually
    ///         interrupted is seconds old. A run that started after the boot is live.
    ///     </para>
    /// </summary>
    public async Task Consume(ConsumeContext<RecoverInterruptedImportsCommand> context)
    {
        var bootedAt = context.Message.BootedAt;
        var token = context.CancellationToken;
        var now = _dateTime.Now;

        var abandoned = await _results.CloseAbandoned(bootedAt - Lookback, now, token);
        var runs = await _results.GetStartedBetween(bootedAt - Lookback, bootedAt, token);

        var replayed = 0;
        var closed = 0;
        var failed = 0;
        foreach (var run in runs)
            try
            {
                // Replayed even when the run finished: the first boot after this shape shipped follows
                // a process that still held finished imports in a two-minute batch, and for every other
                // finished run the replay finds the session announced and does nothing.
                if (run.SessionId is { } sessionId &&
                    await _mediator.Send(new ReplaySessionCommand(run.UserId, sessionId), token) > 0)
                    replayed++;
                if (run.FinishedAt is null)
                {
                    // There is no resuming it — the piugame session is gone and the credential lives
                    // in the player's browser — so it is closed and the player is told.
                    await _results.MarkInterrupted(run.Id, now, token);
                    closed++;
                }
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                // One run's failure must not cost every run after it; the log keeps the detail.
                failed++;
                _logger.LogError(e, "Startup recovery could not finish import run {RunId}", run.Id);
            }

        // Ask before boxing for a line nobody may be listening to (CA1873).
        if ((replayed > 0 || closed > 0 || abandoned > 0 || failed > 0) && _logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Startup recovery: replayed {Replayed} session(s), closed {Closed} run(s) that never reported back, closed {Abandoned} older than a day without notice, {Failed} failed",
                replayed, closed, abandoned, failed);
    }

    /// <summary>
    ///     The five-minute tick: replays the session of every run that saved and then ended in a
    ///     failure during the last day. Such a run announces what it saved on its way out, and this is
    ///     for when that announcement itself failed. Never closes a run — a run with no ending is
    ///     still working, or belongs to the startup pass.
    /// </summary>
    public async Task Consume(ConsumeContext<OverdueScoreBatchesFlushedEvent> context)
    {
        var token = context.CancellationToken;
        var runs = await _results.GetFailedSince(context.Message.FlushedAt - Lookback, token);

        var replayed = 0;
        foreach (var run in runs)
            try
            {
                if (await _mediator.Send(new ReplaySessionCommand(run.UserId, run.SessionId!.Value), token) > 0)
                    replayed++;
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                _logger.LogError(e, "Could not replay the session of failed import run {RunId}", run.Id);
            }

        if (replayed > 0 && _logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Replayed {Replayed} session(s) of import runs that failed after saving",
                replayed);
    }
}
