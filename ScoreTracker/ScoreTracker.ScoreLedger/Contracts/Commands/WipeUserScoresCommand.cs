using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Deletes a chosen scope of a player's score data: the mixes they picked, and the items
///     they picked within each.
///     <paramref name="Mixes" /> is always explicit — there is no "null means everything", which
///     is the kind of default that deletes an account's entire history when a caller forgets to
///     set it.
///     This is the player-facing wipe, not the account purge — the purge deletes each vertical's
///     rows through its own consumer and deliberately does not come through here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record WipeUserScoresCommand(
    Guid UserId,
    IReadOnlyCollection<MixEnum> Mixes,
    ScoreDeletionItems Items) : IRequest
{
}
