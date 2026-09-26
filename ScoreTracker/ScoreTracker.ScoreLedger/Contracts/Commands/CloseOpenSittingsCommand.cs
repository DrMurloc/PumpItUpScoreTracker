using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     The tool that posted a player's plays says their session is over: every sitting the player
///     still has open on the mix closes now and is announced the way a sitting whose quiet window ran
///     out is — replayed from the journal into the card, the highlights and the lamps
///     (docs/design/rise.md D25). A sitting already closed, or none at all, leaves nothing to do, so the
///     command is safe to repeat. The next play on the mix starts a new sitting.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CloseOpenSittingsCommand(Guid UserId, MixEnum Mix) : IRequest;
