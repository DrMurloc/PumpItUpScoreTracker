using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Commands;

/// <summary>
///     Plays a tool observed through the plays endpoint, recorded into sittings. Each play joins the
///     player's open sitting on the mix when it was played within the mix's
///     <see cref="ScoreBatchPolicy.SittingQuietWindow(MixEnum)" /> of that sitting's plays, or starts a
///     new one; a play that beats the record becomes the record, and every play lands in the journal
///     under its sitting. A sitting announces itself once: when the quiet window passes with nothing
///     arriving, or sooner when the tool closes it with <see cref="CloseOpenSittingsCommand" />
///     (docs/design/rise.md §12).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecordSittingPlaysCommand(
    Guid UserId,
    MixEnum Mix,
    string Source,
    IReadOnlyList<RecordObservedPlaysCommand.ObservedPlay> Plays,
    // Whether a break on a chart the player has never passed is seated as their best.
    bool RecordBrokenAsBest) : IRequest;
