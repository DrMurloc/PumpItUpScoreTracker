using System.Security.Authentication;
using MassTransit;
using MediatR;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.OfficialMirror.Contracts.Messages;

namespace ScoreTracker.OfficialMirror.Application;

// Runs the import off the request circuit. A credential that fails at the site (wrong password,
// or an account with no game profile yet) surfaces as a status error event — the UI is already
// listening for those.
internal sealed class RunOfficialImportConsumer : IConsumer<RunOfficialImportCommand>
{
    private readonly IMediator _mediator;

    public RunOfficialImportConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<RunOfficialImportCommand> context)
    {
        var message = context.Message;
        try
        {
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
    }
}
