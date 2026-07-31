using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Deletes a chosen scope of a player's score data. <paramref name="Mix" /> null means every
///     mix; <paramref name="Items" /> chooses what goes.
///     This is the player-facing wipe, not the account purge — the purge deletes each vertical's
///     rows through its own consumer and deliberately does not come through here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WipeUserScoresCommand(Guid UserId, MixEnum? Mix, ScoreDeletionItems Items) : IRequest
{
}
