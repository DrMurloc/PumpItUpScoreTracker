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

// Runs the import off the request circuit, and is the only place that learns how the run ended.
// Sits one level above where the three import paths diverge, which is why the ImportResult row
// is opened here rather than inside the import body: the completeness check RUNS that body, so a
// row minted down there would give every check two.
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
        var resultId = await _results.Open(message.UserId, message.Mix, ImportKind.Standard, message.CardId,
            _dateTime.Now, context.CancellationToken);

        var outcome = ImportOutcome.Completed;
        var reportIt = true;
        try
        {
            // A bus consumer has no HttpContext, so establish the job's user for this scope; the
            // import's inner handlers (UI settings, game-profile writes) then resolve it as usual.
            // SetScopedUser (not SetCurrentUser) so we never issue a cookie — a request context can
            // flow into the consumer, and signing it out would drop the live user's session.
            var user = await _mediator.Send(new GetUserByIdQuery(message.UserId), context.CancellationToken);
            if (user != null) _currentUser.SetScopedUser(user);

            // Opened here rather than inside the import body so this run can point at it. The
            // check path already worked this way; the body takes a session when handed one and
            // mints its own otherwise, so the synchronous API path is unaffected.
            var sessionId = await _mediator.Send(
                new BeginScoreSessionCommand(message.UserId, message.Mix, ScoreJournalEntry.OfficialImportSource,
                    message.ExpectedGameTag, message.CardId), context.CancellationToken);
            await _results.AttachSession(resultId, sessionId, context.CancellationToken);

            await _mediator.Send(new ExecuteImportCommand(message.UserId, message.Mix, message.Sid, message.CardId,
                message.ExpectedGameTag, message.IncludeBroken, sessionId), context.CancellationToken);
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
            // The process is going away, not a fault. The row is deliberately left unfinished:
            // that IS the "never reported back" state, and claiming an outcome here would erase
            // the one signal that says a deploy landed mid-import.
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
            _logger.LogError(exception, "Import failed for {UserId} on {Mix} ({Outcome})", message.UserId,
                message.Mix, outcome);
            await _mediator.Publish(new ImportStatusErrorEvent(message.UserId, ImportFailureMessage.For(outcome),
                message.Mix), context.CancellationToken);
        }
        finally
        {
            // Free the slot the Start handler took, whatever the outcome — the user can import again.
            _guard.End(message.UserId);
            if (reportIt) await _results.Close(resultId, _dateTime.Now, outcome, CancellationToken.None);
        }
    }
}
