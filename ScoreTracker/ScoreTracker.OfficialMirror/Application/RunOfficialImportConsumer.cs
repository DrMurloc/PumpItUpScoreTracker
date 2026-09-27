using System.Security.Authentication;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;

namespace ScoreTracker.OfficialMirror.Application;

// Runs every background import off the request circuit — the Import button, Import and check, and
// the deep scan are one run at three depths — and is the only place that learns how the run ended:
// one press, one ImportResult row, one session.
internal sealed class RunOfficialImportConsumer : IConsumer<RunOfficialImportCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IImportConcurrencyGuard _guard;
    private readonly ILogger _logger;
    private readonly IMediator _mediator;
    private readonly IImportResultRepository _results;

    public RunOfficialImportConsumer(IMediator mediator, ICurrentUserAccessor currentUser,
        IImportConcurrencyGuard guard, IImportResultRepository results, IDateTimeOffsetAccessor dateTime,
        ILogger<RunOfficialImportConsumer> logger)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _guard = guard;
        _results = results;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunOfficialImportCommand> context)
    {
        var message = context.Message;
        Guid? resultId = null;
        var outcome = ImportOutcome.Completed;
        var reportIt = true;
        var deepScanSlot = false;
        int? saved = null;
        try
        {
            // Inside the try, so a database that refuses the row still reaches the finally and hands
            // the player's slot back. The kind is recorded because the three cost wildly different
            // amounts of the official site — a deep scan walks every best-score page — so "deep scans
            // fail" and "everything fails" have to be countable apart.
            resultId = await _results.Open(message.UserId, message.Mix, message.Kind, message.CardId,
                _dateTime.Now, context.CancellationToken);

            // A bus consumer has no HttpContext, so establish the job's user for this scope; the
            // import's inner handlers (UI settings, game-profile writes) then resolve it as usual.
            // SetScopedUser (not SetCurrentUser) so we never issue a cookie — a request context can
            // flow into the consumer, and signing it out would drop the live user's session.
            var user = await _mediator.Send(new GetUserByIdQuery(message.UserId), context.CancellationToken);
            if (user != null) _currentUser.SetScopedUser(user);

            // Deep scans take a site-wide slot as well as the player's own, before the session opens,
            // so one that waits leaves no empty session row in the player's list. The refusal is an
            // error rather than a status: a status leaves the check panel spinning with nothing left
            // to stop it.
            if (message.Kind == ImportKind.DeepScan)
            {
                deepScanSlot = _guard.TryBeginDeepScan();
                if (!deepScanSlot)
                {
                    saved = 0;
                    await _mediator.Publish(new ImportStatusErrorEvent(message.UserId,
                            "Another deep scan is running — try again in a few minutes", message.Mix),
                        context.CancellationToken);
                    return;
                }
            }

            // The run begins here, so the mix's five-minute clock does too — for every kind, a deep scan
            // included, since it reads more of piugame than any other run. After the site-wide gate
            // above, so a scan turned away there starts nothing.
            _guard.Started(message.UserId, message.Mix, _dateTime.Now);

            // Opened here rather than inside the import body so this run can point at it before the
            // scrape starts.
            var sessionId = await _mediator.Send(
                new BeginScoreSessionCommand(message.UserId, message.Mix, ScoreJournalEntry.OfficialImportSource,
                    message.ExpectedGameTag, message.CardId), context.CancellationToken);
            await _results.AttachSession(resultId.Value, sessionId, context.CancellationToken);

            saved = await _mediator.Send(new ExecuteImportCommand(message.UserId, message.Mix, message.Sid,
                    message.CardId, message.ExpectedGameTag, message.IncludeBroken, sessionId, message.Kind),
                context.CancellationToken);
        }
        catch (InvalidCredentialException)
        {
            outcome = ImportOutcome.CredentialRejected;
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId, "Invalid Login Information", message.Mix),
                context.CancellationToken);
        }
        catch (NoGameAccountAssociatedException)
        {
            outcome = ImportOutcome.CredentialRejected;
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId,
                    "No game profile is associated with this account yet.", message.Mix),
                context.CancellationToken);
        }
        catch (Exception) when (context.CancellationToken.IsCancellationRequested)
        {
            // The process is going away, not a fault — whatever shape the cancellation surfaced in (a
            // cancelled SQL command is not an OperationCanceledException). The row is deliberately
            // left unfinished: that IS the "never reported back" state, and claiming an outcome here
            // would erase the one signal that says a deploy landed mid-import.
            reportIt = false;
        }
        catch (Exception exception)
        {
            // Nothing retries this and nothing consumes Fault<T>, so before today an exception
            // here evaporated: no log, no error queue that outlives the process, and no event —
            // which left the player's import pulse spinning forever with no message. The catch is
            // broad on purpose; the classifier decides whose fault it was and the log keeps the
            // detail that must never reach a player's screen.
            outcome = ImportOutcomeClassifier.For(exception);
            _logger.LogError(exception, "Import failed for {UserId} on {Mix} ({Kind}, {Outcome})", message.UserId,
                message.Mix, message.Kind, outcome);
            await _mediator.Publish(new ImportStatusErrorEvent(message.UserId, ImportFailureMessage.For(outcome),
                message.Mix), context.CancellationToken);
        }
        finally
        {
            // Free the slots the run took, whatever the outcome — the user can import again, and a
            // scan that died mid-walk must not hold the site-wide slot until the process restarts.
            if (deepScanSlot) _guard.EndDeepScan();
            _guard.End(message.UserId);
            if (reportIt && resultId is { } id)
                await _results.Close(id, _dateTime.Now, outcome, saved, CancellationToken.None);
        }
    }
}
