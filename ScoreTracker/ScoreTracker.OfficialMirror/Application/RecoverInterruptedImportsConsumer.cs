using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Events;
using ScoreTracker.ScoreLedger.Contracts.Queries;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Import-side recovery, on two triggers that ask different questions of the same candidates
///     (docs/design/import-restart-recovery.md §4 and §4.3): the boot pass, once per process start,
///     for work a restart interrupted; and the five-minute sweep, for a drain that never happened
///     inside a process that is still running. Each gates on its own predicate — see the two
///     <c>Consume</c> overloads — and shares everything after it.
///     <para>
///         Reads its candidates from the Ledger — sessions whose derived work never ran — rather
///         than enumerating import runs by time. That order matters: the marker and the run's end
///         time live in different verticals, so a time-first query cannot filter on the marker and
///         would match every run ever completed, which is what an arbitrary "only the last N hours"
///         window would then exist to paper over. Unprocessed sessions are tiny by construction.
///     </para>
/// </summary>
internal sealed class RecoverInterruptedImportsConsumer : IConsumer<RecoverInterruptedImportsCommand>,
    IConsumer<OverdueScoreBatchesFlushedEvent>
{
    /// <summary>
    ///     Sessions a single pass will replay before leaving the rest to the next one. Every replay
    ///     drops a whole capture chain — rating recalc plus every title ladder — onto the bus, and
    ///     the failure this recovers from strands sessions site-wide, so an uncapped first tick
    ///     after a long outage would herd the entire site through at once.
    /// </summary>
    private const int MaxSessionsPerPass = 25;

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
    ///     The mid-life sweep: sessions whose run finished long enough ago that its batch has
    ///     demonstrably had its chance to drain and did not take it. Runs after the in-memory half,
    ///     never beside it — see <see cref="OverdueScoreBatchesFlushedEvent" />.
    ///     <para>
    ///         Staleness is the right test here and the boot instant is not, for the mirror image
    ///         of §3.0's reason: mid-life, <em>every</em> run started after the boot, so the boot
    ///         test would skip all of them.
    ///     </para>
    ///     <para>
    ///         Only runs that reported an ending are in scope. A run with no <c>FinishedAt</c> is
    ///         either still scraping — and a long deep scan legitimately outlives this window with
    ///         no batch open yet — or died with its process, which is the boot pass's candidate,
    ///         not this one's. So this half never closes a run, it only replays.
    ///     </para>
    /// </summary>
    public Task Consume(ConsumeContext<OverdueScoreBatchesFlushedEvent> context)
    {
        var staleBefore = context.Message.FlushedAt - ScoreBatchPolicy.StaleAfter;
        return Sweep(run => run.FinishedAt is { } finished && finished < staleBefore,
            closeUnfinished: false, "Stalled-batch", context.CancellationToken);
    }

    public Task Consume(ConsumeContext<RecoverInterruptedImportsCommand> context)
    {
        // Orphaned means "began before this process did", NOT "is older than the batch hold
        // window". At startup the accumulator is empty, so nothing from a previous process can
        // ever drain no matter how recently it ran — and the run this boot actually interrupted
        // is, at this moment, seconds old. An age-based guard skips precisely the runs the restart
        // just killed and nothing looks again until the next restart, which is the bug this shape
        // replaced (docs/design/import-restart-recovery.md §4.2).
        //
        // A run that started AFTER this boot is live: its batch is in memory with a real deadline,
        // and the mid-life sweep above will reach it once that deadline passes unmet.
        var bootedAt = context.Message.BootedAt;
        return Sweep(run => run.StartedAt < bootedAt, closeUnfinished: true, "Startup",
            context.CancellationToken);
    }

    private async Task Sweep(Func<ImportRunForSession, bool> isOrphaned, bool closeUnfinished,
        string pass, CancellationToken token)
    {
        var unprocessed = await _mediator.Send(new GetUnprocessedSessionsQuery(), token);
        if (unprocessed.Count == 0) return;

        // Oldest first, so a backlog drains in the order it accumulated rather than by whatever
        // the index hands back — and so the cap below sheds the newest, which the next tick
        // reaches soonest.
        var candidates = unprocessed.OrderBy(s => s.StartedAt).ToArray();
        var deferred = Math.Max(0, candidates.Length - MaxSessionsPerPass);
        if (deferred > 0) candidates = candidates.Take(MaxSessionsPerPass).ToArray();

        var runs = (await _results.GetForSessions(candidates.Select(s => s.Id).ToArray(), token))
            .GroupBy(r => r.SessionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.StartedAt).First());

        var now = _dateTime.Now;
        var replayed = 0;
        var closed = 0;
        foreach (var session in candidates)
        {
            // ⚠ The drain marker, and the reason a session can be unprocessed yet out of scope.
            // ProcessedAt is an END marker — the capture chain stamps it when it finishes — so on
            // its own it cannot tell "the batch never drained" from "the batch drained and capture
            // is still working". Touch and SetCounts both write these counts at the moment a batch
            // is announced, so a non-zero count means the announcement already happened and the
            // only thing outstanding is the chain that follows it. Replaying on top of that
            // publishes the same scores twice and posts a second card.
            if (session.NewCount > 0 || session.UpscoreCount > 0) continue;
            // No run behind it: a manual entry, a CSV upload or an API submission. Those never
            // mint an ImportResult, so there is nothing here that can say whether their batch has
            // had its chance yet, and they are deliberately out of scope.
            if (!runs.TryGetValue(session.Id, out var run)) continue;

            if (!isOrphaned(run)) continue;

            if (closeUnfinished && run.FinishedAt is null)
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
        if ((replayed > 0 || closed > 0 || deferred > 0) && _logger.IsEnabled(LogLevel.Information))
            // A pass that silently stops at the cap reads exactly like a pass that found nothing
            // left, so what was left behind is said out loud.
            _logger.LogInformation(
                "{Pass} recovery: replayed {Replayed} interrupted session(s), closed {Closed} run(s) that never reported back, deferred {Deferred} to the next pass",
                pass, replayed, closed, deferred);
    }
}
