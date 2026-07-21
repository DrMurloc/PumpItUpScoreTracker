using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Contracts.Commands
{
    /// <summary>
    ///     A resolved /piu slash-command invocation, dispatched by the bot host for the
    ///     Communities router to handle. Returns the reply the adapter follows up with.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record HandleBotInteractionCommand(BotInteraction Interaction) : IRequest<BotReply>
    {
    }
}
