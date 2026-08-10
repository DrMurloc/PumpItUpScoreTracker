using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The startup recovery pass (docs/design/import-restart-recovery.md §4). Runs once per
///     process start; there is no scheduled job behind it.
///     <para>
///         Reads its candidates from the Ledger — sessions whose derived work never ran — rather
///         than enumerating import runs by time. That order matters: the marker and the run's end
///         time live in different verticals, so a time-first query cannot filter on the marker and
///         would match every run ever completed, which is what an arbitrary "only the last N hours"
///         window would then exist to paper over. Unprocessed sessions are tiny by construction.
///     </para>
/// </summary>
internal sealed class RecoverInterruptedImportsConsumer : IConsumer<RecoverInterruptedImportsCommand>
{
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

    public async Task Consume(ConsumeContext<RecoverInterruptedImportsCommand> context)
    {
        var token = context.CancellationToken;
        var message = context.Message;
        var unprocessed = await _mediator.Send(new GetUnprocessedSessionsQuery(), token);
        if (unprocessed.Count == 0) return;

        var runs = (await _results.GetForSessions(unprocessed.Select(s => s.Id).ToArray(), token))
            .GroupBy(r => r.SessionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.StartedAt).First());

        var now = _dateTime.Now;
        var replayed = 0;
        var closed = 0;
        foreach (var session in unprocessed)
        {
            // No run behind it: a manual entry, a CSV upload or an API submission. Those never
            // mint an ImportResult, so there is nothing here that can say whether their batch has
            // had its chance yet, and they are deliberately out of scope.
            if (!runs.TryGetValue(session.Id, out var run)) continue;

            // Orphaned means "began before this process did", NOT "is older than the batch hold
            // window". At startup the accumulator is empty, so nothing from a previous process can
            // ever drain no matter how recently it ran — and the run this boot actually
            // interrupted is, at this moment, seconds old. An age-based guard skips precisely the
            // runs the restart just killed and nothing looks again until the next restart, which
            // is the bug this shape replaced (docs/design/import-restart-recovery.md §4.2).
            //
            // A run that started AFTER this boot is live: its batch is in memory with a real
            // deadline, and a future restart will recover it if one interrupts it.
            if (run.StartedAt >= message.BootedAt) continue;

            if (run.FinishedAt is null)
            {
                // Interrupted mid-scrape. There is no resuming it — the piugame session is gone
                // and the credential lives in the player's browser — so it is closed and the
                // player is told. It still gets replayed: the scores it DID save are in a real
                // session, and re-importing will not recover their derived work, because the
                // records already match and those charts never re-enter a batch.
                await _results.MarkInterrupted(run.Id, now, token);
                closed++;
            }

            var count = await _mediator.Send(new ReplaySessionCommand(session.UserId, session.Id), token);
            if (count > 0) replayed++;
        }

        // Ask before boxing for a line nobody may be listening to (CA1873).
        if ((replayed > 0 || closed > 0) && _logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Startup recovery: replayed {Replayed} interrupted session(s), closed {Closed} run(s) that never reported back",
                replayed, closed);
    }
}
