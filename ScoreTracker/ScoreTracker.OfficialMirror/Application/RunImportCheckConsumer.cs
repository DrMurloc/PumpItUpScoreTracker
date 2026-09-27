using System.Security.Authentication;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Messages;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.ScoreLedger.Contracts.Commands;

namespace ScoreTracker.OfficialMirror.Application;

// Runs the completeness check off the request circuit, mirroring RunOfficialImportConsumer —
// including the per-user slot it must release however the run ends, the ImportResult row it
// opens, and the session it points that row at. The check RUNS the standard import inside
// itself, so this is the only consumer that sees the whole attempt: one press, one row, whichever
// of the two buttons was pressed. A deep scan also holds one of the site-wide deep-scan slots
// for the whole run.
internal sealed class RunImportCheckConsumer : IConsumer<RunImportCheckCommand>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IImportConcurrencyGuard _guard;
    private readonly ILogger _logger;
    private readonly IMediator _mediator;
    private readonly IImportResultRepository _results;

    public RunImportCheckConsumer(IMediator mediator, ICurrentUserAccessor currentUser,
        IImportConcurrencyGuard guard, IImportResultRepository results, IDateTimeOffsetAccessor dateTime,
        ILogger<RunImportCheckConsumer> logger)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _guard = guard;
        _results = results;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunImportCheckCommand> context)
    {
        var message = context.Message;
        // The kind is recorded because the two cost wildly different amounts of the official
        // site — a deep scan walks every best-score page — so "deep scans fail" and "everything
        // fails" have to be countable apart.
        var kind = message.DeepScan ? ImportKind.DeepScan : ImportKind.Check;
        var resultId = await _results.Open(message.UserId, message.Mix, kind, message.CardId, _dateTime.Now,
            context.CancellationToken);

        var outcome = ImportOutcome.Completed;
        var reportIt = true;
        var deepScanSlot = false;
        int? saved = null;
        try
        {
            // A bus consumer has no HttpContext, so establish the job's user for this scope.
            // SetScopedUser (not SetCurrentUser) so we never issue a cookie — a request context
            // can flow into the consumer, and signing it out would drop the live user's session.
            var user = await _mediator.Send(new GetUserByIdQuery(message.UserId), context.CancellationToken);
            if (user != null) _currentUser.SetScopedUser(user);

            // Taken before the session opens, so a scan that loses the race for the site-wide slot
            // leaves no empty session row in the player's list.
            if (message.DeepScan)
            {
                deepScanSlot = _guard.TryBeginDeepScan();
                if (!deepScanSlot)
                {
                    await _mediator.Publish(new ImportStatusUpdatedEvent(message.UserId,
                            "Another deep scan is running — try again in a few minutes",
                            Array.Empty<RecordedPhoenixScore>(), message.Mix),
                        context.CancellationToken);
                    saved = 0;
                    return;
                }
            }

            // The run points at its session before the scrape starts: restart recovery finds a run
            // only through its session, so one cut off mid-scrape without it is never closed, and
            // the scores it had already saved are never replayed.
            var sessionId = await _mediator.Send(
                new BeginScoreSessionCommand(message.UserId, message.Mix, ScoreJournalEntry.OfficialImportSource,
                    message.ExpectedGameTag, message.CardId), context.CancellationToken);
            await _results.AttachSession(resultId, sessionId, context.CancellationToken);

            saved = await _mediator.Send(new ExecuteImportCheckCommand(message.UserId, message.Mix,
                message.Sid, message.CardId, message.ExpectedGameTag, message.DeepScan, message.IncludeBroken,
                sessionId), context.CancellationToken);
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
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // Shutdown, not a fault — the row stays open, which is the honest record of a run
            // that never reported back.
            reportIt = false;
        }
        catch (Exception exception)
        {
            outcome = ImportOutcomeClassifier.For(exception);
            _logger.LogError(exception, "Import check failed for {UserId} on {Mix} ({Kind}, {Outcome})",
                message.UserId, message.Mix, kind, outcome);
            await _mediator.Publish(new ImportStatusErrorEvent(message.UserId, ImportFailureMessage.For(outcome),
                message.Mix), context.CancellationToken);
        }
        finally
        {
            if (deepScanSlot) _guard.EndDeepScan();
            _guard.End(message.UserId);
            if (reportIt) await _results.Close(resultId, _dateTime.Now, outcome, saved, CancellationToken.None);
        }
    }
}
