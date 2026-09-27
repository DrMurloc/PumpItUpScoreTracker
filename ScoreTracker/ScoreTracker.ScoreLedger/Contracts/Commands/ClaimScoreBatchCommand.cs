using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Hands an official import the typed-entry batch the player has open on the mix, as save results the
///     run announces with its own (docs/design/import-restart-recovery.md §0).
///     <para>
///         Sent as the run begins. A score typed in just before then opened a two-minute batch that would
///         otherwise announce on its own timer in the middle of the run, and two captures beside each other
///         each read the other's charts as already held — one burst posting two cards, and a title the two
///         only cross together announced on both. Claimed, the batch is gone from the accumulator, so its
///         timer and the five-minute flush find nothing. Empty when nothing was open.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ClaimScoreBatchCommand(Guid UserId, MixEnum Mix) : IRequest<IReadOnlyList<ScoreSaveResult>>;
