using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Contracts.Messages;

namespace ScoreTracker.OfficialMirror.Application;

// Runs the import off the request circuit. A credential that fails at the site (wrong password,
// or an account with no game profile yet) surfaces as a status error event — the UI is already
// listening for those.
internal sealed class RunOfficialImportConsumer : IConsumer<RunOfficialImportCommand>
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IImportConcurrencyGuard _guard;

    public RunOfficialImportConsumer(IMediator mediator, ICurrentUserAccessor currentUser,
        IImportConcurrencyGuard guard)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _guard = guard;
    }

    public async Task Consume(ConsumeContext<RunOfficialImportCommand> context)
    {
        var message = context.Message;

        try
        {
            // A bus consumer has no HttpContext, so establish the job's user for this scope; the
            // import's inner handlers (UI settings, game-profile writes) then resolve it as usual.
            var user = await _mediator.Send(new GetUserByIdQuery(message.UserId), context.CancellationToken);
            if (user != null) await _currentUser.SetCurrentUser(user);

            await _mediator.Send(new ExecuteImportCommand(message.UserId, message.Mix, message.Sid, message.CardId,
                message.ExpectedGameTag, message.IncludeBroken, message.SyncPiuTracker), context.CancellationToken);
        }
        catch (InvalidCredentialException)
        {
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId, "Invalid Login Information", message.Mix),
                context.CancellationToken);
        }
        catch (NoGameAccountAssociatedException)
        {
            await _mediator.Publish(
                new ImportStatusErrorEvent(message.UserId,
                    "No game profile is associated with this account yet.", message.Mix),
                context.CancellationToken);
        }
        finally
        {
            // Free the slot the Start handler took, whatever the outcome — the user can import again.
            _guard.End(message.UserId);
        }
    }
}
